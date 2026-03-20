---
phase: 15-tail-call-optimization
plan: "01"
title: "Trampoline TCO Implementation"
subsystem: evaluator
tags: [tco, trampoline, tail-call, eval, performance]
dependency-graph:
  requires: []
  provides: [tail-call-optimization, stack-safe-recursion]
  affects: []
tech-stack:
  added: []
  patterns: [trampoline-pattern, tailPos-threading]
key-files:
  created:
    - tests/flt/file/tco-simple-loop.flt
    - tests/flt/file/tco-accumulator.flt
    - tests/flt/file/tco-match.flt
    - tests/flt/file/tco-non-tail.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Eval.fs
    - src/LangThree/MatchCompile.fs
    - src/LangThree/Program.fs
    - src/LangThree/Repl.fs
    - tests/LangThree.Tests/IntegrationTests.fs
    - tests/LangThree.Tests/RecordTests.fs
    - tests/LangThree.Tests/ModuleTests.fs
decisions:
  - id: "15-01-D1"
    title: "TailCall returned from App in tail position"
    choice: "When tailPos=true in App case, return TailCall(funcVal, argVal) immediately; caller trampolines"
    rationale: "Simplest trampoline pattern: outermost App does the loop, inner tail-position Apps just return TailCall"
  - id: "15-01-D2"
    title: "applyFunc always evaluates body with tailPos=true"
    choice: "applyFunc passes tailPos from caller; trampoline calls with true so nested tail calls produce TailCall"
    rationale: "Enables arbitrary-depth tail call chains to be unwound iteratively"
metrics:
  duration: "7 min"
  completed: "2026-03-19"
---

# Phase 15 Plan 01: Trampoline TCO Implementation Summary

Tail call optimization via trampoline pattern: TailCall Value variant + tailPos bool parameter + iterative unwrap loop in App/PipeRight.

## What Was Done

1. **TailCall Value variant** (Ast.fs): Added `TailCall of func: Value * arg: Value` to Value DU with proper CustomEquality/CustomComparison/GetHashCode handling.

2. **tailPos parameter threading** (Eval.fs): Added `tailPos: bool` parameter to `eval`, `evalMatchClauses`, and new `applyFunc` helper. Updated ~30 internal call sites:
   - Tail positions (inherit tailPos): Let/LetPat body, LetRec inExpr, If branches, Match clause bodies, Annot, TryWith handler bodies
   - Non-tail positions (always false): operator operands, bindings, constructor args, TryWith body, guard expressions

3. **Trampoline loop** (Eval.fs): In App case, when `tailPos=true`, returns `TailCall(funcVal, argVal)` immediately. When `tailPos=false`, calls `applyFunc` then loops while result is `TailCall`. Same pattern for PipeRight.

4. **applyFunc helper** (Eval.fs): Extracted function application logic shared by App, PipeRight, and trampoline loop. Handles FunctionValue (with self-reference augmentation) and BuiltinValue.

5. **MatchCompile.fs callback update**: Changed `evalDecisionTree` callback from `Env -> Expr -> Value` to `Env -> bool -> Expr -> Value` to thread tailPos through match clause body evaluation.

6. **External caller updates**: Program.fs, Repl.fs, and test files (IntegrationTests.fs, RecordTests.fs, ModuleTests.fs) pass `false` for tailPos at top level.

7. **4 fslit integration tests**: Simple loop 1M, accumulator 1M, match 1M, non-tail factorial.

## Decisions Made

| ID | Decision | Choice | Rationale |
|----|----------|--------|-----------|
| 15-01-D1 | TailCall from App in tail position | Return TailCall immediately when tailPos=true | Outermost App does the trampoline loop |
| 15-01-D2 | applyFunc tailPos passing | Always passes tailPos from caller; trampoline calls with true | Enables arbitrary-depth unwinding |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing] Test files needed tailPos parameter**
- **Found during:** Task 4 (build)
- **Issue:** IntegrationTests.fs, RecordTests.fs, ModuleTests.fs call `eval` directly without tailPos
- **Fix:** Added `false` as tailPos argument to all 3 test files
- **Commit:** 487721d

**2. [Rule 1 - Bug] Module-level let rec not supported**
- **Found during:** Task 6 (fslit tests)
- **Issue:** TCO test files used `let rec` at module top level, but parser only supports `let rec ... in ...` as expression
- **Fix:** Restructured tests to use expression-level `let rec ... in ...` inside `let result = ...`
- **Commit:** 039e289

## Verification

- [x] TailCall variant in Value DU with proper equality/hash/format handling
- [x] eval takes tailPos: bool parameter
- [x] Trampoline loop in App and PipeRight
- [x] evalDecisionTree callback updated for tailPos
- [x] TryWith body always uses tailPos=false
- [x] `loop 1000000` runs without stack overflow -> returns 0
- [x] `fact 10` still returns 3628800
- [x] All 196 F# tests pass
- [x] All 317 fslit tests pass (313 existing + 4 new)

## Test Results

| Suite | Before | After | Delta |
|-------|--------|-------|-------|
| F# unit tests | 196 | 196 | +0 |
| fslit tests | 313 | 317 | +4 |
| **Total** | **509** | **513** | **+4** |

## Next Phase Readiness

No blockers. TCO is complete for direct (self) recursion. Mutual recursion TCO (even/odd pattern) is not supported and was explicitly deferred per plan.
