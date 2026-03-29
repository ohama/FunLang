---
phase: 61-hashtable-tuple-test-conversion
verified: 2026-03-29T09:46:37Z
status: passed
score: 6/6 must-haves verified
---

# Phase 61: Hashtable Tuple Test Conversion Verification Report

**Phase Goal:** Hashtable for-in이 tuple을 생성하고, 모든 flt 테스트가 module function 방식으로 동작한다
**Verified:** 2026-03-29T09:46:37Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth                                                                                      | Status     | Evidence                                                                                    |
|----|--------------------------------------------------------------------------------------------|------------|---------------------------------------------------------------------------------------------|
| 1  | `for (k, v) in ht do ...` destructures key-value tuples directly                          | VERIFIED   | Parser.fsy lines 258-261: FOR TuplePattern IN rules; Eval.fs 1054-1055: TupleValue [k; v]  |
| 2  | Existing dot notation flt tests converted to module function style                         | VERIFIED   | hashtable-forin.flt, hashtable-dot-api.flt, hashtable-keys-tryget.flt all use module fns   |
| 3  | Full flt test suite passes                                                                 | VERIFIED   | 637/637 passing confirmed by direct run                                                      |
| 4  | No `kv.Key` / `kv.Value` / KeyValuePair-based code remains in flt tests                  | VERIFIED   | grep returns zero matches across all .flt files                                              |
| 5  | HashtableValue iteration produces TupleValue, not RecordValue KeyValuePair                | VERIFIED   | Eval.fs line 1055: `TupleValue [kv.Key; kv.Value]`; Bidir.fs line 248: `TTuple [keyTy; valTy]` |
| 6  | All flt files use correct binary path (no stale `/Users/ohama/vibe/LangThree`)            | VERIFIED   | `grep -r "vibe/LangThree" tests/flt` returns 0 matches                                      |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact                                              | Expected                            | Status    | Details                                                          |
|-------------------------------------------------------|-------------------------------------|-----------|------------------------------------------------------------------|
| `src/LangThree/Ast.fs`                               | ForInExpr of var: Pattern           | VERIFIED  | Line 119: `ForInExpr of var: Pattern * collection: Expr * body: Expr * span: Span` |
| `src/LangThree/Parser.fsy`                           | FOR TuplePattern IN rules + VarPat wrap | VERIFIED  | Lines 253-261: both IDENT (VarPat) and TuplePattern rules present |
| `src/LangThree/Bidir.fs`                             | TTuple for THashtable; inferPattern binding | VERIFIED  | Lines 247-248: `THashtable -> TTuple [keyTy; valTy]`; lines 265-272: inferPattern |
| `src/LangThree/Eval.fs`                              | TupleValue for HashtableValue; matchPattern binding | VERIFIED  | Lines 1054-1062: TupleValue + matchPattern loop env              |
| `src/LangThree/Format.fs`                            | formatPattern for loop var          | VERIFIED  | Lines 217-218: `ForInExpr (pat, ...) -> formatPattern pat`       |
| `tests/flt/file/hashtable/hashtable-forin.flt`       | for (k, v) in ht do syntax          | VERIFIED  | Line 6: `for (k, v) in ht do`                                   |
| `tests/flt/file/hashtable/hashtable-dot-api.flt`     | Hashtable.tryGetValue/count/keys    | VERIFIED  | Uses `Hashtable.tryGetValue`, `Hashtable.count`, `Hashtable.keys` |
| `tests/flt/file/hashtable/hashtable-keys-tryget.flt` | Hashtable.keys/tryGetValue/count    | VERIFIED  | Uses `Hashtable.keys`, `Hashtable.tryGetValue`, `Hashtable.count` |

### Key Link Verification

| From                                  | To                                    | Via                                           | Status   | Details                                                                  |
|---------------------------------------|---------------------------------------|-----------------------------------------------|----------|--------------------------------------------------------------------------|
| `Ast.fs ForInExpr`                    | All consumer files                    | `var: Pattern` union case shape               | WIRED    | Parser, Bidir, Eval, Format, TypeCheck, Infer all destructure `ForInExpr(pat, ...)` |
| `Eval.fs HashtableValue iteration`    | `matchPattern`                        | `TupleValue [kv.Key; kv.Value]` as elemVal    | WIRED    | Lines 1054-1060: produces TupleValue, feeds matchPattern for env binding |
| `Bidir.fs THashtable`                 | `TTuple [keyTy; valTy]`               | `inferPattern` + `unifyWithContext`           | WIRED    | Lines 247-272: TTuple emitted, inferPattern binds loop pattern           |
| `hashtable-forin.flt`                 | ForInExpr TuplePat (Plan 01)          | `for (k, v) in ht do` syntax                 | WIRED    | Syntax exercises Plan 01's Parser TuplePattern + Eval.fs TupleValue path |
| `hashtable-dot-api.flt`               | Hashtable module functions (Phase 60) | `Hashtable.tryGetValue/count/keys` calls      | WIRED    | All calls use module function API; test passes in full suite              |

### Requirements Coverage

| Requirement | Status    | Notes                                                                                      |
|-------------|-----------|--------------------------------------------------------------------------------------------|
| STR-01      | SATISFIED | `for (k, v) in ht do` destructuring works; Parser + Eval + Bidir all wired                |
| TST-01      | SATISFIED | Full flt suite 637/637 PASS; all hashtable tests use module function style                 |

### Anti-Patterns Found

| File             | Line | Pattern                                   | Severity | Impact                                                                             |
|------------------|------|-------------------------------------------|----------|------------------------------------------------------------------------------------|
| `src/LangThree/Bidir.fs` | 644 | `TData("KeyValuePair", ...)` arm retained | INFO     | Intentionally kept per plan (dead code path — no LangThree code can produce TData KeyValuePair via hashtable iteration anymore, but arm is harmless) |

No blocker or warning-level anti-patterns found. The retained KeyValuePair field access arm in Bidir.fs is dead code (hashtable iteration now produces TTuple, not TData KeyValuePair), but it does not affect correctness and was explicitly kept per plan for future cleanup in Phase 62.

### Human Verification Required

None. All success criteria are programmatically verifiable and confirmed.

### Gaps Summary

No gaps. All truths verified, all artifacts substantive and wired, full test suites pass.

- Build: 0 errors, 0 warnings
- F# unit tests: 224/224 passed
- flt integration tests: 637/637 passed
- No stale paths in flt files
- No KeyValuePair dot-notation in any .flt test file
- `for (k, v) in ht do` syntax operative end-to-end

---

_Verified: 2026-03-29T09:46:37Z_
_Verifier: Claude (gsd-verifier)_
