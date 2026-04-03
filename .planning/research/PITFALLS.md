# Domain Pitfalls: Infix Operator Reform in FunLang

**Domain:** Reforming an operator system in an existing ML-family interpreter  
**Researched:** 2026-04-02  
**Scope:** Common mistakes when adding user-defined fixity declarations and migrating
`PipeRight`/`ComposeRight`/`ComposeLeft` to the generic infix mechanism in FunLang  
**Project context:** FunLang has 714 flt tests, ~16,600 LOC. `|>` appears in 23 flt tests
and throughout Prelude code. The current pipeline is:
`Lexer → IndentFilter → Parser → AST → Bidir → Eval`.
`PipeRight`/`ComposeRight`/`ComposeLeft` are dedicated AST nodes (Ast.fs lines 105-107)
with handlers in Bidir.fs, Eval.fs, Infer.fs, Format.fs, and ExportApi.fs.

---

## How to Read This File

Each pitfall has a **Phase** tag for the phase most at risk:

- **P1**: Grammar / parser changes (new fixity syntax, Pratt post-processor)
- **P2**: AST migration (retiring dedicated nodes, adding generic `InfixApp`)
- **P3**: Fixity declaration loading (prelude order, scope rules)
- **P4**: Pipeline integration (IndentFilter, error messages, ExportApi)
- **P5**: Consumer coordination (FunLangCompiler, ExportApi typed-AST format)

---

## PART A: Critical Pitfalls (Cause Rewrites or Silent Semantic Breaks)

### Pitfall IR-1: Precedence Changes Alter Existing Code Semantics Silently

**What goes wrong:** The current LALR grammar encodes `|>` as `%left PIPE_RIGHT` at the
*lowest* precedence level — below `OR`, `AND`, and all comparisons. Any change that moves
`|>` relative to other operators changes the parse tree of existing code without a syntax
error. For example, if `|>` were accidentally given higher precedence than comparisons:

```
x = 5 |> f       -- currently: x = (5 |> f)  i.e.  Equal(x, PipeRight(5, f))
                  -- if |> rose above =: (x = 5) |> f  i.e.  PipeRight(Equal(x,5), f)
```

This breaks silently — the code still parses and evaluates without a type error if `f`
accepts a `bool`.

**Why it happens:** FsYacc's `%left`/`%right`/`%nonassoc` table is ordered lowest-to-highest
from top to bottom. Adding a new `infixl` / `infixr` keyword must slot into the correct
position. Off-by-one errors in the ordering table are invisible at build time.

**Consequences:** Existing flt tests pass at the parsing level (the grammar accepts the input)
but produce wrong values. The 23 pipe-using flt tests and any Prelude code using `|>` would
fail with wrong outputs rather than parse errors, making the regression harder to locate.

**Prevention:**
- Lock the precedence of `|>` (PIPE_RIGHT), `>>` (COMPOSE_RIGHT), and `<<` (COMPOSE_LEFT)
  to their current levels throughout the reform. Do not change their numeric positions in
  the `%left`/`%right` table as a side effect of adding new rows.
- After every grammar change that touches the precedence table, run the full 714-test suite
  before proceeding to the next change. Do not batch multiple precedence edits.
- Add a regression test that explicitly checks associativity and precedence of `|>` with
  comparisons: `1 = 2 |> not` must parse as `Equal(1, PipeRight(2, not))` = `false`, not
  as `PipeRight(Equal(1,2), not)` = `true`.

**Warning signs:**
- `|>`-heavy tests in `tests/flt/file/pipe/` or `tests/flt/expr/pipe/` produce wrong numeric
  output (not parse errors).
- `dotnet test` passes but `scripts/fslit tests/flt/` shows output mismatches in pipe tests.

**Phase:** P1. Must be verified after any grammar edit.

---

### Pitfall IR-2: LALR(1) Conflicts When Adding Fixity Declaration Syntax

**What goes wrong:** Adding `infixl` / `infixr` / `infix` as declaration keywords creates
LALR conflicts in two distinct ways:

1. **Keyword collision with identifiers.** FunLang uses bare `IDENT` for many things. If
   `infixl` is added as a keyword token it becomes reserved; any existing code that uses
   `infixl` as a variable name silently breaks. If it is NOT added as a keyword (parsed via
   IDENT dispatch), the parser must lookahead to decide whether `infixl 6 (+)` is a fixity
   declaration or a function call expression — which can require 2+ tokens of lookahead that
   LALR(1) cannot provide.

2. **Reduce-reduce conflict with module-level declarations.** The module rule currently
   matches `TopDecl` alternatives. A fixity declaration `infixl 6 (++)` begins with an IDENT
   (`infixl`) or keyword followed by a `NUMBER` and a `LPAREN IDENT RPAREN`. The `NUMBER`
   token is also the start of an expression. If the module parser cannot distinguish a fixity
   declaration from an expression statement `infixl 6 (+)` (a call to `infixl` with args),
   a reduce-reduce conflict arises on the `NUMBER` token.

**Why it happens:** FsYacc (FunLang's LALR(1) generator) resolves shift-reduce conflicts by
defaulting to shift (usually correct) but resolves reduce-reduce conflicts by choosing the
first rule in the grammar (potentially wrong). It emits a warning to stderr during `dotnet
build` but does NOT fail the build — the conflict is silently accepted with possibly wrong
behavior.

**Consequences:** The grammar compiles but fixity declarations in some syntactic positions
parse as function calls; or fixity declarations shadow variable names silently. The bug is
intermittent: it depends on which token follows the declaration.

**Prevention:**
- Add `INFIXL`, `INFIXR`, `INFIX` as reserved keyword tokens (similar to how `TYPECLASS`
  and `INSTANCE` were added at line 87 of Parser.fsy). This avoids IDENT-dispatch ambiguity.
- After any grammar change, grep the FsYacc build output for `"conflict"`:
  ```
  dotnet build src/FunLang/FunLang.fsproj 2>&1 | grep -i conflict
  ```
  Treat any new conflict as a blocker — do not proceed with conflicts present.
- Use `%nonassoc` for the `INFIXL`/`INFIXR`/`INFIX` keyword token (it only appears at
  declaration level, never in an expression) to prevent it from participating in any
  expression-level shift-reduce decision.

**Warning signs:**
- `dotnet build` output contains `"SR conflict"` or `"RR conflict"`.
- A fixity declaration parses correctly in one position but causes a parse error in another
  (e.g., works as first decl in a module, fails after a `let` decl).

**Phase:** P1. Must be resolved before any fixity declaration tests are written.

---

### Pitfall IR-3: Flat-Chain Ambiguity After Pratt Post-Processing

**What goes wrong:** A Pratt parser for user-defined operators is typically implemented as
a *post-processing pass* that receives a flat list of atoms and operators from the LALR
parser, then rebuilds the tree with correct precedence. The key invariant is: the LALR
grammar must produce a *completely flat chain* — i.e., it must NOT attempt to apply any
precedence at parse time for user-defined operators. If the LALR grammar applies even one
level of precedence (e.g., `INFIXOP2` left-associates operands at parse time as currently
implemented in Parser.fsy lines 327-331), then a mixed expression like:

```
a ++ b ** c     -- ++ is user INFIXOP2, ** is user INFIXOP4
```

gets partially folded by the LALR parser before the Pratt pass sees it. The Pratt pass then
receives `(a ++ b)` as a pre-folded unit and `** c` as a dangling tail — it cannot rebalance
the tree because `(a ++ b)` is already an opaque `App` node, not individual atoms.

**Why it happens:** FunLang's current grammar handles INFIXOP0–4 directly in `Parser.fsy`
(lines 293-355) using the existing Term/Factor hierarchy — not a post-processing Pratt pass.
If the reform strategy is to add a Pratt pass on top of the existing LALR grammar (rather
than replacing the LALR rules), the two mechanisms must not overlap on the same operator
class.

**Consequences:** Expressions involving two different user-defined operators at different
precedence levels parse incorrectly. The wrong operand grouping is used without an error.
For built-in operators (`+`, `-`, `*`, `/`) that have hard-coded LALR rules, this does not
apply — only user-defined `INFIXOP*` operators are affected.

**Prevention:**
- Choose ONE approach, not both: either (A) keep INFIXOP0-4 handled entirely by the LALR
  grammar (current approach), or (B) produce a fully flat token sequence for ALL user
  operators and apply a Pratt pass for all of them. Do not use LALR for some operator
  classes and Pratt for others.
- If adding dynamic fixity (user-declared precedence), approach (B) is required because the
  LALR table is static. In that case, the LALR grammar must emit a flat `InfixChain` node
  containing the unevaluated operand/operator sequence, and the Pratt pass rebuilds the
  correct tree from that.
- The Pratt pass must see ALL operators in a chain simultaneously — it cannot be applied
  incrementally operator-by-operator.

**Warning signs:**
- `a +++ b *** c` (with `+++ :: INFIXOP2`, `*** :: INFIXOP4`) produces the wrong grouping:
  check by asserting `(a +++ (b *** c))` = expected but `((a +++ b) *** c)` = wrong.
- Tests pass for single-operator expressions but fail for chains of two different
  user-defined operators.

**Phase:** P1. The architecture decision (LALR-only vs. Pratt-over-flat-chain) must be made
before any grammar changes are written.

---

### Pitfall IR-4: Removing Dedicated AST Nodes Breaks FunLangCompiler and ExportApi

**What goes wrong:** `PipeRight`, `ComposeRight`, and `ComposeLeft` are dedicated `Ast.Expr`
union cases. They are matched exhaustively in:

| File | Lines | What happens on removal |
|------|-------|------------------------|
| `Bidir.fs` | 737, 747, 761 | Compile error (incomplete match) |
| `Eval.fs` | 1584, 1598, 1611 | Compile error (incomplete match) |
| `Infer.fs` | 407 | Compile error (incomplete match) |
| `Format.fs` | 209-211 | Compile error (incomplete match) |

If the reform plan replaces `PipeRight` with a generic `InfixApp("|>", left, right, span)`
node, every match site above must be updated simultaneously. A partial update compiles
successfully if the new `InfixApp` case is added to `Ast.Expr` before the old cases are
removed — but during the transition window, both representations exist in the AST and any
pass that only handles one form silently ignores the other.

Additionally, `ExportApi.fs` serializes the AST for FunLangCompiler. If FunLangCompiler has
hard-coded handlers for `"PipeRight"`, `"ComposeRight"`, `"ComposeLeft"` in the emitted JSON
or S-expression format, removing these nodes breaks the consumer without a compiler error on
the FunLang side.

**Why it happens:** FunLang's AST discriminated union gives exhaustiveness checking only
within a single project build. FunLangCompiler is a separate consumer; it has no compile-time
link to `Ast.fs`. The FunLang build succeeds after removing AST nodes even if FunLangCompiler
breaks.

**Consequences:** FunLangCompiler silently receives `InfixApp("|>", ...)` nodes it does not
recognize and either crashes at runtime or generates incorrect code for pipe expressions.
This is not caught by `scripts/fslit` because flt tests run the interpreter, not the compiler.

**Prevention:**
- Before removing any AST node, audit all consumers: grep for the node name across the full
  repo including any external projects:
  ```
  grep -rn "PipeRight\|ComposeRight\|ComposeLeft" /Users/ohama/vibe-coding/
  ```
- Coordinate the AST change with FunLangCompiler in the same pull request or in consecutive
  commits with a documented protocol.
- Maintain backward compatibility by keeping the old nodes as aliases or by having
  `ExportApi.fs` emit a compatibility shim: serialize `InfixApp("|>", ...)` as `PipeRight`
  in the export format until FunLangCompiler is updated.
- The safest migration order: (1) add `InfixApp` to AST, (2) update all internal passes to
  handle both, (3) update FunLangCompiler, (4) remove old nodes.

**Warning signs:**
- `grep -rn "PipeRight"` returns zero hits in `*.fs` files after the change, but no compile
  error was thrown — meaning the removal happened but some match site was missed via
  wildcard (`| _ ->`).
- FunLangCompiler tests (if any) fail with "unknown node type" errors.

**Phase:** P2 (AST migration) and P5 (consumer coordination). Must be planned before any
AST node is removed.

---

## PART B: Moderate Pitfalls (Cause Delays and Technical Debt)

### Pitfall IR-5: Prelude Fixity Must Be Known Before User Code Is Parsed

**What goes wrong:** If `|>` and `>>` are migrated from hard-coded tokens to
fixity-declared-in-prelude operators, the Prelude files must be fully loaded (parsed,
evaluated, and their fixity table extracted) BEFORE any user file is lexed or parsed. The
current pipeline in `Program.fs` loads Prelude first, then parses user code — which is
correct. The risk is in two edge cases:

1. **`-e` (inline expression) mode.** `Program.fs` currently supports a `--eval` / `-e` flag
   that parses a string expression. If the `-e` path bypasses `loadPrelude`, user code that
   uses `|>` in an inline expression will fail with "unknown operator" errors. This is
   already a known issue (commit `7f53f3a` fixed a similar prelude-in-e-mode bug).

2. **REPL mode.** The REPL (`Repl.fs`) may parse each line before the fixity table is
   populated if initialization order changes.

**Why it happens:** Fixity information is parser-level state — it must be available at lex
time (to classify tokens) or at Pratt-pass time (to assign precedence). If it is stored in a
module-level mutable (consistent with FunLang's existing `mutableVars` pattern), a stale
empty table at parse time produces wrong results silently.

**Consequences:** `|>` in `-e` mode or REPL throws a parse error or is treated as `INFIXOP0`
(comparison level) instead of its correct precedence — causing silent wrong-precedence
parsing for inline expressions.

**Prevention:**
- Gate any fixity table mutation behind the same initialization check used by prelude loading.
- In `-e` mode, explicitly call the fixity-table initialization step even if no user file is
  loaded (see how commit `7f53f3a` fixed prelude loading for `-e`).
- Add an flt test for `-e` mode with `|>` to ensure regression coverage:
  ```
  // Command: src/FunLang/bin/Release/net10.0/fn -e "5 |> (fun x -> x + 1)"
  // Output: 6
  ```
- In REPL mode, populate the fixity table once at startup, before the first prompt.

**Warning signs:**
- `fn -e "5 |> println"` fails with a parse error after the change.
- REPL throws "unexpected token |>" on the first line that uses `|>`.

**Phase:** P3 (fixity loading). Must be tested immediately after the loading mechanism is
implemented.

---

### Pitfall IR-6: IndentFilter Continuation Detection Breaks for New Operators

**What goes wrong:** `IndentFilter.fs` contains `isContinuationStart` (line 104), which
decides whether a token at the start of an indented line continues the previous expression
or starts a new declaration. Currently it explicitly lists:

```fsharp
| Parser.PIPE_RIGHT | Parser.COMPOSE_RIGHT | Parser.COMPOSE_LEFT -> true
| Parser.INFIXOP0 _ | Parser.INFIXOP1 _ | Parser.INFIXOP2 _ | Parser.INFIXOP3 _ | Parser.INFIXOP4 _ -> true
```

If the reform introduces a new token class (e.g., `INFIXOP_USER` or replaces the hard-coded
tokens with a generic `INFIX_OP of string`) and that token class is not added to
`isContinuationStart`, then multi-line infix expressions silently break:

```
let result =
  someList
  |> filter pred    -- IndentFilter sees |> on a new line; if isContinuationStart
                    -- returns false, it inserts a NEWLINE/DEDENT between lines,
                    -- breaking the pipe chain into two separate expressions
```

**Why it happens:** `isContinuationStart` is a structural match on token tags. Adding new
token cases to the lexer does not automatically update this function — the compiler does not
warn because the existing wildcard or exhaustive match may still compile cleanly.

**Consequences:** Multi-line pipe chains (`x\n  |> f\n  |> g`) silently become parse errors
or evaluate as separate expressions (the second line is treated as a standalone expression
`|> f` which is invalid). This affects 23+ flt tests that use `|>` across lines.

**Prevention:**
- Any new operator token must be added to `isContinuationStart` in the same commit that
  introduces the token.
- Write the flt test for multi-line chained `|>` BEFORE implementing the token change, so
  the test fails visibly if `isContinuationStart` is missed.
- After any token change, search `IndentFilter.fs` for `isContinuationStart` and verify all
  new token cases are covered.

**Warning signs:**
- Tests in `tests/flt/file/pipe/` pass for single-line pipes but fail for multi-line pipes.
- Parser error on `|>` at the start of a continuation line: `"unexpected token |>"`.

**Phase:** P1 (if token classes change) and P4 (IndentFilter integration). Must be updated
in lockstep with any lexer change.

---

### Pitfall IR-7: Pratt Post-Processing Performance Regression

**What goes wrong:** If a Pratt post-processor is added to rebuild operator trees after
LALR parsing, it runs on every expression in every file, including all Prelude files and all
714 flt tests. Naively building a Pratt pass that re-traverses the entire `Expr` tree
(visiting every `App`, `Let`, `LetRec`, `Match`, etc. to find `InfixChain` nodes) adds a
constant multiplier to parse time. For FunLang's interpreted use case this is likely
acceptable, but if the pass uses immutable F# `Map` for the fixity table and rebuilds it at
each operator lookup, the cost per lookup is O(log n) where n is the number of fixity
declarations.

**Why it happens:** F#'s immutable `Map<string, Fixity>` is appropriate for persistent
functional data but has higher constant factor than `Dictionary<string, Fixity>`. With
hundreds of user-defined operators, 714 test files × n fixity lookups per file accumulates.

**Consequences:** `scripts/fslit tests/flt/` noticeably slows. This is a regression that
does not affect correctness but degrades developer experience.

**Prevention:**
- Use `System.Collections.Generic.Dictionary<string, Fixity>` (or F# `dict`) for the
  fixity table — consistent with the `Dictionary<Span, Type>` recommendation in the typed
  AST pitfalls.
- The fixity table is populated once at prelude load time and is read-only during parsing —
  a mutable dictionary initialized once is the right pattern here.
- Gate Pratt re-traversal behind a check: only call the Pratt pass on `InfixChain` nodes;
  skip expression subtrees that contain no user-defined operators (i.e., short-circuit on
  nodes that are not `InfixChain`).

**Warning signs:**
- `time scripts/fslit tests/flt/` is more than 20% slower after the Pratt pass is added.
- Profiling shows the fixity table lookup function consuming >5% of total parse time.

**Phase:** P1 (architecture) and P4 (if integrated). Measure baseline before implementing.

---

### Pitfall IR-8: Error Messages Degrade When Hard-Coded Nodes Are Removed

**What goes wrong:** The current type checker (`Bidir.fs` lines 737-761) provides specific
error messages for misuses of `|>`, `>>`, and `<<` because these are dedicated AST nodes with
dedicated match arms. For example, `Bidir.fs` line 737 for `PipeRight` can say
`"pipe operator |> requires a function on the right"`. If `PipeRight` is replaced by
`InfixApp("|>", ...)`, the generic `InfixApp` handler must reconstruct the operator name
from the string payload to produce an equally specific error. If it falls back to a generic
`"type mismatch in infix expression"`, the quality of error messages for the most common
operators degrades.

**Why it happens:** Specific error messages require knowing which operator caused the error.
A generic `InfixApp` case only knows the operator by its string name at runtime, not at
compile time. The handler must explicitly branch on the operator string to restore specificity:

```fsharp
| InfixApp(op, left, right, span) ->
    match op with
    | "|>" -> (* pipe-specific error *)
    | ">>" | "<<" -> (* compose-specific error *)
    | _ -> (* generic infix error *)
```

If this branching is not added, the specific error messages are permanently lost.

**Consequences:** Users writing `42 |> 5` (pipe with non-function right-hand side) receive
`"type mismatch in expression at line X"` instead of `"right side of |> must be a function,
got int"`. Error quality degrades for the most commonly used operators.

**Prevention:**
- Before removing `PipeRight`/`ComposeRight`/`ComposeLeft`, extract and document all
  error messages currently in their Bidir.fs handlers.
- The `InfixApp` handler in Bidir.fs must include a `match op with` branch for `|>`, `>>`,
  and `<<` with the same messages.
- Write flt tests in `tests/flt/error/` for type errors involving `|>` before the migration,
  asserting the exact error message text. These tests fail if messages degrade.

**Warning signs:**
- Error flt tests for `|>` misuse produce a different error message string after the change.
- `grep -n "pipe\|compose" src/FunLang/Bidir.fs` shows zero hits after the migration.

**Phase:** P2 (AST migration) and P4 (error handling). Preserve error messages explicitly.

---

### Pitfall IR-9: Operator-Section Parsing (`(+)`, `(|>)`) Breaks for New Operators

**What goes wrong:** FunLang supports operator sections — using an operator as a first-class
function by wrapping it in parentheses. The current grammar has explicit rules for this
(Parser.fsy lines 420-424 for INFIXOP0-4, and lines 1003-1007 for operator name extraction).
These rules are enumerated by token class. If new operator token classes are added (e.g., a
generic `INFIX_OP of string`) or if the fixity system introduces operators that do not belong
to INFIXOP0-4 classes, the operator-section rules do not automatically cover them.

For example, if `|>` is migrated from a hard-coded `PIPE_RIGHT` token to a user-fixity
operator, then `(|>)` would need to be handled by the operator-section rule for whatever
token class `|>` is lexed as. If the section rules only list `INFIXOP0`-`INFIXOP4` and not
`PIPE_RIGHT` (or its replacement), `(|>)` fails to parse.

**Why it happens:** The operator-section rules are written as explicit token-class matches,
not as a catch-all. The lexer's `classifyOperator` function assigns `|>` to a specific class,
but the section rule must independently list that class.

**Consequences:** `(|>)` fails with a parse error. Code like `List.map (|>) items funcs`
that uses `|>` as a first-class function breaks.

**Prevention:**
- If `|>` and `>>` stay as hard-coded tokens (`PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT`),
  add explicit operator-section rules for them alongside the INFIXOP0-4 rules.
- If they are migrated to a new generic token, update the section rules to cover the new
  token class.
- Add flt tests for `(|>)` as a value before the migration: `let f = (|>) in f 5 inc`.

**Warning signs:**
- `(|>)` produces `"unexpected token |>"` inside parentheses.
- Operator sections for user-defined operators like `(++)` stop working after a token class
  change.

**Phase:** P1 (grammar changes) and P2 (if token class changes). Easy to miss because
operator sections are an uncommon pattern.

---

## PART C: Minor Pitfalls (Annoying but Fixable)

### Pitfall IR-10: Fixity Scope Creep — Module-Local vs. Global Fixity

**What goes wrong:** In Haskell and OCaml, fixity declarations are module-scoped — `infixl 6
(++)` in module A does not affect module B unless explicitly imported. FunLang currently has
no module-private namespace for operators (all user operators are globally named). If fixity
declarations are added without a scoping rule, two modules that both declare `infixl 6 (++)`
at different levels create an inconsistency: which declaration wins when both modules are
`open`ed?

**Why it happens:** The first implementation of fixity declarations will likely use a single
global fixity table (simplest to implement). This is fine for MVP but becomes a problem when
FunLang projects use multiple modules with conflicting operator definitions.

**Prevention:**
- For MVP: document that fixity declarations are global and last-declaration-wins (consistent
  with FunLang's existing last-definition-wins for type class instances).
- Do NOT attempt to implement module-scoped fixity in the first milestone. Defer to a
  follow-up.
- Record a test that explicitly verifies last-declaration-wins behavior so the semantics are
  locked and visible.

**Warning signs:**
- Two flt tests that each define `infixl 6 (+++)` at different levels interfere when run
  in the same process (if the fixity table is a module-level static).
- The test runner (fslit) runs tests in separate processes, which masks this issue.

**Phase:** P3. Note it as a known limitation with a follow-up ticket.

---

### Pitfall IR-11: `--check` Mode Must Not Apply Pratt Rewriting

**What goes wrong:** FunLang has a `--check` flag (commit `7f53f3a` notes a `--check on
prelude files` fix). If `--check` mode runs only the parser and type-checker but skips the
Pratt post-processor (because it is conceptually a "parse" step), operator trees are not
rebuilt and type checking operates on a malformed AST. Conversely, if the Pratt pass always
runs, it must be safe to call on partially initialized state (before prelude is fully loaded).

**Why it happens:** `--check` mode has a shorter pipeline than normal evaluation. Any new
pass added between parsing and type checking must be explicitly included in the `--check`
code path, or it is silently skipped.

**Prevention:**
- The Pratt post-processing pass should be part of parsing, not a separate pipeline stage.
  Build it into the parser's output so that by the time any downstream pass (including
  `--check`) receives the AST, the operator tree is already correct.
- Search `Program.fs` for all code paths that call the parser and ensure each one includes
  the Pratt pass.

**Warning signs:**
- `fn --check file.fun` passes but `fn file.fun` fails with a type error for operator
  expressions.

**Phase:** P4. Verify `--check` mode explicitly in testing.

---

### Pitfall IR-12: `Format.fs` AST Printer Loses Operator Identity

**What goes wrong:** `Format.fs` currently prints `PipeRight(...)` and `ComposeRight(...)`
as named constructors (lines 209-211). If these are replaced by `InfixApp("|>", ...)`, the
formatter must be updated to print `InfixApp("|>", ...)` instead. If it falls back to a
generic `App(App(...))` representation (since `InfixApp` is desugared to `App(App(...))` in
the parser action), the formatted output loses all trace of the original operator name.

**Why it happens:** If the `InfixApp` desugaring happens at parse time (i.e., the parser
action immediately converts `InfixApp(op, l, r)` to `App(App(Var(op), l), r)` as currently
done for INFIXOP0-1 at lines 295-301 of Parser.fsy), then the AST never contains an
`InfixApp` node — it is already an `App` tree. `Format.fs` has no way to distinguish
`println "hello"` from `println |> arg` in the formatted output.

**Prevention:**
- If preserving the original operator name in the formatted AST output matters (for debugging
  or for ExportApi), keep `InfixApp` as a distinct AST node rather than desugaring at parse
  time.
- Update `Format.fs` in the same commit that changes the AST.
- Check the `emit/ast-expr/` flt tests — they test `--emit-ast` output which uses
  `Format.formatAst`. These tests will fail if operator formatting changes.

**Warning signs:**
- `tests/flt/emit/ast-expr/` tests fail after the AST change.
- Formatted AST shows `App(App(Var("|>"), ...)` instead of `PipeRight(...)` or
  `InfixApp("|>", ...)`.

**Phase:** P2 (AST migration). Update `Format.fs` alongside `Ast.fs`.

---

## PART D: Phase-Specific Warning Summary

| Phase | Topic | Most Likely Pitfall | Mitigation |
|-------|-------|--------------------|-|
| P1 | Grammar/precedence table | Precedence shift silently breaks `|>` semantics (IR-1) | Run full flt suite after each table change |
| P1 | Fixity declaration syntax | LALR conflicts from new keyword (IR-2) | Reserved keywords; check `dotnet build` output for "conflict" |
| P1 | Pratt + LALR interaction | Flat-chain ambiguity for mixed operators (IR-3) | Choose one mechanism; document the decision |
| P1 | Token classes | Operator sections break for new/changed tokens (IR-9) | Add section rules alongside token introduction |
| P2 | AST node removal | FunLangCompiler and ExportApi break silently (IR-4) | Grep all consumers; migrate in stages |
| P2 | AST node removal | Error messages degrade for `|>`, `>>`, `<<` (IR-8) | Preserve specific Bidir error messages explicitly |
| P2 | Format.fs | Operator identity lost in AST printer (IR-12) | Update Format.fs in same commit as Ast.fs |
| P3 | Fixity loading order | `-e` mode and REPL skip fixity init (IR-5) | Explicit fixity init in all entry points; flt test for `-e` mode |
| P3 | Fixity scope | Global table causes cross-module conflicts (IR-10) | Document last-wins; defer scoping |
| P4 | IndentFilter | Multi-line pipe chains break (IR-6) | Update `isContinuationStart` in same commit as token changes |
| P4 | `--check` mode | Pratt pass skipped in check pipeline (IR-11) | Embed Pratt in parser output, not as a separate stage |
| P4 | Performance | Pratt pass too slow on 714 tests (IR-7) | Use `Dictionary` for fixity table; measure baseline first |
| P5 | FunLangCompiler | Removed AST nodes break consumer (IR-4) | Coordinate with consumer; emit compatibility shim in ExportApi |

---

## PART E: FunLang-Specific Architecture Risks

### Risk IR-AX-1: Hard-Coded Tokens Are an Asset, Not a Liability

The current system has `PIPE_RIGHT`, `COMPOSE_RIGHT`, and `COMPOSE_LEFT` as named token
types. This is not technical debt — it gives the lexer O(1) classification, gives the LALR
table deterministic precedence, and gives Bidir.fs type-safe match exhaustion. The reform
should preserve these properties. If the goal is only to add *new* user-defined operators
with custom fixity, the cleanest approach is to keep the hard-coded tokens for built-in
operators and add the fixity mechanism only for INFIXOP0-4 user operators. Migrating
hard-coded tokens to the dynamic fixity system is optional and carries the IR-4, IR-6, IR-8,
IR-9 risks with no user-visible benefit.

**Recommendation:** Keep `PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT` as hard-coded tokens.
Add fixity declarations only for the INFIXOP0-4 category, where the first-character rule is
already the precedence assignment mechanism. This is the minimal-risk path.

---

### Risk IR-AX-2: The `classifyOperator` Function in Lexer.fsl Is the Fixity Bottleneck

The lexer's `classifyOperator` (Lexer.fsl lines 11-22) hard-codes the precedence of any
user-defined operator based on its first character. This is how `++` becomes `INFIXOP2` and
`**` becomes `INFIXOP4` without any declarations. If dynamic fixity is added, the lexer can
no longer assign the token class at lex time (because the class depends on declarations not
yet parsed). The lexer must either emit a single `INFIXOP_UNKNOWN` token and let the Pratt
pass assign precedence, or it must consult a pre-populated fixity table at lex time. The
former requires changing the LALR grammar; the latter requires the fixity table to be
populated before lexing (requiring Prelude to be evaluated before any user file is even
lexed, which is the current order — but must be preserved explicitly).

**Recommendation:** Document this dependency explicitly in the implementation plan. The
correct load order is: (1) lex and parse Prelude files using default precedences, (2)
extract fixity declarations from Prelude, (3) populate fixity table, (4) lex and parse user
files using the populated table.

---

## Sources

- FunLang source: `/src/FunLang/Parser.fsy` — precedence table (lines 98-111), INFIXOP0-4
  rules (lines 293-355), existing LALR conflict notes (lines 247, 894)
- FunLang source: `/src/FunLang/Lexer.fsl` — `classifyOperator` (lines 11-22), hard-coded
  pipe/compose tokens (lines 127-130), operator catch-all rule (line 168)
- FunLang source: `/src/FunLang/IndentFilter.fs` — `isContinuationStart` (lines 104-109)
- FunLang source: `/src/FunLang/Ast.fs` — `PipeRight`/`ComposeRight`/`ComposeLeft` nodes
  (lines 105-107)
- FunLang source: `/src/FunLang/Eval.fs` — pipe/compose handlers (lines 1584-1621)
- FunLang source: `/src/FunLang/Bidir.fs` — pipe/compose type inference (lines 737-761)
- FunLang source: `/src/FunLang/Infer.fs` — stub handlers for pipe/compose (line 407)
- FunLang source: `/src/FunLang/Format.fs` — AST printer for pipe/compose (lines 209-211)
- FunLang tests: `tests/flt/expr/pipe/`, `tests/flt/file/pipe/`, `tests/flt/expr/compose/`,
  `tests/flt/file/operator/` — 23 pipe tests, 8 compose tests, 3 operator definition tests
- Git history: commit `7f53f3a` — prelude-in-e-mode bug (precedent for IR-5)
- "Crafting Interpreters" (Nystrom) — Pratt parsing chapter, flat-chain invariant
- GHC Commentary on Fixity Resolution — post-parse Pratt pass architecture
- OCaml source: `parsing/lexer.mll` — INFIXOP0-4 first-character classification (same
  approach used in FunLang)
