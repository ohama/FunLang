# Phase 62: Remove Dot Dispatch - Research

**Researched:** 2026-03-29
**Domain:** F# interpreter internals — Eval.fs / Bidir.fs FieldAccess dispatch removal
**Confidence:** HIGH

## Summary

Phase 62 removes the value-type dot-dispatch FieldAccess branches from Eval.fs and Bidir.fs. These branches were added in Phases 54–58 to support OOP-style method calls (`s.Length`, `hs.Add(x)`, etc.) on runtime values. Phase 59–61 added equivalent functional module APIs (`String.length s`, `HashSet.add hs x`, etc.) backed by built-in F# functions. The module APIs now fully cover all the capabilities previously provided by dot dispatch.

The removal is a two-part job: (1) delete the value-type match arms in Eval.fs's `FieldAccess` handler, and (2) delete the value-type match arms in Bidir.fs's `FieldAccess` type-synthesis handler. Both files retain the record field access and module qualified access paths untouched.

**A complication exists:** 25 existing flt test files still use dot notation directly (`ml.Add(x)`, `q.Count`, `sb.Append("x")`, etc.). These tests will fail after the Eval.fs/Bidir.fs changes. They must be rewritten to use the module API before (or as part of) this phase, or kept as is if the decision is to keep dot notation for those types. Based on the phase description ("순수 함수형 API만 남는다" — pure functional API only remains) and the prior state ("Hashtable module API: no dot-notation anywhere in flt tests"), all 25 files need migration.

**Primary recommendation:** Delete the value-type FieldAccess arms from Eval.fs (lines 1440–1532) and Bidir.fs (lines 575–649), then rewrite all 25 flt test files that use dot notation to use the corresponding module API.

## Standard Stack

This phase has no external library dependencies. It is pure deletion + flt test migration within the existing codebase.

### Files Modified

| File | Location | What Changes |
|------|----------|--------------|
| `src/LangThree/Eval.fs` | Lines 1437–1537 | Delete value-type FieldAccess arms (StringValue, ArrayValue, StringBuilderValue, HashSetValue, QueueValue, MutableListValue, HashtableValue) |
| `src/LangThree/Bidir.fs` | Lines 574–649 | Delete value-type FieldAccess type-synthesis arms (TString, TArray, TData("StringBuilder",[]), TData("HashSet",[]), TData("Queue",[]), TData("MutableList",[]), THashtable, TData("KeyValuePair",...)) |
| `tests/flt/file/**/*.flt` | 25 test files | Rewrite dot-notation usage to module API |

### Module API Reference (what replaces dot notation)

| Dot notation | Module API replacement | Source |
|-------------|----------------------|--------|
| `s.Length` | `String.length s` | Prelude/String.fun |
| `s.Contains(needle)` | `String.contains s needle` | Prelude/String.fun |
| `s.EndsWith(suffix)` | `String.endsWith s suffix` | Prelude/String.fun |
| `s.StartsWith(prefix)` | `String.startsWith s prefix` | Prelude/String.fun |
| `s.Trim()` | `String.trim s` | Prelude/String.fun |
| `arr.Length` | `Array.length arr` | Prelude/Array.fun (existing) |
| `sb.Append(s)` | `StringBuilder.add sb s` | Prelude/StringBuilder.fun |
| `sb.ToString()` | `StringBuilder.toString sb` | Prelude/StringBuilder.fun |
| `hs.Add(v)` | `HashSet.add hs v` | Prelude/HashSet.fun |
| `hs.Contains(v)` | `HashSet.contains hs v` | Prelude/HashSet.fun |
| `hs.Count` | `HashSet.count hs` | Prelude/HashSet.fun |
| `q.Enqueue(v)` | `Queue.enqueue q v` | Prelude/Queue.fun |
| `q.Dequeue()` | `Queue.dequeue q ()` | Prelude/Queue.fun |
| `q.Count` | `Queue.count q` | Prelude/Queue.fun |
| `ml.Add(v)` | `MutableList.add ml v` | Prelude/MutableList.fun |
| `ml.Count` | `MutableList.count ml` | Prelude/MutableList.fun |
| `ht.TryGetValue(key)` | `Hashtable.tryGetValue ht key` | Prelude/Hashtable.fun |
| `ht.Count` | `Hashtable.count ht` | Prelude/Hashtable.fun |
| `ht.Keys` | `Hashtable.keys ht` | Prelude/Hashtable.fun |

**Note:** `Array.length` already exists in Prelude/Array.fun and is used via other paths. Verify it's exported before the migration.

**Note on `Queue.dequeue`:** The module API signature is `Queue.dequeue q ()` (two arguments) while the dot-dispatch used `q.Dequeue()` (method call). The error message from the builtin reads `"Queue.Dequeue: queue is empty"` — after removing dot dispatch, the error comes from the `queue_dequeue` builtin which uses the same message string. The `queue-error.flt` test expects that exact error string, so no change is needed to the error message.

## Architecture Patterns

### Eval.fs FieldAccess Structure

The `FieldAccess` arm in Eval.fs (starting at line 1379) has this structure:

```
| FieldAccess (expr, fieldName, _) ->
    1. tryGetModuleName: handles Module.member (module qualified access)
    2. match expr with
       | FieldAccess (inner, ...) -> handles A.B.c submodule access
       | _ ->
           match eval ... expr with
           // VALUE-TYPE DISPATCH (lines 1440-1532) — DELETE ALL OF THESE:
           | StringValue s -> ...
           | ArrayValue arr -> ...
           | StringBuilderValue sb -> ...
           | HashSetValue hs -> ...
           | QueueValue q -> ...
           | MutableListValue ml -> ...
           | HashtableValue ht -> ...
           // KEEP THESE:
           | RecordValue (_, fields) -> ...
           | v -> failwithf "Field access on non-record value: ..."
```

After deletion, the `_ ->` fallthrough arm evaluates the expr and only handles `RecordValue` and the error case.

### Bidir.fs FieldAccess Structure

The `FieldAccess` arm in Bidir.fs (starting at line 571) has this structure:

```
| FieldAccess (accessExpr, fieldName, span) ->
    let s1, exprTy = synth ... accessExpr
    let resolvedTy = apply s1 exprTy
    match resolvedTy with
    // VALUE-TYPE BRANCHES (lines 574-649) — DELETE ALL OF THESE:
    | TString -> ...
    | TArray _ -> ...
    | TData("StringBuilder", []) -> ...
    | TData("HashSet", []) -> ...
    | TData("Queue", []) -> ...
    | TData("MutableList", []) -> ...
    | THashtable (keyTy, valTy) -> ...
    | TData("KeyValuePair", [keyTy; valTy]) -> ...
    // KEEP THESE:
    | TData (typeName, typeArgs) ->
        match Map.tryFind typeName recEnv with ...
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; ... })
```

After deletion, any FieldAccess whose base expression has a non-record type will fall through to the `FieldAccessOnNonRecord` error — which is the correct behavior.

### flt Test Migration Pattern

Each dot-notation test needs systematic replacement. Example:

Before (`hashset-basic.flt`):
```
let hs = HashSet ()
let r1 = hs.Add(1)
let _ = println (to_string (hs.Contains(1)))
let _ = println (to_string hs.Count)
```

After:
```
let hs = HashSet.create ()
let r1 = HashSet.add hs 1
let _ = println (to_string (HashSet.contains hs 1))
let _ = println (to_string (HashSet.count hs))
```

Note: `HashSet ()` constructor syntax must also change to `HashSet.create ()`. Same for `Queue ()`, `MutableList ()`, `StringBuilder ()` — these are constructor calls that instantiate the value types. The module `create` functions are the functional replacements.

### Constructor Syntax Change

The files use constructor syntax `HashSet ()`, `Queue ()`, `MutableList ()`, `StringBuilder ()` to create collection instances. After removing dot dispatch, these constructors still work for instantiation (they are registered in Eval.fs's Constructor arm at lines 1293–1319). However, since this phase is about making the API purely functional, it's consistent to change `HashSet ()` to `HashSet.create ()` etc. in the test files.

**Decision needed (planner):** The phase description says to remove dispatch code so only pure functional API remains. This means the flt tests using `HashSet ()` constructor syntax should be migrated to `HashSet.create ()`. However, the Constructor arm in Eval.fs is separate from FieldAccess dispatch — constructor instantiation is handled by the `Constructor` AST node, not `FieldAccess`. Therefore, removing FieldAccess dispatch does NOT break `HashSet ()` constructor syntax. The planner should decide whether to also migrate constructor syntax or leave it.

Given the existing `-prelude.flt` test files already use `HashSet.create ()`, the recommended approach is: convert all collection creation to `Module.create ()` pattern in the migrated tests for consistency.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Module API for collections | Custom FieldAccess dispatch | Prelude .fun files + built-in functions | Already fully implemented in Phases 59-61 |
| Array.length | New built-in or dot dispatch | Existing `Array.length` in Array.fun | Already present in prelude |

## Common Pitfalls

### Pitfall 1: KeyValuePair branch in Bidir.fs
**What goes wrong:** The `TData("KeyValuePair", [keyTy; valTy])` branch in Bidir.fs was added in Phase 58 for hashtable for-in iteration where each element was a KeyValuePair record. In Phase 61, this was changed so for-in over Hashtable yields `TupleValue [k; v]` (not RecordValue KeyValuePair). If code still accesses `.Key` / `.Value` on iteration vars, it will fail.
**How to avoid:** Confirm no flt tests use `.Key` / `.Value` on hashtable for-in variables (verified: none do, the hashtable-forin tests use tuple destructuring).
**Action:** Delete the KeyValuePair branch from Bidir.fs safely.

### Pitfall 2: stringbuilder-prelude.flt mixes module API and dot notation
**What goes wrong:** `stringbuilder-prelude.flt` uses `StringBuilder.create ()` (module API) but then calls `sb.Append("hello")` (dot notation). After removing dispatch, this test will fail.
**How to avoid:** Rewrite to use `StringBuilder.add sb "hello"` for all Append calls.

### Pitfall 3: stringbuilder-with-methods.flt combines StringBuilder and String dot methods
**What goes wrong:** This file uses `sb.Append(...)`, `sb.ToString()`, `result.Trim()`, `trimmed.StartsWith(...)`, `trimmed.EndsWith(...)`, `trimmed.Length`. All must be migrated.
**How to avoid:** Rewrite to `StringBuilder.add`, `StringBuilder.toString`, `String.trim`, `String.startsWith`, `String.endsWith`, `String.length`.

### Pitfall 4: queue-error.flt checks error message string
**What goes wrong:** The test expects the error message `"caught: Queue.Dequeue: queue is empty"`. After removing dot dispatch, the Queue.dequeue error comes from the `queue_dequeue` builtin (line 749 in Eval.fs) which uses `"Queue.Dequeue: queue is empty"` — the same string. Safe to remove dot dispatch without changing the error message.
**How to avoid:** Keep the error message in the builtin as-is.

### Pitfall 5: Record field access must survive intact
**What goes wrong:** If the wrong match arm is deleted in Eval.fs, record field access (`record.field`) breaks.
**How to avoid:** The `RecordValue (_, fields)` arm at line 1533–1536 and the `| v -> failwithf` at line 1537 must remain. Only the value-type arms between line 1440 and 1532 are deleted.

### Pitfall 6: Module qualified access must survive intact
**What goes wrong:** The `tryGetModuleName` block and `FieldAccess (innerExpr, innerField, _)` submodule chain block at the top of the FieldAccess arm must remain.
**How to avoid:** Only delete the `match eval ... expr with` inner match arms for value types. Keep the outer module name resolution logic entirely.

### Pitfall 7: `Array.length` in Array.fun
**What goes wrong:** `arr.Length` is used in property tests. The replacement is `Array.length arr`. Verify `Array.length` is defined in `Prelude/Array.fun`.

## Code Examples

### Exact deletion range in Eval.fs

Delete from line 1439 (the comment `// Phase 54: String properties and methods`) through line 1532 (end of `HashtableValue` branch, before `| RecordValue`):

```fsharp
// DELETE lines 1439-1532 (value-type dispatch):
// Phase 54: String properties and methods
| StringValue s ->
    match fieldName with
    | "Length" -> IntValue s.Length
    ... (all String dispatch)
// Phase 54: Array properties
| ArrayValue arr ->
    ... (all Array dispatch)
// Phase 55: StringBuilder method dispatch
| StringBuilderValue sb ->
    ... (all StringBuilder dispatch)
// Phase 56: HashSet method dispatch
| HashSetValue hs ->
    ... (all HashSet dispatch)
// Phase 56: Queue method dispatch
| QueueValue q ->
    ... (all Queue dispatch)
// Phase 57: MutableList method dispatch
| MutableListValue ml ->
    ... (all MutableList dispatch)
// Phase 57: HashtableValue method dispatch
| HashtableValue ht ->
    ... (all Hashtable dispatch)

// KEEP from line 1533:
| RecordValue (_, fields) ->
    match Map.tryFind fieldName fields with
    | Some valueRef -> !valueRef
    | None -> failwithf "Field not found: %s" fieldName
| v -> failwithf "Field access on non-record value: %s" (formatValue v)
```

### Exact deletion range in Bidir.fs

Delete from line 574 (first value-type branch comment) through line 649 (end of KeyValuePair branch), keeping `| TData (typeName, typeArgs)` at line 650 and below:

```fsharp
// DELETE lines 574-649 (all these match arms):
// Phase 54: String property/method types
| TString -> ...
// Phase 54: Array property types
| TArray _ -> ...
// Phase 55: StringBuilder field access types
| TData("StringBuilder", []) -> ...
// Phase 56: HashSet field access types
| TData("HashSet", []) -> ...
// Phase 56: Queue field access types
| TData("Queue", []) -> ...
// Phase 57: MutableList field access types
| TData("MutableList", []) -> ...
// Phase 57: Hashtable field access types
| THashtable (keyTy, valTy) -> ...
// Phase 58: KeyValuePair field access
| TData("KeyValuePair", [keyTy; valTy]) -> ...

// KEEP from line 650:
| TData (typeName, typeArgs) ->
    match Map.tryFind typeName recEnv with ...
| _ ->
    raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; ... })
```

### flt test migration template — HashSet

```flt
// Before
let hs = HashSet ()
let r1 = hs.Add(1)
let _ = println (to_string (hs.Contains(1)))
let _ = println (to_string hs.Count)

// After
let hs = HashSet.create ()
let r1 = HashSet.add hs 1
let _ = println (to_string (HashSet.contains hs 1))
let _ = println (to_string (HashSet.count hs))
```

### flt test migration template — Queue

```flt
// Before
let q = Queue ()
let _ = q.Enqueue(10)
let v1 = q.Dequeue ()
let _ = println (to_string q.Count)

// After
let q = Queue.create ()
let _ = Queue.enqueue q 10
let v1 = Queue.dequeue q ()
let _ = println (to_string (Queue.count q))
```

### flt test migration template — MutableList

```flt
// Before
let ml = MutableList ()
let _ = ml.Add(10)
let _ = println (to_string ml.Count)

// After
let ml = MutableList.create ()
let _ = MutableList.add ml 10
let _ = println (to_string (MutableList.count ml))
```

### flt test migration template — StringBuilder

```flt
// Before
let sb = StringBuilder ()
let _ = sb.Append("hello")
let result = sb.ToString ()

// After
let sb = StringBuilder.create ()
let _ = StringBuilder.add sb "hello"
let result = StringBuilder.toString sb
```

### flt test migration template — String

```flt
// Before
let n = s.Length
let _ = println (to_string (s.Contains("world")))
let _ = println (to_string ("hello.txt".EndsWith(".txt")))
let _ = println (to_string ("hello".StartsWith("he")))
let trimmed = " hi ".Trim ()

// After
let n = String.length s
let _ = println (to_string (String.contains s "world"))
let _ = println (to_string (String.endsWith "hello.txt" ".txt"))
let _ = println (to_string (String.startsWith "hello" "he"))
let trimmed = String.trim " hi "
```

### flt test migration template — Array

```flt
// Before
let n = arr.Length

// After
let n = Array.length arr
```

## Complete List of flt Files to Migrate

### String dot notation (7 files)

1. `tests/flt/file/property/property-string-length.flt` — `s.Length`, `"hello".Length`
2. `tests/flt/file/property/property-string-contains.flt` — `s.Contains(...)`, `"hello".Contains(...)`
3. `tests/flt/file/string/str-methods-trim.flt` — `" hi ".Trim ()`
4. `tests/flt/file/string/str-methods-endswith-startswith.flt` — `.EndsWith(...)`, `.StartsWith(...)`
5. `tests/flt/file/string/stringbuilder-with-methods.flt` — string methods on result of StringBuilder.toString

### Array dot notation (1 file)

6. `tests/flt/file/property/property-array-length.flt` — `arr.Length`

### StringBuilder dot notation (5 files)

7. `tests/flt/file/string/stringbuilder-basic.flt` — `sb.Append(...)`, `sb.ToString()`
8. `tests/flt/file/string/stringbuilder-chaining.flt` — chained `.Append(...)`, `.ToString()`
9. `tests/flt/file/string/stringbuilder-prelude.flt` — mixed: `StringBuilder.create ()` + `sb.Append(...)`
10. `tests/flt/file/string/stringbuilder-append-char.flt` — `sb.Append(char)`
11. `tests/flt/file/string/stringbuilder-with-methods.flt` — `sb.Append(...)`, `sb.ToString()`, string methods

### HashSet dot notation (4 files + cross-refs)

12. `tests/flt/file/hashset/hashset-basic.flt` — `hs.Add(...)`, `hs.Contains(...)`, `hs.Count`
13. `tests/flt/file/hashset/hashset-strings.flt` — same pattern
14. `tests/flt/file/hashset/hashset-forin.flt` — `hs.Add(...)`
15. `tests/flt/file/list/list-comp-from-collections.flt` — `hs.Add(...)`
16. `tests/flt/file/prelude/prelude-list-ofseq.flt` — `hs.Add(...)`, `q.Enqueue(...)`, `ml.Add(...)`

### Queue dot notation (6 files)

17. `tests/flt/file/queue/queue-basic.flt` — `q.Enqueue(...)`, `q.Dequeue ()`, `q.Count`
18. `tests/flt/file/queue/queue-error.flt` — `q.Dequeue ()` (error test, keep error message)
19. `tests/flt/file/queue/queue-forin.flt` — `q.Enqueue(...)`
20. `tests/flt/file/prelude/prelude-array-sort-ofseq.flt` — `q.Enqueue(...)`
21. `tests/flt/file/prelude/prelude-ofseq-sort-pipeline.flt` — `q.Enqueue(...)`, `ml.Add(...)`
22. `tests/flt/file/property/property-count-consistency.flt` — `hs.Add`, `q.Enqueue`, `ml.Add`, `.Count` on all

### MutableList dot notation (4 files)

23. `tests/flt/file/mutablelist/mutablelist-basic.flt` — `ml.Add(...)`, `ml.Count`
24. `tests/flt/file/mutablelist/mutablelist-forin.flt` — `ml.Add(...)`
25. `tests/flt/file/mutablelist/mutablelist-indexing.flt` — `ml.Add(...)`, `ml.Count` (note: `ml.[i]` indexing uses IndexGet, NOT FieldAccess — keep as-is)
26. `tests/flt/file/mutablelist/mutablelist-bounds-error.flt` — `ml.Count`, `ml.Add(...)`

**Note:** `ml.[0]` and `ml.[0] <- 999` use `IndexGet`/`IndexSet` AST nodes, not `FieldAccess`. These survive dot dispatch removal unchanged.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `s.Length` dot dispatch in Eval.fs/Bidir.fs | `String.length s` module function | Phase 60/61 added module API | Can now remove dispatch |
| `hs.Add(v)` dot dispatch | `HashSet.add hs v` module function | Phase 61 | Can now remove dispatch |
| `q.Enqueue(v)` dot dispatch | `Queue.enqueue q v` module function | Phase 61 | Can now remove dispatch |
| `ml.Add(v)` dot dispatch | `MutableList.add ml v` module function | Phase 61 | Can now remove dispatch |
| `sb.Append(s)` dot dispatch | `StringBuilder.add sb s` module function | Phase 61 | Can now remove dispatch |
| `ht.TryGetValue(k)` dot dispatch | `Hashtable.tryGetValue ht k` module function | Phase 60 | Can now remove dispatch |
| Hashtable for-in yields KeyValuePair | Hashtable for-in yields TupleValue [k; v] | Phase 61 | KeyValuePair branch in Bidir.fs is dead code |

**Dead code confirmed:** The `TData("KeyValuePair", ...)` branch in Bidir.fs is dead code as of Phase 61 since hashtable for-in no longer yields KeyValuePair values.

## Open Questions

1. **`Array.length` availability**
   - What we know: `Array.length` built-in is listed in TypeCheck.fs initial type env. Need to verify `Prelude/Array.fun` exports it as `Array.length`.
   - What's unclear: Whether `Array.length` is accessible as `Array.length arr` in flt code.
   - Recommendation: Check `Prelude/Array.fun` in plan step 01. If missing, add `let length arr = array_length arr` to Array.fun.

2. **Constructor syntax `HashSet ()` after dispatch removal**
   - What we know: `HashSet ()`, `Queue ()`, `MutableList ()`, `StringBuilder ()` are handled by the `Constructor` arm in Eval.fs (lines 1293–1319), not by `FieldAccess`. They will continue to work after dot dispatch removal.
   - What's unclear: Phase intent — keep constructor syntax as valid or also phase it out?
   - Recommendation: Migrate flt tests to `HashSet.create ()` for consistency with the functional API goal, since `hashset-prelude.flt` already uses this pattern.

## Sources

### Primary (HIGH confidence)
- Direct code reading: `src/LangThree/Eval.fs` lines 1379–1537 — complete FieldAccess dispatch code
- Direct code reading: `src/LangThree/Bidir.fs` lines 571–663 — complete FieldAccess type synthesis code
- Direct code reading: `Prelude/*.fun` — all module API definitions
- Direct code reading: All 25 flt test files listed above

### Secondary (MEDIUM confidence)
- `src/LangThree/TypeCheck.fs` lines 610–673: `rewriteModuleAccess` confirms module qualified access is rewritten BEFORE reaching Bidir.fs — so `Module.member` FieldAccess never hits the value-type branches
- flt test run: 637/637 tests pass on current codebase (baseline confirmed)

## Metadata

**Confidence breakdown:**
- Exact code to delete: HIGH — read every line of Eval.fs 1440–1532 and Bidir.fs 574–649
- flt files requiring migration: HIGH — exhaustive grep of all 637 test files
- Module API correctness: HIGH — verified against Prelude/*.fun files and built-in function definitions
- Constructor syntax behavior: HIGH — verified Constructor arm in Eval.fs is separate from FieldAccess

**Research date:** 2026-03-29
**Valid until:** Stable — this is entirely internal code with no external dependencies
