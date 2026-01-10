module FunLang.Parser

open FSharp.Text.Lexing
open FunLang.Ast
open FunLang.Errors
open FunLang.GeneratedParser

/// Re-export token type for external use
type Token = token

/// Lex result type alias
type LexResult = Result<Token list, FunLangError>

// =============================================================================
// Raw Tokenization (without indentation processing)
// =============================================================================

/// Tokenize with position information (raw, no indentation processing)
let tokenizeRawWithPositions (input: string) : Result<(Token * Position) list, FunLangError> =
    try
        let lexbuf = LexBuffer<char>.FromString(input)
        let rec loop acc =
            let tok = FunLang.GeneratedLexer.token lexbuf
            // Capture StartPos AFTER tokenizing - this gives the actual start of the matched token
            let startPos = lexbuf.StartPos
            let pos = { Line = startPos.Line + 1; Column = startPos.Column + 1; File = None }
            match tok with
            | EOF -> Ok (List.rev ((EOF, pos) :: acc))
            | _ -> loop ((tok, pos) :: acc)
        loop []
    with
    | ex ->
        let pos = { Line = 1; Column = 1; File = None }
        Error (Error.lexerMsg ex.Message pos)

/// Tokenize without indentation processing (raw tokens)
let tokenizeRaw (input: string) : Result<Token list, FunLangError> =
    tokenizeRawWithPositions input
    |> Result.map (List.map fst)

// =============================================================================
// Tokenization with Indentation Processing
// =============================================================================

/// Tokenize with indentation processing (INDENT/DEDENT/NEWLINE tokens inserted)
let tokenize (input: string) : Result<Token list, FunLangError> =
    match tokenizeRawWithPositions input with
    | Error e -> Error e
    | Ok tokensWithPos ->
        Indentation.processIndentation tokensWithPos

// =============================================================================
// Parsing
// =============================================================================

/// Create a lexer function from a token list
let private makeListLexer (tokens: Token list ref) : (LexBuffer<char> -> token) =
    fun _ ->
        match !tokens with
        | [] -> EOF
        | t :: rest ->
            tokens := rest
            t

/// Parse from a token list - returns full Program
let parseProgram (tokens: Token list) : Result<Program, string> =
    try
        let tokensRef = ref tokens
        let dummyLexbuf = LexBuffer<char>.FromString("")
        let lexer = makeListLexer tokensRef
        let result = prog lexer dummyLexbuf
        Ok result
    with
    | ex -> Error ex.Message

/// Parse from a token list - extracts main expression for backward compatibility
let parse (tokens: Token list) : Result<Expr, string> =
    parseProgram tokens
    |> Result.bind (fun program ->
        match program.MainExpr with
        | Some expr -> Ok expr
        | None -> Error "No main expression in program")

/// Parse a string to Program (tokenize + parse)
let parseProgramString (input: string) : Result<Program, string> =
    match tokenize input with
    | Error e -> Error e.Message
    | Ok tokens -> parseProgram tokens

/// Parse a string directly (tokenize + parse) - extracts main expression
let parseString (input: string) : Result<Expr, string> =
    match tokenize input with
    | Error e -> Error e.Message
    | Ok tokens -> parse tokens

// =============================================================================
// Compatibility Module
// =============================================================================

module Lexer =
    let tokenize = tokenize
    let tokenizeRaw = tokenizeRaw
