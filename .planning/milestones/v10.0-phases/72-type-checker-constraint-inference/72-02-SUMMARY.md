---
phase: 72-type-checker-constraint-inference
plan: 02
subsystem: type-checker
tags: [typeclasses, constraints, pendingConstraints, instantiate, generalize, Bidir, Infer, TypeCheck]

# Dependency graph
requires:
  - phase: 72-01
    provides: currentClassEnv/currentInstEnv, TypeClassDecl/InstanceDecl processing, ClassEnv/InstanceEnv populated

provides:
  - pendingConstraints accumulator in Bidir.fs for constraint tracking across inference
  - Constraint-aware instantiate in Bidir.fs: emits fresh constraints on scheme instantiation
  - Constraint-aware generalize in Bidir.fs: drains pendingConstraints, resolves concrete constraints, defers polymorphic constraints into Scheme
  - applySubstToConstraints helper in Bidir.fs: resolves TVar refs after unification
  - Full constraint inference: show_twice infers Show 'a => 'a -> string
  - Constraint resolution: show_twice 42 resolves Show int; show_twice (fun x -> x) produces NoInstance error

affects:
  - 72-03: integration tests (full constraint pipeline testable end-to-end)
  - 73-dictionary-passing: reads Scheme constraints to construct dict args

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Shadow pattern: Bidir.fs defines instantiate/generalize that shadow Infer.fs versions for all callers opening Bidir after Infer"
    - "Circular dep avoidance: constraint mutable refs live in Bidir.fs (not Infer.fs) since Infer -> Bidir is impossible"
    - "applySubstToConstraints called at every let-boundary before generalize to resolve TVar refs from unification"
    - "Partition-and-resolve: generalize splits pending constraints into deferred (mention polymorphic vars) vs concrete (must resolve)"

key-files:
  created: []
  modified:
    - src/LangThree/Bidir.fs
    - src/LangThree/TypeCheck.fs

key-decisions:
  - "instantiate/generalize defined in Bidir.fs shadowing Infer.fs -- avoids circular dep (Infer cannot reference Bidir since Bidir opens Infer)"
  - "applySubstToConstraints called at all generalize call sites (LetDecl, LetMutDecl, LetPatDecl, LetRecDecl in TypeCheck; Let, LetRec, LetPat in Bidir) to ensure TVar unification results propagate to constraints before resolution"
  - "Concrete constraint resolution: InstanceInfo.InstanceType compared with constraint TypeArg (exact match); polymorphic types are partitioned away first"
  - "uniqueDeferred uses List.distinctBy to remove duplicate constraints from multi-use scenarios"

patterns-established:
  - "Shadow pattern for circular dep: module B opens A; module B defines f that shadows A.f for all callers open B"
  - "applySubstToConstraints before generalize: standard pattern at every let boundary"

# Metrics
duration: 9min
completed: 2026-03-31
---

# Phase 72 Plan 02: Constraint Inference in Bidir.synth Summary

**Core constraint inference works: show_twice infers Show 'a => 'a -> string; show_twice 42 resolves; show_twice (fun x -> x) produces NoInstance error**

## Performance

- **Duration:** 9 min
- **Started:** 2026-03-31T11:16:47Z
- **Completed:** 2026-03-31T11:25:35Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- pendingConstraints, currentClassEnv, currentInstEnv, applySubstToConstraints added to Bidir.fs
- constraint-aware instantiate (shadows Infer.instantiate) defined in Bidir.fs: emits fresh constraints via substitution-applied TypeArgs
- constraint-aware generalize (shadows Infer.generalize) defined in Bidir.fs: drains pendingConstraints, resolves concrete constraints against currentInstEnv (raises NoInstance on failure), defers polymorphic constraints into Scheme
- TypeCheck.fs local currentClassEnv/currentInstEnv removed; TypeCheck now sets Bidir.currentClassEnv/Bidir.currentInstEnv
- applySubstToConstraints called at all 7 generalize call sites (4 in TypeCheck, 3 in Bidir)
- Full pipeline verified: `show_twice : Show 'a => 'a -> string`, `show_twice 42` type-checks, `show_twice (fun x -> x)` gives `No instance of Show for 'q -> 'q`, polymorphic use at `Show int` and `Show bool` resolves separately
- All 224 unit tests pass unchanged

## Task Commits

1. **Task 1: pendingConstraints accumulator + constraint-aware instantiate/generalize** - `2cf9d20` (feat)
2. **Task 2: Apply substitution to pending constraints at all generalize call sites** - `0eec7c7` (feat)

## Files Created/Modified

- `src/LangThree/Bidir.fs` - Added pendingConstraints/currentClassEnv/currentInstEnv mutable refs, applySubstToConstraints helper, constraint-aware instantiate and generalize (shadow Infer versions), applySubstToConstraints calls before Let/LetRec/LetPat generalize
- `src/LangThree/TypeCheck.fs` - Removed local currentClassEnv/currentInstEnv (now in Bidir), added Bidir.pendingConstraints reset, added Bidir.applySubstToConstraints calls before all generalize call sites
- `src/LangThree/Infer.fs` - Kept pure (notes added explaining Bidir shadows them)

## Decisions Made

- Shadow pattern in Bidir.fs: `instantiate` and `generalize` defined in Bidir.fs shadow the pure Infer.fs versions. Any module that `open Bidir` after `open Infer` uses Bidir's constraint-aware versions. This avoids a circular dependency (Infer cannot reference Bidir since Bidir opens Infer).
- Constraint mutable refs live in Bidir.fs (not TypeCheck.fs). TypeCheck.fs sets them via `Bidir.currentInstEnv <- ...`. Infer.fs references `Bidir.currentInstEnv` would create circular dep -- keeping them in Bidir solves this cleanly.
- `applySubstToConstraints` is called at EVERY generalize call site (not just inside generalize) because by the time generalize runs, the pending constraint TypeArgs may still contain TVar IDs that have already been unified. The caller has the substitution; generalize doesn't receive it.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Circular dependency: Infer.fs cannot reference Bidir.fs**

- **Found during:** Task 1 (build failure)
- **Issue:** Plan specified adding instantiate/generalize modifications to Infer.fs, but Bidir.fs opens Infer.fs -- so Infer.fs referencing Bidir.* creates a circular compilation dependency
- **Fix:** Define constraint-aware instantiate/generalize in Bidir.fs instead, shadowing the Infer.fs pure versions via F# open module shadowing
- **Files modified:** Bidir.fs (added functions), Infer.fs (kept pure with notes)
- **Commit:** 2cf9d20

The plan explicitly anticipated this issue and suggested the solution ("Use Bidir.fs module-level refs instead"). Executed as intended.

## Issues Encountered

None beyond the anticipated circular dependency (which was pre-documented in the plan).

## User Setup Required

None.

## Next Phase Readiness

- Full constraint inference pipeline is operational: constrained functions infer constrained Schemes, monomorphic use sites resolve constraints against InstanceEnv
- NoInstance error (E0701) raised with proper ClassName and TypeArg when resolution fails
- Phase 72 Plan 03 (integration tests) can now test the complete typeclass pipeline
- Phase 73 (dictionary passing) can read Scheme constraints to construct dict args

---
*Phase: 72-type-checker-constraint-inference*
*Completed: 2026-03-31*
