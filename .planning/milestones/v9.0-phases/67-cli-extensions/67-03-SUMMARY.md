---
phase: 67-cli-extensions
plan: 03
subsystem: interpreter
tags: [fsharp, caching, dictionary, import, diamond-dependency, prelude]

# Dependency graph
requires:
  - phase: 67-01
    provides: CLI flag foundation (--prelude, --check, --deps flags)
provides:
  - Dictionary-based TC and eval caches in Prelude.fs
  - tcCache: type-check results keyed by absolute file path
  - evalCache: eval results keyed by absolute file path
  - Diamond dependency pattern (A imports B, C; both import D) now processes D only once
affects: [67-04, any future import/caching work]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cache-after-cycle-detection: TC cache check placed AFTER fileLoadingStack cycle check"
    - "Own-exports-only caching: store file's own exports, re-merge with each caller's env on hit"
    - "Process-scoped caches: module-level Dictionaries live for single CLI invocation lifetime"

key-files:
  created: []
  modified:
    - src/LangThree/Prelude.fs

key-decisions:
  - "Cache file's own exports only, not merged result -- avoids diamond dep env leakage"
  - "TC cache check AFTER fileLoadingStack ensures cycle detection always takes priority"
  - "Eval cache check at top is safe -- TC phase has already caught any cycles"
  - "No cache clearing needed -- caches are process-scoped (single CLI invocation)"

patterns-established:
  - "Cache key = absolute resolved file path (consistent with fileLoadingStack pattern)"

# Metrics
duration: 2min
completed: 2026-03-31
---

# Phase 67 Plan 03: File Import Caching Summary

**Dictionary-based TC and eval caches in Prelude.fs eliminate redundant parse/typecheck/eval for diamond dependency patterns**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-31T08:47:18Z
- **Completed:** 2026-03-31T08:49:19Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- Added `tcCache` Dictionary that prevents re-parsing and re-type-checking the same file
- Added `evalCache` Dictionary that prevents re-evaluating the same file
- Diamond dependency test (A imports B,C; both import D) correctly outputs 87 with D processed once
- All 224 F# unit tests and 652 flt integration tests pass unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Add TC and eval caches to Prelude.fs** - `9075303` (feat)

## Files Created/Modified

- `src/LangThree/Prelude.fs` - Added tcCache/evalCache Dictionaries and cache hit/miss logic in loadAndTypeCheckFileImpl and loadAndEvalFileImpl

## Decisions Made

- Caches store file's own exports only (not merged result). In diamond patterns, B and C may call D with different caller environments -- caching merged results would leak B's env into C's D import. Storing D's own exports and re-merging on each hit avoids this.
- TC cache check placed AFTER fileLoadingStack cycle detection. This is critical: a file currently on the loading stack must raise CircularModuleDependency, not return a (nonexistent) cached result.
- Eval cache check is safe at the top since TC phase has already caught any circular imports before eval runs.
- No cache invalidation/clearing -- caches are module-level (process-scoped), consistent with single CLI invocation semantics.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- File import caching is transparent to callers -- no API changes required
- Plan 67-04 (funproj.toml support) can proceed without changes related to caching
- All existing test infrastructure validated against cached imports

---
*Phase: 67-cli-extensions*
*Completed: 2026-03-31*
