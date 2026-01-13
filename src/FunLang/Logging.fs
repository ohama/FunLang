module FunLang.Logging

open Serilog
open Serilog.Events

// =============================================================================
// Phase Types
// =============================================================================

type Phase =
    | Lexer
    | Parser
    | TypeCheck
    | Eval
    | Runtime

let phaseToString = function
    | Lexer -> "LEXER"
    | Parser -> "PARSER"
    | TypeCheck -> "TYPECHECK"
    | Eval -> "EVAL"
    | Runtime -> "RUNTIME"

// =============================================================================
// Run Options
// =============================================================================

type RunOptions = {
    Verbose: bool
    Debug: bool
    Interactive: bool
    LogLevel: LogEventLevel
    LogFile: string option
    ShowTokens: bool
    ShowAst: bool
    ShowTypes: bool
    ShowIndents: bool
    TracePhases: Set<Phase>
    NoColor: bool
    NoPrelude: bool
    EmitPath: string option option  // None = not used, Some None = stdout, Some (Some path) = file
}

let defaultOptions = {
    Verbose = false
    Debug = false
    Interactive = false
    LogLevel = LogEventLevel.Information
    LogFile = None
    ShowTokens = false
    ShowAst = false
    ShowTypes = false
    ShowIndents = false
    TracePhases = Set.empty
    NoColor = false
    NoPrelude = false
    EmitPath = None
}

// =============================================================================
// Logger State
// =============================================================================

let mutable private logger : ILogger option = None
let mutable private currentOptions : RunOptions option = None

// =============================================================================
// Initialization
// =============================================================================

let initialize (opts: RunOptions) =
    currentOptions <- Some opts

    let config =
        LoggerConfiguration()
            .MinimumLevel.Is(opts.LogLevel)
            .Enrich.WithProperty("Application", "FunLang")

    let config =
        config.WriteTo.Console(
            outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")

    let config =
        match opts.LogFile with
        | Some path -> config.WriteTo.File(path, rollingInterval = RollingInterval.Day)
        | None -> config

    logger <- Some (config.CreateLogger())

// =============================================================================
// Logging Functions
// =============================================================================

let private log level phase msg =
    match logger with
    | Some l -> l.Write(level, "[{Phase}] {Message}", phaseToString phase, msg)
    | None -> ()

let shouldTrace phase =
    match currentOptions with
    | Some opts -> opts.Debug || Set.contains phase opts.TracePhases
    | None -> false

let logDebug phase msg = log LogEventLevel.Debug phase msg
let logInfo phase msg = log LogEventLevel.Information phase msg
let logWarning phase msg = log LogEventLevel.Warning phase msg
let logError phase msg = log LogEventLevel.Error phase msg

/// Phase-specific trace logging (only logs if phase tracing is enabled)
let trace phase msg =
    if shouldTrace phase then
        log LogEventLevel.Debug phase msg

// =============================================================================
// Options Helpers
// =============================================================================

let getOptions () = currentOptions |> Option.defaultValue defaultOptions

let showTokens () =
    match currentOptions with
    | Some opts -> opts.ShowTokens || opts.Debug
    | None -> false

let showAst () =
    match currentOptions with
    | Some opts -> opts.ShowAst || opts.Debug
    | None -> false

let showTypes () =
    match currentOptions with
    | Some opts -> opts.ShowTypes || opts.Debug
    | None -> false

let showIndents () =
    match currentOptions with
    | Some opts -> opts.ShowIndents || opts.Debug
    | None -> false
