# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-31)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v10.0 Type Classes — Phase 70: Core Type Infrastructure

## Current Position

Milestone: v10.0 Type Classes
Phase: 70 of 74 (Core Type Infrastructure)
Plan: — (not yet planned)
Status: Ready to plan
Last activity: 2026-03-31 — roadmap created for v10.0 (5 phases, 13 requirements)

Progress: [████████████████████] v1.0-v9.1 done (69 phases, 150 plans)
         [░░░░░░░░░░░░░░░░░░░░] v10.0: 0% (phases 70-74)

## Performance Metrics

**Velocity:**
- Total plans completed: 150
- v9.1: 1 plan (phase 69) in 1 day
- v9.0: 7 plans across 2 phases in 1 day
- v8.1: 4 plans (phases 65-66) in 1 day
- v8.0: 5 plans across 2 phases in 1 day
- v7.1: 7 plans across 3 phases in 1 day

## Accumulated Context

### Decisions

(Full log in PROJECT.md Key Decisions table)

Key context carried into v10.0:
- Dictionary passing strategy chosen: constraints elaborated to explicit RecordValue dict args; evaluator stays type-class-unaware
- `ClassEnv`/`InstanceEnv` threaded as module-level mutable refs in `Bidir.fs` (same pattern as `mutableVars`) to avoid call-site explosion
- `Scheme` shape change to `Scheme(vars, constraints, ty)` is most invasive single change — do it first so F# exhaustive matching flags all incomplete sites immediately
- Constraint resolution deferred past unification (never resolve `TVar 1042` before it unifies) to avoid false "no instance" errors

### Pending Todos

None.

### Blockers/Concerns

- [Phase 71] Audit `where` keyword in `Lexer.fsl` before writing parser rules — may conflict with existing GADT syntax
- [Phase 72] `synth` evidence representation decision needed before coding: recommended mutable ref accumulator (like `mutableVars`) to avoid call-site explosion
- [Phase 72] GADT branch constraint isolation (LT-2): branch-local type refinements must be applied to constraints before they escape the branch

## Session Continuity

Last session: 2026-03-31
Stopped at: Roadmap created for v10.0 (5 phases, phases 70-74)
Resume file: None
Next action: `/gsd:plan-phase 70`

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-31 (v10.0 roadmap created)*
