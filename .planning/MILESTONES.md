# Project Milestones: LangThree

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
