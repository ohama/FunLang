---
phase: 102-fix-lambdaannot-span-collision
plan: 01
subsystem: parser
tags: [parser, spans, lambdaannot, desugaring, annotation-map]

# Dependency graph
requires:
  - phase: 91-95 (v14.0 Prelude polish)
    provides: stable parser grammar used as base for this fix
provides:
  - Per-parameter span assignment in desugarAnnotParams (string * TypeExpr * Span list)
  - Per-parameter span assignment in desugarMixedParams (Choice<string * Span, string * TypeExpr * Span> list)
  - Unique LambdaAnnot spans for every nested param in annotated multi-param functions
affects:
  - annotationMap reliability in TypeChecker.fs / Elaborate.fs
  - Phase 96+ (type class correctness depends on accurate annotation spans)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-param span: grammar rules emit (name, ty, span) tuples; desugaring functions no longer take a shared outer span"

key-files:
  created: []
  modified:
    - src/FunLang/Parser.fsy

key-decisions:
  - "Span embedded in each parameter tuple at parse time, not passed as a single outer span to desugaring functions"
  - "AnnotParam emits ruleSpan parseState 1 5 (the full (name : Type) token range)"
  - "MixedParam plain case emits symSpan parseState 1 (the IDENT token only)"
  - "desugarMultiParamLambda left unchanged -- handles plain unannotated lambdas, out of scope"

patterns-established:
  - "Grammar rule tuples carry their own spans: (name, ty, span) instead of passing span to desugaring function"

# Metrics
duration: 12min
completed: 2026-04-10
---

# Phase 102 Plan 01: Fix LambdaAnnot Span Collision Summary

**Per-parameter span injection into AnnotParam and MixedParam grammar rules, eliminating the span collision in desugarAnnotParams/desugarMixedParams that caused annotationMap to overwrite inner param types with the outermost arrow type (Issue #18)**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-04-10T00:00:00Z
- **Completed:** 2026-04-10T00:12:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Changed `desugarAnnotParams` signature from `(string * TypeExpr) list -> Expr -> Span -> Expr` to `(string * TypeExpr * Span) list -> Expr -> Expr`; each LambdaAnnot node now uses its own parameter span
- Changed `desugarMixedParams` signature from `Choice<string, string * TypeExpr> list -> Expr -> Span -> Expr` to `Choice<string * Span, string * TypeExpr * Span> list -> Expr -> Expr`; each Lambda/LambdaAnnot node now uses its own parameter span
- Updated `AnnotParam` rule to emit `(name, ty, ruleSpan parseState 1 5)` covering the full `(name : Type)` token range
- Updated `MixedParam` rules: plain IDENT emits `Choice1Of2 (name, symSpan parseState 1)`, annotated param emits `Choice2Of2 (name, ty, ruleSpan parseState 1 5)`
- Removed trailing shared-span argument from all 33+ callsites of both functions across SeqExpr, LetDecl, LetRecDecl, LetRecContinuation, InstanceMethod, and operator declaration rules
- All 725 flt tests pass, all 244 F# unit tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Update desugaring functions and grammar rules for per-parameter spans** - `cbda855` (fix)

## Files Created/Modified
- `src/FunLang/Parser.fsy` - desugarAnnotParams, desugarMixedParams, AnnotParam, MixedParam, and all callsites updated

## Decisions Made
- Span is captured at the grammar rule level (AnnotParam, MixedParam) and stored in the parameter tuple, so the desugaring function receives all necessary information without a separate outer span argument.
- `desugarMultiParamLambda` was intentionally left unchanged -- it handles unannotated lambdas where span collision is not an issue.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Parser fix complete; annotationMap will now correctly record distinct spans for each nested LambdaAnnot node in multi-param annotated functions
- Ready to plan Phase 96 (Correctness Foundations) or add a regression test for Issue #18

---
*Phase: 102-fix-lambdaannot-span-collision*
*Completed: 2026-04-10*
