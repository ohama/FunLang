---
phase: 48-if-then-without-else
plan: 01
subsystem: parser
tags: [fsyacc, lalr1, desugar, if-then, unit-type, type-checking]

# Dependency graph
requires:
  - phase: 45-expression-sequencing
    provides: SeqExpr nonterminal used in the new grammar rule
  - phase: 46-while-for-loops
    provides: imperative-style expression patterns established
provides:
  - "IF Expr THEN SeqExpr grammar rule in Parser.fsy desugaring to If(cond, then, Tuple([], ...), span)"
  - "3 flt integration tests covering IFTHEN-01 and IFTHEN-02"
affects:
  - 49-for-in-loops
  - future imperative pattern phases

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Desugar in parser: if-then-without-else -> If(cond, then, Tuple([], unit_span), full_span)"
    - "symSpan parseState N for attaching synthetic unit span to end of then-branch"
    - "Error message for non-unit then-branch: 'expected int but got unit' (unifier checks else vs then)"

key-files:
  created:
    - tests/flt/expr/control/if-then-unit.flt
    - tests/flt/expr/control/if-then-seq.flt
    - tests/flt/expr/control/if-then-nonunit-error.flt
  modified:
    - src/LangThree/Parser.fsy

key-decisions:
  - "Desugar in parser only — zero changes to Ast, Eval, Bidir, Infer, TypeCheck, or Format"
  - "New rule placed AFTER existing IF-THEN-ELSE rule so LALR prefers ELSE shift when present"
  - "Error message for non-unit then-branch is 'expected int but got unit' (not 'expected unit but got int') because the type unifier checks synthetic unit else-branch against the then-type"

patterns-established:
  - "Parser-level desugar pattern: synthesize missing branches as Tuple([], span) for optional else"

# Metrics
duration: 10min
completed: 2026-03-28
---

# Phase 48 Plan 01: If-Then Without Else Summary

**Parser-only desugar of `if cond then expr` to `If(cond, expr, Tuple([], unit_span), full_span)` — zero AST/Eval/type-checker changes, 573/573 flt tests passing**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-28T00:00:00Z
- **Completed:** 2026-03-28
- **Tasks:** 2
- **Files modified:** 4 (Parser.fsy + 3 new flt tests)

## Accomplishments

- Added `IF Expr THEN SeqExpr` grammar rule in Parser.fsy immediately after the IF-THEN-ELSE rule
- Desugars to `If($2, $4, Tuple([], symSpan parseState 4), ruleSpan parseState 1 4)` — pure parser transformation
- Parser regenerated via `dotnet build` (build clean, 0 errors; pre-existing shift/reduce warnings expected)
- 3 flt integration tests covering basic unit branch, sequenced branch (`;`), and E0301 type error case

## Task Commits

1. **Task 1: Add if-then-without-else grammar rule** - `52c679a` (feat)
2. **Task 2: Write 3 flt integration tests** - `12e0412` (test)

## Files Created/Modified

- `src/LangThree/Parser.fsy` - Added `IF Expr THEN SeqExpr` rule with Tuple([], ...) desugar
- `tests/flt/expr/control/if-then-unit.flt` - IFTHEN-01: basic unit-returning then-branch
- `tests/flt/expr/control/if-then-seq.flt` - IFTHEN-01+SEQ: semicolon-sequenced then-branch
- `tests/flt/expr/control/if-then-nonunit-error.flt` - IFTHEN-02: non-unit then-branch produces E0301

## Decisions Made

- Desugar in parser only — no changes needed to Ast, Eval, Bidir, Infer, TypeCheck, or Format. The synthetic `Tuple([], unit_span)` makes the existing type checker handle the else-branch unification automatically.
- Error wording for IFTHEN-02 is "expected int but got unit" not "expected unit but got int" because the type unifier checks the synthetic unit else-branch against the already-established then-branch type (int). Adjusted test accordingly.

## Deviations from Plan

None — plan executed exactly as written. The only adjustment was the exact error message wording in if-then-nonunit-error.flt, which was verified against actual compiler output before writing the test (not a deviation, just accurate testing).

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 49 (final v5.0 phase) can proceed — if-then-without-else fully operational
- 573/573 flt tests passing, no regressions
- Entire v5.0 imperative ergonomics milestone only one phase away from completion

---
*Phase: 48-if-then-without-else*
*Completed: 2026-03-28*
