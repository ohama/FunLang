# Domain Pitfalls: LangThree v6.0 Practical Programming Features

**Domain:** Adding newline implicit sequencing, for-in loops, and Option/Result utilities to an existing ML-style interpreter
**Researched:** 2026-03-28
**Context:** LangThree v5.0 baseline — IndentFilter + SeqExpr + ForExpr + BracketDepth all already in place
**Scope:** Pitfalls specific to the v6.0 milestone: (1) newline-based implicit sequencing, (2) `for x in collection do body`, (3) Option/Result Prelude utilities

---

## PART A: Pitfalls for Earlier Milestones (Inherited, Already Resolved)

The original PITFALLS.md covered language implementation pitfalls from v1.0–v5.0. Those are not repeated here. This file focuses exclusively on v6.0 features. The most relevant previously-resolved pitfalls that v6.0 must NOT regress are:

- **Pitfall 1** (INDENT/DEDENT buffering): IndentFilter already handles multi-DEDENT; newline sequencing must not corrupt this.
- **Pitfall 9** (LALR conflicts via offside rule): SeqExpr nonterminal already avoids the semicolon conflict; newline sequencing must not reintroduce conflicts.
- **Prior Phase 45 research** (45-RESEARCH.md): All SeqExpr analysis already done; do not redo.

---

## PART B: Critical Pitfalls for v6.0

Mistakes that cause rewrites or major issues.

### Pitfall V6-1: Newline Sequencing Swallows Multi-Line Function Application

**What goes wrong:** The IndentFilter's `InFunctionApp` context correctly handles:
```
f x
  y    ← continuation: parsed as "f x y" (one application)
```
If newline-based implicit sequencing is added naively, the SAME layout would instead be parsed as:
```
f x
y    ← sequence: parsed as "f x; y" (two statements)
```
This silently changes program semantics — no parse error, wrong behavior.

**Why it happens:** The distinction between "argument on next line" and "next statement on next line" is ambiguous from indentation alone:
- `f x` followed by `y` at same indent = sequence
- `f x` followed by `y` at deeper indent = function application continuation
But `InFunctionApp` detection only fires when `y` is **deeper** than `f`. Code where the next statement happens to be at the SAME indent as the function call hits an edge: is `y` a new statement or a zero-indent continuation?

The current `InFunctionApp` detection in `IndentFilter.fs` (lines 128–138) enters `InFunctionApp` only when `col > topIndent`. A newline token at `col == topIndent` therefore falls through to the normal sequencing path. For the pattern:
```
let _ =
    f x
    y       ← col == 4, same as f x
```
If newline sequencing emits `SEMICOLON` at same-level continuations, `y` becomes a sequence step. But if `y` is meant as an argument to `f`, it was previously parsed as `(f x y)` via `InFunctionApp`.

**Consequences:**
- Code that worked as multi-line applications silently becomes sequencing
- No error is produced — different runtime behavior
- Existing tests in `tests/flt/` that rely on multi-line function application break
- Particularly dangerous for curried functions like `List.fold f init xs` split across lines

**Prevention:**
1. **Do NOT emit implicit SEMICOLON between any two tokens at the same indent when `canBeFunction prevToken && isAtom nextToken`**. The IndentFilter already has `canBeFunction` and `isAtom` predicates for this exact purpose.
2. Reuse the `InFunctionApp` context check: if STILL in a function app context when a same-level newline is seen, do NOT emit a semicolon — it is still a continuation.
3. **The safest rule:** Only emit implicit SEMICOLON when the `PrevToken` is a token that cannot be a function (not `IDENT`, not `RPAREN`) OR when the next token is not an atom. Sequences typically start with keywords (`let`, `if`, `for`, `while`), identifiers that are known statements, or pipe-right `|>` chains.
4. Run ALL existing flt tests for function application after any IndentFilter change.

**Detection:**
- Warning sign: `tests/flt/expr/lambda/` or `tests/flt/file/function/` tests start failing after IndentFilter change.
- Write a specific regression test: `let result = List.fold (fun acc x -> acc + x) 0 [1; 2; 3]` split across lines — must still return 6, not produce a type error or wrong value.

**Phase:** Phase 1 (newline sequencing in IndentFilter) — must be addressed first.

---

### Pitfall V6-2: Implicit Semicolon Double-Firing Inside `let = ... body` Blocks

**What goes wrong:** The offside rule (InLetDecl) and the newline-sequencing rule both fire on the same newline, producing duplicate effects:

```fsharp
let f () =
    x <- 1          ← line 1 of body
    x <- x + 1      ← line 2 of body
```

If newline sequencing emits `SEMICOLON` between line 1 and line 2 at the same indent, AND the offside rule also emits `IN` (because line 2's column equals the `let`'s offside column), the token stream becomes malformed: `...x <- 1 IN SEMICOLON x <- x + 1 IN...` which is a parse error.

**Why it happens:** `IndentFilter.processNewlineWithContext` checks `isAtSameLevel` (no INDENT/DEDENT emitted) to decide whether to fire the offside rule. If newline sequencing also runs in the `isAtSameLevel` branch, both transformations apply simultaneously to the same newline event.

Current code structure (lines 254–282 in IndentFilter.fs): the `isAtSameLevel` branch handles offside `IN` insertion. Newline sequencing would need to fit into this same branch without conflicting.

**Consequences:**
- Parse error on perfectly valid indented blocks
- Offside `IN` and implicit `SEMICOLON` appear in the wrong order
- All `let` bodies with multiple statements break

**Prevention:**
1. **Mutual exclusion:** At any same-level newline: if the offside rule emits `IN`, do NOT also emit `SEMICOLON`. The IN already handles the sequence boundary.
2. **Order of operations:** Check offside first; if it fires, stop processing that newline for sequencing.
3. Newline sequencing should ONLY apply inside `InExprBlock` context — inside an already-established indented expression block. It must NOT apply when `InLetDecl` offside firing is pending.
4. The cleanest model: newline sequencing fires only in `InExprBlock` context (after `= INDENT` opens a block), not at the `InLetDecl` offside column itself.

**Detection:**
- Warning sign: Any multi-statement function body breaks with parse error after IndentFilter change.
- Test `let f () = \n    e1\n    e2` — `e2` must execute as second statement.
- If this produces `syntax error: unexpected IN` or `unexpected SEMICOLON`, both rules fired.

**Phase:** Phase 1 (newline sequencing) — core correctness requirement.

---

### Pitfall V6-3: Implicit Semicolon Before `else` / `with` / `|` Terminators

**What goes wrong:** Newline sequencing emits a SEMICOLON before a token that should close a branch, not start a new statement:

```fsharp
if cond then
    doSomething ()
else                  ← should NOT have SEMICOLON before "else"
    doSomethingElse ()
```

```fsharp
match x with
| A -> doA ()
| B ->                ← PIPE should NOT be preceded by implicit SEMICOLON
    doB ()
```

If the IndentFilter emits `SEMICOLON` before `ELSE`, `WITH`, `PIPE`, the parser sees `doSomething (); else ...` which is a grammar error.

**Why it happens:** `ELSE`, `WITH`, and `PIPE` tokens at the same indent level as the preceding expression look, structurally, like "the next statement in a sequence." But they are structural terminators, not expression starts.

Note that the current IndentFilter ALREADY handles `ELSE` correctly (lines 196–201): it suppresses `INDENT` before `ELSE`. Newline sequencing must apply the same logic for SEMICOLON emission.

**Consequences:**
- `if/then/else` completely breaks
- Match expressions with multiple arms break (every arm after the first fails)
- Try/with expressions break

**Prevention:**
1. **Terminator token list:** Before emitting an implicit SEMICOLON, check if the next token is a structural terminator: `ELSE`, `WITH`, `PIPE`, `IN`, `THEN`, `DO`, `DEDENT`, `EOF`. If so, do not emit.
2. `PIPE` is the trickiest: it can start a match arm (terminator context) or appear mid-expression in `a || b` patterns. Use context: if in `InMatch` or `InTry` context, `PIPE` is a terminator; otherwise it is an operator. The existing context stack already tracks `InMatch` and `InTry`.
3. Reuse the existing `nextToken` lookahead already computed in `processNewlineWithContext` — it is already available when deciding what to emit.

**Detection:**
- Warning sign: Tests with if/else or match break immediately after IndentFilter change.
- Test: Simple `if true then 1 else 2` split across lines must still work.
- Test: Three-arm match must produce all three arms, not a parse error after arm 1.

**Phase:** Phase 1 (newline sequencing) — must be addressed before any other work.

---

### Pitfall V6-4: `for x in collection` Conflicts with `for i = s to e` Grammar

**What goes wrong:** Adding `for x in collection do body` as a new grammar rule creates a shift/reduce conflict with the existing `for i = s to e do body` rule because both start with `FOR IDENT` and the parser cannot determine which production to use until it sees `IN` vs `EQUALS`.

In an LALR(1) parser, the one-token lookahead after `FOR IDENT` sees either `EQUALS` (integer range loop) or `IN` (collection loop). This is only one token away from disambiguating, but if IDENT binds to a common production early, a reduce/reduce or shift/reduce conflict can appear depending on the grammar structure.

**Why it happens:** fsyacc is LALR(1). After `FOR IDENT`, it has a conflict between:
- `ForExpr: FOR IDENT EQUALS Expr TO Expr DO SeqExpr`
- `ForInExpr: FOR IDENT IN Expr DO SeqExpr`

The discriminating token (`EQUALS` vs `IN`) is at position 3, which IS within the single-token lookahead from the point of the `FOR IDENT` reduce. However, the conflict depends on whether `IDENT` is first reduced to something or kept as a terminal — in LR parsing, both rules share the same `FOR IDENT` prefix, which means the parser can simply wait for the third token. This SHOULD be unambiguous for LALR(1) since no reduction of `IDENT` is needed before seeing `EQUALS` or `IN`.

**The real risk:** `IN` is already used as a keyword in `let x = e IN body`. If `in` appears in a collection loop context, the parser must not confuse `for x in collection` with `let x = ... in ...` — specifically, does `for x in` shift `IN` as "for-in loop start" or try to reduce via some IN-related rule? Since `FOR` is not part of any `let` production, this should be safe, but needs explicit verification.

**Consequences:**
- fsyacc prints "shift/reduce conflict" or "reduce/reduce conflict" during build
- Parser silently chooses wrong production (fsyacc defaults to shift)
- `for x in list` mis-parses as `for x = ...` or vice versa

**Prevention:**
1. **Verify experimentally:** After adding the new grammar rule, check fsyacc output for conflict warnings. Build with verbose fsyacc output enabled.
2. **IDENT before IN is safe:** Because neither `FOR IDENT` reduces to anything in the existing grammar — both loops must read more tokens before any reduction. LALR(1) should handle this without conflict.
3. **Explicit token test:** Add a test with `for x in [1; 2; 3] do body` immediately after adding the grammar rule, before anything else. If it parses, there is no conflict.
4. **Do not reuse `IN` token for for-in loop.** Consider whether a fresh keyword is needed. But `in` is the natural ML keyword here and OCaml/F# both use it — it should be fine since the parser context (after `FOR IDENT`) is unambiguous.

**Detection:**
- Build output from `dotnet build` includes fsyacc warnings — watch for any conflict messages.
- Parser test: `for x in [1]` must parse, `for i = 1 to 3` must still parse, both in the same file.

**Phase:** Phase 2 (for-in loop parser) — check for conflicts immediately.

---

### Pitfall V6-5: `for x in collection` Loop Variable Scoping in Type Checker

**What goes wrong:** The loop variable `x` in `for x in collection do body` is typed incorrectly — either (a) given the type of the collection instead of the element type, (b) allowed to be reassigned inside the body (should be immutable), or (c) not added to the type environment for the body.

**Why it happens:** The existing `ForExpr` in Bidir.fs (lines 181–195) handles integer range loops: it types `startExpr` and `stopExpr` as `TInt` and binds `var` as `Scheme([], TInt)`. For `for x in collection`, the element type must be extracted from the collection's type:
- `TList(elemTy)` → `x : elemTy`
- `TArray(elemTy)` → `x : elemTy`

If the type checker does `synth collection` and gets `TList(TVar 0)` but then binds `x : TVar 0` (the variable, not a monomorphic copy), unification later might accidentally generalize `x` or leave it as a type variable instead of an int.

Also, the loop variable must be excluded from `mutableVars` (same as the integer range loop variable — see the "For-loop variable immutability via mutableVars exclusion" Key Decision in PROJECT.md). Forgetting this allows `x <- newValue` inside the body, which would be type-correct but semantically wrong (the loop variable is not a ref cell).

**Consequences:**
- `for x in [1; 2; 3] do println (to_string x)` fails to typecheck with type variable error
- `for x in items do x <- modified` incorrectly accepted
- Type mismatch errors with misleading messages about list vs element types

**Prevention:**
1. **Extract element type from collection type:** Pattern match on the synthesized type of `collectionExpr`:
   - `TList elemTy` → bind `x : Scheme([], elemTy)` in body environment
   - `TArray elemTy` → bind `x : Scheme([], elemTy)` in body environment
   - Otherwise → type error "for-in requires list or array"
2. **Do NOT put `x` in `mutableVars`** — identical to the integer range loop treatment.
3. **Force monomorphic binding:** Use `Scheme([], elemTy)` not `Scheme([tv], TVar tv)` — the loop variable is not polymorphic.
4. **Body returns unit:** The type checker must unify the body type with `TTuple []`, same as integer for-loops and while-loops.

**Detection:**
- Test: `for x in [1; 2; 3] do println (to_string x)` — must print 1, 2, 3.
- Test: `for x in ["a"; "b"] do println x` — must print a, b.
- Test: `for x in [1; 2; 3] do x <- 0` — must produce E0320 immutable assignment error.
- Test: Body type mismatch: `for x in [1] do 42` — must error "body must return unit".

**Phase:** Phase 2 (for-in type checker) — required for correctness.

---

### Pitfall V6-6: `for x in collection` Evaluator Handles Wrong Value Types

**What goes wrong:** The `ForInExpr` evaluator case only handles `ListValue` but not `ArrayValue`, or vice versa. Or it incorrectly handles an empty list by crashing instead of silently doing nothing.

**Why it happens:** The existing `ForExpr` evaluator (Eval.fs lines 766–777) handles only `IntValue` range. A new `ForInExpr` case must handle both `ListValue` and `ArrayValue`. Forgetting one means runtime errors that don't reflect the program logic — they reflect evaluator incompleteness.

The empty-collection case must be a no-op (loop executes zero times) — not an error. Developers often forget to test the empty case.

**Consequences:**
- `for x in [||] do body` crashes with match failure instead of silently skipping
- `for x in array do body` fails where `for x in list do body` works — asymmetric
- Confusing runtime errors about value types instead of logical errors

**Prevention:**
1. Handle both cases in the evaluator:
   ```fsharp
   | ListValue elems ->
       for elem in elems do
           let env' = Map.add var elem env
           eval recEnv moduleEnv env' false body |> ignore
       TupleValue []
   | ArrayValue arr ->
       for elem in arr do
           let env' = Map.add var elem env
           eval recEnv moduleEnv env' false body |> ignore
       TupleValue []
   | _ -> failwith "for-in: collection must be a list or array"
   ```
2. Empty list/array must be tested explicitly — they loop zero times and return `TupleValue []`.
3. The loop variable must be bound freshly for each iteration (Map.add, not mutation).

**Detection:**
- Test with empty list: `for x in [] do println "never"` — must produce no output, return `()`.
- Test with array: `for x in [|1; 2; 3|] do ...`
- Test mixing: type checker accepts `for x in array`, evaluator also handles it.

**Phase:** Phase 2 (for-in evaluator) — correctness requirement.

---

## PART C: Moderate Pitfalls for v6.0

Mistakes that cause delays or technical debt.

### Pitfall V6-7: Newline Sequencing Ambiguity with Multi-Line Operators

**What goes wrong:** Code like:

```fsharp
let result =
    x + y
    + z        ← is this "x + y" SEMICOLON "+z" (error), or "x + y + z" (continuation)?
```

In most languages, a line starting with a binary operator signals continuation. But with newline sequencing, `+ z` at the same indent would be treated as a new sequence step — which is not a valid expression (unary `+` on `z` is unusual; worse, `+z` is a type error if `z` is not an int, but `+` on its own fails).

Similar with pipe chains:
```fsharp
let result =
    xs
    |> List.map f    ← must be continuation, not sequence
    |> List.filter g
```

**Why it happens:** `|>` starts a line and is a binary operator that needs a left argument. Without a rule saying "lines starting with binary operators are continuations," the implicit sequencing would treat `|> List.map f` as a standalone expression — which is a parse error (binary operator without left operand).

**Consequences:**
- Pipe chains spanning multiple lines break
- Standard F#-style operator chaining breaks
- Users get confusing parse errors about unexpected `|>`

**Prevention:**
1. **Operator-continuation rule:** Do NOT emit implicit SEMICOLON if the next line starts with a binary operator token (`|>`, `>>`, `<<`, `+`, `-`, `*`, `/`, `|`, `&&`, `||`, `<`, `>`, `=`, infix operators generally).
2. In the IndentFilter, check `nextToken` before emitting SEMICOLON: if next token is an infix operator, it is a continuation, not a new statement.
3. This rule is additive with the terminator rule (Pitfall V6-3): both suppress SEMICOLON emission.

**Detection:**
- Test: `let r = xs |> List.map f \n    |> List.filter g` — must parse as one pipeline.
- Test: `let r = a + b \n    + c` — should this be continuation or sequence? Document the design decision explicitly. F# treats leading operators as continuation; this is the safer choice.

**Phase:** Phase 1 (newline sequencing) — design decision required upfront.

---

### Pitfall V6-8: Option/Result Utility Functions Clash with Existing Prelude Names

**What goes wrong:** New Option/Result functions added to Prelude (`Option.map`, `Option.bind`, `Result.map`, etc.) conflict with existing user-defined functions in programs that already define their own `map` or `bind`.

**Why it happens:** The Prelude is loaded via `open "Prelude/Option.fun"` style directives. If a user program defines `let map f xs = ...` at module level and the Prelude also defines `map`, one of two bad things happens:
1. User's `map` shadows Prelude's `map` — silently, with no warning
2. Prelude's `map` shadows user's — wrong function is called

This is the standard "shadowing" problem in module systems. LangThree's existing Prelude already has this tension with `List.map`, `Array.map`, etc.

**Consequences:**
- Programs that define their own `map`/`bind` break after Prelude loading
- Shadowing is silent — wrong function called with no type error if signatures match
- Functions with same name but different arity produce confusing type errors

**Prevention:**
1. **Namespace all Prelude utilities:** Use `Option.map`, `Option.bind`, `Option.getOrDefault` (qualified names) not top-level `map`, `bind`. Users access them via `open Option` or `Option.map`.
2. **Follow existing Prelude pattern:** `Array.fun` in `Prelude/Array.fun` exposes `array_iter`, `array_map` (with prefix). The Option/Result utilities should use a similar prefix (`option_map`, `result_bind`) OR be in their own module (`Option`).
3. **Test:** Load Prelude, then define `let map f x = f x`, verify no unexpected behavior.

**Detection:**
- Warning sign: A user program with `let bind f x = ...` breaks after adding Option utilities.
- Test: Open Prelude, call `Option.map`, then define local `let map = ...` — must coexist.

**Phase:** Phase 3 (Option/Result Prelude) — namespace before implementing.

---

### Pitfall V6-9: Result Type Defined in Prelude Conflicts with Exhaustiveness Checker

**What goes wrong:** If `Result` type is defined in a Prelude file (`type Result<'a, 'e> = Ok of 'a | Error of 'e`), the exhaustiveness checker and pattern matching compilation may not recognize it as a sum type unless the ADT is properly registered in the constructor environment.

**Why it happens:** LangThree's exhaustiveness checker (`Exhaustive.fs`, `MatchCompile.fs`) uses the constructor environment to check that all branches of a match are covered. ADTs defined in user code are registered when their `TypeDecl` is processed. But Prelude ADTs loaded via file import must also be registered — if the import pipeline does not fully evaluate type declarations from imported files (just values), the `Result` type's constructors `Ok` and `Error` are unknown to the exhaustiveness checker.

**Consequences:**
- `match r with | Ok v -> ... | Error e -> ...` produces spurious "non-exhaustive match" warnings
- Pattern matching on `Result` requires a catch-all `| _ -> ...` even when it should be exhaustive
- Developer confusion about whether their match is actually complete

**Prevention:**
1. **Verify Prelude type declarations are processed:** When `open "Prelude/Result.fun"` is executed, the `TypeDecl` for `Result` must be registered in the `ConstructorEnv`. Check that the existing file-import pipeline in `TypeCheck.fs` does this for type declarations, not just value bindings.
2. **Test exhaustiveness after Prelude load:** After defining `Result` in a Prelude file, write a match that covers both `Ok` and `Error` — the checker must not warn.
3. **Alternatively:** Define `Result` as a built-in type with built-in constructor info, similar to how `bool` is treated.

**Detection:**
- Write `match (Ok 1) with | Ok v -> v | Error _ -> 0` — must type-check without warning.
- If exhaustiveness checker warns "non-exhaustive" on a complete two-arm match, type declarations are not registered.

**Phase:** Phase 3 (Option/Result Prelude) — verify type registration.

---

### Pitfall V6-10: `ForInExpr` Variable Captured by Closures Inside Loop Body

**What goes wrong:** Closures defined inside a `for x in collection do body` capture `x` by reference rather than by value, causing all closures to see the last value of `x` when eventually called:

```fsharp
let handlers = [||]
for x in [1; 2; 3] do
    let h = fun () -> x    // intends to capture current x
    handlers.[...] <- h
// All handlers return 3, not 1, 2, 3
```

**Why it happens:** In LangThree's evaluator, the loop variable `x` is bound via `Map.add var elem env` for each iteration. Each closure captures the immutable `env` at the time of its creation, which includes `x` as an `IntValue` (or whatever type). Since F# `Map` is immutable and the environment is a value (not a reference), each closure captures a DIFFERENT copy of the environment with the current value of `x`.

This means LangThree **does not have** the classic closure-in-loop bug that JavaScript/Python have with `var` — because the environment is copied, not shared.

However, if `x` were implemented as a `RefValue` (like mutable variables), ALL closures would share the same ref cell. For an immutable loop variable, this would be wrong.

**The risk:** If during implementation the loop variable is accidentally added to `mutableVars` or implemented as a `RefValue`, this bug appears.

**Consequences:**
- All closures inside the loop see the final value of `x`
- Logic errors that are hard to diagnose
- Behavior difference between list iteration and integer iteration

**Prevention:**
1. **Loop variable is immutable, bound as value:** Use `Map.add var elem env` where `elem` is the actual value (not a `RefValue`). This guarantees each iteration gets its own value in a separate environment copy.
2. **Do NOT add loop variable to `mutableVars`:** Same rule as integer for-loops.
3. **Test closure capture:** Create closures inside a loop, collect them, call them all — each must return its iteration's value.

**Detection:**
- Test: `let fs = ref [] in for x in [1; 2; 3] do fs := (fun () -> x) :: !fs; let results = List.map (fun f -> f ()) !fs` — must return `[3; 2; 1]` (reversed), not `[3; 3; 3]`.

**Phase:** Phase 2 (for-in evaluator) — correctness property.

---

### Pitfall V6-11: `option_map` / `option_bind` Need Correct Polymorphic Type Schemes

**What goes wrong:** `Option.map : ('a -> 'b) -> 'a option -> 'b option` requires a polymorphic type scheme in the type environment. If the type scheme is written incorrectly (e.g., monomorphic `(TVar 1 -> TVar 2) -> TData("Option", [TVar 1]) -> TData("Option", [TVar 2])` without proper generalization), the function works for the first use but fails for subsequent uses at different types.

**Why it happens:** LangThree's `TypeCheck.fs` maintains a `TypeEnv` (map from name to `Scheme`). Built-in functions are registered with their schemes. If the scheme for `option_map` uses specific `TVar` integers that are later used in unification, those "reused" type variable IDs clash with freshly allocated ones, causing spurious type errors.

The existing Prelude built-ins (like `array_map`) work because they are either (a) polymorphically typed from a Prelude `.fun` file where the type is inferred, or (b) explicitly registered with correct `Scheme([0; 1], ...)` where `0` and `1` are the universally quantified variable IDs.

**Consequences:**
- `option_map to_string (Some 1)` works, but `option_map string_length (Some "x")` fails with a type unification error
- First use "locks in" the type, second use fails
- Confusing error messages about `int` vs `string` in Option context

**Prevention:**
1. **Define Option/Result in `.fun` files and let type inference handle polymorphism.** This is the safest approach — type inference generalizes correctly.
2. **If registering as built-ins:** Use unique, high-numbered `TVar` IDs that are distinct from inference-generated IDs, OR ensure `freshVar()` in the unifier always generates IDs above any manually-assigned ones.
3. **Test:** Call `option_map` with at least two different element types in the same program.

**Detection:**
- Test: `option_map to_string (Some 1)` then `option_map string_length (Some "hello")` in same program — both must work.
- If second call fails, the scheme is not properly polymorphic.

**Phase:** Phase 3 (Option/Result Prelude) — type scheme correctness.

---

## PART D: Minor Pitfalls for v6.0

Mistakes that cause annoyance but are fixable.

### Pitfall V6-12: `for x in string` Silently Accepted or Confusingly Rejected

**What goes wrong:** Users write `for c in "hello" do ...` expecting character-by-character iteration (as in Python or F#). LangThree does not support string iteration — strings are not `TList(TChar)`. The type checker would reject this with a confusing error about `TString` not matching `TList('a)`.

**Why it happens:** Strings in LangThree are `StringValue`, not `ListValue`. The `for-in` loop type-checks the collection and pattern-matches on `TList elemTy | TArray elemTy`. `TString` does not match either, so an error is produced.

**Prevention:**
1. **Give a clear error message:** "for-in: `string` is not iterable; use `string_to_chars` to convert, or iterate indices with `for i = 0 to string_length s - 1 do`"
2. **Do NOT silently accept:** Do not coerce `string` to `char list` implicitly.
3. **Document the limitation:** Tutorial must show the workaround pattern.

**Detection:**
- Test: `for c in "abc" do ()` — must produce a clear type error, not a crash.

**Phase:** Phase 2 (for-in type checker) — error message quality.

---

### Pitfall V6-13: Implicit Newline Sequencing Breaks REPL Multi-Line Input

**What goes wrong:** In the REPL, a user types `f x` and presses Enter expecting to type `y` as an argument on the next line. If newline sequencing is active, Enter now terminates the expression. The REPL evaluates `f x` immediately, then `y` as a separate expression.

**Why it happens:** The REPL (Repl.fs) reads lines and runs them through the same IndentFilter pipeline. If newline sequencing emits SEMICOLON between lines, multi-line expressions in the REPL break.

**Consequences:**
- REPL UX degrades significantly
- Users cannot write multi-line expressions interactively
- A previously-working pattern (multi-line function application in REPL) stops working

**Prevention:**
1. **The REPL may need a different IndentFilter configuration:** Use the BracketDepth mechanism (already in place) and require users to use explicit `;` or brackets for REPL multi-line input.
2. **Alternatively:** In REPL mode, newline sequencing is disabled — explicit semicolons required. Document this difference clearly.
3. **Pragmatic choice:** File mode gets implicit sequencing; REPL requires explicit `;;` or semicolons. This is the OCaml REPL convention.

**Detection:**
- Manual REPL test: type `List.fold \n    (fun acc x -> acc + x) \n    0 \n    [1; 2; 3]` — must either work or fail with a clear message about explicit semicolons needed.

**Phase:** Phase 1 (newline sequencing) — REPL mode exception.

---

### Pitfall V6-14: `option_getOrDefault` vs `option_defaultValue` Naming Convention

**What goes wrong:** The new utility function is named inconsistently with the rest of the Prelude. Different functions use different styles:
- `array_map`, `array_fold` (snake_case with module prefix)
- `hashtable_get`, `hashtable_keys` (snake_case with module prefix)
- `string_length`, `string_concat` (snake_case with module prefix)

If Option/Result functions follow a DIFFERENT naming convention (`getOrDefault`, `defaultValue`, `mapOption`), users have to remember two styles.

**Prevention:**
1. Follow the established pattern: `option_map`, `option_bind`, `option_get_or_default`, `option_is_some`, `option_is_none`.
2. If using module syntax: `Option.map`, `Option.bind` — which requires module support in Prelude loading.
3. Pick one and document it clearly. Do not mix.

**Detection:**
- Check existing Prelude files for naming convention before writing any new functions.
- The convention is `module_verb` or `module_noun`, all snake_case.

**Phase:** Phase 3 (Option/Result Prelude) — naming convention.

---

### Pitfall V6-15: `result_bind` Propagates `Error` Incorrectly in Chains

**What goes wrong:** `result_bind f (Error e)` should return `Error e` unchanged. If implemented as `match r with | Ok v -> f v | Error e -> Error e`, it is correct. But if the error type is lost (e.g., `Error e` becomes `Error (Error e)` due to double-wrapping), monad chains fail silently.

**Why it happens:** Copy-paste error in the Prelude implementation: `| Error e -> Error (f e)` instead of `| Error e -> Error e`.

**Prevention:**
1. Test the three monad laws: (a) `result_bind (fun x -> Ok x) (Ok v) = Ok v`, (b) `result_bind f (Ok v) = f v`, (c) associativity.
2. Specifically test error propagation: `result_bind (fun x -> Ok (x + 1)) (Error "oops") = Error "oops"`.

**Detection:**
- Test: `(Error "fail") |> result_bind (fun x -> Ok (x + 1)) |> result_bind (fun x -> Ok (x * 2))` — must return `Error "fail"`, not `Error (Error "fail")`.

**Phase:** Phase 3 (Option/Result Prelude) — correctness test.

---

## Phase-Specific Warnings

| Phase | Topic | Likely Pitfall | Mitigation |
|-------|-------|---------------|------------|
| Phase 1 | Newline sequencing in IndentFilter | Function application broken (V6-1) | Check `canBeFunction`/`isAtom` before emitting SEMICOLON |
| Phase 1 | Newline sequencing in IndentFilter | Double-emission with offside rule (V6-2) | Mutual exclusion: IN emission suppresses SEMICOLON |
| Phase 1 | Newline sequencing in IndentFilter | SEMICOLON before `else`/`with`/`|` (V6-3) | Terminator token list: skip SEMICOLON before structural closers |
| Phase 1 | Newline sequencing in IndentFilter | Operator continuation breaks (V6-7) | Lines starting with infix operators are continuations, not statements |
| Phase 1 | Newline sequencing in IndentFilter | REPL multi-line input breaks (V6-13) | REPL mode uses different config or explicit `;;` convention |
| Phase 2 | for-in grammar | LALR conflict with `for i = ... to` (V6-4) | IDENT followed by IN vs EQUALS is 1-token-lookahead, should be fine; verify with build |
| Phase 2 | for-in type checker | Element type extraction (V6-5) | Pattern-match synthesized type on TList/TArray; exclude from mutableVars |
| Phase 2 | for-in type checker | String iteration (V6-12) | Clear error message pointing to workaround |
| Phase 2 | for-in evaluator | Missing ArrayValue case (V6-6) | Handle both ListValue and ArrayValue; test empty collection |
| Phase 2 | for-in evaluator | Closure capture (V6-10) | Use Map.add (immutable bind), not RefValue |
| Phase 3 | Option/Result Prelude | Name collision with user code (V6-8) | Namespace with prefix or module; follow existing Prelude convention |
| Phase 3 | Option/Result Prelude | Type declaration not registered (V6-9) | Verify ADT constructors enter ConstructorEnv when Prelude is loaded |
| Phase 3 | Option/Result Prelude | Monomorphic type scheme (V6-11) | Define in .fun files for inferred polymorphism, or use correct Scheme(tvars, ty) |
| Phase 3 | Option/Result Prelude | Naming convention (V6-14) | Use `option_X` snake_case consistent with existing builtins |
| Phase 3 | Option/Result Prelude | `result_bind` error propagation (V6-15) | Test monad laws; specifically `Error e` unchanged through bind |

---

## Risk Summary

**Highest risk area (likely needs deeper phase-specific research):**
Phase 1 — Newline sequencing. The IndentFilter already has significant complexity (BracketDepth, InFunctionApp, InLetDecl offside, InExprBlock, InMatch, InTry). Adding newline sequencing to this state machine without breaking existing behavior requires careful specification of exactly when a newline becomes a SEMICOLON vs a continuation. The interaction with InFunctionApp (V6-1) is the most dangerous because it changes program semantics silently.

**Medium risk area:**
Phase 2 — for-in loop. The grammar addition is low-risk (LALR(1) should handle it), but the type checker integration (extracting element types from collection types) requires careful handling of TList vs TArray polymorphism.

**Lower risk area:**
Phase 3 — Option/Result Prelude. Following established patterns from Array.fun and Hashtable.fun reduces risk. Main danger is naming consistency and polymorphic type schemes.

---

## Sources

- Direct inspection of `IndentFilter.fs` — `canBeFunction`, `isAtom`, `processNewlineWithContext`, `isAtSameLevel`, BracketDepth mechanism
- Direct inspection of `Parser.fsy` — `SeqExpr`, `ForExpr`, `InFunctionApp` grammar
- Direct inspection of `Bidir.fs` — `ForExpr` type checker, `mutableVars` exclusion pattern
- Direct inspection of `Eval.fs` — `ForExpr` evaluator, `ListValue`/`ArrayValue` handling
- Direct inspection of `45-RESEARCH.md` — Phase 45 pitfalls (already resolved), offside interaction analysis
- Project history via `PROJECT.md` Key Decisions table — closure capture, mutableVars, SeqExpr, DOTLBRACKET patterns
- F# language reference: `for x in collection do` semantics (F# loop variable is immutable)
- OCaml manual: sequence expression (`seq_expr` nonterminal), for-in semantics
