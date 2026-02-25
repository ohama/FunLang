module LangThree.IndentFilter

open Parser

/// Configuration for indent processing
type IndentConfig = {
    IndentWidth: int  // Expected indent width (2, 4, or 8)
    StrictWidth: bool // If true, enforce exact multiples of IndentWidth
}

/// Default configuration: 4 spaces, not strict
let defaultConfig = { IndentWidth = 4; StrictWidth = false }

/// State maintained during filtering
type FilterState = {
    IndentStack: int list  // Stack of indent levels, starts with [0]
    LineNum: int           // Current line number for errors
}

/// Initial state
let initialState = { IndentStack = [0]; LineNum = 1 }

/// Error for indentation problems
exception IndentationError of line: int * message: string

/// Process a NEWLINE token and generate INDENT/DEDENT as needed
let processNewline (state: FilterState) (col: int) : FilterState * Parser.token list =
    let rec unwind acc stack =
        match stack with
        | [] ->
            raise (IndentationError(state.LineNum, "Internal error: empty indent stack"))
        | top :: rest when col < top ->
            // Dedent: pop and emit DEDENT
            unwind (Parser.DEDENT :: acc) rest
        | top :: _ when col = top ->
            // Same level: done unwinding, return accumulated DEDENTs
            (List.rev acc, stack)
        | top :: _ when col > top && List.isEmpty acc ->
            // Indent: only valid if we haven't emitted any DEDENTs
            // (i.e., col is greater than current top without any pops)
            ([Parser.INDENT], col :: stack)
        | _ ->
            // col > top but we've been unwinding (emitted DEDENTs)
            // This means col doesn't match any level in the stack
            raise (IndentationError(state.LineNum,
                $"Invalid indentation: column {col} doesn't match any level in stack"))

    let (tokens, newStack) = unwind [] state.IndentStack
    ({ state with IndentStack = newStack }, tokens)
