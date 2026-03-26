---
phase: 38-array-type
plan: 02
subsystem: runtime
tags: [array, builtins, prelude, type-checker, module-system]

# Dependency graph
requires:
  - phase: 38-01
    provides: ArrayValue DU case and TArray type constructor

provides:
  - Six array_* builtin functions registered in Eval.fs initialBuiltinEnv
  - Six Scheme entries in TypeCheck.fs initialTypeEnv for array_* builtins
  - Prelude/Array.fun module wrapper with qualified Array.create/get/set/length/ofList/toList
  - 4 flt integration tests covering basic ops, mutation, OOB, and list conversion
  - Bug fix: TypeCheck.fs SubModules captured entire outer mods map (breaking qualified access)

affects: ["39-hashtable-type", "40-record-mutation", "41-for-loops", "future-stdlib-phases"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "OOB errors use LangThreeException not .NET exceptions (catchable by language try-with)"
    - "Prelude module wrappers do NOT use open to avoid flat namespace conflicts with same-named functions across modules"
    - "Module SubModules should only include newly-defined inner modules, not inherited outer mods"

key-files:
  created:
    - Prelude/Array.fun
    - tests/flt/file/array/array-basic.flt
    - tests/flt/file/array/array-mutation.flt
    - tests/flt/file/array/array-oob.flt
    - tests/flt/file/array/array-convert.flt
  modified:
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs

key-decisions:
  - "Array.fun does NOT include 'open Array' - avoids shadowing List.length and other same-named functions"
  - "OOB access raises LangThreeException (catchable) not .NET System.Exception (uncatchable)"
  - "TypeCheck.fs SubModules bug fixed: innerMods filtered to exclude outer mods before assigning to SubModules"

patterns-established:
  - "Prelude module: define 'module Foo = ...' without 'open Foo' when Foo exports names that conflict with other modules"
  - "Array builtins: array_* flat names (builtins) + Array.* qualified access (prelude module wrapper)"

# Metrics
duration: ~30min
completed: 2026-03-25
---

# Phase 38 Plan 02: Array Builtins + Prelude Summary

**Six array_* builtins wired into Eval.fs/TypeCheck.fs with Prelude/Array.fun qualified access, plus a TypeCheck SubModules scoping bug fix enabling correct qualified access across all modules**

## Performance

- **Duration:** ~30 min
- **Started:** 2026-03-25T00:00:00Z
- **Completed:** 2026-03-25T06:08:32Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- Registered `array_create`, `array_get`, `array_set`, `array_length`, `array_of_list`, `array_to_list` in `Eval.fs initialBuiltinEnv` with `LangThreeException` for OOB errors
- Added correct polymorphic `Scheme` entries in `TypeCheck.fs initialTypeEnv` for all six builtins
- Created `Prelude/Array.fun` module wrapper providing `Array.create/get/set/length/ofList/toList` qualified access
- Added 4 passing flt integration tests (478/478 total flt tests pass, 224/224 unit tests pass)
- Fixed latent bug in `TypeCheck.fs` where `ModuleDecl` used full outer `mods` as `SubModules`, causing `mergeModuleExportsForTypeCheck` to flatten all sibling module exports and corrupt qualified access type lookup

## Task Commits

Each task was committed atomically:

1. **Task 1: Register six array_* builtins in Eval.fs initialBuiltinEnv and TypeCheck.fs initialTypeEnv** - `e093f4d` (feat)
2. **Task 2: Create Prelude/Array.fun and add flt integration tests** - `fae4ff0` (feat, includes SubModules bug fix)

## Files Created/Modified
- `src/LangThree/Eval.fs` - Added six array_* BuiltinValue entries to initialBuiltinEnv (ARR-01 through ARR-06)
- `src/LangThree/TypeCheck.fs` - Added six Scheme entries to initialTypeEnv; fixed SubModules scoping bug
- `Prelude/Array.fun` - Module Array wrapper (no open to preserve flat namespace)
- `tests/flt/file/array/array-basic.flt` - Tests Array.create, Array.get, Array.length
- `tests/flt/file/array/array-mutation.flt` - Tests Array.set in-place mutation
- `tests/flt/file/array/array-oob.flt` - Tests OOB exception catchable by try-with
- `tests/flt/file/array/array-convert.flt` - Tests Array.ofList/toList round-trip

## Decisions Made
- **No `open Array` in Prelude/Array.fun**: Adding `open Array` would bring `Array.length` into the flat namespace, shadowing `List.length` for any code that used qualified `List.length` (due to the SubModules bug pattern in mergeModuleExportsForTypeCheck). Even after fixing that bug, it's better practice not to open a module that has conflicting names. Users use `Array.create` etc. directly.
- **OOB raises `LangThreeException`**: The plan showed `failwithf` but that raises a .NET exception not catchable by the language's `try-with`. Changed to `raise (LangThreeException ...)` so `try Array.get arr 5 with e -> -1` works correctly.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed TypeCheck.fs SubModules capturing entire outer mods map**
- **Found during:** Task 2 (flt integration tests failing on List.length qualified access)
- **Issue:** `ModuleDecl` arm in `typeCheckDecls` set `SubModules = innerMods` where `innerMods` was the full accumulated modules map passed through the type checker. This meant every module's `SubModules` contained all previously-defined sibling modules. Then `mergeModuleExportsForTypeCheck` merges ALL submodule exports when resolving a qualified access, flattening sibling module exports and overwriting types. `List.length` was overwritten by `Array.length` (TArray -> TInt) in the merged env.
- **Fix:** Changed `SubModules = innerMods` to filter out modules already in the outer `mods`: `let newSubMods = Map.fold (fun acc k v -> if Map.containsKey k mods then acc else Map.add k v acc) Map.empty innerMods`
- **Files modified:** `src/LangThree/TypeCheck.fs`
- **Verification:** `List.length [1;2;3]` type-checks and evaluates to `3`; 224/224 unit tests pass; 478/478 flt tests pass
- **Committed in:** `fae4ff0` (Task 2 commit)

**2. [Rule 1 - Bug] Changed OOB error from failwithf to LangThreeException**
- **Found during:** Task 1 (analyzing array-oob.flt requirement)
- **Issue:** Plan specified `failwithf "Array.get: index %d..."` which raises .NET `System.Exception`, not catchable by LangThree's `try-with` (which only catches `LangThreeException`)
- **Fix:** Changed to `raise (LangThreeException (StringValue (sprintf "...")))` matching the pattern used by `read_file` and other builtins
- **Files modified:** `src/LangThree/Eval.fs`
- **Verification:** `array-oob.flt` passes; `try Array.get arr 5 with e -> -1` returns `-1`
- **Committed in:** `e093f4d` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bugs)
**Impact on plan:** Both fixes required for correctness. No scope creep.

## Issues Encountered
- TypeCheck SubModules bug was latent before Array.fun existed (no other module defined `length` as a top-level export). Array being loaded first alphabetically exposed the bug.

## Next Phase Readiness
- Phase 38 complete: ArrayValue + TArray (38-01) + builtins + prelude (38-02)
- Phase 39 (Hashtable Type) can proceed - independent DU case
- Phase 40 (Record Mutation) can proceed - independent feature
- Array builtins fully accessible as both `Array.create` (qualified) and `array_create` (flat)

---
*Phase: 38-array-type*
*Completed: 2026-03-25*
