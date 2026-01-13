module FunLang.Tests.FormatterTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Parser
open FunLang.Formatter

// =============================================================================
// Helper Functions
// =============================================================================

/// Parse and format, then check if the formatted code can be parsed
let canParseFormatted input =
    match parseString input with
    | Error _ -> None  // Skip unparseable inputs
    | Ok ast ->
        let formatted = format ast
        match parseString formatted with
        | Error _ -> Some (formatted, "parse failed")
        | Ok _ -> None

/// Parse and format, then check if ASTs match (ignoring position info)
let roundtripMatches input =
    match parseString input with
    | Error _ -> true  // Skip unparseable inputs
    | Ok ast1 ->
        let formatted = format ast1
        match parseString formatted with
        | Error _ -> false  // Formatted code should parse
        | Ok ast2 ->
            // Compare ASTs without position info
            Display.ofExpr ast1 = Display.ofExpr ast2

// =============================================================================
// Property-Based Tests
// =============================================================================

let propertyTests = testList "Formatter Properties" [
    testProperty "formatted code is parseable (literals)" <| fun (n: NonNegativeInt) ->
        let input = string n.Get
        roundtripMatches input

    testProperty "formatted code is parseable (bool)" <| fun (b: bool) ->
        let input = if b then "true" else "false"
        roundtripMatches input

    testProperty "formatted code is parseable (simple arithmetic)" <| fun (a: NonNegativeInt) (b: NonNegativeInt) ->
        let ops = ["+"; "-"; "*"; "/"]
        ops |> List.forall (fun op ->
            let input = sprintf "%d %s %d" a.Get op b.Get
            roundtripMatches input)

    testProperty "format is deterministic" <| fun (n: NonNegativeInt) ->
        let input = string n.Get
        match parseString input with
        | Error _ -> true
        | Ok ast ->
            let f1 = format ast
            let f2 = format ast
            f1 = f2
]

// =============================================================================
// Unit Tests - Literals
// =============================================================================

let literalTests = testList "Formatter Literals" [
    test "formats integer literal" {
        match parseString "42" with
        | Ok ast -> Expect.equal (format ast) "42" "integer"
        | Error e -> failtest e
    }

    test "formats negative integer with parens" {
        match parseString "-42" with
        | Ok ast -> Expect.equal (format ast) "-42" "negative integer"
        | Error e -> failtest e
    }

    test "formats true literal" {
        match parseString "true" with
        | Ok ast -> Expect.equal (format ast) "true" "true"
        | Error e -> failtest e
    }

    test "formats false literal" {
        match parseString "false" with
        | Ok ast -> Expect.equal (format ast) "false" "false"
        | Error e -> failtest e
    }

    test "formats unit literal" {
        match parseString "()" with
        | Ok ast -> Expect.equal (format ast) "()" "unit"
        | Error e -> failtest e
    }

    test "formats string literal" {
        match parseString "\"hello\"" with
        | Ok ast -> Expect.equal (format ast) "\"hello\"" "string"
        | Error e -> failtest e
    }

    test "formats string with escapes" {
        match parseString "\"hello\\nworld\"" with
        | Ok ast -> Expect.equal (format ast) "\"hello\\nworld\"" "escaped string"
        | Error e -> failtest e
    }
]

// =============================================================================
// Unit Tests - Operators
// =============================================================================

let operatorTests = testList "Formatter Operators" [
    test "formats addition" {
        match parseString "1 + 2" with
        | Ok ast -> Expect.equal (format ast) "1 + 2" "addition"
        | Error e -> failtest e
    }

    test "formats multiplication" {
        match parseString "2 * 3" with
        | Ok ast -> Expect.equal (format ast) "2 * 3" "multiplication"
        | Error e -> failtest e
    }

    test "respects precedence: a + b * c" {
        match parseString "1 + 2 * 3" with
        | Ok ast -> Expect.equal (format ast) "1 + 2 * 3" "no parens needed"
        | Error e -> failtest e
    }

    test "preserves needed parens: (a + b) * c" {
        match parseString "(1 + 2) * 3" with
        | Ok ast -> Expect.equal (format ast) "(1 + 2) * 3" "parens preserved"
        | Error e -> failtest e
    }

    test "respects left associativity: a - b - c" {
        match parseString "1 - 2 - 3" with
        | Ok ast -> Expect.equal (format ast) "1 - 2 - 3" "left associative"
        | Error e -> failtest e
    }

    test "adds parens for right grouping: a - (b - c)" {
        match parseString "1 - (2 - 3)" with
        | Ok ast -> Expect.equal (format ast) "1 - (2 - 3)" "right grouping parens"
        | Error e -> failtest e
    }

    test "formats comparison operators" {
        match parseString "1 < 2" with
        | Ok ast -> Expect.equal (format ast) "1 < 2" "less than"
        | Error e -> failtest e
    }

    test "formats logical and" {
        match parseString "true and false" with
        | Ok ast -> Expect.equal (format ast) "true and false" "and"
        | Error e -> failtest e
    }

    test "formats logical or" {
        match parseString "true or false" with
        | Ok ast -> Expect.equal (format ast) "true or false" "or"
        | Error e -> failtest e
    }

    test "formats not operator" {
        match parseString "not true" with
        | Ok ast -> Expect.equal (format ast) "not true" "not"
        | Error e -> failtest e
    }
]

// =============================================================================
// Unit Tests - Data Structures
// =============================================================================

let dataStructureTests = testList "Formatter Data Structures" [
    test "formats tuple" {
        match parseString "(1, 2, 3)" with
        | Ok ast -> Expect.equal (format ast) "(1, 2, 3)" "tuple"
        | Error e -> failtest e
    }

    test "formats empty list" {
        match parseString "[]" with
        | Ok ast -> Expect.equal (format ast) "[]" "empty list"
        | Error e -> failtest e
    }

    test "formats list literal" {
        match parseString "[1; 2; 3]" with
        | Ok ast -> Expect.equal (format ast) "[1; 2; 3]" "list"
        | Error e -> failtest e
    }

    test "formats cons operator" {
        match parseString "1 :: []" with
        | Ok ast -> Expect.equal (format ast) "1 :: []" "cons"
        | Error e -> failtest e
    }

    test "formats cons chain" {
        match parseString "1 :: 2 :: []" with
        | Ok ast -> Expect.equal (format ast) "1 :: 2 :: []" "cons chain"
        | Error e -> failtest e
    }
]

// =============================================================================
// Unit Tests - Functions
// =============================================================================

let functionTests = testList "Formatter Functions" [
    test "formats lambda" {
        match parseString "fun x -> x" with
        | Ok ast -> Expect.equal (format ast) "fun x -> x" "identity"
        | Error e -> failtest e
    }

    test "formats lambda with body" {
        match parseString "fun x -> x + 1" with
        | Ok ast -> Expect.equal (format ast) "fun x -> x + 1" "increment"
        | Error e -> failtest e
    }

    test "formats curried lambda" {
        match parseString "fun x -> fun y -> x + y" with
        | Ok ast -> Expect.equal (format ast) "fun x -> fun y -> x + y" "curried"
        | Error e -> failtest e
    }

    test "formats application" {
        match parseString "f x" with
        | Ok ast -> Expect.equal (format ast) "f x" "application"
        | Error e -> failtest e
    }

    test "formats chained application" {
        match parseString "f x y" with
        | Ok ast -> Expect.equal (format ast) "f x y" "chained"
        | Error e -> failtest e
    }
]

// =============================================================================
// Unit Tests - Let Bindings
// =============================================================================

let letTests = testList "Formatter Let Bindings" [
    test "formats simple let" {
        match parseString "let x = 1\nx" with
        | Ok ast ->
            let formatted = format ast
            Expect.stringContains formatted "let x = 1" "let binding"
            Expect.stringContains formatted "x" "body"
        | Error e -> failtest e
    }

    test "formats let rec" {
        match parseString "let rec f = fun x -> x\nf 1" with
        | Ok ast ->
            let formatted = format ast
            Expect.stringContains formatted "let rec f" "let rec"
        | Error e -> failtest e
    }
]

// =============================================================================
// Unit Tests - Control Flow
// =============================================================================

let controlFlowTests = testList "Formatter Control Flow" [
    test "formats if expression" {
        match parseString "if true then 1 else 2" with
        | Ok ast -> Expect.equal (format ast) "if true then 1 else 2" "if"
        | Error e -> failtest e
    }

    test "formats nested if" {
        match parseString "if true then if false then 1 else 2 else 3" with
        | Ok ast ->
            let formatted = format ast
            Expect.stringContains formatted "if true then" "outer if"
            Expect.stringContains formatted "if false then" "inner if"
        | Error e -> failtest e
    }
]

// =============================================================================
// Unit Tests - Pattern Matching
// =============================================================================

let patternTests = testList "Formatter Patterns" [
    test "formats wildcard pattern" {
        Expect.equal (formatPattern (Located.noLoc PWildcard)) "_" "wildcard"
    }

    test "formats variable pattern" {
        Expect.equal (formatPattern (Located.noLoc (PVariable "x"))) "x" "variable"
    }

    test "formats literal pattern" {
        Expect.equal (formatPattern (Located.noLoc (PLiteral (LInt 42)))) "42" "literal"
    }

    test "formats tuple pattern" {
        let pat = PTuple [Located.noLoc (PVariable "x"); Located.noLoc (PVariable "y")]
        Expect.equal (formatPattern (Located.noLoc pat)) "(x, y)" "tuple"
    }

    test "formats list pattern" {
        let pat = PList [Located.noLoc (PVariable "x"); Located.noLoc (PVariable "y")]
        Expect.equal (formatPattern (Located.noLoc pat)) "[x; y]" "list"
    }

    test "formats cons pattern" {
        let pat = PCons (Located.noLoc (PVariable "h"), Located.noLoc (PVariable "t"))
        Expect.equal (formatPattern (Located.noLoc pat)) "h :: t" "cons"
    }

    test "formats constructor pattern without arg" {
        let pat = PConstructor ("None", None)
        Expect.equal (formatPattern (Located.noLoc pat)) "None" "nullary"
    }

    test "formats constructor pattern with arg" {
        let pat = PConstructor ("Some", Some (Located.noLoc (PVariable "x")))
        Expect.equal (formatPattern (Located.noLoc pat)) "Some x" "unary"
    }
]

// =============================================================================
// Unit Tests - Type Definitions
// =============================================================================

let typeDefTests = testList "Formatter Type Definitions" [
    test "formats simple type def" {
        let td = { Name = "Bool"; TypeParams = []; Constructors = ["True", None; "False", None] }
        Expect.equal (formatTypeDef td) "type Bool = True | False" "simple type"
    }

    test "formats parametric type def" {
        let td = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = ["None", None; "Some", Some (TEVar "a")]
        }
        Expect.equal (formatTypeDef td) "type Option 'a = None | Some of 'a" "parametric type"
    }

    test "formats type with tuple constructor" {
        let td = {
            Name = "Pair"
            TypeParams = ["a"; "b"]
            Constructors = ["MkPair", Some (TETuple [TEVar "a"; TEVar "b"])]
        }
        Expect.equal (formatTypeDef td) "type Pair 'a 'b = MkPair of 'a * 'b" "tuple ctor"
    }
]

// =============================================================================
// Roundtrip Tests
// =============================================================================

let roundtripTests = testList "Formatter Roundtrip" [
    test "roundtrip: simple expression" {
        Expect.isTrue (roundtripMatches "1 + 2 * 3") "simple"
    }

    test "roundtrip: let binding" {
        Expect.isTrue (roundtripMatches "let x = 1\nx + 1") "let"
    }

    test "roundtrip: lambda" {
        Expect.isTrue (roundtripMatches "fun x -> x + 1") "lambda"
    }

    test "roundtrip: if expression" {
        Expect.isTrue (roundtripMatches "if true then 1 else 2") "if"
    }

    test "roundtrip: list" {
        Expect.isTrue (roundtripMatches "[1; 2; 3]") "list"
    }

    test "roundtrip: tuple" {
        Expect.isTrue (roundtripMatches "(1, 2, 3)") "tuple"
    }

    test "roundtrip: complex expression" {
        Expect.isTrue (roundtripMatches "(1 + 2) * (3 - 4)") "complex"
    }
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let allTests = testList "Formatter" [
    propertyTests
    literalTests
    operatorTests
    dataStructureTests
    functionTests
    letTests
    controlFlowTests
    patternTests
    typeDefTests
    roundtripTests
]
