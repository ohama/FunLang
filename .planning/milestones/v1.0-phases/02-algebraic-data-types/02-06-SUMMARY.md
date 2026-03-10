---
phase: 02-algebraic-data-types
plan: 06
subsystem: type-checking
tags: [exhaustiveness, redundancy, pattern-matching, warnings, diagnostics]

requires:
  - phase: 02-algebraic-data-types (plan 04)
    provides: Exhaustive module with Maranget usefulness algorithm
  - phase: 02-algebraic-data-types (plan 03)
    provides: Constructor pattern type checking in Bidir.fs

provides:
  - Exhaustiveness warnings for incomplete ADT pattern matches
  - Redundancy warnings for unreachable ADT pattern clauses
  - AST-to-CasePat conversion bridging Ast.Pattern to Exhaustive analysis
  - Warning diagnostic infrastructure (W-prefixed codes)

affects: [phase-03-error-messages, phase-06-tooling]

tech-stack:
  added: []
  patterns:
    - "Warning diagnostics with W-prefix codes separate from E-prefix errors"
    - "Pattern-based ADT type inference (infer scrutinee type from constructor patterns)"
    - "Recursive AST traversal for collecting nested match expressions"

key-files:
  created: []
  modified:
    - src/LangThree/Exhaustive.fs
    - src/LangThree/Diagnostic.fs
    - src/LangThree/TypeCheck.fs
    - tests/LangThree.Tests/IntegrationTests.fs

key-decisions:
  - "Infer scrutinee type from constructor patterns rather than re-synthesizing (avoids scope issues with function parameters)"
  - "W-prefix for warning codes to distinguish from E-prefix error codes in formatDiagnostic"

patterns-established:
  - "Warning pipeline: collect match expressions -> infer ADT type from patterns -> check exhaustiveness/redundancy -> emit Diagnostic list"

duration: 5min
completed: 2026-03-09
---

# Phase 2 Plan 6: Exhaustiveness Wiring Summary

**Wired Exhaustive.fs into TypeCheck pipeline with AST-to-CasePat conversion, W-prefixed warning diagnostics, and pattern-based ADT type inference**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-09T04:38:54Z
- **Completed:** 2026-03-09T04:43:54Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Exhaustive module is no longer orphaned -- called from typeCheckModule during type checking
- Incomplete ADT pattern matches produce W0001 warnings listing missing constructors
- Redundant/unreachable pattern clauses produce W0002 warnings identifying the clause
- Complete ADT pattern matches produce no warnings (verified by tests)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add AST-to-CasePat conversion, constructor set lookup, and warning types** - `f9a60e1` (feat)
2. **Task 2: Wire exhaustiveness and redundancy checking into TypeCheck.fs with integration tests** - `02d7b2c` (feat)

## Files Created/Modified
- `src/LangThree/Exhaustive.fs` - Added getConstructorsFromEnv (ConstructorEnv lookup) and astPatToCasePat (Ast.Pattern to CasePat conversion), removed failwith stub
- `src/LangThree/Diagnostic.fs` - Added NonExhaustiveMatch/RedundantPattern TypeErrorKind variants, W-prefix warning formatting
- `src/LangThree/TypeCheck.fs` - Added collectMatches for recursive match collection, wired checkExhaustive/checkRedundant into typeCheckModule, changed return type to Result<Diagnostic list, Diagnostic>
- `tests/LangThree.Tests/IntegrationTests.fs` - Added 4 integration tests for exhaustiveness/redundancy warnings

## Decisions Made
- **Infer scrutinee type from constructor patterns:** Instead of re-synthesizing the scrutinee expression (which fails for function parameters not in the module-level type environment), we look at the constructor patterns in the match clauses and resolve the ADT type from the ConstructorEnv. This is reliable since any ADT match must have at least one constructor pattern.
- **Warning vs error code prefix:** Used "W" prefix for warning codes (W0001, W0002) to cleanly distinguish from "E" prefix error codes in formatDiagnostic output.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Changed scrutinee type inference approach**
- **Found during:** Task 2 (wiring exhaustiveness into TypeCheck.fs)
- **Issue:** Plan specified re-synthesizing scrutinee via `Bidir.synth ctorEnv [] env scrutinee`, but function parameters (e.g., `x` in `let f x = match x with ...`) are not in the module-level finalEnv, causing UnboundVar errors
- **Fix:** Instead of re-synthesizing, infer the ADT type from the first constructor pattern found in the match clauses via ConstructorEnv lookup
- **Files modified:** src/LangThree/TypeCheck.fs
- **Verification:** All 89 tests pass including the 4 new integration tests
- **Committed in:** 02d7b2c (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 missing critical)
**Impact on plan:** Essential correction for correctness. The plan's approach would have failed for the primary use case (matching on function parameters). No scope creep.

## Issues Encountered
None beyond the deviation documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 2 exhaustiveness wiring complete -- truths #3 and #4 from verification now satisfied
- All ADT features (parsing, elaboration, type checking, evaluation, exhaustiveness) are integrated
- Ready for Phase 3 (error messages) or Phase 4 (GADTs)

---
*Phase: 02-algebraic-data-types*
*Completed: 2026-03-09*
