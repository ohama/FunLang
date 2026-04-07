# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-07)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v13.0 Uniform Tagged Representation

## Current Position

Milestone: v13.0 Uniform Tagged Representation
Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-04-07 — Milestone v13.0 started

Progress: N/A (requirements phase)

## Performance Metrics

**Velocity:**
- Total plans completed: 170+
- v12.0: 4 phases, 4 plans in 1 day
- v11.1: 1 phase, 1 plan in 1 day
- v11.0: 4 phases, 5 plans in 1 day

## Accumulated Context

### Decisions

From v12.0 (Phase 84-87):
- |>, >>, << are now Prelude-defined operators with #[left 1], #[left 2], #[right 2] fixity
- Prelude operator params use __prefix_ names (e.g. __comp_lhs) to avoid applyFunc closure overwrite
- applyFunc self-name injection guarded: only injects when name NOT already in closure env

From v11.1 (Phase 83):
- dbg builtin prints to stderr (eprintfn), not stdout
- hashtable_*_str builtins use monomorphic string key (Scheme([0]) not Scheme([0;1]))

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-07
Stopped at: Defining requirements for v13.0
Resume file: None
Next action: Define requirements → create roadmap
