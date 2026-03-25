---
phase: 39-hashtable-type
plan: 01
subsystem: runtime-types
tags: [hashtable, dictionary, mutable, type-system, builtins, flt-tests]

# Dependency graph
requires:
  - phase: 38-array-type
    provides: ArrayValue DU pattern and TArray type pattern used verbatim for HashtableValue/THashtable

provides:
  - HashtableValue of Dictionary<Value, Value> in Value DU (Ast.fs)
  - THashtable of Type * Type in Type DU (Type.fs) with full propagation (formatType, apply, freeVars, collectVars)
  - THashtable unification arm in Unify.fs
  - THashtable in NotAFunction guard in Bidir.fs
  - Six hashtable_* builtins in Eval.fs initialBuiltinEnv
  - Six Scheme entries in TypeCheck.fs initialTypeEnv
  - Prelude/Hashtable.fun module wrapper (qualified access)
  - Four flt integration tests covering HT-01 through HT-06

affects: [40-ref-type, 41-channel-type, future-stdlib-phases]

# Tech tracking
tech-stack:
  added: [System.Collections.Generic.Dictionary]
  patterns:
    - "Mutable reference type DU case with CustomEquality/identity hash/reference equality/zero compare"
    - "THashtable of Type * Type for key+value parameterized type"
    - "BuiltinValue curried closures capturing htVal then keyVal for hashtable operations"
    - "Prelude module wrapper with module Hashtable = ... qualified access (no open)"

key-files:
  created:
    - Prelude/Hashtable.fun
    - tests/flt/file/hashtable/hashtable-basic.flt
    - tests/flt/file/hashtable/hashtable-mutation.flt
    - tests/flt/file/hashtable/hashtable-keys.flt
    - tests/flt/file/hashtable/hashtable-remove.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Type.fs
    - src/LangThree/Unify.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs

key-decisions:
  - "HashtableValue uses System.Collections.Generic.Dictionary<Value, Value> as backing store (direct F# dict, no wrapper)"
  - "Reference equality for HashtableValue identical to ArrayValue pattern"
  - "flt tests use let _ = Hashtable.set ... (not let () =) because () pattern not supported at module level"
  - "hashtable-keys.flt uses single-key ht to avoid Dict ordering non-determinism (List.sort not available as builtin)"

patterns-established:
  - "Reference type DU case: identity hash (RuntimeHelpers.GetHashCode), reference equality (ReferenceEquals), zero compare"
  - "Two-param type constructor THashtable (k, v) requires both type params in all Type.fs propagation arms"
  - "flt tests for mutable operations must use let _ = pattern, not let () ="

# Metrics
duration: ~15min
completed: 2026-03-25
---

# Phase 39 Plan 01: Hashtable Type Summary

**HashtableValue DU case + THashtable type constructor + 6 hashtable_* builtins via Prelude/Hashtable.fun, verified by 4 flt tests**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-25
- **Completed:** 2026-03-25
- **Tasks:** 2
- **Files modified:** 11 (6 modified + 5 created)

## Accomplishments
- Added `HashtableValue of Dictionary<Value, Value>` to Value DU with full CustomEquality/Comparison obligations
- Added `THashtable of Type * Type` to Type DU with complete propagation (formatType, formatTypeNormalized, apply, freeVars)
- Added THashtable unification arm and NotAFunction guard — type system fully aware of hashtable type
- Registered 6 builtins: hashtable_create/get/set/containsKey/keys/remove in Eval.fs + TypeCheck.fs
- Created Prelude/Hashtable.fun module wrapper for qualified `Hashtable.*` access
- 4 flt integration tests pass covering HT-01 through HT-06

## Task Commits

Each task was committed atomically:

1. **Task 1: HashtableValue DU + THashtable type infrastructure** - `359d052` (feat)
2. **Task 2: 6 builtins + Prelude/Hashtable.fun + 4 flt tests** - `a1b2483` (feat)

## Files Created/Modified
- `src/LangThree/Ast.fs` - HashtableValue DU case, GetHashCode, valueEqual, valueCompare arms
- `src/LangThree/Type.fs` - THashtable, formatType/formatTypeNormalized/apply/freeVars arms
- `src/LangThree/Unify.fs` - THashtable unification arm
- `src/LangThree/Bidir.fs` - THashtable in NotAFunction guard
- `src/LangThree/Eval.fs` - formatValue + valuesEqual arms; 6 hashtable_* builtins
- `src/LangThree/TypeCheck.fs` - 6 Scheme entries for hashtable_* builtins
- `Prelude/Hashtable.fun` - module Hashtable = ... qualified wrapper (created)
- `tests/flt/file/hashtable/hashtable-basic.flt` - HT-01/02/04 tests (created)
- `tests/flt/file/hashtable/hashtable-mutation.flt` - HT-03 overwrite test (created)
- `tests/flt/file/hashtable/hashtable-keys.flt` - HT-05 keys list test (created)
- `tests/flt/file/hashtable/hashtable-remove.flt` - HT-06 remove test (created)

## Decisions Made
- Used `System.Collections.Generic.Dictionary<Value, Value>` directly as backing store — no wrapper needed
- Reference equality for HashtableValue (matching ArrayValue pattern) — two hashtables are only equal if they are the same object
- flt tests must use `let _ =` not `let () =` for unit-returning expressions at module level (parse limitation)
- hashtable-keys.flt uses single-key ht for determinism — `List.sort` is not available as a builtin

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] let () = pattern is not valid at module level**
- **Found during:** Task 2 (flt test creation)
- **Issue:** Plan's proposed test code used `let () = Hashtable.set ht ...` which causes a parse error
- **Fix:** Changed all unit-returning assignments to `let _ =` pattern, matching the array test convention
- **Files modified:** All four flt test files
- **Verification:** Tests pass with correct output
- **Committed in:** a1b2483 (Task 2 commit)

**2. [Rule 1 - Bug] List.sort not available as builtin**
- **Found during:** Task 2 (hashtable-keys.flt)
- **Issue:** Plan suggested sorting keys for determinism, but `List.sort` is not in Prelude or builtins
- **Fix:** Rewrote hashtable-keys.flt to use single-key hashtable — tests `List.length ks = 1` and `containsKey ht "only" = true`
- **Files modified:** tests/flt/file/hashtable/hashtable-keys.flt
- **Verification:** Test passes
- **Committed in:** a1b2483 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 bugs in plan's proposed test code)
**Impact on plan:** Both fixes minor — test logic identical in intent, syntax/stdlib adjusted for reality.

## Issues Encountered
- `println x` at module top level without `let _ =` causes parse error in LangThree — array tests already demonstrated correct pattern but plan used F# style `let () =`

## Next Phase Readiness
- HashtableValue and THashtable fully integrated into runtime and type system
- All 6 required builtins callable via both flat names (hashtable_create) and qualified access (Hashtable.create)
- Build: 0 warnings, 0 errors; 224 unit tests + 4 new flt tests all pass
- Phase 39 complete, ready for Phase 40

---
*Phase: 39-hashtable-type*
*Completed: 2026-03-25*
