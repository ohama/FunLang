---
phase: 103-fix-bidir-annotationmap-lambdaannot-span
plan: 02
subsystem: testing
tags: [fsharp, bidir, annotationmap, letrec, expecto, regression]

# Dependency graph
requires:
  - phase: 103-01
    provides: 6-tuple LetRec binding, Bidir.fs TArrow recording at firstSpOpt, Issue #19 fix
provides:
  - TA-09 unit test: let rec with 3 annotated params produces >= 3 distinct TArrow entries
  - TA-09b unit test: mutual rec with annotated params records first-param spans for both bindings
  - letrec-annot-first-param-map.flt: Issue #19 regression test (f 100 20 0 = 120)
affects: [Phase 96, future annotationMap consumers]

# Tech tracking
tech-stack:
  added: []
  patterns: [parseModuleWithPositions used for span-aware annotationMap tests]

key-files:
  created:
    - tests/flt/file/let/letrec-annot-first-param-map.flt
  modified:
    - tests/FunLang.Tests/TypeAnnotationTests.fs

key-decisions:
  - "TA-09b mutual rec input changed from 'g x' (type error) to 'g (x > 0)' to produce valid bool arg"

patterns-established:
  - "Regression tests for annotationMap use parseModuleWithPositions + Bidir.annotationMap.Clear() before TypeCheck.typeCheckModule"

# Metrics
duration: 5min
completed: 2026-04-10
---

# Phase 103 Plan 02: Regression Tests for annotationMap let rec First-Param Fix Summary

**TA-09/TA-09b unit tests and letrec-annot-first-param-map.flt flt regression test for Issue #19 annotationMap let rec first-param fix**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-04-10T00:00:00Z
- **Completed:** 2026-04-10T00:05:00Z
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments
- Added TA-09 test: let rec with 3 annotated params produces >= 3 distinct TArrow entries in annotationMap
- Added TA-09b test: mutual rec `and` binding records first-param spans for both `f` and `g`
- Added flt regression test: `let rec f (x:int) (y:int) (z:int) = x + y + z` with `f 100 20 0` outputs `120`
- All 247 unit tests and 727 flt tests pass (no regressions)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add TA-09 unit test and flt regression test for let rec first-param annotationMap** - `25d2d2c` (test)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `tests/FunLang.Tests/TypeAnnotationTests.fs` - Added TA-09, TA-09b tests; 52 lines inserted
- `tests/flt/file/let/letrec-annot-first-param-map.flt` - New flt regression test for Issue #19

## Decisions Made
- TA-09b mutual rec input needed to be `g (x > 0)` rather than `g x` because `g : bool -> int` requires a bool argument; `x : int` would cause a type error

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed TA-09b type mismatch in test input**
- **Found during:** Task 1 (running unit tests)
- **Issue:** Plan specified `let rec f (x : int) = g x\nand g (y : bool) = 0` but `g x` passes `int` to `g : bool -> int`, causing E0301 type mismatch
- **Fix:** Changed to `g (x > 0)` so `bool` is passed to `g`
- **Files modified:** tests/FunLang.Tests/TypeAnnotationTests.fs
- **Verification:** All 247 unit tests pass after fix
- **Committed in:** 25d2d2c (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug in plan's test input)
**Impact on plan:** Test input was incorrect; fix preserves the intent of TA-09b while making it type-correct.

## Issues Encountered
- Plan's TA-09b input had a type error: `g x` passes `int` to `g : bool -> int`. Fixed automatically.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 103 fully complete (Issues #18 and #19 fixed with regression tests)
- Test baseline: 727 flt + 247 unit tests
- Ready for Phase 96 (Correctness Foundations: TC-01–04)

---
*Phase: 103-fix-bidir-annotationmap-lambdaannot-span*
*Completed: 2026-04-10*
