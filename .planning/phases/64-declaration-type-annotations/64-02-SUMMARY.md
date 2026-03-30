---
phase: 64-declaration-type-annotations
plan: 02
subsystem: parser
tags: [parser, grammar, fslexYacc, type-annotations, return-type, let-declarations, annot]

# Dependency graph
requires:
  - phase: 64-01
    provides: MixedParamList grammar and desugarMixedParams helper
  - phase: 63-angle-bracket-generics
    provides: TypeExpr grammar supporting angle bracket generics
provides:
  - Return type annotation `: TypeExpr` before `=` in all let-forms
  - Decl-level: LET IDENT COLON TypeExpr EQUALS (no params) and LET IDENT MixedParamList COLON TypeExpr EQUALS
  - Expr-level: same patterns plus expression let/let rec with return type
  - LetRecDeclaration: MixedParamList + return type (inline and INDENT variants)
  - LetRecContinuation: MixedParamList + return type (inline and INDENT variants)
  - Expression let rec: MixedParamList + return type (inline and INDENT variants)
affects:
  - 64-03 and beyond (type checking of annotated let declarations)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Return type annotation wraps body: Annot(body, typeExpr, span)"
    - "Annot node is erased at runtime — type-checking only"
    - "MixedParamList + COLON TypeExpr EQUALS: return type after all params"

key-files:
  created: []
  modified:
    - src/LangThree/Parser.fsy

key-decisions:
  - "No ParamList + return type productions added: MixedParamList subsumes ParamList (Choice1Of2 covers all plain params)"
  - "Expression-level let rec with return type added for completeness (parallel with Decl-level)"

patterns-established:
  - "Pattern: return type annotation always wraps body — Annot(body, typeExpr, span)"
  - "Pattern: body index is after EQUALS token — $N where N = position after EQUALS"

# Metrics
duration: 4min
completed: 2026-03-30
---

# Phase 64 Plan 02: Return Type Annotations Summary

**Return type annotation `: TypeExpr` between params and `=` added to all let-forms, wrapping body in `Annot(body, typeExpr, span)` for type erasure at runtime**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-30T08:30:44Z
- **Completed:** 2026-03-30T08:34:45Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- Added `LET IDENT COLON TypeExpr EQUALS SeqExpr` productions to Decl (no-params return type: `let f : int = 42`)
- Added `LET IDENT MixedParamList COLON TypeExpr EQUALS SeqExpr` productions to Decl (params + return type: `let f x : int = x + 1`)
- Added same patterns to Expr-level let and expression-level let rec
- Added `LET REC IDENT MixedParamList COLON TypeExpr EQUALS SeqExpr` to LetRecDeclaration
- Added `AND_KW IDENT MixedParamList COLON TypeExpr EQUALS SeqExpr` to LetRecContinuation
- Zero regressions: 224 unit tests and 643/643 flt integration tests pass
- Smoke tests verified: `let add (x : int) : int = x + 1` and `let f : int = 42` both execute correctly

## Task Commits

Each task was committed atomically:

1. **Task 1: Add return type annotation productions to Decl and Expr let-forms** - `9502220` (feat)
2. **Task 2: Add return type annotation productions to LetRecDeclaration and LetRecContinuation** - `d6d2dcc` (feat)

## Files Created/Modified
- `src/LangThree/Parser.fsy` - Added return type annotation productions (75 lines added total across both tasks)

## Decisions Made
- **No ParamList + return type needed**: Since Plan 01 removed ParamList in favor of MixedParamList, only `MixedParamList + return type` productions are needed. Plain params (formerly ParamList) are handled via `Choice1Of2` in MixedParamList.
- **Expression-level let rec with return type added**: Matched Decl-level completeness — `LET REC IDENT MixedParamList COLON TypeExpr EQUALS Expr IN SeqExpr` added for consistency.

## Deviations from Plan

None — plan executed exactly as written. Plan explicitly noted that ParamList productions were removed in 64-01 and only MixedParamList + return type was needed.

## Issues Encountered
None — build succeeded on first attempt, all tests passed with no conflicts introduced.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Return type annotation parsing complete for all let-forms
- `Annot(body, typeExpr, span)` wrapping established — ready for type checking phase
- All existing tests pass — no regressions introduced
- Ready for Plan 03: type checking of annotated let declarations (bidirectional type checking using Annot node)

---
*Phase: 64-declaration-type-annotations*
*Completed: 2026-03-30*
