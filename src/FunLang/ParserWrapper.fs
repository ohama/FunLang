module FunLang.Parser

open FSharp.Text.Lexing
open FunLang.Ast
open FunLang.Errors
open FunLang.GeneratedParser
open FunLang.GeneratedLexer

/// Re-export token type for external use
type Token = token

/// Tokenize a string into a list of tokens
let tokenize (input: string) : Result<Token list, FunLangError> =
    try
        let lexbuf = LexBuffer<char>.FromString(input)
        let rec loop acc =
            let tok = FunLang.GeneratedLexer.token lexbuf
            match tok with
            | EOF -> Ok (List.rev (EOF :: acc))
            | _ -> loop (tok :: acc)
        loop []
    with
    | ex ->
        let pos = { Line = 1; Column = 1; File = None }
        Error (Error.lexerMsg ex.Message pos)

/// Parse a string directly
let parseString (input: string) : Result<Expr, string> =
    try
        let lexbuf = LexBuffer<char>.FromString(input)
        let result = prog token lexbuf
        Ok result
    with
    | ex -> Error ex.Message

/// Create a lexer function from a token list (for compatibility)
let private makeListLexer (tokens: Token list ref) : (LexBuffer<char> -> token) =
    fun _ ->
        match !tokens with
        | [] -> EOF
        | t :: rest ->
            tokens := rest
            t

/// Parse from a token list (for compatibility with existing API)
let parse (tokens: Token list) : Result<Expr, string> =
    try
        let tokensRef = ref tokens
        let dummyLexbuf = LexBuffer<char>.FromString("")
        let lexer = makeListLexer tokensRef
        let result = prog lexer dummyLexbuf
        Ok result
    with
    | ex -> Error ex.Message

module Lexer =
    let tokenize = tokenize
