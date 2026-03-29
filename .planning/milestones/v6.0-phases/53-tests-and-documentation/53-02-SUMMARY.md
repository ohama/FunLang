---
phase: 53-tests-and-documentation
plan: 02
subsystem: docs
tags: [tutorial, korean, mdbook, option, result, for-in, newline-sequencing]

# Dependency graph
requires:
  - phase: 50-newline-sequencing
    provides: newline implicit sequencing in indent blocks
  - phase: 51-for-in-loops
    provides: for-in collection loop syntax
  - phase: 52-option-result-prelude
    provides: optionMap, optionBind, optionFilter, optionDefaultValue, optionIter, optionIsSome, optionIsNone, resultMap, resultToOption, resultDefaultValue, resultIter
provides:
  - tutorial/22-practical-programming.md: 223-line Korean tutorial chapter covering all v6.0 features
  - tutorial/SUMMARY.md updated with chapter 22 entry
affects: [future tutorial chapters, v6.0 release docs]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "All tutorial examples verified against binary before writing"
    - "Korean prose with English code/identifiers following chapter 21 style"

key-files:
  created:
    - tutorial/22-practical-programming.md
  modified:
    - tutorial/SUMMARY.md

key-decisions:
  - "Used ^^ (not ^) for string concatenation — ^ is not a LangThree operator"
  - "Example 1 adapted from plan: replaced ^ with ^^ after binary verification"

patterns-established:
  - "Binary verification before writing any expected output in tutorials"

# Metrics
duration: 3min
completed: 2026-03-29
---

# Phase 53 Plan 02: Practical Programming Tutorial Summary

**223-line Korean tutorial chapter 22 covering newline sequencing, for-in loops, and Option/Result utilities, with all 8 code examples verified against the LangThree binary**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-28T23:48:14Z
- **Completed:** 2026-03-28T23:50:46Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Verified all 8 tutorial code examples against the binary, recording exact outputs
- Wrote tutorial/22-practical-programming.md (223 lines) in Korean with English code, matching chapter 21 style
- Added chapter 22 entry to tutorial/SUMMARY.md under 실용 프로그래밍 section
- Covered all three v6.0 features: newline implicit sequencing, for-in collection loops, Option/Result utilities

## Task Commits

Each task was committed atomically:

1. **Task 1: Verify all tutorial code examples against the binary** - (no files changed; verification only)
2. **Task 2: Write tutorial/22-practical-programming.md and update SUMMARY.md** - `dd86812` (docs)

**Plan metadata:** (included in task 2 commit)

## Files Created/Modified
- `tutorial/22-practical-programming.md` - Chapter 22: 실용 프로그래밍 covering v6.0 features (223 lines)
- `tutorial/SUMMARY.md` - Added chapter 22 entry after chapter 21

## Decisions Made
- **`^^` not `^` for string concatenation:** The plan's example 1 used `"Hello, " ^ name` but `^` is not a LangThree operator. Tests and existing tutorial examples use `^^`. Updated example to use `^^` after binary verification returned "Error: unrecognized input".

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed string concat operator in example 1**
- **Found during:** Task 1 (binary verification)
- **Issue:** Plan specified `"Hello, " ^ name` but `^` is not a LangThree operator; binary returned "Error: unrecognized input"
- **Fix:** Changed to `"Hello, " ^^ name` (the correct LangThree string concat operator)
- **Files modified:** tutorial/22-practical-programming.md
- **Verification:** Binary ran successfully and returned `Hello, Alice\nWelcome to LangThree\n()`
- **Committed in:** dd86812 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug in plan's example code)
**Impact on plan:** Auto-fix necessary for correctness. The tutorial example now runs correctly.

## Issues Encountered
- Plan example 1 used `^` for string concatenation, which is not a LangThree operator. The actual operator is `^^`. Discovered and corrected during binary verification in Task 1.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 53 Plan 02 complete. Phase 53 (tests and documentation) may have additional plans.
- Tutorial chapter 22 satisfies TST-36 (documentation for v6.0 features).
- 589/589 tests still passing (no code changes made).

---
*Phase: 53-tests-and-documentation*
*Completed: 2026-03-29*
