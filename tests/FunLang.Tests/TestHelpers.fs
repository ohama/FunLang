module FunLang.Tests.TestHelpers

open Expecto
open FunLang.Ast
open FunLang.Parser
open FunLang.Interpreter
open FunLang.ConstructorResolver
open FunLang.Types
open FunLang.TypeInfer

/// Helper to expect Result.Ok
let expectOk msg result =
    match result with
    | Ok v -> v
    | Error e -> failtest (sprintf "%s: %A" msg e)

/// Helper to expect Result.Error
let expectError msg result =
    match result with
    | Ok v -> failtest (sprintf "%s: expected error but got %A" msg v)
    | Error e -> e

// =============================================================================
// Tokenization Helpers
// =============================================================================

/// Tokenize a string (with indentation processing)
let tokenizeString (input: string) : Result<Token list, FunLang.Errors.FunLangError> =
    tokenize input

/// Tokenize a string (raw, without indentation processing)
let tokenizeStringRaw (input: string) : Result<Token list, FunLang.Errors.FunLangError> =
    tokenizeRaw input

// =============================================================================
// Parsing Helpers
// =============================================================================

/// Parse a string to an AST (main expression)
let parseStringToAst (input: string) : Result<Expr, string> =
    parseString input

/// Parse a string to a full Program (type defs + optional main expr)
let parseStringToProgram (input: string) : Result<Program, string> =
    parseProgramString input

// =============================================================================
// Evaluation Helpers
// =============================================================================

/// Run a string through the full pipeline (tokenize -> parse -> eval)
let runString (input: string) : Result<Value, string> =
    match parseString input with
    | Error e -> Error e
    | Ok ast ->
        match eval Map.empty ast with
        | Ok v -> Ok v
        | Error e -> Error e.Message

/// Run a program string through the full pipeline with type definitions
/// (tokenize -> parse program -> resolve constructors -> type check -> eval)
let runProgram (input: string) : Result<Value, string> =
    match parseProgramString input with
    | Error e -> Error e
    | Ok program ->
        // Resolve constructors based on type definitions
        let resolved = resolveProgram program
        match resolved.MainExpr with
        | None -> Error "No main expression in program"
        | Some ast ->
            // Build type definition environment
            let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv program.TypeDefs
            // Type check (optional, but good for catching errors early)
            match inferTypeWithTypeDefEnv typeDefEnv ast with
            | Error e -> Error (formatTypeError e)
            | Ok _ ->
                // Evaluate
                match eval Map.empty ast with
                | Ok v -> Ok v
                | Error e -> Error e.Message
