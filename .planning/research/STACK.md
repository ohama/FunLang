# Technology Stack: Infix Operator Reform

**Project:** FunLang — ML-style functional language interpreter
**Researched:** 2026-04-02
**Milestone:** Infix operator system reform — `#[left N]` / `#[right N]` attributes,
  `|>` / `>>` / `<<` moved to Prelude, Pratt-style precedence resolution
**Confidence:** HIGH — strategy derived from direct codebase inspection + cross-language survey

---

## Existing Stack (No NuGet Changes Needed)

| Technology | Version | Role |
|------------|---------|------|
| F# | .NET 10 | Implementation language |
| FsLexYacc (fslex + fsyacc) | 11.3.0 | LALR(1) parser generation |
| Argu | 6.2.5 | CLI argument parsing |
| Tomlyn | 2.3.0 | funproj.toml parsing |

No new NuGet packages required. The entire reform is implemented within
the existing F# source files using standard F# data structures.

---

## The Core Problem

FunLang's parser is LALR(1). LALR(1) parsers determine precedence at compile
time by encoding it into the grammar's shift/reduce table. This means operator
precedence is baked into the generated `Parser.fs` — it cannot be changed at
runtime based on user-supplied `#[left N]` attributes.

There are exactly three viable strategies for user-defined precedence in a
language with a generated LALR(1) parser. They are ordered from most invasive
to least:

### Strategy 1: Replace fsyacc with a hand-written Pratt parser (REJECT)

Pratt parsing handles arbitrary user-defined precedence naturally, with precedence
tables consulted at runtime. Languages like Rust use hand-written recursive descent
with Pratt-style operator loops.

**Why to reject for FunLang:** The existing fsyacc grammar covers indent filtering,
complex pattern syntax, type declarations, record expressions, module system, type
classes, and list comprehensions — roughly 1,000 lines of production-quality grammar
rules. Replacing it with a hand-written parser is a 2,000–3,000 line rewrite with
high regression risk. The milestone does not justify this scope.

### Strategy 2: Extend LALR grammar with a generic `InfixChain` node, resolve in a post-pass (RECOMMENDED)

**What:** Add a single new grammar rule that collects any sequence of expressions
separated by user-defined INFIXOP tokens into a flat list: `InfixChain`. After
the LALR parse produces the AST, a lightweight post-pass walks the tree and
restructures every `InfixChain` into a correct precedence tree using the
binding-power (Pratt/precedence-climbing) algorithm applied to the flat list.

**Precedence information source:** A mutable `OperatorTable : Map<string, int * Assoc>`
populated when the interpreter evaluates `#[left N]` / `#[right N]` attribute
declarations above an operator definition in the Prelude or user code.

**How this is already half-done:** The LALR grammar already produces
`App(App(Var(op), left), right)` for all INFIXOP tokens. The post-pass needs
only to identify chains of same-or-related-precedence operators and rebalance.

### Strategy 3: Keep LALR levels, map attributes to existing levels (PARTIAL SOLUTION)

**What:** Keep the existing INFIXOP0–INFIXOP4 buckets. Allow `#[left N]` to
select which bucket an operator falls into (0–4), resolving the bucket at
lex-time based on the operator table.

**Why partially useful:** Eliminates PipeRight / ComposeRight / ComposeLeft as
special AST nodes immediately. `|>` becomes INFIXOP0 (starts with `|`, which maps
to bucket 0), `>>` becomes INFIXOP4 (starts with `**` logic — actually INFIXOP3
or needs a new bucket), and `<<` similarly.

**Why insufficient alone:** Users cannot express precedence finer than 5 levels.
Two distinct user operators that should have different precedences within the same
character class are indistinguishable. This is the existing OCaml / F* limitation:
character-based buckets only.

---

## Recommended Approach: Strategy 2 — LALR + Post-Pass Rebalance

This is the same technique used by GHC for Haskell's `infixl`/`infixr`/`infix`
fixity declarations. GHC's parser produces a flat left-spine of operator applications
during parsing (using the low-precedence default), then the renamer applies fixity
resolution in a separate pass that restructures the spine.

### How GHC Does It (the authoritative precedent)

From the Haskell Prime fixity resolution specification: the context-free grammar
parses all infix operator applications as a flat sequence (alternating operands and
operators). A post-parse pass takes the flat list and applies the precedence/
associativity table to produce a correctly parenthesized tree. The algorithm
is standard precedence climbing:

```
resolve_flat_chain(operands, operators, table):
  use the binding-power loop to repeatedly:
    peek at the next operator's (prec, assoc)
    if prec > current_min_bp: recurse for right operand
    if prec == current_min_bp and assoc == Left: reduce immediately
    if prec == current_min_bp and assoc == Right: recurse
    if prec < current_min_bp: return current accumulation
```

This is algebraically identical to Pratt parsing. The `l_bp` / `r_bp` split
handles left-associative operators (r_bp = l_bp + 1) vs. right-associative
(r_bp = l_bp).

### What Changes in FunLang

**New AST node in `Ast.fs`:**

```fsharp
// Flat operator chain — produced by LALR when precedence is unknown at parse time.
// Resolved to nested App/InfixOp nodes by Elaborate.resolveOperatorChains.
// Invariant: exprs.Length = ops.Length + 1
| InfixChain of exprs: Expr list * ops: (string * Span) list * span: Span
```

**New grammar rule in `Parser.fsy`:**

The existing INFIXOP rules already desugar to `App(App(Var(op), left), right)`.
This needs to change for operators whose precedence will be declared via attributes.
For Phase 1 of the reform, the safest approach is: keep existing INFIXOP0–4 rules
for the character-class operators (they won't have `#[left N]`), and add a
separate token class `USEROP` for operators that are explicitly attribute-declared.
`InfixChain` is only needed for `USEROP` sequences.

However, for moving `|>` / `>>` / `<<` specifically, the simpler path is:

1. Add `|>` / `>>` / `<<` as INFIXOP0/INFIXOP1 tokens respectively (character class)
2. Remove `PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT` tokens
3. Remove `PipeRight`, `ComposeRight`, `ComposeLeft` AST nodes
4. Add Prelude definitions for `(|>)`, `(>>)`, `(<<)`

This requires no post-pass rebalancer. The character-class bucket handles precedence.
`|>` starts with `|` → INFIXOP0 (comparison level). This is lower precedence than
arithmetic — correct. `>>` starts with `>` → INFIXOP0. `<<` starts with `<` →
INFIXOP0. Both `>>` and `<<` at INFIXOP0, left-associative. This may not be
ideal for `<<` (compose-left should be right-associative), but it is sufficient
for Phase 1.

**New runtime table in `Elaborate.fs` or a new `OpTable.fs`:**

```fsharp
type Assoc = Left | Right | Non

/// Runtime operator precedence table, populated from #[left N] / #[right N] declarations.
/// Keys are operator strings (e.g. "|>", "+++"). Values are (precedence 0-9, associativity).
/// Initialized with defaults for all built-in operators.
let mutable operatorTable : Map<string, int * Assoc> = Map.ofList [
    ("|>",  (1, Left))
    (">>",  (9, Left))
    ("<<",  (9, Right))
    // ... other operators added at declaration time
]
```

**New elaboration pass:**

`Elaborate.fs` already exists and handles some AST transformations. Add a
`resolveOperatorChains` traversal that rewrites `InfixChain` nodes using the
operator table. This pass runs before type checking, so the type checker sees
only correctly-nested `App` nodes.

---

## The `#[left N]` / `#[right N]` Attribute Syntax

### Lexing Strategy

The sequence `#[` is the critical question. Current lexer state:

- `[` is `LBRACKET`
- `#` is not currently in `op_char` and would fail with an error
- `#[` is not a token

**Recommended approach: Lex `#[` as a single token `ATTR_OPEN`.**

In `Lexer.fsl`, add before the catch-all rule:

```
| "#["   { ATTR_OPEN }
```

This requires `#[` to appear before the general identifier and operator rules.
Because fslex uses longest-match with first-match tiebreak, and `#` is currently
illegal, there is no conflict. The `]` is the existing `RBRACKET` token.

This is exactly how Rust lexes `#[`: it is a single token sequence `POUND LBRACKET`
that the parser recognizes as the start of an outer attribute. FunLang can go
further and make it a single `ATTR_OPEN` token, since attributes are always
`#[ident rest]` and FunLang does not need the `#` to appear standalone.

**Alternative: Two tokens `HASH` + `LBRACKET`.**

Add `| '#' { HASH }` to the lexer. In the grammar, recognize attribute syntax
as `HASH LBRACKET ...`. This is less ergonomic but keeps the lexer simpler.
The parser still needs a dedicated rule for attribute syntax, so the two-token
approach has no grammar advantage.

**Verdict: Single `ATTR_OPEN` token is cleaner.** It makes the parser rule
unambiguous:

```
AttributeDecl:
    | ATTR_OPEN IDENT NUMBER RBRACKET  { Attribute($2, $3) }
    | ATTR_OPEN IDENT IDENT  NUMBER RBRACKET  { Attribute($2 + " " + $3, $4) }
    // i.e.: #[left 5], #[right 5]
```

### Grammar Position

Attributes in ML-family languages (OCaml `[@attr]`, F# `[<Attr>]`) attach to the
following declaration. In FunLang, `#[left N]` precedes the operator `let` binding:

```
#[left 5]
let (|>) x f = f x
```

In the grammar, `LetDecl` needs an optional leading `AttributeDecl`. The attribute
payload is not stored in the `LetDecl` AST node (keep `Decl` clean). Instead,
the elaboration pass collects attribute–declaration pairs and updates the
`operatorTable` before processing the rest of the module.

---

## The Eval.fs `PipeRight` / `ComposeRight` / `ComposeLeft` Removal

These three cases in `Eval.fs` are the most code-intensive parts of the removal:

- `PipeRight`: dispatches to `applyFunc`, supports the trampoline TCO path
- `ComposeRight` / `ComposeLeft`: construct synthetic closures with unique names
  via `composeCounter`

When `|>` becomes a Prelude function, these eval cases disappear. But the Prelude
definition must be semantically equivalent:

```fsharp
// Prelude/Core.fun
let (|>) x f = f x
let (>>) f g x = g (f x)
let (<<) f g x = f (g x)
```

The trampoline TCO for `PipeRight` is the only concern. The Prelude definition
`let (|>) x f = f x` desugars to `App(Var "f", Var "x")`. Since `App` already
goes through `applyFunc` with `tailPos`, TCO is preserved for the final application.
The only loss is the specialized fast path in `eval` for `PipeRight` — but that
fast path exists today only because `PipeRight` was a special node. After removal,
`f x` in the Prelude body gets the normal `App` TCO treatment, which is identical.

**Conclusion: No TCO regression from removing PipeRight.**

---

## Phased Implementation Order

The research supports this sequencing:

### Phase 1: Remove special AST nodes for `|>`, `>>`, `<<`

- Lex `|>` → `INFIXOP0 "|>"`, `>>` → `INFIXOP3 ">>"`, `<<` → `INFIXOP3 "<<"`
  (using existing character-class rules — `|` starts INFIXOP0, `>` and `<` start
  INFIXOP0; but for `>>` and `<<` specifically we want higher precedence, so lex
  them explicitly before the catch-all as INFIXOP3 or INFIXOP4)
- Add `(|>)`, `(>>)`, `(<<)` definitions to `Prelude/Core.fun`
- Remove `PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT` tokens from lexer + parser
- Remove `PipeRight`, `ComposeRight`, `ComposeLeft` from `Ast.fs`, `Eval.fs`,
  `Bidir.fs`, `Format.fs`, `Elaborate.fs`, `TypeCheck.fs`
- All existing tests should pass; pipe/composition tests now exercise Prelude paths

**Precedence concern for `>>` / `<<`:** The character class rules give `>` →
INFIXOP0 (comparison level). `>>` and `<<` as composition operators need higher
precedence than function application, which is modeled by giving them their own
explicit tokens matched before the catch-all. Map them to INFIXOP3 (multiplicative
level) or INFIXOP4 (exponentiation level) via an explicit rule in the lexer.

### Phase 2: Add `#[left N]` / `#[right N]` attribute syntax

- Add `ATTR_OPEN` token to `Lexer.fsl`
- Add `AttributeDecl` grammar rule to `Parser.fsy`
- Add `OperatorAttr` to `Decl` DU (or handle purely in elaboration without AST storage)
- Implement operator table in `Elaborate.fs` or new `OpTable.fs`
- Elaborate pass reads attributes and populates the table before type checking

### Phase 3: Add `InfixChain` node and post-pass rebalancer (if needed)

- Add `InfixChain` to `Ast.fs`
- Change grammar rules for attribute-declared operators to produce `InfixChain`
- Implement `resolveOperatorChains` using the binding-power algorithm
- This phase is only needed if Phase 2 users need finer control than the 5 INFIXOP
  buckets provide

**Recommendation: Phase 3 is optional.** If the goal is only to move `|>` / `>>` / `<<`
to Prelude and allow future operators to be declared with `#[left N]`, the existing
INFIXOP bucket system (mapped via the operator table at lex time) is sufficient.
Full Pratt rebalancing adds complexity with little practical gain unless FunLang
needs more than 5 distinct precedence levels.

---

## What NOT to Build

**Do not replace fsyacc with a hand-written parser.** The grammar is large,
well-tested, and handles indent-sensitive parsing via `IndentFilter.fs`. A rewrite
is out of scope.

**Do not add `InfixChain` in Phase 1.** The existing INFIXOP0–4 buckets handle
the immediate goal of removing special AST nodes. `InfixChain` is only needed
for user operators with custom numeric precedence that falls between existing
buckets.

**Do not attempt intransitive or partial-order precedence** (as proposed by
Adamant's blog). This is theoretically elegant but adds error-reporting complexity
and is unnecessary for the stated goals. Ten numeric levels (0–9 like Haskell)
are sufficient and familiar.

**Do not store attributes in the `Decl` DU if avoidable.** Attributes are a
parsing artifact that feed the operator table. The elaborator can consume them
and discard them, keeping `Decl` clean. Only add an `OperatorAttr` DU case if
the attribute needs to propagate through the pipeline (e.g., for serialization
or IDE tooling).

**Do not try to make `>>` and `<<` have different associativity at INFIXOP3.**
The grammar `%left INFIXOP3` means all INFIXOP3 operators are left-associative.
If `<<` must be right-associative, it needs its own grammar level or a post-pass.
For Phase 1, accept left-associativity for all three pipe/compose operators and
add a note. Phase 3 can correct this.

---

## Cross-Language Survey Summary

| Language | Approach | Notes |
|----------|----------|-------|
| Haskell (GHC) | Two-pass: LALR flat spine → fixity resolution pass | Exact pattern to follow for FunLang |
| OCaml | Character-class buckets, baked into yacc grammar | What FunLang already does; no user extension |
| F* | Inherits OCaml character-class system | Same limitation as OCaml |
| F# | Character-class operators, limited user extension | Same limitation |
| Rust | Hand-written recursive descent, no user-defined precedence | Not applicable |
| Scala | Character-class buckets (first char determines level) | Same limitation as OCaml |

The GHC approach is the right model: parse with a permissive default, resolve
in a separate pass. FunLang's version of this is lighter-weight because it only
needs to handle the 5 existing INFIXOP buckets plus attribute-declared overrides,
not an arbitrary 0–9 system with local scoping.

---

## Files Affected by the Reform

| File | Change | Phase |
|------|--------|-------|
| `Lexer.fsl` | Remove `PIPE_RIGHT`/`COMPOSE_RIGHT`/`COMPOSE_LEFT`; lex `|>` as INFIXOP0, `>>`/`<<` as explicit tokens; add `ATTR_OPEN` | 1, 2 |
| `Parser.fsy` | Remove 3 special token decls + 3 grammar rules; add `AttributeDecl` rule | 1, 2 |
| `Ast.fs` | Remove `PipeRight`/`ComposeRight`/`ComposeLeft`; optionally add `InfixChain` | 1, 3 |
| `Eval.fs` | Remove 3 eval cases; remove `composeCounter` | 1 |
| `Bidir.fs` | Remove 3 type-check cases | 1 |
| `Format.fs` | Remove 3 format cases | 1 |
| `Elaborate.fs` | Add operator table; add attribute processing; add `resolveOperatorChains` | 2, 3 |
| `Prelude/Core.fun` | Add `(|>)`, `(>>)`, `(<<)` definitions | 1 |
| `TypeCheck.fs` | Minor: pass operator table through if needed for type class resolution | 2 |

**Total estimated change for Phase 1: ~150 lines removed, ~20 lines added.**
Phase 2 adds ~100 lines. Phase 3 adds ~200 lines if pursued.

---

## Sources

- Codebase inspection: `Lexer.fsl`, `Parser.fsy`, `Ast.fs`, `Eval.fs`,
  `Prelude/Core.fun` (read directly, 2026-04-02)
- [Haskell 98 Report: Declarations — fixity declarations](https://www.haskell.org/onlinereport/decls.html)
- [Kowainik: Fix(ity) me — Haskell fixity resolution mechanics](https://kowainik.github.io/posts/fixity)
- [FStar: Parsing and operator precedence](https://github.com/FStarLang/FStar/wiki/Parsing-and-operator-precedence) — OCaml-inherited character-class system
- [Simple but Powerful Pratt Parsing (matklad)](https://matklad.github.io/2020/04/13/simple-but-powerful-pratt-parsing.html) — binding-power algorithm
- [From Precedence Climbing to Pratt Parsing (Russ Cox / Theo Johnson-Freyd)](https://www.engr.mun.ca/~theo/Misc/pratt_parsing.htm) — equivalence proof
- [Rust compiler dev guide: Lexing and parsing](https://rustc-dev-guide.rust-lang.org/the-parser.html) — `#[attr]` as `POUND LBRACKET` token pair
- [Adamant: Operator Precedence — We Can Do Better](https://blog.adamant-lang.org/2019/operator-precedence/) — intransitive precedence alternative (surveyed, rejected as over-engineering)
- [OCaml: Custom operators](https://blog.shaynefletcher.org/2016/09/custom-operators-in-ocaml.html) — confirms character-class approach
