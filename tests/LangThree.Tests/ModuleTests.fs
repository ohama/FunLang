module LangThree.Tests.ModuleTests

open Expecto
open LangThree.IndentFilter

// Helper to lex and filter through IndentFilter
let lexAndFilter (input: string) =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let rec collect () =
        let tok = Lexer.tokenize lexbuf
        if tok = Parser.EOF then [Parser.EOF]
        else tok :: collect ()
    let rawTokens = collect ()
    filter defaultConfig rawTokens |> Seq.toList

// Helper to parse a module
let parseModule (input: string) : Ast.Module =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let filteredTokens = lexAndFilter input
    let mutable index = 0
    let tokenizer (lexbuf: FSharp.Text.Lexing.LexBuffer<_>) =
        if index < filteredTokens.Length then
            let tok = filteredTokens.[index]
            index <- index + 1
            tok
        else
            Parser.EOF
    Parser.parseModule tokenizer lexbuf

// Helper to parse and type check a module
let parseAndTypeCheck (input: string) =
    let m = parseModule input
    TypeCheck.typeCheckModule m

// Helper to parse, type check, and evaluate a module -- returns the value of the last let binding
let evalModule (input: string) : Ast.Value =
    let m = parseModule input
    match TypeCheck.typeCheckModule m with
    | Error diag -> failtest (sprintf "Type checking failed: %s" (Diagnostic.formatDiagnostic diag))
    | Ok (_warnings, recEnv, _modules, _typeEnv) ->
        let decls =
            match m with
            | Ast.Module(decls, _) | Ast.NamedModule(_, decls, _) | Ast.NamespacedModule(_, decls, _) -> decls
            | Ast.EmptyModule _ -> []
        let finalEnv, moduleEnv =
            Eval.evalModuleDecls recEnv Map.empty Eval.emptyEnv decls
        // Evaluate the last let binding's body
        match decls |> List.rev |> List.tryPick (function Ast.LetDecl(_, body, _) -> Some body | _ -> None) with
        | Some lastBody -> Eval.eval recEnv moduleEnv finalEnv lastBody
        | None -> failtest "No let binding found to evaluate"

// Helper to expect a type error with a specific code
let expectTypeError (input: string) (expectedCode: string) =
    let m = parseModule input
    match TypeCheck.typeCheckModule m with
    | Error diag ->
        match diag.Code with
        | Some code -> Expect.equal code expectedCode (sprintf "Expected error code %s" expectedCode)
        | None -> failtest (sprintf "Expected error code %s but got None. Message: %s" expectedCode diag.Message)
    | Ok _ -> failtest (sprintf "Expected type error %s but got Ok" expectedCode)

[<Tests>]
let moduleTests = testList "Modules" [

    // =====================================================
    // SC1: Top-level module and namespace declarations (MOD-01, MOD-02)
    // =====================================================

    testList "SC1: Top-level module/namespace" [
        test "top-level module declaration" {
            let result = evalModule "module MyModule\nlet x = 42\nlet result = x\n"
            Expect.equal result (Ast.IntValue 42) "top-level module binding"
        }

        test "top-level namespace declaration" {
            let result = evalModule "namespace MyApp.Utils\nlet x = 10\nlet result = x\n"
            Expect.equal result (Ast.IntValue 10) "namespace declaration"
        }

        test "top-level module with function" {
            let result = evalModule "module MyLib\nlet double x = x + x\nlet result = double 21\n"
            Expect.equal result (Ast.IntValue 42) "module with function"
        }
    ]

    // =====================================================
    // SC2: Nested modules with indentation (MOD-03)
    // =====================================================

    testList "SC2: Nested modules" [
        test "nested module declaration" {
            let result = evalModule "module Inner =\n    let x = 100\n\nlet result = Inner.x\n"
            Expect.equal result (Ast.IntValue 100) "nested module access"
        }

        test "nested module with function" {
            let result = evalModule "module Math =\n    let double x = x + x\n\nlet result = Math.double 21\n"
            Expect.equal result (Ast.IntValue 42) "nested module function"
        }

        test "multiple nested modules" {
            let result = evalModule "module A =\n    let x = 10\n\nmodule B =\n    let y = 20\n\nlet result = A.x + B.y\n"
            Expect.equal result (Ast.IntValue 30) "multiple nested modules"
        }
    ]

    // =====================================================
    // SC3: Open directive (MOD-04)
    // =====================================================

    testList "SC3: Open directive" [
        test "open module and use unqualified names" {
            let result = evalModule "module Utils =\n    let add x y = x + y\n\nopen Utils\nlet result = add 20 22\n"
            Expect.equal result (Ast.IntValue 42) "open for unqualified access"
        }

        test "open module with multiple bindings" {
            let result = evalModule "module M =\n    let a = 10\n    let b = 20\n\nopen M\nlet result = a + b\n"
            Expect.equal result (Ast.IntValue 30) "open with multiple bindings"
        }

        test "open then qualified access coexist" {
            let result = evalModule "module M =\n    let a = 1\n    let b = 2\n\nopen M\nlet result = a + M.b\n"
            Expect.equal result (Ast.IntValue 3) "open and qualified coexist"
        }
    ]

    // =====================================================
    // SC4: Qualified name access (MOD-05)
    // =====================================================

    testList "SC4: Qualified access" [
        test "qualified access to module member" {
            let result = evalModule "module Config =\n    let maxRetries = 3\n\nlet result = Config.maxRetries\n"
            Expect.equal result (Ast.IntValue 3) "qualified access"
        }

        test "qualified access with function application" {
            let result = evalModule "module StringUtils =\n    let greet name = name\n\nlet result = StringUtils.greet \"hello\"\n"
            Expect.equal result (Ast.StringValue "hello") "qualified function call"
        }
    ]

    // =====================================================
    // SC5: Forward reference error / circular dep proxy (E0504)
    // =====================================================

    testList "SC5: Forward reference prevents cycles" [
        test "forward module reference produces E0504" {
            expectTypeError "open NotYetDefined\n\nmodule NotYetDefined =\n    let x = 1\n\nlet result = x\n" "E0504"
        }

        test "forward reference in nested module produces E0504" {
            expectTypeError "module A =\n    open B\n    let x = 1\n\nmodule B =\n    let y = 2\n\nlet result = 0\n" "E0504"
        }

        test "valid top-to-bottom ordering succeeds" {
            let result = evalModule "module A =\n    let x = 1\n\nmodule B =\n    open A\n    let y = x + 1\n\nlet result = B.y\n"
            Expect.equal result (Ast.IntValue 2) "valid ordering works"
        }
    ]

    // =====================================================
    // Additional: ADT in modules, record regression, error cases
    // =====================================================

    testList "Module with ADT" [
        test "module with ADT types and qualified constructor" {
            let result = evalModule "module Shapes =\n    type Shape =\n        | Circle of int\n        | Square of int\n    let area s =\n        match s with\n        | Circle r -> r\n        | Square s -> s\n\nlet result = Shapes.area (Shapes.Circle 5)\n"
            Expect.equal result (Ast.IntValue 5) "ADT in module with qualified access"
        }
    ]

    testList "Record field access regression" [
        test "record field access still works with module system" {
            let result = evalModule "type Point = { x: int; y: int }\nlet p = { x = 10; y = 20 }\nlet result = p.x + p.y\n"
            Expect.equal result (Ast.IntValue 30) "record field access unaffected"
        }
    ]

    testList "Error cases" [
        test "duplicate module name produces E0503" {
            expectTypeError "module M =\n    let x = 1\n\nmodule M =\n    let y = 2\n\nlet result = 0\n" "E0503"
        }
    ]
]
