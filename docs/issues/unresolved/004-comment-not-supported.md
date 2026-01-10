# Issue 004: Comments Not Supported in Lexer

## Status
Unresolved

## Created
2026-01-11

## Description
The lexer does not handle comments (`-- ...`). When a file contains comment lines, parsing fails.

## Reproduction
```bash
# This file will fail to parse via CLI
echo '-- This is a comment
1 + 2' | dotnet run --project src/FunLang -- -e "$(cat)"
```

## Current Workaround
- DemoTests.fs uses `removeComments` function to strip comment lines before parsing
- Demo files keep `-- Expected:` header for test extraction, but tests remove all comments before parsing

## Expected Behavior
Lexer should skip comment lines (lines starting with `--`) or tokenize them as whitespace.

## Proposed Fix
Add comment handling rule in `Lexer.fsl`:
```fsl
| "--" [^ '\n']* { token lexbuf }  // Skip single-line comments
```

## Impact
- Low: Tests work with workaround
- CLI cannot run files with comments directly

## Related Files
- `src/FunLang/Lexer.fsl`
- `tests/FunLang.Tests/DemoTests.fs` (workaround implementation)
