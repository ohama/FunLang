---
phase: 24-list-separator-semicolon
plan: "03"
subsystem: docs
tags: [tutorial, markdown, list, semicolon, syntax, mdbook]

# Dependency graph
requires:
  - phase: 24-list-separator-semicolon
    provides: "[1; 2; 3] semicolon list syntax accepted by parser (Plan 01)"
provides:
  - "All 16 tutorial .md files updated to [1; 2; 3] list syntax"
  - "Tuple syntax (1, 2, 3) left untouched throughout"
  - "List-of-tuple examples correctly use [(1, 'a'); (2, 'b')] format"
  - "to_string output examples updated to reflect new list format"
  - "mdBook HTML docs rebuilt with correct examples"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bracket-stack-aware Python script for context-sensitive comma-to-semicolon replacement"

key-files:
  created: []
  modified:
    - tutorial/01-getting-started.md
    - tutorial/02-functions.md
    - tutorial/03-lists-and-tuples.md
    - tutorial/04-pattern-matching.md
    - tutorial/05-algebraic-types.md
    - tutorial/07-strings-and-output.md
    - tutorial/08-pipes-and-composition.md
    - tutorial/09-prelude.md
    - tutorial/12-error-handling.md
    - tutorial/13-user-defined-operators.md
    - tutorial/15-algorithms.md
    - tutorial/16-cli-reference.md
    - docs/ (rebuilt HTML)

key-decisions:
  - "Bracket-stack algorithm required: initial script using (paren_depth == 0) condition missed lists nested inside parens (e.g., [1, 2, 3] inside find f (... [...]))"
  - "formatList user-defined function examples intentionally build comma-formatted strings — these were left unchanged"
  - "to_string output strings updated: '[1, 2, 3]' → '[1; 2; 3]' since formatValue now produces semicolons"

patterns-established:
  - "Use bracket stack (not simple depth counters) for context-sensitive comma replacement in nested structures"

# Metrics
duration: 2min
completed: 2026-03-20
---

# Phase 24 Plan 03: Tutorial Update — Semicolon List Syntax Summary

**All 16 tutorial markdown files updated from `[1, 2, 3]` to `[1; 2; 3]` list syntax using a bracket-stack-aware transformation script, with mdBook rebuilt.**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-20T09:07:29Z
- **Completed:** 2026-03-20T09:10:01Z
- **Tasks:** 2 (merged into one commit per plan spec)
- **Files modified:** 40 (16 tutorial .md + 24 docs HTML files)

## Accomplishments

- All code examples in tutorial files use `[1; 2; 3]` semicolon list syntax
- Tuple commas `(1, 2, 3)` left completely unchanged throughout all files
- List-of-tuple examples correctly show `[(1, "a"); (2, "b")]` format
- Prose description in `03-lists-and-tuples.md` updated (`[2; 3]` not `[2, 3]` in description)
- `to_string` output examples corrected: `"[1; 2; 3]"` and `"Some [1; 2; 3]"`
- mdBook HTML docs rebuilt successfully

## Task Commits

1. **Tasks 1+2: Update tutorial markdown files and rebuild mdBook** - `c29fc0d` (docs)

**Plan metadata:** (docs commit below)

## Files Created/Modified

- `tutorial/01-getting-started.md` - List examples updated
- `tutorial/02-functions.md` - List examples updated
- `tutorial/03-lists-and-tuples.md` - All list examples + prose updated
- `tutorial/04-pattern-matching.md` - List examples updated
- `tutorial/05-algebraic-types.md` - List examples updated
- `tutorial/07-strings-and-output.md` - List examples + to_string output updated
- `tutorial/08-pipes-and-composition.md` - List examples updated
- `tutorial/09-prelude.md` - List examples + to_string output updated
- `tutorial/12-error-handling.md` - List examples updated (were nested inside parens, caught by v2 script)
- `tutorial/13-user-defined-operators.md` - List examples updated
- `tutorial/15-algorithms.md` - List examples updated (was nested inside parens, caught by v2 script)
- `tutorial/16-cli-reference.md` - List examples updated
- `docs/` - mdBook HTML rebuild (all HTML files + new searchindex)

## Decisions Made

- **Bracket-stack algorithm over depth counters:** The initial plan's script used `paren_depth == 0` as a guard condition. This missed cases like `(find f [1, 2, 3])` where the list is nested inside a function call parenthesis. A bracket-stack approach (tracking innermost enclosing bracket type) correctly handles all nesting. This was the key deviation from the plan's provided script.
- **`formatList` examples left unchanged:** Two tutorial files (`08-pipes-and-composition.md`, `13-user-defined-operators.md`) define a user `formatList` function that intentionally builds strings like `"[1, 2, 3]"` with commas as the string separator. This is user code formatting strings, not list syntax — left as-is.
- **`to_string` output updated:** The plan didn't explicitly call out updating `to_string` output string values. Since `formatValue` now outputs semicolons, `to_string [1; 2; 3]` returns `"[1; 2; 3]"`. These were manually updated.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Upgraded to bracket-stack algorithm**

- **Found during:** Task 1 (running initial script from plan)
- **Issue:** Plan's provided script used `paren_depth == 0` condition, missing list literals nested inside function-call parentheses (e.g., `find (fun x -> x > 3) [1, 2, 3, 4, 5]`)
- **Fix:** Rewrote script to use a bracket stack; innermost `[` means list context, innermost `(` means tuple/paren context
- **Files modified:** `/tmp/fix_tutorial_syntax_v2.py` (new script)
- **Verification:** Test cases confirm `(find f [1, 2, 3])` → `(find f [1; 2; 3])` while `(1, 2, 3)` stays unchanged
- **Committed in:** c29fc0d (task commit)

---

**Total deviations:** 1 auto-fixed (script logic improvement for correctness)
**Impact on plan:** Necessary for correctness — plan's script would have left 3 list literals unchanged in `12-error-handling.md` and `15-algorithms.md`.

## Issues Encountered

None beyond the script logic issue described above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Tutorial documentation now matches the actual language syntax
- v1.7 milestone (list separator semicolon) fully complete: parser, formatter, evaluator, tests, and tutorial all updated
- No blockers for future phases

---
*Phase: 24-list-separator-semicolon*
*Completed: 2026-03-20*
