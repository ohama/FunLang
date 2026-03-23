---
phase: 25-polymorphic-gadt-return-types
plan: 03
subsystem: docs
tags: [gadt, tutorial, mdbook, type-inference, polymorphic-return]

# Dependency graph
requires:
  - phase: 25-polymorphic-gadt-return-types
    provides: 25-01 synth delegation enabling annotation-free GADT match
provides:
  - Updated Ch14 GADT tutorial with polymorphic return type section
  - Accurate annotation guidance reflecting annotation is now optional
  - poly-eval.l3 example demonstrating eval : 'a Expr -> 'a pattern
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Tutorial documents three annotation modes: none (polymorphic), 'a (explicit poly), concrete (constrained)"

key-files:
  created: []
  modified:
    - tutorial/14-gadt.md
    - docs/14-gadt.html

key-decisions:
  - "Renamed section from '왜 타입 주석이 필요한가' to '타입 주석의 역할' to reflect optional-annotation behavior"
  - "Removed E0401 error example entirely — annotation is no longer required"
  - "Added 다형적 반환 타입 section after 재귀 GADT, before GADT 완전성 검사"
  - "Updated Haskell/OCaml comparison tables and language summary table to show LangThree now supports polymorphic return"

patterns-established:
  - "Tutorial sections: annotation-free (polymorphic) → type-var annotation → concrete annotation"

# Metrics
duration: 8min
completed: 2026-03-23
---

# Phase 25 Plan 03: Polymorphic GADT Return — Tutorial Update Summary

**Ch14 GADT tutorial updated to document annotation-free polymorphic return with poly-eval.l3 example and three-mode annotation guidance**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-03-23T00:31:08Z
- **Completed:** 2026-03-23T00:39:00Z
- **Tasks:** 1
- **Files modified:** 2 (tutorial/14-gadt.md, docs/14-gadt.html + mdBook rebuilds)

## Accomplishments
- Added new "다형적 반환 타입" section with poly-eval.l3 showing `eval : 'a Expr -> 'a`
- Replaced mandatory-annotation section with three-mode annotation guidance (none/`'a`/concrete)
- Removed E0401 mandatory-annotation error example (now obsolete)
- Updated Haskell comparison table: "필수" → "선택사항 (생략 시 다형적 반환)"
- Updated OCaml comparison paragraph to credit LangThree going further than OCaml
- Updated language summary table: LangThree row now shows polymorphic return as supported
- Updated GADT syntax summary table: annotation marked optional
- Rebuilt mdBook successfully

## Task Commits

Each task was committed atomically:

1. **Task 1: Add polymorphic return type section and update annotation guidance in Ch14** - `34d3237` (docs)

**Plan metadata:** (included in task commit per single-task plan)

## Files Created/Modified
- `tutorial/14-gadt.md` - All content changes: new section, updated annotation guidance, updated comparison tables
- `docs/14-gadt.html` - Rebuilt HTML from mdBook

## Decisions Made
- Updated both in-chapter comparison table (Haskell vs LangThree) and the multi-language summary table for consistency
- Updated OCaml comparison paragraph to reflect LangThree's improvement over OCaml's require-explicit-annotation approach
- Updated GADT syntax summary table at chapter end so it stays accurate reference material

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Updated additional comparison tables for consistency**
- **Found during:** Task 1
- **Issue:** Plan specified three changes, but the chapter contains two additional comparison tables (Haskell/LangThree inline table, and the multi-language summary table at the bottom) plus the GADT syntax summary table — all still described annotation as required or LangThree as limited to fixed return types
- **Fix:** Updated all tables and the OCaml comparison paragraph to be accurate with the new behavior
- **Files modified:** tutorial/14-gadt.md
- **Verification:** grep confirms no remaining inaccurate "필수" annotation statements; language table shows polymorphic return as supported
- **Committed in:** 34d3237 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 missing critical — table consistency)
**Impact on plan:** Necessary for tutorial coherence. Without updating comparison tables, readers would receive contradictory information within the same chapter.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 25 tutorial documentation complete
- All three plans (25-01 compiler, 25-02 tests, 25-03 tutorial) delivered
- v1.8 Polymorphic GADT milestone ready to close

---
*Phase: 25-polymorphic-gadt-return-types*
*Completed: 2026-03-23*
