---
phase: 40-array-higher-order
plan: 01
subsystem: interpreter
tags: [fsharp, array, higher-order-functions, builtins, type-inference]

# Dependency graph
requires:
  - phase: 38-array-type
    provides: ArrayValue DU case, TArray type, array_create/get/set/length/ofList/toList builtins
provides:
  - array_iter, array_map, array_fold, array_init builtins in Eval.fs
  - Scheme entries for all four HOF builtins in TypeCheck.fs
  - iter, map, fold, init wrappers in Prelude/Array.fun
  - callValue helper (via callValueRef forward ref) for invoking user closures from builtins
affects: [41-array-tests, future-stdlib-expansion]

# Tech tracking
tech-stack:
  added: []
  patterns: [callValueRef forward reference pattern for builtins that call user functions]

key-files:
  created: []
  modified:
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs
    - Prelude/Array.fun

key-decisions:
  - "callValue uses a mutable ref (callValueRef) to avoid forward reference error — eval is defined after initialBuiltinEnv in Eval.fs, so callValue cannot directly call eval at definition time"
  - "callValueRef is wired up with the real implementation via a do-binding immediately after evalExpr is defined"
  - "array_fold type scheme uses TVar 0 for accumulator and TVar 1 for element (matches F# Array.fold)"

patterns-established:
  - "callValueRef pattern: when a builtin needs to invoke user closures, use a mutable ref initialized to a placeholder and set after eval is defined"
  - "Phase 40 block inserted between array_to_list and Phase 39 hashtable block in both Eval.fs and TypeCheck.fs"

# Metrics
duration: 2min
completed: 2026-03-25
---

# Phase 40 Plan 01: Array Higher-Order Functions Summary

**Four array HOF builtins (iter/map/fold/init) wired into Eval.fs + TypeCheck.fs + Prelude/Array.fun using callValueRef forward-reference pattern**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-25T06:34:15Z
- **Completed:** 2026-03-25T06:37:10Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Added `callValue` helper via `callValueRef` mutable forward reference, enabling builtins to invoke user closures (FunctionValue and BuiltinValue) without circular dependency issues
- Registered `array_iter`, `array_map`, `array_fold`, `array_init` in `initialBuiltinEnv` with correct curried BuiltinValue implementations
- Added four polymorphic Scheme entries in `initialTypeEnv` with correct TVar assignments for each HOF
- Exposed all four as `Array.iter`, `Array.map`, `Array.fold`, `Array.init` in `Prelude/Array.fun`
- All smoke tests pass: iter prints each element, map doubles elements, fold sums to 15, init creates [0;1;4;9;16]

## Task Commits

Each task was committed atomically:

1. **Task 1: Add array_iter/map/fold/init to Eval.fs** - `ff33311` (feat)
2. **Task 2: Add type schemes to TypeCheck.fs and wrappers to Prelude/Array.fun** - `9b4ce39` (feat)

## Files Created/Modified
- `src/LangThree/Eval.fs` - callValueRef + callValue helper; four Phase 40 HOF builtins; do-binding to wire callValueRef after eval
- `src/LangThree/TypeCheck.fs` - Four Scheme entries for array HOF builtins in Phase 40 block
- `Prelude/Array.fun` - iter, map, fold, init wrappers added to Array module

## Decisions Made
- **callValueRef forward reference:** `eval` is defined after `initialBuiltinEnv` in Eval.fs. A direct call to `eval` in `callValue` at that point causes a compile error ("value not defined"). Solution: use a `ref` placeholder wired after `eval` is defined via a `do` binding after `evalExpr`.
- **array_fold TVar assignment:** TVar 0 = accumulator type, TVar 1 = element type. Matches the F# convention and the plan spec.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] callValue could not directly reference eval at definition site**
- **Found during:** Task 1 (adding callValue helper and builtins to Eval.fs)
- **Issue:** Plan specified placing `callValue` before `initialBuiltinEnv` as a module-level `let`. But `eval` is defined much later (line 730+) as a mutually recursive function group. Compiler error: "The value or constructor 'eval' is not defined."
- **Fix:** Replaced direct `callValue` with a `callValueRef` mutable ref initialized to a placeholder, plus a thin `callValue` wrapper. Added a `do` binding after `evalExpr` to set `callValueRef` to the real implementation.
- **Files modified:** src/LangThree/Eval.fs
- **Verification:** dotnet build exits 0 warnings 0 errors; all four smoke tests pass
- **Committed in:** ff33311 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary to resolve F# forward reference constraint. No scope creep.

## Issues Encountered
- Multi-arg lambda syntax `fun acc x ->` is a parse error in LangThree (single-param only). The plan's smoke test example used this syntax. Used `fun acc -> fun x -> acc + x` in testing instead. The builtin implementation is correct — this is a language limitation, not a bug in the builtins.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All four array HOF builtins available at both builtin and module-qualified name levels
- Phase 41 (array tests / final milestone) can proceed immediately
- Build is clean with 0 warnings, 0 errors

---
*Phase: 40-array-higher-order*
*Completed: 2026-03-25*
