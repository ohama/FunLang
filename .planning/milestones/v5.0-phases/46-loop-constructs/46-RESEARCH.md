# Phase 46: Loop Constructs - Research

**Researched:** 2026-03-28
**Domain:** LALR(1) grammar extension in fsyacc — while/for loops with new keywords
**Confidence:** HIGH

## Summary

This phase adds `while cond do body` and `for i = start to/downto end do body` loops to LangThree. All five keywords (`while`, `for`, `to`, `downto`, `do`) are currently absent from the lexer and parser — none are reserved. They must be added as new tokens.

Both loop constructs return `unit` (`TupleValue []`). The `while` loop evaluates its body in a F# `while` loop until the condition is `BoolValue false`. The `for` loop binds the loop variable as a plain immutable `int` (NOT a `RefValue`) in the environment, iterates over a range, and executes the body for each value.

The immutability of the for-loop variable is enforced by NOT adding the variable name to `mutableVars` in `Bidir.fs`. This means any `i <- 42` inside the for body will trigger the existing `ImmutableVariableAssignment` error (E0320), which is exactly what LOOP-04 requires.

Both constructs are straightforward new `Expr` grammar rules using `SeqExpr` for the body (already available from Phase 45). The `do` keyword is the only potential conflict source: it must not interfere with any existing grammar. Current audit shows `do` is not used anywhere in Parser.fsy or Lexer.fsl.

**Primary recommendation:** Add 5 new tokens (`WHILE`, `FOR`, `TO`, `DOWNTO`, `DO`), 2 new AST nodes (`WhileExpr`, `ForExpr`), 2 grammar rules in `Expr`, 2 eval cases, and 2 type-checker cases. No IndentFilter changes needed.

## Standard Stack

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| fsyacc (`FSharp.Text.ParserGenerator`) | existing | LALR(1) parser generator | Already in use |
| fslex (`FSharp.Text.Lexing`) | existing | Lexer generator | Already in use |
| `SeqExpr` nonterminal | Phase 45 | Loop body allows sequencing | Already implemented |
| `mutableVars` set in `Bidir.fs` | Phase 42 | Track which names are mutable | Already implemented |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `ImmutableVariableAssignment` error kind | Phase 42 | Reuse for LOOP-04 | For-loop variable assignment attempt |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New `WhileExpr`/`ForExpr` AST nodes | Desugar to existing nodes | No good desugaring target exists — while requires actual mutation-safe looping; for requires range iteration. New nodes are the right choice. |
| `RefValue` for loop variable | Plain immutable binding | Plain binding is correct — loop variable must be immutable (LOOP-04) |

**Installation:** No new packages. All existing dependencies.

## Architecture Patterns

### Recommended Project Structure

Changes span exactly these files:
```
src/LangThree/
├── Lexer.fsl     # Add 5 new keyword tokens
├── Parser.fsy    # Add token declarations + 2 grammar rules in Expr
├── Ast.fs        # Add WhileExpr, ForExpr to Expr DU + spanOf cases
├── Eval.fs       # Add eval cases for WhileExpr and ForExpr
├── Bidir.fs      # Add synth cases for WhileExpr and ForExpr
├── Infer.fs      # Add stub cases (pattern: mirrors Bidir stubs)
├── Format.fs     # Add formatAst cases for WhileExpr and ForExpr
└── TypeCheck.fs  # May need mutableVars reset if per-decl (likely no change)
```

### Pattern 1: New AST Nodes (WhileExpr, ForExpr)

**What:** Two new `Expr` discriminated union cases carrying condition/body (while) or variable/bounds/body (for).

**When to use:** Always — no desugar path is appropriate here.

**AST additions to `Ast.fs`:**
```fsharp
// Phase 46 (Loop Constructs): while and for loops
| WhileExpr of cond: Expr * body: Expr * span: Span
// while cond do body — repeats while cond is true, returns unit
| ForExpr of var: string * start: Expr * isTo: bool * stop: Expr * body: Expr * span: Span
// for i = start to/downto end do body — isTo=true for ascending, false for descending
```

The `isTo: bool` flag distinguishes `to` (ascending) from `downto` (descending). Alternative is two separate constructors (`ForTo`, `ForDownto`) — either is fine but a single `ForExpr` with a flag is more compact and follows similar patterns in OCaml's AST.

**spanOf additions:**
```fsharp
| WhileExpr(_, _, s) -> s
| ForExpr(_, _, _, _, _, s) -> s
```

### Pattern 2: Lexer Keywords

**What:** Five new keyword rules in `Lexer.fsl`, ordered before the identifier catch-all.

```fsharp
| "while"       { WHILE }
| "for"         { FOR }
| "to"          { TO }
| "downto"      { DOWNTO }
| "do"          { DO }
```

**Critical ordering:** These MUST appear before `ident_start ident_char*` in `Lexer.fsl` (currently line 103). The existing keyword block (lines 53–90) is the right insertion point.

**`to` conflict risk:** The word `to` appears in common identifiers (e.g., `to_string`). This is safe because `to_string` is an `ident_start ident_char*` match — it contains `_` and is longer. Lexer uses longest match, so `to_string` matches the identifier rule first (longer). However, `to` alone would now be a keyword, not an identifier. This is intentional and correct per requirements. The existing `to_string` builtin uses an underscore, so no issue.

**`do` conflict risk:** `do` is a new keyword. Currently no user code uses `do` as an identifier in the standard library. If any user code uses `do` as a variable name, it will break — this is an expected/intended keyword reservation.

### Pattern 3: Parser Grammar Rules

**What:** Two new productions in the `Expr` nonterminal.

```fsharp
// Phase 46 (Loop Constructs)
// LOOP-01: while cond do body
| WHILE Expr DO SeqExpr
    { WhileExpr($2, $4, ruleSpan parseState 1 4) }
// Indented body variant
| WHILE Expr DO INDENT SeqExpr DEDENT
    { WhileExpr($2, $5, ruleSpan parseState 1 6) }
// LOOP-02/03: for i = start to/downto end do body
| FOR IDENT EQUALS Expr TO Expr DO SeqExpr
    { ForExpr($2, $4, true, $6, $8, ruleSpan parseState 1 8) }
| FOR IDENT EQUALS Expr TO Expr DO INDENT SeqExpr DEDENT
    { ForExpr($2, $4, true, $6, $9, ruleSpan parseState 1 10) }
| FOR IDENT EQUALS Expr DOWNTO Expr DO SeqExpr
    { ForExpr($2, $4, false, $6, $8, ruleSpan parseState 1 8) }
| FOR IDENT EQUALS Expr DOWNTO Expr DO INDENT SeqExpr DEDENT
    { ForExpr($2, $4, false, $6, $9, ruleSpan parseState 1 10) }
```

**Token declarations in Parser.fsy:**
```fsharp
// Phase 46 (Loop Constructs): Loop keywords
%token WHILE FOR TO DOWNTO DO
```

**Placement in Expr:** Near the top of `Expr` (alongside other low-precedence constructs like `IF`, `LET`, `MATCH`). Specifically after the `MATCH` and `LET` rules.

### Pattern 4: Evaluator (Eval.fs)

**What:** Two new match arms in the `eval` function.

```fsharp
// Phase 46: while loop
| WhileExpr (cond, body, _) ->
    let mutable running = true
    while running do
        let condVal = eval recEnv moduleEnv env false cond
        match condVal with
        | BoolValue true  -> eval recEnv moduleEnv env false body |> ignore
        | BoolValue false -> running <- false
        | _ -> failwith "while: condition must be a bool"
    TupleValue []  // returns unit

// Phase 46: for loop
| ForExpr (var, startExpr, isTo, stopExpr, body, _) ->
    let startVal = eval recEnv moduleEnv env false startExpr
    let stopVal  = eval recEnv moduleEnv env false stopExpr
    match startVal, stopVal with
    | IntValue s, IntValue e ->
        let range = if isTo then [s..e] else [s .. -1 .. e]
        for i in range do
            let loopEnv = Map.add var (IntValue i) env  // plain IntValue, NOT RefValue
            eval recEnv moduleEnv loopEnv false body |> ignore
        TupleValue []  // returns unit
    | _ -> failwith "for: start and end must be ints"
```

**Key design note:** The loop variable is bound as `IntValue i` (NOT `RefValue`). This makes it transparent and immutable from the user's perspective. The type checker enforces this at compile time (see Bidir section). At runtime, any attempt to assign `i <- 42` will fail the type check before eval is reached.

**While loop body evaluation:** The body is evaluated in `tailPos = false` because the result is discarded (loop returns unit). Using `ignore` on the body result prevents any tail-call weirdness from leaking.

### Pattern 5: Type Checker (Bidir.fs)

**What:** Two new match arms in `synth`.

```fsharp
// Phase 46: while loop
| WhileExpr (cond, body, span) ->
    let s1, condTy = synth ctorEnv recEnv ctx env cond
    let s2 = unifyWithContext ctx [] span (apply s1 condTy) TBool
    let env' = applyEnv (compose s2 s1) env
    let s3, _bodyTy = synth ctorEnv recEnv ctx env' body
    (compose s3 (compose s2 s1), TTuple [])  // returns unit

// Phase 46: for loop
| ForExpr (var, startExpr, _isTo, stopExpr, body, span) ->
    let s1, startTy = synth ctorEnv recEnv ctx env startExpr
    let s2 = unifyWithContext ctx [] span (apply s1 startTy) TInt
    let s3, stopTy = synth ctorEnv recEnv (compose s2 s1 |> applyEnv |> fun _ -> ctx) (applyEnv (compose s2 s1) env) stopExpr
    let s4 = unifyWithContext ctx [] span (apply s3 stopTy) TInt
    let s = compose s4 (compose s3 (compose s2 s1))
    let env' = applyEnv s env
    // Bind loop variable as int, NOT in mutableVars → immutable
    let loopEnv = Map.add var (Scheme([], TInt)) env'
    // Do NOT add var to mutableVars — it is immutable (LOOP-04)
    let s5, _bodyTy = synth ctorEnv recEnv ctx loopEnv body
    (compose s5 s, TTuple [])  // returns unit
```

**LOOP-04 enforcement mechanism:** The for-loop variable is added to `loopEnv` with type `int`, but is NOT added to `mutableVars`. When the body contains `i <- 42`, the `Assign` case in `synth` checks `Set.contains name mutableVars` and raises `ImmutableVariableAssignment` because `i` is not in `mutableVars`. This reuses the exact existing E0320 machinery from Phase 42 — no new error kind needed.

**Simplified synth composition:** The above uses a simplified version; the actual implementation should chain substitutions correctly. The critical points are:
1. Unify `cond` with `TBool` for while
2. Unify `start` and `stop` with `TInt` for for
3. Bind loop var as `Scheme([], TInt)` — no generalization, no mutableVars entry
4. Return `TTuple []` (unit) for both

### Pattern 6: Infer.fs Stubs

`Infer.fs` contains legacy/stub inference code. Following the existing pattern for Phase 42:

```fsharp
// Phase 46: Loop constructs (stub — primary implementation in Bidir)
| WhileExpr (_, body, _) ->
    let s1, _ = inferWithContext ctx env body
    (s1, TTuple [])

| ForExpr (_, start, _, stop, body, _) ->
    let s1, _ = inferWithContext ctx env start
    let s2, _ = inferWithContext ctx env stop
    let s3, _ = inferWithContext ctx env body
    (compose s3 (compose s2 s1), TTuple [])
```

### Pattern 7: Format.fs

```fsharp
| Ast.WhileExpr (cond, body, _) ->
    sprintf "WhileExpr (%s, %s)" (formatAst cond) (formatAst body)
| Ast.ForExpr (var, start, isTo, stop, body, _) ->
    let dir = if isTo then "to" else "downto"
    sprintf "ForExpr (\"%s\", %s, %s, %s, %s)" var (formatAst start) dir (formatAst stop) (formatAst body)
```

### Pattern 8: IndentFilter — No Changes Needed

The IndentFilter already handles the `WHILE`/`FOR`/`DO` body correctly because:

- The grammar uses `INDENT SeqExpr DEDENT` for indented bodies (same as `IF`, `LET` etc.)
- The grammar uses `DO SeqExpr` (inline) for same-line bodies
- `DO` is not a context-sensitive token that IndentFilter needs to track
- No new `SyntaxContext` case is needed

The existing `InExprBlock` context (pushed when INDENT follows EQUALS or ARROW) does NOT need to change because the loop body INDENT is handled by the `WHILE Expr DO INDENT SeqExpr DEDENT` grammar rule directly.

**However:** There is one consideration for `DO` in IndentFilter. Currently IndentFilter checks `PrevToken` for `EQUALS | ARROW | IN` to push `InExprBlock`. `DO` is the token before the loop body INDENT. If we want consistent behavior, we should add `DO` to this list. Looking at the logic in IndentFilter line 316–320:

```fsharp
match state.PrevToken with
| Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN ->
    let baseCol = match state.IndentStack with _ :: parent :: _ -> parent | _ -> 0
    state <- { state with Context = InExprBlock(baseCol) :: state.Context }
| _ -> ()
```

This means an INDENT after `DO` would not push `InExprBlock`. But since the loop body is bound by the grammar rule (`DO INDENT SeqExpr DEDENT`), the parser already delimits it. The `InExprBlock` context is used for the offside rule (implicit `IN` insertion for nested `let`s), not for the INDENT/DEDENT structure itself. So the lack of `InExprBlock` after `DO` only matters if someone writes:

```fsharp
while cond do
    let x = 1
    body_using_x
```

In this case, the `let x = 1` inside the while body needs the offside rule to insert `IN` before `body_using_x`. For this to work, `DO` must be added to the `PrevToken` check in IndentFilter.

**Conclusion:** Add `DO` to the IndentFilter `PrevToken` check that pushes `InExprBlock`. This is a one-line change.

### Anti-Patterns to Avoid

- **Binding loop variable as RefValue:** Makes `i` mutable at the value level. The type checker would then not fire E0320 for `i <- 42`, and the immutability guarantee is lost.
- **New `LoopVarImmutable` error kind:** Unnecessary — the existing `ImmutableVariableAssignment` (E0320) from Phase 42 is exactly right for LOOP-04.
- **Emitting `do` bodies as implicit IN via offside:** While `do` could theoretically work like `=` for offside purposes, this adds complexity without benefit since the body is already bounded by `INDENT...DEDENT`.
- **Single production without inline variant:** Both `DO SeqExpr` (inline body) and `DO INDENT SeqExpr DEDENT` (indented body) variants are needed, following the same pattern as all other body-containing rules.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Immutable loop var error | New error kind `ForVarImmutable` | `ImmutableVariableAssignment` (E0320) | Already formatted, already tested, same semantics |
| Empty range semantics | Custom range check | Standard F# `[s..e]` semantics | For `for i = 5 to 3`, the range `[5..3]` is empty — loop body never executes. Correct behavior. |
| Loop body unit enforcement | Explicit check | Natural from type system | Body type is synthesized but result is discarded; type checker doesn't need to enforce it is unit (OCaml/F# don't enforce this either — any body type is fine, result ignored) |
| Keyword conflict resolution | Lexer state machine | First-match ordering in fslex | Already how all other keywords work |

**Key insight:** The `mutableVars` set in `Bidir.fs` is a mutable global that tracks which names are currently in scope as mutable variables. For the for-loop, simply not adding `i` to that set — while adding `i` to the type environment with type `int` — gives the immutability guarantee for free.

## Common Pitfalls

### Pitfall 1: Forgetting the Inline Body Variant
**What goes wrong:** `for i = 0 to 9 do println (to_string i)` on a single line fails — only the indented body variant exists.
**Why it happens:** Indented body (`DO INDENT SeqExpr DEDENT`) parses multi-line bodies, but single-line `DO SeqExpr` is also needed.
**How to avoid:** Add BOTH variants for each loop form (6 grammar rules total: 2 for while, 4 for for).
**Warning signs:** Single-line loops fail, multi-line succeed.

### Pitfall 2: Loop Variable Bound in Wrong Scope
**What goes wrong:** `i` is visible after the loop ends; or `i` is not visible inside the loop body.
**Why it happens:** `Map.add var (IntValue i) env` adds to the local `loopEnv` used only for the body eval. The outer `env` is unchanged — `i` is not visible after the loop.
**How to avoid:** Use a fresh `loopEnv = Map.add var ... env` for each iteration, then discard it. Do NOT update the outer `env`.
**Warning signs:** `i` accessible after loop (scope leak) or body errors about unbound `i`.

### Pitfall 3: While Condition Not Dereferenced
**What goes wrong:** `while !running do ...` (where `running` is a mutable bool) — `Var` lookup returns `RefValue r`, and comparing it to `BoolValue` fails.
**Why it happens:** In Phase 42, `Var` lookup in `eval` already transparently dereferences `RefValue` (Eval.fs line 739: `| Some (RefValue r) -> r.Value`). So this is actually fine — no special handling needed.
**Warning signs:** If this pitfall is encountered, check that the `Var` eval case dereferences. It already does.

### Pitfall 4: `to` Breaks Existing User Code Using `to` as Variable
**What goes wrong:** Existing code that uses `to` as a variable name (e.g., `let to = 5`) stops parsing.
**Why it happens:** `to` becomes a reserved keyword.
**How to avoid:** Search existing tests and Prelude for `to` as a variable name. Check that `to_string` still works (it should — `to_string` is an identifier, not affected by the `to` keyword since lexer uses longest match).
**Warning signs:** Prelude tests break; `to_string` lexes as `TO UNDERSCORE IDENT "string"` — **this would be wrong**. Must verify `to_string` lexes as a single `IDENT` token.

**Critical verification for `to_string`:** The lexer rule `ident_start ident_char*` matches `t o _ s t r i n g` (9 chars). The rule `"to"` matches only 2 chars. Since fslex uses longest match, `to_string` (9 chars) beats `to` (2 chars). So `to_string` continues to lex as `IDENT "to_string"`. **Confirmed safe.**

### Pitfall 5: Missing `DO` in IndentFilter PrevToken Check
**What goes wrong:** `while cond do\n    let x = 1\n    x + 1` fails — the `let x` inside the loop body gets no implicit `IN` token before `x + 1`.
**Why it happens:** IndentFilter's offside rule fires for `let` inside `InExprBlock`, but `DO` doesn't push `InExprBlock`.
**How to avoid:** Add `| Some Parser.DO ->` (alongside `EQUALS | ARROW | IN`) in the `PrevToken` check that pushes `InExprBlock` context (IndentFilter.fs line ~317).
**Warning signs:** `let` inside loop bodies with continuation doesn't work; `let x = 1\n    x + 1` fails inside loop.

### Pitfall 6: Empty For-Loop Range Behavior
**What goes wrong:** `for i = 5 to 3 do body` executes the body once with `i=5` (wrong) or errors.
**Why it happens:** If eval uses `for i in [s..e] do` and `[5..3]` is empty, the body correctly never executes. This is the expected F# behavior.
**How to avoid:** Use `[s..e]` for `to` and `[s .. -1 .. e]` for `downto`. In F#, `[5..3]` is `[]` so body never runs. This is correct for empty ranges.
**Warning signs:** Test `for i = 5 to 3 do println "x"` should produce no output.

### Pitfall 7: Shadowing in Nested Loops
**What goes wrong:** `for i = 0 to 9 do for i = 0 to 9 do body` — inner `i` shadows outer `i`. Both loops use the same variable name.
**Why it happens:** Each iteration rebuilds `loopEnv = Map.add var value env`. The inner for-loop creates its own `loopEnv` with the same key `i`, shadowing the outer one.
**How to avoid:** This is correct behavior — lexical scoping. No action needed. Document as expected.

## Code Examples

Verified patterns from existing codebase inspection:

### New Tokens in Lexer.fsl
```fsharp
// Phase 46 (Loop Constructs): Loop keywords — add alongside existing keywords (after "when", before '_')
| "while"       { WHILE }
| "for"         { FOR }
| "to"          { TO }
| "downto"      { DOWNTO }
| "do"          { DO }
```

### Token Declarations in Parser.fsy
```fsharp
// Phase 46 (Loop Constructs): Loop keywords
%token WHILE FOR TO DOWNTO DO
```

### AST Additions in Ast.fs
```fsharp
// Phase 46 (Loop Constructs): while and for loops
| WhileExpr of cond: Expr * body: Expr * span: Span
| ForExpr of var: string * start: Expr * isTo: bool * stop: Expr * body: Expr * span: Span
```

### spanOf additions in Ast.fs
```fsharp
| WhileExpr(_, _, s) -> s
| ForExpr(_, _, _, _, _, s) -> s
```

### Grammar Rules in Parser.fsy (add inside Expr nonterminal)
```fsharp
// Phase 46 (Loop Constructs)
// LOOP-01: while cond do body
| WHILE Expr DO SeqExpr
    { WhileExpr($2, $4, ruleSpan parseState 1 4) }
| WHILE Expr DO INDENT SeqExpr DEDENT
    { WhileExpr($2, $5, ruleSpan parseState 1 6) }
// LOOP-02: for i = start to end do body (ascending)
| FOR IDENT EQUALS Expr TO Expr DO SeqExpr
    { ForExpr($2, $4, true, $6, $8, ruleSpan parseState 1 8) }
| FOR IDENT EQUALS Expr TO Expr DO INDENT SeqExpr DEDENT
    { ForExpr($2, $4, true, $6, $9, ruleSpan parseState 1 10) }
// LOOP-03: for i = start downto end do body (descending)
| FOR IDENT EQUALS Expr DOWNTO Expr DO SeqExpr
    { ForExpr($2, $4, false, $6, $8, ruleSpan parseState 1 8) }
| FOR IDENT EQUALS Expr DOWNTO Expr DO INDENT SeqExpr DEDENT
    { ForExpr($2, $4, false, $6, $9, ruleSpan parseState 1 10) }
```

### Eval.fs Cases
```fsharp
// Phase 46: while loop
| WhileExpr (cond, body, _) ->
    let mutable keepGoing = true
    while keepGoing do
        match eval recEnv moduleEnv env false cond with
        | BoolValue true  -> eval recEnv moduleEnv env false body |> ignore
        | BoolValue false -> keepGoing <- false
        | _ -> failwith "while: condition must be of type bool"
    TupleValue []

// Phase 46: for loop
| ForExpr (var, startExpr, isTo, stopExpr, body, _) ->
    let startVal = eval recEnv moduleEnv env false startExpr
    let stopVal  = eval recEnv moduleEnv env false stopExpr
    match startVal, stopVal with
    | IntValue s, IntValue e ->
        let range = if isTo then [s..e] else [s .. -1 .. e]
        for iVal in range do
            let loopEnv = Map.add var (IntValue iVal) env
            eval recEnv moduleEnv loopEnv false body |> ignore
        TupleValue []
    | _ -> failwith "for: start and end must be integers"
```

### Bidir.fs Cases
```fsharp
// Phase 46: while loop
| WhileExpr (cond, body, span) ->
    let s1, condTy = synth ctorEnv recEnv ctx env cond
    let s2 = unifyWithContext ctx [] span (apply s1 condTy) TBool
    let s12 = compose s2 s1
    let env' = applyEnv s12 env
    let s3, _bodyTy = synth ctorEnv recEnv ctx env' body
    (compose s3 s12, TTuple [])  // while always returns unit

// Phase 46: for loop
| ForExpr (var, startExpr, _isTo, stopExpr, body, span) ->
    let s1, startTy = synth ctorEnv recEnv ctx env startExpr
    let s2 = unifyWithContext ctx [] span (apply s1 startTy) TInt
    let s12 = compose s2 s1
    let env1 = applyEnv s12 env
    let s3, stopTy = synth ctorEnv recEnv ctx env1 stopExpr
    let s4 = unifyWithContext ctx [] span (apply s3 stopTy) TInt
    let s1234 = compose s4 (compose s3 s12)
    let env2 = applyEnv s1234 env
    // Bind loop variable as immutable int — NOT in mutableVars (LOOP-04)
    let loopEnv = Map.add var (Scheme([], TInt)) env2
    // savedMutableVars not needed — var is never added to mutableVars
    let s5, _bodyTy = synth ctorEnv recEnv ctx loopEnv body
    (compose s5 s1234, TTuple [])  // for always returns unit
```

### IndentFilter.fs Change
```fsharp
// Current (line ~316-320):
match state.PrevToken with
| Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN ->
    let baseCol = match state.IndentStack with _ :: parent :: _ -> parent | _ -> 0
    state <- { state with Context = InExprBlock(baseCol) :: state.Context }
| _ -> ()

// Changed to (add DO):
match state.PrevToken with
| Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN | Some Parser.DO ->
    let baseCol = match state.IndentStack with _ :: parent :: _ -> parent | _ -> 0
    state <- { state with Context = InExprBlock(baseCol) :: state.Context }
| _ -> ()
```

### FLT Test Structure

Tests should go in `tests/flt/expr/loop/` (new directory):

```
loop-while-basic.flt         — LOOP-01: basic while loop
loop-while-mutable.flt       — LOOP-01: while with mutable condition (!running)
loop-for-ascending.flt       — LOOP-02: for i = 0 to 9
loop-for-descending.flt      — LOOP-03: for i = 9 downto 0
loop-for-immutable-error.flt — LOOP-04: i <- 42 inside for = E0320 error
loop-for-empty-range.flt     — for i = 5 to 3 (no body execution)
loop-while-nested.flt        — while inside while
loop-for-nested.flt          — for inside for, same/different variable names
loop-body-sequencing.flt     — loop body with e1; e2 (SeqExpr)
loop-for-with-array.flt      — for loop populating an array
```

**Example FLT for LOOP-01:**
```
// Test LOOP-01: while loop repeats body until condition false
// --- Command: .../LangThree %input
// --- Input:
let mut i = 0
let _ =
    while i < 5 do
        i <- i + 1
let _ = println (to_string i)
// --- Output:
5
()
```

**Example FLT for LOOP-04:**
```
// Test LOOP-04: for loop variable is immutable (cannot assign)
// --- Command: .../LangThree %input
// --- Input:
let _ =
    for i = 0 to 9 do
        i <- 42
// --- Error:
E0320
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Explicit recursion for loops | `while`/`for` syntax | Phase 46 | Ergonomic imperative iteration |
| `let rec loop i = if i < n then ... else ()` | `for i = 0 to n-1 do ...` | Phase 46 | Much simpler imperative code |
| No loop construct | F#-style `while`/`for` | Phase 46 | Matches F# semantics |

**F# for-loop semantics confirmed:** In F#, `for i = 0 to 9 do body` binds `i` as immutable `int`. Assigning `i <- v` is a compile error. The range is inclusive on both ends. Empty ranges (start > stop for `to`, start < stop for `downto`) execute the body zero times.

## Open Questions

1. **`to` as keyword vs. identifier in existing user code**
   - What we know: `to_string` uses longest-match, safe. No other `to`-prefixed identifiers found in Prelude.
   - What's unclear: Whether any existing `.flt` test files use `to` as a standalone variable name.
   - Recommendation: Run test suite after adding `to` keyword; if tests break, investigate. Unlikely issue.

2. **For-loop variable type annotation**
   - What we know: F# requires `for i = 0 to 9` where `i` is always `int`. LangThree follows suit.
   - What's unclear: Should we support `for (i: int) = 0 to 9`? Probably not needed for Phase 46.
   - Recommendation: Do not add annotation syntax for loop variable in Phase 46. Type is always inferred as `int`.

3. **While condition: require `bool` or allow any truthy value?**
   - What we know: F# requires the condition to be `bool`. The type checker unifies with `TBool`.
   - What's unclear: Whether users will find it annoying that e.g. `while count do ...` doesn't work.
   - Recommendation: Require `bool` (matches F# semantics, consistent with `if` which also requires `bool`).

4. **Handling `break`/`continue` inside loops**
   - What we know: Not in Phase 46 requirements.
   - What's unclear: Future phases may want these.
   - Recommendation: Out of scope for Phase 46. Document that loops run to completion.

## Sources

### Primary (HIGH confidence)
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Lexer.fsl` — all current keywords, no `while`/`for`/`do`/`to`/`downto`
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Parser.fsy` — all grammar rules, `SeqExpr` available, no loop rules
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Ast.fs` — current `Expr` DU, `LetMut`/`Assign` pattern from Phase 42
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Eval.fs` — `LetMut`/`Assign` eval pattern, `Var` dereferences `RefValue` transparently
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Bidir.fs` — `mutableVars` set, `LetMut`/`Assign` type-check pattern, `ImmutableVariableAssignment` check
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/IndentFilter.fs` — `PrevToken` handling for `InExprBlock`, `DO` not currently tracked
- Direct inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Diagnostic.fs` — `ImmutableVariableAssignment` error kind, E0320
- Direct inspection of `.planning/phases/45-expression-sequencing/45-RESEARCH.md` — `SeqExpr` nonterminal fully implemented in Phase 45

### Secondary (MEDIUM confidence)
- F# language specification: `for` loop variable is immutable int; `while` condition must be `bool`; empty ranges produce no iterations

## Metadata

**Confidence breakdown:**
- New keywords (5 tokens): HIGH — direct inspection confirms none currently exist
- AST nodes (`WhileExpr`, `ForExpr`): HIGH — straightforward extension of existing pattern
- Grammar rules (6 productions): HIGH — follows exact pattern of existing Expr rules
- Eval implementation: HIGH — standard F# while/for semantics, mirrors existing mutable patterns
- Type checker (Bidir): HIGH — LOOP-04 via mutableVars exclusion is proven pattern from Phase 42
- IndentFilter change (add DO): MEDIUM — logic analysis correct but not runtime-tested; `let` inside loop body with offside rule needs verification
- `to` keyword safety with `to_string`: HIGH — fslex longest-match rule guarantees no conflict

**Research date:** 2026-03-28
**Valid until:** Stable for this codebase (LangThree grammar stable, Phase 45 SeqExpr complete)
