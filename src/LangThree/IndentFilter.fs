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
    | InTry of baseColumn: int          // Try-with expression with pipe alignment
    | InFunctionApp of baseColumn: int  // Multi-line function application

/// State maintained during filtering
type FilterState = {
    IndentStack: int list       // Stack of indent levels, starts with [0]
    LineNum: int                // Current line number for errors
    Context: SyntaxContext list // Stack of syntax contexts for nesting
    JustSawMatch: bool          // Flag: did we just see MATCH token?
    JustSawTry: bool            // Flag: did we just see TRY token?
    PrevToken: Parser.token option  // Previous token for function app detection
}

/// Initial state
let initialState = { IndentStack = [0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false; JustSawTry = false; PrevToken = None }

/// Error for indentation problems
exception IndentationError of line: int * message: string

/// Format expected indent levels for error messages
let formatExpectedIndents (stack: int list) : string =
    match stack with
    | [] -> "0"
    | [single] -> string single
    | multiple ->
        let levels = multiple |> List.rev |> List.map string |> String.concat ", "
        $"one of [{levels}] or a new indent level"

/// Validate indent width matches configured expectations
let validateIndentWidth (config: IndentConfig) (col: int) : unit =
    if config.StrictWidth && col > 0 && col % config.IndentWidth <> 0 then
        raise (IndentationError(0, $"Indentation must be a multiple of {config.IndentWidth}, found {col}"))

/// Process a NEWLINE token and generate INDENT/DEDENT as needed
let processNewline (config: IndentConfig) (state: FilterState) (col: int) : FilterState * Parser.token list =
    // Validate indent width if configured
    validateIndentWidth config col

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
            let expected = formatExpectedIndents state.IndentStack
            raise (IndentationError(state.LineNum,
                $"Invalid indentation at line {state.LineNum}, column {col}. Expected {expected}"))

    let (tokens, newStack) = unwind [] state.IndentStack
    ({ state with IndentStack = newStack }, tokens)

/// Check if a token could be a function being applied
let canBeFunction (token: Parser.token) : bool =
    match token with
    | Parser.IDENT _ | Parser.RPAREN -> true
    | _ -> false

/// Check if a token is an atom (can be an argument)
let isAtom (token: Parser.token) : bool =
    match token with
    | Parser.NUMBER _ | Parser.IDENT _ | Parser.TRUE | Parser.FALSE
    | Parser.STRING _ | Parser.LPAREN -> true
    | _ -> false

/// Process NEWLINE with context awareness for special indentation rules
let processNewlineWithContext (config: IndentConfig) (state: FilterState) (col: int) (nextToken: Parser.token option) : FilterState * Parser.token list =
    // If we just saw MATCH, enter match context with current indent level BEFORE processing
    let stateWithMatchContext =
        if state.JustSawMatch then
            { state with Context = InMatch state.IndentStack.Head :: state.Context; JustSawMatch = false }
        else
            state

    // If we just saw TRY, enter try context with current indent level BEFORE processing
    let stateWithTryContext =
        if stateWithMatchContext.JustSawTry then
            { stateWithMatchContext with Context = InTry stateWithMatchContext.IndentStack.Head :: stateWithMatchContext.Context; JustSawTry = false }
        else
            stateWithMatchContext

    // Check if we're entering a function application context
    // Only enter if we're not already in one
    let enteringFunctionApp =
        match stateWithTryContext.Context with
        | InFunctionApp _ :: _ -> false  // Already in function app
        | _ ->
            match stateWithTryContext.PrevToken, nextToken with
            | Some prevTok, Some nextTok when canBeFunction prevTok && isAtom nextTok ->
                // Previous token could be a function, newline, now an atom at deeper indent
                match stateWithTryContext.IndentStack with
                | topIndent :: _ when col > topIndent -> true
                | _ -> false
            | _ -> false

    // First, check if we need to process normal indentation (INDENT/DEDENT)
    // This updates the indent stack and context
    let (stateAfterIndent, indentTokens) =
        match stateWithTryContext.Context with
        | InFunctionApp baseCol :: rest when col <= baseCol ->
            // Exiting function app - emit DEDENT
            let stateAfterExit = { stateWithTryContext with Context = rest }
            let (newState, tokens) = processNewline config stateAfterExit col
            (newState, Parser.DEDENT :: tokens)
        | _ when enteringFunctionApp ->
            // Entering multi-line function application
            let baseCol = List.head stateWithTryContext.IndentStack
            let newState = { stateWithTryContext with Context = InFunctionApp baseCol :: stateWithTryContext.Context }
            (newState, [Parser.INDENT])
        | InFunctionApp _ :: _ ->
            // Still in function app, don't process indentation normally
            (stateWithTryContext, [])
        | _ ->
            // Process normal indentation
            let (newState, tokens) = processNewline config stateWithTryContext col
            // Update context based on DEDENTs (pop match/try contexts if dedented below them)
            let updatedState =
                if List.contains Parser.DEDENT tokens then
                    // Pop contexts that are at or above the current indent level
                    let rec popContexts ctx =
                        match ctx with
                        | InMatch baseCol :: rest when newState.IndentStack.Head <= baseCol -> popContexts rest
                        | InTry baseCol :: rest when newState.IndentStack.Head <= baseCol -> popContexts rest
                        | InFunctionApp baseCol :: rest when newState.IndentStack.Head <= baseCol -> popContexts rest
                        | _ -> ctx
                    { newState with Context = popContexts newState.Context }
                else
                    newState
            (updatedState, tokens)

    // Now check if we're in a match/try context and the next token is a pipe
    match stateAfterIndent.Context with
    | InMatch baseCol :: _ when nextToken = Some Parser.PIPE ->
        // Validate pipe alignment with match base column
        if col <> baseCol then
            raise (IndentationError(stateAfterIndent.LineNum,
                $"Match pipe at line {stateAfterIndent.LineNum}, column {col} must align with 'match' keyword at column {baseCol}"))
        // Pipe aligns correctly, don't emit the indent tokens (pipe doesn't change level)
        (stateAfterIndent, [])
    | InTry baseCol :: _ when nextToken = Some Parser.PIPE ->
        // Validate pipe alignment with try base column
        if col <> baseCol then
            raise (IndentationError(stateAfterIndent.LineNum,
                $"Try-with pipe at line {stateAfterIndent.LineNum}, column {col} must align with 'try' keyword at column {baseCol}"))
        (stateAfterIndent, [])
    | _ ->
        // Not checking pipe alignment, return the indent tokens
        (stateAfterIndent, indentTokens)

/// Update context stack based on DEDENTs
let updateContextOnDedent (state: FilterState) : FilterState =
    match state.Context with
    | InMatch baseCol :: rest ->
        // If current indent level is at or below match base, exit context
        if state.IndentStack.Head <= baseCol then
            { state with Context = rest }
        else
            state
    | InTry baseCol :: rest ->
        // If current indent level is at or below try base, exit context
        if state.IndentStack.Head <= baseCol then
            { state with Context = rest }
        else
            state
    | InFunctionApp baseCol :: rest ->
        // If current indent level is at or below function app base, exit context
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

                let (newState, emitted) = processNewlineWithContext config state col nextToken
                state <- { newState with LineNum = state.LineNum + 1 }
                yield! emitted

            | Parser.EOF ->
                // Emit DEDENTs for all open indents before EOF
                while state.IndentStack.Length > 1 do
                    let (newState, tokens) = processNewline config state 0
                    state <- newState
                    yield! tokens
                yield Parser.EOF

            | Parser.MATCH ->
                // Mark that we just saw MATCH, will enter context on next NEWLINE
                state <- { state with JustSawMatch = true; PrevToken = Some token }
                yield token

            | Parser.TRY ->
                // Mark that we just saw TRY, will enter context on next NEWLINE
                state <- { state with JustSawTry = true; PrevToken = Some token }
                yield token

            | Parser.DEDENT ->
                // Update context when dedenting
                state <- updateContextOnDedent state
                yield token

            | other ->
                state <- { state with PrevToken = Some other }
                yield other

            index <- index + 1
    }
