# Requirements: LangThree v10.0

**Defined:** 2026-03-31
**Core Value:** Haskell 스타일 타입 클래스로 ad-hoc polymorphism 지원

## v10.0 Requirements

### 타입 인프라

- [ ] **TC-01**: Scheme 확장 — `Scheme(vars, constraints, ty)`로 제약 조건 지원, 기존 코드 하위 호환
- [ ] **TC-02**: ClassEnv/InstanceEnv 타입 — 타입 클래스 정보와 인스턴스 정보를 저장하는 환경 타입

### 파싱/AST

- [ ] **TC-03**: `typeclass` 선언 파싱 — `typeclass Show 'a = show : 'a -> string` 구문
- [ ] **TC-04**: `instance` 선언 파싱 — `instance Show int = let show x = to_string x` 구문
- [ ] **TC-05**: 제약 타입 구문 — `Show 'a =>` 제약 조건 어노테이션

### 타입 체커

- [ ] **TC-06**: 제약 전파 — Bidir.fs synth/check에서 타입 클래스 제약 수집 및 전파
- [ ] **TC-07**: 인스턴스 해결 — 제약 조건에 맞는 인스턴스 검색 (단일 인스턴스 강제, 중복 금지)
- [ ] **TC-08**: generalize/instantiate 확장 — let-generalization 시 제약 보존, instantiate 시 제약 해결

### 딕셔너리 전달

- [ ] **TC-09**: 딕셔너리 생성 — instance 선언에서 RecordValue 딕셔너리 생성
- [ ] **TC-10**: 딕셔너리 삽입 — 제약된 함수 호출 시 elaboration으로 딕셔너리 인자 자동 삽입

### 내장 인스턴스

- [ ] **TC-11**: Show 인스턴스 — int, bool, string, char, list, option 타입의 Show 인스턴스
- [ ] **TC-12**: Eq 인스턴스 — 기본 타입의 Eq 인스턴스 (= 연산자 타입 클래스화)
- [ ] **TC-13**: flt 테스트 — 타입 클래스 선언, 인스턴스, 제약 함수, 내장 인스턴스 통합 테스트

## Out of Scope

| Feature | Reason |
|---------|--------|
| 중복 인스턴스 (overlapping instances) | 구현 복잡도 HIGH, 일관성(coherence) 보장 어려움 |
| 다중 파라미터 타입 클래스 | GHC 확장, MVP에 불필요 |
| Higher-Kinded Types (Functor/Monad) | 타입 시스템 근본 변경 필요, v11+ |
| Num 타입 클래스 (+, -, * 마이그레이션) | 기존 코드 대량 파괴, Show/Eq 후 별도 검토 |
| derive 자동 유도 | 구현 복잡도, 수동 인스턴스로 충분 |
| 슈퍼클래스 제약 | v10.1로 연기 |
| 제약 조건부 인스턴스 (Show 'a => Show (Option 'a)) | v10.1로 연기 |
| 모듈 스코프 인스턴스 | Haskell 스타일 전역 인스턴스로 시작 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| TC-01 | Phase 70 | Pending |
| TC-02 | Phase 70 | Pending |
| TC-03 | Phase 71 | Pending |
| TC-04 | Phase 71 | Pending |
| TC-05 | Phase 71 | Pending |
| TC-06 | Phase 72 | Pending |
| TC-07 | Phase 72 | Pending |
| TC-08 | Phase 72 | Pending |
| TC-09 | Phase 73 | Pending |
| TC-10 | Phase 73 | Pending |
| TC-11 | Phase 74 | Pending |
| TC-12 | Phase 74 | Pending |
| TC-13 | Phase 74 | Pending |

**Coverage:**
- v10.0 requirements: 13 total
- Mapped to phases: 13
- Unmapped: 0

---
*Requirements defined: 2026-03-31*
*Traceability updated: 2026-03-31 (roadmap created)*
