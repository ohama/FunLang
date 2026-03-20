# Roadmap: LangThree v1.5

## Current Milestone: v1.5 User-Defined Operators & Utilities

### Phase 19: User-Defined Operators (COMPLETE)

**Goal:** 사용자 정의 중위 연산자 — 정의, 우선순위, 사용을 LALR(1) 파서와 호환되게 구현
**Depends on:** v1.4 (complete)
**Requirements:** LEX-01, LEX-02, DEF-01, DEF-02, DEF-03, USE-01, USE-02, USE-03, INTEROP-01
**Status:** COMPLETE — INFIXOP0-4 + let (op) + (op) function form, 577 tests passing

Plans:
- [x] 19-01: User-Defined Operators (INFIXOP0-4, operator definition, 6 tests)

---

### Phase 20: Prelude Utility Functions

**Goal:** 기본 유틸리티 함수 (not, min, max, abs, fst, snd, ignore) 를 Prelude에 추가
**Depends on:** Phase 19
**Requirements:** UTIL-01, UTIL-02, UTIL-03, UTIL-04, UTIL-05, UTIL-06, UTIL-07
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 20 to break down)

**Success Criteria:**
1. `not true` → `false`
2. `min 3 5` → `3`, `max 3 5` → `5`
3. `abs (0 - 5)` → `5`
4. `fst (1, 2)` → `1`, `snd (1, 2)` → `2`
5. `ignore (print "hi")` → `()`
6. 기존 테스트 전부 통과

---

### Phase 21: sprintf, printfn, Modulo Operator

**Goal:** sprintf (문자열 반환 포맷), printfn (줄바꿈 포함 printf), % 모듈로 연산자 추가
**Depends on:** Phase 19
**Requirements:** FMT-01, FMT-02, FMT-03
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 21 to break down)

**Success Criteria:**
1. `sprintf "%d + %d = %d" 1 2 3` → `"1 + 2 = 3"` (문자열 반환, 출력 안 함)
2. `printfn "hello %d" 42` → stdout에 "hello 42\n" 출력
3. `10 % 3` → `1` (모듈로 연산)
4. 기존 테스트 전부 통과

---

### Phase 22: Negative Patterns & Tuple Lambda

**Goal:** 음수 패턴 매칭과 튜플 파라미터 람다 추가
**Depends on:** Phase 19
**Requirements:** PAR-01, PAR-02
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 22 to break down)

**Success Criteria:**
1. `match x with | -1 -> "neg one" | 0 -> "zero" | _ -> "other"` — 음수 패턴 동작
2. `(fun (x, y) -> x + y) (1, 2)` → `3` — 튜플 파라미터 람다
3. 기존 테스트 전부 통과

---
