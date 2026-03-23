---
phase: 25-polymorphic-gadt-return-types
plan: "04"
subsystem: type-system
tags: [gadt, bidirectional-typing, polymorphism, type-inference, fsharp]

# Dependency graph
requires:
  - phase: 25-polymorphic-gadt-return-types
    provides: GADT check-mode handler in Bidir.fs and regression test infrastructure from plans 01-03

provides:
  - Per-branch independent result type in GADT check-mode polymorphic match (isPolyExpected fix)
  - eval : 'a Expr -> 'a type-checks: IntLit branch returns int, BoolLit branch returns bool
  - GADT-05 F# unit test group (3 tests) for cross-type polymorphic return
  - Updated gadt-poly-eval.flt testing two-branch cross-type eval

affects:
  - Any future GADT work building on polymorphic match semantics

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "isPolyExpected: detect unbound TVar expected before folder accumulation to gate per-branch isolation"
    - "Polymorphic GADT mode: apply combinedLocalS directly to original expected (not accumulated s); don't compose bodyS into cross-branch accumulator"

key-files:
  created: []
  modified:
    - src/LangThree/Bidir.fs
    - tests/LangThree.Tests/GadtTests.fs
    - tests/flt/file/adt/gadt-poly-eval.flt

key-decisions:
  - "isPolyExpected gates on apply s1 expected being a TVar — uses s1 (scrutinee synth) not the folder's accumulated s"
  - "In polymorphic mode, bodyS is branch-local and NOT composed into cross-branch accumulator s; concrete mode keeps existing behavior"
  - "localExpected = apply combinedLocalS expected uses combinedLocalS (constructor unification) to refine original expected TVar per branch"
  - "flt test uses printf for r1 to get two outputs since runtime only prints last let binding"

patterns-established:
  - "isPolyExpected pattern: check unbound TVar before folder to branch between polymorphic and concrete GADT match modes"

# Metrics
duration: 15min
completed: 2026-03-23
---

# Phase 25 Plan 04: Per-branch Independent Result Type for Polymorphic GADT Summary

**Per-branch independent GADT result type via isPolyExpected flag in Bidir.fs, enabling eval : 'a Expr -> 'a with cross-type IntLit/BoolLit branches**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-23T00:00:00Z
- **Completed:** 2026-03-23T00:15:00Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- Fixed the root cause of E0301 in two-branch GADT matches: the folder accumulated substitution `s` was propagating branch 1's result-type unification into branch 2
- Added `isPolyExpected` flag (true when `apply s1 expected` is an unbound TVar) to detect polymorphic mode before the `folder` function
- In polymorphic mode: each branch applies `combinedLocalS` to the original `expected` independently, `bodyS` is NOT composed into `s`
- In concrete mode: existing behavior preserved exactly (no regression)
- Added GADT-05 test group (3 tests): type-checks without E0301, IntLit→42, BoolLit→true
- Updated gadt-poly-eval.flt to two-branch cross-type pattern; all 442 fslit tests pass

## Task Commits

1. **Task 1: Fix Bidir.fs GADT check-mode folder** - `bbb29ac` (feat)
2. **Task 2: Add GADT-05 F# tests and update flt** - `eb2d229` (test)

**Plan metadata:** (docs commit below)

## Files Created/Modified

- `src/LangThree/Bidir.fs` - Added `isPolyExpected` and polymorphic/concrete branch split in GADT folder (lines ~538-660)
- `tests/LangThree.Tests/GadtTests.fs` - Added `parseAndEval` helper and `GADT-05` testList with 3 tests
- `tests/flt/file/adt/gadt-poly-eval.flt` - Updated to two-branch cross-type eval (output: 42 then true)

## Decisions Made

- **isPolyExpected uses s1 (scrutinee subst), not the folder's s**: The right baseline — after scrutinee synthesis, does the expected type remain free? Yes → polymorphic mode.
- **localExpected = apply combinedLocalS expected**: Uses constructor unification (e.g., 'a→int for IntLit) to refine the original expected TVar per branch. Each branch starts from the same original TVar, so they're independent.
- **bodyS not composed in polymorphic mode**: Branch body constraints stay local; cross-branch `s` stays at `s1` throughout, so no cross-contamination.
- **flt uses `printf "%d\n"` for r1**: Runtime only prints last let binding, so printf used to show r1, last binding shows r2.

## Deviations from Plan

None — plan executed exactly as written. The implementation approach in the prompt context was followed precisely.

## Issues Encountered

None - the fix compiled and passed all tests on the first attempt.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- COV-01 and TYP-03 gaps are now closed: `eval : 'a Expr -> 'a` fully works
- Phase 25 (v1.8 Polymorphic GADT) is complete: all 4 plans (01-04) done
- 199 F# unit tests passing, 442 fslit tests passing
- Ready for v1.9 milestone

---
*Phase: 25-polymorphic-gadt-return-types*
*Completed: 2026-03-23*
