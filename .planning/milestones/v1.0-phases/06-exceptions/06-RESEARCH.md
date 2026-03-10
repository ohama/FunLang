# Phase 6: Exceptions - Research

**Researched:** 2026-03-09
**Domain:** Exception handling in a typed functional language (F#-style syntax)
**Confidence:** HIGH

## Summary

This phase adds exception declarations, `raise`, and `try...with` expressions to LangThree. The language already has ADT constructors, pattern matching with exhaustiveness checking, and a bidirectional type checker -- exceptions build directly on all of these.

F# exceptions are structurally identical to single-case discriminated union constructors. An `exception Foo of int` declaration creates a constructor `Foo` that wraps an int, but all exceptions share a single open type (`exn`). Unlike closed ADTs, the exception type is *open* -- new cases can be added anywhere, so exhaustiveness checking must treat exception handlers as inherently non-exhaustive (wildcard required for safety, or emit a warning).

The `when` guard feature (EXC-05) is new to LangThree -- match expressions do not currently support guards. The implementation should add `when` guards to both `try...with` handlers AND standard `match` expressions simultaneously, since they share the same MatchClause infrastructure.

**Primary recommendation:** Model exceptions as an open variant of the existing ADT constructor system. Reuse `ConstructorPat`/`DataValue` patterns with a new `exn` base type. Use F#'s .NET `System.Exception` as the runtime mechanism (F# exceptions ARE .NET exceptions).

## Standard Stack

This phase uses no new libraries. All implementation is within the existing codebase.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| FsLexYacc | existing | Lexer/parser generation | Already in use, add new tokens/rules |
| Expecto | existing | Test framework | Already in use for all test files |

### Supporting
No new dependencies needed. Exceptions are a language feature, not a library concern.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| .NET exceptions for runtime | Custom continuation-based | .NET exceptions are idiomatic F#, zero overhead when not thrown |
| Open `exn` type | Closed ADT per module | Open type matches F# semantics exactly |

## Architecture Patterns

### AST Extensions

The following new AST nodes are needed:

```fsharp
// In Ast.fs - New Expr variants
| Raise of expr: Expr * span: Span
    // raise (MyError "bad input")
| TryWith of body: Expr * handlers: MatchClause list * span: Span
    // try expr with | Pat1 -> e1 | Pat2 -> e2

// In Ast.fs - New Decl variant
| ExceptionDecl of name: string * dataType: TypeExpr option * Span
    // exception MyError of string
```

### MatchClause Extension for `when` Guards

Currently: `and MatchClause = Pattern * Expr`

Must become: `and MatchClause = Pattern * Expr option * Expr`
where the middle `Expr option` is the `when` guard.

This is a **cross-cutting change** that affects:
- `Ast.fs` (type definition)
- `Parser.fsy` (grammar rules for match and try-with)
- `Eval.fs` (evalMatchClauses must evaluate guard)
- `Bidir.fs` (synth/check Match must type-check guard as bool)
- `TypeCheck.fs` (collectMatches, checkMatchWarnings, rewriteModuleAccess)
- `Exhaustive.fs` (guards make patterns non-exhaustive by default)
- `Infer.fs` (deprecated but still compiled -- must update Match case)

### Type System Extensions

```fsharp
// In Type.fs - New type
| TExn    // The exception base type (open type, like F#'s exn)

// In Type.fs - ConstructorEnv reuse
// Exception constructors are stored in ConstructorEnv with:
//   ResultType = TExn (not TData)
//   IsGadt = false
//   TypeParams = [] (exceptions are monomorphic in F#)
```

**Critical design decision:** F# exceptions are monomorphic -- `exception Foo of 'a` is NOT valid F#. Exception payloads cannot be polymorphic. This simplifies the type system significantly.

### Value/Runtime Extensions

```fsharp
// In Ast.fs Value type - New variant
| ExnValue of name: string * value: Value option
    // Runtime representation of an exception value

// In F# host: use a .NET exception to carry the value
exception LangThreeException of Value
```

The evaluator uses F#'s own exception mechanism:
- `raise` evaluates its argument, wraps in `LangThreeException`, throws
- `try...with` uses F# `try...with` to catch `LangThreeException`, then pattern matches the carried `Value`

### Error Codes

Following the existing convention (E01xx indent, E02xx ADT, E03xx records, E04xx GADT, E05xx modules):

| Code | Description |
|------|-------------|
| E0601 | Undefined exception constructor |
| E0602 | Exception constructor arity mismatch |
| E0603 | `raise` argument is not an exception type |
| E0604 | `when` guard expression is not boolean |
| E0605 | Non-exhaustive exception handler (warning W0003) |

### Recommended Implementation Order

```
1. AST changes (Expr, Pattern, Decl, Value, MatchClause)
2. Type.fs: Add TExn
3. Lexer tokens: EXCEPTION, RAISE, TRY, WHEN
4. Parser rules: exception decl, raise expr, try-with, when guards
5. Elaborate.fs: elaborateExceptionDecl
6. Eval.fs: LangThreeException, raise eval, try-with eval, when guard eval
7. Bidir.fs: synth/check for Raise, TryWith, when guards
8. TypeCheck.fs: typeCheckDecls for ExceptionDecl
9. Exhaustive.fs: handle open type (exn handlers always need wildcard)
10. Tests
```

### Pattern 1: Exception Declaration Elaboration

**What:** Convert `exception ParseError of string * int` into a ConstructorEnv entry
**When to use:** During typeCheckDecls first-pass, same as TypeDecl processing

```fsharp
// In Elaborate.fs
let elaborateExceptionDecl (name: string) (dataType: TypeExpr option) : string * ConstructorInfo =
    let argType =
        dataType |> Option.map (substTypeExprWithMap Map.empty)
    (name, {
        TypeParams = []          // Exceptions are monomorphic
        ArgType = argType
        ResultType = TExn        // All exceptions produce exn type
        IsGadt = false
        ExistentialVars = []
    })
```

### Pattern 2: Raise Expression

**What:** `raise (MyError "bad")` evaluates argument, throws .NET exception
**When to use:** Raise is an expression that never returns (has type `'a`)

```fsharp
// In Bidir.fs synth
| Raise (arg, span) ->
    let s1, argTy = synth ctorEnv recEnv ctx env arg
    let s2 = unifyWithContext ctx [] span (apply s1 argTy) TExn
    let resultTy = freshVar()  // raise has type 'a (bottom)
    (compose s2 s1, resultTy)

// In Eval.fs
| Raise (arg, _) ->
    let exnVal = eval recEnv moduleEnv env arg
    raise (LangThreeException exnVal)
```

### Pattern 3: Try-With Expression

**What:** `try expr with | pattern -> handler` catches and handles exceptions
**When to use:** Expression context, similar to match

```fsharp
// In Eval.fs
| TryWith (body, handlers, _) ->
    try
        eval recEnv moduleEnv env body
    with
    | LangThreeException exnVal ->
        evalMatchClauses recEnv moduleEnv env exnVal handlers

// In Bidir.fs synth
| TryWith (body, handlers, span) ->
    let s1, bodyTy = synth ctorEnv recEnv ctx env body
    // Each handler must unify pattern with TExn and body with bodyTy
    let folder (s, idx) (pat, guard, expr) =
        let patEnv, patTy = inferPattern ctorEnv pat
        let s' = unifyWithContext ctx [] span (apply s patTy) TExn
        let clauseEnv = Map.fold (fun acc k v -> Map.add k v acc)
                            (applyEnv s' (applyEnv s env)) patEnv
        // Type check when guard (must be bool)
        let sGuard = match guard with
                     | Some g ->
                         let sg, gTy = synth ctorEnv recEnv ctx clauseEnv g
                         let sg' = unifyWithContext ctx [] span gTy TBool
                         compose sg' sg
                     | None -> empty
        let clauseEnv' = applyEnv sGuard clauseEnv
        let s'', exprTy = synth ctorEnv recEnv ctx clauseEnv' expr
        let s''' = unifyWithContext ctx [] span (apply s'' bodyTy) exprTy
        (compose s''' (compose s'' (compose sGuard (compose s' s))), idx + 1)
    let finalS, _ = List.fold folder (s1, 0) handlers
    (finalS, apply finalS bodyTy)
```

### Pattern 4: When Guards in Match/Try-With

**What:** `| pattern when condition -> body`
**When to use:** Both match expressions and try-with handlers

```fsharp
// Parser grammar (same for MatchClauses and TryWithClauses)
MatchClauses:
    | PIPE Pattern ARROW Expr                     { [($2, None, $4)] }
    | PIPE Pattern WHEN Expr ARROW Expr           { [($2, Some $4, $6)] }
    | PIPE Pattern ARROW Expr MatchClauses        { ($2, None, $4) :: $5 }
    | PIPE Pattern WHEN Expr ARROW Expr MatchClauses  { ($2, Some $4, $6) :: $7 }

// Eval: guard check before binding
and evalMatchClauses recEnv moduleEnv env scrutinee clauses =
    match clauses with
    | [] -> failwith "Match failure: no pattern matched"
    | (pattern, guard, resultExpr) :: rest ->
        match matchPattern pattern scrutinee with
        | Some bindings ->
            let extendedEnv = List.fold (fun e (n, v) -> Map.add n v e) env bindings
            // Check when guard
            match guard with
            | None ->
                eval recEnv moduleEnv extendedEnv resultExpr
            | Some guardExpr ->
                match eval recEnv moduleEnv extendedEnv guardExpr with
                | BoolValue true -> eval recEnv moduleEnv extendedEnv resultExpr
                | _ -> evalMatchClauses recEnv moduleEnv env scrutinee rest
        | None ->
            evalMatchClauses recEnv moduleEnv env scrutinee rest
```

### Anti-Patterns to Avoid

- **Making exceptions polymorphic:** F# exceptions are monomorphic. Do NOT add type parameters to exception declarations. `exception Foo of 'a` should be a parse error.
- **Treating exn as a closed type for exhaustiveness:** Exception handlers should NOT require exhaustiveness. The `exn` type is open -- new exceptions can be declared anywhere. A warning for non-exhaustive handlers is fine, but it must not be an error.
- **Forgetting MatchClause is a triple now:** The change from `Pattern * Expr` to `Pattern * Expr option * Expr` touches every file that destructures MatchClause. Missing even one will cause a compile error. Use the F# compiler as your guide -- it will catch all missed sites.
- **Using `try` as identifier name:** `try` is an F# keyword. Make sure it does not conflict with the token name. Use `TRY` as the token name.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Exception propagation | Custom stack unwinding | F#'s native `try`/`with` + .NET exceptions | Battle-tested, correct stack unwinding, zero cost when not thrown |
| Exception value transport | Custom global mutable state | `exception LangThreeException of Value` | F# DU exception carries payload through .NET exception mechanism |
| Pattern matching in handlers | Separate handler matching | Reuse existing `matchPattern` + `evalMatchClauses` | Already handles all pattern types correctly |
| Guard type checking | New type check function | Add guard parameter to existing synth Match logic | Same type checking applies |

**Key insight:** The entire exception runtime mechanism is just 5 lines of F# code wrapping/unwrapping .NET exceptions. The real work is in the parser, AST, and type checker, which all follow established patterns from ADT/match implementation.

## Common Pitfalls

### Pitfall 1: MatchClause Tuple Width Change
**What goes wrong:** Changing `MatchClause = Pattern * Expr` to `Pattern * Expr option * Expr` causes compile errors in 8+ files
**Why it happens:** MatchClause is destructured everywhere in the codebase
**How to avoid:** Change the type definition FIRST, then fix every compile error. The F# compiler will list every site.
**Warning signs:** Any file that touches `MatchClause`, `Match`, or `clauses` will need updating

### Pitfall 2: `raise` Return Type
**What goes wrong:** `raise` returns bottom type (never returns), but the type checker expects a concrete type
**Why it happens:** In `if cond then value else raise exn`, both branches must unify. `raise` must have the same type as `value`.
**How to avoid:** Give `raise` a fresh type variable as its return type. This is the standard approach -- the fresh variable will unify with whatever the context requires.
**Warning signs:** Type errors in if-then-else or let expressions using raise

### Pitfall 3: When Guards and Exhaustiveness
**What goes wrong:** A pattern with a `when` guard is NOT guaranteed to match even if the pattern itself matches
**Why it happens:** The guard can be `false`, causing the clause to be skipped
**How to avoid:** In exhaustiveness checking, treat guarded patterns as non-exhaustive (they don't cover any cases definitively). The Maranget algorithm should treat guarded patterns as if they might not match.
**Warning signs:** False "exhaustive" reports when all patterns have guards

### Pitfall 4: Exception Constructor vs ADT Constructor Ambiguity
**What goes wrong:** Uppercase identifiers are parsed as constructors. Exception constructors and ADT constructors share the namespace.
**Why it happens:** The parser uses `System.Char.IsUpper` to distinguish constructors from variables
**How to avoid:** Store exception constructors in the same `ConstructorEnv` as ADT constructors. They already share the same pattern matching infrastructure. The only difference is their `ResultType` is `TExn` instead of `TData`.
**Warning signs:** Name conflicts between exception and ADT constructors

### Pitfall 5: Token Conflicts with `try` and `when`
**What goes wrong:** `try` and `when` might already be used as identifiers in existing programs
**Why it happens:** They were not previously reserved keywords
**How to avoid:** Add them as keywords in the lexer BEFORE the general identifier rule (same pattern as all other keywords). This is a breaking change for programs using `try` or `when` as variable names, which is acceptable.
**Warning signs:** Parse errors in programs using these as identifiers

### Pitfall 6: IndentFilter for try-with blocks
**What goes wrong:** `try...with` is a multi-line construct that needs proper INDENT/DEDENT handling
**Why it happens:** The IndentFilter processes indentation for multi-line constructs like `match...with`
**How to avoid:** Model `try...with` indentation handling identically to `match...with` in IndentFilter. The `with` keyword resets to the base column, and handler clauses are indented under it.
**Warning signs:** Parse errors on multi-line try-with blocks

## Code Examples

### Exception Declaration (User Syntax)
```
exception DivideByZero
exception ParseError of string
exception NetworkError of string * int
```

### Raise Expression (User Syntax)
```
let safeDivide x y =
    if y = 0 then
        raise DivideByZero
    else
        x / y
```

### Try-With Expression (User Syntax)
```
let result =
    try
        safeDivide 10 0
    with
    | DivideByZero -> -1
    | ParseError msg -> -2
```

### When Guards (User Syntax)
```
let classify x =
    match x with
    | n when n < 0 -> "negative"
    | n when n = 0 -> "zero"
    | _ -> "positive"

let handle =
    try
        riskyOperation ()
    with
    | NetworkError (msg, code) when code >= 500 -> "server error"
    | NetworkError (msg, code) when code >= 400 -> "client error"
    | _ -> "unknown error"
```

### Lexer Additions
```
// In Lexer.fsl - new keywords (add before ident_start rule)
| "exception"   { EXCEPTION }
| "raise"       { RAISE }
| "try"         { TRY }
| "when"        { WHEN }
```

### Parser Additions
```
// In Parser.fsy - new tokens
%token EXCEPTION RAISE TRY WHEN

// Exception declaration (in Decls)
| EXCEPTION IDENT                           { [ExceptionDecl($2, None, ruleSpan parseState 1 2)] }
| EXCEPTION IDENT OF TypeExpr              { [ExceptionDecl($2, Some $4, ruleSpan parseState 1 4)] }

// Raise expression (in Expr, AppExpr, or Factor)
// raise is a function-like keyword applied to an exception value
// Best approach: parse as unary prefix in Factor
| RAISE AppExpr    { Raise($2, ruleSpan parseState 1 2) }

// Try-with expression (in Expr)
| TRY Expr WITH MatchClauses   { TryWith($2, $4, ruleSpan parseState 1 4) }

// When guard in MatchClauses
MatchClauses:
    | PIPE Pattern ARROW Expr                         { [($2, None, $4)] }
    | PIPE Pattern WHEN Expr ARROW Expr               { [($2, Some $4, $6)] }
    | PIPE Pattern ARROW Expr MatchClauses            { ($2, None, $4) :: $5 }
    | PIPE Pattern WHEN Expr ARROW Expr MatchClauses  { ($2, Some $4, $6) :: $7 }
```

### F# Runtime Exception Definition
```fsharp
// In Eval.fs or a new shared module
exception LangThreeException of Value
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No exception handling | F#-style exceptions with try-with | Phase 6 | Users can handle errors structurally |
| MatchClause = Pattern * Expr | MatchClause = Pattern * Expr option * Expr | Phase 6 | Enables when guards everywhere |
| Closed exhaustiveness only | Open type (exn) exhaustiveness | Phase 6 | Exception handlers are inherently non-exhaustive |

## Open Questions

1. **Should `raise` be a keyword or a built-in function?**
   - What we know: F# treats `raise` as a built-in function with type `exn -> 'a`. OCaml treats it as a keyword.
   - What's unclear: Whether to add it as a keyword (simpler parser) or as a prelude function (more consistent)
   - Recommendation: Make it a keyword parsed in the grammar. Simpler implementation, and the user syntax is identical either way. The type `exn -> 'a` is hard to express in the current type system (requires bottom type), so a keyword is easier.

2. **Should `when` guards also apply to `let` pattern bindings?**
   - What we know: F# allows guards only in match/try-with, not in let bindings
   - What's unclear: Whether to scope this to just match/try-with or also let-pat
   - Recommendation: Match F# behavior -- guards only in match/try-with clauses. Keep scope narrow for Phase 6.

3. **How should `try...with` interact with indentation?**
   - What we know: `match...with` already works with INDENT/DEDENT via IndentFilter. `try` body needs indentation, `with` resets to base level.
   - What's unclear: Whether IndentFilter needs special `try` handling or if the existing match-with logic extends naturally
   - Recommendation: The IndentFilter's InMatch context should work if we add InTry context with similar logic. The `with` keyword is already handled. Test with multi-line try-with blocks early.

4. **Exception pattern in module-qualified access?**
   - What we know: ADT constructors work with `Module.Constructor` syntax via rewriteModuleAccess
   - What's unclear: Whether exception constructors need the same treatment
   - Recommendation: Yes, since exception constructors go in ConstructorEnv and ModuleExports.CtorEnv, the existing module access rewriting should work automatically. Verify with a test.

## Sources

### Primary (HIGH confidence)
- Codebase analysis: Ast.fs, Type.fs, Bidir.fs, Eval.fs, TypeCheck.fs, Parser.fsy, Lexer.fsl, Exhaustive.fs, Elaborate.fs, Diagnostic.fs, IndentFilter.fs -- all source files read in full
- F# language specification: Exception type semantics (exceptions as open DU variants, monomorphic, .NET interop)

### Secondary (MEDIUM confidence)
- Prior phase patterns: Phase 2 (ADT), Phase 4 (GADT) implementation patterns from .planning/phases/02-algebraic-data-types/

### Tertiary (LOW confidence)
- None. All findings are based on direct codebase analysis and F# language semantics.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new dependencies, extends existing infrastructure
- Architecture: HIGH - Follows established ADT/match patterns exactly, all extension points identified
- Pitfalls: HIGH - Based on direct analysis of every file that touches MatchClause and constructors

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable -- no external dependencies to change)
