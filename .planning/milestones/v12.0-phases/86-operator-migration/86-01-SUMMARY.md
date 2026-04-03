---
phase: 86-operator-migration
plan: 01
subsystem: compiler
tags: [fsharp, funlang, operators, prelude, lexer, parser, ast, eval, fixity]

# Dependency graph
requires:
  - phase: 85-fixity-system
    provides: FixityEnv infrastructure with collectFixity and rewriteFixity for #[left/right N] attributes
provides:
  - "|>, >>, << defined as Prelude functions with fixity attributes in Core.fun"
  - "Compiler without PipeRight/ComposeRight/ComposeLeft AST nodes"
  - "applyFunc self-name injection guard preventing closure variable overwrite"
affects:
  - phase-87-stdlib-operators
  - any future Prelude operator definitions

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Operator-as-function: language operators defined in Prelude using #[left/right N] fixity"
    - "Mangled param names: Prelude operator params use __prefix_ to avoid user-name collision"
    - "applyFunc guard: self-name injection only when name absent from closure env"

key-files:
  created: []
  modified:
    - Prelude/Core.fun
    - src/FunLang/Lexer.fsl
    - src/FunLang/Parser.fsy
    - src/FunLang/Ast.fs
    - src/FunLang/IndentFilter.fs
    - src/FunLang/Eval.fs
    - src/FunLang/Bidir.fs
    - src/FunLang/Infer.fs
    - src/FunLang/TypeCheck.fs
    - src/FunLang/Format.fs
    - src/FunLang/FixityEnv.fs
    - tests/FunLang.Tests/TypeAnnotationTests.fs
    - tests/flt/emit/ast-expr/ast-expr-pipe.flt
    - tests/flt/emit/ast-expr/ast-expr-compose-right.flt
    - tests/flt/emit/ast-expr/ast-expr-compose-left.flt

key-decisions:
  - "Use mangled parameter names (__pipe_x, __comp_lhs, __comp_rhs, __comp_x) in Prelude operators to prevent applyFunc self-name injection from overwriting captured closure variables"
  - "Fix applyFunc to only inject self-name when name is NOT already in closure env - prevents infinite recursion in compose chains"
  - "Remove composeCounter mutable from Eval.fs since ComposeRight/Left handlers were deleted"
  - "Keep compose function in Prelude (not removed) because prelude-compose.flt test references it"

patterns-established:
  - "Prelude operators: use #[left/right N] attributes for fixity, mangled __prefix_ params to avoid collisions"
  - "applyFunc guard: Map.containsKey check before self-name injection is now standard"

# Metrics
duration: 23min
completed: 2026-04-03
---

# Phase 86 Plan 01: Operator Migration Summary

**|>, >>, << migrated from 39-reference special AST infrastructure to 3 Prelude function definitions with fixity attributes; applyFunc closure-injection guard added to fix compose chain infinite recursion**

## Performance

- **Duration:** 23 min
- **Started:** 2026-04-03T08:23:04Z
- **Completed:** 2026-04-03T08:45:44Z
- **Tasks:** 3
- **Files modified:** 15

## Accomplishments
- Removed 39 references to PipeRight/ComposeRight/ComposeLeft/PIPE_RIGHT/COMPOSE_RIGHT/COMPOSE_LEFT across 10 source files
- Added `|>`, `>>`, `<<` to Prelude/Core.fun with `#[left 1]`, `#[right 2]`, `#[left 2]` fixity attributes
- Discovered and fixed a critical applyFunc bug: self-name injection was overwriting captured closure variables, causing infinite recursion in compose chains
- 721/722 flt tests pass (1 pre-existing err-occurs-check failure)
- 244/244 F# unit tests pass

## Task Commits

Each task was committed atomically:

1. **Tasks 1+2: Remove infrastructure, add Prelude defs** - `eedb513` (feat)
2. **Task 3: Fix tests and closures** - `55350cb` (feat)

**Plan metadata:** `[pending]` (docs)

## Files Created/Modified
- `Prelude/Core.fun` - Added |>, >>, << with fixity attributes and mangled param names
- `src/FunLang/Lexer.fsl` - Removed PIPE_RIGHT/COMPOSE_RIGHT/COMPOSE_LEFT token rules
- `src/FunLang/Parser.fsy` - Removed token declarations, precedences, and grammar rules
- `src/FunLang/Ast.fs` - Removed PipeRight/ComposeRight/ComposeLeft from Expr DU and spanOf
- `src/FunLang/IndentFilter.fs` - Removed PIPE_RIGHT/COMPOSE_RIGHT/COMPOSE_LEFT from isContinuationStart
- `src/FunLang/Eval.fs` - Removed 40-line handler block, composeCounter, fixed applyFunc guard
- `src/FunLang/Bidir.fs` - Removed 37-line synth handler block
- `src/FunLang/Infer.fs` - Removed 2-line stub
- `src/FunLang/TypeCheck.fs` - Removed 6 handler lines across 4 functions
- `src/FunLang/Format.fs` - Removed formatAst and formatToken cases
- `src/FunLang/FixityEnv.fs` - Removed mapExprChildren cases
- `tests/FunLang.Tests/TypeAnnotationTests.fs` - Removed AST pattern match
- `tests/flt/emit/ast-expr/ast-expr-pipe.flt` - Updated to expect App(App(Var "|>"), ...) format
- `tests/flt/emit/ast-expr/ast-expr-compose-right.flt` - Updated to expect App(App(Var ">>"), ...) format
- `tests/flt/emit/ast-expr/ast-expr-compose-left.flt` - Updated to expect App(App(Var "<<"), ...) format

## Decisions Made
- Use mangled parameter names (`__pipe_x`, `__comp_lhs`, `__comp_rhs`, `__comp_x`) in Prelude operator definitions. Rationale: `applyFunc` injects the call-site name into the closure env for recursive function support. If `f >> g` is called as `pipeline`, and the composed function's body references `__comp_lhs`, injecting `"pipeline"` is harmless. But if the name `f` is used as a param name AND the user calls the composed result via a variable named `f`, the injection overwrites the captured `f`. Mangled names prevent any realistic collision.
- Fix `applyFunc` to guard self-name injection: `not (Map.containsKey name closureEnv)`. Rationale: Without this guard, calling `pipeline_inner` (stored as `__comp_rhs` in an outer compose closure) via `Var "__comp_rhs"` would inject `"__comp_rhs" → pipeline_inner` into the inner compose closure, overwriting its `__comp_rhs = sub3` binding and causing infinite recursion. The guard ensures we only inject for genuinely recursive functions where the self-name is absent from the closure.
- Keep `compose` function in Prelude (not removed). Rationale: `tests/flt/file/prelude/prelude-compose.flt` calls `compose inc double` directly, so removing it would break a test.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Format.fs had additional token formatter cases not listed in plan**
- **Found during:** Task 2 (build failure)
- **Issue:** `formatToken` in Format.fs had `Parser.PIPE_RIGHT/COMPOSE_RIGHT/COMPOSE_LEFT` cases (~line 100) that were separate from the `formatAst` cases listed in the plan. Build failed with 3 errors.
- **Fix:** Removed `formatToken` cases for the 3 tokens.
- **Files modified:** `src/FunLang/Format.fs`
- **Committed in:** eedb513 (combined task 1+2 commit)

**2. [Rule 1 - Bug] Infinite recursion in compose chains due to applyFunc self-name injection**
- **Found during:** Task 3 (flt test failures)
- **Issue:** `applyFunc` unconditionally added the call-site var name to the closure, overwriting captured variables. `add1 >> mul2 >> sub3` evaluated `pipeline_inner` via `Var "__comp_rhs"`, injecting `"__comp_rhs" → pipeline_inner` and overwriting `"__comp_rhs" → sub3`.
- **Fix:** Added `not (Map.containsKey name closureEnv)` guard to self-name injection in `applyFunc`.
- **Files modified:** `src/FunLang/Eval.fs`
- **Committed in:** 55350cb (task 3 commit)

**3. [Rule 1 - Bug] TypeAnnotationTests.fs had PipeRight/ComposeRight/ComposeLeft reference**
- **Found during:** Task 2 (grep of tests/)
- **Issue:** The unit test helper `collectSpans` had a pattern match on pipe/compose AST nodes.
- **Fix:** Removed the pattern match line (App/Cons line already handles these cases via the App node path after migration).
- **Files modified:** `tests/FunLang.Tests/TypeAnnotationTests.fs`
- **Committed in:** eedb513

---

**Total deviations:** 3 auto-fixed (2 bugs, 1 missing file)
**Impact on plan:** All auto-fixes necessary for correctness. The applyFunc fix is a genuine compiler correctness improvement that affects all higher-order function composition patterns.

## Issues Encountered
- The compose chain infinite recursion was subtle: it only appeared at runtime, not at compile time. Single compose `f >> g` worked but chains `f >> g >> h` failed. Root cause was the interaction between `applyFunc`'s recursive-function support mechanism and composed function closures with shared parameter names.

## Next Phase Readiness
- Phase 87 (stdlib operators) can proceed: |>, >>, << are now fully Prelude-defined
- The applyFunc guard fix benefits all future Prelude operator definitions that use closures
- Pre-existing `err-occurs-check.flt` failure unchanged (documented in STATE.md)

---
*Phase: 86-operator-migration*
*Completed: 2026-04-03*
