# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-31)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v10.0 Type Classes — Phase 71: Parsing and AST

## Current Position

Milestone: v10.0 Type Classes
Phase: 71 of 74 (Parsing and AST) — COMPLETE
Plan: 2 of 2 complete
Status: Phase complete — ready for Phase 72
Last activity: 2026-03-31 — Completed 71-02-PLAN.md (typeclass integration tests)

Progress: [████████████████████] v1.0-v9.1 done (69 phases, 150 plans)
         [████░░░░░░░░░░░░░░░░] v10.0: 20% (phases 70+71 complete)

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

From Phase 70 Plan 01:
- `InstanceInfo` defined WITHOUT `MethodBodies` for Phase 70 (avoids circular dep with Expr); Phase 71 adds bodies
- Tasks 1+2 committed together (T1 alone won't compile due to F# exhaustive matching)
- `mkScheme`/`schemeType` helpers added as zero-cost backward-compat for Phase 71+ gradual migration

From Phase 70 Plan 02:
- `typeCheckModuleWithPrelude` now accepts `preludeClassEnv: ClassEnv` and `preludeInstEnv: InstanceEnv` (same threading pattern as CtorEnv/RecEnv)
- `PreludeResult` has `ClassEnv`/`InstEnv` fields; `loadPrelude` accumulates them per-file
- `loadAndTypeCheckFileImpl` (file import handler) passes `Map.empty` for both — file imports don't declare typeclasses yet
- Test helpers in GadtTests.fs and ModuleTests.fs updated alongside (auto-fixed blocking deviation)

### Pending Todos

None.

From Phase 71 Plan 01:
- FATARROW (`=>`) token placed after LARROW (`<-`) in Lexer.fsl — before the `op_char op_char+` catch-all, no conflict with GE (`>=`)
- ConstraintList grammar uses bare `IDENT TYPE_VAR` (not parenthesized); FATARROW lookahead disambiguates from AtomicType in LALR(1) — no conflicts
- Tasks 1+2 committed together — new Decl variants make TypeCheck/Eval pattern matches non-exhaustive if committed separately
- formatToken in Format.fs must always cover all Parser.token variants (new tokens need entries there too)

From Phase 71 Plan 02:
- TypeClassMethod syntax uses leading PIPE: `| methodName : type` (not `methodName : type`) — IDENT after TypeExpr shifts as type application (163 silent LALR conflicts), PIPE is unambiguous
- TYPECLASS/INSTANCE bodies get InModule context in IndentFilter (not InExprBlock) — prevents spurious IN injection for instance method LET and SEMICOLON between typeclass methods
- flt integration tests: one test case per file (5 files created in tests/flt/file/typeclass/)

### Blockers/Concerns

- [Phase 71] `where` keyword audit RESOLVED — `where` not used anywhere in Lexer.fsl, no conflict
- [Phase 72] `synth` evidence representation decision needed before coding: recommended mutable ref accumulator (like `mutableVars`) to avoid call-site explosion
- [Phase 72] GADT branch constraint isolation (LT-2): branch-local type refinements must be applied to constraints before they escape the branch

## Session Continuity

Last session: 2026-03-31T00:40:00Z
Stopped at: Completed 71-02-PLAN.md (typeclass integration tests)
Resume file: None
Next action: Execute Phase 72 (type inference for type classes)

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-31 (phase 71 plan 01 complete)*
