---
phase: 01-indentation-based-syntax
plan: 03
subsystem: compiler-lexer
tags: [indentation, error-messages, validation, fsharp]

# Dependency graph
requires:
  - phase: 01-01
    provides: IndentFilter with processNewline and SyntaxContext
provides:
  - Enhanced error messages showing line, column, and expected indent levels
  - Configurable indent width validation (strict/lenient modes)
  - Helper functions formatExpectedIndents and validateIndentWidth
affects: [error-reporting, linting-tools, ide-integration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Error message formatting pattern: include line, column, and context-specific hints
    - Config-driven validation pattern: strict vs lenient modes for different use cases

key-files:
  created: []
  modified:
    - src/LangThree/IndentFilter.fs
    - tests/LangThree.Tests/IndentFilterTests.fs

key-decisions:
  - "formatExpectedIndents shows all valid indent levels from stack plus 'or a new indent level' for clarity"
  - "validateIndentWidth checks multiples only when StrictWidth=true, allowing flexible development mode"
  - "EOF handling yields all DEDENTs returned by processNewline (not one per loop iteration)"

patterns-established:
  - "IndentationError messages follow format: context description at line X, column Y with expected values"
  - "Match-specific errors mention 'Match pipe' and alignment requirements with 'match' keyword"

# Metrics
duration: 3min
completed: 2026-03-02
---

# Phase 01 Plan 03: Improved Error Messages Summary

**Enhanced indentation error messages with line/column/expected-levels context and configurable indent width validation**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-02T08:55:59Z
- **Completed:** 2026-03-02T08:59:18Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Error messages now include line number, actual column, and list of expected indent levels
- Match pipe errors clearly state alignment requirement with 'match' keyword column
- Strict indent width mode validates multiples of configured width (useful for enforcing style guides)
- Lenient mode allows any indent level (useful for development and mixed codebases)
- Fixed EOF handling to emit all DEDENTs at once

## Task Commits

Each task was committed atomically:

1. **Task 1: Add indent width validation and improve error message formatting** - `40b4db8` (feat)
2. **Task 2: Add tests for error messages and indent width validation** - `5cffefb` (test)

## Files Created/Modified
- `src/LangThree/IndentFilter.fs` - Added validateIndentWidth and formatExpectedIndents functions, updated error messages throughout, fixed EOF DEDENT emission
- `tests/LangThree.Tests/IndentFilterTests.fs` - Added 5 new test cases for error messages and indent width validation, updated existing tests for new signature

## Decisions Made

**formatExpectedIndents output format:**
- For empty stack: "0" (edge case)
- For single level: just the number
- For multiple levels: "one of [0, 4] or a new indent level" (reversed order for readability)
- Rationale: Clear and actionable for users to understand valid indentation options

**validateIndentWidth placement:**
- Called at start of processNewline before any stack operations
- Only validates when StrictWidth=true and col > 0
- Rationale: Catches violations early before attempting stack operations, column 0 always valid

**EOF DEDENT fix:**
- Changed from `yield Parser.DEDENT` to `yield! tokens` in while loop
- Rationale: processNewline can return multiple DEDENTs in one call when unwinding multiple levels, must emit all of them

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed EOF DEDENT emission**
- **Found during:** Task 2 (test writing)
- **Issue:** testStrictIndentWidthMultiple failed - only emitting 1 DEDENT instead of 2 at EOF. While loop was calling processNewline which returned multiple DEDENTs, but code was ignoring the returned tokens and only yielding one Parser.DEDENT per iteration
- **Fix:** Changed `yield Parser.DEDENT` to `yield! tokens` to emit all DEDENTs returned by processNewline
- **Files modified:** src/LangThree/IndentFilter.fs
- **Verification:** testStrictIndentWidthMultiple now passes, all 34 tests pass
- **Committed in:** 5cffefb (Task 2 commit)

**2. [Rule 3 - Blocking] Updated existing tests for new signature**
- **Found during:** Task 2 (test compilation)
- **Issue:** processNewline signature changed to include config parameter, existing tests calling old signature failed to compile
- **Fix:** Added defaultConfig parameter to all processNewline test calls
- **Files modified:** tests/LangThree.Tests/IndentFilterTests.fs
- **Verification:** All tests compile and pass
- **Committed in:** 5cffefb (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking)
**Impact on plan:** Both auto-fixes essential for correctness. The EOF bug was latent (not caught by existing tests) and discovered when writing more comprehensive tests. Signature updates were necessary consequences of adding the config parameter.

## Issues Encountered

None - implementation proceeded smoothly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Error messages are now clear and actionable for users
- Indent width validation ready for integration into language tooling
- All tests passing (34 total)
- No blockers for subsequent phases

Future enhancements could include:
- Suggesting auto-fix commands for indentation errors
- IDE integration to show inline error hints
- Configuration file support for project-wide indent settings

---
*Phase: 01-indentation-based-syntax*
*Completed: 2026-03-02*
