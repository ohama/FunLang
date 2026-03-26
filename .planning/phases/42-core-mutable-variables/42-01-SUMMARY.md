---
phase: 42-core-mutable-variables
plan: 01
subsystem: language-core
tags: [ast, parser, mutable-variables, diagnostics]
depends_on:
  requires: []
  provides: [LetMut-ast, Assign-ast, LetMutDecl-ast, RefValue, ImmutableVariableAssignment-diagnostic, mut-keyword]
  affects: [42-02]
tech_stack:
  added: []
  patterns: [ref-cell-for-mutable-values]
key_files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Diagnostic.fs
    - src/LangThree/Format.fs
    - src/LangThree/Infer.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Lexer.fsl
decisions:
  - id: D42-01-01
    decision: "Added 'mut' as keyword alias for 'mutable' in lexer so both `let mut x = 5` and `let mutable x = 5` work"
    reason: "Plan examples use `let mut` but lexer only had `mutable`. Both forms are natural for users."
metrics:
  duration: ~10min
  completed: 2026-03-26
---

# Phase 42 Plan 01: AST + Parser + Stubs for Mutable Variables Summary

AST nodes (LetMut, Assign, LetMutDecl, RefValue), parser grammar for `let mut`/`let mutable` and `x <- expr`, diagnostic E0320, and stub cases in Format/Infer/Eval.

## What Was Done

### Task 1: AST nodes + Diagnostic error kind
- Added `LetMut`, `Assign` to Expr type and `LetMutDecl` to Decl type in Ast.fs
- Added `RefValue` variant to Value type with GetHashCode, valueEqual, valueCompare support
- Updated `spanOf` and `declSpanOf` for new AST variants
- Added `ImmutableVariableAssignment` error kind (E0320) to Diagnostic.fs with formatting

### Task 2: Parser grammar + Format + Infer stubs
- Added 3 expression-level `let mutable/mut` rules (inline, indented, standalone)
- Added `IDENT LARROW Expr` assignment rule at Expr level
- Added 2 module-level `LetMutDecl` rules (simple, indented)
- Added `mut` as keyword alias for `MUTABLE` in Lexer.fsl
- Added formatAst/formatDecl cases for LetMut, Assign, LetMutDecl in Format.fs
- Added formatValue case for RefValue in Eval.fs (transparent deref)
- Added inferWithContext stubs for LetMut and Assign in Infer.fs

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing functionality] Added 'mut' keyword alias in Lexer.fsl**
- **Found during:** Task 2 verification
- **Issue:** Plan examples use `let mut x = ...` but lexer only mapped `"mutable"` to MUTABLE token. `mut` was lexed as IDENT, causing `let mut x = 5 in x` to parse as `let mut x = (5 in x)` (treating `mut` as binding name).
- **Fix:** Added `"mut" { MUTABLE }` rule in Lexer.fsl
- **Files modified:** src/LangThree/Lexer.fsl
- **Commit:** 90f0db8

**2. [Rule 2 - Missing functionality] Added RefValue formatValue case in Eval.fs**
- **Found during:** Task 2
- **Issue:** Plan specified adding formatValue for RefValue in Format.fs, but formatValue is defined in Eval.fs, not Format.fs
- **Fix:** Added the case in Eval.fs where formatValue actually lives
- **Files modified:** src/LangThree/Eval.fs
- **Commit:** 90f0db8

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| D42-01-01 | Added `mut` as keyword alias for `mutable` | Plan examples use `let mut` syntax; both forms work |

## Verification Results

- Build: 0 errors, 4 warnings (incomplete matches in Bidir/TypeCheck/Eval -- expected, fixed in Plan 02)
- Parser conflicts: 490 S/R, 294 R/R (up from 437/270 baseline -- new rules, resolved by default shift preference)
- Tests: 224/224 passed
- `let mut x = 5 in x` parses to `LetMut ("x", Number 5, Var "x")`
- `let mut x = 5 in x <- 10` parses to `LetMut ("x", Number 5, Assign ("x", Number 10))`
- Module-level `let mut x = 5` parses to `LetMutDecl ("x", Number 5)`

## Next Phase Readiness

Plan 02 can now:
- Add Eval cases for LetMut (ref cell creation) and Assign (ref cell mutation)
- Add Bidir/TypeCheck cases with mutability tracking
- Handle the incomplete match warnings in Bidir.fs, TypeCheck.fs, Eval.fs
