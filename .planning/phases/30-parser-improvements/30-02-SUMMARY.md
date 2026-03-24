---
phase: 30-parser-improvements
plan: 02
subsystem: parser
tags: [fsyacc, grammar, eval, let-rec, closure, unit-param]

# Dependency graph
requires:
  - phase: 30-01
    provides: SYN-05 IndentFilter ELSE suppression
provides:
  - Multi-param expression-level let rec via ParamList desugaring (SYN-01/SYN-06)
  - Unit param shorthand at Decl and Expr levels: let f () = body (SYN-07)
  - Top-level let x = e1 in e2 as module declaration (SYN-08)
  - BuiltinValue + mutable-ref self-referential closure for expression LetRec
affects: [phase 31, any phase using local let rec, user-facing recursive patterns]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Expression LetRec uses BuiltinValue + envRef pattern (same as LetRecDecl)"
    - "Multi-param grammar sugar: ParamList + foldBack -> nested Lambda/LetRec"
    - "Unit param syntactic sugar: let f () = body desugars to LambdaAnnot('__unit', TETuple [])"
    - "Top-level let-in wraps as LetDecl('_', Let(...))"

key-files:
  created: []
  modified:
    - src/LangThree/Parser.fsy
    - src/LangThree/Eval.fs
    - tests/LangThree.Tests/IntegrationTests.fs

key-decisions:
  - "Expression LetRec uses BuiltinValue + mutable envRef (not FunctionValue). FunctionValue fails inside lambda bodies due to trampoline losing the self-binding."
  - "Multi-param expr let rec replaces the old IDENT IDENT rules entirely (not additive) to avoid LALR conflict"
  - "Unit param desugars to LambdaAnnot('__unit', TETuple []) matching existing fun () -> body pattern"
  - "Top-level let-in wraps as LetDecl('_', Let(...)) - synthetic '_' name consistent with existing wildcard convention"

patterns-established:
  - "Self-referential expression LetRec: envRef := recEnv' (same as LetRecDecl)"
  - "ParamList grammar reuse: use existing ParamList nonterminal for all multi-param function forms"

# Metrics
duration: 3min
completed: 2026-03-25
---

# Phase 30 Plan 02: Parser Improvements (SYN-01/06/07/08) Summary

**Multi-param local let rec + unit param shorthand + top-level let-in grammar additions, plus BuiltinValue+ref LetRec closure fix eliminating trampoline regression for recursive lambdas**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-03-24T20:37:16Z
- **Completed:** 2026-03-24T20:40:27Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- Replaced single-param expression LetRec grammar rules with multi-param ParamList versions (SYN-01/SYN-06) — eliminates LALR conflict and enables `let rec f a b = ...` in expression position
- Added unit param shorthand `let f () = body` at both module-level Decl and expression Expr levels (SYN-07)
- Added top-level `let x = e1 in e2` grammar rule as `LetDecl("_", Let(...))` (SYN-08)
- Fixed expression-level LetRec evaluator to use BuiltinValue + mutable envRef pattern — recursive calls inside lambda bodies now work correctly without "Undefined variable" errors
- All 7 new integration tests pass; 202 pre-existing tests continue to pass (209 total)

## Task Commits

Each task was committed atomically:

1. **Task 1: Parser.fsy grammar additions** - `bd22318` (feat)
2. **Task 2: Fix Eval.fs LetRec closure** - `3933b30` (fix)
3. **Task 3: Integration tests** - `430f9a0` (test)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Parser.fsy` - Four grammar changes: multi-param expr LetRec (2 rules), unit param Decl (2 rules), unit param Expr (2 rules), top-level let-in Decl (1 rule)
- `src/LangThree/Eval.fs` - LetRec case rewritten to use BuiltinValue + envRef pattern
- `tests/LangThree.Tests/IntegrationTests.fs` - 7 new integration tests for SYN-01/06/07/08 + local evalModule helper

## Decisions Made
- Expression LetRec uses BuiltinValue + mutable envRef (not FunctionValue). The naive FunctionValue approach fails when LetRec is inside a lambda body: the trampoline loop re-applies the outer funcExpr on recursive tail calls, losing the self-binding from `applyFunc`'s augmentation step.
- The old `LET REC IDENT IDENT EQUALS` rules were removed entirely (not kept alongside the new ParamList rule) to avoid LALR shift/reduce conflicts.
- Unit param shorthand desugars to `LambdaAnnot("__unit", TETuple [], body)` — consistent with the existing `fun () -> body` pattern.
- Top-level let-in uses `"_"` as the synthetic LetDecl name, consistent with the existing wildcard sequencing convention in the module pipeline.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered
- None. All grammar changes compiled cleanly. fsyacc reported shift/reduce warnings for the pre-existing `LET IDENT EQUALS Expr IN Expr` Expr rules (not new — these existed before), resolved via fsyacc's standard "prefer shift" default. Build succeeded with 0 errors.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- SYN-01, SYN-06, SYN-07, SYN-08 all implemented and tested
- Phase 30 complete (30-01: SYN-05 IndentFilter; 30-02: Parser/Eval SYN-01/06/07/08)
- Ready for Phase 31

---
*Phase: 30-parser-improvements*
*Completed: 2026-03-25*
