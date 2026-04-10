---
phase: 103-fix-bidir-annotationmap-lambdaannot-span
verified: 2026-04-10T00:00:00Z
status: passed
score: 6/6 must-haves verified
---

# Phase 103: Fix Bidir annotationMap LambdaAnnot Span Verification Report

**Phase Goal:** Type checker (Bidir.fs) records arrow type in annotationMap using each LambdaAnnot's own per-parameter span, so FunLangCompiler lookups return the correct type instead of None.
**Verified:** 2026-04-10
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | let rec f (x:int) (y:string) (z:bool) = 42 produces TArrow entries for all 3 params in annotationMap | VERIFIED | TA-09 unit test passes; >= 3 distinct TArrow entries confirmed |
| 2 | Mutual rec (and g (a:T) = ...) also records first param TArrow | VERIFIED | TA-09b unit test passes; >= 2 TArrow entries for mutual rec |
| 3 | All flt tests pass | VERIFIED | 727/727 passed |
| 4 | All F# unit tests pass | VERIFIED | 247/247 passed |
| 5 | TA-09 unit test verifies let rec with 3 annotated params produces >= 3 distinct TArrow entries | VERIFIED | Test exists at TypeAnnotationTests.fs:261 and passes |
| 6 | flt regression test for let rec annotated params passes | VERIFIED | tests/flt/file/let/letrec-annot-first-param-map.flt: 1/1 passed |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/FunLang/Ast.fs` | Contains `Span option` in LetRec/LetRecDecl | VERIFIED | Lines 88, 365: 4-tuple position holds `Span option` in both LetRec and LetRecDecl |
| `src/FunLang/Bidir.fs` | Contains `firstSpOpt` (recordTy call) | VERIFIED | Lines 513-516: `List.iter2` over bindings/funcTypes, `recordTy sp (apply bodySubst funcTy)` on `Some sp` |
| `src/FunLang/TypeCheck.fs` | Contains `firstSpOpt` (TypeAnnotationMap.record call) | VERIFIED | Lines 957-962: mirrors Bidir pattern with `TypeAnnotationMap.record Bidir.annotationMap sp resolvedTy` |
| `tests/FunLang.Tests/TypeAnnotationTests.fs` | Contains TA-09 | VERIFIED | Lines 258-300: TA-09 (multi-param) and TA-09b (mutual rec) both substantive and passing |
| `tests/flt/file/let/letrec-annot-first-param-map.flt` | flt regression test | VERIFIED | File exists, runs let rec f with 3 int params, expected output 120 / () |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `LambdaAnnot(p, ty, b, paramSp)` in Parser.fs | `LetRec` binding `Span option` field | `Some paramSp` stored in 4th tuple position | WIRED | Parser.fs lines 1259, 1279, 1301, 1323: LambdaAnnot's own span captured as firstSpOpt |
| `firstSpOpt` in Bidir.fs | `annotationMap` | `recordTy sp funcTy` | WIRED | Lines 513-516: per-parameter span used, not body span |
| `firstSpOpt` in TypeCheck.fs | `Bidir.annotationMap` | `TypeAnnotationMap.record` | WIRED | Lines 957-962: identical pattern for top-level LetRecDecl path |

### Anti-Patterns Found

None detected. No TODO/FIXME/placeholder patterns in modified files. No stub returns.

### Human Verification Required

None. All correctness criteria are verifiable structurally and through automated tests.

## Summary

Phase 103 fully achieved its goal. The fix correctly threads each `LambdaAnnot`'s own `paramSp` through the AST (`Span option` field in `LetRec`/`LetRecDecl` bindings), and Bidir.fs uses that span when calling `recordTy` — replacing the previous behavior of using only the body span. TypeCheck.fs mirrors this for top-level declarations. All 727 flt tests and 247 unit tests pass, including the new TA-09/TA-09b regression tests and the dedicated flt file.

---

_Verified: 2026-04-10_
_Verifier: Claude (gsd-verifier)_
