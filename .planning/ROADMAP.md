# Roadmap: LangThree

## Milestones

- ✅ **v1.0 Core Language** - Phases 1-7 (shipped 2026-03-10)
- ✅ **v1.1 Test Infrastructure** - Phases 8-9 (shipped 2026-03-10)
- ✅ **v1.2 Practical Features** - Phases 10-12 (shipped 2026-03-18)
- ✅ **v1.3 Tutorial Docs** - Phases 13-14 (shipped 2026-03-19)
- ✅ **v1.4 Language Completion** - Phases 15-18 (shipped 2026-03-20)
- ✅ **v1.5 User-Defined Operators** - Phases 19-22 (shipped 2026-03-20)
- ✅ **v1.6/v1.7 Offside Rule & List Syntax** - Phases 23-24 (shipped 2026-03-22)
- ✅ **v1.8 Polymorphic GADT** - Phase 25 (shipped 2026-03-23)
- ✅ **v2.0 Practical Language Completion** - Phases 26-32 (shipped 2026-03-25)
- ✅ **v2.1 Bug Fixes & Test Hardening** - Phases 33-35 (shipped 2026-03-25)
- ✅ **v2.2 Module Access Fix & Test Coverage** - Phases 36-37 (shipped 2026-03-25)
- 🚧 **v3.0 Mutable Data Structures** - Phases 38-41 (in progress)

## Phases

<details>
<summary>✅ v1.0–v2.2 (Phases 1–37) - SHIPPED 2026-03-25</summary>

Phases 1–37 covered: Core language (ADT/GADT/Records/Modules/Exceptions), test infrastructure, practical features (pipes, builtins, printf, Prelude), tutorial docs, TCO, or-patterns, type aliases, user-defined operators, F# offside rule, polymorphic GADT, file imports, Char type, N-tuples, file I/O, bug fixes, and module access fix. All 92 plans complete.

</details>

### 🚧 v3.0 Mutable Data Structures (In Progress)

**Milestone Goal:** Array와 Hashtable 등 변경 가능한 자료구조를 추가하여 성능이 필요한 알고리즘 지원

#### Phase 38: Array Type
**Goal**: Users can create and manipulate fixed-size mutable arrays in LangThree programs
**Depends on**: Phase 37 (v2.2 complete baseline)
**Requirements**: ARR-01, ARR-02, ARR-03, ARR-04, ARR-05, ARR-06
**Success Criteria** (what must be TRUE):
  1. `Array.create 5 0` produces an array of 5 zeros; indexing with `Array.get` returns the correct element
  2. `Array.set arr 2 99` mutates the array in place so `Array.get arr 2` returns `99`
  3. `Array.length arr` returns the exact number of elements in the array
  4. `Array.ofList [1; 2; 3]` and `Array.toList arr` round-trip without data loss
  5. Out-of-bounds access via `Array.get` or `Array.set` raises a runtime exception
**Plans**: 2 plans

Plans:
- [ ] 38-01-PLAN.md — Add ArrayValue DU case to Ast.fs and TArray to Type.fs; wire all propagation sites (apply, freeVars, formatType, Unify, Bidir); add formatValue arm in Eval.fs
- [ ] 38-02-PLAN.md — Register six array_* builtins in Eval.fs and TypeCheck.fs; create Prelude/Array.fun module wrapper; add flt integration tests

#### Phase 39: Hashtable Type
**Goal**: Users can create and manipulate mutable key-value hashtables in LangThree programs
**Depends on**: Phase 37 (v2.2 complete baseline — can run parallel with Phase 38)
**Requirements**: HT-01, HT-02, HT-03, HT-04, HT-05, HT-06
**Success Criteria** (what must be TRUE):
  1. `Hashtable.create ()` produces an empty hashtable that accepts subsequent operations
  2. `Hashtable.set ht "key" 42` followed by `Hashtable.get ht "key"` returns `42`
  3. `Hashtable.containsKey ht "key"` returns `true` when the key exists, `false` otherwise
  4. `Hashtable.keys ht` returns all keys currently in the table as a list
  5. `Hashtable.remove ht "key"` removes the entry so `containsKey` returns `false` afterwards
**Plans**: TBD

Plans:
- [ ] 39-01: Add HashtableValue DU case to Ast.fs / Value types; implement Hashtable.create, get, set, containsKey, keys, remove builtins in Eval.fs; register TypeCheck entries

#### Phase 40: Array Higher-Order Functions
**Goal**: Users can apply functional patterns (iter, map, fold, init) to arrays without manual indexing
**Depends on**: Phase 38
**Requirements**: ARR-07, ARR-08, ARR-09, ARR-10
**Success Criteria** (what must be TRUE):
  1. `Array.iter (fun x -> println (to_string x)) arr` calls the function once per element in order
  2. `Array.map (fun x -> x * 2) arr` returns a new array with each element doubled
  3. `Array.fold (fun acc x -> acc + x) 0 arr` reduces the array to a single accumulated value
  4. `Array.init 5 (fun i -> i * i)` creates `[|0; 1; 4; 9; 16|]` without an explicit create+set loop
**Plans**: TBD

Plans:
- [ ] 40-01: Implement Array.iter, Array.map, Array.fold, Array.init builtins in Eval.fs; register TypeCheck entries; verify 0 compiler warnings

#### Phase 41: Tests and Documentation
**Goal**: All new mutable data structure features are verified by flt tests and documented in the tutorial
**Depends on**: Phases 38, 39, 40
**Requirements**: TST-18, TST-19, TST-20, TST-21, TST-22, TST-23
**Success Criteria** (what must be TRUE):
  1. flt tests for Array basic operations (create/get/set/length) all pass in CI
  2. flt tests for Array conversions (ofList/toList) and higher-order functions (iter/map/fold/init) all pass in CI
  3. flt tests for Hashtable operations (create/get/set/containsKey/keys/remove) all pass in CI
  4. A tutorial chapter covering both Array and Hashtable with runnable examples exists and all examples are verified
**Plans**: TBD

Plans:
- [ ] 41-01: Write flt tests for Array (TST-18, TST-19) and Hashtable basic + extra operations (TST-21, TST-22)
- [ ] 41-02: Write flt tests for Array higher-order functions (TST-20); write tutorial chapter for Array + Hashtable (TST-23)

## Progress

**Execution Order:** 38 → 39 (parallel with 38) → 40 → 41

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1–37. Previous milestones | v1.0–v2.2 | 92/92 | Complete | 2026-03-25 |
| 38. Array Type | v3.0 | 0/2 | Not started | - |
| 39. Hashtable Type | v3.0 | 0/1 | Not started | - |
| 40. Array Higher-Order Functions | v3.0 | 0/1 | Not started | - |
| 41. Tests and Documentation | v3.0 | 0/2 | Not started | - |
