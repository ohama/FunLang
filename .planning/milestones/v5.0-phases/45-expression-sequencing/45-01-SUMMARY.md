---
phase: 45-expression-sequencing
plan: 01
subsystem: parser
tags: [fsyacc, lalr1, grammar, sequencing, desugar, flt-tests]

# Dependency graph
requires:
  - phase: 42-mutable-variables
    provides: LetMut/Assign nodes that sequencing operates on; WildcardPat for LetPat desugar
  - phase: 10-unit
    provides: LetPat(WildcardPat, e1, e2) already fully implemented in all passes
provides:
  - SeqExpr nonterminal in Parser.fsy enabling e1; e2 sequencing syntax
  - OCaml-style grammar structure that avoids LALR(1) conflict with list/record semicolons
  - 5 flt tests covering SEQ-01, SEQ-02, SEQ-03, trailing semicolon, list non-conflict
affects:
  - phase 46 (loop constructs): loop bodies can now use sequencing
  - any future phase that adds statement-position grammar rules must use SeqExpr

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SeqExpr nonterminal wraps Expr to add sequencing only at statement positions"
    - "e1; e2 desugars to LetPat(WildcardPat, e1, e2) — no new AST node needed"
    - "LALR(1) conflict avoidance via grammar nonterminal separation (not precedence tricks)"

key-files:
  created:
    - tests/flt/expr/seq/seq-basic.flt
    - tests/flt/expr/seq/seq-chained.flt
    - tests/flt/expr/seq/seq-in-block.flt
    - tests/flt/expr/seq/seq-trailing.flt
    - tests/flt/expr/seq/seq-list-no-conflict.flt
  modified:
    - src/LangThree/Parser.fsy

key-decisions:
  - "SeqExpr nonterminal approach (OCaml-style) chosen over precedence declarations"
  - "Desugar to LetPat(WildcardPat, e1, e2) — reuses existing node, all passes handle it"
  - "Decl RHS positions also updated to SeqExpr so module-level let _ = e1; e2 works"
  - "List/record/pattern contexts intentionally left as bare Expr to preserve separator semantics"

patterns-established:
  - "Statement-position grammar rules: use SeqExpr, not Expr, for expression bodies"
  - "New Decl rule bodies: must use SeqExpr to allow sequencing in all let-binding forms"

# Metrics
duration: 9min
completed: 2026-03-28
---

# Phase 45 Plan 01: Expression Sequencing Summary

**SeqExpr nonterminal added to Parser.fsy enabling e1; e2 sequencing via LetPat(WildcardPat) desugar, with zero AST/eval/type-checker changes and 556/556 flt tests passing**

## Performance

- **Duration:** 9 min
- **Started:** 2026-03-27T21:39:21Z
- **Completed:** 2026-03-27T21:48:00Z
- **Tasks:** 2
- **Files modified:** 6 (1 modified, 5 created)

## Accomplishments
- Added `SeqExpr` nonterminal to Parser.fsy: `Expr SEMICOLON SeqExpr | Expr SEMICOLON | Expr`
- Updated all statement-position `Expr` references to `SeqExpr` (start rule, INDENT/DEDENT blocks, IN-bodies, if-then-else branches, FUN/lambda bodies, TRY bodies, MatchClauses, all Decl RHS positions)
- Created 5 flt tests: SEQ-01 basic sequencing, SEQ-02 chained mutations, SEQ-03 sequencing in indented block, trailing semicolon, list non-conflict
- Full suite 556/556 passing after changes

## Task Commits

Each task was committed atomically:

1. **Task 1: Add SeqExpr nonterminal and update all statement-position Expr refs** - `46d30a5` (feat)
2. **Task 2: Write flt tests for SEQ-01, SEQ-02, SEQ-03 and run them** - `ecfa12b` (test)

## Files Created/Modified
- `src/LangThree/Parser.fsy` - SeqExpr nonterminal added; ~30 statement-position rules updated to use SeqExpr
- `tests/flt/expr/seq/seq-basic.flt` - SEQ-01: println "hello"; println "world"
- `tests/flt/expr/seq/seq-chained.flt` - SEQ-02: let mut + x <- 1; x <- x+1; x <- x+1; x -> 3
- `tests/flt/expr/seq/seq-in-block.flt` - SEQ-03: sequencing in indented block
- `tests/flt/expr/seq/seq-trailing.flt` - Trailing semicolon accepted
- `tests/flt/expr/seq/seq-list-no-conflict.flt` - [1; 2; 3] still works

## Decisions Made
- **SeqExpr nonterminal (not precedence):** Chose the OCaml-style grammar nonterminal separation approach instead of `%prec` on SEMICOLON. This solves the LALR(1) conflict by grammar structure — list/record contexts keep bare `Expr` which does not consume SEMICOLON as sequence start.
- **Desugar to existing LetPat(WildcardPat, e1, e2):** No new AST node created. `let _ = e1 in e2` already works in all passes (eval, type-checker, exhaustiveness, format). The desugar is a pure grammar-level transformation.
- **Decl RHS uses SeqExpr:** Module-level `let _ = e1; e2` now binds the full sequence as the body of one `LetDecl`. The two-declaration alternative (`let _ = e1` then orphaned `; e2`) is avoided.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered
- fsyacc reported shift/reduce and reduce/reduce conflicts from the `INDENT SeqExpr DEDENT` rule interacting with explicit `FUN ... ARROW INDENT SeqExpr DEDENT` rules. Investigation showed the original grammar already had 784 conflicts (490 shift/reduce + 294 reduce/reduce); the new version actually has fewer (396 total). All conflicts are resolved correctly by fsyacc defaults (prefer shift; prefer earlier rule). Build: 0 errors, 0 warnings. All 556 tests pass.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- Expression sequencing complete: `e1; e2` works at all statement positions
- Phase 46 (loop constructs) can now use sequencing inside loop bodies
- Any future grammar rules that introduce expression bodies must use `SeqExpr` not `Expr`

---
*Phase: 45-expression-sequencing*
*Completed: 2026-03-28*
