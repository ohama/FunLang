---
phase: 68-project-file
plan: 02
subsystem: cli
tags: [argu, subcommands, type-check, eval, funproj, prelude-priority]

# Dependency graph
requires:
  - phase: 68-project-file/68-01
    provides: ProjectFile.fs with FunProjConfig, findFunProj, loadFunProj
provides:
  - langthree build subcommand (type-checks [[executable]] targets)
  - langthree test subcommand (evaluates [[test]] targets)
  - Named target filtering (build <name>, test <name>)
  - Prelude priority chain extended with funproj.toml [project].prelude
affects: [68-03, integration-tests, CLI-documentation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Argu subcommands via [<CliPrefix(CliPrefix.None)>] on parent DU cases + sub-DU types"
    - "Build/Test dispatch before eager prelude loading (prelude loaded per-branch)"
    - "Prelude priority: --prelude > LANGTHREE_PRELUDE env > funproj.toml > auto-discovery"

key-files:
  created: []
  modified:
    - src/LangThree/Cli.fs
    - src/LangThree/Prelude.fs
    - src/LangThree/Program.fs
    - src/LangThree/Repl.fs
    - tests/LangThree.Tests/ModuleTests.fs

key-decisions:
  - "Build/test dispatch placed BEFORE eager prelude loading so project prelude (config.PreludePath) can be passed"
  - "loadPrelude signature extended with projPrelude: string option parameter (None for legacy paths)"
  - "Build reports OK/Error per target to stderr; exit 1 if any target fails"
  - "Test targets use Eval.scriptArgs <- [] (no argv passthrough)"

patterns-established:
  - "Pattern: project subcommands use ProjectFile.findFunProj() + ProjectFile.loadFunProj to load config"
  - "Pattern: each target processed independently, exitCode accumulated, final return is overall code"

# Metrics
duration: 3min
completed: 2026-03-31
---

# Phase 68 Plan 02: CLI Build/Test Subcommands Summary

**`langthree build` and `langthree test` subcommands wired to type-check and eval pipelines via funproj.toml, with funproj.toml prelude priority in resolution chain**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-03-31T09:22:47Z
- **Completed:** 2026-03-31T09:25:50Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- Added `BuildArgs`/`TestArgs` sub-DU types to Cli.fs with proper Argu subcommand attributes
- Extended `resolvePreludeDir`/`loadPrelude` in Prelude.fs to accept `projPrelude: string option` as third priority in chain
- Wired `build` dispatch in Program.fs: finds funproj.toml, type-checks all or named `[[executable]]` targets
- Wired `test` dispatch in Program.fs: finds funproj.toml, evaluates all or named `[[test]]` targets
- Updated all `loadPrelude` call sites (Repl.fs, ModuleTests.fs) to pass `None` as second arg

## Task Commits

1. **Task 1: Add BuildArgs/TestArgs to Cli.fs and extend resolvePreludeDir in Prelude.fs** - `299da1f` (feat)
2. **Task 2: Wire build/test dispatch in Program.fs** - `ecbe0f0` (feat)

## Files Created/Modified

- `src/LangThree/Cli.fs` - Added `BuildArgs`, `TestArgs` sub-DU types and `Build`/`Test` cases to `CliArgs`
- `src/LangThree/Prelude.fs` - Extended `resolvePreludeDir` and `loadPrelude` with `projPrelude` parameter
- `src/LangThree/Program.fs` - Added build/test dispatch branches before prelude loading; fixed loadPrelude call site
- `src/LangThree/Repl.fs` - Updated `loadPrelude None` -> `loadPrelude None None`
- `tests/LangThree.Tests/ModuleTests.fs` - Updated `loadPrelude(None)` -> `loadPrelude None None`

## Decisions Made

- Build/test dispatch placed BEFORE eager prelude loading so `config.PreludePath` (from funproj.toml) can be passed to `loadPrelude`
- `loadPrelude` signature extended with `projPrelude: string option` - all legacy call sites use `None`
- `langthree build` uses type-check pipeline only (no eval); reports `OK: <name> (N warnings)` per target
- `langthree test` uses full eval pipeline; reports `OK: <name>` per target
- `Eval.scriptArgs <- []` for test targets (no user argv passthrough into tests)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `langthree build` and `langthree test` are functional end-to-end
- Prelude priority chain includes funproj.toml as lowest-priority override
- Ready for 68-03: integration tests / flt tests for build and test subcommands

---
*Phase: 68-project-file*
*Completed: 2026-03-31*
