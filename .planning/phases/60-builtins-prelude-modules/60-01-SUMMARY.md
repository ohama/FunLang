---
phase: 60-builtins-prelude-modules
plan: 01
subsystem: interpreter
tags: [fsharp, builtins, typechecker, string, hashtable, eval]

# Dependency graph
requires:
  - phase: 59-prelude-extensions
    provides: established Prelude module pattern and flt test infrastructure
provides:
  - 5 new builtin runtime implementations in Eval.fs initialBuiltinEnv
  - 5 matching type schemes in TypeCheck.fs initialTypeEnv
  - string_endswith, string_startswith, string_trim (BLT-01..03)
  - hashtable_trygetvalue, hashtable_count (BLT-04..05)
affects:
  - 60-02 (Prelude .fun module wrappers call these builtins by name)
  - 61-62 (dot-notation removal phases depend on these as the replacement API)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Builtin pattern: BuiltinValue (fun v -> match v with | ExpectedType x -> result | _ -> failwith msg)"
    - "Curried builtin pattern for 2-arg builtins: nested BuiltinValue lambdas"
    - "Type scheme pattern: Scheme(typeVars, TArrow(...))"
    - "Polymorphic hashtable scheme: Scheme([0;1], TArrow(THashtable(TVar 0, TVar 1), ...))"

key-files:
  created: []
  modified:
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs

key-decisions:
  - "hashtable_trygetvalue returns TupleValue [BoolValue true/false; value_or_unit] matching .NET TryGetValue pattern"
  - "Scheme([0;1], ...) used for hashtable builtins so both key and value type vars are quantified"
  - "String builtins use Scheme([], ...) since they are monomorphic string->string->bool/string"

patterns-established:
  - "BLT-XX: Both Eval.fs and TypeCheck.fs must be updated together — runtime and type checker are separate Maps"

# Metrics
duration: 2min
completed: 2026-03-29
---

# Phase 60 Plan 01: Builtins (BLT-01..05) Summary

**5 new builtin functions added to Eval.fs and TypeCheck.fs: string_endswith, string_startswith, string_trim (string->string->bool/string), and hashtable_trygetvalue (returns bool*value tuple), hashtable_count**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-29T08:33:32Z
- **Completed:** 2026-03-29T08:35:03Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Added string_endswith, string_startswith, string_trim builtins with full error handling to Eval.fs initialBuiltinEnv
- Added hashtable_trygetvalue (returning (bool * 'v) tuple on found/not-found) and hashtable_count to Eval.fs
- Added matching Scheme entries for all 5 builtins to TypeCheck.fs initialTypeEnv
- All 224 existing unit tests pass with no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add 5 builtin functions to Eval.fs** - `03a5bf8` (feat)
2. **Task 2: Add 5 type schemes to TypeCheck.fs** - `cf53a5f` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Eval.fs` - Added 42 lines: BLT-01..05 builtin entries in initialBuiltinEnv
- `src/LangThree/TypeCheck.fs` - Added 9 lines: BLT-01..05 type schemes in initialTypeEnv

## Decisions Made
- hashtable_trygetvalue returns `TupleValue [BoolValue false; TupleValue []]` for missing keys (unit as the "empty" value, consistent with LangThree's unit = empty tuple convention)
- Scheme([0; 1], ...) used for both hashtable builtins so both 'k and 'v type variables are properly quantified — required for correct type inference when pattern-matching the return tuple
- String builtins are Scheme([], ...) (monomorphic) since they only operate on the concrete string type

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All 5 builtins are runtime-ready; Plan 02 can now add Prelude .fun module wrappers that call them
- hashtable_trygetvalue type scheme uses TTuple [TBool; TVar 1] — Prelude wrapper `let tryGetValue ht key = hashtable_trygetvalue ht key` will type-check correctly
- No blockers

---
*Phase: 60-builtins-prelude-modules*
*Completed: 2026-03-29*
