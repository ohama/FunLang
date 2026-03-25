---
phase: 33-tco-fix-test-isolation
plan: 01
subsystem: evaluator
tags: [tco, tail-call-optimization, let-rec, trampoline, fsharp, eval]

# Dependency graph
requires:
  - phase: 30-letrec-fix
    provides: BuiltinValue+mutable envRef approach for LetRec/LetRecDecl
provides:
  - TCO-correct BuiltinValue wrappers in LetRec and LetRecDecl
  - 1M-iteration tail-recursive loops complete without stack overflow
affects: [34-test-isolation, 35-final-qa]

# Tech tracking
tech-stack:
  added: []
  patterns: ["tailPos=true in BuiltinValue wrappers enables trampoline to catch TailCall from recursive bodies"]

key-files:
  created: []
  modified: [src/LangThree/Eval.fs]

key-decisions:
  - "BuiltinValue wrappers must use tailPos=true so eval returns TailCall values that the App trampoline loop can catch"

patterns-established:
  - "TCO pattern: eval ... callEnv true funcBody inside BuiltinValue — false breaks tail call propagation through closure boundary"

# Metrics
duration: 3min
completed: 2026-03-25
---

# Phase 33 Plan 01: TCO Fix (LetRec + LetRecDecl tailPos false→true) Summary

**Two-character fix restores TCO for all let rec: change tailPos=false to true in BuiltinValue wrappers so the existing App trampoline catches TailCall from recursive bodies**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-03-25T01:59:05Z
- **Completed:** 2026-03-25T02:02:11Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments

- Fixed TCO regression introduced in Phase 30 (LetRec BuiltinValue conversion)
- LetRec wrapper now uses `eval recEnv moduleEnv callEnv true funcBody` (line 777)
- LetRecDecl wrapper now uses `eval recEnv modEnv callEnv true body` (line 1039)
- 1,000,000-iteration tail-recursive loop completes without stack overflow
- All 214 tests pass

## Task Commits

Each task was committed atomically:

1. **Tasks 1+2: Fix LetRec and LetRecDecl TCO** - `240d6ff` (fix)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `src/LangThree/Eval.fs` - Line 777: `false` → `true` in LetRec BuiltinValue wrapper; Line 1039: `false` → `true` in LetRecDecl BuiltinValue wrapper

## Decisions Made

- Kept both tasks in a single commit since both are the same fix (tailPos=false→true) to the same logical mechanism (BuiltinValue wrapper eval call), applied in two places

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Pre-existing MatchCompile.fs compile signature mismatch**

- **Found during:** Task 2 (test run)
- **Issue:** Working tree had MatchCompile.fs with `compile` refactored to take `freshTestVar` as parameter but `compileMatch` still calling old signature — causing build failure
- **Fix:** Investigation revealed HEAD already contained the fully-fixed version; the apparent mismatch was a stale incremental build artifact. No code change needed.
- **Files modified:** None (HEAD was already correct)
- **Verification:** `dotnet build` succeeded after cache invalidation
- **Committed in:** N/A (no code change required)

---

**Total deviations:** 1 investigated (resolved as non-issue — HEAD already correct)
**Impact on plan:** No scope creep. Build issue was transient incremental cache artifact.

## Issues Encountered

- Initial test run showed MatchCompile.fs build errors — traced to stale incremental build cache, not actual code issue. HEAD was already correct.

## Next Phase Readiness

- TCO regression fully fixed; all 214 tests pass
- Ready for Phase 33-02: Test isolation improvements
- No blockers

---
*Phase: 33-tco-fix-test-isolation*
*Completed: 2026-03-25*
