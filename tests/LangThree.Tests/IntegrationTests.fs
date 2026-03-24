module LangThree.Tests.IntegrationTests

open Expecto
open LangThree.IndentFilter

// Helper to lex a string and get raw tokens
let lexString (input: string) =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let rec collect () =
        let tok = Lexer.tokenize lexbuf
        if tok = Parser.EOF then [Parser.EOF]
        else tok :: collect ()
    collect ()

// Helper to lex and filter through IndentFilter
let lexAndFilter (input: string) =
    let rawTokens = lexString input
    filter defaultConfig rawTokens |> Seq.toList

// Helper to parse a module (top-level declarations)
let parseModule (input: string) : Ast.Module =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"

    // Create a tokenizer function from filtered tokens
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

[<Tests>]
let integrationTests = testList "Integration" [
    test "simple let with indent produces INDENT" {
        let input = "let x =\n    42"
        let tokens = lexAndFilter input
        Expect.contains tokens Parser.INDENT "Should have INDENT"
        Expect.contains tokens (Parser.NUMBER 42) "Should have NUMBER 42"
    }

    test "tab character raises error" {
        Expect.throws
            (fun () -> lexString "let x =\n\t42" |> ignore)
            "Tab should raise error"
    }

    test "let with dedent back to column 0" {
        let input = "let x =\n    42\nlet y = 1"
        let tokens = lexAndFilter input
        Expect.contains tokens Parser.INDENT "Should have INDENT"
        Expect.contains tokens Parser.DEDENT "Should have DEDENT"
    }

    // Phase 1 (INDENT-05): Module-level declarations tests
    test "debug tokens for simple module" {
        let input = "let x = 42\n"
        let tokens = lexAndFilter input
        printfn "Tokens for 'let x = 42\\n': %A" tokens
        Expect.isTrue true "Debug test"
    }

    test "debug tokens for testModuleLevelWithIndentedBodies" {
        let input = "let x =\n    let a = 1 in\n    let b = 2 in\n    a + b\n\nlet y =\n    x * 2\n"
        let tokens = lexAndFilter input
        printfn "Tokens for indented bodies: %A" tokens
        Expect.isTrue true "Debug test"
    }

    test "debug tokens for testModuleWithVariousExprTypes" {
        let input = "let id x = x\n\nlet const42 =\n    42\n\nlet app = id const42\n"
        let tokens = lexAndFilter input
        printfn "Tokens for various expr types: %A" tokens
        Expect.isTrue true "Debug test"
    }

    test "testModuleLevelDeclarations" {
        let input = "let x = 42\nlet y = 10\nlet z = x + y\n"
        let result = parseModule input
        match result with
        | Ast.Module(decls, _) ->
            Expect.equal (List.length decls) 3 "Should have 3 declarations"
            match decls with
            | [Ast.LetDecl("x", Ast.Number(42, _), _);
               Ast.LetDecl("y", Ast.Number(10, _), _);
               Ast.LetDecl("z", Ast.Add(Ast.Var("x", _), Ast.Var("y", _), _), _)] ->
                ()
            | _ -> failtest "Declarations should match expected structure"
        | Ast.EmptyModule _ -> failtest "Should not be empty module"
    }

    test "testModuleLevelWithIndentedBodies" {
        let input = "let x =\n    let a = 1 in\n    let b = 2 in\n    a + b\n\nlet y =\n    x * 2\n"
        let result = parseModule input
        match result with
        | Ast.Module(decls, _) ->
            Expect.equal (List.length decls) 2 "Should have 2 declarations"
            match decls with
            | [Ast.LetDecl("x", _, _); Ast.LetDecl("y", _, _)] ->
                ()
            | _ -> failtest "Should have two let declarations"
        | Ast.EmptyModule _ -> failtest "Should not be empty module"
    }

    test "testEmptyModule" {
        let input = ""
        let result = parseModule input
        match result with
        | Ast.EmptyModule _ -> ()
        | Ast.Module _ -> failtest "Empty input should parse as EmptyModule"
    }

    test "testModuleWithVariousExprTypes" {
        let input = "let id x = x\n\nlet const42 =\n    42\n\nlet app = id const42\n"
        let result = parseModule input
        match result with
        | Ast.Module(decls, _) ->
            Expect.equal (List.length decls) 3 "Should have 3 declarations"
            match decls with
            | [Ast.LetDecl("id", Ast.Lambda(_, _, _), _);
               Ast.LetDecl("const42", Ast.Number(42, _), _);
               Ast.LetDecl("app", Ast.App(_, _, _), _)] ->
                ()
            | _ -> failtest "Declarations should have lambda, number, and application"
        | Ast.EmptyModule _ -> failtest "Should not be empty module"
    }

    // Phase 1 (01-02): Multi-line function application tests
    test "debug multi-line tokens" {
        let input = "let result = add\n    10\n    20\n"
        let tokens = lexAndFilter input
        printfn "Tokens: %A" tokens
        Expect.isTrue true "Debug test"
    }

    test "testMultiLineFunctionApp" {
        let input = "let add = fun x -> fun y -> x + y\nlet result = add\n    10\n    20\n"
        let result = parseModule input
        match result with
        | Ast.Module(decls, _) ->
            Expect.equal (List.length decls) 2 "Should have 2 declarations"
            match decls with
            | [Ast.LetDecl("add", _, _); Ast.LetDecl("result", appExpr, _)] ->
                // Verify the result is an App expression
                match appExpr with
                | Ast.App(Ast.App(Ast.Var("add", _), Ast.Number(10, _), _), Ast.Number(20, _), _) ->
                    ()
                | _ -> failtest $"Expected App(App(Var add, 10), 20), got: %A{appExpr}"
            | _ -> failtest "Should have add and result declarations"
        | Ast.EmptyModule _ -> failtest "Should not be empty module"
    }

    test "testMultiLineFunctionAppWithComplexArgs" {
        let input = "let f = fun x -> fun y -> fun z -> x + y * z\nlet result = f\n    (1 + 2)\n    (3 + 4)\n    (5 + 6)\n"
        let result = parseModule input
        match result with
        | Ast.Module(decls, _) ->
            Expect.equal (List.length decls) 2 "Should have 2 declarations"
            match decls with
            | [Ast.LetDecl("f", _, _); Ast.LetDecl("result", appExpr, _)] ->
                // Verify the result is an App expression with complex args
                match appExpr with
                | Ast.App(Ast.App(Ast.App(Ast.Var("f", _), Ast.Add _, _), Ast.Add _, _), Ast.Add _, _) ->
                    ()
                | _ -> failtest $"Expected nested App with Add args, got: %A{appExpr}"
            | _ -> failtest "Should have f and result declarations"
        | Ast.EmptyModule _ -> failtest "Should not be empty module"
    }

    test "testCurriedMultiLineApp" {
        let input = "let add = fun x -> fun y -> x + y\nlet addTen = add\n    10\nlet result = addTen 5\n"
        let result = parseModule input
        match result with
        | Ast.Module(decls, _) ->
            Expect.equal (List.length decls) 3 "Should have 3 declarations"
            match decls with
            | [Ast.LetDecl("add", _, _);
               Ast.LetDecl("addTen", Ast.App(Ast.Var("add", _), Ast.Number(10, _), _), _);
               Ast.LetDecl("result", Ast.App(Ast.Var("addTen", _), Ast.Number(5, _), _), _)] ->
                ()
            | _ -> failtest "Should have add, addTen, and result declarations with correct structure"
        | Ast.EmptyModule _ -> failtest "Should not be empty module"
    }

    // Phase 2 (ADT-01): Type declaration parsing tests
    // Helper to extract TypeDecl details from a Decl
    let extractTypeDecl (d: Ast.Decl) =
        match d with
        | Ast.Decl.TypeDecl(Ast.TypeDecl(name, tparams, ctors, _)) -> (name, tparams, ctors)
        | _ -> failwith "Expected TypeDecl"

    test "testParseSimpleADT" {
        let input = "type Color = Red | Green | Blue\n"
        let result = parseModule input
        match result with
        | Ast.Module([d], _) ->
            let (name, tparams, ctors) = extractTypeDecl d
            Expect.equal name "Color" "Type name should be Color"
            Expect.equal tparams [] "Should have no type params"
            Expect.equal (List.length ctors) 3 "Should have 3 constructors"
            match ctors with
            | [Ast.ConstructorDecl("Red", None, _)
               Ast.ConstructorDecl("Green", None, _)
               Ast.ConstructorDecl("Blue", None, _)] -> ()
            | _ -> failtest (sprintf "Unexpected constructors: %A" ctors)
        | _ -> failtest (sprintf "Expected simple ADT, got: %A" result)
    }

    test "testParseADTWithData" {
        let input = "type Option = None | Some of int\n"
        let result = parseModule input
        match result with
        | Ast.Module([d], _) ->
            let (name, _, ctors) = extractTypeDecl d
            Expect.equal name "Option" "Type name should be Option"
            match ctors with
            | [Ast.ConstructorDecl("None", None, _)
               Ast.ConstructorDecl("Some", Some(Ast.TEInt), _)] -> ()
            | _ -> failtest (sprintf "Unexpected constructors: %A" ctors)
        | _ -> failtest (sprintf "Expected ADT with data, got: %A" result)
    }

    test "testParseADTWithTypeParam" {
        let input = "type Option 'a = None | Some of 'a\n"
        let result = parseModule input
        match result with
        | Ast.Module([d], _) ->
            let (name, tparams, ctors) = extractTypeDecl d
            Expect.equal name "Option" "Type name should be Option"
            Expect.equal tparams ["'a"] "Should have type param 'a"
            match ctors with
            | [Ast.ConstructorDecl("None", None, _)
               Ast.ConstructorDecl("Some", Some(Ast.TEVar "'a"), _)] -> ()
            | _ -> failtest (sprintf "Unexpected constructors: %A" ctors)
        | _ -> failtest (sprintf "Expected ADT with type param, got: %A" result)
    }

    test "testParseRecursiveADT" {
        let input = "type Tree = Leaf | Node of Tree * int * Tree\n"
        let result = parseModule input
        match result with
        | Ast.Module([d], _) ->
            let (name, _, ctors) = extractTypeDecl d
            Expect.equal name "Tree" "Type name should be Tree"
            match ctors with
            | [Ast.ConstructorDecl("Leaf", None, _)
               Ast.ConstructorDecl("Node", Some(Ast.TETuple _), _)] -> ()
            | _ -> failtest (sprintf "Unexpected constructors: %A" ctors)
        | _ -> failtest (sprintf "Expected recursive ADT, got: %A" result)
    }

    test "testParseMutuallyRecursiveADT" {
        let input = "type Expr = Lit of int | Arith of ArithExpr\nand ArithExpr = Add of Expr * Expr\n"
        let result = parseModule input
        match result with
        | Ast.Module(decls, _) ->
            Expect.equal (List.length decls) 2 "Should have 2 type declarations"
            let (name1, _, _) = extractTypeDecl decls.[0]
            let (name2, _, _) = extractTypeDecl decls.[1]
            Expect.equal name1 "Expr" "First type should be Expr"
            Expect.equal name2 "ArithExpr" "Second type should be ArithExpr"
        | _ -> failtest (sprintf "Expected module with decls, got: %A" result)
    }

    test "testParseADTWithLeadingPipe" {
        let input = "type Color =\n    | Red\n    | Green\n    | Blue\n"
        let result = parseModule input
        match result with
        | Ast.Module([d], _) ->
            let (name, _, ctors) = extractTypeDecl d
            Expect.equal name "Color" "Type name should be Color"
            Expect.equal (List.length ctors) 3 "Should have 3 constructors"
        | _ -> failtest (sprintf "Expected ADT with leading pipe, got: %A" result)
    }

    // Phase 2 (ADT-02): Type elaboration tests
    test "testElaborateSimpleADT" {
        // type Bool = True | False
        let decl = Ast.TypeDecl("Bool", [], [
            Ast.ConstructorDecl("True", None, Ast.unknownSpan)
            Ast.ConstructorDecl("False", None, Ast.unknownSpan)
        ], Ast.unknownSpan)

        let ctorEnv = Elaborate.elaborateTypeDecl decl

        // Check True constructor
        match Map.tryFind "True" ctorEnv with
        | Some { TypeParams = []; ArgType = None; ResultType = Type.TData("Bool", []) } -> ()
        | other -> failtest (sprintf "True constructor type mismatch: %A" other)

        // Check False constructor
        match Map.tryFind "False" ctorEnv with
        | Some { TypeParams = []; ArgType = None; ResultType = Type.TData("Bool", []) } -> ()
        | other -> failtest (sprintf "False constructor type mismatch: %A" other)
    }

    test "testElaborateParametricADT" {
        // type Option 'a = None | Some of 'a
        let decl = Ast.TypeDecl("Option", ["'a"], [
            Ast.ConstructorDecl("None", None, Ast.unknownSpan)
            Ast.ConstructorDecl("Some", Some(Ast.TEVar "'a"), Ast.unknownSpan)
        ], Ast.unknownSpan)

        let ctorEnv = Elaborate.elaborateTypeDecl decl

        // Check None
        match Map.tryFind "None" ctorEnv with
        | Some { TypeParams = [0]; ArgType = None; ResultType = Type.TData("Option", [Type.TVar 0]) } -> ()
        | other -> failtest (sprintf "None type mismatch: %A" other)

        // Check Some
        match Map.tryFind "Some" ctorEnv with
        | Some { TypeParams = [0]; ArgType = Some(Type.TVar 0); ResultType = Type.TData("Option", [Type.TVar 0]) } -> ()
        | other -> failtest (sprintf "Some type mismatch: %A" other)
    }

    test "testElaborateRecursiveADT" {
        // type IntList = Nil | Cons of int * IntList
        let decl = Ast.TypeDecl("IntList", [], [
            Ast.ConstructorDecl("Nil", None, Ast.unknownSpan)
            Ast.ConstructorDecl("Cons", Some(Ast.TETuple [Ast.TEInt; Ast.TEName "IntList"]), Ast.unknownSpan)
        ], Ast.unknownSpan)

        let ctorEnv = Elaborate.elaborateTypeDecl decl

        // Check Nil
        match Map.tryFind "Nil" ctorEnv with
        | Some { ArgType = None; ResultType = Type.TData("IntList", []) } -> ()
        | other -> failtest (sprintf "Nil type mismatch: %A" other)

        // Check Cons has tuple argument with int and TData("IntList", [])
        match Map.tryFind "Cons" ctorEnv with
        | Some { ArgType = Some(Type.TTuple [Type.TInt; Type.TData("IntList", [])]); ResultType = Type.TData("IntList", []) } -> ()
        | other -> failtest (sprintf "Cons type mismatch: %A" other)
    }

    // Phase 2 (ADT-03): Constructor pattern type checking tests
    // Helper to parse and type check a module
    let parseAndTypeCheck (input: string) =
        let m = parseModule input
        TypeCheck.typeCheckModule m

    test "testMatchOnSimpleADT" {
        let input = "type Color = Red | Green | Blue\n\nlet toInt c =\n    match c with\n    | Red -> 1\n    | Green -> 2\n    | Blue -> 3\n"
        let result = parseAndTypeCheck input
        match result with
        | Ok _ -> ()
        | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
    }

    test "testMatchOnParametricADT" {
        let input = "type Option 'a = None | Some of 'a\n\nlet getOrZero opt =\n    match opt with\n    | None -> 0\n    | Some x -> x\n"
        let result = parseAndTypeCheck input
        match result with
        | Ok _ -> ()
        | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
    }

    test "testMatchWithNestedConstructors" {
        let input = "type Option 'a = None | Some of 'a\n\nlet unwrapTwice opt =\n    match opt with\n    | None -> 0\n    | Some (Some x) -> x\n    | Some None -> 0\n"
        let result = parseAndTypeCheck input
        match result with
        | Ok _ -> ()
        | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
    }

    test "testConstructorArityMismatch" {
        let input = "type Option 'a = None | Some of 'a\n\nlet bad opt =\n    match opt with\n    | None x -> 1\n    | Some -> 0\n"
        let result = parseAndTypeCheck input
        match result with
        | Error _ -> ()  // Should fail with arity mismatch
        | Ok _ -> failtest "Expected type error for arity mismatch"
    }

    // Phase 2 (ADT): Helper to parse and evaluate a module, returning final env
    // Skips type checking since ADT type infrastructure is not yet complete
    let parseAndEvalModule (input: string) : Ast.Env =
        let m = parseModule input
        match m with
        | Ast.Module(decls, _) ->
            decls |> List.fold (fun env decl ->
                match decl with
                | Ast.LetDecl(name, body, _) ->
                    let value = Eval.eval Map.empty Map.empty env false body
                    Map.add name value env) Eval.emptyEnv
        | Ast.EmptyModule _ -> Eval.emptyEnv

    // Phase 2 (02-05): ADT evaluation integration tests

    test "testADTNullaryConstructor" {
        let input = "let x = None\n"
        let env = parseAndEvalModule input
        match Map.tryFind "x" env with
        | Some (Ast.DataValue ("None", None)) -> ()
        | other -> failtest $"Expected DataValue(None, None), got: %A{other}"
    }

    test "testADTConstructorWithArg" {
        let input = "let x = Some 42\n"
        let env = parseAndEvalModule input
        match Map.tryFind "x" env with
        | Some (Ast.DataValue ("Some", Some (Ast.IntValue 42))) -> ()
        | other -> failtest $"Expected DataValue(Some, IntValue 42), got: %A{other}"
    }

    test "testADTPatternMatchingNullary" {
        let input = "let x = None\nlet result =\n    match x with\n    | None -> 0\n    | Some y -> y\n"
        let env = parseAndEvalModule input
        match Map.tryFind "result" env with
        | Some (Ast.IntValue 0) -> ()
        | other -> failtest $"Expected IntValue 0, got: %A{other}"
    }

    test "testADTPatternMatchingWithData" {
        let input = "let x = Some 42\nlet result =\n    match x with\n    | None -> 0\n    | Some y -> y\n"
        let env = parseAndEvalModule input
        match Map.tryFind "result" env with
        | Some (Ast.IntValue 42) -> ()
        | other -> failtest $"Expected IntValue 42, got: %A{other}"
    }

    test "testADTRecursiveTreeConstruction" {
        // Build a tree and pattern match one level
        let input = "let tree = Node (Leaf, 10, Leaf)\nlet result =\n    match tree with\n    | Leaf -> 0\n    | Node (left, value, right) -> value\n"
        let env = parseAndEvalModule input
        match Map.tryFind "result" env with
        | Some (Ast.IntValue 10) -> ()
        | other -> failtest $"Expected IntValue 10, got: %A{other}"
    }

    test "testADTRecursiveTreeEval" {
        // Build a deeper tree and use let rec to sum it
        let input = "let tree = Node (Leaf, 10, Node (Leaf, 20, Leaf))\nlet result = let rec sumTree t = match t with | Leaf -> 0 | Node (left, value, right) -> sumTree left + value + sumTree right in sumTree tree\n"
        let env = parseAndEvalModule input
        match Map.tryFind "result" env with
        | Some (Ast.IntValue 30) -> ()
        | other -> failtest $"Expected IntValue 30, got: %A{other}"
    }

    test "testADTFormatValue" {
        let none = Ast.DataValue("None", None)
        let some42 = Ast.DataValue("Some", Some (Ast.IntValue 42))
        let leaf = Ast.DataValue("Leaf", None)
        Expect.equal (Eval.formatValue none) "None" "Nullary constructor format"
        Expect.equal (Eval.formatValue some42) "Some 42" "Constructor with int arg format"
        Expect.equal (Eval.formatValue leaf) "Leaf" "Nullary constructor format"
    }

    test "testADTNestedConstructors" {
        // Test nested constructor: Some (Some 1) - extract inner value via single-line match
        let input = "let x = Some (Some 1)\nlet result = match x with | None -> 0 | Some inner -> match inner with | None -> 0 | Some n -> n\n"
        let env = parseAndEvalModule input
        match Map.tryFind "result" env with
        | Some (Ast.IntValue 1) -> ()
        | other -> failtest $"Expected IntValue 1, got: %A{other}"
    }

    // Phase 2 (02-06): Exhaustiveness and redundancy warning integration tests

    test "testExhaustivenessWarningMissingCase" {
        let input = "type Option 'a = None | Some of 'a\n\nlet f x =\n    match x with\n    | Some y -> y\n"
        let result = parseAndTypeCheck input
        match result with
        | Ok (warnings, _, _, _) ->
            Expect.isNonEmpty warnings "Should have exhaustiveness warning"
            let hasW0001 = warnings |> List.exists (fun d -> d.Code = Some "W0001")
            Expect.isTrue hasW0001 "Should have W0001 warning code"
            let w = warnings |> List.find (fun d -> d.Code = Some "W0001")
            Expect.stringContains w.Message "None" "Warning should mention missing 'None' case"
        | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
    }

    test "testExhaustivenessNoWarningComplete" {
        let input = "type Option 'a = None | Some of 'a\n\nlet f x =\n    match x with\n    | None -> 0\n    | Some y -> y\n"
        let result = parseAndTypeCheck input
        match result with
        | Ok (warnings, _, _, _) ->
            Expect.isEmpty warnings "Complete match should have no warnings"
        | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
    }

    test "testRedundancyWarning" {
        let input = "type Option 'a = None | Some of 'a\n\nlet f x =\n    match x with\n    | None -> 0\n    | Some y -> y\n    | None -> 2\n"
        let result = parseAndTypeCheck input
        match result with
        | Ok (warnings, _, _, _) ->
            let hasW0002 = warnings |> List.exists (fun d -> d.Code = Some "W0002")
            Expect.isTrue hasW0002 "Should have W0002 redundancy warning"
        | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
    }

    test "testExhaustivenessWarningTree" {
        let input = "type Tree = Leaf | Node of Tree\n\nlet f t =\n    match t with\n    | Leaf -> 0\n"
        let result = parseAndTypeCheck input
        match result with
        | Ok (warnings, _, _, _) ->
            Expect.isNonEmpty warnings "Should have exhaustiveness warning for Tree"
            let w = warnings |> List.find (fun d -> d.Code = Some "W0001")
            Expect.stringContains w.Message "Node" "Warning should mention missing 'Node' case"
        | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
    }

    test "testMixedSingleAndMultiLine" {
        let input = "let f = fun x -> fun y -> fun z -> x + y + z\nlet result = f 1\n    2\n    3\n"
        let result = parseModule input
        match result with
        | Ast.Module(decls, _) ->
            Expect.equal (List.length decls) 2 "Should have 2 declarations"
            match decls with
            | [Ast.LetDecl("f", _, _); Ast.LetDecl("result", appExpr, _)] ->
                // The "f 1" is on the same line, then args 2 and 3 are indented
                // This should parse as: App(App(App(f, 1), 2), 3)
                match appExpr with
                | Ast.App(Ast.App(Ast.App(Ast.Var("f", _), Ast.Number(1, _), _), Ast.Number(2, _), _), Ast.Number(3, _), _) ->
                    ()
                | _ -> failtest $"Expected App(App(App(f, 1), 2), 3), got: %A{appExpr}"
            | _ -> failtest "Should have f and result declarations"
        | Ast.EmptyModule _ -> failtest "Should not be empty module"
    }
]

// Helper to parse, type-check, and evaluate a module returning the last let binding value
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
        match decls |> List.rev |> List.tryPick (function Ast.LetDecl(_, body, _) -> Some body | _ -> None) with
        | Some lastBody -> Eval.eval recEnv moduleEnv finalEnv false lastBody
        | None -> failtest "No let binding found to evaluate"

[<Tests>]
let syn0106070810Tests = testList "SYN-01/06/07/08: Parser improvements" [

    // SYN-01: Local let rec single-param inside function body
    test "local let rec single-param works inside function body" {
        let src = "let countdown =\n    let rec loop n =\n        if n <= 0 then 0\n        else loop (n - 1)\n    in loop 5\nlet result = countdown\n"
        let result = evalModule src
        Expect.equal result (Ast.IntValue 0) "countdown should reach 0"
    }

    // SYN-01/SYN-06: Local let rec multi-param inside function body
    test "local let rec multi-param works inside function body" {
        let src = "let result =\n    let rec add a b =\n        if b = 0 then a\n        else add (a + 1) (b - 1)\n    in add 3 4\n"
        let result = evalModule src
        Expect.equal result (Ast.IntValue 7) "add 3 4 should equal 7"
    }

    // SYN-01: Recursive call inside lambda body (trampoline regression)
    test "local let rec actually recurses correctly inside lambda" {
        let src = "let fact n =\n    let rec helper acc k =\n        if k <= 1 then acc\n        else helper (acc * k) (k - 1)\n    in helper 1 n\nlet result = fact 5\n"
        let result = evalModule src
        Expect.equal result (Ast.IntValue 120) "fact 5 should equal 120"
    }

    // SYN-07: Unit param shorthand at module level
    test "let f () = body works at module level" {
        let src = "let greet () = 42\nlet result = greet ()\n"
        let result = evalModule src
        Expect.equal result (Ast.IntValue 42) "greet () should return 42"
    }

    // SYN-07: Unit param shorthand in expression context
    test "let f () = body works in expression context" {
        let src = "let result =\n    let f () = 42\n    in f ()\n"
        let result = evalModule src
        Expect.equal result (Ast.IntValue 42) "f () should return 42"
    }

    // SYN-08: Top-level let...in
    test "top-level let x = e1 in e2 works as module declaration" {
        let src = "let base = 10\nlet result = let x = base + 5 in x * 2\n"
        let result = evalModule src
        Expect.equal result (Ast.IntValue 30) "let x = 15 in x * 2 should equal 30"
    }

    // SYN-06: 4-level let nesting parses and evaluates correctly
    test "4 levels of let nesting parse and evaluate correctly" {
        let src = "let result =\n    let a = 1 in\n    let b = a + 1 in\n    let c = b + 1 in\n    let d = c + 1 in\n    d\n"
        let result = evalModule src
        Expect.equal result (Ast.IntValue 4) "4-level nesting should produce 4"
    }
]
