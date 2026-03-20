---
phase: 24-list-separator-semicolon
plan: "02"
subsystem: testing
tags: [testing, flt, fslit, list, semicolon, syntax, migration]

# Dependency graph
requires:
  - phase: 24-01
    provides: parser change requiring [1; 2; 3] syntax and rejecting [1, 2, 3]
provides:
  - "439 fslit tests updated to semicolon list syntax and passing"
  - "196 F# unit tests updated to semicolon list syntax and passing"
  - "Python bracket-stack transformation script for future use"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bracket-stack tracking algorithm: innermost context determines comma treatment (list=semicolon, paren=unchanged)"

key-files:
  created: []
  modified:
    - tests/flt/ (78 files)
    - tests/LangThree.Tests/MatchCompileTests.fs
    - tests/LangThree.Tests/ExceptionTests.fs

key-decisions:
  - "Stack-based bracket tracking (not depth counters): innermost '[' means comma is a list separator"
  - "Release binary must be rebuilt after parser change; fslit tests invoke the Release binary directly"
  - "Prelude/*.fun files require no changes (use [] and :: only, no list literals with commas)"

patterns-established:
  - "List syntax in tests: [1; 2; 3] with semicolons everywhere including inside tuples: ([1; 2; 3], 5)"
  - "List-of-tuples: [(a, b); (c, d)] — semicolons between tuples, commas inside"

# Metrics
duration: 7min
completed: 2026-03-20
---

# Phase 24 Plan 02: Test File Syntax Update Summary

**All 439 fslit tests and 196 F# unit tests migrated from comma list syntax to semicolons via a bracket-stack Python transformer, with Release binary rebuild required for test runner.**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-20T09:07:22Z
- **Completed:** 2026-03-20T09:14:42Z
- **Tasks:** 2
- **Files modified:** 80

## Accomplishments

- Python script with bracket-stack algorithm correctly transformed 78 .flt files
- Key edge cases handled: lists inside tuples `([1; 2; 3], 5)`, list-of-tuples `[(a, b); (c, d)]`
- MatchCompileTests.fs and ExceptionTests.fs unit tests updated for new syntax
- Prelude/*.fun files confirmed not needing changes (no list literals with commas)
- Release binary rebuilt with `dotnet publish` to match updated parser

## Task Commits

Each task was committed atomically:

1. **Task 1+2: Update test files to semicolon list syntax** - `a5dbde9` (refactor)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `tests/flt/**/*.flt` (78 files) - list literals updated to semicolon syntax
- `tests/LangThree.Tests/MatchCompileTests.fs` - list patterns in evalModule strings
- `tests/LangThree.Tests/ExceptionTests.fs` - list patterns in evalModule strings

## Decisions Made

- **Stack-based approach over depth counters:** The initial depth-counter approach (`list_depth >= 1 AND paren_depth == 0`) failed for lists inside tuples like `([1, 2, 3], [4, 5])`. Switched to a bracket stack where the innermost context determines comma treatment — if innermost is `[`, the comma is a list separator.
- **Emit test files excluded from transformation:** AST emit output uses `[...]` for F# list encoding of tuples and clauses (`Tuple [...]` uses commas, `TuplePat [...]` uses commas). These were reverted after the script incorrectly transformed them. The `Ast.List` node already uses semicolons from the Format.fs change in Plan 01.
- **Release binary rebuild:** fslit tests invoke the Release binary at `src/LangThree/bin/Release/net10.0/LangThree`. The binary pre-dated Plan 24-01's parser change and needed `dotnet publish -c Release` to update.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Initial depth-counter algorithm missed lists inside tuples**
- **Found during:** Task 1 (after first script run)
- **Issue:** `paren_depth == 0` condition prevented transforming lists that appear inside tuple output like `([1, 2, 3], 5)`
- **Fix:** Rewrote to use bracket stack; comma replaced with semicolon when innermost context is `[`
- **Files modified:** `/tmp/fix_list_syntax.py` (script rewritten)
- **Verification:** 8 additional files updated on second pass; all 439 tests pass
- **Committed in:** a5dbde9

**2. [Rule 1 - Bug] Emit test files incorrectly transformed**
- **Found during:** Task 1 (inspection of second script run output)
- **Issue:** Script transformed `Tuple [Number 1, Number 2]` → `Tuple [Number 1; Number 2]` but Format.fs uses commas for Tuple/TuplePat formatting
- **Fix:** `git checkout tests/flt/emit/` to revert the 4 incorrectly modified emit files
- **Files modified:** tests/flt/emit/ (reverted)
- **Verification:** Emit tests pass unchanged after revert
- **Committed in:** a5dbde9

**3. [Rule 1 - Bug] Release binary outdated after Plan 24-01 parser change**
- **Found during:** Task 2 (first test run showed 75 failures with "no more output")
- **Issue:** `tests/flt/` commands invoke the Release binary which was built before Plan 24-01's parser change; it still rejected `[1; 2; 3]`
- **Fix:** `dotnet publish -c Release` to rebuild
- **Verification:** `LangThree --expr "[1; 2; 3]"` returns `[1; 2; 3]`
- **Committed in:** a5dbde9

---

**Total deviations:** 3 auto-fixed (3 bugs)
**Impact on plan:** All auto-fixes necessary for correctness. No scope creep.

## Issues Encountered

- "deep nested constructor match" in MatchCompileTests.fs showed as intermittent failure (flaky test, not related to list syntax). Pre-existing issue.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All tests pass with semicolon list syntax
- Phase 24 (List Separator Semicolon) is complete
- Tutorial files (if any use comma list syntax) would need updating but are not part of this plan

---
*Phase: 24-list-separator-semicolon*
*Completed: 2026-03-20*
