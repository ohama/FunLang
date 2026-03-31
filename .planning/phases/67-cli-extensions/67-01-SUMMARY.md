---
phase: 67-cli-extensions
plan: 01
subsystem: cli
tags: [argu, fsharp, cli-flags, prelude, environment-variables]

# Dependency graph
requires: []
provides:
  - Check, Deps, Prelude DU cases in CliArgs with Usage strings
  - resolvePreludeDir with priority chain (--prelude > LANGTHREE_PRELUDE env > auto-discovery)
  - Program.fs extracts --prelude before loadPrelude call
affects: [67-cli-extensions]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Argu DU case for optional valued flag: | Prelude of path: string"
    - "loadPrelude(explicitPath: string option) -- standard option parameter, not F# optional"
    - "resolvePreludeDir as public function for testability and reuse"

key-files:
  created: []
  modified:
    - src/LangThree/Cli.fs
    - src/LangThree/Prelude.fs
    - src/LangThree/Program.fs
    - src/LangThree/Repl.fs
    - tests/LangThree.Tests/ModuleTests.fs

key-decisions:
  - "loadPrelude uses standard string option parameter (not F# ?optional syntax) per plan specification"
  - "resolvePreludeDir exposed as public function for future reuse by --check/--deps modes"
  - "Repl.fs and ModuleTests.fs updated to pass None at call sites (auto-fix deviation)"

patterns-established:
  - "Prelude resolution priority: --prelude CLI flag > LANGTHREE_PRELUDE env var > auto-discovery"
  - "Explicit path validation: Directory.Exists check with failwithf on missing path"

# Metrics
duration: 8min
completed: 2026-03-31
---

# Phase 67 Plan 01: CLI Flag Foundation Summary

**Argu Check/Deps/Prelude flags added and Prelude loading refactored to honor --prelude > LANGTHREE_PRELUDE env > auto-discovery priority chain**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-03-31
- **Completed:** 2026-03-31
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Cli.fs now has Check, Deps, and Prelude DU cases with proper Usage strings visible in --help
- Prelude.fs has public `resolvePreludeDir` implementing the three-tier priority chain
- Program.fs correctly extracts `--prelude` path before calling loadPrelude (ordering bug fixed)
- All 224 existing unit tests pass unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Argu flags and implement Prelude path resolution** - `7c45d23` (feat)
2. **Task 2: Fix Program.fs Prelude loading order** - `5e93013` (feat)

## Files Created/Modified
- `src/LangThree/Cli.fs` - Added Check, Deps, Prelude DU cases with Usage strings
- `src/LangThree/Prelude.fs` - Added resolvePreludeDir, updated loadPrelude signature to `(explicitPath: string option)`
- `src/LangThree/Program.fs` - Extract preludePath from Argu results before loadPrelude call
- `src/LangThree/Repl.fs` - Updated loadPrelude call site to pass None
- `tests/LangThree.Tests/ModuleTests.fs` - Updated loadPrelude call site to pass None

## Decisions Made
- Used standard `string option` parameter for `loadPrelude` (not F# `?explicitPath` optional syntax) per plan specification
- `resolvePreludeDir` is public so future --check and --deps modes can reuse it if needed

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated Repl.fs and ModuleTests.fs call sites for new loadPrelude signature**
- **Found during:** Task 2 (fix Program.fs Prelude loading order)
- **Issue:** `loadPrelude()` signature changed to require `string option` parameter; Repl.fs and ModuleTests.fs were not in the plan's files_modified list but would not compile
- **Fix:** Updated both files to pass `None` at their call sites
- **Files modified:** src/LangThree/Repl.fs, tests/LangThree.Tests/ModuleTests.fs
- **Verification:** `dotnet test` passes with all 224 tests
- **Committed in:** `5e93013` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary fix to keep all call sites compiling. No scope creep.

## Issues Encountered
None beyond the expected call-site updates from the loadPrelude signature change.

## Next Phase Readiness
- Foundation complete for --check and --deps implementation (Plans 02+)
- `resolvePreludeDir` available for use by future modes
- All existing CLI modes continue to work as before

---
*Phase: 67-cli-extensions*
*Completed: 2026-03-31*
