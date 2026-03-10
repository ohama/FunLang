# Phase 6 Plan 02: Exception Type Checking Summary

Exception elaboration, raise/try-with type checking, when guard checking, and open-type exhaustiveness for exception handlers.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Exception elaboration and TypeCheck integration | f831242 | Elaborate.fs, TypeCheck.fs |
| 2 | Raise, TryWith, and when guard type checking | c263d40 | Bidir.fs, TypeCheck.fs, IndentFilterTests.fs |

## Implementation Details

### Exception Elaboration
- `elaborateExceptionDecl` creates ConstructorInfo with `TypeParams=[]`, `ResultType=TExn`, `IsGadt=false`
- Exception declarations processed in typeCheckDecls first pass alongside type/record declarations
- Exception constructors added to both ctorEnv (for pattern matching) and typeEnv (as functions: `ArgType -> TExn` or monomorphic `TExn`)
- Module exports automatically include exception constructors via existing ctorEnv diff logic

### Raise Expression
- Argument synthesized and unified with TExn
- Returns fresh type variable (divergent expression, compatible with any expected type)

### TryWith Expression
- Body synthesized to get body type
- Each handler pattern unified with TExn
- When guards type-checked as TBool in handler scope
- Handler body types unified with body type
- Substitutions properly composed through guard/pattern/body chain

### When Guards in Match
- Synth mode: guard expression synthesized in clause environment, unified with TBool
- GADT check mode: guard synthesized in refined branch environment, unified with TBool
- Regular ADT check mode: same pattern applied
- Guard substitution composed into overall clause substitution

### Exhaustiveness
- Guarded match clauses excluded from exhaustiveness matrix (since guards may fail)
- Try-with handlers checked for catch-all pattern (WildcardPat or VarPat without guard)
- Missing catch-all emits W0003 warning
- TExn returns empty constructor set from getConstructorsFromEnv (open type, no ADT-style exhaustiveness)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] IndentFilterTests missing JustSawTry field**
- **Found during:** Task 2 (test run)
- **Issue:** Plan 06-01 added JustSawTry to FilterState but didn't update test record literals
- **Fix:** Added `JustSawTry = false` to all 5 FilterState records in IndentFilterTests.fs
- **Commit:** c263d40

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Guarded patterns excluded from exhaustiveness | A pattern with when guard may not match even if structurally correct |
| W0003 for try-with without catch-all | Open type means individual exception matching can never be exhaustive |
| Exception constructors added to typeEnv | Enables using exception constructors as functions (e.g., passing to higher-order functions) |

## Metrics

- **Duration:** ~5 min
- **Tests:** 149/149 passing
- **Completed:** 2026-03-09
