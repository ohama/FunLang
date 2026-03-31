# Phase 66: Expression-Level Mutual Recursion - Research

**Researched:** 2026-03-31
**Domain:** AST extension, parser grammar, type inference, evaluator for `let rec ... and ... in expr`
**Confidence:** HIGH

## Summary

This phase extends the existing single-binding expression-level `LetRec` to support mutual recursion via `and` keyword, mirroring the module-level `LetRecDecl` capability. The codebase already has a proven implementation of mutual recursion at the module level (Phase 18) that can be directly adapted.

The approach is straightforward: extend the `LetRec` AST node to carry a list of bindings (like `LetRecDecl`), then adapt the existing module-level patterns in Parser, TypeCheck/Bidir, Eval, and Format to work at expression level with the addition of an `inExpr` continuation.

**Primary recommendation:** Extend the existing `LetRec` node to carry a bindings list instead of a single binding, reusing the same tuple shape as `LetRecDecl`. This minimizes new code and keeps all `let rec` expression handling in one branch.

## Architecture Patterns

### Pattern 1: AST Node Design

**Decision: Extend LetRec to binding list + inExpr**

Current single-binding `LetRec`:
```fsharp
| LetRec of name: string * param: string * paramType: TypeExpr option * body: Expr * inExpr: Expr * span: Span
```

New mutual-recursion `LetRec` (replaces the above):
```fsharp
| LetRec of bindings: (string * string * TypeExpr option * Expr * Span) list * inExpr: Expr * span: Span
```

This uses the exact same binding tuple shape as `LetRecDecl`:
```fsharp
| LetRecDecl of bindings: (string * string * TypeExpr option * Expr * Span) list * Span
```

The only difference is the addition of `inExpr: Expr` for the `in` continuation.

**Why this over a new node:** Every file that matches on `LetRec` already needs updating. Changing the shape is the same effort as adding a new node, but avoids duplication. Single-binding `let rec f x = ... in expr` becomes `LetRec([(f, x, None, body, span)], inExpr, outerSpan)` -- a list of one.

### Pattern 2: Parser Grammar

The expression-level parser rules (lines 176-206 in Parser.fsy) currently produce single-binding `LetRec`. They need:

1. Add `LetRecContinuation` after the body expression, before `IN`
2. Wrap result as a binding list

The existing `LetRecContinuation` nonterminal (lines 890-919) can be reused directly -- it already parses `and IDENT MixedParamList ...` chains and returns a `(string * string * TypeExpr option * Expr * Span) list`.

Grammar pattern for each variant:
```
// Before (single binding):
LET REC IDENT MixedParamList EQUALS Expr IN SeqExpr

// After (mutual recursion):
LET REC IDENT MixedParamList EQUALS Expr LetRecContinuation IN SeqExpr
```

There are 4 expression-level rule variants (lines 176-206):
- Without return type, without INDENT/DEDENT
- Without return type, with INDENT/DEDENT
- With return type (COLON TypeExpr), without INDENT/DEDENT
- With return type (COLON TypeExpr), with INDENT/DEDENT

Each needs `LetRecContinuation` inserted between the body and `IN`.

**CRITICAL: Token position shifts.** When `LetRecContinuation` is inserted, the `IN` and `SeqExpr` positional references (`$8`, `$10`, `$12`) shift by 1 since the continuation becomes a new symbol. For example:
- `LET REC IDENT MixedParamList EQUALS Expr IN SeqExpr` -- `$8` is SeqExpr
- `LET REC IDENT MixedParamList EQUALS Expr LetRecContinuation IN SeqExpr` -- `$9` is SeqExpr

### Pattern 3: Type Checking (Bidir.fs)

Current Bidir.fs `LetRec` handler (line 302) handles a single binding. The new handler must follow the same pattern as TypeCheck.fs `LetRecDecl` handler (line 852):

1. Create fresh type variables for each function
2. Add ALL functions to env simultaneously with monomorphic types
3. Type-check each body in the extended env
4. Unify each body type with expected return type
5. Generalize all function types
6. Add generalized schemes to env for the `inExpr`
7. Synth the `inExpr` in the extended env

Key code from TypeCheck.fs LetRecDecl (lines 852-903) to adapt:
```fsharp
// 1. Fresh type vars per function
let funcTypes = bindings |> List.map (fun (name, param, paramTyOpt, _, _) ->
    let paramTy = match paramTyOpt with Some te -> elaborateTypeExpr te | None -> freshVar()
    let retTy = freshVar()
    (name, param, TArrow(paramTy, retTy), paramTy))
// 2. All in env simultaneously
let recEnv = funcTypes |> List.fold (fun acc (name, _, funcTy, _) ->
    Map.add name (Scheme([], funcTy)) acc) env
// 3-4. Check each body, unify
// 5-6. Generalize, extend env
// 7. Synth inExpr (NEW -- not in LetRecDecl)
```

### Pattern 4: Evaluator (Eval.fs)

Current Eval.fs `LetRec` handler (line 1359) uses a mutable `envRef` for self-reference. The module-level `LetRecDecl` (line 1632) uses a `sharedEnvRef` for mutual references. The expression-level mutual version combines both:

```fsharp
| LetRec (bindings, inExpr, _) ->
    let sharedEnvRef = ref env
    let funcValues =
        bindings |> List.map (fun (name, param, _, body, _) ->
            let wrapper = BuiltinValue (fun argVal ->
                let callEnv = Map.add param argVal !sharedEnvRef
                eval recEnv moduleEnv callEnv true body)
            (name, wrapper))
    let mutualEnv =
        funcValues |> List.fold (fun acc (name, v) -> Map.add name v acc) env
    sharedEnvRef := mutualEnv
    eval recEnv moduleEnv mutualEnv tailPos inExpr  // <-- the NEW part vs LetRecDecl
```

### Pattern 5: Infer.fs Handler

The Infer.fs `LetRec` handler (line 267) mirrors Bidir.fs. It needs the same extension: loop over bindings, add all to env simultaneously, then infer the `inExpr`. The pattern is identical to Bidir.fs but using `inferWithContext` instead of `synth`.

## Files Requiring Changes

| File | Change | Complexity |
|------|--------|------------|
| `Ast.fs` | Change `LetRec` node shape to bindings list + inExpr | LOW |
| `Parser.fsy` | Add `LetRecContinuation` to 4 expression rules, adjust positional refs | MEDIUM |
| `Bidir.fs` | Rewrite LetRec handler for binding list (adapt from TypeCheck.fs LetRecDecl) | MEDIUM |
| `Infer.fs` | Same as Bidir.fs but for infer mode | MEDIUM |
| `Eval.fs` | Rewrite LetRec handler for binding list (adapt from LetRecDecl eval) | LOW |
| `Format.fs` | Update LetRec pretty-printer for binding list | LOW |
| `TypeCheck.fs` | Update 3 collector functions that destructure LetRec | LOW |
| `Diagnostic.fs` | No changes needed (InLetRecBody context is name-based, still works) | NONE |
| `IndentFilter.fs` | No changes needed (does not reference LetRec or AND_KW) | NONE |

### TypeCheck.fs Collector Functions

Three functions destructure `LetRec` and need updating:

1. **`collectMatches`** (line 350): `LetRec(_, _, _, rhs, body, _)` -- must iterate over all bindings' bodies
2. **`collectTryWiths`** (line 483): Same pattern
3. **`collectModuleRefs`** (line 569): Same pattern
4. **`rewriteModuleAccess`** (line 647): Same pattern -- must rewrite all bindings' bodies

Each changes from destructuring a single `(name, param, _, rhs, body, _)` to iterating `bindings |> List.collect`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Mutual closure linking | Custom recursive Value type | BuiltinValue + shared mutable envRef | Proven pattern in LetRecDecl eval (line 1632) |
| Simultaneous binding type checking | Sequential type checking | Copy TypeCheck.fs LetRecDecl approach (line 852) | Gets monomorphic pre-binding right |
| Parser continuation grammar | New nonterminal | Reuse existing `LetRecContinuation` | Already handles `and` chains with all annotation variants |

## Common Pitfalls

### Pitfall 1: Forgetting positional reference shifts in Parser.fsy
**What goes wrong:** Adding `LetRecContinuation` as a new symbol shifts all subsequent `$N` references by 1. If you forget to update `IN` and `SeqExpr` references, you get parse errors or wrong AST nodes.
**How to avoid:** Carefully audit each of the 4 rule variants. The continuation result becomes the new `$7`/`$9`/`$9`/`$11` depending on the variant.

### Pitfall 2: Not adding ALL bindings to env before type-checking any body
**What goes wrong:** If you type-check binding A's body before adding binding B to the environment, calls from A to B will fail with "unbound variable."
**How to avoid:** Follow the LetRecDecl pattern: first pass creates fresh type vars for ALL functions, second pass adds ALL to env, third pass type-checks bodies.

### Pitfall 3: Generalization timing
**What goes wrong:** Generalizing function types too early (before all bodies are checked) can lead to unsound polymorphism.
**How to avoid:** Only generalize after ALL bodies have been checked and all substitutions composed. The LetRecDecl pattern in TypeCheck.fs does this correctly.

### Pitfall 4: Exhaustive match warnings on old LetRec shape
**What goes wrong:** After changing LetRec's shape, the F# compiler will flag every old match as non-exhaustive. Missing one causes a runtime crash.
**How to avoid:** Build after changing Ast.fs and fix every compiler warning before proceeding to logic changes. The compiler will find all sites for you.

### Pitfall 5: Single-binding backwards compatibility
**What goes wrong:** Existing `let rec f x = ... in expr` (no `and`) must still work with the new binding-list shape.
**How to avoid:** Single-binding becomes `LetRec([(name, param, tyOpt, body, span)], inExpr, outerSpan)`. All handler code iterates over the list, so a single-element list works naturally. Parser rules with empty `LetRecContinuation` produce `[]` which gets prepended to form `[singleBinding]`.

## Code Examples

### New AST Node
```fsharp
// Ast.fs -- replace existing LetRec
| LetRec of bindings: (string * string * TypeExpr option * Expr * Span) list * inExpr: Expr * span: Span
```

### Parser Rule (one variant shown)
```fsharp
// Expression-level let rec with mutual recursion
| LET REC IDENT MixedParamList EQUALS Expr LetRecContinuation IN SeqExpr
    { let span = ruleSpan parseState 1 9
      let lambda = desugarMixedParams $4 $6 span
      match lambda with
      | Lambda(p, b, _) -> LetRec(($3, p, None, b, ruleSpan parseState 3 6) :: $7, $9, span)
      | LambdaAnnot(p, ty, b, _) -> LetRec(($3, p, Some ty, b, ruleSpan parseState 3 6) :: $7, $9, span)
      | _ -> failwith "impossible: MixedParamList must produce at least one param" }
```

### Bidir.fs Synth Handler
```fsharp
| LetRec (bindings, inExpr, span) ->
    // 1. Fresh types for each function
    let funcTypes =
        bindings |> List.map (fun (name, param, paramTyOpt, _, _) ->
            let paramTy = match paramTyOpt with Some te -> elaborateTypeExpr te | None -> freshVar()
            let retTy = freshVar()
            (name, param, TArrow(paramTy, retTy), paramTy))
    // 2. All functions in env simultaneously (monomorphic)
    let recEnv =
        funcTypes |> List.fold (fun acc (name, _, funcTy, _) ->
            Map.add name (Scheme([], funcTy)) acc) env
    // 3. Type-check each body
    let bodySubst =
        List.map2 (fun (_, _, _, body, _) (_, param, funcTy, paramTy) ->
            let bodyEnv = Map.add param (Scheme([], paramTy)) recEnv
            let s, bodyTy = synth ctorEnv recEnv (InLetRecBody (fst3 ...) :: ctx) bodyEnv body
            let expectedRet = match apply s funcTy with TArrow(_, r) -> r | t -> t
            let s2 = unifyWithContext ctx [] span (apply s bodyTy) expectedRet
            compose s2 s
        ) bindings funcTypes
        |> List.fold compose empty
    // 4. Generalize and extend env for inExpr
    let env' = applyEnv bodySubst env
    let exprEnv =
        funcTypes |> List.fold (fun acc (name, _, funcTy, _) ->
            let scheme = generalize env' (apply bodySubst funcTy)
            Map.add name scheme acc) env'
    let s3, exprTy = synth ctorEnv recEnv ctx exprEnv inExpr
    (compose s3 bodySubst, exprTy)
```

### Eval.fs Handler
```fsharp
| LetRec (bindings, inExpr, _) ->
    let sharedEnvRef = ref env
    let funcValues =
        bindings |> List.map (fun (name, param, _, body, _) ->
            let wrapper = BuiltinValue (fun argVal ->
                let callEnv = Map.add param argVal !sharedEnvRef
                eval recEnv moduleEnv callEnv true body)
            (name, wrapper))
    let mutualEnv =
        funcValues |> List.fold (fun acc (name, v) -> Map.add name v acc) env
    sharedEnvRef := mutualEnv
    eval recEnv moduleEnv mutualEnv tailPos inExpr
```

### TypeCheck.fs Collector Update Pattern
```fsharp
// Before:
| LetRec(_, _, _, rhs, body, _) -> collectMatches rhs @ collectMatches body

// After:
| LetRec(bindings, body, _) ->
    (bindings |> List.collect (fun (_, _, _, rhs, _) -> collectMatches rhs)) @ collectMatches body
```

### Format.fs Update
```fsharp
| Ast.LetRec (bindings, inExpr, _) ->
    let bindingsStr =
        bindings
        |> List.map (fun (name, param, paramTyOpt, body, _) ->
            match paramTyOpt with
            | Some ty -> sprintf "%s (%s : %s) = %s" name param (formatTypeExpr ty) (formatAst body)
            | None -> sprintf "%s %s = %s" name param (formatAst body))
        |> String.concat " and "
    sprintf "LetRec (%s) in %s" bindingsStr (formatAst inExpr)
```

## Open Questions

1. **Span extraction from binding names**
   - What we know: `InLetRecBody` context needs a name and span. With multiple bindings, each body gets its own context.
   - What's unclear: The binding tuple already carries a per-binding span -- use that.
   - Recommendation: Use per-binding span from the tuple for `InLetRecBody` context.

## Sources

### Primary (HIGH confidence)
- `src/LangThree/Ast.fs` -- Current LetRec (line 79) and LetRecDecl (line 360) definitions
- `src/LangThree/Parser.fsy` -- Expression rules (lines 176-206), module rules (lines 844-919)
- `src/LangThree/Bidir.fs` -- LetRec synth (line 302)
- `src/LangThree/Infer.fs` -- LetRec infer (line 267)
- `src/LangThree/TypeCheck.fs` -- LetRecDecl type checking (lines 852-903), collectors (lines 350, 483, 569, 647)
- `src/LangThree/Eval.fs` -- LetRec eval (line 1359), LetRecDecl eval (line 1632)
- `src/LangThree/Format.fs` -- LetRec format (line 149), LetRecDecl format (line 325)

## Metadata

**Confidence breakdown:**
- AST design: HIGH -- directly mirrors existing LetRecDecl pattern
- Parser grammar: HIGH -- reuses existing LetRecContinuation nonterminal
- Type checking: HIGH -- adapts proven LetRecDecl approach from TypeCheck.fs
- Evaluation: HIGH -- adapts proven LetRecDecl approach from Eval.fs
- Propagation files: HIGH -- compiler warnings will find all sites

**Research date:** 2026-03-31
**Valid until:** Stable -- no external dependencies, internal codebase patterns only
