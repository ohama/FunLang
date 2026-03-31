---
phase: 67-cli-extensions
plan: 04
subsystem: testing
tags: [flt, integration-tests, cli, check, deps, prelude, caching, diamond-dependency]

# Dependency graph
requires:
  - phase: 67-cli-extensions
    provides: --check, --deps, --prelude CLI flags and file import caching
provides:
  - flt integration tests for --check mode (valid and type error cases)
  - flt integration tests for --deps mode (single file and import tree)
  - flt integration test for --prelude flag with explicit path
  - flt integration test for diamond dependency caching
affects: [68-funproj-toml]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "flt CLI test pattern: bash -c to create temp files, LT variable for binary path, \\x22 for double quotes"
    - "Combined output verification with CONTAINS directive for multi-step CLI tests"

key-files:
  created:
    - tests/flt/file/cli/cli-check.flt
    - tests/flt/file/cli/cli-deps.flt
    - tests/flt/file/cli/cli-prelude.flt
    - tests/flt/file/cli/cli-cache-diamond.flt
  modified: []

key-decisions:
  - "Use \\x22 hex escape for double quotes in flt printf commands (flt strips backslash from \\\")"
  - "Use CONTAINS directives for multi-step CLI tests combining valid and error cases in one command"
  - "Redirect --check stderr to stdout with 2>&1 in bash -c command; use Output section not Stderr section"

patterns-established:
  - "CLI flt test pattern: bash -c creates temp .fun files, runs LangThree, verifies output"
  - "Multi-scenario flt: single bash -c command runs two checks, CONTAINS matches both outcomes"

# Metrics
duration: 20min
completed: 2026-03-31
---

# Phase 67 Plan 04: CLI Integration Tests Summary

**flt integration tests for --check (valid/error), --deps (tree output), --prelude (explicit path), and diamond dependency caching covering all CLI-01 through CLI-05 features**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-03-31T00:00:00Z
- **Completed:** 2026-03-31T00:20:00Z
- **Tasks:** 1
- **Files modified:** 4 created

## Accomplishments
- Created cli-check.flt: combined bash -c test verifying --check reports "OK (0 warnings)" for valid code and "Type mismatch" for type errors
- Created cli-deps.flt: verifies --deps shows single filename for no-import file and indented tree for imported file
- Created cli-prelude.flt: verifies --prelude flag works with explicit Prelude directory path
- Created cli-cache-diamond.flt: verifies diamond dependency (d imported by b and c, both imported by a) produces correct result 87

## Task Commits

Each task was committed atomically:

1. **Task 1: Create flt integration tests for CLI features** - `1c0e44a` (test)

## Files Created/Modified
- `tests/flt/file/cli/cli-check.flt` - Tests --check on valid file (OK) and type error file (error message)
- `tests/flt/file/cli/cli-deps.flt` - Tests --deps showing single filename and 2-level indented dependency tree
- `tests/flt/file/cli/cli-prelude.flt` - Tests --prelude with explicit /path/to/Prelude directory
- `tests/flt/file/cli/cli-cache-diamond.flt` - Tests diamond dependency: d=42, b=d+1, c=d+2, a=b+c=87

## Decisions Made
- Used `\x22` hex escape for double quotes in flt printf commands: flt strips the backslash from `\"` when parsing the command line, turning `"hello"` into `\hello` which causes parse errors. Using `\x22` (printf hex escape for `"`) works correctly.
- Combined valid and error --check into a single flt file using a bash -c command that runs both and uses CONTAINS matching: simpler than two separate files and matches plan requirement for a single cli-check.flt.
- Redirected --check stderr to stdout (2>&1 in bash -c): flt's Stderr section does a "contains-match" against the actual stderr fd, but when using bash -c, the 2>&1 redirect sends stderr to stdout before flt captures it. Used Output section with CONTAINS instead.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

The flt command parser strips backslashes before double quotes in the command string. A command like `printf "let x : int = \"hello\"\n"` becomes `printf "let x : int = \hello\n"`, producing a LangThree parse error instead of a type error. Resolved by using `\x22` printf hex escape for double quotes in all flt CLI test commands.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All 656 flt tests pass (652 existing + 4 new CLI tests)
- Phase 67 (CLI Extensions) is now complete: all 4 plans done
- Phase 68 (funproj.toml support) can proceed

---
*Phase: 67-cli-extensions*
*Completed: 2026-03-31*
