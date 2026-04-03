# Feature Landscape: Infix Operator Reform (v12.0)

**Domain:** Operator precedence / fixity system in FunLang interpreter
**Researched:** 2026-04-02
**Milestone focus:** Replace hardcoded `|>`, `>>`, `<<` AST nodes with user-definable operators; add `#[left N]` / `#[right N]` attribute syntax for explicit precedence/associativity

---

## Context: What is Being Reformed

The current system has two tiers of operator handling, and the goal is to collapse them:

**Tier 1 — Hardcoded operators** (what we want to remove)

| Operator | Token | AST node | Eval handler | Bidir handler |
|----------|-------|----------|--------------|---------------|
| `|>` | `PIPE_RIGHT` | `PipeRight` | tailcall-aware application | unifies `right : leftTy -> resultTy` |
| `>>` | `COMPOSE_RIGHT` | `ComposeRight` | closure synthesis with counter | unifies `left : a->b`, `right : b->c` |
| `<<` | `COMPOSE_LEFT` | `ComposeLeft` | closure synthesis with counter | unifies `left : b->c`, `right : a->b` |

These are currently treated specially in: Lexer.fsl, Parser.fsy, Ast.fs, Eval.fs, Bidir.fs, Infer.fs (stub), TypeCheck.fs (5 match arms), Format.fs, TypeAnnotationTests.fs.

**Tier 2 — User-defined operators** (the existing foundation to build on)

| Precedence bucket | First chars | Associativity | Parser rule |
|------------------|-------------|---------------|-------------|
| INFIXOP0 | `= < > | & $ !` | left | `Expr INFIXOP0 Expr` |
| INFIXOP1 | `@ ^` | right | `Expr INFIXOP1 Expr` |
| INFIXOP2 | `+ -` | left | `Expr INFIXOP2 Term` |
| INFIXOP3 | `* / %` | left | `Term INFIXOP3 Factor` |
| INFIXOP4 | `**` | right | `Factor INFIXOP4 Factor` |

All INFIXOP variants desugar identically in the parser: `App(App(Var(op), left), right)`. No special AST nodes. No special evaluator handling. Type inference works via the operator's bound type in the environment — if `(|>)` is defined in the prelude as a function, Bidir.synth handles it as `App(App(Var("|>"), left), right)`, same as any other user function.

**The fundamental insight:** `|>`, `>>`, `<<` would work as plain INFIXOP operators if:
1. They can be defined in Prelude/Core.fun with `let (|>) x f = f x`, etc.
2. Their precedence and associativity can be specified explicitly (not inferred from first character).
3. The lexer routes them to INFIXOP instead of dedicated tokens.

---

## Table Stakes

**These are required to achieve the milestone goals (issues #6 and #7).**

### TS-1: `#[left N]` and `#[right N]` attribute parsing

Syntax to declare a fixity for an operator definition:

```funlang
#[left 1]
let (|>) x f = f x
```

**What the attribute expresses:**
- `left` or `right` — associativity
- `N` — numeric precedence level (integer, 0 = lowest user level)

**Parsing requirements:**
- `#[` ... `]` before a `let (op) ...` definition, at module level and inside module blocks
- Attribute content: `left N` or `right N` where N is an integer literal
- The attribute binds to the immediately following `let (op)` binding
- Non-operator `let` bindings with attributes are a parse error (or silently ignored — decide)

**Constraint:** fsyacc is LALR(1). The attribute syntax must be parseable without lookahead beyond one token after `#[`. Since the content is `IDENT INT`, this is straightforward.

**Complexity:** Medium — requires new tokens (`HASH_LBRACKET`, or lex `#[` as a single token), new grammar rules for the attribute prefix, and a way to attach the attribute to the binding node in the AST.

---

### TS-2: Fixity table — runtime-populated precedence registry

A data structure mapping operator strings to `(associativity, precedence_level)` pairs, populated as the interpreter loads modules and encounters attributed operator definitions.

**Required operations:**
- `registerFixity : string -> Associativity -> int -> unit` — called when `#[left N] let (op) ...` is parsed/evaluated
- `lookupFixity : string -> (Associativity * int) option` — called by the Pratt parser post-processor
- **Default for unattributed operators:** Infer from first character (INFIXOP0-4 buckets), exactly as today. This is mandatory for backward compatibility — `<|>` in Option.fun, `^^` in Core.fun, `++` in Prelude, and all user-defined operators written before v12.0 must continue to work without modification.

**Initialization order matters:** The fixity table must be populated before expression parsing begins for any file that uses the operator with its attribute-specified precedence. Since FunLang loads Prelude before user files, Prelude-defined operators with attributes will always be available.

**Complexity:** Low — a mutable dictionary is sufficient. The lookup is fast (constant time). Thread-safety is not required (single-threaded interpreter).

---

### TS-3: Pratt parser post-processor for expression trees

The fsyacc-generated parser is LALR(1) with static precedence tables baked into the generated state machine. It cannot dynamically use the fixity table at parse time. The standard solution is a **Pratt post-processing pass** that re-associates expression trees after the LALR parser produces them.

**What the LALR parser produces for `a |> b >> c`:**

With `|>` and `>>` mapped to INFIXOP0 (starts with `|` and `>` respectively), the LALR parser would parse this as `INFIXOP0(a, INFIXOP0(b, c))` — wrong. The Pratt pass must re-associate based on the fixity table.

**Alternative approach — Single INFIXOP bucket:**

Map all user-defined operators to a single INFIXOP level in the LALR grammar (e.g., INFIXOP0), let the LALR parser build a flat spine, then have the Pratt pass rebuild the tree with correct precedence and associativity. This is the OCaml approach for custom operators.

**Pratt pass inputs:**
- A flat or minimally-structured expression tree produced by LALR
- The fixity table

**Pratt pass outputs:**
- A correctly-associated expression tree using the existing `App(App(Var(op), left), right)` desugaring

**Complexity:** High — the Pratt pass is the most algorithmically complex part of this milestone. It must handle:
- Mixed precedence levels in chained operator expressions
- Left vs right associativity
- Operators at different levels interleaved (e.g., `a |> b >> c |> d`)
- Parenthesized subexpressions (must be opaque to the pass)

The pass operates on the `App(App(Var(op), ...))` structure. A simpler representation is a flat list `[expr, op, expr, op, expr]` that the pass rebuilds into a tree — this is the classic Pratt approach.

---

### TS-4: `|>`, `>>`, `<<` moved to Prelude/Core.fun

After TS-1 through TS-3 are in place, Prelude/Core.fun gains:

```funlang
#[left 1]
let (|>) x f = f x

#[left 2]
let (>>) f g = fun x -> g (f x)

#[right 2]
let (<<) f g = fun x -> f (g x)
```

**Semantic preservation requirements:**
- `|>` must preserve tail-call optimization. Currently `PipeRight` in Eval.fs has a special tailcall path (`TailCall(funcVal, argVal)`). When `|>` becomes `App(App(Var("|>"), x), f)`, the `App` evaluator's existing trampoline handles tail calls through the `applyFunc` path. Verify this is correct — the current `App` evaluator already handles `TailCall` in `AppExpr`. The key question: does `(|>) x f` in tail position trigger TCO through the existing App trampoline? It should, because the final application `f x` inside `(|>)` is a tail call within the `(|>)` body.
- `>>` and `<<` currently synthesize closures with unique names (composeCounter) to avoid shadowing. A prelude definition `fun x -> g (f x)` uses the bound names `f` and `g` from the closure — this is correct and simpler than the current approach.

**Removal of special AST nodes:**
- `PipeRight`, `ComposeRight`, `ComposeLeft` removed from `Ast.fs`
- Corresponding match arms removed from: Eval.fs, Bidir.fs, Infer.fs, TypeCheck.fs (5 locations), Format.fs, TypeAnnotationTests.fs
- `PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT` tokens removed from Lexer.fsl, Parser.fsy

**Complexity:** Medium — mechanical removal across many files, but each removal is straightforward. The type-checking correctness follows from the prelude definition being type-inferred normally.

---

### TS-5: Default precedence for unattributed operators — backward compatibility

Any operator defined without a `#[left N]` / `#[right N]` attribute must default to the existing first-character bucket:

| First char | Default level | Default associativity |
|------------|---------------|-----------------------|
| `= < > | & $ !` | 0 | left |
| `@ ^` | 1 | right |
| `+ -` | 2 | left |
| `* / %` | 3 | left |
| `**` (starts with `**`) | 4 | right |

This preserves the behavior of all existing user-defined operators (`<|>`, `^^`, `++`, any user operators) without requiring them to be updated.

**Complexity:** Zero new implementation — this is the existing INFIXOP0-4 classification, already in `classifyInfixOp` in Lexer.fsl. The fixity table's `lookupFixity` returns `None` for unattributed operators, and the caller falls back to the first-character classification.

---

### TS-6: Error message for malformed attribute syntax

When `#[left N]` or `#[right N]` is used with invalid syntax, the error must be clear:

| Error case | Message |
|------------|---------|
| `#[left]` — missing precedence | "Expected precedence level after 'left' in operator attribute" |
| `#[nonassoc 2]` — unknown associativity | "Unknown associativity 'nonassoc'; expected 'left' or 'right'" |
| `#[left 2]` before a non-operator `let` | "Operator attribute #[left N] can only precede an operator definition: let (op) ..." |
| `#[left -1]` — negative precedence | "Precedence level must be a non-negative integer" |
| `#[left 100]` — out of range | Either accept (no upper limit) or bound at 9 (matching INFIXOP0-4 convention + 5 extra levels) |

**Complexity:** Low — these are parse-time and semantic errors, following existing diagnostic patterns (Diagnostic.fs, E0xxx codes).

---

## Differentiators

**Useful extensions but not required for issues #6 and #7.**

### D-1: `#[nonassoc N]` — non-associative operators

Allows declaring that chaining an operator is a parse error: `a op b op c` must be parenthesized.

**Value:** Prevents bugs like `1 < 2 < 3` being silently parsed. The existing grammar already uses `%nonassoc` for `EQUALS LT GT LE GE NE` — this extends that concept to user-defined operators.

**Complexity:** Medium — the Pratt pass must emit a parse error when it encounters two non-associative operators at the same level without parentheses.

**Recommendation:** Defer. The existing comparison operators are already non-associative via the LALR grammar. User-defined operators are unlikely to need this in practice. Add in a follow-up if requested.

---

### D-2: Attribute on `let rec` operator definitions

Allow:
```funlang
#[left 1]
let rec (>>=) m f = ...
```

**Value:** Monadic bind `>>=` is a common operator that benefits from explicit precedence.

**Complexity:** Low — same attribute parsing; the binding node is `LetRecDecl` instead of `LetDecl`. The fixity table registration is the same.

**Recommendation:** Include if the attribute parsing infrastructure handles it naturally. The AST attribute attachment likely covers both `let` and `let rec` with minimal extra work.

---

### D-3: Fixity declaration as a standalone statement (separate from `let`)

Some languages (Haskell's `infixl`, `infixr`) separate fixity declarations from definitions:

```funlang
infixl 1 |>
let (|>) x f = f x
```

**Value:** Allows setting fixity for built-in or imported operators without redefining them.

**Complexity:** High — requires new syntax, new AST node, ordering semantics (declaration must precede use in scope).

**Recommendation:** Do not build. The attribute-on-definition approach covers all use cases in this milestone. Standalone fixity declarations add syntax complexity without clear benefit given FunLang's module system.

---

### D-4: Precedence level validation against existing levels

When a user writes `#[left 3]`, warn if this conflicts with the built-in levels (INFIXOP3 is multiplication level). A warning clarifies that user operators at level 3 will interleave with `*`, `/`, `%`.

**Value:** Prevents surprising interactions.

**Complexity:** Low — a single check at fixity registration time.

**Recommendation:** Defer. The interaction is correct-by-construction (the Pratt pass uses numeric levels consistently), and users who write `#[left 3]` intend to operate at that level.

---

## Anti-Features

**Explicitly do not build these in v12.0.**

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Modify the LALR grammar to be dynamically extensible** | fsyacc generates a static state machine; adding dynamic precedence rules requires regenerating the parser per program. This is architecturally unsound. | Use a static LALR grammar (single INFIXOP bucket for all user ops) + Pratt post-processing pass. Standard approach in OCaml-family parsers. |
| **Remove the INFIXOP0-4 first-character classification** | Over 700 flt tests use operators whose precedence is determined by first character. Removing this default breaks all existing operator code. | Keep the first-character defaults; `#[left N]` overrides them. |
| **Support operator sections `(|> f)` or `(f |>)`** | Partial application of operators in section syntax requires parser changes and is a separate feature (not in issues #6 or #7). | Users write `fun x -> x |> f` instead. |
| **Allow attribute on expression-level `let`** | Expression-level operators are scoped to a block; precedence attributes in expressions would require the Pratt pass to be scope-aware. Complex, low value. | Only support attributes on module-level operator definitions. |
| **`#[left N]` for built-in infix operators (`+`, `-`, `*`, etc.)** | Built-in arithmetic operators are hardcoded as `Add`, `Subtract`, `Multiply` AST nodes — they are not INFIXOP. Changing their precedence is a separate architectural decision. | Leave built-in arithmetic operators alone. |
| **Retroactively add `#[left N]` to all existing Prelude operators** | `<|>`, `^^`, `++` already work correctly via first-character classification. Adding attributes adds noise with zero behavior change. | Only add attributes to operators that need precedence not expressible via first character (i.e., `|>`, `>>`, `<<`). |
| **Pratt parser for the full grammar** | Full Pratt parsing replaces the LALR grammar, which is a complete rewrite of the parser. Out of scope. | Apply the Pratt pass only to the flat chains of user-defined operators within expressions. LALR handles everything else. |
| **`#[precedence N]` without associativity** | Associativity is semantically required to resolve `a op b op c`. A precedence-only attribute forces a default (likely left), which is silently wrong for right-associative operators like `>>`. | Always require explicit `left` or `right`. |

---

## Feature Dependencies

```
TS-1: #[left N] / #[right N] attribute parsing
  └─► TS-2: Fixity table (populated when attributed definitions are loaded)
        └─► TS-3: Pratt post-processor (uses fixity table at expression tree construction)
              └─► TS-4: |>, >>, << moved to Prelude (now safe to remove special AST nodes)

TS-5: Default precedence (no new work — existing first-char classification)
  └─► already satisfies backward compat, blocks nothing

TS-6: Error messages (can be done alongside TS-1)
```

**Critical path:** TS-1 → TS-2 → TS-3 → TS-4.

TS-3 (Pratt post-processor) is the hardest part and is the dependency for everything else. Until the Pratt pass exists, removing the special AST nodes (TS-4) would break `|>`, `>>`, `<<`.

TS-4 is the visible deliverable (issues #6). TS-1 through TS-3 are the enabling infrastructure (issue #7).

**TS-5 is a non-dependency** — it is already implemented via `classifyInfixOp` in the lexer. No new work required. Mentioning it as a feature is important for the requirements document to make clear that backward compat is guaranteed.

---

## Interaction with Existing INFIXOP0-4 Levels

The INFIXOP0-4 levels serve two purposes:

1. **First-character classification at lex time** — `classifyInfixOp` in Lexer.fsl
2. **Parser precedence tokens** — `%left INFIXOP0`, `%left INFIXOP2`, etc.

After reform:

- Purpose 1 is retained unchanged (backward compat for unattributed operators).
- Purpose 2 is the obstacle: if `|>` (starts with `|`) falls into INFIXOP0, and `>>` (starts with `>`) also falls into INFIXOP0, the LALR parser treats them at the same precedence. The Pratt pass then re-associates correctly. This means the LALR grammar only needs to handle the coarse bucket; fine-grained precedence is the Pratt pass's responsibility.

**Numeric precedence scale recommendation:**

Map the existing 5 buckets to a 0-9 integer scale (2 levels per bucket, leaving room for user levels between):

| INFIXOP level | Numeric range | Example operators |
|---------------|---------------|-------------------|
| INFIXOP0 (comparison) | 0-1 | `|>` (level 1), comparison ops, `<|>` |
| INFIXOP1 (concat) | 2-3 | `@@`, `^^`, concat-style |
| INFIXOP2 (additive) | 4-5 | `+`, `-`, additive-style |
| INFIXOP3 (multiplicative) | 6-7 | `*`, `/`, multiply-style |
| INFIXOP4 (exponentiation) | 8-9 | `**`, exponent-style |

`>>` and `<<` should sit at level 2 (above pipe, below additive). This matches F# and Haskell conventions where function composition binds tighter than pipe.

**Specific recommended levels:**

| Operator | Level | Assoc | Rationale |
|----------|-------|-------|-----------|
| `|>` | 1 | left | Lowest user op; chains naturally left-to-right |
| `>>` | 2 | left | Above pipe; `f >> g |> x` = `(f >> g) |> x` |
| `<<` | 2 | right | Same level as `>>`, right-assoc: `f << g << h` = `f << (g << h)` |

---

## Operators: Prelude vs Builtin After Reform

| Operator | Before v12.0 | After v12.0 | Notes |
|----------|-------------|-------------|-------|
| `|>` | Builtin (PIPE_RIGHT token, PipeRight AST) | Prelude/Core.fun | TCO via App trampoline |
| `>>` | Builtin (COMPOSE_RIGHT token, ComposeRight AST) | Prelude/Core.fun | Closure via lambda |
| `<<` | Builtin (COMPOSE_LEFT token, ComposeLeft AST) | Prelude/Core.fun | Closure via lambda |
| `<|>` | Prelude/Option.fun (INFIXOP0 via `<`) | Unchanged | No attribute needed |
| `^^` | Prelude/Core.fun (INFIXOP1 via `^`) | Unchanged | No attribute needed |
| `++` | Prelude (INFIXOP2 via `+`) | Unchanged | No attribute needed |
| `+`, `-`, `*`, `/`, `%` | Builtin AST nodes | Unchanged | Not touched this milestone |
| `=`, `<`, `>`, etc. | Builtin AST nodes | Unchanged | Not touched this milestone |
| `::` | Builtin (CONS token, Cons AST) | Unchanged | Not touched this milestone |

---

## MVP Recommendation

**Minimum to deliver issues #6 and #7:**

1. **Implement TS-1** — Parse `#[left N]` and `#[right N]` before `let (op)` definitions at module level. Attach fixity info to the binding AST node (or store it alongside). Add parse errors (TS-6) for malformed attributes.

2. **Implement TS-2** — Build the fixity table as a module-level mutable dictionary. Populate it during evaluation of module top-level bindings (or during parsing if fixity is needed at parse time — but since we use a Pratt post-pass, evaluation-time population is sufficient for the Pratt pass applied to later expressions).

   **Important ordering consideration:** The Pratt post-pass runs after LALR parsing. If fixity is stored in the parsed AST (on the binding node), the post-pass can use it even on the same file. If fixity is only available after evaluation, the post-pass must run after the fixity-registering bindings are evaluated — which works because FunLang evaluates top-level bindings in order.

3. **Implement TS-3** — Write the Pratt post-processor. Input: a right-leaning `App(App(Var(op), left), right)` tree for a chain of operators. Output: correctly-associated tree based on fixity table. Apply this pass after each `Expr` production in the parser (or as a separate tree-walk after the full parse).

4. **Implement TS-4** — Add `|>`, `>>`, `<<` to Prelude/Core.fun with `#[left 1]`, `#[left 2]`, `#[right 2]` attributes respectively. Remove `PipeRight`, `ComposeRight`, `ComposeLeft` from all files. Remove `PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT` tokens.

5. **Verify TS-5** — Run existing flt test suite. Unattributed operators must behave identically to today.

**Post-MVP (defer):**

| Feature | Reason to Defer |
|---------|-----------------|
| D-1: `#[nonassoc N]` | No current need; comparison operators already non-assoc via grammar |
| D-2: Attribute on `let rec` | Minor; add if the implementation handles it for free |
| D-4: Level conflict warnings | Nice diagnostic, not blocking |

---

## Confidence Assessment

| Area | Confidence | Basis |
|------|------------|-------|
| Existing operator system (INFIXOP0-4) | HIGH | Read Lexer.fsl, Parser.fsy directly; fully understood |
| Scope of hardcoded operator removal (TS-4) | HIGH | Grep found all 12 files containing PipeRight/ComposeRight/ComposeLeft |
| Attribute syntax feasibility (TS-1) | HIGH | `#[` is not currently used in FunLang; straightforward new token + grammar rule |
| Fixity table design (TS-2) | HIGH | Standard approach; no F#/fsyacc-specific obstacles |
| Pratt post-processor necessity (TS-3) | HIGH | LALR cannot use runtime fixity; Pratt post-pass is the established solution |
| TCO preservation for `|>` after reform (TS-4) | MEDIUM | App trampoline handles `f x` tail calls; needs verification that `(|>) x f` as App chain reaches the trampoline in the same way as PipeRight did |
| Backward compat for unattributed operators | HIGH | First-character classification unchanged; no user-visible behavior change |

---

**Document Status:** Research complete for v12.0 Infix Operator Reform — Features dimension
**Next step:** Use this to structure phase requirements (attribute parsing phase, fixity table phase, Pratt pass phase, prelude migration phase)
