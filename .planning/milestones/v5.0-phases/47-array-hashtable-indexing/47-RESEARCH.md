# Phase 47: Array and Hashtable Indexing Syntax - Research

**Researched:** 2026-03-28
**Domain:** LALR(1) grammar extension — lexer token strategy, AST nodes, type checking, evaluation
**Confidence:** HIGH

## Summary

This phase adds `.[i]` indexing syntax so users can write `arr.[i]` and `arr.[i] <- v` instead of calling `array_get`/`array_set` functions directly. The same syntax applies to hashtables: `ht.[key]` and `ht.[key] <- v`. Chained indexing `matrix.[r].[c]` must also work.

The central design question is how to lex and parse `.[`. Three strategies were considered. The recommended strategy is a **single `DOTLBRACKET` token** produced by the lexer. This sidesteps all LALR(1) shift/reduce conflicts that arise if `.` and `[` remain separate tokens, and it is the exact approach F# itself uses (`.[` is lexed as a single token in FSharp.Compiler.Service). The implementation touches six layers: Lexer, Parser, Ast, Bidir (type check), Eval, and Format. Desugar at the parser level is the cleanest approach because it keeps the AST minimal: `arr.[i]` becomes `App(App(Var "array_get", arr), i)` and `arr.[i] <- v` becomes `App(App(App(Var "array_set", arr), i), v)`, with hashtable equivalents.

**Primary recommendation:** Add `DOTLBRACKET` token to the lexer, add two grammar rules (get and set forms) in the `Atom`/`Expr` levels, desugar directly to existing builtin calls in the parser action, and add no new AST nodes. Type checking and eval require zero changes.

## Standard Stack

This phase is a pure language extension within the existing codebase. No new libraries are needed.

### Core components touched

| File | Change | Why |
|------|--------|-----|
| `Lexer.fsl` | Add `".[" { DOTLBRACKET }` rule before `'.'` and `'['` | Single token sidesteps LALR conflicts |
| `Parser.fsy` | Declare `%token DOTLBRACKET`, add grammar rules | Gets/sets for both array and hashtable |
| `IndentFilter.fs` | No change needed | `DOTLBRACKET` contains no bracket the filter tracks |
| `Ast.fs` | No new nodes if desugaring | Desugared to `App` chains |
| `Bidir.fs` | No change | Existing `App` and `Var` paths handle it |
| `Eval.fs` | No change | Existing `array_get`/`array_set` builtins handle it |
| `Format.fs` | No change | No new AST nodes |
| `Infer.fs` | No change | Stubs already handle `App` |

### Installation

No new packages — all work is in existing source files.

## Architecture Patterns

### Recommended Project Structure

No new files are required. All changes are in-place edits to existing files.

### Pattern 1: DOTLBRACKET single token (RECOMMENDED)

**What:** Lex `.[` as a single token instead of two tokens DOT followed by LBRACKET.

**When to use:** Any time a two-character sequence has fixed meaning and would create conflicts if split. F# uses this exact approach.

**Why it avoids conflicts:** If `.` and `[` remain separate, the grammar rule `Atom DOT LBRACKET Expr RBRACKET` collides with `Atom DOT IDENT` (field access) — the parser sees `Atom DOT` and doesn't know whether to shift `[` (index) or `IDENT` (field). With `DOTLBRACKET` the parser sees `Atom DOTLBRACKET` and there is no ambiguity.

**Lexer rule order matters:** In `Lexer.fsl`, the rule for `".[" { DOTLBRACKET }` must appear BEFORE the rules for `'.'` and `'['`. In fslex, longer matches win, so `".["`  (2 chars) will win over `'.'` (1 char). But for safety, place it before both.

```fsharp
// In Lexer.fsl, before existing DOT and LBRACKET rules:
| ".["           { DOTLBRACKET }
| ".."           { DOTDOT }    // already present — keep this before '.'
| '.'            { DOT }       // already present
| '['            { LBRACKET }  // already present
```

Note: `..` (DOTDOT) is already two chars and currently placed before `.`. Adding `.[` requires no reordering relative to `..` (they share no prefix after the first char).

### Pattern 2: Parser-level desugaring (get form)

**What:** Grammar rule in `Atom` that transforms `arr.[i]` directly to an `App(App(Var "array_get", arr), i)` AST.

**When to use:** When the new syntax is pure sugar over existing builtins — no new semantics needed.

**Key insight:** The type checker and evaluator already handle `App(App(Var "array_get", ...))` perfectly. Zero changes needed in those layers.

```fsharp
// In Parser.fsy, in the Atom production:
// IDX-01: arr.[i] -> array_get arr i  (and hashtable_get ht key)
| Atom DOTLBRACKET Expr RBRACKET
    { let span = ruleSpan parseState 1 4
      let getSpan = symSpan parseState 2
      App(App(Var("array_get", getSpan), $1, span), $3, span) }
```

Wait — `array_get` vs `hashtable_get` depends on the type of `$1`. At parse time we have no type info. We cannot choose between `array_get` and `hashtable_get` in the parser. Two options:

**Option A (recommended):** Use a single unified builtin. Add a new builtin `index_get` (and `index_set`) that dispatches on the runtime value type. These thin dispatchers live in Eval.fs's `makeBuiltins` function and in TypeCheck.fs's type environment.

**Option B:** Desugar using a dedicated AST node `IndexGet(expr, index, span)` that the type checker resolves polymorphically, choosing `TArray` vs `THashtable` at check time.

**Option A is simpler** — no new AST nodes, no pattern in Format/Infer/Bidir/Eval. The dispatcher builtins are 10-line functions.

### Pattern 3: New builtins `index_get` / `index_set` for unified dispatch

**What:** Two new builtins that accept either an `ArrayValue` or `HashtableValue` and dispatch accordingly.

**Type signature:**
- `index_get`: `'a -> 'b -> 'c` (fully polymorphic at this level; type checker treats it as a special case OR uses union-type trick)
- `index_set`: `'a -> 'b -> 'c -> unit`

**Type checking approach:** The simplest approach that avoids a union type is to give `index_get` and `index_set` a polymorphic type in the type environment that matches both cases, and rely on the evaluator to dispatch correctly. Specifically, give them fresh type variables: `Scheme([0;1;2], TArrow(TVar 0, TArrow(TVar 1, TVar 2)))` for get. This is intentionally weak — it won't catch type errors in indexing at compile time but it won't reject valid programs either.

**Better approach:** Add a proper bidirectional check in `Bidir.fs` for `IndexGet`/`IndexSet` AST nodes. This is Pattern B (Option B above). Given the existing infrastructure in Bidir.fs, it's not much harder than Option A and gives proper type error messages. See "Architecture Patterns — Pattern 4" below.

### Pattern 4: IndexGet / IndexSet AST nodes with Bidir type checking (PREFERRED over Option A)

**What:** Two new AST node variants handle both array and hashtable indexing with real type checking.

```fsharp
// In Ast.fs, Expr union:
| IndexGet of collection: Expr * index: Expr * span: Span
    // arr.[i]  or  ht.[key]
| IndexSet of collection: Expr * index: Expr * value: Expr * span: Span
    // arr.[i] <- v  or  ht.[key] <- v
```

**Parser desugaring — NOT used.** Instead the parser produces `IndexGet`/`IndexSet` nodes.

**Bidir.fs type checking:**

```fsharp
// IDX-01 / IDX-03: arr.[i] or ht.[key]
| IndexGet (collExpr, idxExpr, span) ->
    let s1, collTy = synth ctorEnv recEnv ctx env collExpr
    let resolvedCollTy = apply s1 collTy
    match resolvedCollTy with
    | TArray elemTy ->
        let s2, idxTy = synth ctorEnv recEnv ctx (applyEnv s1 env) idxExpr
        let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
        (compose s3 (compose s2 s1), apply s3 (apply s2 elemTy))
    | THashtable (keyTy, valTy) ->
        let s2, idxTy = synth ctorEnv recEnv ctx (applyEnv s1 env) idxExpr
        let s3 = unifyWithContext ctx [] span (apply s2 idxTy) keyTy
        (compose s3 (compose s2 s1), apply s3 valTy)
    | _ ->
        raise (TypeException { Kind = IndexOnNonCollection resolvedCollTy; ... })

// IDX-02 / IDX-04: arr.[i] <- v or ht.[key] <- v
| IndexSet (collExpr, idxExpr, valExpr, span) ->
    let s1, collTy = synth ctorEnv recEnv ctx env collExpr
    let resolvedCollTy = apply s1 collTy
    match resolvedCollTy with
    | TArray elemTy ->
        let s2, idxTy = synth ... idxExpr
        let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
        let s4, valTy = synth ... valExpr
        let s5 = unifyWithContext ctx [] span (apply s4 valTy) elemTy
        (compose s5 ..., TTuple [])
    | THashtable (keyTy, valTy) ->
        let s2, idxTy = synth ... idxExpr
        let s3 = unifyWithContext ctx [] span (apply s2 idxTy) keyTy
        let s4, valTy' = synth ... valExpr
        let s5 = unifyWithContext ctx [] span (apply s4 valTy') valTy
        (compose s5 ..., TTuple [])
    | _ ->
        raise (TypeException { Kind = IndexOnNonCollection resolvedCollTy; ... })
```

**Eval.fs dispatch:**

```fsharp
| IndexGet (collExpr, idxExpr, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let idxVal  = eval recEnv moduleEnv env false idxExpr
    match collVal, idxVal with
    | ArrayValue arr, IntValue i ->
        if i < 0 || i >= arr.Length then
            raise (LangThreeException (StringValue (sprintf "Index %d out of bounds (length %d)" i arr.Length)))
        arr.[i]
    | HashtableValue ht, key ->
        match ht.TryGetValue(key) with
        | true, v -> v
        | false, _ -> raise (LangThreeException (StringValue "Key not found"))
    | _ -> failwith "IndexGet: expected array or hashtable"

| IndexSet (collExpr, idxExpr, valExpr, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let idxVal  = eval recEnv moduleEnv env false idxExpr
    let newVal  = eval recEnv moduleEnv env false valExpr
    match collVal, idxVal with
    | ArrayValue arr, IntValue i ->
        if i < 0 || i >= arr.Length then
            raise (LangThreeException (StringValue (sprintf "Index %d out of bounds (length %d)" i arr.Length)))
        arr.[i] <- newVal
        TupleValue []
    | HashtableValue ht, key ->
        ht.[key] <- newVal
        TupleValue []
    | _ -> failwith "IndexSet: expected array or hashtable"
```

### Pattern 5: Chained indexing `matrix.[r].[c]`

**What:** `matrix.[r].[c]` should parse as `(matrix.[r]).[c]` — left-associative. This works automatically because the grammar rule is `Atom DOTLBRACKET Expr RBRACKET` and `Atom` itself includes the result of `Atom DOTLBRACKET Expr RBRACKET` (left recursion in Atom). The LALR(1) parser handles this naturally.

**Precedence:** Index access (`.[i]`) should bind at the same level as field access (`.IDENT`), which is at the `Atom` level — tighter than function application. This is correct: `f arr.[i]` should parse as `f (arr.[i])`, not `(f arr).[i]`.

### Pattern 6: IndexSet placement in grammar

The set form `arr.[i] <- v` must live in `Expr` (not `Atom`), just like `x <- v` (Assign) and `r.field <- v` (SetField). The grammar rule is:

```fsharp
// In Expr production:
| Atom DOTLBRACKET Expr RBRACKET LARROW Expr
    { IndexSet($1, $3, $6, ruleSpan parseState 1 6) }
```

The get form lives in `Atom`:
```fsharp
// In Atom production:
| Atom DOTLBRACKET Expr RBRACKET
    { IndexGet($1, $3, ruleSpan parseState 1 4) }
```

This mirrors exactly how `SetField` (`Atom DOT IDENT LARROW Expr`) lives in `Expr` while `FieldAccess` (`Atom DOT IDENT`) lives in `Atom`.

### Anti-Patterns to Avoid

- **Two tokens DOT + LBRACKET:** Causes shift/reduce conflict in LALR(1) because after seeing `Atom DOT`, the parser cannot determine if `[` starts an index expression or if it's a different production. Use `DOTLBRACKET` instead.
- **Parsing without type info:** Do not try to choose between `array_get` and `hashtable_get` at parse time. Either use dispatch builtins or new AST nodes.
- **IndexSet in Atom:** Set operations must be in `Expr` because they are statements (return unit), not expressions that can appear as function arguments. Placing `arr.[i] <- v` in `Atom` would allow `f (arr.[i] <- v)` which is nonsensical and a type error.
- **Forgetting spanOf:** When adding new AST nodes to `Ast.fs`, add them to the `spanOf` match. If missed, the compiler will give an incomplete match warning and runtime failures on error reporting.
- **Forgetting Infer.fs stubs:** `Infer.fs` has stubs for every AST node (it's the Hindley-Milner inference path, with Bidir being the primary checker). When adding `IndexGet`/`IndexSet`, add stubs there too.
- **Forgetting Format.fs:** `Format.fs` has a `formatAst` function used by the REPL and debug tooling. New nodes need a match arm.
- **DOTLBRACKET bracket depth tracking:** The `IndentFilter.fs` tracks bracket depth when it sees `LBRACKET`. Since `DOTLBRACKET` is a different token, it does NOT increment bracket depth. The `]` (RBRACKET) that closes it DOES decrement. This means `DOTLBRACKET` contents are NOT automatically in "bracket mode" for the indent filter. However, since `arr.[i]` is a self-contained expression that doesn't span lines in normal usage, this should not cause problems. If multi-line indexing is needed, add `DOTLBRACKET` to the bracket tracking list in `IndentFilter.fs`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Array bounds checking | Custom check code | Reuse existing `array_get`/`array_set` logic in eval | Bounds check already in builtins; copy-paste creates drift |
| Hashtable missing-key error | New error path | Reuse existing `hashtable_get` error path | Already raises `LangThreeException` with correct message |
| New type `TIndexable` | Union type for array+hashtable | Match on `TArray`/`THashtable` in Bidir.fs | Adding a type risks breaking unification; match-based dispatch is simpler |
| DOTDOT conflict avoidance | Lookahead hack | Correct fslex rule order | fslex uses longest-match; `.[` (2 chars) always beats `.` (1 char) |

**Key insight:** The existing `array_get`/`array_set`/`hashtable_get`/`hashtable_set` builtins have all the error handling, bounds checking, and mutation logic. `IndexGet`/`IndexSet` in the evaluator are thin wrappers that do the same thing inline, OR these nodes can simply call `callValue` on the builtin values looked up from the environment (but the direct inline approach is cleaner for a specialized eval node).

## Common Pitfalls

### Pitfall 1: LALR(1) conflict with DOT + LBRACKET as two tokens

**What goes wrong:** If you add rule `Atom DOT LBRACKET Expr RBRACKET` without a new token, the parser sees `Atom DOT` and has two lookaheads: `LBRACKET` (could be index access) and `IDENT` (could be field access). Depending on LALR(1) state construction, this causes a shift/reduce conflict that fsyacc reports as a warning or error.

**Why it happens:** The `Atom DOT IDENT` and `Atom DOT LBRACKET` rules share the common prefix `Atom DOT`. LALR(1) parsers cannot use lookahead past the current symbol to resolve this.

**How to avoid:** Use `DOTLBRACKET` as a single token.

**Warning signs:** `fsyacc` prints "shift/reduce conflict" warning during code generation.

### Pitfall 2: IndexSet in wrong grammar level

**What goes wrong:** If `arr.[i] <- v` is placed in `Atom` instead of `Expr`, then the expression `arr.[i] <- v` would be valid as a function argument: `f (arr.[i] <- v)`. This is syntactically accepted but semantically wrong (assignment returns unit, confusing users).

**Why it happens:** Developer follows the get-form pattern (Atom) for the set form too.

**How to avoid:** Put set form in `Expr`, exactly mirroring `SetField`. Pattern: get → Atom, set → Expr.

### Pitfall 3: spanOf not updated in Ast.fs

**What goes wrong:** Adding `IndexGet`/`IndexSet` to the `Expr` union but forgetting to add them to the `spanOf` function causes incomplete match warnings (or errors with `--warnaserror`) and crashes when the type checker tries to get a span for an IndexGet expression.

**Why it happens:** `spanOf` is a large match expression, easy to miss.

**How to avoid:** Search for `ForExpr` in `Ast.fs` and add the new cases right after it.

### Pitfall 4: Infer.fs stub missing

**What goes wrong:** The HM inference path in `Infer.fs` has stubs for all AST nodes. Missing a new node causes an F# incomplete match compile error.

**Why it happens:** Infer.fs is easy to forget since Bidir.fs is the primary checker.

**How to avoid:** Add stub `| IndexGet _ | IndexSet _ -> (empty, freshVar())` to `Infer.fs` alongside the existing stubs (near the `RecordExpr` and `WhileExpr` stubs).

### Pitfall 5: DOTLBRACKET inside string/char literals

**What goes wrong:** The lexer processes `".[` inside a string literal. Since string literals are handled by the `read_string` sub-rule, not the main `tokenize` rule, `.[` inside strings is not tokenized as `DOTLBRACKET`. This is correct behavior — no pitfall here, but confirm the `read_string` rule is not affected.

**Why it happens:** Developer worries about strings. The existing string lexer rule `read_string` handles characters one at a time and does not invoke `tokenize`, so `".[" ` in source produces STRING ".[" not DOTLBRACKET.

**How to avoid:** No action needed — the lexer sub-rule architecture handles this correctly.

### Pitfall 6: Chained indexing `matrix.[r].[c]` ambiguity

**What goes wrong:** Someone worries that `matrix.[r].[c]` might not work because `Atom` can't be left-recursive in LALR(1).

**Why it is actually fine:** fsyacc handles left-recursive `Atom` rules. The existing `FieldAccess` rule is already `Atom DOT IDENT`, which is left-recursive on `Atom`. `IndexGet` follows the same pattern and left-associativity is automatic.

**How to verify:** Add a test `matrix.[0].[1]` and check it parses and evaluates correctly.

### Pitfall 7: IndentFilter bracket depth mismatch

**What goes wrong:** `arr.[  \n  i  \n]` (multi-line index expression) might cause indent/dedent issues because `DOTLBRACKET` doesn't trigger bracket depth tracking in `IndentFilter.fs`.

**How to avoid:** Add `DOTLBRACKET` to the bracket-depth tracking list in `IndentFilter.fs` alongside `LBRACKET`:

```fsharp
| Parser.LBRACKET | Parser.LPAREN | Parser.LBRACE | Parser.DOTLBRACKET ->
    state <- { state with BracketDepth = state.BracketDepth + 1; PrevToken = Some token }
    yield token
```

This ensures that newlines inside `.[  ]` don't generate spurious INDENT/DEDENT tokens.

## Code Examples

### Lexer addition

```fsharp
// In Lexer.fsl, in the main tokenize rule
// MUST appear before ".." and "." rules (it already will win by longest-match,
// but explicit ordering is clearer)
| ".["           { DOTLBRACKET }
| ".."           { DOTDOT }
| '.'            { DOT }
```

### Parser token declaration

```fsharp
// In Parser.fsy, token declarations section
// Phase 47 (Array/Hashtable Indexing): dot-bracket token
%token DOTLBRACKET
```

### Parser grammar rules

```fsharp
// In Parser.fsy, in the Atom production (get form):
// IDX-01 / IDX-03: arr.[i] reads array/hashtable element
| Atom DOTLBRACKET Expr RBRACKET
    { IndexGet($1, $3, ruleSpan parseState 1 4) }

// In Parser.fsy, in the Expr production (set form):
// IDX-02 / IDX-04: arr.[i] <- v writes array/hashtable element
| Atom DOTLBRACKET Expr RBRACKET LARROW Expr
    { IndexSet($1, $3, $6, ruleSpan parseState 1 6) }
```

### AST node additions

```fsharp
// In Ast.fs, Expr union — add after ForExpr:
// Phase 47 (Array/Hashtable Indexing): index access
| IndexGet of collection: Expr * index: Expr * span: Span
| IndexSet of collection: Expr * index: Expr * value: Expr * span: Span
```

```fsharp
// In Ast.fs, spanOf function — add after ForExpr case:
| IndexGet(_, _, s) | IndexSet(_, _, _, s) -> s
```

### Bidir.fs type check

```fsharp
// In Bidir.fs, after the SetField case:

// === IndexGet (Phase 47 - array/hashtable index read) ===
| IndexGet (collExpr, idxExpr, span) ->
    let s1, collTy = synth ctorEnv recEnv ctx env collExpr
    let resolvedCollTy = apply s1 collTy
    match resolvedCollTy with
    | TArray elemTy ->
        let s2, idxTy = synth ctorEnv recEnv ctx (applyEnv s1 env) idxExpr
        let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
        (compose s3 (compose s2 s1), apply (compose s3 s2) elemTy)
    | THashtable (keyTy, valTy) ->
        let s2, idxTy = synth ctorEnv recEnv ctx (applyEnv s1 env) idxExpr
        let s3 = unifyWithContext ctx [] span (apply s2 idxTy) keyTy
        (compose s3 (compose s2 s1), apply s3 valTy)
    | ty ->
        raise (TypeException {
            Kind = IndexOnNonCollection ty
            Span = span; Term = Some expr; ContextStack = ctx; Trace = []
        })

// === IndexSet (Phase 47 - array/hashtable index write) ===
| IndexSet (collExpr, idxExpr, valExpr, span) ->
    let s1, collTy = synth ctorEnv recEnv ctx env collExpr
    let resolvedCollTy = apply s1 collTy
    match resolvedCollTy with
    | TArray elemTy ->
        let env1 = applyEnv s1 env
        let s2, idxTy = synth ctorEnv recEnv ctx env1 idxExpr
        let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
        let env2 = applyEnv (compose s3 s2) env1
        let s4, valTy = synth ctorEnv recEnv ctx env2 valExpr
        let s5 = unifyWithContext ctx [] span (apply s4 valTy) (apply (compose s4 (compose s3 s2)) elemTy)
        (compose s5 (compose s4 (compose s3 (compose s2 s1))), TTuple [])
    | THashtable (keyTy, valTy) ->
        let env1 = applyEnv s1 env
        let s2, idxTy = synth ctorEnv recEnv ctx env1 idxExpr
        let s3 = unifyWithContext ctx [] span (apply s2 idxTy) keyTy
        let env2 = applyEnv (compose s3 s2) env1
        let s4, valTy' = synth ctorEnv recEnv ctx env2 valExpr
        let s5 = unifyWithContext ctx [] span (apply s4 valTy') (apply (compose s4 s3) valTy)
        (compose s5 (compose s4 (compose s3 (compose s2 s1))), TTuple [])
    | ty ->
        raise (TypeException {
            Kind = IndexOnNonCollection ty
            Span = span; Term = Some expr; ContextStack = ctx; Trace = []
        })
```

### Infer.fs stub

```fsharp
// In Infer.fs, near existing stubs for RecordExpr, WhileExpr etc.:
| IndexGet _ | IndexSet _ ->
    (empty, freshVar())
```

### Eval.fs dispatch

```fsharp
// In Eval.fs, eval function — add after ForExpr case:
| IndexGet (collExpr, idxExpr, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let idxVal  = eval recEnv moduleEnv env false idxExpr
    match collVal, idxVal with
    | ArrayValue arr, IntValue i ->
        if i < 0 || i >= arr.Length then
            raise (LangThreeException (StringValue (sprintf "Array index %d out of bounds (length %d)" i arr.Length)))
        arr.[i]
    | HashtableValue ht, key ->
        match ht.TryGetValue(key) with
        | true, v -> v
        | false, _ -> raise (LangThreeException (StringValue "Hashtable key not found"))
    | _ -> failwith "IndexGet: expected array or hashtable"

| IndexSet (collExpr, idxExpr, valExpr, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let idxVal  = eval recEnv moduleEnv env false idxExpr
    let newVal  = eval recEnv moduleEnv env false valExpr
    match collVal, idxVal with
    | ArrayValue arr, IntValue i ->
        if i < 0 || i >= arr.Length then
            raise (LangThreeException (StringValue (sprintf "Array index %d out of bounds (length %d)" i arr.Length)))
        arr.[i] <- newVal
        TupleValue []
    | HashtableValue ht, key ->
        ht.[key] <- newVal
        TupleValue []
    | _ -> failwith "IndexSet: expected array or hashtable"
```

### Format.fs additions

```fsharp
// In Format.fs, formatAst function — add after ForExpr case:
| Ast.IndexGet (coll, idx, _) ->
    sprintf "IndexGet (%s, %s)" (formatAst coll) (formatAst idx)
| Ast.IndexSet (coll, idx, v, _) ->
    sprintf "IndexSet (%s, %s, %s)" (formatAst coll) (formatAst idx) (formatAst v)
```

### Diagnostic.fs new error kind

```fsharp
// In Diagnostic.fs, TypeErrorKind union — add near FieldAccessOnNonRecord:
| IndexOnNonCollection of ty: Type    // E0471: tried to index non-array/hashtable
```

And a corresponding format in the `formatTypeError` function:
```fsharp
| IndexOnNonCollection ty ->
    sprintf "Cannot index into value of type %s; expected array or hashtable" (formatType ty)
```

### IndentFilter.fs bracket tracking

```fsharp
// In IndentFilter.fs, in the token match:
| Parser.LBRACKET | Parser.LPAREN | Parser.LBRACE | Parser.DOTLBRACKET ->
    state <- { state with BracketDepth = state.BracketDepth + 1; PrevToken = Some token }
    yield token
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Array.get arr i` | `arr.[i]` | Phase 47 | Ergonomic syntax for array/hashtable access |
| `Array.set arr i v` | `arr.[i] <- v` | Phase 47 | Consistent with mutable field syntax `r.f <- v` |
| `Hashtable.get ht k` | `ht.[k]` | Phase 47 | Uniform syntax for both collection types |
| `Hashtable.set ht k v` | `ht.[k] <- v` | Phase 47 | Same mutation pattern as arrays |

**Not deprecated:** `Array.get`, `Array.set`, `Hashtable.get`, `Hashtable.set` remain available. The `.[i]` syntax is additive sugar.

## Open Questions

1. **Error message for wrong index type**
   - What we know: Type checker unifies index type with `TInt` (array) or key type (hashtable)
   - What's unclear: Whether the error message will be clear enough ("expected int, got string") when user writes `arr.["x"]`
   - Recommendation: The standard `UnifyMismatch` error from `unifyWithContext` will suffice; no special error kind needed for index type mismatch

2. **TypeCheck.fs rewriteModuleAccess**
   - What we know: `TypeCheck.fs` has a `rewriteModuleAccess` function that recursively rewrites `FieldAccess` nodes for module-qualified names
   - What's unclear: Whether `IndexGet`/`IndexSet` can appear in contexts where module rewriting is needed (e.g., `SomeModule.arr.[i]`)
   - Recommendation: Add `IndexGet`/`IndexSet` to `rewriteModuleAccess` to recurse into sub-expressions, but no rewriting of the nodes themselves is needed

3. **Exhaustive.fs and MatchCompile.fs**
   - What we know: These files do not currently pattern-match on `WhileExpr`/`ForExpr` (they have no match clause handling needed there)
   - What's unclear: Whether `IndexGet`/`IndexSet` need to be added to these files
   - Recommendation: Search for `ForExpr` in both files to confirm. Based on current code, no changes needed.

4. **TypeCheck.fs collectMatches / collectTryWiths**
   - What we know: `TypeCheck.fs` has `collectMatches` and `collectTryWiths` that recurse through expressions; `SetField` is handled there
   - What's unclear: Whether `IndexSet` needs to be in `collectMatches` / `collectTryWiths`
   - Recommendation: Add `IndexGet`/`IndexSet` arms to `collectMatches` and `collectTryWiths` in `TypeCheck.fs` to correctly recurse into sub-expressions. Pattern: `| IndexGet(e, i, _) -> collectMatches e @ collectMatches i`

## Sources

### Primary (HIGH confidence)
- Direct source code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Lexer.fsl` — confirmed lexer rule order and DOT/DOTDOT/LBRACKET rules
- Direct source code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Parser.fsy` — confirmed Atom/Expr grammar structure, existing FieldAccess/SetField pattern
- Direct source code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Ast.fs` — confirmed Expr union, Value union, spanOf function
- Direct source code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Bidir.fs` — confirmed SetField/FieldAccess type check pattern, mutableVars, synth function signature
- Direct source code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Eval.fs` — confirmed eval function signature, array_get/array_set/hashtable_get/hashtable_set builtins
- Direct source code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/IndentFilter.fs` — confirmed LBRACKET bracket tracking
- Direct source code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/TypeCheck.fs` — confirmed collectMatches/collectTryWiths/rewriteModuleAccess patterns

### Secondary (MEDIUM confidence)
- Analogy to F# compiler: F# uses `.[` as a single token for array indexing, supporting the DOTLBRACKET approach

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all files directly inspected
- Architecture: HIGH — confirmed against existing SetField/FieldAccess precedent in same codebase
- Pitfalls: HIGH — derived from actual LALR(1) grammar constraints and code structure analysis

**Research date:** 2026-03-28
**Valid until:** 2026-04-28 (stable codebase, low churn)

## Affected Files Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `Lexer.fsl` | Add rule | `".[" { DOTLBRACKET }` before `..` and `.` rules |
| `Parser.fsy` | Add token + 2 rules | `%token DOTLBRACKET`; Atom get rule; Expr set rule |
| `Ast.fs` | Add 2 variants + spanOf | `IndexGet`, `IndexSet` in Expr; spans in spanOf |
| `Diagnostic.fs` | Add 1 error kind | `IndexOnNonCollection` with format |
| `Bidir.fs` | Add 2 cases | IndexGet and IndexSet type checking |
| `Infer.fs` | Add 1 stub | `IndexGet \| IndexSet -> (empty, freshVar())` |
| `Eval.fs` | Add 2 cases | IndexGet and IndexSet evaluation |
| `Format.fs` | Add 2 cases | formatAst for IndexGet and IndexSet |
| `IndentFilter.fs` | Add token to bracket tracking | `DOTLBRACKET` increments BracketDepth |
| `TypeCheck.fs` | Add arms to 4 helpers | collectMatches, collectTryWiths, collectModuleRefs, rewriteModuleAccess |
