# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-08)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v14.0 Type Annotation Completeness

## Current Position

Milestone: v14.0 Type Annotation Completeness
Phase: 91 — Array 타입 어노테이션 (Elaborate TEData→TArray)
Plan: —
Status: Roadmap created, awaiting plan
Last activity: 2026-04-08 — Roadmap created (5 phases, 5 requirements)

Progress: [..........] 0/5 phases

## Performance Metrics

**Velocity:**
- Total plans completed: 170+
- v12.0: 4 phases, 4 plans in 1 day
- v11.1: 1 phase, 1 plan in 1 day
- v11.0: 4 phases, 5 plans in 1 day

## Accumulated Context

### Decisions

From v14.0 준비 (Prelude 타입 어노테이션):
- Prelude/*.fun에서 `fun x ->` 패턴을 인자로 펼치는 리팩토링 완료 (Core/List/Option/Result/Typeclass)
- Core/List/Option/Result/String/Int/Char.fun에 타입 어노테이션 추가 완료
- Array/Hashtable/HashSet/Queue/MutableList/StringBuilder/Typeclass는 제약으로 미적용

From v14.0 (Prelude 타입 어노테이션):
- Prelude 함수에서 `fun x ->` 패턴을 직접 인자로 펼침 완료
- Prelude 연산자 파라미터 간소화: `__pipe_x`/`__comp_lhs` → `x`/`f`/`g` (applyFunc 가드로 안전)
- 모든 Prelude 함수에 타입 어노테이션 추가 (Array, Hashtable, HashSet, Queue, MutableList, StringBuilder 포함)

From v12.0 (Phase 84-87):
- |>, >>, << are now Prelude-defined operators with #[left 1], #[left 2], #[right 2] fixity
- applyFunc self-name injection guarded: only injects when name NOT already in closure env

From v11.1 (Phase 83):
- dbg builtin prints to stderr (eprintfn), not stdout
- hashtable_*_str builtins use monomorphic string key (Scheme([0]) not Scheme([0;1]))

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-08
Stopped at: v14.0 Roadmap created, Prelude 타입 어노테이션 부분 적용 완료
Resume file: None
Next action: Plan Phase 91 (Array 타입 어노테이션)
