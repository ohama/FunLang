---
phase: 73-dictionary-construction-elaboration
plan: 01
subsystem: typeclass-elaboration
tags: [fsharp, typeclass, elaboration, ast-transform, eval-pipeline]

# Dependency graph
requires:
  - phase: 72-type-checker-constraint-inference
    provides: TypeClassDecl/InstanceDecl type-checked; Eval.fs has no-ops for both
provides:
  - elaborateTypeclasses function in Elaborate.fs (InstanceDecl -> LetDecl, TypeClassDecl -> removed)
  - Program.fs wired to apply elaboration pass after type check, before eval
  - Runtime execution of typeclass method calls (show 42 evaluates to "42")
affects: [73-02, phase 74 runtime, future typeclass plans]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Post-type-check AST elaboration pass: InstanceDecl -> LetDecl flatmap, TypeClassDecl -> []"
    - "Program.fs pipeline: parse -> type-check -> elaborateTypeclasses -> eval"
    - "Evaluator stays type-class-unaware; elaboration is the bridge"

key-files:
  created: []
  modified:
    - src/LangThree/Elaborate.fs
    - src/LangThree/Program.fs

key-decisions:
  - "Method names bound directly (not mangled): works for single-instance scenarios; last-wins shadowing acceptable for Phase 73 MVP"
  - "elaborateTypeclasses recurses into ModuleDecl and NamespaceDecl to handle nested decls"
  - "Last-binding lookup in Program.fs uses elaboratedDecls (not moduleDecls) so TypeClassDecl removal doesn't cause missed output"
  - "Prelude.fs evalModuleDecls call sites NOT touched: prelude files don't declare user typeclasses"

patterns-established:
  - "elaborateTypeclasses: List.collect flatmap over Decl list, recursive on module/namespace bodies"
  - "Program.fs: let elaboratedDecls = Elaborate.elaborateTypeclasses moduleDecls inserted before every Eval.evalModuleDecls call"

# Metrics
duration: 8min
completed: 2026-03-31
---

# Phase 73 Plan 01: Dictionary Construction Elaboration Summary

**InstanceDecl -> LetDecl AST elaboration pass added to Elaborate.fs and wired into Program.fs, enabling typeclass method calls at runtime without any changes to Eval.fs**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-03-31
- **Completed:** 2026-03-31
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- `elaborateTypeclasses` function added to Elaborate.fs: removes TypeClassDecl, expands InstanceDecl to one LetDecl per method, recurses into module/namespace bodies
- Program.fs wired at both eval call sites (Test subcommand and file execution) to apply elaboration after type check
- `show 42` evaluates to `"42"` at runtime; all 224 unit tests and 668 flt tests pass; Eval.fs unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Add elaborateTypeclasses to Elaborate.fs** - `c555c3b` (feat)
2. **Task 2: Wire elaboration pass into Program.fs pipeline** - `81b79d6` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Elaborate.fs` - Added `elaborateTypeclasses` rec function at end of module
- `src/LangThree/Program.fs` - Applied elaboration at both Eval.evalModuleDecls call sites; last-binding lookup updated to use elaboratedDecls

## Decisions Made
- Method names bound directly without mangling (e.g., `show` not `Show$int$show`). Works for single-instance scenarios where Phase 73 success criteria are defined. Multiple instances for same class would shadow each other (last-wins) -- acceptable MVP.
- Prelude.fs call sites intentionally not updated: prelude files contain no InstanceDecl, so elaboration would be a no-op there anyway.
- Last-binding lookup in file execution mode uses `elaboratedDecls` instead of `moduleDecls`. This is necessary correctness fix: if the last decl is TypeClassDecl (removed by elaboration), the original lookup would find `None` and print nothing; with `elaboratedDecls` we find the actual last let-binding.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Last-binding lookup updated to use elaboratedDecls**
- **Found during:** Task 2 (Program.fs wiring)
- **Issue:** After elaboration, TypeClassDecl and InstanceDecl are removed from the decl list. If the original file ends with a TypeClassDecl, the last-binding lookup over `moduleDecls` could find the wrong last decl.
- **Fix:** Changed `moduleDecls |> List.rev |> List.tryPick` to `elaboratedDecls |> List.rev |> List.tryPick` in the file execution path.
- **Files modified:** src/LangThree/Program.fs
- **Verification:** Smoke test `show 42` prints `"42"` correctly.
- **Committed in:** `81b79d6` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (missing critical)
**Impact on plan:** Necessary correctness fix. No scope creep.

## Issues Encountered
None - implementation was straightforward.

## Next Phase Readiness
- Phase 73 Plan 02: Integration tests for runtime typeclass method dispatch can now be written against real eval output
- The evaluator sees ordinary LetDecl bindings; method calls resolve via standard variable lookup
- Multiple instances of the same class shadow each other (last-wins); Phase 74 can add name mangling if needed

---
*Phase: 73-dictionary-construction-elaboration*
*Completed: 2026-03-31*
