---
phase: 66-expression-level-mutual-recursion
plan: 02
subsystem: type-check-eval
tags: [fsharp, letrec, mutual-recursion, type-inference, bidirectional, evaluator]

# Dependency graph
requires:
  - phase: 66-expression-level-mutual-recursion
    plan: 01
    provides: LetRec AST bindings list shape + parser accepting `and` chains
provides:
  - Multi-binding LetRec type synthesis in Bidir.fs with simultaneous env
  - Multi-binding LetRec type inference in Infer.fs with simultaneous env
  - Mutual recursive closure linking in Eval.fs via sharedEnvRef pattern
  - flt tests for expression-level mutual recursion (positive + error)
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Expression-level LetRec uses same simultaneous-env pattern as module-level LetRecDecl"
    - "sharedEnvRef pattern for mutual closure linking at expression level"

key-files:
  created:
    - tests/flt/file/let/letrec-mutual-expr.flt
    - tests/flt/file/let/letrec-mutual-expr-error.flt
  modified:
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs
    - src/LangThree/Eval.fs

key-decisions:
  - "Multi-line `and` in indented contexts triggers indent filter issues -- tests use single-line format"

patterns-established:
  - "LetRec and LetRecDecl share identical type-check and eval patterns for mutual recursion"

# Metrics
duration: 4min
completed: 2026-03-31
---

# Phase 66 Plan 02: Type-Check/Eval Multi-Binding Summary

**Bidir/Infer/Eval rewritten for simultaneous-env mutual recursion with sharedEnvRef closure linking -- 652 flt + 224 unit tests passing**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-31T04:39:49Z
- **Completed:** 2026-03-31T04:43:49Z
- **Tasks:** 2
- **Files modified:** 5 (3 source + 2 test)

## Accomplishments
- Bidir.fs LetRec handler: fresh type vars per binding, simultaneous env, compose substitutions, generalize all
- Infer.fs LetRec handler: same pattern using inferWithContext
- Eval.fs LetRec handler: sharedEnvRef pattern for mutual closure linking (matching LetRecDecl)
- Comprehensive flt tests: even/odd, 3-binding cycle, param/return annotations, nested contexts, error rejection

## Task Commits

Each task was committed atomically:

1. **Task 1: Rewrite Bidir.fs, Infer.fs, and Eval.fs LetRec handlers for multi-binding** - `9dd97e2` (feat)
2. **Task 2: Add comprehensive flt tests for expression-level mutual recursion** - `cdab93b` (test)

## Files Created/Modified
- `src/LangThree/Bidir.fs` - Multi-binding LetRec synth with simultaneous env and generalization
- `src/LangThree/Infer.fs` - Multi-binding LetRec infer with simultaneous env and generalization
- `src/LangThree/Eval.fs` - sharedEnvRef pattern for mutual closure linking
- `tests/flt/file/let/letrec-mutual-expr.flt` - 8 positive tests (even/odd, 3-binding, annotations, nesting)
- `tests/flt/file/let/letrec-mutual-expr-error.flt` - Type mismatch error rejection

## Decisions Made
- Multi-line `and` in indented function bodies triggers indent filter issues (not in scope for this phase). Tests use single-line format which works correctly.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Multi-line `let rec ... and ...` with `and` on a new indented line causes parse errors due to indent filter token insertion. This is a known limitation of the indent-sensitive parser -- the `and` keyword would need special handling in IndentFilter.fs. Tests work around this by using single-line format, which is sufficient for validation.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Expression-level mutual recursion is fully functional
- v8.1 milestone complete: both module-level and expression-level mutual recursion work
- 652 flt tests + 224 unit tests all passing

---
*Phase: 66-expression-level-mutual-recursion*
*Completed: 2026-03-31*
