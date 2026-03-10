# Roadmap: LangThree v1.2

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
