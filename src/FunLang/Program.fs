module FunLang.Program

open System
open Argu
open FunLang.Ast
open FunLang.Lexer
open FunLang.Errors
open FunLang.Logging
open FunLang.Options

let version = "0.1.0"

/// Run the interpreter on input
let run (opts: RunOptions) (input: string) =
    logInfo Lexer "Starting tokenization"

    match tokenize input with
    | Ok tokens ->
        logInfo Lexer (sprintf "Tokenization complete: %d tokens" (List.length tokens))

        if opts.ShowTokens || opts.Debug then
            printfn "=== LEXER TOKENS ==="
            tokens |> List.iter (printfn "  %A")
            printfn "===================="

        // TODO: Parse and evaluate
        printfn "Tokens: %A" tokens
        0

    | Error e ->
        logError Lexer e.Message
        printfn "%s" (formatError e)
        1

/// Run REPL mode
let runRepl (opts: RunOptions) =
    printfn "FunLang Interactive Mode (v%s)" version
    printfn "Type :help for commands, :quit to exit"
    printfn ""

    let rec loop () =
        printf "fun> "
        match Console.ReadLine() with
        | null | ":quit" | ":q" ->
            printfn "Goodbye!"
            0
        | ":help" | ":h" ->
            printfn """
FunLang REPL Commands:
  :help, :h       Show this help
  :quit, :q       Exit REPL
  :tokens <expr>  Show tokens for expression
"""
            loop ()
        | input when input.StartsWith(":tokens ") ->
            let expr = input.Substring(8)
            ignore (run { opts with ShowTokens = true } expr)
            loop ()
        | "" ->
            loop ()
        | input ->
            ignore (run opts input)
            loop ()

    loop ()

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CliArgs>(programName = "funlang")

    try
        let results = parser.ParseCommandLine(argv)

        // Handle version flag
        if results.Contains Version then
            printfn "FunLang %s" version
            0
        else
            // Parse options and initialize logging
            let opts = parseOptions results
            initialize opts

            logInfo Runtime "FunLang starting"

            match getInputSource results with
            | FileInput path ->
                logInfo Runtime (sprintf "Reading file: %s" path)
                if IO.File.Exists(path) then
                    let content = IO.File.ReadAllText(path)
                    run opts content
                else
                    printfn "Error: File not found: %s" path
                    1

            | ExpressionInput expr ->
                logInfo Runtime (sprintf "Evaluating expression: %s" expr)
                run opts expr

            | ReplMode ->
                logInfo Runtime "Starting REPL mode"
                runRepl opts

            | NoInput ->
                // Show help if no input provided
                printfn "%s" (parser.PrintUsage())
                0

    with
    | :? ArguParseException as e ->
        printfn "%s" e.Message
        1
    | e ->
        printfn "Unexpected error: %s" e.Message
        1
