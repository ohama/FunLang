# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-09)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** 다음 milestone 미정

## Current Position

Milestone: (없음)
Phase: —
Plan: —
Status: v13.0 + v14.0 완료, 다음 milestone 대기
Last activity: 2026-04-09 — v13.0/v14.0 아카이브 정리

Progress: idle

## Performance Metrics

**Velocity:**
- Total plans completed: 175+
- v14.0: 5 phases in 1 day (2026-04-08)
- v13.0: Standard Library Extension 완료
- v12.0: 4 phases, 4 plans in 1 day

## Accumulated Context

### Decisions

From v14.0 (Phase 91-95):
- Prelude 함수에서 `fun x ->` 패턴을 직접 인자로 펼침 완료
- Prelude 연산자 파라미터 간소화: `__pipe_x`/`__comp_lhs` → `x`/`f`/`g` (applyFunc 가드로 안전)
- 모든 Prelude 함수에 타입 어노테이션 추가
- TEData("array")→TArray, TEData("hashset")→THashSet 등 Elaborate.fs 매핑
- OccursCheck 에러 메시지에 formatTypeNormalized 적용

From v12.0 (Phase 84-87):
- |>, >>, << are now Prelude-defined operators with #[left 1], #[left 2], #[right 2] fixity
- applyFunc self-name injection guarded: only injects when name NOT already in closure env

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

### Blockers/Concerns

- v13.0 Uniform Tagged Representation은 FunLangCompiler 범위 → 이 저장소 Out of Scope

## Session Continuity

Last session: 2026-04-09
Stopped at: v13.0/v14.0 아카이브 완료
Resume file: None
Next action: 새 milestone 결정
