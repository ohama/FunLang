# Phase 63: Angle Bracket Generics - Research

**Researched:** 2026-03-30
**Domain:** FsLexYacc lexer/parser extension — type expression grammar
**Confidence:** HIGH

## Summary

Phase 63 adds angle bracket generic syntax (`Result<'a>`, `Map<string, int>`) to the type expression grammar. The codebase already has the `TEData` AST node for parameterized types, and already handles postfix type application (`'a option`, `int list`). This phase is purely a lexer/parser extension — no AST changes are needed because `TEData of name: string * args: TypeExpr list` already supports multi-argument generics.

The core challenge is an LALR(1) ambiguity: the `<` and `>` characters are already tokenized as `LT` and `GT` for comparison operators in expressions. Type expressions appear in a strictly separate grammatical context (after `:` in lambda annotations, after `of` in constructor declarations, in type alias RHS, etc.), so separate tokens `LANGLE` and `RANGLE` are needed. These tokens must be emitted by the lexer only when the parser is in a type context — but FsLexYacc lexers are context-free, so the practical solution is to add dedicated `LANGLE`/`RANGLE` tokens alongside `LT`/`GT` and use them only in type grammar rules.

The standard FsLexYacc approach is to reuse `LT` and `GT` in the type grammar (since the parser knows it is parsing a type expression and can resolve the shift/reduce choice unambiguously) or to add distinct `LANGLE`/`RANGLE` tokens. Reusing `LT`/`GT` in type rules is cleanest because the type grammar rules are already isolated from expression rules — the LALR(1) parser state machine will never be in an ambiguous state where `<` could be either comparison or angle bracket.

**Primary recommendation:** Reuse existing `LT` and `GT` tokens in the type grammar rules for angle bracket generics. Add `LANGLE`/`RANGLE` as aliases only if tests reveal conflicts; the parser state machine will disambiguate because type expression rules are entered from dedicated non-terminals (`TypeExpr`, `AtomicType`, etc.) that expression rules never enter from a state where `LT`/`GT` is ambiguous.

## Standard Stack

This phase uses the existing toolchain — no new libraries required.

### Core

| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| FSharp.Text.Lexing | (bundled with FsLexYacc) | Lexer runtime | Already in use |
| FSharp.Text.Parsing | (bundled with FsLexYacc) | Parser runtime | Already in use |
| Lexer.fsl | existing | Lexer spec — add `LANGLE`/`RANGLE` if needed | Single source of lexer truth |
| Parser.fsy | existing | Parser grammar — add angle bracket type rules | Single source of grammar truth |

### Supporting

| File | Purpose | When to Use |
|------|---------|-------------|
| `Ast.fs` | AST definition — `TEData` already exists | No changes needed |
| `Elaborate.fs` | Handles `TEData` already | No changes needed |
| `IndentFilter.fs` | Bracket depth tracking | Only if `LANGLE`/`RANGLE` need newline suppression |

**Installation:** No new packages needed.

## Architecture Patterns

### Recommended Project Structure

Changes are isolated to two files:

```
src/LangThree/
├── Lexer.fsl       # Add LANGLE/RANGLE token rules (if separate tokens chosen)
├── Parser.fsy      # Add AngleBracketType rule to AtomicType and AliasAtomicType
└── Ast.fs          # NO CHANGES — TEData already supports multi-arg generics
```

### Pattern 1: Reuse LT/GT in Type Grammar Rules

**What:** Add angle bracket type application directly using existing `LT` and `GT` tokens inside `AtomicType` and `AliasAtomicType` grammar rules.

**When to use:** Always, for this phase. The parser state machine will not confuse `LT` in a type rule with `LT` in an expression rule because those states are mutually exclusive in LALR(1).

**Example (Parser.fsy additions):**

```fsharp
// In AtomicType:
AtomicType:
    // ... existing rules ...
    | IDENT LT TypeArgList GT          { Ast.TEData($1, $3) }  // Result<'a>
    | IDENT LT TypeArgList GT TYPE_LIST { TEList(Ast.TEData($1, $3)) }  // Result<'a> list (postfix after angle bracket)

TypeArgList:
    | TypeExpr                         { [$1] }
    | TypeExpr COMMA TypeArgList       { $1 :: $3 }
```

The same rule addition is needed in `AliasAtomicType` (which omits bare `IDENT` to avoid conflict with ADT constructor syntax):

```fsharp
// In AliasAtomicType (used by type alias RHS):
AliasAtomicType:
    // ... existing rules ...
    | IDENT LT TypeArgList GT          { Ast.TEData($1, $3) }  // Result<'a> in aliases
```

### Pattern 2: Type Declaration with Angle Bracket Type Parameters (GEN-01)

**What:** `type Result<'a> = Ok of 'a | Error of string`

The current `TypeParams` rule accepts space-separated type variables: `type Result 'a = ...`. For angle bracket syntax, the `TypeDeclaration` rule needs an alternative head that accepts `IDENT LT TypeParams GT`.

**Example (Parser.fsy):**

```fsharp
TypeDeclaration:
    // New: angle bracket form: type Result<'a> = ...
    | TYPE IDENT LT TypeParams GT EQUALS Constructors TypeDeclContinuation
        { Ast.TypeDecl($2, $4, $7, ruleSpan parseState 1 8) :: $9 }
    | TYPE IDENT LT TypeParams GT EQUALS PIPE Constructors TypeDeclContinuation
        { Ast.TypeDecl($2, $4, $8, ruleSpan parseState 1 9) :: $10 }
    | TYPE IDENT LT TypeParams GT EQUALS INDENT Constructors DEDENT TypeDeclContinuation
        { Ast.TypeDecl($2, $4, $8, ruleSpan parseState 1 9) :: $11 }
    | TYPE IDENT LT TypeParams GT EQUALS INDENT PIPE Constructors DEDENT TypeDeclContinuation
        { Ast.TypeDecl($2, $4, $9, ruleSpan parseState 1 10) :: $12 }
    // ... existing rules (space-separated form) ...
```

Note: `TypeParams` is already `TYPE_VAR*` — this reuse works because the angle brackets delimit the params list.

### Pattern 3: Mixed Postfix/Angle Bracket Composition (GEN-03, success criterion 4)

**What:** `Result<'a> list` — angle bracket type followed by postfix type combinator.

This falls naturally out of the grammar if `IDENT LT TypeArgList GT` produces an `AtomicType`, because `AtomicType TYPE_LIST` and `AtomicType IDENT` already handle postfix application on any atomic type:

```
Result<'a> list
= AtomicType TYPE_LIST
= (IDENT LT TypeArgList GT) TYPE_LIST
= TEList(TEData("Result", [TEVar "'a"]))
```

No additional rules needed once the angle bracket `AtomicType` rule is in place.

### Anti-Patterns to Avoid

- **Adding LANGLE/RANGLE as separate lexer tokens without needing them:** The `<` and `>` characters are already `LT`/`GT`. Creating new tokens `LANGLE`/`RANGLE` requires duplicating lexer rules and complicates the `IndentFilter` bracket tracking. Only add these if testing reveals actual LALR(1) conflicts (which is unlikely given type/expression rule isolation).
- **Modifying `TEData` in Ast.fs:** The existing `TEData of name: string * args: TypeExpr list` already supports multi-argument generics. It is already handled in `Elaborate.fs` and `Format.fs`. Do not add new AST nodes.
- **Adding `TypeArgList` as a comma-separated rule inside angle brackets that allows empty:** Require at least one type argument (`TypeExpr COMMA TypeArgList | TypeExpr`). A bare `Name<>` with no type args is not valid F# and not needed for FunLexYacc compatibility.
- **Forgetting AliasAtomicType:** The `TypeAliasDeclaration` rule uses `AliasTypeExpr`/`AliasAtomicType`, which is a restricted subset that excludes bare `IDENT` (to prevent conflict with ADT constructor names). The angle bracket rule `IDENT LT TypeArgList GT` must be added to `AliasAtomicType` as well as `AtomicType`.
- **Forgetting IndentFilter bracket depth for LANGLE/RANGLE:** If you decide to add `LANGLE`/`RANGLE` as distinct tokens, you must add them to the `BracketDepth` tracking in `IndentFilter.fs`. Otherwise, a type expression like `fun (x : Result<\n    'a>) ->` on multiple lines would inject spurious NEWLINE/INDENT/DEDENT tokens inside the angle brackets.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Multi-arg parameterized type representation | New AST node | `TEData` (already exists) | Supports `name: string * args: TypeExpr list`; already elaborated in Elaborate.fs |
| Type arg parsing | Custom recursive function | Grammar rule `TypeArgList` in Parser.fsy | FsLexYacc handles LALR(1) correctly; keep logic in grammar |
| Distinguishing `<` as comparison vs type bracket | Context-sensitive lexer | Parser context via separate grammar rules | LALR(1) parser is already in different states for types vs expressions |

**Key insight:** The `TEData` AST node was designed from the beginning (Phase 4/GADT) to carry a list of type arguments. Angle bracket syntax is purely a surface syntax addition — the semantic representation already exists.

## Common Pitfalls

### Pitfall 1: LALR(1) Shift/Reduce Conflict on LT/GT

**What goes wrong:** Adding `IDENT LT TypeArgList GT` to `AtomicType` introduces a potential conflict if the parser could be in a state where an `IDENT` followed by `LT` is ambiguous between starting a comparison expression and starting a generic type.

**Why it happens:** `AtomicType` is reached only from type-expression non-terminals (`TypeExpr`, `ArrowType`, `TupleType`, `AtomicType`), which are only reached from type annotation positions in the grammar (after `COLON` in lambda params, after `OF` in constructors, after `EQUALS` in type alias, etc.). Expression rules never reference `TypeExpr` non-terminals. So the LALR(1) state when the parser is in `AtomicType` is completely disjoint from expression states where `LT` means comparison.

**How to avoid:** Use `LT`/`GT` directly. If FsLexYacc reports a shift/reduce conflict on `LT` or `GT`, check which grammar rule has the conflict and resolve by explicit precedence declaration or by splitting rules.

**Warning signs:** FsLexYacc emits conflict warnings during `dotnet build`. Zero warnings means the grammar is unambiguous.

### Pitfall 2: TypeDeclaration Rule Explosion

**What goes wrong:** The `TypeDeclaration` rule already has 4 variants (inline, inline+leading pipe, indented, indented+leading pipe). Adding angle bracket `type X<'a>` form doubles this to 8 variants.

**Why it happens:** FsLexYacc grammars require explicit alternatives for each syntactic combination.

**How to avoid:** Keep the multiplication to the minimum needed. The indented+leading-pipe form (`type T =\n    | A\n    | B`) is the most common for multi-constructor types. The inline form (`type T = A | B`) is also needed. Both must be supported for angle bracket form. That is 4 new rules (2 existing forms × 2 indent variants each).

**Warning signs:** Parser test failures on indented vs inline ADTs with type parameters.

### Pitfall 3: Forgetting the AliasTypeDeclaration Rule

**What goes wrong:** `type Result<'a> = 'a option` (a type alias) fails to parse while `type Result<'a> = Ok of 'a` (an ADT) works.

**Why it happens:** The parser uses a separate grammar (`AliasTypeExpr`/`AliasAtomicType`) for type alias RHS to avoid conflict. Also, the `TypeAliasDeclaration` rule head currently only accepts `TYPE IDENT TypeParams EQUALS`. It does not have an angle bracket form.

**How to avoid:** Also add `TYPE IDENT LT TypeParams GT EQUALS AliasTypeExpr` to the `TypeAliasDeclaration` rule.

**Warning signs:** `type Alias<'a> = ...` fails to parse even after ADT form works.

### Pitfall 4: Multi-line Type Arguments and Newline Suppression

**What goes wrong:** If the `TypeArgList` spans multiple lines (e.g., `Map<\n  string,\n  int>`), the `IndentFilter` will inject `NEWLINE` tokens that cause parse errors.

**Why it happens:** `IndentFilter` suppresses `NEWLINE` tokens only when `BracketDepth > 0`. The `BracketDepth` is incremented for `LBRACKET`, `LPAREN`, `LBRACE`, `DOTLBRACKET` but not for `LT`/`GT`.

**How to avoid:** For the current scope, multi-line type expressions are not required by any success criterion. Document that type arguments must be on a single line. If `LANGLE`/`RANGLE` tokens are used, add them to `IndentFilter` bracket depth tracking.

**Warning signs:** Parse error on `Map<\n  string,\n  int>`.

### Pitfall 5: `>` Closing Nested Types

**What goes wrong:** `Result<'a option>` — the `GT` after `'a option` might be consumed by expression comparison rules in some edge case.

**Why it happens:** `'a option` inside angle brackets produces `AtomicType TYPE_LIST` → `TEList(TEVar "'a")`. Then the closing `GT` needs to be parsed as closing the `IDENT LT TypeArgList GT` rule. Since type rules are only in type contexts, the parser state is unambiguous.

**How to avoid:** Use the grammar structure naturally; `AtomicType` produces fully resolved types before `GT` is consumed. No special handling needed.

**Warning signs:** Test `Result<'a option>` explicitly in flt tests.

## Code Examples

### Current AtomicType (Parser.fsy lines 492-504)

```fsharp
// Source: src/LangThree/Parser.fsy
AtomicType:
    | TYPE_UNIT                     { TETuple [] }
    | TYPE_INT                      { TEInt }
    | TYPE_BOOL                     { TEBool }
    | TYPE_STRING                   { TEString }
    | TYPE_CHAR                     { TEChar }
    | TYPE_VAR                      { TEVar($1) }
    | IDENT                         { TEName($1) }
    | AtomicType TYPE_LIST          { TEList($1) }
    | AtomicType IDENT              { Ast.TEData($2, [$1]) }  // postfix: int expr, 'a option
    | LPAREN TypeExpr RPAREN        { $2 }
```

**Addition for Phase 63:**

```fsharp
AtomicType:
    // ... existing rules above ...
    | IDENT LT TypeArgList GT       { Ast.TEData($1, $3) }   // angle bracket: Result<'a>
    | IDENT LT TypeArgList GT TYPE_LIST { TEList(Ast.TEData($1, $3)) }  // Result<'a> list

TypeArgList:
    | TypeExpr                      { [$1] }
    | TypeExpr COMMA TypeArgList    { $1 :: $3 }
```

### Current TypeParams rule (Parser.fsy lines 524-526)

```fsharp
// Source: src/LangThree/Parser.fsy
TypeParams:
    |                          { [] }
    | TYPE_VAR TypeParams      { $1 :: $2 }
```

This is reused as-is inside angle brackets for `type Result<'a> = ...` — only the `TypeDeclaration` rule head changes to wrap TypeParams in `LT ... GT`.

### Existing TEData handling in Elaborate.fs (lines 62-69)

```fsharp
// Source: src/LangThree/Elaborate.fs
| TEData (name, args) ->
    let canonical = match name with "option" -> "Option" | "result" -> "Result" | n -> n
    let folder (acc, env) t =
        let (ty, env') = elaborateWithVars env t
        (ty :: acc, env')
    let (revTypes, finalVars) = List.fold folder ([], vars) args
    (TData(canonical, List.rev revTypes), finalVars)
```

No changes needed — `TEData` with a list of args is already elaborated correctly.

### Existing TEData with multiple args in substTypeExprWithMap (Elaborate.fs lines 95-97)

```fsharp
// Source: src/LangThree/Elaborate.fs
| Ast.TEData(name, args) ->
    let canonical = match name with "option" -> "Option" | "result" -> "Result" | n -> n
    TData(canonical, List.map (substTypeExprWithMap paramMap) args)
```

Multi-arg generics like `Map<string, int>` would produce `TEData("Map", [TEString; TEInt])` which maps to `TData("Map", [TString; TInt])`. This works as-is.

## State of the Art

| Old Approach | Current Approach | Phase | Impact |
|--------------|------------------|-------|--------|
| Space-separated type params only: `type Result 'a = ...` | Add angle bracket alternative: `type Result<'a> = ...` | 63 | Both syntaxes coexist |
| Postfix type application only: `'a option`, `int list` | Add prefix angle bracket: `Result<'a>`, `Map<string, int>` | 63 | Both syntaxes coexist |
| Single-arg TEData only: `AtomicType IDENT` (one arg) | Multi-arg TEData via `TypeArgList`: `Map<string, int>` | 63 | TEData already supports lists |

**Deprecated/outdated:** Nothing is removed. Postfix type syntax (`'a option`) must remain working (GEN-03).

## Open Questions

1. **Should LANGLE/RANGLE be separate tokens or reuse LT/GT?**
   - What we know: Type expression rules are in separate LALR(1) states from expression rules; `LT`/`GT` in type positions will not conflict with comparison operators.
   - What's unclear: Whether FsLexYacc generates any shift/reduce warning when `LT` appears in both `AtomicType` and `Expr` rules.
   - Recommendation: Start with `LT`/`GT` reuse (simpler, fewer changes). If `dotnet build` reports conflicts, switch to separate `LANGLE`/`RANGLE` tokens.

2. **Do LANGLE/RANGLE need IndentFilter bracket depth tracking?**
   - What we know: Multi-line type argument lists (e.g., `Map<\n  string,\n  int>`) would fail without bracket depth tracking.
   - What's unclear: Whether any success criterion requires multi-line type arguments.
   - Recommendation: No success criterion requires multi-line type args. Skip bracket depth tracking for now; document as a known limitation.

3. **TypeAliasDeclaration: does it need angle bracket head?**
   - What we know: `type Name 'a = AliasTypeExpr` is the current alias rule. The ADT rules also need the angle bracket form.
   - What's unclear: Is `type Result<'a> = 'a option` (alias) required by GEN-01/GEN-02/GEN-03?
   - Recommendation: GEN-01 specifies `type Result<'a> = Ok of 'a | Error of string` (ADT, not alias). Add alias support as a bonus but it is not strictly required by the stated requirements.

## Sources

### Primary (HIGH confidence)

- Direct source code inspection of `src/LangThree/Parser.fsy` — grammar rules for `AtomicType`, `TypeExpr`, `TypeDeclaration`, `TypeAliasDeclaration`, `AliasAtomicType`
- Direct source code inspection of `src/LangThree/Ast.fs` — `TEData` AST node definition
- Direct source code inspection of `src/LangThree/Elaborate.fs` — `TEData` elaboration
- Direct source code inspection of `src/LangThree/Lexer.fsl` — `LT`/`GT` token rules
- Direct source code inspection of `src/LangThree/IndentFilter.fs` — `BracketDepth` tracking
- `.planning/REQUIREMENTS.md` — GEN-01, GEN-02, GEN-03 requirements
- `.planning/ROADMAP.md` — Phase 63 success criteria and planned tasks
- `survey/funlexyacc-type-annotation-incompatibility.md` — context on why this feature is needed

### Secondary (MEDIUM confidence)

- `survey/funlexyacc-gap-status-v9.md` — confirms `Result<'a>` angle bracket syntax as a remaining gap

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; purely lexer/parser changes
- Architecture: HIGH — TEData node already exists; grammar additions are straightforward
- Pitfalls: HIGH — LALR(1) conflict analysis is based on direct grammar inspection

**Research date:** 2026-03-30
**Valid until:** 2026-04-30 (stable domain — grammar changes rarely invalidate this)
