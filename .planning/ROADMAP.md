# Roadmap: v12.0 Infix Operator Reform

## Overview

Reform FunLang's hardcoded infix operator handling by introducing a `#[left N]` / `#[right N]` attribute system and a Pratt post-processor. The three special-cased operators (`|>`, `>>`, `<<`) become ordinary Prelude definitions, their dedicated AST nodes and tokens are removed, and the full regression suite confirms no behavior change.

## Milestones

- **v12.0 Infix Operator Reform** — Phases 84-87 (in progress)

## Phase Details

### Phase 84: Attribute Infrastructure

**Goal**: The compiler can parse and attach fixity attributes to operator let-declarations
**Depends on**: Phase 83 (v11.1 complete)
**Requirements**: ATTR-01, ATTR-02
**Success Criteria** (what must be TRUE):
  1. `#[left 6]` and `#[right 6]` lex without conflicts and produce an ATTR_OPEN token
  2. Parser accepts attribute lists before `let` declarations without parse error
  3. An operator defined with `#[left N] let (|>) ...` has the attribute recorded in its AST LetDecl node
  4. Source files without attributes parse identically to before (no regression)
**Plans**: 1 plan

Plans:
- [x] 84-01-PLAN.md — Lexer ATTR_OPEN token + Parser attribute grammar + AST InfixDecl + flt tests

---

### Phase 85: Fixity System

**Goal**: User-defined fixity attributes control operator precedence and associativity at runtime
**Depends on**: Phase 84
**Requirements**: FIX-01, FIX-02, FIX-03
**Success Criteria** (what must be TRUE):
  1. FixityEnv is populated from Prelude operator attributes when the interpreter starts
  2. An infix chain `a |> f |> g` is restructured into the correct left-associative tree by the Pratt post-processor
  3. Operators with no attribute fall back to the existing first-character precedence rules (INFIXOP0-4 behavior preserved)
  4. A custom user-defined infix operator with `#[right 5]` binds right-to-left at precedence 5
**Plans**: 1 plan

Plans:
- [x] 85-01-PLAN.md — FixityEnv module + pipeline threading + post-parse rewrite + flt tests (FIX-01, FIX-02, FIX-03)

---

### Phase 86: Operator Migration

**Goal**: `|>`, `>>`, `<<` are ordinary Prelude functions; all special compiler infrastructure for them is deleted
**Depends on**: Phase 85
**Requirements**: MIG-01, MIG-02, MIG-03
**Success Criteria** (what must be TRUE):
  1. Prelude/Core.fun defines `|>`, `>>`, `<<` with correct `#[left N]` / `#[right N]` attributes
  2. The compiler builds with no reference to PipeRight, ComposeRight, or ComposeLeft AST nodes
  3. PIPE_RIGHT, COMPOSE_RIGHT, COMPOSE_LEFT tokens are absent from the Lexer and IndentFilter
  4. Existing programs using `|>`, `>>`, `<<` produce identical output to before the migration
**Plans**: TBD

Plans:
- [ ] 86-01: Prelude/Core.fun — add `#[...]` operator definitions for `|>`, `>>`, `<<` (MIG-01)
- [ ] 86-02: Remove PipeRight/ComposeRight/ComposeLeft AST nodes from Ast, Eval, Bidir, Infer, TypeCheck, Format (MIG-02)
- [ ] 86-03: Remove PIPE_RIGHT/COMPOSE_RIGHT/COMPOSE_LEFT tokens from Lexer and IndentFilter (MIG-03)

---

### Phase 87: Verification

**Goal**: The full test suite confirms zero regressions and TCO is preserved through `|>` chains
**Depends on**: Phase 86
**Requirements**: VER-01, VER-02
**Success Criteria** (what must be TRUE):
  1. All 714 flt integration tests pass with no new failures
  2. A deep `|>` chain (100+ levels) completes without stack overflow, confirming TCO is intact
  3. The pre-existing `tests/flt/error/err-occurs-check.flt` failure is the only known skip (unchanged from v11.1)
**Plans**: TBD

Plans:
- [ ] 87-01: Run full flt suite, document results, add TCO deep-pipe test (VER-01, VER-02)

---

## Progress

**Execution Order:** 84 → 85 → 86 → 87

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 84. Attribute Infrastructure | v12.0 | 1/1 | Complete | 2026-04-03 |
| 85. Fixity System | v12.0 | 1/1 | ✅ Complete | 2026-04-03 |
| 86. Operator Migration | v12.0 | 0/3 | Not started | - |
| 87. Verification | v12.0 | 0/1 | Not started | - |
