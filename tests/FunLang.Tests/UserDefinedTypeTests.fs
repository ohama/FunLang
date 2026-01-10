module FunLang.Tests.UserDefinedTypeTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Types
open FunLang.TypeInfer
open FunLang.Interpreter
open FunLang.Parser
open FunLang.GeneratedParser
open FunLang.Tests.TestHelpers

// =============================================================================
// Phase 6: User-Defined Types (Discriminated Unions)
// =============================================================================
//
// Syntax: type Option 'a = None | Some of 'a
//         type List 'a = Nil | Cons of 'a * List 'a
//
// This file tests:
// 1. Lexer: TYPEVAR token ('a, 'b, etc.)
// 2. Parser: Type declarations, constructor expressions
// 3. Type inference: User-defined type constructors
// 4. Interpreter: Constructor creation and pattern matching
// =============================================================================

// =============================================================================
// Lexer Tests - TYPEVAR token
// =============================================================================

let lexerTests = testList "Lexer - TYPEVAR" [

    test "tokenize single type variable 'a" {
        let result = tokenizeString "'a"
        let tokens = expectOk "tokenize 'a" result
        Expect.exists tokens (function TYPEVAR "a" -> true | _ -> false) "should have TYPEVAR a"
    }

    test "tokenize type variable 'abc" {
        let result = tokenizeString "'abc"
        let tokens = expectOk "tokenize 'abc" result
        Expect.exists tokens (function TYPEVAR "abc" -> true | _ -> false) "should have TYPEVAR abc"
    }

    test "tokenize multiple type variables" {
        let result = tokenizeString "'a 'b 'c"
        let tokens = expectOk "tokenize 'a 'b 'c" result
        Expect.exists tokens (function TYPEVAR "a" -> true | _ -> false) "should have TYPEVAR a"
        Expect.exists tokens (function TYPEVAR "b" -> true | _ -> false) "should have TYPEVAR b"
        Expect.exists tokens (function TYPEVAR "c" -> true | _ -> false) "should have TYPEVAR c"
    }

    test "tokenize type declaration keywords" {
        let result = tokenizeString "type Option of"
        let tokens = expectOk "tokenize type of" result
        Expect.exists tokens (function TYPE -> true | _ -> false) "should have TYPE"
        Expect.exists tokens (function OF -> true | _ -> false) "should have OF"
    }

    test "tokenize type with type variable" {
        let result = tokenizeString "type Option 'a"
        let tokens = expectOk "tokenize type Option 'a" result
        Expect.exists tokens (function TYPE -> true | _ -> false) "should have TYPE"
        Expect.exists tokens (function IDENT "Option" -> true | _ -> false) "should have IDENT Option"
        Expect.exists tokens (function TYPEVAR "a" -> true | _ -> false) "should have TYPEVAR a"
    }
]

// =============================================================================
// AST Tests - Constructor Expressions
// =============================================================================

let astTests = testList "AST - Constructors" [

    test "EConstructor with no argument" {
        let expr = EConstructor ("None", None)
        match expr with
        | EConstructor ("None", None) -> ()
        | _ -> failtest "should be EConstructor None"
    }

    test "EConstructor with argument" {
        let expr = EConstructor ("Some", Some (ELiteral (LInt 42)))
        match expr with
        | EConstructor ("Some", Some (ELiteral (LInt 42))) -> ()
        | _ -> failtest "should be EConstructor Some with int"
    }

    test "PConstructor pattern with no argument" {
        let pat = PConstructor ("None", None)
        match pat with
        | PConstructor ("None", None) -> ()
        | _ -> failtest "should be PConstructor None"
    }

    test "PConstructor pattern with argument" {
        let pat = PConstructor ("Some", Some (PVariable "x"))
        match pat with
        | PConstructor ("Some", Some (PVariable "x")) -> ()
        | _ -> failtest "should be PConstructor Some with variable"
    }

    test "VConstructed value with no argument" {
        let value = VConstructed ("None", None)
        match value with
        | VConstructed ("None", None) -> ()
        | _ -> failtest "should be VConstructed None"
    }

    test "VConstructed value with argument" {
        let value = VConstructed ("Some", Some (VInt 42))
        match value with
        | VConstructed ("Some", Some (VInt 42)) -> ()
        | _ -> failtest "should be VConstructed Some with int"
    }
]

// =============================================================================
// Interpreter Tests - Constructor Values
// =============================================================================

let interpreterTests = testList "Interpreter - Constructors" [

    test "evaluate constructor without argument" {
        let expr = EConstructor ("None", None)
        let result = eval Map.empty expr
        let value = expectOk "eval None" result
        match value with
        | VConstructed ("None", None) -> ()
        | _ -> failtest (sprintf "expected VConstructed None, got %A" value)
    }

    test "evaluate constructor with argument" {
        let expr = EConstructor ("Some", Some (ELiteral (LInt 42)))
        let result = eval Map.empty expr
        let value = expectOk "eval Some 42" result
        match value with
        | VConstructed ("Some", Some (VInt 42)) -> ()
        | _ -> failtest (sprintf "expected VConstructed Some 42, got %A" value)
    }

    test "pattern match on constructor without argument" {
        // match None with | None -> 1 | Some _ -> 2
        let expr = EMatch (
            EConstructor ("None", None),
            [
                (PConstructor ("None", None), None, ELiteral (LInt 1))
                (PConstructor ("Some", Some PWildcard), None, ELiteral (LInt 2))
            ])
        let result = eval Map.empty expr
        let value = expectOk "match None" result
        Expect.equal value (VInt 1) "should match None case"
    }

    test "pattern match on constructor with argument" {
        // match Some 42 with | None -> 0 | Some x -> x
        let expr = EMatch (
            EConstructor ("Some", Some (ELiteral (LInt 42))),
            [
                (PConstructor ("None", None), None, ELiteral (LInt 0))
                (PConstructor ("Some", Some (PVariable "x")), None, EVariable "x")
            ])
        let result = eval Map.empty expr
        let value = expectOk "match Some 42" result
        Expect.equal value (VInt 42) "should extract value from Some"
    }

    test "nested constructor pattern matching" {
        // match Some (Some 1) with | Some (Some x) -> x | _ -> 0
        let expr = EMatch (
            EConstructor ("Some", Some (EConstructor ("Some", Some (ELiteral (LInt 1))))),
            [
                (PConstructor ("Some", Some (PConstructor ("Some", Some (PVariable "x")))), None, EVariable "x")
                (PWildcard, None, ELiteral (LInt 0))
            ])
        let result = eval Map.empty expr
        let value = expectOk "match nested Some" result
        Expect.equal value (VInt 1) "should extract nested value"
    }
]

// =============================================================================
// Parser Tests - Type Declarations (TODO: implement parsing)
// =============================================================================

let parserTests = ptestList "Parser - Type Declarations" [

    test "parse simple type declaration" {
        // type Bool = True | False
        let result = parseStringToAst "type Bool = True | False"
        Expect.isOk result "should parse type declaration"
    }

    test "parse type declaration with type parameter" {
        // type Option 'a = None | Some of 'a
        let result = parseStringToAst "type Option 'a = None | Some of 'a"
        Expect.isOk result "should parse polymorphic type"
    }

    test "parse constructor expression without argument" {
        // None
        let result = parseStringToAst "None"
        let expr = expectOk "parse None" result
        match expr with
        | EConstructor ("None", None) -> ()
        | EVariable "None" -> () // Initially might be parsed as variable
        | _ -> failtest (sprintf "expected constructor, got %A" expr)
    }

    test "parse constructor expression with argument" {
        // Some 42
        let result = parseStringToAst "Some 42"
        let expr = expectOk "parse Some 42" result
        match expr with
        | EConstructor ("Some", Some (ELiteral (LInt 42))) -> ()
        | EApply (EVariable "Some", ELiteral (LInt 42)) -> () // Initially might be function application
        | _ -> failtest (sprintf "expected constructor application, got %A" expr)
    }
]

// =============================================================================
// Type Inference Tests - Constructors (TODO: implement type checking)
// =============================================================================

let typeInferenceTests = ptestList "Type Inference - Constructors" [

    test "infer type of constructor without argument" {
        // Assuming Option type is defined
        // None : Option 'a
        TypeHelpers.resetCounter ()
        let expr = EConstructor ("None", None)
        let result = inferType expr
        Expect.isOk result "should infer type of None"
    }

    test "infer type of constructor with argument" {
        // Some 42 : Option int
        TypeHelpers.resetCounter ()
        let expr = EConstructor ("Some", Some (ELiteral (LInt 42)))
        let result = inferType expr
        let t = expectOk "infer Some 42" result
        // Should be something like TConstructor ("Option", [TInt])
        ()
    }
]

// =============================================================================
// Integration Tests - Full Pipeline (TODO: implement full pipeline)
// =============================================================================

let integrationTests = ptestList "Integration - User Types" [

    test "define and use Option type" {
        // type Option 'a = None | Some of 'a
        // let x = Some 42
        // match x with | None -> 0 | Some n -> n
        let code = """
type Option 'a = None | Some of 'a
let x = Some 42
match x with
| None -> 0
| Some n -> n
"""
        let result = runString code
        let value = expectOk "run Option example" result
        Expect.equal value (VInt 42) "should evaluate to 42"
    }
]

// =============================================================================
// Property-Based Tests
// =============================================================================

let propertyTests = testList "Properties - User Types" [

    testProperty "constructor creates VConstructed" <| fun (name: NonEmptyString) ->
        // Filter to only allow valid identifier names
        let validName = name.Get |> String.filter System.Char.IsLetter
        if validName.Length > 0 then
            let expr = EConstructor (validName, None)
            match eval Map.empty expr with
            | Ok (VConstructed (n, None)) -> n = validName
            | _ -> false
        else true  // Skip invalid names

    testProperty "Some x pattern extracts x" <| fun (n: int) ->
        let expr = EMatch (
            EConstructor ("Some", Some (ELiteral (LInt n))),
            [
                (PConstructor ("Some", Some (PVariable "x")), None, EVariable "x")
                (PWildcard, None, ELiteral (LInt 0))
            ])
        match eval Map.empty expr with
        | Ok (VInt m) -> m = n
        | _ -> false
]

// =============================================================================
// All User-Defined Type Tests
// =============================================================================

[<Tests>]
let allUserTypeTests = testList "User-Defined Types" [
    lexerTests
    astTests
    interpreterTests     // Enable after implementing interpreter
    // parserTests       // TODO: Enable after implementing parser
    // typeInferenceTests // TODO: Enable after implementing type inference
    // integrationTests  // TODO: Enable after full implementation
    propertyTests
]
