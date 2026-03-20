# Requirements: LangThree v1.5

**Defined:** 2026-03-20
**Core Value:** 사용자 정의 연산자로 DSL 표현력 향상

## v1.5 Requirements

### Operator Lexing (LEX)

- [ ] **LEX-01**: 연산자 문자 토큰 — `!`, `%`, `&`, `*`, `+`, `-`, `.`, `/`, `<`, `=`, `>`, `?`, `@`, `^`, `|`, `~` 조합으로 이루어진 다중 문자 토큰을 INFIXOP0-4로 분류
- [ ] **LEX-02**: 기존 연산자 호환 — `|>`, `>>`, `<<`, `&&`, `||`, `<=`, `>=`, `<>`, `::`, `->`, `<-` 등 기존 연산자가 영향받지 않음

### Operator Definition (DEF)

- [ ] **DEF-01**: 중위 연산자 정의 — `let (++) a b = append a b` 구문으로 사용자 정의 중위 연산자 선언
- [ ] **DEF-02**: 모듈 레벨 연산자 — Prelude 및 일반 모듈에서 연산자 정의 가능
- [ ] **DEF-03**: let rec 연산자 — `let rec (op) a b = ...` 재귀 연산자 정의

### Operator Usage (USE)

- [ ] **USE-01**: 중위 사용 — `[1, 2] ++ [3, 4]` 형태로 사용자 정의 연산자를 중위 표기로 사용
- [ ] **USE-02**: 우선순위 — 첫 번째 문자 기반 우선순위 (F#/OCaml 규칙)
- [ ] **USE-03**: 결합성 — 좌결합 (INFIXOP0-3), `**` 등 특수 문자는 우결합 (INFIXOP4)

### Operator Interop (INTEROP)

- [ ] **INTEROP-01**: 함수로서의 연산자 — `(++)` 형태로 연산자를 일반 함수처럼 사용 (예: `map (++) lists`)

## Out of Scope (v1.5)

| Feature | Reason |
|---------|--------|
| 전위 연산자 (prefix operators) | 복잡도 높음, v1.6 |
| 기존 연산자 재정의 (+, -, * 등) | 안전성 문제 |
| 사용자 정의 우선순위 선언 | LALR(1) 호환 어려움, 첫 문자 규칙으로 충분 |
| 모듈 한정 연산자 (Module.(+)) | v1.6 |

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| LEX-01 | 19 | Complete |
| LEX-02 | 19 | Complete |
| DEF-01 | 19 | Complete |
| DEF-02 | 19 | Complete |
| DEF-03 | 19 | Complete |
| USE-01 | 19 | Complete |
| USE-02 | 19 | Complete |
| USE-03 | 19 | Complete |
| INTEROP-01 | 19 | Complete |

**Coverage:**
- v1.5 requirements: 9 total
- Mapped to phases: 9
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-20*
