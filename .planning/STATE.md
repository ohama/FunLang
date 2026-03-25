# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-25)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v2.1 Bug Fixes & Test Hardening

## Current Position

Milestone: v2.1 Bug Fixes & Test Hardening
Phase: 33 of 35 (TCO Fix + Test Isolation)
Plan: 0 of 2 in Phase 33
Status: Ready to plan
Last activity: 2026-03-25 — Roadmap created for v2.1 (Phases 33-35)

Progress: v1.0-v2.0 (32p, 82pl) complete | v2.1: [░░░░░░░░░░] 0% (0/5 plans)

## Performance Metrics

**Velocity:**
- Total plans completed: 82
- v2.0 Phases 26-32: 14 plans in 2 days

**Recent Trend:**
- Trend: Stable, accelerating

## Accumulated Context

### Decisions

- [v2.0]: Requirements derived from FunLexYacc real-world usage (34 constraints)
- [Phase 30-02]: Expression LetRec uses BuiltinValue + mutable envRef (not FunctionValue) — FunctionValue fails inside lambda bodies due to trampoline losing self-binding
- [Phase 31-02]: Tests using shared mutable currentTypeCheckingFile/currentEvalFile must be wrapped in testSequenced to avoid parallel-execution race conditions
- [v2.1]: TCO broken by Phase 30 LetRec→BuiltinValue change (tailPos=false hardcoded in wrapper)
- [v2.1]: BuiltinValue signature (Value -> Value) cannot carry tailPos — structural limitation requiring workaround

### Pending Todos

None.

### Blockers/Concerns

- TCO regression: `let rec loop n = if n = 0 then 0 else loop (n - 1)` with 1M iterations → Stack overflow (root cause: Phase 30 BuiltinValue wrapping hardcodes tailPos=false)

## Session Continuity

Last session: 2026-03-25
Stopped at: v2.1 roadmap created — Phase 33 ready to plan
Resume file: None

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-25 (v2.1 roadmap written, Phases 33-35 defined)*
