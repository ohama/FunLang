module Prelude

open System.IO
open FSharp.Text.Lexing
open Ast
open Eval

/// Parse a string into an AST expression
let private parse (input: string) : Expr =
    let lexbuf = LexBuffer<char>.FromString(input)
    Lexer.setInitialPos lexbuf "Prelude.fun"
    Parser.start Lexer.tokenize lexbuf

/// Recursively collect let/let rec bindings into environment
/// Handles nested let-in structure: let x = ... in let y = ... in ... in ()
let rec private evalToEnv (env: Env) (expr: Expr) : Env =
    match expr with
    | Let (name, binding, body, _) ->
        let value = eval env binding
        let extendedEnv = Map.add name value env
        evalToEnv extendedEnv body
    | LetRec (name, param, funcBody, inExpr, _) ->
        let funcVal = FunctionValue (param, funcBody, env)
        let extendedEnv = Map.add name funcVal env
        evalToEnv extendedEnv inExpr
    | _ ->
        // Base case: return accumulated environment (final expr is discarded)
        env

/// Load the Prelude.fun standard library and return initial environment
/// Returns emptyEnv if file not found or parse error
let loadPrelude () : Env =
    let preludePath = "Prelude.fun"
    if File.Exists preludePath then
        try
            let source = File.ReadAllText preludePath
            let ast = parse source
            evalToEnv emptyEnv ast
        with ex ->
            eprintfn "Warning: Failed to load Prelude.fun: %s" ex.Message
            emptyEnv
    else
        eprintfn "Warning: Prelude.fun not found, starting with empty environment"
        emptyEnv
