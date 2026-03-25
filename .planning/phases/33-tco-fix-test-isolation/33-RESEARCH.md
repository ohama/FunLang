# Phase 33: TCO Fix + Test Isolation - Research

**Researched:** 2026-03-25
**Domain:** F# interpreter TCO (trampoline), Expecto parallel test isolation
**Confidence:** HIGH

## Summary

Phase 33 addresses two independent bugs: broken tail-call optimization (TCO) in
BuiltinValue-wrapped recursive functions, and a race condition in the
MatchCompile module's global test-variable counter that causes non-deterministic
test failures under parallel execution.

**TCO bug:** Phase 30 converted `LetRec`/`LetRecDecl` from `FunctionValue` to
`BuiltinValue + mutable envRef` to fix self-binding inside lambda bodies.
However the change hardcoded `tailPos=false` inside the BuiltinValue wrapper,
bypassing the trampoline. The fix is a one-line change per wrapper: pass
`true` instead of `false` so the body can return `TailCall` values which the
existing trampoline loop already handles correctly.

**Test isolation bug:** `MatchCompile.compileMatch` calls `resetTestVarCounter()`
at the start of every compilation. This resets a module-level mutable counter to
0. When tests run in parallel, test A can have its counter reset mid-compilation
by test B, causing TestVar ID collisions inside decision trees. The result is
that Leaf nodes bind wrong values (e.g., a list instead of an int) causing
runtime type errors. The fix is to eliminate the global counter: make
`freshTestVar` a local closure inside `compileMatch`, and restructure the
module-level `compile` helper to accept it as a parameter.

**Primary recommendation:** Fix TCO by changing `false` to `true` in both
BuiltinValue wrappers; fix test isolation by moving the counter into
`compileMatch` and passing `freshTestVar` as a parameter to `compile`.

## Standard Stack

### Core (already in use, no new packages needed)
| Component | Version | Purpose | Notes |
|-----------|---------|---------|-------|
| Expecto | 10.2.x | Test framework | Already used; `testSequenced` is the isolation primitive |
| F# mutable ref | — | Self-referential BuiltinValue closure | Already used in LetRec/LetRecDecl |

### testSequenced
`testSequenced` wraps a `Test` value with `Sequenced(Synchronous, test)`.
Expecto's runner ensures tests marked Sequenced are never executed concurrently.
`testSequencedGroup` is a variant that serialises within a named group while
allowing other groups to run in parallel — but for this phase the simpler
`testSequenced` wrapper is sufficient for any list of tests that share mutable
global state.

## Architecture Patterns

### TCO Pattern: BuiltinValue with tailPos=true

The existing trampoline lives in `Eval.App`:

```fsharp
// Eval.fs lines 752-767 (read 2026-03-25, no change needed here)
| App (funcExpr, argExpr, _) ->
    let funcVal  = eval recEnv moduleEnv env false funcExpr
    let argValue = eval recEnv moduleEnv env false argExpr
    if tailPos then
        TailCall (funcVal, argValue)
    else
        let mutable result = applyFunc recEnv moduleEnv funcVal argValue funcExpr true
        while (match result with TailCall _ -> true | _ -> false) do
            match result with
            | TailCall (f, a) ->
                result <- applyFunc recEnv moduleEnv f a funcExpr true
            | _ -> ()
        result
```

`applyFunc` is called with `tailPos=true` from both trampoline sites (App and
PipeRight). For `FunctionValue`, it passes this `tailPos` to `eval`:

```fsharp
// Eval.fs lines 559-570 (current)
and applyFunc ... (tailPos: bool) : Value =
    match funcVal with
    | FunctionValue (param, body, closureEnv) ->
        ...
        eval recEnv moduleEnv callEnv tailPos body   // tailPos propagated
    | BuiltinValue fn -> fn argVal                   // tailPos IGNORED
```

The fix: the BuiltinValue wrapper in `LetRec`/`LetRecDecl` must call `eval`
with `true` so the body can return `TailCall`. The existing trampoline loop
already handles any `TailCall` returned by `fn argVal` — nothing else changes.

```fsharp
// BEFORE (broken) — Eval.fs line 777
eval recEnv moduleEnv callEnv false funcBody

// AFTER (fixed)
eval recEnv moduleEnv callEnv true funcBody
```

Apply the same fix at `Eval.fs line 1039` for `LetRecDecl`.

**Why this is safe:** Every caller of `applyFunc` (lines 761, 765, 896, 900)
already runs a while-loop trampoline that catches `TailCall`. If the
BuiltinValue body returns `TailCall(f, a)`, the trampoline continues the loop.
No call site returns a raw `TailCall` to code that does not trampoline.

### Test Isolation Pattern: Local counter in compileMatch

Current broken code (MatchCompile.fs lines 9-16):

```fsharp
let mutable private nextTestVar = 0

let freshTestVar () =
    let v = nextTestVar
    nextTestVar <- nextTestVar + 1
    v

let resetTestVarCounter () = nextTestVar <- 0
```

`compileMatch` calls `resetTestVarCounter()` on line 242. This is a shared
write that races with any concurrent `freshTestVar` reads.

The module-level `compile` function (line 135) also calls `freshTestVar` at
line 157. It is NOT called from outside `compileMatch` — grep confirmed only
two call sites: line 157 (inside `compile`) and line 243 (in `compileMatch`).

Fixed pattern — make the counter local by passing `freshTestVar` to `compile`:

```fsharp
// 1. Change compile signature to accept freshTestVar as a parameter
let rec compile (freshTestVar: unit -> TestVar) (clauses: MatchRow list) : DecisionTree =
    ...
    let freshVars = List.init arity (fun _ -> freshTestVar())  // line 157 unchanged
    ...
    Switch(testVar, ctorName, freshVars, compile freshTestVar yesClauses, compile freshTestVar noClauses)

// 2. In compileMatch: remove resetTestVarCounter, use local counter
let compileMatch (clauses: MatchClause list) : DecisionTree * TestVar =
    let mutable nextVar = 0
    let freshTestVar () =
        let v = nextVar
        nextVar <- nextVar + 1
        v
    // NO resetTestVarCounter() call
    let rootVar = freshTestVar()
    let expandedClauses = expandOrPatterns clauses
    let rows = ...
    let tree = compile freshTestVar rows   // pass local freshTestVar
    (tree, rootVar)
```

This eliminates shared mutable state completely. Each `compileMatch` call has
its own counter. No locking required.

**Alternative: testSequenced wrapper**
If the compile-signature refactor is riskier than expected, wrapping the
affected test list with `testSequenced` also fixes the race:

```fsharp
[<Tests>]
let matchCompileTests = testSequenced (testList "Match Compilation (Decision Tree)" [
    ...
])
```

But the local-counter refactor is cleaner and addresses the root cause; the
`testSequenced` wrapper is a fallback.

### Anti-Patterns to Avoid

- **Passing tailPos through BuiltinValue signature:** The signature is
  `Value -> Value` — do not change it. The fix is inside the closure, not the
  signature.
- **Adding a new Value case (BuiltinTCOValue):** Unnecessary complexity; the
  existing trampoline already handles `TailCall` from any source.
- **Global lock around freshTestVar:** Serialises all match compilation; the
  local-counter approach is zero-overhead and race-free.
- **Keeping resetTestVarCounter() and adding a lock:** Adds overhead with no
  architectural improvement. Delete the global counter instead.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Thread-safe unique IDs | Custom atomic counter or lock | Local `mutable` in closure passed as parameter | Each call site gets independent state; zero concurrency risk |
| Trampoline for BuiltinValue | New trampoline around `fn argVal` | Return `TailCall` from closure body (existing trampoline handles it) | Less code, reuses existing infrastructure |
| Sequential test execution | Test ordering hacks | `testSequenced` (Expecto built-in) | Idiomatic Expecto; integrates with parallel runner |

**Key insight:** The trampoline already handles `TailCall` from any source. The
only gap was that the BuiltinValue wrapper never produced `TailCall` because it
evaluated with `tailPos=false`. Changing that one boolean is the complete fix.

## Common Pitfalls

### Pitfall 1: Only fixing LetRec but not LetRecDecl (or vice versa)
**What goes wrong:** TCO works for local `let rec` but not for top-level
`let rec ... and ...` mutual recursion (or vice versa).
**Why it happens:** Both paths use BuiltinValue with `false`; they are at
different line numbers (777 and 1039).
**How to avoid:** Fix both. The LetRecDecl path is in `evalModuleDecls`, not
`eval`.
**Warning signs:** TCO test for single recursive function passes but mutual
recursion test stack overflows.

### Pitfall 2: Forgetting to update recursive calls in `compile`
**What goes wrong:** `compile` calls itself recursively (line 147 and line 161
in the Switch branch). All recursive calls must pass `freshTestVar` through.
**How to avoid:** Update ALL recursive calls to `compile freshTestVar ...` —
there are two: `compile clauses'.Tail` (fallback) and the two arms of Switch.
**Warning signs:** Compilation error (wrong arity for `compile`).

### Pitfall 3: testSequenced wrapping too narrowly
**What goes wrong:** Wrapping only `fileImportTests` but not `matchCompileTests`
leaves the race on `MatchCompile.nextTestVar`.
**Why it happens:** The file import tests were already wrapped with
`testSequenced` because they write `currentTypeCheckingFile` / `currentEvalFile`.
Match compile tests were not identified as sharing state.
**How to avoid:** The local-counter refactor is preferred because it eliminates
the need for `testSequenced` on match tests entirely.

### Pitfall 4: TailCall leaking out of non-trampolined call sites
**What goes wrong:** Some code path calls `applyFunc` but does not trampoline.
If the BuiltinValue now returns `TailCall`, it propagates as a value.
**Why this does NOT happen here:** All call sites of `applyFunc` in Eval.fs
(lines 761, 765, 896, 900) run a while-loop trampoline. Verified by reading the
source.
**Warning signs:** A `TailCall` value appears in a match result, causing a
"Type error" because TailCall is not IntValue/BoolValue/etc.

### Pitfall 5: Multi-param LetRecDecl TCO
**What goes wrong:** Multi-param `let rec f a b = ... f (a-1) (b+1)` is
desugared to `BuiltinValue(a) -> Lambda("b", body)`. Only the outermost
parameter goes through the BuiltinValue wrapper; the inner `b` argument goes
through `FunctionValue` which already propagates tailPos. The fix to the
BuiltinValue wrapper's body eval (`true` instead of `false`) ensures the
`Lambda("b", ...)` is returned directly (not wrapped in TailCall) and the final
application's tail call is handled by the trampoline. Verified: no additional
changes needed for multi-param.

## Code Examples

### Fix 1: LetRec TCO (Eval.fs ~line 773)

```fsharp
// Source: Eval.fs line 773-780, read 2026-03-25
| LetRec (name, param, funcBody, inExpr, _) ->
    let envRef = ref env
    let wrapper = BuiltinValue (fun argVal ->
        let callEnv = Map.add param argVal !envRef
        eval recEnv moduleEnv callEnv true funcBody)   // CHANGED: false -> true
    let recEnv' = Map.add name wrapper env
    envRef := recEnv'
    eval recEnv moduleEnv recEnv' tailPos inExpr
```

### Fix 2: LetRecDecl TCO (Eval.fs ~line 1034)

```fsharp
// Source: Eval.fs lines 1034-1040, read 2026-03-25
let funcValues =
    bindings |> List.map (fun (name, param, body, _) ->
        let wrapper = BuiltinValue (fun argVal ->
            let currentEnv = !sharedEnvRef
            let callEnv = Map.add param argVal currentEnv
            eval recEnv modEnv callEnv true body)   // CHANGED: false -> true
        (name, wrapper))
```

### Fix 3: MatchCompile local counter (MatchCompile.fs)

Step 1: Change `compile` to accept `freshTestVar` as a parameter.

```fsharp
// Change module-level `compile` signature:
let rec compile (freshTestVar: unit -> TestVar) (clauses: MatchRow list) : DecisionTree =
    match clauses with
    | [] -> Fail
    | _ ->
        let clauses' = clauses |> List.map pushVarBindings
        let first = clauses'.Head
        if Map.isEmpty first.Patterns then
            let fallback =
                if clauses'.Tail.IsEmpty then None
                else Some(compile freshTestVar clauses'.Tail)   // pass through
            Leaf(first.Bindings, first.Guard, first.Body, fallback)
        else
            let testVar = selectTestVariable first clauses'
            let pat = Map.find testVar first.Patterns
            let ctorName, arity = ...
            let freshVars = List.init arity (fun _ -> freshTestVar())  // unchanged
            let yesClauses, noClauses = splitClauses testVar ctorName freshVars clauses'
            Switch(testVar, ctorName, freshVars,
                compile freshTestVar yesClauses,   // pass through
                compile freshTestVar noClauses)    // pass through
```

Step 2: Update `compileMatch` to create and use a local counter.

```fsharp
// Remove the module-level mutable state:
// DELETE: let mutable private nextTestVar = 0
// DELETE: let freshTestVar () = ...
// DELETE: let resetTestVarCounter () = nextTestVar <- 0

let compileMatch (clauses: MatchClause list) : DecisionTree * TestVar =
    // Local counter — no shared state, no race condition
    let mutable nextVar = 0
    let freshTestVar () =
        let v = nextVar
        nextVar <- nextVar + 1
        v
    let rootVar = freshTestVar()
    let expandedClauses = expandOrPatterns clauses
    let rows =
        expandedClauses |> List.mapi (fun i (pattern, guard, body) ->
            { Patterns = Map.ofList [(rootVar, pattern)]
              Guard = guard
              Body = body
              Bindings = Map.empty
              OriginalIndex = i })
    let tree = compile freshTestVar rows   // pass local freshTestVar
    (tree, rootVar)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| FunctionValue for LetRec self-reference | BuiltinValue + mutable envRef | Phase 30-02 | Fixed self-binding in lambda bodies but broke TCO |
| Global MatchCompile counter with reset | (to be fixed) Local counter per call | Phase 33 | Eliminates parallel test race |
| Single `testSequenced` on file import tests | testSequenced on all tests using global state | Phase 31-02 partial | File import tests safe; match compile tests still race |

**Deprecated approach:**
- `resetTestVarCounter()` public API: will be removed; it exists solely to support
  the current global-counter pattern. Once the counter is local, this function
  serves no purpose and should be deleted.

## Open Questions

1. **Is `compile` called from anywhere other than `compileMatch`?**
   - What we know: `grep -n "compile " MatchCompile.fs` shows `compile` is
     called at lines 147, 161, and 252. Lines 147 and 161 are within `compile`
     itself (recursive). Line 252 is `compileMatch`. There are no external
     callers.
   - What's unclear: Nothing — confirmed by source inspection.
   - Recommendation: Safe to add `freshTestVar` parameter to `compile`.

2. **Does wrapping `matchCompileTests` with `testSequenced` suffice as a
   fallback if the local-counter approach is too invasive?**
   - What we know: `testSequenced` prevents parallel execution of the wrapped
     list entirely. The race is between concurrent `compileMatch` calls
     triggered by tests running in parallel.
   - What's unclear: Whether other test lists also call `compileMatch` and
     would still race. `evalModule` (used in ModuleTests, MatchCompileTests,
     GadtTests, etc.) calls `typeCheckModule` which doesn't call `compileMatch`.
     However `Match` expression evaluation calls `compileMatch` at runtime.
     Every test that evaluates a `match` expression could trigger `compileMatch`.
   - Recommendation: Local-counter refactor is the correct fix; it is complete
     and eliminates the shared state entirely. `testSequenced` on
     `matchCompileTests` alone would NOT be sufficient since match compilation
     also happens in other test groups.

3. **Are there other globally mutable counters that could race?**
   - What we know: `Infer.freshVar` uses a module-level `ref 1000`. This is
     used during type checking. If two `typeCheckModule` calls run in parallel
     they share this counter.
   - What's unclear: Whether colliding type variable IDs would cause observable
     test failures (they might cancel out since type inference is scoped locally).
   - Recommendation: Out of scope for Phase 33 unless a new parallel test
     failure implicates it. `Infer.freshVar` is already incremental (not reset)
     so collisions produce non-canonical IDs but usually not incorrect results.
     The `MatchCompile` counter is reset to 0 which causes structural
     corruption.

## Sources

### Primary (HIGH confidence)
- Eval.fs read directly (2026-03-25) — lines 557-570, 752-780, 1026-1046
- MatchCompile.fs read directly (2026-03-25) — lines 1-16, 108-165, 198-253
- TypeCheck.fs read directly (2026-03-25) — lines 590-600, 869-872
- Infer.fs read directly (2026-03-25) — lines 21-28
- tests/LangThree.Tests/ModuleTests.fs read directly (2026-03-25) — evalModule, evalFileModule, testSequenced usage
- tests/LangThree.Tests/MatchCompileTests.fs read directly (2026-03-25)
- Git log + diff commit 3933b30 (2026-03-25) — Phase 30-02 change that introduced the TCO regression
- TestResults/results.trx read directly (2026-03-25) — confirmed failure message and stack trace

### Secondary (MEDIUM confidence)
- Expecto GitHub README / WebFetch (2026-03-25) — `testSequenced` = `Sequenced(Synchronous, test)`
- Expecto issue #87 (WebSearch 2026-03-25) — `testSequencedGroup` distinction

### Tertiary (LOW confidence)
- None required; all critical facts verified from source code directly.

## Metadata

**Confidence breakdown:**
- TCO root cause: HIGH — traced through source code, verified against Phase 30-02 git diff
- TCO fix correctness: HIGH — verified all 4 call sites of `applyFunc` use trampoline
- Test isolation root cause: HIGH — `nextTestVar` global reset in `compileMatch` confirmed; failure reproduced in TestResults/results.trx
- Test isolation fix: HIGH — local mutable pattern is standard F#; `compile` call graph confirmed by source inspection
- Multi-param LetRecDecl edge case: HIGH — traced desugaring path

**Research date:** 2026-03-25
**Valid until:** Stable (internal implementation — no external dependencies changing)
