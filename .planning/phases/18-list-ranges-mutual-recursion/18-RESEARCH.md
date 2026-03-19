# Phase 18: List Ranges & Mutual Recursive Functions - Research

**Researched:** 2026-03-19
**Domain:** LALR(1) parser extension, AST design, type inference for mutual recursion
**Confidence:** HIGH

## Summary

This phase adds two independent features to LangThree: (1) list range syntax `[1..5]` and `[1..2..10]` for generating integer lists, and (2) mutual recursive function declarations at module level using `let rec f x = ... and g y = ...` syntax.

For list ranges, a new `DOTDOT` token (`..`) must be added to the lexer. The key challenge is LALR(1) ambiguity: since `DOT` (`.`) already exists for field access, the lexer must match `..` before `.` (longer match wins in FsLex). The parser needs new productions inside the `Atom` rule for `LBRACKET Expr DOTDOT Expr RBRACKET` and `LBRACKET Expr DOTDOT Expr DOTDOT Expr RBRACKET`.

For mutual recursion, `AND_KW` token already exists (used for mutually recursive type declarations). The `Decl` rule needs new productions for `LET REC IDENT ParamList EQUALS Expr` with `AND_KW`-separated continuations. Type inference requires a simultaneous binding approach: all functions in the group get fresh type variables, are added to the environment, then all bodies are checked against their types. Evaluation requires creating closures that share a mutable reference to the common environment.

**Primary recommendation:** Implement ranges first (simpler, self-contained), then mutual recursion. Keep `Range` as a single AST node with optional step. For mutual recursion, add a `LetRecDecl` variant to `Decl` that carries a list of (name, params, body) bindings.

## Standard Stack

No new libraries needed. All features are implemented within the existing FsLexYacc toolchain.

### Core
| Component | Current | Purpose | Notes |
|-----------|---------|---------|-------|
| FsLexYacc | existing | Parser/Lexer generator | Already in use |
| Ast.fs | existing | AST definitions | Add Range, LetRecDecl |
| Bidir.fs | existing | Type inference | Add mutual rec group handling |

## Architecture Patterns

### Recommended AST Design

```fsharp
// In Expr:
| Range of start: Expr * stop: Expr * step: Expr option * span: Span
// [1..5] -> Range(1, 5, None, span)
// [1..2..10] -> Range(1, 10, Some 2, span)
// NOTE: step is the SECOND element in syntax [start..step..stop]
// but stored as (start, stop, step option) in AST

// In Decl:
| LetRecDecl of bindings: (string * string list * Expr) list * Span
// let rec f x = ... and g y = ... ->
// LetRecDecl([("f", ["x"], body1); ("g", ["y"], body2)], span)
```

### Pattern 1: DOTDOT Token in Lexer

**What:** Add `..` as a two-character token, BEFORE the single `.` rule.
**When to use:** FsLex longest-match semantics handle this naturally.

```fsl
// In Lexer.fsl - MUST come before '.' rule (line ~121)
| ".."          { DOTDOT }
| '.'           { DOT }
```

**Critical detail:** FsLex uses longest match, so `..` will match before `.` when two dots appear consecutively. This is the same pattern as `::` matching before `:`. No ambiguity.

### Pattern 2: Range Parsing in Grammar

**What:** Parse `[expr..expr]` and `[expr..expr..expr]` inside the Atom rule.

```fsy
// In Parser.fsy Atom rule, alongside existing list literals:
// Range: [start..stop]
| LBRACKET Expr DOTDOT Expr RBRACKET
    { Range($2, $4, None, ruleSpan parseState 1 5) }
// Step range: [start..step..stop]
| LBRACKET Expr DOTDOT Expr DOTDOT Expr RBRACKET
    { Range($2, $6, Some $4, ruleSpan parseState 1 7) }
```

**LALR(1) analysis:** The existing list rules are:
- `LBRACKET RBRACKET` (empty list)
- `LBRACKET Expr RBRACKET` (single element)
- `LBRACKET Expr COMMA ExprList RBRACKET` (multi element)

Adding range rules creates a potential conflict at the `LBRACKET Expr` prefix. After seeing `LBRACKET Expr`, the parser sees either `RBRACKET` (single list), `COMMA` (multi list), or `DOTDOT` (range). Since these are different tokens, there is NO shift-reduce or reduce-reduce conflict. The parser decides based on the next token (lookahead 1).

### Pattern 3: Mutual Recursive Decl Parsing

**What:** Parse `let rec f x = body and g y = body` at module level.

```fsy
// New Decl productions:
// Single recursive function at module level
| LET REC IDENT ParamList EQUALS Expr
    { LetRecDecl([($3, $4, $6)], ruleSpan parseState 1 6) }
| LET REC IDENT ParamList EQUALS INDENT Expr DEDENT
    { LetRecDecl([($3, $4, $7)], ruleSpan parseState 1 8) }
// With AND_KW continuation
| LET REC IDENT ParamList EQUALS Expr LetRecContinuation
    { LetRecDecl(($3, $4, $6) :: $7, ruleSpan parseState 1 7) }
| LET REC IDENT ParamList EQUALS INDENT Expr DEDENT LetRecContinuation
    { LetRecDecl(($3, $4, $7) :: $9, ruleSpan parseState 1 9) }

// Continuation for mutual recursion
LetRecContinuation:
    | AND_KW IDENT ParamList EQUALS Expr
        { [($2, $3, $5)] }
    | AND_KW IDENT ParamList EQUALS Expr LetRecContinuation
        { ($2, $3, $5) :: $6 }
    | AND_KW IDENT ParamList EQUALS INDENT Expr DEDENT
        { [($2, $3, $6)] }
    | AND_KW IDENT ParamList EQUALS INDENT Expr DEDENT LetRecContinuation
        { ($2, $3, $6) :: $8 }
```

**Note:** This mirrors the existing `TypeDeclContinuation` pattern for mutually recursive type declarations.

**Desugar params to lambdas:** Each binding's `ParamList` should be desugared into nested `Lambda` nodes, exactly like the existing `Decl` for `LET IDENT ParamList EQUALS Expr`.

### Pattern 4: Mutual Recursion Type Inference

**What:** Simultaneously type all functions in a `let rec ... and ...` group.

```fsharp
// In TypeCheck.typeCheckDecls, handle LetRecDecl:
| LetRecDecl(bindings, _) ->
    // 1. Create fresh type variables for each function
    let freshTypes = bindings |> List.map (fun (name, _, _) -> (name, freshVar()))

    // 2. Add all functions to env with fresh types (monomorphic during checking)
    let recEnv =
        freshTypes
        |> List.fold (fun acc (name, ty) -> Map.add name (Scheme([], ty)) acc) env

    // 3. Type-check each body in the extended environment
    let substitutions =
        List.map2 (fun (name, _, body) (_, funcTy) ->
            let s, bodyTy = Bidir.synth ctorEnv recEnv ctx recEnv body
            let s' = unify funcTy (apply s bodyTy)
            compose s' s
        ) bindings freshTypes

    // 4. Compose all substitutions
    let finalSubst = List.fold compose empty substitutions

    // 5. Generalize all function types and add to env
    let env' = applyEnv finalSubst env
    let env'' =
        freshTypes
        |> List.fold (fun acc (name, ty) ->
            let resolvedTy = apply finalSubst ty
            let scheme = generalize env' resolvedTy
            Map.add name scheme acc) env'
```

**Key insight:** All functions are monomorphic within the group (no polymorphic recursion). Only after all bodies are checked are types generalized. This is the standard ML/OCaml/F# approach.

### Pattern 5: Mutual Recursion Evaluation

**What:** Create closures that can reference each other.

```fsharp
// In Eval.evalModuleDecls, handle LetRecDecl:
| LetRecDecl(bindings, _) ->
    // Strategy: use mutable environment reference
    // 1. Create placeholder closures with empty env
    let mutable recEnv = env

    // 2. Create all closures pointing to recEnv
    let closures =
        bindings |> List.map (fun (name, params, body) ->
            let lambdaBody = desugarParams params body
            (name, FunctionValue(firstParam, restBody, recEnv)))

    // 3. Add all closures to the environment
    for (name, closure) in closures do
        recEnv <- Map.add name closure recEnv

    // 4. Re-create closures with the complete environment
    let finalClosures =
        bindings |> List.map (fun (name, params, body) ->
            let lambdaBody = desugarParams params body
            (name, FunctionValue(firstParam, restBody, recEnv)))

    let env' =
        finalClosures |> List.fold (fun acc (name, v) -> Map.add name v acc) env
    (env', modEnv)
```

**Alternative (simpler) approach:** Since `applyFunc` in Eval.fs already augments the closure with the function name at call time (line 343-344), we can leverage this pattern. For mutual recursion, we need ALL function names in each closure. The simplest approach: build the complete env with all functions first, then create closures using that env.

```fsharp
| LetRecDecl(bindings, _) ->
    // Build lambda bodies from params
    let lambdaBodies =
        bindings |> List.map (fun (name, params, body) ->
            let lambda = List.foldBack (fun p b -> Lambda(p, b, unknownSpan)) params body
            match lambda with
            | Lambda(param, innerBody, _) -> (name, param, innerBody)
            | _ -> failwith "impossible: empty param list")

    // Create initial closures (will be patched)
    let mutable mutualEnv = env
    for (name, param, body) in lambdaBodies do
        mutualEnv <- Map.add name (FunctionValue(param, body, env)) mutualEnv

    // Re-create closures with mutual environment
    let finalEnv =
        lambdaBodies |> List.fold (fun acc (name, param, body) ->
            Map.add name (FunctionValue(param, body, mutualEnv)) acc) env
    (finalEnv, modEnv)
```

### Anti-Patterns to Avoid

- **Don't make Range lazy/infinite:** Keep ranges as eager list generation. Lazy ranges would require a fundamentally different value type. The requirement says `[1..5]` produces `[1, 2, 3, 4, 5]`.
- **Don't allow non-integer ranges:** Start with integer-only ranges. Float ranges have precision issues. String/char ranges add complexity.
- **Don't use polymorphic recursion for mutual rec:** Standard ML-family languages don't support polymorphic recursion without explicit annotations. Keep mutual rec functions monomorphic within the group.
- **Don't forget to desugar multi-param functions:** `let rec f x y = ...` must become `LetRecDecl([("f", ["x"; "y"], body)])` where params are desugared to nested lambdas.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Param desugaring | Custom lambda nesting | Reuse existing `List.foldBack Lambda` pattern | Already used in Decl rule (Parser.fsy line 419) |
| AND_KW token | New keyword | Existing `AND_KW` token | Already lexed from "and" keyword |
| TypeDeclContinuation pattern | New continuation style | Mirror existing pattern | Same `and` syntax, same parsing pattern |

## Common Pitfalls

### Pitfall 1: DOTDOT Lexer Ordering
**What goes wrong:** If `'.'` rule matches before `".."`, the lexer produces DOT DOT instead of DOTDOT.
**Why it happens:** FsLex uses first-match when lengths are equal, but `..` is two chars vs `.` one char, so longest-match wins. However, if you accidentally write `'.' '.'` as a pattern, it won't work.
**How to avoid:** Add `| ".."  { DOTDOT }` as a STRING pattern (not two char patterns) BEFORE the `| '.'  { DOT }` line.
**Warning signs:** `[1..5]` parses as `[1 . . 5]` and fails with parse error.

### Pitfall 2: Step Range AST Confusion
**What goes wrong:** Syntax is `[start..step..stop]` but you store it as `(start, step, stop)` while the natural reading is `(start, stop, step)`.
**Why it happens:** The step is in the MIDDLE of the syntax but logically a modifier of the range.
**How to avoid:** Document clearly. The parser rule `LBRACKET Expr DOTDOT Expr DOTDOT Expr RBRACKET` gives $2=start, $4=step, $6=stop. Store as `Range(start=$2, stop=$6, step=Some $4, span)`.

### Pitfall 3: Mutual Recursion Closure Environment
**What goes wrong:** Closures capture the environment at creation time. If functions are added to env one-by-one, earlier functions don't see later ones.
**Why it happens:** Immutable Map-based environments mean each closure captures a snapshot.
**How to avoid:** Build the complete environment with ALL functions first, THEN create the final closures using that complete environment. Two-pass approach.
**Warning signs:** `f` can call `g` but `g` cannot call `f`, or vice versa.

### Pitfall 4: Mutual Recursion Type Generalization Timing
**What goes wrong:** If you generalize function types too early (before all bodies are checked), type variables get frozen prematurely.
**Why it happens:** Standard let-polymorphism generalizes at the let boundary. For mutual rec, the "boundary" is after ALL bodies.
**How to avoid:** Keep all functions monomorphic (Scheme([], ty)) during body checking. Only generalize after all bodies pass.

### Pitfall 5: Range with Negative Step or Wrong Direction
**What goes wrong:** `[5..1]` or `[1..-1..5]` produces empty list or infinite loop.
**Why it happens:** No validation of step direction vs start/stop relationship.
**How to avoid:** Handle direction automatically: if step > 0, iterate while current <= stop; if step < 0, iterate while current >= stop. Default step is +1 for start <= stop, -1 for start > stop. OR simply: default step = 1, iterate while current <= stop (empty list if start > stop).

### Pitfall 6: spanOf Missing New AST Nodes
**What goes wrong:** Adding `Range` to `Expr` but forgetting to update `spanOf` in Ast.fs causes incomplete match warning or runtime crash.
**Why it happens:** The `spanOf` function pattern-matches all Expr variants.
**How to avoid:** Update ALL pattern match exhaustive functions: `spanOf`, `collectMatches`, `rewriteModuleAccess`, `collectModuleRefs`, etc.

## Code Examples

### Range Evaluation

```fsharp
// In Eval.fs, add to eval function:
| Range(startExpr, stopExpr, stepOpt, _) ->
    let startVal = eval recEnv moduleEnv env false startExpr
    let stopVal = eval recEnv moduleEnv env false stopExpr
    match startVal, stopVal with
    | IntValue start, IntValue stop ->
        let step =
            match stepOpt with
            | Some stepExpr ->
                match eval recEnv moduleEnv env false stepExpr with
                | IntValue s -> s
                | _ -> failwith "Type error: range step must be integer"
            | None -> if start <= stop then 1 else -1
        let values =
            if step > 0 then
                [start .. step .. stop] |> List.map IntValue
            elif step < 0 then
                [start .. step .. stop] |> List.map IntValue
            else
                failwith "Range error: step cannot be zero"
        ListValue values
    | _ -> failwith "Type error: range bounds must be integers"
```

**Note:** F#'s built-in `[start .. step .. stop]` handles both positive and negative steps correctly, producing an empty list when the range is invalid (e.g., `[5 .. 1 .. 1]` = `[]`).

### Range Type Checking

```fsharp
// In Bidir.synth, add:
| Range(startExpr, stopExpr, stepOpt, span) ->
    let s1, startTy = synth ctorEnv recEnv ctx env startExpr
    let s2 = unifyWithContext ctx [] span (apply s1 startTy) TInt
    let s3, stopTy = synth ctorEnv recEnv ctx (applyEnv (compose s2 s1) env) stopExpr
    let s4 = unifyWithContext ctx [] span (apply s3 stopTy) TInt
    let sStep =
        match stepOpt with
        | Some stepExpr ->
            let s5, stepTy = synth ctorEnv recEnv ctx (applyEnv (compose s4 (compose s3 (compose s2 s1))) env) stepExpr
            let s6 = unifyWithContext ctx [] span (apply s5 stepTy) TInt
            compose s6 s5
        | None -> empty
    let finalS = compose sStep (compose s4 (compose s3 (compose s2 s1)))
    (finalS, TList TInt)
```

### Mutual Recursion Type Checking (typeCheckDecls)

```fsharp
| LetRecDecl(bindings, _) ->
    // Phase 1: Create fresh type variables for all functions
    let funcTypes =
        bindings |> List.map (fun (name, params, _) ->
            // For multi-param functions, create arrow chain
            let paramTypes = params |> List.map (fun _ -> freshVar())
            let retType = freshVar()
            let funcType =
                List.foldBack (fun pt acc -> TArrow(pt, acc)) paramTypes retType
            (name, funcType, paramTypes))

    // Phase 2: Add all functions to env as monomorphic
    let recEnv =
        funcTypes |> List.fold (fun acc (name, funcTy, _) ->
            Map.add name (Scheme([], funcTy)) acc) env

    // Phase 3: Type-check each body
    let finalSubst =
        List.map2 (fun (_, params, body) (name, funcTy, paramTypes) ->
            // Add params to env
            let bodyEnv =
                List.zip params paramTypes
                |> List.fold (fun acc (p, t) -> Map.add p (Scheme([], t)) acc) recEnv
            // Desugar multi-param: just check the lambda-desugared body
            let s, bodyTy = Bidir.synth ctorEnv recEnv ctx bodyEnv body
            // ... unify and compose
        ) bindings funcTypes
        |> List.fold compose empty

    // Phase 4: Generalize
    let env' = applyEnv finalSubst env
    let env'' =
        funcTypes |> List.fold (fun acc (name, funcTy, _) ->
            let ty = apply finalSubst funcTy
            Map.add name (generalize env' ty) acc) env'
    // ... continue
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No module-level rec | Add LetRecDecl | Phase 18 | Enables mutual recursion at top level |
| Manual list construction | Range syntax | Phase 18 | Ergonomic list generation |

## Open Questions

1. **Should `let rec f x = ...` (single function, module level) use LetRecDecl?**
   - Recommendation: YES. `LetRecDecl` with a single binding is the natural way. This also enables self-recursion at module level (currently module-level `let` desugars functions but doesn't create recursive bindings). Currently module-level `let f x = x + f 1` would fail because `f` is not in scope during its own body. Adding `let rec` at module level solves this.

2. **Range with descending default?**
   - F# uses step=1 by default for `[1..5]`, and `[5..1]` produces empty list. `[5..-1..1]` produces `[5; 4; 3; 2; 1]`.
   - Recommendation: Follow F# behavior. Default step = 1. `[5..1]` = `[]`. Require explicit negative step.

3. **Should step range syntax be `[start..step..stop]` or `[start,step..stop]`?**
   - F# uses `[start..step..stop]` with DOTDOT for both separators.
   - Haskell uses `[start,step..stop]` with comma and dotdot.
   - Recommendation: Use `[start..step..stop]` (F# style) as specified in requirements.

## Sources

### Primary (HIGH confidence)
- LangThree source code: Ast.fs, Parser.fsy, Lexer.fsl, Eval.fs, TypeCheck.fs, Bidir.fs, Infer.fs
- F# language specification: range expression syntax `[start..step..stop]`
- FsLex documentation: longest-match semantics for lexer rules

### Secondary (MEDIUM confidence)
- Standard ML / OCaml `let rec ... and ...` semantics for mutual recursion
- Hindley-Milner type inference: simultaneous binding for recursive groups

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all existing tooling, no new dependencies
- Architecture (Range): HIGH - straightforward LALR(1) extension, verified no conflicts
- Architecture (Mutual Rec): HIGH - mirrors existing TypeDeclContinuation pattern
- Pitfalls: HIGH - derived from direct codebase analysis
- Type inference for mutual rec: MEDIUM - standard algorithm but implementation details need care

**Research date:** 2026-03-19
**Valid until:** 2026-04-19 (stable - no external dependencies)
