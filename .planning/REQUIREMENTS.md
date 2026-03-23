# Requirements: LangThree v1.8

**Defined:** 2026-03-23
**Core Value:** OCaml 스타일 다형적 GADT 반환 타입 지원

## v1.8 Requirements

### Type System (TYP)

- [ ] **TYP-01**: GADT match에서 타입 변수 주석 허용 — `(match e with ... : 'a)` 구문
- [ ] **TYP-02**: synth 모드에서 GADT match를 fresh type variable로 check 위임 (E0401 에러 대신)
- [ ] **TYP-03**: 분기별 독립적 타입 정제 — IntLit 분기에서 `'a=int`, BoolLit 분기에서 `'a=bool`
- [ ] **TYP-04**: 기존 구체적 주석 `(match ... : int)` 호환성 유지

### Coverage (COV)

- [ ] **COV-01**: `eval : 'a Expr -> 'a` 패턴 동작 — 정수 표현식 입력 시 int, 불리언 입력 시 bool 반환
- [ ] **COV-02**: 재귀 GADT 평가기 동작 — `Add (IntLit 10, IntLit 20)` → `30`
- [ ] **COV-03**: 기존 GADT 테스트 전부 통과 (하위 호환)
- [ ] **COV-04**: 튜토리얼 Ch14 업데이트

## Out of Scope (v1.8)

| Feature | Reason |
|---------|--------|
| 함수 레벨 타입 시그니처 (`let eval : 'a Expr -> 'a = ...`) | 파서 변경 최소화, match 주석으로 충분 |
| 다형적 재귀 (`let rec eval : type a. ...`) | OCaml의 고급 기능, 향후 마일스톤 |
| GADT 패턴에서 existential type 추론 개선 | 현재 수준 유지 |

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| TYP-01 | - | Pending |
| TYP-02 | - | Pending |
| TYP-03 | - | Pending |
| TYP-04 | - | Pending |
| COV-01 | - | Pending |
| COV-02 | - | Pending |
| COV-03 | - | Pending |
| COV-04 | - | Pending |

**Coverage:**
- v1.8 requirements: 8 total
- Mapped to phases: 0
- Unmapped: 8

---
*Requirements defined: 2026-03-23*
