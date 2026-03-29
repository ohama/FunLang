---
phase: 57-mutablelist-hashtable-enhancement
plan: "01"
subsystem: native-collections
tags: [mutablelist, collections, ast, eval, bidir, prelude]

dependency-graph:
  requires: [56-01]
  provides: [MutableListValue DU case, MutableList constructor interception, FieldAccess dispatch, IndexGet/IndexSet, Bidir type rules, Prelude/MutableList.fun, raw builtins]
  affects: [57-02]

tech-stack:
  added: []
  patterns: [constructor-interception, fieldaccess-dispatch, raw-builtin-pattern, indexget-indexset-bounds-check]

key-files:
  created:
    - Prelude/MutableList.fun
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/TypeCheck.fs

decisions:
  - id: raw-builtins-for-prelude
    choice: "Used raw builtins (mutablelist_*) in Prelude/MutableList.fun instead of dot-dispatch"
    reason: "Dot-dispatch on TVar causes type warning during Prelude loading (same pattern as HashSet/Queue Preludes)"

metrics:
  duration: ~8 minutes
  completed: 2026-03-29
---

# Phase 57 Plan 01: MutableList Native Collection Summary

**One-liner:** MutableListValue wrapping System.Collections.Generic.List<Value> with constructor interception, FieldAccess .Add/.Count, IndexGet/IndexSet bounds-checking, Bidir type rules, and Prelude module using raw builtins.

## What Was Built

Added `MutableList` as a new native collection type following the Phase 56 HashSet/Queue pattern:

1. **Ast.fs** — `MutableListValue of System.Collections.Generic.List<Value>` DU case with GetHashCode (reference equality), valueEqual (reference equality), valueCompare (returns 0), and formatValue (`MutableList[elem; ...]`).

2. **Eval.fs** — Four integration points:
   - Constructor interception: `"MutableList"` → `MutableListValue(List<Value>())` (handles both `MutableList()` and `MutableList ()`).
   - FieldAccess dispatch: `.Add(v)` returns `TupleValue []` (mutates list), `.Count` returns `IntValue` directly (not wrapped in BuiltinValue).
   - IndexGet: bounds-checked `ml.[i]` raising `LangThreeException` on out-of-bounds.
   - IndexSet: bounds-checked `ml.[i] <- v` raising `LangThreeException` on out-of-bounds.
   - Raw builtins: `mutablelist_create`, `mutablelist_add`, `mutablelist_get`, `mutablelist_set`, `mutablelist_count`.

3. **Bidir.fs** — Four type rules:
   - Constructor synthesis: `"MutableList"` → `TData("MutableList", [])`.
   - FieldAccess: `.Add` → `TArrow(tv, TTuple [])`, `.Count` → `TInt`.
   - IndexGet: index must be `TInt`, returns fresh type var.
   - IndexSet: index must be `TInt`, returns `TTuple []`.

4. **TypeCheck.fs** — Type schemes for all five raw builtins.

5. **Prelude/MutableList.fun** — `create`, `add`, `get`, `set`, `count` using raw builtins.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add MutableListValue to Ast.fs and Eval.fs | 6401340 | Ast.fs, Eval.fs |
| 2 | Add MutableList type rules to Bidir.fs and create Prelude/MutableList.fun | 5d62920 | Bidir.fs, Eval.fs, TypeCheck.fs, Prelude/MutableList.fun |

## Verification

All three smoke tests passed:

1. **Basic operations:** `MutableList()`, `.Add(1)`, `.Add(2)`, `.Add(3)`, `.Count` → `3`, `.[1]` → `2`, `()`
2. **IndexSet:** `.Add(99)`, `.[0] <- 42`, `.[0]` → `42`, `()`
3. **Prelude module:** `open MutableList; create()`, `add`, `count`, `get` → correct values

Build: zero errors, zero warnings.

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Prelude implementation style | Raw builtins (`mutablelist_*`) | Dot-dispatch on TVar fails type check during Prelude loading; same pattern as HashSet/Queue |
| Added `mutablelist_set` | Yes (not in original plan) | Needed for symmetry with IndexSet; allows Prelude to expose full set functionality |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Added raw builtins + TypeCheck.fs type schemes**

- **Found during:** Task 2 when testing Prelude/MutableList.fun with dot-dispatch
- **Issue:** Prelude functions using dot-dispatch (`ml.Add v`, `ml.[i]`) caused type error warning "Cannot access field on non-record type 'v'" because `ml` is a TVar in the Prelude function body
- **Fix:** Added raw builtins `mutablelist_create/add/get/set/count` in Eval.fs and TypeCheck.fs; rewrote Prelude/MutableList.fun to use raw builtins (identical pattern to HashSet/Queue Preludes)
- **Files modified:** src/LangThree/Eval.fs, src/LangThree/TypeCheck.fs, Prelude/MutableList.fun
- **Commit:** 5d62920

## Next Phase Readiness

- Phase 57 Plan 02 can proceed (Hashtable enhancements)
- MutableList is fully functional for COLL-04 requirements
- No blockers or concerns
