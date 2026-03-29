---
phase: 59-prelude-extensions
plan: 02
subsystem: prelude-library
tags: [list, array, sort, tryFind, choose, distinctBy, mapi, option, prelude]

dependency-graph:
  requires: [phases/59-01-prelude-builtins]
  provides: [List.sort, List.sortBy, List.exists, List.tryFind, List.choose, List.distinctBy, List.mapi, List.item, List.isEmpty, List.head, List.tail, List.ofSeq, Array.sort, Array.ofSeq]
  affects: [phases/59-03-tests]

tech-stack:
  added: []
  patterns: [insertion-sort in LangThree, curried-helper pattern for mapi/distinctBy, A_-prefix for Prelude loading order control]

key-files:
  created: []
  modified:
    - Prelude/A_Option.fun (renamed from Option.fun)
    - Prelude/List.fun
    - Prelude/Array.fun
    - src/LangThree/TypeCheck.fs

decisions:
  - Renamed Option.fun to A_Option.fun so Some/None constructors are available when List.fun is type-checked (L < O alphabetical loading order)
  - Added list_sort_by, list_of_seq, array_sort, array_of_seq type schemes to TypeCheck.fs (builtins were added to Eval.fs in Plan 01 but TypeCheck.fs was missed)
  - No blank lines inside .fun module bodies (NEWLINE(0) causes premature DEDENT out of module in IndentFilter)
  - Helper functions use underscore prefix (_insert, _mapi_helper, _distinctBy_helper) for visual distinction

metrics:
  duration: "~16 minutes"
  completed: 2026-03-29
---

# Phase 59 Plan 02: Prelude Library Extensions Summary

Twelve new `List.*` functions and two new `Array.*` functions added to the LangThree standard library, with two infrastructure fixes needed to make them work.

## What Was Built

### Prelude/List.fun (12 new functions)

| Function | Type | Implementation |
|----------|------|----------------|
| `head` | `'a list -> 'a` | Alias for `hd` |
| `tail` | `'a list -> 'a list` | Alias for `tl` |
| `exists` | `('a -> bool) -> 'a list -> bool` | Alias for `any` |
| `item` | `int -> 'a list -> 'a` | Alias for `nth` |
| `isEmpty` | `'a list -> bool` | Single-line match |
| `sort` | `'a list -> 'a list` | Insertion sort (pure LangThree) |
| `sortBy` | `('a -> 'b) -> 'a list -> 'a list` | Wraps `list_sort_by` builtin |
| `mapi` | `(int -> 'a -> 'b) -> 'a list -> 'b list` | Recursive with index helper |
| `tryFind` | `('a -> bool) -> 'a list -> 'a option` | Returns `Some`/`None` |
| `choose` | `('a -> 'b option) -> 'a list -> 'b list` | Filter + map combined |
| `distinctBy` | `('a -> 'b) -> 'a list -> 'a list` | O(n^2), correct for small lists |
| `ofSeq` | `seq<'a> -> 'a list` | Wraps `list_of_seq` builtin |

### Prelude/Array.fun (2 new functions)

| Function | Type | Implementation |
|----------|------|----------------|
| `sort` | `'a array -> unit` | Wraps `array_sort` (in-place) |
| `ofSeq` | `seq<'a> -> 'a array` | Wraps `array_of_seq` builtin |

## Infrastructure Fixes (Deviations)

### Fix 1: Prelude Loading Order (Option.fun rename)

**Found during:** Task 1 — type error "Unbound constructor: Some" in List.fun
**Root cause:** Prelude .fun files load alphabetically. `L` < `O`, so `List.fun` loads BEFORE `Option.fun`. The new `tryFind` and `choose` functions use `Some`/`None` constructors, which were not yet in scope.
**Fix:** Renamed `Prelude/Option.fun` → `Prelude/A_Option.fun`. `A_` sorts before `Ar...` (because `_` ASCII 95 < `r` ASCII 114), so it now loads FIRST among all Prelude files.
**Files modified:** `Prelude/A_Option.fun` (renamed from `Prelude/Option.fun`)
**Commits:** 480e07d

### Fix 2: TypeCheck.fs Missing Builtin Type Schemes

**Found during:** Task 1 — "Unbound variable: array_sort" type error in Array.fun
**Root cause:** Plan 01 added `list_sort_by`, `list_of_seq`, `array_sort`, `array_of_seq` to `Eval.fs` (runtime) but the research stated "No changes required to TypeCheck.fs". This was incorrect — the type checker needs to know about builtins too.
**Fix:** Added 4 type schemes to `TypeCheck.fs` `initialTypeEnv`.
**Files modified:** `src/LangThree/TypeCheck.fs`
**Commits:** 480e07d

### Fix 3: No Blank Lines Inside .fun Module Bodies

**Found during:** Task 1 debugging
**Root cause:** The `IndentFilter` uses `NEWLINE(col)` tokens to track indentation. A blank line (empty line) emits `NEWLINE(0)` which causes a DEDENT from module body (col 4) back to top level (col 0), prematurely closing the module block.
**Fix:** Removed all blank lines between function definitions inside `module List = ...`. Single-line functions and multi-line functions are listed consecutively.
**Files modified:** `Prelude/List.fun`
**Commits:** 480e07d

## Decisions Made

1. **Option.fun rename** — `A_Option.fun` prefix ensures loading before `Array.fun` and all other Prelude files. This is the documented approach for controlling Prelude dependency order (see tutorial/09-prelude.md).

2. **TypeCheck.fs was incomplete from Plan 01** — The plan said "no TypeCheck.fs changes needed" but builtins referenced by .fun files must have type schemes registered. Added minimal schemes using existing patterns.

3. **Blank line prohibition in .fun files** — IndentFilter behavior makes blank lines inside module bodies cause parse errors. All Prelude .fun files implicitly follow this convention; our new functions follow suit.

4. **Insertion sort for List.sort** — O(n^2) but correct and simple. Consistent with the research recommendation. No new AST nodes or builtins needed.

## Verification Results

```
List.sort [3;1;2]              → [1; 2; 3]   OK
List.sortBy (fun x -> 0-x) [1;2;3] → [3; 2; 1]   OK
List.tryFind (fun x -> x > 2) [1;2;3] → Some 3   OK
List.exists (fun x -> x > 2) [1;2;3] → true   OK
List.isEmpty []                → true   OK
Array.sort [3;1;2] (in-place)  → [1; 2; 3]   OK
All 224 unit tests: PASS
```

## Deviations from Plan

Three auto-fixed deviations (infrastructure issues not in plan):

1. **[Rule 3 - Blocking Issue] Prelude loading order** — Option.fun renamed to A_Option.fun
2. **[Rule 3 - Blocking Issue] TypeCheck.fs missing builtin schemes** — 4 new entries added
3. **[Rule 1 - Bug] Blank lines in .fun files cause parse errors** — removed all blank lines from module body

## Next Phase Readiness

Plan 59-03 (tests) can now test all 14 new functions. The complete Phase 59 API surface is now exposed.
