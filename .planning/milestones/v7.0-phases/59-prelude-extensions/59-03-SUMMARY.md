---
phase: 59-prelude-extensions
plan: 03
subsystem: prelude-library-tests
tags: [flt, integration-tests, list, array, sort, tryFind, choose, distinctBy, mapi, ofSeq, prelude]

dependency-graph:
  requires: [phases/59-02-prelude-library]
  provides: [flt coverage for PRE-01, PRE-02, PRE-03, PRE-04, PRE-05]
  affects: []

tech-stack:
  added: []
  patterns: [flt integration test pattern, run-binary-first to capture actual output]

key-files:
  created:
    - tests/flt/file/prelude/prelude-list-sort.flt
    - tests/flt/file/prelude/prelude-list-search.flt
    - tests/flt/file/prelude/prelude-list-transform.flt
    - tests/flt/file/prelude/prelude-list-ofseq.flt
    - tests/flt/file/prelude/prelude-array-sort-ofseq.flt
  modified:
    - src/LangThree/TypeCheck.fs

decisions:
  - list_of_seq and array_of_seq type schemes changed from TList/TArray input to TVar 0 so HashSet/Queue/MutableList can be passed
  - List.mapi test uses curried lambda (fun i -> fun x -> ...) not multi-arg (fun i x -> ...) since multi-arg lambda parse fails
  - List.distinctBy test uses % 2 not mod 2 since mod is not a keyword in LangThree
  - HashSet order is non-deterministic so test uses List.length not content comparison

metrics:
  duration: "~10 minutes"
  completed: 2026-03-29
---

# Phase 59 Plan 03: Prelude Integration Tests Summary

Five flt integration tests covering all Phase 59 Prelude extension requirements (PRE-01 through PRE-05). One infrastructure fix in TypeCheck.fs to allow passing native collections to `List.ofSeq` and `Array.ofSeq`.

## What Was Built

### New flt Test Files

| File | Coverage | Functions Tested |
|------|----------|-----------------|
| `prelude-list-sort.flt` | PRE-01 | `List.sort`, `List.sortBy` |
| `prelude-list-search.flt` | PRE-02 | `List.exists`, `List.tryFind`, `List.choose`, `List.distinctBy` |
| `prelude-list-transform.flt` | PRE-03 | `List.mapi`, `List.item`, `List.isEmpty`, `List.head`, `List.tail` |
| `prelude-list-ofseq.flt` | PRE-04 | `List.ofSeq` from HashSet, Queue, MutableList |
| `prelude-array-sort-ofseq.flt` | PRE-05 | `Array.sort` (in-place), `Array.ofSeq` from Queue |

### Test Highlights

**PRE-01 sort:** `List.sort [3;1;2]` → `[1; 2; 3]`, `List.sortBy (fun x -> 0 - x) [1;2;3]` → `[3; 2; 1]`

**PRE-02 search:** `List.tryFind (fun x -> x > 2) [1;2;3]` → `Some 3`, `List.tryFind (fun x -> x > 10) [1;2;3]` → `None`

**PRE-03 transform:** `List.mapi (fun i -> fun x -> i + x) [10;20;30]` → `[10; 21; 32]`

**PRE-04 ofSeq:** Tests `List.length` of result from HashSet (3), Queue (2), MutableList (2) — avoids HashSet order non-determinism

**PRE-05 array:** `Array.sort` mutates array in place; `Array.ofSeq q` from Queue of 3 elements → length 3

## Infrastructure Fix

### TypeCheck.fs: list_of_seq and array_of_seq Type Schemes

**Found during:** Task 1 — type error "Type mismatch: expected 'i list but got HashSet"

**Root cause:** The Plan 02 TypeCheck.fs fix typed `list_of_seq` as `TArrow(TList (TVar 0), TList (TVar 0))` — i.e., takes a list and returns a list. This worked for list inputs but rejected HashSet/Queue/MutableList because the type checker unified the input with `TList`.

**Fix:** Changed type scheme input from `TList (TVar 0)` to `TVar 0` (unconstrained), giving `list_of_seq : 'a -> 'b list`. The runtime implementation in Eval.fs already handles all collection types; the type scheme change just relaxes the type checker constraint.

Same fix applied to `array_of_seq`: `TList (TVar 0)` → `TVar 0`.

**Files modified:** `src/LangThree/TypeCheck.fs` lines 204-209

**Commit:** 2899f72

## Language Quirks Discovered

1. **Multi-arg lambda `(fun i x -> ...)` fails to parse** — LangThree lambdas must be curried: `(fun i -> fun x -> ...)`. The plan used the multi-arg form; corrected to curried form in the test.

2. **`mod` is not a keyword** — LangThree uses `%` for integer modulo. The plan snippet used `x mod 2`; corrected to `x % 2`.

3. **`Some 3` not `Some(3)`** — The plan description said Option values print as `"Some(2)"` but actual output is `Some 3` (no parens). The flt test uses the verified actual output.

## Verification Results

```
prelude-list-sort.flt       PASS
prelude-list-search.flt     PASS
prelude-list-transform.flt  PASS
prelude-list-ofseq.flt      PASS
prelude-array-sort-ofseq.flt PASS

Full suite before: 556/614
Full suite after:  561/619
Change: +5 new tests, 0 regressions
```

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking Issue] list_of_seq / array_of_seq type schemes too restrictive**

- **Found during:** Task 1 (prelude-list-ofseq.flt)
- **Issue:** Type checker rejected `List.ofSeq hs` where `hs : HashSet` because `list_of_seq` was typed as `'a list -> 'b list`
- **Fix:** Changed both `list_of_seq` and `array_of_seq` input types from `TList (TVar 0)` to `TVar 0` in `TypeCheck.fs`
- **Files modified:** `src/LangThree/TypeCheck.fs`
- **Commit:** 2899f72

**2. [Rule 1 - Bug] Multi-arg lambda syntax unsupported in plan snippet**

- **Found during:** Task 1 (prelude-list-transform.flt)
- **Issue:** `(fun i x -> i + x)` is a parse error in LangThree
- **Fix:** Changed to curried form `(fun i -> fun x -> i + x)` in test
- **Files modified:** `tests/flt/file/prelude/prelude-list-transform.flt`
- **Commit:** 2899f72

**3. [Rule 1 - Bug] `mod` operator not in LangThree**

- **Found during:** Task 1 (prelude-list-search.flt)
- **Issue:** `x mod 2` is "Unbound variable: mod" error
- **Fix:** Changed to `x % 2` in test
- **Files modified:** `tests/flt/file/prelude/prelude-list-search.flt`
- **Commit:** 2899f72

## Next Phase Readiness

Phase 59 is now complete. All three plans done:
- 59-01: Builtin functions in Eval.fs
- 59-02: Prelude .fun implementations (List.fun, Array.fun)
- 59-03: flt integration tests (this plan)

All 14 new Prelude functions (12 List + 2 Array) are covered by passing tests.
