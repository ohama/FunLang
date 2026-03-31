# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-01)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v10.1 — Type Class Error Reporting & Module Integration

## Current Position

Milestone: v10.1
Phase: 75 — E0701 Source Location Fix
Plan: N/A (direct execution)
Status: In progress
Last activity: 2026-04-01 -- v10.1 started

Progress: [████████████████████] v1.0-v10.0 done (74 phases, 161 plans)

## Performance Metrics

**Velocity:**
- Total plans completed: 161
- v10.0: 11 plans across 5 phases in 1 day
- v9.1: 1 plan (phase 69) in 1 day
- v9.0: 7 plans across 2 phases in 1 day

## Accumulated Context

### Decisions

Key cross-milestone context carried forward:
- Dictionary passing strategy: constraints elaborated to RecordValue dict args; evaluator type-class-unaware
- Scheme shape: Scheme(vars, constraints, ty) with mkScheme/schemeType helpers
- pendingConstraints mutable ref in Bidir.fs (same pattern as mutableVars)
- Constraint resolution deferred past unification
- elaborateTypeclasses desugars InstanceDecl → LetDecl before eval
- Polymorphic instances need unification-based resolution (deferred to v10.1+)

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-01
Stopped at: Starting v10.1 Phase 75
Resume file: None
Next action: Fix Constraint type to carry Span, thread through Bidir.fs

---
*State initialized: 2026-02-25*
*Last updated: 2026-04-01 (v10.1 started)*
