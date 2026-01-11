module FunLang.Indentation

open FunLang.Ast
open FunLang.Errors
open FunLang.GeneratedParser

// =============================================================================
// Indentation State
// =============================================================================

type IndentState = {
    IndentStack: int list      // Stack of indentation levels, starts with [0]
    ParenDepth: int            // Parenthesis/bracket nesting depth
    AtLineStart: bool          // Whether at beginning of logical line
    CurrentLine: int           // Current line number for error reporting
}

let initialState = {
    IndentStack = []       // Empty stack - first token sets initial level
    ParenDepth = 0
    AtLineStart = true
    CurrentLine = 1
}

// =============================================================================
// Token with Position
// =============================================================================

type TokenWithPos = token * Position

// =============================================================================
// Helper Functions
// =============================================================================

/// Check if token opens a parenthesis/bracket
let private isOpenParen = function
    | LPAREN | LBRACKET -> true
    | _ -> false

/// Check if token closes a parenthesis/bracket
let private isCloseParen = function
    | RPAREN | RBRACKET -> true
    | _ -> false

/// Generate DEDENT tokens to close levels down to target column
let private generateDedents (stack: int list) (targetCol: int) (pos: Position)
    : Result<int list * token list, FunLangError> =
    let rec loop stk dedents =
        match stk with
        | [] ->
            // Should never happen - stack always has at least [0]
            Error (Error.indentation 0 targetCol pos)
        | top :: rest when top = targetCol ->
            // Found matching level
            Ok (stk, dedents)
        | top :: rest when top > targetCol ->
            // Need to pop this level
            loop rest (DEDENT :: dedents)
        | _ ->
            // targetCol doesn't match any level in stack
            Error (Error.indentation (List.head stack) targetCol pos)
    loop stack []

// =============================================================================
// Indentation Processing
// =============================================================================

/// Process raw token stream and insert INDENT/DEDENT/NEWLINE tokens
/// based on indentation levels (Python-style offside rule)
let processIndentation (tokens: TokenWithPos list) : Result<token list, FunLangError> =
    let rec loop (state: IndentState) (remaining: TokenWithPos list) (output: token list)
        : Result<token list, FunLangError> =
        match remaining with
        | [] ->
            // End of input - should not happen, EOF should be present
            Ok (List.rev output)

        | (EOF, _) :: _ ->
            // End of file - generate DEDENTs to close all open indentation levels
            // Keep only the base level (first element if exists)
            let rec closeLevels stk acc =
                match stk with
                | [] -> Ok (List.rev (EOF :: acc))
                | [_] -> Ok (List.rev (EOF :: acc))  // Base level, no DEDENT needed
                | _ :: rest -> closeLevels rest (DEDENT :: acc)
            closeLevels state.IndentStack output

        | (NEWLINE, _) :: rest ->
            // Newline token - if inside parens, skip; otherwise mark line start
            if state.ParenDepth > 0 then
                // Inside parens - ignore newline, continue without adding to output
                loop state rest output
            else
                // Outside parens - mark that next token is at line start
                loop { state with AtLineStart = true } rest output

        | (tok, pos) :: rest ->
            // First, update parenthesis depth for open/close parens
            let newParenDepth =
                if isOpenParen tok then state.ParenDepth + 1
                elif isCloseParen tok then max 0 (state.ParenDepth - 1)
                else state.ParenDepth

            // If inside parens (before this token), just output the token
            if state.ParenDepth > 0 then
                loop { state with ParenDepth = newParenDepth } rest (tok :: output)
            // Handle open paren specially - it starts paren mode, no indent processing
            elif isOpenParen tok then
                loop { state with ParenDepth = newParenDepth; AtLineStart = false } rest (tok :: output)
            // If at line start, check indentation
            elif state.AtLineStart then
                let col = pos.Column
                match state.IndentStack with
                | [] ->
                    // First token - set initial indentation level
                    loop { state with IndentStack = [col]; AtLineStart = false; ParenDepth = newParenDepth }
                         rest (tok :: output)
                | top :: _ when col > top ->
                    // Increased indentation - emit INDENT
                    let newStack = col :: state.IndentStack
                    loop { state with IndentStack = newStack; AtLineStart = false; ParenDepth = newParenDepth }
                         rest (tok :: INDENT :: output)
                | top :: _ when col = top ->
                    // Same level - emit NEWLINE as statement separator (if there's output)
                    if output <> [] then
                        loop { state with AtLineStart = false; ParenDepth = newParenDepth }
                             rest (tok :: NEWLINE :: output)
                    else
                        loop { state with AtLineStart = false; ParenDepth = newParenDepth }
                             rest (tok :: output)
                | _ ->
                    // Decreased indentation - emit DEDENT(s)
                    // Add NEWLINE after DEDENTs to separate from next item at top-level
                    match generateDedents state.IndentStack col pos with
                    | Error e -> Error e
                    | Ok (newStack, dedents) ->
                        // Insert NEWLINE between DEDENTs and next token for proper separation
                        let newOutput = tok :: NEWLINE :: (dedents @ output)
                        loop { state with IndentStack = newStack; AtLineStart = false; ParenDepth = newParenDepth }
                             rest newOutput
            else
                // Not at line start - just output the token
                loop { state with ParenDepth = newParenDepth } rest (tok :: output)

    loop initialState tokens []
