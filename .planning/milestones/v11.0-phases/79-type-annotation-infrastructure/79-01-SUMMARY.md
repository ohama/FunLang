---
phase: 79-type-annotation-infrastructure
plan: 01
subsystem: typechecker
tags: [fsharp, type-inference, span, annotation-map, bidir, typecheck]

# Dependency graph
requires:
  - phase: 78-poison-type
    provides: accumulatedErrors mutable pattern in Bidir.fs that this plan follows
provides:
  - TypeAnnotationMap module with Dictionary<Span, Type> helpers (create/record/tryFind/toSeq)
  - Bidir.annotationMap mutable ref for per-expression type storage
  - Reset lifecycle wired into all TypeCheck entry points
affects:
  - 79-02 (populate annotationMap in Bidir.synth)
  - 80-typed-ast-export (read annotationMap to attach types to AST nodes)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Mutable module-level Dictionary<Span, Type> reset at entry points (same lifecycle as mutableVars/pendingConstraints)"
    - "unknownSpan guard in record helper to skip synthetic elaboration nodes"

key-files:
  created:
    - src/FunLang/TypeAnnotationMap.fs
  modified:
    - src/FunLang/FunLang.fsproj
    - src/FunLang/Bidir.fs
    - src/FunLang/TypeCheck.fs

key-decisions:
  - "TypeAnnotationMap placed at position 2.6 (between Elaborate.fs and Diagnostic.fs) so it depends only on Ast/Type"
  - "annotationMap declared in Bidir.fs (not TypeCheck.fs) so synth can populate it without circular dependency"
  - "record helper skips unknownSpan to avoid polluting map with synthetic elaboration nodes"

patterns-established:
  - "Mutable lifecycle: declare in Bidir.fs, reset at every TypeCheck entry point, populate during synth"

# Metrics
duration: 3min
completed: 2026-04-03
---

# Phase 79 Plan 01: Type Annotation Infrastructure Summary

**Dictionary<Span, Type> annotation map module with Bidir mutable ref and reset lifecycle wired into all TypeCheck entry points**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-04-03T02:40:28Z
- **Completed:** 2026-04-03T02:42:33Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Created TypeAnnotationMap.fs module with create/record/tryFind/toSeq helpers; record skips unknownSpan to avoid synthetic nodes
- Declared `Bidir.annotationMap` mutable following the exact same pattern as mutableVars/pendingConstraints/accumulatedErrors
- Wired resets into both TypeCheck entry points (typecheckExprWithPrelude and typeCheckModuleWithPrelude)
- All 223 existing tests pass unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Create TypeAnnotationMap.fs and update fsproj** - `a533da0` (feat)
2. **Task 2: Declare annotationMap mutable in Bidir and reset at TypeCheck entry points** - `31f735a` (feat)

## Files Created/Modified

- `src/FunLang/TypeAnnotationMap.fs` - New module: Dictionary<Span, Type> lifecycle helpers
- `src/FunLang/FunLang.fsproj` - Added TypeAnnotationMap.fs at position 2.6; updated BUILD ORDER comment
- `src/FunLang/Bidir.fs` - Added mutable annotationMap declaration after accumulatedErrors
- `src/FunLang/TypeCheck.fs` - Added annotationMap resets at both entry points

## Decisions Made

- TypeAnnotationMap placed at position 2.6 (after Elaborate.fs, before Diagnostic.fs) so it only depends on Ast and Type — no forward reference issues.
- Declared in Bidir.fs rather than TypeCheck.fs so that synth (in Bidir) can call TypeAnnotationMap.record without a circular module dependency.
- The `record` helper guards against unknownSpan to prevent synthetic elaboration nodes from polluting the map.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Infrastructure complete; Plan 79-02 can now populate Bidir.annotationMap via TypeAnnotationMap.record calls inside synth
- Key call site: after substitution is applied (`let ty' = apply s ty`), call `TypeAnnotationMap.record Bidir.annotationMap span ty'`

---
*Phase: 79-type-annotation-infrastructure*
*Completed: 2026-04-03*
