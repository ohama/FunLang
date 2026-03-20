# User-Defined Operators - Research

**Researched:** 2026-03-20
**Domain:** Language design, LALR(1) parsing, operator precedence
**Confidence:** HIGH

## Summary

User-defined operators can be cleanly integrated into LangThree's existing LALR(1) parser by following the OCaml/F# approach: the **lexer categorizes operator tokens by their first character** into fixed precedence/associativity buckets (e.g., INFIXOP0, INFIXOP1, etc.), and the **parser treats each bucket as a single grammar rule** with declared precedence. This eliminates any need for a Pratt parser or runtime precedence resolution.

The key insight is that OCaml -- which also uses an LALR(1) parser (ocamlyacc/menhir) -- has supported user-defined operators since its inception using exactly this technique. The lexer classifies any sequence of operator characters into one of ~6 token categories based on the first character, and the parser grammar has rules for each category at the appropriate precedence level.

For LangThree, this means: (1) define allowed operator characters, (2) add a catch-all lexer rule that classifies operator sequences into INFIXOP0-4 and PREFIXOP tokens, (3) add parser rules for each category at the correct precedence level, (4) desugar `Expr INFIXOP2 Expr` into `App(App(Var(op), lhs), rhs)` in the AST. User-defined operators are **just functions** with special calling syntax.

**Primary recommendation:** Follow OCaml's INFIXOP0-4 token categorization approach. Operators are lexed into precedence-bucket tokens based on their first character, parsed by LALR(1) rules for each bucket, and desugared to function application in the AST.

## The OCaml/F# Approach (Recommended)

### How It Works

Both OCaml and F# determine operator precedence **statically from the first character** of the operator symbol. This is a compile-time rule, not a runtime decision, making it perfectly compatible with LALR(1) parsing.

### F# Precedence Rules (from official spec)

In F#, the precedence table uses `op` notation where `*op*` means "any operator starting with `*`":

| Precedence (low to high) | Pattern | Associativity | Examples |
|--------------------------|---------|---------------|----------|
| 1 | `\|op` `&op` `$` (incl. `\|\|\|`, `&&&`, `<<<`, `>>>`) | Left | `\|>`, `<\|`, `\|\|\|` |
| 2 | `^op` (incl. `^^^`) | Right | `^`, `^^^` |
| 3 | `::` | Right | `::` |
| 4 | `-op` `+op` | Left (infix) | `+`, `-`, `+.` |
| 5 | `*op` `/op` `%op` | Left | `*`, `/`, `%` |
| 6 | `**op` | Right | `**`, `**~` |

Leading `.` characters are **ignored** when determining precedence (so `.*` has same precedence as `*`).

### OCaml Token Categories

OCaml's lexer (lexer.mll) classifies operators into exactly these tokens:

| Token | First char(s) | Associativity | Precedence level |
|-------|--------------|---------------|------------------|
| `INFIXOP0` | `=` `<` `>` `\|` `&` `$` `!` | Left | Comparison level |
| `INFIXOP1` | `@` `^` | Right | Concatenation level |
| `INFIXOP2` | `+` `-` | Left | Additive level |
| `INFIXOP3` | `*` `/` `%` | Left | Multiplicative level |
| `INFIXOP4` | `**` | Right | Exponentiation level |
| `PREFIXOP` | `!` `~` `?` | N/A (prefix) | Highest |

The parser grammar then has rules like:
```
expr: expr INFIXOP0 expr { ... }
    | expr INFIXOP1 expr { ... }
    | ...
```

Each INFIXOP token carries the actual operator string as its payload (e.g., `INFIXOP2 of string` where the string might be `"++"` or `"+."`).

## Architecture: LangThree Integration Plan

### Operator Character Set

Allowed characters for custom operators (following F# exactly):
```
! $ % & * + - . / < = > ? @ ^ | ~
```

The `~` character is special: it marks prefix operator definitions (not part of the operator name when used).

### Lexer Changes

Add a catch-all operator rule **after** all existing specific operator rules. The existing rules for `+`, `-`, `*`, etc. will match first (longest match / first-match priority), so built-in operators continue to work unchanged.

```
// fslex rule (conceptual)
let op_char = ['!' '$' '%' '&' '*' '+' '-' '.' '/' '<' '=' '>' '?' '@' '^' '|' '~']

// In the tokenize rule, AFTER all existing specific operator matches:
| op_char op_char+    { classifyOperator (lexeme lexbuf) }
```

The `classifyOperator` function examines the first character (ignoring leading dots) and returns the appropriate token:

```fsharp
let classifyOperator (op: string) =
    // Strip leading dots for precedence classification
    let effective = op.TrimStart('.')
    match effective.[0] with
    | '!' | '~' | '?' when isPrefix op -> PREFIXOP op
    | '=' | '<' | '>' | '|' | '&' | '$' -> INFIXOP0 op
    | '@' | '^' -> INFIXOP1 op
    | '+' | '-' -> INFIXOP2 op
    | '*' | '/' | '%' -> INFIXOP3 op  // but ** -> INFIXOP4
    | _ -> INFIXOP0 op  // fallback
```

**Critical detail:** The `**` pattern must be detected before `*` classification:
- If the effective string starts with `**`, classify as INFIXOP4
- Otherwise if it starts with `*`, classify as INFIXOP3

### New Tokens

```
%token <string> INFIXOP0   // = < > | & $ level (comparison)
%token <string> INFIXOP1   // @ ^ level (concatenation)
%token <string> INFIXOP2   // + - level (additive)
%token <string> INFIXOP3   // * / % level (multiplicative)
%token <string> INFIXOP4   // ** level (exponentiation)
%token <string> PREFIXOP   // ! ~ ? level (prefix, highest)
```

### Parser Precedence Declarations

Add to existing precedence section in Parser.fsy:

```
// Existing declarations stay as-is, interleave new ones:
%left PIPE_RIGHT
%left COMPOSE_RIGHT
%right COMPOSE_LEFT
%left OR
%left AND
%nonassoc EQUALS LT GT LE GE NE
%left INFIXOP0              // same level as comparisons
%right INFIXOP1             // @ ^ level (between cons and comparison)
%right CONS
// PLUS/MINUS stay as Term/Factor grammar-based precedence
%left INFIXOP2              // custom +op -op at same level as +/-
// STAR/SLASH stay as Term/Factor grammar-based precedence
%left INFIXOP3              // custom *op /op at same level as *//
%right INFIXOP4             // ** level (above multiplication)
```

**Note:** LangThree currently uses grammar-based precedence (Term/Factor) for arithmetic, not `%left` declarations. The INFIXOP tokens should be integrated at appropriate levels. The cleanest approach: add INFIXOP rules at the Expr level for INFIXOP0-1, at the Term level for INFIXOP2-3, and at the Factor level for INFIXOP4.

### Parser Rules

```
Expr:
    // ... existing rules ...
    | Expr INFIXOP0 Expr  { App(App(Var($2, ruleSpan parseState 2 2), $1, ...), $3, ...) }
    | Expr INFIXOP1 Expr  { App(App(Var($2, ...), $1, ...), $3, ...) }

Term:
    | Term INFIXOP3 Factor  { App(App(Var($2, ...), $1, ...), $3, ...) }
    | Expr INFIXOP2 Term    { App(App(Var($2, ...), $1, ...), $3, ...) }

Factor:
    | Factor INFIXOP4 Factor  { App(App(Var($2, ...), $1, ...), $3, ...) }
    | PREFIXOP Atom           { App(Var($1, ...), $2, ...) }
```

### AST Representation

User-defined operators desugar to **plain function application**. No new AST nodes needed:

```
a ++ b   -->   App(App(Var("++"), a), b)
!x       -->   App(Var("!"), x)
```

This means type checking, evaluation, and all downstream phases work **automatically** -- operators are just functions.

### Operator Definition Syntax

Following F#, operators are defined by wrapping the symbol in parentheses:

```
let (++) xs ys = append xs ys
let (<+>) a b = optionMap (fun x -> x + b) a
```

**Lexer support for `( op )`:** When the lexer sees `(` followed by operator characters followed by `)`, it should emit a regular `IDENT` token with the operator string as the identifier value. This can be done either:

1. **In the lexer:** A special rule that matches `'(' op_chars+ ')'` and emits `IDENT(op)` -- but this is fragile with whitespace.
2. **In the IndentFilter/post-lex phase:** When the token stream has `LPAREN`, `INFIXOP*`, `RPAREN` in sequence, collapse them to a single `IDENT(op)`. This is cleaner and handles whitespace naturally.
3. **In the parser:** Add grammar rules for `LPAREN INFIXOP0 RPAREN` etc. that produce an identifier. This is the most flexible.

**Recommended: Option 3 (parser rules).** Add an `OpName` nonterminal:

```
OpName:
    | LPAREN INFIXOP0 RPAREN  { $2 }
    | LPAREN INFIXOP1 RPAREN  { $2 }
    | LPAREN INFIXOP2 RPAREN  { $2 }
    | LPAREN INFIXOP3 RPAREN  { $2 }
    | LPAREN INFIXOP4 RPAREN  { $2 }
    | LPAREN PREFIXOP RPAREN  { $2 }
    // Also allow referencing built-in operators as functions:
    | LPAREN PLUS RPAREN      { "+" }
    | LPAREN MINUS RPAREN     { "-" }
    | LPAREN STAR RPAREN      { "*" }
    // etc.
```

Then in Decl rules:
```
Decl:
    | LET OpName ParamList EQUALS Expr  { ... same as LET IDENT ... }
```

And in Atom (to use operators as values):
```
Atom:
    | OpName  { Var($1, ...) }
```

### Interaction with Existing Built-in Operators

**Existing operators remain as-is.** The lexer's specific rules (`"+="`, `"|>"`, `">>"`, `"<<"`, `"&&"`, `"||"`, etc.) match before the catch-all operator rule. The LALR grammar continues to parse them with their dedicated tokens and precedence.

**However**, users can also refer to built-in operators as functions using parenthesized syntax:
```
let result = List.fold (+) 0 xs
```
This requires the `OpName` rules to include built-in operator tokens.

### Prelude Integration

Operators defined in Prelude files work naturally since they are just `let` bindings:

```
// Prelude/List.fun
let (++) xs ys = append xs ys
```

These are loaded into the environment before user code runs, so user code can use `++` as an infix operator.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Operator precedence parsing | Custom Pratt parser for expressions | First-char token categorization (OCaml approach) | Integrates seamlessly with existing LALR(1); battle-tested in OCaml for 30+ years |
| Operator-to-function resolution | Special operator call AST node | Desugar to `App(App(Var(op), lhs), rhs)` | Operators as functions means type checking, eval, and all passes work for free |
| Operator definition syntax | Special operator definition AST node | Reuse existing `LetDecl` with operator name as string | Operators are just functions with funny names |
| Precedence declarations | Runtime precedence tables or `infix`/`infixl`/`infixr` directives | Fixed precedence from first character | Avoids the Haskell problem of import-dependent precedence; works with static LALR(1) |

**Key insight:** The entire feature is a lexer+parser change that desugars to existing infrastructure. No new AST nodes, no new type-checking logic, no new evaluation logic.

## Common Pitfalls

### Pitfall 1: Lexer Rule Ordering
**What goes wrong:** The catch-all operator rule matches before specific built-in operators.
**Why it happens:** fslex uses first-match for equal-length matches. If `op_char+` appears before `"+="`, the catch-all wins.
**How to avoid:** Place the catch-all rule AFTER all specific operator rules. fslex uses longest match first, but for equal lengths uses rule order. Since `+` is 1 char and `++` is 2 chars matched by op_char+, test carefully.
**Warning signs:** Existing operators like `|>`, `>>`, `<<` stop working or change behavior.

### Pitfall 2: Ambiguity with Existing Tokens
**What goes wrong:** Operator character sequences that are already tokens (like `->`, `<-`, `|>`) get reclassified.
**Why it happens:** The catch-all rule doesn't exclude reserved sequences.
**How to avoid:** Keep all existing specific lexer rules in place (they take priority via longest match + order). The catch-all should only match sequences of 2+ operator chars that don't match any existing rule. Test that `->`, `<-`, `|>`, `>>`, `<<`, `::`, `<=`, `>=`, `<>`, `&&`, `||`, `..` all still lex correctly.
**Warning signs:** Parse errors in code that previously worked.

### Pitfall 3: Single-Character Operator Conflicts
**What goes wrong:** A user tries to redefine `+` or `*` via the custom operator mechanism, creating confusion.
**Why it happens:** Single built-in operator characters are lexed as their specific tokens (PLUS, STAR, etc.), not as INFIXOP tokens.
**How to avoid:** This is actually correct behavior -- built-in single-char operators keep their semantics. Users can shadow them via `let (+) a b = ...` but this uses the `OpName` syntax for definition, not the INFIXOP lexer path. Document clearly: single-char built-ins always parse as built-ins; custom operators need 2+ chars.
**Warning signs:** User expects `let (+) a b = string_concat a b` to change `+` behavior for integers.

### Pitfall 4: Minus/Negate Ambiguity
**What goes wrong:** Operators starting with `-` could conflict with unary minus (Negate).
**Why it happens:** `- x` is unary minus, but `->> x` should be a prefix operator application.
**How to avoid:** The existing single `-` rule produces MINUS, and the Factor rule handles `MINUS Factor -> Negate`. Multi-char operators like `-->` or `->>` get classified as INFIXOP2. The lexer's longest-match ensures `->>` is one INFIXOP2 token, not MINUS followed by `>>`.
**Warning signs:** `--> f x` fails to parse.

### Pitfall 5: Parenthesized Operator Parsing Ambiguity
**What goes wrong:** `(+)` as an expression (operator-as-value) conflicts with `(expr)` grouping.
**Why it happens:** Both start with LPAREN and the parser needs to look ahead to distinguish.
**How to avoid:** With LALR(1), when the parser sees LPAREN, it shifts. The next token is either an INFIXOP/PLUS/MINUS/STAR/etc. (operator name case) or an expression. Since operator tokens and expression-start tokens are disjoint (operators can't start an expression except for unary minus), there's no conflict -- the parser rules for `OpName` and `Atom` can coexist.
**Warning signs:** Shift-reduce conflicts in fsyacc output.

### Pitfall 6: The `|` Character Ambiguity
**What goes wrong:** Operators starting with `|` (like `|+|`) conflict with the PIPE token used in match expressions.
**Why it happens:** `|` is already lexed as PIPE. Multi-char `|>` is PIPE_RIGHT.
**How to avoid:** Only multi-char operators starting with `|` (besides `|>` and `||`) would hit the catch-all. The lexer must ensure `|` alone stays as PIPE, `||` stays as OR, `|>` stays as PIPE_RIGHT. Any other `|` followed by operator chars becomes INFIXOP0. Test match expressions still work.
**Warning signs:** Match clauses break.

## Recommended Scoping Strategy

### Phase 1: Core Infrastructure
1. Add INFIXOP0-4 and PREFIXOP token types to the parser
2. Add lexer classification function and catch-all rule
3. Add parser rules for infix expressions (desugar to App)
4. Add OpName nonterminal for operator definitions
5. Add Decl rules for `let (op) params = body`

### Phase 2: Operator-as-Value
1. Add `OpName` to Atom so operators can be passed as values: `fold (+) 0 xs`
2. Include built-in operator tokens in OpName

### Phase 3: Prelude Operators
1. Define useful operators in Prelude files (e.g., `++` for list append, `<+>` for option map)
2. Test that Prelude-defined operators work in user code

### Phase 4: Polish
1. Support `let rec` with operator names (if needed)
2. Error messages that mention operator names nicely
3. REPL display of operator types

## Code Examples

### Lexer Classification (F#)
```fsharp
/// Classify a multi-character operator token into precedence bucket
let classifyOperator (op: string) =
    // Strip leading dots for precedence (F# rule)
    let eff = op.TrimStart('.')
    if eff.Length = 0 then INFIXOP3 op  // pure dots -> multiplicative level
    elif eff.Length >= 2 && eff.[0] = '*' && eff.[1] = '*' then INFIXOP4 op
    else
        match eff.[0] with
        | '=' | '<' | '>' | '|' | '&' | '$' | '!' -> INFIXOP0 op
        | '@' | '^' -> INFIXOP1 op
        | '+' | '-' -> INFIXOP2 op
        | '*' | '/' | '%' -> INFIXOP3 op
        | _ -> INFIXOP0 op  // fallback to comparison level
```

### Parser Rules (fsyacc)
```
// In Expr (comparison level):
    | Expr INFIXOP0 Expr
        { let span = ruleSpan parseState 1 3
          let opSpan = symSpan parseState 2
          App(App(Var($2, opSpan), $1, span), $3, span) }

// In Expr (between cons and comparison):
    | Expr INFIXOP1 Expr
        { let span = ruleSpan parseState 1 3
          let opSpan = symSpan parseState 2
          App(App(Var($2, opSpan), $1, span), $3, span) }

// In Expr (additive level, alongside PLUS/MINUS):
    | Expr INFIXOP2 Term
        { let span = ruleSpan parseState 1 3
          let opSpan = symSpan parseState 2
          App(App(Var($2, opSpan), $1, span), $3, span) }

// In Term (multiplicative level, alongside STAR/SLASH):
    | Term INFIXOP3 Factor
        { let span = ruleSpan parseState 1 3
          let opSpan = symSpan parseState 2
          App(App(Var($2, opSpan), $1, span), $3, span) }

// In Factor (exponentiation level):
    | Factor INFIXOP4 Factor
        { let span = ruleSpan parseState 1 3
          let opSpan = symSpan parseState 2
          App(App(Var($2, opSpan), $1, span), $3, span) }

// Prefix operators (in Factor):
    | PREFIXOP Atom
        { let span = ruleSpan parseState 1 2
          let opSpan = symSpan parseState 1
          App(Var($1, opSpan), $2, span) }
```

### Operator Definition
```
// In LangThree source:
let (++) xs ys = append xs ys
let (<+>) a b = match a with | Some x -> Some (x + b) | None -> None

// Usage:
let result = [1, 2] ++ [3, 4]
let maybe = Some 5 <+> 3
```

### Desugared AST
```
// a ++ b  desugars to:
App(App(Var("++"), Var("a")), Var("b"))

// Identical to: (++) a b
// Which is: apply (apply (++) a) b
```

## Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| First-char precedence (OCaml/F#) | Haskell-style `infixl`/`infixr` declarations | Haskell approach requires parsing to depend on which operators are in scope; breaks LALR(1) unless done in a post-parse fixup phase. Much more complex. |
| First-char precedence | Single precedence level for all custom ops | Simpler but severely limits expressiveness. `a ++ b * c` would need explicit parens. |
| First-char precedence | Pratt parser for expression level | Requires replacing the expression parser entirely. Not compatible with fsyacc. |
| Desugaring to App | Dedicated `InfixApp` AST node | Extra complexity in type checker, evaluator, pattern matching. No benefit over plain App. |

## Open Questions

1. **Should single-char built-in operators be redefinable?**
   - What we know: F# allows shadowing built-in operators. OCaml does too.
   - What's unclear: Whether LangThree's grammar-based arithmetic precedence (Term/Factor) can coexist with user redefinition of `+`.
   - Recommendation: Defer this. Phase 1 only supports custom multi-char operators. Single-char operators keep built-in behavior. Can revisit later.

2. **Should there be a `<|` (backward pipe) operator?**
   - What we know: F# has `<|` and `<||`. It's at the same precedence as `|>`.
   - What's unclear: Whether it's needed in LangThree's ecosystem.
   - Recommendation: With user-defined operators, users can define it themselves: `let (<|) f x = f x`. No special support needed.

3. **Module-qualified operators?**
   - What we know: F# supports `Array.(+)` style qualified operator references.
   - What's unclear: Whether LangThree's module system can handle this.
   - Recommendation: Defer. Start with global operator definitions only.

## Sources

### Primary (HIGH confidence)
- [F# Operator Overloading - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/operator-overloading) - Allowed characters, definition syntax, prefix/infix rules
- [F# Symbol and Operator Reference - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/symbol-and-operator-reference/) - Complete precedence table with `*op*` notation
- [OCaml parser.mly (GitHub)](https://github.com/ocaml/ocaml/blob/trunk/parsing/parser.mly) - INFIXOP0-4 token approach in production LALR parser
- [OCaml lexer.mll (GitHub)](https://github.com/ocaml/ocaml/blob/trunk/parsing/lexer.mll) - Lexer classification of operator tokens
- LangThree source code: Lexer.fsl, Parser.fsy, Ast.fs (direct inspection)

### Secondary (MEDIUM confidence)
- [Custom operators in OCaml - Shayne Fletcher](https://blog.shaynefletcher.org/2016/09/custom-operators-in-ocaml.html) - OCaml operator character categories and precedence levels
- [Operator-precedence parser - Wikipedia](https://en.wikipedia.org/wiki/Operator-precedence_parser) - Hybrid parser approaches (GCC, Raku)

## Metadata

**Confidence breakdown:**
- Lexer approach: HIGH - Directly mirrors OCaml's production implementation
- Parser integration: HIGH - LALR(1) grammar rules for INFIXOP tokens are well-understood
- Precedence by first character: HIGH - Used by both OCaml and F# in production
- AST desugaring: HIGH - Operators-as-functions is standard ML family approach
- Interaction with existing operators: MEDIUM - Need to verify no shift-reduce conflicts in practice
- OpName/definition syntax: MEDIUM - Parser rules need testing for LALR conflicts

**Research date:** 2026-03-20
**Valid until:** Indefinite (language design principles, not library-specific)
