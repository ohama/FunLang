# Architecture Patterns: Infix Operator Reform

**Domain:** FunLang interpreter — adding user-defined operator fixity declarations
**Researched:** 2026-04-02
**Confidence:** HIGH (full codebase inspection, 16,551 lines across 20 source files)

---

## Current Architecture: How Operators Flow Today

The existing pipeline for a source file is:

```
Source text
    -> Lexer.fsl          (tokenize; |> -> PIPE_RIGHT, >> -> COMPOSE_RIGHT, << -> COMPOSE_LEFT,
                            op_char+ -> classifyOperator -> INFIXOP0..4)
    -> IndentFilter.fs     (NEWLINE -> INDENT/DEDENT; continuation detection for PIPE_RIGHT etc.)
    -> Parser.fsy          (LALR(1); precedence from %left/%right/%nonassoc; builds AST)
    -> TypeCheck.fs        (collect refs, rewrite module access, then Bidir.synth)
    -> Bidir.fs            (synth cases for PipeRight/ComposeRight/ComposeLeft)
    -> Eval.fs             (eval cases for PipeRight/ComposeRight/ComposeLeft)
```

### Existing Special Tokens

`|>`, `>>`, `<<` are hard-coded into the lexer as named tokens (`PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT`) rather than going through the `classifyOperator` catch-all. This means they have dedicated grammar rules in Parser.fsy that produce dedicated AST nodes (`PipeRight`, `ComposeRight`, `ComposeLeft`), and every downstream consumer has explicit match arms for those three nodes.

### INFIXOP0..4 Path

User-defined operators that go through `classifyOperator` (e.g., `^^`, `**`) produce `INFIXOP0..4` tokens with the operator string as payload. Parser.fsy folds them into `App(App(Var(op), lhs), rhs)` directly — no dedicated AST node. Downstream code (Eval, Bidir, TypeCheck) handles them automatically via the `Var`/`App` cases already in place. This is the correct end-state for ALL user operators.

---

## Integration Map: Every File That Must Change

### Files Containing Hardcoded PipeRight/ComposeRight/ComposeLeft

| File | Lines | What it contains | Change required |
|------|-------|-----------------|-----------------|
| `Ast.fs` | 401 | DU cases `PipeRight`, `ComposeRight`, `ComposeLeft`; `spanOf` arms; `Decl` type | Add `InfixDecl`; optionally keep or remove pipe/compose DU cases |
| `Lexer.fsl` | 197 | Hardcoded `\|>`, `>>`, `<<` rules emitting named tokens | Convert to `classifyOperator` path or keep tokens but change parser |
| `Parser.fsy` | 1,012 | `%token PIPE_RIGHT COMPOSE_RIGHT COMPOSE_LEFT`; `%left PIPE_RIGHT` etc.; grammar rules building `PipeRight(...)` etc. | Add `InfixDecl` grammar rule; change pipe/compose rules |
| `IndentFilter.fs` | 678 | `isContinuationStart` matches `PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT` explicitly | Update for new token set |
| `Bidir.fs` | 1,230 | `synth` cases for `PipeRight`, `ComposeRight`, `ComposeLeft` | Remove if desugared away; or keep |
| `Eval.fs` | 1,818 | `eval` cases for `PipeRight`, `ComposeRight`, `ComposeLeft` | Remove if desugared away; or keep |
| `TypeCheck.fs` | 1,318 | `collectMatches`, `collectTryWiths`, `collectModuleRefs`, `rewriteModuleAccess` arms | Remove matching arms if desugared |
| `Infer.fs` | 464 | Stub arms `PipeRight _ \| ComposeRight _ \| ComposeLeft _ -> (empty, freshVar())` | Remove if desugared |
| `Format.fs` | 372 | `formatToken` (PIPE_RIGHT etc.) and `formatAst` (PipeRight/ComposeRight/ComposeLeft) | Update to print new forms |

### Files That Need New Logic

| File | Why |
|------|-----|
| `Ast.fs` | New `InfixDecl` variant in `Decl` type; new `InfixChain` variant OR rely on `App(App(...))` desugaring |
| `Parser.fsy` | New grammar rule: `infixl`/`infixr`/`infix` declaration syntax |
| `TypeCheck.fs` | Collect and validate `InfixDecl`s; thread `FixityEnv` through type checking |
| `Eval.fs` | Thread `FixityEnv` into `evalModuleDecls` (or fold into existing env) |
| `Prelude.fs` | Thread `FixityEnv` through prelude loading; expose it in `PreludeResult` |
| `Program.fs` | Pass `FixityEnv` from prelude into all pipeline entry points |

---

## Recommended Architecture: Flat-Chain + Post-Parse Pratt Rewrite

### Core Design Decision

The cleanest approach for arbitrary user-defined operator precedence is a two-phase operator parse:

1. **Parse phase**: The LALR(1) grammar collects sequences of infix operator uses into a flat `InfixChain` node rather than encoding all possible precedence levels in the grammar itself. The LALR grammar still handles EXISTING operators correctly via `%left`/`%right` declarations.

2. **Rewrite phase**: A Pratt parser pass runs immediately after LALR(1) and walks `InfixChain` nodes, consulting a `FixityEnv` built from `InfixDecl` declarations, to produce a properly-nested `App(App(...))` tree.

This approach has one constraint: `InfixDecl` declarations in a module must be visible to the Pratt rewrite pass before the pass runs. This is achievable by scanning the LALR AST for `InfixDecl` nodes in a single linear pass before running the Pratt rewrite.

### The FixityEnv Type

```fsharp
// In a new FixityEnv.fs (or at the bottom of Ast.fs)
type Associativity = AssocLeft | AssocRight | AssocNone

type FixityInfo = {
    Level: int           // 0-9, where 9 is tightest (mirrors INFIXOP0..4 scheme)
    Assoc: Associativity
}

type FixityEnv = Map<string, FixityInfo>
```

**Where to store it**: A standalone `Map<string, FixityInfo>` passed through the pipeline as an explicit parameter. Do NOT fold it into `TypeEnv` or `Env` — it is used during the parse/rewrite phase (before type checking), so it must be available earlier than those environments.

**Built-in defaults** (pre-populate FixityEnv with known operators):
- `"|>"`: level 1, left (matching current LALR precedence behavior)
- `">>"`: level 2, left
- `"<<"`: level 2, right

This ensures existing code works with no source changes. Note that `|>` having lower precedence than `>>` means `a |> f >> g` correctly parses as `a |> (f >> g)`.

### New AST Node: InfixChain

```fsharp
// In Ast.fs, in the Expr DU:
| InfixChain of operands: Expr list * operators: (string * Span) list * span: Span
// Invariant: List.length operands = List.length operators + 1
// Example: a + b + c becomes InfixChain([a; b; c], [("+", s1); ("+", s2)], span)
// Example: a |> f >> g becomes InfixChain([a; f; g], [("|>", s1); (">>", s2)], span)
```

This node exists only transiently. It must be eliminated by the Pratt rewrite pass before any downstream consumer sees it. Add a validation assertion in `Bidir.synth` and `Eval.eval` that raises an internal error if `InfixChain` reaches them.

**Critical scope of use**: `InfixChain` is only needed for NEW user-defined operators (INFIXOP* path) where the grammar cannot know the precedence. Existing arithmetic operators (`+`, `-`, `*`, `/`) continue to use the Term/Factor grammar hierarchy and are NOT routed through `InfixChain`. The LALR grammar already handles their precedence correctly.

### New Declaration Node: InfixDecl

```fsharp
// In Ast.fs, in the Decl DU:
| InfixDecl of assoc: string * level: int * ops: string list * Span
// Examples:
//   InfixDecl("left", 5, ["<|>"; ">>?"], span)    -- infixl 5 <|> >>?
//   InfixDecl("right", 6, ["^^^"], span)           -- infixr 6 ^^^
//   InfixDecl("none", 4, ["==="], span)            -- infix 4 ===
```

---

## Parser Grammar Changes (Parser.fsy)

### New tokens to add

```
%token INFIXL INFIXR INFIX_KW    // 'infixl', 'infixr', 'infix' keywords
```

In Lexer.fsl, add these as keyword rules (before the identifier catch-all):
```
| "infixl"  { INFIXL }
| "infixr"  { INFIXR }
| "infix"   { INFIX_KW }
```

### InfixDecl grammar rule

In the module declaration grammar (alongside `LetDecl`, `TypeDecl`, etc.):

```yacc
ModuleItem:
    | INFIXL NUMBER OperatorNameList  { InfixDecl("left", $2, $3, ruleSpan parseState 1 3) }
    | INFIXR NUMBER OperatorNameList  { InfixDecl("right", $2, $3, ruleSpan parseState 1 3) }
    | INFIX_KW NUMBER OperatorNameList { InfixDecl("none", $2, $3, ruleSpan parseState 1 3) }

OperatorNameList:
    | LPAREN INFIXOP0 RPAREN                     { [$2] }
    | LPAREN INFIXOP1 RPAREN                     { [$2] }
    | LPAREN INFIXOP2 RPAREN                     { [$2] }
    | LPAREN INFIXOP3 RPAREN                     { [$2] }
    | LPAREN INFIXOP4 RPAREN                     { [$2] }
    | LPAREN PIPE_RIGHT RPAREN                   { ["|>"] }
    | LPAREN COMPOSE_RIGHT RPAREN                { [">>"] }
    | LPAREN COMPOSE_LEFT RPAREN                 { ["<<"] }
    | OperatorNameList LPAREN INFIXOP0 RPAREN    { $1 @ [$3] }
    // ...etc...
```

Operator names in `infixl`/`infixr`/`infix` declarations use the parenthesized form `(op)` to distinguish them from function application. This is consistent with how FunLang already allows `(++)` as a function reference.

### Existing INFIXOP grammar rules

Currently `Expr INFIXOP0 Expr` etc. produce `App(App(Var(op), lhs), rhs)` directly. Under the new approach, these should instead produce `InfixChain` entries. However, since the LALR grammar already encodes relative precedence between INFIXOP levels via `%left INFIXOP0` through `%right INFIXOP4`, simply wrapping in `InfixChain` without changing the grammar structure achieves nothing — the LALR parser has already structured the tree by level.

The correct scope for `InfixChain`: within a single INFIXOP level when a user has declared a DIFFERENT fixity. Example: if `<|>` is currently INFIXOP0 (because `<` starts the operator) but the user declares `infixl 6 (<|>)`, the user expects `<|>` to bind tighter than `+`. The LALR grammar cannot accommodate this because `INFIXOP0` is at comparison-level precedence.

**Resolution**: The Pratt rewrite runs on the LALR output and re-associates operator chains where a custom fixity declaration overrides the default LALR classification. For operators that have no `InfixDecl`, the LALR tree is authoritative.

---

## Data Flow Changes

### Current flow (simplified):

```
Source -> Lex -> IndentFilter -> LALR parse -> TypeCheck -> Bidir/Eval
                                  (PipeRight/ComposeRight/ComposeLeft AST nodes in place)
```

### New flow:

```
Source -> Lex -> IndentFilter -> LALR parse -> CollectFixity -> PrattRewrite -> TypeCheck -> Bidir/Eval
                                  (InfixChain nodes   (FixityEnv built)     (all App(App(...)) by here)
                                   for new user ops)
```

### FixityEnv Threading

`FixityEnv` is built from three sources in order:

1. **Built-in defaults** (hardcoded in a `defaultFixityEnv` constant in `FixityEnv.fs`)
2. **Prelude InfixDecls** (collected during `loadPrelude`, accumulated across prelude files)
3. **User file InfixDecls** (collected from the LALR AST before Pratt rewrite of that file)

Threading diagram:

```
Program.fs:
    builtinFixity: FixityEnv    <- defaultFixityEnv (hardcoded)
    prelude = loadPrelude(...)   <- PreludeResult now includes FixityEnv field
    userFixity = builtinFixity ++ prelude.FixityEnv

    parseAndRewrite input filename userFixity:
        lalrModule = LALR parse (may contain InfixChain nodes for user-defined ops)
        localDecls = collectInfixDecls lalrModule    <- one linear pass
        localFixity = userFixity ++ localDecls
        rewrittenModule = prattRewrite localFixity lalrModule
        -- rewrittenModule has NO InfixChain nodes

    TypeCheck.typeCheckModuleWithPrelude ... rewrittenModule
    Eval.evalModuleDecls ... rewrittenModule
```

**Key constraint**: An `infixl`/`infixr`/`infix` declaration anywhere in a file applies to the entire file (same as Haskell behavior). The `collectInfixDecls` scan is a full-module sweep before the Pratt rewrite starts.

---

## Component Boundaries

### New Component: FixityEnv.fs (recommended as new file)

Responsibility: Define `FixityEnv` type, `defaultFixityEnv`, and the Pratt rewrite function.

```fsharp
// FixityEnv.fs
module FixityEnv

open Ast

type Associativity = AssocLeft | AssocRight | AssocNone
type FixityInfo = { Level: int; Assoc: Associativity }
type FixityEnv = Map<string, FixityInfo>

val defaultFixityEnv : FixityEnv
    // Pre-populated: |> (level 1, left), >> (level 2, left), << (level 2, right)

val collectInfixDecls : Decl list -> FixityEnv
    // Scan for InfixDecl nodes in a declaration list; return as FixityEnv

val prattRewrite : FixityEnv -> Module -> Module
    // Walk AST, resolve InfixChain nodes to App(App(...)) using FixityEnv
    // Panics if InfixChain nodes remain after rewrite (defense in depth)
```

**Build order in .fsproj**: Place `FixityEnv.fs` AFTER `Ast.fs` and BEFORE `Parser.fs` (generated) and `Prelude.fs`.

The Pratt rewrite function itself is a recursive AST transformation. Each `InfixChain([e1; e2; e3], [op1; op2], span)` is processed by a standard precedence-climbing algorithm:

```
prattParse operands operators fixityEnv:
    1. Find the operator with the highest precedence among operators
    2. Split the chain at that operator
    3. Decide associativity to handle ties
    4. Recursively rewrite left and right subchainsthe
    5. Return App(App(Var(op), left_result), right_result)
```

### Modified Component: Prelude.fs

`PreludeResult` gains a `FixityEnv` field:

```fsharp
type PreludeResult = {
    Env: Env
    TypeEnv: TypeEnv
    CtorEnv: ConstructorEnv
    RecEnv: RecordEnv
    ClassEnv: ClassEnv
    InstEnv: InstanceEnv
    Modules: Map<string, ModuleExports>
    ModuleValueEnv: Map<string, ModuleValueEnv>
    FixityEnv: FixityEnv        // NEW
}
```

`loadPrelude` accumulates `FixityEnv` across prelude files the same way it accumulates `TypeEnv`. The Pratt rewrite is applied to each prelude file after collecting that file's own `InfixDecl`s merged with the accumulated prelude fixity.

The `emptyPrelude` constant gains `FixityEnv = defaultFixityEnv` (not `Map.empty` — built-in defaults must always be present).

---

## Backward Compatibility Strategy

### Phase Approach (Recommended)

**Phase 1 (no behavior change)**: Add `InfixDecl` and `FixityEnv` infrastructure. The Pratt rewrite exists but only handles `InfixChain` nodes — and since no LALR rules produce `InfixChain` yet, it is a no-op. All 714 tests pass unchanged.

**Phase 2 (INFIXOP* through Pratt)**: Change LALR rules for `Expr INFIXOP* Expr` to produce `InfixChain` instead of `App(App(...))`. Pratt rewrite resolves them using the default INFIXOP level-to-precedence mapping. For operators with no `InfixDecl`, the default mapping preserves current behavior exactly. For operators WITH an `InfixDecl`, the Pratt rewrite overrides. Tests pass because default mapping is identical to current grammar.

**Phase 3 (user fixity works)**: A user can write `infixl 6 (<|>)` and subsequently use `x <|> y` with left-associativity at level 6 regardless of the operator's leading character. New tests added.

**Phase 4 (optional: remove PipeRight/ComposeRight/ComposeLeft)**: Deferred cleanup. Remove dedicated AST nodes from all 9 files after `|>`, `>>`, `<<` are defined in Prelude and handled via `App(App(...))`.

### Why the phased approach is necessary

There are 29 tests using `|>`, 8 tests using `>>`, and 4 tests using `<<` in the `tests/flt/` directory. Several of these test the AST output format (`--emit-ast`) and would break if the AST node names changed without updating expected outputs. The phased approach keeps the AST node names stable through Phase 3, deferring the breaking cleanup to an explicit Phase 4 where the test updates are intentional.

---

## Suggested Build Order for Phases

### Phase 1: Fixity Infrastructure (no behavior change)

**Goal**: Add all new types and infrastructure without changing how anything parses or evaluates.

Files to change (in F# compile order):
1. `Ast.fs` — add `InfixDecl` to `Decl` DU; add `InfixChain` to `Expr` DU; extend `spanOf` and `declSpanOf`
2. **New** `FixityEnv.fs` — `FixityInfo`, `FixityEnv`, `defaultFixityEnv`, `collectInfixDecls`, `prattRewrite` stub
3. `FunLang.fsproj` — insert `FixityEnv.fs` between `Ast.fs` and generated `Lexer.fs`
4. `Lexer.fsl` — add `infixl`, `infixr`, `infix` keyword rules
5. `Parser.fsy` — add `INFIXL`, `INFIXR`, `INFIX_KW` tokens; add `InfixDecl` grammar rules; add `OperatorNameList` non-terminal
6. `TypeCheck.fs` — add `InfixDecl` arm to `collectMatches`, `collectTryWiths`, `collectModuleRefs`, `rewriteModuleAccess` (all just return empty/pass-through)
7. `Eval.fs` — add `InfixDecl` arm in `evalModuleDecls` (skip, like `TypeClassDecl`)
8. `Format.fs` — add `InfixChain` and `InfixDecl` formatting
9. `Prelude.fs` — add `FixityEnv` field to `PreludeResult`; initialize to `defaultFixityEnv`

**Touchpoints NOT changed in Phase 1**: `Bidir.fs`, `Infer.fs`, `IndentFilter.fs`, `Exhaustive.fs`, `Elaborate.fs`.

**Test gate**: All 714 flt tests pass. New tests: parsing `infixl 5 (^^)` produces correct `InfixDecl` in `--emit-ast` output.

### Phase 2: INFIXOP* Operators Through Pratt

**Goal**: All `INFIXOP*` operators produce `InfixChain` from the LALR parser, and the Pratt rewrite resolves them. Behavior is semantically identical to before.

Files to change:
1. `Parser.fsy` — change 5 `Expr INFIXOP* Term/Factor/Expr` rules to produce `InfixChain`; add chain-extension helper
2. `FixityEnv.fs` — implement full Pratt rewrite (replace stub); add default INFIXOP-level-to-precedence mapping
3. `Program.fs` — call `prattRewrite localFixity module` after LALR parse, before TypeCheck; wire `FixityEnv` from prelude

**Risk area**: The LALR grammar has different non-terminals for different INFIXOP levels (`Expr INFIXOP0 Expr` vs `Term INFIXOP3 Factor`). These distinctions exist to encode precedence. When converting to `InfixChain`, the operands at different levels are already structured by the LALR precedence hierarchy. The Pratt rewrite should respect the already-structured sub-trees and only re-associate at the same chain level. This is the subtle part: the Pratt rewrite is not starting from a fully flat chain; it is re-associating within groups that LALR has already partially structured. The `buildOrExtendChain` helper function must be careful to only chain operators at the same INFIXOP level together.

**Simpler alternative**: Only use `InfixChain` at the TOP-LEVEL Expr grammar (the `%left PIPE_RIGHT` etc. section), not inside the Term/Factor hierarchy. This means the Pratt rewrite only handles pipe/compose-level operators, not arithmetic. Arithmetic operators keep their Term/Factor structure. This is less general but much lower risk.

**Test gate**: All 714 flt tests pass. All operator tests specifically pass. New test: `infixl 5 (^^)` allows `a ^^ b ^^ c` to associate correctly left.

### Phase 3: User-Defined Fixity Works End-to-End

**Goal**: A file can write `infixl 6 (<|>)` and subsequently use `x <|> y` with left-associativity at precedence level 6.

Files changed:
1. `FixityEnv.fs` — handle user `InfixDecl` entries in Pratt rewrite
2. `Prelude.fs` — accumulate `InfixDecl` from prelude files into `PreludeResult.FixityEnv`
3. Integration tests covering: custom fixity in user file, custom fixity from Prelude, interaction with existing operators

**Test gate**: All 714 flt tests pass. New tests for user-defined operator fixity.

### Phase 4: Remove PipeRight/ComposeRight/ComposeLeft (Optional, Post-MVP)

**Goal**: `|>`, `>>`, `<<` become ordinary operators handled by `App(App(...))`. No dedicated AST nodes.

Prerequisite: `|>`, `>>`, `<<` must be defined as functions in Prelude:
```
// Prelude/Core.fun
infixl 1 (|>)
let (|>) x f = f x

infixl 9 (>>)
let (>>) f g = fun x -> g (f x)

infixr 9 (<<)
let (<<) f g = fun x -> f (g x)
```

Files to change (all 9 hardcoded locations):
1. `Ast.fs` — remove `PipeRight`, `ComposeRight`, `ComposeLeft` from `Expr`; update `spanOf`
2. `Lexer.fsl` — remove dedicated `|>`, `>>`, `<<` rules (they now go through `classifyOperator`)
3. `Parser.fsy` — remove `%token PIPE_RIGHT COMPOSE_RIGHT COMPOSE_LEFT`; remove `%left PIPE_RIGHT` etc.; remove 3 grammar rules
4. `IndentFilter.fs` — remove `PIPE_RIGHT | COMPOSE_RIGHT | COMPOSE_LEFT` from `isContinuationStart` (already covered by `INFIXOP*` cases)
5. `Bidir.fs` — remove 3 synth cases (~40 lines at 737-773)
6. `Eval.fs` — remove 3 eval cases (~40 lines at 1584-1621)
7. `TypeCheck.fs` — remove 4 match arms across `collectMatches`, `collectTryWiths`, `collectModuleRefs`, `rewriteModuleAccess`
8. `Infer.fs` — remove 1 stub arm (line 407-408)
9. `Format.fs` — remove 3 format cases for `formatToken` and `formatAst`

**Test gate**: All 714 flt tests pass including the 29 `|>` tests and 8 `>>` tests. The `--emit-ast` tests for compose (e.g., `ast-expr-compose-right.flt`) will need updated expected output (from `ComposeRight (Var "f", Var "g")` to `App (App (Var ">>", Var "f"), Var "g")`).

---

## Impact on IndentFilter.fs

`isContinuationStart` currently lists `PIPE_RIGHT`, `COMPOSE_RIGHT`, `COMPOSE_LEFT` explicitly (lines 106-107). It also covers `INFIXOP0..4` at lines 108-109. Therefore:

- In Phase 4, removing the three explicit cases is sufficient — `INFIXOP*` already covers them after the lexer routes `|>`, `>>`, `<<` through `classifyOperator`.
- No logic change is needed in Phases 1-3.
- The new `INFIXL`, `INFIXR`, `INFIX_KW` keyword tokens should NOT be added to `isContinuationStart` — they are declaration keywords, not expression-level operators.

---

## Impact on Prelude Loading

`Prelude.loadPrelude` calls `parseModuleFromString`, then `typeCheckModuleWithPrelude`, then `evalModuleDecls` for each `.fun` file. After operator reform, it must also:

1. Call `prattRewrite` after LALR parse (inserting one step)
2. Accumulate `FixityEnv` from each file's `InfixDecl` nodes

The accumulated prelude `FixityEnv` is then available to user files via `PreludeResult.FixityEnv`.

Because prelude files are loaded in topological order (already handled by `resolveLoadOrder`), a fixity declared in `Core.fun` will be available when `List.fun` is loaded. This matches the behavior for `TypeEnv` accumulation.

**`loadAndTypeCheckFileImpl` and `loadAndEvalFileImpl`** (file-level imports via `import`) also need the Pratt rewrite step. They must receive `FixityEnv` as a parameter alongside the existing `cEnv`, `rEnv`, `typeEnv`, etc. This adds one parameter to both functions and to the delegate types `TypeCheck.fileImportTypeChecker` and `Eval.fileImportEvaluator`.

---

## Where Attribute Declarations Get Stored

`InfixDecl` nodes live in the `Decl list` of a module — the same list that holds `LetDecl`, `TypeDecl`, etc. They are NOT stored in `TypeEnv`, `Env`, or any runtime environment because they have no runtime meaning.

The `FixityEnv` (extracted from `InfixDecl` nodes) is a compile-time / parse-time structure. It is:
- Built once per module (during the `collectInfixDecls` scan)
- Merged with the accumulated prelude `FixityEnv`
- Used during `prattRewrite`
- NOT passed to type checking or evaluation

`TypeCheck.fs` and `Eval.fs` see `InfixDecl` nodes in the declaration list and skip them (similar to how `TypeClassDecl` is skipped by `Elaborate`). The fixity information has already been consumed by the time those phases run.

---

## Scalability Considerations

| Concern | Current | After Reform |
|---------|---------|--------------|
| Adding new operators | Requires lexer + parser + AST + downstream changes | Just write `infixl N (op)` in source |
| Type checking operators | Dedicated synth cases per operator | All user ops -> `App(App(...))` -> existing App synth |
| Eval operators | Dedicated eval cases per operator | All user ops -> `App(App(...))` -> existing App eval |
| Pratt rewrite overhead | N/A | O(n) per module, negligible for interpreter scale |
| Test surface area | 9 files hardcode pipe/compose | After Phase 4: 0 files special-case them |
| `--emit-ast` output | Dedicated node names (`PipeRight`) | After Phase 4: `App (App (Var "|>", ...))` — breaking for flt tests |

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Global Mutable FixityEnv

Do not store `FixityEnv` in a global mutable (analogous to `Eval.currentEvalFile`). The codebase uses global mutables for file path tracking (acceptable for single-threaded use). But `FixityEnv` is module-scoped: each file import brings its own local fixity declarations. A global mutable would cause incorrect fixity inheritance across file imports. Pass it as a parameter.

### Anti-Pattern 2: Encoding User Fixity in New LALR Tokens

Do not add more `INFIXOP` levels to the grammar to represent user-defined precedence. There are already 5 levels (INFIXOP0-4). Adding more requires regenerating `Parser.fs` and still cannot handle runtime-declared precedence from Prelude files. The Pratt rewrite handles arbitrary precedence without grammar changes.

### Anti-Pattern 3: Running Pratt Rewrite Inside TypeCheck.fs

The Pratt rewrite must run on the AST BEFORE type checking, not inside it. It should be an explicit step in `Program.fs` (and `Prelude.fs`). Mixing parse-phase concerns into TypeCheck.fs violates the clear phase separation that the current architecture enforces.

### Anti-Pattern 4: Removing PipeRight/ComposeLeft Before Prelude Defines Them

Phase 4 (removing dedicated nodes) requires that `|>`, `>>`, `<<` be defined in Prelude first. Do not remove the Eval/Bidir handlers until the Prelude provides the function implementations. The 29+ tests using `|>` will break if this order is violated.

### Anti-Pattern 5: Flat `InfixChain` Spanning Arithmetic and Pipe Levels

Do not route arithmetic operators (`+`, `-`, `*`, `/`) through `InfixChain`. The Term/Factor grammar hierarchy already handles their precedence correctly and is well-tested. `InfixChain` is for operators that the LALR grammar cannot statically encode (because their precedence is user-declared). Limiting `InfixChain` to the `%left PIPE_RIGHT`, `%left COMPOSE_RIGHT`, `INFIXOP0..4` levels keeps the change surface small.

### Anti-Pattern 6: Two-Pass Parsing to Collect Fixity

Do not re-lex/re-parse the source text to collect `InfixDecl` declarations before parsing expressions. Instead, scan the already-built LALR AST for `InfixDecl` nodes in a single O(n) pass. The LALR parse runs once, producing an AST that contains both `InfixDecl` nodes and `InfixChain` nodes. The `collectInfixDecls` function extracts fixity from that AST, and then `prattRewrite` uses it. No second parse needed.

---

## Sources

- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Ast.fs` — complete; DU cases at lines 104-112, Decl type at lines 349-371
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Lexer.fsl` — complete; PIPE_RIGHT/COMPOSE_RIGHT/COMPOSE_LEFT at lines 128-130; classifyOperator at lines 13-22
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Parser.fsy` — lines 89-111 (tokens, precedence), 281-283 (pipe/compose rules), 293-355 (INFIXOP rules)
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/IndentFilter.fs` — complete; isContinuationStart at lines 104-110
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Bidir.fs` — synth cases at lines 737-773
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Eval.fs` — eval cases at lines 1584-1621
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/TypeCheck.fs` — four match arms at lines 420, 556, 647, 748-750
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Infer.fs` — stub at lines 407-408
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Format.fs` — cases at lines 98-100, 209-211
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Prelude.fs` — complete; PreludeResult at lines 13-22, loadPrelude at lines 266-316
- `/Users/ohama/vibe-coding/FunLang/src/FunLang/Program.fs` — complete pipeline entry points
- `tests/flt/` — 29 files use `|>`, 8 use `>>`, 4 use `<<`
