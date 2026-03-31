# Phase 65: LetRecDecl AST Refactoring - Research

**Researched:** 2026-03-31
**Domain:** F# AST refactoring (LangThree interpreter internals)
**Confidence:** HIGH

## Summary

This phase fixes a bug where `LetRecDecl` and `LetRec` drop the first parameter's type annotation. When `let rec f (x : int) y = ...` is parsed, `MixedParamList` desugars to `LambdaAnnot(x, int, Lambda(y, body))`, but the parser extracts only the name `x` (discarding `int`) to store in the binding tuple `(string * string * Expr * Span)`.

The fix is surgical: change `string` to `string * TypeExpr option` for the first param in both AST nodes, update the parser to preserve the type, and update all pattern-match sites (type checker, evaluator, formatter, etc.) to handle the new shape.

**Primary recommendation:** Change the binding tuple from `(string * string * Expr * Span)` to `(string * string * TypeExpr option * Expr * Span)` for `LetRecDecl`, and change `LetRec` from `string * string * Expr * Expr * Span` to `string * string * TypeExpr option * Expr * Expr * Span`. This is the minimal change that preserves the type info without restructuring the AST.

## Architecture Patterns

### The Current AST Shape

```fsharp
// Expression-level (Ast.fs line 79)
| LetRec of name: string * param: string * body: Expr * inExpr: Expr * span: Span

// Declaration-level (Ast.fs line 360)
| LetRecDecl of bindings: (string * string * Expr * Span) list * Span
```

### The Target AST Shape

```fsharp
// Expression-level
| LetRec of name: string * param: string * paramType: TypeExpr option * body: Expr * inExpr: Expr * span: Span

// Declaration-level
| LetRecDecl of bindings: (string * string * TypeExpr option * Expr * Span) list * Span
```

### Why Tuple (Not Record)

The existing codebase uses tuples consistently for AST binding representations. Introducing a record type would be inconsistent with the codebase style and require more changes. The tuple approach adds one field and is the minimal diff.

### Anti-Patterns to Avoid
- **Don't re-wrap as LambdaAnnot in the body:** The first param is extracted from the desugared chain specifically to enable direct recursive call typing. Re-wrapping would break the evaluator's closure pattern.
- **Don't change the desugaring strategy:** `desugarMixedParams` works correctly; the problem is only at the extraction site in the parser.

## Complete Site Inventory

### LetRecDecl Sites (Declaration-level)

All files/locations that destructure `LetRecDecl` bindings:

| File | Line | What It Does | Change Needed |
|------|------|-------------|---------------|
| `Ast.fs` | 360 | Type definition | Add `TypeExpr option` field |
| `Ast.fs` | 386 | `declSpanOf` | No change (only matches outer span) |
| `Parser.fsy` | 850-851 | LetRecDeclaration rule 1 (no return type) | Capture `None`/`Some ty` from Lambda/LambdaAnnot |
| `Parser.fsy` | 857-858 | LetRecDeclaration rule 2 (indented, no return type) | Same |
| `Parser.fsy` | 866-867 | LetRecDeclaration rule 3 (return type) | Same |
| `Parser.fsy` | 874-875 | LetRecDeclaration rule 4 (indented + return type) | Same |
| `Parser.fsy` | 881 | OpName rule 1 (operators, Lambda only) | Add `None` (operators don't use annotations) |
| `Parser.fsy` | 886 | OpName rule 2 (operators, indented) | Add `None` |
| `Parser.fsy` | 896-897 | LetRecContinuation rule 1 | Capture type |
| `Parser.fsy` | 902-903 | LetRecContinuation rule 2 (indented) | Capture type |
| `Parser.fsy` | 910-911 | LetRecContinuation rule 3 (return type) | Capture type |
| `Parser.fsy` | 917-918 | LetRecContinuation rule 4 (indented + return type) | Capture type |
| `TypeCheck.fs` | 852-900 | Type checking LetRecDecl | Use annotation to constrain paramTy (key logic) |
| `Eval.fs` | 1632-1652 | Evaluation of LetRecDecl | Ignore type (runtime doesn't need it) |
| `Format.fs` | 323-329 | Pretty printing | Include type annotation in output |

**Total: 14 sites across 5 source files** (plus generated `Parser.fs` which is auto-generated from `.fsy`)

### LetRec Sites (Expression-level)

| File | Line | What It Does | Change Needed |
|------|------|-------------|---------------|
| `Ast.fs` | 79 | Type definition | Add `TypeExpr option` field |
| `Ast.fs` | 312 | `spanOf` | Update pattern (ignore new field) |
| `Parser.fsy` | 180-181 | Expression let rec rule 1 | Capture `None`/`Some ty` |
| `Parser.fsy` | 187-188 | Expression let rec rule 2 (indented) | Same |
| `Parser.fsy` | 196-197 | Expression let rec rule 3 (return type) | Same |
| `Parser.fsy` | 204-205 | Expression let rec rule 4 (indented + return type) | Same |
| `TypeCheck.fs` | 350 | `collectMatches` | Update destructure pattern |
| `TypeCheck.fs` | 483 | `collectTryWiths` | Update destructure pattern |
| `TypeCheck.fs` | 569 | `collectModuleRefs` | Update destructure pattern |
| `TypeCheck.fs` | 647 | `rewriteModuleAccess` | Update destructure pattern + reconstruct |
| `Bidir.fs` | 302-318 | Bidirectional type synthesis | Use annotation to constrain paramTy (key logic) |
| `Infer.fs` | 267-283 | Type inference | Use annotation to constrain paramTy (key logic) |
| `Eval.fs` | 1359-1366 | Evaluation | Ignore type (runtime) |
| `Format.fs` | 149-150 | Pretty printing | Include type annotation |

**Total: 14 sites across 6 source files**

## Key Logic Changes

### TypeCheck.fs - LetRecDecl (line 852)

Currently creates a fresh `paramTy` with no constraint:
```fsharp
let funcTypes =
    bindings |> List.map (fun (name, param, _body, _) ->
        let paramTy = Infer.freshVar()
        let retTy = Infer.freshVar()
        (name, param, TArrow(paramTy, retTy), paramTy))
```

Should become:
```fsharp
let funcTypes =
    bindings |> List.map (fun (name, param, paramTyOpt, _body, _) ->
        let paramTy =
            match paramTyOpt with
            | Some tyExpr -> elaborateTypeExpr tyExpr
            | None -> Infer.freshVar()
        let retTy = Infer.freshVar()
        (name, param, TArrow(paramTy, retTy), paramTy))
```

### Bidir.fs - LetRec (line 302)

Currently:
```fsharp
| LetRec (name, param, body, expr, span) ->
    let funcTy = freshVar()
    let paramTy = freshVar()
```

Should become:
```fsharp
| LetRec (name, param, paramTyOpt, body, expr, span) ->
    let funcTy = freshVar()
    let paramTy =
        match paramTyOpt with
        | Some tyExpr -> elaborateTypeExpr tyExpr
        | None -> freshVar()
```

### Infer.fs - LetRec (line 267)

Same pattern as Bidir.fs above.

### Parser.fsy - Extraction Pattern

Currently drops type:
```fsharp
| LambdaAnnot(p, _, b, _) -> LetRec($3, p, b, $8, span)
```

Should capture type:
```fsharp
| Lambda(p, b, _) -> LetRec($3, p, None, b, $8, span)
| LambdaAnnot(p, ty, b, _) -> LetRec($3, p, Some ty, b, $8, span)
```

And for `LetRecDecl` bindings:
```fsharp
| Lambda(p, b, _) -> [LetRecDecl(($3, p, None, b, ruleSpan parseState 3 6) :: $7, ...)]
| LambdaAnnot(p, ty, b, _) -> [LetRecDecl(($3, p, Some ty, b, ruleSpan parseState 3 6) :: $7, ...)]
```

### Eval.fs - Both LetRec and LetRecDecl

Evaluation ignores types -- just update the destructuring pattern to add `_` for the new field:
```fsharp
// LetRec (line 1359)
| LetRec (name, param, _, funcBody, inExpr, _) ->  // add _ for paramTyOpt

// LetRecDecl (line 1641)
bindings |> List.map (fun (name, param, _, body, _) ->  // add _ for paramTyOpt
```

## Common Pitfalls

### Pitfall 1: Forgetting to Update Generated Parser.fs
**What goes wrong:** Editing `Parser.fsy` without regenerating `Parser.fs` leaves stale code.
**How to avoid:** After editing `.fsy`, regenerate with `dotnet build` (or the fsyacc tool). The generated `Parser.fs` is checked in; it must be regenerated.

### Pitfall 2: LetRecContinuation Returns Binding Tuples, Not LetRecDecl
**What goes wrong:** `LetRecContinuation` returns `(string * string * Expr * Span) list`, not `LetRecDecl`. Must also update the continuation tuple type.
**How to avoid:** Change the continuation to return `(string * string * TypeExpr option * Expr * Span) list`.

### Pitfall 3: Missing a Pattern Match Site
**What goes wrong:** F# will emit a compiler error for incomplete patterns, but it's better to find them all upfront.
**How to avoid:** Use the complete site inventory above. After the AST change, `dotnet build` will catch any missed sites.

### Pitfall 4: The reconstructing site in `rewriteModuleAccess`
**What goes wrong:** `TypeCheck.fs` line 647 not only destructures `LetRec` but also reconstructs it. The new field must be threaded through.
**Current code:** `LetRec(n, p, rhs, body, s) -> LetRec(n, p, rewriteModuleAccess modules rhs, rewriteModuleAccess modules body, s)`
**Must become:** `LetRec(n, p, pty, rhs, body, s) -> LetRec(n, p, pty, rewriteModuleAccess modules rhs, rewriteModuleAccess modules body, s)`

### Pitfall 5: elaborateTypeExpr Availability
**What goes wrong:** `elaborateTypeExpr` converts `TypeExpr` AST to internal `Type`. It is defined in `Bidir.fs`. In `TypeCheck.fs` it is accessed via `Bidir.elaborateTypeExpr` or a local helper. Verify the exact import path.
**How to avoid:** Check how existing `LambdaAnnot` handling in `TypeCheck.fs` calls `elaborateTypeExpr`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| TypeExpr -> Type conversion | Custom converter | `elaborateTypeExpr` from Bidir.fs | Already handles all TypeExpr variants correctly |
| Fresh type variables | Manual counter | `Infer.freshVar()` | Existing infrastructure with proper scoping |

## Open Questions

1. **elaborateTypeExpr access from TypeCheck.fs**
   - What we know: Bidir.fs defines `elaborateTypeExpr`. TypeCheck.fs already imports and uses `Bidir.synth`.
   - What's unclear: Whether `elaborateTypeExpr` is public in Bidir.fs.
   - Recommendation: Check visibility; if private, make it public or use `Bidir.elaborateTypeExpr`.

2. **Test coverage**
   - What we know: There should be flt tests for `let rec f (x : int) y = ...` at module level.
   - What's unclear: Whether existing tests already cover this case (and just don't verify the type is enforced).
   - Recommendation: Write tests that verify type mismatch errors for annotated first params.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection of all files listed in the site inventory
- `Ast.fs` lines 79, 360 -- AST definitions
- `Parser.fsy` lines 170-210, 844-920 -- Parser rules
- `TypeCheck.fs` lines 340-900 -- Type checking logic
- `Bidir.fs` lines 169-318 -- Bidirectional type synthesis
- `Infer.fs` lines 266-283 -- Type inference
- `Eval.fs` lines 1355-1652 -- Evaluation
- `Format.fs` lines 149-329 -- Pretty printing

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - this is internal F# AST refactoring with no external dependencies
- Architecture: HIGH - the change is minimal and follows existing patterns (LambdaAnnot handling)
- Pitfalls: HIGH - all sites enumerated via grep; F# compiler will catch any missed patterns
- Site inventory: HIGH - exhaustive grep of entire src/ directory

**Research date:** 2026-03-31
**Valid until:** indefinite (internal codebase knowledge, no external dependencies)
