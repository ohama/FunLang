# Phase 1: Indentation-Based Syntax Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement F#-style indentation-based syntax for LangThree using a token stream filter.

**Architecture:** Lexer emits NEWLINE tokens with column info → IndentFilter transforms to INDENT/DEDENT → Parser consumes filtered stream. This mirrors F# compiler's LexFilter.fs approach.

**Tech Stack:** F# (.NET), FsLexYacc, Expecto (tests)

---

## Task 1: Copy FunLang Source to LangThree

**Files:**
- Create: `src/LangThree/` directory structure
- Copy from: `/home/shoh/vibe-coding/LangTutorial/FunLang/`

**Step 1: Create project structure**

```bash
mkdir -p src/LangThree
```

**Step 2: Copy FunLang source files**

```bash
cp /home/shoh/vibe-coding/LangTutorial/FunLang/*.fs src/LangThree/
cp /home/shoh/vibe-coding/LangTutorial/FunLang/*.fsl src/LangThree/
cp /home/shoh/vibe-coding/LangTutorial/FunLang/*.fsy src/LangThree/
cp /home/shoh/vibe-coding/LangTutorial/FunLang/*.fsproj src/LangThree/
```

**Step 3: Rename project file**

```bash
mv src/LangThree/FunLang.fsproj src/LangThree/LangThree.fsproj
```

**Step 4: Update project file namespace**

Edit `src/LangThree/LangThree.fsproj`:
- Change `<RootNamespace>FunLang</RootNamespace>` to `<RootNamespace>LangThree</RootNamespace>`
- Change `<AssemblyName>FunLang</AssemblyName>` to `<AssemblyName>LangThree</AssemblyName>`

**Step 5: Verify build**

```bash
cd src/LangThree && dotnet build
```

Expected: Build succeeds

**Step 6: Commit**

```bash
git add src/
git commit -m "chore: copy FunLang source as LangThree base"
```

---

## Task 2: Add Token Types for Indentation

**Files:**
- Modify: `src/LangThree/Parser.fsy` (token declarations)

**Step 1: Add NEWLINE, INDENT, DEDENT tokens to Parser.fsy**

Find the token declarations section (around line 23-45) and add:

```fsharp
// Indentation tokens
%token <int> NEWLINE   // NEWLINE with column position
%token INDENT          // Indentation increase
%token DEDENT          // Indentation decrease
```

**Step 2: Regenerate parser (if using FsLexYacc build)**

```bash
cd src/LangThree && dotnet build
```

Expected: Build succeeds (tokens added but not yet used)

**Step 3: Commit**

```bash
git add src/LangThree/Parser.fsy
git commit -m "feat(parser): add NEWLINE, INDENT, DEDENT token declarations"
```

---

## Task 3: Create IndentFilter Module - Types and Config

**Files:**
- Create: `src/LangThree/IndentFilter.fs`
- Modify: `src/LangThree/LangThree.fsproj` (add to compilation)

**Step 1: Create IndentFilter.fs with types**

Create `src/LangThree/IndentFilter.fs`:

```fsharp
module LangThree.IndentFilter

open Parser

/// Configuration for indent processing
type IndentConfig = {
    IndentWidth: int  // Expected indent width (2, 4, or 8)
    StrictWidth: bool // If true, enforce exact multiples of IndentWidth
}

/// Default configuration: 4 spaces, not strict
let defaultConfig = { IndentWidth = 4; StrictWidth = false }

/// State maintained during filtering
type FilterState = {
    IndentStack: int list  // Stack of indent levels, starts with [0]
    LineNum: int           // Current line number for errors
}

/// Initial state
let initialState = { IndentStack = [0]; LineNum = 1 }

/// Error for indentation problems
exception IndentationError of line: int * message: string
```

**Step 2: Add IndentFilter.fs to project file**

Edit `src/LangThree/LangThree.fsproj`, add before `Lexer.fsl`:

```xml
<Compile Include="IndentFilter.fs" />
```

**Step 3: Verify build**

```bash
cd src/LangThree && dotnet build
```

Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/LangThree/IndentFilter.fs src/LangThree/LangThree.fsproj
git commit -m "feat(indent): add IndentFilter module with types"
```

---

## Task 4: Create Test Project

**Files:**
- Create: `tests/LangThree.Tests/LangThree.Tests.fsproj`
- Create: `tests/LangThree.Tests/IndentFilterTests.fs`

**Step 1: Create test project directory**

```bash
mkdir -p tests/LangThree.Tests
```

**Step 2: Create test project file**

Create `tests/LangThree.Tests/LangThree.Tests.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="IndentFilterTests.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Expecto" Version="10.*" />
    <PackageReference Include="YoloDev.Expecto.TestSdk" Version="0.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/LangThree/LangThree.fsproj" />
  </ItemGroup>
</Project>
```

**Step 3: Create test entry point**

Create `tests/LangThree.Tests/Program.fs`:

```fsharp
module LangThree.Tests.Program

open Expecto

[<EntryPoint>]
let main args =
    runTestsInAssemblyWithCLIArgs [] args
```

**Step 4: Create initial IndentFilter tests**

Create `tests/LangThree.Tests/IndentFilterTests.fs`:

```fsharp
module LangThree.Tests.IndentFilterTests

open Expecto
open LangThree.IndentFilter

[<Tests>]
let configTests = testList "IndentFilter.Config" [
    test "defaultConfig has IndentWidth 4" {
        Expect.equal defaultConfig.IndentWidth 4 "Default indent should be 4"
    }

    test "initialState starts with stack [0]" {
        Expect.equal initialState.IndentStack [0] "Initial stack should be [0]"
    }
]
```

**Step 5: Run tests**

```bash
cd tests/LangThree.Tests && dotnet test
```

Expected: 2 tests pass

**Step 6: Commit**

```bash
git add tests/
git commit -m "test: add LangThree.Tests project with IndentFilter tests"
```

---

## Task 5: Implement processNewline Function (TDD)

**Files:**
- Modify: `src/LangThree/IndentFilter.fs`
- Modify: `tests/LangThree.Tests/IndentFilterTests.fs`

**Step 1: Write failing tests for processNewline**

Add to `tests/LangThree.Tests/IndentFilterTests.fs`:

```fsharp
[<Tests>]
let processNewlineTests = testList "IndentFilter.processNewline" [
    test "same indent level emits no tokens" {
        let state = { IndentStack = [0]; LineNum = 1 }
        let (newState, tokens) = processNewline state 0
        Expect.equal tokens [] "Same level should emit no tokens"
        Expect.equal newState.IndentStack [0] "Stack unchanged"
    }

    test "deeper indent emits INDENT" {
        let state = { IndentStack = [0]; LineNum = 1 }
        let (newState, tokens) = processNewline state 4
        Expect.equal tokens [INDENT] "Should emit INDENT"
        Expect.equal newState.IndentStack [4; 0] "Stack should push 4"
    }

    test "shallower indent emits DEDENT" {
        let state = { IndentStack = [4; 0]; LineNum = 1 }
        let (newState, tokens) = processNewline state 0
        Expect.equal tokens [DEDENT] "Should emit DEDENT"
        Expect.equal newState.IndentStack [0] "Stack should pop to [0]"
    }

    test "multiple dedents for big unindent" {
        let state = { IndentStack = [8; 4; 0]; LineNum = 1 }
        let (newState, tokens) = processNewline state 0
        Expect.equal tokens [DEDENT; DEDENT] "Should emit 2 DEDENTs"
        Expect.equal newState.IndentStack [0] "Stack should be [0]"
    }

    test "invalid indent throws error" {
        let state = { IndentStack = [4; 0]; LineNum = 5 }
        Expect.throws
            (fun () -> processNewline state 2 |> ignore)
            "Should throw for misaligned indent"
    }
]
```

**Step 2: Run tests to verify they fail**

```bash
cd tests/LangThree.Tests && dotnet test
```

Expected: Tests fail (processNewline not implemented)

**Step 3: Implement processNewline**

Add to `src/LangThree/IndentFilter.fs`:

```fsharp
/// Process a NEWLINE token and generate INDENT/DEDENT as needed
let processNewline (state: FilterState) (col: int) : FilterState * Parser.token list =
    let rec unwind acc stack =
        match stack with
        | [] ->
            raise (IndentationError(state.LineNum, "Internal error: empty indent stack"))
        | top :: rest when col < top ->
            // Dedent: pop and emit DEDENT
            unwind (Parser.DEDENT :: acc) rest
        | top :: _ when col = top ->
            // Same level: no tokens
            (List.rev acc, stack)
        | top :: _ when col > top ->
            // Indent: push and emit INDENT
            ([Parser.INDENT], col :: stack)
        | _ ->
            raise (IndentationError(state.LineNum,
                $"Invalid indentation: column {col} doesn't match any level in stack"))

    let (tokens, newStack) = unwind [] state.IndentStack
    ({ state with IndentStack = newStack }, tokens)
```

**Step 4: Run tests to verify they pass**

```bash
cd tests/LangThree.Tests && dotnet test
```

Expected: All tests pass

**Step 5: Commit**

```bash
git add src/LangThree/IndentFilter.fs tests/LangThree.Tests/IndentFilterTests.fs
git commit -m "feat(indent): implement processNewline with INDENT/DEDENT generation"
```

---

## Task 6: Implement filter Function (TDD)

**Files:**
- Modify: `src/LangThree/IndentFilter.fs`
- Modify: `tests/LangThree.Tests/IndentFilterTests.fs`

**Step 1: Write failing tests for filter**

Add to `tests/LangThree.Tests/IndentFilterTests.fs`:

```fsharp
[<Tests>]
let filterTests = testList "IndentFilter.filter" [
    test "passes through non-NEWLINE tokens" {
        let input = [Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.NUMBER 1]
        let output = filter defaultConfig input |> Seq.toList
        Expect.equal output input "Should pass through unchanged"
    }

    test "converts NEWLINE to INDENT on deeper" {
        let input = [Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.NEWLINE 4; Parser.NUMBER 1]
        let output = filter defaultConfig input |> Seq.toList
        let expected = [Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.INDENT; Parser.NUMBER 1]
        Expect.equal output expected "NEWLINE(4) should become INDENT"
    }

    test "emits DEDENT at end for open indents" {
        let input = [Parser.LET; Parser.NEWLINE 4; Parser.NUMBER 1; Parser.EOF]
        let output = filter defaultConfig input |> Seq.toList
        let expected = [Parser.LET; Parser.INDENT; Parser.NUMBER 1; Parser.DEDENT; Parser.EOF]
        Expect.equal output expected "Should emit DEDENT before EOF"
    }
]
```

**Step 2: Run tests to verify they fail**

```bash
cd tests/LangThree.Tests && dotnet test
```

Expected: Tests fail (filter not implemented)

**Step 3: Implement filter function**

Add to `src/LangThree/IndentFilter.fs`:

```fsharp
/// Filter a token stream, converting NEWLINE(col) to INDENT/DEDENT
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

**Step 4: Run tests to verify they pass**

```bash
cd tests/LangThree.Tests && dotnet test
```

Expected: All tests pass

**Step 5: Commit**

```bash
git add src/LangThree/IndentFilter.fs tests/LangThree.Tests/IndentFilterTests.fs
git commit -m "feat(indent): implement filter function for token stream"
```

---

## Task 7: Modify Lexer for NEWLINE Token

**Files:**
- Modify: `src/LangThree/Lexer.fsl`

**Step 1: Add helper function for getting next line indent**

Add to the header section of `src/LangThree/Lexer.fsl`:

```fsharp
/// Count leading spaces on the rest of the input
let getNextLineIndent (lexbuf: LexBuffer<_>) =
    let mutable col = 0
    let mutable i = lexbuf.BufferScanStart
    while i < lexbuf.BufferScanLength && lexbuf.Buffer.[i] = ' ' do
        col <- col + 1
        i <- i + 1
    col
```

**Step 2: Modify whitespace rules**

Replace the whitespace and newline rules:

```fsharp
// Before:
// | whitespace+   { tokenize lexbuf }
// | newline       { lexbuf.EndPos <- lexbuf.EndPos.NextLine
//                   tokenize lexbuf }

// After:
| ' '+          { tokenize lexbuf }                    // Spaces: skip
| '\t'          { failwith "Tab character not allowed, use spaces" }
| newline       { lexbuf.EndPos <- lexbuf.EndPos.NextLine
                  NEWLINE (getNextLineIndent lexbuf) }
```

**Step 3: Regenerate lexer and build**

```bash
cd src/LangThree && dotnet build
```

Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/LangThree/Lexer.fsl
git commit -m "feat(lexer): emit NEWLINE token with column, reject tabs"
```

---

## Task 8: Modify Parser Grammar for Indentation

**Files:**
- Modify: `src/LangThree/Parser.fsy`

**Step 1: Modify let expression rule**

Find and update the let expression rules (around line 71-77):

```fsharp
// Before:
// | LET IDENT EQUALS Expr IN Expr  { Let($2, $4, $6, ruleSpan parseState 1 6) }

// After: Support both indented and inline let
    | LET IDENT EQUALS INDENT Expr DEDENT  { Let($2, $5, Var("()", symSpan parseState 6), ruleSpan parseState 1 6) }
    | LET IDENT EQUALS Expr                 { Let($2, $4, Var("()", symSpan parseState 4), ruleSpan parseState 1 4) }
```

Note: We use `Var("()")` as a placeholder for the body since we're changing from `let x = e1 in e2` to `let x = e1` block style. This will need refinement for proper sequencing.

**Step 2: Build and verify**

```bash
cd src/LangThree && dotnet build
```

Expected: Build succeeds (may have shift/reduce warnings - OK for now)

**Step 3: Commit**

```bash
git add src/LangThree/Parser.fsy
git commit -m "feat(parser): add INDENT/DEDENT support for let expressions"
```

---

## Task 9: Integration Test - Simple Let Expression

**Files:**
- Create: `tests/LangThree.Tests/IntegrationTests.fs`
- Modify: `tests/LangThree.Tests/LangThree.Tests.fsproj`

**Step 1: Add IntegrationTests.fs to project**

Edit `tests/LangThree.Tests/LangThree.Tests.fsproj`, add:

```xml
<Compile Include="IntegrationTests.fs" />
```

**Step 2: Create integration test**

Create `tests/LangThree.Tests/IntegrationTests.fs`:

```fsharp
module LangThree.Tests.IntegrationTests

open Expecto
open LangThree.IndentFilter

// Helper to parse a string through the full pipeline
let parseString (input: string) =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let rawTokens =
        Seq.unfold (fun () ->
            let tok = Lexer.tokenize lexbuf
            if tok = Parser.EOF then None
            else Some(tok, ())
        ) ()
        |> Seq.append [Parser.EOF]
    let filteredTokens = filter defaultConfig rawTokens
    // For now, just return token list for inspection
    filteredTokens |> Seq.toList

[<Tests>]
let integrationTests = testList "Integration" [
    test "simple let with indent parses" {
        let input = "let x =\n    42"
        let tokens = parseString input
        Expect.contains tokens Parser.INDENT "Should have INDENT"
        Expect.contains tokens (Parser.NUMBER 42) "Should have NUMBER 42"
    }

    test "tab character raises error" {
        Expect.throws
            (fun () -> parseString "let x =\n\t42" |> ignore)
            "Tab should raise error"
    }
]
```

**Step 3: Run integration tests**

```bash
cd tests/LangThree.Tests && dotnet test
```

Expected: Tests pass

**Step 4: Commit**

```bash
git add tests/LangThree.Tests/
git commit -m "test: add integration tests for indentation parsing"
```

---

## Task 10: Final Verification and Cleanup

**Files:**
- All modified files

**Step 1: Run all tests**

```bash
dotnet test
```

Expected: All tests pass

**Step 2: Verify success criteria**

| Criterion | Status |
|-----------|--------|
| let-binding continuation | ✅ Task 9 test |
| Tab → clear error | ✅ Task 9 test |
| INDENT/DEDENT generation | ✅ Task 5, 6 tests |

**Step 3: Update ROADMAP.md progress**

Mark Phase 1 as "In Progress" with initial tasks complete.

**Step 4: Final commit**

```bash
git add .
git commit -m "feat(phase1): complete initial indentation syntax implementation

- IndentFilter module with processNewline and filter
- Lexer emits NEWLINE(col), rejects tabs
- Parser accepts INDENT/DEDENT for let expressions
- Unit and integration tests passing"
```

---

## Summary

| Task | Description | Files |
|------|-------------|-------|
| 1 | Copy FunLang source | `src/LangThree/*` |
| 2 | Add token types | `Parser.fsy` |
| 3 | Create IndentFilter types | `IndentFilter.fs` |
| 4 | Create test project | `tests/LangThree.Tests/*` |
| 5 | Implement processNewline | `IndentFilter.fs` |
| 6 | Implement filter | `IndentFilter.fs` |
| 7 | Modify Lexer | `Lexer.fsl` |
| 8 | Modify Parser | `Parser.fsy` |
| 9 | Integration tests | `IntegrationTests.fs` |
| 10 | Verify and cleanup | All |

**Total estimated tasks:** 10
**Commits:** ~10 atomic commits
