---
phase: 69-span-position-fix
plan: 01
subsystem: lexer-parser
tags: [fslexyacc, positions, spans, lexbuf, error-messages, indent-filter]

# Dependency graph
requires:
  - phase: 68-cli-extensions
    provides: completed prior CLI phase; Program.fs and IndentFilter.fs in stable state
provides:
  - PositionedToken type carrying Token + StartPos + EndPos from lexbuf
  - filterPositioned function preserving source positions through indent-filter pipeline
  - lexAndFilter returning PositionedToken list with actual source positions
  - parseModuleFromString tokenizer that sets lb.StartPos/lb.EndPos per token
  - Correct file:line:col in all type-checker error messages
affects: [all future phases that read error message output, any phase using AST Span fields]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - PositionedToken wrapping pattern: capture lexbuf.StartPos before tokenize, EndPos after
    - Synthetic token position inheritance: inserted INDENT/DEDENT/SEMICOLON/IN copy position from last real token

key-files:
  created: []
  modified:
    - src/LangThree/IndentFilter.fs
    - src/LangThree/Program.fs

key-decisions:
  - "Capture StartPos BEFORE Lexer.tokenize and EndPos AFTER - this is how fslex advances the buffer"
  - "Synthetic tokens (INDENT/DEDENT/SEMICOLON/IN) inherit position from last real token via withPosOf"
  - "Keep existing filter function unchanged for --emit-tokens and any other callers returning token list"

patterns-established:
  - "filterPositioned: positions-aware mirror of filter, operating on PositionedToken list instead of token seq"

# Metrics
duration: 3min
completed: 2026-03-31
---

# Phase 69 Plan 01: Span Position Fix Summary

**Fixed AST Span zeroing by threading PositionedToken through lex/filter/parse pipeline, enabling correct file:line:col in type-checker error messages**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-31T06:47:48Z
- **Completed:** 2026-03-31T06:51:08Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Added `PositionedToken` record type with `Token`, `StartPos`, `EndPos` fields to IndentFilter.fs
- Added `filterPositioned` function that mirrors `filter` but preserves source positions through all token transformations including synthetic INDENT/DEDENT/SEMICOLON/IN tokens
- Updated `lexAndFilter` to capture `lexbuf.StartPos`/`lexbuf.EndPos` around each `Lexer.tokenize` call
- Updated `parseModuleFromString` tokenizer to set `lb.StartPos <- pt.StartPos` and `lb.EndPos <- pt.EndPos` before returning each token - this was the core fix
- Error messages now show correct positions (e.g., `test_span.lt:2:6-19` instead of `:0:0`)

## Task Commits

1. **Task 1: Add PositionedToken and filterPositioned to IndentFilter** - `64d77f8` (feat)
2. **Task 2: Update lexAndFilter and parseModuleFromString to use PositionedToken pipeline** - `988098b` (fix)

## Files Created/Modified

- `src/LangThree/IndentFilter.fs` - Added `PositionedToken` type, `withPosOf` helper, `filterPositioned` function (~195 lines added)
- `src/LangThree/Program.fs` - Updated `lexAndFilter` return type and position capture; updated `parseModuleFromString` tokenizer to set lexbuf positions

## Decisions Made

- Capture `lexbuf.StartPos` BEFORE calling `Lexer.tokenize` and `lexbuf.EndPos` AFTER - fslex advances the buffer during tokenization so order matters
- Synthetic tokens (INDENT, DEDENT, SEMICOLON, IN) inherit position from `lastRealToken` via `withPosOf` - they have no source position of their own
- Keep the existing `filter` function unchanged - it returns `Parser.token seq` which is still used by `--emit-tokens` path in Format.fs

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

One unit test (`MOD-02: List.length via prelude qualified access`) showed a flaky failure on first full suite run due to pre-existing mutable global state (`TypeCheck.currentTypeCheckingFile`) being set by a preceding test. The test passes when run individually and on subsequent full suite runs. This was a pre-existing issue, not caused by this fix.

## Next Phase Readiness

- Span fix is complete; all 224 unit tests and 659 integration tests pass
- Error messages now include correct source positions throughout the compiler pipeline
- No blockers for future work

---
*Phase: 69-span-position-fix*
*Completed: 2026-03-31*
