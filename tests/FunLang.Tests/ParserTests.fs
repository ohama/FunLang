module FunLang.Tests.ParserTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Parser

// =============================================================================
// Helper Functions
// =============================================================================

/// Parse expression and extract Node (for comparing AST structure without positions)
let parseExpr input =
    match tokenize input with
    | Ok tokens ->
        parse tokens |> Result.map (fun lexpr -> lexpr.Node)
    | Error e -> Error (sprintf "Lexer error: %s" e.Message)

// Aliases for Unlocated helpers
let binOp = Unlocated.binOp
let unaryOp = Unlocated.unaryOp
let elet = Unlocated.elet
let eletrec = Unlocated.eletrec
let elambda = Unlocated.elambda
let eapply = Unlocated.eapply
let eif = Unlocated.eif
let etuple = Unlocated.etuple
let elist = Unlocated.elist
let econs = Unlocated.econs

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
        | Ok (EBinaryOp (Add, left, _)) ->
            match left.Node with
            | EBinaryOp (Add, _, _) -> true
            | _ -> false
        | _ -> false

    testProperty "multiplication has higher precedence than addition" <| fun (a: NonNegativeInt) (b: NonNegativeInt) (c: NonNegativeInt) ->
        let input = sprintf "%d + %d * %d" a.Get b.Get c.Get
        match parseExpr input with
        | Ok (EBinaryOp (Add, _, right)) ->
            match right.Node with
            | EBinaryOp (Mul, _, _) -> true
            | _ -> false
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
        let expected = binOp Add (ELiteral (LInt 1)) (ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse addition"
    }

    test "parse subtraction" {
        let result = parseExpr "5 - 3"
        let expected = binOp Sub (ELiteral (LInt 5)) (ELiteral (LInt 3))
        Expect.equal result (Ok expected) "should parse subtraction"
    }

    test "parse multiplication" {
        let result = parseExpr "2 * 3"
        let expected = binOp Mul (ELiteral (LInt 2)) (ELiteral (LInt 3))
        Expect.equal result (Ok expected) "should parse multiplication"
    }

    test "parse division" {
        let result = parseExpr "10 / 2"
        let expected = binOp Div (ELiteral (LInt 10)) (ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse division"
    }

    test "parse complex expression" {
        let result = parseExpr "1 + 2 * 3"
        // Should be 1 + (2 * 3) due to precedence
        let expected = binOp Add (ELiteral (LInt 1)) (binOp Mul (ELiteral (LInt 2)) (ELiteral (LInt 3)))
        Expect.equal result (Ok expected) "should respect precedence"
    }

    test "parse parenthesized expression" {
        let result = parseExpr "(1 + 2) * 3"
        let expected = binOp Mul (binOp Add (ELiteral (LInt 1)) (ELiteral (LInt 2))) (ELiteral (LInt 3))
        Expect.equal result (Ok expected) "should respect parentheses"
    }

    test "parse unary minus" {
        let result = parseExpr "-5"
        let expected = unaryOp Neg (ELiteral (LInt 5))
        Expect.equal result (Ok expected) "should parse unary minus"
    }
]

// =============================================================================
// Unit Tests - Comparison
// =============================================================================

let comparisonTests = testList "Comparison" [
    test "parse less than" {
        let result = parseExpr "1 < 2"
        let expected = binOp Lt (ELiteral (LInt 1)) (ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse <"
    }

    test "parse greater than" {
        let result = parseExpr "2 > 1"
        let expected = binOp Gt (ELiteral (LInt 2)) (ELiteral (LInt 1))
        Expect.equal result (Ok expected) "should parse >"
    }

    test "parse equality" {
        let result = parseExpr "1 == 1"
        let expected = binOp Eq (ELiteral (LInt 1)) (ELiteral (LInt 1))
        Expect.equal result (Ok expected) "should parse =="
    }

    test "parse inequality" {
        let result = parseExpr "1 != 2"
        let expected = binOp Neq (ELiteral (LInt 1)) (ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse !="
    }
]

// =============================================================================
// Unit Tests - Let Binding
// =============================================================================

let letTests = testList "Let Binding" [
    test "parse simple let" {
        let result = parseExpr "let x = 1 in x"
        let expected = elet "x" (ELiteral (LInt 1)) (EVariable "x")
        Expect.equal result (Ok expected) "should parse let"
    }

    test "parse let with expression" {
        let result = parseExpr "let x = 1 + 2 in x * 3"
        let value = binOp Add (ELiteral (LInt 1)) (ELiteral (LInt 2))
        let body = binOp Mul (EVariable "x") (ELiteral (LInt 3))
        let expected = elet "x" value body
        Expect.equal result (Ok expected) "should parse let with expressions"
    }

    test "parse nested let" {
        let result = parseExpr "let x = 1 in let y = 2 in x + y"
        let inner = elet "y" (ELiteral (LInt 2)) (binOp Add (EVariable "x") (EVariable "y"))
        let expected = elet "x" (ELiteral (LInt 1)) inner
        Expect.equal result (Ok expected) "should parse nested let"
    }
]

// =============================================================================
// Unit Tests - If Expression
// =============================================================================

let ifTests = testList "If Expression" [
    test "parse if-then-else" {
        let result = parseExpr "if true then 1 else 2"
        let expected = eif (ELiteral (LBool true)) (ELiteral (LInt 1)) (ELiteral (LInt 2))
        Expect.equal result (Ok expected) "should parse if"
    }

    test "parse if with comparison" {
        let result = parseExpr "if x < 10 then x else 10"
        let cond = binOp Lt (EVariable "x") (ELiteral (LInt 10))
        let expected = eif cond (EVariable "x") (ELiteral (LInt 10))
        Expect.equal result (Ok expected) "should parse if with condition"
    }
]

// =============================================================================
// Unit Tests - Lambda
// =============================================================================

let lambdaTests = testList "Lambda" [
    test "parse simple lambda" {
        let result = parseExpr "fun x -> x"
        let expected = elambda "x" (EVariable "x")
        Expect.equal result (Ok expected) "should parse lambda"
    }

    test "parse lambda with expression body" {
        let result = parseExpr "fun x -> x + 1"
        let expected = elambda "x" (binOp Add (EVariable "x") (ELiteral (LInt 1)))
        Expect.equal result (Ok expected) "should parse lambda with expression"
    }

    test "parse nested lambda (curried)" {
        let result = parseExpr "fun x -> fun y -> x + y"
        let expected = elambda "x" (elambda "y" (binOp Add (EVariable "x") (EVariable "y")))
        Expect.equal result (Ok expected) "should parse nested lambda"
    }
]

// =============================================================================
// Unit Tests - Function Application
// =============================================================================

let applicationTests = testList "Function Application" [
    test "parse simple application" {
        let result = parseExpr "f x"
        let expected = eapply (EVariable "f") (EVariable "x")
        Expect.equal result (Ok expected) "should parse application"
    }

    test "parse application with literal" {
        let result = parseExpr "f 42"
        let expected = eapply (EVariable "f") (ELiteral (LInt 42))
        Expect.equal result (Ok expected) "should parse application with literal"
    }

    test "parse chained application (left-associative)" {
        let result = parseExpr "f x y"
        let expected = eapply (eapply (EVariable "f") (EVariable "x")) (EVariable "y")
        Expect.equal result (Ok expected) "should parse chained application"
    }

    test "parse application with parenthesized argument" {
        let result = parseExpr "f (1 + 2)"
        let expected = eapply (EVariable "f") (binOp Add (ELiteral (LInt 1)) (ELiteral (LInt 2)))
        Expect.equal result (Ok expected) "should parse application with paren arg"
    }

    test "parse lambda application" {
        let result = parseExpr "(fun x -> x) 42"
        let expected = eapply (elambda "x" (EVariable "x")) (ELiteral (LInt 42))
        Expect.equal result (Ok expected) "should parse lambda application"
    }
]

// =============================================================================
// Unit Tests - Let Rec
// =============================================================================

let letRecTests = testList "Let Rec" [
    test "parse let rec" {
        let result = parseExpr "let rec f = fun x -> x in f"
        let expected = eletrec "f" (elambda "x" (EVariable "x")) (EVariable "f")
        Expect.equal result (Ok expected) "should parse let rec"
    }

    test "parse recursive factorial" {
        let result = parseExpr "let rec fact = fun n -> if n == 0 then 1 else n * fact (n - 1) in fact 5"
        Expect.isOk result "should parse recursive function"
    }

    // Issue-005 regression test: multiline let rec chains with 'in' on separate line
    test "parse multiline let rec with in on separate line" {
        let input = """let rec a = fun x -> x
in
a 1"""
        let result = parseExpr input
        Expect.isOk result "should parse multiline let rec with in on separate line"
    }

    test "parse chained multiline let rec" {
        let input = """let rec a = fun x ->
  match x with
  | [] -> []
  | h :: t -> a t
in
let rec b = fun x -> x
in
b [1]"""
        let result = parseExpr input
        Expect.isOk result "should parse chained multiline let rec"
    }

    test "parse 5 chained multiline let rec (issue-005)" {
        let input = """let rec a = fun x -> x
in
let rec b = fun x -> x
in
let rec c = fun x -> x
in
let rec d = fun x -> x
in
let rec e = fun x -> x
in
e 1"""
        let result = parseExpr input
        Expect.isOk result "should parse 5 chained multiline let rec"
    }
]

// =============================================================================
// Unit Tests - Tuples
// =============================================================================

let tupleTests = testList "Tuples" [
    test "parse pair" {
        let result = parseExpr "(1, 2)"
        let expected = etuple [ELiteral (LInt 1); ELiteral (LInt 2)]
        Expect.equal result (Ok expected) "should parse pair"
    }

    test "parse triple" {
        let result = parseExpr "(1, 2, 3)"
        let expected = etuple [ELiteral (LInt 1); ELiteral (LInt 2); ELiteral (LInt 3)]
        Expect.equal result (Ok expected) "should parse triple"
    }

    test "parse tuple with expressions" {
        let result = parseExpr "(1 + 2, x, true)"
        let expected = etuple [
            binOp Add (ELiteral (LInt 1)) (ELiteral (LInt 2))
            EVariable "x"
            ELiteral (LBool true)
        ]
        Expect.equal result (Ok expected) "should parse tuple with expressions"
    }

    test "parse nested tuple" {
        let result = parseExpr "((1, 2), 3)"
        let expected = etuple [etuple [ELiteral (LInt 1); ELiteral (LInt 2)]; ELiteral (LInt 3)]
        Expect.equal result (Ok expected) "should parse nested tuple"
    }
]

// =============================================================================
// Unit Tests - Lists
// =============================================================================

let listTests = testList "Lists" [
    test "parse empty list" {
        let result = parseExpr "[]"
        let expected = EList []
        Expect.equal result (Ok expected) "should parse empty list"
    }

    test "parse singleton list" {
        let result = parseExpr "[1]"
        let expected = elist [ELiteral (LInt 1)]
        Expect.equal result (Ok expected) "should parse singleton list"
    }

    test "parse list with multiple elements" {
        let result = parseExpr "[1; 2; 3]"
        let expected = elist [ELiteral (LInt 1); ELiteral (LInt 2); ELiteral (LInt 3)]
        Expect.equal result (Ok expected) "should parse list"
    }

    test "parse list with expressions" {
        let result = parseExpr "[1 + 2; x; 4]"
        let expected = elist [
            binOp Add (ELiteral (LInt 1)) (ELiteral (LInt 2))
            EVariable "x"
            ELiteral (LInt 4)
        ]
        Expect.equal result (Ok expected) "should parse list with expressions"
    }

    test "parse nested list" {
        let result = parseExpr "[[1]; [2; 3]]"
        let expected = elist [
            elist [ELiteral (LInt 1)]
            elist [ELiteral (LInt 2); ELiteral (LInt 3)]
        ]
        Expect.equal result (Ok expected) "should parse nested list"
    }
]

// =============================================================================
// Unit Tests - Cons
// =============================================================================

let consTests = testList "Cons" [
    test "parse simple cons" {
        let result = parseExpr "1 :: []"
        let expected = econs (ELiteral (LInt 1)) (EList [])
        Expect.equal result (Ok expected) "should parse cons"
    }

    test "parse chained cons (right-associative)" {
        let result = parseExpr "1 :: 2 :: []"
        let expected = econs (ELiteral (LInt 1)) (econs (ELiteral (LInt 2)) (EList []))
        Expect.equal result (Ok expected) "should parse chained cons"
    }

    test "parse cons with variable" {
        let result = parseExpr "x :: xs"
        let expected = econs (EVariable "x") (EVariable "xs")
        Expect.equal result (Ok expected) "should parse cons with variables"
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
    lambdaTests
    applicationTests
    letRecTests
    tupleTests
    listTests
    consTests
]
