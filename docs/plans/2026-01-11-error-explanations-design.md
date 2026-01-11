# Phase 8.5: Error Explanations Design

**Date:** 2026-01-11
**Status:** Approved

## Overview

Add detailed error explanations to FunLang, providing:
1. **Inline one-liner** - Brief explanation shown with every error
2. **CLI --explain flag** - Full documentation with examples

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Explanation style | Both inline + CLI | Quick context inline, deep docs when needed |
| Inline detail level | One-liner | Keeps output clean |
| --explain content | Full docs | Title, explanation, examples (bad+good), related errors |
| Storage | F# code | Simple, no file I/O, type-safe |

## Data Types

```fsharp
// ErrorExplanations.fs

/// Detailed explanation for an error code
type ErrorExplanation = {
    Code: string              // "E202"
    Title: string             // "Unbound variable"
    Brief: string             // One-liner for inline display
    Explanation: string       // Full explanation paragraph
    BadExample: string        // Code that causes this error
    GoodExample: string       // Fixed version
    RelatedCodes: string list // ["E201"; "E208"]
}

/// Registry of all error explanations
module ErrorExplanations =
    let private explanations: Map<string, ErrorExplanation> = ...

    /// Get brief one-liner for inline display
    let getBrief (code: string) : string option

    /// Get full explanation for --explain
    let get (code: string) : ErrorExplanation option

    /// List all available codes
    let allCodes () : string list
```

## Inline Integration

### Current Output
```
error[E202]: Unbound variable 'prnt'
  --> :1:1
  |
1 | prnt "hello"
  | ^^^^
   = help: did you mean `print`?
```

### New Output
```
error[E202]: Unbound variable 'prnt'
  --> :1:1
  |
1 | prnt "hello"
  | ^^^^
   = help: did you mean `print`?
   = info: variables must be defined with 'let' before use
```

### Implementation

Modify `ErrorFormatter.formatFooter`:

```fsharp
let formatFooter (diag: Diagnostic) : string =
    let notes = ...
    let helps = ...
    let suggestions = ...

    // NEW: Add brief explanation if available
    let info =
        diag.Code
        |> Option.bind ErrorExplanations.getBrief
        |> Option.map (sprintf "   = info: %s")
        |> Option.toList

    [ yield! notes; yield! helps; yield! suggestions; yield! info ]
    |> String.concat "\n"
```

## CLI --explain Flag

### Usage
```bash
funlang --explain E202         # Single code
funlang --explain E001,E202    # Multiple codes
funlang --explain all          # List all codes
```

### Output Format
```
Error E202: Unbound variable
============================

A variable was used before it was defined. In FunLang, all variables
must be introduced with 'let' before they can be referenced.

Example of incorrect code:
--------------------------
    x + 1
    ^ error: 'x' is not defined

How to fix:
-----------
    let x = 10
    x + 1

Related errors:
- E201: Type mismatch
- E208: Duplicate binding
```

### Implementation

**Options.fs:**
```fsharp
type CliArgs =
    // ... existing args
    | [<AltCommandLine("--explain")>] Explain of codes: string
```

**Program.fs:**
```fsharp
match results.TryGetResult Explain with
| Some codes ->
    handleExplain codes  // Print explanations and exit
    0
| None ->
    // Normal execution...
```

## Error Codes to Document

### Lexer Errors (E001-E099)
| Code | Title | Brief |
|------|-------|-------|
| E001 | Unexpected character | character not recognized by lexer |
| E002 | Unterminated string | string literal missing closing quote |
| E003 | Invalid escape sequence | unrecognized escape in string |
| E004 | Invalid number | malformed numeric literal |

### Parser Errors (E100-E199)
| Code | Title | Brief |
|------|-------|-------|
| E101 | Unexpected token | parser found token it didn't expect |
| E102 | Missing token | expected token not found |
| E104 | Indentation error | incorrect indentation level |
| E105 | Unclosed delimiter | missing closing bracket or paren |
| E106 | Empty block | block requires at least one expression |

### Type Errors (E200-E299)
| Code | Title | Brief |
|------|-------|-------|
| E201 | Type mismatch | expression type doesn't match expected type |
| E202 | Unbound variable | variable used before definition |
| E203 | Infinite type | type refers to itself (occurs check) |
| E204 | Not a function | cannot apply arguments to non-function |
| E205 | Arity mismatch | wrong number of arguments |
| E206 | Pattern type mismatch | pattern incompatible with matched value |
| E207 | Undefined constructor | unknown type constructor |
| E208 | Duplicate binding | variable already defined in scope |

### Runtime Errors (E300-E399)
| Code | Title | Brief |
|------|-------|-------|
| E301 | Division by zero | cannot divide by zero |
| E302 | Non-exhaustive match | no pattern matched the value |
| E303 | Invalid operation | operation not supported |
| E304 | Stack overflow | too many recursive calls |

## File Structure

### New Files
| File | Purpose |
|------|---------|
| `src/FunLang/ErrorExplanations.fs` | Type + all explanations data |
| `tests/FunLang.Tests/ErrorExplanationTests.fs` | Tests for explanations |

### Compilation Order (.fsproj)
```xml
<Compile Include="Diagnostic.fs" />
<Compile Include="ErrorExplanations.fs" />  <!-- NEW -->
<Compile Include="ErrorFormatter.fs" />
```

## Testing Strategy

### Property Tests
```fsharp
// All error codes have explanations
testProperty "all error codes have explanations" <| fun () ->
    let allCodes = ["E001"; "E002"; ...; "E302"]
    allCodes |> List.forall (fun code ->
        ErrorExplanations.get code |> Option.isSome)

// Briefs are concise (under 80 chars)
testProperty "briefs are concise" <| fun () ->
    ErrorExplanations.allCodes()
    |> List.forall (fun code ->
        let brief = ErrorExplanations.getBrief code |> Option.get
        String.length brief < 80)
```

### Unit Tests
```fsharp
// --explain output format
test "explain E202 shows full documentation" {
    let output = formatExplain "E202"
    Expect.stringContains output "Unbound variable" "should have title"
    Expect.stringContains output "let" "should mention let"
}

// Inline integration
test "error output includes brief explanation" {
    let diag = Diagnostic.error "E202" "Unbound variable 'x'"
    let output = ErrorFormatter.format "" defaultConfig diag
    Expect.stringContains output "= info:" "should have info line"
}
```

## Implementation Order

1. **Create ErrorExplanations.fs** with type and empty map
2. **Add tests** for explanations (TDD - RED)
3. **Populate explanations** for all error codes (GREEN)
4. **Integrate inline** into ErrorFormatter.formatFooter
5. **Add CLI --explain** option
6. **Update error test expectations** (new info line in output)

## Verification

```bash
# All tests pass
dotnet run --project tests/FunLang.Tests

# Inline explanation appears
echo "x + 1" | dotnet run --project src/FunLang
# Should show: = info: variables must be defined with 'let' before use

# --explain works
dotnet run --project src/FunLang -- --explain E202
# Should show full documentation
```
