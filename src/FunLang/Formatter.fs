module FunLang.Formatter

open FunLang.Ast

// =============================================================================
// Operator Precedence and Associativity
// =============================================================================

type Associativity = Left | Right | NonAssoc

/// Get precedence and associativity for binary operators
/// Higher precedence = binds tighter
let opPrecedence = function
    | Or -> (1, Left)
    | And -> (2, Left)
    | Eq | Neq -> (3, NonAssoc)
    | Lt | Gt | Lte | Gte -> (4, NonAssoc)
    | Add | Sub -> (5, Left)
    | Mul | Div | Mod -> (6, Left)

/// Convert binary operator to string
let opToString = function
    | Add -> "+"
    | Sub -> "-"
    | Mul -> "*"
    | Div -> "/"
    | Mod -> "%"
    | Eq -> "="
    | Neq -> "<>"
    | Lt -> "<"
    | Gt -> ">"
    | Lte -> "<="
    | Gte -> ">="
    | And -> "and"
    | Or -> "or"

// =============================================================================
// String Escaping
// =============================================================================

let escapeString (s: string) : string =
    s
    |> String.collect (function
        | '\\' -> "\\\\"
        | '"' -> "\\\""
        | '\n' -> "\\n"
        | '\t' -> "\\t"
        | '\r' -> "\\r"
        | c -> string c)

// =============================================================================
// Literal Formatting
// =============================================================================

let formatLiteral = function
    | LInt n -> string n
    | LBool true -> "true"
    | LBool false -> "false"
    | LString s -> sprintf "\"%s\"" (escapeString s)
    | LUnit -> "()"

// =============================================================================
// Pattern Formatting
// =============================================================================

/// Check if pattern needs parentheses in constructor argument position
let patternNeedsParens (lpat: LPattern) : bool =
    match lpat.Node with
    | PCons _ -> true
    | PConstructor (_, Some _) -> true
    | _ -> false

let rec formatPattern (lpat: LPattern) : string =
    match lpat.Node with
    | PWildcard -> "_"
    | PVariable name -> name
    | PLiteral lit -> formatLiteral lit
    | PTuple ps ->
        ps |> List.map formatPattern |> String.concat ", " |> sprintf "(%s)"
    | PList ps ->
        ps |> List.map formatPattern |> String.concat "; " |> sprintf "[%s]"
    | PCons (h, t) ->
        sprintf "%s :: %s" (formatPattern h) (formatPattern t)
    | PConstructor (name, None) -> name
    | PConstructor (name, Some arg) ->
        if patternNeedsParens arg then
            sprintf "%s (%s)" name (formatPattern arg)
        else
            sprintf "%s %s" name (formatPattern arg)

// =============================================================================
// Type Expression Formatting
// =============================================================================

let rec formatTypeExpr = function
    | TEVar name -> sprintf "'%s" name
    | TEName name -> name
    | TEApp (name, args) ->
        let argsStr = args |> List.map formatTypeExpr |> String.concat " "
        sprintf "%s %s" name argsStr
    | TETuple ts ->
        ts |> List.map formatTypeExpr |> String.concat " * "

// =============================================================================
// Constructor and Type Definition Formatting
// =============================================================================

let formatConstructor (name, argOpt) =
    match argOpt with
    | None -> name
    | Some arg -> sprintf "%s of %s" name (formatTypeExpr arg)

let formatTypeDef (td: TypeDef) : string =
    let paramsStr =
        if List.isEmpty td.TypeParams then ""
        else td.TypeParams |> List.map (sprintf "'%s") |> String.concat " " |> sprintf " %s"
    let ctorsStr =
        td.Constructors
        |> List.map formatConstructor
        |> String.concat " | "
    sprintf "type %s%s = %s" td.Name paramsStr ctorsStr

// =============================================================================
// Expression Formatting
// =============================================================================

/// Precedence levels for non-binary expressions
let appPrecedence = 9       // function application
let atomPrecedence = 10     // literals, variables (highest)
let consPrecedence = 7      // :: operator
let lambdaPrecedence = 0    // lowest

/// Check if expression needs parentheses at the given precedence level
let exprNeedsParens parentPrec (lexpr: LExpr) : bool =
    match lexpr.Node with
    | ELiteral _ | EVariable _ | ETuple _ | EList _ -> false
    | EBinaryOp (op, _, _) ->
        let (prec, _) = opPrecedence op
        prec < parentPrec
    | EUnaryOp _ -> atomPrecedence < parentPrec
    | ELambda _ -> lambdaPrecedence < parentPrec
    | EApply _ -> appPrecedence < parentPrec
    | ECons _ -> consPrecedence < parentPrec
    | ELet _ | ELetRec _ | EIf _ | EMatch _ | EBlock _ -> true
    | EConstructor (_, Some _) -> appPrecedence < parentPrec
    | EConstructor (_, None) -> false

/// Format binary operation with proper parenthesization
let rec formatBinaryOp parentPrec op left right =
    let (prec, assoc) = opPrecedence op

    // Determine required precedence for left and right operands
    let leftPrec =
        match assoc with
        | Left -> prec
        | Right | NonAssoc -> prec + 1
    let rightPrec =
        match assoc with
        | Right -> prec
        | Left | NonAssoc -> prec + 1

    let leftStr = formatExpr leftPrec left
    let rightStr = formatExpr rightPrec right
    let result = sprintf "%s %s %s" leftStr (opToString op) rightStr

    if prec < parentPrec then
        sprintf "(%s)" result
    else
        result

/// Format expression with precedence-based parenthesization
and formatExpr (parentPrec: int) (lexpr: LExpr) : string =
    match lexpr.Node with
    | ELiteral lit -> formatLiteral lit
    | EVariable name -> name

    | EBinaryOp (op, left, right) ->
        formatBinaryOp parentPrec op left right

    | EUnaryOp (Neg, e) ->
        let inner = formatExpr atomPrecedence e
        sprintf "-%s" inner

    | EUnaryOp (Not, e) ->
        let inner = formatExpr atomPrecedence e
        sprintf "not %s" inner

    | ELambda (param, body) ->
        let result = sprintf "fun %s -> %s" param (formatExpr 0 body)
        if parentPrec > lambdaPrecedence then
            sprintf "(%s)" result
        else
            result

    | EApply (func, arg) ->
        let funcStr = formatExpr appPrecedence func
        let argStr = formatExpr atomPrecedence arg
        let result = sprintf "%s %s" funcStr argStr
        if parentPrec > appPrecedence then
            sprintf "(%s)" result
        else
            result

    | ETuple es ->
        es |> List.map (formatExpr 0) |> String.concat ", " |> sprintf "(%s)"

    | EList es ->
        es |> List.map (formatExpr 0) |> String.concat "; " |> sprintf "[%s]"

    | ECons (h, t) ->
        let hStr = formatExpr (consPrecedence + 1) h
        let tStr = formatExpr consPrecedence t
        let result = sprintf "%s :: %s" hStr tStr
        if parentPrec > consPrecedence then
            sprintf "(%s)" result
        else
            result

    | EConstructor (name, None) -> name
    | EConstructor (name, Some arg) ->
        let argStr = formatExpr atomPrecedence arg
        let result = sprintf "%s %s" name argStr
        if parentPrec > appPrecedence then
            sprintf "(%s)" result
        else
            result

    | ELet (name, value, body) ->
        formatLet "let" name value body

    | ELetRec (name, value, body) ->
        formatLet "let rec" name value body

    | EIf (cond, thenE, elseE) ->
        sprintf "if %s then %s else %s"
            (formatExpr 0 cond)
            (formatExpr 0 thenE)
            (formatExpr 0 elseE)

    | EMatch (scrut, cases) ->
        let casesStr = cases |> List.map formatCase |> String.concat " "
        sprintf "match %s with %s" (formatExpr 0 scrut) casesStr

    | EBlock exprs ->
        exprs |> List.map (formatExpr 0) |> String.concat "\n"

and formatLet keyword name value body =
    sprintf "%s %s = %s\n%s" keyword name (formatExpr 0 value) (formatExpr 0 body)

and formatCase (pat, guardOpt, body) =
    let patStr = formatPattern pat
    let guardStr =
        match guardOpt with
        | Some guard -> sprintf " when %s" (formatExpr 0 guard)
        | None -> ""
    sprintf "| %s%s -> %s" patStr guardStr (formatExpr 0 body)

// =============================================================================
// Indentation-Aware Formatting
// =============================================================================

let defaultIndent = 2

/// Format expression with proper indentation
let rec formatExprIndent (indent: int) (lexpr: LExpr) : string =
    let spaces = String.replicate indent " "

    match lexpr.Node with
    | ELet (name, value, body) ->
        formatLetIndent indent "let" name value body

    | ELetRec (name, value, body) ->
        formatLetIndent indent "let rec" name value body

    | EIf (cond, thenE, elseE) ->
        formatIfIndent indent cond thenE elseE

    | EMatch (scrut, cases) ->
        formatMatchIndent indent scrut cases

    | EBlock exprs ->
        exprs
        |> List.map (formatExprIndent indent)
        |> String.concat (sprintf "\n%s" spaces)

    | ELambda (param, body) ->
        sprintf "fun %s ->\n%s%s"
            param
            (String.replicate (indent + defaultIndent) " ")
            (formatExprIndent (indent + defaultIndent) body)

    | _ -> formatExpr 0 lexpr

and formatLetIndent indent keyword name value body =
    let nextIndent = indent + defaultIndent
    let valueStr =
        match value.Node with
        | ELambda _ | EIf _ | EMatch _ | ELet _ | ELetRec _ ->
            sprintf "\n%s%s"
                (String.replicate nextIndent " ")
                (formatExprIndent nextIndent value)
        | _ -> formatExpr 0 value
    let bodyStr = formatExprIndent indent body
    sprintf "%s %s = %s\n%s" keyword name valueStr bodyStr

and formatIfIndent indent cond thenE elseE =
    let spaces = String.replicate indent " "
    let nextSpaces = String.replicate (indent + defaultIndent) " "

    // Simple case: inline if both branches are simple
    match (thenE.Node, elseE.Node) with
    | (ELiteral _ | EVariable _ | ETuple _ | EList _ | EConstructor (_, None)),
      (ELiteral _ | EVariable _ | ETuple _ | EList _ | EConstructor (_, None)) ->
        sprintf "if %s then %s else %s"
            (formatExpr 0 cond)
            (formatExpr 0 thenE)
            (formatExpr 0 elseE)
    | _ ->
        sprintf "if %s then\n%s%s\n%selse\n%s%s"
            (formatExpr 0 cond)
            nextSpaces
            (formatExprIndent (indent + defaultIndent) thenE)
            spaces
            nextSpaces
            (formatExprIndent (indent + defaultIndent) elseE)

and formatMatchIndent indent scrut cases =
    let spaces = String.replicate indent " "
    let caseSpaces = String.replicate (indent + defaultIndent) " "
    let casesStr =
        cases
        |> List.map (formatCaseIndent (indent + defaultIndent))
        |> String.concat (sprintf "\n%s" caseSpaces)
    sprintf "match %s with\n%s%s"
        (formatExpr 0 scrut)
        caseSpaces
        casesStr

and formatCaseIndent indent (pat, guardOpt, body) =
    let patStr = formatPattern pat
    let guardStr =
        match guardOpt with
        | Some guard -> sprintf " when %s" (formatExpr 0 guard)
        | None -> ""
    let bodyStr =
        match body.Node with
        | ELet _ | ELetRec _ | EIf _ | EMatch _ | EBlock _ ->
            sprintf "\n%s%s"
                (String.replicate (indent + defaultIndent) " ")
                (formatExprIndent (indent + defaultIndent) body)
        | _ -> formatExpr 0 body
    sprintf "| %s%s -> %s" patStr guardStr bodyStr

// =============================================================================
// Public API
// =============================================================================

/// Format an expression to source code (compact single-line format)
let format (lexpr: LExpr) : string =
    formatExpr 0 lexpr

/// Format an expression with indentation (multi-line format)
let formatIndented (lexpr: LExpr) : string =
    formatExprIndent 0 lexpr

/// Format type definitions
let formatTypeDefs (typeDefs: TypeDef list) : string =
    typeDefs
    |> List.map formatTypeDef
    |> String.concat "\n"

/// Format a complete program (type definitions + main expression)
let formatProgram (program: Program) : string =
    let typeDefsStr =
        if List.isEmpty program.TypeDefs then ""
        else formatTypeDefs program.TypeDefs + "\n\n"

    let exprStr =
        match program.MainExpr with
        | Some expr -> formatExprIndent 0 expr
        | None -> ""

    typeDefsStr + exprStr

/// Format a complete program with type definitions and expression
let formatWithTypeDefs (typeDefs: TypeDef list) (lexpr: LExpr) : string =
    formatProgram { TypeDefs = typeDefs; MainExpr = Some lexpr }
