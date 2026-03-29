---
phase: 62-remove-dot-dispatch
plan: 02
subsystem: testing
tags: [flt, hashset, queue, mutablelist, module-api, dot-notation-removal]

# Dependency graph
requires:
  - phase: 62-remove-dot-dispatch-01
    provides: HashSet/Queue/MutableList module API builtins and Prelude .fun modules
provides:
  - 15 flt test files migrated from dot notation to module function API
  - HashSet/Queue/MutableList tests using HashSet.create/add/contains/count, Queue.create/enqueue/dequeue/count, MutableList.create/add/count
  - Cross-type tests (list, prelude, property) all using module API
affects: [62-remove-dot-dispatch-03, milestone-v7.1]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Collection module API: HashSet.create/add/contains/count, Queue.create/enqueue/dequeue/count, MutableList.create/add/count"
    - "ml.[i] indexing (IndexGet/IndexSet) preserved — not dot notation"
    - "Queue.dequeue q () — unit arg passed explicitly"

key-files:
  created:
    - tests/flt/file/list/list-comp-from-collections.flt
    - tests/flt/file/mutablelist/mutablelist-bounds-error.flt
    - tests/flt/file/prelude/prelude-ofseq-sort-pipeline.flt
    - tests/flt/file/property/property-count-consistency.flt
  modified:
    - tests/flt/file/hashset/hashset-basic.flt
    - tests/flt/file/hashset/hashset-strings.flt
    - tests/flt/file/hashset/hashset-forin.flt
    - tests/flt/file/queue/queue-basic.flt
    - tests/flt/file/queue/queue-error.flt
    - tests/flt/file/queue/queue-forin.flt
    - tests/flt/file/mutablelist/mutablelist-basic.flt
    - tests/flt/file/mutablelist/mutablelist-forin.flt
    - tests/flt/file/mutablelist/mutablelist-indexing.flt
    - tests/flt/file/prelude/prelude-list-ofseq.flt
    - tests/flt/file/prelude/prelude-array-sort-ofseq.flt

key-decisions:
  - "ml.[i] indexing (IndexGet) and ml.[i] <- v (IndexSet) are NOT dot notation — leave unchanged"
  - "queue-error.flt expected output 'Queue.Dequeue: queue is empty' comes from builtin, not dot dispatch — not changed"
  - "Queue.dequeue signature is dequeue q u — pass () explicitly at call sites"

patterns-established:
  - "Module API pattern: Type.create () to construct, Type.verb collection args for mutation/query"
  - "No dot notation on any collection value in flt tests"

# Metrics
duration: 8min
completed: 2026-03-29
---

# Phase 62 Plan 02: Remove Dot Dispatch — Collection flt Test Migration Summary

**15 flt test files rewritten from dot notation (hs.Add/q.Enqueue/ml.Add/.Count) to HashSet/Queue/MutableList module API — all 15 pass**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-03-29T10:54:54Z
- **Completed:** 2026-03-29T11:02:00Z
- **Tasks:** 2
- **Files modified:** 15 (11 modified, 4 created/promoted from untracked)

## Accomplishments

- Migrated all 6 HashSet and Queue flt tests to module function API
- Migrated all 4 MutableList flt tests, preserving ml.[i] indexing syntax
- Migrated 5 cross-type tests (list comprehension, prelude ofSeq/sort, property count consistency)
- All 15 files verified passing with FsLit runner

## Task Commits

Each task was committed atomically:

1. **Task 1: Migrate HashSet and Queue tests** - `ba5c634` (feat)
2. **Task 2: Migrate MutableList and cross-type tests** - `844d6fc` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `tests/flt/file/hashset/hashset-basic.flt` - HashSet() -> HashSet.create(), dot methods -> module functions
- `tests/flt/file/hashset/hashset-strings.flt` - Same migration
- `tests/flt/file/hashset/hashset-forin.flt` - Same migration
- `tests/flt/file/queue/queue-basic.flt` - Queue() -> Queue.create(), .Enqueue/.Dequeue/.Count -> module functions
- `tests/flt/file/queue/queue-error.flt` - Queue.create(), preserved error message from builtin
- `tests/flt/file/queue/queue-forin.flt` - Same migration
- `tests/flt/file/mutablelist/mutablelist-basic.flt` - MutableList.create/add/count, ml.[i] preserved
- `tests/flt/file/mutablelist/mutablelist-forin.flt` - MutableList.create/add
- `tests/flt/file/mutablelist/mutablelist-indexing.flt` - MutableList.create/add/count, ml.[0] and ml.[0]<-999 unchanged
- `tests/flt/file/mutablelist/mutablelist-bounds-error.flt` - MutableList.create/add/count (was untracked)
- `tests/flt/file/list/list-comp-from-collections.flt` - HashSet.create/add, Queue.create/enqueue (was untracked)
- `tests/flt/file/prelude/prelude-list-ofseq.flt` - All three collection types migrated
- `tests/flt/file/prelude/prelude-array-sort-ofseq.flt` - Queue migration
- `tests/flt/file/prelude/prelude-ofseq-sort-pipeline.flt` - MutableList + Queue migration (was untracked)
- `tests/flt/file/property/property-count-consistency.flt` - All four collection types migrated (was untracked)

## Decisions Made

- ml.[i] indexing (IndexGet/IndexSet) is not dot notation — left exactly as written in all tests
- queue-error.flt's expected output "Queue.Dequeue: queue is empty" comes from the builtin runtime, not dot dispatch — left unchanged
- Queue.dequeue passes unit explicitly: `Queue.dequeue q ()` — matches the module function signature `let dequeue q u = queue_dequeue q u`

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All HashSet, Queue, MutableList, and cross-type flt tests now use module API
- Dot notation eliminated from collection tests
- Ready for Phase 62 Plan 03 or milestone v7.1 close

---
*Phase: 62-remove-dot-dispatch*
*Completed: 2026-03-29*
