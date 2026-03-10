---
phase: 01-indentation-based-syntax
plan: 02
subsystem: parser
tags: [indentation, function-application, fsyacc, indent-filter]

# Dependency graph
requires:
  - phase: 01-indentation-based-syntax
    plan: 01
    provides: IndentFilter with Match expression context support
provides:
  - Multi-line function application with INDENT/DEDENT tokens
  - InFunctionApp context in IndentFilter for argument grouping
  - AppArgs grammar for parsing indented argument lists
  - Integration tests covering basic, complex, curried, and mixed function applications
affects: [phases using function application syntax, future parser grammar extensions]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Function application detection: canBeFunction + isAtom check with lookahead"
    - "Context-aware indentation: InFunctionApp prevents nested INDENT emissions"

key-files:
  created: []
  modified:
    - src/LangThree/IndentFilter.fs
    - src/LangThree/Parser.fsy
    - tests/LangThree.Tests/IntegrationTests.fs

key-decisions:
  - "Use canBeFunction (IDENT | RPAREN) to identify function positions, preventing number-to-number app detection"
  - "Prevent re-entering InFunctionApp when already in context to avoid nested INDENT tokens"
  - "Emit no tokens for newlines within function app context (consume newlines between arguments)"

patterns-established:
  - "Multi-line function calls: function_name NEWLINE INDENT args... DEDENT"
  - "Token lookahead in IndentFilter: check nextToken to determine context entry"

# Metrics
duration: 13min
completed: 2026-03-02
---

# Phase 01 Plan 02: Multi-line Function Application Summary

**IndentFilter emits INDENT/DEDENT for multi-line function arguments with canBeFunction detection preventing spurious context entry**

## Performance

- **Duration:** 13 min
- **Started:** 2026-03-02T08:40:12Z
- **Completed:** 2026-03-02T08:53:10Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- InFunctionApp context in IndentFilter detects function-atom-newline-atom pattern at deeper indent
- AppArgs grammar rule enables parser to fold multi-line arguments into App nodes
- canBeFunction distinguishes function positions from argument positions
- All 4 integration tests pass (basic, complex args, curried, mixed)

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend SyntaxContext for function application and update Parser grammar** - `7c9eb03` (feat)
2. **Task 2: Add integration tests for multi-line function application** - `c728687` (test)

## Files Created/Modified
- `src/LangThree/IndentFilter.fs` - Added InFunctionApp context, canBeFunction/isAtom predicates, lookahead-based context detection
- `src/LangThree/Parser.fsy` - Added AppArgs non-terminal for multi-line argument lists, AppExpr INDENT AppArgs DEDENT rule
- `tests/LangThree.Tests/IntegrationTests.fs` - Added 4 integration tests covering basic, complex, curried, and mixed function application patterns

## Decisions Made

**canBeFunction refinement:** Initially used isAtom for both function and argument positions, causing false positives when NUMBER followed NUMBER across newlines. Refined to canBeFunction (IDENT | RPAREN only) for prevToken check, preventing `10 NEWLINE 20` from being detected as function application.

**Context re-entry prevention:** Added explicit check to prevent entering InFunctionApp when already in that context. Without this, `f NEWLINE (1+2) NEWLINE (3+4)` would trigger function app detection between RPAREN and LPAREN, emitting nested INDENT tokens.

**Newline consumption within function app:** When in InFunctionApp context and seeing a newline at the same or greater indent than baseColumn, emit no tokens. This allows multiple arguments on separate lines without additional INDENT/DEDENT pairs.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

**Test failures with plain number arguments:** Initial tests used `add\n    10\n    20` which failed because NUMBER tokens weren't being detected as function application triggers. Root cause was canBeFunction only including IDENT, excluding NUMBER positions. Resolved by ensuring isAtom check for nextToken allows any atom (including NUMBER) as argument, while canBeFunction restricts prevToken to function-capable positions.

**Complex argument test failed initially:** Test with `(1+2)\n(3+4)` failed because RPAREN was in canBeFunction, causing RPAREN NEWLINE LPAREN to trigger function app detection. Resolved by adding context re-entry check - don't enter InFunctionApp if already in that context.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Multi-line function application works end-to-end (lex → filter → parse)
- Parser handles both single-line `f 1 2` and multi-line `f\n    1\n    2` syntax
- Integration tests verify correct AST structure for curried and partial applications
- Ready for indentation-based let expressions or other syntax extensions

---
*Phase: 01-indentation-based-syntax*
*Completed: 2026-03-02*
