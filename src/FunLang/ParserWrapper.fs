module FunLang.Parser

open FSharp.Text.Lexing
open FunLang.Ast
open FunLang.Errors
open FunLang.GeneratedParser
open FunLang.CommentCollector

/// Re-export token type for external use
type Token = token

/// Lex result type alias
type LexResult = Result<Token list, FunLangError>

// =============================================================================
// Raw Tokenization (without indentation processing)
// =============================================================================

/// Tokenize with position information and comment collection (raw, no indentation processing)
let tokenizeRawWithComments (input: string) : Result<(Token * Position) list * Comment list, FunLangError> =
    if isNull input then
        Error (Error.lexerMsg "null input" { Line = 1; Column = 1; File = None })
    else
    let lexbuf = LexBuffer<char>.FromString(input)
    try
        let rec loop tokAcc commentAcc =
            let tok = FunLang.GeneratedLexer.token lexbuf
            // Capture StartPos AFTER tokenizing - this gives the actual start of the matched token
            let startPos = lexbuf.StartPos
            let pos = { Line = startPos.Line + 1; Column = startPos.Column + 1; File = None }

            // Check for pending comment from lexer buffer
            let commentAcc' =
                match FunLang.GeneratedLexer.getAndClearComment () with
                | Some (commentText, line, col) ->
                    // line and col are 0-based from lexbuf, convert to 1-based
                    let commentPos = { Line = line + 1; Column = col + 1; File = None }
                    let comment = { Text = commentText; Pos = commentPos; Kind = LineComment }
                    comment :: commentAcc
                | None -> commentAcc

            match tok with
            | EOF -> Ok (List.rev ((EOF, pos) :: tokAcc), List.rev commentAcc')
            | _ -> loop ((tok, pos) :: tokAcc) commentAcc'
        loop [] []
    with
    | ex ->
        let startPos = lexbuf.StartPos
        let pos = { Line = startPos.Line + 1; Column = startPos.Column + 1; File = None }
        Error (Error.lexerMsg ex.Message pos)

/// Tokenize with position information (raw, no indentation processing)
let tokenizeRawWithPositions (input: string) : Result<(Token * Position) list, FunLangError> =
    tokenizeRawWithComments input
    |> Result.map fst

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

/// Tokenize with indentation processing, preserving position information
let tokenizeWithPositions (input: string) : Result<(Token * Position) list, FunLangError> =
    match tokenizeRawWithPositions input with
    | Error e -> Error e
    | Ok tokensWithPos ->
        Indentation.processIndentationWithPositions tokensWithPos

// =============================================================================
// Parsing
// =============================================================================

/// Mutable state for tracking current token position during parsing
let mutable private currentTokenPosition : Position = { Line = 1; Column = 1; File = None }

/// Create a lexer function from a token list with positions
let private makeListLexerWithPositions (tokensWithPos: (Token * Position) list ref) : (LexBuffer<char> -> token) =
    fun lexbuf ->
        match !tokensWithPos with
        | [] -> EOF
        | (t, pos) :: rest ->
            currentTokenPosition <- pos
            // Update lexbuf position so parseState.InputStartPosition works correctly
            // Position is 1-based in our system, but LexBuffer uses 0-based internally
            let lexPos = FSharp.Text.Lexing.Position.Empty
            let lexPos = { lexPos with pos_lnum = pos.Line - 1; pos_cnum = pos.Column - 1 }
            lexbuf.StartPos <- lexPos
            lexbuf.EndPos <- lexPos
            tokensWithPos := rest
            t

/// Create a lexer function from a token list (no positions)
let private makeListLexer (tokens: Token list ref) : (LexBuffer<char> -> token) =
    fun _ ->
        match !tokens with
        | [] -> EOF
        | t :: rest ->
            tokens := rest
            t

/// Format a rich parse error into a human-readable message
let private formatRichParseError (currentToken: string option) (expectedTokens: string list) (pos: Position) : string =
    let tokenStr =
        match currentToken with
        | Some t -> sprintf "unexpected '%s'" t
        | None -> "unexpected end of input"

    let expectedStr =
        match expectedTokens with
        | [] -> ""
        | [t] -> sprintf ", expected %s" t
        | ts -> sprintf ", expected one of: %s" (String.concat ", " ts)

    sprintf "Parse error at line %d, column %d: %s%s" pos.Line pos.Column tokenStr expectedStr

/// Parse from a token list with positions - returns full Program
let parseProgramWithPositions (tokensWithPos: (Token * Position) list) : Result<Program, string> =
    try
        currentTokenPosition <- { Line = 1; Column = 1; File = None }
        let tokensRef = ref tokensWithPos
        let dummyLexbuf = LexBuffer<char>.FromString("")
        let lexer = makeListLexerWithPositions tokensRef
        let result = prog lexer dummyLexbuf
        Ok result
    with
    | FunLang.GeneratedParser.RichParseError (currentToken, expectedTokens, _) ->
        // Use the tracked position instead of the one from the exception
        Error (formatRichParseError currentToken expectedTokens currentTokenPosition)
    | ex -> Error (sprintf "Parse error: %s" ex.Message)

/// Parse from a token list - returns full Program
let parseProgram (tokens: Token list) : Result<Program, string> =
    // If we don't have positions, use default position
    let tokensWithPos = tokens |> List.map (fun t -> (t, { Line = 1; Column = 1; File = None }))
    parseProgramWithPositions tokensWithPos

/// Parse from a token list - extracts main expression for backward compatibility
let parse (tokens: Token list) : Result<LExpr, string> =
    parseProgram tokens
    |> Result.bind (fun program ->
        match program.MainExpr with
        | Some expr -> Ok expr
        | None -> Error "No main expression in program")

/// Parse a string to Program (tokenize + parse) with position tracking
let parseProgramString (input: string) : Result<Program, string> =
    match tokenizeWithPositions input with
    | Error e -> Error e.Message
    | Ok tokensWithPos -> parseProgramWithPositions tokensWithPos

/// Parse a string to Program with comment collection
let parseProgramWithComments (input: string) : Result<Program * Comment list, string> =
    match tokenizeRawWithComments input with
    | Error e -> Error e.Message
    | Ok (tokensWithPos, comments) ->
        // Process indentation on tokens (comments already extracted)
        match Indentation.processIndentationWithPositions tokensWithPos with
        | Error e -> Error e.Message
        | Ok processedTokens ->
            // Parse tokens to Program
            match parseProgramWithPositions processedTokens with
            | Error e -> Error e
            | Ok program -> Ok (program, comments)

/// Parse a string directly (tokenize + parse) - extracts main expression
let parseString (input: string) : Result<LExpr, string> =
    match tokenizeWithPositions input with
    | Error e -> Error e.Message
    | Ok tokensWithPos ->
        parseProgramWithPositions tokensWithPos
        |> Result.bind (fun program ->
            match program.MainExpr with
            | Some expr -> Ok expr
            | None -> Error "No main expression in program")

// =============================================================================
// Compatibility Module
// =============================================================================

module Lexer =
    let tokenize = tokenize
    let tokenizeRaw = tokenizeRaw
