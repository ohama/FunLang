# LangThree

## What This Is

FunLang v6.0을 기반으로 한 실용적인 ML 스타일 함수형 프로그래밍 언어. F# 스타일의 들여쓰기 기반 문법, ADT/GADT/Records 타입 시스템, 모듈, 예외 처리, 그리고 효율적인 패턴 매칭 컴파일을 갖춘 완전한 인터프리터.

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

### Active

(None — v1.1 milestone complete)

### Out of Scope

- IO / 파일 시스템 / 네트워크 — 다음 마일스톤으로
- OCaml 스타일 functor — F# 스타일 모듈만
- 컴파일러 (네이티브/바이트코드) — 인터프리터 유지
- IDE 통합 / LSP — 언어 기능 완성 후

## Context

**Shipped:** v1.0 Core Language (2026-03-10)
- 23 source files, 8,362 lines of F#
- 196 tests, all passing
- 7 phases, 32 plans executed in 13 days

**기반 코드**: ../LangTutorial의 FunLang v6.0
- Hindley-Milner 타입 추론
- Bidirectional type checking
- 패턴 매칭
- 리스트, 튜플
- Prelude (map, filter, fold 등)

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

---
*Last updated: 2026-03-10 after v1.1 milestone*
