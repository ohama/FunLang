---
phase: 14-tutorial-practical
plan: "02"
subsystem: docs
tags: [tutorial, prelude, option, cli, reference]

# Dependency graph
requires:
  - phase: 14-tutorial-practical
    plan: "01"
    provides: Tutorial chapters 8-11, established writing style
provides:
  - Tutorial chapters 12-13 (prelude, CLI reference)
  - 65 CLI-verified code examples
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Reference-style chapter with tables for quick lookup"

key-files:
  created:
    - tutorial/12-prelude.md
    - tutorial/13-cli-reference.md
  modified: []

key-decisions:
  - "Option pattern matching documented as file-mode only (expr mode type checker cannot resolve Prelude constructors in patterns)"
  - "emit-type for Prelude types demonstrated in file mode (expr mode returns unresolved type variable)"

patterns-established:
  - "Summary tables for categorization (Prelude vs built-in vs runtime)"

# Metrics
duration: 4min
completed: 2026-03-19
---

# Phase 14 Plan 02: Tutorial Chapters 12-13 Summary

**Two tutorial chapters covering Prelude/standard library and CLI reference with 65 CLI-verified examples**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-19T00:40:31Z
- **Completed:** 2026-03-19T00:44:36Z
- **Tasks:** 3
- **Files created:** 2

## Accomplishments
- Chapter 12 (Prelude): 25 examples covering Option type, pattern matching, optionMap, optionBind, pipe integration, Prelude extension, type-only vs runtime built-ins
- Chapter 13 (CLI Reference): 40 examples covering expression mode, file mode, REPL, emit-ast, emit-type, emit-tokens, error messages, warning codes W0001/W0002/W0003

## Task Commits

Each task was committed atomically:

1. **Task 1: Chapter 12 - Prelude and Standard Library** - `e007d3d` (feat)
2. **Task 2: Chapter 13 - CLI Reference** - `df2da4c` (feat)
3. **Task 3: Verify all examples** - verified inline during tasks 1-2

## Files Created/Modified
- `tutorial/12-prelude.md` - Option type, Prelude mechanism, built-in type signatures, runtime built-ins
- `tutorial/13-cli-reference.md` - All CLI modes, diagnostic flags, error messages, warning codes

## Decisions Made
- Option pattern matching examples use file mode only (expr mode cannot resolve Prelude constructors in pattern context)
- emit-type for Prelude types demonstrated in file mode (expr mode returns `'a` instead of `Option<int>`)
- REPL documented with caveat: module-level `let` (without `in`) not supported

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- `--emit-type --expr 'Some 42'` returns `'a` instead of `Option<int>` -- documented with file-mode alternative
- Pattern matching on Prelude constructors in `--expr` mode gives "Unbound constructor" error -- documented as file-mode feature

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All 13 tutorial chapters complete
- Total verified examples across all chapters: 224 (159 from chapters 1-11 + 65 from chapters 12-13)
- v1.3 Tutorial Documentation milestone complete

---
*Phase: 14-tutorial-practical*
*Completed: 2026-03-19*
