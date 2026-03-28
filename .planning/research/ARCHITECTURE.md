# Architecture Patterns for LangThree

**Domain:** Programming Language Interpreter (ML-style functional language)
**Researched:** 2026-03-28 (updated for expression-sequencing milestone)
**Confidence:** HIGH

---

## Scope of This Document

This document covers the integration architecture for three features being added to the existing
LangThree v4.0+ interpreter:

1. **Newline implicit sequencing** — emit SEMICOLON-like behavior on newlines at statement level
2. **For-in collection loops** — `for x in collection do body` iterating over lists/arrays/ranges
3. **Option/Result Prelude utilities** — additional combinators in `Prelude/Option.fun` and `Prelude/Result.fun`

The underlying pipeline is unchanged:

```
Source text
    ↓
[Lexer.fsl]  →  raw tokens (NEWLINE carries column position)
    ↓
[IndentFilter.fs]  →  filtered token stream (INDENT/DEDENT + implicit SEMICOLON)
    ↓
[Parser.fsy]  →  Ast.Module
    ↓
[Elaborate.fs]  →  desugared Ast.Module
    ↓
[Bidir.fs / TypeCheck.fs]  →  type-checked module
    ↓
[Eval.fs]  →  Value
```

---

## Feature 1: Newline Implicit Sequencing

### What the feature does

Today, sequencing requires an explicit semicolon: `e1; e2`. The goal is to allow:

```
e1
e2
```

...at statement positions to desugar to the same `LetPat(WildcardPat, e1, e2)` that `e1; e2` produces.

### Where the work lives

**IndentFilter.fs is the only file that changes.** The parser already accepts `SeqExpr` via the
`SEMICOLON` token. Implicit sequencing means emitting a synthetic SEMICOLON token when a newline
at statement level separates two expressions.

### Existing mechanism to build on

The `filter` function in `IndentFilter.fs` already:

- Tracks `BracketDepth` and suppresses all NEWLINE processing inside `[]`, `()`, `{}`
- Tracks `PrevToken` for detecting when to enter `InFunctionApp` context
- Emits `INDENT`/`DEDENT` based on column changes
- Has `InExprBlock of baseColumn` in `SyntaxContext` — pushed when `INDENT` follows `=`, `->`, `in`, or `do`
- Has `InModule` context where no implicit IN is generated

The key insight: `InExprBlock` already identifies statement-level positions. A newline at the same
indent level inside an `InExprBlock` with no INDENT/DEDENT emitted is a candidate for implicit SEMICOLON.

### Integration design

**Where to inject:** In the `filter` function, in the NEWLINE branch, after `processNewlineWithContext`
returns with an empty `emitted` list (same-level, no INDENT/DEDENT):

```
if isAtSameLevel && emitted = [] then
    // existing offside IN-insertion check ...
    // NEW: check if inside InExprBlock and next token is a statement-starting token
    // → emit SEMICOLON
```

**Context check:** Only emit implicit SEMICOLON when:
- Context stack top is `InExprBlock` (we are in a block body: let RHS, lambda body, do body, etc.)
- No INDENT/DEDENT was emitted (same column — this is a peer statement, not a deeper block)
- No pending offside IN to emit (existing offside logic takes priority)
- Next token is not `IN`, `ELSE`, `WITH`, `|`, or `DEDENT` (those tokens close the current context;
  emitting SEMICOLON before them would create a dangling expression)
- `BracketDepth = 0` (already guaranteed by the outer NEWLINE handler)

**What "statement-starting token" means:** Rather than a positive allowlist, the safer formulation
is a negative blocklist of tokens that should NOT receive a preceding SEMICOLON:
`IN`, `ELSE`, `WITH`, `|` (PIPE), `THEN`, `AND_KW`, `DEDENT`, `EOF`.

**PrevToken constraint:** Do not emit if the previous non-whitespace token already ends a non-expression
context (e.g., a `TYPE` declaration, an `EXCEPTION` declaration). Since those currently exist only at
module level (not inside `InExprBlock`), this is less of a concern in practice, but should be tracked.

### Parser impact

None. `SeqExpr` already handles `Expr SEMICOLON SeqExpr` and `Expr SEMICOLON`. Adding an implicit
SEMICOLON from IndentFilter produces exactly the same token stream the parser already processes.

### Bidir / Eval impact

None. Implicit sequencing desugars to `LetPat(WildcardPat, e1, e2)`, which is already typed and
evaluated correctly.

### Component boundary summary

| Component | Change | Rationale |
|-----------|--------|-----------|
| IndentFilter.fs | Emit SEMICOLON in NEWLINE handler when context = InExprBlock and same indent level | Only place that knows layout context and column |
| Parser.fsy | None | SeqExpr already handles SEMICOLON |
| Ast.fs | None | LetPat(WildcardPat) is the existing desugar target |
| Bidir.fs | None | LetPat already typed |
| Eval.fs | None | LetPat already evaluated |

### Critical integration point: DO token

The `DO` token already triggers `InExprBlock` push (line 317 of IndentFilter.fs):

```fsharp
| Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN | Some Parser.DO ->
    state <- { state with Context = InExprBlock(baseCol) :: state.Context }
```

This means `while cond do\n  e1\n  e2` already enters `InExprBlock` after the `do`'s INDENT. The
implicit SEMICOLON will fire between `e1` and `e2` at the same column inside that block. This is
the primary motivation for the feature and should work automatically once the SEMICOLON injection
is in place.

---

## Feature 2: For-In Collection Loops

### What the feature does

Add `for x in collection do body` syntax. Collection can be a list, array, or range.

```
for x in [1; 2; 3] do
  printfn "%d" x
```

This is distinct from the existing `for i = start to stop do body` (range-index loop). Both coexist.

### AST change

Add a new variant to `Expr` in `Ast.fs`:

```fsharp
// Phase 45+ (For-In Loop): for var in collection do body
| ForInExpr of var: string * collection: Expr * body: Expr * span: Span
```

Update `spanOf` to include `ForInExpr`.

**Design decision:** Do NOT reuse `ForExpr`. `ForExpr` is hardwired to integer bounds and an
`isTo: bool` flag. `ForInExpr` iterates over arbitrary `Value` collections. Keeping them separate
avoids a union type hack and makes Bidir/Eval cases cleaner.

### Lexer change

No new tokens needed. `FOR`, `IN`, and `DO` already exist. The lexer already emits `IN` for the
keyword `in`. Verify there is no collision with the existing `IN` token used for `let...in` — there
is none because the parser grammar rule position disambiguates (FOR is followed by IDENT, then IN).

### Parser change

Add two rules to `Expr` in `Parser.fsy`, alongside the existing `FOR IDENT EQUALS ... TO ...` rules:

```fsharp
// FOR-IN-01: for x in collection do body (inline body)
| FOR IDENT IN Expr DO SeqExpr
    { ForInExpr($2, $4, $6, ruleSpan parseState 1 6) }

// FOR-IN-02: for x in collection do (indented body)
| FOR IDENT IN Expr DO INDENT SeqExpr DEDENT
    { ForInExpr($2, $4, $7, ruleSpan parseState 1 8) }
```

**Precedence/conflict risk:** `IN` is also used in `let x = e IN body`. The parser context makes
this unambiguous: `FOR IDENT IN` is a distinct prefix. FsLexYacc LALR(1) can resolve this without
precedence declarations. Verify no shift/reduce conflict by running `dotnet build` and checking
parser output.

**IndentFilter impact for `in` keyword:** `IN` is already in IndentFilter's offside rule logic. When
`IN` appears after `FOR IDENT`, IndentFilter's `IN` handler pops `InLetDecl` contexts. Since `FOR`
does not push `InLetDecl`, the `IN` handler's `popLetDecl` traversal will find nothing to pop and
exit harmlessly. No IndentFilter change needed.

### Bidir / TypeCheck change

Add a case to `synth` in `Bidir.fs`:

```fsharp
// === ForInExpr (For-in loop) ===
| ForInExpr (var, collExpr, body, span) ->
    let s1, collTy = synth ctorEnv recEnv ctx env collExpr
    // collTy must be 'a list, 'a array, or a range (int list).
    // Introduce fresh element type variable.
    let elemTy = freshVar()
    let s2 = unifyWithContext ctx [] span (apply s1 collTy) (TList elemTy)
    // ... also accept TArray elemTy via separate unify attempt or union
    let s12 = compose s2 s1
    let loopEnv = Map.add var (Scheme([], apply s12 elemTy)) (applyEnv s12 env)
    let s3, _bodyTy = synth ctorEnv recEnv ctx loopEnv body
    (compose s3 s12, TTuple [])  // for-in always returns unit
```

**Collection type handling:** Lists are `TList elemTy`. Arrays are `TArray elemTy`. Ranges desugar
to `TList TInt` in the existing `Range` evaluator. The type checker should accept at minimum `TList`
and `TArray`. If unification of both is needed, attempt `TList` first, then `TArray` on failure, or
introduce a `TIterable` type class (deferred — not needed for MVP).

**MVP choice:** Constrain to `TList elemTy` only for the first pass. Arrays can be added as a
follow-on. Range literals already evaluate to `ListValue`, so they work automatically.

### Eval change

Add a case to `eval` in `Eval.fs`:

```fsharp
// For-in loop: iterate over list or array
| ForInExpr (var, collExpr, body, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    match collVal with
    | ListValue items ->
        for item in items do
            let loopEnv = Map.add var item env
            eval recEnv moduleEnv loopEnv false body |> ignore
        TupleValue []
    | ArrayValue arr ->
        for item in arr do
            let loopEnv = Map.add var item env
            eval recEnv moduleEnv loopEnv false body |> ignore
        TupleValue []
    | _ -> failwith "for-in: collection must be a list or array"
```

### Component boundary summary

| Component | Change | Rationale |
|-----------|--------|-----------|
| Ast.fs | Add `ForInExpr` variant + `spanOf` case | New AST node required |
| Lexer.fsl | None | `FOR`, `IN`, `DO` already tokenized |
| Parser.fsy | Add 2 grammar rules for `FOR IDENT IN Expr DO` | Syntax |
| IndentFilter.fs | None | `DO` already triggers InExprBlock; `IN` handling already correct |
| Bidir.fs | Add `ForInExpr` synth case | Type checking |
| Eval.fs | Add `ForInExpr` eval case | Execution |
| Prelude/*.fun | None | No Prelude changes needed for this feature |

### Conflict with existing `IN` handling in IndentFilter

The IndentFilter `IN` handler pops `InLetDecl` contexts. The `for x in collection` construct does
not push `InLetDecl`, so the handler is a no-op in this context. However, if the collection
expression itself contains `let...in`, the offside logic fires for the inner `let`. This is correct
and requires no special handling.

---

## Feature 3: Option/Result Prelude Utilities

### What the feature does

Expand `Prelude/Option.fun` and `Prelude/Result.fun` with additional combinators that are missing
or differently named compared to idiomatic F# usage.

Current state of `Option.fun`:
- `optionMap`, `optionBind`, `optionDefault`, `isSome`, `isNone`, `(<|>)`

Current state of `Result.fun`:
- `resultMap`, `resultBind`, `resultMapError`, `resultDefault`, `isOk`, `isError`

Likely additions (common in F# Option/Result usage):
- `Option.map` alias (or rename) using standard F# naming convention
- `Option.orElse` (lazy version of `(<|>)`)
- `Option.filter` — `optionFilter pred opt` returns `None` if pred fails
- `Option.toList` — `Some x -> [x]`, `None -> []`
- `Option.ofBool` — `if b then Some () else None`
- `Result.toOption` — `Ok x -> Some x`, `Error _ -> None`
- `Result.ofOption` — `Some x -> Ok x`, `None -> Error msg`
- `Result.mapBoth` — map both Ok and Error branches

### Architecture: Prelude-only, no interpreter changes

**This feature touches only `.fun` files.** The LangThree language already supports everything
needed to implement these functions: match expressions, ADT constructors (`Some`, `None`, `Ok`,
`Error`), lambdas, and the module/open system.

The Prelude loader (`Prelude.fs`) loads `*.fun` files alphabetically. `List.fun` loads before
`Option.fun` (L < O), which loads before `Result.fun` (O < R). Dependencies flow in this order:
- `Core.fun` (C) — basic operations
- `List.fun` (L) — list operations
- `Option.fun` (O) — can use List utilities if needed
- `Result.fun` (R) — can use Option utilities

If `Result.fun` needs `Option` types, they are already available because `Option.fun` loaded first
and `open Option` is at the bottom of that file, making `Option` type and constructors available.

### Type system impact

None. The `Option 'a` and `Result 'a 'b` types are already declared in the existing Prelude files.
Adding functions to these modules does not require any type system changes — the existing HM
inference handles polymorphic functions over ADTs correctly.

### Naming convention decision

The existing functions use `optionMap`, `resultBind` style (prefixed, not dot-notation). New
functions should follow the same convention for consistency within the existing codebase. Do not
introduce a separate `Option.map` alongside `optionMap` — pick one name and use it.

Alternatively, if the milestone wants to move toward F#-idiomatic `Option.map`, this is the right
time to rename existing functions and add aliases. That is a naming decision for the roadmap, not
an architecture concern.

### Load order guarantee

The Prelude loader sorts files alphabetically:

```fsharp
let files = Directory.GetFiles(preludeDir, "*.fun") |> Array.sort
```

Current files: `Array.fun`, `Core.fun`, `Hashtable.fun`, `List.fun`, `Option.fun`, `Result.fun`.
This alphabetical order means each file can depend on all earlier files. No changes to the loading
mechanism are needed.

### Component boundary summary

| Component | Change | Rationale |
|-----------|--------|-----------|
| Prelude/Option.fun | Add new combinator functions | Pure language-level implementation |
| Prelude/Result.fun | Add new combinator functions | Pure language-level implementation |
| Prelude.fs | None | Loader already handles *.fun alphabetically |
| Lexer, Parser, Bidir, Eval | None | No new syntax or semantics required |

---

## Integration Order and Build Sequence

The three features are largely independent. Suggested order:

### Phase 1: Option/Result Prelude utilities

**Why first:** Purely additive, zero risk of breaking existing behavior, no interpreter changes.
Provides immediate value and can be tested in isolation by running flt tests on Prelude-dependent
programs.

**Files:** `Prelude/Option.fun`, `Prelude/Result.fun`

**Tests:** New `.flt` files exercising each new combinator.

### Phase 2: Newline implicit sequencing

**Why second:** IndentFilter change is self-contained but has the highest risk of breaking existing
tests (anything with multi-line expression blocks). Do this before adding new AST nodes to keep the
diff focused. Run the full flt suite after each IndentFilter change.

**Files:** `IndentFilter.fs`

**Tests:** New `.flt` files with newline-sequenced do-blocks, lambda bodies, let-RHS blocks.
Regression: all existing flt tests must still pass.

### Phase 3: For-in collection loops

**Why third:** Requires AST change (ForInExpr), Parser change, Bidir change, and Eval change — the
most invasive set. Building on a stable newline-sequencing implementation first means that
`for x in xs do\n  e1\n  e2` bodies work correctly through the already-tested implicit SEMICOLON.

**Files:** `Ast.fs`, `Parser.fsy`, `Bidir.fs`, `Eval.fs`

**Tests:** New `.flt` files with for-in over lists, arrays, ranges, and multi-statement bodies.

---

## Component Interaction Map

```
IndentFilter.fs
  ├─ emits SEMICOLON (new) on newline at InExprBlock level
  └─ emits INDENT/DEDENT as before
        ↓
Parser.fsy
  ├─ SeqExpr handles SEMICOLON (existing, no change)
  └─ new ForInExpr rules: FOR IDENT IN Expr DO SeqExpr
        ↓
Ast.fs
  └─ ForInExpr variant (new)
        ↓
Bidir.fs
  └─ ForInExpr case: unify collection as TList elemTy, bind var, return unit
        ↓
Eval.fs
  └─ ForInExpr case: iterate ListValue/ArrayValue, bind var per iteration

Prelude/Option.fun  ← independent, loaded by Prelude.fs
Prelude/Result.fun  ← independent, loaded by Prelude.fs
```

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Emitting SEMICOLON at module level

IndentFilter must only emit implicit SEMICOLON when `Context` stack top is `InExprBlock`. At module
level the context is `InModule`, which explicitly does NOT generate implicit IN tokens. The same
guard must block implicit SEMICOLON at module level, otherwise top-level declarations would be
separated with semicolons and break the `Decls` grammar rule which uses no SEMICOLON separator.

**Detection:** If `let x = 1` followed by `let y = 2` at column 0 suddenly fails to parse, the
guard is missing.

### Anti-Pattern 2: Emitting SEMICOLON before IN/ELSE/WITH/PIPE

These tokens close the current expression context. Emitting SEMICOLON before `in`, `else`, `with`,
or `|` would produce `e1 ; in ...` which is a parse error. The negative blocklist in the
`processNewlineWithContext` lookahead logic must include these tokens.

**Detection:** `let x = e1\nin e2` (indented `in`) suddenly fails; or `if b then e1\nelse e2` fails.

### Anti-Pattern 3: Reusing ForExpr for for-in

Trying to express `for x in xs do body` by detecting a list in `ForExpr` at eval time (checking
whether startVal/stopVal are lists) creates a false unification between integer-range loops and
collection iteration. The type checker would need special-case logic for the same AST node. Two
separate AST variants with separate typing rules is the clean approach.

**Detection:** `ForExpr` gains a new branch like `| ListValue items -> ...` in Eval.fs.

### Anti-Pattern 4: Introducing TIterable or type class for for-in

Unifying lists and arrays through a new type constructor (e.g., `TIterable 'a`) requires unification
changes, new Type.fs cases, and printer updates. For MVP, constraining for-in to `TList` (which also
covers Range results) is sufficient. Arrays can be added later by trying `TArray` unification as a
second attempt. Do not introduce a new polymorphic collection type until the need is concrete.

### Anti-Pattern 5: Adding Option/Result functions as builtins in Eval.fs

The existing `optionMap`, `isSome` etc. are implemented in `.fun` files, not as builtins in Eval.fs.
New combinators must follow the same pattern. Adding them as F# builtins (like `BuiltinValue`) would
create asymmetry: they would work without loading Prelude, would bypass type checking, and would be
harder to test and audit.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| IndentFilter newline sequencing | HIGH | Mechanism is well-understood; InExprBlock context is the right hook |
| ForInExpr AST/Parser | HIGH | Parallel to existing ForExpr pattern; no new tokens needed |
| ForInExpr type checking | HIGH | Fresh element TVar + TList unification is standard |
| ForInExpr IN token disambiguation | HIGH | LALR(1) context makes FOR IDENT IN unambiguous from let...in |
| Option/Result Prelude | HIGH | Pure .fun implementation, no interpreter changes |
| Implicit SEMICOLON edge cases | MEDIUM | Needs careful testing of all context-exiting tokens (IN, ELSE, WITH, PIPE) |

---

## Files Modified Per Feature

### Newline Implicit Sequencing

- `src/LangThree/IndentFilter.fs` — emit SEMICOLON in NEWLINE handler for `InExprBlock` + same-level

### For-In Collection Loops

- `src/LangThree/Ast.fs` — add `ForInExpr` variant, update `spanOf`
- `src/LangThree/Parser.fsy` — add `FOR IDENT IN Expr DO SeqExpr` rules
- `src/LangThree/Bidir.fs` — add `ForInExpr` synth case
- `src/LangThree/Eval.fs` — add `ForInExpr` eval case

### Option/Result Prelude Utilities

- `Prelude/Option.fun` — new combinator functions
- `Prelude/Result.fun` — new combinator functions
