module FunLang.Tests.LexerTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Lexer

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
    test "empty input returns empty token list" {
        let result = tokenize ""
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens -> Expect.equal tokens [] "should be empty"
        | Error _ -> failtest "unexpected error"
    }

    test "single integer tokenizes correctly" {
        let result = tokenize "42"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 42] "should be single INT token"
        | Error _ -> failtest "unexpected error"
    }

    test "whitespace is ignored" {
        let result = tokenize "  42  "
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 42] "should be single INT token"
        | Error _ -> failtest "unexpected error"
    }

    test "multiple tokens" {
        let result = tokenize "1 + 2"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [INT 1; PLUS; INT 2] "should be three tokens"
        | Error _ -> failtest "unexpected error"
    }

    test "let expression tokenizes" {
        let result = tokenize "let x = 42 in x + 1"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [LET; IDENT "x"; EQ; INT 42; IN; IDENT "x"; PLUS; INT 1] "correct tokens"
        | Error _ -> failtest "unexpected error"
    }

    test "keywords are recognized" {
        let result = tokenize "let rec if then else fun match with"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [LET; REC; IF; THEN; ELSE; FUN; MATCH; WITH] "keywords recognized"
        | Error _ -> failtest "unexpected error"
    }

    test "operators tokenize" {
        let result = tokenize "+ - * / < > <= >= == !="
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [PLUS; MINUS; STAR; SLASH; LT; GT; LTE; GTE; EQ; NEQ] "operators recognized"
        | Error _ -> failtest "unexpected error"
    }

    test "arrow tokenizes" {
        let result = tokenize "fun x -> x"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [FUN; IDENT "x"; ARROW; IDENT "x"] "arrow recognized"
        | Error _ -> failtest "unexpected error"
    }

    test "string literals tokenize" {
        let result = tokenize "\"hello world\""
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [STRING "hello world"] "string recognized"
        | Error _ -> failtest "unexpected error"
    }

    test "booleans tokenize" {
        let result = tokenize "true false"
        Expect.isOk result "should succeed"
        match result with
        | Ok tokens ->
            Expect.equal tokens [BOOL true; BOOL false] "booleans recognized"
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
]
