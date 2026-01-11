# Issue 005: Parse Error with Multiple Multiline let rec Chains

## Status
Resolved

## Created
2026-01-11

## Resolved
2026-01-11

## Description
When chaining `let rec` expressions with `in` on separate lines, parsing failed. The issue was that the parser grammar didn't allow NEWLINE before `IN` keyword.

## Root Cause
The indentation processor generates NEWLINE tokens when encountering tokens at the same indentation level. When `in` was on a separate line:

```
let rec a = fun x -> x
in       <-- NEWLINE generated before IN
a 1
```

The parser grammar was:
```fsharp
| LET REC IDENT EQ expr IN nl_opt expr
```

This didn't allow NEWLINE between `expr` (the body) and `IN`.

## Solution
Added `nl_opt` before `IN` in both `LET...IN` and `LET REC...IN` rules:

```fsharp
| LET IDENT EQ expr nl_opt IN nl_opt expr
| LET REC IDENT EQ expr nl_opt IN nl_opt expr
```

## Files Changed
- `src/FunLang/Parser.fsy` - Added `nl_opt` before `IN`
- `tests/FunLang.Tests/ParserTests.fs` - Added 3 regression tests

## Verification
- 323 tests pass (3 new regression tests added)
- 5+ multiline let rec chains now parse correctly
- Existing single-line syntax unaffected

## Related Files
- `src/FunLang/Parser.fsy`
- `src/FunLang/Indentation.fs`
- `tests/FunLang.Tests/ParserTests.fs`
