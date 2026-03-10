---
phase: 09-pipe-composition
plan: 01
subsystem: language-core
tags: [pipe, composition, operators, f-sharp, functional]

# Dependency graph
requires:
  - phase: 08-fslit-coverage
    provides: fslit test infrastructure and conventions
provides:
  - "|> pipe operator (reversed application)"
  - ">> forward composition operator"
  - "<< backward composition operator"
  - "11 fslit integration tests for pipe/composition"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Unique variable naming for closure composition (avoids stack overflow in chained composition)"

key-files:
  created:
    - tests/flt/expr/pipe-basic.flt
    - tests/flt/expr/pipe-chain.flt
    - tests/flt/expr/compose-right.flt
    - tests/flt/expr/compose-left.flt
    - tests/flt/expr/compose-chain.flt
    - tests/flt/emit/ast-expr/ast-expr-pipe.flt
    - tests/flt/emit/ast-expr/ast-expr-compose-right.flt
    - tests/flt/emit/ast-expr/ast-expr-compose-left.flt
    - tests/flt/emit/type-expr/type-expr-pipe.flt
    - tests/flt/emit/type-expr/type-expr-compose.flt
    - tests/flt/file/pipe-with-prelude.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Format.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Infer.fs

key-decisions:
  - "Unique compose variable names per closure (composeCounter) to avoid stack overflow in chained composition"
  - "Pipe/composition precedence: PIPE_RIGHT < COMPOSE_RIGHT = COMPOSE_LEFT < OR (lowest of all operators)"
  - "File-mode pipe test uses declared functions instead of prelude (no Prelude.fun file exists)"

patterns-established:
  - "Closure-based composition: compose operators capture evaluated function values in closure with unique names"

# Metrics
duration: 8min
completed: 2026-03-10
---

# Phase 09 Plan 01: Pipe and Composition Operators Summary

**F#-style |>, >>, << operators across full pipeline: lexer, parser, AST, type checker, evaluator, with 11 fslit tests**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-10T06:21:01Z
- **Completed:** 2026-03-10T06:29:06Z
- **Tasks:** 3
- **Files modified:** 19 (8 source + 11 test files)

## Accomplishments
- Full pipe operator support: `x |> f` evaluates as `f(x)` with correct left-to-right chaining
- Forward composition `f >> g` and backward composition `f << g` with correct type inference
- 11 new fslit tests covering eval, AST emit, type emit, and file mode

## Task Commits

Each task was committed atomically:

1. **Task 1: Lexer + Parser + AST foundation** - `58ba87c` (feat)
2. **Task 2: Type checking + Evaluation + exhaustive match updates** - `1e7c34e` (feat)
3. **Task 3: fslit integration tests** - `800da59` (feat)

## Files Created/Modified
- `src/LangThree/Ast.fs` - PipeRight, ComposeRight, ComposeLeft Expr variants + spanOf cases
- `src/LangThree/Lexer.fsl` - |>, >>, << token rules (before single-char prefixes)
- `src/LangThree/Parser.fsy` - Token declarations, precedence, grammar rules
- `src/LangThree/Format.fs` - formatAst and formatToken cases for new nodes
- `src/LangThree/Bidir.fs` - synth cases for pipe (reversed application) and composition (arrow unification)
- `src/LangThree/Eval.fs` - eval cases with closure-based composition using unique variable names
- `src/LangThree/TypeCheck.fs` - Traversal cases in collectMatches, collectModuleRefs, rewriteModuleAccess, collectTryWiths
- `src/LangThree/Infer.fs` - Stub cases in deprecated inferWithContext for pattern completeness

## Decisions Made
- **Unique compose variable names:** Chained composition (`f >> g >> h`) caused stack overflow when all closures used the same `__compose_x` parameter name. Fixed by using a mutable counter to generate unique names (`__compose_x_1`, `__compose_f_1`, etc.) per closure.
- **Minimal closure env:** Composition closures use `Map.ofList` with only the captured function values instead of the full `env`, avoiding env bloat.
- **Precedence:** Pipe and composition are the lowest-precedence operators (below OR), with `<<` right-associative and `|>`, `>>` left-associative.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Stack overflow in chained composition**
- **Found during:** Task 3 (compose-chain.flt test)
- **Issue:** All composed closures used identical parameter name `__compose_x`, causing infinite recursion when chained
- **Fix:** Added `composeCounter` for unique variable names per composition closure
- **Files modified:** src/LangThree/Eval.fs
- **Verification:** `(fun x -> x + 1) >> (fun x -> x * 2) >> (fun x -> x - 1)` applied to 3 gives 7
- **Committed in:** 800da59 (Task 3 commit)

**2. [Rule 3 - Blocking] Prelude.fun not found for file-mode test**
- **Found during:** Task 3 (pipe-with-prelude.flt test)
- **Issue:** Plan specified using `filter` prelude function, but no Prelude.fun file exists
- **Fix:** Changed test to use declared functions (`let double x = x * 2`) instead of prelude
- **Files modified:** tests/flt/file/pipe-with-prelude.flt
- **Committed in:** 800da59 (Task 3 commit)

**3. [Rule 2 - Missing Critical] Infer.fs incomplete pattern match**
- **Found during:** Task 2 (build warnings)
- **Issue:** Deprecated `inferWithContext` in Infer.fs missing cases for new Expr variants
- **Fix:** Added stub cases returning `(empty, freshVar())`
- **Files modified:** src/LangThree/Infer.fs
- **Committed in:** 1e7c34e (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (1 bug, 1 blocking, 1 missing critical)
**Impact on plan:** All fixes necessary for correctness. No scope creep.

## Issues Encountered
None beyond the auto-fixed deviations above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Pipe and composition operators fully operational
- All 196 F# tests + 179 fslit tests pass (zero regressions)
- Ready for next phase of v1.2 milestone

---
*Phase: 09-pipe-composition*
*Completed: 2026-03-10*