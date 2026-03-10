# Phase 10: Unit Type - Research

**Researched:** 2026-03-10
**Domain:** Functional language interpreter — unit type, parser extension, type inference
**Confidence:** HIGH

## Summary

Phase 10 adds the `()` unit value and `unit` type to the LangThree interpreter. The language is written in F# / .NET 10 using fslex/fsyacc, with Hindley-Milner type inference (bidirectional). The codebase already has the runtime value representation (`TupleValue []` / `TTuple []`) used for mutable field assignment results. The work is surgical: extend the parser to recognize `()` as a literal, wire `unit` as a type keyword, fix `formatType` to render `TTuple []` as `"unit"`, and add pattern/parameter support for `()`.

The standard approach for unit in ML-family interpreters is to treat it as a zero-element tuple — exactly what this codebase already does at the value/type level. No new AST node or Value variant is needed. All changes are surface-level wiring.

Sequencing (`let _ = expr1 in expr2` and semicolon sequencing) requires separate handling. `let _ = ...` already works via `WildcardPat`. Module-level sequencing works through multiple `LetDecl` declarations. Semicolon sequencing is out of scope unless explicitly listed as a requirement (UNIT-03 mentions it but the success criteria only show `let _ = record.field <- 42`).

**Primary recommendation:** Reuse `TTuple []` / `TupleValue []` as the unit representation throughout. Add only the minimal parser/lexer changes to surface this as `()` and `unit`. No new AST variant needed.

## Standard Stack

This phase operates entirely within the existing project stack. No new external libraries are needed.

### Core (existing, confirmed by codebase inspection)

| Tool | Version | Purpose | Notes |
|------|---------|---------|-------|
| F# / .NET 10 | 10 | Implementation language | All source files confirmed |
| FsLexYacc | embedded | Lexer (Lexer.fsl) + Parser (Parser.fsy) | Tokens defined in Parser.fsy |
| Expecto | embedded | Unit test framework | Tests in `tests/LangThree.Tests/` |

### No New Libraries Required

All changes are within the existing source tree:
- `Lexer.fsl` — add `"unit"` keyword token (or handle via `IDENT "unit"`)
- `Parser.fsy` — add `LPAREN RPAREN` atom rule, unit parameter, unit pattern
- `Type.fs` — fix `formatType` to handle `TTuple []` → `"unit"`
- `Elaborate.fs` — handle `TEName "unit"` → `TTuple []` (currently treated as fresh TVar)
- `Eval.fs` — handle `Tuple([], ...)` → `TupleValue []` (Tuple with empty list already works)
- `Infer.fs` / `Bidir.fs` — may need `()` variable lookup in Var case

## Architecture Patterns

### Pattern 1: `()` as Tuple with Empty List

**What:** The `()` literal maps to `Tuple([], span)` in the AST. The existing `Tuple` eval case handles this: `TupleValue (List.map eval [])` = `TupleValue []`. The type inference case for `Tuple([], _)` returns `TTuple []`.

**Current state:** `Tuple` AST node supports empty list. The eval case (`Tuple(exprs, _) -> TupleValue values`) works with empty list. The type inference case (`Tuple(exprs, span)`) folds over elements, producing `TTuple []` for empty.

**Implication:** No new AST variant. Add one grammar rule: `LPAREN RPAREN -> Tuple([], span)`.

**Example (target parser rule):**
```fsharp
// In Parser.fsy Atom section:
| LPAREN RPAREN  { Tuple([], ruleSpan parseState 1 2) }
```

### Pattern 2: `unit` as a Type Keyword

**What:** `unit` must elaborate to `TTuple []` in the type system. Currently `TEName "unit"` goes through `elaborateWithVars` which treats it as a fresh type variable (line 55-59 in Elaborate.fs). Must special-case.

**Option A (preferred):** Add `TYPE_UNIT` token in Parser.fsy (like `TYPE_INT`, `TYPE_BOOL`), add lexer rule `"unit" -> TYPE_UNIT`, add grammar rule `AtomicType: | TYPE_UNIT -> TETuple []`.

**Option B:** Keep `TEName "unit"` in the AST and special-case in `elaborateTypeExpr`.

Option A is cleaner because it's consistent with `int`, `bool`, `string` keyword tokens. Option B avoids changing the lexer. Since `unit` is a reserved type name in F#, Option A is the correct approach.

**Example:**
```fsharp
// Lexer.fsl:
| "unit"        { TYPE_UNIT }

// Parser.fsy token:
%token TYPE_UNIT

// Parser.fsy AtomicType:
| TYPE_UNIT  { TETuple [] }
```

### Pattern 3: `fun () -> body` Unit Parameter

**What:** Functions that take unit parameter. The parser currently handles `FUN IDENT ARROW Expr` for named parameters. For `fun () -> body`, the `()` must be parsed as a wildcard pattern binding (discarded parameter).

**Approach:** Add a `FUN LPAREN RPAREN ARROW Expr` parser rule that desugars to `Lambda("_unit", body, span)` where the parameter name is a fresh synthetic name. Alternatively, desugar to `Lambda("()", body, span)` and handle `"()"` specially in eval/type-checking.

The cleanest approach consistent with F# semantics: desugar `fun () -> body` to `Lambda("__unit", body, span)`. At call site, the argument must be `TupleValue []` (unit value), and the parameter binding is just discarded (never used). This keeps the existing Lambda eval unchanged.

For type inference, `fun () -> body` should produce type `unit -> bodyType`. This requires the parameter type be `TTuple []`.

**Recommended desugar:**
```fsharp
// Parser.fsy Factor/Expr:
| FUN LPAREN RPAREN ARROW Expr
    { LambdaAnnot("__unit", TETuple [], $5, ruleSpan parseState 1 5) }
```
Using `LambdaAnnot` with an explicit `TETuple []` type annotation ensures the type inference knows the parameter type without needing to match `"__unit"` specially.

### Pattern 4: `let _ = expr` Wildcard Sequencing

**What:** Module-level sequencing of side effects. `let _ = record.field <- 42` evaluates the mutation and discards the result.

**Current state:** `WildcardPat` is already defined in Pattern AST and handled in `matchPattern`. The `LetPat` node with `WildcardPat` works. Module-level `LetDecl` doesn't support patterns yet — it uses `LetDecl of name * Expr * Span`.

**Issue:** At module level, `let _ = expr` is currently not supported by `LetDecl(name, body, _)` because `_` is a wildcard, not a valid binding name. The parser rule `LET IDENT EQUALS Expr` would produce `LetDecl("_", body, _)` where `"_"` is a normal string. This would actually evaluate and bind to name `"_"` in the env, which is benign for sequencing purposes.

**Verdict:** `let _ = expr` at module level probably already works (binds `_` as a name in env, discarding effectively). Needs verification. If `UNDERSCORE` token is used instead of `IDENT "_"`, the parser rule fails. Checking: in the lexer, `'_'` returns `UNDERSCORE` token (not `IDENT "_"`). So `let _ = expr` would fail to parse with current grammar.

**Fix needed:** Add parser rule `LET UNDERSCORE EQUALS Expr` at `Decl` level, evaluating the body and not adding to env (or add to env as `"_"`).

### Pattern 5: `formatType TTuple []` → `"unit"`

**What:** The `formatType` function in `Type.fs` currently renders `TTuple []` as `""` (empty string from `String.concat " * " []`). This causes `--emit-type` to show an empty string for unit type.

**Fix:**
```fsharp
// Type.fs formatType:
| TTuple [] -> "unit"
| TTuple ts -> ts |> List.map formatType |> String.concat " * "
```

Same fix needed in `formatTypeNormalized`.

### Anti-Patterns to Avoid

- **Adding a new `TUnit` case to `Type`:** Unnecessary. `TTuple []` is the established representation. Adding `TUnit` requires updating all match expressions in Type.fs, Bidir.fs, Infer.fs, Unify.fs, etc. The decision is locked: use `TTuple []`.
- **Adding a `UnitValue` to `Value`:** Same issue. `TupleValue []` already works. The eval already returns it from `SetField`.
- **Adding `Unit` to `Expr` AST:** Not needed. `Tuple([], span)` suffices and keeps the parser/evaluator simpler.
- **Treating `Var("()", ...)` as unit:** The parser already uses `Var("()", ...)` as a placeholder in indented let continuation (line 94 of Parser.fsy). This means `"()"` would need to be in the type environment. Better to parse `()` as `Tuple([], span)` rather than a variable lookup.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Unit value representation | New `UnitValue` DU case | `TupleValue []` | Already used by SetField in Eval.fs line 483 |
| Unit type representation | New `TUnit` case in Type DU | `TTuple []` | Already used by Bidir.fs line 470 |
| Unit format string | Special-case across codebase | Fix `formatType` in one place | All formatters call `formatType` |
| Unit type elaboration | Scattered `TEUnit` handling | `TYPE_UNIT` token → `TETuple []` in parser | Consistent with existing int/bool/string keywords |

**Key insight:** The runtime and type system infrastructure for unit already exists. The only work is the surface wiring: parsing `()`, formatting `TTuple []` as `"unit"`, elaborating `unit` type keyword, and handling `let _ = ...` at module level.

## Common Pitfalls

### Pitfall 1: `Var("()")` Clash with Indented Let Continuation

**What goes wrong:** The parser uses `Var("()", symSpan parseState 6)` as the body placeholder when an indented `let x = <indent>expr<dedent>` is parsed without an `in` expression (line 94 of Parser.fsy). This means `"()"` must be in the environment, or the expression evaluates as an unbound variable.

**Why it happens:** The indented let form needs a continuation body. The parser uses `Var("()")` as a sentinel. If `()` is now a real expression (parsed as `Tuple([], ...)` from `LPAREN RPAREN`), this sentinel still works in eval (it would look up `"()"` in env and fail). However, if the planner changes `()` parsing to emit `Tuple([], span)`, the `Var("()")` sentinel in the old rule should be replaced with `Tuple([], span)`.

**How to avoid:** Replace `Var("()", symSpan parseState 6)` with `Tuple([], symSpan parseState 6)` when implementing `()` parsing. This makes `let x =\n    expr` truly unit-terminated.

**Warning signs:** Test `let x = 42` as indented fails at runtime with "Undefined variable: ()".

### Pitfall 2: `LPAREN RPAREN` Grammar Conflict

**What goes wrong:** The parser has `LPAREN Expr RPAREN` for parenthesized expressions and `LPAREN Expr COMMA ExprList RPAREN` for tuples. Adding `LPAREN RPAREN` as a new Atom rule could create reduce/reduce conflicts if not positioned correctly.

**Why it happens:** fsyacc is LALR(1). If the parser sees `LPAREN` followed by `RPAREN`, it needs to choose between "parenthesized expression" (which needs at least one Expr) and "unit literal". With look-ahead of `RPAREN`, it should unambiguously choose unit. No conflict expected in practice.

**How to avoid:** Add the `LPAREN RPAREN` rule as the first rule in `Atom` (before `LPAREN Expr RPAREN`). LALR parsers use the first applicable reduction, and since this requires no inner expression, there's no ambiguity.

### Pitfall 3: `fun () -> body` Type Inference for Unit Parameter

**What goes wrong:** If `fun () -> body` desugars to `Lambda("__unit", body, span)`, the parameter `__unit` gets a fresh type variable `'a`, not `TTuple []`. The type would be `'a -> bodyType` instead of `unit -> bodyType`.

**Why it happens:** `Lambda` with an unknown parameter uses `freshVar()` in both Bidir.synth and inferWithContext. The parameter type is not constrained unless the function is called with a unit value.

**How to avoid:** Use `LambdaAnnot("__unit", TETuple [], body, span)` for the desugar. This provides explicit type annotation ensuring the parameter type is `TTuple []`.

### Pitfall 4: `unit` Keyword as Type Conflicts with `unit` as Identifier

**What goes wrong:** Adding `"unit"` as a keyword token breaks any user code that uses `unit` as an identifier (variable name, constructor name). In F#, `unit` is a type keyword but can be used as a variable name with backtick escaping.

**Why it happens:** The lexer pattern matching on `"unit"` before `ident_start ident_char*` would consume the word as a keyword.

**How to avoid:** Since `unit` is a type keyword, add it only to the `AtomicType` grammar rule path (as `TYPE_UNIT`). In expression position, `unit` as an identifier still works through the existing `IDENT` token if the lexer returns `IDENT "unit"`. However, for consistency with `int`, `bool`, `string` keywords (which ARE lexer keywords), add `TYPE_UNIT` as a lexer keyword. The context (expression vs. type annotation) resolves usage. Users who use `unit` as a variable name should be able to do so — check if `int`, `bool`, `string` are blocked as variable names currently (they are: they're keywords in the lexer returning `TYPE_INT` etc., not `IDENT`). So `unit` as a variable name is already blocked in type position by analogy.

### Pitfall 5: `let _ = expr` at Module Level vs. Expression Level

**What goes wrong:** `LET UNDERSCORE EQUALS Expr` needs to work both at module level (`Decl`) and at expression level (`Expr`). At expression level, `let _ = expr1 in expr2` is `LetPat(WildcardPat, expr1, expr2, span)` and already works. At module level, `LetDecl("_", expr, span)` would work if `_` is parsed as `IDENT` — but `'_'` is lexed as `UNDERSCORE` token.

**How to avoid:** Add `LET UNDERSCORE EQUALS Expr` production to both `Decl` and `Expr` sections of the grammar. For `Decl`, produce `LetDecl("_", body, span)` — simple and safe. For `Expr`, the existing `LET TuplePattern EQUALS Expr IN Expr` with `WildcardPat` already handles `let _ = e1 in e2` if `TuplePattern` includes `UNDERSCORE`. Check: `TuplePattern` requires `LPAREN PatternList RPAREN` — it does NOT include bare `UNDERSCORE`. So `let _ = ...` at expression level also needs fixing.

**Better fix:** Add `LET Pattern EQUALS Expr IN Expr` as a general pattern-binding production in `Expr`, covering `let _ = e1 in e2` and `let () = e1 in e2`. And add `LET UNDERSCORE EQUALS Expr` to `Decl`.

## Code Examples

### Example 1: `()` Literal Parsing

```fsharp
// Parser.fsy Atom (add as first rule before LPAREN Expr RPAREN):
| LPAREN RPAREN  { Tuple([], ruleSpan parseState 1 2) }
```

### Example 2: `unit` Type Token

```fsharp
// Lexer.fsl (after "string" keyword, before identifiers):
| "unit"        { TYPE_UNIT }

// Parser.fsy tokens:
%token TYPE_UNIT

// Parser.fsy AtomicType:
| TYPE_UNIT  { TETuple [] }

// Parser.fsy Format.fs formatToken:
| Parser.TYPE_UNIT -> "TYPE_UNIT"
```

### Example 3: `TTuple []` Format Fix

```fsharp
// Type.fs formatType (add before TTuple ts case):
| TTuple [] -> "unit"
| TTuple ts -> ts |> List.map formatType |> String.concat " * "

// Same fix needed in formatTypeNormalized inner format function.
```

### Example 4: `fun () -> body` Desugar

```fsharp
// Parser.fsy (in Expr or Factor section):
| FUN LPAREN RPAREN ARROW Expr
    { LambdaAnnot("__unit", TETuple [], $5, ruleSpan parseState 1 5) }
```

Note: `LambdaAnnot` is already defined and handled in Bidir.synth. This ensures parameter type is `TTuple []`.

### Example 5: `let _ = expr` at Decl Level

```fsharp
// Parser.fsy Decl (add new production):
| LET UNDERSCORE EQUALS Expr
    { LetDecl("_", $4, ruleSpan parseState 1 4) }
| LET UNDERSCORE EQUALS INDENT Expr DEDENT
    { LetDecl("_", $5, ruleSpan parseState 1 6) }
```

At eval level, `LetDecl("_", body, _)` evaluates `body` (for side effects) and binds `"_"` to the result. The binding is harmless and enables sequencing.

### Example 6: `let _ = e1 in e2` at Expression Level

The pattern `let _ = e1 in e2` requires adding bare `WildcardPat` as a valid let-binding pattern in the `Expr` section:

```fsharp
// Parser.fsy Expr (add new production):
| LET UNDERSCORE EQUALS Expr IN Expr
    { LetPat(WildcardPat(ruleSpan parseState 2 2), $4, $6, ruleSpan parseState 1 6) }
```

`LetPat` with `WildcardPat` is already handled in both Eval.fs (line 156) and type inference.

### Example 7: Elaborate `TEName "unit"` Fallback

If `TYPE_UNIT` token approach is not used, alternatively handle in `elaborateWithVars`:

```fsharp
// Elaborate.fs elaborateWithVars (add case before TEName):
| TEName "unit" -> (TTuple [], vars)
| TEName _name -> ...
```

This is the fallback approach if the lexer keyword change is avoided.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `()` not parseable | `()` parses as `Tuple([], span)` | Phase 10 | Enables unit literal |
| `TTuple []` formats as `""` | `TTuple []` formats as `"unit"` | Phase 10 | `--emit-type` shows correct type |
| `unit` type not supported | `unit` keyword → `TTuple []` | Phase 10 | Type annotations with unit work |
| `let _ =` not parseable | `let _ = expr` at module level | Phase 10 | Side-effect sequencing works |

**Current facts about the codebase (verified by source inspection):**

- `SetField` in `Bidir.fs` line 470 already returns `TTuple []` as the type
- `SetField` in `Eval.fs` line 483 already returns `TupleValue []` as the value
- `Var("()")` is used in `Parser.fsy` line 94 as an indented let continuation placeholder
- `TEName "unit"` falls through to `elaborateWithVars` → fresh type variable (BUG to fix)
- `formatType (TTuple [])` currently returns `""` (empty string — BUG to fix)
- `WildcardPat` is fully supported in `matchPattern` and type inference
- `LambdaAnnot` is fully supported in `Bidir.synth` and `Eval.fs`

## Open Questions

1. **Semicolon sequencing (UNIT-03)**
   - What we know: `let _ = e1 in e2` works through `LetPat(WildcardPat, ...)`. UNIT-03 mentions "semicolon sequencing" as an alternative.
   - What's unclear: Is semicolon sequencing (`e1; e2`) required by the success criteria? The success criteria only show `let _ = record.field <- 42` (module-level, not expression-level semicolons).
   - Recommendation: Implement `let _ = expr` at both module and expression level. Skip true semicolon sequencing unless the planner determines it's required for the success criteria. Semicolons are currently used for record field separators; adding them as expression separators requires grammar work.

2. **`fun ()` as a parameter in `LambdaAnnot` vs. `Lambda`**
   - What we know: `LambdaAnnot("__unit", TETuple [], body, span)` gives the right type.
   - What's unclear: Whether `fun () -> body` should also be usable in positions where a `Lambda` is expected to check against an arrow type (e.g., `(fun () -> 42 : unit -> int)`).
   - Recommendation: The `check` function in `Bidir.fs` handles `Lambda` against `TArrow`. `LambdaAnnot` falls through to synthesis mode. This should be fine for the success criteria.

3. **`let _ = ...` in module-level eval output**
   - What we know: `Program.fs` prints the last `LetDecl` body's value. If the last declaration is `let _ = record.field <- 42`, it would print `()`.
   - What's unclear: Whether printing `()` for unit is desired, or if unit expressions should be silently ignored in output.
   - Recommendation: Make `formatValue (TupleValue [])` return `"()"` so the output is meaningful. Currently it would format as `"()"` via the existing `TupleValue values` case: `sprintf "(%s)" (String.concat ", " [])` = `"()"`. So this already works correctly.

## Sources

### Primary (HIGH confidence)
- Direct source code inspection: `Ast.fs`, `Type.fs`, `Eval.fs`, `Bidir.fs`, `Infer.fs`, `Elaborate.fs`, `Parser.fsy`, `Lexer.fsl`, `TypeCheck.fs`, `Program.fs`
- All findings verified by reading the actual implementation files

### Secondary (MEDIUM confidence)
- F# language reference for `unit` type semantics (consistent with what's implemented)
- Hindley-Milner type inference standard pattern: unit = zero-element tuple

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all tools confirmed from source files
- Architecture: HIGH — all patterns verified against actual source code
- Pitfalls: HIGH — specific line numbers cited from source files

**Research date:** 2026-03-10
**Valid until:** This research is based on the current codebase state. Valid until Phase 10 implementation begins (stable domain, no external dependencies changing).
