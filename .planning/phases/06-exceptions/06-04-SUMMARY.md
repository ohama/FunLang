---
phase: "06"
plan: "04"
subsystem: "exceptions"
tags: ["testing", "integration-tests", "exceptions", "when-guards", "bug-fixes"]
dependency-graph:
  requires: ["06-01", "06-02", "06-03"]
  provides: ["exception-integration-tests", "exception-bug-fixes"]
  affects: ["07"]
tech-stack:
  added: []
  patterns: ["integration-test-pipeline"]
key-files:
  created:
    - tests/LangThree.Tests/ExceptionTests.fs
  modified:
    - tests/LangThree.Tests/LangThree.Tests.fsproj
    - src/LangThree/Unify.fs
    - src/LangThree/IndentFilter.fs
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs
decisions:
  - id: "06-04-01"
    decision: "Use strict < instead of <= for InTry context popping in IndentFilter"
    rationale: "Try-with body DEDENTs back to try level before pipes; context must survive this DEDENT"
  - id: "06-04-02"
    decision: "Re-raise LangThreeException when no handler matches in try-with"
    rationale: "Match failure in try-with should propagate exception, not crash with generic error"
  - id: "06-04-03"
    decision: "Thread ctorEnv/recEnv through typeCheckDecls fold state"
    rationale: "Open directive was not propagating constructor/record environments to subsequent declarations"
  - id: "06-04-04"
    decision: "Qualified exception patterns (Module.Exn) not tested -- parser doesn't support qualified constructor patterns"
    rationale: "Out of scope for current phase; would require parser grammar changes"
metrics:
  duration: "12 min"
  completed: "2026-03-09"
---

# Phase 6 Plan 4: Exception Integration Tests Summary

Comprehensive integration tests for all exception features (EXC-01 through EXC-05) plus when guards in standard match expressions. 29 new tests added, 178 total passing.

## One-liner

Exception integration tests covering declarations, raise, try-with, pattern matching, when guards, and nested handlers with 4 bug fixes

## What Was Done

### Task 1: Create ExceptionTests.fs with comprehensive test coverage

Created 194-line test file covering all 5 exception requirements:

- **EXC-01 (4 tests):** Nullary, with-data, tuple-data, and multiple exception declarations
- **EXC-02 (5 tests):** Raise nullary, with data, in if-else branches (both paths), type error for non-exn
- **EXC-03 (4 tests):** Basic catch, data extraction, no-exception passthrough, let-in body
- **EXC-04 (5 tests):** Multiple handlers, first-match wins, wildcard catch-all, nested inner/outer catches
- **EXC-05 (3 tests):** When guards selecting handlers, falling through, reaching wildcard
- **When guards in match (5 tests):** Classification with guards, list pattern with guards
- **Edge cases (3 tests):** Exception via open module, tuple data extraction, re-raise propagation

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] TExn unification missing**
- **Found during:** First test run (all exception tests failing)
- **Issue:** Unify.fs had no case for `TExn, TExn`, causing "Type mismatch: expected exn but got exn"
- **Fix:** Added `| TExn, TExn -> empty` alongside other primitive type cases
- **Files modified:** src/LangThree/Unify.fs
- **Commit:** 2d57648

**2. [Rule 1 - Bug] InTry context popped too early for nested try-with**
- **Found during:** Nested try-with tests
- **Issue:** `popContexts` used `<=` for InTry, causing context to be popped when DEDENT returned to try-body level (before pipes processed)
- **Fix:** Changed to strict `<` for InTry only (InMatch kept `<=` since match pipes don't face INDENT/DEDENT dance)
- **Files modified:** src/LangThree/IndentFilter.fs
- **Commit:** 2d57648

**3. [Rule 1 - Bug] No handler match in try-with crashed instead of re-raising**
- **Found during:** Nested try-with outer-catches test
- **Issue:** `evalMatchClauses` returned "Match failure: no pattern matched" when no handler matched, instead of re-raising the exception for outer handlers
- **Fix:** Wrapped evalMatchClauses call in try-with that catches the match failure and re-raises the original LangThreeException
- **Files modified:** src/LangThree/Eval.fs
- **Commit:** 2d57648

**4. [Rule 1 - Bug] Open directive not propagating ctorEnv/recEnv**
- **Found during:** Exception-in-module-via-open test
- **Issue:** `typeCheckDecls` fold only threaded `(env, mods, warns)` but `ctorEnv/recEnv` were captured from outer scope, so `openModuleExports` updates were discarded
- **Fix:** Expanded fold state to `(env, cEnv, rEnv, mods, warns)` so open propagates constructor and record environments
- **Files modified:** src/LangThree/TypeCheck.fs
- **Commit:** 2d57648

### Test Adjustments

- Error code for `raise 42` is E0301 (unification mismatch), not E0001
- Let bindings inside try body require explicit `in` keywords (known limitation from 01-04)
- Qualified exception patterns (`Module.Exn`) not supported in parser; tested via `open` instead
- Exception as first-class function (`let wrap = Wrapper`) not supported; uppercase parsed as nullary constructor

## Next Phase Readiness

Phase 6 (Exceptions) is now complete:
- All 5 requirements implemented and tested end-to-end
- 4 bugs discovered and fixed during integration testing
- 178 total tests passing, zero regressions
- Ready for Phase 7 (Pattern Matching Compilation)
