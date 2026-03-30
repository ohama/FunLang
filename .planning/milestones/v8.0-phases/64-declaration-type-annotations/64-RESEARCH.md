# Phase 64: Declaration Type Annotations - Research

**Researched:** 2026-03-30
**Domain:** Parser grammar extension (FsLexYacc), AST desugaring, type inference integration
**Confidence:** HIGH

## Summary

Phase 64 adds parameter type annotations and return type annotations to `let` declarations at both module level and expression level. The implementation strategy is to reuse the existing `LambdaAnnot` and `Annot` AST nodes — no new AST nodes are required. The parser grammar needs two new concepts: a `MixedParamList` rule (handles plain and annotated params in any order) for PARAM-01/PARAM-02, and return type annotation syntax (`: TypeExpr` between params and `=`) for RET-01/RET-02.

All type annotations are erased at runtime — `LambdaAnnot` already evaluates to `FunctionValue(param, body, env)` identical to `Lambda`, and `Annot` already evaluates by unwrapping to the inner expression. The Infer/Bidir type systems already handle both nodes. No changes to Eval.fs, Infer.fs, Bidir.fs, or TypeCheck.fs are needed.

The non-trivial work is the grammar: adding `MixedParamList` to every `LET`/`LET REC`/`LetRecDeclaration`/`LetRecContinuation` production in Parser.fsy, and adding return type annotation productions. The `LetRecDecl` binding tuple `(string * string * Expr * Span)` stores only one param and the already-desugared body — so multi-param annotated let rec uses the same foldBack desugaring as plain multi-param but produces `LambdaAnnot` nodes instead of `Lambda` nodes for annotated params.

**Primary recommendation:** Extend Parser.fsy with a `MixedParamList` rule and return type annotation productions; desugar to existing `LambdaAnnot`/`Annot` nodes. Zero changes to evaluation or type inference.

## Standard Stack

This is an internal language extension — no external libraries involved.

### Core
| Component | File | Purpose | Why Standard |
|-----------|------|---------|--------------|
| Parser.fsy | src/LangThree/Parser.fsy | FsLexYacc grammar rules | Only grammar entry point |
| Ast.fs | src/LangThree/Ast.fs | AST node definitions | Already has LambdaAnnot, Annot |
| Eval.fs | src/LangThree/Eval.fs | Runtime evaluation | LambdaAnnot already handled |
| Infer.fs | src/LangThree/Infer.fs | Type inference | LambdaAnnot already handled |

### Supporting
| Component | File | Purpose | When to Use |
|-----------|------|---------|-------------|
| desugarAnnotParams | Parser.fsy line 15-19 | Folds AnnotParam list into nested LambdaAnnot | Reuse for MixedParamList |
| TypeExpr grammar | Parser.fsy line 474-511 | Parses type expressions including generics | Used in return type annotations |

## Architecture Patterns

### Recommended Project Structure
```
src/LangThree/
├── Parser.fsy        # Add MixedParamList, return type annotation productions
├── Ast.fs            # No changes (LambdaAnnot and Annot already exist)
├── Eval.fs           # No changes (both nodes already handled)
├── Infer.fs          # No changes (both nodes already handled)
└── TypeCheck.fs      # No changes (bindings structure unchanged)
tests/flt/file/
└── let/              # Add annotated declaration test files
```

### Pattern 1: MixedParamList Desugaring

**What:** A new grammar rule `MixedParamList` produces a list of discriminated-union items (plain param string | annotated param string * TypeExpr). The action desugars them via `List.foldBack` into nested `Lambda`/`LambdaAnnot` chains.

**When to use:** Every `LET IDENT ... EQUALS ...` production that uses `ParamList` must be duplicated with `MixedParamList`.

**Example desugaring in parser action:**
```fsharp
// Source: existing desugarAnnotParams (Parser.fsy line 15-19) + new mixed variant
let rec desugarMixedParams (paramList: Choice<string, string * TypeExpr> list) (body: Expr) (span: Span) : Expr =
    match paramList with
    | [] -> body  // body is already the target expression
    | Choice1Of2 name :: rest ->
        Lambda(name, desugarMixedParams rest body span, span)
    | Choice2Of2 (name, ty) :: rest ->
        LambdaAnnot(name, ty, desugarMixedParams rest body span, span)
```

**Grammar rule:**
```
MixedParamList:
    | MixedParam                   { [$1] }
    | MixedParam MixedParamList    { $1 :: $2 }

MixedParam:
    | IDENT                                    { Choice1Of2 $1 }
    | LPAREN IDENT COLON TypeExpr RPAREN       { Choice2Of2 ($2, $4) }
```

### Pattern 2: Return Type Annotation Wrapping

**What:** After parsing `let f params : ReturnType = body`, wrap `body` in `Annot(body, ReturnType, span)`. The Annot node is already handled by eval (unwraps) and infer (unifies inferred type with annotation).

**When to use:** Separate grammar productions for the return-annotation case, covering both `Decl` and `Expr` let rules.

**Example grammar production (Decl level):**
```
// let f x (y : bool) : int = body  (mixed params + return type)
| LET IDENT MixedParamList COLON TypeExpr EQUALS SeqExpr
    { let body = Annot($7, $5, ruleSpan parseState 1 7)
      let lambda = desugarMixedParams $3 body (ruleSpan parseState 1 7)
      LetDecl($2, lambda, ruleSpan parseState 1 7) }

// let f x : int = body  (plain params + return type, using existing ParamList)
| LET IDENT ParamList COLON TypeExpr EQUALS SeqExpr
    { let body = Annot($7, $5, ruleSpan parseState 1 7)
      let lambda = List.foldBack (fun p b -> Lambda(p, b, ruleSpan parseState 1 7)) $3 body
      LetDecl($2, lambda, ruleSpan parseState 1 7) }

// let f : int = body  (no params, only return type)
| LET IDENT COLON TypeExpr EQUALS SeqExpr
    { LetDecl($2, Annot($6, $4, ruleSpan parseState 1 6), ruleSpan parseState 1 6) }
```

### Pattern 3: LetRecDecl Binding with Annotated Params

**What:** `LetRecDecl` binding tuple is `(string * string * Expr * Span)` — name, first-param, already-desugared-body, span. The first param is extracted by matching the outermost `Lambda` or `LambdaAnnot` produced by `desugarMixedParams`.

**When to use:** LetRecDeclaration and LetRecContinuation rules in Parser.fsy.

**Example:**
```fsharp
// Parser.fsy LetRecDeclaration action (extended)
| LET REC IDENT MixedParamList EQUALS SeqExpr LetRecContinuation
    { let span = ruleSpan parseState 1 7
      let lambda = desugarMixedParams $4 $6 span
      match lambda with
      | Lambda(p, b, _) -> [LetRecDecl(($3, p, b, span) :: $7, span)]
      | LambdaAnnot(p, _, b, _) -> [LetRecDecl(($3, p, b, span) :: $7, span)]
      | _ -> failwith "impossible: MixedParamList must produce at least one param" }
```

Note: the type annotation on the first param is absorbed into `b` (the body is the rest of the lambda chain starting from the second param). The outermost lambda's param type information is lost at the `LetRecDecl` binding level — but it doesn't matter because `LambdaAnnot` in the body is handled by Infer/Eval. Alternatively, we can store the full desugared lambda as the body of a single-param LetRecDecl where the first param is `__annotated_outer` to preserve types. See Open Questions.

### Pattern 4: Production Explosion and How to Avoid It

**What:** Every let-form needs both an inline (`EQUALS Expr`) and indented (`EQUALS INDENT Expr DEDENT`) variant. With MixedParamList + return type, the combinations multiply. Use consistent naming and systematic coverage.

**Productions to add (Decl level):**
1. `LET IDENT MixedParamList EQUALS SeqExpr` (inline)
2. `LET IDENT MixedParamList EQUALS INDENT SeqExpr DEDENT` (indented)
3. `LET IDENT MixedParamList COLON TypeExpr EQUALS SeqExpr` (return annot inline)
4. `LET IDENT MixedParamList COLON TypeExpr EQUALS INDENT SeqExpr DEDENT` (return annot indented)
5. `LET IDENT ParamList COLON TypeExpr EQUALS SeqExpr` (plain params + return annot)
6. `LET IDENT ParamList COLON TypeExpr EQUALS INDENT SeqExpr DEDENT`
7. `LET IDENT COLON TypeExpr EQUALS SeqExpr` (no params, just return type)
8. `LET IDENT COLON TypeExpr EQUALS INDENT SeqExpr DEDENT`

Same set for `Expr` level let bindings. For `LetRecDeclaration` and `LetRecContinuation`, add MixedParamList and return type variants.

### Anti-Patterns to Avoid

- **Adding new AST nodes:** Don't create `LetAnnotDecl` or `AnnotatedParam` variants. Desugar to `Lambda`/`LambdaAnnot` in the parser action. All downstream passes already handle these.
- **Changing LetRecDecl tuple type:** The `(string * string * Expr * Span)` tuple stores name, first-param-name, body. Don't change this to carry type info — the type is embedded in the body (as nested `LambdaAnnot`).
- **Forgetting expression-level let:** `Expr` rule has its own let productions (lines ~127-167 in Parser.fsy). These need the same MixedParamList + return type treatment for completeness (RET-01 says `let f x : int = x + 1`).
- **LALR(1) conflict with COLON:** The return type annotation `: TypeExpr` after params uses `COLON`, which also appears in `AnnotParam` as `(x : T)`. The LALR parser resolves this by position: `COLON` after `EQUALS`/params-end at declaration level is unambiguous because `COLON` cannot follow IDENT in a paramlist position at that point. Watch for conflicts by checking `Parser.fs` after codegen.
- **Missing AnnotParam unit case:** The existing `FUN LPAREN RPAREN` unit param handling uses `LambdaAnnot("__unit", TETuple [], ...)`. The `MixedParamList` rule does NOT need to include the `LPAREN RPAREN` unit param variant — the existing dedicated rules for unit params can remain as-is for now (PARAM-01 does not mention unit params).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Type annotation erasure | Custom eval case | Existing `Annot`/`LambdaAnnot` eval handlers | Already implemented in Eval.fs |
| Annotated param type inference | New unification code | Existing `LambdaAnnot` infer case | Already calls `elaborateTypeExpr` |
| Return type checking | New type check pass | `Annot` node wrapping body | Infer already unifies Annot |
| Mutual recursion with annotations | New LetRecDecl variant | Desugar annotated params into body | TypeCheck.fs uses body expression |

**Key insight:** All infrastructure for type annotations already exists. The only work is grammar rules and parser actions that produce the existing node types.

## Common Pitfalls

### Pitfall 1: MixedParamList vs AnnotParamList Ambiguity

**What goes wrong:** Both `MixedParamList` (mixed plain+annotated) and `AnnotParamList` (all-annotated, used for `fun`) have `AnnotParam` in common. If `MixedParamList` is introduced in Decl position, the LALR parser may have shift-reduce conflicts with existing `ParamList` or `AnnotParamList`.

**Why it happens:** `IDENT` in `ParamList` and `LPAREN IDENT COLON TypeExpr RPAREN` in `AnnotParam` are both valid starts of a MixedParamList item. FsLexYacc needs distinct lookahead — `IDENT` → plain, `LPAREN` → annotated — which is fine because these are distinguishable by one token.

**How to avoid:** Define `MixedParam` as a choice between `IDENT` (plain) and `LPAREN IDENT COLON TypeExpr RPAREN` (annotated). Keep `MixedParamList` separate from the existing `ParamList`. Both are non-empty lists. The LALR(1) parser handles this cleanly because the first token distinguishes the alternatives.

**Warning signs:** `Parser.fs` (generated) contains "shift/reduce conflict" messages in its header comments. Run `dotnet build` after parser changes and check for conflicts.

### Pitfall 2: Return Type COLON Conflicts with Existing COLON Uses

**What goes wrong:** `LET IDENT ParamList COLON TypeExpr EQUALS` introduces `COLON` in a position the grammar hasn't seen before. If there's a prior rule that can reach the same state expecting `EQUALS`, the parser may reduce too early.

**Why it happens:** FsLexYacc's LALR(1) lookahead is one token. After reducing `ParamList`, the parser sees either `COLON` (return type) or `EQUALS` (body). These are distinguishable with one lookahead token.

**How to avoid:** Ensure the `COLON TypeExpr EQUALS` suffix is only reachable after `ParamList` completes. Test the no-params return annotation case `LET IDENT COLON TypeExpr EQUALS` carefully — `IDENT COLON` appears in `RecordField` too, but at different grammar levels so no conflict.

**Warning signs:** Build failures from FsLexYacc with conflict messages. Parser.fs will show them.

### Pitfall 3: LetRecDecl First-Param Extraction from LambdaAnnot

**What goes wrong:** `LetRecDeclaration` extracts the first param by pattern-matching `Lambda(p, b, _)`. After introducing `MixedParamList`, the outermost node could be `LambdaAnnot(p, ty, b, _)` instead. A missing match arm causes a runtime exception.

**Why it happens:** The production action matches only `Lambda` (line 786 in Parser.fsy).

**How to avoid:** Add `LambdaAnnot(p, _, b, _)` to the match. The type annotation `ty` on the first param is embedded inside `b` as a nested `LambdaAnnot` chain — no information is lost.

**Warning signs:** `failwith "impossible"` being thrown at parse time for annotated let rec.

### Pitfall 4: LetRecContinuation Missing MixedParamList Variant

**What goes wrong:** PARAM-02 requires annotated params in `let rec f ... and g (y : bool) = ...`. If `LetRecContinuation` is not updated, the `and` clause silently falls through to a parse error.

**Why it happens:** `LetRecContinuation` (lines 806-817) only has `ParamList` variants.

**How to avoid:** Add `MixedParamList` and optionally `MixedParamList COLON TypeExpr` variants to `LetRecContinuation`.

**Warning signs:** Parse error on `and g (y : bool) = ...` despite `LetRecDeclaration` being updated.

### Pitfall 5: Forgetting Indented Body Variants

**What goes wrong:** New productions for annotated params added inline only, not in INDENT/DEDENT form. Multi-line bodies fail to parse.

**Why it happens:** Every let-form in this grammar has two variants. Easy to miss when adding many new productions.

**How to avoid:** For every new inline production added, immediately add the indented counterpart. The pattern is `... EQUALS SeqExpr` → `... EQUALS INDENT SeqExpr DEDENT`.

**Warning signs:** Integration tests with multi-line bodies fail while single-line tests pass.

## Code Examples

### Existing desugarAnnotParams (reuse pattern)
```fsharp
// Source: Parser.fsy line 14-19
let rec desugarAnnotParams (paramList: (string * TypeExpr) list) (body: Expr) (span: Span) : Expr =
    match paramList with
    | [] -> failwith "desugarAnnotParams: empty param list"
    | [(name, ty)] -> LambdaAnnot(name, ty, body, span)
    | (name, ty) :: rest -> LambdaAnnot(name, ty, desugarAnnotParams rest body span, span)
```

### New desugarMixedParams helper (to add in Parser.fsy preamble)
```fsharp
// Add to Parser.fsy preamble (after desugarAnnotParams)
let rec desugarMixedParams (paramList: Choice<string, string * TypeExpr> list) (body: Expr) (span: Span) : Expr =
    match paramList with
    | [] -> body
    | Choice1Of2 name :: rest ->
        Lambda(name, desugarMixedParams rest body span, span)
    | Choice2Of2 (name, ty) :: rest ->
        LambdaAnnot(name, ty, desugarMixedParams rest body span, span)
```

### MixedParamList grammar rules
```
MixedParamList:
    | MixedParam                   { [$1] }
    | MixedParam MixedParamList    { $1 :: $2 }

MixedParam:
    | IDENT                                    { Choice1Of2 $1 }
    | LPAREN IDENT COLON TypeExpr RPAREN       { Choice2Of2 ($2, $4) }
```

### Decl production: annotated params (no return type)
```
// let f (x : int) y (z : bool) = body
| LET IDENT MixedParamList EQUALS SeqExpr
    { let lambda = desugarMixedParams $3 $5 (ruleSpan parseState 1 5)
      LetDecl($2, lambda, ruleSpan parseState 1 5) }
| LET IDENT MixedParamList EQUALS INDENT SeqExpr DEDENT
    { let lambda = desugarMixedParams $3 $6 (ruleSpan parseState 1 7)
      LetDecl($2, lambda, ruleSpan parseState 1 7) }
```

### Decl production: return type annotation
```
// let f x : int = body  (ParamList + return type)
| LET IDENT ParamList COLON TypeExpr EQUALS SeqExpr
    { let body = Annot($7, $5, ruleSpan parseState 1 7)
      let lambda = List.foldBack (fun p b -> Lambda(p, b, ruleSpan parseState 1 7)) $3 body
      LetDecl($2, lambda, ruleSpan parseState 1 7) }
// let f (x : int) : bool = body  (MixedParamList + return type)
| LET IDENT MixedParamList COLON TypeExpr EQUALS SeqExpr
    { let body = Annot($7, $5, ruleSpan parseState 1 7)
      let lambda = desugarMixedParams $3 body (ruleSpan parseState 1 7)
      LetDecl($2, lambda, ruleSpan parseState 1 7) }
// let f : int = body  (no params, only return type)
| LET IDENT COLON TypeExpr EQUALS SeqExpr
    { LetDecl($2, Annot($6, $4, ruleSpan parseState 1 6), ruleSpan parseState 1 6) }
```

### LetRecDeclaration: extracting first param from LambdaAnnot
```fsharp
// Extend match in LetRecDeclaration action
let lambda = desugarMixedParams $4 $6 span
match lambda with
| Lambda(p, b, _) -> [LetRecDecl(($3, p, b, span) :: $7, span)]
| LambdaAnnot(p, _, b, _) -> [LetRecDecl(($3, p, b, span) :: $7, span)]
| _ -> failwith "impossible: MixedParamList must produce at least one param"
```

### flt test: mixed annotated params (PARAM-01)
```
// Test: let declaration with mixed annotated and plain parameters (PARAM-01)
// --- Command: /path/to/LangThree %input
// --- Input:
let f (x : int) y (z : bool) = if z then x else y
let result = f 10 20 true
// --- Output:
10
```

### flt test: return type annotation (RET-01)
```
// Test: let declaration with return type annotation (RET-01)
// --- Command: /path/to/LangThree %input
// --- Input:
let add x : int = x + 1
let result = add 41
// --- Output:
42
```

### flt test: param + return type (RET-02)
```
// Test: parameter annotation and return type annotation combined (RET-02)
// --- Command: /path/to/LangThree %input
// --- Input:
let isPos (x : int) : bool = x > 0
let result = isPos 5
// --- Output:
true
```

### flt test: mutual recursion with annotated params (PARAM-02)
```
// Test: mutual recursion with annotated parameters (PARAM-02)
// --- Command: /path/to/LangThree %input
// --- Input:
let rec isEven (n : int) = if n = 0 then true else isOdd (n - 1)
and isOdd (n : int) = if n = 0 then false else isEven (n - 1)
let result = isEven 4
// --- Output:
true
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Plain params only in let decls | Plain params only (Phase 1-63) | — | Phase 64 changes this |
| `fun (x: T) -> e` for annotated lambdas | `LambdaAnnot` in AST (v6.0) | v6.0 | Reuse in Phase 64 |
| Angle brackets in type exprs not supported | Full `IDENT<TypeArgList>` support (Phase 63) | Phase 63 | Available in return/param type exprs |

**What remains same:**
- `AnnotParamList` / `AnnotParam` for `fun (x:T) (y:U) -> e` — unchanged, still used for lambdas only
- `LambdaAnnot` eval: still `FunctionValue(param, body, env)` — type erased
- `Annot` eval: still unwraps — type erased

## Open Questions

1. **Should LetRecDecl store first-param type annotation?**
   - What we know: `LetRecDecl` binding is `(name * param * body * span)`. When desugarMixedParams produces `LambdaAnnot(p, ty, b, _)`, we extract `(name, p, b, span)` — the type annotation `ty` on the first param is not stored separately. However, `b` will contain `LambdaAnnot` nodes for remaining params and/or `Annot` on the body.
   - What's unclear: Whether TypeCheck.fs needs the first-param's annotation for correct inference. Looking at TypeCheck.fs lines 852-900: it uses `freshVar()` for `paramTy` regardless — does not use the annotation. So for mutual recursion, the first-param annotation is effectively ignored by the type checker (but this matches the requirement: "type annotations are erased and existing type inference is maintained").
   - Recommendation: Do not change `LetRecDecl` tuple type. First-param annotation is silently ignored — this is correct per requirement 5 ("type annotations are erased at runtime"). If stricter annotation enforcement is needed, it can be a future phase.

2. **Expression-level let with annotations — scope of PARAM-01/RET-01**
   - What we know: The requirements say `let` declarations. The `Expr` rule (lines 127-167) has let-expression forms too.
   - What's unclear: Whether PARAM-01/RET-01 require expression-level let annotations (e.g., `let result = let f (x:int) = x in f 1`).
   - Recommendation: Add expression-level productions too for completeness and consistency. The FunLexYacc compatibility target likely uses top-level declarations, but expression-level is trivially supported with the same grammar additions.

3. **Unit param `()` in MixedParamList**
   - What we know: Existing rules `LET IDENT LPAREN RPAREN EQUALS` handle `let f () = body`. This uses `LambdaAnnot("__unit", TETuple [], ...)`.
   - What's unclear: Whether `MixedParamList` should handle `LPAREN RPAREN` as a unit param item.
   - Recommendation: Keep the existing dedicated `let f () = body` rule as-is. Do not add unit param to `MixedParam`. If mixing unit params with other params is needed (e.g., `let f () (x:int) = ...`), that can be a separate extension. The requirements do not mention unit params.

## Sources

### Primary (HIGH confidence)
- `/Users/ohama/vibe-coding/LangThree/src/LangThree/Parser.fsy` — Full grammar examined, all relevant productions identified
- `/Users/ohama/vibe-coding/LangThree/src/LangThree/Ast.fs` — Confirmed `LambdaAnnot`, `Annot`, `LetRecDecl` shape
- `/Users/ohama/vibe-coding/LangThree/src/LangThree/Eval.fs` — Confirmed LambdaAnnot and Annot eval (type erasure at lines 1325-1330)
- `/Users/ohama/vibe-coding/LangThree/src/LangThree/Infer.fs` — Confirmed LambdaAnnot and Annot inference (lines 211-223)
- `/Users/ohama/vibe-coding/LangThree/src/LangThree/TypeCheck.fs` — Confirmed LetRecDecl type check structure (lines 852-900)
- `/Users/ohama/vibe-coding/LangThree/.planning/REQUIREMENTS.md` — PARAM-01/02, RET-01/02 exact requirements

### Secondary (MEDIUM confidence)
- Existing test files in `tests/flt/file/` — pattern for flt test format confirmed
- Phase 63 implementation in Parser.fsy (lines 506-511, 531-546) — angle bracket syntax available in TypeExpr

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all relevant files read, implementation fully understood
- Architecture patterns: HIGH — desugarMixedParams is a direct extension of existing desugarAnnotParams; LambdaAnnot/Annot reuse is confirmed
- Pitfalls: HIGH — all pitfalls derived from reading actual code, not speculation

**Research date:** 2026-03-30
**Valid until:** 2026-04-30 (grammar changes are stable; this is internal codebase research)
