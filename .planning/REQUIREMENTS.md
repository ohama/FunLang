# Requirements: LangThree v3.0

**Defined:** 2026-03-25
**Core Value:** 변경 가능한 자료구조로 성능이 필요한 알고리즘 지원

## v3.0 Requirements

### Mutable Array

- [ ] **ARR-01**: `Array.create n default` — 크기 n의 배열 생성 (기본값으로 초기화)
- [ ] **ARR-02**: `Array.get arr i` — 인덱스 i의 요소 반환 (범위 초과 시 예외)
- [ ] **ARR-03**: `Array.set arr i v` — 인덱스 i에 값 v를 설정 (in-place mutation)
- [ ] **ARR-04**: `Array.length arr` — 배열의 길이 반환
- [ ] **ARR-05**: `Array.ofList xs` — 리스트를 배열로 변환
- [ ] **ARR-06**: `Array.toList arr` — 배열을 리스트로 변환

### Mutable Hashtable

- [ ] **HT-01**: `Hashtable.create ()` — 빈 해시테이블 생성
- [ ] **HT-02**: `Hashtable.get ht key` — 키로 값 조회 (없으면 예외)
- [ ] **HT-03**: `Hashtable.set ht key value` — 키에 값 설정 (in-place mutation)
- [ ] **HT-04**: `Hashtable.containsKey ht key` — 키 존재 여부 확인
- [ ] **HT-05**: `Hashtable.keys ht` — 모든 키를 리스트로 반환
- [ ] **HT-06**: `Hashtable.remove ht key` — 키-값 쌍 제거

### Array 고차 함수

- [ ] **ARR-07**: `Array.iter f arr` — 배열의 각 요소에 함수 적용 (부수효과용)
- [ ] **ARR-08**: `Array.map f arr` — 배열의 각 요소에 함수 적용하여 새 배열 반환
- [ ] **ARR-09**: `Array.fold f init arr` — 배열을 하나의 값으로 축약
- [ ] **ARR-10**: `Array.init n f` — f(0), f(1), ..., f(n-1) 으로 배열 생성

### Test Coverage

- [ ] **TST-18**: Array 기본 연산 flt 테스트 (create, get, set, length)
- [ ] **TST-19**: Array 변환 flt 테스트 (ofList, toList)
- [ ] **TST-20**: Array 고차 함수 flt 테스트 (iter, map, fold, init)
- [ ] **TST-21**: Hashtable 기본 연산 flt 테스트 (create, get, set, containsKey)
- [ ] **TST-22**: Hashtable 추가 연산 flt 테스트 (keys, remove)
- [ ] **TST-23**: 튜토리얼 챕터 작성 (Array + Hashtable)

## Out of Scope

| Feature | Reason |
|---------|--------|
| for/while 루프 | 함수형 스타일 유지 — iter/map/fold로 대체 |
| Resizable array (ArrayList) | 고정 크기 배열 우선 — 필요하면 추후 |
| Hashtable iteration order | 순서 보장 안 함 — keys로 리스트 변환 후 처리 |
| 네이티브 컴파일 | 별도 마일스톤 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| ARR-01 | Phase 38 | Complete |
| ARR-02 | Phase 38 | Complete |
| ARR-03 | Phase 38 | Complete |
| ARR-04 | Phase 38 | Complete |
| ARR-05 | Phase 38 | Complete |
| ARR-06 | Phase 38 | Complete |
| HT-01 | Phase 39 | Complete |
| HT-02 | Phase 39 | Complete |
| HT-03 | Phase 39 | Complete |
| HT-04 | Phase 39 | Complete |
| HT-05 | Phase 39 | Complete |
| HT-06 | Phase 39 | Complete |
| ARR-07 | Phase 40 | Complete |
| ARR-08 | Phase 40 | Complete |
| ARR-09 | Phase 40 | Complete |
| ARR-10 | Phase 40 | Complete |
| TST-18 | Phase 41 | Complete |
| TST-19 | Phase 41 | Complete |
| TST-20 | Phase 41 | Complete |
| TST-21 | Phase 41 | Complete |
| TST-22 | Phase 41 | Complete |
| TST-23 | Phase 41 | Complete |

**Coverage:**
- v3.0 requirements: 22 total
- Mapped to phases: 22
- Unmapped: 0

---
*Requirements defined: 2026-03-25*
*Traceability updated: 2026-03-25 (v3.0 roadmap)*
