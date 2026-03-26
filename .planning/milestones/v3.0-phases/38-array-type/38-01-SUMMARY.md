---
phase: 38-array-type
plan: 01
subsystem: infra
tags: [fsharp, DU, CustomEquality, TArray, ArrayValue, type-system]

# Dependency graph
requires:
  - phase: 32-file-io-system-builtins
    provides: BuiltinValue pattern for native F# function carrier
  - phase: 37-records-mutable
    provides: RecordValue mutable field pattern (ref cells), SetField mutation semantics
provides:
  - ArrayValue of Value array DU case in Ast.fs with CustomEquality obligations fully satisfied
  - TArray of Type DU case in Type.fs with all propagation arms (apply, freeVars, formatType, formatTypeNormalized, collectVars)
  - TArray unification arm in Unify.fs
  - TArray in NotAFunction guard in Bidir.fs
  - formatValue arm for ArrayValue in Eval.fs ([|e1; e2; ...|] notation)
  - valuesEqual arm for ArrayValue in Eval.fs (reference inequality semantics)
affects:
  - 38-02 (Array builtins — needs ArrayValue and TArray to exist)
  - 39-hashtable-type (same pattern for HashtableValue)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ArrayValue uses Value array (no outer ref) — in-place mutation via arr.[i] <- v is sufficient for fixed-size arrays"
    - "Array equality uses ReferenceEquals — two array objects are never equal by value, consistent with F# array semantics"
    - "TArray follows TList exactly — every TList arm gets a parallel TArray arm in all match expressions"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Type.fs
    - src/LangThree/Unify.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Eval.fs

key-decisions:
  - "ArrayValue of Value array (no outer ref) — fixed-size arrays mutate elements in place, no need to replace the whole array"
  - "valueEqual uses ReferenceEquals — two distinct arrays with identical contents are not equal (matches F# semantics for mutable containers)"
  - "valuesEqual in Eval.fs returns false for ArrayValue _ , ArrayValue _ — consistent with reference equality above"

patterns-established:
  - "Pattern: Adding new Value case requires 4 sites: GetHashCode, valueEqual, valueCompare in Ast.fs + valuesEqual + formatValue in Eval.fs"
  - "Pattern: Adding new Type case requires 5 propagation sites: formatType, formatTypeNormalized (format + collectVars), apply, freeVars in Type.fs + Unify arm + Bidir guard"

# Metrics
duration: 8min
completed: 2026-03-25
---

# Phase 38 Plan 01: Array Type Infrastructure Summary

**ArrayValue (Value array) and TArray (Type) DU cases added with all CustomEquality, unification, and propagation arms — zero warnings, 224/224 tests green**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-03-25T05:51:05Z
- **Completed:** 2026-03-25T05:59:00Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Added `ArrayValue of Value array` to the Value DU in Ast.fs with all four CustomEquality/Comparison obligations (GetHashCode, valueEqual, valueCompare, valuesEqual in Eval.fs)
- Added `TArray of Type` to the Type DU in Type.fs with all five propagation arms (formatType, formatTypeNormalized inner format + collectVars, apply, freeVars)
- Wired TArray unification in Unify.fs and TArray in the NotAFunction guard in Bidir.fs
- Added formatValue arm for ArrayValue in Eval.fs printing `[|e1; e2; ...|]`

## Task Commits

Each task was committed atomically:

1. **Task 1: Add ArrayValue DU case to Ast.fs with all CustomEquality obligations** - `83ff07b` (feat)
2. **Task 2: Add TArray to Type.fs and all propagation sites** - `6892dec` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Ast.fs` - ArrayValue DU case + GetHashCode/valueEqual/valueCompare arms
- `src/LangThree/Eval.fs` - valuesEqual arm (false) + formatValue arm ([|...|])
- `src/LangThree/Type.fs` - TArray DU case + formatType/formatTypeNormalized/apply/freeVars arms
- `src/LangThree/Unify.fs` - TArray t1, TArray t2 unification arm
- `src/LangThree/Bidir.fs` - TArray _ added to NotAFunction guard

## Decisions Made
- Used `Value array` (no outer `ref`) for ArrayValue — in-place mutation via `arr.[i] <- v` is sufficient for fixed-size arrays; wrapping in a ref cell adds indirection with no benefit
- Used `System.Object.ReferenceEquals` for array equality — two different array objects are never equal even if they contain the same values (matches F# semantics for mutable containers)
- `valuesEqual` in Eval.fs returns `false` for `ArrayValue _, ArrayValue _` — consistent with reference equality, slightly conservative (two variables pointing to the same array are `=` at the Ast level but not via `valuesEqual`; this is acceptable since `valuesEqual` is only used for pattern match `ConstPat` which will never match an ArrayValue constant anyway)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added formatValue arm during Task 1**
- **Found during:** Task 1 (build verification)
- **Issue:** After adding ArrayValue to the DU, `formatValue` in Eval.fs produced FS0025 incomplete match warning. The plan assigned formatValue to Task 2, but the build could not pass Task 1's zero-warning requirement without it.
- **Fix:** Added the `ArrayValue arr -> sprintf "[|%s|]" ...` arm to formatValue as part of Task 1 compilation fix. Task 2 then confirmed it was already present.
- **Files modified:** src/LangThree/Eval.fs
- **Verification:** Zero warnings after adding the arm; build succeeded.
- **Committed in:** 83ff07b (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 2 — missing critical for zero-warning build)
**Impact on plan:** formatValue arm was planned for Task 2 but was added in Task 1 to meet the zero-warning requirement. No scope change — the arm is identical to what Task 2 specified.

## Issues Encountered
None — the only deviation was the formatValue ordering which was resolved immediately.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ArrayValue and TArray infrastructure is complete and compiles clean
- Plan 38-02 can now add the six flat array builtins (array_create, array_get, array_set, array_length, array_of_list, array_to_list) plus TypeCheck.fs signatures and Prelude/Array.fun
- No blockers

---
*Phase: 38-array-type*
*Completed: 2026-03-25*
