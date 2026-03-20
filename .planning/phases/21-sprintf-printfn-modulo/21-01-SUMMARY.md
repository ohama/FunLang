---
phase: 21-sprintf-printfn-modulo
plan: "01"
title: "sprintf, printfn, and modulo operator"
subsystem: "eval-builtins"
tags: [sprintf, printfn, modulo, builtins, format-strings]
dependency-graph:
  requires: [12-printf-output]
  provides: [sprintf-builtin, printfn-builtin, modulo-operator]
  affects: []
tech-stack:
  added: []
  patterns: [curried-builtin-chain, format-string-reuse]
key-files:
  created:
    - tests/flt/file/modulo-basic.flt
    - tests/flt/file/modulo-zero.flt
    - tests/flt/file/printfn-basic.flt
    - tests/flt/file/sprintf-basic.flt
    - tests/flt/file/sprintf-string.flt
    - tests/flt/file/sprintf-no-spec.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Eval.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Format.fs
decisions: []
metrics:
  duration: "6 min"
  completed: "2026-03-20"
---

# Phase 21 Plan 01: sprintf, printfn, and modulo operator Summary

**One-liner:** Added % modulo operator (Modulo AST node), printfn (printf+newline), and sprintf (format-to-string) reusing existing printf infrastructure.

## What Was Done

### Task 1: Add % modulo operator
- Added `Modulo of Expr * Expr * Span` to Expr DU in Ast.fs
- Added `PERCENT` token to Parser.fsy, `| '%' { PERCENT }` to Lexer.fsl before catch-all
- Grammar rule at multiplicative level: `Term PERCENT Factor`
- Eval: `IntValue (l % r)` for integer operands
- Bidir/Infer synth: same as Divide (int -> int -> int)
- Updated spanOf, collectMatches, rewriteModuleAccess, formatAst, formatToken

### Task 2: Add printfn built-in function
- Added `applyPrintfnArgs`: same as `applyPrintfArgs` but writes "\n" after result
- Added to initialBuiltinEnv and initialTypeEnv with same type as printf

### Task 3: Add sprintf built-in function
- Added `applySprintfArgs`: same curried chain but returns `StringValue result` instead of writing
- Added to initialBuiltinEnv and initialTypeEnv with `Scheme([0], TArrow(TString, TVar 0))`

### Task 4: Build, test, add fslit tests
- All 196 F# tests pass
- All 408 fslit tests pass (402 existing + 6 new)
- 6 new file-mode tests: modulo-basic, modulo-zero, printfn-basic, sprintf-basic, sprintf-string, sprintf-no-spec

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing] Added Modulo to Infer.fs inferWithContext**
- **Found during:** Task 1 build
- **Issue:** FS0025 incomplete pattern match warning for Modulo in deprecated Infer.fs
- **Fix:** Added `| Modulo (e1, e2, _)` to the arithmetic operator pattern in inferWithContext
- **Files modified:** src/LangThree/Infer.fs
- **Commit:** 6035f97

## Verification

- [x] `10 % 3` returns `1`
- [x] `printfn "hello %d" 42` prints "hello 42\n" and returns ()
- [x] `sprintf "%d + %d = %d" 1 2 3` returns `"1 + 2 = 3"`
- [x] All existing 402 fslit + 196 F# tests pass
- [x] New fslit tests pass (6 new, 408 total)

## Test Coverage

| Category | Before | After | Delta |
|----------|--------|-------|-------|
| F# unit tests | 196 | 196 | +0 |
| fslit tests | 402 | 408 | +6 |
| **Total** | **598** | **604** | **+6** |
