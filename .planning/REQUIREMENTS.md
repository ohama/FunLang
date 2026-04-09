# Requirements: v15.0 Type Class Maturity

**Defined:** 2026-04-09
**Core Value:** 타입 클래스 시스템 성숙 — 제약 인스턴스, 슈퍼클래스, Num/Eq, derive, 에러 메시지

## v15.0 Requirements

### Correctness Foundations (TC)

- [ ] **TC-01**: 중복 인스턴스 감지 — alpha-equivalent 인스턴스를 TVar 정규화로 정확히 감지
- [ ] **TC-02**: 제약 누출 방지 — pendingConstraints가 let 경계를 넘어 누출되지 않음
- [ ] **TC-03**: 재귀 인스턴스 메서드 — 제약 조건부 인스턴스 body에서 같은 클래스 메서드 호출 가능
- [ ] **TC-04**: E0701 에러에 formatTypeNormalized 적용 — 내부 TVar 대신 정규화된 이름 표시

### Superclass Entailment (SC)

- [ ] **SC-01**: ClassInfo에 Superclasses 필드 추가, resolveConstraint에서 슈퍼클래스 체인 탐색
- [ ] **SC-02**: InstanceDecl 등록 시 슈퍼클래스 인스턴스 존재 검증 (선언 시점 에러)
- [ ] **SC-03**: Ord 타입 클래스를 Prelude에 추가 (Eq 'a => Ord 'a)

### Constrained Instance Runtime Dispatch (CD)

- [ ] **CD-01**: Elaborate.fs에서 제약 조건부 인스턴스 메서드 이름 맹글링
- [ ] **CD-02**: 호출 지점에서 타입 기반 올바른 인스턴스 메서드 디스패치

### Num Type Class (NUM)

- [ ] **NUM-01**: Num 타입 클래스를 Prelude에 추가 (add, sub, mul, negate 메서드)
- [ ] **NUM-02**: instance Num int 추가
- [ ] **NUM-03**: Eq ('a list) 인스턴스 추가

### Derive for Parameterized ADTs (DRV)

- [ ] **DRV-01**: deriving Show/Eq가 파라미터화된 ADT에서 올바른 InstanceVars/InstanceConstraints 생성
- [ ] **DRV-02**: 재귀적 ADT (Tree 'a 등)에 대한 derive 지원

### Error Message Polish (ERR)

- [ ] **ERR-01**: E0701 NoInstance 에러에서 원소 타입 힌트 제공
- [ ] **ERR-02**: E0704가 실제로 발생하도록 수정
- [ ] **ERR-03**: 제약 체인 컨텍스트 표시 (어떤 함수 호출이 이 제약을 요구했는지)

## Out of Scope

| Feature | Reason |
|---------|--------|
| `+`/`-`/`*` 연산자 타입 클래스 디스패치 | 724 테스트 전부 위험, 별도 milestone |
| `=`/`<>` 연산자 Eq 디스패치 | 동일 사유, 별도 milestone |
| First-class 딕셔너리 (레코드 기반) | 현재 name-based dispatch로 충분 |
| Multi-parameter type classes | 복잡도 대비 필요성 낮음 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| TC-01 | TBD | Pending |
| TC-02 | TBD | Pending |
| TC-03 | TBD | Pending |
| TC-04 | TBD | Pending |
| SC-01 | TBD | Pending |
| SC-02 | TBD | Pending |
| SC-03 | TBD | Pending |
| CD-01 | TBD | Pending |
| CD-02 | TBD | Pending |
| NUM-01 | TBD | Pending |
| NUM-02 | TBD | Pending |
| NUM-03 | TBD | Pending |
| DRV-01 | TBD | Pending |
| DRV-02 | TBD | Pending |
| ERR-01 | TBD | Pending |
| ERR-02 | TBD | Pending |
| ERR-03 | TBD | Pending |

**Coverage:**
- v15.0 requirements: 17 total
- Mapped to phases: 0 (awaiting roadmap)
- Unmapped: 17

---
*Requirements defined: 2026-04-09*
