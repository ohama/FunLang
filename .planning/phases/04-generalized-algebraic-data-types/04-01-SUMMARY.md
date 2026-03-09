---
phase: 04-generalized-algebraic-data-types
plan: 01
subsystem: type-system
tags: [gadt, ast, parser, diagnostics, type-inference]

# Dependency graph
requires:
  - phase: 02-algebraic-data-types
    provides: ConstructorDecl, ConstructorInfo, ConstructorEnv, type declaration parser
  - phase: 03-records
    provides: substTypeExprWithMap shared helper, RecordEnv pattern
provides:
  - GadtConstructorDecl AST node for GADT constructor syntax
  - TEData TypeExpr variant for parameterized named types
  - Extended ConstructorInfo with IsGadt and ExistentialVars fields
  - GADT parser grammar (type application and GADT constructor rules)
  - GADT diagnostic kinds (E0401, E0402, E0403)
affects: [04-02, 04-03, 04-04, 04-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "GADT constructor elaboration with existential variable detection"
    - "Type application syntax via AtomicType IDENT grammar rule"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Type.fs
    - src/LangThree/Diagnostic.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Elaborate.fs
    - src/LangThree/Format.fs

key-decisions:
  - "GADT constructor uses splitGadt to decompose TEArrow into argTypes and returnType"
  - "Existential vars computed as set difference of arg vars minus result vars"
  - "TEData used for parameterized named types (e.g., int expr) separate from TEName"

patterns-established:
  - "GADT constructor elaboration: detect IsGadt by comparing elaborated result type to generic result type"
  - "Type application grammar: AtomicType IDENT produces TEData"

# Metrics
duration: 3min
completed: 2026-03-09
---

# Phase 4 Plan 01: GADT Foundation Summary

**GadtConstructorDecl AST node, TEData type expression, GADT parser grammar, and three GADT diagnostic kinds (E0401-E0403)**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-09T06:48:31Z
- **Completed:** 2026-03-09T06:51:22Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- GadtConstructorDecl variant in ConstructorDecl union with argTypes, returnType, and span
- TEData variant in TypeExpr for parameterized named types (e.g., `int expr`)
- ConstructorInfo extended with IsGadt and ExistentialVars for GADT metadata
- Parser accepts GADT constructor syntax (`Name : Type -> ReturnType`) and type application (`int expr`)
- Three GADT diagnostic kinds: GadtAnnotationRequired, ExistentialEscape, GadtReturnTypeMismatch
- All 115 existing tests pass, no LALR conflicts introduced

## Task Commits

Each task was committed atomically:

1. **Task 1: AST, Type, and Diagnostic extensions for GADT** - `4f7db86` (feat)
2. **Task 2: Parser grammar for GADT constructor syntax** - `75f36e9` (feat)

## Files Created/Modified
- `src/LangThree/Ast.fs` - GadtConstructorDecl variant, TEData variant, updated constructorSpanOf
- `src/LangThree/Type.fs` - IsGadt and ExistentialVars fields on ConstructorInfo
- `src/LangThree/Diagnostic.fs` - GadtAnnotationRequired, ExistentialEscape, GadtReturnTypeMismatch error kinds
- `src/LangThree/Parser.fsy` - GADT constructor rule and type application rule
- `src/LangThree/Elaborate.fs` - TEData handling, GadtConstructorDecl elaboration with existential detection
- `src/LangThree/Format.fs` - TEData formatting support

## Decisions Made
- Used `splitGadt` helper in parser action to decompose `TEArrow(args, ret)` into argTypes list and returnType, keeping a single grammar rule to avoid LALR conflicts
- Existential variables detected automatically: vars in arg types but not in result type
- IsGadt flag set when elaborated return type differs from the generic TData result type
- TEData is a separate TypeExpr variant from TEName to distinguish parameterized from bare named types

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- GADT data structures ready for type checking (plan 04-02)
- Parser can parse GADT constructor declarations for integration testing
- Diagnostic kinds ready for error reporting in GADT type checker

---
*Phase: 04-generalized-algebraic-data-types*
*Completed: 2026-03-09*
