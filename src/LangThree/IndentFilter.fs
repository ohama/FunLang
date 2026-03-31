module LangThree.IndentFilter

open Parser

/// A token together with the source position at which it appeared
type PositionedToken = {
    Token: Parser.token
    StartPos: FSharp.Text.Lexing.Position
    EndPos: FSharp.Text.Lexing.Position
}

/// Create a positioned token by copying positions from a reference token
let withPosOf (ref: PositionedToken) (tok: Parser.token) : PositionedToken =
    { Token = tok; StartPos = ref.StartPos; EndPos = ref.EndPos }

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
    | InLetDecl of blockLet: bool * offsideCol: int  // Let binding with offside tracking
    | InExprBlock of baseColumn: int                  // Expression block (let RHS, lambda body)
    | InModule                                        // Module body (no implicit IN)

/// State maintained during filtering
type FilterState = {
    IndentStack: int list       // Stack of indent levels, starts with [0]
    LineNum: int                // Current line number for errors
    Context: SyntaxContext list // Stack of syntax contexts for nesting
    JustSawMatch: bool          // Flag: did we just see MATCH token?
    JustSawTry: bool            // Flag: did we just see TRY token?
    JustSawModule: bool         // Flag: did we just see MODULE token?
    PrevToken: Parser.token option  // Previous token for function app detection
    BracketDepth: int           // Depth of [] () {} nesting; NEWLINE suppressed when > 0
}

/// Initial state
let initialState = { IndentStack = [0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false; JustSawTry = false; JustSawModule = false; PrevToken = None; BracketDepth = 0 }

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

/// Check if a token starts a continuation line (infix operator continuing prev expr)
/// These tokens cannot start a new statement, so no SEMICOLON should precede them.
let isContinuationStart (token: Parser.token) : bool =
    match token with
    | Parser.PIPE_RIGHT | Parser.COMPOSE_RIGHT | Parser.COMPOSE_LEFT -> true
    | Parser.AND | Parser.OR | Parser.CONS -> true
    | Parser.INFIXOP0 _ | Parser.INFIXOP1 _ | Parser.INFIXOP2 _
    | Parser.INFIXOP3 _ | Parser.INFIXOP4 _ -> true
    | _ -> false

/// Check if a token is a structural terminator that should NOT be preceded by SEMICOLON.
/// These keywords close or continue an outer construct (if-then-else, try-with, match arms).
let isStructuralTerminator (token: Parser.token) : bool =
    match token with
    | Parser.ELSE | Parser.WITH | Parser.THEN | Parser.PIPE | Parser.IN -> true
    | _ -> false

/// Check if a token is an atom (can be an argument)
let isAtom (token: Parser.token) : bool =
    match token with
    | Parser.NUMBER _ | Parser.IDENT _ | Parser.TRUE | Parser.FALSE
    | Parser.STRING _ | Parser.LPAREN -> true
    | _ -> false

/// Check if a context is an expression context (where block-let produces implicit IN)
let isExprContext (ctx: SyntaxContext list) : bool =
    match ctx with
    | InLetDecl _ :: _ -> true      // nested let inside another let body
    | InExprBlock _ :: _ -> true    // expression block (let RHS, lambda body)
    | InMatch _ :: _ -> true        // match body is expression context
    | InTry _ :: _ -> true          // try body is expression context
    | InModule :: _ -> false        // module body - declarations, no implicit in
    | TopLevel :: _ -> false        // top level - declarations
    | InFunctionApp _ :: _ -> true  // function app is expression context
    | [] -> false

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
            // Update context based on DEDENTs (pop match/try/module/letdecl contexts if dedented below them)
            let updatedState =
                if List.contains Parser.DEDENT tokens then
                    // Pop contexts that are at or above the current indent level
                    let rec popContexts ctx =
                        match ctx with
                        | InMatch baseCol :: rest when newState.IndentStack.Head <= baseCol -> popContexts rest
                        | InTry baseCol :: rest when newState.IndentStack.Head < baseCol -> popContexts rest
                        | InFunctionApp baseCol :: rest when newState.IndentStack.Head <= baseCol -> popContexts rest
                        // InLetDecl NOT popped here — filter handles it with IN emission
                        | InExprBlock baseCol :: rest when newState.IndentStack.Head <= baseCol -> popContexts rest
                        | InModule :: rest when newState.IndentStack.Head <= 0 -> popContexts rest
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
        // Suppress INDENT when next token is ELSE
        // (ELSE follows THEN directly — INDENT between them confuses the parser)
        // DEDENTs are NOT suppressed — they close open blocks from the THEN branch.
        let filteredTokens =
            match nextToken with
            | Some Parser.ELSE when List.contains Parser.INDENT indentTokens ->
                indentTokens |> List.filter (fun t -> t <> Parser.INDENT)
            | _ -> indentTokens
        (stateAfterIndent, filteredTokens)

/// Update context stack based on DEDENTs
let updateContextOnDedent (state: FilterState) : FilterState =
    let rec popContexts ctx =
        match ctx with
        | InMatch baseCol :: rest when state.IndentStack.Head <= baseCol -> popContexts rest
        | InTry baseCol :: rest when state.IndentStack.Head < baseCol -> popContexts rest
        | InFunctionApp baseCol :: rest when state.IndentStack.Head <= baseCol -> popContexts rest
        | InLetDecl(_, offsideCol) :: rest when state.IndentStack.Head <= offsideCol -> popContexts rest
        | InExprBlock baseCol :: rest when state.IndentStack.Head <= baseCol -> popContexts rest
        | InModule :: rest ->
            // Pop module context when dedenting back to top level
            if state.IndentStack.Head <= 0 then popContexts rest else ctx
        | _ -> ctx
    { state with Context = popContexts state.Context }

/// Filter a token stream, converting NEWLINE(col) to INDENT/DEDENT
let filter (config: IndentConfig) (tokens: Parser.token seq) : Parser.token seq =
    seq {
        let mutable state = initialState
        let tokenList = tokens |> Seq.toList
        let mutable index = 0

        while index < tokenList.Length do
            let token = tokenList.[index]

            match token with
            | Parser.LBRACKET | Parser.LPAREN | Parser.LBRACE | Parser.DOTLBRACKET ->
                state <- { state with BracketDepth = state.BracketDepth + 1; PrevToken = Some token }
                yield token

            | Parser.RBRACKET | Parser.RPAREN | Parser.RBRACE ->
                state <- { state with BracketDepth = max 0 (state.BracketDepth - 1); PrevToken = Some token }
                yield token

            | Parser.NEWLINE _ when state.BracketDepth > 0 ->
                // Inside brackets: suppress INDENT/DEDENT, just advance line counter
                state <- { state with LineNum = state.LineNum + 1 }

            | Parser.NEWLINE _ when index + 1 < tokenList.Length && (match tokenList.[index + 1] with Parser.NEWLINE _ -> true | _ -> false) ->
                // Blank line: next token is also NEWLINE, skip without processing indentation.
                // This prevents blank lines from triggering premature DEDENT in module/let/match bodies.
                state <- { state with LineNum = state.LineNum + 1 }

            | Parser.NEWLINE col ->
                // Look ahead to next non-NEWLINE token
                let nextToken =
                    tokenList
                    |> List.skip (index + 1)
                    |> List.tryFind (fun t -> match t with Parser.NEWLINE _ -> false | _ -> true)

                let (newState_, emitted) = processNewlineWithContext config state col nextToken
                let newState = { newState_ with LineNum = state.LineNum + 1 }

                // Offside rule: check if any InLetDecl(blockLet=true) contexts have their
                // offside column reached by the next token's column.
                // This applies at same level (no INDENT/DEDENT emitted) and when not in
                // match/try pipe context.
                let nextIsExplicitIn = match nextToken with | Some Parser.IN -> true | _ -> false
                let isAtSameLevel =
                    List.isEmpty emitted &&
                    newState.IndentStack.Length > 1 &&
                    not nextIsExplicitIn &&  // Don't insert implicit IN when explicit IN follows
                    (match state.PrevToken with
                     | Some Parser.IN -> false
                     | _ -> true) &&
                    (match newState.Context with
                     | InFunctionApp _ :: _ -> false  // Don't insert IN in function app
                     | _ -> true)
                    // Note: InMatch/InTry do NOT block offside rule anymore.
                    // They only affect pipe alignment in processNewlineWithContext.

                if isAtSameLevel then
                    // Check offside: pop InLetDecl contexts where col <= offsideCol, emit IN for each
                    let rec checkOffside ctx acc =
                        match ctx with
                        | InLetDecl(true, offsideCol) :: rest when col <= offsideCol ->
                            checkOffside rest (Parser.IN :: acc)
                        | _ -> (ctx, List.rev acc)
                    let (newCtx, insTokens) = checkOffside newState.Context []
                    if not (List.isEmpty insTokens) then
                        state <- { newState with Context = newCtx }
                        yield! insTokens
                    else
                        // NLSEQ: inject SEMICOLON if InExprBlock is the direct top context
                        // and the next token is neither a structural terminator nor a continuation operator.
                        let shouldInjectSemicolon =
                            match newState.Context with
                            | InExprBlock _ :: _ ->
                                let suppressByNext =
                                    match nextToken with
                                    | Some t -> isContinuationStart t || isStructuralTerminator t
                                    | None -> false
                                not suppressByNext
                            | _ -> false
                        if shouldInjectSemicolon then
                            state <- newState
                            yield Parser.SEMICOLON
                        else
                            state <- newState
                            yield! emitted
                else
                    // Check offside on DEDENT too: pop InLetDecl contexts that are above
                    // the new indent level, emitting IN for each block-let AFTER the DEDENT.
                    // Skip if next token is explicit IN (user handles it).
                    if List.contains Parser.DEDENT emitted then
                        if nextIsExplicitIn then
                            // Pop InLetDecl contexts silently (explicit IN will handle them)
                            let rec popLetDecls ctx =
                                match ctx with
                                | InLetDecl(true, offsideCol) :: rest when newState.IndentStack.Head <= offsideCol ->
                                    popLetDecls rest
                                | _ -> ctx
                            state <- { newState with Context = popLetDecls newState.Context }
                            yield! emitted
                        else
                            let rec checkOffsideDedent ctx acc =
                                match ctx with
                                | InLetDecl(true, offsideCol) :: rest when newState.IndentStack.Head <= offsideCol ->
                                    checkOffsideDedent rest (Parser.IN :: acc)
                                | _ -> (ctx, List.rev acc)
                            let (newCtx, insTokens) = checkOffsideDedent newState.Context []
                            state <- { newState with Context = newCtx }
                            yield! emitted
                            yield! insTokens
                    else
                        state <- newState
                        // When INDENT is emitted, push appropriate block context
                        if List.contains Parser.INDENT emitted then
                            if state.JustSawModule then
                                state <- { state with Context = InModule :: state.Context; JustSawModule = false }
                            else
                                // Push expression block for EQUALS (not module) or ARROW
                                match state.PrevToken with
                                | Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN | Some Parser.DO ->
                                    // baseCol = parent indent level (before INDENT)
                                    let baseCol = match state.IndentStack with _ :: parent :: _ -> parent | _ -> 0
                                    state <- { state with Context = InExprBlock(baseCol) :: state.Context }
                                | _ -> ()
                        yield! emitted

            | Parser.EOF ->
                // Emit INs for any remaining InLetDecl(blockLet=true) contexts before DEDENTs
                let rec emitPendingIns ctx acc =
                    match ctx with
                    | InLetDecl(true, _) :: rest -> emitPendingIns rest (Parser.IN :: acc)
                    | _ -> (ctx, List.rev acc)
                let (ctxAfterIns, insTokens) = emitPendingIns state.Context []
                state <- { state with Context = ctxAfterIns }
                yield! insTokens
                // Emit DEDENTs for all open indents before EOF
                while state.IndentStack.Length > 1 do
                    let (newState, tokens) = processNewline config state 0
                    state <- newState
                    yield! tokens
                yield Parser.EOF

            | Parser.MODULE ->
                // Mark that next INDENT should push InModule context
                state <- { state with JustSawModule = true; PrevToken = Some token }
                yield token

            | Parser.TYPECLASS | Parser.INSTANCE ->
                // Phase 71 (Type Classes): treat typeclass/instance bodies like module bodies
                // (declaration context, not expression context) so that LET inside instance
                // methods does not trigger the offside-rule IN emission.
                state <- { state with JustSawModule = true; PrevToken = Some token }
                yield token

            | Parser.MATCH ->
                // Mark that we just saw MATCH, will enter context on next NEWLINE
                state <- { state with JustSawMatch = true; PrevToken = Some token }
                yield token

            | Parser.TRY ->
                // Mark that we just saw TRY, will enter context on next NEWLINE
                state <- { state with JustSawTry = true; PrevToken = Some token }
                yield token

            | Parser.PIPE when state.JustSawMatch ->
                // PIPE on same line as MATCH → single-line match, no InMatch needed
                state <- { state with JustSawMatch = false; PrevToken = Some token }
                yield token

            | Parser.PIPE when state.JustSawTry ->
                // PIPE on same line as TRY → single-line try-with, no InTry needed
                state <- { state with JustSawTry = false; PrevToken = Some token }
                yield token

            | Parser.LET ->
                // Push InLetDecl if in expression context (blockLet = true)
                // Must be inside an indented block (depth > 1) AND in expression context
                let blockLet = state.IndentStack.Length > 1 && isExprContext state.Context
                if blockLet then
                    let offsideCol = state.IndentStack.Head
                    state <- { state with Context = InLetDecl(true, offsideCol) :: state.Context; PrevToken = Some token }
                else
                    state <- { state with PrevToken = Some token }
                yield token

            | Parser.IN ->
                // Explicit IN: pop the matching InLetDecl from context.
                // Also pop stale InMatch/InTry at the same indent level that were
                // pushed from single-line match/try (JustSawMatch consumed on wrong NEWLINE).
                let currentCol = state.IndentStack.Head
                let rec popLetDecl ctx =
                    match ctx with
                    | InLetDecl(_, _) :: rest -> rest
                    | InMatch col :: rest when col = currentCol -> popLetDecl rest
                    | InTry col :: rest when col = currentCol -> popLetDecl rest
                    | _ -> ctx
                state <- { state with Context = popLetDecl state.Context; PrevToken = Some token }
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

/// Filter a PositionedToken stream, converting NEWLINE(col) to INDENT/DEDENT while preserving positions.
/// Synthetic tokens (INDENT, DEDENT, SEMICOLON, IN) receive the position of the most recent real token.
let filterPositioned (config: IndentConfig) (tokens: PositionedToken list) : PositionedToken list =
    let result = System.Collections.Generic.List<PositionedToken>()
    let mutable state = initialState
    let mutable lastRealToken = { Token = Parser.EOF; StartPos = FSharp.Text.Lexing.Position.Empty; EndPos = FSharp.Text.Lexing.Position.Empty }
    let mutable index = 0

    while index < tokens.Length do
        let pt = tokens.[index]
        let token = pt.Token

        match token with
        | Parser.LBRACKET | Parser.LPAREN | Parser.LBRACE | Parser.DOTLBRACKET ->
            lastRealToken <- pt
            state <- { state with BracketDepth = state.BracketDepth + 1; PrevToken = Some token }
            result.Add(pt)

        | Parser.RBRACKET | Parser.RPAREN | Parser.RBRACE ->
            lastRealToken <- pt
            state <- { state with BracketDepth = max 0 (state.BracketDepth - 1); PrevToken = Some token }
            result.Add(pt)

        | Parser.NEWLINE _ when state.BracketDepth > 0 ->
            // Inside brackets: suppress INDENT/DEDENT, just advance line counter
            state <- { state with LineNum = state.LineNum + 1 }

        | Parser.NEWLINE _ when index + 1 < tokens.Length && (match tokens.[index + 1].Token with Parser.NEWLINE _ -> true | _ -> false) ->
            // Blank line: next token is also NEWLINE, skip without processing indentation.
            // This prevents blank lines from triggering premature DEDENT in module/let/match bodies.
            state <- { state with LineNum = state.LineNum + 1 }

        | Parser.NEWLINE col ->
            // Look ahead to next non-NEWLINE token
            let nextToken =
                tokens
                |> List.skip (index + 1)
                |> List.tryFind (fun p -> match p.Token with Parser.NEWLINE _ -> false | _ -> true)
                |> Option.map (fun p -> p.Token)

            let (newState_, emitted) = processNewlineWithContext config state col nextToken
            let newState = { newState_ with LineNum = state.LineNum + 1 }

            let nextIsExplicitIn = match nextToken with | Some Parser.IN -> true | _ -> false
            let isAtSameLevel =
                List.isEmpty emitted &&
                newState.IndentStack.Length > 1 &&
                not nextIsExplicitIn &&
                (match state.PrevToken with
                 | Some Parser.IN -> false
                 | _ -> true) &&
                (match newState.Context with
                 | InFunctionApp _ :: _ -> false
                 | _ -> true)

            if isAtSameLevel then
                let rec checkOffside ctx acc =
                    match ctx with
                    | InLetDecl(true, offsideCol) :: rest when col <= offsideCol ->
                        checkOffside rest (Parser.IN :: acc)
                    | _ -> (ctx, List.rev acc)
                let (newCtx, insTokens) = checkOffside newState.Context []
                if not (List.isEmpty insTokens) then
                    state <- { newState with Context = newCtx }
                    for t in insTokens do result.Add(withPosOf lastRealToken t)
                else
                    let shouldInjectSemicolon =
                        match newState.Context with
                        | InExprBlock _ :: _ ->
                            let suppressByNext =
                                match nextToken with
                                | Some t -> isContinuationStart t || isStructuralTerminator t
                                | None -> false
                            not suppressByNext
                        | _ -> false
                    if shouldInjectSemicolon then
                        state <- newState
                        result.Add(withPosOf lastRealToken Parser.SEMICOLON)
                    else
                        state <- newState
                        for t in emitted do result.Add(withPosOf lastRealToken t)
            else
                if List.contains Parser.DEDENT emitted then
                    if nextIsExplicitIn then
                        let rec popLetDecls ctx =
                            match ctx with
                            | InLetDecl(true, offsideCol) :: rest when newState.IndentStack.Head <= offsideCol ->
                                popLetDecls rest
                            | _ -> ctx
                        state <- { newState with Context = popLetDecls newState.Context }
                        for t in emitted do result.Add(withPosOf lastRealToken t)
                    else
                        let rec checkOffsideDedent ctx acc =
                            match ctx with
                            | InLetDecl(true, offsideCol) :: rest when newState.IndentStack.Head <= offsideCol ->
                                checkOffsideDedent rest (Parser.IN :: acc)
                            | _ -> (ctx, List.rev acc)
                        let (newCtx, insTokens) = checkOffsideDedent newState.Context []
                        state <- { newState with Context = newCtx }
                        for t in emitted do result.Add(withPosOf lastRealToken t)
                        for t in insTokens do result.Add(withPosOf lastRealToken t)
                else
                    state <- newState
                    if List.contains Parser.INDENT emitted then
                        if state.JustSawModule then
                            state <- { state with Context = InModule :: state.Context; JustSawModule = false }
                        else
                            match state.PrevToken with
                            | Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN | Some Parser.DO ->
                                let baseCol = match state.IndentStack with _ :: parent :: _ -> parent | _ -> 0
                                state <- { state with Context = InExprBlock(baseCol) :: state.Context }
                            | _ -> ()
                    for t in emitted do result.Add(withPosOf lastRealToken t)

        | Parser.EOF ->
            let rec emitPendingIns ctx acc =
                match ctx with
                | InLetDecl(true, _) :: rest -> emitPendingIns rest (Parser.IN :: acc)
                | _ -> (ctx, List.rev acc)
            let (ctxAfterIns, insTokens) = emitPendingIns state.Context []
            state <- { state with Context = ctxAfterIns }
            for t in insTokens do result.Add(withPosOf lastRealToken t)
            while state.IndentStack.Length > 1 do
                let (newState, toks) = processNewline config state 0
                state <- newState
                for t in toks do result.Add(withPosOf lastRealToken t)
            result.Add(pt)  // EOF with its own position

        | Parser.MODULE ->
            lastRealToken <- pt
            state <- { state with JustSawModule = true; PrevToken = Some token }
            result.Add(pt)

        | Parser.TYPECLASS | Parser.INSTANCE ->
            // Phase 71 (Type Classes): treat typeclass/instance bodies like module bodies
            lastRealToken <- pt
            state <- { state with JustSawModule = true; PrevToken = Some token }
            result.Add(pt)

        | Parser.MATCH ->
            lastRealToken <- pt
            state <- { state with JustSawMatch = true; PrevToken = Some token }
            result.Add(pt)

        | Parser.TRY ->
            lastRealToken <- pt
            state <- { state with JustSawTry = true; PrevToken = Some token }
            result.Add(pt)

        | Parser.PIPE when state.JustSawMatch ->
            lastRealToken <- pt
            state <- { state with JustSawMatch = false; PrevToken = Some token }
            result.Add(pt)

        | Parser.PIPE when state.JustSawTry ->
            lastRealToken <- pt
            state <- { state with JustSawTry = false; PrevToken = Some token }
            result.Add(pt)

        | Parser.LET ->
            lastRealToken <- pt
            let blockLet = state.IndentStack.Length > 1 && isExprContext state.Context
            if blockLet then
                let offsideCol = state.IndentStack.Head
                state <- { state with Context = InLetDecl(true, offsideCol) :: state.Context; PrevToken = Some token }
            else
                state <- { state with PrevToken = Some token }
            result.Add(pt)

        | Parser.IN ->
            lastRealToken <- pt
            let currentCol = state.IndentStack.Head
            let rec popLetDecl ctx =
                match ctx with
                | InLetDecl(_, _) :: rest -> rest
                | InMatch col :: rest when col = currentCol -> popLetDecl rest
                | InTry col :: rest when col = currentCol -> popLetDecl rest
                | _ -> ctx
            state <- { state with Context = popLetDecl state.Context; PrevToken = Some token }
            result.Add(pt)

        | Parser.DEDENT ->
            lastRealToken <- pt
            state <- updateContextOnDedent state
            result.Add(pt)

        | other ->
            lastRealToken <- pt
            state <- { state with PrevToken = Some other }
            result.Add(pt)

        index <- index + 1

    result |> Seq.toList
