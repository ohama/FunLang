---
phase: 64-declaration-type-annotations
plan: 01
subsystem: parser
tags: [parser, grammar, fslexYacc, type-annotations, let-declarations, desugar]

# Dependency graph
requires:
  - phase: 63-angle-bracket-generics
    provides: TypeExpr grammar supporting angle bracket generics (used in annotated params)
  - phase: 60-02
    provides: LambdaAnnot and AnnotParam already exist in AST/Parser for lambda params
provides:
  - MixedParamList grammar rule (plain + annotated params in any order)
  - MixedParam grammar rule (IDENT | (IDENT : TypeExpr))
  - desugarMixedParams helper that produces Lambda/LambdaAnnot chains
  - MixedParamList productions in Decl, Expr let, Expr let rec, LetRecDeclaration, LetRecContinuation
affects:
  - 64-02 (return type annotations will extend the same grammar sections)
  - future phases using let declarations with type annotations

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "MixedParamList subsumes ParamList: remove old ParamList productions when both conflict"
    - "desugarMixedParams: Choice<string, string * TypeExpr> list desugared to Lambda/LambdaAnnot chain"
    - "LetRecDecl first-param extraction: match Lambda | LambdaAnnot to handle both cases"

key-files:
  created: []
  modified:
    - src/LangThree/Parser.fsy

key-decisions:
  - "Remove duplicate ParamList productions after adding MixedParamList to resolve reduce/reduce conflicts"
  - "MixedParamList placed BEFORE ParamList in grammar file to ensure parser prefers it"
  - "LetRecContinuation ParamList productions removed entirely (MixedParamList handles all cases)"
  - "Expr let rec also updated with MixedParamList (not just LetRecDeclaration)"

patterns-established:
  - "Pattern: MixedParamList subsumes ParamList - when both exist for same IDENT lookahead, keep only MixedParamList"
  - "Pattern: LetRecDecl first-param extraction must match both Lambda and LambdaAnnot branches"

# Metrics
duration: 6min
completed: 2026-03-30
---

# Phase 64 Plan 01: Declaration Type Annotations - Parser Foundation Summary

**MixedParamList grammar rule enabling `let f (x : int) y (z : bool) = ...` across all let-forms, with desugarMixedParams helper producing Lambda/LambdaAnnot chains**

## Performance

- **Duration:** 6 min
- **Started:** 2026-03-30T08:21:46Z
- **Completed:** 2026-03-30T08:28:06Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- Added `desugarMixedParams` helper to Parser.fsy preamble — takes `Choice<string, string * TypeExpr> list` and produces nested `Lambda`/`LambdaAnnot` chain
- Added `MixedParam` and `MixedParamList` grammar rules enabling mixed plain and annotated params
- Extended all five let-form grammar sections: Decl, Expr let, Expr let rec, LetRecDeclaration, LetRecContinuation
- Zero regressions: 224 unit tests and 643/643 flt integration tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Add desugarMixedParams helper and MixedParam/MixedParamList grammar rules** - `04b4f28` (feat)
2. **Task 2: Add MixedParamList productions to all let-forms** - `98b5e2b` (feat)

## Files Created/Modified
- `src/LangThree/Parser.fsy` - Added desugarMixedParams helper, MixedParam/MixedParamList grammar rules, and MixedParamList productions in all five let-form grammar sections

## Decisions Made
- **Remove duplicate ParamList productions**: After adding MixedParamList productions, `reduce/reduce` conflict appeared on `EQUALS` token between `ParamList: IDENT` and `MixedParam: IDENT`. Resolution: remove old ParamList productions since MixedParamList (with `Choice1Of2 IDENT`) is a strict superset. All 643 tests continued to pass after removal.
- **Expr let rec updated**: Plan specified Decl/LetRecDeclaration/LetRecContinuation — also updated Expr-level `let rec ... in` for completeness (consistent with research recommendation).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added MixedParamList to Expr-level let rec**
- **Found during:** Task 2 (Add MixedParamList productions)
- **Issue:** Plan specified four sections but Expr-level `LET REC IDENT ParamList EQUALS Expr IN SeqExpr` also needed updating for consistency
- **Fix:** Added `LET REC IDENT MixedParamList EQUALS Expr IN SeqExpr` (inline and INDENT variants) and removed old ParamList variant
- **Files modified:** src/LangThree/Parser.fsy
- **Verification:** All existing let rec expression tests continue to pass
- **Committed in:** 98b5e2b (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (missing critical)
**Impact on plan:** Necessary for completeness — all let-rec forms now support annotated params. No scope creep.

## Issues Encountered
- Initial addition of both MixedParamList and ParamList productions caused `reduce/reduce` conflict: `state 605 on terminal EQUALS between ParamList:'IDENT' and MixedParam:'IDENT'`. Resolved by removing redundant ParamList productions. Plan explicitly anticipated this and provided the resolution.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- MixedParamList grammar foundation complete — ready for Plan 02 (return type annotations with COLON TypeExpr)
- All existing tests pass — no regressions introduced
- desugarMixedParams helper available for reuse in return type annotation desugaring

---
*Phase: 64-declaration-type-annotations*
*Completed: 2026-03-30*
