# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-01)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** Planning next milestone

## Current Position

Milestone: v11.0 Typed AST Export
Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-04-03 — Milestone v11.0 started

Progress: [░░░░░░░░░░░░░░░░░░░░] v11.0 planning

## Performance Metrics

**Velocity:**
- Total plans completed: 161+
- v10.1: 4 phases (75-78) direct execution + 1 IndentFilter fix, 1 session
- v10.0: 11 plans across 5 phases in 1 day
- v9.1: 1 plan (phase 69) in 1 day

## Accumulated Context

### Decisions

Key cross-milestone context carried forward:
- Dictionary passing strategy: constraints elaborated to RecordValue dict args; evaluator type-class-unaware
- Scheme shape: Scheme(vars, constraints, ty) with mkScheme/schemeType helpers
- pendingConstraints mutable ref in Bidir.fs (same pattern as mutableVars)
- Constraint now carries SourceSpan for error location (v10.1)
- ModuleExports includes ClassEnv/InstanceEnv (v10.1)
- Instance methods promoted to outer scope from ModuleDecl during elaboration (v10.1)
- TEName for user-defined ADTs resolves to TData in instance processing (v10.1)
- TEConstrained annotations validated against ClassEnv in Bidir.synth (v10.1)
- AND_KW added to IndentFilter isContinuationStart + offside suppression (v10.1)
- Polymorphic instances need unification-based resolution (deferred to future)

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-01
Stopped at: v10.1 milestone complete
Resume file: None
Next action: /gsd:new-milestone

---
*State initialized: 2026-02-25*
*Last updated: 2026-04-01 (v10.1 milestone archived)*
