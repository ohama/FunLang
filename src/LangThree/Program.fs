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

        // Extract --prelude path before loading (must happen before loadPrelude)
        let preludePath =
            if results.Contains Prelude then Some (results.GetResult Prelude)
            else None

        // Load prelude from Prelude/*.fun directory
        let prelude = Prelude.loadPrelude(preludePath)
        let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.Env Eval.initialBuiltinEnv

        // Check if help was requested
        if results.IsUsageRequested then
            printfn "%s" (parser.PrintUsage())
            0
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
                    // Set current file path for FileImportDecl relative path resolution
                    TypeCheck.currentTypeCheckingFile <- System.IO.Path.GetFullPath filename
                    let m = parseModuleFromString input filename
                    match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.TypeEnv prelude.Modules m with
                    | Ok (warnings, _ctorEnv, _recEnv, _modules, typeEnv) ->
                        for w in warnings do
                            eprintfn "Warning: %s" (formatDiagnostic w)
                        // Print types of user-defined top-level bindings (exclude built-in and prelude)
                        let userBindings =
                            typeEnv
                            |> Map.filter (fun k _ -> not (Map.containsKey k TypeCheck.initialTypeEnv) && not (Map.containsKey k prelude.TypeEnv))
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
                    let result = eval Map.empty Map.empty initialEnv false ast
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
                    // Guard: empty or whitespace-only files produce no declarations
                    if System.String.IsNullOrWhiteSpace input then
                        printfn "()"
                        0
                    else
                    // Set current file path for FileImportDecl relative path resolution
                    let absFilename = System.IO.Path.GetFullPath filename
                    TypeCheck.currentTypeCheckingFile <- absFilename
                    Eval.currentEvalFile <- absFilename
                    // Populate scriptArgs: everything after the script filename in raw argv
                    let rawArgv = System.Environment.GetCommandLineArgs()
                    let idxInRaw = rawArgv |> Array.tryFindIndex (fun a -> a = absFilename || a = filename)
                    Eval.scriptArgs <-
                        match idxInRaw with
                        | Some i -> rawArgv |> Array.skip (i + 1) |> Array.toList
                        | None -> []
                    let m = parseModuleFromString input filename
                    match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.TypeEnv prelude.Modules m with
                    | Error diag ->
                        eprintfn "%s" (formatDiagnostic diag)
                        1
                    | Ok (warnings, _ctorEnv, recEnv, _modules, _typeEnv) ->
                        // Print any warnings (non-exhaustive matches, etc.)
                        for w in warnings do
                            eprintfn "Warning: %s" (formatDiagnostic w)
                        // Extract declarations from any module variant
                        let moduleDecls =
                            match m with
                            | Module (decls, _) | NamedModule(_, decls, _) | NamespacedModule(_, decls, _) -> decls
                            | EmptyModule _ -> []
                        // Guard: no declarations means nothing to evaluate
                        if List.isEmpty moduleDecls then
                            printfn "()"
                            0
                        else
                        // Evaluate module declarations with module-aware pipeline
                        let mergedRecEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.RecEnv recEnv
                        let finalEnv, moduleEnv =
                            Eval.evalModuleDecls mergedRecEnv prelude.ModuleValueEnv initialEnv moduleDecls
                        // Print the last let binding's value (look up from env to avoid re-evaluating side effects)
                        match moduleDecls |> List.rev |> List.tryPick (function LetDecl(name, _, _) -> Some name | _ -> None) with
                        | Some lastName ->
                            match Map.tryFind lastName finalEnv with
                            | Some result -> printfn "%s" (formatValue result)
                            | None -> ()
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
