---
phase: 05-module-system
plan: 05
subsystem: testing
tags: [module-system, integration-tests, qualified-access, type-checking, evaluation]

requires:
  - phase: 05-04
    provides: "Module-aware evaluation pipeline (evalModuleDecls, module FieldAccess)"
  - phase: 05-03
    provides: "Module-scoped type checking (typeCheckModule with ModuleExports)"
provides:
  - "17 integration tests covering all 5 module system success criteria"
  - "Qualified module access fix in type checker (AST rewriting for Module.member)"
  - "Constructor-as-module-name fix in evaluator FieldAccess dispatch"
  - "Constructor registration in module environments for qualified ADT access"
affects: [06-exception-handling]

tech-stack:
  added: []
  patterns:
    - "AST rewriting for qualified module access before type checking"
    - "Module export merging into type/ctor/rec envs for synth"

key-files:
  created:
    - "tests/LangThree.Tests/ModuleTests.fs"
  modified:
    - "src/LangThree/TypeCheck.fs"
    - "src/LangThree/Eval.fs"
    - "tests/LangThree.Tests/LangThree.Tests.fsproj"

key-decisions:
  - "AST rewriting approach for qualified access instead of threading modules through synth/check (avoids 47 call site changes)"
  - "Constructor nodes (uppercase idents) handled in evaluator FieldAccess via tryGetModuleName helper"
  - "TypeDecl in evalModuleDecls registers constructors as FunctionValue or DataValue for qualified constructor access"

patterns-established:
  - "rewriteModuleAccess: walk expression tree converting Module.member to direct references"
  - "mergeModuleExportsForTypeCheck: temporarily merge module exports into env/ctorEnv/recEnv for type checking"

duration: 12min
completed: 2026-03-09
---

# Phase 5 Plan 5: Module Integration Tests Summary

**17 integration tests covering all module system success criteria with 3 critical bug fixes for qualified access in type checker and evaluator**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-09T08:08:36Z
- **Completed:** 2026-03-09T08:20:36Z
- **Tasks:** 1
- **Files modified:** 4

## Accomplishments
- 17 integration tests covering all 5 module system success criteria (SC1-SC5)
- Fixed type checker: qualified module access (Module.member) now works via AST rewriting
- Fixed evaluator: uppercase identifiers (Constructor nodes) recognized as module names in FieldAccess
- Fixed evaluator: ADT constructors registered in module environments for qualified constructor access (e.g., Shapes.Circle 5)

## Task Commits

1. **Task 1: Create ModuleTests.fs with comprehensive test coverage** - `cb2b9fb` (feat)

## Files Created/Modified
- `tests/LangThree.Tests/ModuleTests.fs` - 17 integration tests for module system (188 lines)
- `tests/LangThree.Tests/LangThree.Tests.fsproj` - Added ModuleTests.fs to compile list
- `src/LangThree/TypeCheck.fs` - Added collectModuleRefs, rewriteModuleAccess, mergeModuleExportsForTypeCheck functions
- `src/LangThree/Eval.fs` - Fixed FieldAccess to handle Constructor nodes; added TypeDecl constructor registration in evalModuleDecls

## Decisions Made
- Used AST rewriting approach for qualified module access instead of threading modules map through all 47 synth/check call sites in Bidir.fs. The rewriting converts `FieldAccess(Constructor("Module", None), "member")` to `Var("member")` or `Constructor("member")` before type checking, while merging module exports into the type environment.
- Constructor nodes (uppercase identifiers) need special handling because the parser creates `Constructor("Name", None)` for uppercase IDENT tokens, not `Var("Name")`. Both evaluator and type checker needed fixes for this.
- For module-qualified constructor application (e.g., `Shapes.Circle 5`), the type checker rewrites to `Constructor("Circle", Some arg)` rather than `App(Constructor("Circle", None), arg)` because synth handles these differently.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Type checker did not handle qualified module access**
- **Found during:** Task 1 (writing tests)
- **Issue:** `Module.member` parsed as `FieldAccess(Constructor("Module", None), "member")` but synth in Bidir.fs only handled record field access, not module access. Root cause: uppercase idents parsed as Constructor nodes, and synth for unknown Constructor returns fresh type var, leading to E0313.
- **Fix:** Added AST rewriting functions (collectModuleRefs, rewriteModuleAccess, mergeModuleExportsForTypeCheck) in TypeCheck.fs to resolve qualified access before calling synth.
- **Files modified:** src/LangThree/TypeCheck.fs
- **Verification:** All 17 module tests pass
- **Committed in:** cb2b9fb

**2. [Rule 1 - Bug] Evaluator FieldAccess only checked Var nodes for module dispatch**
- **Found during:** Task 1 (writing tests)
- **Issue:** `eval` FieldAccess handler checked `| Var(name, _) when Map.containsKey name moduleEnv ->` but module names are uppercase and parsed as `Constructor(name, None, _)`, so module dispatch was never reached.
- **Fix:** Added `tryGetModuleName` helper that matches both `Var` and `Constructor(_, None, _)` against moduleEnv.
- **Files modified:** src/LangThree/Eval.fs
- **Verification:** All qualified access tests pass
- **Committed in:** cb2b9fb

**3. [Rule 1 - Bug] ADT constructors not registered in module environments**
- **Found during:** Task 1 (writing tests)
- **Issue:** `evalModuleDecls` TypeDecl case was a no-op (`| _ -> (env, modEnv)`), so constructor values were never added to env, and the CtorEnv collector found nothing for modules with ADTs.
- **Fix:** Added TypeDecl handling that registers constructors as FunctionValue (for constructors with args) or DataValue (for nullary constructors) in the environment.
- **Files modified:** src/LangThree/Eval.fs
- **Verification:** `Shapes.Circle 5` qualified constructor test passes
- **Committed in:** cb2b9fb

---

**Total deviations:** 3 auto-fixed (3 bugs found during integration testing)
**Impact on plan:** All fixes necessary for correctness. These were latent bugs in plans 05-03 and 05-04 that were never caught because no integration tests existed for qualified module access.

## Issues Encountered
None beyond the deviations above.

## Next Phase Readiness
- Phase 5 (Module System) is complete with all 5 success criteria verified
- 149 total tests pass (132 existing + 17 new module tests)
- No regressions in record or GADT features
- Ready for Phase 6 (Exception Handling)

---
*Phase: 05-module-system*
*Completed: 2026-03-09*
