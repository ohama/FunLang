# Project Milestones: LangThree

## v1.7 F#-Style Offside Rule & List Syntax (Shipped: 2026-03-22)

**Delivered:** F# 스타일 offside rule로 implicit `in` 지원, 리스트 구분자를 세미콜론으로 변경 (`[1; 2; 3]`)

**Phases completed:** 23-24 (4 plans total)

**Key accomplishments:**
- CtxtLetDecl + offside column 기반 implicit `in` 삽입 (LetSeqDepth counter 교체)
- InExprBlock, InModule 컨텍스트로 expression/module 구분
- 리스트 구분자 `,` → `;` (F# 관례 호환): `[1; 2; 3]`
- SemiExprList 규칙 도입, 튜플은 COMMA 유지
- 439 fslit + 196 F# tests, 16 tutorial chapters 전체 업데이트

**Stats:**
- 23 source files, 10,624 lines of F#
- 635 tests (196 F# + 439 fslit), all passing
- 2 phases, 4 plans
- 2 days (2026-03-20 → 2026-03-22)

**Git range:** `feat(23-01)` → `docs(24): complete`

---

## v1.5 User-Defined Operators & Utilities (Shipped: 2026-03-20)

**Delivered:** 사용자 정의 연산자 (INFIXOP0-4), Prelude 유틸리티, sprintf/printfn/%, 음수 패턴, 튜플 람다

**Phases completed:** 19-22 (4 plans total)

**Key accomplishments:**
- 사용자 정의 중위 연산자: `let (++) a b = ...` + `[1,2] ++ [3,4]`
- Prelude 유틸리티: not, min, max, abs, fst, snd, ignore
- Prelude 연산자: `++`, `<|>`, `^^`
- `sprintf "%d" 42` → `"42"`, `printfn`, `%` 모듈로
- 음수 패턴 `| -1 ->`, 튜플 람다 `fun (x,y) ->`

**Stats:**
- 23 source files, 10,304 lines of F#
- 608 tests (196 F# + 412 fslit), all passing
- 4 phases, 4 plans
- 1 day (2026-03-20)

**Git range:** `feat(19-01)` → `feat(22-01)`

---

## v1.4 Language Completion (Shipped: 2026-03-20)

**Delivered:** TCO, or-패턴/문자열 패턴, 타입 별칭, 리스트 범위, 상호 재귀 함수로 언어 완성도 달성

**Phases completed:** 15-18 (6 plans total)

**Key accomplishments:**
- Trampoline TCO — `loop 1000000` stack overflow 없이 동작
- Or-patterns `| 1 | 2 | 3 ->` + 문자열 패턴 `| "hello" ->`
- 타입 별칭 `type Name = string`
- 리스트 범위 `[1..5]`, `[1..2..10]`
- 모듈 레벨 `let rec f = ... and g = ...` 상호 재귀

**Stats:**
- 23 source files, 9,805 lines of F#
- 533 tests (196 F# + 337 fslit), all passing
- 4 phases, 6 plans
- 1 day (2026-03-19 → 2026-03-20)

**Git range:** `feat(15-01)` → `docs(18)`

**What's next:** v1.5 (user-defined operators, type classes)

---

## v1.3 Tutorial Documentation (Shipped: 2026-03-19)

**Delivered:** 함수형 프로그래밍 경험자를 위한 포괄적 LangThree 튜토리얼 13개 챕터, 224개 CLI-verified 예제

**Phases completed:** 13-14 (4 plans total)

**Key accomplishments:**
- 13개 마크다운 튜토리얼 챕터 (기초부터 GADT, 모듈, 예외, 파이프, CLI까지)
- 224개 CLI에서 실제 실행 검증된 코드 예제
- 2,524 lines of tutorial documentation
- Prelude 시스템과 Option 타입 활용법 문서화

**Stats:**
- 13 tutorial files, 2,524 lines of markdown
- 224 CLI-verified examples
- 2 phases, 4 plans
- 1 day (2026-03-19)

**Git range:** `feat(13-01)` → `docs(14-02)`

**What's next:** v1.4 Language Completion (TCO, or-patterns, type aliases, list ranges, mutual recursion)

---

## v1.2 Practical Language Features (Shipped: 2026-03-18)

**Delivered:** 파이프/합성 연산자, unit 타입, 문자열 내장 함수, printf 포맷 출력, Prelude 디렉토리 기반 표준 라이브러리, Option 타입

**Phases completed:** 8-12 (12 plans total)

**Key accomplishments:**
- `|>` 파이프, `>>` `<<` 합성 연산자로 F# 스타일 파이프라인 프로그래밍
- `()` unit 타입과 `let _ =` 부수효과 시퀀싱
- BuiltinValue 인프라로 6개 문자열 내장 함수 (string_length, string_concat, string_sub, string_contains, to_string, string_to_int)
- print/println/printf 포맷 출력 (curried 인자 체인)
- Prelude/*.fun 디렉토리 로딩 + Option 타입 표준 라이브러리
- 168→260 fslit 테스트 확장 (전 기능 커버리지)

**Stats:**
- 23 source files, 9,112 lines of F#
- 456 tests (196 F# + 260 fslit), all passing
- 5 phases, 12 plans
- 8 days from start to ship (2026-03-10 → 2026-03-18)

**Git range:** `test(08-01)` → `fix: handle BuiltinValue in pipe operator`

**What's next:** v1.3 Language Completion (TCO, or-patterns, type aliases, list ranges, mutual recursion)

---

## v1.0 Core Language (Shipped: 2026-03-10)

**Delivered:** FunLang v6.0을 F# 스타일 문법과 현대적 타입 시스템(ADT, GADT, Records, Modules, Exceptions)을 갖춘 실용 함수형 언어로 변환

**Phases completed:** 1-7 (32 plans total)

**Key accomplishments:**
- F# 스타일 들여쓰기 기반 파싱 (offside rule, pipe alignment, multi-line function application)
- 대수적 데이터 타입 (ADT) with Maranget exhaustiveness/redundancy checking
- Generalized ADT (GADT) with bidirectional type refinement and existential types
- Records with mutable fields, copy-update syntax, dot notation access
- Module system with namespaces, qualified names, circular dependency detection
- Exception handling (try-with, when guards, custom exception types)
- Pattern matching compilation to binary decision trees (Jules Jacobs algorithm)

**Stats:**
- 23 source files, 8,362 lines of F#
- 196 tests (2,231 lines), all passing
- 7 phases, 32 plans
- 13 days from start to ship (2026-02-25 → 2026-03-10)

**Git range:** `feat(01-01)` → `test(07-03)`

**What's next:** Project complete (v1.0 milestone fulfills all planned requirements)

---
