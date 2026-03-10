open System
open System.IO
open FSharp.Text.Lexing
open Argu
open Cli
open Ast
open Eval
open Format
open TypeCheck
open Diagnostic
open LangThree.IndentFilter

/// Parse a string input as expression (no indentation filtering needed for single-line expressions)
let parse (input: string) (filename: string) : Expr =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf filename
    Parser.start Lexer.tokenize lexbuf

/// Tokenize input and apply IndentFilter
let lexAndFilter (input: string) (filename: string) : Parser.token list =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf filename
    let rec collect () =
        let tok = Lexer.tokenize lexbuf
        if tok = Parser.EOF then [Parser.EOF]
        else tok :: collect ()
    let rawTokens = collect ()
    filter defaultConfig rawTokens |> Seq.toList

/// Parse a string input as module (with IndentFilter for indentation-based syntax)
let parseModuleFromString (input: string) (filename: string) : Module =
    let filteredTokens = lexAndFilter input filename
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf filename
    let mutable index = 0
    let tokenizer (_lexbuf: LexBuffer<_>) =
        if index < filteredTokens.Length then
            let tok = filteredTokens.[index]
            index <- index + 1
            tok
        else
            Parser.EOF
    Parser.parseModule tokenizer lexbuf

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CliArgs>(
        programName = "funlang",
        errorHandler = ProcessExiter(colorizer = function
            | ErrorCode.HelpText -> None
            | _ -> Some ConsoleColor.Red))

    try
        let results = parser.Parse(argv, raiseOnUsage = false)

        // Load prelude for evaluation modes
        let initialEnv = Prelude.loadPrelude()

        // Check if help was requested
        if results.IsUsageRequested then
            printfn "%s" (parser.PrintUsage())
            0
        // --repl flag
        elif results.Contains Repl then
            Repl.startRepl()
        // --emit-tokens with --expr
        elif results.Contains Emit_Tokens && results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let tokens = lex expr
                printfn "%s" (formatTokens tokens)
                0
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // --emit-ast with --expr
        elif results.Contains Emit_Ast && results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let ast = parse expr "<expr>"
                printfn "%s" (formatAst ast)
                0
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // --emit-type with --expr
        elif results.Contains Emit_Type && results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let ast = parse expr "<expr>"
                match typecheckWithDiagnostic ast with
                | Ok ty ->
                    printfn "%s" (Type.formatTypeNormalized ty)
                    0
                | Error diag ->
                    eprintfn "%s" (formatDiagnostic diag)
                    1
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // --emit-type with file (module pipeline)
        elif results.Contains Emit_Type && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let m = parseModuleFromString input filename
                    match TypeCheck.typeCheckModule m with
                    | Ok (warnings, _recEnv, _modules, typeEnv) ->
                        for w in warnings do
                            eprintfn "Warning: %s" (formatDiagnostic w)
                        // Print types of user-defined top-level bindings
                        let userBindings =
                            typeEnv
                            |> Map.filter (fun k _ -> not (Map.containsKey k TypeCheck.initialTypeEnv))
                        userBindings
                        |> Map.iter (fun name scheme ->
                            printfn "%s : %s" name (Type.formatSchemeNormalized scheme))
                        0
                    | Error diag ->
                        eprintfn "%s" (formatDiagnostic diag)
                        1
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --emit-tokens with file
        elif results.Contains Emit_Tokens && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let tokens = lex input
                    printfn "%s" (formatTokens tokens)
                    0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --emit-ast with file (module pipeline)
        elif results.Contains Emit_Ast && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let m = parseModuleFromString input filename
                    printfn "%s" (formatModule m)
                    0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --expr only
        elif results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let ast = parse expr "<expr>"
                // Type check first
                match typecheckWithDiagnostic ast with
                | Error diag ->
                    eprintfn "%s" (formatDiagnostic diag)
                    1
                | Ok _ ->
                    // Type check passed, evaluate
                    let result = eval Map.empty Map.empty initialEnv ast
                    printfn "%s" (formatValue result)
                    0
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // file only -- use module pipeline with IndentFilter
        elif results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let m = parseModuleFromString input filename
                    match TypeCheck.typeCheckModule m with
                    | Error diag ->
                        eprintfn "%s" (formatDiagnostic diag)
                        1
                    | Ok (warnings, recEnv, _modules, _typeEnv) ->
                        // Print any warnings (non-exhaustive matches, etc.)
                        for w in warnings do
                            eprintfn "Warning: %s" (formatDiagnostic w)
                        // Extract declarations from any module variant
                        let moduleDecls =
                            match m with
                            | Module (decls, _) | NamedModule(_, decls, _) | NamespacedModule(_, decls, _) -> decls
                            | EmptyModule _ -> []
                        // Evaluate module declarations with module-aware pipeline
                        let finalEnv, moduleEnv =
                            Eval.evalModuleDecls recEnv Map.empty initialEnv moduleDecls
                        // Print the last let binding's value
                        match moduleDecls |> List.rev |> List.tryPick (function LetDecl(_, body, _) -> Some body | _ -> None) with
                        | Some lastBody ->
                            let result = eval recEnv moduleEnv finalEnv lastBody
                            printfn "%s" (formatValue result)
                        | None -> ()
                        0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // no arguments - start REPL
        else
            Repl.startRepl()
    with
    | :? ArguParseException as ex ->
        eprintfn "%s" ex.Message
        1
