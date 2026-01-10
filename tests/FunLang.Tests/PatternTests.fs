module FunLang.Tests.PatternTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Parser

// =============================================================================
// Helper Functions
// =============================================================================

let parse = parseString

let eval input =
    match parse input with
    | Ok ast ->
        match FunLang.Interpreter.eval Map.empty ast with
        | Ok v -> Some v
        | Error _ -> None
    | Error _ -> None

let shouldEqual expected actual =
    Expect.equal actual expected "values should match"

let shouldBeOk result =
    Expect.isOk result "should succeed"

let shouldBeError result =
    Expect.isError result "should fail"

// =============================================================================
// Parsing Tests - Basic Patterns
// =============================================================================

let parsingTests = testList "Pattern Parsing" [
    test "parse wildcard pattern" {
        let result = parse "match x with | _ -> 1"
        shouldBeOk result
    }

    test "parse variable pattern" {
        let result = parse "match x with | y -> y"
        shouldBeOk result
    }

    test "parse literal int pattern" {
        let result = parse "match x with | 0 -> true | _ -> false"
        shouldBeOk result
    }

    test "parse literal bool pattern" {
        let result = parse "match x with | true -> 1 | false -> 0"
        shouldBeOk result
    }

    test "parse tuple pattern" {
        let result = parse "match p with | (x, y) -> x + y"
        shouldBeOk result
    }

    test "parse list pattern empty" {
        let result = parse "match xs with | [] -> 0 | _ -> 1"
        shouldBeOk result
    }

    test "parse list pattern elements" {
        let result = parse "match xs with | [a; b] -> a + b | _ -> 0"
        shouldBeOk result
    }

    test "parse cons pattern" {
        let result = parse "match xs with | h :: t -> h | [] -> 0"
        shouldBeOk result
    }

    test "parse nested cons pattern" {
        let result = parse "match xs with | a :: b :: t -> a + b | _ -> 0"
        shouldBeOk result
    }

    test "parse guard clause" {
        let result = parse "match x with | n when n > 0 -> n | _ -> 0"
        shouldBeOk result
    }

    test "parse multiple cases" {
        let result = parse "match x with | 0 -> \"zero\" | 1 -> \"one\" | _ -> \"many\""
        shouldBeOk result
    }
]

// =============================================================================
// Evaluation Tests - Wildcard Pattern
// =============================================================================

let wildcardTests = testList "Wildcard Pattern" [
    test "wildcard matches anything" {
        eval "match 42 with | _ -> true" |> shouldEqual (Some (VBool true))
    }

    test "wildcard in second case" {
        eval "match 5 with | 0 -> false | _ -> true" |> shouldEqual (Some (VBool true))
    }
]

// =============================================================================
// Evaluation Tests - Variable Pattern
// =============================================================================

let variableTests = testList "Variable Pattern" [
    test "variable binds value" {
        eval "match 42 with | x -> x" |> shouldEqual (Some (VInt 42))
    }

    test "variable in expression" {
        eval "match 10 with | n -> n * 2" |> shouldEqual (Some (VInt 20))
    }
]

// =============================================================================
// Evaluation Tests - Literal Patterns
// =============================================================================

let literalTests = testList "Literal Patterns" [
    test "int pattern matches" {
        eval "match 1 with | 1 -> true | _ -> false" |> shouldEqual (Some (VBool true))
    }

    test "int pattern no match" {
        eval "match 2 with | 1 -> true | _ -> false" |> shouldEqual (Some (VBool false))
    }

    test "bool pattern true" {
        eval "match true with | true -> 1 | false -> 0" |> shouldEqual (Some (VInt 1))
    }

    test "bool pattern false" {
        eval "match false with | true -> 1 | false -> 0" |> shouldEqual (Some (VInt 0))
    }

    test "string pattern matches" {
        eval "match \"hello\" with | \"hello\" -> true | _ -> false" |> shouldEqual (Some (VBool true))
    }
]

// =============================================================================
// Evaluation Tests - Tuple Patterns
// =============================================================================

let tupleTests = testList "Tuple Patterns" [
    test "tuple pattern binds elements" {
        eval "match (1, 2) with | (a, b) -> a + b" |> shouldEqual (Some (VInt 3))
    }

    test "nested tuple pattern" {
        eval "match ((1, 2), 3) with | ((a, b), c) -> a + b + c" |> shouldEqual (Some (VInt 6))
    }

    test "tuple with wildcard" {
        eval "match (1, 2, 3) with | (a, _, c) -> a + c" |> shouldEqual (Some (VInt 4))
    }
]

// =============================================================================
// Evaluation Tests - List Patterns
// =============================================================================

let listTests = testList "List Patterns" [
    test "empty list pattern" {
        eval "match [] with | [] -> true | _ -> false" |> shouldEqual (Some (VBool true))
    }

    test "non-empty list against empty pattern" {
        eval "match [1] with | [] -> true | _ -> false" |> shouldEqual (Some (VBool false))
    }

    test "single element list pattern" {
        eval "match [42] with | [x] -> x | _ -> 0" |> shouldEqual (Some (VInt 42))
    }

    test "two element list pattern" {
        eval "match [1; 2] with | [a; b] -> a + b | _ -> 0" |> shouldEqual (Some (VInt 3))
    }

    test "list pattern length mismatch" {
        eval "match [1; 2; 3] with | [a; b] -> a + b | _ -> 0" |> shouldEqual (Some (VInt 0))
    }
]

// =============================================================================
// Evaluation Tests - Cons Patterns
// =============================================================================

let consTests = testList "Cons Patterns" [
    test "cons pattern head" {
        eval "match [1; 2; 3] with | h :: _ -> h | [] -> 0" |> shouldEqual (Some (VInt 1))
    }

    test "cons pattern tail" {
        eval "match [1; 2; 3] with | _ :: t -> t | [] -> []"
        |> shouldEqual (Some (VList [VInt 2; VInt 3]))
    }

    test "cons pattern on empty list" {
        eval "match [] with | h :: t -> h | [] -> 0" |> shouldEqual (Some (VInt 0))
    }

    test "nested cons pattern" {
        eval "match [1; 2; 3] with | a :: b :: _ -> a + b | _ -> 0" |> shouldEqual (Some (VInt 3))
    }

    test "cons with list tail pattern" {
        eval "match [1; 2] with | h :: [x] -> h + x | _ -> 0" |> shouldEqual (Some (VInt 3))
    }
]

// =============================================================================
// Evaluation Tests - Guard Clauses
// =============================================================================

let guardTests = testList "Guard Clauses" [
    test "guard true proceeds" {
        eval "match 5 with | n when n > 0 -> n | _ -> 0" |> shouldEqual (Some (VInt 5))
    }

    test "guard false skips" {
        eval "match 5 with | n when n > 10 -> n | _ -> 0" |> shouldEqual (Some (VInt 0))
    }

    test "guard with binding" {
        eval "match (3, 4) with | (a, b) when a < b -> b - a | _ -> 0" |> shouldEqual (Some (VInt 1))
    }

    test "multiple guards" {
        eval "match 5 with | n when n < 0 -> -1 | n when n = 0 -> 0 | _ -> 1"
        |> shouldEqual (Some (VInt 1))
    }
]

// =============================================================================
// Evaluation Tests - Practical Examples
// =============================================================================

let practicalTests = testList "Practical Examples" [
    test "length function" {
        let code = "let rec length = fun xs -> match xs with | [] -> 0 | _ :: t -> 1 + length t in length [1; 2; 3; 4; 5]"
        eval code |> shouldEqual (Some (VInt 5))
    }

    test "sum function" {
        let code = "let rec sum = fun xs -> match xs with | [] -> 0 | h :: t -> h + sum t in sum [1; 2; 3; 4]"
        eval code |> shouldEqual (Some (VInt 10))
    }

    test "map function" {
        let code = "let rec map = fun f -> fun xs -> match xs with | [] -> [] | h :: t -> f h :: map f t in map (fun x -> x * 2) [1; 2; 3]"
        eval code |> shouldEqual (Some (VList [VInt 2; VInt 4; VInt 6]))
    }

    test "filter function" {
        let code = "let rec filter = fun p -> fun xs -> match xs with | [] -> [] | h :: t when p h -> h :: filter p t | _ :: t -> filter p t in filter (fun x -> x > 2) [1; 2; 3; 4; 5]"
        eval code |> shouldEqual (Some (VList [VInt 3; VInt 4; VInt 5]))
    }

    test "fibonacci with pattern matching" {
        let code = "let rec fib = fun n -> match n with | 0 -> 0 | 1 -> 1 | _ -> fib (n - 1) + fib (n - 2) in fib 10"
        eval code |> shouldEqual (Some (VInt 55))
    }
]

// =============================================================================
// Property-Based Tests
// =============================================================================

let propertyTests = testList "Pattern Properties" [
    testProperty "wildcard always matches" <| fun (n: int) ->
        eval $"match {n} with | _ -> true" = Some (VBool true)

    testProperty "variable captures value" <| fun (n: NonNegativeInt) ->
        eval $"match {n.Get} with | x -> x" = Some (VInt n.Get)

    testProperty "cons pattern decomposes list" <| fun (h: NonNegativeInt) (t: NonNegativeInt list) ->
        let listStr = h.Get :: (t |> List.map (fun x -> x.Get))
                      |> List.map string
                      |> String.concat "; "
        let code = $"match [{listStr}] with | x :: _ -> x | [] -> -1"
        eval code = Some (VInt h.Get)

    testProperty "tuple pattern extracts elements" <| fun (a: NonNegativeInt) (b: NonNegativeInt) ->
        let code = $"match ({a.Get}, {b.Get}) with | (x, y) -> x + y"
        eval code = Some (VInt (a.Get + b.Get))
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "Pattern Matching" [
    parsingTests
    wildcardTests
    variableTests
    literalTests
    tupleTests
    listTests
    consTests
    guardTests
    practicalTests
    propertyTests
]
