# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-28)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v5.0 Imperative Ergonomics — Phase 47: Array and Hashtable Indexing (complete)

## Current Position

Milestone: v5.0 Imperative Ergonomics
Phase: 47 of 49 (Array and Hashtable Indexing)
Plan: 1 of 1 in current phase
Status: Phase complete
Last activity: 2026-03-28 — Completed 47-01-PLAN.md (.[i] indexing syntax + 7 flt tests, 570/570 passing)

Progress: [████████████████████] v1.0-v4.0 done (44p/103pl) | v5.0: [███░░] 3/5 phases

## Performance Metrics

**Velocity:**
- Total plans completed: 103
- v1.0-v2.2: 92 plans across 37 phases
- v3.0: 6 plans across 4 phases in 1 day
- v4.0: 5 plans across 3 phases in 1 day

## Accumulated Context

### Decisions

(Full log in PROJECT.md Key Decisions table)

Key context for v5.0:
- Grammar already has `atom '.' IDENT` for record field access — `.[` token needs careful lexer handling to avoid conflict
- [DONE] Semicolons now work as expression sequencing (`e1; e2`) via SeqExpr nonterminal
- [DONE] `let _ = e1 in e2` still valid; `e1; e2` is the new shorthand (desugars to same)
- `if` requires else branch (Ast.fs: `If of Expr * Expr * Expr * span: Span`) — AST needs update for optional else
- [DONE] while/for loops added (Phase 46) — `while cond do body` and `for i = s to/downto e do body`
- [DONE] for-loop variable is immutable (E0320 fires on assignment attempt)
- Multi-statement loop bodies require explicit `;` — newline-based implicit sequencing not implemented
- SeqExpr established: future statement-position grammar rules must use SeqExpr not Expr
- WHILE FOR TO DOWNTO DO are now reserved keywords
- [DONE] arr.[i] / ht.[key] indexing syntax added (Phase 47) — DOTLBRACKET token, IndexGet/IndexSet AST nodes
- Array builtins: array_create (not array_new), hashtable_create (not hashtable_new)
- IndexGet in Atom (left-recursive chaining); IndexSet in Expr (mirrors SetField)

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-03-28
Stopped at: Completed 47-01-PLAN.md — Phase 47 Array/Hashtable Indexing done
Resume file: None

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-28 (Phase 47 Array and Hashtable Indexing complete)*
