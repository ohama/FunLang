---
phase: 68-project-file
plan: 01
subsystem: build-system
tags: [toml, tomlyn, project-file, funproj, fsproj, nuget]

# Dependency graph
requires:
  - phase: 67-cli-extensions
    provides: Argu CLI infrastructure (Cli.fs, Prelude.fs, Program.fs) that ProjectFile.fs will plug into
provides:
  - Tomlyn 2.3.0 NuGet dependency for TOML parsing
  - ProjectFile.fs module with parseFunProj/findFunProj/loadFunProj functions
  - FunProjConfig and TargetConfig F# record types for funproj.toml consumption
affects: [68-02, 68-03, Program.fs build/test dispatch]

# Tech tracking
tech-stack:
  added: [Tomlyn 2.3.0]
  patterns:
    - "[<CLIMutable>] F# records as Tomlyn POCO deserialization targets"
    - "TOML -> POCO -> F# record two-stage parsing pipeline"
    - "Absolute path resolution at parse time (projDir-relative)"

key-files:
  created: [src/LangThree/ProjectFile.fs]
  modified: [src/LangThree/LangThree.fsproj]

key-decisions:
  - "Use [<CLIMutable>] F# records (not C# classes) as Tomlyn POCO types"
  - "Resolve all paths to absolute at parse time using Path.GetFullPath(Path.Combine(projDir, ...))"
  - "Place ProjectFile.fs between Repl.fs and Cli.fs in compile order"
  - "findFunProj looks only in CWD (no directory walk-up), matching Cargo behavior"

patterns-established:
  - "Pattern: TomlSerializer.Deserialize<TomlFunProj>(text) -> typed POCO -> FunProjConfig record"
  - "Pattern: null-check POCO fields using box projSection <> null for struct/record types"

# Metrics
duration: 1min
completed: 2026-03-31
---

# Phase 68 Plan 01: Project File Summary

**Tomlyn 2.3.0 TOML parser added and ProjectFile.fs module created with parseFunProj/findFunProj/loadFunProj functions that deserialize funproj.toml into typed FunProjConfig F# records with absolute paths**

## Performance

- **Duration:** 1 min
- **Started:** 2026-03-31T09:19:33Z
- **Completed:** 2026-03-31T09:20:33Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Added Tomlyn 2.3.0 as a NuGet package reference (TOML 1.1 parser, net10.0 native)
- Created ProjectFile.fs with three [<CLIMutable>] POCO types for Tomlyn deserialization
- Created FunProjConfig and TargetConfig F# records for idiomatic downstream consumption
- Implemented parseFunProj with null-safe handling of optional [project] section and prelude field
- Implemented findFunProj (CWD-only discovery) and loadFunProj (file read + error wrapping)
- Registered ProjectFile.fs in LangThree.fsproj before Cli.fs; project builds with 0 errors

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Tomlyn NuGet package** - `a482cf4` (chore)
2. **Task 2: Create ProjectFile.fs module** - `21efa11` (feat)

**Plan metadata:** (see below)

## Files Created/Modified
- `src/LangThree/ProjectFile.fs` - TOML POCO types, FunProjConfig record, parseFunProj/findFunProj/loadFunProj
- `src/LangThree/LangThree.fsproj` - Added Tomlyn 2.3.0 PackageReference and ProjectFile.fs compile entry

## Decisions Made
- Used [<CLIMutable>] F# records as POCO types rather than mutable C# classes -- idiomatic and sufficient for Tomlyn reflection
- Paths resolved to absolute at parse time so Program.fs never needs to re-resolve relative paths
- null-check via `box projSection <> null` since TomlFunProj.project is a struct-like type

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ProjectFile.fs exports are ready for Plan 68-02 (Argu subcommand extension in Cli.fs)
- FunProjConfig.PreludePath (string option with absolute path) ready for prelude priority chain in Program.fs
- No blockers

---
*Phase: 68-project-file*
*Completed: 2026-03-31*
