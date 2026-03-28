# Phase 45: Expression Sequencing - Research

**Researched:** 2026-03-28
**Domain:** LALR(1) grammar extension in fsyacc — adding semicolon sequencing without breaking list/record syntax
**Confidence:** HIGH

## Summary

This phase adds `e1; e2` expression sequencing to LangThree. The core challenge is that SEMICOLON is already used as a list element separator (`[1; 2; 3]`) and as a record field separator (`{x = 1; y = 2}`). Naively adding `Expr SEMICOLON Expr` to the `Expr` rule would create immediate LALR(1) shift/reduce conflicts in every list and record context.

The standard solution — used by OCaml's own grammar — is to introduce a separate `SeqExpr` nonterminal that wraps `Expr` and additionally allows sequencing. Sequencing is only valid at "statement positions": function bodies, match clause bodies, let-binding bodies, and the top-level start rule. Inside list literals and record expressions, only plain `Expr` is accepted, which does not include sequencing. This is context-free disambiguation by grammar structure, requiring no GLR parsing, no lexer hack, and no precedence trick.

The desugar approach is correct and aligned with existing patterns: `e1; e2` desugars to `LetPat(WildcardPat, e1, e2)` which is `let _ = e1 in e2`. No new AST node is needed. The evaluator, type checker, and all other passes need zero changes.

**Primary recommendation:** Add `SeqExpr` nonterminal (wraps Expr, adds sequencing rules), update all top-level `Expr` references in statement positions to `SeqExpr`, desugar to existing `LetPat(WildcardPat, ...)`.

## Standard Stack

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| fsyacc (`FSharp.Text.ParserGenerator`) | existing | LALR(1) parser generator | Already in use |
| fslex (`FSharp.Text.Lexing`) | existing | Lexer generator | Already in use |
| `SEMICOLON` token | existing | Already lexed | No lexer changes needed |

### Approach
| Approach | Verdict | Reason |
|----------|---------|--------|
| `SeqExpr` nonterminal (OCaml-style) | USE THIS | Solves conflict by grammar structure, zero ambiguity |
| Precedence declaration on SEMICOLON | DO NOT USE | fsyacc precedence doesn't cleanly resolve context-sensitive conflicts |
| Desugar to new `Seq` AST node | UNNECESSARY | `LetPat(WildcardPat, e1, e2)` already exists and does exactly this |
| Newline-based sequencing in IndentFilter | AVOID | Complicates IndentFilter, harder to test |

## Architecture Patterns

### Recommended Grammar Structure

The solution uses a dedicated nonterminal `SeqExpr` that:
- Accepts any `Expr`
- Adds `Expr SEMICOLON SeqExpr` (right-associative, natural for left-fold desugaring)
- Is only referenced where sequencing is semantically valid

```
SeqExpr:
    | Expr SEMICOLON SeqExpr
        { LetPat(WildcardPat(symSpan parseState 2), $1, $3, ruleSpan parseState 1 3) }
    | Expr SEMICOLON
        { $1 }   // trailing semicolon allowed, no-op
    | Expr
        { $1 }
```

The key: `SemiExprList` (for list literals) and `RecordFieldBindings` (for records) already use `Expr` — NOT `SeqExpr`. So `[1; 2; 3]` continues working unchanged. No conflict.

### Where to Replace `Expr` with `SeqExpr`

Only "statement positions" accept sequencing. Audit of `Parser.fsy` shows these positions must use `SeqExpr`:

1. **`start` rule** — top-level single expression entry point:
   ```
   start:
       | SeqExpr EOF   { $1 }
   ```

2. **`INDENT Expr DEDENT` blocks** — general indented expression blocks used as bodies:
   ```
   | INDENT SeqExpr DEDENT   { $2 }
   ```
   This covers lambda bodies, let RHS blocks, match clause bodies.

3. **`LET IDENT EQUALS Expr IN Expr`** — the `Expr` after `IN` is statement position:
   ```
   | LET IDENT EQUALS Expr IN SeqExpr
   | LET IDENT EQUALS INDENT Expr DEDENT IN SeqExpr
   ```
   (Similar pattern for all `IN body` positions)

4. **`IF Expr THEN Expr ELSE Expr`** — the then/else branches:
   ```
   | IF Expr THEN SeqExpr ELSE SeqExpr
   ```

5. **Match clause bodies** — the `Expr` after `->` in `MatchClauses`:
   ```
   | PIPE OrPattern ARROW SeqExpr
   | PIPE OrPattern WHEN Expr ARROW SeqExpr
   ```

6. **Lambda bodies** — the `Expr` after `->` in `FUN` rules.

7. **`TRY Expr WITH`** — the try body and match handlers.

**Do NOT change** `Expr` in these positions (they are separator/delimiter contexts):
- `LBRACKET Expr SEMICOLON SemiExprList RBRACKET` — list literal
- `SemiExprList` rule itself
- `RecordFieldBindings`
- `RecordPatFields`
- Function application arguments (`Atom`)
- Pattern positions

### Desugar Strategy

`e1; e2; e3` is right-associative in the grammar:
```
e1; (e2; e3)
= LetPat(WildcardPat, e1, LetPat(WildcardPat, e2, e3))
= let _ = e1 in (let _ = e2 in e3)
```

This is identical to `let _ = e1 in let _ = e2 in e3`, which already works. No new AST node, no evaluator changes, no type-checker changes.

### Indentation / Offside Rule (SEQ-03)

The IndentFilter already handles this correctly because:
- `BracketDepth` tracks `[`, `(`, `{` nesting and suppresses NEWLINE tokens inside brackets
- The SEMICOLON token inside `[1; 2; 3]` never triggers newline processing
- Multi-line sequencing in an indented block uses INDENT/DEDENT, not bare NEWLINEs

A typical pattern that must work:

```
let result =
    x <- x + 1
    y <- y + 2
    x + y
```

This uses the existing offside rule (IndentFilter emits implicit `IN` tokens). The `;` version:

```
let f () =
    print "a"; print "b"; x
```

All on one line — this works without any IndentFilter changes since it is inline.

Multi-line with `;` at start of continuation:

```
let f () =
    print "a"
    ; print "b"
    x
```

This is potentially tricky. Research shows: the NEWLINE between `print "a"` and `; print "b"` will go through normal IndentFilter NEWLINE processing. If `;` is at the same indentation level as the body, IndentFilter emits nothing (same-level continuation). This means the grammar will see: `print "a" SEMICOLON print "b" NEWLINE x`. This should parse correctly as two sequencing steps.

**Risk area:** If `;` appears at the SAME column as the `let` binding's offside column, IndentFilter may emit an implicit `IN`. This would split `e1; e2` incorrectly. Research indicates this is NOT a problem because the offside check fires for `col <= offsideCol` and the `;` line is indented INSIDE the let body (deeper than the `let` keyword column).

### Anti-Patterns to Avoid

- **Adding `Expr SEMICOLON Expr` directly to `Expr`:** Creates ambiguity in `[1; 2; 3]` — shift/reduce conflict: after reducing `1` in `[1`, seeing `;`, should the parser shift (start a sequence) or apply list context? LALR(1) cannot resolve without explicit disambiguation.
- **Using `%prec` on SEMICOLON to resolve the conflict:** The conflict is context-dependent (inside `[...]` vs outside), and precedence declarations are context-free. This would produce wrong parse trees for lists.
- **Creating a new `Seq` AST node:** Unnecessary complexity — `LetPat(WildcardPat, e1, e2)` is the correct desugar and is already eval'd, type-checked, formatted, and exhaustiveness-checked.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Sequence semantics | New `Seq(e1, e2)` AST node | `LetPat(WildcardPat, e1, e2)` | Already works, all passes handle it |
| Context tracking for `;` | Lexer state machine | Grammar nonterminal separation | Grammar structure is the right tool |
| LALR conflict resolution | GLR / backtracking | Separate `SeqExpr` nonterminal | Proven: this is how OCaml's parser.mly works |

**Key insight:** The `LetPat(WildcardPat, e1, e2)` desugar is validated by the existing `let _ = e1 in e2` feature (Phase 10). Every pass already handles it correctly. The type of the sequence is the type of `e2`, which is correct for sequencing.

## Common Pitfalls

### Pitfall 1: Forgetting to update INDENT/DEDENT block rule
**What goes wrong:** `print "a"; print "b"` in an indented block parses, but multi-line version with INDENT fails.
**Why it happens:** The rule `| INDENT Expr DEDENT { $2 }` uses `Expr` not `SeqExpr`, so sequencing inside indented blocks would be rejected.
**How to avoid:** Change EVERY `INDENT Expr DEDENT` that acts as a body to `INDENT SeqExpr DEDENT`.
**Warning signs:** Test `let f x = \n    e1; e2` fails while `let f x = e1; e2` succeeds.

### Pitfall 2: Changing `IN Expr` in let forms but missing some
**What goes wrong:** `let x = 1 in e1; e2` fails (body not SeqExpr) but `let x = 1 in (e1; e2)` works.
**Why it happens:** Many let forms are duplicated (with/without INDENT). Missing even one means inconsistent behavior.
**How to avoid:** Systematically audit ALL `IN Expr` positions. There are approximately 15 let/let-rec rules in `Parser.fsy` — each has an `in body` position.
**Warning signs:** Some let forms accept sequences, others don't — asymmetric failure.

### Pitfall 3: Trailing semicolon creates parse error
**What goes wrong:** User writes `e1; e2;` (trailing) and gets a parse error.
**Why it happens:** `SeqExpr` rule `Expr SEMICOLON SeqExpr` requires something after `;`.
**How to avoid:** Add explicit trailing-semicolon rule: `| Expr SEMICOLON { $1 }`.
**Warning signs:** Test file `e1;` fails or `[e1;]` still works but `e1;` as standalone fails.

### Pitfall 4: Record field separator broken
**What goes wrong:** `{x = 1; y = 2}` fails after changes.
**Why it happens:** `RecordFieldBindings` uses `Expr SEMICOLON` for separators. If `Expr` absorbs the `SEMICOLON` as sequence start, the record grammar breaks.
**How to avoid:** `RecordFieldBindings` uses `Expr` not `SeqExpr` — the rule already terminates at `SEMICOLON`. As long as you don't add sequencing to `Expr` (only to `SeqExpr`), records are safe.
**Warning signs:** `{x = 1; y = 2}` parse error after changes.

### Pitfall 5: Module-level sequencing vs. Decl structure
**What goes wrong:** User writes `let _ = e1; e2` at module level expecting `e2` to also execute.
**Why it happens:** `Decl` rules use `Expr` for the binding RHS, not `SeqExpr`. `let _ = e1; e2` would parse as two declarations: `let _ = e1` then orphaned `; e2`.
**How to avoid:** The `Decl` rules can also use `SeqExpr` for their RHS. This must be included.
**Warning signs:** Module-level multi-expression sequencing silently drops `e2`.

### Pitfall 6: SeqExpr in match clause RHS vs. ambiguity with multi-clause match
**What goes wrong:** In match arms, `| P -> e1; e2` — does `;` end the arm or continue it?
**Why it happens:** If `SeqExpr` is used for match arm bodies and consumes `;`, there's no ambiguity with `|` (the next arm starts with `|`). So this is fine.
**How to avoid:** Use `SeqExpr` for match arm bodies. The `|` of the next arm terminates the current body naturally since `SeqExpr` doesn't include `|`.
**Warning signs:** (No expected issues here, documented for completeness.)

## Code Examples

### SeqExpr Grammar Rule (Parser.fsy addition)
```fsharp
// Phase 45 (Expression Sequencing): SeqExpr wraps Expr, adds e1; e2 sequencing
// SEQ-01: e1; e2 evaluates e1 (discards result), then evaluates and returns e2
// SEQ-02: Multi-statement via right-associativity: e1; e2; e3 = e1; (e2; e3)
// Desugars to existing LetPat(WildcardPat, e1, e2) = let _ = e1 in e2
SeqExpr:
    | Expr SEMICOLON SeqExpr
        { LetPat(WildcardPat(symSpan parseState 2), $1, $3, ruleSpan parseState 1 3) }
    | Expr SEMICOLON
        { $1 }   // trailing semicolon: silently ignored
    | Expr
        { $1 }
```

### Updated start Rule
```fsharp
start:
    | SeqExpr EOF   { $1 }
```

### Updated INDENT block rule in Expr
```fsharp
// Change this:
| INDENT Expr DEDENT   { $2 }
// To this:
| INDENT SeqExpr DEDENT   { $2 }
```

### Updated Match Clause Rule
```fsharp
MatchClauses:
    | PIPE OrPattern ARROW SeqExpr                         { [($2, None, $4)] }
    | PIPE OrPattern WHEN Expr ARROW SeqExpr               { [($2, Some $4, $6)] }
    | PIPE OrPattern ARROW SeqExpr MatchClauses            { ($2, None, $4) :: $5 }
    | PIPE OrPattern WHEN Expr ARROW SeqExpr MatchClauses  { ($2, Some $4, $6) :: $7 }
```

### Updated Let Expression Rule (one example of many)
```fsharp
// Change:
| LET IDENT EQUALS Expr IN Expr  { Let($2, $4, $6, ruleSpan parseState 1 6) }
// To:
| LET IDENT EQUALS Expr IN SeqExpr  { Let($2, $4, $6, ruleSpan parseState 1 6) }
```

### Type-checker: No change needed
```fsharp
// Bidir.fs already handles LetPat(WildcardPat, e1, e2) correctly:
| LetPat (pat, bindingExpr, bodyExpr, span) ->
    // ... WildcardPat matches anything, returns unit type for e1, e2 type for result
```

### FLT Test Example (SEQ-01)
```
// --- Input:
let x = 0
let _ =
    print "hello"; print "world"
// --- Output:
hello
world
```

### FLT Test Example (SEQ-02)
```
// --- Input:
let result =
    let mut x = 0
    x <- 1; x <- x + 1; x <- x + 1
    x
// --- Output:
3
```

### FLT Test Example (SEQ-03 — inside list, no conflict)
```
// --- Input:
let lst = [1; 2; 3]
// --- Output:
[1; 2; 3]
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `let _ = e1 in e2` boilerplate | `e1; e2` sequence syntax | Phase 45 | Ergonomic imperative code |
| No sequencing in match arms | `| P -> e1; e2` works | Phase 45 | Cleaner mutable-variable code in match |

**Deprecated/outdated:**
- `let _ = e1 in e2` is NOT removed — it remains valid syntax. SEQ adds a shorthand. Programs using `let _ = ...` continue to work unchanged.

## Open Questions

1. **Sequencing at module level in `Decl` RHS**
   - What we know: `Decl` rules use `Expr` for the binding body. `let x = e1; e2` at module level could mean `(let x = e1); e2` (two decls) or `let x = (e1; e2)` (one decl with sequence body).
   - What's unclear: Should module-level `let _ = e1; e2` produce one `LetDecl` containing a sequence, or should the `;` separate two top-level statements?
   - Recommendation: Use `SeqExpr` in `Decl` RHS positions too, making `let _ = e1; e2` equivalent to `let _ = e1 in e2` in a single decl. This is the more useful behavior and matches the stated goal. The module-level pattern `let _ = e1 \n let _ = e2` already handles the "two separate statements" case.

2. **Interaction between IndentFilter's implicit IN and `;`**
   - What we know: The offside rule inserts `IN` tokens when a `let` body's next line is at the same indent as the `let` keyword. If a `;` line is at the same column as the enclosing `let`, IndentFilter might insert an `IN` before it.
   - What's unclear: Exact interaction when `;` appears at the offside column.
   - Recommendation: Ensure test cases cover this edge case. If a problem arises, IndentFilter may need to not fire the offside rule when the next non-whitespace token is `SEMICOLON`. This is a LOW-risk issue since `;` at the offside column is unusual formatting.

## Sources

### Primary (HIGH confidence)
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Parser.fsy` — all grammar rules
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/IndentFilter.fs` — offside rule logic
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Ast.fs` — `LetPat(WildcardPat, ...)` node
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Bidir.fs` — type checker handling of LetPat
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Eval.fs` — evaluator handling of LetPat
- `https://raw.githubusercontent.com/ocaml/ocaml/trunk/parsing/parser.mly` — `fun_seq_expr`, `seq_expr`, `expr_semi_list` grammar structure confirming the `SeqExpr` separation approach

### Secondary (MEDIUM confidence)
- Web search: OCaml semicolon disambiguation approach (confirmed by parser.mly inspection)

## Metadata

**Confidence breakdown:**
- LALR conflict analysis: HIGH — directly inspected all grammar rules, identified exact conflict sites
- SeqExpr solution: HIGH — verified against OCaml's production grammar (same approach)
- Desugar to LetPat: HIGH — LetPat(WildcardPat, ...) already fully implemented in all passes
- IndentFilter interaction: MEDIUM — analyzed logic but edge case around offside column needs runtime verification
- Module-level behavior: MEDIUM — an open question about desired semantics

**Research date:** 2026-03-28
**Valid until:** Stable (grammar approach for this codebase does not change rapidly)
