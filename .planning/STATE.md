# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-02)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v11.0 Complete

## Current Position

Milestone: v11.0 Typed AST Export
Phase: 82 of 82 (CLI Integration)
Plan: 01 of 01
Status: Milestone complete
Last activity: 2026-04-03 — Completed 82-01-PLAN.md (CLI Integration)

Progress: [████████████████████] v11.0 (4/4 phases)

## Performance Metrics

**Velocity:**
- Total plans completed: 163+
- v10.1: 4 phases (75-78) direct execution + 1 IndentFilter fix, 1 session
- v10.0: 11 plans across 5 phases in 1 day
- v9.1: 1 plan (phase 69) in 1 day

## Accumulated Context

### Decisions

Key cross-milestone context carried forward:
- --emit-typed-ast (Phase 82): serializeTypedModule filters annotations by FileName=absUserFile; filters bindings by excluding both BuiltinSchemes (initialTypeEnv) AND prelude.TypeEnv — plan only said exclude BuiltinSchemes but prelude also needed exclusion
- --emit-typed-ast output format: {"annotations":[{"span":{"startLine","startCol","endLine","endCol"},"type":"..."}],"bindings":{"name":"scheme"}}

- ExportApi.typeCheckFile (Phase 81) uses position-tracked parseModuleFromString to preserve accurate Span data; AnnotationMap snapshot taken immediately after typeCheckModuleWithPrelude to avoid races
- BindingEnv = TypeEnv type alias (identity wrapper exportBindingEnv) in TypeCheck.fs — Phase 81 uses this for ExportApi surface
- typeCheckModule returns (warnings, recEnv, modules, typeEnv) — typeEnv includes initialTypeEnv builtins + user bindings
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

Last session: 2026-04-03T03:42:47Z
Stopped at: Completed 82-01-PLAN.md (milestone v11.0 complete)
Resume file: None
Next action: Plan v12.0 or next milestone
