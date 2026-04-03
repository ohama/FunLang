# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-02)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** Phase 79 — Type Annotation Infrastructure

## Current Position

Milestone: v11.0 Typed AST Export
Phase: 79 of 82 (Type Annotation Infrastructure)
Plan: 1 of ? (79-01 complete)
Status: In progress
Last activity: 2026-04-03 — Completed 79-01-PLAN.md

Progress: [█░░░░░░░░░░░░░░░░░░░] v11.0 (1/? plans)

## Performance Metrics

**Velocity:**
- Total plans completed: 161+
- v10.1: 4 phases (75-78) direct execution + 1 IndentFilter fix, 1 session
- v10.0: 11 plans across 5 phases in 1 day
- v9.1: 1 plan (phase 69) in 1 day

## Accumulated Context

### Decisions

Key cross-milestone context carried forward:
- pendingConstraints mutable ref in Bidir.fs (same pattern as mutableVars) — v11.0 uses same pattern for annotation map (now implemented)
- annotationMap declared in Bidir.fs at position 2.6; reset at both TypeCheck entry points — 79-02 will populate it in synth
- TypeAnnotationMap.record skips unknownSpan (synthetic elaboration nodes must not pollute map)
- Constraint now carries SourceSpan for error location (v10.1) — Span is already threaded through synth
- ModuleExports includes ClassEnv/InstanceEnv (v10.1) — TypeEnv already available at typeCheckModuleWithPrelude return
- Pre/post-elaboration AST mismatch is key risk: elaborateTypeclasses rewrites AST before Bidir runs

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-03
Stopped at: Completed 79-01-PLAN.md (TypeAnnotationMap module + Bidir mutable + TypeCheck resets)
Resume file: None
Next action: Execute 79-02 (populate annotationMap in Bidir.synth)
