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
        | Some lastBody -> Eval.eval recEnv moduleEnv finalEnv false lastBody
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

// Helper to evaluate a .fun file by path (for file import tests).
// Sets currentTypeCheckingFile and currentEvalFile so resolveImportPath works correctly.
// Also triggers Prelude module initialization so the fileImportTypeChecker delegate is set.
let evalFileModule (filePath: string) : Ast.Value =
    // Access Prelude.emptyPrelude to trigger the Prelude module `do` block which
    // registers the fileImportTypeChecker and fileImportEvaluator delegates.
    let _initPrelude = Prelude.emptyPrelude
    let absPath = System.IO.Path.GetFullPath filePath
    let source = System.IO.File.ReadAllText absPath
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString source
    Lexer.setInitialPos lexbuf absPath
    let filteredTokens =
        let rec collect () =
            let tok = Lexer.tokenize lexbuf
            if tok = Parser.EOF then [Parser.EOF]
            else tok :: collect ()
        let rawTokens = collect ()
        filter defaultConfig rawTokens |> Seq.toList
    let mutable index = 0
    let tokenizer (lb: FSharp.Text.Lexing.LexBuffer<_>) =
        if index < filteredTokens.Length then
            let tok = filteredTokens.[index]
            index <- index + 1
            tok
        else Parser.EOF
    let lexbuf2 = FSharp.Text.Lexing.LexBuffer<_>.FromString source
    Lexer.setInitialPos lexbuf2 absPath
    let m = Parser.parseModule tokenizer lexbuf2
    // Set file path mutables so resolveImportPath works for relative file imports.
    // Save and restore to be safe in parallel test environments.
    let prevTCFile = TypeCheck.currentTypeCheckingFile
    let prevEvalFile = Eval.currentEvalFile
    TypeCheck.currentTypeCheckingFile <- absPath
    try
        match TypeCheck.typeCheckModule m with
        | Error diag -> failtest (sprintf "Type checking failed: %s" (Diagnostic.formatDiagnostic diag))
        | Ok (_warnings, recEnv, _modules, _typeEnv) ->
            let decls =
                match m with
                | Ast.Module(d, _) | Ast.NamedModule(_, d, _) | Ast.NamespacedModule(_, d, _) -> d
                | Ast.EmptyModule _ -> []
            Eval.currentEvalFile <- absPath
            let finalEnv, moduleEnv = Eval.evalModuleDecls recEnv Map.empty Eval.emptyEnv decls
            match decls |> List.rev |> List.tryPick (function Ast.LetDecl(_, body, _) -> Some body | _ -> None) with
            | Some body -> Eval.eval recEnv moduleEnv finalEnv false body
            | None -> Ast.TupleValue []
    finally
        TypeCheck.currentTypeCheckingFile <- prevTCFile
        Eval.currentEvalFile <- prevEvalFile

[<Tests>]
let fileImportTests = testSequenced (testList "File Import Tests" [

    // SC-1: Basic file import — imported bindings are accessible
    test "SC-1: basic file import — imported bindings accessible" {
        let tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())
        System.IO.Directory.CreateDirectory(tmpDir) |> ignore
        try
            let libPath = System.IO.Path.Combine(tmpDir, "lib.fun")
            let mainPath = System.IO.Path.Combine(tmpDir, "main.fun")
            System.IO.File.WriteAllText(libPath, "let greeting = \"hello\"\nlet add x y = x + y\n")
            System.IO.File.WriteAllText(mainPath, sprintf "open \"%s\"\nlet result = add 3 4\n" libPath)
            let result = evalFileModule mainPath
            Expect.equal result (Ast.IntValue 7) "imported add function returns 7"
        finally
            System.IO.Directory.Delete(tmpDir, true)
    }

    // SC-1 variant: imported binding used in expression
    test "SC-1 variant: imported binding used in expression" {
        let tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName())
        System.IO.Directory.CreateDirectory(tmpDir) |> ignore
        try
            let utilsPath = System.IO.Path.Combine(tmpDir, "utils.fun")
            let progPath = System.IO.Path.Combine(tmpDir, "prog.fun")
            System.IO.File.WriteAllText(utilsPath, "let double x = x * 2\n")
            System.IO.File.WriteAllText(progPath, sprintf "open \"%s\"\nlet result = double 5\n" utilsPath)
            let result = evalFileModule progPath
            Expect.equal result (Ast.IntValue 10) "imported double function returns 10"
        finally
            System.IO.Directory.Delete(tmpDir, true)
    }

    // SC-2: Multiple module declarations in one file (indentation-based, no 'end' keyword)
    test "SC-2: multiple module declarations coexist in one file" {
        let result = evalModule "module A =\n    let x = 10\n\nmodule B =\n    let y = 20\n\nopen A\nopen B\nlet result = x + y\n"
        Expect.equal result (Ast.IntValue 30) "bindings from A and B are accessible after open"
    }

    // SC-3: Qualified access to same type name in different modules (constructors differ)
    test "SC-3: qualified access to same type name in different modules" {
        let result = evalModule "module Lexer =\n    type Token = Ident of string | Num of int\n\nmodule Parser =\n    type Token = LParen | RParen | Atom of string\n\nlet t1 = Lexer.Ident \"hello\"\nlet t2 = Parser.LParen\nlet result = 1\n"
        Expect.equal result (Ast.IntValue 1) "same type name in different modules does not conflict"
    }

    // SC-4 / MOD-05: Record types in sibling modules with shared field names
    // Uses 'open M1' to bring M1.Tok into scope; the record literal {kind="id"; value=42}
    // resolves to M1.Tok (exact field set match) rather than M2.Item ({kind, count}).
    test "SC-4: record types in sibling modules with shared field names do not collide" {
        let result = evalModule "module M1 =\n    type Tok = { kind: string; value: int }\n\nmodule M2 =\n    type Item = { kind: string; count: int }\n\nopen M1\nlet t = { kind = \"id\"; value = 42 }\nlet result = t.value\n"
        Expect.equal result (Ast.IntValue 42) "sibling module record types with shared field names do not cause DuplicateFieldName"
    }
])

// Helper to eval with prelude loaded
let evalWithPrelude (input: string) : Ast.Value =
    let prelude = Prelude.loadPrelude()
    let m = parseModule input
    match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.TypeEnv prelude.Modules m with
    | Error diag -> failtest (sprintf "Type checking failed: %s" (Diagnostic.formatDiagnostic diag))
    | Ok (_warnings, _ctorEnv, recEnv, _modules, _typeEnv) ->
        let decls =
            match m with
            | Ast.Module(decls, _) | Ast.NamedModule(_, decls, _) | Ast.NamespacedModule(_, decls, _) -> decls
            | Ast.EmptyModule _ -> []
        let mergedRecEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.RecEnv recEnv
        let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.Env Eval.initialBuiltinEnv
        let finalEnv, moduleEnv =
            Eval.evalModuleDecls mergedRecEnv prelude.ModuleValueEnv initialEnv decls
        match decls |> List.rev |> List.tryPick (function Ast.LetDecl(_, body, _) -> Some body | _ -> None) with
        | Some lastBody -> Eval.eval mergedRecEnv moduleEnv finalEnv false lastBody
        | None -> failtest "No let binding found to evaluate"

[<Tests>]
let moduleBugFixTests = testList "SC-MOD-BUG: Module access bug fixes (Phase 36)" [

    testList "MOD-02: Prelude qualified access" [
        test "List.length via prelude qualified access" {
            let result = evalWithPrelude "let result = List.length [1; 2; 3]"
            Expect.equal result (Ast.IntValue 3) "List.length should return 3"
        }
        test "List.map via prelude qualified access" {
            let result = evalWithPrelude "let result = List.map (fun x -> x + 1) [1; 2; 3]"
            Expect.equal result (Ast.ListValue [Ast.IntValue 2; Ast.IntValue 3; Ast.IntValue 4]) "List.map should add 1 to each element"
        }
        test "unqualified map still works after prelude wrapping" {
            let result = evalWithPrelude "let result = map (fun x -> x * 2) [1; 2; 3]"
            Expect.equal result (Ast.ListValue [Ast.IntValue 2; Ast.IntValue 4; Ast.IntValue 6]) "unqualified map should still work"
        }
        test "unqualified length still works after prelude wrapping" {
            let result = evalWithPrelude "let result = length [1; 2; 3; 4]"
            Expect.equal result (Ast.IntValue 4) "unqualified length should still work"
        }
    ]

    testList "MOD-01: Imported file qualified access" [
        test "module function accessible via qualified access after open" {
            let tmpPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".fun")
            System.IO.File.WriteAllText(tmpPath, "module Math =\n    let square x = x * x\n")
            let code = sprintf "open \"%s\"\nlet result = Math.square 5" tmpPath
            let result = evalWithPrelude code
            System.IO.File.Delete(tmpPath)
            Expect.equal result (Ast.IntValue 25) "Math.square 5 should return 25"
        }
        test "multiple module functions accessible after open" {
            let tmpPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".fun")
            System.IO.File.WriteAllText(tmpPath, "module Util =\n    let double x = x * 2\n    let triple x = x * 3\n")
            let code = sprintf "open \"%s\"\nlet result = Util.double 4 + Util.triple 2" tmpPath
            let result = evalWithPrelude code
            System.IO.File.Delete(tmpPath)
            Expect.equal result (Ast.IntValue 14) "Util.double 4 + Util.triple 2 should be 14"
        }
    ]
]
