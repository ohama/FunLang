# Requirements: LangThree v1.4

**Defined:** 2026-03-19
**Core Value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어

## v1.4 Requirements

### Tail Call Optimization (TCO)

- [ ] **TCO-01**: 꼬리 위치 감지 — 함수 본문의 꼬리 위치 호출 식별
- [ ] **TCO-02**: Trampoline 평가 — 꼬리 호출을 stack-safe하게 실행
- [ ] **TCO-03**: 대규모 재귀 — n=1,000,000 이상에서도 stack overflow 없음

### Or-Patterns & String Patterns (PAT)

- [ ] **PAT-01**: Or-패턴 — `| 1 | 2 | 3 -> expr` 여러 패턴이 같은 본문 공유
- [ ] **PAT-02**: 문자열 패턴 — `| "hello" -> expr` 문자열 상수 매칭
- [ ] **PAT-03**: Or-패턴 exhaustiveness — or-패턴이 소진 검사와 decision tree에 올바르게 통합

### Type Aliases (ALIAS)

- [ ] **ALIAS-01**: 단순 별칭 — `type Name = string` 타입 별칭 선언
- [ ] **ALIAS-02**: 복합 별칭 — `type IntPair = int * int` 튜플/함수 타입 별칭

### List Ranges (RANGE)

- [ ] **RANGE-01**: 리스트 범위 — `[1..5]` → `[1, 2, 3, 4, 5]`
- [ ] **RANGE-02**: 스텝 범위 — `[1..2..10]` → `[1, 3, 5, 7, 9]`

### Mutual Recursive Functions (MUTREC)

- [ ] **MUTREC-01**: 상호 재귀 함수 — `let rec f x = ... and g y = ...` 모듈 레벨
- [ ] **MUTREC-02**: 상호 재귀 타입 추론 — 상호 재귀 함수의 타입이 올바르게 추론됨

## Out of Scope (v1.4)

| Feature | Reason |
|---------|--------|
| Mutual recursion TCO | 단일 함수 TCO만; mutual TCO는 v1.5 |
| Active patterns | F# 고급 기능, v1.5+ |
| User-defined operators | v1.5 |
| Type classes / interfaces | v1.5+ |

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| TCO-01 | 15 | Pending |
| TCO-02 | 15 | Pending |
| TCO-03 | 15 | Pending |
| PAT-01 | 16 | Pending |
| PAT-02 | 16 | Pending |
| PAT-03 | 16 | Pending |
| ALIAS-01 | 17 | Pending |
| ALIAS-02 | 17 | Pending |
| RANGE-01 | 18 | Pending |
| RANGE-02 | 18 | Pending |
| MUTREC-01 | 18 | Pending |
| MUTREC-02 | 18 | Pending |

**Coverage:**
- v1.4 requirements: 12 total
- Mapped to phases: 12
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-19*
