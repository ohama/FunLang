---
phase: 66-expression-level-mutual-recursion
plan: 01
subsystem: ast-parser
tags: [fsharp, ast, parser, letrec, mutual-recursion, lalr]

# Dependency graph
requires:
  - phase: 65-letrecdecl-ast-refactoring
    provides: LetRec/LetRecDecl AST nodes carry TypeExpr option for first parameter
provides:
  - LetRec AST node with bindings list shape (matching LetRecDecl)
  - Parser accepts `let rec f x = ... and g y = ... in expr` syntax
  - All pattern-match sites compile with new shape
affects: [66-02 (type-check/eval multi-binding logic)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "LetRec bindings list shape matches LetRecDecl exactly -- same tuple (string * string * TypeExpr option * Expr * Span)"
    - "LetRecContinuation nonterminal reused for both module-level and expression-level let rec"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Format.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs
    - src/LangThree/Eval.fs
    - tests/flt/emit/ast-expr/ast-expr-letrec.flt

key-decisions:
  - "Bidir/Infer/Eval use List.head for single-binding -- Plan 02 rewrites with full multi-binding logic"
  - "Binding-internal ruleSpan unchanged; only outer span endpoint shifts with LetRecContinuation"

patterns-established:
  - "LetRec and LetRecDecl share identical binding tuple shape for code reuse"

# Metrics
duration: 6min
completed: 2026-03-31
---

# Phase 66 Plan 01: LetRec AST + Parser Summary

**LetRec AST changed to bindings list shape, parser accepts expression-level `and` chains via LetRecContinuation -- 650 flt + 224 unit tests passing**

## Performance

- **Duration:** 6 min
- **Started:** 2026-03-31T04:18:42Z
- **Completed:** 2026-03-31T04:24:42Z
- **Tasks:** 2
- **Files modified:** 8 (7 source + 1 test)

## Accomplishments
- LetRec AST node now carries `(string * string * TypeExpr option * Expr * Span) list` matching LetRecDecl shape
- All 4 expression-level let rec parser rules accept `and` continuations via existing LetRecContinuation nonterminal
- All pattern-match sites (Format, TypeCheck collectors, Bidir, Infer, Eval, spanOf) updated mechanically
- Single-binding `let rec f x = ... in expr` works exactly as before (binding list of one)

## Task Commits

Each task was committed atomically:

1. **Task 1: Change LetRec AST node and fix all mechanical pattern-match sites** - `52c4bca` (feat)
2. **Task 2: Update Parser.fsy expression-level let rec rules with LetRecContinuation** - `8500671` (feat)

## Files Created/Modified
- `src/LangThree/Ast.fs` - LetRec node shape changed to bindings list + inExpr + span
- `src/LangThree/Parser.fsy` - 4 expression let rec rules now include LetRecContinuation
- `src/LangThree/Format.fs` - Pretty-printer adapted for bindings list with "and" separator
- `src/LangThree/TypeCheck.fs` - 4 collector functions updated for bindings list iteration
- `src/LangThree/Bidir.fs` - Mechanical List.head extraction (Plan 02 rewrites)
- `src/LangThree/Infer.fs` - Mechanical List.head extraction (Plan 02 rewrites)
- `src/LangThree/Eval.fs` - Mechanical List.head extraction (Plan 02 rewrites)
- `tests/flt/emit/ast-expr/ast-expr-letrec.flt` - Updated expected output for new Format shape

## Decisions Made
- Used mechanical `List.head bindings` in Bidir/Infer/Eval to keep existing single-binding logic working. Plan 02 will rewrite these with full multi-binding support.
- Binding-internal `ruleSpan` (e.g., `ruleSpan parseState 3 6`) stays the same since it covers only one binding's tokens. Only the outer span endpoint shifts when LetRecContinuation is inserted.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated ast-expr-letrec.flt expected output**
- **Found during:** Task 2 verification
- **Issue:** Format.fs output changed from old shape to new bindings-list shape, breaking AST emit test
- **Fix:** Updated expected output from `LetRec ("f", "x", ...)` to `LetRec (f x = ...) in ...`
- **Files modified:** tests/flt/emit/ast-expr/ast-expr-letrec.flt
- **Verification:** Test passes
- **Committed in:** 8500671 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Necessary test update due to Format.fs shape change. No scope creep.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- AST and parser foundation complete for mutual recursion
- Plan 02 rewrites Bidir/Infer/Eval with full multi-binding logic
- All 650 flt + 224 unit tests passing

---
*Phase: 66-expression-level-mutual-recursion*
*Completed: 2026-03-31*
