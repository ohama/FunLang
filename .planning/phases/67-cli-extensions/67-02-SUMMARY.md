---
phase: 67-cli-extensions
plan: 02
subsystem: cli
tags: [fsharp, cli, type-checking, dependency-analysis, argparse]

# Dependency graph
requires:
  - phase: 67-01
    provides: Check/Deps/Prelude CLI flags in Cli.fs, resolvePreludeDir helper
provides:
  - "--check mode: type-checks a file without executing, reports OK/error to stderr"
  - "--deps mode: prints recursive import dependency tree with indentation and circular detection"
affects:
  - 67-03
  - 67-04

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "All --check output via eprintfn to keep stdout clean for piping"
    - "collectDeps: parse-only recursive AST walk using FileImportDecl nodes"
    - "Circular imports detected via visited Set<string> tracking absolute paths"

key-files:
  created: []
  modified:
    - src/LangThree/Program.fs

key-decisions:
  - "--check uses typeCheckModuleWithPrelude without evalModuleDecls (no side effects)"
  - "--deps parses only (no type-check) to avoid import resolution errors in dep scanning"
  - "Print filenames only (not full paths) in --deps output for readability"
  - "collectDeps placed as module-level private function before main"

patterns-established:
  - "New mode branches placed before file-only branch in if/elif chain"
  - "Missing-file guard returns usage hint to stderr and exit 1"

# Metrics
duration: 3min
completed: 2026-03-31
---

# Phase 67 Plan 02: --check and --deps Mode Implementation Summary

**--check type-checks a .fun file without executing (stderr-only output), --deps prints recursive import tree with circular detection**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-31T08:46:41Z
- **Completed:** 2026-03-31T08:49:27Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- --check mode: calls typeCheckModuleWithPrelude without evalModuleDecls, reports OK/warnings to stderr, exits 0 on success or 1 on type error
- --deps mode: collectDeps function walks AST FileImportDecl nodes recursively, prints indented filename tree with (circular) markers
- Both modes handle missing file, no-argument, and error cases with appropriate usage hints

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement --check mode** - `f5d6cd3` (feat)
2. **Task 2: Implement --deps mode** - `11eef20` (feat)

**Plan metadata:** (docs commit to follow)

## Files Created/Modified
- `src/LangThree/Program.fs` - Added collectDeps helper + --check and --deps elif branches

## Decisions Made
- --check uses typeCheckModuleWithPrelude without evalModuleDecls to ensure no side effects during checking
- --deps uses parse-only (no type-check) to avoid cascading errors when scanning import graphs
- Print filenames only (not absolute paths) in --deps for readability; circular detection uses absolute paths internally
- collectDeps placed as module-level private function before `main`

## Deviations from Plan
None - plan executed exactly as written.

## Issues Encountered
- flt test files contain metadata comments and are not directly usable as .fun files for --check testing; used /tmp test files instead (expected, no issue)

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- --check and --deps fully implemented and verified (224 unit tests pass)
- Ready for Phase 67 Plan 03 (--prelude and environment variable support)

---
*Phase: 67-cli-extensions*
*Completed: 2026-03-31*
