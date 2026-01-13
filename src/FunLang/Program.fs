module FunLang.Program

open System
open Argu
open FunLang.Ast
open FunLang.Parser
open FunLang.Interpreter
open FunLang.ConstructorResolver
open FunLang.Types
open FunLang.TypeInfer
open FunLang.Errors
open FunLang.Logging
open FunLang.Options
open FunLang.ErrorFormatter
open FunLang.PatternAnalysis

module Diag = FunLang.Diagnostic
module Fmt = FunLang.Formatter

let version = "0.1.0"

/// Display a FunLangError using Rust-style formatting
let displayError (source: string) (err: FunLangError) : unit =
    let diag = Diag.Diagnostic.fromFunLangError err
    eprintfn "%s" (format source defaultConfig diag)

/// Display a TypeError using Rust-style formatting
let displayTypeError (source: string) (err: TypeError) : unit =
    let diag = Diag.Diagnostic.fromTypeError err
    eprintfn "%s" (format source defaultConfig diag)

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
    | VConstructed (name, None) -> name
    | VConstructed (name, Some v) -> sprintf "%s %s" name (formatValue v)

/// Emit formatted source code
let emitFormatted (opts: RunOptions) (program: Ast.Program) : bool =
    match opts.EmitPath with
    | None -> false  // No emit requested
    | Some pathOpt ->
        let formatted = Fmt.formatProgram program
        match pathOpt with
        | None ->
            // stdout
            printfn "%s" formatted
        | Some path ->
            // file
            IO.File.WriteAllText(path, formatted)
            printfn "Formatted source written to: %s" path
        true  // Emit was performed

/// Check if input contains type definitions
let hasTypeDefinitions (input: string) =
    input.Contains("type ") && input.Contains(" = ")

/// Run the interpreter on input (expression only)
let runExpr (opts: RunOptions) (input: string) =
    logInfo Lexer "Starting tokenization"

    match tokenizeWithPositions input with
    | Error e ->
        logError Lexer e.Message
        displayError input e
        1
    | Ok tokensWithPos ->
        logInfo Lexer (sprintf "Tokenization complete: %d tokens" (List.length tokensWithPos))

        if opts.ShowTokens then
            printfn "=== LEXER TOKENS ==="
            tokensWithPos |> List.iter (fun (tok, pos) -> printfn "  [%d:%d] %A" pos.Line pos.Column tok)
            printfn "===================="
            0  // Stop here if --show-tokens
        else

        logInfo Parser "Starting parsing"

        match parseProgramWithPositions tokensWithPos with
        | Error e ->
            logError Parser e
            eprintfn "Parse error: %s" e
            1
        | Ok program ->
            match program.MainExpr with
            | None ->
                eprintfn "Parse error: No main expression in program"
                1
            | Some ast ->
                logInfo Parser "Parsing complete"

                // Handle --emit option (output formatted source and exit)
                if emitFormatted opts program then
                    0
                else

                if opts.ShowAst then
                    printfn "=== PARSED AST ==="
                    printfn "  %A" (Ast.Display.ofExpr ast)  // Show without Located wrappers
                    printfn "=================="
                    0  // Stop here if --show-ast
                else

                // Type check with pattern analysis (empty registry for built-in types only)
                logInfo TypeCheck "Starting type inference"
                match inferTypeWithWarnings Map.empty Map.empty ast with
                | Error e ->
                    logError TypeCheck (formatTypeError e)
                    displayTypeError input e
                    1
                | Ok (inferredType, warnings) ->
                    logInfo TypeCheck (sprintf "Type inference complete: %s" (formatType inferredType))

                    // Display pattern matching warnings
                    for warning in warnings do
                        eprintfn "%s" (formatWarning warning)

                    if opts.ShowTypes || opts.Debug then
                        printfn "=== INFERRED TYPE ==="
                        printfn "  %s" (formatType inferredType)
                        printfn "====================="

                    logInfo Eval "Starting evaluation"

                    match eval Map.empty ast with
                    | Error e ->
                        logError Eval e.Message
                        displayError input e
                        1
                    | Ok value ->
                        logInfo Eval (sprintf "Evaluation complete: %s" (formatValue value))
                        printfn "%s" (formatValue value)
                        0

/// Run the interpreter on input (program with type definitions)
let runProgram (opts: RunOptions) (input: string) =
    logInfo Lexer "Starting tokenization"

    match tokenizeWithPositions input with
    | Error e ->
        logError Lexer e.Message
        displayError input e
        1
    | Ok tokensWithPos ->
        logInfo Lexer (sprintf "Tokenization complete: %d tokens" (List.length tokensWithPos))

        if opts.ShowTokens then
            printfn "=== LEXER TOKENS ==="
            tokensWithPos |> List.iter (fun (tok, pos) -> printfn "  [%d:%d] %A" pos.Line pos.Column tok)
            printfn "===================="
            0  // Stop here if --show-tokens
        else

        logInfo Parser "Starting parsing (program mode)"

        match parseProgramWithPositions tokensWithPos with
        | Error e ->
            logError Parser e
            eprintfn "Parse error: %s" e
            1
        | Ok program ->
            logInfo Parser (sprintf "Parsing complete: %d type definitions" (List.length program.TypeDefs))

            // Resolve constructors
            let resolved = resolveProgram program

            match resolved.MainExpr with
            | None ->
                eprintfn "Error: No main expression in program"
                1
            | Some ast ->
                // Handle --emit option (output formatted source and exit)
                if emitFormatted opts resolved then
                    0
                else

                if opts.ShowAst then
                    printfn "=== PARSED AST ==="
                    printfn "  TypeDefs: %A" program.TypeDefs
                    printfn "  MainExpr: %A" (Ast.Display.ofExpr ast)  // Show without Located wrappers
                    printfn "=================="
                    0  // Stop here if --show-ast
                else

                // Build type definition environments
                let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv program.TypeDefs
                let registry = TypeDefRegistryBuilder.buildTypeDefRegistry program.TypeDefs

                // Type check with pattern analysis
                logInfo TypeCheck "Starting type inference"
                match inferTypeWithWarnings typeDefEnv registry ast with
                | Error e ->
                    logError TypeCheck (formatTypeError e)
                    displayTypeError input e
                    1
                | Ok (inferredType, warnings) ->
                    logInfo TypeCheck (sprintf "Type inference complete: %s" (formatType inferredType))

                    // Display pattern matching warnings
                    for warning in warnings do
                        eprintfn "%s" (formatWarning warning)

                    if opts.ShowTypes || opts.Debug then
                        printfn "=== INFERRED TYPE ==="
                        printfn "  %s" (formatType inferredType)
                        printfn "====================="

                    logInfo Eval "Starting evaluation"

                    match eval Map.empty ast with
                    | Error e ->
                        logError Eval e.Message
                        displayError input e
                        1
                    | Ok value ->
                        logInfo Eval (sprintf "Evaluation complete: %s" (formatValue value))
                        printfn "%s" (formatValue value)
                        0

/// Run the interpreter on input (auto-detect mode)
let run (opts: RunOptions) (input: string) =
    if hasTypeDefinitions input then
        runProgram opts input
    else
        runExpr opts input

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
            match tokenizeWithPositions expr with
            | Ok tokensWithPos -> tokensWithPos |> List.iter (fun (tok, pos) -> printfn "  [%d:%d] %A" pos.Line pos.Column tok)
            | Error e -> displayError expr e
            loop env
        | input when input.StartsWith ":ast " ->
            let expr = input.Substring 5
            match parseString expr with
            | Ok ast -> printfn "  %A" (Ast.Display.ofExpr ast)  // Show without Located wrappers
            | Error e -> eprintfn "Parse error: %s" e
            loop env
        | "" ->
            loop env
        | input ->
            match parseString input with
            | Error e ->
                eprintfn "Parse error: %s" e
                loop env
            | Ok ast ->
                    match eval env ast with
                    | Error e ->
                        displayError input e
                        loop env
                    | Ok value ->
                        printfn "%s" (formatValue value)
                        // For let bindings at top level, add to environment
                        match ast.Node with
                        | ELet (name, _, _) ->
                            loop (Map.add name value env)
                        | _ ->
                            loop env

    loop Map.empty

/// Handle --explain option
let handleExplain (codes: string) : int =
    if codes.ToLowerInvariant() = "all" then
        printfn "%s" (ErrorExplanations.formatAllCodes ())
        0
    else
        let codeList =
            codes.Split([|','; ' '|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList

        let (found, notFound) =
            codeList
            |> List.partition ErrorExplanations.hasExplanation

        // Print found explanations
        if not (List.isEmpty found) then
            printfn "%s" (ErrorExplanations.formatExplanations found)

        // Warn about unknown codes
        for code in notFound do
            eprintfn "Warning: Unknown error code '%s'" code

        if List.isEmpty found && not (List.isEmpty notFound) then 1 else 0

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CliArgs>(programName = "funlang")

    try
        let results = parser.ParseCommandLine argv

        // Handle --explain flag (before other processing)
        match results.TryGetResult Explain with
        | Some codes -> handleExplain codes
        | None ->

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
                    eprintfn "Error: File not found: %s" path
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
        eprintfn "Unexpected error: %s" e.Message
        1
