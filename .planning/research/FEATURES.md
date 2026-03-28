# Feature Landscape: v6.0 Practical Programming

**Domain:** ML-style interpreter — LangThree v6.0 milestone
**Researched:** 2026-03-28
**Milestone focus:** Newline implicit sequencing, for-in collection loops, Option/Result utility functions

---

## Context: What Already Exists

Before categorizing new features, the baseline matters:

| Feature | Status | Notes |
|---------|--------|-------|
| `e1; e2` explicit sequencing (SeqExpr) | Shipped v5.0 | Desugars to `let _ = e1 in e2` |
| `while cond do body` loop | Shipped v5.0 | Returns unit |
| `for i = s to e do body` loop | Shipped v5.0 | Integer ranges only |
| `for i = s downto e do body` loop | Shipped v5.0 | Integer ranges only |
| Option type: `None`, `Some` | Prelude | `optionMap`, `optionBind`, `optionDefault`, `isSome`, `isNone`, `<|>` operator |
| Result type: `Ok`, `Error` | Prelude | `resultMap`, `resultBind`, `resultMapError`, `resultDefault`, `isOk`, `isError` |
| Implicit `in` via offside rule | Shipped (InLetDecl contexts) | F#-style let sequences without `in` |
| Mutable variables (`let mut`) | Shipped v4.0 | `x <- expr` reassignment |

---

## Feature 1: Newline Implicit Sequencing

### What It Is

In F#, inside `do`-blocks and function bodies, expressions at the same indentation level are implicitly sequenced — a newline at the same column acts as `;`. This allows:

```fsharp
// F# — works without semicolons
let printThree () =
    printfn "a"
    printfn "b"
    printfn "c"
```

Without this feature, LangThree currently requires explicit `;`:
```
let printThree () =
    println "a"; println "b"; println "c"
```

With implicit newline sequencing, each line at the same indent level is treated as `e1; e2`:
```
let printThree () =
    println "a"
    println "b"
    println "c"
```

### ML-Family Behavior

| Language | Mechanism | Notes |
|----------|-----------|-------|
| F# | Offside rule: same-column continuations in `do`-blocks auto-sequence | Applies in do-blocks, function bodies, loop bodies |
| OCaml | Explicit `;` required | No implicit newline sequencing — must write `e1; e2` |
| Haskell | `do`-notation with layout rule | `do { stmt1; stmt2 }` desugars via layout |

**F# specifics:**
- In a function body after `=`, each line at the offside column is implicitly `;`-sequenced with the next
- The indented block `INDENT SeqExpr DEDENT` already handles a single expression; the new feature extends `SeqExpr` to treat same-level newlines as sequence separators
- This is the dominant pattern in real F# imperative code
- Only applies in expression contexts (inside a function body, loop body, etc.) — NOT at module top level (where each line is a separate declaration)

### Implementation Approach (IndentFilter-based)

The IndentFilter currently emits INDENT/DEDENT for blocks. For newline sequencing, when the IndentFilter sees a NEWLINE at the same level as the current expression block (no INDENT/DEDENT emitted), it can inject a SEMICOLON token. This mirrors the existing mechanism that injects IN tokens for the offside rule.

The SeqExpr grammar nonterminal already handles `Expr SEMICOLON SeqExpr`, so injected SEMICOLONs are transparent to the parser.

### Table Stakes (Must Have)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Newline-as-semicolon in function bodies | F# convention, ergonomic imperative code | Medium | IndentFilter injects SEMICOLON at same-level newlines in expression contexts |
| Newline-as-semicolon in loop bodies | While/for bodies need multiple statements | Low | Follows same mechanism as function bodies |
| Newline-as-semicolon in match arms | Multi-statement match branches | Low | Match arm body is already an expression context |
| Newline-as-semicolon in if/then/else branches | Multi-statement branches | Low | Same mechanism |
| Module-level declarations NOT affected | Module top-level uses declarations, not sequencing | Critical | Must not inject SEMICOLON between top-level `let` bindings |

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Mixing explicit `;` and newline sequencing | Flexibility — single-line and multi-line styles both work | Low | SeqExpr already handles `e1; e2` — newlines add to this |
| Newline sequencing in let-rec bodies | Recursive function bodies with side effects | Low | Same mechanism |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Module-level newline sequencing | Module top-level contains declarations, not expressions | Keep module context as declarations-only |
| Newline sequencing inside `[...]` list literals | Would break list syntax — `[1\n2]` must not become `[1; 2]` | BracketDepth guard already in IndentFilter suppresses this |
| Newline sequencing inside `{...}` record literals | Same — record field separators are `;`, not newlines | BracketDepth guard handles this |
| Newline sequencing inside `(...)` | Parenthesized expressions should be single units | BracketDepth guard handles this |
| Mandatory newline sequencing | Must remain opt-in via indentation level — explicit `;` still works | Keep both styles |

### Feature Dependencies

```
IndentFilter NEWLINE processing
    → inject SEMICOLON when: same-level, expression context, NOT in brackets
SeqExpr grammar already handles SEMICOLON
    → zero parser changes needed
```

### Critical Behavior: Context Discrimination

The key challenge is knowing when a same-level NEWLINE should become SEMICOLON vs. an implicit IN. The existing system already handles this for InLetDecl contexts (emits IN). The new feature adds: in InExprBlock contexts, same-level NEWLINE emits SEMICOLON.

```
// Module level — no injection (InModule context)
let x = 1      // declaration 1
let y = 2      // declaration 2

// Inside function — SEMICOLON injected (InExprBlock context)
let f () =
    println "a"   // implicit ; before next line
    println "b"   // implicit ; before next line
    42            // final expression value
```

---

## Feature 2: for-in Collection Loop

### What It Is

A loop that iterates over any collection (list, array) directly, binding each element to a variable:

```fsharp
// F# for-in loop
for x in [1; 2; 3] do
    printfn "%d" x

// With arrays
for item in myArray do
    processItem item

// With pattern destructuring (advanced)
for (k, v) in pairs do
    printfn "%s = %d" k v
```

The existing `for i = s to e do` only handles integer ranges. `for x in collection do` handles any list or array.

### ML-Family Behavior

| Language | Syntax | What It Iterates | Pattern Support |
|----------|--------|-----------------|-----------------|
| F# | `for x in expr do body` | IEnumerable (list, array, seq, set, map, ranges, any .NET collection) | Full pattern matching in binding position |
| OCaml | No for-in; use `List.iter f xs` | N/A | N/A |
| Haskell | `forM_ xs (\x -> ...)` or list comprehension | Monadic only; no imperative for-in | N/A |

**F# specifics:**
- `for x in expr do body` where `expr` is any enumerable collection
- Body must return `unit` — this is a statement loop, not a map/comprehension
- Loop variable `x` is immutable (read-only) inside body
- Supports tuple/pattern destructuring in loop variable: `for (a, b) in pairs do`
- The `..` range operator works too: `for i in 1..10 do` (already covered by existing for-to)
- Returns `unit` — same as existing `for i = s to e do`

**LangThree scope:** Lists and Arrays are the two collection types. The feature needs to handle:
1. `for x in list do body` — iterate list elements
2. `for x in array do body` — iterate array elements
3. Ranges via `..` are already handled by existing `for i = s to e do`; `for i in 1..5 do` should desugar to the existing form or be a separate path

### Table Stakes (Must Have)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `for x in list do body` | Primary use case — iterate list | Medium | New AST node or desugar to List.iter |
| `for x in array do body` | Arrays are a first-class collection | Medium | Same mechanism, iterate ArrayValue |
| Loop body returns unit | Consistent with existing for/while | Low | Body type checked as unit |
| Loop variable is immutable | F# semantics, prevents confusion | Low | Excluded from mutableVars set (existing pattern from Phase 42) |
| Returns unit | Consistent with while/for-to | Low | Already established pattern |

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `for (a, b) in pairs do` — tuple pattern destructuring in loop var | Ergonomic for list of tuples | Medium | Pattern binding in loop position |
| `for x in 1..10 do` — range via `..` | Sugar — unifies for-in and for-to syntax | Low | Could desugar to existing ForExpr |
| `for _ in collection do` — wildcard iteration | When count matters but value doesn't | Low | Already works via VarPat/WildcardPat |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| `for x in seq do` — lazy sequence iteration | Seq type not in LangThree | Out of scope — only list/array |
| `for x in map do` — hashtable iteration | Hashtable iteration is `for k in ht.keys` pattern | Provide `Hashtable.keys` and iterate that list |
| List comprehensions (`[for x in xs -> f x]`) | Different construct — not imperative loop | Use `List.map` |
| `yield` inside for-in body | Sequence expressions not in scope | Not building computation expressions |
| Break/continue | Not in F# for-in | F# uses exceptions or boolean mutable for early exit; document pattern |
| Nested for-in with automatic cartesian product | Too complex | Use explicit nesting |

### Feature Dependencies

```
ForInExpr AST node (or desugar to existing pattern)
    → Eval: extract ListValue/ArrayValue, bind var, eval body per element
    → TypeCheck: collection must be 'a list or 'a array; body must be unit
    → Parser: FOR IDENT IN Expr DO body
```

Alternative approach — desugar to `List.iter`:
```
for x in xs do body
→ List.iter (fun x -> body) xs
```
This has zero eval/typecheck changes but requires `List.iter` in scope and loses array support (needs Array.iter path). The AST-node approach is cleaner and more robust.

---

## Feature 3: Option/Result Utility Functions

### Current State

The Prelude already defines:

**Option module (Prelude/Option.fun):**
- `optionMap f opt` — apply f to Some value
- `optionBind f opt` — flatMap (f returns option)
- `optionDefault def opt` — extract with fallback
- `isSome opt`, `isNone opt` — predicates
- `(<|>)` operator — alternative (first Some wins)

**Result module (Prelude/Result.fun):**
- `resultMap f r` — apply f to Ok value
- `resultBind f r` — flatMap (f returns result)
- `resultMapError f r` — transform Error value
- `resultDefault def r` — extract with fallback
- `isOk r`, `isError r` — predicates

### What F# Option Module Provides (Standard Reference)

The full F# `Option` module has 28 functions. The most commonly used in practical ML code:

| Function | Signature | Purpose |
|----------|-----------|---------|
| `Option.map` | `('a -> 'b) -> 'a option -> 'b option` | Transform Some value |
| `Option.bind` | `('a -> 'b option) -> 'a option -> 'b option` | Flatmap / chain |
| `Option.defaultValue` | `'a -> 'a option -> 'a` | Extract or default |
| `Option.defaultWith` | `(unit -> 'a) -> 'a option -> 'a` | Lazy default (thunk) |
| `Option.orElse` | `'a option -> 'a option -> 'a option` | First Some wins |
| `Option.orElseWith` | `(unit -> 'a option) -> 'a option -> 'a option` | Lazy alternative |
| `Option.iter` | `('a -> unit) -> 'a option -> unit` | Side effect on Some |
| `Option.filter` | `('a -> bool) -> 'a option -> 'a option` | Conditional Some/None |
| `Option.get` | `'a option -> 'a` | Unsafe extract (throws on None) |
| `Option.isSome` | `'a option -> bool` | Test predicate |
| `Option.isNone` | `'a option -> bool` | Test predicate |
| `Option.toList` | `'a option -> 'a list` | `[]` or `[x]` |
| `Option.count` | `'a option -> int` | 0 or 1 |
| `Option.exists` | `('a -> bool) -> 'a option -> bool` | Test value with predicate |
| `Option.forall` | `('a -> bool) -> 'a option -> bool` | True if None, else apply predicate |

### What F# Result Module Provides (Standard Reference)

The F# `Result` module has 18 functions. Most commonly used:

| Function | Signature | Purpose |
|----------|-----------|---------|
| `Result.map` | `('a -> 'b) -> Result<'a,'e> -> Result<'b,'e>` | Transform Ok value |
| `Result.bind` | `('a -> Result<'b,'e>) -> Result<'a,'e> -> Result<'b,'e>` | Chain fallible operations |
| `Result.mapError` | `('e -> 'f) -> Result<'a,'e> -> Result<'a,'f>` | Transform Error value |
| `Result.defaultValue` | `'a -> Result<'a,'e> -> 'a` | Extract or default |
| `Result.defaultWith` | `('e -> 'a) -> Result<'a,'e> -> 'a` | Compute default from error |
| `Result.isOk` | `Result<'a,'e> -> bool` | Test predicate |
| `Result.isError` | `Result<'a,'e> -> bool` | Test predicate |
| `Result.toOption` | `Result<'a,'e> -> 'a option` | Convert to Option (Ok → Some, Error → None) |
| `Result.iter` | `('a -> unit) -> Result<'a,'e> -> unit` | Side effect on Ok |
| `Result.exists` | `('a -> bool) -> Result<'a,'e> -> bool` | Test Ok value |
| `Result.forall` | `('a -> bool) -> Result<'a,'e> -> bool` | True if Error, else apply predicate |
| `Result.count` | `Result<'a,'e> -> int` | 1 for Ok, 0 for Error |
| `Result.toList` | `Result<'a,'e> -> 'a list` | `[]` or `[x]` |

### Gap Analysis: What LangThree Prelude Is Missing

**Option — missing from current Prelude:**

| Function | Priority | Why |
|----------|----------|-----|
| `Option.iter` (`optionIter`) | High | Side effects on optional values — very common in imperative code |
| `Option.filter` (`optionFilter`) | High | `if pred x then Some x else None` — common validation pattern |
| `Option.get` (`optionGet`) | Medium | Unsafe extract; useful when caller knows value exists |
| `Option.orElse` (`optionOrElse`) | Medium | Already has `<|>` operator but named function needed for piping |
| `Option.toList` (`optionToList`) | Low | Infrequently needed |
| `Option.count` (`optionCount`) | Low | Rarely needed directly |
| `Option.exists` (`optionExists`) | Low | `Option.map + isSome` equivalent |
| `Option.forall` (`optionForall`) | Low | Infrequently used |

**Result — missing from current Prelude:**

| Function | Priority | Why |
|----------|----------|-----|
| `Result.iter` (`resultIter`) | High | Side effects on successful results |
| `Result.toOption` (`resultToOption`) | High | Convert between error-handling styles — very common interop |
| `Result.mapBoth` / `bimap` | Medium | Map both Ok and Error simultaneously |
| `Result.toList` (`resultToList`) | Low | Rarely needed |
| `Result.count` (`resultCount`) | Low | Rarely needed |
| `Result.exists` (`resultExists`) | Low | Uncommon |
| `Result.forall` (`resultForall`) | Low | Uncommon |

### Naming Convention: Conflict with F# Standard

The current Prelude uses camelCase full-names (`optionMap`, `resultBind`). F# standard uses module-qualified dot notation (`Option.map`, `Result.bind`). LangThree uses `open Option` and `open Result`, so names land directly in scope without module prefix.

**Current pattern:** `optionMap f opt` (curried, function-first)
**F# standard:** `Option.map f opt` (same curried order after `open`)

The current naming is consistent with the rest of the Prelude and works well with pipe:
```
Some 5 |> optionMap double |> optionMap inc
```

This is equivalent to F#'s:
```fsharp
Some 5 |> Option.map double |> Option.map inc
```

**Recommendation:** Keep existing `optionXxx` / `resultXxx` naming convention for consistency with existing tests. Do NOT rename existing functions.

### Table Stakes (Must Have)

| Feature | Priority | Complexity | Notes |
|---------|----------|------------|-------|
| `optionIter` — side effect on Some | High | Low | `fun f opt -> match opt with Some x -> f x \| None -> ()` |
| `optionFilter` — conditional Some/None | High | Low | `fun pred opt -> match opt with Some x -> if pred x then Some x else None \| None -> None` |
| `resultIter` — side effect on Ok | High | Low | `fun f r -> match r with Ok x -> f x \| Error _ -> ()` |
| `resultToOption` — convert Result to Option | High | Low | `fun r -> match r with Ok x -> Some x \| Error _ -> None` |
| Curried, pipeline-friendly signatures | Required | Low | All functions follow `f -> collection -> result` curried order |

### Differentiators (Nice to Have)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `optionGet` — unsafe unwrap | Useful when caller guarantees Some | Low | Should raise exception on None with clear message |
| `optionOrElse` — named alternative function | Complement to `<|>` operator, better for piping | Low | Thin wrapper over existing `<|>` |
| `resultMapBoth` — bimap | Transform both Ok and Error | Low | `fun fOk fErr r -> ...` |
| `optionToList` / `resultToList` | Collection interop | Low | Rarely needed in practice |
| `optionExists` / `optionForall` | Predicate combinators | Low | Equivalent to `optionMap + isSome` |

### Anti-Features (Explicitly NOT Building)

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Computation expressions (`option { ... }`) | Requires computation expression infrastructure not yet built | Use explicit `optionBind` piping |
| `Option.zip` / `Option.map2` | Rarely needed, adds cognitive load | Use explicit pattern matching |
| `Result.combine` / `Result.traverseList` | Complex, belongs in FsToolkit not stdlib | Document for user implementation |
| Renaming existing functions | Would break all existing tests | Additive only: new functions, no renames |
| `optionTry` wrapping exceptions | Mixes exception and option models | Document pattern in tutorial instead |
| Haskell-style `<$>`, `<*>`, `>>=` operators | Unfamiliar to Korean tutorial audience | Use named functions, keep `|>` pipeline |
| `ValueOption` / unboxed option | Performance optimization not needed | Reference semantics sufficient for interpreter |

### Feature Dependencies

```
Option/Result types already defined in Prelude
    → New utility functions are pure .fun additions to Option.fun / Result.fun
    → No changes to Eval.fs, TypeCheck.fs, or Ast.fs needed
    → Zero risk of breaking existing code (additive only)
```

---

## Feature Interaction Matrix

| Feature | Interacts With | Notes |
|---------|---------------|-------|
| Newline sequencing | for-in loops | Loop bodies with multiple lines need sequencing to work |
| Newline sequencing | while loop bodies | Same — multi-line while bodies use newline sequencing |
| Newline sequencing | Option.iter / Result.iter | `optionIter` called on separate lines only works with sequencing |
| for-in loops | Lists/Arrays | Requires existing ListValue/ArrayValue in evaluator |
| Option.iter | for-in loops | Common pattern: `for x in options do optionIter process x` |
| Result.toOption | Option functions | Bridge between error-handling styles |

---

## MVP Priority Order

**Phase 1 — Foundation (blocks everything else):**
1. Newline implicit sequencing (IndentFilter change) — enables multi-line loop/function bodies without explicit `;`

**Phase 2 — New loop construct:**
2. `for x in collection do body` — for-in loop over list/array

**Phase 3 — Library expansion (pure Prelude additions):**
3. `optionIter`, `optionFilter` (most-needed Option additions)
4. `resultIter`, `resultToOption` (most-needed Result additions)
5. Lower-priority additions: `optionGet`, `optionOrElse`, `resultMapBoth`

**Rationale for order:**
- Newline sequencing first: every test for the new features will likely use multi-line bodies; writing those tests is painful without newline sequencing
- for-in second: it is a standalone language change (parser + eval + typecheck)
- Prelude additions last: pure .fun changes, zero risk, can be done in parallel

---

## Complexity Assessment

| Feature | Complexity | Primary Challenge |
|---------|------------|-------------------|
| Newline implicit sequencing | **Medium** | IndentFilter context discrimination: when to inject SEMICOLON vs IN vs nothing. Must not inject at module level. |
| for-in loop | **Medium** | New parser rule + AST node + eval cases for ListValue/ArrayValue dispatch. Type checking the collection type. |
| Option/Result utilities | **Low** | Pure `.fun` Prelude additions. No interpreter changes. |

---

## Sources

- [F# for...in Expression - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-for-in-expression)
- [F# Option Module - FSharp.Core docs](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-optionmodule.html)
- [F# Result Module - FSharp.Core docs](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
- [Options - F# Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/options)
- [OCaml Mutability and Imperative Control Flow](https://ocaml.org/docs/mutability-imperative-control-flow)
- [F# syntax: indentation and verbosity - F# for fun and profit](https://fsharpforfunandprofit.com/posts/fsharp-syntax/)

---

**Document Status:** Research complete for v6.0 milestone
**Confidence Level:** HIGH — all three features have clear precedents in F# standard library and language specification
**Next Step:** Use this feature catalog to define requirements and roadmap phases
