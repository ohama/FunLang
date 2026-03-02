module LangThree.IndentFilter

open Parser

/// Configuration for indent processing
type IndentConfig = {
    IndentWidth: int  // Expected indent width (2, 4, or 8)
    StrictWidth: bool // If true, enforce exact multiples of IndentWidth
}

/// Default configuration: 4 spaces, not strict
let defaultConfig = { IndentWidth = 4; StrictWidth = false }

/// Syntax context for tracking multi-line constructs
type SyntaxContext =
    | TopLevel
    | InMatch of baseColumn: int        // Match expression with pipe alignment

/// State maintained during filtering
type FilterState = {
    IndentStack: int list       // Stack of indent levels, starts with [0]
    LineNum: int                // Current line number for errors
    Context: SyntaxContext list // Stack of syntax contexts for nesting
    JustSawMatch: bool          // Flag: did we just see MATCH token?
}

/// Initial state
let initialState = { IndentStack = [0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false }

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

/// Process NEWLINE with context awareness for special indentation rules
let processNewlineWithContext (state: FilterState) (col: int) (nextToken: Parser.token option) : FilterState * Parser.token list =
    // Check if we're in a match context and the next token is a pipe
    match state.Context with
    | InMatch baseCol :: _ when nextToken = Some Parser.PIPE ->
        // Validate pipe alignment with match base column
        if col <> baseCol then
            raise (IndentationError(state.LineNum,
                $"Match pipe must align with 'match' keyword at column {baseCol}, found at column {col}"))
        // Pipe aligns correctly, don't change indent stack
        (state, [])
    | _ ->
        // Not in match context or not a pipe, use normal processing
        let (newState, tokens) = processNewline state col
        // If we just saw MATCH, enter match context with the current indent level
        let finalState =
            if newState.JustSawMatch then
                { newState with Context = InMatch state.IndentStack.Head :: newState.Context; JustSawMatch = false }
            else
                newState
        (finalState, tokens)

/// Update context stack based on DEDENTs
let updateContextOnDedent (state: FilterState) : FilterState =
    match state.Context with
    | InMatch baseCol :: rest ->
        // If current indent level is at or below match base, exit context
        if state.IndentStack.Head <= baseCol then
            { state with Context = rest }
        else
            state
    | _ -> state

/// Filter a token stream, converting NEWLINE(col) to INDENT/DEDENT
let filter (config: IndentConfig) (tokens: Parser.token seq) : Parser.token seq =
    seq {
        let mutable state = initialState
        let tokenList = tokens |> Seq.toList
        let mutable index = 0

        while index < tokenList.Length do
            let token = tokenList.[index]

            match token with
            | Parser.NEWLINE col ->
                // Look ahead to next non-NEWLINE token
                let nextToken =
                    tokenList
                    |> List.skip (index + 1)
                    |> List.tryFind (fun t -> match t with Parser.NEWLINE _ -> false | _ -> true)

                let (newState, emitted) = processNewlineWithContext state col nextToken
                state <- { newState with LineNum = state.LineNum + 1 }
                yield! emitted

            | Parser.EOF ->
                // Emit DEDENTs for all open indents before EOF
                while state.IndentStack.Length > 1 do
                    let (newState, _) = processNewline state 0
                    state <- newState
                    yield Parser.DEDENT
                yield Parser.EOF

            | Parser.MATCH ->
                // Mark that we just saw MATCH, will enter context on next NEWLINE
                state <- { state with JustSawMatch = true }
                yield token

            | Parser.DEDENT ->
                // Update context when dedenting
                state <- updateContextOnDedent state
                yield token

            | other ->
                yield other

            index <- index + 1
    }
