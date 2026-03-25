---
phase: 26-quick-fixes
plan: "02"
subsystem: interpreter
tags: [prelude, path-resolution, empty-files, robustness, dotnet]

# Dependency graph
requires:
  - phase: 25-prelude-library
    provides: Prelude/*.fun files that need to be found reliably
provides:
  - findPreludeDir 3-stage path search (CWD, assembly-relative, walk-up)
  - Empty/whitespace-only .fun file graceful handling
affects: [any phase involving interpreter invocation or Prelude loading]

# Tech tracking
tech-stack:
  added: []
  patterns: [assembly-location-relative path search, whitespace guard before parse]

key-files:
  created: []
  modified:
    - src/LangThree/Prelude.fs
    - src/LangThree/Program.fs

key-decisions:
  - "Use 3-stage search (CWD -> assembly-relative -> walk-up) to support both dev and installed binary scenarios"
  - "Treat whitespace-only input before parse, not after, since the parser errors on stray whitespace"
  - "Add List.isEmpty moduleDecls guard as defensive belt-and-suspenders after typecheck"

patterns-established:
  - "Path-search pattern: CWD first (dev), assembly-relative second (published), walk-up third (run from other dir)"
  - "Input guard pattern: IsNullOrWhiteSpace check before expensive parse for empty-file safety"

# Metrics
duration: 2min
completed: 2026-03-24
---

# Phase 26 Plan 02: Prelude Path Fix & Empty File Guard Summary

**Prelude loads via 3-stage directory search from any CWD; empty and whitespace-only .fun files exit 0 with `()` instead of crashing**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-24T07:50:34Z
- **Completed:** 2026-03-24T07:52:53Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- `findPreludeDir` helper added to Prelude.fs with CWD, assembly-relative, and 6-level walk-up search
- Empty files (0 bytes) and whitespace-only files now print `()` and exit 0
- All 199 existing tests continue to pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix Prelude path resolution (MOD-04)** - `148ebdd` (fix)
2. **Task 2: Handle empty .fun files gracefully (MOD-03)** - `59cbf0f` (fix)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Prelude.fs` - Added `findPreludeDir` private helper; updated `loadPrelude` to use it
- `src/LangThree/Program.fs` - Added `IsNullOrWhiteSpace` pre-parse guard and `List.isEmpty moduleDecls` post-typecheck guard

## Decisions Made
- Stage 3 walks up 6 levels from assembly dir: the binary lives at `src/LangThree/bin/Debug/net10.0/LangThree`, so 6 levels reaches repo root where `Prelude/` lives
- The `parent <> dir` guard prevents infinite loop at filesystem root
- Whitespace guard placed before `parseModuleFromString` call because the parser emits a parse error on whitespace-only input (not a graceful empty module)
- `List.isEmpty moduleDecls` guard added after typecheck as defensive layer for future empty-module variants

## Deviations from Plan

None - plan executed exactly as written. Empirical testing confirmed the exact crash scenario (whitespace at parser) described in the plan notes, and the pre-parse guard addressed it as specified.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Interpreter now robust for any CWD and empty inputs
- Prelude path fix enables `dotnet run` from subdirectories and scripts that invoke the binary from arbitrary locations
- No blockers for remaining Phase 26 plans

---
*Phase: 26-quick-fixes*
*Completed: 2026-03-24*
