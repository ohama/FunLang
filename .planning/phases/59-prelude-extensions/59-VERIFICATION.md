---
phase: 59-prelude-extensions
verified: 2026-03-29T07:18:49Z
status: passed
score: 5/5 must-haves verified
---

# Phase 59: Prelude Extensions Verification Report

**Phase Goal:** 사용자가 List/Array 표준 라이브러리 함수로 정렬, 검색, 변환 등 일반적인 컬렉션 연산을 수행할 수 있다
**Verified:** 2026-03-29T07:18:49Z
**Status:** PASSED
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth                                                                        | Status     | Evidence                                                         |
| --- | ---------------------------------------------------------------------------- | ---------- | ---------------------------------------------------------------- |
| 1   | List.sort [3;1;2] returns [1;2;3] and List.sortBy (fun x -> -x) [1;2;3] returns [3;2;1] | ✓ VERIFIED | Smoke test output confirmed; prelude-list-sort.flt PASS          |
| 2   | List.tryFind, List.choose, List.distinctBy, List.exists work with predicates | ✓ VERIFIED | Smoke test output confirmed; prelude-list-search.flt PASS        |
| 3   | List.mapi, List.item, List.isEmpty, List.head, List.tail provide standard ops | ✓ VERIFIED | Smoke test output confirmed; prelude-list-transform.flt PASS     |
| 4   | List.ofSeq converts HashSet, Queue, MutableList to immutable lists           | ✓ VERIFIED | Smoke test lengths (3,2,2) confirmed; prelude-list-ofseq.flt PASS |
| 5   | Array.sort sorts in-place and Array.ofSeq creates arrays from collections    | ✓ VERIFIED | Smoke test [1;2;3] and length 3 confirmed; prelude-array-sort-ofseq.flt PASS |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact                                             | Expected                              | Status     | Details                                                    |
| ---------------------------------------------------- | ------------------------------------- | ---------- | ---------------------------------------------------------- |
| `src/LangThree/Eval.fs` (lines 759-795)              | 4 new BuiltinValue entries            | ✓ VERIFIED | list_sort_by, list_of_seq, array_sort, array_of_seq present |
| `Prelude/List.fun`                                   | sort, sortBy, exists, tryFind, choose, distinctBy, mapi, item, isEmpty, head, tail, ofSeq | ✓ VERIFIED | All 12 functions present; 59 lines total |
| `Prelude/Array.fun`                                  | sort and ofSeq wrappers               | ✓ VERIFIED | Lines 12-13: sort and ofSeq present                        |
| `tests/flt/file/prelude/prelude-list-sort.flt`       | PRE-01 test coverage                  | ✓ VERIFIED | PASS; tests List.sort and List.sortBy                       |
| `tests/flt/file/prelude/prelude-list-search.flt`     | PRE-02 test coverage                  | ✓ VERIFIED | PASS; tests exists, tryFind, choose, distinctBy             |
| `tests/flt/file/prelude/prelude-list-transform.flt`  | PRE-03 test coverage                  | ✓ VERIFIED | PASS; tests mapi, item, isEmpty, head, tail                 |
| `tests/flt/file/prelude/prelude-list-ofseq.flt`      | PRE-04 test coverage                  | ✓ VERIFIED | PASS; tests List.ofSeq from HashSet, Queue, MutableList     |
| `tests/flt/file/prelude/prelude-array-sort-ofseq.flt`| PRE-05 test coverage                  | ✓ VERIFIED | PASS; tests Array.sort in-place and Array.ofSeq             |

### Key Link Verification

| From                  | To                         | Via                       | Status     | Details                                              |
| --------------------- | -------------------------- | ------------------------- | ---------- | ---------------------------------------------------- |
| `Prelude/List.fun`    | `src/LangThree/Eval.fs`    | `list_sort_by` builtin    | ✓ WIRED    | Line 31: `let sortBy f xs = list_sort_by f xs`       |
| `Prelude/List.fun`    | `src/LangThree/Eval.fs`    | `list_of_seq` builtin     | ✓ WIRED    | Line 57: `let ofSeq coll = list_of_seq coll`         |
| `Prelude/Array.fun`   | `src/LangThree/Eval.fs`    | `array_sort` builtin      | ✓ WIRED    | Line 12: `let sort arr = array_sort arr`             |
| `Prelude/Array.fun`   | `src/LangThree/Eval.fs`    | `array_of_seq` builtin    | ✓ WIRED    | Line 13: `let ofSeq coll = array_of_seq coll`        |
| `list_sort_by` builtin| `Value.valueCompare`       | F# List.sortWith          | ✓ WIRED    | Eval.fs line 765: sortWith uses Value.valueCompare   |
| `list_sort_by` builtin| user closure               | callValue                 | ✓ WIRED    | Eval.fs line 764: callValue fVal x for key extraction |
| `array_sort` builtin  | `System.Array.Sort`        | Value.valueCompare lambda | ✓ WIRED    | Eval.fs line 783: Sort with valueCompare comparer    |

### Requirements Coverage

| Requirement | Status      | Blocking Issue |
| ----------- | ----------- | -------------- |
| PRE-01      | ✓ SATISFIED | None — prelude-list-sort.flt PASS |
| PRE-02      | ✓ SATISFIED | None — prelude-list-search.flt PASS |
| PRE-03      | ✓ SATISFIED | None — prelude-list-transform.flt PASS |
| PRE-04      | ✓ SATISFIED | None — prelude-list-ofseq.flt PASS |
| PRE-05      | ✓ SATISFIED | None — prelude-array-sort-ofseq.flt PASS |

### Anti-Patterns Found

No blockers or warnings found. No TODO/FIXME/placeholder patterns in modified files. No stub implementations detected.

### Test Suite Health

- 5 new flt tests added (prelude-list-sort, prelude-list-search, prelude-list-transform, prelude-list-ofseq, prelude-array-sort-ofseq)
- Full suite result: 561/619 passed (was 556/614 before Phase 59)
- Change: +5 new passing tests, 0 regressions
- The 58 failures are pre-existing and unrelated to Phase 59

### Build Health

- `dotnet build src/LangThree/LangThree.fsproj -c Release` — 0 errors, 0 warnings

### Infrastructure Fix (noted in SUMMARY)

During Plan 03, TypeCheck.fs was updated to change `list_of_seq` and `array_of_seq` type schemes: input changed from `TList (TVar 0)` to `TVar 0` (unconstrained). This allows HashSet/Queue/MutableList to be passed where the type checker had previously rejected them. This was a necessary and correct fix.

---

_Verified: 2026-03-29T07:18:49Z_
_Verifier: Claude (gsd-verifier)_
