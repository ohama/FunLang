# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-02)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** Phase 80 — Type Environment Export

## Current Position

Milestone: v11.0 Typed AST Export
Phase: 80 of 82 (Type Environment Export)
Plan: —
Status: Ready to plan
Last activity: 2026-04-03 — Phase 79 complete (Type Annotation Infrastructure)

Progress: [█████░░░░░░░░░░░░░░░] v11.0 (1/4 phases)

## Performance Metrics

**Velocity:**
- Total plans completed: 163+
- v10.1: 4 phases (75-78) direct execution + 1 IndentFilter fix, 1 session
- v10.0: 11 plans across 5 phases in 1 day
- v9.1: 1 plan (phase 69) in 1 day

## Accumulated Context

### Decisions

Key cross-milestone context carried forward:
- pendingConstraints mutable ref in Bidir.fs (same pattern as mutableVars) — v11.0 uses same pattern for annotation map (now implemented)
- annotationMap is ConcurrentDictionary<Span, Type> (upgraded from Dictionary for parallel test safety in 79-02)
- TypeAnnotationMap.record skips unknownSpan (synthetic elaboration nodes must not pollute map)
- annotationMap populated at ALL ~65 synth return points + GADT check arm; Phase 80 can read it directly after typeCheckModuleWithPrelude
- Constraint now carries SourceSpan for error location (v10.1) — Span is already threaded through synth
- ModuleExports includes ClassEnv/InstanceEnv (v10.1) — TypeEnv already available at typeCheckModuleWithPrelude return
- Pre/post-elaboration AST mismatch is key risk: elaborateTypeclasses rewrites AST before Bidir runs
- Two-lexbuf test helper produces ASTs where all spans are identical {FileName="" L=0 C=0} — annotationMap tests use testSequenced and check Count>=1

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

1 pre-existing flt failure:
- tests/flt/error/err-occurs-check.flt — pre-existing, unrelated to Phase 79

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-03
Stopped at: Phase 79 complete, ready to plan Phase 80
Resume file: None
Next action: /gsd:plan-phase 80
