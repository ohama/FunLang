# LangThree

## What This Is

FunLang을 기반으로 한 실용적인 ML 스타일 함수형 프로그래밍 언어. F# 스타일의 문법과 타입 시스템을 갖춘, 실제로 사용할 수 있는 언어를 목표로 한다.

## Core Value

현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Indentation-based syntax (F# 스타일 들여쓰기 기반 파싱)
- [ ] ADT (Algebraic Data Types) with GADT support
- [ ] Records (`{ name: string; age: int }`)
- [ ] Module system (F# 스타일, functor 없이)
- [ ] Exceptions (F# 스타일 — `exception`, `try...with`, `raise`)

### Out of Scope

- IO / 파일 시스템 / 네트워크 — 다음 마일스톤으로
- OCaml 스타일 functor — F# 스타일 모듈만
- 컴파일러 (네이티브/바이트코드) — 인터프리터 유지
- IDE 통합 / LSP — 언어 기능 완성 후

## Context

**기반 코드**: ../LangTutorial의 FunLang v6.0
- Hindley-Milner 타입 추론
- Bidirectional type checking
- 패턴 매칭
- 리스트, 튜플
- Prelude (map, filter, fold 등)

**기술 스택**: F# / .NET / fslex / fsyacc

**구현 순서**: Indentation → ADT → Records → Modules → Exceptions

## Constraints

- **Tech stack**: F# / .NET 10 — FunLang과 동일
- **Parser**: fslex / fsyacc — 기존 인프라 활용
- **Scope**: 각 기능의 기본형만. 고급 기능은 이후 마일스톤

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| F# 스타일 선택 | OCaml보다 단순, 들여쓰기 기반이 현대적 | — Pending |
| GADT 포함 | 표현력 있는 타입 시스템, FunLang의 bidirectional checking 활용 | — Pending |
| Functor 제외 | 복잡도 대비 실용성 낮음 | — Pending |

---
*Last updated: 2026-02-25 after initialization*
