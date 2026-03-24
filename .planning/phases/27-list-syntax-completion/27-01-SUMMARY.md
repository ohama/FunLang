---
phase: 27-list-syntax-completion
plan: 01
subsystem: parser
tags: [indentfilter, bracket-depth, newline-suppression, list-literals, fsharp, flt]

# Dependency graph
requires:
  - phase: 26-quick-fixes
    provides: IndentFilter.fs with full context tracking (FilterState, SyntaxContext)
provides:
  - BracketDepth field in FilterState tracks depth of [] () {} nesting
  - NEWLINE suppression inside brackets (no INDENT/DEDENT emitted)
  - Multi-line list literal parsing support (SYN-02)
  - flt regression tests for multi-line list syntax
affects: [27-02-list-syntax-completion, 28-string-interpolation, 30-expression-let-rec]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bracket depth counter in lexer filter: increment on open, decrement (floor 0) on close, suppress NEWLINE processing when depth > 0"
    - "Guarded match arm order: specific guard (BracketDepth > 0) before unguarded arm for same token type"

key-files:
  created:
    - tests/flt/expr/list/list-multiline.flt
    - tests/flt/file/list/list-multiline-file.flt
  modified:
    - src/LangThree/IndentFilter.fs
    - tests/LangThree.Tests/IndentFilterTests.fs

key-decisions:
  - "BracketDepth uses max 0 (depth - 1) on close to guard against underflow from malformed input"
  - "Guarded NEWLINE arm placed before unguarded arm in filter match expression (F# top-to-bottom matching)"

patterns-established:
  - "BracketDepth pattern: any future tokenizer changes must preserve bracket depth tracking"

# Metrics
duration: 4min
completed: 2026-03-24
---

# Phase 27 Plan 01: List Syntax Completion Summary

**BracketDepth counter in IndentFilter suppresses INDENT/DEDENT inside [], (), and {} brackets, enabling multi-line list literals to parse correctly**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-03-24T08:40:26Z
- **Completed:** 2026-03-24T08:44:23Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Added `BracketDepth: int` field to `FilterState` record and set to 0 in `initialState`
- Added explicit LBRACKET/LPAREN/LBRACE and RBRACKET/RPAREN/RBRACE match arms to increment/decrement depth
- Added guarded `NEWLINE _ when state.BracketDepth > 0` arm before unguarded arm to suppress INDENT/DEDENT inside brackets
- Updated all FilterState record literals in IndentFilterTests.fs with `BracketDepth = 0`
- Created two flt regression tests confirming multi-line list literals parse and evaluate correctly

## Task Commits

Each task was committed atomically:

1. **Task 1: Add BracketDepth to FilterState and suppress NEWLINE inside brackets** - `ff0844e` (feat)
2. **Task 2: Fix IndentFilterTests.fs and add multi-line list flt tests** - `b1d19ed` (test)

**Plan metadata:** (see final commit below)

## Files Created/Modified
- `src/LangThree/IndentFilter.fs` - Added BracketDepth field, bracket open/close arms, guarded NEWLINE arm
- `tests/LangThree.Tests/IndentFilterTests.fs` - Added BracketDepth = 0 to all 5 FilterState record literals
- `tests/flt/expr/list/list-multiline.flt` - flt test: multi-line list expression `[1;2;3]` spanning 4 lines
- `tests/flt/file/list/list-multiline-file.flt` - flt test: multi-line list in file mode with dependent let binding

## Decisions Made
- BracketDepth decrement uses `max 0 (depth - 1)` to guard against underflow from malformed/incomplete input
- Guarded NEWLINE arm placed before unguarded arm — F# match arms are top-to-bottom, and the guard must fire first

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None - build succeeded first try, all 199 tests pass.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Multi-line list literals now parse correctly
- BracketDepth infrastructure is in place for any future parenthesized/bracketed expression improvements
- Ready for Phase 27-02 (remaining list syntax completion work)

---
*Phase: 27-list-syntax-completion*
*Completed: 2026-03-24*
