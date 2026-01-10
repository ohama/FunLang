module FunLang.Lexer

open FunLang.Ast
open FunLang.Errors

// =============================================================================
// Lexer Error (using FunLangError from Errors module)
// =============================================================================

type LexerError = FunLangError

// =============================================================================
// Lexer State
// =============================================================================

type private LexerState = {
    Input: string
    Position: int
    Line: int
    Column: int
}

let private initialState input = {
    Input = input
    Position = 0
    Line = 1
    Column = 1
}

// =============================================================================
// Helper Functions
// =============================================================================

let private isAtEnd state =
    state.Position >= state.Input.Length

let private peek state =
    if isAtEnd state then None
    else Some state.Input.[state.Position]

let private peekNext state =
    if state.Position + 1 >= state.Input.Length then None
    else Some state.Input.[state.Position + 1]

let private advance state =
    if isAtEnd state then state
    else
        let ch = state.Input.[state.Position]
        let newLine, newCol =
            if ch = '\n' then (state.Line + 1, 1)
            else (state.Line, state.Column + 1)
        { state with
            Position = state.Position + 1
            Line = newLine
            Column = newCol }

let private currentPos state =
    { Line = state.Line; Column = state.Column; File = None }

let private isDigit c = c >= '0' && c <= '9'
let private isAlpha c = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c = '_'
let private isAlphaNumeric c = isDigit c || isAlpha c

// =============================================================================
// Token Scanning
// =============================================================================

let private skipWhitespace state =
    let rec loop s =
        match peek s with
        | Some ' ' | Some '\t' | Some '\r' -> loop (advance s)
        | _ -> s
    loop state

let private scanNumber state =
    let startPos = state.Position
    let rec loop s =
        match peek s with
        | Some c when isDigit c -> loop (advance s)
        | _ -> s
    let endState = loop state
    let numStr = state.Input.Substring(startPos, endState.Position - startPos)
    match System.Int32.TryParse(numStr) with
    | true, n -> Ok (INT n, endState)
    | false, _ -> Error (Error.lexerMsg (sprintf "Invalid number: %s" numStr) (currentPos state))

let private scanIdentifierOrKeyword state =
    let startPos = state.Position
    let rec loop s =
        match peek s with
        | Some c when isAlphaNumeric c -> loop (advance s)
        | _ -> s
    let endState = loop state
    let text = state.Input.Substring(startPos, endState.Position - startPos)
    let token =
        match text with
        | "let" -> LET
        | "rec" -> REC
        | "in" -> IN
        | "if" -> IF
        | "then" -> THEN
        | "else" -> ELSE
        | "fun" -> FUN
        | "match" -> MATCH
        | "with" -> WITH
        | "when" -> WHEN
        | "true" -> BOOL true
        | "false" -> BOOL false
        | "type" -> TYPE
        | "of" -> OF
        | "not" -> NOT
        | "and" -> AND
        | "or" -> OR
        | _ -> IDENT text
    Ok (token, endState)

let private scanString state =
    // Skip opening quote
    let state = advance state
    let startPos = state.Position
    let rec loop s =
        match peek s with
        | None -> Error (Error.lexerMsg "Unterminated string" (currentPos state))
        | Some '"' ->
            let text = state.Input.Substring(startPos, s.Position - startPos)
            Ok (STRING text, advance s)
        | Some '\\' ->
            // Skip escape sequence
            loop (advance (advance s))
        | _ -> loop (advance s)
    loop state

let private scanToken state =
    let state = skipWhitespace state
    if isAtEnd state then
        None
    else
        let pos = currentPos state
        match peek state with
        | Some c when isDigit c ->
            Some (scanNumber state)

        | Some c when isAlpha c ->
            Some (scanIdentifierOrKeyword state)

        | Some '"' ->
            Some (scanString state)

        | Some '+' -> Some (Ok (PLUS, advance state))
        | Some '*' -> Some (Ok (STAR, advance state))
        | Some '/' -> Some (Ok (SLASH, advance state))
        | Some '%' -> Some (Ok (PERCENT, advance state))
        | Some '(' -> Some (Ok (LPAREN, advance state))
        | Some ')' -> Some (Ok (RPAREN, advance state))
        | Some '[' -> Some (Ok (LBRACKET, advance state))
        | Some ']' -> Some (Ok (RBRACKET, advance state))
        | Some ',' -> Some (Ok (COMMA, advance state))
        | Some ';' -> Some (Ok (SEMICOLON, advance state))
        | Some '_' -> Some (Ok (UNDERSCORE, advance state))
        | Some '|' -> Some (Ok (PIPE, advance state))
        | Some '\n' -> Some (Ok (NEWLINE, advance state))

        | Some '-' ->
            match peekNext state with
            | Some '>' -> Some (Ok (ARROW, advance (advance state)))
            | _ -> Some (Ok (MINUS, advance state))

        | Some ':' ->
            match peekNext state with
            | Some ':' -> Some (Ok (DOUBLECOLON, advance (advance state)))
            | _ -> Some (Ok (COLON, advance state))

        | Some '<' ->
            match peekNext state with
            | Some '=' -> Some (Ok (LTE, advance (advance state)))
            | Some '>' -> Some (Ok (NEQ, advance (advance state)))
            | _ -> Some (Ok (LT, advance state))

        | Some '>' ->
            match peekNext state with
            | Some '=' -> Some (Ok (GTE, advance (advance state)))
            | _ -> Some (Ok (GT, advance state))

        | Some '=' ->
            match peekNext state with
            | Some '=' -> Some (Ok (EQ, advance (advance state)))
            | _ -> Some (Ok (EQ, advance state))

        | Some '!' ->
            match peekNext state with
            | Some '=' -> Some (Ok (NEQ, advance (advance state)))
            | _ -> Some (Error (Error.lexer '!' pos))

        | Some c ->
            Some (Error (Error.lexer c pos))

        | None -> None

// =============================================================================
// Public API
// =============================================================================

/// Tokenize input string into a list of tokens
let tokenize (input: string) : Result<Token list, LexerError> =
    // Handle null input
    if isNull input then
        Ok []
    else
        let rec loop state acc =
            match scanToken state with
            | None -> Ok (List.rev acc)
            | Some (Ok (NEWLINE, newState)) ->
                // Skip newlines for now (will handle in indentation phase)
                loop newState acc
            | Some (Ok (token, newState)) ->
                loop newState (token :: acc)
            | Some (Error e) ->
                Error e
        loop (initialState input) []
