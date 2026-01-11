module FunLang.Tests.LexerTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Parser
open FunLang.GeneratedParser

// =============================================================================
// Property-Based Tests (Required)
// =============================================================================

let propertyTests = testList "Lexer Properties" [
    testProperty "integer literals tokenize to INT token" <| fun (n: NonNegativeInt) ->
        let input = string n.Get
        match tokenize input with
        | Ok tokens ->
            tokens |> List.exists (function
                | INT v -> v = n.Get
                | _ -> false)
        | Error _ -> false

    testProperty "tokenize is deterministic" <| fun (input: NonEmptyString) ->
        let r1 = tokenize input.Get
        let r2 = tokenize input.Get
        r1 = r2

    testProperty "tokenize never crashes" <| fun (input: string) ->
        // Should always return Ok or Error, never throw
        let result = tokenize input
        match result with
        | Ok _ -> true
        | Error _ -> true
]

// =============================================================================
// Unit Tests (Edge cases)
// =============================================================================

let unitTests = testList "Lexer Unit Tests" [
    test "empty input returns EOF only" {
        let result = tokenize ""
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens -> Expect.equal tokens [EOF] "should be EOF only"
        | Error _ -> failtest "unexpected error"
    }

    test "single integer tokenizes correctly" {
        let result = tokenize "42"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 42; EOF] "should be INT and EOF"
        | Error _ -> failtest "unexpected error"
    }

    test "whitespace is ignored" {
        let result = tokenize "  42  "
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 42; EOF] "should be INT and EOF"
        | Error _ -> failtest "unexpected error"
    }

    test "multiple tokens" {
        let result = tokenize "1 + 2"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 1; PLUS; INT 2; EOF] "should include EOF"
        | Error _ -> failtest "unexpected error"
    }

    test "let expression tokenizes" {
        let result = tokenize "let x = 42 in x + 1"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [LET; IDENT "x"; EQ; INT 42; IN; IDENT "x"; PLUS; INT 1; EOF] "correct tokens"
        | Error _ -> failtest "unexpected error"
    }

    test "keywords are recognized" {
        let result = tokenize "let rec if then else fun match with"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [LET; REC; IF; THEN; ELSE; FUN; MATCH; WITH; EOF] "keywords recognized"
        | Error _ -> failtest "unexpected error"
    }

    test "operators tokenize" {
        let result = tokenize "+ - * / < > <= >= == !="
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [PLUS; MINUS; STAR; SLASH; LT; GT; LTE; GTE; EQ; NEQ; EOF] "operators recognized"
        | Error _ -> failtest "unexpected error"
    }

    test "arrow tokenizes" {
        let result = tokenize "fun x -> x"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [FUN; IDENT "x"; ARROW; IDENT "x"; EOF] "arrow recognized"
        | Error _ -> failtest "unexpected error"
    }

    test "string literals tokenize" {
        let result = tokenize "\"hello world\""
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [STRING "hello world"; EOF] "string recognized"
        | Error _ -> failtest "unexpected error"
    }

    test "booleans tokenize" {
        let result = tokenize "true false"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [TRUE; FALSE; EOF] "booleans recognized"
        | Error _ -> failtest "unexpected error"
    }
]

// =============================================================================
// Comment Tests
// =============================================================================

let commentTests = testList "Lexer Comment Tests" [
    test "single line comment is ignored" {
        let result = tokenize "// this is a comment"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [EOF] "comment should be ignored"
        | Error _ -> failtest "unexpected error"
    }

    test "comment after code is ignored" {
        let result = tokenize "42 // the answer"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 42; EOF] "only code before comment"
        | Error _ -> failtest "unexpected error"
    }

    test "code after comment on new line" {
        // Note: Leading NEWLINEs are filtered by tokenize for indentation handling
        let result = tokenize "// comment\n42"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 42; EOF] "code on next line works"
        | Error _ -> failtest "unexpected error"
    }

    test "multiple comments" {
        // Note: Leading NEWLINEs are filtered by tokenize for indentation handling
        let result = tokenize "// first\n// second\n1 + 2"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 1; PLUS; INT 2; EOF] "multiple comments work"
        | Error _ -> failtest "unexpected error"
    }

    test "comment with special characters" {
        let result = tokenize "// @#$%^&*() special chars!"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [EOF] "special chars in comment ok"
        | Error _ -> failtest "unexpected error"
    }

    test "empty comment" {
        let result = tokenize "//"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [EOF] "empty comment ok"
        | Error _ -> failtest "unexpected error"
    }

    test "comment in expression" {
        let result = tokenize "let x = 1 // define x\nin x + 1"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [LET; IDENT "x"; EQ; INT 1; NEWLINE; IN; IDENT "x"; PLUS; INT 1; EOF] "comment in middle"
        | Error _ -> failtest "unexpected error"
    }
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "Lexer" [
    propertyTests
    unitTests
    commentTests
]
