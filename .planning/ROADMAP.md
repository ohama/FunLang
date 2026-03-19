# Roadmap: LangThree v1.4

## Current Milestone: v1.4 Language Completion

### Phase 15: Tail Call Optimization (COMPLETE)

**Goal:** 꼬리 위치 호출을 trampoline 방식으로 최적화하여 대규모 재귀에서도 stack overflow 방지
**Depends on:** v1.3 (complete)
**Requirements:** TCO-01, TCO-02, TCO-03
**Status:** COMPLETE — TailCall DU + trampoline, 513 tests passing

Plans:
- [x] 15-01: Trampoline TCO (TailCall DU, tailPos parameter, trampoline loop, 4 TCO tests)

**Success Criteria:**
1. `let rec loop n = if n = 0 then 0 else loop (n - 1) in loop 1000000` — stack overflow 없이 동작
2. 꼬리 위치가 아닌 재귀는 영향 없음 (`fact n = n * fact (n-1)` 정상 동작)
3. 기존 테스트 전부 통과

---

### Phase 16: Or-Patterns & String Patterns (COMPLETE)

**Goal:** Or-패턴과 문자열 패턴 매칭을 추가하여 패턴 매칭 완성도 향상
**Depends on:** Phase 15
**Requirements:** PAT-01, PAT-02, PAT-03
**Status:** COMPLETE — StringConst + OrPat, 520 tests passing

Plans:
- [x] 16-01: String patterns + Or-patterns (StringConst, OrPat, LALR(1) grammar, 7 tests)

**Success Criteria:**
1. `match x with | 1 | 2 | 3 -> "small" | _ -> "big"` 동작
2. `match name with | "admin" -> true | _ -> false` 동작
3. Or-패턴이 exhaustiveness checker와 decision tree에 올바르게 통합
4. `--emit-ast`에서 OrPat / ConstPat(StringConst) 노드 확인

---

### Phase 17: Type Aliases (COMPLETE)

**Goal:** 타입 별칭으로 타입 가독성 향상
**Depends on:** Phase 15
**Requirements:** ALIAS-01, ALIAS-02
**Status:** COMPLETE — TypeAliasDecl + transparent expansion, 525 tests passing

Plans:
- [x] 17-01: Type alias implementation (TypeAliasDecl, parser grammar, 5 tests)

**Success Criteria:**
1. `type Name = string` — Name을 string으로 사용 가능
2. `type IntPair = int * int` — 복합 타입 별칭
3. `--emit-type`에서 별칭 또는 원본 타입 출력

---

### Phase 18: List Ranges & Mutual Recursive Functions

**Goal:** 리스트 범위 문법과 상호 재귀 함수 선언을 추가
**Depends on:** Phase 15
**Requirements:** RANGE-01, RANGE-02, MUTREC-01, MUTREC-02
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 18 to break down)

**Success Criteria:**
1. `[1..5]` evaluates to `[1, 2, 3, 4, 5]`
2. `[1..2..10]` evaluates to `[1, 3, 5, 7, 9]`
3. `let rec even n = ... and odd n = ...` works at module level
4. 상호 재귀 함수의 타입 추론 올바름

---
