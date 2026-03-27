# Roadmap: LangThree v4.0 Mutable Variables

## Milestones

- v1.0-v3.0: Phases 1-41 (shipped 2026-03-25)
- v4.0 Mutable Variables: Phases 42-44 (in progress)

## Overview

v4.0 adds `let mut` mutable variable declarations and `<-` reassignment to LangThree, enabling imperative-style programming alongside the existing functional core. The work proceeds from core language mechanics (parser, AST, eval, typechecker) through edge cases and error handling, finishing with comprehensive test coverage and documentation.

## Phases

<details>
<summary>v1.0-v3.0 (Phases 1-41) -- SHIPPED 2026-03-25</summary>

41 phases, 98 plans completed. See git history for details.

</details>

### v4.0 Mutable Variables (In Progress)

**Milestone Goal:** `let mut` mutable variable declaration and `<-` reassignment with full type safety

- [x] **Phase 42: Core Mutable Variables** - AST, Parser, Eval, TypeCheck for let mut and x <- expr
- [ ] **Phase 43: Edge Cases and Error Handling** - Closure capture, immutability enforcement, parameter safety
- [ ] **Phase 44: Tests and Documentation** - flt test suite and tutorial chapter

## Phase Details

### Phase 42: Core Mutable Variables
**Goal**: Users can declare mutable variables with `let mut` and reassign them with `<-` at both expression and module level
**Depends on**: Phase 41 (v3.0 complete)
**Requirements**: MUT-01, MUT-02, MUT-03, MUT-05
**Success Criteria** (what must be TRUE):
  1. User can write `let mut x = 5` in an expression body and later read `x` to get the current value
  2. User can write `let mut x = 0` at module level and the variable persists across subsequent module-level expressions
  3. User can write `x <- 10` to reassign a mutable variable and the expression returns unit
  4. Type checker correctly infers and tracks mutable variable types without annotation
**Plans**: 2 plans

Plans:
- [x] 42-01-PLAN.md — AST nodes, parser grammar, diagnostics, formatting, inference stubs
- [x] 42-02-PLAN.md — Eval, Bidir type checking, TypeCheck module-level support

### Phase 43: Edge Cases and Error Handling
**Goal**: Mutable variable system correctly rejects invalid operations and handles advanced scenarios like closure capture
**Depends on**: Phase 42
**Requirements**: MUT-04, MUT-06, MUT-07, MUT-08
**Success Criteria** (what must be TRUE):
  1. Reassigning an immutable variable with `<-` produces a clear type error (not a runtime crash)
  2. Reassigning a mutable variable with a different type produces a type mismatch error
  3. A closure that captures an outer `let mut` variable can both read its current value and write a new value
  4. Attempting `<-` on a function parameter produces a type error
**Plans**: 1 plan

Plans:
- [ ] 43-01-PLAN.md — flt tests for error diagnostics (E0320, E0301) and closure capture

### Phase 44: Tests and Documentation
**Goal**: Comprehensive flt test coverage for all mutable variable scenarios and a tutorial chapter for users
**Depends on**: Phase 43
**Requirements**: TST-24, TST-25, TST-26, TST-27
**Success Criteria** (what must be TRUE):
  1. flt tests cover basic mutable variable operations: declaration, reassignment, reading updated values
  2. flt tests cover error cases: immutable variable reassignment error, type mismatch error on reassignment
  3. flt tests cover advanced scenarios: closure capture of mutable variables, nested let mut, offside rule with let mut
  4. Tutorial chapter explains mutable variables with progressive examples from basic to advanced usage
**Plans**: TBD

Plans:
- [ ] 44-01: TBD
- [ ] 44-02: TBD

## Progress

**Execution Order:** 42 -> 43 -> 44

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 42. Core Mutable Variables | v4.0 | 2/2 | Complete | 2026-03-27 |
| 43. Edge Cases and Error Handling | v4.0 | 0/1 | Not started | - |
| 44. Tests and Documentation | v4.0 | 0/TBD | Not started | - |

---
*Roadmap created: 2026-03-26*
