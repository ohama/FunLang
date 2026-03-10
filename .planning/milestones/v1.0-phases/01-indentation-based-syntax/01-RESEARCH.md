# Phase 1: Indentation-Based Syntax - Research

**Researched:** 2026-03-02
**Domain:** F# style indentation-based parsing (offside rule)
**Confidence:** HIGH

## Summary

This research focuses on completing the remaining work for Phase 1: Indentation-Based Syntax. The initial implementation (10 tasks, PR #8) successfully established the foundation with IndentFilter module, NEWLINE token emission, and basic let-expression support.

The standard approach for F#-style indentation is a three-stage pipeline: Lexer emits NEWLINE tokens with column information → IndentFilter transforms to INDENT/DEDENT → Parser consumes filtered stream. This mirrors F# compiler's LexFilter.fs architecture using token stream filtering rather than parser-level or combinator approaches.

Key remaining work includes: (1) match expression pattern alignment with pipe indentation rules, (2) multi-line function application grouping, (3) configurable indent width with validation, and (4) improved error messages showing expected vs actual indentation. All of these extend the existing IndentFilter and Parser rather than requiring new architectural components.

**Primary recommendation:** Extend the existing IndentFilter module with context-aware processing for match expressions and function applications, using the same token stream filtering pattern already proven successful for let-expressions.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| FsLexYacc | 10.2.0+ | Lexer and parser generators for F# | De facto standard for F# parser development, used by F# compiler itself |
| Expecto | 10.x | Testing framework for F# | F# community standard, concise syntax for property-based testing |
| .NET SDK | 9.0+ | Runtime and build tools | Latest LTS version with F# 9.0 support |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FsCheck | 2.x | Property-based testing | When testing IndentFilter edge cases with generated inputs |
| BenchmarkDotNet | 0.13+ | Performance benchmarking | If IndentFilter performance becomes concern for large files |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| FsLexYacc | FParsec (parser combinators) | FParsec requires parser-level indentation tracking; harder to maintain separation of concerns |
| FsLexYacc | ANTLR with F# target | ANTLR less idiomatic for F#, more complex toolchain |
| Token filter | Parser-level offside | Parser-level mixing concerns; F# compiler proves token filter works at scale |

**Installation:**
```bash
# Already installed in project
dotnet add package FsLexYacc --version 10.2.0
dotnet add package Expecto --version 10.2.0
```

## Architecture Patterns

### Recommended Project Structure
Current structure is correct and follows F# compiler conventions:
```
src/LangThree/
├── IndentFilter.fs      # Token stream filter (NEWLINE → INDENT/DEDENT)
├── Lexer.fsl            # Lexer emits NEWLINE(col), rejects tabs
├── Parser.fsy           # Parser consumes INDENT/DEDENT tokens
└── Ast.fs               # AST definitions
tests/LangThree.Tests/
├── IndentFilterTests.fs # Unit tests for filter logic
└── IntegrationTests.fs  # End-to-end parsing tests
```

### Pattern 1: Token Stream Filtering (ESTABLISHED)
**What:** Three-stage pipeline where lexer emits positional tokens, filter transforms to structural tokens, parser consumes structural tokens.
**When to use:** Indentation-based syntax where offside rule applies uniformly across constructs.
**Example:**
```fsharp
// Already implemented in IndentFilter.fs
let filter (config: IndentConfig) (tokens: Parser.token seq) : Parser.token seq =
    seq {
        let mutable state = initialState
        for token in tokens do
            match token with
            | Parser.NEWLINE col ->
                let (newState, emitted) = processNewline state col
                state <- { newState with LineNum = state.LineNum + 1 }
                yield! emitted
            | Parser.EOF ->
                // Emit DEDENTs for all open indents before EOF
                while state.IndentStack.Length > 1 do
                    let (newState, _) = processNewline state 0
                    state <- newState
                    yield Parser.DEDENT
                yield Parser.EOF
            | other ->
                yield other
    }
```

### Pattern 2: Context-Aware Indent Processing (NEEDED)
**What:** Track syntactic context (in match, in function application) to apply different indentation rules.
**When to use:** When different constructs have different alignment requirements (e.g., match pipes align with `match`, not previous indent).
**Example:**
```fsharp
// Source: F# formatting guidelines - match expressions
// Pipes align directly under "match" keyword, not indented from it
match expression with
| Pattern1 -> result1
| Pattern2 -> result2
| Pattern3 -> result3

// NOT this (common mistake):
match expression with
    | Pattern1 -> result1
    | Pattern2 -> result2
```

**Implementation approach:**
```fsharp
// Extend FilterState to track context
type SyntaxContext =
    | TopLevel
    | InMatch of baseColumn: int
    | InFunctionApp of baseColumn: int

type FilterState = {
    IndentStack: int list
    LineNum: int
    Context: SyntaxContext list  // Stack of contexts
}

// Context-aware indent validation
let processNewlineWithContext (state: FilterState) (col: int) (nextToken: Parser.token option) =
    match state.Context, nextToken with
    | (InMatch matchCol) :: _, Some(Parser.PIPE) ->
        // Pipe must align with match keyword column
        if col <> matchCol then
            raise (IndentationError(state.LineNum,
                $"Match pattern pipe must align with 'match' at column {matchCol}, found at column {col}"))
        // Don't change indent stack for pipes
        (state, [])
    | _ ->
        // Normal indent/dedent processing
        processNewline state col
```

### Pattern 3: Multi-Line Function Application Grouping (NEEDED)
**What:** Allow function arguments to span multiple lines with consistent indentation.
**When to use:** Function applications where arguments don't fit on one line.
**Example:**
```fsharp
// Source: F# formatting guidelines - function applications
// Arguments on new lines indented one level from function name
someFunction2
    x.IngredientName x.Quantity

someFunction4
    x.IngredientName1
    x.Quantity2
    x.IngredientName2
    x.Quantity2
```

**Parser grammar approach:**
```fsharp
// Extend Parser.fsy AppExpr rules to accept INDENT/DEDENT
AppExpr:
    | AppExpr Atom               { App($1, $2, ruleSpan parseState 1 2) }
    | AppExpr INDENT AppArgs DEDENT
        { AppMultiLine($1, $3, ruleSpan parseState 1 4) }
    | Atom                       { $1 }

AppArgs:
    | Atom                       { [$1] }
    | Atom AppArgs               { $1 :: $2 }
```

### Pattern 4: Configurable Indent Width Validation (NEEDED)
**What:** Validate that indentation is a multiple of configured width (default 4), with clear error when violated.
**When to use:** Always active, to enforce consistent indentation style.
**Example:**
```fsharp
// Extend IndentConfig (already exists)
type IndentConfig = {
    IndentWidth: int  // 4 (default), 2, or 8
    StrictWidth: bool // If true, enforce exact multiples
}

// Validation in processNewline
let validateIndentWidth (config: IndentConfig) (col: int) (lineNum: int) =
    if config.StrictWidth && col % config.IndentWidth <> 0 then
        let expected = (col / config.IndentWidth + 1) * config.IndentWidth
        raise (IndentationError(lineNum,
            $"Indentation must be multiple of {config.IndentWidth} spaces. Found {col}, expected {expected}"))
```

### Anti-Patterns to Avoid
- **Name-sensitive alignment:** Don't align continuation lines to match identifier length (e.g., aligning after long function names). This breaks when names change and wastes horizontal space. Use fixed indent levels instead.
- **Parser-level indentation:** Don't track indentation in parser grammar. Keep lexer→filter→parser separation clean.
- **Tab support:** Don't try to support tabs "sometimes" or convert tabs to spaces. Hard reject with clear error.
- **Implicit INDENT context:** Don't infer where INDENT should start based on token lookahead. Explicit NEWLINE emission keeps it predictable.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Error message formatting | Custom string interpolation | F# sprintf with structured data | Consistent format, easier testing, internationalization-ready |
| Indent stack management | List append/prepend | Persistent list (built-in) | Already optimized, immutable by default |
| Token stream processing | Mutable loop with accumulator | F# seq expression | Lazy evaluation, cleaner code, already in codebase |
| Configuration validation | Manual checks in multiple places | Single validation function called at config creation | Single source of truth, fails fast |

**Key insight:** The F# compiler's LexFilter.fs has solved these problems at scale. The token stream filtering approach handles Python, Haskell, F#, and YAML-style indentation without complex parser changes. Don't reinvent indentation handling when the pattern is proven.

## Common Pitfalls

### Pitfall 1: Match Pipe Indentation Confusion
**What goes wrong:** Treating match pipes like normal indent levels, pushing them onto indent stack.
**Why it happens:** Pipes look like they create a new block, but they're actually at the same level as `match`.
**How to avoid:** Don't push pipe column to indent stack. Track match base column in context instead.
**Warning signs:** Error "indentation doesn't match any level in stack" when writing second match pattern.

**Example:**
```fsharp
// WRONG - pipes create new indent levels
match x with
    | Some _ -> 1  // Pushes column 4 to stack
    | None -> 0    // Tries to match column 4, succeeds accidentally

// RIGHT - pipes align with "match" keyword at column 0
match x with
| Some _ -> 1
| None -> 0
```

### Pitfall 2: Function Application Ambiguity
**What goes wrong:** Parser can't distinguish between multi-line function application and multiple independent expressions.
**Why it happens:** Without explicit INDENT, "f\n  x\n  y" could be "f (x) (y)" or three separate expressions.
**How to avoid:** Require explicit INDENT token after function name when arguments span lines.
**Warning signs:** Parser shift/reduce conflicts in AppExpr grammar rules.

**Example:**
```fsharp
// Ambiguous without INDENT
someFunction
    arg1
    arg2

// Clear with INDENT/DEDENT tokens
someFunction INDENT
    arg1
    arg2
DEDENT
```

### Pitfall 3: EOF Without Closing Indents
**What goes wrong:** File ends mid-block without explicit DEDENT, leaving open indents.
**Why it happens:** User forgets to complete block, or copies partial code.
**How to avoid:** Always emit DEDENT tokens for all open indents before EOF (already implemented in filter).
**Warning signs:** Parser expects more input at EOF, "unexpected end of file" errors.

**Current implementation (CORRECT):**
```fsharp
| Parser.EOF ->
    // Emit DEDENTs for all open indents before EOF
    while state.IndentStack.Length > 1 do
        let (newState, _) = processNewline state 0
        state <- newState
        yield Parser.DEDENT
    yield Parser.EOF
```

### Pitfall 4: Poor Error Messages for Indentation Errors
**What goes wrong:** Generic "parse error" instead of clear "expected indent 4, found 2".
**Why it happens:** IndentationError exception doesn't provide enough context.
**How to avoid:** Include in error message: (1) line number, (2) expected indent levels (from stack), (3) actual indent level, (4) what was expected (continuation, new block, dedent).
**Warning signs:** User confusion, repeated indentation errors.

**Example of good error:**
```fsharp
exception IndentationError of line: int * message: string

// In processNewline when col doesn't match stack
raise (IndentationError(state.LineNum,
    $"Invalid indentation at line {state.LineNum}: found {col} spaces.
    Expected one of: {String.concat ", " (List.map string state.IndentStack)} (to match previous level)
    or {List.head state.IndentStack + config.IndentWidth} (to start new indented block)"))
```

### Pitfall 5: Tab/Space Mixing
**What goes wrong:** Some users mix tabs and spaces, causing invisible indentation errors.
**Why it happens:** Editor settings, copy-paste from different sources.
**How to avoid:** Reject tabs immediately in lexer with clear error "tabs not allowed, use spaces" (already implemented).
**Warning signs:** "Indentation error" on visually correct code.

**Current implementation (CORRECT):**
```fsharp
// In Lexer.fsl
| '\t'  { failwith "Tab character not allowed, use spaces" }
```

## Code Examples

Verified patterns from official sources:

### Match Expression Formatting (F# Standard)
```fsharp
// Source: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting
// Pipes align directly under "match", no indentation
match l with
| { him = x; her = "Posh" } :: tail -> x
| _ :: tail -> findDavid tail
| [] -> failwith "Couldn't find David"

// Multi-line pattern results indent one level from pipe
match lam with
| Var v -> 1
| Abs(x, body) ->
    1 + sizeLambda body
| App(lam1, lam2) ->
    sizeLambda lam1 + sizeLambda lam2
```

### Multi-Line Function Application (F# Standard)
```fsharp
// Source: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting
// Arguments on new lines indent one level from function
someFunction2
    x.IngredientName x.Quantity

someFunction4
    x.IngredientName1
    x.Quantity2
    x.IngredientName2
    x.Quantity2

// Each argument on own line when complex
someFunction5
    (convertVolumeToLiter x)
    (convertVolumeUSPint x)
    (convertVolumeImperialPint x)
```

### Indented Let Expressions (Already Implemented)
```fsharp
// Source: Current LangThree implementation
// Parser.fsy rule:
// | LET IDENT EQUALS INDENT Expr DEDENT

let x =
    42

let y =
    let z = 10
    z + 20
```

### Error Message Format (Best Practice)
```fsharp
// Source: F# compiler error conventions
exception IndentationError of line: int * message: string

// Good error message includes:
// 1. Line number
// 2. What was found
// 3. What was expected
// 4. Why it's wrong
raise (IndentationError(5,
    "Invalid indentation: column 2 doesn't match any level in stack [4; 0].
    Expected: 0 (to close block) or 4 (to continue) or 8 (to open new block)"))
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Parser-level indentation tracking (Haskell style) | Token stream filtering (F# LexFilter.fs) | F# 2.0+ (2010) | Cleaner separation, easier to maintain parser grammar |
| Manual indent stack in parser | Lexer emits NEWLINE, filter generates INDENT/DEDENT | Python 3.0 (2008) | Parser sees only structural tokens, not positions |
| Tab-to-spaces conversion | Hard reject tabs | F# 4.0+ (2015) | Prevents invisible errors, enforces consistency |
| Global indent width | Configurable IndentConfig | Modern practice (2020+) | Teams can choose 2/4/8 spaces, but consistently |

**Deprecated/outdated:**
- **Verbose syntax with explicit begin/end:** F# still supports it but light syntax (indentation) is standard since 2.0
- **Tab support:** Never standardized in ML family, Python removed in 3.0, F# never allowed
- **Parser combinators for indentation:** Possible (Parsec.Indent) but more complex than token filtering for simple offside rule

## Open Questions

Things that couldn't be fully resolved:

1. **How does F# compiler handle nested match expressions with different base columns?**
   - What we know: Each match creates its own alignment context, pipes align with their `match` keyword
   - What's unclear: Exact stack/context management when match expressions are nested (match inside match pattern result)
   - Recommendation: Test with nested matches, observe F# compiler behavior, implement same rules

2. **Should configurable indent width be strict or lenient by default?**
   - What we know: Default config already exists with `StrictWidth: bool` field set to `false`
   - What's unclear: User preference for enforcement level, performance impact of validation
   - Recommendation: Keep lenient (false) by default for backward compatibility, let users opt-in to strict

3. **How to handle comments and blank lines in indentation calculation?**
   - What we know: Lexer currently skips whitespace, comments don't emit tokens
   - What's unclear: Should blank lines between indented blocks affect indent stack?
   - Recommendation: Ignore blank lines and comments for indentation (F# behavior), they don't emit NEWLINE tokens

4. **Error recovery strategy when indentation is invalid?**
   - What we know: Currently throws IndentationError and stops parsing
   - What's unclear: Could we continue parsing to show multiple errors, or would that confuse AST?
   - Recommendation: Fail fast on first indentation error (current behavior), multiple errors in indentation are usually cascade failures

## Sources

### Primary (HIGH confidence)
- [F# Code Formatting Guidelines - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting) - Match expression formatting, function application rules, indentation conventions
- [F# Compiler Source - LexFilter.fs](https://github.com/dotnet/fsharp/blob/main/src/fsharp/LexFilter.fs) - Token stream filtering architecture (referenced but not directly accessed)
- Current LangThree implementation - IndentFilter.fs, Parser.fsy, Lexer.fsl (analyzed directly)

### Secondary (MEDIUM confidence)
- [F# syntax: indentation and verbosity | F# for fun and profit](https://fsharpforfunandprofit.com/posts/fsharp-syntax/) - Offside rule explanation
- [Match expressions | F# for fun and profit](https://fsharpforfunandprofit.com/posts/match-expression/) - Match expression patterns
- [FsLexYacc Documentation](https://github.com/fsprojects/FsLexYacc) - Lexer/parser generator usage

### Tertiary (LOW confidence)
- [Indentation-aware parsing patterns](https://markkarpov.com/megaparsec/indentation-sensitive-parsing.html) - General parser combinator approach (not F# specific)
- [Using FsLexYacc - Random coding knowledge](https://thanos.codes/blog/using-fslexyacc-the-fsharp-lexer-and-parser/) - Tutorial examples

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - FsLexYacc, Expecto are established tools, current implementation uses them successfully
- Architecture: HIGH - Token stream filtering proven in F# compiler, current implementation validates pattern
- Match expression patterns: MEDIUM - F# formatting guidelines clear, but LexFilter.fs implementation details not directly accessible
- Multi-line function application: MEDIUM - Formatting guidelines clear, grammar extension straightforward
- Error messages: MEDIUM - Best practices known, specific format needs testing with users
- Pitfalls: HIGH - Based on common issues in existing implementation and F# community knowledge

**Research date:** 2026-03-02
**Valid until:** 2026-04-02 (30 days - stable domain, F# formatting conventions don't change rapidly)

## Remaining Work Analysis

Based on success criteria from ROADMAP.md:

| Success Criterion | Status | What Remains |
|------------------|--------|--------------|
| 1. Let-binding continuation | ✅ Complete | None (PR #8) |
| 2. Match expression pattern alignment | ⏳ Not started | Context tracking, pipe alignment rules, parser grammar |
| 3. Multi-line function application | ⏳ Not started | INDENT in AppExpr, argument grouping |
| 4. Tab error message | ✅ Complete | None (Lexer.fsl line 33) |
| 5. Indentation error messages | ⏳ Partial | Improve IndentationError message format with expected/actual |

**Estimated remaining tasks:** 5-7 tasks
- Task: Add context tracking to IndentFilter (SyntaxContext type)
- Task: Implement match expression pipe alignment
- Task: Extend parser grammar for match with INDENT/DEDENT
- Task: Implement multi-line function application
- Task: Add configurable indent width validation
- Task: Improve error messages with expected vs actual
- Task: Integration tests for all remaining constructs
