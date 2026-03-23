---
phase: 25-polymorphic-gadt-return-types
plan: 01
subsystem: typechecker
tags: [gadt, bidir, type-inference, fresh-var, check-mode, synth-mode]

# Dependency graph
requires:
  - phase: 12-printf-output
    provides: stable test suite baseline (196 F# + 439 fslit tests)
provides:
  - synth-mode GADT match delegates to check via fresh type variable instead of raising E0401
  - GADT matches without annotation now type-check successfully
affects:
  - 25-polymorphic-gadt-return-types (remaining plans using this foundation)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Fresh-var delegation: synth creates freshVar(), calls check, returns (s, apply s freshTy)"
    - "Bidir else-branch guard: if isGadtMatch then ... else (non-GADT path)"

key-files:
  created: []
  modified:
    - src/LangThree/Bidir.fs
    - tests/LangThree.Tests/GadtTests.fs

key-decisions:
  - "Use InCheckMode context wrapper when delegating from synth to check for GADT match"
  - "Add explicit else branch to preserve non-GADT match path (required by F# expression semantics)"
  - "Update GADT-04 tests from E0401-expected to success-expected, reflecting v1.8 behavior"

patterns-established:
  - "Synth-to-check delegation pattern: freshVar + check + apply s freshTy"

# Metrics
duration: 10min
completed: 2026-03-23
---

# Phase 25 Plan 01: Polymorphic GADT Match Delegation Summary

**Synth-mode GADT match now delegates to check via freshVar() instead of raising E0401, enabling unannotated GADT matches**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-23T00:00:00Z
- **Completed:** 2026-03-23T00:10:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Replaced E0401 raise in synth-mode GADT match with fresh-variable check delegation
- GADT matches without type annotation now type-check successfully via per-branch refinement
- All 196 F# unit tests pass (3 GADT-04 tests updated to reflect new behavior)
- All 439 fslit tests pass — zero regressions

## Task Commits

1. **Task 1+2: Replace E0401 with fresh-var GADT match delegation** - `01647d4` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `src/LangThree/Bidir.fs` - synth Match branch: replaced raise with freshVar + check delegation + else guard
- `tests/LangThree.Tests/GadtTests.fs` - GADT-04 test group updated from E0401-expected to success-expected

## Decisions Made

- Used `InCheckMode (freshTy, "gadt-match", span) :: ctx` as the context wrapper when delegating, consistent with how Annot delegates to check (lines 133, 141)
- Added explicit `else` keyword before the non-GADT path because the `if` branch now returns a value (previously it always raised, making the else implicit)
- Updated GADT-04 tests rather than deleting them — repurposed to verify the new polymorphic match behavior

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] F# compiler error: missing else branch**
- **Found during:** Task 1 build
- **Issue:** The old code used the GADT `if` arm as a terminating guard (always raised). After replacing the raise with a return value, F# required an explicit `else` branch because `if/then` without `else` must return `unit`.
- **Fix:** Added `else` keyword before `let s1, scrutTy = synth ...` (the non-GADT path), which F# accepts as a valid if/then/else expression.
- **Files modified:** src/LangThree/Bidir.fs
- **Verification:** Build succeeded with 0 errors after fix
- **Committed in:** 01647d4

**2. [Rule 1 - Bug] Three GADT-04 tests expected E0401 for behavior that now succeeds**
- **Found during:** Task 2 test run
- **Issue:** GADT-04 tests verified the old E0401 error path; those inputs now type-check successfully
- **Fix:** Updated the 3 tests to verify successful type-checking and absence of E0401
- **Files modified:** tests/LangThree.Tests/GadtTests.fs
- **Verification:** All 196 tests pass
- **Committed in:** 01647d4

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bugs)
**Impact on plan:** Both fixes necessary for correctness. No scope creep.

## Issues Encountered

None beyond the two auto-fixed deviations above.

## Next Phase Readiness

- Foundation for polymorphic GADT return types is in place
- `eval : 'a Expr -> 'a` pattern is now unblocked — synth mode no longer rejects unannotated GADT matches
- Ready for plans that add new fslit tests exercising the polymorphic pattern

---
*Phase: 25-polymorphic-gadt-return-types*
*Completed: 2026-03-23*
