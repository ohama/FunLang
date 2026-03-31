---
phase: 70-core-type-infrastructure
plan: "02"
subsystem: type-system
tags: [fsharp, typecheck, classenv, instanceenv, typeclasses, threading]

# Dependency graph
requires:
  - phase: 70-01
    provides: ClassEnv/InstanceEnv/ClassInfo/InstanceInfo types defined in Type.fs; Scheme extended to 3-field
provides:
  - typeCheckModuleWithPrelude accepts and returns ClassEnv/InstanceEnv parameters
  - PreludeResult record includes ClassEnv/InstEnv fields
  - All call sites in Program.fs and Prelude.fs pass ClassEnv/InstanceEnv (empty maps)
  - ClassEnv/InstanceEnv accumulate through prelude loading pipeline
affects:
  - phase 71 (typeclass/instance parsing): will populate ClassEnv/InstanceEnv from declarations
  - phase 72 (constraint solving): will read ClassEnv/InstanceEnv during type inference

# Tech tracking
tech-stack:
  added: []
  patterns:
    - ClassEnv/InstanceEnv threaded as explicit parameters matching ConstructorEnv/RecordEnv pattern
    - Empty maps passed for now; populated in Phase 71

key-files:
  created: []
  modified:
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Prelude.fs
    - src/LangThree/Program.fs
    - tests/LangThree.Tests/GadtTests.fs
    - tests/LangThree.Tests/ModuleTests.fs

key-decisions:
  - "Test helper call sites (GadtTests.fs, ModuleTests.fs) updated as part of Task 2 deviation fix"
  - "loadAndTypeCheckFileImpl passes Map.empty for ClassEnv/InstanceEnv since file imports do not yet declare typeclasses"

patterns-established:
  - "ClassEnv/InstanceEnv follow identical threading pattern to ConstructorEnv/RecordEnv throughout pipeline"

# Metrics
duration: 8min
completed: 2026-03-31
---

# Phase 70 Plan 02: ClassEnv/InstanceEnv Threading Summary

**ClassEnv and InstanceEnv wired through typeCheckModuleWithPrelude and all callers, passing empty maps as plumbing for Phase 71 typeclass declarations**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-31T10:28:49Z
- **Completed:** 2026-03-31T10:36:00Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Extended `typeCheckModuleWithPrelude` signature with `preludeClassEnv: ClassEnv` and `preludeInstEnv: InstanceEnv` parameters, plus expanded Ok result tuple
- Added `ClassEnv`/`InstEnv` fields to `PreludeResult` record with accumulation in `loadPrelude`
- Updated all 5 Program.fs call sites and 2 Prelude.fs call sites to pass and destructure new environment slots
- Build succeeds with zero warnings; 224 unit tests and 659 flt integration tests all pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Add ClassEnv/InstanceEnv to typeCheckModuleWithPrelude** - `0191b1f` (feat)
2. **Task 2: Update all callers in Program.fs and Prelude.fs** - `d274725` (feat)

## Files Created/Modified
- `src/LangThree/TypeCheck.fs` - Extended typeCheckModuleWithPrelude signature and return type; updated typeCheckModule wrapper
- `src/LangThree/Prelude.fs` - Added ClassEnv/InstEnv to PreludeResult; accumulation in loadPrelude; updated loadAndTypeCheckFileImpl
- `src/LangThree/Program.fs` - All 5 call sites updated with ClassEnv/InstEnv args and expanded result destructuring
- `tests/LangThree.Tests/GadtTests.fs` - Updated call site in parseAndEval helper
- `tests/LangThree.Tests/ModuleTests.fs` - Updated call site in evalWithPrelude helper

## Decisions Made
- `loadAndTypeCheckFileImpl` (file import handler) passes `Map.empty Map.empty` for ClassEnv/InstanceEnv because file imports do not declare typeclasses yet; the file cache stores only CtorEnv/RecEnv/Mods/TypeEnv as before
- Test helper functions updated to match the new signature (this was auto-fixed as a Rule 3 deviation — blocking the test build)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed test call sites in GadtTests.fs and ModuleTests.fs**
- **Found during:** Task 2 verification (dotnet test)
- **Issue:** Two test helpers called `typeCheckModuleWithPrelude` with the old arity; test build failed
- **Fix:** Updated both call sites to pass two extra `Map.empty` args and destructure two extra `_classEnv/_instEnv` slots in the Ok result
- **Files modified:** `tests/LangThree.Tests/GadtTests.fs`, `tests/LangThree.Tests/ModuleTests.fs`
- **Verification:** `dotnet test` — 224/224 pass
- **Committed in:** `d274725` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary to unblock test compilation. Plan didn't mention test files but they directly called the changed function.

## Issues Encountered
None beyond the test call site deviation above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ClassEnv/InstanceEnv plumbing complete; Phase 71 can populate them from typeclass/instance declarations by passing non-empty maps to `typeCheckModuleWithPrelude`
- PreludeResult already has ClassEnv/InstEnv fields that Phase 71 can fill during prelude loading
- Blocker: Audit `where` keyword in Lexer.fsl before writing Phase 71 parser rules (may conflict with GADT syntax)

---
*Phase: 70-core-type-infrastructure*
*Completed: 2026-03-31*
