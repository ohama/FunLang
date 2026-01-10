module FunLang.Errors

open FunLang.Ast

// =============================================================================
// Error Types
// =============================================================================

type ErrorKind =
    | LexerError of char: char * position: Position
    | ParseError of token: string * expected: string list * position: Position
    | UnboundVariable of name: string * position: Position
    | TypeError of expected: string * actual: string * position: Position
    | RuntimeError of message: string * position: Position option
    | DivisionByZero of position: Position
    | NonExhaustiveMatch of position: Position
    | IndentationError of expected: int * actual: int * position: Position
    | MixedTabsSpaces of position: Position

type FunLangError = {
    Kind: ErrorKind
    Message: string
    Hint: string option
    Position: Position option
}

// =============================================================================
// Error Creation Helpers
// =============================================================================

module Error =
    let lexer char pos =
        { Kind = LexerError (char, pos)
          Message = sprintf "Unexpected character '%c'" char
          Hint = Some "Check for typos or unsupported characters"
          Position = Some pos }

    let lexerMsg msg pos =
        { Kind = LexerError ('\000', pos)
          Message = msg
          Hint = None
          Position = Some pos }

    let parse token expected pos =
        { Kind = ParseError (token, expected, pos)
          Message = sprintf "Unexpected token '%s'" token
          Hint = if List.isEmpty expected then None
                 else Some (sprintf "Expected: %s" (String.concat ", " expected))
          Position = Some pos }

    let unboundVar name pos =
        { Kind = UnboundVariable (name, pos)
          Message = sprintf "Unbound variable '%s'" name
          Hint = None
          Position = Some pos }

    let typeError expected actual pos =
        { Kind = TypeError (expected, actual, pos)
          Message = sprintf "Type mismatch: expected %s, got %s" expected actual
          Hint = None
          Position = Some pos }

    let runtime message pos =
        { Kind = RuntimeError (message, pos)
          Message = message
          Hint = None
          Position = pos }

    let divisionByZero pos =
        { Kind = DivisionByZero pos
          Message = "Division by zero"
          Hint = Some "Check divisor is not zero"
          Position = Some pos }

    let nonExhaustive pos =
        { Kind = NonExhaustiveMatch pos
          Message = "Non-exhaustive pattern match"
          Hint = Some "Add missing pattern cases"
          Position = Some pos }

    let indentation expected actual pos =
        { Kind = IndentationError (expected, actual, pos)
          Message = sprintf "Indentation error: expected %d spaces, got %d" expected actual
          Hint = Some "Check your indentation"
          Position = Some pos }

    let mixedTabsSpaces pos =
        { Kind = MixedTabsSpaces pos
          Message = "Mixed tabs and spaces in indentation"
          Hint = Some "Use spaces only for indentation"
          Position = Some pos }

// =============================================================================
// Error Formatting
// =============================================================================

let formatError (err: FunLangError) : string =
    let posStr =
        match err.Position with
        | Some p ->
            match p.File with
            | Some f -> sprintf "%s:%d:%d" f p.Line p.Column
            | None -> sprintf "line %d, column %d" p.Line p.Column
        | None -> "unknown location"

    let hintStr =
        match err.Hint with
        | Some h -> sprintf "\nHint: %s" h
        | None -> ""

    sprintf "Error at %s: %s%s" posStr err.Message hintStr

let formatErrorWithSource (source: string) (err: FunLangError) : string =
    match err.Position with
    | Some pos when pos.Line > 0 ->
        let lines = source.Split('\n')
        if pos.Line <= lines.Length then
            let line = lines.[pos.Line - 1]
            let pointer = String.replicate (pos.Column - 1) " " + "^"
            sprintf "%s\n  |\n%d | %s\n  | %s"
                (formatError err) pos.Line line pointer
        else
            formatError err
    | _ -> formatError err

// =============================================================================
// Result Type Aliases
// =============================================================================

// Note: LexResult is defined in ParserWrapper.fs (after Parser.fs compiles)
type ParseResult = Result<Expr, FunLangError>
type EvalResult = Result<Value, FunLangError>

// =============================================================================
// Result Computation Expression
// =============================================================================

type ResultBuilder() =
    member _.Return(x) = Ok x
    member _.ReturnFrom(m) = m
    member _.Bind(m, f) = Result.bind f m
    member _.Zero() = Ok ()
    member _.Combine(m1, m2) = Result.bind (fun () -> m2) m1
    member _.Delay(f) = f
    member _.Run(f) = f()

let result = ResultBuilder()

// =============================================================================
// Result Helper Functions
// =============================================================================

module Result =
    /// Sequence a list of Results into a Result of list
    let sequence (results: Result<'a, 'e> list) : Result<'a list, 'e> =
        List.foldBack (fun r acc ->
            match r, acc with
            | Ok x, Ok xs -> Ok (x :: xs)
            | Error e, _ -> Error e
            | _, Error e -> Error e
        ) results (Ok [])

    /// Apply a function to two Results
    let map2 f r1 r2 =
        match r1, r2 with
        | Ok x, Ok y -> Ok (f x y)
        | Error e, _ -> Error e
        | _, Error e -> Error e

    /// Convert Option to Result with error message
    let ofOption error opt =
        match opt with
        | Some x -> Ok x
        | None -> Error error
