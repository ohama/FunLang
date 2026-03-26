# Requirements: LangThree v4.0

**Defined:** 2026-03-26
**Core Value:** `let mut` 가변 변수로 명령형 스타일 프로그래밍 지원

## v4.0 Requirements

### Mutable Variable Declaration

- [ ] **MUT-01**: `let mut x = expr` — expression-level 가변 변수 선언 (implicit in 지원)
- [ ] **MUT-02**: `let mut x = expr` — module-level 가변 변수 선언
- [ ] **MUT-03**: `x <- expr` — 가변 변수 재할당 (unit 반환)
- [ ] **MUT-04**: 불변 변수에 `<-` 사용 시 타입 에러 발생

### Type System

- [ ] **MUT-05**: Type checker가 변수의 mutability를 추적 (mutable/immutable 구분)
- [ ] **MUT-06**: 가변 변수의 재할당 시 동일 타입 강제 (type mismatch 에러)

### Edge Cases

- [ ] **MUT-07**: 클로저가 외부 `let mut` 변수를 캡처하여 읽기/쓰기 가능
- [ ] **MUT-08**: 함수 파라미터는 항상 불변 (`<-` 불가)

### Test Coverage

- [ ] **TST-24**: `let mut` 기본 연산 flt 테스트 (선언, 재할당, 읽기)
- [ ] **TST-25**: `let mut` 에러 케이스 flt 테스트 (불변 변수 재할당, 타입 mismatch)
- [ ] **TST-26**: `let mut` 고급 시나리오 flt 테스트 (클로저 캡처, 중첩, offside rule)
- [ ] **TST-27**: 튜토리얼 챕터 작성 (mutable variables)

## Out of Scope

| Feature | Reason |
|---------|--------|
| `ref` 타입 (OCaml style) | `let mut`이 더 직관적, F# 스타일 유지 |
| `mutable` function parameter | F#에서도 미지원, 복잡도 높음 |
| `let mut` pattern destructuring | `let mut (x, y) = ...` 은 의미가 불명확 |
| for/while 루프 | 별도 마일스톤 — iter/map/fold로 대체 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| MUT-01 | — | Pending |
| MUT-02 | — | Pending |
| MUT-03 | — | Pending |
| MUT-04 | — | Pending |
| MUT-05 | — | Pending |
| MUT-06 | — | Pending |
| MUT-07 | — | Pending |
| MUT-08 | — | Pending |
| TST-24 | — | Pending |
| TST-25 | — | Pending |
| TST-26 | — | Pending |
| TST-27 | — | Pending |

**Coverage:**
- v4.0 requirements: 12 total
- Mapped to phases: 0
- Unmapped: 12

---
*Requirements defined: 2026-03-26*
