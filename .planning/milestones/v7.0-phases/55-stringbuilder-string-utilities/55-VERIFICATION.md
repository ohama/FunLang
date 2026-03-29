---
phase: 55-stringbuilder-string-utilities
verified: 2026-03-29T03:01:37Z
status: gaps_found
score: 4/5 must-haves verified
gaps:
  - truth: "StringBuilder() creates a builder, .Append('text') chains appends, .ToString() produces the final string"
    status: partial
    reason: "Implementation is correct and verified against new binary, but flt tests (stringbuilder-basic.flt, stringbuilder-chaining.flt) reference old binary path /Users/ohama/vibe-coding/LangThree/... causing test suite to show 2 failures when run via FsLit"
    artifacts:
      - path: "tests/flt/file/string/stringbuilder-basic.flt"
        issue: "Command line references /Users/ohama/vibe-coding/LangThree/... (old binary, no StringBuilder) instead of /Users/ohama/vibe/LangThree/... (new binary)"
      - path: "tests/flt/file/string/stringbuilder-chaining.flt"
        issue: "Command line references /Users/ohama/vibe-coding/LangThree/... (old binary, no StringBuilder) instead of /Users/ohama/vibe/LangThree/..."
    missing:
      - "Update command path in stringbuilder-basic.flt from /Users/ohama/vibe-coding/LangThree/... to /Users/ohama/vibe/LangThree/..."
      - "Update command path in stringbuilder-chaining.flt from /Users/ohama/vibe-coding/LangThree/... to /Users/ohama/vibe/LangThree/..."
---

# Phase 55: StringBuilder & String Utilities Verification Report

**Phase Goal:** 사용자가 StringBuilder로 효율적인 문자열 조합을 하고, 문자열/문자 유틸리티 메서드를 사용할 수 있다
**Verified:** 2026-03-29T03:01:37Z
**Status:** gaps_found (implementation correct, 2 flt tests have wrong binary path)
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| 1 | `"hello.txt".EndsWith(".txt")` returns true, `"hello".StartsWith("he")` returns true | VERIFIED | str-methods-endswith-startswith.flt PASS; code in Eval.fs lines 1193-1202 |
| 2 | `" hi ".Trim()` returns "hi" (no crash) | VERIFIED | str-methods-trim.flt PASS; Trim returns BuiltinValue accepting TupleValue [] |
| 3 | `Char.IsDigit('3')` returns true, `Char.ToUpper('a')` returns 'A' | VERIFIED | char-module-isdigit-toupper.flt PASS; Prelude/Char.fun + char_is_digit/char_to_upper builtins |
| 4 | `String.concat ", " ["a"; "b"; "c"]` returns "a, b, c" | VERIFIED | str-concat-module.flt PASS; Prelude/String.fun + string_concat_list builtin |
| 5 | `eprintfn "error: %s" msg` prints to stderr and returns unit | VERIFIED | eprintfn-basic.flt PASS; applyEprintfnArgs in Eval.fs + eprintfn scheme in TypeCheck.fs |
| 6 | `StringBuilder()` creates builder, `.Append()` chains, `.ToString()` returns string | PARTIAL | Runtime correct (verified with new binary); 2 flt tests fail due to wrong binary path in test files |

**Score:** 5/6 truths functionally verified (StringBuilder runtime works; flt tests have path issue)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/LangThree/Eval.fs` | EndsWith/StartsWith/Trim in StringValue FieldAccess arm; applyEprintfnArgs; eprintfn in initialBuiltinEnv; char_is_digit/char_to_upper etc.; StringBuilderValue constructor + FieldAccess | VERIFIED | All patterns confirmed at lines 1193-1207, 105-115, 289-293, 316-353, 1058-1068, 1215-1228 |
| `src/LangThree/Bidir.fs` | EndsWith/StartsWith/Trim type rules; TData("StringBuilder",[]) constructor and FieldAccess arms | VERIFIED | Lines 534-536 (string methods), lines 67-74 (constructor), lines 546-554 (FieldAccess) |
| `src/LangThree/TypeCheck.fs` | eprintfn scheme; char_is_digit etc. schemes; string_concat_list scheme | VERIFIED | Line 57 (eprintfn), lines 69-77 (char + string_concat_list) |
| `src/LangThree/Ast.fs` | StringBuilderValue DU case with GetHashCode/valueEqual/valueCompare | VERIFIED | Lines 209, 232, 258, 271 |
| `Prelude/Char.fun` | Char module wrapping char builtins | VERIFIED | 7 lines, exports IsDigit/ToUpper/IsLetter/IsUpper/IsLower/ToLower |
| `Prelude/String.fun` | String module wrapping string_concat_list | VERIFIED | 3 lines, exports concat |
| `Prelude/StringBuilder.fun` | StringBuilder module with create/append/toString | VERIFIED | 4 lines, uses stringbuilder_create/append/tostring builtins |
| `tests/flt/file/string/str-methods-endswith-startswith.flt` | flt test for EndsWith and StartsWith | VERIFIED | PASS with new binary path |
| `tests/flt/file/string/str-methods-trim.flt` | flt test for Trim() | VERIFIED | PASS |
| `tests/flt/file/print/eprintfn-basic.flt` | flt test for eprintfn with Stderr section | VERIFIED | PASS (uses // --- Stderr: section) |
| `tests/flt/file/char/char-module-isdigit-toupper.flt` | flt test for Char.IsDigit and Char.ToUpper | VERIFIED | PASS |
| `tests/flt/file/string/str-concat-module.flt` | flt test for String.concat | VERIFIED | PASS |
| `tests/flt/file/string/stringbuilder-basic.flt` | flt test for basic StringBuilder usage | PARTIAL | FAIL — wrong binary path (vibe-coding instead of vibe) |
| `tests/flt/file/string/stringbuilder-chaining.flt` | flt test for method chaining | PARTIAL | FAIL — wrong binary path (vibe-coding instead of vibe) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Eval.fs FieldAccess StringValue arm | EndsWith/StartsWith/Trim dispatch | match fieldName cases | WIRED | Lines 1193-1207, after Contains case |
| Eval.fs initialBuiltinEnv | applyEprintfnArgs | "eprintfn" BuiltinValue entry | WIRED | Line 289-293 |
| Bidir.fs TString FieldAccess arm | EndsWith/StartsWith/Trim type arrows | match fieldName cases | WIRED | Lines 534-536 |
| Eval.fs Constructor arm | StringBuilderValue creation | match name, argOpt with "StringBuilder" | WIRED | Lines 1058-1068 |
| Eval.fs FieldAccess StringBuilderValue arm | Append/ToString dispatch | match fieldName with Append/ToString | WIRED | Lines 1215-1228 |
| Bidir.fs Constructor arm | TData("StringBuilder",[]) | match name "StringBuilder" intercept | WIRED | Lines 67-74 |
| Bidir.fs FieldAccess arm | TData("StringBuilder",[]) Append/ToString types | TData("StringBuilder",[]) arm before general TData | WIRED | Lines 546-554 |
| Prelude/Char.fun | char_is_digit builtin | Char.IsDigit calls char_is_digit | WIRED | Line 2 of Char.fun |
| Prelude/String.fun | string_concat_list builtin | String.concat calls string_concat_list | WIRED | Line 2 of String.fun |

### Requirements Coverage

| Requirement | Status | Notes |
|-------------|--------|-------|
| COLL-01 (StringBuilder) | SATISFIED (runtime) | StringBuilder() creates builder, Append chains (via intermediate bindings), ToString returns accumulated string. flt tests have path issue. |
| STR-01 (EndsWith, StartsWith, Trim) | SATISFIED | All three methods working and tested |
| STR-02 (Char module: IsDigit, ToUpper) | SATISFIED | Char.IsDigit, Char.ToUpper, plus IsLetter, IsUpper, IsLower, ToLower |
| STR-03 (String.concat) | SATISFIED | String.concat ", " ["a";"b";"c"] returns "a, b, c" |
| STR-04 (eprintfn) | SATISFIED | eprintfn writes to stderr, returns unit, supports %s format specifier |

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| `tests/flt/file/string/stringbuilder-basic.flt` | Wrong binary path (vibe-coding vs vibe) | Warning | Test fails when run via FsLit suite; actual code is correct |
| `tests/flt/file/string/stringbuilder-chaining.flt` | Wrong binary path (vibe-coding vs vibe) | Warning | Test fails when run via FsLit suite; actual code is correct |

### Build Status

`dotnet build src/LangThree/LangThree.fsproj -c Release` — SUCCESS, 0 errors, 0 warnings

### Test Results

| Test Suite | Result |
|------------|--------|
| tests/flt/file/string/ | 8/10 passed (2 stringbuilder failures due to binary path) |
| tests/flt/file/print/ | 10/10 passed |
| tests/flt/file/char/ | 7/7 passed |
| tests/flt/file/ (full suite) | 376/378 passed (only stringbuilder failures) |

### Gaps Summary

The implementation for all 5 success criteria is correct and substantive. All runtime behavior works as expected:

- `"hello.txt".EndsWith(".txt")` evaluates to true (verified)
- `" hi ".Trim()` evaluates to "hi" (verified — Trim correctly returns BuiltinValue accepting TupleValue [])
- `Char.IsDigit '3'` returns true, `Char.ToUpper 'a'` returns `'A'` (verified)
- `String.concat ", " ["a"; "b"; "c"]` returns "a, b, c" (verified)
- `eprintfn "error: %s" "oops"` writes to stderr (verified)
- `StringBuilder()` creates a builder, `.Append("text")` appends and returns the same builder, `.ToString()` returns the accumulated string (verified with new binary)

The single gap is that `stringbuilder-basic.flt` and `stringbuilder-chaining.flt` reference the old binary path `/Users/ohama/vibe-coding/LangThree/...` instead of `/Users/ohama/vibe/LangThree/...`. This causes these 2 tests to fail in the FsLit test suite, even though the underlying implementation is fully correct. The fix is a 1-line change in each test file.

---

*Verified: 2026-03-29T03:01:37Z*
*Verifier: Claude (gsd-verifier)*
