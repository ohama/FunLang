---
phase: 27-list-syntax-completion
plan: 02
subsystem: parser
tags: [fsyacc, grammar, list-patterns, trailing-semicolon, SYN-03, SYN-04, desugaring, flt-tests]

# Dependency graph
requires:
  - phase: 27-list-syntax-completion
    provides: Research for list syntax gaps (SYN-03 trailing semicolon, SYN-04 list literal patterns)
  - phase: 26-quick-fixes
    provides: Stable parser baseline with EmptyListPat/ConsPat already in AST

provides:
  - Trailing semicolon in list literals: [1; 2; 3;] accepted without error
  - List literal patterns [x], [x; y], [x; y; z] desugared to ConsPat/EmptyListPat chains at parse time
  - SemiPatList nonterminal in Parser.fsy
  - desugarListPat helper in Parser.fsy header
  - Three flt tests for the new features

affects:
  - Any future phase using pattern matching on known-length lists
  - flt test suite coverage

# Tech tracking
tech-stack:
  added: []
  patterns:
    - desugarListPat: list literal patterns desugar at parse time to ConsPat chains (same as Haskell/OCaml/F#)
    - SemiPatList mirrors SemiExprList: same three-production structure (single, trailing-semi, recursive)

key-files:
  created:
    - tests/flt/expr/list/list-trailing-semi.flt
    - tests/flt/expr/list/list-pattern-literal.flt
    - tests/flt/file/match/match-list-literal-pattern.flt
  modified:
    - src/LangThree/Parser.fsy

key-decisions:
  - "SYN-03: Added Expr SEMICOLON production to SemiExprList (between single and recursive) for trailing semicolons"
  - "SYN-04: desugarListPat placed in Parser.fsy header section (not a separate module) to co-locate with grammar"
  - "SYN-04: SemiPatList mirrors SemiExprList structure for consistency"
  - "SYN-04: Trailing-semicolon pattern form LBRACKET SemiPatList SEMICOLON RBRACKET added for symmetry with expr side"

patterns-established:
  - "Parser desugaring: complex syntax (list literals) desugared at parse time, never reaches AST/evaluator as raw syntax"

# Metrics
duration: 5min
completed: 2026-03-24
---

# Phase 27 Plan 02: List Syntax Completion Summary

**Trailing-semicolon list literals ([1; 2; 3;]) and list literal patterns ([x], [x; y], [x; y; z]) added to Parser.fsy via SemiExprList extension and new SemiPatList nonterminal with desugarListPat helper**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-24T08:40:39Z
- **Completed:** 2026-03-24T08:46:02Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Added trailing semicolon support in SemiExprList (SYN-03): [1; 2; 3;] now valid
- Added list literal pattern syntax via SemiPatList and desugarListPat (SYN-04): [x], [x; y], [x; y; z] patterns work in match expressions
- Three new flt tests covering trailing semicolons and list literal patterns
- No shift/reduce conflicts introduced; all 199 existing tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Add trailing semicolon to SemiExprList and list literal patterns to Parser.fsy** - `6709c7d` (feat)
2. **Task 2: Add flt tests for trailing semicolons and list literal patterns** - `3ea02d9` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Parser.fsy` - Added desugarListPat helper, Expr SEMICOLON production to SemiExprList, SemiPatList nonterminal, list literal pattern rules in Pattern
- `tests/flt/expr/list/list-trailing-semi.flt` - Verifies [1; 2; 3;] evaluates to [1; 2; 3]
- `tests/flt/expr/list/list-pattern-literal.flt` - Verifies [x; y] pattern in match returns x + y = 3
- `tests/flt/file/match/match-list-literal-pattern.flt` - Verifies [x], [x;y], [x;y;z] patterns in describe function

## Decisions Made
- desugarListPat uses the parser header section (not a separate F# module) for co-location with the grammar rules that call it
- Trailing semicolon production placed between single-element and recursive productions in SemiPatList/SemiExprList (follows natural specificity order)
- Both `LBRACKET SemiPatList RBRACKET` and `LBRACKET SemiPatList SEMICOLON RBRACKET` added to Pattern, mirroring expr-side symmetry

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- The plan's verification command (`echo '...' | dotnet run`) uses REPL mode (Parser.start) which only accepts expression-form `let ... in ...`. Module-level `let` without `in` requires file mode. The actual tests all use file mode via `%input` in flt tests and confirmed working. The verification command in the plan itself is not valid for module-level let, but this is a pre-existing REPL limitation, not a regression.

## Next Phase Readiness
- List syntax completion phase (27) plan 02 done
- [1; 2; 3;] trailing semicolon and [x; y; z] literal patterns fully functional
- Ready to continue remaining Phase 27 plans (range literals, multiline list syntax) if planned

---
*Phase: 27-list-syntax-completion*
*Completed: 2026-03-24*
