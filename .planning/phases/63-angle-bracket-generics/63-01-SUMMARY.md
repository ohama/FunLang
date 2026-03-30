---
phase: 63-angle-bracket-generics
plan: 01
subsystem: parser
tags: [parser, generics, type-system, fsy, lalr, angle-brackets]

# Dependency graph
requires:
  - phase: 62-prelude-extensions
    provides: stable parser baseline for type grammar extension
provides:
  - TypeArgList grammar rule for comma-separated type expressions
  - Angle bracket type syntax in AtomicType and AliasAtomicType
  - Angle bracket type declarations (ADT, alias, mutual recursion)
  - AngleBracketTypeParams rule for comma-separated type variable lists
affects:
  - 63-02 (type annotation syntax builds on these type expression rules)
  - 64-declaration-type-annotations (FunLexYacc compatibility uses angle bracket generics)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Angle bracket generics reuse LT/GT tokens — no new tokens needed"
    - "Separate AngleBracketTypeParams rule (comma-separated) distinct from TypeParams (space-separated)"
    - "TypeArgList in type expressions mirrors AngleBracketTypeParams in type declarations"

key-files:
  created: []
  modified:
    - src/LangThree/Parser.fsy

key-decisions:
  - "Reuse LT/GT tokens for angle brackets — LALR(1) disambiguates by parser state"
  - "Introduce AngleBracketTypeParams (comma-separated) separate from TypeParams (space-separated) because declarations need comma syntax inside < >"
  - "TypeArgList placed after AtomicType rule to avoid forward reference issues"

patterns-established:
  - "Angle bracket type syntax: IDENT LT TypeArgList GT in type expression positions"
  - "Angle bracket declaration syntax: TYPE IDENT LT AngleBracketTypeParams GT EQUALS ..."

# Metrics
duration: 18min
completed: 2026-03-30
---

# Phase 63 Plan 01: Angle Bracket Generics (Parser) Summary

**Parser angle bracket generic syntax: `Result<'a>`, `Map<'k, 'v>` in type expressions and declarations, reusing LT/GT tokens with zero new LALR conflicts**

## Performance

- **Duration:** 18 min
- **Started:** 2026-03-30T09:00:00Z
- **Completed:** 2026-03-30T09:18:00Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- Added `TypeArgList` rule and `IDENT LT TypeArgList GT` to `AtomicType` and `AliasAtomicType` for type expression positions
- Added `AngleBracketTypeParams` (comma-separated) rule for use inside angle bracket declarations
- Added 4 angle bracket variants to `TypeDeclaration` (inline, inline+pipe, indented, indented+pipe)
- Added angle bracket variant to `TypeAliasDeclaration`
- Added 2 angle bracket variants to `TypeDeclContinuation` for mutual recursion
- Zero new shift/reduce or reduce/reduce conflicts (157/480 unchanged)
- All 20 ADT tests, 4 alias tests, and lambda annotation test pass unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Add TypeArgList rule and angle bracket rules to AtomicType and AliasAtomicType** - `c1c5b05` (feat)
2. **Task 2: Add angle bracket forms to TypeDeclaration, TypeAliasDeclaration, and TypeDeclContinuation** - `015552a` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Parser.fsy` - Added TypeArgList, AngleBracketTypeParams rules and angle bracket alternatives in all type declaration rules

## Decisions Made
- **Reuse LT/GT tokens** instead of separate LANGLE/RANGLE — the LALR(1) parser disambiguates since type rules and expression rules are in disjoint states. This avoids lexer complications.
- **AngleBracketTypeParams vs TypeParams** — The existing `TypeParams` rule is space-separated (`'a 'b`). Angle bracket syntax requires comma-separation (`'a, 'b`). Added a separate `AngleBracketTypeParams` rule to avoid breaking existing space-separated type param syntax.
- **TypeArgList for type expressions** — A separate comma-separated list rule for type expression arguments (`Map<string, int>`), distinct from `AngleBracketTypeParams` which is only TYPE_VARs.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] AngleBracketTypeParams needed for comma-separated type params in declarations**
- **Found during:** Task 2 (testing `type Map<'k, 'v> = ...`)
- **Issue:** Plan specified reusing `TypeParams` inside angle brackets, but `TypeParams` is space-separated (`'a 'b`). `<'k, 'v>` with a comma requires a different rule.
- **Fix:** Added `AngleBracketTypeParams` rule (comma-separated TYPE_VARs) and used it in all angle bracket declaration rules instead of `TypeParams`.
- **Files modified:** src/LangThree/Parser.fsy
- **Verification:** `type Map<'k, 'v> = Empty | Node of 'k * 'v` parses correctly
- **Committed in:** 015552a (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — plan used wrong rule for comma-separated type params)
**Impact on plan:** Essential fix for multi-param generics. Plan-specified positions ($N references) remained correct after substituting AngleBracketTypeParams for TypeParams.

## Issues Encountered
- Initial smoke test showed `type Map<'k, 'v> = Empty | Node of 'k * 'v` failed to parse. Root cause: `TypeParams` (space-separated) was used inside `< >` but the comma caused a parse failure. Fixed by adding `AngleBracketTypeParams` (comma-separated).

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Angle bracket type syntax fully working in type expressions and declarations
- Phase 63 Plan 02 (type annotation syntax in lambda parameters) can proceed immediately
- Phase 64 (declaration type annotations) can use angle bracket generics in annotated let declarations

---
*Phase: 63-angle-bracket-generics*
*Completed: 2026-03-30*
