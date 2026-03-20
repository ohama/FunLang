---
phase: 24-list-separator-semicolon
plan: "01"
subsystem: parser
tags: [parser, fsy, list, semicolon, syntax, fsharp-style]

# Dependency graph
requires:
  - phase: 23-offside-rule-refactoring
    provides: baseline build with SEMICOLON token already declared in Parser.fsy
provides:
  - "[1; 2; 3] semicolon list syntax accepted by parser"
  - "[1, 2, 3] comma list syntax rejected (parse error)"
  - "List values print as [1; 2; 3] in Eval.fs formatValue"
  - "AST emit for List nodes uses semicolons in Format.fs formatAst"
affects: [tests, tutorial, any .lt or .flt files that use comma list syntax]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SemiExprList grammar rule for semicolon-separated list literals (parallel to ExprList for tuples)"

key-files:
  created: []
  modified:
    - src/LangThree/Parser.fsy
    - src/LangThree/Format.fs
    - src/LangThree/Eval.fs

key-decisions:
  - "SemiExprList is a separate grammar rule from ExprList — tuples keep COMMA, lists use SEMICOLON"
  - "SEMICOLON token was already declared in Parser.fsy and lexed in Lexer.fsl — no lexer changes needed"
  - "Only ListValue case changed in Eval.fs; TupleValue stays with COMMA"
  - "Only Ast.List case changed in Format.fs; Tuple, TETuple, TuplePat stay with COMMA"

patterns-established:
  - "List literals: [e1; e2; e3] with semicolons (F# convention)"
  - "Tuple literals: (e1, e2, e3) with commas (unchanged)"

# Metrics
duration: 2min
completed: 2026-03-20
---

# Phase 24 Plan 01: List Separator Semicolon Summary

**Parser, formatter, and evaluator updated so list literals use semicolons ([1; 2; 3]) instead of commas, aligning with F# convention while leaving tuple syntax (1, 2, 3) unchanged.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-20T09:04:05Z
- **Completed:** 2026-03-20T09:05:17Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- Parser now accepts `[1; 2; 3]` and rejects `[1, 2, 3]` with a parse error
- `formatValue` in Eval.fs outputs list values as `[1; 2; 3]`
- `formatAst` in Format.fs emits List nodes as `List [Number 1; Number 2; Number 3]`
- Tuple syntax `(1, 2, 3)` is completely unaffected — ExprList rule unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Update Parser.fsy — add SemiExprList rule, change list literal grammar** - `5e091fd` (feat)
2. **Task 2: Update Format.fs and Eval.fs — change list output separator to semicolon** - `3de6bc6` (feat)

**Plan metadata:** (docs commit below)

## Files Created/Modified

- `src/LangThree/Parser.fsy` - Added SemiExprList rule; changed list literal from COMMA ExprList to SEMICOLON SemiExprList
- `src/LangThree/Format.fs` - Ast.List case: `String.concat "; "` (was `", "`)
- `src/LangThree/Eval.fs` - ListValue case: `String.concat "; "` (was `", "`)

## Decisions Made

- **Separate SemiExprList rule:** Rather than reusing ExprList with a different separator, a dedicated `SemiExprList` rule was added. This keeps the two syntaxes cleanly separated — ExprList is the COMMA rule for tuples, SemiExprList is the SEMICOLON rule for list literals. No ambiguity.
- **No lexer changes:** SEMICOLON was already declared and lexed from prior record syntax work. Zero new tokens needed.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Core syntax change complete; `[1; 2; 3]` is now the canonical list syntax
- Any existing `.lt` / `.flt` test files that use `[1, 2, 3]` will now fail — follow-on plan needed to update tests
- Tutorials/documentation using comma list syntax will need updating

---
*Phase: 24-list-separator-semicolon*
*Completed: 2026-03-20*
