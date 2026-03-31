---
phase: 72-type-checker-constraint-inference
plan: 03
subsystem: testing
tags: [typeclasses, constraints, flt, integration-tests, E0701, E0702]

# Dependency graph
requires:
  - phase: 72-02
    provides: constraint inference, constraint resolution, NoInstance/DuplicateInstance errors

provides:
  - flt integration test: typeclass-infer-basic.flt (constraint inference, Show 'a => 'a -> string)
  - flt integration test: typeclass-infer-resolve.flt (constraint resolution at Show int and Show bool call sites)
  - flt integration test: typeclass-infer-errors.flt (E0701 no-instance error for show on function type)
  - flt integration test: typeclass-infer-poly.flt (E0702 duplicate instance error for Show int)

affects:
  - 73-dictionary-passing: integration tests lock in typeclass type-checking behavior for regression detection

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "flt check-mode tests: use --check %input with Stderr: OK (0 warnings) for type-check success"
    - "flt error tests: use --check %input with ExitCode: 1 and Stderr: first error line (FsLit does partial Stderr match)"

key-files:
  created:
    - tests/flt/file/typeclass/typeclass-infer-basic.flt
    - tests/flt/file/typeclass/typeclass-infer-resolve.flt
    - tests/flt/file/typeclass/typeclass-infer-errors.flt
    - tests/flt/file/typeclass/typeclass-infer-poly.flt
  modified: []

key-decisions:
  - "Tests use --check mode (type-check only) since Phase 73 dictionary elaboration not yet implemented -- runtime calls to typeclass methods would be unbound"
  - "String concatenation uses + (not ++), which works when types are known to be string"
  - "FsLit Stderr: section does partial match -- only first error line needed, path-containing --> line not needed"

patterns-established:
  - "Typeclass check-mode pattern: --check %input with Stderr: OK (0 warnings) for no-error tests"

# Metrics
duration: 3min
completed: 2026-03-31
---

# Phase 72 Plan 03: Integration Tests for Typeclass Constraint Inference Summary

**4 flt integration tests locking in all Phase 72 success criteria: E0701 no-instance error, E0702 duplicate instance, constraint inference, and multi-type constraint resolution**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-31T11:29:44Z
- **Completed:** 2026-03-31T11:32:02Z
- **Tasks:** 1
- **Files modified:** 4

## Accomplishments

- typeclass-infer-basic.flt: verifies show_twice type-checks with Show int resolved at call site
- typeclass-infer-resolve.flt: verifies show_twice resolves Show int and Show bool at two separate call sites
- typeclass-infer-errors.flt: verifies E0701 no-instance error when show applied to function type (no Show instance)
- typeclass-infer-poly.flt: verifies E0702 duplicate instance error when Show int declared twice
- All 9 typeclass tests pass (5 existing parse tests + 4 new inference tests)
- All 224 unit tests pass unchanged

## Task Commits

1. **Task 1: Create flt integration tests for type class inference** - `df9c42e` (test)

## Files Created/Modified

- `tests/flt/file/typeclass/typeclass-infer-basic.flt` - --check test: show_twice + Show int resolves, OK
- `tests/flt/file/typeclass/typeclass-infer-resolve.flt` - --check test: Show int + Show bool resolve at separate sites
- `tests/flt/file/typeclass/typeclass-infer-errors.flt` - --check test: E0701 for show on function type
- `tests/flt/file/typeclass/typeclass-infer-poly.flt` - --check test: E0702 for duplicate Show int instance

## Decisions Made

- Used `--check` mode instead of runtime evaluation because Phase 73 (dictionary elaboration) is not yet implemented. Calling typeclass methods at runtime would produce "unbound variable: show" since no dictionary is constructed yet.
- FsLit Stderr: section does partial matching (not exact). Only the first line of the error message is needed in the test -- the `-->` location line varies by temp file path and is excluded.
- `typeclass-infer-poly.flt` covers polymorphic let-generalization conceptually (show_twice gets a constrained scheme) but is named for the duplicate instance test which is the key error case in this file. The polymorph resolution test is in typeclass-infer-resolve.flt.

## Deviations from Plan

None - plan executed exactly as written. The test content suggestions in the plan were adapted to match actual binary behavior (+ for string concatenation, --check mode, exact error message format).

## Issues Encountered

- `++` operator is not string concatenation in LangThree (it is list append). Used `+` instead, which works when string type is known from context.
- `printfn "%d" x` in file mode causes a type error (format string parsing issue). Tests avoid runtime execution entirely by using `--check` mode.

## User Setup Required

None.

## Next Phase Readiness

- All four Phase 72 success criteria are now locked in by integration tests
- Phase 73 (dictionary passing) can proceed; regression tests will catch any constraint inference breakage
- typeclass-infer-basic.flt and typeclass-infer-resolve.flt can be upgraded to runtime tests once Phase 73+74 enable full method dispatch

---
*Phase: 72-type-checker-constraint-inference*
*Completed: 2026-03-31*
