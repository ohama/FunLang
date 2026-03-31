# Requirements: LangThree v8.1

**Defined:** 2026-03-31
**Core Value:** v8.0 타입 어노테이션을 `let rec ... and ...`에 완전 적용 + expression-level 상호 재귀 구현

## v8.1 Requirements

### LetRecDecl AST Refactoring

- [ ] **AST-01**: `LetRecDecl` 바인딩 튜플에 첫 파라미터 타입 정보 보존 — `(string * string * Expr * Span)` → 타입 정보 포함 형태로 확장
- [ ] **AST-02**: `LetRecDecl` AST 변경에 따른 TypeCheck.fs, Eval.fs, Format.fs, Exhaustive.fs 등 모든 패턴 매치 사이트 업데이트
- [ ] **AST-03**: `let rec f (x : int) y = ... and g (z : bool) = ...` 에서 첫 파라미터 타입 어노테이션이 타입 체커에 전달되어 실제 검증됨

### Expression-Level Mutual Recursion

- [ ] **EXPR-01**: Expression-level `let rec ... and ... in expr` AST 노드 추가
- [ ] **EXPR-02**: Parser에 expression-level `let rec f x = ... and g y = ... in expr` 문법 규칙 추가
- [ ] **EXPR-03**: Bidir.fs에서 expression-level mutual recursion 타입 체킹 — 모든 바인딩을 동시에 환경에 추가 후 각 바디 체크
- [ ] **EXPR-04**: Eval.fs에서 expression-level mutual recursion 평가 — 상호 재귀 클로저 환경 연결
- [ ] **EXPR-05**: Expression-level mutual recursion에서 MixedParamList + 반환 타입 어노테이션 지원
- [ ] **EXPR-06**: flt 테스트 — expression-level mutual recursion 기본, 타입 어노테이션 조합, 3개 이상 바인딩

## Out of Scope

| Feature | Reason |
|---------|--------|
| Expression-level `let rec (op) ... and ...` | 사용자 정의 연산자 상호 재귀는 극히 드문 패턴 |
| Module-level LetRecDecl에 `and` 바인딩별 혼합 스타일 | 모든 바인딩이 동일한 어노테이션 규칙 따름 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| AST-01 | — | Pending |
| AST-02 | — | Pending |
| AST-03 | — | Pending |
| EXPR-01 | — | Pending |
| EXPR-02 | — | Pending |
| EXPR-03 | — | Pending |
| EXPR-04 | — | Pending |
| EXPR-05 | — | Pending |
| EXPR-06 | — | Pending |

**Coverage:**
- v8.1 requirements: 9 total
- Mapped to phases: 0
- Unmapped: 9 ⚠️

---
*Requirements defined: 2026-03-31*
