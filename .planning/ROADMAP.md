# Roadmap: LangThree v8.1 Mutual Recursion Completion

## Overview

v8.1 completes the mutual recursion story: first by fixing the LetRecDecl AST to preserve type annotations on first parameters (lost during v8.0 parser desugaring), then by implementing expression-level `let rec ... and ...` across all compiler layers. Two phases, building on 64 phases shipped across v1.0-v8.0.

## Milestones

- [archived] **v1.0-v8.0** - Phases 1-64 (shipped 2026-03-30)
- **v8.1 Mutual Recursion Completion** - Phases 65-66 (in progress)

## Phases

- [x] **Phase 65: LetRecDecl AST Refactoring** - Preserve first param type info in mutual recursion bindings
- [ ] **Phase 66: Expression-Level Mutual Recursion** - Full `let rec ... and ... in expr` support

## Phase Details

### Phase 65: LetRecDecl AST Refactoring
**Goal**: Module-level `let rec ... and ...` correctly preserves and verifies first parameter type annotations
**Depends on**: Nothing (first phase of v8.1)
**Requirements**: AST-01, AST-02, AST-03
**Success Criteria** (what must be TRUE):
  1. `let rec f (x : int) y = ... and g (z : bool) = ...` at module level compiles without losing the `int` and `bool` annotations
  2. Type checker rejects `let rec f (x : int) = x ^^ "hi"` with a type error (annotation is actually enforced, not silently dropped)
  3. All existing `let rec ... and ...` flt tests continue to pass (no regression from AST change)
  4. `dotnet build` succeeds with no warnings from exhaustive pattern match on the changed AST node
**Plans**: 2 plans

Plans:
- [x] 65-01-PLAN.md — AST definition change + parser + all 28 mechanical pattern-match site updates
- [x] 65-02-PLAN.md — Type checker enforcement logic + flt tests for annotation enforcement

### Phase 66: Expression-Level Mutual Recursion
**Goal**: Users can write `let rec f x = ... and g y = ... in expr` inside any expression context with full type annotation support
**Depends on**: Phase 65 (builds on corrected AST design for param type preservation)
**Requirements**: EXPR-01, EXPR-02, EXPR-03, EXPR-04, EXPR-05, EXPR-06
**Success Criteria** (what must be TRUE):
  1. `let rec even n = if n = 0 then true else odd (n - 1) and odd n = if n = 0 then false else even (n - 1) in even 10` evaluates to `true`
  2. Expression-level mutual recursion works with type annotations: `let rec f (x : int) : bool = ... and g (y : bool) : int = ... in f 42`
  3. Three or more mutually recursive bindings work: `let rec a x = ... and b y = ... and c z = ... in a 1`
  4. Type checker rejects type mismatches in mutual recursive bindings (e.g., calling `g` with wrong argument type)
  5. Expression-level `let rec ... and ... in` works nested inside other expressions (inside function bodies, match arms, etc.)
**Plans**: 2 plans

Plans:
- [ ] 66-01-PLAN.md — AST node change + parser grammar + mechanical pattern-match fixes
- [ ] 66-02-PLAN.md — Type checking/eval logic for multi-binding + flt tests

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 65. LetRecDecl AST Refactoring | 2/2 | ✓ Complete | 2026-03-31 |
| 66. Expression-Level Mutual Recursion | 0/2 | Not started | - |
