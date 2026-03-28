---
phase: 42-core-mutable-variables
plan: "02"
subsystem: eval-typecheck
tags: [mutable-variables, eval, bidir, typecheck, ref-cells]
dependency-graph:
  requires: ["42-01"]
  provides: ["mutable-variable-eval", "mutable-variable-typecheck", "module-level-mutables"]
  affects: ["43-mutable-tests", "44-mutable-polish"]
tech-stack:
  added: []
  patterns: ["ref-cell-for-mutable-variables", "mutableVars-set-tracking"]
key-files:
  created: []
  modified:
    - src/LangThree/Eval.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/TypeCheck.fs
decisions:
  - id: "D42-02-01"
    summary: "Module-level mutable mutableVars set in Bidir.fs for tracking"
    rationale: "Simple, effective; save/restore pattern for expression-level scoping"
metrics:
  duration: "~8 min"
  completed: "2026-03-26"
---

# Phase 42 Plan 02: Eval + Type Checking for Mutable Variables Summary

**One-liner:** Ref-cell eval with transparent dereference, monomorphic Bidir synth for LetMut/Assign, module-level LetMutDecl type checking

## What Was Done

### Task 1: Eval -- LetMut, Assign, Var dereference, LetMutDecl
- `Var` case now dereferences `RefValue(r)` transparently (returns `r.Value`)
- `LetMut` creates a `ref` cell wrapping the value, stores `RefValue` in env
- `Assign` updates the ref cell and returns `TupleValue []` (unit)
- `LetMutDecl` at module level follows same ref-cell pattern
- Assigning to immutable variable raises runtime error

### Task 2: Bidir -- mutableVars threading + LetMut/Assign synth
- Added `mutableVars : Set<string>` module-level mutable for tracking
- `LetMut` synth: infers type, NO generalization (monomorphic only), saves/restores mutableVars across scope
- `Assign` synth: checks name is in mutableVars (raises E0320 if not), unifies assigned value type with variable type, returns `TTuple []`

### Task 3: TypeCheck -- module-level LetMutDecl + tree-walking
- `collectMatches`, `collectTryWiths`, `collectModuleRefs`, `rewriteModuleAccess` all handle `LetMut` and `Assign` nodes
- `LetMutDecl` in module type checking: monomorphic scheme, registers in `Bidir.mutableVars`
- `mutableVars` reset to `Set.empty` at start of `typeCheckModuleWithPrelude`

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| D42-02-01 | Module-level `mutable` set in Bidir.fs for mutableVars tracking | Simple save/restore pattern works for expression-level scoping; module-level set accessible from TypeCheck.fs |

## Deviations from Plan

None -- plan executed exactly as written.

## Verification Results

1. `dotnet build` -- 0 errors, 0 warnings
2. `dotnet test` -- 224/224 passed
3. Expression: `let mut x = 5 in let _ = x <- 10 in x` -> 10
4. Module: `let mut counter = 0` then two increments -> prints 2
5. Type error: `let x = 5 in x <- 10` -> E0320 ImmutableVariableAssignment
6. flt suite: 521/521 passed

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | e47cafb | feat(42-02): add eval support for LetMut, Assign, and LetMutDecl |
| 2 | 0b51d19 | feat(42-02): add bidirectional type checking for LetMut and Assign |
| 3 | e19cff0 | feat(42-02): add module-level LetMutDecl type checking and tree-walking |
