---
phase: 83
plan: "01"
subsystem: builtins
tags: [hashtable, string-keys, dbg, prelude-compatibility]

dependency-graph:
  requires: []
  provides: [hashtable_create_str, hashtable_get_str, hashtable_set_str, hashtable_containsKey_str, hashtable_keys_str, hashtable_remove_str, hashtable_trygetvalue_str, dbg]
  affects: [prelude-compiler-compatibility]

tech-stack:
  added: []
  patterns: [string-specialized-builtins]

key-files:
  created:
    - tests/flt/file/hashtable/hashtable-str-builtins.flt
    - tests/flt/expr/dbg/dbg.flt
  modified:
    - src/FunLang/TypeCheck.fs
    - src/FunLang/Eval.fs

decisions:
  - id: dbg-stderr
    summary: dbg prints to stderr via eprintfn, not stdout
    rationale: stderr avoids interfering with program output; matches standard debug tool behavior

metrics:
  duration: "3 minutes"
  completed: "2026-04-03"
---

# Phase 83 Plan 01: Builtin Compatibility Summary

**One-liner:** Added 7 string-key hashtable builtins + identity dbg builtin for FunLangCompiler Prelude compatibility.

## What Was Built

8 new builtins added to TypeCheck.fs (type signatures) and Eval.fs (runtime):

- `hashtable_create_str : unit -> hashtable<string, 'v>`
- `hashtable_get_str : hashtable<string, 'v> -> string -> 'v`
- `hashtable_set_str : hashtable<string, 'v> -> string -> 'v -> unit`
- `hashtable_containsKey_str : hashtable<string, 'v> -> string -> bool`
- `hashtable_keys_str : hashtable<string, 'v> -> string list`
- `hashtable_remove_str : hashtable<string, 'v> -> string -> unit`
- `hashtable_trygetvalue_str : hashtable<string, 'v> -> string -> (bool * 'v)`
- `dbg : 'a -> 'a` (prints to stderr, returns value unchanged)

The `_str` variants use monomorphic string keys (type var 0 only) rather than the generic `(TVar 0, TVar 1)` of the original hashtable builtins. Runtime implementations share the same underlying `Dictionary<Value, Value>` mechanism.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add type signatures and runtime for all 8 builtins | 00dc90f | TypeCheck.fs, Eval.fs |
| 2 | Add flt integration tests for new builtins | 0d2fd5c | hashtable-str-builtins.flt, dbg/dbg.flt |

## Decisions Made

**dbg prints to stderr:** Used `eprintfn` so debug output goes to stderr and does not contaminate stdout. The flt test only checks stdout, so stderr output from `dbg` is ignored by the test runner (confirmed passing).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Used List.length instead of list_length in test**

- **Found during:** Task 2 verification
- **Issue:** Plan specified `list_length keys` but the actual builtin is accessed as `List.length` (from Prelude module)
- **Fix:** Changed test to use `List.length keys`
- **Files modified:** tests/flt/file/hashtable/hashtable-str-builtins.flt

**2. [Rule 1 - Bug] Renamed test directory from debug/ to dbg/**

- **Found during:** Task 2 git add
- **Issue:** `.gitignore` contains `[Dd]ebug/` pattern which blocked `tests/flt/expr/debug/`
- **Fix:** Created `tests/flt/expr/dbg/` instead
- **Files modified:** tests/flt/expr/dbg/dbg.flt (path change)

## Verification Results

- Build: PASS (0 warnings, 0 errors)
- Unit tests: 244/244 PASS
- Integration tests: All new tests PASS; 1 pre-existing failure (err-occurs-check.flt, tracked in STATE.md)

## Next Phase Readiness

Phase 83 complete. v11.1 milestone (1/1 phases) done.
