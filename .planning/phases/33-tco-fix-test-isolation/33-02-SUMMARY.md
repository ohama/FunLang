---
phase: 33-tco-fix-test-isolation
plan: 02
subsystem: testing
tags: [pattern-matching, decision-tree, mutable-state, parallel-tests, fsharp]

requires:
  - phase: 31-02
    provides: testSequenced pattern for shared mutable state isolation

provides:
  - compileMatch with fully local counter — no module-level mutable state
  - compile function accepting freshTestVar as parameter (threaded through recursion)
  - Elimination of TestVar ID collisions under parallel test execution

affects:
  - Any future phase touching MatchCompile
  - Phase 35 (test hardening follow-up)

tech-stack:
  added: []
  patterns:
    - "Local counter pattern: move mutable state into calling function scope instead of module scope to guarantee per-invocation isolation"

key-files:
  created: []
  modified:
    - src/LangThree/MatchCompile.fs

key-decisions:
  - "Thread freshTestVar as first parameter to compile rather than capturing from module scope — keeps the recursive algorithm pure with respect to external state"
  - "Local let mutable nextVar inside compileMatch rather than resetting a global counter — reset-based approach is inherently racy under parallelism"

patterns-established:
  - "Counter locality: any module that needs a fresh-ID generator should own it locally in the entry-point function, threading it into recursive helpers"

duration: 5min
completed: 2026-03-25
---

# Phase 33 Plan 02: MatchCompile Global Counter Elimination Summary

**Replaced module-level mutable `nextTestVar`/`resetTestVarCounter` with a local counter inside `compileMatch`, threading `freshTestVar` through `compile` as a parameter to fix parallel-test TestVar ID collisions.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-03-25T00:00:00Z
- **Completed:** 2026-03-25T00:05:00Z
- **Tasks:** 2 (executed as one atomic commit — both edits in same file)
- **Files modified:** 1

## Accomplishments

- Removed `nextTestVar`, `freshTestVar`, `resetTestVarCounter` module-level declarations entirely
- Updated `compile` signature: `let rec compile (freshTestVar: unit -> TestVar) (clauses: MatchRow list) : DecisionTree`
- Updated all recursive calls inside `compile` to pass `freshTestVar` through
- Rewrote `compileMatch` with local `let mutable nextVar = 0` and local `freshTestVar` closure, passed to `compile`
- All 214 tests pass on two consecutive runs with no flakiness

## Task Commits

1. **Tasks 1+2: Remove global counter, update compile signature, update compileMatch** - `eb1117a` (fix)

**Plan metadata:** (docs commit below)

## Files Created/Modified

- `/Users/ohama/vibe-coding/LangThree/src/LangThree/MatchCompile.fs` — Removed 3 module-level declarations, updated `compile` to accept `freshTestVar` parameter, rewrote `compileMatch` with local counter

## Decisions Made

- Threaded `freshTestVar` as the first parameter to `compile` rather than using a module-level reference. This keeps the Jacobs pattern-match compilation algorithm self-contained and safe under concurrent invocations.
- Used `let mutable nextVar` inside `compileMatch` rather than a reset-on-entry approach. The reset approach (`resetTestVarCounter()`) was racy: concurrent calls could reset the counter mid-compilation in another goroutine, corrupting TestVar IDs in that call's decision tree.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

The linter (likely Fantomas or an IDE formatter hook) reverted one of the intermediate edits during multi-step editing. Resolved by reading the current file state after each linter intervention and re-applying only the remaining delta. Final result matches the plan specification exactly.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- MatchCompile is now safe for parallel test execution
- All 214 tests pass deterministically on consecutive runs
- Ready for Phase 33-03 (TCO fix) or Phase 34 (whatever follows)
- No blockers

---
*Phase: 33-tco-fix-test-isolation*
*Completed: 2026-03-25*
