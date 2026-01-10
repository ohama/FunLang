module FunLang.Tests.ParserTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Lexer
open FunLang.Parser

// =============================================================================
// Helper Functions
// =============================================================================

let parseExpr input =
    match tokenize input with
    | Ok tokens -> parse tokens
    | Error e -> Error (sprintf "Lexer error: %s" e.Message)

// =============================================================================
// Property-Based Tests
// =============================================================================

let propertyTests = testList "Parser Properties" [
    testProperty "integer literals parse to ELiteral" <| fun (n: NonNegativeInt) ->
        let input = string n.Get
        match parseExpr input with
        | Ok (ELiteral (LInt v)) -> v = n.Get
        | _ -> false

    testProperty "parse is deterministic" <| fun (input: NonEmptyString) ->
        let r1 = parseExpr input.Get
        let r2 = parseExpr input.Get
        r1 = r2

    testProperty "addition is left-associative" <| fun (a: NonNegativeInt) (b: NonNegativeInt) (c: NonNegativeInt) ->
        let input = sprintf "%d + %d + %d" a.Get b.Get c.Get
        match parseExpr input with
        | Ok (EBinaryOp (Add, EBinaryOp (Add, _, _), _)) -> true
        | _ -> false

    testProperty "multiplication has higher precedence than addition" <| fun (a: NonNegativeInt) (b: NonNegativeInt) (c: NonNegativeInt) ->
        let input = sprintf "%d + %d * %d" a.Get b.Get c.Get
        match parseExpr input with
        | Ok (EBinaryOp (Add, _, EBinaryOp (Mul, _, _))) -> true
        | _ -> false
]

// =============================================================================
// Unit Tests - Literals
// =============================================================================

let literalTests = testList "Literals" [
    test "parse integer literal" {
        let result = parseExpr "42"
        Expect.equal result (Ok (ELiteral (LInt 42))) "should parse integer"
    }

    test "parse boolean true" {
        let result = parseExpr "true"
        Expect.equal result (Ok (ELiteral (LBool true))) "should parse true"
    }

    test "parse boolean false" {
        let result = parseExpr "false"
        Expect.equal result (Ok (ELiteral (LBool false))) "should parse false"
    }

    test "parse string literal" {
        let result = parseExpr "\"hello\""
        Expect.equal result (Ok (ELiteral (LString "hello"))) "should parse string"
    }
]

// =============================================================================
// Unit Tests - Variables
// =============================================================================

let variableTests = testList "Variables" [
    test "parse identifier" {
        let result = parseExpr "x"
        Expect.equal result (Ok (EVariable "x")) "should parse variable"
    }

    test "parse multi-char identifier" {
        let result = parseExpr "fooBar123"
        Expect.equal result (Ok (EVariable "fooBar123")) "should parse variable"
    }
]

// =============================================================================
// Unit Tests - Arithmetic
// =============================================================================

let arithmeticTests = testList "Arithmetic" [
    test "parse addition" {
        let result = parseExpr "1 + 2"
        let expected = EBinaryOp (Add, ELiteral (LInt 1), ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse addition"
    }

    test "parse subtraction" {
        let result = parseExpr "5 - 3"
        let expected = EBinaryOp (Sub, ELiteral (LInt 5), ELiteral (LInt 3))
        Expect.equal result (Ok expected) "should parse subtraction"
    }

    test "parse multiplication" {
        let result = parseExpr "2 * 3"
        let expected = EBinaryOp (Mul, ELiteral (LInt 2), ELiteral (LInt 3))
        Expect.equal result (Ok expected) "should parse multiplication"
    }

    test "parse division" {
        let result = parseExpr "10 / 2"
        let expected = EBinaryOp (Div, ELiteral (LInt 10), ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse division"
    }

    test "parse complex expression" {
        let result = parseExpr "1 + 2 * 3"
        // Should be 1 + (2 * 3) due to precedence
        let expected = EBinaryOp (Add,
            ELiteral (LInt 1),
            EBinaryOp (Mul, ELiteral (LInt 2), ELiteral (LInt 3)))
        Expect.equal result (Ok expected) "should respect precedence"
    }

    test "parse parenthesized expression" {
        let result = parseExpr "(1 + 2) * 3"
        let expected = EBinaryOp (Mul,
            EBinaryOp (Add, ELiteral (LInt 1), ELiteral (LInt 2)),
            ELiteral (LInt 3))
        Expect.equal result (Ok expected) "should respect parentheses"
    }

    test "parse unary minus" {
        let result = parseExpr "-5"
        let expected = EUnaryOp (Neg, ELiteral (LInt 5))
        Expect.equal result (Ok expected) "should parse unary minus"
    }
]

// =============================================================================
// Unit Tests - Comparison
// =============================================================================

let comparisonTests = testList "Comparison" [
    test "parse less than" {
        let result = parseExpr "1 < 2"
        let expected = EBinaryOp (Lt, ELiteral (LInt 1), ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse <"
    }

    test "parse greater than" {
        let result = parseExpr "2 > 1"
        let expected = EBinaryOp (Gt, ELiteral (LInt 2), ELiteral (LInt 1))
        Expect.equal result (Ok expected) "should parse >"
    }

    test "parse equality" {
        let result = parseExpr "1 == 1"
        let expected = EBinaryOp (Eq, ELiteral (LInt 1), ELiteral (LInt 1))
        Expect.equal result (Ok expected) "should parse =="
    }

    test "parse inequality" {
        let result = parseExpr "1 != 2"
        let expected = EBinaryOp (Neq, ELiteral (LInt 1), ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse !="
    }
]

// =============================================================================
// Unit Tests - Let Binding
// =============================================================================

let letTests = testList "Let Binding" [
    test "parse simple let" {
        let result = parseExpr "let x = 1 in x"
        let expected = ELet ("x", ELiteral (LInt 1), EVariable "x")
        Expect.equal result (Ok expected) "should parse let"
    }

    test "parse let with expression" {
        let result = parseExpr "let x = 1 + 2 in x * 3"
        let expected = ELet ("x",
            EBinaryOp (Add, ELiteral (LInt 1), ELiteral (LInt 2)),
            EBinaryOp (Mul, EVariable "x", ELiteral (LInt 3)))
        Expect.equal result (Ok expected) "should parse let with expressions"
    }

    test "parse nested let" {
        let result = parseExpr "let x = 1 in let y = 2 in x + y"
        let expected = ELet ("x", ELiteral (LInt 1),
            ELet ("y", ELiteral (LInt 2),
                EBinaryOp (Add, EVariable "x", EVariable "y")))
        Expect.equal result (Ok expected) "should parse nested let"
    }
]

// =============================================================================
// Unit Tests - If Expression
// =============================================================================

let ifTests = testList "If Expression" [
    test "parse if-then-else" {
        let result = parseExpr "if true then 1 else 2"
        let expected = EIf (ELiteral (LBool true), ELiteral (LInt 1), ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse if"
    }

    test "parse if with comparison" {
        let result = parseExpr "if x < 10 then x else 10"
        let expected = EIf (
            EBinaryOp (Lt, EVariable "x", ELiteral (LInt 10)),
            EVariable "x",
            ELiteral (LInt 10))
        Expect.equal result (Ok expected) "should parse if with condition"
    }
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "Parser" [
    propertyTests
    literalTests
    variableTests
    arithmeticTests
    comparisonTests
    letTests
    ifTests
]
