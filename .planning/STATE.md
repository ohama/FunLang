# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-24)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v2.0 Practical Language Completion

## Current Position

Milestone: v2.0 Practical Language Completion
Phase: 29 complete, next: 30
Plan: —
Status: Phase 29 verified, ready to plan Phase 30
Last activity: 2026-03-24 — Phase 29 executed and verified

Progress: v1.0-v1.8 (25p, 68pl) complete | v2.0 [████░░░░░░] 4/7 phases (7 plans)

## Performance Metrics

**Velocity:**
- Total plans completed: 75
- Phase 26: 2 plans in ~6 min (parallel wave)
- Phase 27: 2 plans in ~7 min (parallel wave)
- Phase 28: 1 plan in ~11 min
- Phase 29: 2 plans in ~20 min (29-02 folded into 29-01)

**Recent Trend:**
- v2.0 Phase 27: 1 phase, 2 plans, <1 hour
- v2.0 Phase 28: 1 phase, 1 plan, <1 hour
- v2.0 Phase 29: 1 phase, 2 plans, <1 hour
- Trend: Stable

## Accumulated Context

### Decisions

- [v2.0]: Requirements derived from FunLexYacc real-world usage (34 constraints)
- [v2.0]: Module system (import/scoping) is heaviest work, placed late
- [Phase 26]: `option`/`result` alias via Elaborate.fs TEData normalization (not grammar change)
- [Phase 26]: Prelude path uses 3-stage search: CWD → assembly dir → walk-up 6 levels
- [Phase 26]: `failwith` uses LangThreeException + polymorphic return Scheme([0], TArrow(TString, TVar 0))
- [Phase 26]: Whitespace-only input guard placed before parse (not after)
- [Phase 27-01]: BracketDepth uses `max 0 (depth - 1)` on close to guard against underflow
- [Phase 27-01]: Guarded NEWLINE arm (BracketDepth > 0) must appear before unguarded arm in F# match
- [Phase 27-02]: SYN-03 trailing semicolon via Expr SEMICOLON production in SemiExprList (between single and recursive)
- [Phase 27-02]: SYN-04 list literal patterns via SemiPatList nonterminal + desugarListPat in parser header
- [Phase 28-01]: LetPatDecl uses cEnv (not Map.empty) in inferPattern - matches Bidir.fs LetPat pattern
- [Phase 28-01]: `string` is TYPE_STRING keyword token - cannot use as function in test files
- [Phase 29-01]: Unify.fs must have explicit `TX, TX -> empty` case for every new Type DU variant
- [Phase 29-01]: MatchCompile.fs needs CharConst in patternToConstructor and CharValue in matchesConstructor
- [Phase 29-01]: char literal rules placed BEFORE type_var in Lexer.fsl (longest-match: 'A' 3 chars > 'a 2 chars)
- [Phase 29-01]: int_to_char ASCII-only (0-127); string comparisons use System.String.CompareOrdinal
- [Phase 29-01]: Comparison widening uses synth-then-unify-then-match (not inferBinaryOp - hardcoded single type)

### Pending Todos

None (expression-level let rec addressed by SYN-01 in Phase 30).

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-03-24
Stopped at: Phase 29 complete, ready to plan Phase 30
Resume file: None

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-24 (Phase 29 complete)*
