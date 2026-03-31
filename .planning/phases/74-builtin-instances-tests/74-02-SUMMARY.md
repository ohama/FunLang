---
phase: 74-builtin-instances-tests
plan: 02
subsystem: testing
tags: [typeclass, Show, Eq, flt, integration-tests, prelude, instances]

# Dependency graph
requires:
  - phase: 74-builtin-instances-tests/74-01
    provides: Prelude/Typeclass.fun Show/Eq instances for int/bool/string/char; Prelude.fs calls elaborateTypeclasses before eval
provides:
  - 5 new flt integration tests covering built-in Show and Eq instances end-to-end
  - regression coverage for: show int/bool/char/string, eq int, eq function type error, constrained function with built-in Show
affects: [any future phase modifying typeclass elaboration or prelude instances]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "flt tests with no typeclass/instance declarations: show/eq available directly from prelude"
    - "Error flt test uses --check flag and partial stderr matching (only first error line)"

key-files:
  created:
    - tests/flt/file/typeclass/typeclass-builtin-show.flt
    - tests/flt/file/typeclass/typeclass-builtin-show-containers.flt
    - tests/flt/file/typeclass/typeclass-builtin-eq.flt
    - tests/flt/file/typeclass/typeclass-builtin-eq-error.flt
    - tests/flt/file/typeclass/typeclass-constrained-fn-builtin.flt
  modified: []

key-decisions:
  - "typeclass-builtin-show-containers.flt tests bool/char/string Show (not list/option — polymorphic instances not supported in v10.0)"
  - "typeclass-builtin-eq.flt picks eq 1 1 -> true as primary test case; eq 1 2 is covered in same task but separate file would add noise"

patterns-established:
  - "Runtime built-in test pattern: no typeclass/instance headers; just use show/eq directly"

# Metrics
duration: 2min
completed: 2026-03-31
---

# Phase 74 Plan 02: Built-in Instances Tests Summary

**5 flt integration tests locking in built-in Show/Eq prelude instances: show int/bool/char/string, eq int, Eq function type error, and constrained function using prelude Show — 676/676 tests passing**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-31T12:32:24Z
- **Completed:** 2026-03-31T12:34:38Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Created 5 new flt tests covering all Phase 74 success criteria
- `show 42`, `show true`, `show 'x'`, `show "hello"` all work without any user instance declaration
- `eq 1 1` → true without user instance declaration
- `eq (fun x -> x) (fun x -> x)` → E0701 type error (Eq not available for function types)
- `show_twice 42` → `"4242"` using user-defined constrained function with built-in Show
- No regressions: 676/676 total tests pass (was 671 before Phase 74, +5 new in this plan)

## Task Commits

1. **Task 1: flt test for built-in Show int** - `b086fa0` (test)
2. **Task 2: 4 remaining flt tests** - `e701acc` (test)

## Files Created/Modified
- `tests/flt/file/typeclass/typeclass-builtin-show.flt` - show 42 works from prelude without user instance
- `tests/flt/file/typeclass/typeclass-builtin-show-containers.flt` - show bool/char/string (note: list/option not available — polymorphic instances deferred)
- `tests/flt/file/typeclass/typeclass-builtin-eq.flt` - eq 1 1 returns true from prelude
- `tests/flt/file/typeclass/typeclass-builtin-eq-error.flt` - eq on function type produces E0701
- `tests/flt/file/typeclass/typeclass-constrained-fn-builtin.flt` - user function using built-in Show

## Decisions Made
- `typeclass-builtin-show-containers.flt` tests bool/char/string primitives (not list/option) because polymorphic instances (`instance Show (list 'a)`) are not supported in v10.0 — exact-equality instance resolution cannot match `TList(TVar 0)` against `TList TInt`. The filename is kept as planned for traceability.
- Used exact binary output captured from live runs to ensure flt expected values are precise.

## Deviations from Plan

None - plan executed exactly as written. The plan already noted the list/option limitation and provided an alternative (bool/char/string) if needed.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 74 (Built-in Instances and Tests) is now complete: 2 plans done, all success criteria met
- Milestone v10.0 Type Classes is complete: phases 70+71+72+73+74 all done
- Regression coverage for built-in Show/Eq is locked in via 5 new flt tests
- Known limitation documented: polymorphic instances (Show list, Show option) require unification-based instance resolution — deferred to a future milestone

---
*Phase: 74-builtin-instances-tests*
*Completed: 2026-03-31*
