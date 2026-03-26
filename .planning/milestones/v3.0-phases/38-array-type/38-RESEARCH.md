# Phase 38: Array Type - Research

**Researched:** 2026-03-25
**Domain:** F# tree-walking interpreter — adding a new mutable Value case with builtin module
**Confidence:** HIGH

## Summary

Phase 38 adds fixed-size mutable arrays to LangThree. The work spans five files: `Ast.fs` (new `ArrayValue` DU case), `Type.fs` (new `TArray` type constructor), `Unify.fs` / `Bidir.fs` / `Infer.fs` (propagate `TArray`), `TypeCheck.fs` (add Array module type signatures), `Eval.fs` (add Array module runtime bindings and `formatValue` arm).

The array module is delivered identically to the `List` prelude pattern: a `module Array = ...` block in a new `Prelude/Array.fun` file is not applicable here because the operations require mutation and F# backing — they must be BuiltinValue entries registered in a module-shaped `ModuleValueEnv`. The correct pattern is a **Prelude `.fun` file that wraps thin user-land helpers around native builtins** OR a **purely native module registered at startup** before prelude loads. Because `Array.create`, `Array.get`, `Array.set`, `Array.length` all need direct access to a mutable F# array, they must be BuiltinValue entries wired up in F# code (not `.fun` script), similarly to how `string_length`, `read_file`, etc. are in `initialBuiltinEnv`. The module shape (`Array.create` qualified access) is then built by placing those bindings inside a `ModuleValueEnv` entry in `initialBuiltinEnv` — **or** by loading a tiny `Prelude/Array.fun` that declares `module Array = ...` using the native flat builtins.

The cleanest approach that matches existing patterns: add six flat `array_create`, `array_get`, … builtins to `initialBuiltinEnv` / `initialTypeEnv`, then expose them as qualified `Array.*` via a `Prelude/Array.fun` file exactly as `List.fun` does (module wrapper + `open Array` at bottom). This requires zero changes to the module system and leverages the already-working prelude pipeline.

**Primary recommendation:** Add `ArrayValue of Value array ref` to the Value DU, add `TArray` to Type, wire the six flat builtins in F#, expose them through `Prelude/Array.fun` as a `module Array` wrapper, and add `open Array` at the bottom.

## Standard Stack

### Core (all already in the project — no new packages)
| Component | Location | Purpose | Notes |
|-----------|----------|---------|-------|
| `Ast.Value` DU | `src/LangThree/Ast.fs` | Carry array at runtime | Add `ArrayValue of Value array ref` |
| `Type.Type` DU | `src/LangThree/Type.fs` | Array type constructor | Add `TArray of Type` |
| `Eval.initialBuiltinEnv` | `src/LangThree/Eval.fs` | Register six native functions | Flat names `array_create` … |
| `TypeCheck.initialTypeEnv` | `src/LangThree/TypeCheck.fs` | Register type signatures | Same flat names |
| `Prelude/Array.fun` | `Prelude/Array.fun` | Expose as `Array.*` + `open Array` | New file, same shape as `List.fun` |

### Supporting
| Component | Location | Purpose | When to Use |
|-----------|----------|---------|-------------|
| `Unify.unifyWithContext` | `src/LangThree/Unify.fs` | Unify two `TArray t` | One new match arm |
| `Type.apply` / `freeVars` / format | `src/LangThree/Type.fs` | Substitute / print `TArray` | Three one-liners |
| `Bidir.synth` non-function guard | `src/LangThree/Bidir.fs` | Reject applying an array as a function | Add `TArray _` to the guard |
| `Eval.formatValue` | `src/LangThree/Eval.fs` | Print array as `[|e1; e2; ...|]` | New match arm |
| `Ast.Value.GetHashCode` / `valueEqual` | `src/LangThree/Ast.fs` | CustomEquality obligation | New match arms |

**Installation:** none — no new NuGet packages required.

## Architecture Patterns

### Recommended Project Structure (changes only)
```
src/LangThree/
├── Ast.fs           # +ArrayValue of Value array ref
├── Type.fs          # +TArray of Type (+ apply, freeVars, format)
├── Unify.fs         # +TArray unification arm
├── Bidir.fs         # +TArray in non-function guard
├── Infer.fs         # no changes needed (arrays come from builtins only)
├── Elaborate.fs     # no changes needed (no array type-expression syntax)
├── Eval.fs          # +ArrayValue arm in formatValue, valueEqual, GetHashCode
│                    # +six BuiltinValue entries in initialBuiltinEnv
└── TypeCheck.fs     # +six Scheme entries in initialTypeEnv
Prelude/
└── Array.fun        # module Array = ... (wraps flat builtins) + open Array
```

### Pattern 1: Adding a New Value Case With Custom Equality

Every new `Value` DU case must handle all three custom equality/comparison obligations.
`ArrayValue` uses physical identity for equality (two different arrays are never equal even if same contents — matches F# semantics for mutable ref cells).

```fsharp
// In Ast.fs — Value DU cases (after TailCall)
| ArrayValue of Value array ref  // Phase 38: mutable fixed-size array

// GetHashCode
| ArrayValue r -> hash (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(r))

// valueEqual
| ArrayValue r1, ArrayValue r2 -> System.Object.ReferenceEquals(r1, r2)

// valueCompare (Ast.valueCompare is used for < > only; arrays are not comparable)
| ArrayValue _, _ | _, ArrayValue _ -> 0
```

`Value array ref` (an F# ref cell wrapping an array) makes the entire array replaceable, matching the existing `RecordValue` pattern of `Map<string, Value ref>`. However since arrays are fixed-size and we never reassign the whole array (only elements), using `Value array` directly (no outer `ref`) is simpler. `Array.set` mutates in place without needing to replace the outer container. Use `ArrayValue of Value array` (no ref).

### Pattern 2: Adding a New Type Constructor

TArray follows TList exactly. Every place TList appears must gain TArray:

```fsharp
// Type.fs
| TArray of Type   // 'a array

// formatType / formatTypeNormalized
| TArray t -> sprintf "%s array" (formatType t)

// apply
| TArray t -> TArray (apply s t)

// freeVars
| TArray t -> freeVars t

// collectVars (in formatTypeNormalized)
| TArray t -> collectVars acc t
```

### Pattern 3: Flat Builtins Surfaced Through Module Wrapper

This is the established pattern. Native operations are registered as `array_create` (flat, in `initialBuiltinEnv`), then `Prelude/Array.fun` aliases them:

```
// Prelude/Array.fun
module Array =
    let create n default = array_create n default
    let get arr i       = array_get arr i
    let set arr i v     = array_set arr i v
    let length arr      = array_length arr
    let ofList xs       = array_of_list xs
    let toList arr      = array_to_list arr

open Array
```

The `module Array = ...` block causes the module system to build a `ModuleValueEnv` entry with `Values` = `{create=..., get=..., ...}`. After `open Array`, the names are also available unqualified. This is identical to how `List.fun` exposes `map`, `filter`, etc.

### Pattern 4: Curried Multi-Argument Builtins

Three-argument builtins (`array_set arr i v`) use nested `BuiltinValue` wrappers:

```fsharp
// In Eval.fs initialBuiltinEnv
"array_set", BuiltinValue (fun arrVal ->
    BuiltinValue (fun idxVal ->
        BuiltinValue (fun newVal ->
            match arrVal, idxVal with
            | ArrayValue arr, IntValue i ->
                if i < 0 || i >= arr.Length then
                    failwithf "Array.set: index %d out of bounds (length %d)" i arr.Length
                arr.[i] <- newVal
                TupleValue []   // unit
            | _ -> failwith "Array.set: expected array and int")))
```

### Pattern 5: Array.create Semantics

F# `Array.create n x` creates an array of length `n` with all elements set to `x`. The default value is evaluated once and shared — this is correct for immutable values (int, bool, string). For record values this means all slots point to the same `RecordValue` ref cells, which may surprise users. Document this (standard F# behaviour).

```fsharp
"array_create", BuiltinValue (fun nVal ->
    BuiltinValue (fun defVal ->
        match nVal with
        | IntValue n when n >= 0 ->
            ArrayValue (Array.create n defVal)
        | IntValue n -> failwithf "Array.create: negative size %d" n
        | _ -> failwith "Array.create: expected int"))
```

### Pattern 6: Array.ofList / Array.toList

```fsharp
"array_of_list", BuiltinValue (fun v ->
    match v with
    | ListValue xs -> ArrayValue (Array.ofList xs)
    | _ -> failwith "Array.ofList: expected list")

"array_to_list", BuiltinValue (fun v ->
    match v with
    | ArrayValue arr -> ListValue (Array.toList arr)
    | _ -> failwith "Array.toList: expected array")
```

### Pattern 7: Type Signatures for Polymorphic Builtins

TVar 0 is the conventional first generic variable in `Scheme`:

```fsharp
// TypeCheck.fs initialTypeEnv additions
// array_create : int -> 'a -> 'a array
"array_create", Scheme([0], TArrow(TInt, TArrow(TVar 0, TArray (TVar 0))))
// array_get : 'a array -> int -> 'a
"array_get",    Scheme([0], TArrow(TArray (TVar 0), TArrow(TInt, TVar 0)))
// array_set : 'a array -> int -> 'a -> unit
"array_set",    Scheme([0], TArrow(TArray (TVar 0), TArrow(TInt, TArrow(TVar 0, TTuple []))))
// array_length : 'a array -> int
"array_length", Scheme([0], TArrow(TArray (TVar 0), TInt))
// array_of_list : 'a list -> 'a array
"array_of_list", Scheme([0], TArrow(TList (TVar 0), TArray (TVar 0)))
// array_to_list : 'a array -> 'a list
"array_to_list", Scheme([0], TArrow(TArray (TVar 0), TList (TVar 0)))
```

### Anti-Patterns to Avoid

- **Hand-rolling a prelude Array module using only user-land code:** arrays require mutation at the F# level; this cannot be written in `.fun` without the native builtins underneath.
- **Wrapping the whole array in a `Value ref`:** `ArrayValue of Value array ref` adds indirection with no benefit. Use `ArrayValue of Value array` directly; mutation of individual elements (`arr.[i] <- v`) does not need the outer ref.
- **Using `ResizeArray` (dynamic list) as the backing store:** the requirement says _fixed-size_. Use `Value array` (F# fixed-size array).
- **Using `TData("array", [t])` instead of a dedicated `TArray t`:** TData causes the type system to treat arrays as a user-defined ADT which requires a ConstructorEnv entry and breaks unification. Add `TArray` to the Type DU to keep it structurally typed, consistent with `TList`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Mutable F# array | Custom linked-list mutation | `Value array` + `arr.[i] <- v` | O(1) get/set, matches the fixed-size requirement |
| Array display | Custom formatter | F# `Array.toList` + existing list formatter pattern | Consistent with `formatValue` |
| Module qualified access | Custom parser/dispatch for `Array.*` | Existing `ModuleValueEnv` + prelude `.fun` wrapper | Already proven by `List.fun`; zero new infrastructure |
| Bounds checking | Custom bounds logic | `if i < 0 || i >= arr.Length` in each builtin | Two-line guard in each BuiltinValue, idiomatic |

**Key insight:** the module system, custom equality infrastructure, and prelude pipeline are already general enough. This phase is entirely additive — no existing mechanism needs modification, only extension.

## Common Pitfalls

### Pitfall 1: Missing CustomEquality Obligations
**What goes wrong:** Compiler error "The struct, record, or union type 'Value' uses 'CustomEquality' but does not define the 'op_Equality' method" or exhaustiveness warning on `GetHashCode`/`valueEqual`/`valueCompare`.
**Why it happens:** `Value` carries `[<CustomEquality; CustomComparison>]` which requires explicit handling for every new case.
**How to avoid:** After adding `ArrayValue` to the DU, immediately add the case to: (1) `GetHashCode`, (2) `valueEqual`, (3) `valueCompare`, (4) `formatValue`.
**Warning signs:** F# compiler error E0001 or missing match arm warning.

### Pitfall 2: Forgetting TArray in Type.fs Propagation Points
**What goes wrong:** Type-checking crashes with "unmatched pattern" or silently produces wrong types when `apply`, `freeVars`, or `formatType` encounters `TArray`.
**Why it happens:** `TArray` is a new DU case; every `match` on `Type` must handle it.
**How to avoid:** Search for all `match ... with | TList` patterns in `Type.fs`, `Unify.fs`, `Bidir.fs`, `Infer.fs`, and add `TArray` arms next to each `TList` arm.
**Warning signs:** F# incomplete match warning at compile time.

### Pitfall 3: Forgetting TArray in the Bidir.fs "not a function" Guard
**What goes wrong:** Applying an array value as a function produces a confusing type error rather than the correct "not a function" error.
**Why it happens:** The guard in `Bidir.synth` for `App` checks `| TInt | TBool | TString | TTuple _ | TList _ | TData _ ->` and raises `NotAFunction`. Without `TArray _` in the list, the match falls through and produces `OccursCheck` or `UnifyMismatch`.
**How to avoid:** Add `| TArray _` to that guard line.

### Pitfall 4: Array.create Shares Default Value Reference
**What goes wrong:** `let a = Array.create 3 { x = 0 }` creates three slots all pointing to the same `RecordValue` ref map. Mutating `a.[0].x` also mutates `a.[1].x`.
**Why it happens:** `Array.create n defVal` stores `defVal` reference `n` times — standard F# behaviour.
**How to avoid:** This is correct F# semantics. Document it in error message or comments. If independence is desired users must do `Array.init` (a possible future builtin).
**Warning signs:** Test with a record default — expect shared-reference behaviour.

### Pitfall 5: Array.get/set out-of-bounds F# Exception Leaks
**What goes wrong:** An unguarded `arr.[i]` in `array_get` throws an F# `System.IndexOutOfRangeException` that surfaces as an ugly .NET exception message.
**How to avoid:** Add an explicit bounds check in each of `array_get` and `array_set` before indexing, and `failwithf` with a readable message (which becomes a `LangThreeException` caught by the user's try-with).

### Pitfall 6: Type Annotation Syntax for Arrays Not Supported
**What goes wrong:** User writes `(arr : int array)` and gets a parse error.
**Why it happens:** `TEArray` does not exist in `Ast.TypeExpr`; the parser does not recognise `T array` as a type expression.
**How to avoid:** This phase does not need to add type annotation syntax for arrays (ARR-01 through ARR-06 do not require it). The type checker will infer `TArray` from the builtin signatures. Leave `TEArray` for a future phase. Document the limitation.

## Code Examples

### Registering a Two-Argument Builtin in initialBuiltinEnv
```fsharp
// Source: Eval.fs existing pattern (e.g., string_concat, write_file)
"array_get", BuiltinValue (fun arrVal ->
    BuiltinValue (fun idxVal ->
        match arrVal, idxVal with
        | ArrayValue arr, IntValue i ->
            if i < 0 || i >= arr.Length then
                failwithf "Array.get: index %d out of bounds (length %d)" i arr.Length
            arr.[i]
        | _ -> failwith "Array.get: expected (array, int)"))
```

### Registering a Three-Argument Builtin (array_set)
```fsharp
// Source: Eval.fs existing pattern (e.g., string_sub)
"array_set", BuiltinValue (fun arrVal ->
    BuiltinValue (fun idxVal ->
        BuiltinValue (fun newVal ->
            match arrVal, idxVal with
            | ArrayValue arr, IntValue i ->
                if i < 0 || i >= arr.Length then
                    failwithf "Array.set: index %d out of bounds (length %d)" i arr.Length
                arr.[i] <- newVal
                TupleValue []
            | _ -> failwith "Array.set: expected (array, int)")))
```

### Formatting ArrayValue
```fsharp
// Source: Eval.fs formatValue (TupleValue and ListValue as models)
| ArrayValue arr ->
    let formattedElements = arr |> Array.toList |> List.map formatValue
    sprintf "[|%s|]" (String.concat "; " formattedElements)
```

### Prelude/Array.fun (full file)
```
module Array =
    let create n def = array_create n def
    let get arr i    = array_get arr i
    let set arr i v  = array_set arr i v
    let length arr   = array_length arr
    let ofList xs    = array_of_list xs
    let toList arr   = array_to_list arr

open Array
```

### Unify.fs arm for TArray
```fsharp
// Source: Unify.fs TList arm (line 54)
| TArray t1, TArray t2 ->
    unifyWithContext ctx trace span t1 t2
```

### flt integration test skeleton
```
// Test: Array.create and Array.set mutation
// --- Command: /path/to/LangThree %input
// --- Input:
let arr = Array.create 3 0
let _ = Array.set arr 1 42
let result = Array.get arr 1
// --- Output:
42
```

## State of the Art

| Old Approach | Current Approach | Notes |
|--------------|------------------|-------|
| All builtins as flat global names | Flat names wrapped in prelude module | `List.fun` established this since Phase 2 v3.0 |
| No mutation primitives | `SetField` in-place mutation via `Value ref` | Established in Phase Records-06 |

**Deprecated/outdated:**
- Attempting to implement `Array.set` as a pure function returning a new array — the requirement explicitly says in-place mutation (ARR-03).

## Open Questions

1. **Type annotation syntax `'a array`**
   - What we know: `TEData` exists and `substTypeExprWithMap` handles `TEData("array", [te])` as `TData("array", [t])`. This is different from our new `TArray`.
   - What's unclear: should the parser recognise `T array` as a postfix type constructor (like `T list`)? The current Lexer/Parser does support `T list` as `TEList`.
   - Recommendation: skip type annotation support in this phase. Users annotate `int list` today; they can use `array_create` without annotations. Add `TEArray` only if a future phase needs it.

2. **Equality semantics for ArrayValue**
   - What we know: `RecordValue` uses structural equality on its `Map<string, Value ref>` via `Map` equality (which compares the `ref` objects by reference, since `Map` compares keys and values with `=`, and `ref` equality compares addresses).
   - What's unclear: should two arrays with identical contents be `=`? F# arrays use reference equality by default.
   - Recommendation: use reference equality (`System.Object.ReferenceEquals`) in `valueEqual`. This matches F# semantics and avoids O(n) comparison on every `=` expression.

3. **`valuesEqual` in Eval.fs (duplicate of `Ast.Value.valueEqual`)**
   - What we know: `Eval.fs` defines a separate `valuesEqual` function (line ~447) that is used in `matchPattern` for `ConstPat`. It handles `ListValue`, `TupleValue`, etc. but must also handle `ArrayValue`.
   - Recommendation: add `ArrayValue _ , ArrayValue _ -> false` (or reference equality) to `valuesEqual` in Eval.fs alongside the Ast change.

## Sources

### Primary (HIGH confidence)
- Direct code reading: `src/LangThree/Ast.fs` — Value DU, CustomEquality implementation
- Direct code reading: `src/LangThree/Type.fs` — Type DU, all propagation points
- Direct code reading: `src/LangThree/Unify.fs` — unification patterns
- Direct code reading: `src/LangThree/Bidir.fs` — non-function guard
- Direct code reading: `src/LangThree/Eval.fs` — initialBuiltinEnv, formatValue, valuesEqual, ModuleValueEnv
- Direct code reading: `src/LangThree/TypeCheck.fs` — initialTypeEnv, Scheme patterns
- Direct code reading: `Prelude/List.fun` — module wrapper + open pattern
- Direct code reading: `tests/flt/file/record/record-mutable.flt` — SetField mutation semantics

### Secondary (MEDIUM confidence)
- `tests/flt/file/module/module-qualified.flt` — confirms qualified access works through ModuleValueEnv
- `src/LangThree/Elaborate.fs` — confirms `TEList -> TList` elaboration pattern; no `TEArray` exists yet

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — directly read from source, no assumptions
- Architecture: HIGH — `List.fun` pattern is proven and identical
- Pitfalls: HIGH — derived from existing CustomEquality patterns and direct gaps analysis
- Type annotation open question: MEDIUM — parser rules not fully read; omitting for this phase is safe

**Research date:** 2026-03-25
**Valid until:** Stable codebase — valid until any of Ast.fs/Type.fs/Eval.fs/TypeCheck.fs change shape (estimate 90 days)
