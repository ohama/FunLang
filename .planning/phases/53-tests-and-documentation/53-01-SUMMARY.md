---
phase: 53-tests-and-documentation
plan: "01"
subsystem: testing
tags: [flt, regression, nlseq, newline-sequencing, structural-terminator, multiline-app]

# Dependency graph
requires:
  - phase: 50-newline-sequencing
    provides: SEMICOLON injection with structural terminator and continuation guards
provides:
  - NLSEQ regression test: structural terminators (else/with/|) not preceded by spurious SEMICOLON
  - NLSEQ regression test: multi-line function application with indented args works correctly
affects: [any future newline-sequencing changes]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created:
    - tests/flt/expr/seq/nlseq-structural-terminator.flt
    - tests/flt/expr/seq/nlseq-multiline-app.flt
  modified: []

key-decisions:
  - "Used let _ = result as last line in multiline-app test to avoid trailing-newline parse error from flt runner input extraction"

patterns-established: []

# Metrics
duration: 3min
completed: 2026-03-29
---

# Phase 53 Plan 01: NLSEQ Regression Tests Summary

**Two flt regression tests for NLSEQ: structural-terminator guard and multi-line function application with indented args**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-03-28T23:47:46Z
- **Completed:** 2026-03-28T23:50:47Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Added `nlseq-structural-terminator.flt` confirming `else` at same indent is NOT preceded by spurious SEMICOLON when then-block has multiple statements
- Added `nlseq-multiline-app.flt` confirming `add3` called with each argument on its own indented continuation line evaluates correctly to 6
- Full seq/ suite (12 tests) passes with no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add nlseq-structural-terminator.flt** - `4378d6d` (test)
2. **Task 2: Add nlseq-multiline-app.flt** - `8e25012` (test)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `tests/flt/expr/seq/nlseq-structural-terminator.flt` - Regression test for structural terminator guard; if/then/else with multi-statement then-block
- `tests/flt/expr/seq/nlseq-multiline-app.flt` - Regression test for multi-line function application; add3 with args on separate indented lines

## Decisions Made

- In `nlseq-multiline-app.flt`, used `let _ = result` as the final line rather than `let result = add3 ...` alone. The flt runner strips the trailing newline from extracted input; without `let _ = result` the parser encountered a parse error because `3` ended the file without a final newline. Adding `let _ = result` makes `result` the non-indented last token so parsing succeeds.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] flt input trailing-newline causes parse error for multiline app test**

- **Found during:** Task 2 (nlseq-multiline-app.flt)
- **Issue:** Plan's suggested input ended with `        3` as the last line; flt runner strips trailing newline, causing parse error in LangThree binary
- **Fix:** Added `let _ = result` after the multiline application block so the last line is a well-formed top-level declaration
- **Files modified:** tests/flt/expr/seq/nlseq-multiline-app.flt
- **Verification:** `../fslit/dist/FsLit tests/flt/expr/seq/nlseq-multiline-app.flt` passes; full seq/ suite 12/12 pass
- **Committed in:** 8e25012 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Auto-fix necessary for test correctness. No scope creep.

## Issues Encountered

- flt runner strips trailing newline from extracted input; this matters when the last input line is a heavily-indented continuation (like `        3`) since the parser needs the newline to finalize the expression. Worked around by appending a top-level `let _ = result` binding.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Both regression tests pass and are committed
- Full seq/ suite (12/12) passes
- Phase 53 Plan 01 complete; remaining plans in phase 53 can proceed

---
*Phase: 53-tests-and-documentation*
*Completed: 2026-03-29*
