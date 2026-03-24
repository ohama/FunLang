# Phase 27: List Syntax Completion - Research

**Researched:** 2026-03-24
**Domain:** fsyacc grammar rules, IndentFilter token transformation, pattern desugaring
**Confidence:** HIGH

## Summary

Phase 27 implements three independent list syntax improvements: multi-line list literals (SYN-02), trailing semicolons in list literals (SYN-03), and list literal patterns in match expressions (SYN-04). All three map to concrete, contained changes in `Parser.fsy`, `IndentFilter.fs`, and the test suite. No downstream pipeline changes are needed for any of the three requirements.

The hardest requirement is SYN-02. The root cause is that `IndentFilter` has no bracket depth awareness — when it encounters a `NEWLINE` token inside `[...]`, it emits `INDENT`/`DEDENT` tokens just like any other newline. The parser's `SemiExprList` grammar rule does not accept `INDENT`/`DEDENT`, so the parse fails. The correct fix is to add a `BracketDepth: int` field to `FilterState` in `IndentFilter.fs`, increment it on `LBRACKET`, decrement on `RBRACKET`, and suppress `INDENT`/`DEDENT` emission (pass `NEWLINE` through as nothing) when depth > 0.

SYN-03 (trailing semicolon) is a pure grammar change: add one production to `SemiExprList` that accepts a trailing `SEMICOLON` before the end of the list. SYN-04 (list literal patterns) is also pure grammar, desugaring `[x; y; z]` patterns into nested `ConsPat`/`EmptyListPat` chains at parse time so no downstream pipeline changes are needed.

**Primary recommendation:** Fix IndentFilter bracket depth first (SYN-02), then add trailing semicolon grammar (SYN-03), then add list literal pattern grammar (SYN-04). Each change is independent and testable in isolation.

## Standard Stack

### Core
| Component | File | Purpose | Why Standard |
|-----------|------|---------|--------------|
| fsyacc | `Parser.fsy` | LALR(1) grammar rules | Only parser in use |
| IndentFilter | `IndentFilter.fs` | NEWLINE → INDENT/DEDENT conversion | Offside rule implementation |
| FSharp.Text.Lexing | lexer runtime | Token position tracking | Already used throughout |

### Supporting
| Component | File | Purpose | When to Use |
|-----------|------|---------|-------------|
| `FilterState` record | `IndentFilter.fs` | Carries bracket depth alongside indent stack | For SYN-02 |
| `SemiExprList` rule | `Parser.fsy` | Semicolon-separated list elements | Already exists, needs trailing-semicolon variant |
| `ConsPat` / `EmptyListPat` | `Ast.fs` | List pattern nodes | Already in AST — reuse for SYN-04 desugaring |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| IndentFilter bracket depth | Grammar rules accepting INDENT/DEDENT in lists | Grammar approach creates dozens of new rules and shift/reduce conflicts |
| Desugar list patterns at parse time | Add `ListLitPat` to AST | Desugaring at parse time is zero downstream impact; new AST node would touch Eval, TypeCheck, Infer, MatchCompile, Exhaustive, Format |

## Architecture Patterns

### Recommended Project Structure
```
src/LangThree/
├── IndentFilter.fs      # Add BracketDepth to FilterState (SYN-02)
├── Parser.fsy           # Add trailing semicolon + list pattern rules (SYN-03, SYN-04)
tests/flt/
├── expr/list/           # Add multi-line and trailing-semicolon expr tests
└── file/list/           # Add file-mode tests for multi-line lists
tests/flt/
└── file/match/          # Add list pattern match tests
tests/LangThree.Tests/
└── IndentFilterTests.fs # Add bracket depth suppression unit tests
```

### Pattern 1: IndentFilter Bracket Depth Tracking (SYN-02)

**What:** Add a `BracketDepth: int` field to `FilterState`. Increment on `LBRACKET`, `LPAREN`, `LBRACE`; decrement on `RBRACKET`, `RPAREN`, `RBRACE`. When `BracketDepth > 0`, suppress INDENT/DEDENT for any NEWLINE encountered (emit nothing).

**When to use:** Required for SYN-02. Also fixes incidental cases where multi-line expressions inside parens and braces trigger spurious indentation.

**Example:**
```fsharp
// In FilterState record definition (IndentFilter.fs):
type FilterState = {
    IndentStack: int list
    LineNum: int
    Context: SyntaxContext list
    JustSawMatch: bool
    JustSawTry: bool
    JustSawModule: bool
    PrevToken: Parser.token option
    BracketDepth: int   // NEW: depth of [...], (...), {...} nesting
}

// Initial state update:
let initialState = { ...; BracketDepth = 0 }

// In the filter seq { ... } loop, handle bracket tokens:
| Parser.LBRACKET | Parser.LPAREN | Parser.LBRACE ->
    state <- { state with BracketDepth = state.BracketDepth + 1; PrevToken = Some token }
    yield token

| Parser.RBRACKET | Parser.RPAREN | Parser.RBRACE ->
    state <- { state with BracketDepth = state.BracketDepth - 1; PrevToken = Some token }
    yield token

// In the NEWLINE handler, skip INDENT/DEDENT when inside brackets:
| Parser.NEWLINE _col when state.BracketDepth > 0 ->
    // Inside [...], (...), {...}: suppress all indentation tokens
    state <- { state with LineNum = state.LineNum + 1 }
    // yield nothing — discard the newline

| Parser.NEWLINE col ->
    // existing newline processing unchanged
    ...
```

**Critical:** The `BracketDepth > 0` guard must come BEFORE the existing `NEWLINE col` handler in the `match token with` expression. Otherwise the newline fires and emits INDENT/DEDENT.

### Pattern 2: Trailing Semicolon Grammar (SYN-03)

**What:** Add an alternative to `SemiExprList` that accepts a trailing `SEMICOLON` at the end. Also add a dedicated rule for single-element lists with trailing semicolons.

**When to use:** Required for SYN-03. Follows the same pattern already established in `RecordFieldBindings` which allows trailing semicolons.

**Example:**
```fsharp
// Current SemiExprList (Parser.fsy):
SemiExprList:
    | Expr                            { [$1] }
    | Expr SEMICOLON SemiExprList     { $1 :: $3 }

// After adding trailing semicolon support:
SemiExprList:
    | Expr                            { [$1] }
    | Expr SEMICOLON                  { [$1] }          // trailing semicolon: [1;] -> [1]
    | Expr SEMICOLON SemiExprList     { $1 :: $3 }

// The Atom rule for list literals remains the same — no change needed there.
// [1; 2; 3;] parses as: LBRACKET 1 SEMICOLON SemiExprList(2 SEMICOLON []) RBRACKET
```

**Note:** The existing rule `LBRACKET Expr SEMICOLON SemiExprList RBRACKET` already handles `[a; b; c;]` because `SemiExprList` can now accept `c SEMICOLON`. No new Atom rules needed.

### Pattern 3: List Literal Pattern Desugaring (SYN-04)

**What:** Add grammar rules for `[pat]`, `[pat; pat]`, `[pat; pat; pat]` etc. in the `Pattern` nonterminal. Desugar these at parse time into `ConsPat`/`EmptyListPat` chains. No new AST node needed.

**When to use:** Required for SYN-04. The downstream pipeline (Eval, TypeCheck, Infer, MatchCompile, Exhaustive) already fully supports `ConsPat` and `EmptyListPat`.

**Example:**
```fsharp
// Helper function in Parser.fsy header (%{ ... %}):
let rec desugarListPat (pats: Pattern list) (span: Span) : Pattern =
    match pats with
    | [] -> EmptyListPat(span)
    | p :: rest -> ConsPat(p, desugarListPat rest span, span)

// New rules added to Pattern nonterminal:
Pattern:
    | ...                         // existing rules unchanged
    | LBRACKET RBRACKET           { EmptyListPat(ruleSpan parseState 1 2) }  // already exists
    | LBRACKET SemiPatList RBRACKET
        { desugarListPat $2 (ruleSpan parseState 1 3) }
    | LBRACKET SemiPatList SEMICOLON RBRACKET  // trailing semicolon in pattern
        { desugarListPat $2 (ruleSpan parseState 1 4) }

// New nonterminal for semicolon-separated pattern list:
SemiPatList:
    | Pattern                         { [$1] }
    | Pattern SEMICOLON SemiPatList   { $1 :: $3 }
```

**Desugaring:** `[x; y; z]` → `ConsPat(VarPat "x", ConsPat(VarPat "y", ConsPat(VarPat "z", EmptyListPat)))`. This is identical to what `x :: y :: z :: []` produces, which is already tested and working.

### Anti-Patterns to Avoid

- **Adding INDENT/DEDENT handling inside SemiExprList:** This creates a combinatorial explosion of grammar rules and introduces shift/reduce conflicts. Fix IndentFilter instead.
- **Adding a new `ListLitPat` AST node:** Requires touching Eval.fs, TypeCheck.fs, Infer.fs, MatchCompile.fs, Exhaustive.fs, Format.fs. Parse-time desugaring is zero downstream impact.
- **Using `LPAREN`/`RPAREN` bracket tracking only:** Track all three bracket types (`[]`, `()`, `{}`) together to handle multi-line expressions in function arguments and record literals consistently. This is a freebie improvement when the fix is implemented.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| INDENT/DEDENT suppression inside `[...]` | Custom grammar rules accepting INDENT in SemiExprList | BracketDepth field in FilterState | Grammar approach: exponential rule count, shift/reduce conflicts |
| List pattern AST representation | New `ListLitPat` constructor in Pattern | Desugar to ConsPat/EmptyListPat chains at parse time | Five downstream files already handle ConsPat/EmptyListPat perfectly |
| Trailing semicolon in pattern | Separate parser entry point | One production in SemiPatList | RecordFields already does this pattern, it works |

**Key insight:** The entire downstream pipeline (MatchCompile, Exhaustive, Eval) was built around cons-cell patterns. Desugaring list literal patterns at parse time is zero-cost and zero-risk.

## Common Pitfalls

### Pitfall 1: BracketDepth Guard Order in Pattern Match

**What goes wrong:** If the existing `NEWLINE col` handler in `IndentFilter.filter` comes before the new `NEWLINE _ when BracketDepth > 0` guard, F# pattern matching hits the non-guarded case first and emits INDENT/DEDENT even inside brackets.

**Why it happens:** F# match arms are evaluated top-to-bottom. A non-guarded arm takes priority over a guarded arm for the same pattern unless the guarded arm is first.

**How to avoid:** Place the `NEWLINE _ when state.BracketDepth > 0` arm BEFORE the `NEWLINE col` arm in the match expression.

**Warning signs:** Multi-line list test still produces parse error after fix. Check token output with `--emit-tokens`.

### Pitfall 2: IndentFilterTests.fs Hardcoded FilterState Initialization

**What goes wrong:** `IndentFilterTests.fs` constructs `FilterState` records directly (e.g., `{ IndentStack = [0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false; ... }`). Adding `BracketDepth: int` to the record causes all these direct constructions to fail to compile.

**Why it happens:** F# requires all record fields to be specified unless a `with` copy-expression is used. Tests that build `FilterState` literals must be updated.

**How to avoid:** After adding `BracketDepth` to `FilterState`, search all test files for `IndentStack = [0]` (or similar FilterState literal patterns) and add `BracketDepth = 0` to each one.

**Warning signs:** `dotnet test` compile errors mentioning missing `BracketDepth` field.

### Pitfall 3: SemiPatList vs SemiExprList LALR Conflict

**What goes wrong:** Inside pattern context, if `SemiPatList` allows trailing semicolon via `Pattern SEMICOLON`, the parser may get confused about whether the `;` before `]` ends the last element or starts another. In LALR(1), with one token of lookahead (`]`), this is unambiguous: `Pattern SEMICOLON RBRACKET` reduces via trailing-semicolon rule.

**Why it happens:** Misunderstanding LALR lookahead. The `]` token after `;` uniquely identifies the trailing semicolon case.

**How to avoid:** Add `Pattern SEMICOLON` as a separate production in `SemiPatList` (same as `SemiExprList`). Run `dotnet build` and check for shift/reduce conflict warnings from fsyacc. If warnings appear, they will be in the build output.

**Warning signs:** `fsyacc: state X: shift/reduce conflict on RBRACKET` in build output.

### Pitfall 4: BracketDepth Goes Negative on Unbalanced Input

**What goes wrong:** If user writes malformed code like `let x = 1]`, the RBRACKET decrements `BracketDepth` below 0. This makes `BracketDepth > 0` false when it should be tracking nothing, which is fine — but if the code later emits confusing errors due to state corruption, it is hard to debug.

**Why it happens:** No underflow guard on BracketDepth decrement.

**How to avoid:** Use `max 0 (state.BracketDepth - 1)` when decrementing, or accept the behavior since the parse will fail anyway on unbalanced brackets.

**Warning signs:** Only matters for error recovery; unbalanced brackets will fail at parse time regardless.

## Code Examples

Verified patterns from codebase analysis:

### Current SemiExprList rule (to be modified for SYN-03)
```fsharp
// Source: src/LangThree/Parser.fsy lines 284-286
SemiExprList:
    | Expr                            { [$1] }
    | Expr SEMICOLON SemiExprList     { $1 :: $3 }
```

### Current FilterState record (to be modified for SYN-02)
```fsharp
// Source: src/LangThree/IndentFilter.fs lines 25-36
type FilterState = {
    IndentStack: int list
    LineNum: int
    Context: SyntaxContext list
    JustSawMatch: bool
    JustSawTry: bool
    JustSawModule: bool
    PrevToken: Parser.token option
}

let initialState = { IndentStack = [0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false; JustSawTry = false; JustSawModule = false; PrevToken = None }
```

### Empty list pattern (already working, reference for SYN-04)
```fsharp
// Source: src/LangThree/Parser.fsy line 321
| LBRACKET RBRACKET           { EmptyListPat(ruleSpan parseState 1 2) }
```

### ConsPat handling in Eval.fs (reference — what SYN-04 desugars to)
```fsharp
// Source: src/LangThree/Eval.fs lines 325-330
| EmptyListPat _, ListValue [] -> Some []
| ConsPat (headPat, tailPat, _), ListValue (h :: t) ->
    match matchPattern headPat h with
    | Some headBindings ->
        match matchPattern tailPat (ListValue t) with
        | Some tailBindings -> Some (headBindings @ tailBindings)
        | None -> None
    | None -> None
```

### RecordFieldBindings trailing semicolon (reference pattern for SYN-03)
```fsharp
// Source: src/LangThree/Parser.fsy lines 548-550
RecordFieldBindings:
    | IDENT EQUALS Expr                              { [($1, $3)] }
    | IDENT EQUALS Expr SEMICOLON RecordFieldBindings { ($1, $3) :: $5 }
    | IDENT EQUALS Expr SEMICOLON                    { [($1, $3)] }  // trailing semicolon already supported
```

### Expected token stream for multi-line list (current vs fixed)
```
Input:  let result = [1;\n  2;\n  3]\n

Current (broken) filtered tokens:
  LET IDENT(result) EQUALS LBRACKET NUMBER(1) SEMICOLON
  INDENT NUMBER(2) SEMICOLON NUMBER(3) RBRACKET DEDENT

Expected (fixed) filtered tokens after BracketDepth fix:
  LET IDENT(result) EQUALS LBRACKET NUMBER(1) SEMICOLON
  NUMBER(2) SEMICOLON NUMBER(3) RBRACKET
```

### Test file format for new flt tests
```
// Test: multi-line list literal
// --- Command: /path/to/LangThree %input
// --- Input:
let result = [1;
  2;
  3]
// --- Output:
[1; 2; 3]
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single-line lists only | Multi-line lists via bracket depth tracking | Phase 27 | Removes 3.3 constraint |
| No trailing semicolons | Optional trailing semicolon | Phase 27 | Removes 3.4 constraint |
| Only `[]`, `h :: t` patterns | `[x]`, `[x; y]`, `[x; y; z]` patterns | Phase 27 | Removes 3.5 constraint |

**Deprecated/outdated:**
- Workaround for 3.3: "write all list literals on one line" — removed by SYN-02
- Workaround for 3.4: "strip trailing semicolons in code generation" — removed by SYN-03
- Workaround for 3.5: nested `match rest with | [] ->` patterns — removed by SYN-04

## Open Questions

1. **Should bracket depth suppress LPAREN and LBRACE too, or only LBRACKET?**
   - What we know: The problem report only mentions list literals (SYN-02). Record literals `{ field = \n   value }` and parenthesized expressions `(\n  expr\n)` have similar issues but are not required.
   - What's unclear: Whether tracking all three bracket types causes unexpected behavior (e.g., suppressing INDENT inside a `( )` group that a user intended to use as a block delimiter).
   - Recommendation: Track all three types (`[]`, `()`, `{}`) together. The offside rule inside brackets is not meaningful in most functional languages (OCaml, F#, Haskell all suppress it inside brackets). This is a superset fix with no known downside.

2. **Do multi-line list literals need IndentFilterTests updates?**
   - What we know: `IndentFilterTests.fs` constructs `FilterState` records directly, and any new `BracketDepth` field will cause compile errors in those tests.
   - What's unclear: Exact count of affected test constructions.
   - Recommendation: After adding the field, run `dotnet build tests/LangThree.Tests/` to find all affected lines and add `BracketDepth = 0` to each.

## Sources

### Primary (HIGH confidence)
- `src/LangThree/IndentFilter.fs` — Complete source read, FilterState record and filter function
- `src/LangThree/Parser.fsy` — Complete source read, SemiExprList, Pattern, Atom rules
- `src/LangThree/Ast.fs` — Complete source read, Pattern type, ConsPat, EmptyListPat
- `src/LangThree/Eval.fs` — Verified matchPattern for ConsPat and EmptyListPat
- `.planning/REQUIREMENTS.md` — SYN-02, SYN-03, SYN-04 definitions
- `langthree-constraints.md` — Constraints 3.3, 3.4, 3.5 descriptions

### Secondary (MEDIUM confidence)
- Live test: `echo 'let result = [1;\n  2;\n  3]' | dotnet run` → confirmed parse error
- Live test: trailing semicolon → confirmed parse error
- Live test: `[x]` pattern in match → confirmed parse error
- Live token dump: multi-line list → confirmed INDENT/DEDENT emitted inside brackets

### Tertiary (LOW confidence)
- N/A — all findings verified against actual source code

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — files directly inspected, no external libraries involved
- Architecture: HIGH — root causes confirmed via live testing and source analysis
- Pitfalls: HIGH — FilterState literal construction verified in IndentFilterTests.fs

**Research date:** 2026-03-24
**Valid until:** 2026-05-24 (stable internal codebase, no external dependencies)
