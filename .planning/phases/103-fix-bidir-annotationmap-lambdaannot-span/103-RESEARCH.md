# Phase 103: Fix Bidir.fs annotationMap Population for LambdaAnnot Spans - Research

**Researched:** 2026-04-10
**Domain:** F# type checker AST transformation — LetRec binding desugaring vs. annotationMap recording
**Confidence:** HIGH

## Summary

Phase 102 (cbda855) fixed the parser so each `LambdaAnnot` node in a curried annotated function receives its own per-parameter span (`ruleSpan parseState 1 5` for `(x : T)`). This correctly enables distinct annotationMap entries for multi-param annotated functions declared with plain `let`.

However, `let rec` declarations follow a different path. The parser rules for `LetRecDecl` and `LetRec` destructure the outermost `LambdaAnnot` node into the binding tuple `(funcName, firstParam, Some paramTy, body, bindSpan)`. The per-param span of the first parameter is discarded (`_` in the pattern match). The remaining parameters form the `body`, a nested `LambdaAnnot` chain that is correctly type-checked and recorded.

**Confirmed by test**: `let rec f (x : int) (y : string) (z : bool) = 42` produces only 2 TArrow entries (for `y` and `z`), not 3. The entry for `x` (first parameter) is missing because its span is thrown away during AST construction and no `recordTy` call is made for it.

The fix requires storing the first parameter's annotation span in the `LetRec`/`LetRecDecl` binding tuple and using it in `Bidir.fs` and `TypeCheck.fs` to call `recordTy` with the full function type.

**Primary recommendation:** Add a `firstParamAnnotSpan: Span option` field to the LetRec/LetRecDecl binding 5-tuple, promote it from parser, and record `funcTy` at that span in both LetRec expression handler (Bidir.fs) and LetRecDecl declaration handler (TypeCheck.fs).

## Standard Stack

This is an internal compiler fix — no external libraries involved. All files are within the FunLang project.

### Core Files to Modify

| File | Role | Change Needed |
|------|------|---------------|
| `Ast.fs` | Defines LetRec / LetRecDecl binding tuple type | Extend 5-tuple to 6-tuple with `Span option` |
| `Parser.fsy` | Constructs LetRec / LetRecDecl bindings | Capture `paramSp` instead of `_` in LambdaAnnot match |
| `Bidir.fs` | LetRec expression handler — type synthesis | Call `recordTy firstParamSpan funcTy` |
| `TypeCheck.fs` | LetRecDecl declaration handler | Call `TypeAnnotationMap.record` (or `Bidir.recordTy`) for firstParamSpan |

### Files Requiring Pattern Match Updates (no logic change)

| File | Usage | Impact |
|------|-------|--------|
| `Infer.fs` | LetRec expression handler | Pattern `(name, param, paramTyOpt, _, _)` → add 6th element |
| `Eval.fs` | LetRec expression + LetRecDecl | Same pattern update |
| `FixityEnv.fs` | LetRec / LetRecDecl map functions | Same pattern update |
| `Format.fs` | AST pretty-printer | Same pattern update |
| `TypeCheck.fs` (other patterns) | collectMatches, collectTryWiths, etc. | Multiple pattern sites |
| `Parser.fsy` (other sites) | LetRecContinuation, mutual let rec | All LambdaAnnot/Lambda match sites |

## Architecture Patterns

### Current Data Flow (Broken)

```
Parser:
  let rec f (x: T) (y: U) = body
  → desugarMixedParams [Choice2Of2("x",T,span_x); Choice2Of2("y",U,span_y)] body
  → LambdaAnnot("x", T, LambdaAnnot("y", U, body, span_y), span_x)
  → match LambdaAnnot(p, ty, b, _) with   ← span_x DROPPED HERE
    → LetRecDecl([("f", "x", Some T, LambdaAnnot("y",U,body,span_y), bindSpan)], ...)

TypeCheck.fs LetRecDecl handler:
  funcTy = TArrow(T, freshRetTy)          ← correct type, but no span to record it at
  Bidir.synth body = synth LambdaAnnot("y",U,body,span_y)
    → recordTy span_y TArrow(U, bodyTy)  ← span_y recorded correctly
  // span_x never recorded → FunLangCompiler lookup at span_x returns None
```

### Fixed Data Flow

```
Ast.fs:
  LetRec binding = (name * param * TypeExpr option * Span option * Expr * Span)
  //                                                  ^^^^^^^^^^^^
  //                                                  firstParamAnnotSpan

Parser.fsy:
  | LambdaAnnot(p, ty, b, paramSp) → LetRecDecl([("f", p, Some ty, Some paramSp, b, bindSpan)])
  | Lambda(p, b, paramSp)           → LetRecDecl([("f", p, None,    Some paramSp, b, bindSpan)])

TypeCheck.fs LetRecDecl handler:
  for each (name, param, paramTyOpt, firstParamAnnotSpanOpt, body, bindSpan):
    funcTy = TArrow(paramTy, retTy)
    // Record funcTy at firstParamAnnotSpan after finalSubst is computed
    firstParamAnnotSpanOpt |> Option.iter (fun sp ->
        TypeAnnotationMap.record Bidir.annotationMap sp (apply finalSubst funcTy))
    Bidir.synth body   ← records span_y, span_z etc. as before

Bidir.fs LetRec expression handler:
  Same: record funcTy at firstParamAnnotSpanOpt after bodySubst is applied
```

### Pattern: Tuple Extension Strategy

When extending the binding tuple from 5 to 6 elements:

1. All pattern matches `(n, p, pty, body, sp)` become `(n, p, pty, firstSp, body, sp)`
2. Sites that don't use `firstSp` use `_` for the new field
3. Sites that construct the tuple (parser) must supply `Some paramSp` or `None`
4. Only Bidir.fs and TypeCheck.fs consume `firstSp` meaningfully

### When to Record

Record `funcTy` at `firstParamAnnotSpan` **after** `finalSubst` is fully computed (after all body type-checks and unifications). This ensures the recorded type is fully resolved, matching the pattern used for all other `recordTy` calls.

```fsharp
// In TypeCheck.fs LetRecDecl, after computing finalSubst:
List.iter2 (fun (_, _, _, firstSpOpt, _, _) (_, _, funcTy, _) ->
    let resolvedFuncTy = apply finalSubst funcTy
    firstSpOpt |> Option.iter (fun sp ->
        TypeAnnotationMap.record Bidir.annotationMap sp resolvedFuncTy)
) bindings funcTypes

// In Bidir.fs LetRec, after computing bodySubst:
List.iter2 (fun (_, _, _, firstSpOpt, _, _) (_, _, funcTy, _) ->
    let resolvedFuncTy = apply bodySubst funcTy
    firstSpOpt |> Option.iter (fun sp ->
        recordTy sp resolvedFuncTy)
) bindings funcTypes
```

### Anti-Patterns to Avoid

- **Recording at `bindSpan`**: The binding span covers the entire function definition, not the first parameter. FunLangCompiler looks up spans at parameter token positions, not function spans.
- **Recording before finalSubst is applied**: The funcTy initially contains unresolved `TVar`s that would appear as `?0 -> ?1` in the map.
- **Wrapping body in a synthetic LambdaAnnot**: Would require modifying the body before synth and could interfere with type inference context.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Span-to-type recording | Custom dictionary | `TypeAnnotationMap.record` | Already handles unknownSpan filter |
| Substitution application | Manual type walk | `Type.apply s ty` | Already handles all TVar cases |
| Per-param span generation | New span calculation | Already in Parser via `ruleSpan parseState 1 5` | Phase 102 already fixed this |

## Common Pitfalls

### Pitfall 1: Forgetting `LetRecContinuation` sites

**What goes wrong:** Parser.fsy has two separate production groups for `LetRecDecl`: top-level declarations and expression-level `let rec ... in`. Each group has multiple sub-rules (with/without INDENT, with/without return type). The `LetRecContinuation` non-terminal also generates `LambdaAnnot`/`Lambda` destructures.

**How to avoid:** Search Parser.fsy for ALL occurrences of `LambdaAnnot(p, ty, b, _)` in the context of LetRec/LetRecDecl production rules. There are approximately 8 in Parser.fsy (expression-level) and 8 more for declaration-level `LetRecDecl`.

**Warning signs:** Missing test failures for mutual recursion (`let rec f ... and g ...`) cases.

### Pitfall 2: Parser.fs is auto-generated

**What goes wrong:** `Parser.fs` is auto-generated from `Parser.fsy` by `fsyacc`. Modifying `Parser.fsy` requires re-running the code generator to update `Parser.fs`. If only `Parser.fsy` is modified without regenerating `Parser.fs`, the old compiled parser continues to run.

**How to avoid:** After modifying `Parser.fsy`, run the build process which triggers fsyacc. The project's `.fsproj` should include a build step for this. Verify by checking if `Parser.fs` changes after build.

**Confirmed:** `Parser.fs` already reflects the Phase 102 changes (cbda855), so the build pipeline does regenerate it automatically on `dotnet build`.

### Pitfall 3: Applying finalSubst vs bodySubst

**What goes wrong:** In `Bidir.fs LetRec`, `bodySubst` is computed by folding over all binding type-checks. At the point where we want to record `funcTy`, we need to use the accumulated substitution up to and including the current binding. Using `empty` substitution will record unresolved TVars.

**How to avoid:** Record after all bindings are checked, using the final composed substitution (same timing as the existing `recordTy span exprTy` call for the whole LetRec expression).

### Pitfall 4: Tuple index confusion in List.map2

**What goes wrong:** `bindings` has 6 elements after the change; `funcTypes` has 4 elements `(name, param, funcTy, paramTy)`. In `List.map2`, binding destructuring indices must be updated carefully.

**How to avoid:** Use named pattern `(bindName, param, paramTyOpt, firstSpOpt, body, bindSpan)` in all `List.map2` lambdas.

## Code Examples

### Ast.fs Tuple Extension

```fsharp
// BEFORE (current):
| LetRec of bindings: (string * string * TypeExpr option * Expr * Span) list * inExpr: Expr * span: Span
| LetRecDecl of bindings: (string * string * TypeExpr option * Expr * Span) list * Span

// AFTER:
| LetRec of bindings: (string * string * TypeExpr option * Span option * Expr * Span) list * inExpr: Expr * span: Span
| LetRecDecl of bindings: (string * string * TypeExpr option * Span option * Expr * Span) list * Span
//                                                              ^^^^^^^^^^^^
//                                                              firstParamAnnotSpan
```

### Parser.fsy LambdaAnnot Destructure Fix

```fsharp
// BEFORE (current, drops span):
| LambdaAnnot(p, ty, b, _) -> LetRecDecl(($3, p, Some ty, b, ruleSpan parseState 3 6) :: $7, ...)
| Lambda(p, b, _)           -> LetRecDecl(($3, p, None,    b, ruleSpan parseState 3 6) :: $7, ...)

// AFTER:
| LambdaAnnot(p, ty, b, paramSp) -> LetRecDecl(($3, p, Some ty, Some paramSp, b, ruleSpan parseState 3 6) :: $7, ...)
| Lambda(p, b, paramSp)           -> LetRecDecl(($3, p, None,    Some paramSp, b, ruleSpan parseState 3 6) :: $7, ...)
```

### TypeCheck.fs LetRecDecl Recording (after finalSubst)

```fsharp
// Add after computing finalSubst, before building env'':
List.iter2 (fun (_, _, _, firstSpOpt, _, _) (_, _, funcTy, _) ->
    match firstSpOpt with
    | Some sp ->
        let resolvedTy = apply finalSubst funcTy
        TypeAnnotationMap.record Bidir.annotationMap sp resolvedTy
    | None -> ()
) bindings funcTypes
```

### Bidir.fs LetRec Recording (after bodySubst)

```fsharp
// Add after computing bodySubst, before building exprEnv:
List.iter2 (fun (_, _, _, firstSpOpt, _, _) (_, _, funcTy, _) ->
    match firstSpOpt with
    | Some sp -> recordTy sp (apply bodySubst funcTy)
    | None -> ()
) bindings funcTypes
```

### Test TA-09: let rec with annotated params

```fsharp
// New test to add in TypeAnnotationTests.fs
test "TA-09: let rec multi-param annotated records all LambdaAnnot spans" {
    let input = "let rec f (x : int) (y : string) (z : bool) = 42"
    let m = parseModuleWithPositions input
    let result = TypeCheck.typeCheckModule m
    let annots =
        Bidir.annotationMap
        |> Seq.map (fun kv -> (kv.Key, kv.Value))
        |> Map.ofSeq
    match result with
    | Error errs -> failwith (sprintf "Type check error: %A" errs)
    | Ok _ -> ()
    let arrowEntries =
        annots |> Map.toSeq
        |> Seq.filter (fun (_, ty) -> match ty with | Type.TArrow _ -> true | _ -> false)
        |> Seq.toList
    // Must have TArrow at span_x, span_y, span_z (3 entries)
    Expect.isTrue (arrowEntries.Length >= 3)
        (sprintf "Expected >= 3 TArrow for let rec with 3 annotated params, got %d" arrowEntries.Length)
    let spans = arrowEntries |> List.map fst
    let distinctSpans = spans |> List.distinct
    Expect.equal distinctSpans.Length spans.Length
        "All TArrow spans should be distinct"
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| All LambdaAnnot share one span | Per-param spans via `ruleSpan parseState 1 5` | Phase 102 (cbda855) | Parser fixed; Bidir.fs still has gap for let rec first param |
| LetRec binding 5-tuple | 6-tuple with firstParamAnnotSpan | Phase 103 (this phase) | Closes the gap |

## Open Questions

1. **`LetRecContinuation` mutual bindings (`and g (a: T) = ...`)**
   - What we know: `LetRecContinuation` generates additional bindings in the same `LetRecDecl` list. Each binding also destructures a `LambdaAnnot` with `_` for its span.
   - What's unclear: Are these also affected? Likely yes.
   - Recommendation: Fix all `LambdaAnnot(p, ty, b, _)` patterns in LetRec/LetRecDecl rules — both main function and `and`-continuations.

2. **`let rec` expression (not declaration) form**
   - What we know: `let rec f (x: T) (y: U) = e in body` uses `LetRec` (expression) not `LetRecDecl`. The same span-dropping pattern exists in the parser expression rules.
   - What's unclear: Whether FunLangCompiler traverses expression-level let recs.
   - Recommendation: Fix both `LetRec` and `LetRecDecl` parser rules consistently.

3. **Mutual recursion with multiple `and` bindings**
   - What we know: `LetRecContinuation` produces a list that is prepended to the `LetRecDecl` binding list.
   - What's unclear: Parser.fsy structure for `LetRecContinuation` and how it handles `and g (a: T) = ...`.
   - Recommendation: Check all Parser.fsy `LetRecContinuation` rules for `LambdaAnnot` matches.

## Sources

### Primary (HIGH confidence)
- Direct code inspection: `/Users/ohama/vibe-coding/FunLang/src/FunLang/Bidir.fs` lines 480–518
- Direct code inspection: `/Users/ohama/vibe-coding/FunLang/src/FunLang/TypeCheck.fs` lines 913–966
- Direct code inspection: `/Users/ohama/vibe-coding/FunLang/src/FunLang/Ast.fs` lines 88, 365
- Direct code inspection: `/Users/ohama/vibe-coding/FunLang/src/FunLang/Parser.fsy` — all LetRec/LetRecDecl rules
- Empirical test: DEBUG-TA-09 confirmed 2 TArrow entries (not 3) for `let rec f (x:int) (y:string) (z:bool) = 42`
- Phase 102 commit `cbda855` — shows exactly which `_` patterns drop the per-param span

### Secondary (MEDIUM confidence)
- `TypeAnnotationMap.fs` — behavior of `record` and `tryFind` verified, no normalization
- `ExportApi.fs` — confirms FunLangCompiler reads `Bidir.annotationMap` as `Map<Span, Type>` snapshot

## Metadata

**Confidence breakdown:**
- Bug root cause: HIGH — confirmed by DEBUG-TA-09 test (2 arrows, not 3)
- Fix approach (6-tuple): HIGH — minimal change, preserves all existing semantics
- Impact scope (files): HIGH — all 8 files identified, pattern mechanical
- Parser.fsy callsite count: MEDIUM — approximately 16 sites need updating (8 for LetRec expr + 8 for LetRecDecl), exact count from grep

**Research date:** 2026-04-10
**Valid until:** Until Ast.fs LetRec tuple structure changes again (stable)
