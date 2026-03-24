# Phase 28: N-Tuples - Research

**Researched:** 2026-03-24
**Domain:** F# / fsyacc parser grammar, AST extension, type checker, evaluator
**Confidence:** HIGH

## Summary

Phase 28 adds N-tuple support (3-tuples and larger) to LangThree. The critical finding from direct codebase analysis is that **most of this work is already done**. The AST (`Tuple of Expr list`, `TuplePat of Pattern list`, `TTuple of Type list`, `TupleValue of Value list`) already handles arbitrary arity. N-tuple expressions, match patterns, local let-destructuring, and function parameters all work today.

The **only missing piece** is top-level module-level tuple destructuring: `let (a, b, c) = expr` at declaration scope (outside any expression context). The `Decl` grammar nonterminal in `Parser.fsy` only handles `LET IDENT EQUALS ...` patterns, not `LET TuplePattern EQUALS ...`. This requires adding a new `LetPatDecl` variant to `Ast.Decl`, a new parser rule, and handling in `TypeCheck.fs` and `Eval.fs`.

The implementation is small and low-risk because the expression-level `LetPat` case (which already works) provides an exact template for the declaration-level case.

**Primary recommendation:** Add `LetPatDecl of Pattern * Expr * Span` to `Ast.Decl`, wire it through Parser/TypeCheck/Eval using `LetDecl` + `LetPat` as the template, and write regression tests for all four success criteria.

## Standard Stack

This is pure language implementation work with no external libraries. The project uses:

### Core
| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| F# / .NET | 10 | Implementation language | Project baseline |
| fsyacc (FsLexYacc) | bundled | LALR(1) parser generator | Project baseline |
| fslex (FsLexYacc) | bundled | Lexer generator | Project baseline |

### Supporting
| File | Purpose | When to Touch |
|------|---------|---------------|
| `Ast.fs` | Add `LetPatDecl` to `Decl` DU | New Decl variant needed |
| `Parser.fsy` | Add grammar rules for top-level tuple let | Missing grammar production |
| `TypeCheck.fs` | Handle `LetPatDecl` in `typeCheckDecls` fold | Type-check new Decl |
| `Eval.fs` | Handle `LetPatDecl` in `evalModuleDecls` fold | Evaluate new Decl |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New `LetPatDecl` Decl variant | Reuse `LetDecl("__pattern_...", LetPat(..., body_unit))` | Desugar at parse time avoids new Decl variant but breaks top-level binding semantics (all bindings need individual names in env) |

The cleanest approach is a new `LetPatDecl` variant that mirrors how `LetPat` works at expression level.

## Architecture Patterns

### Recommended Project Structure

No structural changes needed. All modifications are in existing files:

```
src/LangThree/
├── Ast.fs              # Add LetPatDecl to Decl
├── Parser.fsy          # Add grammar rules in Decl nonterminal
├── TypeCheck.fs        # Handle LetPatDecl in typeCheckDecls
└── Eval.fs             # Handle LetPatDecl in evalModuleDecls
```

### Pattern 1: Existing Expression-Level Template (LetPat in Expr)

**What:** The expression-level `let (a, b, c) = expr in body` already works via `LetPat`. The declaration-level case needs identical logic but bound into the accumulating environment.

**When to use:** Use this as the exact template for the new `LetPatDecl` handler.

**Expression-level grammar (existing, Parser.fsy line 122):**
```fsharp
// Expression level — already works:
| LET TuplePattern EQUALS Expr IN Expr  { LetPat($2, $4, $6, ruleSpan parseState 1 6) }
```

**Declaration-level grammar (to add, in Decl nonterminal):**
```fsharp
// Declaration level — to add:
| LET TuplePattern EQUALS Expr
    { LetPatDecl($2, $4, ruleSpan parseState 1 4) }
| LET TuplePattern EQUALS INDENT Expr DEDENT
    { LetPatDecl($2, $5, ruleSpan parseState 1 6) }
```

### Pattern 2: New AST Decl Variant

**What:** Add `LetPatDecl` to `Ast.Decl` discriminated union.

**Where:** `Ast.fs`, in the `Decl` type definition (around line 289):
```fsharp
type Decl =
    | LetDecl of name: string * body: Expr * Span
    | LetPatDecl of pat: Pattern * body: Expr * Span   // NEW
    | TypeDecl of TypeDecl
    // ... rest unchanged
```

The `declSpanOf` function must also handle the new variant:
```fsharp
| LetPatDecl(_, _, s) -> s
```

### Pattern 3: TypeCheck.fs Declaration Handler

**What:** The `typeCheckDecls` fold in `TypeCheck.fs` needs a new match arm for `LetPatDecl`.

**Template from expression-level `LetPat` inference (`Infer.fs` line 362):**
```fsharp
| LetPatDecl(pat, body, _) ->
    // Type-check body
    let refsInBody = collectModuleRefs mods body
    let rewrittenBody = rewriteModuleAccess mods body
    let (envForSynth, ctorForSynth, recForSynth) =
        if Set.isEmpty refsInBody then (env, cEnv, rEnv)
        else mergeModuleExportsForTypeCheck mods refsInBody env cEnv rEnv
    let s, valueTy = Bidir.synth ctorForSynth recForSynth [] envForSynth rewrittenBody
    // Infer pattern bindings
    let patEnv, patTy = Infer.inferPattern Map.empty pat
    // Unify value type with pattern type
    let s2 = Unify.unify (apply s valueTy) patTy
    let s' = compose s2 s
    // Generalize each binding and add to env
    let env' = applyEnv s' env
    let generalizedPatEnv =
        patEnv
        |> Map.map (fun _ (Scheme(_, ty)) ->
            let ty' = apply s' ty
            generalize env' ty')
    let env'' = Map.fold (fun acc k v -> Map.add k v acc) env' generalizedPatEnv
    let matchWarnings = checkMatchWarnings cEnv body
    (env'', cEnv, rEnv, mods, warns @ matchWarnings)
```

### Pattern 4: Eval.fs Declaration Handler

**What:** `evalModuleDecls` fold in `Eval.fs` needs a match arm for `LetPatDecl`.

**Template from expression-level `LetPat` eval (`Eval.fs` line 419):**
```fsharp
| LetPatDecl(pat, bodyExpr, _) ->
    let value = eval recEnv modEnv env false bodyExpr
    match matchPattern pat value with
    | Some bindings ->
        let env' = List.fold (fun e (n, v) -> Map.add n v e) env bindings
        (env', modEnv)
    | None ->
        failwith "Pattern match failed in let declaration"
```

### Pattern 5: What Already Works (No Changes Needed)

The following all work today and must remain working (regression check):

```
// 3-tuple expression — works
let t = (1, "hello", true)

// match on 3-tuple — works
match t with | (a, b, c) -> a + c

// local let-destructuring — works
let result =
  let (a, b, c) = (1, "hello", true)
  a

// function with tuple parameter — works
let f = fun (a, b, c) -> a + b + c

// fst/snd from Prelude — works (pattern-match based, arbitrary arity fine)
```

### Anti-Patterns to Avoid

- **Adding separate grammar for 2-tuple vs 3-tuple:** The `TuplePattern` grammar nonterminal (`PatternList`) already handles any N >= 2 via left-recursive `Pattern COMMA PatternList`. Do not special-case sizes.
- **Forgetting `declSpanOf`:** F# DU exhaustiveness checking will catch this, but the compiler will give cryptic errors if the `Decl` DU is extended without updating `declSpanOf`.
- **Touching MatchCompile.fs:** It already handles `TuplePat` of arbitrary arity via `#tuple_N` constructor encoding. No changes needed.
- **Touching Infer.fs:** The `inferPattern` function for `TuplePat` already handles arbitrary arity. No changes needed.
- **Touching Exhaustive.fs:** It already treats `TuplePat` as wildcard. No changes needed.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Pattern matching on tuple values | Custom tuple destructor | Existing `matchPattern` function in Eval.fs | Already handles N-tuples correctly |
| Type inference for tuple patterns | New type unification | Existing `inferPattern` in Infer.fs line 62 | Already returns `TTuple tys` for any N |
| N-tuple grammar | New tokens or AST nodes | Existing `TuplePattern` / `PatternList` grammar rules | Already parse any N >= 2 |

**Key insight:** The infrastructure is complete. Only the declaration-level plumbing is missing.

## Common Pitfalls

### Pitfall 1: Exhaustiveness Check Compile Error After Adding LetPatDecl

**What goes wrong:** After adding `LetPatDecl` to `Ast.Decl`, the F# compiler will emit exhaustiveness warnings/errors in every file that pattern-matches on `Decl`. These are: `TypeCheck.fs` (`typeCheckDecls`), `Eval.fs` (`evalModuleDecls`), `Format.fs` (`declSpanOf`), and `Ast.fs` (`declSpanOf`).

**Why it happens:** F# discriminated unions require all cases to be handled.

**How to avoid:** After adding the variant, immediately update all match sites. The compiler will tell you exactly where.

**Warning signs:** `FS0025: Incomplete pattern matches` compiler warnings.

### Pitfall 2: Missing declSpanOf Case

**What goes wrong:** `Ast.declSpanOf` only matches on `Decl` variants. It is easy to forget this utility function when extending `Decl`.

**How to avoid:** Search for `declSpanOf` after adding the new variant and add the case:
```fsharp
| LetPatDecl(_, _, s) -> s
```

### Pitfall 3: Top-Level Test Uses Wrong Syntax

**What goes wrong:** Testing `let (a, b, c) = expr` at the REPL may behave differently from file parsing because the REPL uses `start: Expr EOF` entry point (not `parseModule`).

**Why it happens:** The REPL entry point is `start` which parses a single `Expr`, not `parseModule` which parses `Decls`.

**How to avoid:** Test via file execution (`dotnet run -- file.fun`), not REPL mode.

### Pitfall 4: Parse Conflict With Existing TuplePattern Rules

**What goes wrong:** Adding `LET TuplePattern EQUALS Expr` to `Decl` might create shift/reduce conflicts with existing expression-level `LET TuplePattern EQUALS Expr IN Expr`.

**Why it happens:** The parser sees `LET LPAREN ...` and must choose between the declaration-level and expression-level rules.

**How to avoid:** The `Decl` and `Expr` nonterminals are in different contexts (module level vs expression level), so no conflict exists. The `parseModule` entry only invokes `Decls` which only invokes `Decl`; the `start` entry invokes `Expr`. They do not overlap. The existing 332 shift/reduce conflicts are pre-existing and unchanged.

### Pitfall 5: Type Environment Not Propagated for Multiple Bindings

**What goes wrong:** If `let (a, b, c) = expr` binds `a`, `b`, `c`, they must all be available to subsequent declarations. Forgetting to merge all bindings from `patEnv` into the accumulated type environment leaves some names unresolved.

**How to avoid:** Use `Map.fold` to merge all bindings from `generalizedPatEnv` into `env`, identical to how `LetPat` at expression level does it (Infer.fs line 378).

## Code Examples

### Adding LetPatDecl to Ast.Decl

```fsharp
// Source: Ast.fs (direct codebase inspection)
// Existing Decl type (line ~289), add LetPatDecl after LetDecl:
type Decl =
    | LetDecl of name: string * body: Expr * Span
    | LetPatDecl of pat: Pattern * body: Expr * Span   // Phase 28: N-tuple top-level binding
    | TypeDecl of TypeDecl
    // ... rest unchanged

// Update declSpanOf:
let declSpanOf (decl: Decl) : Span =
    match decl with
    | LetDecl(_, _, s) -> s
    | LetPatDecl(_, _, s) -> s    // Phase 28
    // ... rest unchanged
```

### Parser.fsy: Add Decl Rules

```fsharp
// Source: Parser.fsy Decl nonterminal (line ~507)
// Add after existing LET UNDERSCORE rules (around line 512):
Decl:
    // ... existing rules ...
    // Phase 28: Top-level tuple pattern binding
    | LET TuplePattern EQUALS Expr
        { LetPatDecl($2, $4, ruleSpan parseState 1 4) }
    | LET TuplePattern EQUALS INDENT Expr DEDENT
        { LetPatDecl($2, $5, ruleSpan parseState 1 6) }
```

### TypeCheck.fs: Handle LetPatDecl in typeCheckDecls

```fsharp
// Source: TypeCheck.fs typeCheckDecls fold (line ~568)
// Add new match arm after the LetDecl case:
| LetPatDecl(pat, body, _) ->
    let refsInBody = collectModuleRefs mods body
    let rewrittenBody = rewriteModuleAccess mods body
    let (envForSynth, ctorEnvForSynth, recEnvForSynth) =
        if Set.isEmpty refsInBody then (env, cEnv, rEnv)
        else mergeModuleExportsForTypeCheck mods refsInBody env cEnv rEnv
    let s, valueTy = Bidir.synth ctorEnvForSynth recEnvForSynth [] envForSynth rewrittenBody
    let patEnv, patTy = Infer.inferPattern Map.empty pat
    let s2 = Unify.unify (apply s valueTy) patTy
    let s' = compose s2 s
    let env' = applyEnv s' env
    let generalizedPatEnv =
        patEnv |> Map.map (fun _ (Scheme(_, ty)) ->
            generalize env' (apply s' ty))
    let env'' = Map.fold (fun acc k v -> Map.add k v acc) env' generalizedPatEnv
    let matchWarnings = checkMatchWarnings cEnv body
    (env'', cEnv, rEnv, mods, warns @ matchWarnings)
```

### Eval.fs: Handle LetPatDecl in evalModuleDecls

```fsharp
// Source: Eval.fs evalModuleDecls fold (line ~781)
// Add new match arm after LetDecl case:
| LetPatDecl(pat, bodyExpr, _) ->
    let value = eval recEnv modEnv env false bodyExpr
    match matchPattern pat value with
    | Some bindings ->
        let env' = List.fold (fun e (n, v) -> Map.add n v e) env bindings
        (env', modEnv)
    | None ->
        failwith "Pattern match failed in module-level let pattern"
```

### Test File Patterns

```fsharp
// Source: direct testing 2026-03-24 — all tests passed

// Success criterion 1: 3-tuple create and type-check
let t = (1, "hello", true)
let result = t
// expect: (1, "hello", true)

// Success criterion 2: let-destructuring N-tuple (currently works locally, need module-level)
let (a, b, c) = (1, "hello", true)
let result = a
// expect: 1

// Success criterion 3: function parameter tuple
let f = fun (a, b, c) -> a + b + c
let result = f (1, 2, 3)
// expect: 6

// Success criterion 4: fst/snd still work (already in Prelude/Core.fun)
let p = (10, 20)
let result = fst p
// expect: 10
```

## State of the Art

| Old State | Current State | When Changed | Impact |
|-----------|---------------|--------------|--------|
| Only 2-tuples (workaround: nested or records) | N-tuple infrastructure exists, top-level binding missing | Infrastructure added pre-Phase 28 | Only declaration-level gap remains |
| `let (a, b) = e` parse error at top level | Works in local let-in, fails at module level | Phase 22 added TuplePattern for fun params | Phase 28 extends to Decl |

**What was already present before Phase 28:**
- `Tuple of Expr list` in Ast.fs (handles any N)
- `TuplePat of Pattern list` in Ast.fs (handles any N)
- `TTuple of Type list` in Type.fs (handles any N)
- `TupleValue of Value list` in Ast.fs (handles any N)
- `inferPattern` for TuplePat in Infer.fs (handles any N)
- `matchPattern` for TuplePat in Eval.fs (handles any N)
- `patternToConstructor` using `#tuple_N` in MatchCompile.fs (handles any N)
- Grammar for `fun (a, b, c) -> body` via `FUN TuplePattern ARROW Expr` (added Phase 22)
- Grammar for `let (a, b, c) = e in body` via `LET TuplePattern EQUALS Expr IN Expr` (Expr-level)
- `LPAREN Expr COMMA ExprList RPAREN` grammar produces `Tuple` with any N elements

**What is missing for Phase 28:**
- `LetPatDecl` in `Ast.Decl` — does not exist
- `LET TuplePattern EQUALS Expr` in `Decl` nonterminal — does not exist
- `LetPatDecl` handler in `typeCheckDecls` — does not exist
- `LetPatDecl` handler in `evalModuleDecls` — does not exist

## Open Questions

1. **Should LetPatDecl support wildcard patterns too?**
   - What we know: The expression-level `LET UNDERSCORE EQUALS Expr IN Expr` has its own rule (`WildcardPat`). TuplePattern only matches `LPAREN PatternList RPAREN`.
   - What's unclear: Should `let _ = expr` at module level use `LetPatDecl` or stay as `LetDecl("_", ...)`?
   - Recommendation: Keep `LET UNDERSCORE EQUALS` as a separate existing rule; only add `LET TuplePattern EQUALS` for Phase 28. Do not conflate.

2. **Do type annotations work on top-level tuple bindings?**
   - What we know: `let t: int * string * bool = (1, "hello", true)` at top level hits a parse error today (the annotation syntax uses `COLON TypeExpr` only inside specific grammar rules). This is a separate constraint not in scope for Phase 28.
   - Recommendation: Explicitly out of scope. Document that `(e : T)` Annot syntax works inside expressions but not in top-level let LHS.

3. **Should `Format.fs` pretty-printer be updated?**
   - What we know: `Format.fs` has `declSpanOf` and AST formatting. It does not affect execution.
   - Recommendation: Update `declSpanOf` (required). Other Format.fs changes are cosmetic and low priority.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection (`Ast.fs`, `Parser.fsy`, `Type.fs`, `Infer.fs`, `Eval.fs`, `TypeCheck.fs`, `MatchCompile.fs`, `Exhaustive.fs`) — all tuple-handling code read and verified
- Live testing 2026-03-24 — all existing behaviors verified by running `dotnet run -- file.fun`

### Secondary (MEDIUM confidence)
- `langthree-constraints.md` section 2.1 and 2.2 — constraint analysis confirmed gap is only at module level
- `.planning/REQUIREMENTS.md` TYPE-01 and TYPE-02 — requirements confirmed

## Metadata

**Confidence breakdown:**
- What already works: HIGH — verified by running tests and reading code
- What is missing: HIGH — directly verified that `let (a, b, c) = e` at module level gives parse error
- Implementation approach: HIGH — `LetDecl` + `LetPat` provide exact templates
- Scope of changes: HIGH — 4 files, ~30 lines total

**Research date:** 2026-03-24
**Valid until:** 2026-04-24 (stable codebase, no external dependencies)
