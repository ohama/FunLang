# Roadmap: LangThree v1.2 / v1.3

## Current Milestone: v1.2 Practical Language Features

### Phase 08: Full Coverage fslit Testing (COMPLETE)

**Goal:** --emit-ast, --emit-type, fslit을 이용한 LangThree 전체 문법 100% coverage 테스트
**Status:** COMPLETE — 168/168 fslit tests passing

---

### Phase 09: Pipe & Composition Operators

**Goal:** `|>`, `>>`, `<<` 연산자를 추가하여 F# 스타일 파이프라인 프로그래밍 지원
**Depends on:** v1.0 (complete)
**Requirements:** PIPE-01, PIPE-02, PIPE-03
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 09 to break down)

**Success Criteria:**
1. `[1,2,3] |> map (fun x -> x * 2) |> filter (fun x -> x > 2)` evaluates correctly
2. `let double_then_add = (fun x -> x * 2) >> (fun x -> x + 1)` works
3. `--emit-ast` shows PipeRight / ComposeRight / ComposeLeft nodes
4. `--emit-type` infers correct types for pipe/composition chains
5. All existing tests still pass

---

### Phase 10: Unit Type

**Goal:** `()` unit 값과 `unit` 타입을 추가하여 부수효과 표현 지원
**Depends on:** Phase 09
**Requirements:** UNIT-01, UNIT-02, UNIT-03
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 10 to break down)

**Success Criteria:**
1. `()` parses and evaluates to UnitValue
2. `let _ = record.field <- 42` works without type errors
3. `fun () -> 42` works (unit parameter)
4. `--emit-type` shows `unit` type correctly
5. Mutable field set returns unit type

---

### Phase 11: String Operations

**Goal:** 문자열 내장 함수를 추가하여 실질적 문자열 처리 지원
**Depends on:** Phase 10
**Requirements:** STR-01, STR-02, STR-03, STR-04, STR-05, STR-06
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 11 to break down)

**Success Criteria:**
1. `string_length "hello"` returns 5
2. `string_sub "hello" 1 3` returns "ell"
3. `to_string 42` returns "42", `to_string true` returns "true"
4. `string_to_int "42"` returns 42
5. All string functions have correct types in `--emit-type`

---

### Phase 12: Printf Output

**Goal:** printf 계열 함수를 추가하여 포맷 출력 지원
**Depends on:** Phase 10 (unit type needed for return), Phase 11 (string ops)
**Requirements:** PRINT-01, PRINT-02, PRINT-03
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 12 to break down)

**Success Criteria:**
1. `print "hello"` outputs "hello" to stdout (no newline)
2. `println "hello"` outputs "hello\n" to stdout
3. `printf "x=%d, s=%s" 42 "hi"` outputs "x=42, s=hi"
4. Print functions return unit type
5. File-mode programs can produce output mid-execution (not just final value)

---

## Next Milestone: v1.3 Language Completion

### Phase 13: Tail Call Optimization

**Goal:** 꼬리 위치 호출을 trampoline 방식으로 최적화하여 대규모 재귀에서도 stack overflow 방지
**Depends on:** v1.2 (complete)
**Requirements:** TCO-01, TCO-02, TCO-03
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 13 to break down)

**Success Criteria:**
1. `let rec loop n = if n = 0 then 0 else loop (n - 1)` — n=1000000에서도 동작
2. 꼬리 위치가 아닌 재귀는 영향 없음
3. 기존 테스트 전부 통과

---

### Phase 14: Or-Patterns & String Patterns

**Goal:** Or-패턴과 문자열 패턴 매칭을 추가하여 패턴 매칭 완성도 향상
**Depends on:** Phase 13
**Requirements:** PAT-01, PAT-02, PAT-03
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 14 to break down)

**Success Criteria:**
1. `match x with | 1 | 2 | 3 -> "small" | _ -> "big"` 동작
2. `match name with | "admin" -> true | _ -> false` 동작
3. Or-패턴이 exhaustiveness checker와 decision tree에 올바르게 통합
4. `--emit-ast`에서 OrPat / ConstPat(StringConst) 노드 확인

---

### Phase 15: Type Aliases

**Goal:** 타입 별칭으로 타입 가독성 향상
**Depends on:** Phase 13
**Requirements:** ALIAS-01, ALIAS-02
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 15 to break down)

**Success Criteria:**
1. `type Name = string` — Name을 string으로 사용 가능
2. `type IntPair = int * int` — 복합 타입 별칭
3. `--emit-type`에서 별칭 또는 원본 타입 출력

---

### Phase 16: List Ranges & Mutual Recursive Functions

**Goal:** 리스트 범위 문법과 상호 재귀 함수 선언을 추가
**Depends on:** Phase 13
**Requirements:** RANGE-01, RANGE-02, MUTREC-01
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 16 to break down)

**Success Criteria:**
1. `[1..5]` evaluates to `[1, 2, 3, 4, 5]`
2. `[1..2..10]` evaluates to `[1, 3, 5, 7, 9]`
3. `let rec even n = ... and odd n = ...` works at module level
4. 상호 재귀 함수의 타입 추론 올바름

---
