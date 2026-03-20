---
phase: 22-negative-patterns-tuple-lambda
plan: "01"
subsystem: parser
tags: [parser, pattern-matching, lambda, tuple, negative-integer, fsyacc]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: Parser.fsy grammar infrastructure
  - phase: 03-records
    provides: TuplePattern nonterminal, PatternList grammar
provides:
  - Negative integer patterns in match expressions (| -1 -> ...)
  - Tuple parameter lambdas (fun (x, y) -> body)
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Tuple lambda desugaring via LetPat + synthetic __tuple_arg variable"

key-files:
  created:
    - tests/flt/file/pat-negative-int.flt
    - tests/flt/file/pat-negative-match.flt
    - tests/flt/file/lambda-tuple-basic.flt
    - tests/flt/file/lambda-tuple-nested.flt
  modified:
    - src/LangThree/Parser.fsy

key-decisions:
  - "Negative pattern: MINUS NUMBER in Pattern rule, no AST/Eval changes needed"
  - "Tuple lambda: desugar to Lambda(__tuple_arg, LetPat(TuplePat, Var(__tuple_arg), body))"

patterns-established:
  - "Synthetic variable naming: __tuple_arg for desugared tuple parameter"

# Metrics
duration: 5min
completed: 2026-03-20
---

# Phase 22 Plan 01: Negative Patterns & Tuple Lambda Summary

**Negative integer patterns (MINUS NUMBER) and tuple parameter lambdas (fun (x,y) -> body) via parser-only changes**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-20T03:37:58Z
- **Completed:** 2026-03-20T03:42:27Z
- **Tasks:** 3
- **Files modified:** 1 (+ 4 test files created)

## Accomplishments
- Negative integer patterns work in match expressions: `| -1 -> "neg"` matches correctly
- Tuple parameter lambdas desugar cleanly: `(fun (x, y) -> x + y) (1, 2)` returns 3
- Zero LALR(1) conflicts introduced -- COMMA vs COLON disambiguates tuple from annotated lambda
- All 196 F# + 412 fslit tests pass (4 new fslit tests added)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add negative integer pattern** - `bef2894` (feat)
2. **Task 2: Add tuple parameter lambda** - `68d8379` (feat)
3. **Task 3: Add fslit tests** - `b035299` (test)

## Files Created/Modified
- `src/LangThree/Parser.fsy` - Added MINUS NUMBER pattern rule and FUN TuplePattern ARROW Expr rule
- `tests/flt/file/pat-negative-int.flt` - Negative integer pattern matching with -1, 0, 1, wildcard
- `tests/flt/file/pat-negative-match.flt` - Negative pattern in temperature classification function
- `tests/flt/file/lambda-tuple-basic.flt` - Basic tuple lambda with 2-tuple and swap
- `tests/flt/file/lambda-tuple-nested.flt` - 3-tuple lambda and higher-order tuple function passing

## Decisions Made
- **Negative pattern:** Simple `MINUS NUMBER` rule in Pattern produces `ConstPat(IntConst(-N))`. No changes needed to AST, Eval, MatchCompile, or Exhaustive since IntConst already supports any integer value.
- **Tuple lambda desugaring:** `fun (x, y) -> body` desugars to `Lambda("__tuple_arg", LetPat(TuplePat([VarPat "x"; VarPat "y"]), Var("__tuple_arg"), body))`. Uses existing TuplePattern nonterminal and LetPat mechanism -- no new AST nodes.
- **No LALR(1) conflicts:** PatternList requires COMMA, which disambiguates from COLON in annotated lambda at the LALR(1) lookahead level.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Parser grammar extended with two new production rules
- All existing functionality preserved (zero regressions)
- Ready for any future parser extensions

---
*Phase: 22-negative-patterns-tuple-lambda*
*Completed: 2026-03-20*
