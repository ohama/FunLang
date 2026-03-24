# Phase 30: Parser Improvements - Research

**Researched:** 2026-03-25
**Domain:** F# parser (fsyacc grammar), IndentFilter token transform, F# evaluator
**Confidence:** HIGH

## Summary

Phase 30 addresses 5 syntax requirements (SYN-01, SYN-05, SYN-06, SYN-07, SYN-08) that were identified from real FunLexYacc usage. Each requirement involves a specific parser or evaluator deficiency. Through direct testing against the current interpreter, the exact failure modes and fixes have been identified.

The 5 requirements break down into 3 distinct layers of change: (1) IndentFilter token suppression for SYN-05, (2) Parser.fsy grammar additions for SYN-01/SYN-06/SYN-07/SYN-08, and (3) Eval.fs LetRec closure fix for SYN-01. Most cases already work — the remaining failures are precise and well-understood. This is primarily a correctness and ergonomics phase, not an architectural change.

**Primary recommendation:** Fix each requirement independently with targeted changes. SYN-01 requires both a grammar change AND an evaluator fix. All other requirements are grammar-only or IndentFilter-only.

## Standard Stack

This is an internal-to-codebase phase. No external packages required.

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| F# / .NET | 10.0 | Host language | Project standard |
| fslex/fsyacc (FSharp.Text.Lexing) | bundled with .NET SDK | Lexer/parser generation | Already in use |
| Expecto | latest | Test framework | Already in use |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| IndentFilter.fs | internal | Token stream transform | When modifying whitespace-sensitivity |
| Parser.fsy | internal | Grammar rules | When adding/changing syntax |
| Eval.fs | internal | Expression evaluator | When adding new AST nodes or fixing evaluation |

## Architecture Patterns

### Project Structure (Relevant Files)
```
src/LangThree/
├── IndentFilter.fs    # SYN-05: token stream transforms (JustSawElse fix)
├── Parser.fsy         # SYN-01/06/07/08: grammar additions
├── Eval.fs            # SYN-01: LetRec evaluator fix
└── (Ast.fs)           # No changes expected

tests/LangThree.Tests/
├── IndentFilterTests.fs  # New tests for SYN-05
├── IntegrationTests.fs   # New tests for SYN-01/06/07/08 parse behavior
└── ModuleTests.fs        # New tests for eval correctness
```

### Pattern 1: IndentFilter State Flags (JustSawX)
**What:** FilterState uses boolean flags (JustSawMatch, JustSawTry, JustSawModule) to track that the previous token was a specific keyword, then special-cases the next NEWLINE processing.
**When to use:** When a token keyword changes how the following NEWLINE should be interpreted.
**Example:**
```fsharp
// Source: src/LangThree/IndentFilter.fs:333-345
| Parser.MATCH ->
    state <- { state with JustSawMatch = true; PrevToken = Some token }
    yield token
// ...
// processNewlineWithContext uses JustSawMatch to enter InMatch context
```

### Pattern 2: Next-Token Lookahead in processNewlineWithContext
**What:** `processNewlineWithContext` already receives `nextToken: Parser.token option` (one-token lookahead). Use this to conditionally suppress INDENT/DEDENT.
**When to use:** When the next token after a NEWLINE changes what indentation tokens should be emitted.
**Example:**
```fsharp
// Source: src/LangThree/IndentFilter.fs:179-193
| InMatch baseCol :: _ when nextToken = Some Parser.PIPE ->
    // Suppress indent tokens — pipe aligns with match
    (stateAfterIndent, [])
```
The SYN-05 fix follows the same pattern: when `nextToken = Some Parser.ELSE`, suppress INDENT (but allow DEDENT).

### Pattern 3: Grammar Desugaring in Parser Actions
**What:** Complex syntax is desugared to simpler AST nodes in parser action code. Multi-param lambdas desugar to nested `Lambda`. Multi-param let rec should desugar to nested `LetRec`.
**When to use:** When adding syntactic sugar that maps to existing AST nodes.
**Example:**
```fsharp
// Source: src/LangThree/Parser.fsy:116-118
| LET IDENT ParamList EQUALS Expr IN Expr
    { let lambda = List.foldBack (fun param body -> Lambda(param, body, ...)) $3 $5
      Let($2, lambda, $7, ...) }
// The same pattern applies to LET REC IDENT ParamList EQUALS Expr IN Expr
```

### Pattern 4: LetRec Self-Referential Closure (BuiltinValue + mutable ref)
**What:** Module-level `LetRecDecl` uses `BuiltinValue` wrappers with a shared mutable `env ref` to create true self-referential closures. Expression-level `LetRec` should use the same approach.
**When to use:** Whenever a recursive binding needs to call itself from inside a lambda that captures the environment at creation time.
**Example:**
```fsharp
// Source: src/LangThree/Eval.fs:860-880 (LetRecDecl)
let sharedEnvRef = ref env
let wrapper = BuiltinValue (fun argVal ->
    let currentEnv = !sharedEnvRef
    let callEnv = Map.add param argVal currentEnv
    eval recEnv modEnv callEnv false body)
let mutualEnv = Map.add name wrapper env
sharedEnvRef := mutualEnv
```

### Anti-Patterns to Avoid
- **Adding JustSawElse flag instead of nextToken lookahead**: The `processNewlineWithContext` function already receives `nextToken`. Use that instead of adding another state flag — it's cleaner and avoids state management complexity.
- **Adding separate Decl variant for SYN-08**: Don't add a new `Decl` DU case (e.g., `ExprDecl`). Instead, wrap the let...in expression in a synthetic `LetDecl("_last", letInExpr)`. This requires minimal AST changes.
- **Modifying applyFunc for LetRec fix**: The trampoline bug in `applyFunc` is hard to fix without breaking other cases. The clean fix is in `LetRec` evaluation itself — use a mutable ref closure, matching how `LetRecDecl` works.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Multi-param let rec desugaring | Custom AST node | Desugar to nested single-param LetRec (curried lambdas) | Consistent with how regular let f x y = body works |
| Self-referential closure | Mutable env mutation | BuiltinValue + mutable ref (same pattern as LetRecDecl) | Already proven to work; avoids trampoline issue |
| ELSE context tracking | New SyntaxContext variant | nextToken lookahead in processNewlineWithContext | Simpler; nextToken already available |

**Key insight:** Every fix can reuse existing patterns from the codebase. No new infrastructure needed.

## Common Pitfalls

### Pitfall 1: SYN-01 - Trampoline Bug Misdiagnosis
**What goes wrong:** The "Undefined variable: helper" error misleads you into thinking it's a parser issue, not an evaluator issue.
**Why it happens:** The parser produces correct AST for `let rec h x = ... in h n` inside a lambda body. But when the outer `App` evaluates the lambda, the trampoline loop calls `applyFunc` with the **original** outer `funcExpr` (e.g., `Lambda("n",...)`) for all recursive tail calls. Since `funcExpr` is not `Var "h"`, the augmentation step in `applyFunc` doesn't add `h` to the closure.
**How to avoid:** Fix `LetRec` in Eval.fs to create a self-referential `BuiltinValue` closure (like `LetRecDecl` does). Do NOT try to fix `applyFunc`.
**Warning signs:** If `let rec h x = ... in h n` works at module level but fails inside a lambda body, it's the trampoline bug.

### Pitfall 2: SYN-05 - Suppressing INDENT but Not DEDENT
**What goes wrong:** When next token is ELSE, naively suppressing ALL indentation tokens (including DEDENT) breaks the else-branch body parsing.
**Why it happens:** DEDENT is needed when the THEN branch had a deeper indentation block. INDENT is the problem (emitted before ELSE when ELSE is at a deeper column than IF).
**How to avoid:** In `processNewlineWithContext`, when `nextToken = Some Parser.ELSE`, run normal DEDENT processing but suppress any INDENT. Specifically: call `processNewline` normally, but if the result contains INDENT, return `[]` instead; DEDENTs are kept.
**Warning signs:** Tests with deeply indented ELSE blocks (e.g., `then\n    x\n    else\n    y`) start failing with indent mismatch.

### Pitfall 3: SYN-05 - Not Updating the Indent Stack
**What goes wrong:** If you suppress INDENT without updating the indent stack, a later DEDENT will pop to the wrong level.
**Why it happens:** The indent stack tracks open blocks. If we skip pushing a level, we lose synchronization.
**How to avoid:** When suppressing INDENT before ELSE (because ELSE is deeper), you CAN skip the stack update because ELSE doesn't open a new block — it's just part of the if-then-else. The else-branch body will handle its own indentation when parsed.
**Warning signs:** IndentStack becomes misaligned after ELSE blocks, causing spurious DEDENT errors.

### Pitfall 4: SYN-01 Grammar - Multi-param let rec conflict with existing rules
**What goes wrong:** Adding `| LET REC IDENT ParamList EQUALS Expr IN Expr` to Expr conflicts with `| LET REC IDENT IDENT EQUALS Expr IN Expr` since `ParamList = IDENT ParamList | IDENT`.
**Why it happens:** fsyacc LALR(1) conflict: when parser sees `LET REC IDENT IDENT`, it can't tell if IDENT is the function name and the second IDENT is the param (existing rule) or if IDENT IDENT starts ParamList (new rule).
**How to avoid:** Replace the existing single-param rule with the multi-param version. `ParamList` already handles 1+ params (including the single-param case). Remove the old rule entirely.
**Warning signs:** fsyacc reports shift/reduce or reduce/reduce conflicts on `LET REC`.

### Pitfall 5: SYN-07 - Unit param shorthand needs both Decl and Expr levels
**What goes wrong:** Adding `let f () = body` only to `Decl` but not testing that it also works in expression context (`let f () = body in f ()`).
**Why it happens:** Expr rules and Decl rules are separate in this grammar.
**How to avoid:** Add to BOTH `Decl` (module-level `let f () = body`) and `Expr` (expression-level `let f () = body in expr`). The Expr version already has `FUN LPAREN RPAREN ARROW Expr` but not `LET IDENT LPAREN RPAREN EQUALS Expr`.
**Warning signs:** Module-level `let f () = 42` works but `let main = let f () = 42 in f ()` fails.

### Pitfall 6: SYN-08 - Synthetic name collision
**What goes wrong:** Using a fixed synthetic name like `"__last"` for the LetDecl wrapping `let x = e1 in e2` causes shadowing if user has multiple such expressions.
**Why it happens:** Each `let x = e1 in e2` at module level becomes `LetDecl("__last", Let("x", e1, e2))`. If there are two such expressions, the second shadows the first in the module env.
**How to avoid:** Either use a unique synthetic name per occurrence (e.g., counter-based), or use a consistent convention (`"_"` or `"__module_expr_N"`). Since the module's "last value" heuristic already uses the last `LetDecl` name, this might work fine with `"_"`.
**Warning signs:** Multiple top-level let...in expressions cause the wrong one's value to be printed.

### Pitfall 7: SYN-06 - Overlap with SYN-01
**What goes wrong:** Treating SYN-06 as a completely separate requirement when it's largely the same fix as SYN-01's grammar change.
**Why it happens:** SYN-06 is "deeply nested function bodies" but the actual failure found is `let rec f a b = ...` (multi-param) inside nested expressions — the same as SYN-01's multi-param grammar fix.
**How to avoid:** Plan SYN-06 as: (a) verify that 4-level nesting works after SYN-01 grammar fix, (b) add tests for 4+ levels of let/match/if. The deeper nesting itself (pure let chains, pure match nesting) already works without any changes.
**Warning signs:** If you're writing completely new code for SYN-06 that doesn't overlap with SYN-01, you're probably over-engineering it.

## Code Examples

### SYN-01: LetRec Evaluator Fix (Mutable Ref Pattern)
```fsharp
// Source: Pattern from src/LangThree/Eval.fs:860-880 (LetRecDecl)
// Fix for expression-level LetRec:
| LetRec (name, param, funcBody, inExpr, _) ->
    // Create self-referential closure using mutable ref (same as LetRecDecl)
    let envRef = ref env
    let wrapper = BuiltinValue (fun argVal ->
        let callEnv = Map.add param argVal !envRef
        eval recEnv moduleEnv callEnv false funcBody)
    let recEnv' = Map.add name wrapper env
    envRef := recEnv'  // Now closure sees itself
    eval recEnv moduleEnv recEnv' tailPos inExpr
```

### SYN-01/SYN-06: Multi-param let rec in Expr (Grammar)
```fsharp
// Replace in Parser.fsy Expr rules:
// BEFORE (single-param only):
// | LET REC IDENT IDENT EQUALS Expr IN Expr
//     { LetRec($3, $4, $6, $8, ...) }
// AFTER (multi-param via ParamList):
| LET REC IDENT ParamList EQUALS Expr IN Expr
    { let lambda = List.foldBack (fun param body -> Lambda(param, body, span)) $4 $6
      match lambda with
      | Lambda(p, b, _) -> LetRec($3, p, b, $8, span)
      | _ -> failwith "impossible" }
// Same for the INDENT Expr DEDENT IN Expr variant
```

### SYN-05: ELSE-before-INDENT suppression in IndentFilter
```fsharp
// In src/LangThree/IndentFilter.fs processNewlineWithContext,
// in the "| _ ->" branch of the final match on context:
| _ ->
    let (newState, tokens) = processNewline config stateWithTryContext col
    // Suppress INDENT when next token is ELSE
    // (ELSE must follow THEN Expr directly — no INDENT between)
    let filteredTokens =
        match nextToken with
        | Some Parser.ELSE when List.contains Parser.INDENT tokens ->
            tokens |> List.filter (fun t -> t <> Parser.INDENT)
        | _ -> tokens
    (newState, filteredTokens)
// NOTE: DEDENTs are NOT suppressed — they're needed to close open blocks
// NOTE: The indent stack IS updated normally — only the emitted token is suppressed
```

### SYN-07: Unit param shorthand in Decl (Grammar)
```fsharp
// Add to Parser.fsy Decl rules:
| LET IDENT LPAREN RPAREN EQUALS Expr
    { LetDecl($2, LambdaAnnot("__unit", TETuple [], $6, ruleSpan parseState 1 6)) }
| LET IDENT LPAREN RPAREN EQUALS INDENT Expr DEDENT
    { LetDecl($2, LambdaAnnot("__unit", TETuple [], $7, ruleSpan parseState 1 8)) }
// Also add to Expr rules:
| LET IDENT LPAREN RPAREN EQUALS Expr IN Expr
    { Let($2, LambdaAnnot("__unit", TETuple [], $6, ruleSpan parseState 1 8), $8, ...) }
```

### SYN-08: top-level let...in as Decl (Grammar)
```fsharp
// Add to Parser.fsy Decl rules (or Decls directly):
// Strategy: wrap the entire let...in expression as LetDecl("_", Let(...))
// This allows the expression to be evaluated at module level.
| LET IDENT EQUALS Expr IN Expr
    { LetDecl("_", Let($2, $4, $6, ruleSpan parseState 1 6)) }
// Note: use a consistent synthetic name "_" which evalModuleDecls's last-binding
// heuristic can handle (or exclude from printing)
```

### Current Eval.fs LetRec (Broken for closures)
```fsharp
// Source: src/LangThree/Eval.fs:611-614
// CURRENT (broken when LetRec is inside a lambda body):
| LetRec (name, param, funcBody, inExpr, _) ->
    let funcVal = FunctionValue (param, funcBody, env)  // env has NO self-ref!
    let recFuncEnv = Map.add name funcVal env
    eval recEnv moduleEnv recFuncEnv tailPos inExpr
// Problem: funcBody is captured with `env` which doesn't include `name`.
// applyFunc adds name to closure only when funcExpr=Var(name),
// but the trampoline loses this context for tail calls inside lambda bodies.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Expression LetRec with augmented closure via applyFunc | BuiltinValue + mutable ref (same as LetRecDecl) | Phase 30 (this phase) | Fixes recursion inside lambda bodies |
| No ELSE handling in IndentFilter | JustSawElse flag OR nextToken=ELSE check to suppress INDENT | Phase 30 | Fixes deeply indented else branches |
| Single-param expression-level let rec | Multi-param via ParamList (curried desugaring) | Phase 30 | Fixes `let rec f a b = ...` inside function bodies |

**Deprecated/outdated:**
- Current `LetRec` evaluator pattern (augmented closure via applyFunc trick): will be replaced by mutable-ref pattern matching LetRecDecl.

## Open Questions

1. **SYN-05: Should INDENT be suppressed or should the indent stack still be updated?**
   - What we know: Suppressing the emitted INDENT token without updating the stack means the stack stays at the current level. Subsequent indented lines after ELSE can still produce their own INDENT.
   - What's unclear: Does leaving the stack at the pre-ELSE level cause issues when processing the ELSE branch's body indentation?
   - Recommendation: Test with both approaches. The `processNewline` function can update the stack without emitting INDENT if needed (by using the returned tokens but discarding INDENT).

2. **SYN-08: What name to use for the synthetic LetDecl?**
   - What we know: The program uses the last `LetDecl` name to determine what to print. Using `"_"` means `let x = 1 in x + 2` at module level prints `x+2` result as the last `LetDecl("_", ...)`.
   - What's unclear: If multiple `let...in` expressions appear at module level, only the last one's result is printed.
   - Recommendation: Use `"_"` as the synthetic name. The module pipeline already filters `_` from display in several contexts. Test with multiple top-level let...in to verify expected behavior.

3. **SYN-06: Are there additional deep-nesting failures beyond multi-param let rec?**
   - What we know: Pure let-chains (4-5 levels), pure match-chains (5 levels), and let+match combos all work without changes. The only confirmed failure is `let rec f a b = ...` inside nested expressions.
   - What's unclear: Whether there are other pathological patterns the FunLexYacc project hit.
   - Recommendation: Treat SYN-06 as: fix multi-param let rec (same as SYN-01 grammar), then add tests for 4-level nesting patterns.

## Requirement-to-Fix Mapping

| Requirement | Root Cause | Fix Location | Complexity |
|-------------|------------|--------------|------------|
| SYN-01: local let rec | (1) Grammar: only 1-param expr LetRec; (2) Eval: trampoline loses rec closure | Parser.fsy + Eval.fs | Medium |
| SYN-05: ELSE+keyword | IndentFilter emits INDENT before ELSE when ELSE is indented | IndentFilter.fs | Low |
| SYN-06: deep nesting | Grammar: multi-param let rec in expr position | Parser.fsy | Low (same as SYN-01 grammar) |
| SYN-07: `f ()` shorthand | Grammar: `let f () = body` missing in Decl and Expr | Parser.fsy | Low |
| SYN-08: top-level let...in | Grammar: Decl has no `let x = e1 in e2` rule | Parser.fsy | Low |

## Confirmed Working (No Changes Needed)

The following were tested and work correctly already:
- `fun () -> expr` accepts unit parameter — works
- `f ()` passes unit as an argument — works
- `else match x with` (ELSE then MATCH on same line) — works
- `else` then indented `match` block — works (INDENT Expr DEDENT rule handles it)
- `else if x then` on same line — works
- `else if x then` at same indentation — works
- 4-5 levels of pure `let` nesting with implicit IN — works
- 4-5 levels of pure `match` nesting — works
- `let result = let x = a in x + 1` (let...in as RHS of module decl) — works
- Single-param `let rec h x = ... in h n` with EXPLICIT `in` at module level — works

## Sources

### Primary (HIGH confidence)
- Direct source code reading: `src/LangThree/IndentFilter.fs`, `src/LangThree/Parser.fsy`, `src/LangThree/Eval.fs`
- Direct testing: built interpreter and ran test inputs for each SYN requirement
- Constraints document: `langthree-constraints.md` — root cause analysis for SYN-05

### Secondary (MEDIUM confidence)
- `.planning/REQUIREMENTS.md` — requirement definitions and traceability
- `.planning/STATE.md` — accumulated decisions and context
- `.planning/phases/27-list-syntax-completion/27-RESEARCH.md` — prior IndentFilter change patterns

## Metadata

**Confidence breakdown:**
- Root causes: HIGH — all failure modes directly verified by testing
- Fix approaches: HIGH — all fixes follow existing patterns in codebase
- Grammar changes: HIGH — specific grammar rules identified
- Eval fix: HIGH — trampoline bug traced precisely; BuiltinValue+ref pattern verified in LetRecDecl
- Side effects: MEDIUM — extensive testing done but edge cases always possible

**Research date:** 2026-03-25
**Valid until:** 2026-04-25 (stable domain; grammar and eval patterns don't change often)
