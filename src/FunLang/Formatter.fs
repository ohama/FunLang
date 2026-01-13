module FunLang.Formatter

open FunLang.Ast
open FunLang.CommentCollector

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

// =============================================================================
// Comment-Aware Formatting
// =============================================================================

/// Build a map from line number to comments on that line
let private buildLineCommentMap (comments: Comment list) : Map<int, Comment> =
    comments
    |> List.map (fun c -> (c.Pos.Line, c))
    |> Map.ofList

/// Get leading comments for a line (comments on lines above)
let private getLeadingCommentsForLine (commentMap: Map<int, Comment>) (targetLine: int) : Comment list =
    // Find comments from the line before targetLine going back
    let rec collectLeading line acc =
        if line < 1 then acc
        else
            match Map.tryFind line commentMap with
            | Some comment -> collectLeading (line - 1) (comment :: acc)
            | None -> acc
    collectLeading (targetLine - 1) []

/// Get trailing comment for a line (comment on the same line)
let private getTrailingCommentForLine (comments: Comment list) (targetLine: int) : Comment option =
    comments
    |> List.tryFind (fun c -> c.Pos.Line = targetLine)

/// Format an expression with leading and trailing comments
let private formatExprWithComments (indent: int) (lexpr: LExpr) (comments: Comment list) : string =
    let spaces = String.replicate indent " "
    let line = lexpr.Pos.Line

    // Get leading comments for this expression
    let leadingComments =
        comments
        |> List.filter (fun c -> c.Pos.Line < line)
        |> List.sortBy (fun c -> c.Pos.Line)

    // Get trailing comment on the same line as the expression starts
    let trailingComment = getTrailingCommentForLine comments line

    // Format leading comments
    let leadingStr =
        leadingComments
        |> List.map (fun c -> sprintf "%s//%s\n" spaces c.Text)
        |> String.concat ""

    // Format the expression itself
    let exprStr = formatExprIndent indent lexpr

    // Format trailing comment
    let trailingStr =
        match trailingComment with
        | Some c -> sprintf "  //%s" c.Text
        | None -> ""

    leadingStr + exprStr + trailingStr

/// Recursively format an expression, inserting comments at appropriate positions
let rec private formatExprWithCommentsRecursive (indent: int) (lexpr: LExpr) (comments: Comment list) : string =
    let spaces = String.replicate indent " "
    let exprLine = lexpr.Pos.Line

    // Find leading comments (on lines immediately before this expression)
    let (leadingComments, remainingComments) =
        comments
        |> List.partition (fun c -> c.Pos.Line < exprLine)

    // Find trailing comment (on the same line as expression)
    let (trailingCommentOpt, childComments) =
        let onSameLine = remainingComments |> List.tryFind (fun c -> c.Pos.Line = exprLine)
        let rest = remainingComments |> List.filter (fun c -> c.Pos.Line <> exprLine)
        (onSameLine, rest)

    // Format leading comments
    let leadingStr =
        leadingComments
        |> List.sortBy (fun c -> c.Pos.Line)
        |> List.map (fun c -> sprintf "%s//%s\n" spaces c.Text)
        |> String.concat ""

    // Format expression based on type
    // For compound expressions (let, match, etc.), pass trailing comment to be placed correctly
    let exprStr =
        match lexpr.Node with
        | ELet (name, value, body) ->
            formatLetWithComments indent "let" name value body trailingCommentOpt childComments
        | ELetRec (name, value, body) ->
            formatLetWithComments indent "let rec" name value body trailingCommentOpt childComments
        | EMatch (scrut, cases) ->
            formatMatchWithComments indent scrut cases trailingCommentOpt childComments
        | EBlock exprs ->
            formatBlockWithComments indent exprs childComments
        | _ ->
            let baseStr = formatExprIndent indent lexpr
            // For simple expressions, add trailing comment at the end
            match trailingCommentOpt with
            | Some c -> baseStr + sprintf "  //%s" c.Text
            | None -> baseStr

    leadingStr + exprStr

and private formatLetWithComments indent keyword name value body (trailingComment: Comment option) comments : string =
    let nextIndent = indent + defaultIndent

    // Trailing comment goes right after the let binding line
    let trailingStr =
        match trailingComment with
        | Some c -> sprintf "  //%s" c.Text
        | None -> ""

    let (valueStr, prefixSpace) =
        match value.Node with
        | ELambda _ | EIf _ | EMatch _ | ELet _ | ELetRec _ ->
            // Multi-line value: newline after =, no space needed
            let str = sprintf "%s\n%s%s"
                        trailingStr
                        (String.replicate nextIndent " ")
                        (formatExprIndent nextIndent value)
            (str, "")
        | _ ->
            // Single-line value: space before value, trailing comment after value
            let str = sprintf "%s%s" (formatExpr 0 value) trailingStr
            (str, " ")

    // Get comments for body (include leading comments that appear before body line)
    // Comments are passed to recursive call which will handle leading/trailing classification
    let bodyStr = formatExprWithCommentsRecursive indent body comments
    sprintf "%s %s =%s%s\n%s" keyword name prefixSpace valueStr bodyStr

and private formatMatchWithComments indent scrut cases (trailingComment: Comment option) comments : string =
    let spaces = String.replicate indent " "
    let caseSpaces = String.replicate (indent + defaultIndent) " "

    // Trailing comment goes after "match ... with"
    let trailingStr =
        match trailingComment with
        | Some c -> sprintf "  //%s" c.Text
        | None -> ""

    let casesStr =
        cases
        |> List.map (fun (pat, guardOpt, body) ->
            // Get comments for this case body
            let caseBodyComments =
                comments
                |> List.filter (fun c -> c.Pos.Line >= body.Pos.Line)
            formatCaseWithComments (indent + defaultIndent) (pat, guardOpt, body) caseBodyComments)
        |> String.concat (sprintf "\n%s" caseSpaces)

    sprintf "match %s with%s\n%s%s"
        (formatExpr 0 scrut)
        trailingStr
        caseSpaces
        casesStr

and private formatCaseWithComments indent (pat, guardOpt, body) comments : string =
    let patStr = formatPattern pat
    let guardStr =
        match guardOpt with
        | Some guard -> sprintf " when %s" (formatExpr 0 guard)
        | None -> ""

    // Format body with comments
    let bodyStr =
        match body.Node with
        | ELet _ | ELetRec _ | EIf _ | EMatch _ | EBlock _ ->
            sprintf "\n%s%s"
                (String.replicate (indent + defaultIndent) " ")
                (formatExprWithCommentsRecursive (indent + defaultIndent) body comments)
        | _ -> formatExpr 0 body

    sprintf "| %s%s -> %s" patStr guardStr bodyStr

and private formatBlockWithComments indent exprs comments : string =
    let spaces = String.replicate indent " "
    exprs
    |> List.map (fun expr ->
        let exprComments =
            comments
            |> List.filter (fun c -> c.Pos.Line <= expr.Pos.Line || c.Pos.Line < (expr.Pos.Line + 10))
        formatExprWithCommentsRecursive indent expr exprComments)
    |> String.concat (sprintf "\n%s" spaces)

/// Format a program with comments preserved
let formatProgramWithComments (program: Program) (comments: Comment list) : string =
    // Group comments by their relationship to AST nodes
    let commentMap = buildLineCommentMap comments

    // Find comments that don't belong to any expression (file-level leading comments)
    let firstExprLine =
        match program.MainExpr with
        | Some expr -> expr.Pos.Line
        | None ->
            match program.TypeDefs with
            | [] -> 0
            | td :: _ -> 1  // Type defs start at line 1

    // File-level leading comments (before any code)
    let fileLevelComments =
        comments
        |> List.filter (fun c -> c.Pos.Line < firstExprLine)
        |> List.sortBy (fun c -> c.Pos.Line)

    let fileLevelStr =
        fileLevelComments
        |> List.map (fun c -> sprintf "//%s\n" c.Text)
        |> String.concat ""

    // Type definitions (currently no comment support)
    let typeDefsStr =
        if List.isEmpty program.TypeDefs then ""
        else formatTypeDefs program.TypeDefs + "\n\n"

    // Main expression with comments
    let exprStr =
        match program.MainExpr with
        | Some expr ->
            // Get comments associated with the main expression
            let exprComments =
                comments
                |> List.filter (fun c -> c.Pos.Line >= firstExprLine)
            formatExprWithCommentsRecursive 0 expr exprComments
        | None -> ""

    fileLevelStr + typeDefsStr + exprStr
