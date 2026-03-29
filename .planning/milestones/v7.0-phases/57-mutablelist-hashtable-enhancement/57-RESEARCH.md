# Phase 57: MutableList & Hashtable Enhancement - Research

**Researched:** 2026-03-29
**Domain:** F# interpreter — MutableList mutable collection type, Hashtable FieldAccess dispatch extension
**Confidence:** HIGH

## Summary

Phase 57 adds two capabilities to the interpreter. First, `MutableList` — a new `Value` DU case wrapping `System.Collections.Generic.List<Value>` — follows the exact same pattern as `HashSet` and `Queue` from Phase 56: new DU case in `Ast.fs`, Constructor interception in `Eval.fs` and `Bidir.fs`, FieldAccess dispatch in both, and a `Prelude/MutableList.fun` module. Second, the existing `HashtableValue` gains FieldAccess dispatch (`.TryGetValue`, `.Count`, `.Keys`) — currently `HashtableValue` is only accessible via `hashtable_*` builtins and has no FieldAccess arm in either `Eval.fs` or `Bidir.fs`.

The key architectural insight is that `HashtableValue`'s type in the type system is `THashtable(k, v)`, not `TData("Hashtable", [])`. This differs from `HashSet` (`TData("HashSet", [])`) and `Queue` (`TData("Queue", [])`). When adding FieldAccess dispatch for `HashtableValue` in `Bidir.fs`, the match arm must be `| THashtable(keyTy, valTy) ->` not `| TData("Hashtable", []) ->`. The `IndexGet`/`IndexSet` arms in `Bidir.fs` already handle `THashtable` correctly — the new FieldAccess arm joins this pattern.

The `.TryGetValue(key)` method returns a tuple `(bool, value)` represented as `TupleValue [BoolValue; value]`. The `.Keys` property returns `ListValue(keys)`. The `.Count` property returns `IntValue`. MutableList `.[i]` indexing is handled by adding `MutableListValue` to the existing `IndexGet`/`IndexSet` arms in `Eval.fs` and `Bidir.fs`.

**Primary recommendation:** Add `MutableListValue` following the Phase 56 HashSet/Queue pattern exactly. Add `THashtable` FieldAccess arms in both `Eval.fs` and `Bidir.fs` for `.TryGetValue`, `.Count`, `.Keys`. Add `MutableListValue` to `IndexGet`/`IndexSet` match arms for `.[i]` access.

## Standard Stack

No new external libraries. All changes are within the interpreter source files.

### Core Files to Modify

| File | What Changes | Notes |
|------|--------------|-------|
| `Ast.fs` | Add `MutableListValue` DU case; update `GetHashCode`, `valueEqual`, `valueCompare`, `formatValue` | Follow `HashSetValue`/`QueueValue` pattern from Phase 56 |
| `Eval.fs` | Constructor interception "MutableList"; FieldAccess for `MutableListValue`; `IndexGet`/`IndexSet` for `MutableListValue`; FieldAccess for `HashtableValue` | Multi-site edits |
| `Bidir.fs` | Constructor interception "MutableList" → `TData("MutableList", [])`; FieldAccess for `TData("MutableList", [])`; `IndexGet`/`IndexSet` for `TData("MutableList", [])`; FieldAccess for `THashtable` | Multi-site edits |
| `TypeCheck.fs` | No new builtins needed — Phase 57 uses constructor interception + FieldAccess dispatch | Optional: can add raw builtins for module wrappers |
| `Prelude/MutableList.fun` | Module wrapper: `create`, `add`, `get`, `count` | New file |

### .NET Backing Type

| LangThree Type | .NET Type | Why |
|----------------|-----------|-----|
| `MutableListValue` | `System.Collections.Generic.List<Value>` | Dynamic resize; integer indexing; `.Count` property; `.Add(v)` method |

### Installation

No packages. Pure F# source modifications.

## Architecture Patterns

### Pattern 1: New Value DU Case in Ast.fs

Add after `QueueValue`:

```fsharp
| MutableListValue of System.Collections.Generic.List<Value>  // Phase 57: Mutable resizable list
```

**GetHashCode** (identity-based):
```fsharp
| MutableListValue ml -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ml)
```

**valueEqual** (reference identity, same as ArrayValue):
```fsharp
| MutableListValue ml1, MutableListValue ml2 -> System.Object.ReferenceEquals(ml1, ml2)
```

**valueCompare** (zero for incomparables):
```fsharp
| MutableListValue _, _ | _, MutableListValue _ -> 0
```

**formatValue**:
```fsharp
| MutableListValue ml ->
    let elements = ml |> Seq.map formatValue |> String.concat "; "
    sprintf "MutableList[%s]" elements
```

### Pattern 2: Constructor Interception in Eval.fs

In the `Constructor` arm (~line 1115), add after the `"Queue"` cases and before `| _ ->`:

```fsharp
// Phase 57: MutableList constructor interception
| "MutableList", Some argExpr ->
    match eval recEnv moduleEnv env false argExpr with
    | TupleValue [] -> MutableListValue (System.Collections.Generic.List<Value>())
    | _ -> failwith "MutableList: expected ()"
| "MutableList", None ->
    MutableListValue (System.Collections.Generic.List<Value>())
```

### Pattern 3: FieldAccess Dispatch in Eval.fs (MutableList)

In the `FieldAccess` arm (~line 1330), add after the `| QueueValue q ->` block:

```fsharp
// Phase 57: MutableList method dispatch
| MutableListValue ml ->
    match fieldName with
    | "Add" ->
        BuiltinValue (fun arg ->
            ml.Add(arg)
            TupleValue [])
    | "Count" -> IntValue ml.Count
    | _ -> failwithf "MutableList has no property or method '%s'" fieldName
```

**Critical:** `.Count` is a property (no `()`), returned directly — not wrapped in `BuiltinValue`. Same rule as HashSet/Queue.

### Pattern 4: FieldAccess Dispatch in Eval.fs (HashtableValue)

In the `FieldAccess` arm, add after the `| MutableListValue ml ->` block (or before `| RecordValue`):

```fsharp
// Phase 57: HashtableValue method dispatch (FieldAccess for THashtable type)
| HashtableValue ht ->
    match fieldName with
    | "TryGetValue" ->
        BuiltinValue (fun keyArg ->
            match ht.TryGetValue(keyArg) with
            | true, v  -> TupleValue [BoolValue true;  v]
            | false, _ -> TupleValue [BoolValue false; TupleValue []])
    | "Count" -> IntValue ht.Count
    | "Keys"  -> ListValue (ht.Keys |> Seq.toList)
    | _ -> failwithf "Hashtable has no property or method '%s'" fieldName
```

**Note:** `.TryGetValue` returns `TupleValue [BoolValue b; value]`. When the key is missing, the second element is `TupleValue []` (unit) since no meaningful value exists. This matches .NET's `bool * 'v` tuple return.

### Pattern 5: IndexGet/IndexSet for MutableListValue in Eval.fs

In the `IndexGet` arm (~line 933), add after `| HashtableValue ht, key ->`:

```fsharp
| MutableListValue ml, IntValue i ->
    if i < 0 || i >= ml.Count then
        raise (LangThreeException (StringValue (sprintf "MutableList index %d out of bounds (length %d)" i ml.Count)))
    ml.[i]
```

In the `IndexSet` arm (~line 948), add after `| HashtableValue ht, key ->`:

```fsharp
| MutableListValue ml, IntValue i ->
    if i < 0 || i >= ml.Count then
        raise (LangThreeException (StringValue (sprintf "MutableList index %d out of bounds (length %d)" i ml.Count)))
    ml.[i] <- newVal
    TupleValue []
```

Also update the error message fallthrough from:
```fsharp
| _ -> failwith "IndexGet: expected array or hashtable"
```
to:
```fsharp
| _ -> failwith "IndexGet: expected array, hashtable, or MutableList"
```

### Pattern 6: Constructor Interception in Bidir.fs

In the `Constructor` arm (~line 64), add after `"Queue"` and before `| _ ->`:

```fsharp
| "MutableList" ->
    match argOpt with
    | Some argExpr ->
        let s, argTy = synth ctorEnv recEnv ctx env argExpr
        let s2 = unifyWithContext ctx [] span (apply s argTy) (TTuple [])
        (compose s2 s, TData("MutableList", []))
    | None ->
        (empty, TData("MutableList", []))
```

### Pattern 7: FieldAccess Type Rules in Bidir.fs (MutableList)

In the `FieldAccess` arm (~line 541), add after `| TData("Queue", []) ->` and before the generic `TData` fallthrough:

```fsharp
// Phase 57: MutableList field access types
| TData("MutableList", []) ->
    match fieldName with
    | "Add" ->
        let tv = freshVar()
        (s1, TArrow(tv, TTuple []))
    | "Count" -> (s1, TInt)
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
```

### Pattern 8: FieldAccess Type Rules in Bidir.fs (THashtable)

In the `FieldAccess` arm, add a `THashtable` case. This must go BEFORE the generic `TData` fallthrough. Looking at the existing code, the match ends at line 607-608 with the generic `| _ ->` raise. Insert before it:

```fsharp
// Phase 57: Hashtable field access types (THashtable is the existing type rep)
| THashtable (keyTy, valTy) ->
    match fieldName with
    | "TryGetValue" ->
        (s1, TArrow(keyTy, TTuple [TBool; valTy]))
    | "Count" -> (s1, TInt)
    | "Keys"  -> (s1, TList keyTy)
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
```

**Critical:** `THashtable(keyTy, valTy)` — NOT `TData("Hashtable", [])`. The existing `THashtable` type is parameterized and is used for `Hashtable.create ()` return types.

### Pattern 9: IndexGet/IndexSet for TData("MutableList", []) in Bidir.fs

In the `IndexGet` arm (~line 660), add after `| THashtable (keyTy, valTy) ->`:

```fsharp
| TData("MutableList", []) ->
    let tv = freshVar()
    let s2, idxTy = synth ctorEnv recEnv ctx (applyEnv s1 env) idxExpr
    let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
    (compose s3 (compose s2 s1), tv)
```

In the `IndexSet` arm (~line 679), add after `| THashtable (keyTy, valTy) ->`:

```fsharp
| TData("MutableList", []) ->
    let env1 = applyEnv s1 env
    let s2, idxTy = synth ctorEnv recEnv ctx env1 idxExpr
    let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
    (compose s3 (compose s2 s1), TTuple [])
```

**Note:** MutableList is unparameterized in the type system (`TData("MutableList", [])`), so element type is `freshVar()`. This is consistent with the `HashSet`/`Queue` approach — simpler than making it `TData("MutableList", [elemTy])`.

### Pattern 10: Prelude/MutableList.fun

Create `Prelude/MutableList.fun`:

```fsharp
module MutableList =
    let create ()     = MutableList ()
    let add ml v      = ml.Add v
    let get ml i      = ml.[i]
    let count ml      = ml.Count
```

### Pattern 11: Test File Structure

FsLit tests use `// --- Output:` (confirmed from Phase 55/56). Binary path: `/Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree`.

Place tests:
- `tests/flt/file/mutablelist/mutablelist-basic.flt` — basic Add/indexing/Count
- `tests/flt/file/mutablelist/mutablelist-indexing.flt` — `.[i]` read and write
- `tests/flt/file/hashtable/hashtable-dot-api.flt` — `.TryGetValue`, `.Count`, `.Keys`

### Anti-Patterns to Avoid

- **Do NOT use `TData("Hashtable", [])` for HashtableValue FieldAccess in Bidir.fs.** The type system represents hashtable as `THashtable(k, v)`, not as `TData`. The FieldAccess match arm must be `| THashtable (keyTy, valTy) ->`.
- **Do NOT forget the `IndexGet`/`IndexSet` arms for MutableList.** MutableList `.[i]` syntax goes through these AST nodes, not through FieldAccess. Both `Eval.fs` and `Bidir.fs` need new arms.
- **Do NOT return `BuiltinValue` for `.Count`.** `.Count` is `FieldAccess(ml, "Count", _)` with no subsequent `App`. Return `IntValue ml.Count` directly.
- **Do NOT use `// --- Stdout:` in test files.** Always `// --- Output:`.
- **Do NOT add raw builtins** (`mutablelist_create`, etc.) to `TypeCheck.fs`. Phase 57 follows the Phase 55/56 constructor+FieldAccess pattern.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Resizable list | `ListValue` mutation (ListValue is immutable F# list) | `System.Collections.Generic.List<Value>` | F# lists are structurally immutable; `.Add()` requires mutable backing |
| TryGetValue tuple | Custom option type | `TupleValue [BoolValue b; v]` | Consistent with spec: "bool * value tuple" is standard F# TryGetValue pattern |
| Hashtable key enumeration | Iterate and collect manually | `ht.Keys \|> Seq.toList` | Already done by `hashtable_keys` builtin; reuse the same pattern |

**Key insight:** `MutableListValue` must NOT reuse `ListValue`. `ListValue` wraps an immutable F# list — there is no way to do `.Add()` on it. `MutableListValue` needs `System.Collections.Generic.List<Value>` specifically for in-place mutation.

## Common Pitfalls

### Pitfall 1: HashtableValue FieldAccess Type Mismatch in Bidir.fs

**What goes wrong:** Adding `| TData("Hashtable", []) ->` in `Bidir.fs` FieldAccess — this will never match because `Hashtable.create ()` returns type `THashtable(TVar 0, TVar 1)`, not `TData("Hashtable", [])`.

**Why it happens:** `HashtableValue` predates the Phase 54/55/56 constructor-interception pattern. Its type in the system is `THashtable`, a dedicated type constructor, not `TData`. Only Phase 55+ types (`StringBuilder`, `HashSet`, `Queue`, `MutableList`) use `TData`.

**How to avoid:** Match `| THashtable (keyTy, valTy) ->` in `Bidir.fs` FieldAccess. In `Eval.fs`, match `| HashtableValue ht ->` (already the correct runtime Value case).

**Warning signs:** Type errors at compile time (`.TryGetValue` unresolved) even though `Eval.fs` has the arm — the Bidir arm is wrong.

### Pitfall 2: MutableList .[i] Indexing Not in FieldAccess

**What goes wrong:** Implementing MutableList indexing in FieldAccess instead of IndexGet/IndexSet.

**Why it happens:** The `.[i]` syntax parses as `IndexGet(ml, i, _)` / `IndexSet(ml, i, v, _)` AST nodes — NOT as FieldAccess. The existing array and hashtable handling is proof of this: they are in the `IndexGet`/`IndexSet` match arms.

**How to avoid:** Add `MutableListValue` cases in the `IndexGet` and `IndexSet` arms (lines ~933 and ~948 in Eval.fs). Also add `TData("MutableList", [])` cases in `IndexGet`/`IndexSet` arms in Bidir.fs.

**Warning signs:** `ml.[0]` raises "IndexGet: expected array or hashtable" even though FieldAccess dispatch works for `.Count`.

### Pitfall 3: TryGetValue Missing Key Value

**What goes wrong:** Returning `TupleValue [BoolValue false]` (a 1-tuple) when key is missing, instead of a 2-tuple.

**Why it happens:** When key is missing, there's no meaningful second value. Developers may return only the bool.

**How to avoid:** Always return a 2-tuple: `TupleValue [BoolValue false; TupleValue []]`. The second element is unit (`TupleValue []`) when the key is missing. The user-facing spec says the return is `(false, ...)` — the second element exists but is undefined/unit.

**Warning signs:** Pattern matching `let (found, value) = ht.TryGetValue(key)` fails at runtime with tuple destructuring error.

### Pitfall 4: Constructor Match Arm Ordering

**What goes wrong:** Placing `"MutableList"` after `| _ ->` in the Constructor match arm.

**Why it happens:** F# match is first-match-wins. The `| _ -> DataValue(name, argValue)` fallthrough must remain last.

**How to avoid:** Insert `"MutableList"` cases immediately after `"Queue"` and before `| _ ->`, preserving the established ordering pattern.

### Pitfall 5: IndexGet/IndexSet Error Message Update

**What goes wrong:** Leaving the fallthrough error as "IndexGet: expected array or hashtable" after adding MutableList.

**Why it happens:** The message is in the final `| _ -> failwith ...` arm and is easy to overlook.

**How to avoid:** Update both `IndexGet` and `IndexSet` fallthrough messages to mention MutableList.

## Code Examples

### MutableListValue DU Case (Ast.fs)

```fsharp
// Source: Phase 56 HashSetValue/QueueValue pattern, Ast.fs lines 210-211
| MutableListValue of System.Collections.Generic.List<Value>  // Phase 57: Mutable resizable list
```

### Constructor Interception (Eval.fs)

```fsharp
// Source: Phase 56 "Queue" interception pattern, Eval.fs ~line 1133
| "MutableList", Some argExpr ->
    match eval recEnv moduleEnv env false argExpr with
    | TupleValue [] -> MutableListValue (System.Collections.Generic.List<Value>())
    | _ -> failwith "MutableList: expected ()"
| "MutableList", None ->
    MutableListValue (System.Collections.Generic.List<Value>())
```

### MutableList FieldAccess Dispatch (Eval.fs)

```fsharp
// Source: Phase 56 QueueValue dispatch, Eval.fs ~line 1314
| MutableListValue ml ->
    match fieldName with
    | "Add" ->
        BuiltinValue (fun arg ->
            ml.Add(arg)
            TupleValue [])
    | "Count" -> IntValue ml.Count
    | _ -> failwithf "MutableList has no property or method '%s'" fieldName
```

### HashtableValue FieldAccess Dispatch (Eval.fs)

```fsharp
// Source: hashtable_get/hashtable_keys builtin patterns, Eval.fs ~lines 597-627
| HashtableValue ht ->
    match fieldName with
    | "TryGetValue" ->
        BuiltinValue (fun keyArg ->
            match ht.TryGetValue(keyArg) with
            | true, v  -> TupleValue [BoolValue true;  v]
            | false, _ -> TupleValue [BoolValue false; TupleValue []])
    | "Count" -> IntValue ht.Count
    | "Keys"  -> ListValue (ht.Keys |> Seq.toList)
    | _ -> failwithf "Hashtable has no property or method '%s'" fieldName
```

### MutableList IndexGet (Eval.fs)

```fsharp
// Source: ArrayValue IndexGet pattern, Eval.fs ~line 937
| MutableListValue ml, IntValue i ->
    if i < 0 || i >= ml.Count then
        raise (LangThreeException (StringValue (sprintf "MutableList index %d out of bounds (length %d)" i ml.Count)))
    ml.[i]
```

### THashtable FieldAccess Types (Bidir.fs)

```fsharp
// Source: THashtable IndexGet arm, Bidir.fs ~line 668
| THashtable (keyTy, valTy) ->
    match fieldName with
    | "TryGetValue" ->
        (s1, TArrow(keyTy, TTuple [TBool; valTy]))
    | "Count" -> (s1, TInt)
    | "Keys"  -> (s1, TList keyTy)
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
```

### MutableList IndexGet Type (Bidir.fs)

```fsharp
// Source: THashtable IndexGet arm pattern, Bidir.fs ~line 668
| TData("MutableList", []) ->
    let tv = freshVar()
    let s2, idxTy = synth ctorEnv recEnv ctx (applyEnv s1 env) idxExpr
    let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
    (compose s3 (compose s2 s1), tv)
```

### Sample flt Test: MutableList Basic

```
// Test: MutableList basic Add, Count, indexing (COLL-04)
// --- Command: /Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let ml = MutableList ()
let _ = ml.Add(10)
let _ = ml.Add(20)
let _ = ml.Add(30)
let _ = println (to_string ml.Count)
let _ = println (to_string ml.[0])
let _ = println (to_string ml.[2])
// --- Output:
3
10
30
()
```

### Sample flt Test: Hashtable TryGetValue

```
// Test: Hashtable.TryGetValue with present and missing keys (COLL-05)
// --- Command: /Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let ht = Hashtable.create ()
let _ = Hashtable.set ht "x" 42
let r1 = ht.TryGetValue("x")
let r2 = ht.TryGetValue("missing")
let _ = println (to_string r1)
let _ = println (to_string r2)
let _ = println (to_string ht.Count)
let ks = ht.Keys
let _ = println (to_string (List.length ks))
// --- Output:
(true, 42)
(false, ())
1
1
()
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Raw builtins + module wrapper (Hashtable) | Constructor interception + FieldAccess dispatch (Phase 55+) | Phase 54–55 | New types use FieldAccess; `HashtableValue` gets FieldAccess added in Phase 57 |
| `// --- Stdout:` in tests | `// --- Output:` in tests | Phase 55 | New tests use `// --- Output:` |
| No `.[i]` on MutableList | `IndexGet`/`IndexSet` arms added for `MutableListValue` | Phase 57 | Same AST nodes as arrays/hashtables |

**Deprecated/outdated:**
- `hashtable_keys` builtin: Still works, but `.Keys` property is the new idiomatic approach.
- `hashtable_get` builtin: Still works, but `.TryGetValue` is safer (no exception on missing key).

## Open Questions

1. **TryGetValue missing-key second element type**
   - What we know: The spec says `(false, ...)` — second element when missing is unspecified
   - What's unclear: Should it be `TupleValue []` (unit) or a dummy `IntValue 0`?
   - Recommendation: Use `TupleValue []` (unit). It is the "no value" sentinel in this interpreter. Tests should use `let (found, _) = ht.TryGetValue(key)` pattern and check only `found` when key is missing.

2. **ForInExpr support for MutableList**
   - What we know: `ForInExpr` in `Eval.fs` currently handles `ListValue` and `ArrayValue` only
   - What's unclear: Should Phase 57 add `MutableListValue` to `ForInExpr`? The spec does not mention `for x in ml`.
   - Recommendation: Skip for Phase 57. The spec requirements (COLL-04) only list `.Add`, `.[i]`, `.Count` — no iteration. Add in a future phase if needed.

3. **MutableList element type in Bidir.fs IndexGet**
   - What we know: `TData("MutableList", [])` is unparameterized, so element type from `.[i]` is `freshVar()`
   - What's unclear: Whether this causes type inference issues (e.g., two `.[i]` accesses on the same list may get different fresh vars)
   - Recommendation: Use `freshVar()` for now, same as HashSet/Queue approach. The type checker will unify vars when they're used together. If this causes issues, a follow-up phase can parameterize MutableList.

## Sources

### Primary (HIGH confidence)

Direct codebase inspection:
- `Ast.fs` — Value DU lines 200-211, 225-236, 253-264, 276-279; `formatValue` lines 130-168
- `Eval.fs` — Constructor arm lines 1114-1141; FieldAccess arm lines 1254-1335; IndexGet/IndexSet lines 933-961; `hashtable_*` builtins lines 592-637
- `Bidir.fs` — Constructor arm lines 64-95; FieldAccess arm lines 541-608; IndexGet/IndexSet lines 659-703; `THashtable` handling lines 668-698
- `TypeCheck.fs` — `initialTypeEnv` lines 149-189 (Hashtable, StringBuilder, HashSet, Queue entries)
- `Prelude/HashSet.fun`, `Prelude/Queue.fun`, `Prelude/Hashtable.fun` — module wrapper patterns
- `tests/flt/file/hashset/hashset-basic.flt` — test format reference
- `tests/flt/file/queue/queue-basic.flt` — test format reference

### Secondary (HIGH confidence)

- Phase 56 RESEARCH.md — architectural patterns confirmed by code inspection
- .NET documentation (knowledge cutoff): `List<T>.Add` is void; `List<T>` has `.Count`, integer indexer, `[i]` setter

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all affected files directly inspected
- Architecture: HIGH — exact patterns confirmed from Phase 55/56 source code, all edge cases verified
- Pitfalls: HIGH — THashtable vs TData distinction verified in source; IndexGet/IndexSet pattern verified

**Research date:** 2026-03-29
**Valid until:** Stable (changes only if Phase 54-56 patterns change)
