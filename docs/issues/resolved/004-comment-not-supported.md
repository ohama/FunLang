# Issue 004: Comments Not Supported in Lexer

## Status
Resolved (Won't Fix)

## Created
2026-01-11

## Resolved
2026-01-11

## Description
The lexer does not handle comments (`-- ...`). When a file contains comment lines, parsing fails.

## Resolution
**Won't Fix** - Comments are not needed for the current implementation:
- File-based testing uses `// --COMMAND` format (not FunLang syntax)
- Demo files no longer use `-- Expected:` comments (migrated to file-based tests)
- Language design decision: no comments in FunLang v0.1

## Original Impact
- Low: Tests worked with workaround
- CLI could not run files with comments directly

## Related Files
- `src/FunLang/Lexer.fsl`
- `tests/FunLang.Tests/FileBasedTests.fs` (replaced DemoTests.fs)
