# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-03)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** Phase 84 — Attribute Infrastructure (v12.0 Infix Operator Reform)

## Current Position

Milestone: v12.0 Infix Operator Reform
Phase: 84 of 87 (Attribute Infrastructure)
Plan: 01 of 1 in phase (complete)
Status: Phase 84 complete
Last activity: 2026-04-03 — Completed 84-01-PLAN.md (attribute infrastructure)

Progress: [█░░░░░░░░░░░░░░░░░░░] v12.0 1/4 phases

## Performance Metrics

**Velocity:**
- Total plans completed: 168+
- v11.1: 1 phase, 1 plan in 1 day
- v11.0: 4 phases, 5 plans in 1 day
- v10.0: 11 plans across 5 phases in 1 day

## Accumulated Context

### Decisions

From Phase 84:
- InfixDecl treated as LetDecl in type-check/eval (attrs are metadata only until phase 85 reads them)
- Attribute test operators use $> and <$ (not |> and <|) because |> lexes as PIPE_RIGHT, not INFIXOP

From v11.1 (Phase 83):
- dbg builtin prints to stderr (eprintfn), not stdout
- hashtable_*_str builtins use monomorphic string key (Scheme([0]) not Scheme([0;1]))

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

1 pre-existing flt failure:
- tests/flt/error/err-occurs-check.flt

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-03
Stopped at: Completed 84-01-PLAN.md (attribute infrastructure)
Resume file: None
Next action: /gsd:plan-phase 85
