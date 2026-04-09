# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-09)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v15.0 Type Class Maturity

## Current Position

Milestone: v15.0 Type Class Maturity
Phase: 96 — Correctness Foundations (not started)
Plan: —
Status: Roadmap defined, ready for Phase 96
Last activity: 2026-04-09 — Roadmap created, phases 96–101 defined

Progress: [..........] 0% (0/6 phases complete)

## Phase Summary

| Phase | Goal | Status |
|-------|------|--------|
| 96 — Correctness Foundations | Reliable type class infrastructure (TC-01–04) | Pending |
| 97 — Superclass Entailment | Superclass chain resolution (SC-01–03) | Pending |
| 98 — Constrained Instance Runtime Dispatch | Constrained instances evaluate correctly (CD-01–02) | Pending |
| 99 — Num Type Class | Num/Eq Prelude additions (NUM-01–03) | Pending |
| 100 — Derive for Parameterized ADTs | derive works on parameterized types (DRV-01–02) | Pending |
| 101 — Error Message Polish | Actionable type class errors (ERR-01–03) | Pending |

## Performance Metrics

**Velocity:**
- Total plans completed: 175+
- v14.0: 5 phases in 1 day (2026-04-08)
- v12.0: 4 phases, 4 plans in 1 day

**Test baseline (start of v15.0):**
- 724 flt tests passing
- 244 F# unit tests passing

## Accumulated Context

### Decisions

From v14.0 (Phase 91-95):
- Prelude 함수에서 `fun x ->` 패턴을 직접 인자로 펼침 완료
- 모든 Prelude 함수에 타입 어노테이션 추가
- OccursCheck 에러 메시지에 formatTypeNormalized 적용

From v10.0-v10.1 (Type Classes):
- typeclass/instance 선언, 제약 추론, 딕셔너리 elaboration
- Show/Eq 내장 인스턴스 (int/bool/string/char)
- ClassEnv/InstanceEnv export, instance method 승격

From v15.0 research (2026-04-08):
- Constrained instance runtime dispatch is broken: Elaborate.fs flattens all instance methods to literal names, so multiple instances of the same class collide. Name mangling needed for InstanceVars != [] instances only.
- Phase 98 (name mangling) is highest-risk: monomorphic instances must keep literal names.
- +/-/* operator dispatch must NOT be migrated to Num in v15.0 — 724-test regression risk.
- Exact mangling scheme must be finalized before Phase 98 implementation begins (e.g., `show__Show_list`).

### Pending Todos

3 low-severity bugs deferred from v10.1 (now addressed in v15.0 roadmap):
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude — keep as-is)
- Bug 9: E0701 shows internal type variable — addressed in TC-04 (Phase 96) and ERR-01 (Phase 101)
- Bug 10: E0704 never fires — addressed in ERR-02 (Phase 101)

### Blockers/Concerns

- Phase 98 design gap: exact Eval.fs dispatch mechanism for mangled names at call sites needs a concrete decision before Phase 98 planning. The TypeAnnotationMap approach (Elaborate.fs rewriting call sites before evaluation) is the proposed direction.
- Phase 100 gap: verify whether `TypeDecl.deriving` (already parsed) is wired to the same code generation as `DerivingDecl`. Low-effort investigation needed before Phase 100 planning.

## Session Continuity

Last session: 2026-04-09
Stopped at: v15.0 roadmap created — phases 96–101 defined, ready to plan Phase 96
Resume file: None
Next action: `/gsd:plan-phase 96`
