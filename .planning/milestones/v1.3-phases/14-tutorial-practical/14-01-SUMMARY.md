---
phase: 14-tutorial-practical
plan: "01"
subsystem: docs
tags: [tutorial, modules, exceptions, pipes, composition, strings, printf]

# Dependency graph
requires:
  - phase: 13-tutorial-core
    provides: Tutorial chapters 1-7, established writing style
provides:
  - Tutorial chapters 8-11 (modules, exceptions, pipes, strings)
  - 53 CLI-verified code examples
affects: [14-02 (chapters 12-13)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Tutorial style: section-per-feature, code blocks with $ langthree prefix, file mode examples with cat+run"

key-files:
  created:
    - tutorial/08-modules.md
    - tutorial/09-exceptions.md
    - tutorial/10-pipes-and-composition.md
    - tutorial/11-strings-and-output.md
  modified: []

key-decisions:
  - "Module-scoped exceptions and records not demonstrated (parse/type errors in current implementation); used open directive pattern instead"
  - "All try-with examples include catch-all | _ -> to suppress W0003 warning by default, with explicit section showing the warning"

patterns-established:
  - "Chapter naming: NN-topic.md with # Chapter N: Title heading"
  - "Examples: --expr for simple, file mode (cat + run) for multi-line"

# Metrics
duration: 5min
completed: 2026-03-19
---

# Phase 14 Plan 01: Tutorial Chapters 8-11 Summary

**Four tutorial chapters covering modules, exceptions, pipes/composition, and strings/output with 53 CLI-verified examples**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-19T00:31:44Z
- **Completed:** 2026-03-19T00:36:51Z
- **Tasks:** 5
- **Files created:** 4

## Accomplishments
- Chapter 8 (Modules): 10 examples covering module declaration, qualified access, open, nesting, namespaces
- Chapter 9 (Exceptions): 9 examples covering declaration, data, handlers, guards, nesting, W0003 warning
- Chapter 10 (Pipes/Composition): 12 examples covering |>, >>, <<, chaining, pipe vs composition guidance
- Chapter 11 (Strings/Output): 22 examples covering all built-in string functions, print/println/printf, sequencing

## Task Commits

Each task was committed atomically:

1. **Task 1: Chapter 8 - Modules and Namespaces** - `1411241` (feat)
2. **Task 2: Chapter 9 - Exceptions** - `2758d4b` (feat)
3. **Task 3: Chapter 10 - Pipes and Composition** - `3da6dde` (feat)
4. **Task 4: Chapter 11 - Strings and Output** - `165b6d1` (feat)
5. **Task 5: Verify all examples** - verified inline during tasks 1-4

## Files Created/Modified
- `tutorial/08-modules.md` - Modules, qualified access, open, nested modules, namespaces
- `tutorial/09-exceptions.md` - Exception declaration, try-with, when guards, W0003 warning
- `tutorial/10-pipes-and-composition.md` - Pipe |>, forward/backward composition, chaining
- `tutorial/11-strings-and-output.md` - String builtins, print/println/printf, sequencing

## Decisions Made
- Module-scoped exceptions and records skipped from examples (current implementation has parse/type errors for qualified access patterns); used `open` directive instead
- Try-with examples default to including catch-all `| _ ->` handler; dedicated W0003 section shows the warning explicitly
- Printf examples use file mode exclusively (fslit %s conflict with printf %s in --expr mode)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Module-scoped exception declaration (`module M = exception Err`) causes parse error -- worked around by not demonstrating this pattern
- Module-scoped record types have field resolution issues with qualified access -- used `open` pattern instead
- Qualified constructor access with arguments (`M.Some 42`) fails for constructors named `Some` due to Prelude conflict -- used unique constructor names (`MSome`)

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Chapters 8-11 complete, ready for plan 14-02 (chapters 12-13: mutable state, practical patterns)
- All 53 examples verified against CLI
- Consistent style with chapters 1-7

---
*Phase: 14-tutorial-practical*
*Completed: 2026-03-19*
