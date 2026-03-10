# Requirements: LangThree v1.2 / v1.3

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
| PIPE-01 | 09 | Complete |
| PIPE-02 | 09 | Complete |
| PIPE-03 | 09 | Complete |
| UNIT-01 | 10 | Complete |
| UNIT-02 | 10 | Complete |
| UNIT-03 | 10 | Complete |
| STR-01 | 11 | Pending |
| STR-02 | 11 | Pending |
| STR-03 | 11 | Pending |
| STR-04 | 11 | Pending |
| STR-05 | 11 | Pending |
| STR-06 | 11 | Pending |
| PRINT-01 | 12 | Pending |
| PRINT-02 | 12 | Pending |
| PRINT-03 | 12 | Pending |

---

## Milestone v1.3: Language Completion

### Tail Call Optimization (TCO)

- [ ] **TCO-01**: 꼬리 위치 감지 — 함수 본문의 꼬리 위치 호출 식별
- [ ] **TCO-02**: Trampoline 평가 — 꼬리 호출을 stack-safe하게 실행
- [ ] **TCO-03**: 대규모 재귀 — n=1,000,000 이상에서도 stack overflow 없음

### Or-Patterns & String Patterns (PAT)

- [ ] **PAT-01**: Or-패턴 — `| 1 | 2 | 3 -> expr` 여러 패턴이 같은 본문 공유
- [ ] **PAT-02**: 문자열 패턴 — `| "hello" -> expr` 문자열 상수 매칭
- [ ] **PAT-03**: Or-패턴 exhaustiveness — or-패턴이 소진 검사에 올바르게 반영

### Type Aliases (ALIAS)

- [ ] **ALIAS-01**: 단순 별칭 — `type Name = string` 타입 별칭 선언
- [ ] **ALIAS-02**: 복합 별칭 — `type IntPair = int * int` 튜플/함수 타입 별칭

### List Ranges & Mutual Recursion (RANGE/MUTREC)

- [ ] **RANGE-01**: 리스트 범위 — `[1..5]` → `[1, 2, 3, 4, 5]`
- [ ] **RANGE-02**: 스텝 범위 — `[1..2..10]` → `[1, 3, 5, 7, 9]`
- [ ] **MUTREC-01**: 상호 재귀 함수 — `let rec f x = ... and g y = ...` 모듈 레벨

## Traceability (continued)

| REQ-ID | Phase | Status |
|--------|-------|--------|
| TCO-01 | 13 | Pending |
| TCO-02 | 13 | Pending |
| TCO-03 | 13 | Pending |
| PAT-01 | 14 | Pending |
| PAT-02 | 14 | Pending |
| PAT-03 | 14 | Pending |
| ALIAS-01 | 15 | Pending |
| ALIAS-02 | 15 | Pending |
| RANGE-01 | 16 | Pending |
| RANGE-02 | 16 | Pending |
| MUTREC-01 | 16 | Pending |

## Out of Scope (v1.2 / v1.3)

- 사용자 정의 연산자 — v1.4
- Type classes / interfaces — v1.4
- Computation expressions — v1.4+
- IO / 파일 시스템 — v1.4+
