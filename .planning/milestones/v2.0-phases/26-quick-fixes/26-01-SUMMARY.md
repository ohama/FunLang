---
phase: 26-quick-fixes
plan: "01"
subsystem: interpreter
tags: [fsharp, builtin, type-system, elaborator, failwith, option]

requires:
  - phase: prior-phases
    provides: LangThreeException, initialBuiltinEnv, initialTypeEnv, Elaborate.fs TEData case

provides:
  - failwith builtin function (raises LangThreeException, polymorphic return type)
  - lowercase option/result as aliases for Option/Result in type annotations

affects:
  - STD-01 library functions that use failwith
  - TYPE-03 any code using lowercase option or result in type annotations

tech-stack:
  added: []
  patterns:
    - "Builtin registration: two-point registration (Eval.initialBuiltinEnv + TypeCheck.initialTypeEnv)"
    - "Type alias normalization: one-line match in Elaborate.fs TEData case, no parser changes"

key-files:
  created: []
  modified:
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Elaborate.fs

key-decisions:
  - "failwith return type is Scheme([0], TArrow(TString, TVar 0)) - polymorphic to unify with any branch type"
  - "failwith raises LangThreeException (not System.Exception) so try-with in user code can catch it"
  - "option/result normalization done in Elaborate.fs TEData case only, not TEName (TEName is correct as fresh TVar)"

patterns-established:
  - "Type alias normalization via one-line match at elaboration boundary, not at parse time"

duration: 4min
completed: 2026-03-24
---

# Phase 26 Plan 01: Quick Fixes - failwith builtin and option alias

**`failwith` builtin with polymorphic return added to evaluator and type checker; lowercase `option`/`result` normalized to `Option`/`Result` in the elaborator**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-24T07:50:36Z
- **Completed:** 2026-03-24T07:54:49Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- `failwith "msg"` now works in user code: raises `LangThreeException(StringValue msg)`, catchable by `try-with`
- `failwith` has polymorphic return type so it works in if-else branches without type errors
- `int option` and `string option` in type annotations normalize to `Option` and unify with `TData("Option", ...)`
- `result` similarly normalizes to `Result`
- All 199 existing tests pass with no regressions

## Task Commits

1. **Task 1: Add failwith builtin (STD-01)** - `bc1754e` (feat)
2. **Task 2: Add option/result type alias (TYPE-03)** - `9420977` (feat)

## Files Created/Modified
- `src/LangThree/Eval.fs` - Added `failwith` entry to `initialBuiltinEnv`
- `src/LangThree/TypeCheck.fs` - Added `failwith` type scheme to `initialTypeEnv`
- `src/LangThree/Elaborate.fs` - Added canonical name normalization in `TEData` cases of `elaborateWithVars` and `substTypeExprWithMap`

## Decisions Made
- `failwith` uses `Scheme([0], TArrow(TString, TVar 0))` — polymorphic return unifies with any expected type, matching `raise`'s behavior. Using unit return would break `if cond then failwith "msg" else value` patterns.
- `failwith` raises `LangThreeException(StringValue msg)` — the only exception type caught by the language's `try-with` evaluator. Using `System.Exception` would make it uncatchable in user code.
- `option`/`result` normalization is in `TEData` case only — `TEName` is intentionally left as a fresh `TVar` for bare named types; changing it would incorrectly resolve `TEName "option"` to `TData("Option", [])` with no type args.

## Deviations from Plan

None - plan executed exactly as written.

Note: The plan's test examples used `let x : option int = Some 42` and `let f (x : option string) = ...` syntax. This language uses postfix type application (`int option`) and only supports annotated params in lambda form (`fun (v : string option) ->`). The verification was adapted to the actual language syntax while exercising the same code paths.

## Issues Encountered
- The plan's verification tests used `print_int` (unavailable) and F#-style `let x : type = val` annotation syntax (not supported in this language). Adapted to use `to_string`/`println` and the actual postfix type annotation syntax (`int option`, `fun (v : string option) ->`). No code changes needed; the implementation is correct.

## Next Phase Readiness
- `failwith` builtin ready for use in STD-01 standard library functions
- `option`/`result` type aliases ready for TYPE-03 user-facing documentation/code
- No blockers

---
*Phase: 26-quick-fixes*
*Completed: 2026-03-24*
