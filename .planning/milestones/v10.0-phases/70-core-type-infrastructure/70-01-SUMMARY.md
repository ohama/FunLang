---
phase: 70-core-type-infrastructure
plan: 01
subsystem: type-system
tags: [fsharp, type-inference, type-classes, hindley-milner, scheme, constraints]

# Dependency graph
requires:
  - phase: 69-cli-extensions
    provides: "stable build baseline (phases 1-69 complete)"
provides:
  - "Scheme(vars, constraints, ty) 3-field type scheme throughout all type inference modules"
  - "Constraint type: { ClassName: string; TypeArg: Type }"
  - "ClassInfo, InstanceInfo, ClassEnv, InstanceEnv types in Type.fs"
  - "mkScheme/schemeType backward-compat helpers"
  - "formatSchemeNormalized displays constraint context (e.g., 'Show 'a => 'a -> string')"
affects:
  - 70-02
  - 71-class-instance-declarations
  - 72-constraint-propagation
  - 73-dictionary-passing
  - 74-type-class-stdlib

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "3-field Scheme(vars, constraints, ty) as the universal polymorphic type representation"
    - "ClassEnv/InstanceEnv as Map-based environments (same pattern as TypeEnv/ConstructorEnv)"
    - "Backward-compat helpers mkScheme/schemeType for gradual migration sites in Phase 71+"

key-files:
  created: []
  modified:
    - src/LangThree/Type.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs

key-decisions:
  - "Scheme shape change done atomically in a single plan — F# exhaustive matching flags all incomplete sites immediately"
  - "InstanceInfo defined without MethodBodies for Phase 70 (placeholder); Phase 71 adds Expr-typed bodies after AST changes"
  - "mkScheme/schemeType added as zero-cost helpers for Phase 71+ gradual migration sites"
  - "ClassEnv/InstanceEnv are Map types (not mutable refs) — threading strategy deferred to Phase 72"

patterns-established:
  - "Constraint: minimal record type { ClassName; TypeArg } avoids circular dependency with Expr"
  - "formatSchemeNormalized: collects vars from constraints + ty before building normalization map for consistent variable naming"

# Metrics
duration: 4min
completed: 2026-03-31
---

# Phase 70 Plan 01: Core Type Infrastructure Summary

**Scheme extended from 2-field to 3-field (vars, constraints, ty) across all 92 pattern match sites; Constraint, ClassInfo, InstanceInfo, ClassEnv, InstanceEnv types added to Type.fs**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-31T10:22:33Z
- **Completed:** 2026-03-31T10:26:34Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Extended `Scheme` from `Scheme(vars, ty)` to `Scheme(vars, constraints, ty)` — the most invasive change in v10.0
- Updated all 92 Scheme pattern match sites across TypeCheck.fs (84), Bidir.fs (11), Infer.fs (9) atomically
- Added `Constraint`, `ClassInfo`, `InstanceInfo`, `ClassEnv`, `InstanceEnv` types to Type.fs
- Added `mkScheme`/`schemeType` helpers for backward-compat; updated `applyScheme`, `freeVarsScheme`, `formatSchemeNormalized`
- Build: 0 errors, 0 warnings; Tests: 224 passed, 0 failed

## Task Commits

Each task was committed atomically (Tasks 1+2 combined since T1 alone doesn't compile):

1. **Tasks 1+2: Extend Type.fs and update all Scheme sites** - `8ddba61` (feat)

**Plan metadata:** (in next commit)

## Files Created/Modified
- `src/LangThree/Type.fs` - Constraint, Scheme(3-field), mkScheme, schemeType, ClassInfo, InstanceInfo, ClassEnv, InstanceEnv; updated applyScheme/freeVarsScheme/formatSchemeNormalized
- `src/LangThree/TypeCheck.fs` - 84 Scheme occurrences updated (83 construction + 1 deconstruction)
- `src/LangThree/Bidir.fs` - 11 Scheme occurrences updated
- `src/LangThree/Infer.fs` - 9 Scheme occurrences updated (instantiate, generalize, inferPattern, etc.)

## Decisions Made
- Tasks 1 and 2 committed together because Task 1 alone (Type.fs change) would not compile — F# exhaustive pattern matching makes partial Scheme changes non-compilable
- Used Python regex for the bulk mechanical transformation (83 TypeCheck.fs construction sites) to avoid error-prone manual edits
- `InstanceInfo` defined without `MethodBodies` field (placeholder for Phase 71) to avoid circular dependency with `Expr` type which is defined in AST.fs after Type.fs

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Phase 70 Plan 02 can now proceed: thread ClassEnv/InstanceEnv into Bidir.fs as mutable refs
- All existing tests green, no regressions from the Scheme shape change
- `mkScheme` helper is available for Phase 71 parser/type-checker code that creates Scheme values

---
*Phase: 70-core-type-infrastructure*
*Completed: 2026-03-31*
