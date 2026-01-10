module FunLang.Tests.InterpreterTests

open Expecto
open FsCheck
open FunLang.Ast
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
// Unit Tests - Lambda and Application
// =============================================================================

let lambdaTests = testList "Lambda and Application" [
    test "identity function" {
        expectValue (VInt 42) "(fun x -> x) 42"
    }

    test "constant function" {
        expectValue (VInt 1) "(fun x -> 1) 99"
    }

    test "lambda with arithmetic" {
        expectValue (VInt 10) "(fun x -> x + 1) 9"
    }

    test "curried function" {
        expectValue (VInt 5) "(fun x -> fun y -> x + y) 2 3"
    }

    test "function in let binding" {
        expectValue (VInt 8) "let double = fun x -> x * 2 in double 4"
    }

    test "closure captures environment" {
        expectValue (VInt 15) "let x = 10 in let addX = fun y -> x + y in addX 5"
    }

    test "higher-order function" {
        expectValue (VInt 9) "let apply = fun f -> fun x -> f x in let sq = fun n -> n * n in apply sq 3"
    }
]

// =============================================================================
// Unit Tests - Let Rec
// =============================================================================

let letRecTests = testList "Let Rec" [
    test "simple recursive function" {
        expectValue (VInt 120) "let rec fact = fun n -> if n == 0 then 1 else n * fact (n - 1) in fact 5"
    }

    test "recursive countdown" {
        expectValue (VInt 0) "let rec countdown = fun n -> if n == 0 then 0 else countdown (n - 1) in countdown 10"
    }

    test "fibonacci" {
        expectValue (VInt 55) "let rec fib = fun n -> if n < 2 then n else fib (n - 1) + fib (n - 2) in fib 10"
    }
]

// =============================================================================
// Unit Tests - Tuples
// =============================================================================

let tupleTests = testList "Tuples" [
    test "pair of integers" {
        expectValue (VTuple [VInt 1; VInt 2]) "(1, 2)"
    }

    test "triple" {
        expectValue (VTuple [VInt 1; VInt 2; VInt 3]) "(1, 2, 3)"
    }

    test "tuple with expressions" {
        expectValue (VTuple [VInt 3; VInt 10; VBool true]) "(1 + 2, 5 * 2, true)"
    }

    test "nested tuple" {
        expectValue (VTuple [VTuple [VInt 1; VInt 2]; VInt 3]) "((1, 2), 3)"
    }

    test "tuple with variables" {
        expectValue (VTuple [VInt 10; VInt 20]) "let x = 10 in let y = 20 in (x, y)"
    }
]

// =============================================================================
// Unit Tests - Lists
// =============================================================================

let listTests = testList "Lists" [
    test "empty list" {
        expectValue (VList []) "[]"
    }

    test "singleton list" {
        expectValue (VList [VInt 1]) "[1]"
    }

    test "list of integers" {
        expectValue (VList [VInt 1; VInt 2; VInt 3]) "[1; 2; 3]"
    }

    test "list with expressions" {
        expectValue (VList [VInt 3; VInt 6; VInt 9]) "[1 + 2; 2 * 3; 3 * 3]"
    }

    test "nested list" {
        expectValue (VList [VList [VInt 1]; VList [VInt 2; VInt 3]]) "[[1]; [2; 3]]"
    }

    test "list of booleans" {
        expectValue (VList [VBool true; VBool false; VBool true]) "[true; false; true]"
    }
]

// =============================================================================
// Unit Tests - Cons
// =============================================================================

let consTests = testList "Cons" [
    test "cons to empty list" {
        expectValue (VList [VInt 1]) "1 :: []"
    }

    test "cons chain" {
        expectValue (VList [VInt 1; VInt 2; VInt 3]) "1 :: 2 :: 3 :: []"
    }

    test "cons to existing list" {
        expectValue (VList [VInt 0; VInt 1; VInt 2]) "0 :: [1; 2]"
    }

    test "cons with expression" {
        expectValue (VList [VInt 6; VInt 1]) "(2 * 3) :: [1]"
    }
]

// =============================================================================
// Property Tests for New Features
// =============================================================================

let newFeaturePropertyTests = testList "New Feature Properties" [
    testProperty "identity function returns input" <| fun (n: NonNegativeInt) ->
        match run (sprintf "(fun x -> x) %d" n.Get) with
        | Ok (VInt v) -> v = n.Get
        | _ -> false

    testProperty "cons prepends to list" <| fun (n: NonNegativeInt) ->
        let code = sprintf "%d :: []" n.Get
        match run code with
        | Ok (VList [VInt v]) -> v = n.Get
        | _ -> false

    testProperty "tuple preserves order" <| fun (a: NonNegativeInt) (b: NonNegativeInt) ->
        let code = sprintf "(%d, %d)" a.Get b.Get
        match run code with
        | Ok (VTuple [VInt x; VInt y]) -> x = a.Get && y = b.Get
        | _ -> false

    testProperty "list preserves elements" <| fun (a: NonNegativeInt) (b: NonNegativeInt) (c: NonNegativeInt) ->
        let code = sprintf "[%d; %d; %d]" a.Get b.Get c.Get
        match run code with
        | Ok (VList [VInt x; VInt y; VInt z]) -> x = a.Get && y = b.Get && z = c.Get
        | _ -> false
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
    lambdaTests
    letRecTests
    tupleTests
    listTests
    consTests
    newFeaturePropertyTests
]
