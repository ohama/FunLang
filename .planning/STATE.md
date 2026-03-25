# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-25)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v3.0 COMPLETE — all phases done

## Current Position

Milestone: v3.0 Mutable Data Structures — COMPLETE
Phase: 41 of 41 complete
Plan: 1/1 in Phase 41
Status: v3.0 milestone complete
Last activity: 2026-03-25 — Phase 41 executed (4 flt tests for Array HOF builtins; 486/486 suite passing)

Progress: [████████████████████] v1.0-v2.2 done (37p/92pl) | v3.0: 6/6 plans DONE

## Performance Metrics

**Velocity:**
- Total plans completed: 95
- v2.2 Phases 36-37: 3 plans in 1 day
- v3.0 Phase 38: 2 plans in 1 day
- v3.0 Phase 39: 1 plan in ~15 min
- v3.0 Phase 40: 1 plan in ~2 min
- v3.0 Phase 41: 1 plan in ~5 min

**Recent Trend:**
- Trend: Stable, accelerating — v3.0 milestone complete

## Accumulated Context

### Decisions

(Full log in PROJECT.md Key Decisions table)

Key decisions relevant to v3.0:
- BuiltinValue DU for native F# functions — ArrayValue and HashtableValue follow same pattern
- Phase 38 and Phase 39 can execute in parallel (independent DU cases)
- ArrayValue uses Value array (no outer ref) — in-place element mutation via arr.[i] <- v; no need to replace whole array
- Array equality uses ReferenceEquals — two distinct arrays are never equal by value (matches F# mutable semantics)
- HashtableValue uses Dictionary<Value, Value> as backing store; reference equality (Phase 39)
- flt tests for mutable operations must use `let _ =` not `let () =` — unit pattern not valid at module level
- Prelude module wrappers do NOT use `open ModuleName` when the module exports names that conflict with other modules (e.g., Array.length vs List.length)
- OOB errors in builtins use LangThreeException (catchable by language try-with), not .NET exceptions
- TypeCheck.fs SubModules bug fixed: ModuleDecl must filter outer mods from innerMods before assigning SubModules
- callValueRef forward reference pattern: builtins that invoke user closures use a mutable ref wired after eval is defined (Phase 40)
- Multi-arg lambda `fun a b ->` is a parse error in LangThree; curried form `fun a -> fun b -> ...` required

### Pending Todos

None.

### Blockers/Concerns

None — v3.0 milestone complete.

## Session Continuity

Last session: 2026-03-25
Stopped at: Completed 41-01-PLAN.md — 4 Array HOF flt tests (TST-20); full 486-test suite green
Resume file: None

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-25 (41-01 complete: Array HOF flt tests; v3.0 milestone done; 486/486 flt tests passing)*
