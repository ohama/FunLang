module FunLang.Tests.IndentationTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Indentation
open FunLang.GeneratedParser

// =============================================================================
// Helper Functions
// =============================================================================

/// Create a TokenWithPos from token and column (line defaults to 1)
let tok t col = (t, { Line = 1; Column = col; File = None })

/// Create a TokenWithPos with specific line and column
let tokAt t line col = (t, { Line = line; Column = col; File = None })

/// Check if token list contains INDENT
let hasIndent tokens =
    tokens |> List.exists (function INDENT -> true | _ -> false)

/// Check if token list contains DEDENT
let hasDedent tokens =
    tokens |> List.exists (function DEDENT -> true | _ -> false)

/// Check if token list contains NEWLINE
let hasNewline tokens =
    tokens |> List.exists (function NEWLINE -> true | _ -> false)

/// Count INDENT tokens
let countIndents tokens =
    tokens |> List.filter (function INDENT -> true | _ -> false) |> List.length

/// Count DEDENT tokens
let countDedents tokens =
    tokens |> List.filter (function DEDENT -> true | _ -> false) |> List.length

// =============================================================================
// Property-Based Tests
// =============================================================================

let propertyTests = testList "Indentation Properties" [
    testProperty "INDENT count equals DEDENT count" <| fun (depths: NonNegativeInt list) ->
        // Create a sequence of tokens at varying depths
        // Each depth should eventually be closed
        let depths' = depths |> List.map (fun d -> d.Get % 10)  // Limit depth
        let tokens =
            depths'
            |> List.mapi (fun i d -> tokAt (INT i) (i + 1) (d * 4 + 1))
            |> fun ts -> ts @ [tokAt EOF (List.length depths' + 1) 1]

        match processIndentation tokens with
        | Ok result ->
            countIndents result = countDedents result
        | Error _ -> true  // Errors are acceptable for invalid inputs

    testProperty "processIndentation is deterministic" <| fun (n: NonNegativeInt) ->
        let tokens = [
            tokAt (INT n.Get) 1 1
            tokAt EOF 2 1
        ]
        let r1 = processIndentation tokens
        let r2 = processIndentation tokens
        r1 = r2

    testProperty "empty parens ignore indentation" <| fun (n: NonNegativeInt) ->
        // Inside parentheses, indentation should be ignored
        let tokens = [
            tokAt LPAREN 1 1
            tokAt NEWLINE 1 2
            tokAt (INT n.Get) 2 5      // Indented inside parens
            tokAt NEWLINE 2 6
            tokAt (INT (n.Get + 1)) 3 9  // Even more indented
            tokAt RPAREN 3 10
            tokAt EOF 4 1
        ]
        match processIndentation tokens with
        | Ok result ->
            // Should not have INDENT/DEDENT inside parens
            not (hasIndent result) && not (hasDedent result)
        | Error _ -> false
]

// =============================================================================
// Unit Tests - INDENT Generation
// =============================================================================

let indentTests = testList "INDENT Generation" [
    test "INDENT generated on increased indentation" {
        // let x =
        //     10
        let tokens = [
            tokAt LET 1 1
            tokAt (IDENT "x") 1 5
            tokAt EQ 1 7
            tokAt NEWLINE 1 8
            tokAt (INT 10) 2 5         // Column 5 > previous 1
            tokAt EOF 3 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.isTrue (hasIndent ts) "should have INDENT token"
        | Error _ -> failtest "unexpected error"
    }

    test "multiple INDENTs for nested blocks" {
        // let x =
        //     let y =
        //         10
        let tokens = [
            tokAt LET 1 1
            tokAt (IDENT "x") 1 5
            tokAt EQ 1 7
            tokAt NEWLINE 1 8
            tokAt LET 2 5              // First indent
            tokAt (IDENT "y") 2 9
            tokAt EQ 2 11
            tokAt NEWLINE 2 12
            tokAt (INT 10) 3 9         // Second indent
            tokAt EOF 4 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.equal (countIndents ts) 2 "should have 2 INDENT tokens"
        | Error _ -> failtest "unexpected error"
    }
]

// =============================================================================
// Unit Tests - DEDENT Generation
// =============================================================================

let dedentTests = testList "DEDENT Generation" [
    test "DEDENT generated on decreased indentation" {
        // let x =
        //     10
        // y
        let tokens = [
            tokAt LET 1 1
            tokAt (IDENT "x") 1 5
            tokAt EQ 1 7
            tokAt NEWLINE 1 8
            tokAt (INT 10) 2 5         // Indent to column 5
            tokAt NEWLINE 2 7
            tokAt (IDENT "y") 3 1      // Back to column 1
            tokAt EOF 4 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.isTrue (hasDedent ts) "should have DEDENT token"
        | Error _ -> failtest "unexpected error"
    }

    test "multiple DEDENTs when closing multiple levels" {
        // let x =
        //     let y =
        //         10
        // z
        let tokens = [
            tokAt LET 1 1
            tokAt (IDENT "x") 1 5
            tokAt EQ 1 7
            tokAt NEWLINE 1 8
            tokAt LET 2 5              // First indent
            tokAt (IDENT "y") 2 9
            tokAt EQ 2 11
            tokAt NEWLINE 2 12
            tokAt (INT 10) 3 9         // Second indent
            tokAt NEWLINE 3 11
            tokAt (IDENT "z") 4 1      // Back to column 1 (close both)
            tokAt EOF 5 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.equal (countDedents ts) 2 "should have 2 DEDENT tokens"
        | Error _ -> failtest "unexpected error"
    }

    test "DEDENT at EOF for unclosed indentation" {
        // let x =
        //     10
        let tokens = [
            tokAt LET 1 1
            tokAt (IDENT "x") 1 5
            tokAt EQ 1 7
            tokAt NEWLINE 1 8
            tokAt (INT 10) 2 5         // Indent
            tokAt EOF 3 1              // EOF should trigger DEDENT
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.isTrue (hasDedent ts) "should have DEDENT before EOF"
        | Error _ -> failtest "unexpected error"
    }
]

// =============================================================================
// Unit Tests - NEWLINE as Statement Separator
// =============================================================================

let newlineTests = testList "NEWLINE as Statement Separator" [
    test "NEWLINE emitted for same-level statements" {
        // let x =
        //     10
        //     20
        let tokens = [
            tokAt LET 1 1
            tokAt (IDENT "x") 1 5
            tokAt EQ 1 7
            tokAt NEWLINE 1 8
            tokAt (INT 10) 2 5         // First in block
            tokAt NEWLINE 2 7
            tokAt (INT 20) 3 5         // Same level - should emit NEWLINE
            tokAt EOF 4 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.isTrue (hasNewline ts) "should have NEWLINE as separator"
        | Error _ -> failtest "unexpected error"
    }
]

// =============================================================================
// Unit Tests - Parentheses Handling
// =============================================================================

let parenTests = testList "Parentheses Handling" [
    test "no indent tokens inside parentheses" {
        // (
        //     10
        // )
        let tokens = [
            tokAt LPAREN 1 1
            tokAt NEWLINE 1 2
            tokAt (INT 10) 2 5         // Indented inside parens
            tokAt NEWLINE 2 7
            tokAt RPAREN 3 1
            tokAt EOF 4 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.isFalse (hasIndent ts) "should not have INDENT inside parens"
            Expect.isFalse (hasDedent ts) "should not have DEDENT inside parens"
        | Error _ -> failtest "unexpected error"
    }

    test "no indent tokens inside brackets" {
        // [
        //     1;
        //     2
        // ]
        let tokens = [
            tokAt LBRACKET 1 1
            tokAt NEWLINE 1 2
            tokAt (INT 1) 2 5
            tokAt SEMICOLON 2 6
            tokAt NEWLINE 2 7
            tokAt (INT 2) 3 5
            tokAt NEWLINE 3 6
            tokAt RBRACKET 4 1
            tokAt EOF 5 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.isFalse (hasIndent ts) "should not have INDENT inside brackets"
            Expect.isFalse (hasDedent ts) "should not have DEDENT inside brackets"
        | Error _ -> failtest "unexpected error"
    }

    test "nested parens and brackets" {
        let tokens = [
            tokAt LPAREN 1 1
            tokAt LBRACKET 1 2
            tokAt NEWLINE 1 3
            tokAt (INT 1) 2 10
            tokAt RBRACKET 2 11
            tokAt RPAREN 2 12
            tokAt EOF 3 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.isFalse (hasIndent ts) "should not have INDENT"
            Expect.isFalse (hasDedent ts) "should not have DEDENT"
        | Error _ -> failtest "unexpected error"
    }
]

// =============================================================================
// Unit Tests - Error Cases
// =============================================================================

let errorTests = testList "Indentation Errors" [
    test "inconsistent dedent causes error" {
        // let x =
        //     10
        //   y    <- dedent to column 3, but stack has [1, 5]
        let tokens = [
            tokAt LET 1 1
            tokAt (IDENT "x") 1 5
            tokAt EQ 1 7
            tokAt NEWLINE 1 8
            tokAt (INT 10) 2 5         // Indent to 5
            tokAt NEWLINE 2 7
            tokAt (IDENT "y") 3 3      // Column 3 doesn't match any level
            tokAt EOF 4 1
        ]

        let result = processIndentation tokens
        Expect.isError result "should fail on inconsistent dedent"
    }
]

// =============================================================================
// Unit Tests - Single Line (No Indentation)
// =============================================================================

let singleLineTests = testList "Single Line Expressions" [
    test "single line expression has no indent tokens" {
        let tokens = [
            tokAt LET 1 1
            tokAt (IDENT "x") 1 5
            tokAt EQ 1 7
            tokAt (INT 42) 1 9
            tokAt IN 1 12
            tokAt (IDENT "x") 1 15
            tokAt EOF 2 1
        ]

        let result = processIndentation tokens
        Expect.isOk result "should succeed"
        match result with
        | Ok ts ->
            Expect.isFalse (hasIndent ts) "should not have INDENT"
            Expect.isFalse (hasDedent ts) "should not have DEDENT"
        | Error _ -> failtest "unexpected error"
    }
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "Indentation" [
    propertyTests
    indentTests
    dedentTests
    newlineTests
    parenTests
    errorTests
    singleLineTests
]
