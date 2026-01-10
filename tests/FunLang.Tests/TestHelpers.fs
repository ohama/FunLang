module FunLang.Tests.TestHelpers

open Expecto
open FunLang.Ast
open FunLang.Parser
open FunLang.Interpreter

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

/// Parse a string to an AST
let parseStringToAst (input: string) : Result<Expr, string> =
    parseString input

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
