---
phase: 56-hashset-queue
plan: 02
subsystem: interpreter
tags: [fsharp, hashset, queue, prelude, flt-tests, builtins, collections]

# Dependency graph
requires:
  - phase: 56-01
    provides: HashSetValue/QueueValue DU cases, Constructor interception, FieldAccess dispatch in Eval.fs/Bidir.fs
provides:
  - Prelude/HashSet.fun with HashSet.create/add/contains/count module API
  - Prelude/Queue.fun with Queue.create/enqueue/dequeue/count module API
  - hashset_create/add/contains/count raw builtins in TypeCheck.fs/Eval.fs
  - queue_create/enqueue/dequeue/count raw builtins in TypeCheck.fs/Eval.fs
  - tests/flt/file/hashset/hashset-basic.flt (COLL-02 integer coverage)
  - tests/flt/file/hashset/hashset-strings.flt (COLL-02 string element coverage)
  - tests/flt/file/queue/queue-basic.flt (COLL-03 FIFO integer coverage)
  - tests/flt/file/queue/queue-error.flt (COLL-03 empty dequeue error coverage)
affects: [57-hashtable, any future phase using HashSet/Queue module APIs]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Prelude modules for method-dispatch types require raw builtins (not direct dot-dispatch) because type checker cannot infer TData type from unresolved TVar in FieldAccess"
    - "Pattern: add hashset_*/queue_* raw builtins to TypeCheck.fs initialTypeEnv and Eval.fs initialBuiltinEnv, then use them in Prelude .fun module wrappers"
    - "Unit parameter pattern () in module let bindings (let f x () = ...) does not parse -- use named parameter u instead"
    - "Prelude .fun files auto-discovered via glob from Prelude/ directory at repo root"

key-files:
  created:
    - Prelude/HashSet.fun
    - Prelude/Queue.fun
    - tests/flt/file/hashset/hashset-basic.flt
    - tests/flt/file/hashset/hashset-strings.flt
    - tests/flt/file/queue/queue-basic.flt
    - tests/flt/file/queue/queue-error.flt
  modified:
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs

decisions:
  - id: HS-PRELUDE-01-builtins
    choice: "Use raw builtins (hashset_*/queue_*) in Prelude modules instead of direct dot-dispatch"
    rationale: "Type checker cannot resolve FieldAccess on TVar parameter; raw builtins have explicit type signatures in initialTypeEnv"
  - id: HS-PRELUDE-02-dequeue-param
    choice: "Queue.dequeue uses named parameter u instead of unit pattern ()"
    rationale: "() pattern in let binding params does not parse in .fun module files; named param achieves same semantics"

metrics:
  duration: ~4 minutes
  completed: 2026-03-29
---

# Phase 56 Plan 02: HashSet and Queue Prelude Modules + flt Tests Summary

**One-liner:** HashSet/Queue Prelude module wrappers via raw builtins plus four flt integration tests covering integers, strings, and empty-dequeue error.

## What Was Built

Added `Prelude/HashSet.fun` and `Prelude/Queue.fun` providing functional-style module APIs (`HashSet.create`, `HashSet.add`, `HashSet.contains`, `HashSet.count` and `Queue.create`, `Queue.enqueue`, `Queue.dequeue`, `Queue.count`). Because the type checker cannot resolve `FieldAccess` on an unresolved type variable, the Prelude modules wrap raw builtins (`hashset_create/add/contains/count` and `queue_create/enqueue/dequeue/count`) added to `TypeCheck.fs` and `Eval.fs` — following the same pattern as `StringBuilder.fun`.

Four flt integration tests verify all Phase 56 requirements:
- `hashset-basic.flt`: Add returns true/false for new/duplicate integers, Contains, Count
- `hashset-strings.flt`: string elements with Add/Contains/Count
- `queue-basic.flt`: FIFO semantics with Enqueue/Dequeue/Count
- `queue-error.flt`: empty Dequeue raises catchable `LangThreeException` via try-with

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create Prelude/HashSet.fun and Prelude/Queue.fun | 6ce1cb6 | Prelude/HashSet.fun, Prelude/Queue.fun, src/LangThree/TypeCheck.fs, src/LangThree/Eval.fs |
| 2 | Write flt integration tests for HashSet and Queue | bd4d092 | tests/flt/file/hashset/hashset-basic.flt, hashset-strings.flt, tests/flt/file/queue/queue-basic.flt, queue-error.flt |

## Verification Results

- Build: `dotnet build` succeeded with 0 errors, 0 warnings
- FsLit hashset/: 2/2 PASS (hashset-basic, hashset-strings)
- FsLit queue/: 2/2 PASS (queue-basic, queue-error)
- HashSet module API: `HashSet.create ()` → HashSetValue, `HashSet.add hs 1` → true, `HashSet.add hs 1` → false (duplicate), `HashSet.contains hs 1` → true, `HashSet.count hs` → 1
- Queue module API: `Queue.create ()` → QueueValue, `Queue.enqueue q 42` → (), `Queue.dequeue q ()` → 42

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Prelude implementation | Raw builtins, not dot-dispatch | TVar FieldAccess not supported in type checker |
| Queue.dequeue param | Named param `u`, not pattern `()` | Unit pattern in module let bindings does not parse |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Initial Prelude modules used dot-dispatch which fails type checking**

- **Found during:** Task 1 verification (smoke test)
- **Issue:** `let add hs v = hs.Add v` fails with "Cannot access field on non-record type 'f'" because `hs` is an unresolved TVar at type-check time
- **Fix:** Added raw builtins `hashset_*` and `queue_*` to TypeCheck.fs/Eval.fs; updated .fun files to use them (same pattern as StringBuilder)
- **Files modified:** src/LangThree/TypeCheck.fs, src/LangThree/Eval.fs, Prelude/HashSet.fun, Prelude/Queue.fun
- **Commit:** 6ce1cb6

**2. [Rule 1 - Bug] Unit pattern `()` in Queue.dequeue parameter does not parse**

- **Found during:** Task 1 (Queue.fun parse error)
- **Issue:** `let dequeue q () = ...` causes parse error in .fun module files
- **Fix:** Changed to `let dequeue q u = queue_dequeue q u` (named parameter)
- **Files modified:** Prelude/Queue.fun
- **Commit:** 6ce1cb6

## Next Phase Readiness

- Phase 56 complete: HashSet and Queue available via both direct method dispatch and functional module API
- COLL-02 (HashSet) and COLL-03 (Queue) requirements fully satisfied
- Phase 57 (Hashtable enhancements) can proceed with the established patterns
