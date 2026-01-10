module FunLang.Program

open System
open Argu
open FunLang.Ast
open FunLang.Lexer
open FunLang.Parser
open FunLang.Interpreter
open FunLang.Errors
open FunLang.Logging
open FunLang.Options

let version = "0.1.0"

/// Format a value for display
let rec formatValue = function
    | VInt n -> string n
    | VBool b -> if b then "true" else "false"
    | VString s -> sprintf "\"%s\"" s
    | VUnit -> "()"
    | VTuple vs -> vs |> List.map formatValue |> String.concat ", " |> sprintf "(%s)"
    | VList vs -> vs |> List.map formatValue |> String.concat "; " |> sprintf "[%s]"
    | VClosure _ -> "<function>"
    | VRecClosure _ -> "<function>"

/// Run the interpreter on input
let run (opts: RunOptions) (input: string) =
    logInfo Lexer "Starting tokenization"

    match tokenize input with
    | Error e ->
        logError Lexer e.Message
        printfn "%s" (formatError e)
        1
    | Ok tokens ->
        logInfo Lexer (sprintf "Tokenization complete: %d tokens" (List.length tokens))

        if opts.ShowTokens || opts.Debug then
            printfn "=== LEXER TOKENS ==="
            tokens |> List.iter (printfn "  %A")
            printfn "===================="

        logInfo Parser "Starting parsing"

        match parse tokens with
        | Error e ->
            logError Parser e
            printfn "Parse error: %s" e
            1
        | Ok ast ->
            logInfo Parser "Parsing complete"

            if opts.ShowAst || opts.Debug then
                printfn "=== PARSED AST ==="
                printfn "  %A" ast
                printfn "=================="

            logInfo Eval "Starting evaluation"

            match eval Map.empty ast with
            | Error e ->
                logError Eval e.Message
                printfn "%s" (formatError e)
                1
            | Ok value ->
                logInfo Eval (sprintf "Evaluation complete: %s" (formatValue value))
                printfn "%s" (formatValue value)
                0

/// Run REPL mode
let runRepl (opts: RunOptions) =
    printfn "FunLang Interactive Mode (v%s)" version
    printfn "Type :help for commands, :quit to exit"
    printfn ""

    let rec loop env =
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
  :ast <expr>     Show AST for expression
  :env            Show current environment
  :clear          Clear environment
"""
            loop env
        | ":env" ->
            if Map.isEmpty env then
                printfn "(empty environment)"
            else
                env |> Map.iter (fun k v -> printfn "  %s = %s" k (formatValue v))
            loop env
        | ":clear" ->
            printfn "Environment cleared."
            loop Map.empty
        | input when input.StartsWith ":tokens " ->
            let expr = input.Substring 8
            match tokenize expr with
            | Ok tokens -> tokens |> List.iter (printfn "  %A")
            | Error e -> printfn "Error: %s" e.Message
            loop env
        | input when input.StartsWith ":ast " ->
            let expr = input.Substring 5
            match tokenize expr with
            | Error e -> printfn "Lexer error: %s" e.Message
            | Ok tokens ->
                match parse tokens with
                | Ok ast -> printfn "  %A" ast
                | Error e -> printfn "Parse error: %s" e
            loop env
        | "" ->
            loop env
        | input ->
            match tokenize input with
            | Error e ->
                printfn "Lexer error: %s" e.Message
                loop env
            | Ok tokens ->
                match parse tokens with
                | Error e ->
                    printfn "Parse error: %s" e
                    loop env
                | Ok ast ->
                    match eval env ast with
                    | Error e ->
                        printfn "Error: %s" e.Message
                        loop env
                    | Ok value ->
                        printfn "%s" (formatValue value)
                        // For let bindings at top level, add to environment
                        match ast with
                        | ELet (name, _, _) ->
                            loop (Map.add name value env)
                        | _ ->
                            loop env

    loop Map.empty

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CliArgs>(programName = "funlang")

    try
        let results = parser.ParseCommandLine argv

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
                if IO.File.Exists path then
                    let content = IO.File.ReadAllText path
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
