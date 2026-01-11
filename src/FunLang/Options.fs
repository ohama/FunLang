module FunLang.Options

open Argu
open Serilog.Events

// =============================================================================
// CLI Arguments
// =============================================================================

type CliArgs =
    | [<MainCommand; Unique>] File of path: string
    | [<AltCommandLine("-e"); Unique>] Expression of code: string
    | [<AltCommandLine("-i")>] Interactive
    | [<AltCommandLine("-v")>] Verbose
    | [<AltCommandLine("-d")>] Debug
    | Log_Level of level: string
    | Log_File of path: string
    | Show_Tokens
    | Show_Ast
    | Show_Types
    | Show_Indents
    | Trace of phases: string
    | No_Color
    | No_Prelude
    | Version

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | File _ -> "FunLang source file to execute"
            | Expression _ -> "Execute expression directly (e.g., -e '1 + 2')"
            | Interactive -> "Start interactive REPL mode"
            | Verbose -> "Enable verbose output"
            | Debug -> "Enable debug mode (all phases)"
            | Log_Level _ -> "Set log level (debug|info|warning|error)"
            | Log_File _ -> "Write logs to file"
            | Show_Tokens -> "Display lexer tokens"
            | Show_Ast -> "Display parsed AST"
            | Show_Types -> "Display inferred types"
            | Show_Indents -> "Display indentation tokens"
            | Trace _ -> "Trace specific phases (comma-separated: lexer,parser,typecheck,eval)"
            | No_Color -> "Disable colored output"
            | No_Prelude -> "Don't load standard prelude"
            | Version -> "Show version"

// =============================================================================
// Parse Options
// =============================================================================

let parseLogLevel (s: string) : LogEventLevel =
    match s.ToLowerInvariant() with
    | "debug" -> LogEventLevel.Debug
    | "info" | "information" -> LogEventLevel.Information
    | "warn" | "warning" -> LogEventLevel.Warning
    | "error" -> LogEventLevel.Error
    | "fatal" -> LogEventLevel.Fatal
    | _ -> LogEventLevel.Information

let parsePhase (s: string) : Logging.Phase option =
    match s.ToLowerInvariant() with
    | "lexer" -> Some Logging.Lexer
    | "parser" -> Some Logging.Parser
    | "typecheck" | "type" -> Some Logging.TypeCheck
    | "eval" | "interpreter" -> Some Logging.Eval
    | _ -> None

let parseOptions (results: ParseResults<CliArgs>) : Logging.RunOptions =
    let tracePhases =
        results.TryGetResult Trace
        |> Option.map (fun s -> s.Split(',') |> Array.toList)
        |> Option.defaultValue []
        |> List.choose parsePhase
        |> Set.ofList

    {
        Verbose = results.Contains Verbose
        Debug = results.Contains Debug
        Interactive = results.Contains Interactive
        LogLevel =
            results.TryGetResult Log_Level
            |> Option.map parseLogLevel
            |> Option.defaultValue (if results.Contains Debug then LogEventLevel.Debug else LogEventLevel.Fatal)
        LogFile = results.TryGetResult Log_File
        ShowTokens = results.Contains Show_Tokens
        ShowAst = results.Contains Show_Ast
        ShowTypes = results.Contains Show_Types
        ShowIndents = results.Contains Show_Indents
        TracePhases = tracePhases
        NoColor = results.Contains No_Color
        NoPrelude = results.Contains No_Prelude
    }

// =============================================================================
// Input Source
// =============================================================================

type InputSource =
    | FileInput of string
    | ExpressionInput of string
    | ReplMode
    | NoInput

let getInputSource (results: ParseResults<CliArgs>) : InputSource =
    match results.TryGetResult File, results.TryGetResult Expression with
    | Some path, _ -> FileInput path
    | None, Some expr -> ExpressionInput expr
    | None, None when results.Contains Interactive -> ReplMode
    | None, None -> NoInput
