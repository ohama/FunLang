---
phase: 65-letrecdecl-ast-refactoring
plan: 01
subsystem: ast
tags: [fsharp, ast, parser, type-annotations, letrec]

# Dependency graph
requires:
  - phase: 64-declaration-type-annotations
    provides: MixedParamList desugaring, LambdaAnnot AST node
provides:
  - LetRec and LetRecDecl AST nodes carry TypeExpr option for first parameter
  - Parser captures type annotations from MixedParamList instead of discarding
  - All 28+ pattern-match sites updated across 7 source files
affects: [65-02, 66-mutual-recursion]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TypeExpr option field in binding tuples for optional type annotations"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Format.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs

key-decisions:
  - "Added TypeExpr option as 3rd field in both LetRec DU case and LetRecDecl binding tuple, minimal change consistent with existing codebase style"
  - "Bidir.fs and Infer.fs updated mechanically (ignore new field) -- logic changes deferred to Plan 02"

patterns-established:
  - "Optional type annotation carried as TypeExpr option in binding tuples"

# Metrics
duration: 15min
completed: 2026-03-31
---

# Phase 65 Plan 01: AST Refactoring Summary

**LetRec/LetRecDecl AST expanded with TypeExpr option for first parameter type annotation -- 28 pattern-match sites updated across 7 files, all 648 flt + 224 unit tests passing**

## Performance

- **Duration:** 15 min
- **Started:** 2026-03-31T01:53:21Z
- **Completed:** 2026-03-31T02:08:19Z
- **Tasks:** 1
- **Files modified:** 7

## Accomplishments
- LetRec and LetRecDecl AST nodes now carry `TypeExpr option` for the first parameter
- Parser captures `Some ty` from LambdaAnnot and `None` from Lambda instead of discarding type info
- All pattern-match sites in TypeCheck.fs (7 sites), Eval.fs (2 sites), Format.fs (2 sites), Bidir.fs (1 site), Infer.fs (1 site), Ast.fs (2 definition + 1 spanOf) updated
- Format.fs pretty-prints type annotations when present: `f (x : int) = ...`

## Task Commits

Each task was committed atomically:

1. **Task 1: Change AST definitions and update all 28 pattern-match sites** - `c144f78` (feat)

## Files Created/Modified
- `src/LangThree/Ast.fs` - LetRec and LetRecDecl type definitions expanded, spanOf updated
- `src/LangThree/Parser.fsy` - All 14 LetRec + 14 LetRecDecl parser rules updated to capture/pass type
- `src/LangThree/TypeCheck.fs` - 7 destructuring sites updated (4 LetRec + 3 LetRecDecl)
- `src/LangThree/Eval.fs` - 2 sites updated (ignore type at runtime)
- `src/LangThree/Format.fs` - 2 sites updated to print type annotation when present
- `src/LangThree/Bidir.fs` - 1 LetRec pattern updated (logic change deferred)
- `src/LangThree/Infer.fs` - 1 LetRec pattern updated (logic change deferred)

## Decisions Made
- Added TypeExpr option as the 3rd positional field in both the LetRec DU case and the LetRecDecl binding tuple. This is the minimal change that follows existing codebase conventions (tuples, not records).
- Bidir.fs and Infer.fs were updated mechanically to compile (add `_` wildcard for new field). The actual logic change to USE the type annotation for constraining paramTy is deferred to Plan 02.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Updated Bidir.fs and Infer.fs pattern matches**
- **Found during:** Task 1 (AST changes)
- **Issue:** Plan listed Bidir.fs and Infer.fs under "key logic changes for Plan 02" but did not include them in Plan 01's task. However, they destructure LetRec and would fail to compile without updating the pattern.
- **Fix:** Added `_paramTyOpt` wildcard to both files' LetRec patterns
- **Files modified:** src/LangThree/Bidir.fs, src/LangThree/Infer.fs
- **Verification:** Build succeeds, all tests pass
- **Committed in:** c144f78

---

**Total deviations:** 1 auto-fixed (1 missing critical)
**Impact on plan:** Essential for compilation. No scope creep -- logic changes still deferred to Plan 02.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- AST carries first param type info, ready for Plan 02 to implement type enforcement
- Plan 02 will update Bidir.fs, Infer.fs, and TypeCheck.fs to actually USE paramTyOpt for constraining paramTy

---
*Phase: 65-letrecdecl-ast-refactoring*
*Completed: 2026-03-31*
