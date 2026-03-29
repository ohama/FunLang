# Roadmap: LangThree v7.0 Native Collections & Built-in Library

FunLexYacc가 .NET interop 없이 동작하도록 네이티브 컬렉션 타입(StringBuilder, HashSet, Queue, MutableList)과 프로퍼티/메서드 디스패치, 문자열 유틸리티, 리스트 컴프리헨션, Prelude 확장을 추가한다. 6개 phase로 기존 인터프리터 위에 점진적으로 구축하며, Phase 54의 디스패치 인프라가 이후 모든 컬렉션 phase의 기반이 된다.

## Phases

### Phase 54: Property & Method Dispatch

**Goal:** 사용자가 `obj.Property`와 `obj.Method(args)` 구문으로 값의 프로퍼티와 메서드에 접근할 수 있다

**Dependencies:** None (builds on existing FieldAccess AST node)

**Requirements:** PROP-01, PROP-04

**Success Criteria:**
1. `"hello".Length` evaluates to `5` and `arr.Length` returns the array size
2. `obj.Method(arg)` syntax dispatches to the correct built-in method based on value type
3. Existing FieldAccess for records and modules continues to work unchanged
4. flt tests verify property access and method dispatch on strings and arrays

**Plans:** 1 plan
Plans:
- [x] 54-01-PLAN.md — Add .Length dispatch to Bidir.fs and Eval.fs, write flt tests

---

### Phase 55: StringBuilder & String Utilities

**Goal:** 사용자가 StringBuilder로 효율적인 문자열 조합을 하고, 문자열/문자 유틸리티 메서드를 사용할 수 있다

**Dependencies:** Phase 54 (method dispatch for .Append, .ToString, .EndsWith, .Trim etc.)

**Requirements:** COLL-01, STR-01, STR-02, STR-03, STR-04

**Success Criteria:**
1. `StringBuilder()` creates a builder, `.Append("text")` chains appends, `.ToString()` produces the final string
2. `"hello.txt".EndsWith(".txt")` returns `true`, `" hi ".Trim()` returns `"hi"`, `"hello".StartsWith("he")` returns `true`
3. `Char.IsDigit('3')` returns `true`, `Char.ToUpper('a')` returns `'A'`
4. `String.concat ", " ["a"; "b"; "c"]` returns `"a, b, c"`
5. `eprintfn "error: %s" msg` prints formatted output to stderr

**Plans:** 3 plans
Plans:
- [ ] 55-01-PLAN.md — Add string methods (EndsWith/StartsWith/Trim) and eprintfn builtin
- [ ] 55-02-PLAN.md — Add Char and String.concat modules via Prelude files and builtins
- [ ] 55-03-PLAN.md — Add StringBuilder type (new Value DU case, constructor interception, dispatch, Prelude module)

---

### Phase 56: HashSet & Queue

**Goal:** 사용자가 HashSet으로 고유 요소 집합을, Queue로 FIFO 큐를 네이티브로 사용할 수 있다

**Dependencies:** Phase 54 (method dispatch for .Add, .Contains, .Enqueue, .Dequeue)

**Requirements:** COLL-02, COLL-03

**Success Criteria:**
1. `HashSet()` creates a set, `.Add(v)` returns `true`/`false` for new/existing, `.Contains(v)` checks membership, `.Count` returns size
2. `Queue()` creates a queue, `.Enqueue(v)` adds to back, `.Dequeue()` removes from front, `.Count` returns size
3. flt tests verify both types with integers, strings, and edge cases (empty dequeue error)

---

### Phase 57: MutableList & Hashtable Enhancement

**Goal:** 사용자가 MutableList로 가변 크기 리스트를 사용하고, Hashtable의 확장된 API(.TryGetValue, .Count, .Keys)를 활용할 수 있다

**Dependencies:** Phase 54 (method dispatch), existing HashtableValue

**Requirements:** COLL-04, COLL-05, PROP-02, PROP-03

**Success Criteria:**
1. `MutableList()` creates a list, `.Add(v)` appends, `ml.[i]` accesses by index, `.Count` returns size
2. `ht.TryGetValue(key)` returns `(true, value)` or `(false, ...)` tuple, `ht.Count` returns entry count, `ht.Keys` returns key list
3. `.Count` property works consistently across HashSet, Queue, MutableList, and Hashtable
4. flt tests verify MutableList indexing, Hashtable TryGetValue with existing/missing keys

---

### Phase 58: Language Constructs

**Goal:** 사용자가 문자열 슬라이싱, 리스트 컴프리헨션, 네이티브 컬렉션 for-in으로 간결한 코드를 작성할 수 있다

**Dependencies:** Phase 54 (dispatch for .Key/.Value), Phases 55-57 (collection types for for-in)

**Requirements:** LANG-01, LANG-02, LANG-03, PROP-05

**Success Criteria:**
1. `s.[1..3]` returns substring from index 1 to 3 inclusive, `s.[2..]` returns from index 2 to end
2. `[for x in [1;2;3] -> x * 2]` produces `[2; 4; 6]` and `[for i in 0..4 -> i * i]` produces `[0; 1; 4; 9; 16]`
3. `for x in hashSet do ...`, `for x in queue do ...`, `for x in mutableList do ...` iterate over native collections
4. `for kv in hashtable do kv.Key ... kv.Value` iterates over key-value pairs with `.Key`/`.Value` access
5. flt tests verify all three constructs including edge cases (empty collections, range slicing boundaries)

---

### Phase 59: Prelude Extensions

**Goal:** 사용자가 List/Array 표준 라이브러리 함수로 정렬, 검색, 변환 등 일반적인 컬렉션 연산을 수행할 수 있다

**Dependencies:** Phase 54 (needed for Array builtins), Phases 55-57 (collection types for ofSeq)

**Requirements:** PRE-01, PRE-02, PRE-03, PRE-04, PRE-05

**Success Criteria:**
1. `List.sort [3;1;2]` returns `[1;2;3]` and `List.sortBy (fun x -> -x) [1;2;3]` returns `[3;2;1]`
2. `List.tryFind`, `List.choose`, `List.distinctBy`, `List.exists` work correctly with predicate functions
3. `List.mapi`, `List.item`, `List.isEmpty`, `List.head`, `List.tail` provide standard list operations
4. `List.ofSeq` converts native collections (HashSet, Queue, MutableList) to immutable lists
5. `Array.sort` sorts arrays in-place and `Array.ofSeq` creates arrays from collections

## Progress

| Phase | Name | Status | Plans |
|-------|------|--------|-------|
| 54 | Property & Method Dispatch | ✓ Complete | 1/1 |
| 55 | StringBuilder & String Utilities | Planned | 0/3 |
| 56 | HashSet & Queue | Not Started | — |
| 57 | MutableList & Hashtable Enhancement | Not Started | — |
| 58 | Language Constructs | Not Started | — |
| 59 | Prelude Extensions | Not Started | — |

## Coverage

| Requirement | Phase | Category |
|-------------|-------|----------|
| PROP-01 | 54 | Property & Method Dispatch |
| PROP-04 | 54 | Property & Method Dispatch |
| COLL-01 | 55 | StringBuilder & String Utilities |
| STR-01 | 55 | StringBuilder & String Utilities |
| STR-02 | 55 | StringBuilder & String Utilities |
| STR-03 | 55 | StringBuilder & String Utilities |
| STR-04 | 55 | StringBuilder & String Utilities |
| COLL-02 | 56 | HashSet & Queue |
| COLL-03 | 56 | HashSet & Queue |
| COLL-04 | 57 | MutableList & Hashtable Enhancement |
| COLL-05 | 57 | MutableList & Hashtable Enhancement |
| PROP-02 | 57 | MutableList & Hashtable Enhancement |
| PROP-03 | 57 | MutableList & Hashtable Enhancement |
| LANG-01 | 58 | Language Constructs |
| LANG-02 | 58 | Language Constructs |
| LANG-03 | 58 | Language Constructs |
| PROP-05 | 58 | Language Constructs |
| PRE-01 | 59 | Prelude Extensions |
| PRE-02 | 59 | Prelude Extensions |
| PRE-03 | 59 | Prelude Extensions |
| PRE-04 | 59 | Prelude Extensions |
| PRE-05 | 59 | Prelude Extensions |

**Total:** 22/22 requirements mapped

---
*Roadmap created: 2026-03-29*
*Milestone: v7.0 Native Collections & Built-in Library*
