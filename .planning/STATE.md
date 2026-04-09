# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-09)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v15.0 Type Class Maturity

## Current Position

Milestone: v15.0 Type Class Maturity
Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-04-09 — Milestone v15.0 started

Progress: [..........] 0%

## Performance Metrics

**Velocity:**
- Total plans completed: 175+
- v14.0: 5 phases in 1 day (2026-04-08)
- v12.0: 4 phases, 4 plans in 1 day

## Accumulated Context

### Decisions

From v14.0 (Phase 91-95):
- Prelude 함수에서 `fun x ->` 패턴을 직접 인자로 펼침 완료
- 모든 Prelude 함수에 타입 어노테이션 추가
- OccursCheck 에러 메시지에 formatTypeNormalized 적용

From v10.0-v10.1 (Type Classes):
- typeclass/instance 선언, 제약 추론, 딕셔너리 elaboration
- Show/Eq 내장 인스턴스 (int/bool/string/char)
- ClassEnv/InstanceEnv export, instance method 승격

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-09
Stopped at: v15.0 milestone 시작, requirements 정의 중
Resume file: None
Next action: Research or define requirements
