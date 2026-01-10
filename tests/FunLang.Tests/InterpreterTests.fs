module FunLang.Tests.InterpreterTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Lexer
open FunLang.Parser
open FunLang.Interpreter

// =============================================================================
// Helper Functions
// =============================================================================

let run input =
    match tokenize input with
    | Error e -> Error (sprintf "Lexer error: %s" e.Message)
    | Ok tokens ->
        match parse tokens with
        | Error e -> Error (sprintf "Parser error: %s" e)
        | Ok ast ->
            match eval Map.empty ast with
            | Error e -> Error (sprintf "Runtime error: %s" e.Message)
            | Ok v -> Ok v

let expectValue expected input =
    match run input with
    | Ok v -> Expect.equal v expected (sprintf "Expected %A" expected)
    | Error e -> failtest e

// =============================================================================
// Property-Based Tests
// =============================================================================

let propertyTests = testList "Interpreter Properties" [
    testProperty "integer literals evaluate to themselves" <| fun (n: int) ->
        match run (string n) with
        | Ok (VInt v) -> v = n
        | _ -> n < 0  // negative numbers need unary minus

    testProperty "addition is commutative" <| fun (a: NonNegativeInt) (b: NonNegativeInt) ->
        let r1 = run (sprintf "%d + %d" a.Get b.Get)
        let r2 = run (sprintf "%d + %d" b.Get a.Get)
        r1 = r2

    testProperty "addition is associative" <| fun (a: NonNegativeInt) (b: NonNegativeInt) (c: NonNegativeInt) ->
        let r1 = run (sprintf "(%d + %d) + %d" a.Get b.Get c.Get)
        let r2 = run (sprintf "%d + (%d + %d)" a.Get b.Get c.Get)
        r1 = r2

    testProperty "multiplication is commutative" <| fun (a: NonNegativeInt) (b: NonNegativeInt) ->
        let r1 = run (sprintf "%d * %d" a.Get b.Get)
        let r2 = run (sprintf "%d * %d" b.Get a.Get)
        r1 = r2

    testProperty "multiplication distributes over addition" <| fun (a: NonNegativeInt) (b: NonNegativeInt) (c: NonNegativeInt) ->
        let r1 = run (sprintf "%d * (%d + %d)" a.Get b.Get c.Get)
        let r2 = run (sprintf "%d * %d + %d * %d" a.Get b.Get a.Get c.Get)
        r1 = r2

    testProperty "let binding substitutes correctly" <| fun (n: NonNegativeInt) ->
        let code = sprintf "let x = %d in x" n.Get
        match run code with
        | Ok (VInt v) -> v = n.Get
        | _ -> false

    testProperty "if true returns then branch" <| fun (a: NonNegativeInt) (b: NonNegativeInt) ->
        let code = sprintf "if true then %d else %d" a.Get b.Get
        match run code with
        | Ok (VInt v) -> v = a.Get
        | _ -> false

    testProperty "if false returns else branch" <| fun (a: NonNegativeInt) (b: NonNegativeInt) ->
        let code = sprintf "if false then %d else %d" a.Get b.Get
        match run code with
        | Ok (VInt v) -> v = b.Get
        | _ -> false
]

// =============================================================================
// Unit Tests - Literals
// =============================================================================

let literalTests = testList "Literals" [
    test "evaluate integer" {
        expectValue (VInt 42) "42"
    }

    test "evaluate boolean true" {
        expectValue (VBool true) "true"
    }

    test "evaluate boolean false" {
        expectValue (VBool false) "false"
    }

    test "evaluate string" {
        expectValue (VString "hello") "\"hello\""
    }
]

// =============================================================================
// Unit Tests - Arithmetic
// =============================================================================

let arithmeticTests = testList "Arithmetic" [
    test "addition" {
        expectValue (VInt 3) "1 + 2"
    }

    test "subtraction" {
        expectValue (VInt 2) "5 - 3"
    }

    test "multiplication" {
        expectValue (VInt 6) "2 * 3"
    }

    test "division" {
        expectValue (VInt 5) "10 / 2"
    }

    test "modulo" {
        expectValue (VInt 1) "10 % 3"
    }

    test "complex expression" {
        expectValue (VInt 7) "1 + 2 * 3"
    }

    test "parentheses" {
        expectValue (VInt 9) "(1 + 2) * 3"
    }

    test "unary minus" {
        expectValue (VInt (-5)) "-5"
    }

    test "negative result" {
        expectValue (VInt (-3)) "2 - 5"
    }
]

// =============================================================================
// Unit Tests - Comparison
// =============================================================================

let comparisonTests = testList "Comparison" [
    test "less than true" {
        expectValue (VBool true) "1 < 2"
    }

    test "less than false" {
        expectValue (VBool false) "2 < 1"
    }

    test "greater than true" {
        expectValue (VBool true) "2 > 1"
    }

    test "greater than false" {
        expectValue (VBool false) "1 > 2"
    }

    test "equality true" {
        expectValue (VBool true) "5 == 5"
    }

    test "equality false" {
        expectValue (VBool false) "5 == 6"
    }

    test "inequality true" {
        expectValue (VBool true) "5 != 6"
    }

    test "inequality false" {
        expectValue (VBool false) "5 != 5"
    }

    test "less or equal" {
        expectValue (VBool true) "5 <= 5"
    }

    test "greater or equal" {
        expectValue (VBool true) "5 >= 5"
    }
]

// =============================================================================
// Unit Tests - Let Binding
// =============================================================================

let letTests = testList "Let Binding" [
    test "simple let" {
        expectValue (VInt 42) "let x = 42 in x"
    }

    test "let with expression" {
        expectValue (VInt 9) "let x = 1 + 2 in x * 3"
    }

    test "nested let" {
        expectValue (VInt 3) "let x = 1 in let y = 2 in x + y"
    }

    test "let shadowing" {
        expectValue (VInt 2) "let x = 1 in let x = 2 in x"
    }

    test "let uses outer scope" {
        expectValue (VInt 3) "let x = 1 in let y = x + 1 in y + 1"
    }
]

// =============================================================================
// Unit Tests - If Expression
// =============================================================================

let ifTests = testList "If Expression" [
    test "if true" {
        expectValue (VInt 1) "if true then 1 else 2"
    }

    test "if false" {
        expectValue (VInt 2) "if false then 1 else 2"
    }

    test "if with comparison" {
        expectValue (VInt 10) "if 5 < 10 then 10 else 5"
    }

    test "if with let" {
        expectValue (VInt 100) "let x = 50 in if x < 100 then 100 else x"
    }

    test "nested if" {
        expectValue (VInt 3) "if true then if false then 1 else 3 else 2"
    }
]

// =============================================================================
// Unit Tests - Boolean Logic
// =============================================================================

let booleanTests = testList "Boolean Logic" [
    test "and true true" {
        expectValue (VBool true) "true and true"
    }

    test "and true false" {
        expectValue (VBool false) "true and false"
    }

    test "or false true" {
        expectValue (VBool true) "false or true"
    }

    test "or false false" {
        expectValue (VBool false) "false or false"
    }

    test "not true" {
        expectValue (VBool false) "not true"
    }

    test "not false" {
        expectValue (VBool true) "not false"
    }
]

// =============================================================================
// Unit Tests - Error Cases
// =============================================================================

let errorTests = testList "Error Cases" [
    test "unbound variable" {
        let result = run "x"
        Expect.isError result "should fail for unbound variable"
    }

    test "division by zero" {
        let result = run "10 / 0"
        Expect.isError result "should fail for division by zero"
    }

    test "type error in addition" {
        let result = run "1 + true"
        Expect.isError result "should fail for type mismatch"
    }

    test "type error in if condition" {
        let result = run "if 1 then 2 else 3"
        Expect.isError result "should fail for non-boolean condition"
    }
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "Interpreter" [
    propertyTests
    literalTests
    arithmeticTests
    comparisonTests
    letTests
    ifTests
    booleanTests
    errorTests
]
