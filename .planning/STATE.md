# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-03)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records, Type Classes)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** Planning next milestone

## Current Position

Milestone: (none — planning next)
Phase: N/A
Plan: N/A
Status: v12.0 complete, ready for next milestone
Last activity: 2026-04-03 — v12.0 Infix Operator Reform milestone archived

Progress: N/A (between milestones)

## Performance Metrics

**Velocity:**
- Total plans completed: 170+
- v11.1: 1 phase, 1 plan in 1 day
- v11.0: 4 phases, 5 plans in 1 day
- v10.0: 11 plans across 5 phases in 1 day

## Accumulated Context

### Decisions

From Phase 86:
- |>, >>, << are now Prelude-defined operators with #[left 1], #[right 2], #[left 2] fixity
- Prelude operator params use __prefix_ names (e.g. __comp_lhs) to avoid applyFunc closure overwrite
- applyFunc self-name injection guarded: only injects when name NOT already in closure env
- compose function kept in Prelude (referenced by prelude-compose.flt test)

From Phase 85:
- defaultFixity prec mapping: INFIXOP0=4, INFIXOP1=5, INFIXOP2=6, INFIXOP3=7, INFIXOP4=8
- flattenInfixChain only flattens same-default-prec operators (same LALR level)
- Mixed-op precedence climbing works in Phase 85 without Pratt parser
- `$:` operator (dollar + colon) is lexer-incompatible; use `$>` etc. for $ + symbol operators
- rewriteFixity called with combined env (prelude + current file fixity)

From Phase 84:
- InfixDecl treated as LetDecl in type-check/eval (attrs are metadata only until phase 85 reads them)
- Attribute test operators use $> and <$ (not |> and <|) — NOTE: |> now lexes as INFIXOP0 since Phase 86

From v11.1 (Phase 83):
- dbg builtin prints to stderr (eprintfn), not stdout
- hashtable_*_str builtins use monomorphic string key (Scheme([0]) not Scheme([0;1]))

### Pending Todos

3 low-severity bugs deferred from v10.1:
- Bug 6: Typeclass redeclaration silently ignored (intentional for Prelude)
- Bug 9: E0701 shows internal type variable for indirect polymorphic constraint
- Bug 10: E0704 never fires (E0301 used instead, functionally correct)

0 pre-existing flt failures (err-occurs-check fixed in v12.0)

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-03
Stopped at: v12.0 milestone archived
Resume file: None
Next action: /gsd:new-milestone
