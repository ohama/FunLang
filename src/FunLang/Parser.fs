module FunLang.Parser

open FunLang.Ast
open FunLang.Errors

// =============================================================================
// Parser State
// =============================================================================

type private ParserState = {
    Tokens: Token list
    Position: int
}

let private initialState tokens = {
    Tokens = tokens
    Position = 0
}

// =============================================================================
// Helper Functions
// =============================================================================

let private isAtEnd state =
    state.Position >= List.length state.Tokens

let private peek state =
    if isAtEnd state then None
    else Some (List.item state.Position state.Tokens)

let private peekNext state =
    if state.Position + 1 >= List.length state.Tokens then None
    else Some (List.item (state.Position + 1) state.Tokens)

let private advance state =
    if isAtEnd state then state
    else { state with Position = state.Position + 1 }

let private current state =
    if isAtEnd state then EOF
    else List.item state.Position state.Tokens

let private consume expected state =
    match peek state with
    | Some tok when tok = expected -> Ok (advance state)
    | Some tok -> Error (sprintf "Expected %A but got %A" expected tok)
    | None -> Error (sprintf "Expected %A but got end of input" expected)

let private expect predicate msg state =
    match peek state with
    | Some tok when predicate tok -> Ok (tok, advance state)
    | Some tok -> Error (sprintf "%s, got %A" msg tok)
    | None -> Error (sprintf "%s, got end of input" msg)

// =============================================================================
// Operator Precedence
// =============================================================================

// Precedence levels (higher = binds tighter)
// 1: || (or)
// 2: && (and)
// 3: ==, !=
// 4: <, >, <=, >=
// 5: +, -
// 6: *, /, %
// 7: unary -, not
// 8: function application

let private getPrecedence = function
    | OR -> Some 1
    | AND -> Some 2
    | EQ | NEQ -> Some 3
    | LT | GT | LTE | GTE -> Some 4
    | PLUS | MINUS -> Some 5
    | STAR | SLASH | PERCENT -> Some 6
    | _ -> None

let private tokenToBinaryOp = function
    | PLUS -> Some Add
    | MINUS -> Some Sub
    | STAR -> Some Mul
    | SLASH -> Some Div
    | PERCENT -> Some Mod
    | EQ -> Some Eq
    | NEQ -> Some Neq
    | LT -> Some Lt
    | GT -> Some Gt
    | LTE -> Some Lte
    | GTE -> Some Gte
    | AND -> Some And
    | OR -> Some Or
    | _ -> None

// =============================================================================
// Recursive Descent Parser
// =============================================================================

// Forward declaration for mutual recursion
let rec private parseExpr state = parseLetOrIf state

// let x = e1 in e2
and private parseLetOrIf state =
    match peek state with
    | Some LET -> parseLet state
    | Some IF -> parseIf state
    | _ -> parseOr state

and private parseLet state =
    // consume 'let'
    let state = advance state

    // get identifier
    match peek state with
    | Some (IDENT name) ->
        let state = advance state

        // consume '='
        match consume EQ state with
        | Error e -> Error e
        | Ok state ->
            // parse value expression
            match parseExpr state with
            | Error e -> Error e
            | Ok (value, state) ->
                // consume 'in'
                match consume IN state with
                | Error e -> Error e
                | Ok state ->
                    // parse body expression
                    match parseExpr state with
                    | Error e -> Error e
                    | Ok (body, state) ->
                        Ok (ELet (name, value, body), state)
    | _ -> Error "Expected identifier after 'let'"

and private parseIf state =
    // consume 'if'
    let state = advance state

    // parse condition
    match parseExpr state with
    | Error e -> Error e
    | Ok (cond, state) ->
        // consume 'then'
        match consume THEN state with
        | Error e -> Error e
        | Ok state ->
            // parse then branch
            match parseExpr state with
            | Error e -> Error e
            | Ok (thenBr, state) ->
                // consume 'else'
                match consume ELSE state with
                | Error e -> Error e
                | Ok state ->
                    // parse else branch
                    match parseExpr state with
                    | Error e -> Error e
                    | Ok (elseBr, state) ->
                        Ok (EIf (cond, thenBr, elseBr), state)

// Binary operators with precedence climbing
and private parseOr state = parseBinaryOp 1 state

and private parseBinaryOp minPrec state =
    match parseUnary state with
    | Error e -> Error e
    | Ok (left, state) ->
        parseBinaryOpLoop minPrec left state

and private parseBinaryOpLoop minPrec left state =
    match peek state with
    | Some tok ->
        match getPrecedence tok with
        | Some prec when prec >= minPrec ->
            match tokenToBinaryOp tok with
            | Some op ->
                let state = advance state
                // Parse right side with higher precedence (left-associative)
                match parseBinaryOp (prec + 1) state with
                | Error e -> Error e
                | Ok (right, state) ->
                    let expr = EBinaryOp (op, left, right)
                    parseBinaryOpLoop minPrec expr state
            | None -> Ok (left, state)
        | _ -> Ok (left, state)
    | None -> Ok (left, state)

and private parseUnary state =
    match peek state with
    | Some MINUS ->
        let state = advance state
        match parseUnary state with
        | Error e -> Error e
        | Ok (expr, state) -> Ok (EUnaryOp (Neg, expr), state)
    | Some NOT ->
        let state = advance state
        match parseUnary state with
        | Error e -> Error e
        | Ok (expr, state) -> Ok (EUnaryOp (Not, expr), state)
    | _ -> parsePrimary state

and private parsePrimary state =
    match peek state with
    | Some (INT n) ->
        Ok (ELiteral (LInt n), advance state)

    | Some (BOOL b) ->
        Ok (ELiteral (LBool b), advance state)

    | Some (STRING s) ->
        Ok (ELiteral (LString s), advance state)

    | Some (IDENT name) ->
        Ok (EVariable name, advance state)

    | Some LPAREN ->
        let state = advance state
        match parseExpr state with
        | Error e -> Error e
        | Ok (expr, state) ->
            match consume RPAREN state with
            | Error e -> Error e
            | Ok state -> Ok (expr, state)

    | Some tok ->
        Error (sprintf "Unexpected token: %A" tok)

    | None ->
        Error "Unexpected end of input"

// =============================================================================
// Public API
// =============================================================================

/// Parse a list of tokens into an AST
let parse (tokens: Token list) : Result<Expr, string> =
    if List.isEmpty tokens then
        Error "Empty input"
    else
        let state = initialState tokens
        match parseExpr state with
        | Error e -> Error e
        | Ok (expr, state) ->
            // Check for remaining tokens (except EOF is ok)
            match peek state with
            | None -> Ok expr
            | Some EOF -> Ok expr
            | Some tok -> Error (sprintf "Unexpected token after expression: %A" tok)
