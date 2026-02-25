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

/// Parse a string input and return the AST
let parse (input: string) (filename: string) : Expr =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf filename
    Parser.start Lexer.tokenize lexbuf

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
        // --emit-type with file
        elif results.Contains Emit_Type && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let ast = parse input filename
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
        // --emit-ast with file
        elif results.Contains Emit_Ast && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let ast = parse input filename
                    printfn "%s" (formatAst ast)
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
                    let result = eval initialEnv ast
                    printfn "%s" (formatValue result)
                    0
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // file only
        elif results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let ast = parse input filename
                    // Type check first
                    match typecheckWithDiagnostic ast with
                    | Error diag ->
                        eprintfn "%s" (formatDiagnostic diag)
                        1
                    | Ok _ ->
                        // Type check passed, evaluate
                        let result = eval initialEnv ast
                        printfn "%s" (formatValue result)
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
