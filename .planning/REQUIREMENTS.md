# Requirements: LangThree v1.2

## Milestone v1.2: Practical Language Features

### Pipe & Composition (PIPE)

- [ ] **PIPE-01**: `|>` 파이프 연산자 — `expr |> func` 를 `func expr`로 평가
- [ ] **PIPE-02**: `>>` 합성 연산자 — `f >> g` 를 `fun x -> g (f x)`로 평가
- [ ] **PIPE-03**: `<<` 역합성 연산자 — `f << g` 를 `fun x -> f (g x)`로 평가

### Unit Type (UNIT)

- [ ] **UNIT-01**: `()` 리터럴 파싱 및 평가 — unit 값 표현
- [ ] **UNIT-02**: `unit` 타입 — 타입 추론에서 unit 타입 지원
- [ ] **UNIT-03**: 부수효과 시퀀싱 — `let _ = expr1 in expr2` 또는 세미콜론 시퀀싱

### String Operations (STR)

- [ ] **STR-01**: `string_length` — 문자열 길이 반환
- [ ] **STR-02**: `string_concat` — 두 문자열 결합 (+ 연산자 외 함수 형태)
- [ ] **STR-03**: `string_sub` — 부분 문자열 추출
- [ ] **STR-04**: `string_contains` — 문자열 포함 여부
- [ ] **STR-05**: `to_string` — int/bool을 문자열로 변환
- [ ] **STR-06**: `string_to_int` — 문자열을 int로 변환

### Printf (PRINT)

- [ ] **PRINT-01**: `print` — 값을 stdout에 출력 (줄바꿈 없이)
- [ ] **PRINT-02**: `println` — 값을 stdout에 출력 (줄바꿈 포함)
- [ ] **PRINT-03**: `printf` — 포맷 문자열로 출력 (`%d`, `%s`, `%b`)

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| PIPE-01 | 09 | Pending |
| PIPE-02 | 09 | Pending |
| PIPE-03 | 09 | Pending |
| UNIT-01 | 10 | Pending |
| UNIT-02 | 10 | Pending |
| UNIT-03 | 10 | Pending |
| STR-01 | 11 | Pending |
| STR-02 | 11 | Pending |
| STR-03 | 11 | Pending |
| STR-04 | 11 | Pending |
| STR-05 | 11 | Pending |
| STR-06 | 11 | Pending |
| PRINT-01 | 12 | Pending |
| PRINT-02 | 12 | Pending |
| PRINT-03 | 12 | Pending |

## Out of Scope (v1.2)

- TCO (꼬리 호출 최적화) — v1.3
- Or-patterns — v1.3
- 사용자 정의 연산자 — v1.4
- Type classes — v1.4
