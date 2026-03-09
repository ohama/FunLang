---
phase: 02-algebraic-data-types
plan: 02
subsystem: type-system
tags: [adt, discriminated-union, type-constructor, constructor-env]

# Dependency graph
requires:
  - phase: 02-01
    provides: TypeDecl/ConstructorDecl AST, TEName/TEVar in TypeExpr
provides:
  - TData type constructor for named ADT types
  - ConstructorInfo/ConstructorEnv for constructor signatures
  - elaborateTypeDecl function converting TypeDecl AST to ConstructorEnv
  - TData support in unification, substitution, and free variables
affects: [02-03, 02-04, 02-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TData(name, typeArgs) pattern for representing named ADT types"
    - "ConstructorEnv as Map<string, ConstructorInfo> for constructor lookup"
    - "Deterministic type param mapping ('a->0, 'b->1) in elaborateTypeDecl"

key-files:
  created: []
  modified:
    - src/LangThree/Type.fs
    - src/LangThree/Elaborate.fs
    - src/LangThree/Unify.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs
    - tests/LangThree.Tests/IntegrationTests.fs

key-decisions:
  - "TEName in elaborateTypeDecl produces TData(name, []) for recursive/named type references"
  - "TData unification requires same name and same arity, then unifies args pairwise"

patterns-established:
  - "TData(name, args): named ADT type representation with type arguments"
  - "ConstructorInfo: {TypeParams, ArgType option, ResultType} for each constructor"

# Metrics
duration: 3min
completed: 2026-03-09
---

# Phase 2 Plan 02: Type System Extension Summary

**TData type constructor, ConstructorInfo/ConstructorEnv definitions, and elaborateTypeDecl for converting TypeDecl AST to typed constructor environment**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-09T00:53:55Z
- **Completed:** 2026-03-09T00:56:43Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- TData type constructor added to Type union for named ADT types (e.g., Option<'a>, Tree)
- ConstructorInfo and ConstructorEnv types for tracking constructor type signatures
- elaborateTypeDecl function converts TypeDecl AST to ConstructorEnv with deterministic type param mapping
- TData fully integrated into type system: formatType, apply, freeVars, unification, NotAFunction checks

## Task Commits

Each task was committed atomically:

1. **Task 1: Add TData constructor and ConstructorEnv to Type.fs** - `9484d20` (feat)
2. **Task 2: Extend Elaborate.fs for ADT type elaboration** - `6bed9c6` (feat)
3. **Task 3: Add unit tests for type elaboration** - `3fe2a5a` (test)

## Files Created/Modified
- `src/LangThree/Type.fs` - TData constructor, ConstructorInfo/ConstructorEnv types, formatType/apply/freeVars updates
- `src/LangThree/Elaborate.fs` - elaborateTypeDecl function with substTypeExpr for type param resolution
- `src/LangThree/Unify.fs` - TData pairwise unification support
- `src/LangThree/Bidir.fs` - TData in NotAFunction check
- `src/LangThree/Infer.fs` - TData in NotAFunction check
- `tests/LangThree.Tests/IntegrationTests.fs` - 3 elaboration tests (simple, parametric, recursive ADT)

## Decisions Made
- TEName in elaborateTypeDecl produces TData(name, []) for recursive and named type references (not a type variable, not an error)
- TData unification requires same name and same arity, then unifies type arguments pairwise

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added TData support to Unify.fs**
- **Found during:** Task 1 (Type.fs extension)
- **Issue:** Plan only specified Type.fs changes, but Unify.fs needs TData case for correct unification
- **Fix:** Added TData pairwise unification case in unifyWithContext
- **Files modified:** src/LangThree/Unify.fs
- **Verification:** Build succeeds, all tests pass
- **Committed in:** 9484d20 (Task 1 commit)

**2. [Rule 2 - Missing Critical] Added TData to NotAFunction checks in Bidir.fs and Infer.fs**
- **Found during:** Task 1 (Type.fs extension)
- **Issue:** NotAFunction checks enumerated concrete types but didn't include TData
- **Fix:** Added TData _ to pattern match in both Bidir.fs and Infer.fs
- **Files modified:** src/LangThree/Bidir.fs, src/LangThree/Infer.fs
- **Verification:** Build succeeds
- **Committed in:** 9484d20 (Task 1 commit)

**3. [Rule 1 - Bug] Fixed recursive ADT test to use TEName instead of TEVar**
- **Found during:** Task 3 (test writing)
- **Issue:** Plan used TEVar "IntList" for recursive type reference, but IntList is a named type (TEName), not a type variable (TEVar)
- **Fix:** Used Ast.TEName "IntList" in test and handled TEName in elaborateTypeDecl
- **Files modified:** tests/LangThree.Tests/IntegrationTests.fs
- **Verification:** Test passes correctly with TData("IntList", []) result
- **Committed in:** 3fe2a5a (Task 3 commit)

---

**Total deviations:** 3 auto-fixed (2 missing critical, 1 bug)
**Impact on plan:** All auto-fixes essential for correctness. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Type system now represents ADT types with TData constructor
- ConstructorEnv ready for use in type inference (02-03: constructor application type checking)
- TEName elaboration in main elaborateWithVars still uses placeholder (fresh TVar) - needs update in 02-03/02-04 when type checking uses ConstructorEnv

---
*Phase: 02-algebraic-data-types*
*Completed: 2026-03-09*
