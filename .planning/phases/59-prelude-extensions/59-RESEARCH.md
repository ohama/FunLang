# Phase 59: Prelude Extensions - Research

**Researched:** 2026-03-29
**Domain:** LangThree interpreter — List/Array standard library functions (sort, search, transform, ofSeq)
**Confidence:** HIGH

## Summary

Phase 59 adds the standard `List.*` and `Array.*` library functions that F# programmers expect: sorting, searching, filtering with predicates, index access, and conversion from native collections. The work splits cleanly into two categories: (1) functions that can be written as pure LangThree `.fun` implementations (aliases, simple recursion), and (2) functions that require new builtins in `Eval.fs` because they must inspect native collection `Value` variants (HashSetValue, QueueValue, MutableListValue) or apply native F# sort logic.

For the pure `.fun` category: `List.head`, `List.tail`, `List.exists`, `List.item` are trivially aliases to already-existing `hd`, `tl`, `any`, `nth` in `List.fun`. `List.isEmpty`, `List.mapi`, `List.tryFind`, `List.choose`, and `List.distinctBy` can be written as recursive LangThree functions with no new builtins. `List.sort` can also be done in pure LangThree using a merge sort or insertion sort — no builtin is required because `Value` already implements `IComparable` (for `IntValue`, `StringValue`, `CharValue`, `BoolValue`) and LangThree's `<` operator works on those types.

For the builtin category: `List.sortBy` requires calling a user closure on each element to extract a key; this follows the exact `callValue` pattern used by `array_map`/`array_fold`. `List.ofSeq`, `Array.sort`, and `Array.ofSeq` all need new entries in `initialBuiltinEnv` in `Eval.fs` because they must pattern-match on `HashSetValue`/`QueueValue`/`MutableListValue` or mutate an `ArrayValue`.

**Primary recommendation:** Implement aliases/pure functions directly in `Prelude/List.fun` as LangThree code. Add four new builtins to `Eval.fs`: `list_sort_by`, `list_of_seq`, `array_sort`, `array_of_seq`. Wire those builtins into the `List` and `Array` module declarations in the `.fun` files. No `Ast.fs`, `Parser.fsy`, `Bidir.fs`, or `TypeCheck.fs` changes are needed.

## Standard Stack

No new external libraries. All changes are within existing interpreter source files.

### Core Files to Modify

| File | What Changes | Notes |
|------|--------------|-------|
| `Prelude/List.fun` | Add sort, sortBy, tryFind, choose, distinctBy, exists, mapi, item, isEmpty, head, tail, ofSeq | Mix of pure LangThree + builtin wrappers |
| `Prelude/Array.fun` | Add sort, ofSeq | Two new builtin wrappers |
| `src/LangThree/Eval.fs` | Add `list_sort_by`, `list_of_seq`, `array_sort`, `array_of_seq` to `initialBuiltinEnv` | Four new BuiltinValue entries |

### No Changes Required

| File | Why Unchanged |
|------|---------------|
| `Ast.fs` | No new AST nodes; no new Value variants |
| `Parser.fsy` | No new syntax |
| `Bidir.fs` | No new type rules needed |
| `TypeCheck.fs` | No changes |
| `Lexer.fsl` | No new tokens |

## Architecture Patterns

### Pattern 1: Pure Alias in List.fun

Functions that are direct aliases to existing bindings need one line:

```fsharp
// In Prelude/List.fun, inside module List = ...
let head xs  = hd xs
let tail xs  = tl xs
let exists pred xs = any pred xs
let item n xs = nth n xs
let isEmpty xs = match xs with | [] -> true | _ -> false
```

These resolve at load time. No runtime overhead beyond a simple function call.

### Pattern 2: Pure Recursive Function in List.fun

Functions with non-trivial logic can be written in LangThree directly:

```fsharp
// List.mapi : (int -> 'a -> 'b) -> 'a list -> 'b list
let rec mapi_helper f i xs =
    match xs with
    | [] -> []
    | h :: t -> f i h :: mapi_helper f (i + 1) t
let mapi f xs = mapi_helper f 0 xs

// List.tryFind : ('a -> bool) -> 'a list -> 'a option
let rec tryFind pred xs =
    match xs with
    | [] -> None
    | h :: t -> if pred h then Some h else tryFind pred t

// List.choose : ('a -> 'b option) -> 'a list -> 'b list
let rec choose f xs =
    match xs with
    | [] -> []
    | h :: t ->
        match f h with
        | Some v -> v :: choose f t
        | None -> choose f t

// List.distinctBy : ('a -> 'b) -> 'a list -> 'a list
// Uses a recursive helper that tracks seen keys as a list (simple, correct for small lists)
let rec distinctBy_helper f seen xs =
    match xs with
    | [] -> []
    | h :: t ->
        let key = f h
        if any (fun k -> k = key) seen
        then distinctBy_helper f seen t
        else h :: distinctBy_helper f (key :: seen) t
let distinctBy f xs = distinctBy_helper f [] xs
```

### Pattern 3: List.sort in Pure LangThree

`List.sort` can be implemented as insertion sort in pure LangThree. The LangThree `<` and `>` operators already compare `IntValue`, `StringValue`, `CharValue` via `Value.valueCompare`. No builtin needed.

```fsharp
// Insertion sort — correct for all comparable element types
let rec insert x xs =
    match xs with
    | [] -> [x]
    | h :: t -> if x < h then x :: h :: t else h :: insert x t
let rec sort xs =
    match xs with
    | [] -> []
    | h :: t -> insert h (sort t)
```

This is O(n^2) but acceptable for a standard library that prioritizes correctness over performance. A merge sort alternative is possible but adds ~15 more lines.

### Pattern 4: Builtin with callValue (sortBy)

`List.sortBy` takes a key function from user code and must invoke it. Use the established `callValue` pattern — identical to how `array_map`, `array_fold`, and `array_iter` work:

```fsharp
// In Eval.fs initialBuiltinEnv:
// list_sort_by : ('a -> 'b) -> 'a list -> 'a list
"list_sort_by", BuiltinValue (fun fVal ->
    BuiltinValue (fun listVal ->
        match listVal with
        | ListValue xs ->
            let keyed = xs |> List.map (fun x -> (callValue fVal x, x))
            let sorted = keyed |> List.sortWith (fun (k1, _) (k2, _) -> Value.valueCompare k1 k2)
            ListValue (sorted |> List.map snd)
        | _ -> failwith "List.sortBy: expected list"))
```

Then in `Prelude/List.fun`:
```fsharp
let sortBy f xs = list_sort_by f xs
```

### Pattern 5: ofSeq Builtin (Native Collection to List)

`List.ofSeq` must pattern-match on `HashSetValue`, `QueueValue`, `MutableListValue` to extract elements. The exact same conversions already exist in `Eval.fs` for the `ForInExpr` arm (lines 967-971). Replicate that pattern:

```fsharp
// In Eval.fs initialBuiltinEnv:
// list_of_seq : seq<'a> -> 'a list
"list_of_seq", BuiltinValue (fun v ->
    match v with
    | ListValue xs       -> ListValue xs  // identity: list is already a list
    | ArrayValue arr     -> ListValue (Array.toList arr)
    | HashSetValue hs    -> ListValue (hs |> Seq.toList)
    | QueueValue q       -> ListValue (q |> Seq.toList)
    | MutableListValue ml -> ListValue (ml |> Seq.toList)
    | _ -> failwith "List.ofSeq: expected a collection (list, array, HashSet, Queue, or MutableList)")
```

Then in `Prelude/List.fun`:
```fsharp
let ofSeq coll = list_of_seq coll
```

### Pattern 6: Array.sort (In-Place Sort)

`Array.sort` sorts an `ArrayValue` in-place. Unlike F#'s `Array.Sort` which returns unit, this follows the same in-place mutation pattern used by `array_set`:

```fsharp
// In Eval.fs initialBuiltinEnv:
// array_sort : 'a array -> unit
"array_sort", BuiltinValue (fun arrVal ->
    match arrVal with
    | ArrayValue arr ->
        System.Array.Sort(arr, fun x y -> Value.valueCompare x y)
        TupleValue []
    | _ -> failwith "Array.sort: expected array")
```

Then in `Prelude/Array.fun`:
```fsharp
let sort arr = array_sort arr
```

### Pattern 7: Array.ofSeq (Collection to Array)

Similar to `list_of_seq` but produces an `ArrayValue`:

```fsharp
// In Eval.fs initialBuiltinEnv:
// array_of_seq : seq<'a> -> 'a array
"array_of_seq", BuiltinValue (fun v ->
    match v with
    | ListValue xs       -> ArrayValue (List.toArray xs)
    | ArrayValue arr     -> ArrayValue (Array.copy arr)  // copy for safety
    | HashSetValue hs    -> ArrayValue (hs |> Seq.toArray)
    | QueueValue q       -> ArrayValue (q |> Seq.toArray)
    | MutableListValue ml -> ArrayValue (ml |> Seq.toArray)
    | _ -> failwith "Array.ofSeq: expected a collection")
```

Then in `Prelude/Array.fun`:
```fsharp
let ofSeq coll = array_of_seq coll
```

### Recommended Project Structure (No Changes)

```
src/LangThree/
├── Eval.fs          # Add 4 new builtins to initialBuiltinEnv list
└── (all other files unchanged)
Prelude/
├── List.fun         # Add ~30 lines (aliases + pure functions + builtin wrappers)
└── Array.fun        # Add 2 lines (sort + ofSeq wrappers)
```

### Anti-Patterns to Avoid

- **Adding new Value DU cases:** There is no need for a new value type. All collection types are already in `Value`.
- **Implementing distinctBy with a HashSet of keys:** Tempting but unnecessarily complex — a plain list comparison is correct and simpler. Reserve HashSet for when performance matters.
- **Making List.sort a builtin:** Unnecessary. Pure LangThree insertion sort works because `<` already delegates to `Value.valueCompare`.
- **Adding type annotations to List.fun functions:** The type checker is structural and infers types. Adding explicit type annotations in `.fun` files would be non-idiomatic and may cause errors.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Key comparison in sortBy | Custom comparison logic | `Value.valueCompare` (already in `Ast.fs`) | Already implements `IComparable` for all scalar types |
| In-place sort | Custom sort algorithm | `System.Array.Sort` with `Comparison<Value>` delegate | .NET stdlib, correct and fast |
| Sequence conversion | Custom iteration | `.Seq.toList` / `Seq.toArray` on `IEnumerable<Value>` | All native collection types implement `IEnumerable<Value>` |

**Key insight:** All native collection types (`HashSet<Value>`, `Queue<Value>`, `List<Value>`) implement `IEnumerable<Value>`, so `Seq.toList` and `Seq.toArray` work directly without per-type special casing beyond the initial pattern match.

## Common Pitfalls

### Pitfall 1: mapi_helper Is Not Visible to Tests

**What goes wrong:** Helper functions in `List.fun` like `mapi_helper` or `distinctBy_helper` become top-level bindings accessible as `mapi_helper xs`. Users could call them directly.

**Why it happens:** LangThree modules export everything — there is no private/internal access modifier.

**How to avoid:** Name helpers with a leading underscore convention (e.g., `_mapi_helper`) or use `let rec` with inner definitions if the language supports it. Alternatively, accept that helpers are visible (they are harmless in practice).

**Warning signs:** If tests show unexpected bindings in scope, check for leaked helper names.

### Pitfall 2: List.sort Operator Comparison Breaks for Tuples/Records

**What goes wrong:** Using `<` in pure LangThree sort will call `Value.valueCompare`, which returns 0 (equal) for TupleValue/ListValue/DataValue/RecordValue (the catch-all `| _ -> 0` case in `valueCompare`). Sort will appear to work on int/string lists but silently produce wrong results on complex types.

**Why it happens:** `Value.valueCompare` only fully implements comparison for scalar types (Int, Bool, String, Char).

**How to avoid:** Document that `List.sort` only works reliably on lists of comparable scalars. This matches F# semantics for most practical uses. The `valueCompare` implementation is in `Ast.fs` — extending it for tuples is a separate concern (out of scope for Phase 59).

**Warning signs:** `List.sort [(1,2); (3,1)]` may not sort correctly. The success criteria only requires `List.sort [3;1;2]` (integers) to work.

### Pitfall 3: list_sort_by Key Comparison Ordering

**What goes wrong:** `List.sortBy (fun x -> -x) [1;2;3]` should return `[3;2;1]`. If the key comparison is inverted, it returns `[1;2;3]`.

**Why it happens:** The key `(fun x -> -x)` maps `1 -> -1`, `2 -> -2`, `3 -> -3`. Sorting by ascending key `-x` gives `[-3; -2; -1]`, corresponding to original values `[3; 2; 1]`. This is correct ascending-key order, but the expected output is `[3;2;1]`, confirming standard ascending sort of keys.

**How to avoid:** Use `List.sortWith (fun (k1,_) (k2,_) -> Value.valueCompare k1 k2)` which sorts ascending. The expected behavior matches.

### Pitfall 4: Mutating a Shared ArrayValue in Array.sort

**What goes wrong:** `Array.sort` sorts in-place. If two variables alias the same `ArrayValue`, both see the sorted result. This is intentional (same as F#'s `Array.sortInPlace`).

**Why it happens:** `ArrayValue` uses reference equality (see `Ast.fs` line 266). The array contents are mutated in-place.

**How to avoid:** Document that `Array.sort` is in-place. This is consistent with the existing `array_set` behavior and the success criteria ("sorts arrays in-place").

### Pitfall 5: Loading Order in Prelude

**What goes wrong:** If `List.fun` references `any` (which is defined in the same `List.fun` module), but the reference comes before the `any` binding, `tryFind` will fail.

**Why it happens:** LangThree evaluates `.fun` files sequentially. Within a module block, bindings are processed top to bottom. Calling `any` inside `distinctBy_helper` requires `any` to already be bound.

**How to avoid:** Define `any` before `distinctBy_helper`. Since `any` is already in the original `List.fun` (line 13), add new functions at the bottom of the `module List = ...` block after all existing bindings.

## Code Examples

### Verified Pattern: callValue for User Closure in Builtin

```fsharp
// Source: Eval.fs lines 567-573 (array_map)
"array_map", BuiltinValue (fun fVal ->
    BuiltinValue (fun arrVal ->
        match arrVal with
        | ArrayValue arr ->
            ArrayValue (Array.map (fun x -> callValue fVal x) arr)
        | _ -> failwith "Array.map: expected array"))
```

The same pattern applies to `list_sort_by` where `fVal` is the key extractor.

### Verified Pattern: ForInExpr Collection Conversion

```fsharp
// Source: Eval.fs lines 966-975 (ForInExpr eval arm)
let elements =
    match collVal with
    | ListValue xs -> xs
    | ArrayValue arr -> arr |> Array.toList
    | HashSetValue hs -> hs |> Seq.toList
    | QueueValue q -> q |> Seq.toList
    | MutableListValue ml -> ml |> Seq.toList
    | HashtableValue ht ->
        ht |> Seq.map (fun kv ->
            let fields = Map.ofList [("Key", ref kv.Key); ("Value", ref kv.Value)]
            RecordValue("KeyValuePair", fields)) |> Seq.toList
```

`list_of_seq` and `array_of_seq` replicate this pattern without the Hashtable KV case (which would produce a list of records, not raw values).

### Verified Pattern: In-Place Array Sort Using System.Array.Sort

```fsharp
// F# standard: System.Array.Sort with a Comparison<T> delegate
System.Array.Sort(arr, System.Comparison<Value>(fun x y -> Value.valueCompare x y))
// Or more concisely:
System.Array.Sort(arr, fun x y -> Value.valueCompare x y)
```

`Value` implements `IComparable` (Ast.fs lines 247-251), so `System.Array.Sort` works directly even without the explicit `Comparison` delegate, but the explicit form is clearer about what comparison is used.

### Verified Pattern: Pure LangThree Insertion Sort

```fsharp
// Source: standard insertion sort idiom, compatible with LangThree's match/if syntax
let rec insert x xs =
    match xs with
    | [] -> [x]
    | h :: t -> if x < h then x :: h :: t else h :: insert x t
let rec sort xs =
    match xs with
    | [] -> []
    | h :: t -> insert h (sort t)
```

This uses `<` which dispatches through `Value.valueCompare` at runtime. Works correctly for `IntValue`, `StringValue`, `CharValue`, `BoolValue`.

## Requirements Coverage

| Requirement | Implementation Strategy | Location |
|-------------|------------------------|----------|
| PRE-01: List.sort, List.sortBy | `sort` = pure LangThree insertion sort; `sortBy` = wraps `list_sort_by` builtin | `List.fun` + `Eval.fs` |
| PRE-02: List.tryFind, List.choose, List.distinctBy, List.exists | `tryFind`/`choose`/`distinctBy` = pure LangThree; `exists` = alias for `any` | `List.fun` only |
| PRE-03: List.mapi, List.item, List.isEmpty, List.head, List.tail | `mapi` = pure LangThree with helper; `item`=`nth`, `head`=`hd`, `tail`=`tl` aliases; `isEmpty` = 1-line match | `List.fun` only |
| PRE-04: List.ofSeq | Wraps `list_of_seq` builtin | `List.fun` + `Eval.fs` |
| PRE-05: Array.sort, Array.ofSeq | Both wrap new builtins | `Array.fun` + `Eval.fs` |

## State of the Art

| Old Approach | Current Approach | Notes |
|--------------|------------------|-------|
| No sort/search library | Phase 59 adds standard F# List/Array API surface | Filling expected stdlib gap |
| HashSet/Queue only accessible via `.fun` wrappers | `ofSeq` enables conversion to immutable `ListValue` | Bridges mutable-to-immutable |

**Not deprecated:** Everything in the existing `List.fun` and `Array.fun` remains. All new bindings are additive.

## Open Questions

1. **List.sort on TupleValue / DataValue**
   - What we know: `Value.valueCompare` returns 0 for tuples/records/ADTs (catch-all `| _ -> 0`). Sort will be stable but not ordered for those types.
   - What's unclear: Is this acceptable for Phase 59? Success criteria only requires `[3;1;2]` (integers).
   - Recommendation: Accept the limitation; document it. Extending `valueCompare` for tuples is a separate concern.

2. **distinctBy with large lists**
   - What we know: The proposed pure LangThree implementation uses `any (fun k -> k = key) seen`, which is O(n^2).
   - What's unclear: Is there a use case requiring large-list performance?
   - Recommendation: Use the simple O(n^2) implementation. For Phase 59, correctness matters more than performance.

3. **Hashtable support in ofSeq**
   - What we know: `HashtableValue` stores `Dictionary<Value, Value>`. Including it in `list_of_seq` would produce a list of `RecordValue("KeyValuePair", ...)` (same as `ForInExpr`).
   - What's unclear: Is this expected? F#'s `List.ofSeq` on a `Dictionary` gives `KeyValuePair` items.
   - Recommendation: Exclude `HashtableValue` from `list_of_seq` for now (success criteria only mentions HashSet, Queue, MutableList). Can add later.

## Sources

### Primary (HIGH confidence)
- Direct code inspection: `/Users/ohama/vibe/LangThree/src/LangThree/Eval.fs` — `initialBuiltinEnv`, `callValue`, `ForInExpr` eval arm
- Direct code inspection: `/Users/ohama/vibe/LangThree/src/LangThree/Ast.fs` — `Value` DU, `valueCompare`, `IComparable` interface
- Direct code inspection: `/Users/ohama/vibe/LangThree/Prelude/List.fun` — existing bindings (map, filter, fold, length, any, nth, hd, tl, etc.)
- Direct code inspection: `/Users/ohama/vibe/LangThree/Prelude/Array.fun` — existing bindings (create, get, set, length, ofList, toList, iter, map, fold, init)
- Direct code inspection: `/Users/ohama/vibe/LangThree/Prelude/HashSet.fun`, `Queue.fun`, `MutableList.fun` — existing module wrappers pattern

### Secondary (MEDIUM confidence)
- Phase 58 RESEARCH.md (`58-RESEARCH.md`) — confirms pattern: pure `.fun` implementations vs. builtins in `Eval.fs`, no parser/type changes needed for library additions

## Metadata

**Confidence breakdown:**
- Which functions need builtins vs pure LangThree: HIGH — confirmed by reading Eval.fs and Ast.fs directly
- callValue pattern for user closures: HIGH — confirmed from array_map/array_fold/array_iter in Eval.fs
- Collection ofSeq conversion: HIGH — exact Seq.toList calls already exist in ForInExpr eval arm
- Array.sort via System.Array.Sort: HIGH — Value implements IComparable, standard .NET sort delegation works
- Pure LangThree insertion sort: HIGH — < operator works on IntValue via valueCompare; confirmed from value comparison logic
- List.sort for non-scalar types: MEDIUM — valueCompare returns 0 for tuples/records; may produce incorrect ordering but is harmless for Phase 59's success criteria

**Research date:** 2026-03-29
**Valid until:** 2026-04-28 (stable codebase; no external dependencies)
