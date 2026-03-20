# Phase 16: Or-Patterns & String Patterns - Research

**Researched:** 2026-03-19
**Domain:** Pattern matching extensions (or-patterns, string constant patterns) in an LALR(1) ML-family language
**Confidence:** HIGH

## Summary

This phase adds two pattern matching features to LangThree: or-patterns (`| 1 | 2 | 3 -> expr`) and string constant patterns (`| "hello" -> expr`). Both are standard ML-family features with well-understood semantics.

The string pattern change is straightforward: add `StringConst of string` to the `Constant` DU, then propagate through all consumers (parser, eval, type checker, match compiler, exhaustiveness, format). The or-pattern is more nuanced due to an LALR(1) parsing ambiguity with `|` used as both match-clause separator and or-pattern combinator, requiring a grammar restructuring approach.

The key insight is that or-patterns in LALR(1) grammars require introducing a new nonterminal for "or-pattern" at the pattern level, and restructuring `MatchClauses` so the leading `PIPE` of a new clause is distinguishable from the `PIPE` within an or-pattern. The standard approach is: the `PIPE` between or-alternatives is parsed inside the pattern grammar, while the leading `PIPE` of a match clause remains in `MatchClauses`.

**Primary recommendation:** Add `OrPat of Pattern list` to AST, `StringConst of string` to Constant. Parse or-patterns as `Pattern PIPE Pattern` inside the `Pattern` rule. Expand or-patterns to multiple rows in MatchCompile and to OrPat in Exhaustive (already stubbed).

## Standard Stack

No new libraries needed. This phase extends existing modules only.

### Core Changes Required
| Module | Change | Purpose |
|--------|--------|---------|
| `Ast.fs` | Add `OrPat of Pattern list * Span`, `StringConst of string` | AST representation |
| `Parser.fsy` | New `OrPattern` nonterminal, STRING in Pattern | Parsing |
| `Eval.fs` | matchPattern for OrPat and StringConst | Runtime matching |
| `MatchCompile.fs` | Expand OrPat to multiple rows, StringConst ctor name | Decision tree |
| `Exhaustive.fs` | OrPat already stubbed in CasePat; handle in specializeRow | Exhaustiveness |
| `Infer.fs` | inferPattern for OrPat and StringConst | Type inference |
| `Bidir.fs` | Uses inferPattern from Infer.fs (no direct changes needed) | Type checking |
| `Format.fs` | formatPattern for OrPat and StringConst | AST display |

## Architecture Patterns

### Pattern 1: String Constant Pattern (StringConst)

**What:** Extend `Constant` DU with `StringConst of string`, then propagate through all pattern consumers.

**Where Constant is used (complete list of touch points):**

1. **Ast.fs line 130:** `Constant` DU -- add `| StringConst of string`
2. **Parser.fsy line 240-242:** Pattern rule for constants -- add `| STRING { ConstPat(StringConst($1), symSpan parseState 1) }`
3. **Eval.fs line 274-277:** `matchPattern` for ConstPat -- add `| ConstPat(StringConst s, _), StringValue v -> if s = v then Some [] else None`
4. **MatchCompile.fs line 48:** `patternToConstructor` -- add `| ConstPat(StringConst s, _) -> Some("#str_" + s, 0)` (using prefix to avoid collision)
5. **MatchCompile.fs line 167-173:** `matchesConstructor` -- add `| StringValue s, c when c.StartsWith("#str_") -> s = c.Substring(5)`
6. **Infer.fs line 81-85:** `inferPattern` for ConstPat -- add `| ConstPat(StringConst _, _) -> (Map.empty, TString)`
7. **Exhaustive.fs line 297-309:** `astPatToCasePat` -- add `| Ast.ConstPat(Ast.StringConst s, _) -> ConstructorPat("#str_" + s, [])`
8. **Format.fs line 206-208:** `formatPattern` for ConstPat -- add `| Ast.StringConst s -> sprintf "ConstPat (StringConst \"%s\")" s`

**Key design:** Use `#str_` prefix (not `#string_`) in MatchCompile constructor names to keep them short and distinct from `#int_` and `#bool_`.

### Pattern 2: Or-Pattern AST Representation

**What:** Add `OrPat of Pattern list * Span` to the `Pattern` DU.

**Why `Pattern list` (not binary `Pattern * Pattern`):** A list representation avoids deep nesting for `| 1 | 2 | 3 | 4` which would otherwise become `OrPat(OrPat(OrPat(1, 2), 3), 4)`. The list `OrPat([1; 2; 3; 4])` is simpler to process in all consumers.

**Restriction:** For simplicity, LangThree or-patterns will NOT support variable bindings in alternatives. Only constant patterns, constructor patterns (nullary), wildcard, and empty-list patterns are allowed within or-alternatives. This avoids the complexity of checking that all alternatives bind the same variables with the same types. The restriction should be enforced during type checking with a clear error message.

### Pattern 3: Or-Pattern Parsing in LALR(1)

**What:** The `|` token is already used as a match clause separator (`PIPE`). We need to distinguish `| 1 | 2 -> expr` (or-pattern) from `| 1 -> expr1 | 2 -> expr2` (two clauses).

**The solution:** Restructure the grammar so that or-patterns are parsed at the pattern level. The key insight is that `PIPE` within a match clause (after the leading `PIPE`) and before `ARROW` is an or-pattern separator, while `PIPE` after `expr` starts a new clause.

**Grammar change:**

```
// Current:
MatchClauses:
    | PIPE Pattern ARROW Expr                         { ... }
    | PIPE Pattern ARROW Expr MatchClauses            { ... }
    // (plus when-guard variants)

// New:
MatchClauses:
    | PIPE OrPattern ARROW Expr                       { ... }
    | PIPE OrPattern WHEN Expr ARROW Expr             { ... }
    | PIPE OrPattern ARROW Expr MatchClauses          { ... }
    | PIPE OrPattern WHEN Expr ARROW Expr MatchClauses { ... }

OrPattern:
    | Pattern                    { $1 }
    | Pattern PIPE OrPattern     {
        match $3 with
        | OrPat(pats, _) -> OrPat($1 :: pats, ruleSpan parseState 1 3)
        | p -> OrPat([$1; p], ruleSpan parseState 1 3)
      }
```

**Why this works in LALR(1):** After parsing a `Pattern`, the parser sees `PIPE`. If the next tokens can start another `Pattern` (before seeing `ARROW`), it's an or-pattern. If the next token is `ARROW`, the current pattern is complete. The LALR(1) lookahead resolves this: after `Pattern PIPE`, if the next token is a pattern start token (NUMBER, STRING, IDENT, LPAREN, UNDERSCORE, LBRACKET, LBRACE, TRUE, FALSE), it continues as an or-pattern. The `PIPE` in `OrPattern` has right-recursive structure which is natural for LALR(1).

**Potential shift/reduce concern:** The `PIPE` token appears in both `OrPattern` (right-recursive: `Pattern PIPE OrPattern`) and `MatchClauses` (leading `PIPE`). After reducing `Expr` in `PIPE OrPattern ARROW Expr`, seeing another `PIPE` should start a new `MatchClauses` rule. The parser can distinguish because after `Expr`, a `PIPE` starts the `MatchClauses` continuation, not an `OrPattern` continuation.

**IMPORTANT:** If the LALR(1) approach causes shift/reduce conflicts, the fallback is to parse or-patterns only inside parentheses: `| (1 | 2 | 3) -> expr`. This is simpler but less ergonomic. Try the unparenthesized version first.

### Pattern 4: Or-Pattern in Decision Tree (MatchCompile.fs)

**What:** Expand or-patterns into multiple match rows before compilation.

**How:** In `compileMatch`, before creating `MatchRow` entries, walk each clause's pattern. If the top-level pattern is `OrPat(pats, _)`, expand it into N rows (one per alternative) all sharing the same guard and body. If an or-pattern is nested (e.g., `Some (1 | 2)`), the expansion must happen during `pushVarBindings` or `splitClauses`.

**Recommended approach -- expand at entry point:**

```fsharp
let expandOrPatterns (clauses: MatchClause list) : MatchClause list =
    clauses |> List.collect (fun (pat, guard, body) ->
        match pat with
        | OrPat(pats, _) -> pats |> List.map (fun p -> (p, guard, body))
        | _ -> [(pat, guard, body)])
```

Call this in `compileMatch` before building rows. For nested or-patterns, add handling in `patternToConstructor` and `extractSubPatterns`:

```fsharp
// In patternToConstructor, OrPat should not appear after expansion
// But defensively: treat as None (wildcard-like)
| OrPat _ -> None

// In extractSubPatterns:
| OrPat _ -> []
```

For truly nested or-patterns (e.g., `Some (1 | 2)`), the expansion should recursively flatten: `Some (1 | 2)` becomes two rows: `Some 1` and `Some 2`. This can be done with a recursive expansion function:

```fsharp
let rec expandPattern (pat: Pattern) : Pattern list =
    match pat with
    | OrPat(pats, _) -> pats |> List.collect expandPattern
    | ConstructorPat(name, Some arg, span) ->
        expandPattern arg |> List.map (fun a -> ConstructorPat(name, Some a, span))
    | TuplePat(pats, span) ->
        let expanded = pats |> List.map expandPattern
        let combinations = cartesianProduct expanded
        combinations |> List.map (fun combo -> TuplePat(combo, span))
    | ConsPat(h, t, span) ->
        let hExpanded = expandPattern h
        let tExpanded = expandPattern t
        [for h' in hExpanded do for t' in tExpanded -> ConsPat(h', t', span)]
    | other -> [other]
```

**Simpler first pass:** Only support top-level or-patterns (not nested). The expansion is trivial. Nested or-patterns can be added later if needed.

### Pattern 5: Or-Pattern in Exhaustiveness (Exhaustive.fs)

**What:** The `CasePat` DU already has `OrPat of CasePat list` (line 29). The `specializeRow` function currently returns `None` for OrPat (line 92). The `useful` function handles OrPat correctly (lines 154-156).

**Changes needed:**
1. `specializeRow`: Expand or-pattern -- if any alternative matches the constructor, include that row.
2. `astPatToCasePat`: Convert `Ast.OrPat` to `Exhaustive.OrPat`.

```fsharp
// In specializeRow, replace the OrPat case:
| OrPat pats ->
    // Or-pattern: try each alternative, take first that matches
    pats |> List.tryPick (fun altPat ->
        specializeRow ctor (altPat :: rest))

// In astPatToCasePat:
| Ast.OrPat(pats, _) ->
    OrPat(pats |> List.map astPatToCasePat)
```

### Pattern 6: Or-Pattern in Type Checking (Infer.fs)

**What:** All alternatives in an or-pattern must have the same type. No variable bindings allowed (LangThree restriction).

```fsharp
// In inferPattern:
| OrPat(pats, span) ->
    // Check no variables in or-pattern alternatives
    let rec hasVarBinding p =
        match p with
        | VarPat _ -> true
        | TuplePat(ps, _) -> List.exists hasVarBinding ps
        | ConsPat(h, t, _) -> hasVarBinding h || hasVarBinding t
        | ConstructorPat(_, Some arg, _) -> hasVarBinding arg
        | OrPat(ps, _) -> List.exists hasVarBinding ps
        | _ -> false
    if List.exists hasVarBinding pats then
        failwith "Variables not allowed in or-patterns"
    // Infer type of first pattern, unify rest with it
    let env0, ty0 = inferPattern ctorEnv (List.head pats)
    for p in List.tail pats do
        let _, tyi = inferPattern ctorEnv p
        let _ = Unify.unify ty0 tyi
        ()
    (env0, ty0)  // env0 should be empty (no bindings)
```

### Anti-Patterns to Avoid
- **Do NOT make or-patterns introduce variable bindings.** OCaml/F# require all alternatives to bind the same names with the same types. This is complex. LangThree should forbid it.
- **Do NOT use a separate token for or-pattern `|`.** Reuse `PIPE`. The grammar structure disambiguates.
- **Do NOT handle string patterns as special cases in MatchCompile.** Use the same `#prefix_value` naming convention as IntConst and BoolConst.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Or-pattern exhaustiveness | Custom exhaustiveness logic for or-patterns | Existing `OrPat` in CasePat DU (already stubbed) | The Maranget `useful` function already handles OrPat correctly at lines 154-156 |
| String equality in matching | Custom string comparison | Reuse `matchesConstructor` with `#str_` prefix | Same pattern as `#int_` and `#bool_` |

## Common Pitfalls

### Pitfall 1: LALR(1) Shift/Reduce Conflict with PIPE
**What goes wrong:** Adding `Pattern PIPE Pattern` to the grammar creates ambiguity with `PIPE` as match clause separator.
**Why it happens:** After reducing a complete `Expr` in `PIPE Pattern ARROW Expr`, the parser sees `PIPE` and must decide: is this starting a new match clause or continuing an or-pattern?
**How to avoid:** The grammar structure ensures `PIPE` after `Expr` always starts a new clause (since `OrPattern` only appears between `PIPE` and `ARROW`). If FsLexYacc still reports conflicts, use parenthesized or-patterns as fallback: `| (1 | 2 | 3) -> expr`.
**Warning signs:** FsLexYacc reports shift/reduce conflicts during build.

### Pitfall 2: Forgetting to Update patternSpanOf
**What goes wrong:** Adding `OrPat` to the Pattern DU without updating `patternSpanOf` in Ast.fs causes incomplete match warnings or runtime errors.
**How to avoid:** Add `| OrPat(_, s) -> s` to `patternSpanOf`.

### Pitfall 3: String Pattern Constructor Name Collision
**What goes wrong:** If a string value contains characters that interfere with the `#str_` prefix scheme (e.g., what if the string itself starts with `#str_`?).
**Why it happens:** The `matchesConstructor` function uses `StartsWith` for prefix matching.
**How to avoid:** The `#str_` prefix is only used internally for constructor names. Since `matchesConstructor` does exact equality after the prefix (`s = c.Substring(5)`), and the string value itself is the suffix, this is safe. No real collision risk.

### Pitfall 4: Or-Pattern with When Guards
**What goes wrong:** `| 1 | 2 when x > 0 -> expr` -- does the guard apply to `2` only or to `1 | 2`?
**How to avoid:** In F#/OCaml, the guard applies to the entire or-pattern. The grammar structure in `MatchClauses` ensures this: `PIPE OrPattern WHEN Expr ARROW Expr` groups the guard with the whole or-pattern.

### Pitfall 5: Nested Or-Patterns in Decision Tree
**What goes wrong:** If or-patterns appear nested inside constructor patterns (e.g., `Some (1 | 2)`), the simple top-level expansion misses them.
**How to avoid:** For the first implementation, only support top-level or-patterns. Document this limitation. The type checker should reject nested or-patterns with a clear error, or implement recursive expansion.

### Pitfall 6: Exhaustive.fs astPatToCasePat Treats ConstPat as Wildcard
**What goes wrong:** Currently `astPatToCasePat` (line 308) maps ALL `ConstPat` to `WildcardPat`. This means int/bool/string constant patterns are invisible to exhaustiveness checking for non-ADT types. This is a pre-existing limitation, not introduced by this phase.
**How to avoid:** This is acceptable for now. LangThree only checks exhaustiveness for ADT-typed scrutinees. String/int/bool exhaustiveness would require infinite constructor sets.

## Code Examples

### Adding StringConst to Constant DU (Ast.fs)
```fsharp
and Constant =
    | IntConst of int
    | BoolConst of bool
    | StringConst of string  // Phase 16: String constant pattern
```

### Adding OrPat to Pattern DU (Ast.fs)
```fsharp
and Pattern =
    | VarPat of string * span: Span
    | TuplePat of Pattern list * span: Span
    | WildcardPat of span: Span
    | ConsPat of Pattern * Pattern * span: Span
    | EmptyListPat of span: Span
    | ConstPat of Constant * span: Span
    | ConstructorPat of name: string * argPattern: Pattern option * span: Span
    | RecordPat of fields: (string * Pattern) list * span: Span
    | OrPat of Pattern list * span: Span  // Phase 16: Or-pattern
```

### Parser Grammar for Or-Pattern
```
MatchClauses:
    | PIPE OrPattern ARROW Expr                         { [($2, None, $4)] }
    | PIPE OrPattern WHEN Expr ARROW Expr               { [($2, Some $4, $6)] }
    | PIPE OrPattern ARROW Expr MatchClauses            { ($2, None, $4) :: $5 }
    | PIPE OrPattern WHEN Expr ARROW Expr MatchClauses  { ($2, Some $4, $6) :: $7 }

OrPattern:
    | Pattern                    { $1 }
    | Pattern PIPE OrPattern     {
        match $3 with
        | Ast.OrPat(pats, _) -> Ast.OrPat($1 :: pats, ruleSpan parseState 1 3)
        | p -> Ast.OrPat([$1; p], ruleSpan parseState 1 3)
      }
```

### String Pattern in Parser (add to Pattern rule)
```
Pattern:
    // ... existing rules ...
    | STRING  { ConstPat(StringConst($1), symSpan parseState 1) }
```

### Eval.fs matchPattern Extension
```fsharp
// String constant pattern
| ConstPat(StringConst s, _), StringValue v ->
    if s = v then Some [] else None

// Or-pattern: try each alternative
| OrPat(pats, _), value ->
    pats |> List.tryPick (fun p -> matchPattern p value)
```

### MatchCompile.fs Extensions
```fsharp
// In patternToConstructor:
| ConstPat(StringConst s, _) -> Some("#str_" + s, 0)
| OrPat _ -> None  // Should not appear after expansion

// In matchesConstructor:
| StringValue s, c when c.StartsWith("#str_") -> s = c.Substring(5)

// Top-level or-pattern expansion:
let expandOrPatterns (clauses: MatchClause list) : MatchClause list =
    clauses |> List.collect (fun (pat, guard, body) ->
        match pat with
        | OrPat(pats, _) -> pats |> List.map (fun p -> (p, guard, body))
        | _ -> [(pat, guard, body)])
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No string patterns | Add StringConst | Phase 16 | Enables string matching |
| No or-patterns | Add OrPat | Phase 16 | Multiple patterns share one body |
| Exhaustive.OrPat stubbed | Fully implemented | Phase 16 | Or-patterns work in exhaustiveness |

**Pre-existing limitation:** `astPatToCasePat` maps `ConstPat` to `WildcardPat`, so constant patterns (int, bool, string) are invisible to exhaustiveness checking. This is acceptable -- exhaustiveness for infinite types (int, string) is undecidable in general.

## Open Questions

1. **Nested or-patterns (e.g., `Some (1 | 2)`):**
   - What we know: Top-level or-patterns are straightforward to expand.
   - What's unclear: Whether nested or-patterns should be supported in Phase 16 or deferred.
   - Recommendation: Start with top-level only. Add nested support if tests require it. The recursive expansion algorithm is well-understood but adds complexity.

2. **LALR(1) conflict resolution:**
   - What we know: The grammar structure should work because `OrPattern` only appears between leading `PIPE` and `ARROW` in `MatchClauses`.
   - What's unclear: Whether FsLexYacc (FsYacc) will accept this without conflicts.
   - Recommendation: Try the unparenthesized grammar first. If conflicts arise, fall back to parenthesized or-patterns, or add a `%left PIPE` precedence declaration to resolve the shift/reduce in favor of shift (continue or-pattern).

3. **Or-patterns with MINUS (negative numbers):**
   - What we know: `| -1 | -2 -> expr` -- negative number patterns are parsed as `MINUS NUMBER` which requires `Factor` or a special negative constant rule.
   - What's unclear: Whether the current grammar supports negative constant patterns at all.
   - Recommendation: Check if `| -1 -> expr` works today. If not, this is a separate issue, not specific to or-patterns.

## Sources

### Primary (HIGH confidence)
- Codebase analysis of all 8 affected modules (Ast.fs, Parser.fsy, Lexer.fsl, Eval.fs, MatchCompile.fs, Exhaustive.fs, Infer.fs, Format.fs)
- Existing `Exhaustive.CasePat.OrPat` stub (line 29) confirms or-patterns were anticipated in the design
- Jules Jacobs pattern matching compilation algorithm as implemented in MatchCompile.fs

### Secondary (MEDIUM confidence)
- F# and OCaml language specifications for or-pattern semantics (no variable bindings in LangThree simplification)
- LALR(1) grammar analysis for `PIPE` disambiguation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new dependencies, straightforward DU extensions
- Architecture: HIGH - All touch points identified, patterns follow existing conventions
- Pitfalls: HIGH - LALR(1) conflict is the main risk, with clear fallback strategy
- Parser grammar: MEDIUM - The or-pattern grammar should work but needs build verification

**Research date:** 2026-03-19
**Valid until:** Indefinite (language implementation patterns are stable)
