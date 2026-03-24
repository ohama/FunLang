---
phase: 30-parser-improvements
plan: 01
subsystem: parser
tags: [indent-filter, token-stream, else, SYN-05, f#]

# Dependency graph
requires:
  - phase: 27-list-syntax-completion
    provides: IndentFilter BracketDepth and InMatch/InTry context patterns used here
provides:
  - IndentFilter.processNewlineWithContext suppresses INDENT before ELSE token
  - Tests verifying ELSE indentation suppression behavior
affects:
  - 30-02 and later plans that rely on correct if-then-else parsing in expression positions

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "nextToken lookahead in processNewlineWithContext to conditionally suppress INDENT (not DEDENT)"

key-files:
  created: []
  modified:
    - src/LangThree/IndentFilter.fs
    - tests/LangThree.Tests/IndentFilterTests.fs

key-decisions:
  - "SYN-05: Use nextToken lookahead (already available as parameter) rather than adding a new JustSawElse state flag"
  - "Only the emitted INDENT token is suppressed; indent stack is updated normally via processNewline"
  - "DEDENT tokens are never suppressed - they are needed to close the THEN branch block"

patterns-established:
  - "Pattern: nextToken=Some Parser.X in processNewlineWithContext | _ -> branch to conditionally filter INDENT from emitted tokens"

# Metrics
duration: 2min
completed: 2026-03-25
---

# Phase 30 Plan 01: ELSE Indentation Suppression Summary

**IndentFilter now suppresses spurious INDENT before ELSE by filtering it from processNewlineWithContext | _ -> branch using the existing nextToken lookahead parameter**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-24T20:36:32Z
- **Completed:** 2026-03-24T20:37:55Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Fixed SYN-05: `else` at deeper indentation than `if` no longer causes spurious INDENT token before ELSE
- DEDENT tokens before ELSE are preserved (required to close THEN branch blocks)
- Added 3 targeted IndentFilter tests covering the else-indentation scenarios
- Full test suite 202/202 passing; integration smoke test confirms `else match ...` evaluates correctly

## Task Commits

Each task was committed atomically:

1. **Task 1: Suppress INDENT before ELSE in processNewlineWithContext** - `c1eee5a` (fix)
2. **Task 2: Add IndentFilter tests for ELSE indentation** - `a9ff96c` (test)

**Plan metadata:** (docs commit to follow)

## Files Created/Modified
- `src/LangThree/IndentFilter.fs` - Added ELSE filtering in `| _ ->` branch of `processNewlineWithContext`
- `tests/LangThree.Tests/IndentFilterTests.fs` - Added `elseIndentationTests` test list with 3 new tests

## Decisions Made
- Used `nextToken` lookahead (already a parameter of `processNewlineWithContext`) instead of adding a new `JustSawElse` state flag — cleaner and avoids state management complexity (confirmed recommendation from research)
- Indent stack is updated normally by `processNewline`; only the emitted INDENT token is discarded — this avoids stack desynchronization on subsequent DEDENT events

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- SYN-05 fix complete; `else` followed by indented expression keywords (match, if, let, fun, try) now parses correctly
- Ready for Plan 30-02 (grammar additions: SYN-01 multi-param let rec, SYN-06/07/08)

---
*Phase: 30-parser-improvements*
*Completed: 2026-03-25*
