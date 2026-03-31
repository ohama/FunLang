---
phase: 72-type-checker-constraint-inference
plan: 01
subsystem: type-checker
tags: [typeclasses, ClassEnv, InstanceEnv, Scheme, constraints, Diagnostic, Elaborate, TypeCheck, Eval]

# Dependency graph
requires:
  - phase: 71-parsing-and-ast
    provides: TypeClassDecl/InstanceDecl AST nodes, TEConstrained TypeExpr, parser support
  - phase: 70-type-class-foundations
    provides: ClassEnv/InstanceEnv types, Scheme(vars,constraints,ty), ClassInfo/InstanceInfo

provides:
  - TypeClassDecl processing: validates uniqueness, builds constrained method schemes, populates ClassEnv and TypeEnv
  - InstanceDecl processing: validates class exists, checks duplicates, validates method set, type-checks bodies, populates InstanceEnv
  - TEConstrained elaboration: inner type elaborated, constraints deferred to Scheme construction
  - Type class diagnostic errors E0701-E0706 (NoInstance/DuplicateInstance/UnknownTypeClass/MethodTypeMismatch/MissingMethod/ExtraMethod)
  - currentClassEnv/currentInstEnv mutable refs in TypeCheck.fs for Bidir access in Plan 02

affects:
  - 72-02: constraint inference (reads currentClassEnv/currentInstEnv from TypeCheck.fs mutable refs)
  - 72-03: integration tests (typeclass declarations now fully processed)
  - 73-dictionary-passing: instance method bodies type-checked here

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "typeCheckDecls threads ClassEnv*InstanceEnv as additional fold accumulator fields (7-tuple)"
    - "Module-level mutable refs (currentClassEnv/currentInstEnv) expose environments to Bidir without call-site signature changes"
    - "TypeClassDecl allocates one fresh TVar per class param; each method scheme gets Scheme([classVarId],[constraint],methodTy)"
    - "InstanceDecl elaborates instance type via elaborateTypeExpr, instantiates class scheme via Map.ofList[classTypeVar,instType], unifies via Bidir.synth+unifyWithContext"

key-files:
  created: []
  modified:
    - src/LangThree/Diagnostic.fs
    - src/LangThree/Elaborate.fs
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs

key-decisions:
  - "TEConstrained in elaborateWithVars/substTypeExprWithMap just recurses into inner type — constraints are handled at Scheme construction, not Type elaboration level"
  - "InstanceDecl does NOT propagate inner classEnv/instEnv from module/namespace recursion back to outer scope — typeclass decls in nested modules don't leak to outer scope"
  - "currentClassEnv/currentInstEnv mutable refs initialized in typeCheckModuleWithPrelude before fold, updated incrementally as TypeClassDecl/InstanceDecl are processed"
  - "Method body type-checking in InstanceDecl uses Bidir.synth with current outer env (not extended env) — methods see module-level bindings but not each other"

patterns-established:
  - "7-tuple fold: typeCheckDecls now (env, cEnv, rEnv, clsEnv, iEnv, mods, warns) — future env additions follow same pattern"
  - "Mutable ref + fold dual threading: fold accumulates pure data, mutable ref provides side-channel for Bidir"

# Metrics
duration: 20min
completed: 2026-03-31
---

# Phase 72 Plan 01: Type Checker Constraint Inference Summary

**TypeClassDecl/InstanceDecl fully processed in type checker with constrained method schemes, duplicate/missing-method validation, and Bidir-accessible mutable refs for ClassEnv/InstanceEnv**

## Performance

- **Duration:** 20 min
- **Started:** 2026-03-31T10:56:00Z
- **Completed:** 2026-03-31T11:16:47Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Type class error diagnostics E0701-E0706 added with full typeErrorToDiagnostic coverage
- TEConstrained elaboration no longer crashes — inner type elaborated, constraints deferred to Scheme level
- Eval.fs TypeClassDecl/InstanceDecl are no-ops returning (env, modEnv) unchanged
- typeCheckDecls threads ClassEnv/InstanceEnv through its 7-tuple fold with all 12 existing arms updated
- TypeClassDecl arm: allocates fresh TVar, builds constrained Scheme per method, populates ClassEnv and TypeEnv
- InstanceDecl arm: validates class, checks duplicates and method set, type-checks bodies via Bidir.synth+unify, populates InstanceEnv
- All 224 unit tests pass unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Diagnostic error kinds + TEConstrained elaboration + Eval no-ops** - `82fda98` (feat)
2. **Task 2: Thread ClassEnv/InstanceEnv through typeCheckDecls + TypeClassDecl/InstanceDecl arms** - `ae04f0c` (feat)

**Plan metadata:** (to be committed with STATE.md update)

## Files Created/Modified
- `src/LangThree/Diagnostic.fs` - Added NoInstance/DuplicateInstance/UnknownTypeClass/MethodTypeMismatch/MissingMethod/ExtraMethod to TypeErrorKind and typeErrorToDiagnostic
- `src/LangThree/Elaborate.fs` - TEConstrained in elaborateWithVars and substTypeExprWithMap: recurse into inner type
- `src/LangThree/Eval.fs` - TypeClassDecl/InstanceDecl: no-ops returning (env, modEnv)
- `src/LangThree/TypeCheck.fs` - Added currentClassEnv/currentInstEnv mutable refs; changed typeCheckDecls to 7-tuple fold threading classEnv/instEnv; implemented TypeClassDecl and InstanceDecl arms; updated typeCheckModuleWithPrelude

## Decisions Made
- TEConstrained in elaborateWithVars/substTypeExprWithMap just recurses into inner type — constraints are handled at Scheme construction level, not Type level. This matches the plan's guidance and avoids complexity in the elaboration pass.
- InstanceDecl method type-checking uses the outer module-level env (not a method-local env). Methods see module-level bindings but not sibling methods. This is correct for the current phase — dictionary passing in Phase 73 will handle dispatch.
- currentClassEnv/currentInstEnv mutable refs initialized before the fold in typeCheckModuleWithPrelude and updated incrementally in TypeClassDecl/InstanceDecl arms, mirroring the Bidir.mutableVars pattern exactly.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- ClassEnv and InstanceEnv are now populated during type checking — Plan 02 (constraint inference) can read currentClassEnv/currentInstEnv from TypeCheck.fs mutable refs in Bidir.synth
- A program with `typeclass Show 'a = | show : 'a -> string` followed by `instance Show int = let show x = to_string x` now parses AND type-checks without error
- Duplicate instance declarations produce DuplicateInstance (E0702) errors
- No blockers for Plan 02

---
*Phase: 72-type-checker-constraint-inference*
*Completed: 2026-03-31*
