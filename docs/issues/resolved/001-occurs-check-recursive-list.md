# Issue #001: OccursCheck Error in Recursive List Function Type Inference

**Date**: 2026-01-10
**Status**: Resolved
**Component**: TypeInfer.fs (inferMatch)

## Problem

Recursive list functions like `len` failed type inference with OccursCheck error:

```fsharp
// let rec len = fun xs -> match xs with | [] -> 0 | _ :: rest -> 1 + len rest
let expr =
    ELetRec ("len",
        ELambda ("xs",
            EMatch (EVariable "xs", [
                (PList [], None, ELiteral (LInt 0));
                (PCons (PWildcard, PVariable "rest"), None,
                 EBinaryOp (Add, ELiteral (LInt 1),
                            EApply (EVariable "len", EVariable "rest")))
            ])),
        EVariable "len")
```

**Error message:**
```
OccursCheck (8, TList (TVar 8))
Message = "Infinite type: type variable occurs in its own definition"
```

## Root Cause

`inferMatch` processed all pattern cases independently using `List.map ... |> Result.sequence`, then composed substitutions at the end.

This caused issues because:
1. Case 1 (`[]`) unifies scrutinee with `TList α`, creating `{xs → TList α}`
2. Case 2 (`_ :: rest`) unifies scrutinee with `TList β`, creating `{xs → TList β}`
3. When composing substitutions, `α` and `β` were never unified
4. This led to inconsistent type constraints causing the OccursCheck failure

## Solution

Rewrote `inferMatch` to thread substitutions sequentially through case processing:

```fsharp
// Before: parallel processing
let! caseResults =
    cases
    |> List.map (fun (pattern, guard, body) -> ...)
    |> Result.sequence

// After: sequential folding with accumulated substitution
let! (finalSubst, reversedBodyTypes) =
    cases |> List.fold (fun accResult case ->
        result {
            let! acc = accResult
            return! processCase acc case
        }) (Ok (s0, []))
```

Each case now uses the accumulated substitution when unifying pattern type with scrutinee:
```fsharp
let! s1 = unify (TypeHelpers.apply accSubst τScrutinee) τPattern
```

## Files Changed

- `src/FunLang/TypeInfer.fs`: Lines 293-364 (inferMatch function)

## Verification

Test `"infer recursive list function"` now passes, correctly inferring `TFun (TList α, TInt)`.
