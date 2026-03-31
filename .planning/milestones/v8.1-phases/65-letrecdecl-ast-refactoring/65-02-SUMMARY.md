---
phase: 65-letrecdecl-ast-refactoring
plan: 02
subsystem: type-checking
tags: [fsharp, type-annotations, letrec, elaborateTypeExpr, bidirectional-typing]

# Dependency graph
requires:
  - phase: 65-letrecdecl-ast-refactoring
    plan: 01
    provides: LetRec/LetRecDecl AST nodes carry TypeExpr option for first parameter
  - phase: 64-declaration-type-annotations
    provides: elaborateTypeExpr function in Elaborate.fs
provides:
  - First-param type annotations on let rec bindings are enforced at type-check time
  - Type mismatches on annotated params produce clear error messages
  - flt tests covering positive and negative annotation enforcement cases
affects: [66-mutual-recursion]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "elaborateTypeExpr used to convert TypeExpr option to concrete Type for param binding"

key-files:
  created:
    - tests/flt/file/let/letrec-decl-param-annotation.flt
    - tests/flt/file/let/letrec-decl-param-annotation-error.flt
  modified:
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs

key-decisions:
  - "Used elaborateTypeExpr directly (bare call via open Elaborate) rather than any wrapper"
  - "No additional unify call needed -- existing unification pipeline catches mismatches naturally"

patterns-established:
  - "match paramTyOpt with Some -> elaborateTypeExpr | None -> freshVar() pattern for optional type annotations"

# Metrics
duration: 8min
completed: 2026-03-31
---

# Phase 65 Plan 02: Type Annotation Enforcement Summary

**elaborateTypeExpr wired into TypeCheck/Bidir/Infer for let rec first-param annotations -- type mismatches now rejected with clear errors, 650 flt tests passing**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-31T02:10:20Z
- **Completed:** 2026-03-31T02:18:20Z
- **Tasks:** 2
- **Files modified:** 5 (3 source + 2 test)

## Accomplishments
- TypeCheck.fs, Bidir.fs, and Infer.fs now use `elaborateTypeExpr` when paramTyOpt is Some, converting the TypeExpr annotation to a concrete Type that constrains the param binding
- Unannotated params continue to get `freshVar()` with no regression
- Positive flt tests: module-level mutual rec with annotated params, expression-level annotated and unannotated let rec
- Negative flt test: `let rec f (x : int) = x + true` correctly rejected with E0301 type mismatch error

## Task Commits

Each task was committed atomically:

1. **Task 1: Enforce first-param type annotation in TypeCheck, Bidir, and Infer** - `7e9dad7` (feat)
2. **Task 2: Add flt tests for annotation enforcement** - `955c23f` (test)

## Files Created/Modified
- `src/LangThree/TypeCheck.fs` - LetRecDecl funcTypes mapping uses elaborateTypeExpr for annotated params
- `src/LangThree/Bidir.fs` - LetRec synthesis uses elaborateTypeExpr for annotated params
- `src/LangThree/Infer.fs` - LetRec inference uses elaborateTypeExpr for annotated params
- `tests/flt/file/let/letrec-decl-param-annotation.flt` - Positive tests: mutual rec, expr-level, unannotated regression
- `tests/flt/file/let/letrec-decl-param-annotation-error.flt` - Negative test: type mismatch on annotated param

## Decisions Made
- Used `elaborateTypeExpr` directly via `open Elaborate` (bare function call) -- all three files already had this import from Plan 01
- No additional `unify` call needed: setting paramTy to a concrete type (e.g., TInt) means the existing unification in the body type-checking pipeline naturally catches mismatches
- Used `x + true` instead of `x + "hi"` in error flt test to avoid shell quoting issues with `--expr` flag

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Shell quoting of double-quotes inside `--expr` flag caused FsLit to receive malformed input. Resolved by using `true` (bool literal) instead of `"hi"` (string literal) for the type mismatch test case.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 65 is now complete: AST carries type annotations (Plan 01) and type checker enforces them (Plan 02)
- Ready for Phase 66 (mutual recursion) which builds on this foundation
- All 650 flt + 224 unit tests passing

---
*Phase: 65-letrecdecl-ast-refactoring*
*Completed: 2026-03-31*
