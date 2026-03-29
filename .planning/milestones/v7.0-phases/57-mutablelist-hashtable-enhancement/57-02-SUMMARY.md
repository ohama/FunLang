---
phase: 57-mutablelist-hashtable-enhancement
plan: "02"
subsystem: native-collections
tags: [hashtable, mutablelist, fieldaccess, bidir, eval, flt-tests]

dependency-graph:
  requires: [57-01]
  provides: [HashtableValue FieldAccess dispatch, THashtable type rules, MutableList flt tests, Hashtable dot API flt tests]
  affects: []

tech-stack:
  added: []
  patterns: [fieldaccess-dispatch, thashtable-type-rule, flt-integration-tests]

key-files:
  created:
    - tests/flt/file/mutablelist/mutablelist-basic.flt
    - tests/flt/file/mutablelist/mutablelist-indexing.flt
    - tests/flt/file/hashtable/hashtable-dot-api.flt
  modified:
    - src/LangThree/Eval.fs
    - src/LangThree/Bidir.fs

decisions:
  - id: thashtable-not-tdata
    choice: "Used THashtable(keyTy, valTy) NOT TData(\"Hashtable\",[]) in Bidir.fs"
    reason: "Hashtable values have type THashtable(k,v) in the type system — a dedicated parameterized type constructor. TData would never match."

metrics:
  duration: ~5 minutes
  completed: 2026-03-29
---

# Phase 57 Plan 02: Hashtable FieldAccess & Integration Tests Summary

**One-liner:** HashtableValue FieldAccess dispatch (.TryGetValue returns 2-tuple, .Count, .Keys) in Eval.fs; THashtable(keyTy,valTy) type rules in Bidir.fs; three flt tests covering MutableList and Hashtable dot API.

## What Was Built

1. **Eval.fs** — `HashtableValue` arm in FieldAccess dispatch (inserted BEFORE `RecordValue`):
   - `.TryGetValue(key)` → `TupleValue [BoolValue true; v]` for found, `TupleValue [BoolValue false; TupleValue []]` for missing
   - `.Count` → `IntValue ht.Count` (property)
   - `.Keys` → `ListValue (ht.Keys |> Seq.toList)`

2. **Bidir.fs** — `THashtable (keyTy, valTy)` arm in FieldAccess (inserted BEFORE `TData (typeName, typeArgs)`):
   - `.TryGetValue` → `TArrow(keyTy, TTuple [TBool; valTy])`
   - `.Count` → `TInt`
   - `.Keys` → `TList keyTy`

3. **Three flt test files** (all 3 pass, 608/608 total suite passing):
   - `mutablelist-basic.flt` — Add, Count, indexing (COLL-04)
   - `mutablelist-indexing.flt` — index read and write via `.[i]` (COLL-04)
   - `hashtable-dot-api.flt` — TryGetValue, Count, Keys (COLL-05, PROP-02, PROP-03)

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add HashtableValue FieldAccess in Eval.fs and THashtable in Bidir.fs | 404f35b | Eval.fs, Bidir.fs |
| 2 | Write flt integration tests for MutableList and Hashtable dot API | 65b3f26 | 3 new flt files |

## Verification

All verification criteria met:

1. Build: zero errors, zero warnings
2. New tests: 3/3 pass (mutablelist-basic, mutablelist-indexing, hashtable-dot-api)
3. Existing hashtable tests: 11/11 pass
4. Full test suite: 608/608 pass (no regressions)
5. .Count consistency: HashSet(1), Queue(1), MutableList(1), Hashtable(1) all return `1` correctly

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Bidir match arm type | `THashtable(keyTy, valTy)` | Hashtable values use dedicated parameterized type in type system; `TData` would never match |
| HashtableValue arm placement | BEFORE RecordValue | Value-type dispatch must precede the generic RecordValue catch-all |

## Deviations from Plan

None — plan executed exactly as written.

## Next Phase Readiness

- Phase 57 complete — both plans done
- All COLL-04, COLL-05, PROP-02, PROP-03 requirements satisfied
- No blockers
