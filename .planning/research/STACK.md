# Technology Stack: LangThree v6.0 Practical Programming

**Project:** LangThree — ML-style functional language interpreter
**Researched:** 2026-03-28
**Milestone:** v6.0 — newline implicit sequencing, for-in loops, Option/Result utilities
**Confidence:** HIGH — all three features extend well-understood existing infrastructure

---

## Existing Stack (No Changes Needed)

| Technology | Version | Role |
|------------|---------|------|
| F# | .NET 10 | Implementation language |
| fslex (FsLexYacc) | 12.x | Lexer — Lexer.fsl |
| fsyacc (FsLexYacc) | 12.x | LALR(1) parser — Parser.fsy |
| IndentFilter.fs | custom | Token-stream filter between lexer and parser |
| SeqExpr nonterminal | Parser.fsy | e1; e2 sequencing, desugars to LetPat(WildcardPat, e1, e2) |
| Prelude/*.fun | .fun files | Auto-loaded standard library |

The entire stack is inherited and validated. No new dependencies, no version changes.

---

## Feature 1: Newline Implicit Sequencing

### What Must Change

**IndentFilter.fs only.** The parser and evaluator need zero changes because SeqExpr already handles `e1; e2`.

The goal: two statements at the same indentation level inside an expression block are automatically sequenced, as if the user wrote `;` between them.

**Current behavior:**
```
let _ =
    println "a"    ← these are two separate lines at the same indent
    println "b"    ← second line is currently a parse error or ignored
```

**Target behavior:**
```
let _ =
    println "a"
    println "b"    ← treated as: println "a"; println "b"
```

### Mechanism

IndentFilter already injects `IN` tokens for the offside rule. The analogous mechanism for sequencing is: when processing a `NEWLINE col` where `col` equals the current indent level, and we are inside an `InExprBlock` context, emit a `SEMICOLON` token before the next statement.

**Specifically, in `filter` in IndentFilter.fs:**

The `isAtSameLevel` branch (no INDENT/DEDENT emitted, same column as current indent) is the insertion point. Currently this branch either emits pending `IN` tokens or nothing. The addition is: also emit `SEMICOLON` when inside `InExprBlock`.

**Condition for SEMICOLON emission:**
- `isAtSameLevel = true` (col equals current indent top, no INDENT/DEDENT)
- Current context is `InExprBlock _`
- Previous token is not a token that opens a block continuation (EQUALS, ARROW, IN, DO, THEN, ELSE, PIPE, WITH — these indicate the current expression continues on next line, not a new statement)
- Next token is not EOF

**No new tokens, no new AST nodes, no new grammar rules.**

### Token Considerations

`SEMICOLON` is the correct token to inject — it is exactly what SeqExpr expects, and the parser rule is already:

```fsharp
SeqExpr:
    | Expr SEMICOLON SeqExpr   { LetPat(WildcardPat(...), $1, $3, ...) }
    | Expr SEMICOLON           { $1 }
    | Expr                     { $1 }
```

### InExprBlock Context

`InExprBlock baseCol` is pushed when `INDENT` follows `EQUALS`, `ARROW`, `IN`, or `DO`. This is the correct scope for implicit sequencing — it covers:
- `let f () = <INDENT>body<DEDENT>` — function body
- `fun x -> <INDENT>body<DEDENT>` — lambda body
- `let x = <INDENT>body<DEDENT>` — let RHS
- `while cond do <INDENT>body<DEDENT>` — loop body
- `for i = s to e do <INDENT>body<DEDENT>` — loop body

**Do not emit SEMICOLON in `InLetDecl`, `InMatch`, `InTry`, `InModule`, or `TopLevel`.** Those contexts use different mechanisms (IN token, pipe alignment, declarations).

### LALR(1) Safety

Injecting SEMICOLON into the token stream is safe because:
1. SEMICOLON already exists at the grammar level for SeqExpr
2. SeqExpr is the top nonterminal for expression positions
3. The injection happens only inside `InExprBlock`, never at record literal or list literal positions (those are inside bracket depth > 0, where NEWLINE is already suppressed)

---

## Feature 2: for x in collection do body

### What Must Change

Three files: Lexer.fsl, Parser.fsy, Eval.fs. Type.fs, Bidir.fs, Infer.fs need minor additions.

### Token Situation

`IN` is already a keyword token, used for `let x = e in body`. This is the same `in` keyword that `for x in xs do` would use.

**Recommendation: reuse the existing `IN` token.** The grammar context disambiguates — `FOR IDENT IN Expr DO` vs `LET IDENT EQUALS Expr IN SeqExpr` — these are different production rules and the LALR(1) parser has no ambiguity because FOR/LET are distinct lookahead tokens.

No new token declaration needed.

### AST Node

Add a new variant to `Expr` in Ast.fs:

```fsharp
| ForInExpr of var: string * collection: Expr * body: Expr * span: Span
```

Do not reuse `ForExpr` (which has `isTo: bool` and integer `start`/`stop` semantics). `ForInExpr` iterates any `ListValue` or `ArrayValue`, binding each element to `var`.

### Grammar Rules (Parser.fsy)

Add inside the `Expr` production, adjacent to existing `ForExpr` rules:

```fsharp
// FORIN-01: for x in collection do body (inline body)
| FOR IDENT IN Expr DO SeqExpr
    { ForInExpr($2, $4, $6, ruleSpan parseState 1 6) }
// FORIN-02: for x in collection do <indent>body<dedent>
| FOR IDENT IN Expr DO INDENT SeqExpr DEDENT
    { ForInExpr($2, $4, $7, ruleSpan parseState 1 8) }
```

The `IN` token in `FOR IDENT IN Expr` cannot conflict with `let x = e IN body` because the LALR(1) parser sees `FOR` as the first token and shifts into the for-loop production.

### Evaluator (Eval.fs)

Add one match arm in `eval`:

```fsharp
| ForInExpr (var, collExpr, body, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    match collVal with
    | ListValue xs ->
        for x in xs do
            let loopEnv = Map.add var x env
            eval recEnv moduleEnv loopEnv false body |> ignore
        TupleValue []
    | ArrayValue arr ->
        for x in arr do
            let loopEnv = Map.add var x env
            eval recEnv moduleEnv loopEnv false body |> ignore
        TupleValue []
    | _ -> failwith "for-in: expected list or array"
```

### Type Checker (Bidir.fs / Infer.fs)

`ForInExpr` returns `unit` (TETuple []). The collection must be `TList 'a` or `TArray 'a`, and `var` is bound to `'a` in the body. The body must type-check to unit (same constraint as `WhileExpr` and `ForExpr`).

Add `var` to `mutableVars` exclusion set (same pattern as `ForExpr` — loop variable is immutable):

```fsharp
// In Bidir.fs synth/check for ForInExpr:
// var is loop-bound, immutable — add to mutableVars exclusion (or simply don't add to mutableVars)
```

### IndentFilter.fs

`DO` is already in the set of tokens that trigger `InExprBlock` on the next INDENT:

```fsharp
| Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN | Some Parser.DO ->
    state <- { state with Context = InExprBlock(baseCol) :: state.Context }
```

No change needed — `for x in xs do <INDENT>body<DEDENT>` already pushes `InExprBlock` correctly.

---

## Feature 3: Option/Result Utility Functions

### What Must Change

**Prelude/Option.fun and Prelude/Result.fun only.** Zero changes to F# source files.

These are pure library additions written in LangThree itself.

### Current State

**Option.fun (current):**
- `optionMap`, `optionBind`, `optionDefault`, `isSome`, `isNone`, `(<|>)`

**Result.fun (current):**
- `resultMap`, `resultBind`, `resultMapError`, `resultDefault`, `isOk`, `isError`

### Additions Needed

**Option.fun — add idiomatic short aliases:**

```fsharp
let map f opt     = optionMap f opt
let bind f opt    = optionBind f opt
let defaultValue d opt = optionDefault d opt
let orElse b a    = match a with | Some x -> Some x | None -> b
let filter pred opt = match opt with | Some x -> if pred x then Some x else None | None -> None
let toList opt    = match opt with | Some x -> [x] | None -> []
let ofBool b x    = if b then Some x else None
```

**Result.fun — add idiomatic short aliases:**

```fsharp
let map f r       = resultMap f r
let bind f r      = resultBind f r
let mapError f r  = resultMapError f r
let defaultValue d r = resultDefault d r
let toOption r    = match r with | Ok x -> Some x | Error _ -> None
let fromOption err opt = match opt with | Some x -> Ok x | None -> Error err
let fold onOk onError r = match r with | Ok x -> onOk x | Error e -> onError e
```

**No new types, no new builtins, no grammar changes.**

### Naming Convention Decision

The existing functions use long prefixed names (`optionMap`, `resultBind`) which are module-qualified as `Option.map` etc. The additions should use short names (`map`, `bind`) for ergonomic use after `open Option`.

The existing long-name functions are kept for backward compatibility. Short names are aliases.

---

## Summary: What Changes Per File

| File | Change | Scope |
|------|--------|-------|
| `IndentFilter.fs` | Emit SEMICOLON in `InExprBlock` at same-level NEWLINE | ~10-20 lines |
| `Ast.fs` | Add `ForInExpr` variant to `Expr` DU and `spanOf` | ~4 lines |
| `Lexer.fsl` | No changes | — |
| `Parser.fsy` | Add 2 grammar rules for `FOR IDENT IN Expr DO` | ~6 lines |
| `Eval.fs` | Add `ForInExpr` match arm | ~10 lines |
| `Bidir.fs` | Add `ForInExpr` type checking | ~8 lines |
| `Infer.fs` | Add `ForInExpr` to inferType if present | ~4 lines |
| `Elaborate.fs` | Add `ForInExpr` passthrough | ~2 lines |
| `Format.fs` | Add `ForInExpr` formatting | ~3 lines |
| `Exhaustive.fs` | Add `ForInExpr` traversal if present | ~2 lines |
| `Prelude/Option.fun` | Add short-name aliases + filter/toList/ofBool | ~7 lines |
| `Prelude/Result.fun` | Add short-name aliases + toOption/fromOption/fold | ~7 lines |

**No new NuGet packages. No new tools. No version changes.**

---

## Alternatives Considered

| Decision | Alternative | Why This Way |
|----------|-------------|--------------|
| Reuse `IN` token for for-in | New `IN_KW` or `OF_KW` token | Grammar context is unambiguous; same keyword is natural |
| New `ForInExpr` AST node | Desugar to `List.iter` call | Need type-level dispatch (list vs array); desugar loses error context |
| SEMICOLON injection in IndentFilter | New `NEWLINE_SEQ` token | SeqExpr already handles SEMICOLON; no grammar change needed |
| Short aliases in Option/Result | Rename existing functions | Break backward compatibility for existing tests |
| Emit SEMICOLON only in InExprBlock | Also in InLetDecl/TopLevel | Declarations are not statements; sequencing only applies inside expression blocks |

---

## Risk Assessment

| Area | Risk | Mitigation |
|------|------|------------|
| SEMICOLON injection | Could break multi-line expressions misread as two statements | Gated strictly on `InExprBlock`; previous-token guard prevents false positives |
| `IN` token reuse | LALR(1) conflict | FOR and LET are distinct shift states; parser table verified at build time by fsyacc |
| ForInExpr propagation | Missing a case in one of 6+ files causes compile error | F# exhaustive DU matching — the compiler flags every missing case |
| Option/Result additions | Name collision with existing | Short names are new; long names kept — no removal |
