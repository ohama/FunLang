---
phase: 59-prelude-extensions
plan: 01
subsystem: runtime-builtins
tags: [eval, builtins, list, array, sort, seq]

dependency-graph:
  requires: [phases/56-hashset-queue, phases/57-mutablelist-hashtable, phases/58-language-constructs]
  provides: [list_sort_by, list_of_seq, array_sort, array_of_seq builtins in initialBuiltinEnv]
  affects: [phases/59-02-list-fun, phases/59-03-array-fun]

tech-stack:
  added: []
  patterns: [BuiltinValue curried wrapping, callValue for user closures, Value.valueCompare for ordering, System.Array.Sort with IComparer lambda]

key-files:
  created: []
  modified:
    - src/LangThree/Eval.fs

decisions:
  - Used callValue (not callValueRef) for list_sort_by since callValue delegates to callValueRef and is the canonical pattern used by array_map
  - array_sort returns TupleValue [] (unit) consistent with other in-place mutation builtins (array_set)
  - array_of_seq copies arrays (Array.copy) rather than sharing the reference, preventing aliasing bugs

metrics:
  duration: "< 5 minutes"
  completed: 2026-03-29
---

# Phase 59 Plan 01: Prelude Extension Builtins Summary

Four new runtime builtins added to `initialBuiltinEnv` in Eval.fs providing the seq-conversion and sort primitives needed by Phase 59 Plans 02 and 03.

## What Was Built

| Builtin | Signature | Behavior |
|---------|-----------|----------|
| `list_sort_by` | `('a -> 'b) -> 'a list -> 'a list` | Applies key fn via callValue, sorts by key using Value.valueCompare |
| `list_of_seq` | `seq<'a> -> 'a list` | Converts list/array/HashSet/Queue/MutableList to ListValue |
| `array_sort` | `'a array -> unit` | In-place sort via System.Array.Sort, returns TupleValue [] |
| `array_of_seq` | `seq<'a> -> 'a array` | Converts any collection to ArrayValue (copies arrays) |

## Decisions Made

1. **callValue vs callValueRef** — Used `callValue` for `list_sort_by` because it already delegates through the forward reference. This is the same pattern used by `array_map` and `array_fold`.

2. **array_of_seq copies arrays** — When the input is already an `ArrayValue`, `Array.copy` is used to avoid aliasing.

3. **array_sort returns unit** — Returns `TupleValue []` consistent with other in-place mutation builtins in the codebase.

## Verification

Build output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

All four builtins confirmed present via grep of Eval.fs (lines 759-795).

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

Plans 59-02 (List.fun) and 59-03 (Array.fun) can now call `list_sort_by`, `list_of_seq`, `array_sort`, and `array_of_seq` as raw builtins from their LangThree wrappers.
