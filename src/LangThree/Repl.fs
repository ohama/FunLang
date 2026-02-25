module Repl

open System
open FSharp.Text.Lexing
open Ast
open Eval

/// Parse a string input and return the AST
let private parse (input: string) : Expr =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf "<repl>"
    Parser.start Lexer.tokenize lexbuf

/// REPL loop with environment threading
let rec private replLoop (env: Env) : unit =
    Console.Write "funlang> "
    Console.Out.Flush()

    match Console.ReadLine() with
    | null ->
        // EOF (Ctrl+D on Unix, Ctrl+Z on Windows)
        printfn ""
    | "#quit" ->
        // Explicit quit command (like F# Interactive)
        ()
    | "" ->
        // Empty line - just continue
        replLoop env
    | line ->
        try
            let ast = parse line
            let result = eval env ast
            printfn "%s" (formatValue result)
            replLoop env
        with ex ->
            eprintfn "Error: %s" ex.Message
            replLoop env

/// Start the REPL with welcome message
let startRepl () : int =
    printfn "FunLang REPL"
    printfn "Type '#quit' or Ctrl+D to quit."
    printfn ""
    let initialEnv = Prelude.loadPrelude()
    replLoop initialEnv
    0
