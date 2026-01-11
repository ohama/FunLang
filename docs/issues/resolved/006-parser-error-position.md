# Issue 006: Parser Error Position Always Shows Line 1, Column 1

## Status
**Unresolved** | Created: 2026-01-12

## Description

Parser error messages now show detailed information (unexpected token, expected tokens), but the position is always reported as `line 1, column 1` regardless of where the actual error occurs.

## Current Behavior

```
$ dotnet run --project src/FunLang -- -e "fun x x"
Parse error: Parse error at line 1, column 1: unexpected 'IDENT "x"', expected '->'
```

The error correctly identifies:
- Unexpected token: `IDENT "x"`
- Expected token: `'->'`

But position is wrong - should be around column 7, not column 1.

## Expected Behavior

```
Parse error at line 1, column 7: unexpected 'IDENT "x"', expected '->'
```

## Root Cause Analysis

1. `currentTokenPosition` mutable variable tracks position during lexing
2. When parser error occurs, the position captured is not the error location
3. The `ParseErrorContext.ParseState.InputStartPosition` from FsYacc doesn't work correctly with our token-list-based parsing approach

## Files Involved

- `src/FunLang/Parser.fsy` - RichParseError exception and parse_error_rich handler
- `src/FunLang/ParserWrapper.fs` - Token position tracking, error formatting
- `src/FunLang/Indentation.fs` - processIndentationWithPositions

## What Was Implemented

1. `RichParseError` exception type with currentToken, expectedTokens, position
2. `parse_error_rich` handler in Parser.fsy header
3. `tokenTagToName` function for human-readable token names
4. `processIndentationWithPositions` to preserve positions through indentation processing
5. `tokenizeWithPositions` and `parseProgramWithPositions` functions

## Potential Solutions

1. **Track "last consumed token position"** - Store position when lexer function is called, use that for error reporting
2. **Use lexbuf position** - But we use dummy lexbuf, so this doesn't work
3. **Pass positions through parser state** - More complex, requires parser changes

## Workaround

Error messages still provide useful information about what was unexpected and what was expected. Position can be inferred from context in most cases.

## Impact

Low - Error messages are significantly improved even without correct positions.

## Related

- Parser error tests (101-106) need updating once position is fixed
