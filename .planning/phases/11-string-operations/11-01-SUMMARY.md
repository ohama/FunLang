---
phase: 11-string-operations
plan: "01"
subsystem: interpreter
tags: [fsharp, builtins, string-operations, value-du, type-env, eval]

# Dependency graph
requires:
  - phase: 10-unit-type
    provides: TupleValue [] unit representation and clean eval infrastructure
provides:
  - BuiltinValue of fn:(Value->Value) in Ast.Value DU
  - valuesEqual helper replacing F# structural = on Value (needed since function types break auto equality)
  - App eval dispatch for BuiltinValue before | _ -> failwith
  - formatValue case for BuiltinValue
  - initialBuiltinEnv with 6 string built-in functions
  - 6 string function type schemes in TypeCheck.initialTypeEnv
affects:
  - 11-02 (Plan 02 will wire initialBuiltinEnv into Program.fs and Repl.fs)
  - Any future phase adding built-in functions

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "BuiltinValue of fn:(Value->Value) — native F# function carrier in Value DU"
    - "initialBuiltinEnv pattern — Map<string,Value> of all built-ins for startup merging"
    - "valuesEqual recursive function — explicit structural equality for Value to avoid function-type constraint issues"
    - "Curried BuiltinValue — nested BuiltinValue wrappers for multi-arg builtins (string_sub takes 3)"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Exhaustive.fs
    - src/LangThree/Format.fs

key-decisions:
  - "Added valuesEqual helper rather than [<CustomEquality>] attribute because Value is a mutually recursive DU; explicit helper is simpler and more readable"
  - "to_string uses Scheme([0], TArrow(TVar 0, TString)) — permissively polymorphic; runtime enforces int/bool/string only"
  - "string_sub uses start+length semantics: string_sub s 1 3 = s.[1..3] = 'ell' (not start+end_exclusive)"

patterns-established:
  - "BuiltinValue: All future native built-ins use BuiltinValue of fn:(Value->Value) in the Value DU"
  - "initialBuiltinEnv: Register built-ins in this Map; Plan 02 merges into startup env"
  - "valuesEqual: Use this for Value comparisons; never use F# = operator on Value directly"

# Metrics
duration: 5min
completed: 2026-03-10
---

# Phase 11 Plan 01: String Operations Infrastructure Summary

**BuiltinValue DU case + valuesEqual helper + initialBuiltinEnv with 6 string functions + 6 type schemes in initialTypeEnv; build: 0 errors, 0 warnings**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-10T07:30:55Z
- **Completed:** 2026-03-10T07:35:37Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Added `BuiltinValue of fn: (Value -> Value)` to the `Value` DU in `Ast.fs`
- Added `valuesEqual` recursive function in `Eval.fs` to replace F# `=` operator on `Value` (needed because function types in `BuiltinValue` break auto-derived structural equality)
- Added `App` eval dispatch for `BuiltinValue` and `formatValue` case
- Added `initialBuiltinEnv` in `Eval.fs` with all 6 string functions (`string_length`, `string_concat`, `string_sub`, `string_contains`, `to_string`, `string_to_int`)
- Added 6 string function type schemes to `TypeCheck.initialTypeEnv`
- Fixed 3 pre-existing warnings (`Exhaustive.fs` FS0025, `Format.fs` FS0025, `Eval.fs` FS0067) — build now produces 0 warnings

## Task Commits

Each task was committed atomically:

1. **Task 1: Add BuiltinValue to Ast.fs and wire into Eval.fs dispatch + formatValue** - `c609acd` (feat)
2. **Task 2: Add initialBuiltinEnv (6 functions) in Eval.fs and 6 type schemes in TypeCheck.fs** - `60e4538` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Ast.fs` - Added `BuiltinValue of fn: (Value -> Value)` to `Value` DU after `RecordValue`
- `src/LangThree/Eval.fs` - Added `valuesEqual`, `initialBuiltinEnv`, `formatValue` BuiltinValue case, `App` BuiltinValue dispatch, fixed FS0067
- `src/LangThree/TypeCheck.fs` - Added 6 string function type schemes to `initialTypeEnv`
- `src/LangThree/Exhaustive.fs` - Fixed pre-existing FS0025: added `RecordPat -> WildcardPat` case
- `src/LangThree/Format.fs` - Fixed pre-existing FS0025: added `MODULE`, `NAMESPACE`, `OPEN` token cases

## Decisions Made
- Used explicit `valuesEqual` function instead of `[<CustomEquality; NoComparison>]` attribute on `Value`. The `Value` DU is mutually recursive with `Expr` and `Env` in `Ast.fs`, making custom equality attributes complex. The explicit helper is simpler and clearer.
- `to_string` type scheme is `Scheme([0], TArrow(TVar 0, TString))` — permissively polymorphic. The type system cannot express "int or bool only" without type classes. The runtime implementation rejects unsupported types with a clear error.
- `string_sub` uses start+length semantics: `string_sub "hello" 1 3` = `"hello".[1..3]` = `"ell"`. Implemented with bounds check to avoid F# `ArgumentOutOfRangeException`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added valuesEqual helper to fix FS0001 equality constraint errors**
- **Found during:** Task 1 (after adding BuiltinValue to Value DU)
- **Issue:** `BuiltinValue of fn: (Value -> Value)` carries a function type; F# cannot auto-derive equality for DUs containing functions. The `Equal`/`NotEqual` eval cases used `TupleValue l = TupleValue r` and `ListValue l = ListValue r` which invoked F# generic `=` on `Value list`, causing 6 FS0001 errors.
- **Fix:** Added `valuesEqual : Value -> Value -> bool` recursive function that matches structurally on each Value case, avoiding the `=` operator. Updated `Equal`/`NotEqual` to call `valuesEqual`.
- **Files modified:** `src/LangThree/Eval.fs`
- **Verification:** Build succeeded with 0 errors after fix
- **Committed in:** c609acd (Task 1 commit)

**2. [Rule 1 - Bug] Fixed 3 pre-existing warnings to achieve 0-warning build**
- **Found during:** Task 1 (first build attempt)
- **Issue:** 3 pre-existing warnings already existed before Phase 11: `Exhaustive.fs` FS0025 (missing `RecordPat` case), `Format.fs` FS0025 (missing `MODULE`/`NAMESPACE`/`OPEN` tokens), `Eval.fs` FS0067 (redundant `:? System.Exception` type test)
- **Fix:** Added missing pattern cases and removed redundant type annotation
- **Files modified:** `src/LangThree/Exhaustive.fs`, `src/LangThree/Format.fs`, `src/LangThree/Eval.fs`
- **Verification:** Build produces 0 warnings
- **Committed in:** c609acd (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (1 build-blocking bug, 1 pre-existing warning cleanup)
**Impact on plan:** Both fixes necessary for correctness and 0-warning build requirement. No scope creep.

## Issues Encountered
- `BuiltinValue of fn: (Value -> Value)` caused FS0001 equality constraint errors on existing `Equal`/`NotEqual` eval cases that used F# structural `=` on `Value list` and `Map<string, Value ref>`. Resolved by adding `valuesEqual` helper.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `BuiltinValue` infrastructure complete; all 6 string functions implemented and type-checked
- `initialBuiltinEnv` is defined and exported from `Eval.fs`
- Plan 02 must wire `initialBuiltinEnv` into `Program.fs` and `Repl.fs` startup environments, and add `.flt` integration tests
- No blockers

---
*Phase: 11-string-operations*
*Completed: 2026-03-10*
