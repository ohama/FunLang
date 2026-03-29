# Requirements: LangThree v7.0

**Defined:** 2026-03-29
**Core Value:** FunLexYacc가 .NET interop 없이 동작하도록 LangThree에 네이티브 컬렉션 타입과 빌트인 함수를 추가

## v7.0 Requirements

### Native Collection Types

- [ ] **COLL-01**: StringBuilder 타입 — 생성(`StringBuilder()`), `.Append(str/char)`, `.ToString()` 지원
- [ ] **COLL-02**: HashSet 타입 — 생성(`HashSet()`), `.Add(value)` (bool 반환), `.Contains(value)`, `.Count` 지원
- [ ] **COLL-03**: Queue 타입 — 생성(`Queue()`), `.Enqueue(value)`, `.Dequeue()`, `.Count` 지원
- [ ] **COLL-04**: MutableList 타입 — 생성(`MutableList()`), `.Add(value)`, `.[i]` 인덱싱, `.Count` 지원
- [ ] **COLL-05**: Hashtable 확장 — `.TryGetValue(key)` (bool * value 튜플 반환), `.Count`, `.Keys` 프로퍼티 추가

### Property & Method Dispatch

- [ ] **PROP-01**: `.Length` 프로퍼티 — 문자열과 배열에서 `.Length`로 길이 접근 (`string_length`/`array_length` 대체)
- [ ] **PROP-02**: `.Count` 프로퍼티 — HashSet, Queue, MutableList, Hashtable에서 요소 수 반환
- [ ] **PROP-03**: `.Keys` 프로퍼티 — Hashtable에서 키 목록을 리스트로 반환
- [ ] **PROP-04**: 메서드 호출 구문 — `obj.Method(args)` 형태로 컬렉션 메서드 호출 (FieldAccess + App 조합)
- [ ] **PROP-05**: `.Key`/`.Value` 접근 — Hashtable for-in 반복 시 KeyValuePair에서 `.Key`, `.Value` 접근

### Language Constructs

- [ ] **LANG-01**: 문자열 슬라이싱 — `s.[start .. end]` (inclusive), `s.[start ..]` (끝까지) 구문 지원
- [ ] **LANG-02**: 리스트 컴프리헨션 — `[ for x in coll -> expr ]`, `[ for i in 0 .. n -> expr ]` 구문 지원
- [ ] **LANG-03**: 네이티브 컬렉션 for-in — HashSet, Hashtable, MutableList, Queue에서 `for x in coll do body` 반복 지원

### String & Char Methods

- [ ] **STR-01**: 문자열 메서드 — `.EndsWith(str)`, `.Trim()`, `.StartsWith(str)` 지원
- [ ] **STR-02**: Char 함수 — `Char.IsDigit(c)`, `Char.ToUpper(c)` 빌트인 함수
- [ ] **STR-03**: `String.concat` — 구분자와 리스트를 결합하는 모듈 함수 (`String.concat sep list`)
- [ ] **STR-04**: `eprintfn` — stderr 포맷 출력 빌트인

### Prelude Extensions

- [ ] **PRE-01**: List 정렬 — `List.sort`, `List.sortBy` 함수
- [ ] **PRE-02**: List 검색/필터 — `List.tryFind`, `List.choose`, `List.distinctBy`, `List.exists` (any 별칭)
- [ ] **PRE-03**: List 유틸 — `List.mapi`, `List.item` (nth 별칭), `List.isEmpty`, `List.head` (hd 별칭), `List.tail` (tl 별칭)
- [ ] **PRE-04**: List 변환 — `List.ofSeq` (컬렉션을 리스트로 변환)
- [ ] **PRE-05**: Array 확장 — `Array.sort`, `Array.ofSeq` 빌트인

## v7.1 Requirements (deferred)

### Compiler Support

- **COMP-01**: LangBackend에 네이티브 컬렉션 C runtime 구현
- **COMP-02**: LangBackend에 .Length/.Count 프로퍼티 컴파일
- **COMP-03**: LangBackend에 리스트 컴프리헨션 컴파일
- **COMP-04**: LangBackend에 문자열 슬라이싱 컴파일

## Out of Scope

| Feature | Reason |
|---------|--------|
| .NET interop 유지 | 네이티브 구현으로 대체 — LangBackend 호환이 목표 |
| Generic type parameters | `Dictionary<string, int>` 제네릭 구문 불요 — 동적 타이핑으로 충분 |
| IEnumerable/ICollection 인터페이스 | 구체적 타입별 구현으로 충분 |
| LINQ-style 쿼리 | 함수형 리스트 연산으로 충분 |
| Unchecked.defaultof | FunLexYacc 코드에서 대체 패턴 사용 가능 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| PROP-01 | Phase 54 | Complete |
| PROP-04 | Phase 54 | Complete |
| COLL-01 | Phase 55 | Complete |
| STR-01 | Phase 55 | Complete |
| STR-02 | Phase 55 | Complete |
| STR-03 | Phase 55 | Complete |
| STR-04 | Phase 55 | Complete |
| COLL-02 | Phase 56 | Complete |
| COLL-03 | Phase 56 | Complete |
| COLL-04 | Phase 57 | Complete |
| COLL-05 | Phase 57 | Complete |
| PROP-02 | Phase 57 | Complete |
| PROP-03 | Phase 57 | Complete |
| LANG-01 | Phase 58 | Complete |
| LANG-02 | Phase 58 | Complete |
| LANG-03 | Phase 58 | Complete |
| PROP-05 | Phase 58 | Complete |
| PRE-01 | Phase 59 | Pending |
| PRE-02 | Phase 59 | Pending |
| PRE-03 | Phase 59 | Pending |
| PRE-04 | Phase 59 | Pending |
| PRE-05 | Phase 59 | Pending |

**Coverage:**
- v7.0 requirements: 22 total
- Mapped to phases: 22
- Unmapped: 0

---
*Requirements defined: 2026-03-29*
*Last updated: 2026-03-29 after roadmap creation (traceability complete)*
