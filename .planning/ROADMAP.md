# Roadmap: LangThree v8.0 Declaration Type Annotations

FunLexYacc 호환성을 위해 module-level 함수 선언에 F# 스타일 타입 어노테이션과 앵글 브래킷 제네릭 구문을 추가한다. 먼저 타입 표현식 문법에 앵글 브래킷 제네릭을 도입하여 어노테이션에서 참조할 수 있게 하고, 이어서 파라미터 타입 어노테이션과 반환 타입 어노테이션을 선언 문법에 추가한다.

## Phases

### Phase 63: Angle Bracket Generics

**Goal:** 타입 표현식에서 앵글 브래킷 제네릭 구문을 사용할 수 있다

**Dependencies:** None (lexer/parser extension only)

**Requirements:** GEN-01, GEN-02, GEN-03

**Success Criteria:**
1. `type Result<'a> = Ok of 'a | Error of string` 선언이 파싱된다
2. 람다 어노테이션 `fun (x : Result<'a>) -> ...`에서 `Result<'a>` 타입 표현식이 파싱된다
3. 기존 후위 구문 `'a option`, `int list`, `'a list` 등이 그대로 동작한다
4. 앵글 브래킷 구문과 후위 구문을 혼합한 타입 `Result<'a> list`가 파싱된다

**Plans:** 2 plans

Plans:
- [ ] 63-01-PLAN.md — Add TypeArgList and angle bracket rules to Parser.fsy type grammar
- [ ] 63-02-PLAN.md — Add flt integration tests for angle bracket generic syntax

---

### Phase 64: Declaration Type Annotations

**Goal:** `let` 선언에서 파라미터 타입 어노테이션과 반환 타입 어노테이션을 모두 사용할 수 있다

**Dependencies:** Phase 63 (annotated params may reference generic types like `Result<'a>`)

**Requirements:** PARAM-01, PARAM-02, RET-01, RET-02

**Success Criteria:**
1. `let f (x : int) y (z : bool) = ...` — plain과 annotated 파라미터를 혼합한 선언이 파싱되고 실행된다
2. `let f x : int = x + 1` — 반환 타입 어노테이션이 있는 선언이 파싱되고 실행된다
3. `let f (x : int) : bool = x > 0` — 파라미터 어노테이션과 반환 타입 어노테이션을 동시에 사용한 선언이 파싱되고 실행된다
4. `let rec f (x : int) = ... and g (y : bool) = ...` 상호 재귀 선언에서 파라미터 어노테이션이 동작한다
5. 타입 어노테이션은 런타임에 소거되며 기존 타입 추론 동작이 유지된다

**Plans:** TBD

Plans:
- [ ] 64-01: Add MixedParamList parser rule and desugar to Lambda/LambdaAnnot chain
- [ ] 64-02: Add return type annotation syntax to let declarations and wrap body in Annot node
- [ ] 64-03: Add flt integration tests for annotated declarations including mutual recursion

---

## Progress

| Phase | Name | Milestone | Plans Complete | Status | Completed |
|-------|------|-----------|----------------|--------|-----------|
| 63 | Angle Bracket Generics | v8.0 | 0/2 | Not started | - |
| 64 | Declaration Type Annotations | v8.0 | 0/TBD | Not started | - |

## Coverage

| Requirement | Phase | Category |
|-------------|-------|----------|
| GEN-01 | 63 | Angle Bracket Generics |
| GEN-02 | 63 | Angle Bracket Generics |
| GEN-03 | 63 | Angle Bracket Generics |
| PARAM-01 | 64 | Declaration Type Annotations |
| PARAM-02 | 64 | Declaration Type Annotations |
| RET-01 | 64 | Declaration Type Annotations |
| RET-02 | 64 | Declaration Type Annotations |

**Total:** 7/7 requirements mapped

---
*Roadmap created: 2026-03-30*
*Milestone: v8.0 Declaration Type Annotations*
