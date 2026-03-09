---
phase: 04-generalized-algebraic-data-types
plan: 03
subsystem: type-checking
tags: [gadt, bidirectional, type-refinement, existential, pattern-matching]

requires:
  - phase: 04-02
    provides: "GADT elaboration with IsGadt sweep, ExistentialVars, inferTypeFromPatterns"
  - phase: 02-algebraic-data-types
    provides: "ADT pattern matching in synth/check modes"
provides:
  - "GADT-aware Match handling in both synth and check modes"
  - "Local type refinement per GADT branch in check mode"
  - "GadtAnnotationRequired enforcement in synth mode"
  - "Existential type variable escape detection"
affects: [04-04, 04-05]

tech-stack:
  added: []
  patterns:
    - "isGadtMatch guard for GADT vs regular ADT dispatch"
    - "Per-branch local constraint scoping via fresh unification"
    - "Existential escape check via freeVars on result type"

key-files:
  created: []
  modified:
    - "src/LangThree/Bidir.fs"

key-decisions:
  - "Local constraints from unifying scrutinee with constructor return type stay branch-local"
  - "Only body substitution propagates across branches (no cross-branch constraint leakage)"
  - "GADT match in synth mode infers scrutinee type first, then raises E0401 with formatted type"

patterns-established:
  - "isGadtMatch helper: check any clause has GADT constructor for dispatch"
  - "GADT check mode: fresh instantiation -> local unification -> pattern binding -> body check -> escape check"

duration: 2min
completed: 2026-03-09
---

# Phase 4 Plan 3: GADT Type Refinement Summary

**Bidirectional GADT pattern matching with per-branch local type refinement, annotation enforcement, and existential escape detection**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-09T07:00:06Z
- **Completed:** 2026-03-09T07:02:02Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- GADT matches in synth mode raise GadtAnnotationRequired (E0401) with formatted scrutinee type
- GADT check mode applies local type refinement per branch: unifies scrutinee with constructor return type for local constraints
- Existential type variables checked for escape after body type checking (E0402)
- Regular ADT pattern matching completely unaffected (isGadtMatch guard)

## Task Commits

Each task was committed atomically:

1. **Task 1: GADT detection helper and annotation enforcement in synth mode** - `3575fe4` (feat)
2. **Task 2: GADT type refinement in check mode with existential escape detection** - `0d086c7` (feat)

## Files Created/Modified
- `src/LangThree/Bidir.fs` - isGadtMatch helper, synth mode GADT error, check mode GADT refinement with existential escape detection

## Decisions Made
- Local constraints from unifying scrutinee with constructor return type are NOT composed into the global substitution -- they only affect branch environment and expected type
- Each branch gets independent local constraints computed via fresh variable instantiation -- no cross-branch leakage
- Body substitution propagates because it may resolve globally-scoped type variables
- Existential vars checked after body type-checking by inspecting freeVars of the result type

## Deviations from Plan
None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- GADT type refinement is wired into the bidirectional checker
- Ready for 04-04 (GADT integration tests) to validate end-to-end behavior
- Ready for 04-05 (GADT evaluator) to add runtime support

---
*Phase: 04-generalized-algebraic-data-types*
*Completed: 2026-03-09*
