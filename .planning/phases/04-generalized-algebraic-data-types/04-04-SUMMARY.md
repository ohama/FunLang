---
phase: 04-generalized-algebraic-data-types
plan: 04
subsystem: type-system
tags: [gadt, exhaustiveness, pattern-matching, type-filtering]

# Dependency graph
requires:
  - phase: 02-algebraic-data-types
    provides: "Exhaustiveness checking infrastructure (Maranget algorithm)"
  - phase: 04-generalized-algebraic-data-types (plan 02)
    provides: "GADT elaboration with IsGadt, ResultType, inferTypeFromPatterns"
provides:
  - "GADT-aware constructor filtering for exhaustiveness checking"
  - "filterPossibleConstructors function in Exhaustive.fs"
  - "inferSpecificScrutineeType for GADT scrutinee type resolution"
affects: [04-05, testing]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Type head mismatch filtering for GADT branches"]

key-files:
  created: []
  modified:
    - "src/LangThree/Exhaustive.fs"
    - "src/LangThree/TypeCheck.fs"

key-decisions:
  - "Two-phase type inference: generic type for constructor lookup, specific type for GADT filtering"
  - "Conservative filtering: keep constructors when arity mismatch or type variables present"

patterns-established:
  - "filterPossibleConstructors: structural type arg comparison for GADT branch elimination"
  - "inferSpecificScrutineeType: raw ResultType from first GADT constructor for filtering"

# Metrics
duration: 3min
completed: 2026-03-09
---

# Phase 4 Plan 4: GADT Exhaustiveness Filtering Summary

**GADT-aware constructor filtering eliminates impossible branches by type head mismatch in exhaustiveness checking**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-09T07:00:25Z
- **Completed:** 2026-03-09T07:03:25Z
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments
- Added filterPossibleConstructors to Exhaustive.fs that filters GADT constructors by type argument compatibility
- Added inferSpecificScrutineeType in TypeCheck.fs to get raw GADT result type for filtering
- Wired filtered constructor set into both checkExhaustive and checkRedundant calls
- Non-GADT types and generic scrutinee types pass through unfiltered (backward compatible)

## Task Commits

Each task was committed atomically:

1. **Task 1: GADT-aware constructor filtering for exhaustiveness** - `6306170` (feat)

## Files Created/Modified
- `src/LangThree/Exhaustive.fs` - Added filterPossibleConstructors function for GADT branch filtering
- `src/LangThree/TypeCheck.fs` - Added inferSpecificScrutineeType, wired filtering into exhaustiveness/redundancy checks

## Decisions Made
- **Two-phase type inference:** inferTypeFromPatterns returns generic type (for getConstructorsFromEnv to find all constructors), while inferSpecificScrutineeType returns the raw ResultType for GADT filtering. This avoids modifying the existing working code path.
- **Conservative filtering:** When constructor return type args have free variables, or scrutinee type has variables, the constructor is kept (not filtered). This ensures we never incorrectly eliminate a reachable branch.
- **Structural equality for type comparison:** Direct equality check (scrutArg = ctorArg) for ground types, skip comparison when either side has type variables.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- GADT exhaustiveness filtering in place, ready for integration testing in 04-05
- filterPossibleConstructors is pure and independently testable
- All 115 existing tests pass, confirming no regression

---
*Phase: 04-generalized-algebraic-data-types*
*Completed: 2026-03-09*
