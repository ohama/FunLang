---
phase: 57-mutablelist-hashtable-enhancement
verified: 2026-03-29T04:17:38Z
status: passed
score: 4/4 must-haves verified
---

# Phase 57: MutableList & Hashtable Enhancement Verification Report

**Phase Goal:** 사용자가 MutableList로 가변 크기 리스트를 사용하고, Hashtable의 확장된 API(.TryGetValue, .Count, .Keys)를 활용할 수 있다
**Verified:** 2026-03-29T04:17:38Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth                                                                                         | Status     | Evidence                                                                                        |
|----|-----------------------------------------------------------------------------------------------|------------|-------------------------------------------------------------------------------------------------|
| 1  | MutableList() creates a list, .Add(v) appends, ml.[i] accesses by index, .Count returns size | VERIFIED   | Eval.fs constructor interception, FieldAccess .Add/.Count, IndexGet/Set; mutablelist-basic.flt PASS, mutablelist-indexing.flt PASS |
| 2  | ht.TryGetValue(key) returns (true, value) or (false, ...) tuple; .Count and .Keys work       | VERIFIED   | Eval.fs HashtableValue FieldAccess dispatch at line 1400-1409; hashtable-dot-api.flt PASS      |
| 3  | .Count works consistently across HashSet, Queue, MutableList, and Hashtable                  | VERIFIED   | Eval.fs: HashSet.Count (line 1370), Queue.Count (line 1388), MutableList.Count (line 1397), Hashtable.Count (line 1407) |
| 4  | flt tests verify MutableList indexing and Hashtable TryGetValue with existing/missing keys   | VERIFIED   | 2/2 mutablelist tests PASS, 11/11 hashtable tests PASS (including hashtable-dot-api.flt)       |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact                                                        | Expected                                   | Status      | Details                                              |
|-----------------------------------------------------------------|--------------------------------------------|-------------|------------------------------------------------------|
| `src/LangThree/Ast.fs`                                         | MutableListValue DU case                   | VERIFIED    | Line 212: `MutableListValue of System.Collections.Generic.List<Value>` |
| `src/LangThree/Eval.fs`                                        | Constructor, FieldAccess, IndexGet/Set, raw builtins, HashtableValue FieldAccess | VERIFIED | Lines 717-755 (raw builtins), 988-1013 (IndexGet/Set), 1191-1197 (constructor), 1391-1409 (FieldAccess) |
| `src/LangThree/Bidir.fs`                                       | MutableList type rules + THashtable type rules | VERIFIED | Lines 91-98 (constructor), 604+ (MutableList FieldAccess), 613-618 (THashtable FieldAccess) |
| `src/LangThree/TypeCheck.fs`                                   | Raw builtin type schemes                   | VERIFIED    | Lines 192-201: all 5 mutablelist_* schemes           |
| `Prelude/MutableList.fun`                                      | create, add, get, set, count               | VERIFIED    | 6 lines, all 5 functions delegating to raw builtins  |
| `tests/flt/file/mutablelist/mutablelist-basic.flt`            | Tests Add, Count, indexing                 | VERIFIED    | Tests ml.Add, ml.Count, ml.[0..2]; PASS              |
| `tests/flt/file/mutablelist/mutablelist-indexing.flt`         | Tests index read and write                 | VERIFIED    | Tests ml.[i] read and ml.[i] <- v write; PASS        |
| `tests/flt/file/hashtable/hashtable-dot-api.flt`              | Tests TryGetValue, Count, Keys             | VERIFIED    | Tests hit/miss TryGetValue, ht.Count, ht.Keys; PASS  |

### Key Link Verification

| From                            | To                          | Via                          | Status   | Details                                                    |
|---------------------------------|-----------------------------|------------------------------|----------|------------------------------------------------------------|
| Eval.fs FieldAccess dispatch    | HashtableValue.TryGetValue  | match fieldName "TryGetValue" | WIRED   | Returns TupleValue [BoolValue true/false; ...] correctly   |
| Eval.fs FieldAccess dispatch    | HashtableValue.Count        | match fieldName "Count"       | WIRED   | Returns IntValue ht.Count                                  |
| Eval.fs FieldAccess dispatch    | HashtableValue.Keys         | match fieldName "Keys"        | WIRED   | Returns ListValue (ht.Keys as list)                        |
| Eval.fs constructor interception | MutableListValue creation  | match "MutableList", arg      | WIRED   | Both `MutableList()` and `MutableList ()` handled          |
| Eval.fs IndexGet/Set            | MutableListValue            | MutableListValue ml, IntValue i | WIRED | Bounds-checked; raises LangThreeException on out-of-bounds |
| Bidir.fs THashtable arm         | THashtable(keyTy, valTy)    | inserted BEFORE TData arm     | WIRED   | Prevents TData catch-all from shadowing Hashtable types    |
| Prelude/MutableList.fun         | mutablelist_* raw builtins  | direct delegation             | WIRED   | All 5 Prelude functions call corresponding raw builtins    |

### Requirements Coverage

| Requirement | Status    | Evidence                                                   |
|-------------|-----------|------------------------------------------------------------|
| COLL-04     | SATISFIED | MutableList: Add, Count, ml.[i], ml.[i] <- v all working; 2 flt tests PASS |
| COLL-05     | SATISFIED | Hashtable.TryGetValue, .Count, .Keys all working; hashtable-dot-api.flt PASS |
| PROP-02     | SATISFIED | .Count consistent: HashSet, Queue, MutableList, Hashtable all return IntValue |
| PROP-03     | SATISFIED | .Keys returns ListValue of keys; tested in hashtable-dot-api.flt |

### Anti-Patterns Found

None. Scanned Prelude/MutableList.fun and all 3 new flt test files — no TODO/FIXME, no placeholder text, no empty implementations.

### Human Verification Required

None. All behaviors are fully verifiable programmatically via flt tests.

### Test Execution Results

| Test Suite                          | Result   |
|-------------------------------------|----------|
| tests/flt/file/mutablelist/         | 2/2 PASS |
| tests/flt/file/hashtable/           | 11/11 PASS |
| tests/flt/ (full suite, regression) | 608/608 PASS |

### Summary

Phase 57 fully achieves its goal. Both plans completed cleanly:

- Plan 01 added MutableList as a native collection: AST DU case, Eval constructor/FieldAccess/IndexGet/IndexSet/raw-builtins, Bidir type rules, TypeCheck schemes, and Prelude module.
- Plan 02 added Hashtable dot-API (TryGetValue, Count, Keys) in Eval.fs FieldAccess dispatch and Bidir.fs THashtable type rules, plus three integration flt tests.

The full 608-test suite passes with no regressions. All four requirements (COLL-04, COLL-05, PROP-02, PROP-03) are satisfied by substantive, wired implementations.

---

_Verified: 2026-03-29T04:17:38Z_
_Verifier: Claude (gsd-verifier)_
