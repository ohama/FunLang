---
phase: 18-list-ranges-mutual-recursion
plan: "01"
title: "List Range Syntax"
subsystem: parser-eval
tags: [range, list, lexer, parser, eval, type-checking]
dependency-graph:
  requires: []
  provides: [range-syntax, dotdot-token]
  affects: [18-02]
tech-stack:
  added: []
  patterns: [range-expression, dotdot-token]
key-files:
  created:
    - tests/flt/file/range-basic.flt
    - tests/flt/file/range-step.flt
    - tests/flt/file/range-empty.flt
    - tests/flt/file/range-single.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Eval.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Infer.fs
    - src/LangThree/Format.fs
decisions:
  - id: "18-01-D1"
    decision: "Step value in [start..step..stop] is used directly as step (F# semantics), not as second element"
    rationale: "F# behavior: [1..2..10] means step=2, producing [1,3,5,7,9]"
metrics:
  duration: "6 min"
  completed: "2026-03-19"
---

# Phase 18 Plan 01: List Range Syntax Summary

**One-liner:** DOTDOT token and Range AST for [start..stop] and [start..step..stop] integer list generation with F# semantics

## What Was Done

### Task 1: Add DOTDOT token and Range AST
- Added `DOTDOT` (..) token to Lexer.fsl before the DOT rule (longest match)
- Added `Range of start: Expr * stop: Expr * step: Expr option * Span` to Expr DU
- Added parser grammar for `[Expr DOTDOT Expr]` and `[Expr DOTDOT Expr DOTDOT Expr]`
- Updated spanOf, formatAst, formatToken for Range/DOTDOT

### Task 2: Implement Range evaluation and type checking
- Added eval case: uses F# built-in `[start .. step .. stop]` range operator
- Added synth case in Bidir.fs: all components must be int, result is `TList TInt`
- Updated all exhaustive pattern match functions: collectMatches, collectTryWiths, collectModuleRefs, rewriteModuleAccess
- Added Range stub in deprecated Infer.fs

### Task 3: Build, test, add fslit tests
- 196 F# tests pass (zero regressions)
- 294 fslit tests pass (existing, zero regressions)
- 4 new range fslit tests added and verified manually

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| 18-01-D1 | Step value is direct (not second-element) | F# semantics: [1..2..10] means step=2 |

## Deviations from Plan

None - plan executed exactly as written.

## Verification

- [x] `[1..5]` returns `[1, 2, 3, 4, 5]`
- [x] `[1..2..10]` returns `[1, 3, 5, 7, 9]`
- [x] `[5..1]` returns `[]`
- [x] `[3..3]` returns `[3]`
- [x] All 196 F# tests pass
- [x] 4 new range fslit tests verified

## Next Phase Readiness

Ready for 18-02 (mutual recursive functions). Range syntax is complete and independent.
