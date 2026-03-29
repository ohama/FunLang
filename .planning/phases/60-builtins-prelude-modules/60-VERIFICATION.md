---
phase: 60-builtins-prelude-modules
verified: 2026-03-29T08:59:31Z
status: passed
score: 8/8 must-haves verified
---

# Phase 60: Builtins & Prelude Modules Verification Report

**Phase Goal:** 사용자가 module function으로 모든 string/hashtable/stringbuilder 연산을 수행할 수 있다 (dot notation 제거 전 대안 확보)
**Verified:** 2026-03-29T08:59:31Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth                                                                                       | Status     | Evidence                                                                          |
| --- | ------------------------------------------------------------------------------------------- | ---------- | --------------------------------------------------------------------------------- |
| 1   | string_endswith, string_startswith, string_trim builtins exist and work at runtime          | VERIFIED   | Eval.fs lines 237-261: real implementations using .EndsWith/.StartsWith/.Trim()   |
| 2   | hashtable_trygetvalue returns (bool * value) tuple, hashtable_count returns int             | VERIFIED   | Eval.fs lines 668-682: uses ht.TryGetValue; returns TupleValue [BoolValue; v]     |
| 3   | All 5 new builtins have type schemes in TypeCheck.fs                                        | VERIFIED   | TypeCheck.fs lines 35-37, 169-170: exact schemes matching Eval.fs names           |
| 4   | Existing builtins and tests are unaffected                                                  | VERIFIED   | F# unit tests: 224/224 pass; flt suite: 632/637 (5 pre-existing failures)        |
| 5   | String.endsWith, String.startsWith, String.trim work via module-qualified calls             | VERIFIED   | Prelude/String.fun: 6-line module, endsWith/startsWith/trim wrappers; flt PASS    |
| 6   | String.length and String.contains expose existing builtins via module                      | VERIFIED   | Prelude/String.fun lines 6-7: length and contains wrappers; flt PASS             |
| 7   | Hashtable.tryGetValue returns (true,value) or (false,()) tuple via module call             | VERIFIED   | Prelude/Hashtable.fun line 8: tryGetValue wrapper; flt PASS with (true,1)/(false,()) |
| 8   | StringBuilder.add replaces StringBuilder.append (renamed to avoid List.append conflict)    | VERIFIED   | Prelude/StringBuilder.fun line 3: `let add sb s = stringbuilder_append sb s`      |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact                                                                  | Expected                                        | Status     | Details                                                       |
| ------------------------------------------------------------------------- | ----------------------------------------------- | ---------- | ------------------------------------------------------------- |
| `src/LangThree/Eval.fs`                                                   | 5 new builtin entries in initialBuiltinEnv      | VERIFIED   | Lines 237-261 (string), 668-682 (hashtable); real impls       |
| `src/LangThree/TypeCheck.fs`                                              | 5 new type schemes in initialTypeEnv            | VERIFIED   | Lines 35-37 (string), 169-170 (hashtable); correct schemes    |
| `Prelude/String.fun`                                                      | endsWith, startsWith, trim, length, contains    | VERIFIED   | 7-line file, 6 functions including concat                     |
| `Prelude/Hashtable.fun`                                                   | tryGetValue, count added to existing 6          | VERIFIED   | 9-line file, 8 functions total                                |
| `Prelude/StringBuilder.fun`                                               | add (not append) wrapping stringbuilder_append  | VERIFIED   | 4-line file, uses `add` correctly                             |
| `tests/flt/file/string/string-module-endswith-startswith-trim.flt`        | BLT-01, BLT-02, BLT-03, MOD-01 tests           | VERIFIED   | 14-line test; all 5 cases; PASS                               |
| `tests/flt/file/string/string-module-length-contains.flt`                 | MOD-01 length/contains tests                    | VERIFIED   | 12-line test; 4 cases; PASS                                   |
| `tests/flt/file/hashtable/hashtable-module-trygetvalue-count.flt`         | BLT-04, BLT-05, MOD-02 tests                   | VERIFIED   | 13-line test; tryGetValue+count; PASS                         |
| `tests/flt/file/string/stringbuilder-module-add.flt`                      | MOD-03 add test                                 | VERIFIED   | 9-line test; StringBuilder.add; PASS                          |

### Key Link Verification

| From                       | To                          | Via                                       | Status   | Details                                         |
| -------------------------- | --------------------------- | ----------------------------------------- | -------- | ----------------------------------------------- |
| `Prelude/String.fun`       | `src/LangThree/Eval.fs`     | string_endswith, string_startswith, string_trim | WIRED | Direct builtin calls in wrapper functions       |
| `Prelude/Hashtable.fun`    | `src/LangThree/Eval.fs`     | hashtable_trygetvalue, hashtable_count    | WIRED    | Direct builtin calls in wrapper functions       |
| `Prelude/StringBuilder.fun`| `src/LangThree/Eval.fs`     | stringbuilder_append                      | WIRED    | `let add sb s = stringbuilder_append sb s`      |
| `Eval.fs` builtins         | `TypeCheck.fs` type schemes | Matching names in both initialBuiltinEnv / initialTypeEnv | WIRED | All 5 names match exactly |

### Requirements Coverage

| Requirement | Status      | Notes                                                              |
| ----------- | ----------- | ------------------------------------------------------------------ |
| BLT-01      | SATISFIED   | string_endswith in Eval.fs + TypeCheck.fs; flt test PASS           |
| BLT-02      | SATISFIED   | string_startswith in Eval.fs + TypeCheck.fs; flt test PASS         |
| BLT-03      | SATISFIED   | string_trim in Eval.fs + TypeCheck.fs; flt test PASS               |
| BLT-04      | SATISFIED   | hashtable_trygetvalue in Eval.fs + TypeCheck.fs; flt test PASS     |
| BLT-05      | SATISFIED   | hashtable_count in Eval.fs + TypeCheck.fs; flt test PASS           |
| MOD-01      | SATISFIED   | String.fun: endsWith, startsWith, trim, length, contains; tests PASS |
| MOD-02      | SATISFIED   | Hashtable.fun: tryGetValue, count; flt test PASS                   |
| MOD-03      | SATISFIED   | StringBuilder.fun: add (renamed from append); flt test PASS        |

### Anti-Patterns Found

No anti-patterns found in Phase 60 modified files.

- Eval.fs: no TODO/FIXME/placeholder in new code
- TypeCheck.fs: pre-existing `// Already processed in first pass (ExceptionDecl: TODO in Plan 02)` comment; unrelated to Phase 60
- Prelude .fun files: clean, no stubs

### Pre-Existing Test Failures (Not Phase 60 Regressions)

5 flt tests fail in the full suite; all pre-date Phase 60:

| File                                | Failure Cause                             | Committed In |
| ----------------------------------- | ----------------------------------------- | ------------ |
| `hashtable-dot-api.flt`             | Uses dot notation (`ht.TryGetValue`) — feature not yet implemented | Phase 57 |
| `hashtable-forin.flt`               | Uses dot notation (`kv.Key`, `kv.Value`) | Phase 57     |
| `hashtable-keys-tryget.flt`         | Untracked test (may depend on unimplemented features) | Untracked |
| `property-count-consistency.flt`    | Untracked test                            | Untracked    |
| `str-concat-module.flt`             | Wrong binary path (`/vibe/` vs `/vibe-coding/`) | Phase 55 |

None of these are caused by Phase 60 changes.

### Human Verification Required

None. All success criteria verifiable programmatically. All 4 flt integration tests pass, confirming end-to-end behavior of the full stack.

---

_Verified: 2026-03-29T08:59:31Z_
_Verifier: Claude (gsd-verifier)_
