---
phase: 74-builtin-instances-tests
plan: 01
subsystem: typeclasses
tags: [typeclass, Show, Eq, prelude, elaboration, instances]

# Dependency graph
requires:
  - phase: 73-elaboration
    provides: elaborateTypeclasses converts InstanceDecl to LetDecl before eval
provides:
  - Prelude/Typeclass.fun with Show and Eq classes + instances for int/bool/string/char
  - Prelude.fs calls elaborateTypeclasses before evalModuleDecls (both loadPrelude and loadAndEvalFileImpl)
  - show and eq available as prelude bindings without user declarations
affects: [74-02, any future phase using Show/Eq instances]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Prelude/Typeclass.fun: typeclass declarations as regular prelude files loaded alphabetically"
    - "TypeCheck: TypeClassDecl redeclaration from prelude is idempotent (skip if already defined)"

key-files:
  created:
    - Prelude/Typeclass.fun
  modified:
    - src/LangThree/Prelude.fs
    - src/LangThree/TypeCheck.fs
    - tests/flt/file/typeclass/typeclass-infer-basic.flt
    - tests/flt/file/typeclass/typeclass-infer-resolve.flt
    - tests/flt/file/typeclass/typeclass-infer-errors.flt

key-decisions:
  - "TypeClassDecl redeclaration is idempotent: if class already in classEnv (from prelude), skip without error. Duplicate instances are still errors."
  - "List/option Show instances omitted: constraint resolution uses exact type equality (TList(TVar 0) != TList TInt), so polymorphic instances would not match. Deferred to future phase."
  - "show 'x' returns \"'x'\" (with single quotes) since to_string delegates to formatValue for CharValue"

patterns-established:
  - "Prelude typeclass files: named with capital letters so alphabetical load order puts them after constructor-based modules"
  - "Test files that depended on user-declared Show class updated to rely on prelude-provided Show instead"

# Metrics
duration: 20min
completed: 2026-03-31
---

# Phase 74 Plan 01: Built-in Show and Eq Instances Summary

**Show and Eq prelude instances for int/bool/string/char; Prelude.fs now calls elaborateTypeclasses before eval so instance methods bind at runtime**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-03-31T00:00:00Z
- **Completed:** 2026-03-31T00:20:00Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- Created `Prelude/Typeclass.fun` with Show and Eq typeclasses + 4 primitive instances each
- Fixed `Prelude.fs` to call `Elaborate.elaborateTypeclasses` before `evalModuleDecls` in both `loadPrelude` and `loadAndEvalFileImpl`
- Fixed TypeCheck to treat duplicate typeclass declaration as a no-op (idempotent) so user code can re-declare prelude classes
- Updated 3 existing typeclass inference tests to use prelude-provided Show instead of redeclaring it
- All 671 flt tests pass

## Task Commits

1. **Task 1: Create Prelude/Typeclass.fun** - `2b8fc4f` (feat)
2. **Task 2: Fix Prelude.fs elaboration + TypeCheck idempotency** - `1ad21ed` (feat)

## Files Created/Modified
- `Prelude/Typeclass.fun` - Show and Eq typeclass declarations with instances for int, bool, string, char
- `src/LangThree/Prelude.fs` - Added `Elaborate.elaborateTypeclasses` call in `loadPrelude` and `loadAndEvalFileImpl`
- `src/LangThree/TypeCheck.fs` - TypeClassDecl arm: skip redeclaration if class already in classEnv
- `tests/flt/file/typeclass/typeclass-infer-basic.flt` - Removed user-declared Show class (now from prelude)
- `tests/flt/file/typeclass/typeclass-infer-resolve.flt` - Removed user-declared Show class (now from prelude)
- `tests/flt/file/typeclass/typeclass-infer-errors.flt` - Removed user-declared Show class; updated type var name in expected error

## Decisions Made
- TypeClassDecl redeclaration is idempotent: if the class is already in classEnv (loaded from prelude), the redeclaration is silently skipped. This lets user code re-declare prelude classes without breaking. Duplicate instances are still errors.
- List/option Show instances were intentionally omitted: the constraint resolver uses exact type equality (`ii.InstanceType = c.TypeArg`), so `instance Show (list 'a)` would register as `TList(TVar 0)` which would never match `TList TInt`. Supporting polymorphic instances requires a unification-based instance search — deferred to a future phase.
- `show 'x'` returns `"'x'"` (including single quotes) because `to_string` delegates to `formatValue` for `CharValue`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] TypeCheck: TypeClassDecl redeclaration was an error**
- **Found during:** Task 2 (running full test suite)
- **Issue:** When Prelude/Typeclass.fun loads Show/Eq, TypeCheck registers them. Then user test files that also declare `typeclass Show 'a` hit `DuplicateModuleName` error, breaking 3 existing typeclass tests.
- **Fix:** Changed TypeCheck `TypeClassDecl` arm to return current state unchanged if class already in `clsEnv` (idempotent skip). Downstream `InstanceDecl` duplicate detection still fires for actual duplicate instances.
- **Files modified:** `src/LangThree/TypeCheck.fs`
- **Verification:** All 12 typeclass tests pass, including typeclass-infer-poly which expects E0702 for duplicate instance
- **Committed in:** `1ad21ed` (Task 2 commit)

**2. [Rule 1 - Bug] Existing typeclass tests redeclared Show class/instances**
- **Found during:** Task 2 (running full test suite)
- **Issue:** Three tests (`typeclass-infer-basic`, `typeclass-infer-resolve`, `typeclass-infer-errors`) declared their own `typeclass Show` and `instance Show int/bool`. With prelude now providing these, instance redeclarations caused E0702 errors.
- **Fix:** Updated tests to rely on prelude-provided Show. Removed local typeclass/instance declarations. Updated expected error type var name in errors test (`'g -> 'g` → `'x -> 'x`).
- **Files modified:** 3 test files
- **Verification:** All 671 flt tests pass
- **Committed in:** `1ad21ed` (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both fixes necessary for correctness. No scope creep.

## Issues Encountered
- Polymorphic instances (`instance Show (list 'a)`) are not supported by current exact-equality instance resolution. The success criterion for `show [1; 2; 3]` cannot be met without extending the instance resolver. This is documented as a known limitation — the plan itself noted this was uncertain.

## Next Phase Readiness
- `show` and `eq` are available as prelude bindings for int, bool, string, char
- Plan 02 can now write flt tests using show/eq without any setup
- Polymorphic Show instances (list, option) require extending instance resolution to use unification rather than exact equality — out of scope for this phase

---
*Phase: 74-builtin-instances-tests*
*Completed: 2026-03-31*
