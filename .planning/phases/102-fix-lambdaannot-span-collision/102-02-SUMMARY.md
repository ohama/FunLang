---
phase: 102-fix-lambdaannot-span-collision
plan: 02
subsystem: testing
tags: [type-annotation, lambdaannot, span, annotationmap, regression-test, flt]

# Dependency graph
requires:
  - phase: 102-01
    provides: per-parameter span injection in desugarAnnotParams/desugarMixedParams (Issue #18 fix)
provides:
  - TA-08 unit test proving distinct TArrow spans per LambdaAnnot in annotationMap
  - flt regression test for multi-param annotated function end-to-end evaluation
  - parseModuleWithPositions helper in TypeAnnotationTests.fs for span-aware testing
affects: [future testing phases that verify annotationMap correctness]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "span-aware parsing in tests: use filterPositioned + lb.StartPos/lb.EndPos to preserve token positions through custom tokenizer"

key-files:
  created:
    - tests/flt/file/let/let-annot-multi-param-span.flt
  modified:
    - tests/FunLang.Tests/TypeAnnotationTests.fs

key-decisions:
  - "TA-08 uses parseModuleWithPositions (not parseModule) because the existing custom tokenizer discards token position info, causing all spans to collapse to (0,0)-(1,0)"
  - "parseModuleWithPositions mirrors Program.parseModuleFromString: sets lb.StartPos/lb.EndPos per token so ruleSpan/symSpan produce correct spans"

patterns-established:
  - "span-aware test pattern: lexAndFilterPositioned returns PositionedToken list; tokenizer sets lb.StartPos/lb.EndPos before returning each token"

# Metrics
duration: 15min
completed: 2026-04-10
---

# Phase 102 Plan 02: Test LambdaAnnot Span Collision Fix Summary

**TA-08 unit test + flt regression test proving each nested LambdaAnnot gets a distinct span and correct arrow type in annotationMap, confirming Issue #18 is resolved**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-04-10
- **Completed:** 2026-04-10
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Added `parseModuleWithPositions` helper to TypeAnnotationTests.fs that preserves token position info via `lb.StartPos`/`lb.EndPos` updates — mirrors `Program.parseModuleFromString`
- Added TA-08 test verifying 3 distinct TArrow entries in annotationMap for `let f (x : int) (y : string) (z : bool) : int = 42` with no span collisions (245 unit tests pass)
- Added `tests/flt/file/let/let-annot-multi-param-span.flt` end-to-end regression test: `add 10 20 30 = 60` (726 flt tests pass)

## Task Commits

1. **Task 1: Add TA-08 unit test for multi-param LambdaAnnot span uniqueness** - `c25b10f` (test)
2. **Task 2: Add flt regression test for multi-param annotated function** - `8717bbe` (test)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `tests/FunLang.Tests/TypeAnnotationTests.fs` - Added `lexAndFilterPositioned`, `parseModuleWithPositions` helpers and TA-08 test
- `tests/flt/file/let/let-annot-multi-param-span.flt` - New regression test for Issue #18

## Decisions Made
- TA-08 requires `parseModuleWithPositions` instead of the existing `parseModule` because the existing custom tokenizer collects only token values (no positions), causing `parseState.InputStartPosition(n)` to return `(0,0)` for all tokens. This collapses all LambdaAnnot spans to the same value, making the annotationMap overwrite itself and appear to have only 1 TArrow entry even after the fix. The new helper sets `lb.StartPos`/`lb.EndPos` per token, restoring correct span generation.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] parseModule helper discards token position info**
- **Found during:** Task 1 (TA-08 implementation)
- **Issue:** Existing `parseModule` uses a custom tokenizer that returns only token values without setting `lb.StartPos`/`lb.EndPos`, so all AST nodes get span `(0,0)-(1,0)`. TA-08 test failed: annotationMap had only 1 TArrow entry instead of 3.
- **Fix:** Added `lexAndFilterPositioned` (using `IndentFilter.filterPositioned`) and `parseModuleWithPositions` that sets `lb.StartPos <- pt.StartPos` and `lb.EndPos <- pt.EndPos` per token, matching `Program.parseModuleFromString`.
- **Files modified:** `tests/FunLang.Tests/TypeAnnotationTests.fs`
- **Verification:** TA-08 passes with 3 distinct TArrow entries; all 245 existing tests still pass
- **Committed in:** c25b10f (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug in test infrastructure)
**Impact on plan:** Fix was necessary to make the test actually validate the span-uniqueness property. No scope creep.

## Issues Encountered
The existing test helper `parseModule` does not preserve token position info, so span-based tests were impossible without a new position-aware variant. This was discovered during Task 1 and auto-fixed inline.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 102 (Fix LambdaAnnot Span Collision) is fully complete: both the fix (102-01) and the tests (102-02) are committed
- 245 F# unit tests passing, 726 flt tests passing
- Ready to proceed with Phase 96 (Correctness Foundations) or any other v15.0 phase

---
*Phase: 102-fix-lambdaannot-span-collision*
*Completed: 2026-04-10*
