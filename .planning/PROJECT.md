# LangThree

## What This Is

FunLang v6.0을 기반으로 한 실용적인 ML 스타일 함수형 프로그래밍 언어. F# 스타일의 들여쓰기 기반 문법, ADT/GADT/Records 타입 시스템, 모듈, 예외 처리, 패턴 매칭 컴파일, 파이프/합성 연산자, 문자열 내장 함수, printf 포맷 출력, Prelude 표준 라이브러리를 갖춘 완전한 인터프리터.

## Core Value

현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어.

## Requirements

### Validated

- Indentation-based syntax (F# 스타일 들여쓰기 기반 파싱) — v1.0
- ADT (Algebraic Data Types) with GADT support — v1.0
- Records with mutable fields — v1.0
- Module system (F# 스타일, functor 없이) — v1.0
- Exceptions (F# 스타일 — exception, try...with, raise) — v1.0
- Pattern matching compilation to decision trees — v1.0
- File-based testing with fslit (63 tests) — v1.1
- CLI module-level --emit-ast and --emit-type — v1.1
- IndentFilter integration in CLI file mode — v1.1
- Pipe operator `|>` and composition `>>` `<<` — v1.2
- Unit type `()` with side-effect sequencing — v1.2
- String built-in functions (string_length, string_concat, string_sub, string_contains, to_string, string_to_int) — v1.2
- `printf` / `print` / `println` formatted output — v1.2
- Prelude/*.fun directory-based standard library loading — v1.2
- Option type in Prelude (None | Some of 'a) — v1.2
- Comprehensive tutorial documentation (13 chapters, 224 examples) — v1.3
- Tail call optimization (trampoline) — v1.4
- Or-patterns and string patterns in match — v1.4
- Type aliases (`type Name = string`) — v1.4
- List ranges (`[1..5]`, `[1..2..10]`) — v1.4
- Module-level let rec and mutual recursion (`let rec f = ... and g = ...`) — v1.4

### Active (v1.5)

- User-defined operators — v1.5

### Future (v1.6+)

- Type classes / interfaces — v1.6

### Out of Scope

- IO / 파일 시스템 / 네트워크 — 다음 마일스톤으로
- OCaml 스타일 functor — F# 스타일 모듈만
- 컴파일러 (네이티브/바이트코드) — 인터프리터 유지
- IDE 통합 / LSP — 언어 기능 완성 후

## Context

**Shipped:** v1.4 Language Completion (2026-03-20)
- 23 source files, 9,805 lines of F#
- 533 tests (196 F# + 337 fslit), all passing
- 18 phases, 54 plans executed across v1.0-v1.4
- TCO, or-patterns, string patterns, type aliases, list ranges, mutual recursion
- Prelude/Option.fun standard library

**기반 코드**: ../LangTutorial의 FunLang v6.0
- Hindley-Milner 타입 추론
- Bidirectional type checking
- 패턴 매칭
- 리스트, 튜플

**기술 스택**: F# / .NET 10 / fslex / fsyacc

## Constraints

- **Tech stack**: F# / .NET 10 — FunLang과 동일
- **Parser**: fslex / fsyacc — 기존 인프라 활용
- **Scope**: 각 기능의 기본형만. 고급 기능은 이후 마일스톤

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| F# 스타일 선택 | OCaml보다 단순, 들여쓰기 기반이 현대적 | ✓ Good |
| GADT 포함 | 표현력 있는 타입 시스템, FunLang의 bidirectional checking 활용 | ✓ Good |
| Functor 제외 | 복잡도 대비 실용성 낮음 | ✓ Good |
| Jules Jacobs algorithm for pattern matching | Binary trees simpler than Maranget N-way, no clause duplication | ✓ Good |
| evalFn parameter pattern | Avoids circular F# module dependency between MatchCompile and Eval | ✓ Good |
| Record field-name encoding in constructor names | Enables partial record pattern matching in decision trees | ✓ Good |
| BuiltinValue DU for native F# functions | Clean carrier for all built-in functions, CustomEquality on Value | ✓ Good |
| Closure-based composition (composeCounter) | Unique names avoid stack overflow in chained >> | ✓ Good |
| Prelude/*.fun directory loading | Modular standard library, type-checked prelude files | ✓ Good |

## Next Milestone: v1.5 User-Defined Operators

**Goal:** 사용자 정의 연산자로 DSL 표현력 향상

**Target features:**
- 사용자 정의 중위 연산자 (`let (++) a b = ...`)
- 연산자 우선순위/결합성 제어
- Prelude에서 연산자 정의 가능

---
*Last updated: 2026-03-20 after v1.5 milestone planning*
