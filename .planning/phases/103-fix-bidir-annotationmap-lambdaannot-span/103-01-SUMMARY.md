---
phase: 103
plan: "01"
subsystem: type-checker
tags: [annotationMap, LetRec, LetRecDecl, Bidir, TypeCheck, AST, Parser, span]

dependency_graph:
  requires:
    - "102-02: LambdaAnnot unique per-param spans (Phase 102)"
  provides:
    - "annotationMap populated for first param of let rec functions"
    - "LetRec/LetRecDecl 6-tuple binding with Span option as 4th element"
  affects:
    - "FunLangCompiler.lookupType: now returns TArrow for let rec first params"
    - "Any future phase consuming annotationMap for let rec bindings"

tech_stack:
  added: []
  patterns:
    - "6-tuple binding tuple: (name * param * TypeExpr option * Span option * Expr * Span)"
    - "recordTy at firstSpOpt after bodySubst in Bidir.fs LetRec handler"
    - "TypeAnnotationMap.record at firstSpOpt after finalSubst in TypeCheck.fs LetRecDecl handler"

key_files:
  created: []
  modified:
    - src/FunLang/Ast.fs
    - src/FunLang/Parser.fsy
    - src/FunLang/Bidir.fs
    - src/FunLang/TypeCheck.fs
    - src/FunLang/Infer.fs
    - src/FunLang/Eval.fs
    - src/FunLang/FixityEnv.fs
    - src/FunLang/Format.fs
    - tests/FunLang.Tests/TypeAnnotationTests.fs

decisions:
  - id: D1
    summary: "Span option as 4th element (not last) preserves parallelism with paramTyOpt (3rd)"
    rationale: "Groups annotation-related fields together: (name, param, TypeExpr option, Span option, body, bindSpan)"
  - id: D2
    summary: "Record at firstSpOpt in Bidir.fs uses bodySubst (not finalSubst) to match existing recordTy pattern"
    rationale: "Bidir.fs LetRec handler applies bodySubst before generalizing; TypeCheck.fs uses finalSubst"

metrics:
  duration: "~15 minutes"
  completed: "2026-04-10"
  tasks_completed: 2
  tasks_total: 2
---

# Phase 103 Plan 01: Fix annotationMap for LetRec first param Summary

**One-liner:** 6-tuple LetRec/LetRecDecl binding adds `Span option` so Bidir.fs/TypeCheck.fs can record `TArrow` at the first-param span, closing annotationMap gap for let rec functions (Issue #19).

## What Was Built

Extended the LetRec and LetRecDecl binding tuple from 5 to 6 elements by inserting `Span option` as the 4th element (capturing the LambdaAnnot param span from the parser). Added `recordTy`/`TypeAnnotationMap.record` calls in Bidir.fs and TypeCheck.fs so FunLangCompiler lookups for the first parameter of a `let rec` function now return the correct `TArrow` type instead of `None`.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Extend Ast.fs tuple and update all pattern matches | 4820552 | Ast.fs, Infer.fs, Eval.fs, FixityEnv.fs, Format.fs, TypeCheck.fs, Bidir.fs |
| 2 | Update Parser.fsy and add recordTy calls | 36b2798 | Parser.fsy, Bidir.fs, TypeCheck.fs, TypeAnnotationTests.fs |

## Decisions Made

1. **6-tuple element order:** `(name * param * TypeExpr option * Span option * Expr * Span)` — Span option placed after TypeExpr option to group annotation-related fields; Expr (body) and bindSpan remain at positions 5-6.

2. **bodySubst vs finalSubst:** Bidir.fs uses `apply bodySubst funcTy` (consistent with existing `recordTy` usage in that file); TypeCheck.fs uses `apply finalSubst funcTy` (consistent with the `resolvedTy` pattern already in that handler).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] TypeAnnotationTests.fs also had a 5-tuple pattern**

- **Found during:** Task 2 (unit test build)
- **Issue:** `tests/FunLang.Tests/TypeAnnotationTests.fs` line 106 destructured `(_, _, _, e, _)` from LetRec bindings — a 5-tuple pattern
- **Fix:** Updated to `(_, _, _, _, e, _)` (6-tuple)
- **Files modified:** `tests/FunLang.Tests/TypeAnnotationTests.fs`
- **Commit:** 36b2798

## Verification Results

- `dotnet build src/FunLang/FunLang.fsproj -c Release`: 0 errors, 0 warnings
- `dotnet test tests/FunLang.Tests/FunLang.Tests.fsproj -c Release`: 245/245 passed
- `scripts/fslit tests/flt/`: 726/726 passed

## Next Phase Readiness

Phase 103 is complete (Issues #18 and #19 both closed). Ready to resume v15.0 roadmap at Phase 96 (Correctness Foundations).
