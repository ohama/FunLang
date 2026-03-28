# Phase 50: Newline Implicit Sequencing - Research

**Researched:** 2026-03-28
**Domain:** IndentFilter.fs — token injection for implicit expression sequencing
**Confidence:** HIGH

## Summary

Phase 50 adds newline-based implicit sequencing so users can write `stmt1 \n stmt2` instead of `stmt1; stmt2` inside expression blocks (lambda bodies, let RHS, match arm bodies, while/for bodies). The implementation is a single, well-scoped change to `IndentFilter.fs`: inject a `SEMICOLON` token when a same-indent newline occurs inside an `InExprBlock` context, with guards to prevent spurious injection before structural terminators and infix-operator continuation lines.

The parser (`SeqExpr`) already handles `SEMICOLON` tokens for sequencing — no parser changes are needed. The `InExprBlock` context (already on the context stack) is the precise discriminator: it is pushed only after `EQUALS`, `ARROW`, `IN`, and `DO` — covering all target scopes. Module-level code (`InModule`/`TopLevel`) and let-chain bodies (`InLetDecl` on top) are automatically excluded by the context guard.

**Primary recommendation:** Add a `shouldInjectSemicolon` check in the `isAtSameLevel` branch of the NEWLINE handler in `IndentFilter.fs`, firing only when `InExprBlock` is the top context and nextToken is neither a structural terminator nor an infix-operator continuation.

## Standard Stack

No new libraries. This is a pure change to `IndentFilter.fs` — the existing filter infrastructure.

### Core Files
| File | Role | What Changes |
|------|------|--------------|
| `src/LangThree/IndentFilter.fs` | Token stream filter | Add SEMICOLON injection in `isAtSameLevel` branch |
| `tests/flt/expr/seq/` | Existing seq tests | Add 5 new nlseq-*.flt tests |
| `tests/flt/file/...` | Regression suite | Must remain 573/573 pass |

### No New Dependencies

```bash
# No installation needed — pure logic change
dotnet build src/LangThree/LangThree.fsproj -c Release
../fslit/dist/FsLit tests/flt/
```

## Architecture Patterns

### Recommended Change Location

The change goes in the `isAtSameLevel` branch of the `NEWLINE` handler inside the `filter` function, after the existing offside rule (IN injection) check.

```
IndentFilter.filter (filter function)
└── NEWLINE col handler
    ├── processNewlineWithContext (unchanged)
    ├── isAtSameLevel calculation (unchanged)
    ├── if isAtSameLevel then
    │   ├── checkOffside → emit IN tokens (existing, unchanged)
    │   └── ELSE branch (currently: yield! emitted which is empty)
    │       NEW: check shouldInjectSemicolon → yield SEMICOLON
    └── else branch (INDENT/DEDENT, unchanged)
```

### Pattern 1: InExprBlock Context

`InExprBlock` is pushed in exactly one place (line 317-320 of IndentFilter.fs):

```fsharp
// Source: IndentFilter.fs lines 315-321
// Push expression block for EQUALS (not module) or ARROW
match state.PrevToken with
| Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN | Some Parser.DO ->
    let baseCol = match state.IndentStack with _ :: parent :: _ -> parent | _ -> 0
    state <- { state with Context = InExprBlock(baseCol) :: state.Context }
| _ -> ()
```

This fires whenever `INDENT` is emitted after these tokens — covering:
- `let x =\n    body` (EQUALS)
- `fun x ->\n    body` (ARROW)
- `| pat ->\n    body` (ARROW — match arm bodies)
- `let x in\n    body` (IN)
- `while cond do\n    body` (DO)
- `for i = s to e do\n    body` (DO)

It does NOT fire for `THEN`, `WITH` — so if-then and try-with bodies at deeper indents do not get `InExprBlock`. This is correct: those bodies use single-expression indented blocks handled by `INDENT SeqExpr DEDENT` in the parser.

### Pattern 2: SEMICOLON Injection Guard Logic

```fsharp
// NEW helper function to add to IndentFilter.fs
/// Check if a token can start a continuation line (suppress SEMICOLON)
let isContinuationStart (token: Parser.token) : bool =
    match token with
    | Parser.PIPE_RIGHT | Parser.COMPOSE_RIGHT | Parser.COMPOSE_LEFT -> true
    | Parser.AND | Parser.OR -> true
    | Parser.CONS -> true
    | Parser.INFIXOP0 _ | Parser.INFIXOP1 _ | Parser.INFIXOP2 _
    | Parser.INFIXOP3 _ | Parser.INFIXOP4 _ -> true
    | _ -> false

/// Check if a token is a structural terminator that follows at same indent
let isStructuralTerminator (token: Parser.token) : bool =
    match token with
    | Parser.ELSE | Parser.WITH | Parser.THEN | Parser.PIPE | Parser.IN -> true
    | _ -> false
```

### Pattern 3: The shouldInjectSemicolon Logic

```fsharp
// Inside the `else` branch of `if isAtSameLevel then`
// (after checkOffside returns no IN tokens)
let shouldInjectSemicolon =
    match newState.Context with
    | InExprBlock _ :: _ ->
        let nextContinues =
            match nextToken with
            | Some t -> isContinuationStart t || isStructuralTerminator t
            | None -> false
        not nextContinues
    | _ -> false

if shouldInjectSemicolon then
    state <- newState
    yield Parser.SEMICOLON
else
    state <- newState
    yield! emitted  // emitted is [] at same level
```

### Pattern 4: Existing Code Path — Offside Takes Priority

The `checkOffside` logic (IN injection for `InLetDecl`) runs BEFORE the SEMICOLON check. This is already correct because:

- When `InLetDecl` is on top, `checkOffside` fires and emits `IN` (existing behavior, unchanged)
- The SEMICOLON check only runs when `checkOffside` produces no tokens
- When `InLetDecl` is on top, the SEMICOLON guard (checking for `InExprBlock _ :: _`) fails, so no double injection

### Recommended Project Structure (Unchanged)

```
src/LangThree/
├── IndentFilter.fs     # THE ONLY FILE THAT CHANGES
└── (all others unchanged)

tests/flt/expr/seq/
├── seq-basic.flt           # existing
├── seq-chained.flt         # existing
├── seq-in-block.flt        # existing
├── seq-list-no-conflict.flt # existing
├── seq-trailing.flt        # existing
├── nlseq-basic.flt         # NEW: basic multi-line sequencing
├── nlseq-in-match.flt      # NEW: match arm body sequencing
├── nlseq-in-while.flt      # NEW: while body sequencing (no explicit ;)
├── nlseq-no-module.flt     # NEW: module-level not affected
└── nlseq-pipe-continuation.flt  # NEW: |> continuation suppresses SEMICOLON
```

### Anti-Patterns to Avoid

- **Adding context push for THEN/ELSE/WITH:** Do not add InExprBlock for THEN-triggered indents — this would break the if-then-else grammar which relies on `INDENT SeqExpr DEDENT` structure.
- **Checking prevToken for operator guards:** The operator guard must check `nextToken`, not `prevToken`. The next line's starting token determines whether it's a continuation, not the previous line's ending token.
- **Using PLUS/MINUS as continuation guards:** Unary `-expr` is valid as a statement start (`-1` as expression). Suppressing SEMICOLON for MINUS would incorrectly merge statements like `println "a" \n -1`. Be conservative: only include unambiguous infix-only tokens (PIPE_RIGHT, etc.) as continuation starters.
- **Double-injection:** Do not place SEMICOLON injection before or inside the `checkOffside` block. SEMICOLON fires only when `checkOffside` produces nothing.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Newline sequencing | Custom parser rule | IndentFilter SEMICOLON injection + existing SeqExpr | SeqExpr already handles SEMICOLON; parser handles `Expr SEMICOLON SeqExpr` |
| Continuation detection | Line lookahead loop | Single `nextToken` option already provided to `processNewlineWithContext` | `nextToken` is the first non-NEWLINE token, already computed |
| Context tracking | New context type | Existing `InExprBlock` is the right discriminator | InExprBlock is already pushed in exactly the right places |

**Key insight:** The `SeqExpr` nonterminal was designed in Phase 45 specifically to enable this injection pattern. No grammar changes required.

## Common Pitfalls

### Pitfall 1: Injecting SEMICOLON When InLetDecl Is on Top (Let Chains Break)

**What goes wrong:** Let chains (`let x = 1 \n let y = 2 \n x + y`) would get spurious SEMICOLON between `let` bindings, causing parse errors.

**Why it happens:** `InLetDecl` is pushed on top of `InExprBlock` whenever `let` appears inside an expression block. If SEMICOLON injection checks `isExprContext` (which returns true for InLetDecl) instead of checking for `InExprBlock` specifically, it fires when InLetDecl is on top.

**How to avoid:** Guard with `match newState.Context with InExprBlock _ :: _ -> ...` — only fires when InExprBlock is the DIRECT top of context. This is already safe because `InLetDecl` is pushed ON TOP of InExprBlock.

**Warning signs:** Tests in `tests/flt/file/implicit-in/` and `tests/flt/file/offside/` fail with parse errors.

### Pitfall 2: ELSE, WITH, THEN Not Guarded (If-Then-Else and Try-With Break)

**What goes wrong:** Code like:
```
let result =
    if cond then e1
    else e2
```
injects SEMICOLON before `else`, giving `if cond then e1; else e2` which is a parse error.

**Why it happens:** Both lines are at the same indent inside an InExprBlock. Without the structural terminator guard, SEMICOLON is injected before ELSE.

**How to avoid:** Add `ELSE | WITH | THEN | PIPE | IN` to `isStructuralTerminator` and check against `nextToken`.

**Warning signs:** Any test with `if-then-else` or `try-with` at same indent as surrounding code.

### Pitfall 3: Multi-Line |> Pipe Chain Gets Spurious SEMICOLON

**What goes wrong:**
```
let result =
    [1..10]
    |> filter (fun x -> x > 5)
    |> map (fun x -> x * 2)
```
Injects SEMICOLON before `|>`, making `[1..10]; |> filter` which is a parse error.

**Why it happens:** `|>` starts at same indent, InExprBlock is on top, no guard.

**How to avoid:** Add `PIPE_RIGHT | COMPOSE_RIGHT | COMPOSE_LEFT` to `isContinuationStart`. These are unambiguous (PIPE_RIGHT cannot start a statement).

**Warning signs:** Tests with multi-line pipe chains fail.

### Pitfall 4: Module-Level Lets Get Spurious SEMICOLON

**What goes wrong:**
```
let x = 1
let y = 2     <- spurious SEMICOLON before this
```
at module level would give `1; let y = 2` which is a parse error.

**Why it happens:** Module-level is not InExprBlock but if the guard is wrong, it fires.

**How to avoid:** The `InExprBlock _ :: _` context guard automatically excludes TopLevel and InModule. Confirm `isAtSameLevel` is only true for `IndentStack.Length > 1` (already in existing code).

**Warning signs:** ALL file-mode tests fail.

### Pitfall 5: BracketDepth Guard Omission

**What goes wrong:** Newlines inside `[a, b, c]` or `(f x\n  y)` could trigger SEMICOLON injection.

**Why it happens:** Missing BracketDepth check.

**How to avoid:** This is already handled — NEWLINEs with `BracketDepth > 0` are caught BEFORE reaching the NEWLINE handler that runs processNewlineWithContext. No additional guard needed.

## Code Examples

### The Complete Change — shouldInjectSemicolon in isAtSameLevel Branch

```fsharp
// Source: IndentFilter.fs — NEWLINE handler, isAtSameLevel branch

// ADD these two helper functions near canBeFunction/isAtom (around line 86):

/// Check if a token starts a continuation line (infix operator continuing prev expr)
let isContinuationStart (token: Parser.token) : bool =
    match token with
    | Parser.PIPE_RIGHT | Parser.COMPOSE_RIGHT | Parser.COMPOSE_LEFT -> true
    | Parser.AND | Parser.OR | Parser.CONS -> true
    | Parser.INFIXOP0 _ | Parser.INFIXOP1 _ | Parser.INFIXOP2 _
    | Parser.INFIXOP3 _ | Parser.INFIXOP4 _ -> true
    | _ -> false

/// Check if a token is a structural terminator that should NOT be preceded by SEMICOLON
let isStructuralTerminator (token: Parser.token) : bool =
    match token with
    | Parser.ELSE | Parser.WITH | Parser.THEN | Parser.PIPE | Parser.IN -> true
    | _ -> false

// MODIFY the isAtSameLevel branch (around line 270-283):
// Current:
//   if isAtSameLevel then
//       let (newCtx, insTokens) = checkOffside newState.Context []
//       if not (List.isEmpty insTokens) then
//           state <- { newState with Context = newCtx }
//           yield! insTokens
//       else
//           state <- newState
//           yield! emitted   // emitted = [] at same level

// NEW (replace the else branch):
//       else
//           // NLSEQ: inject SEMICOLON if in expression block context
//           let shouldInjectSemicolon =
//               match newState.Context with
//               | InExprBlock _ :: _ ->
//                   let suppressByNext =
//                       match nextToken with
//                       | Some t -> isContinuationStart t || isStructuralTerminator t
//                       | None -> false
//                   not suppressByNext
//               | _ -> false
//           if shouldInjectSemicolon then
//               state <- newState
//               yield Parser.SEMICOLON
//           else
//               state <- newState
//               yield! emitted
```

### Tracing: Multi-Line Assignment Block

Input:
```
let f () =
    x <- 1
    x <- 2
```

Token flow:
1. `let f () =` → EQUALS is PrevToken
2. NEWLINE(4) → INDENT emitted, PrevToken=EQUALS → InExprBlock(0) pushed
3. `x <- 1` → tokens: IDENT, LARROW, NUMBER 1; PrevToken=NUMBER 1
4. NEWLINE(4) → `processNewlineWithContext`: col=4 = stack top=4, emitted=[]
   - `isAtSameLevel` = true
   - `checkOffside`: context top = InExprBlock(0), not InLetDecl → no IN tokens
   - `shouldInjectSemicolon`: context top = InExprBlock → check nextToken=IDENT "x"
   - IDENT is not continuation, not terminator → `shouldInjectSemicolon = true`
   - Emit SEMICOLON
5. `x <- 2` → parser sees `x <- 1 SEMICOLON x <- 2` = `SeqExpr` ✓

### Tracing: Let Chain (Must NOT Break)

Input:
```
let result =
    let x = 1
    let y = 2
    x + y
```

Token flow after `let x = 1`:
- LET token → pushes InLetDecl(true, 4) on context
- context = [InLetDecl(true,4), InExprBlock(0), TopLevel]

At NEWLINE(4) before `let y`:
- isAtSameLevel = true
- `checkOffside`: context top = InLetDecl(true, 4), col=4 ≤ offsideCol=4 → emit IN
- insTokens = [IN] → yield! [IN], NOT SEMICOLON

Then `let y` → pushes InLetDecl(true,4) again. Context top = InLetDecl.
At NEWLINE(4) before `x + y`:
- checkOffside fires again → emit IN

`x + y` is the body of `let y = 2 in x + y`. ✓

### Tracing: If-Then-Else (Structural Terminator Guard)

Input:
```
let result =
    if cond then e1
    else e2
```

At NEWLINE(4) before `else`:
- isAtSameLevel = true
- `checkOffside`: context top = InExprBlock(0), no IN tokens
- `shouldInjectSemicolon`: context top = InExprBlock → check nextToken = ELSE
- `isStructuralTerminator(ELSE)` = true → `suppressByNext` = true
- `shouldInjectSemicolon = false`
- Emit nothing ✓

### Tracing: Pipe Continuation (Continuation Guard)

Input:
```
let result =
    [1..10]
    |> filter (fun x -> x > 5)
```

At NEWLINE(4) before `|>`:
- isAtSameLevel = true
- shouldInjectSemicolon: context = InExprBlock(0)
- nextToken = PIPE_RIGHT
- `isContinuationStart(PIPE_RIGHT)` = true → suppress = true
- shouldInjectSemicolon = false ✓

## State of the Art

| Old Approach | Current Approach | Notes |
|--------------|------------------|-------|
| Explicit `;` for all sequencing | Newline injection for same-indent blocks | Phase 50 adds newline injection |
| SEMICOLON only from lexer | SEMICOLON injected by IndentFilter | Same pattern as INDENT/DEDENT/IN |

**How F#/OCaml handle this:**
- F# uses `OBLOCKSEP` token for same-indent lines in `do`/`begin`/`end` blocks
- OCaml requires explicit `;` (no implicit sequencing) — LangThree is F#-style
- The F# rule: "lines at same column as block start emit OBLOCKSEP" with operator-continuation suppression
- LangThree's equivalent: "same-indent lines in InExprBlock emit SEMICOLON"

## Open Questions

1. **THEN-triggered InExprBlock?**
   - What we know: THEN does not push InExprBlock currently
   - What's unclear: Should `if cond then\n    stmt1\n    stmt2` support NLSEQ?
   - Recommendation: Out of scope for Phase 50. If-then branches use `INDENT SeqExpr DEDENT` and work with single expressions. Add THEN to InExprBlock triggers in a future phase if needed.

2. **LARROW (assignment) as line-ending token?**
   - What we know: `x <- expr` — prevToken after this line is the RHS expression token (NUMBER, IDENT, etc.), not LARROW
   - What's unclear: Is there any case where LARROW appears as prevToken?
   - Recommendation: LARROW as prevToken only appears mid-expression; by the time NEWLINE fires, prevToken is the RHS. No special handling needed.

3. **WHEN keyword in match guards?**
   - What we know: `| pat WHEN expr ->` — WHEN appears inside match arms
   - What's unclear: Could WHEN at same indent as match arm body trigger false SEMICOLON?
   - Recommendation: WHEN appears BEFORE ARROW in match arms — by the time we're in InExprBlock (after ARROW), WHEN can't appear at the same indent as the body. Safe without guard.

4. **AND_KW for mutually recursive types/functions?**
   - What we know: `type T = ... and U = ...` at module level
   - What's unclear: Could AND_KW appear at same indent as expression in InExprBlock?
   - Recommendation: `and` only appears at module level (after `let rec f = ... and g = ...`), which is InModule context. Safe.

## Sources

### Primary (HIGH confidence)
- `src/LangThree/IndentFilter.fs` — Full source read, all functions analyzed
- `src/LangThree/Parser.fsy` — SeqExpr, SEMICOLON rules, all token declarations confirmed
- `tests/flt/` — 573 existing tests analyzed for regression risk

### Secondary (MEDIUM confidence)
- F# language spec on OBLOCKSEP/ODECLEND pattern — conceptual alignment confirmed
- OCaml explicit-semicolon approach — contrasted with LangThree's implicit model

## Metadata

**Confidence breakdown:**
- Standard stack (files to change): HIGH — only IndentFilter.fs
- Architecture (injection location): HIGH — isAtSameLevel branch, after checkOffside
- Guard logic (continuation/terminator sets): HIGH — nextToken-based, verified against all edge cases
- Pitfalls: HIGH — all 5 major pitfalls verified against real test patterns
- Scope (what InExprBlock covers): HIGH — traced through EQUALS/ARROW/IN/DO triggers

**Research date:** 2026-03-28
**Valid until:** 2026-04-28 (stable domain — IndentFilter logic doesn't change without major work)

## At-Risk Test Categories (Regression Priority)

Run these first after the change:

| Category | Count | Risk | Why At Risk |
|----------|-------|------|-------------|
| `tests/flt/file/implicit-in/` | 8 | CRITICAL | InLetDecl/InExprBlock interactions |
| `tests/flt/file/offside/` | 30+ | CRITICAL | Complex context stacks, deep nesting |
| `tests/flt/expr/seq/` | 5 | HIGH | Explicit SEMICOLON still works |
| `tests/flt/file/function/` | 12 | HIGH | Multi-line bodies |
| `tests/flt/file/algorithm/` | 20+ | HIGH | Complex let chains, match arms |
| `tests/flt/file/module/` | 8 | MEDIUM | Module-level not affected |
| `tests/flt/file/mutable/` | 30 | MEDIUM | Multi-stmt patterns in blocks |
| All others | ~460 | LOW | Covered by context guard |
