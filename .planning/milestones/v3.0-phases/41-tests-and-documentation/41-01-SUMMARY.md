---
phase: 41-tests-and-documentation
plan: 01
subsystem: testing
tags: [flt, array, higher-order-functions, regression, test-suite]

# Dependency graph
requires:
  - phase: 40-array-higher-order
    provides: array_iter/map/fold/init builtins + Prelude/Array.fun wrappers
  - phase: 38-array-type
    provides: ArrayValue DU, basic array builtins (TST-18, TST-19 covered)
  - phase: 39-hashtable-type
    provides: HashtableValue DU, hashtable builtins (TST-21, TST-22 covered)
provides:
  - flt test: array-hof-iter.flt (Array.iter prints each element in order)
  - flt test: array-hof-map.flt (Array.map transforms to new array)
  - flt test: array-hof-fold.flt (Array.fold sums [1;2;3;4;5] = 15)
  - flt test: array-hof-init.flt (Array.init 4 (fun i -> i*i) = [0;1;4;9])
  - full test suite regression run: 486/486 passing
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: [flt test format: command/input/output sections; let _ = for unit expressions; curried fun a -> fun b -> for multi-arg HOF callbacks]

key-files:
  created:
    - tests/flt/file/array/array-hof-iter.flt
    - tests/flt/file/array/array-hof-map.flt
    - tests/flt/file/array/array-hof-fold.flt
    - tests/flt/file/array/array-hof-init.flt
  modified: []

key-decisions:
  - "TST-20 tests use Array.map/fold/init via element-by-element Array.get calls rather than printing the whole array (avoids dependency on array print format)"
  - "Array.fold test uses curried lambda fun acc -> fun x -> acc + x (multi-arg fun is a parse error in LangThree)"

patterns-established:
  - "flt HOF test pattern: create array with Array.ofList, apply HOF, read back with Array.get and println"

# Metrics
duration: 5min
completed: 2026-03-25
---

# Phase 41 Plan 01: Array HOF flt Tests Summary

**Four flt tests covering Array.iter/map/fold/init close TST-20; full 486-test suite passes with zero regressions**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-03-25T06:43:52Z
- **Completed:** 2026-03-25T06:48:37Z
- **Tasks:** 2
- **Files modified:** 4 (created)

## Accomplishments
- Created `array-hof-iter.flt`: verifies Array.iter prints 1, 2, 3 in order via println side effects
- Created `array-hof-map.flt`: verifies Array.map doubling produces 2, 4, 6 in new array
- Created `array-hof-fold.flt`: verifies Array.fold with curried accumulator sums [1;2;3;4;5] = 15
- Created `array-hof-init.flt`: verifies Array.init 4 (fun i -> i*i) produces [0;1;4;9]
- Ran full flt suite: 486/486 tests pass — TST-18 through TST-22 all green, no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Write flt tests for Array.iter/map/fold/init** - `bf2d744` (test)
2. **Task 2: Run full flt suite** - verification only, no files changed

## Files Created/Modified
- `tests/flt/file/array/array-hof-iter.flt` - Array.iter test: prints 1, 2, 3 via side effects
- `tests/flt/file/array/array-hof-map.flt` - Array.map test: [1;2;3] doubled to [2;4;6]
- `tests/flt/file/array/array-hof-fold.flt` - Array.fold test: sum of [1;2;3;4;5] = 15 with curried lambda
- `tests/flt/file/array/array-hof-init.flt` - Array.init test: squares [0;1;4;9] from index function

## Decisions Made
- Used `Array.get` element-by-element for map and init tests rather than printing the whole array — this avoids coupling the test to the array print format (`[|...|]`) which may change.
- fold test uses `fun acc -> fun x -> acc + x` (required since multi-arg `fun acc x ->` is a parse error in LangThree, per accumulated STATE.md decision).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- First run of four tests together via absolute paths (`/Users/ohama/.local/bin/fslit /abs/path1 /abs/path2 ...`) only ran the first test. Running each test individually (or via directory) worked correctly. Not a bug — just a quirk of the fslit runner's argument parsing with absolute paths. All four tests confirmed passing individually and via `fslit tests/flt/`.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- v3.0 milestone complete: TST-18 (array basics), TST-19 (array convert/OOB), TST-20 (array HOFs), TST-21 (hashtable basics), TST-22 (hashtable mutation/keys/remove) all passing
- Phase 41 is the final phase — project milestone v3.0 Mutable Data Structures is done
- 486 flt tests green, 0 failures

---
*Phase: 41-tests-and-documentation*
*Completed: 2026-03-25*
