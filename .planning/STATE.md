# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-03)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** Planning next milestone

## Current Position

Milestone: v11.1 Builtin Compatibility
Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-04-03 — Milestone v11.1 started

Progress: [░░░░░░░░░░░░░░░░░░░░] v11.1 planning

## Performance Metrics

**Velocity:**
- Total plans completed: 168+
- v11.0: 4 phases, 5 plans in 1 day
- v10.1: 4 phases direct execution in 1 session
- v10.0: 11 plans across 5 phases in 1 day

## Accumulated Context

### Decisions

Carried forward from v11.0:
- annotationMap is ConcurrentDictionary<Span, Type> (parallel test safety)
- TypeAnnotationMap.record skips unknownSpan
- ExportApi.typeCheckFile raises on error (matches codebase style)
- --emit-typed-ast filters annotations by FileName, bindings by excluding builtins+prelude
- parseModuleFromString duplicated in ExportApi.fs (3rd copy, established pattern)

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
Stopped at: v11.0 milestone archived
Resume file: None
Next action: /gsd:new-milestone
