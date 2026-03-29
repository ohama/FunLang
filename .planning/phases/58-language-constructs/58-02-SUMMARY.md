---
phase: 58-language-constructs
plan: "02"
subsystem: language-eval-typecheck
tags: [eval, bidir, string-slicing, list-comprehension, for-in, native-collections, type-checking]

dependency-graph:
  requires: [58-01]
  provides: [StringSliceExpr eval+typecheck, ListCompExpr eval+typecheck, ForInExpr extended for native collections, KeyValuePair FieldAccess typecheck]
  affects: [58-03]

tech-stack:
  added: []
  patterns: [explicit-match-over-try-catch, bidir-type-synthesis, eval-slice-syntax]

key-files:
  created: []
  modified:
    - src/LangThree/Eval.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Format.fs

decisions:
  - id: forin-explicit-match-refactor
    choice: "Replace ForInExpr try/catch unification in Bidir.fs with explicit match on resolvedCollTy"
    reason: "try/catch failed for HashSet/Queue/MutableList because those types don't unify with TList or TArray; explicit match on TData variants is cleaner and handles THashtable -> KeyValuePair correctly"
  - id: supporting-files-arms
    choice: "Add StringSliceExpr/ListCompExpr arms to Infer.fs, TypeCheck.fs, Format.fs"
    reason: "These files had FS0025 incomplete match warnings from Plan 01; adding arms eliminates warnings and ensures correct traversal (module ref collection, rewriting, match analysis)"

metrics:
  duration: ~3 minutes
  completed: 2026-03-29
---

# Phase 58 Plan 02: Eval + Bidir for String Slicing, List Comprehension, Native For-In Summary

**One-liner:** StringSliceExpr/ListCompExpr eval+typecheck + ForInExpr extended to HashSet/Queue/MutableList/Hashtable with KeyValuePair FieldAccess

## What Was Built

All four Phase 58 constructs now evaluate and type-check correctly:

1. **StringSliceExpr eval** — `s.[start..stop]` and `s.[start..]` via F# slice syntax `s.[start .. stop]`
2. **ListCompExpr eval** — `[for x in coll -> body]` maps body over list/array/native collections
3. **ForInExpr extended** — HashSet, Queue, MutableList, HashtableValue (yields `RecordValue("KeyValuePair", {Key->ref k; Value->ref v})`)
4. **Bidir arms** — StringSliceExpr, ListCompExpr, refactored ForInExpr, KeyValuePair FieldAccess

## Smoke Test Results

```
s.[1..3]  => "ell"
s.[2..]   => "llo"
[for x in [1;2;3] -> x * 2]  => [2; 4; 6]
```

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing] Added arms to Infer.fs, TypeCheck.fs, Format.fs**

- **Found during:** Task 2 verification — smoke test returned "The match cases were incomplete"
- **Issue:** Plan only mentioned Eval.fs and Bidir.fs, but Infer.fs/TypeCheck.fs/Format.fs had FS0025 warnings from Plan 01 indicating incomplete pattern matches for StringSliceExpr/ListCompExpr. At runtime these caused a MatchFailureException.
- **Fix:** Added stub arms to Infer.fs; traversal arms (collectMatches, collectTryWiths, collectModuleRefs, rewriteModuleAccess) to TypeCheck.fs; format arms to Format.fs
- **Files modified:** src/LangThree/Infer.fs, src/LangThree/TypeCheck.fs, src/LangThree/Format.fs
- **Commits:** 4d030be

## Commits

| Hash    | Description                                                       |
| ------- | ----------------------------------------------------------------- |
| 7ded461 | feat(58-02): Eval arms for StringSliceExpr, ListCompExpr, ForInExpr |
| 4d030be | feat(58-02): Bidir arms + supporting files (Infer, TypeCheck, Format) |

## Next Phase Readiness

- Plan 58-03 can proceed (flt integration tests for all constructs)
- No blockers
