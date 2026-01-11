# Issue 007: Test Files Need Update for New Token Position Format

## Status
**Resolved** | Created: 2026-01-12 | Resolved: 2026-01-12

## Resolution

Updated all 15 test files to match new output format:
- 7 lex-tests: Added `[line:col]` prefix to each token, EOF at `[2:1]` for files with trailing newline
- 8 error-tests: Updated error positions to correct line/column values

All 479 tests now pass.

## Description

Issue 006 (parser error position) was fixed by changing `Program.fs` to use `tokenizeWithPositions` + `parseProgramWithPositions` instead of `tokenize` + `parse`. However, this change also updated the `--show-tokens` output format to include position information.

## Root Cause of Issue 006

The original issue was that `Program.fs` used `tokenize` (without positions) and then `parse` which assigned `(1,1)` to all tokens:

```fsharp
// OLD (broken)
match tokenize input with
| Ok tokens ->
    match parse tokens with  // parse assigns (1,1) to all tokens
```

Fixed by using:
```fsharp
// NEW (working)
match tokenizeWithPositions input with
| Ok tokensWithPos ->
    match parseProgramWithPositions tokensWithPos with  // preserves positions
```

## Current Behavior

Parser error positions now work correctly:
```
$ dotnet run --project src/FunLang -- -e "fun x x"
Parse error: Parse error at line 1, column 7: unexpected 'IDENT "x"', expected '->'
```

But `--show-tokens` output format changed:
```
OLD:                    NEW:
=== LEXER TOKENS ===    === LEXER TOKENS ===
  INT 42                  [1:1] INT 42
  EOF                     [2:1] EOF
====================    ====================
```

## Test Failures

15 tests failing (464 passing):

**lex-tests (7):** Output format mismatch
- 001-integer.test
- 002-arithmetic.test
- 003-keywords.test
- 004-lambda.test
- 005-boolean.test
- 006-comparison.test
- 007-list.test

**error-tests (8):** Position now correct instead of (1,1)
- 003-parser-incomplete-let.test
- 004-parser-missing-then.test
- 101-parser-match-no-with.test
- 102-parser-lambda-no-arrow.test
- 103-parser-if-no-else.test
- 104-parser-unclosed-paren.test
- 105-parser-unclosed-bracket.test
- 106-parser-empty-body.test

## Files Modified

- `src/FunLang/Program.fs` - Changed to use `tokenizeWithPositions` and `parseProgramWithPositions`
- `src/FunLang/ParserWrapper.fs` - No functional changes (only debug output removed)

## Resolution Required

Update all 15 test files to match new output format:
1. lex-tests: Add `[line:col]` prefix to each token
2. error-tests: Update error position from `line 1, column 1` to correct position

## Impact

Medium - Tests are failing but the actual functionality is improved (positions are now correct).

## Workaround

None needed - the fix improves error messages. Just need to update test expectations.
