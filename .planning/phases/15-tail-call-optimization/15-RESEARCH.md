# Phase 15: Tail Call Optimization - Research

**Researched:** 2026-03-19
**Domain:** Tree-walking interpreter TCO via trampoline pattern in F#
**Confidence:** HIGH

## Summary

This research covers how to add tail call optimization (TCO) to LangThree's tree-walking interpreter without CPS-transforming the entire evaluator. The standard approach for tree-walking interpreters is the **trampoline pattern**: introduce a special `TailCall` value variant that defers evaluation, then add a loop at the call site (`App`) that repeatedly evaluates until a non-`TailCall` value is produced.

The key insight is that TCO in a tree-walking interpreter does NOT require modifying the `eval` function signature or introducing continuation-passing style. Instead, it requires two things: (1) detecting when an expression is in tail position and (2) returning a `TailCall` sentinel value instead of recursively calling `eval`, letting the caller's trampoline loop handle the iteration.

**Primary recommendation:** Add a `TailCall` case to the `Value` DU, thread a `tailPos: bool` parameter through `eval`, and add a trampoline loop in the `App` case that unwraps `TailCall` values iteratively.

## Standard Stack

No new libraries needed. This is a pure interpreter-architecture change using existing F# constructs.

### Core
| Component | Purpose | Why Standard |
|-----------|---------|--------------|
| F# Discriminated Union | `TailCall` value variant | Native F# pattern; zero overhead for pattern matching |
| F# `while` loop | Trampoline unwrap loop | Simple imperative loop avoids .NET stack growth |
| `tailPos: bool` parameter | Tail position tracking | Minimal change to eval signature; widely used in interpreters |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `TailCall` Value variant | .NET `tail.` IL prefix | Only works for F#-compiled tail calls, NOT for interpreted language calls |
| `tailPos` parameter | Separate `evalTail` function | Duplicates eval logic; harder to maintain |
| Trampoline loop in App | CPS-transform entire evaluator | Massive refactor, slower for non-tail calls, harder to debug |
| Mutable loop | F# `Async`/computation expression trampoline | Over-engineered; simple while loop is clearer and faster |

## Architecture Patterns

### Pattern 1: TailCall Value Variant

**What:** Add a new case to the `Value` discriminated union that captures a deferred function call.
**When to use:** Returned by `eval` when evaluating a function application in tail position.

```fsharp
// In Ast.fs - add to Value DU
| TailCall of func: Value * arg: Value
```

This is intentionally minimal. It stores the function value and the already-evaluated argument. The trampoline loop in `App` unwraps it.

### Pattern 2: Tail Position Parameter

**What:** Add `tailPos: bool` parameter to `eval` that indicates whether the current expression is in tail position.
**When to use:** Threaded through all eval calls. Set to `true` only in specific positions.

```fsharp
// Modified eval signature
and eval (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>) (env: Env) (tailPos: bool) (expr: Expr) : Value =
```

Tail position rules for LangThree expressions:
- **`Let(name, binding, body, _)`**: `binding` is NOT tail position; `body` IS tail position (inherits parent's tailPos)
- **`LetPat(pat, binding, body, _)`**: same as Let
- **`LetRec(name, param, funcBody, inExpr, _)`**: `inExpr` IS tail position (inherits); `funcBody` is NOT (it's a lambda body, evaluated later)
- **`If(cond, thenBr, elseBr, _)`**: `cond` is NOT tail position; both branches ARE tail position (inherit)
- **`Match(scrutinee, clauses, _)`**: `scrutinee` is NOT; clause bodies ARE tail position (inherit)
- **`App(func, arg, _)`**: When `tailPos=true`, return `TailCall` instead of recursing
- **`TryWith(body, handlers, _)`**: body is NOT tail position (exception handler needs stack frame); handler bodies ARE
- **`PipeRight(left, right, _)`**: equivalent to `App(right, left)`, same tail position logic
- **`And/Or`**: right operand inherits tail position (but these return BoolValue, not function calls, so moot)
- **All other expressions**: Leaf nodes (Number, Bool, String, Var, Lambda, etc.) just return values; tailPos doesn't affect them

### Pattern 3: Trampoline Loop in App

**What:** After evaluating a function call, check if the result is `TailCall`. If so, loop.
**When to use:** In the `App` case of `eval`, and in `PipeRight`.

```fsharp
// In App case - the trampoline
| App (funcExpr, argExpr, _) ->
    let funcVal = eval recEnv moduleEnv env false funcExpr
    let argValue = eval recEnv moduleEnv env false argExpr
    let mutable currentFunc = funcVal
    let mutable currentArg = argValue
    let mutable result = applyFunction recEnv moduleEnv env currentFunc currentArg funcExpr tailPos
    while (match result with TailCall _ -> true | _ -> false) do
        match result with
        | TailCall (nextFunc, nextArg) ->
            result <- applyFunction recEnv moduleEnv env nextFunc nextArg funcExpr true
        | _ -> ()
    result
```

### Pattern 4: Extract Apply Helper

**What:** Factor out function application logic into a helper to avoid duplication between `App`, `PipeRight`, and the trampoline loop.

```fsharp
// Helper: apply a function value to an argument
let applyFunction recEnv moduleEnv env funcVal argVal funcExpr tailPos =
    match funcVal with
    | FunctionValue (param, body, closureEnv) ->
        let augmentedClosureEnv =
            match funcExpr with
            | Var (name, _) -> Map.add name funcVal closureEnv
            | _ -> closureEnv
        let callEnv = Map.add param argVal augmentedClosureEnv
        if tailPos then
            // In tail position: evaluate body in tail position
            eval recEnv moduleEnv callEnv true body
        else
            // Not in tail position: evaluate normally, unwrap any TailCall
            let result = eval recEnv moduleEnv callEnv false body
            result
    | BuiltinValue fn -> fn argVal
    | _ -> failwith "Type error: attempted to call non-function"
```

**Critical insight:** When `tailPos=true` in the `App` case, the inner `eval` of the function body also runs with `tailPos=true`. If that body ends with another function call (e.g., recursive `loop (n-1)`), it will return `TailCall` instead of recursing deeper. The trampoline loop in the OUTERMOST `App` catches it.

### Anti-Patterns to Avoid
- **CPS-transforming the evaluator:** Massive refactor that makes the entire codebase harder to read and debug. The trampoline pattern achieves the same goal with surgical changes.
- **Trying to use .NET `tail.` IL instruction:** This only applies to the F# compiler's own compilation of F# code. Our interpreter's `eval` function is F# code, but the *interpreted language's* recursion happens within `eval`'s own call to itself -- .NET has no visibility into that.
- **Making ALL calls trampolined:** Only tail-position calls need trampolining. Non-tail calls should evaluate normally. Making all calls trampolined adds overhead with no benefit.
- **Forgetting TryWith:** The body of `try-with` is NOT in tail position because the .NET exception mechanism needs the stack frame. This is a common mistake.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Tail position analysis | Full AST pass/annotation | Inline `tailPos` bool parameter | Simpler, no separate pass needed |
| Stack-safe recursion | Custom continuation monad | Simple while loop + TailCall DU | While loop is maximally efficient in .NET |
| Mutual recursion TCO | Complex multi-function trampoline | Skip for now (Phase 18 scope) | Mutual recursion not yet in language |

**Key insight:** The trampoline pattern for a tree-walking interpreter is remarkably simple -- approximately 20-30 lines of changed code in the core `App` case, plus threading `tailPos` through existing `eval` calls. Do not over-engineer this.

## Common Pitfalls

### Pitfall 1: Forgetting to Pass tailPos=false in Non-Tail Positions
**What goes wrong:** If arithmetic operands or `let` bindings accidentally get `tailPos=true`, a function call inside `let x = f() in ...` could return `TailCall` to a context that doesn't expect it, producing wrong results or crashes.
**Why it happens:** Mechanical error when threading the parameter through ~30 match cases.
**How to avoid:** Default is `tailPos=false`. Only explicitly pass `true` for the specific sub-expressions identified in the tail position rules above.
**Warning signs:** Test `let x = f 1 in x + 1` returns `TailCall` value instead of integer.

### Pitfall 2: TailCall Leaking Out of Eval
**What goes wrong:** `TailCall` values escape into user-visible results (printed as output, stored in data structures, etc.).
**Why it happens:** A `TailCall` is returned from eval but no trampoline loop catches it.
**How to avoid:** The trampoline loop MUST exist at every entry point: `App`, `PipeRight`, `evalMatchClauses` (for guarded clauses that call functions), top-level `evalExpr`, and `evalModuleDecls`. Add an assertion/unwrap at `evalExpr` and `evalModuleDecls`.
**Warning signs:** Runtime errors about unexpected `TailCall` value type.

### Pitfall 3: TryWith Body in Tail Position
**What goes wrong:** If `try-with` body is marked as tail position, the trampoline unwinds the call before the exception handler is installed, so exceptions from the tail call won't be caught.
**Why it happens:** Intuition says "body of try is the last thing" but the exception handler needs the stack frame.
**How to avoid:** `TryWith` body MUST always be evaluated with `tailPos=false`. Handler bodies CAN be in tail position.
**Warning signs:** Exception handling tests fail after TCO is added.

### Pitfall 4: MatchCompile evalDecisionTree Not Updated
**What goes wrong:** Match expressions compiled via decision tree bypass the tail position optimization because `evalDecisionTree` calls `evalFn` without passing `tailPos`.
**Why it happens:** The `evalFn` callback type in `MatchCompile` is `Env -> Expr -> Value`, which doesn't include `tailPos`.
**How to avoid:** Update the callback signature to `Env -> bool -> Expr -> Value` and pass `tailPos` through the Leaf case body evaluation.
**Warning signs:** Tail-recursive functions using pattern matching still overflow.

### Pitfall 5: Self-Reference in Recursive Calls
**What goes wrong:** The current `App` case has special logic to add the function name to the closure env (`augmentedClosureEnv`). When the trampoline loop iterates, `funcExpr` from the original call site is used, but subsequent iterations have a raw `TailCall(func, arg)` without the original `funcExpr`.
**Why it happens:** The trampoline loop loses the `funcExpr` context after the first iteration.
**How to avoid:** Include the function name in `TailCall` variant, OR ensure the recursive function is already in the closure (which `LetRec` already does by adding `name -> funcVal` to env). Verify that `LetRec` closure already contains self-reference.
**Warning signs:** Infinite loop or "undefined variable" in recursive calls.

### Pitfall 6: CustomEquality/CustomComparison on Value
**What goes wrong:** Adding `TailCall` to Value DU requires updating `valueEqual`, `valueCompare`, `GetHashCode`, and `formatValue`.
**Why it happens:** Value has `CustomEquality` and `CustomComparison` attributes.
**How to avoid:** Add `TailCall` cases to all these functions. `TailCall` should never be compared (it's transient), so just return `false`/`0`/`"<tailcall>"`.
**Warning signs:** Compiler errors about incomplete pattern matches.

## Code Examples

### Example 1: TailCall Value Variant Addition

```fsharp
// In Ast.fs Value DU, add:
| TailCall of func: Value * arg: Value

// In Value.valueEqual, add:
| TailCall _, _ | _, TailCall _ -> false

// In Value.valueCompare, add:
| TailCall _, _ | _, TailCall _ -> 0

// In Value.GetHashCode, add:
| TailCall _ -> 0

// In Eval.fs formatValue, add:
| TailCall _ -> "<tailcall>"
```

### Example 2: Modified eval Signature and Let Case

```fsharp
and eval (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>) (env: Env) (tailPos: bool) (expr: Expr) : Value =
    match expr with
    // Leaf nodes - tailPos doesn't matter
    | Number (n, _) -> IntValue n
    | Bool (b, _) -> BoolValue b
    | String (s, _) -> StringValue s

    // Let: binding is NOT tail, body inherits tailPos
    | Let (name, binding, body, _) ->
        let value = eval recEnv moduleEnv env false binding
        let extendedEnv = Map.add name value env
        eval recEnv moduleEnv extendedEnv tailPos body

    // If: condition NOT tail, branches inherit tailPos
    | If (condition, thenBranch, elseBranch, _) ->
        match eval recEnv moduleEnv env false condition with
        | BoolValue true -> eval recEnv moduleEnv env tailPos thenBranch
        | BoolValue false -> eval recEnv moduleEnv env tailPos elseBranch
        | _ -> failwith "Type error: if condition must be boolean"
```

### Example 3: App Case with Trampoline

```fsharp
    | App (funcExpr, argExpr, _) ->
        let funcVal = eval recEnv moduleEnv env false funcExpr
        let argValue = eval recEnv moduleEnv env false argExpr
        // Apply function, potentially getting TailCall back
        let mutable result = applyFunc recEnv moduleEnv funcVal argValue funcExpr
        // Trampoline: unwrap TailCall chain iteratively
        while (match result with TailCall _ -> true | _ -> false) do
            match result with
            | TailCall (f, a) ->
                result <- applyFunc recEnv moduleEnv f a funcExpr
            | _ -> ()
        result
```

Where `applyFunc` evaluates the body with `tailPos=true` so nested tail calls return `TailCall` rather than recursing.

### Example 4: LetRec Self-Reference Verification

```fsharp
    // Current LetRec already adds self to env:
    | LetRec (name, param, funcBody, inExpr, _) ->
        let funcVal = FunctionValue (param, funcBody, env)
        let recFuncEnv = Map.add name funcVal env
        eval recEnv moduleEnv recFuncEnv tailPos inExpr
    // The closure of funcVal captures `env` (without self), but at App time,
    // augmentedClosureEnv adds name->funcVal. This works for direct recursion.
    // For TCO: the trampoline loop calls applyFunc with the same funcExpr,
    // so self-reference is maintained.
```

### Example 5: Test Cases

```
(* TCO-01: Simple tail recursion - must not stack overflow *)
let rec loop n = if n = 0 then 0 else loop (n - 1) in loop 1000000
(* Expected: 0 *)

(* TCO-02: Tail recursion with accumulator *)
let rec sum n acc = if n = 0 then acc else sum (n - 1) (acc + n) in sum 1000000 0
(* Expected: 500000500000 ... wait, int overflow. Use smaller: sum 100000 0 = 5000050000 ... also overflow for 32-bit *)
(* Better: let rec sum n acc = if n = 0 then acc else sum (n - 1) (acc + 1) in sum 1000000 0 *)
(* Expected: 1000000 *)

(* TCO-03: Non-tail recursion should still work (but may overflow for large n) *)
let rec fact n = if n = 0 then 1 else n * fact (n - 1) in fact 10
(* Expected: 3628800 *)

(* TCO-04: Tail call in match expression *)
let rec countdown n = match n with 0 -> "done" | n -> countdown (n - 1) in countdown 1000000
(* Expected: "done" *)

(* TCO-05: Tail call through pipe *)
(* This depends on whether PipeRight is treated as App for TCO *)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| CPS transform entire evaluator | TailCall variant + trampoline loop | Standard since ~2010 | Much less invasive, same stack safety |
| .NET tail. IL instruction | Manual trampoline for interpreted languages | Always | .NET tail prefix only helps compiled F# code, not interpreted language recursion |
| No TCO (current LangThree) | TailCall + tailPos parameter | This phase | Enables stack-safe recursion for 1M+ depth |

## Open Questions

1. **Multi-argument tail calls (curried functions)**
   - What we know: `let rec f x y = ... f a b` desugars to `App(App(f, a), b)`. The outer `App` is in tail position, but the inner `App(f, a)` is NOT (it produces a partial application).
   - What's unclear: Does this just work? The inner App returns a FunctionValue (partial), the outer App calls it in tail position. This should be fine -- only the outermost call needs to be tail.
   - Recommendation: Test with multi-argument recursive function. Should work without special handling.

2. **PipeRight TCO**
   - What we know: `x |> f` is semantically `App(f, x)`. Currently has its own eval case.
   - What's unclear: Should PipeRight reuse the same trampoline logic as App?
   - Recommendation: Yes. Factor out `applyFunc` and reuse. `PipeRight` in tail position should also produce `TailCall`.

3. **Performance impact on non-tail calls**
   - What we know: Adding `tailPos` parameter to every `eval` call adds minor overhead.
   - What's unclear: Exact performance impact.
   - Recommendation: Negligible. A single bool parameter is trivial. The trampoline loop in App only activates when TailCall is returned.

4. **MatchCompile callback signature change**
   - What we know: `evalDecisionTree` takes `Env -> Expr -> Value` callback.
   - Recommendation: Change to `Env -> bool -> Expr -> Value` to thread `tailPos` through match clause body evaluation. This is the cleanest approach.

## Sources

### Primary (HIGH confidence)
- LangThree source code: `Eval.fs`, `Ast.fs`, `MatchCompile.fs` -- direct inspection
- [Ink TCE implementation](https://dotink.co/posts/tce/) -- tree-walking interpreter TCO via thunks
- [Eli Bendersky: On Recursion, Continuations and Trampolines](https://eli.thegreenplace.net/2017/on-recursion-continuations-and-trampolines/) -- trampoline pattern fundamentals

### Secondary (MEDIUM confidence)
- [John Azariah: Bouncing Around with Recursion](https://johnazariah.github.io/2020/12/07/bouncing-around-with-recursion.html) -- F# trampoline DU pattern
- [Wikipedia: Tail call](https://en.wikipedia.org/wiki/Tail_call) -- tail position definitions

### Tertiary (LOW confidence)
- [Pavel Volgarev: Tail Recursion and Trampolining in C#](https://volgarev.me/2013/09/27/tail-recursion-and-trampolining-in-csharp.html) -- .NET context

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - trampoline pattern is well-established for tree-walking interpreters
- Architecture: HIGH - direct inspection of LangThree codebase confirms approach viability
- Pitfalls: HIGH - derived from code analysis (CustomEquality, MatchCompile callback, TryWith)
- Tail position rules: HIGH - standard functional language semantics, verified against codebase AST

**Research date:** 2026-03-19
**Valid until:** Indefinite (fundamental interpreter technique; LangThree codebase is the moving part)
