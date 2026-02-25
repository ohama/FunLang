module Format

open System
open FSharp.Text.Lexing

/// Format a single token as a string
let formatToken (token: Parser.token) : string =
    match token with
    | Parser.NUMBER n -> sprintf "NUMBER(%d)" n
    | Parser.IDENT s -> sprintf "IDENT(%s)" s
    | Parser.PLUS -> "PLUS"
    | Parser.MINUS -> "MINUS"
    | Parser.STAR -> "STAR"
    | Parser.SLASH -> "SLASH"
    | Parser.LPAREN -> "LPAREN"
    | Parser.RPAREN -> "RPAREN"
    | Parser.LET -> "LET"
    | Parser.IN -> "IN"
    | Parser.EQUALS -> "EQUALS"
    // Phase 4: Control flow tokens
    | Parser.TRUE -> "TRUE"
    | Parser.FALSE -> "FALSE"
    | Parser.IF -> "IF"
    | Parser.THEN -> "THEN"
    | Parser.ELSE -> "ELSE"
    // Phase 4: Comparison operator tokens
    | Parser.LT -> "LT"
    | Parser.GT -> "GT"
    | Parser.LE -> "LE"
    | Parser.GE -> "GE"
    | Parser.NE -> "NE"
    // Phase 4: Logical operator tokens
    | Parser.AND -> "AND"
    | Parser.OR -> "OR"
    // Phase 5: Function tokens
    | Parser.FUN -> "FUN"
    | Parser.REC -> "REC"
    | Parser.ARROW -> "ARROW"
    // Phase 2 (v2.0): String token
    | Parser.STRING s -> sprintf "STRING(%s)" s
    // Phase 1 (v3.0): Tuple tokens
    | Parser.COMMA -> "COMMA"
    | Parser.UNDERSCORE -> "UNDERSCORE"
    // Phase 2 (v3.0): List tokens
    | Parser.LBRACKET -> "LBRACKET"
    | Parser.RBRACKET -> "RBRACKET"
    | Parser.CONS -> "CONS"
    // Phase 3 (v3.0): Match tokens
    | Parser.MATCH -> "MATCH"
    | Parser.WITH -> "WITH"
    | Parser.PIPE -> "PIPE"
    // v6.0: Type annotation tokens
    | Parser.COLON -> "COLON"
    | Parser.TYPE_INT -> "TYPE_INT"
    | Parser.TYPE_BOOL -> "TYPE_BOOL"
    | Parser.TYPE_STRING -> "TYPE_STRING"
    | Parser.TYPE_LIST -> "TYPE_LIST"
    | Parser.TYPE_VAR s -> sprintf "TYPE_VAR(%s)" s
    | Parser.EOF -> "EOF"

/// Format a list of tokens as a space-separated string
let formatTokens (tokens: Parser.token list) : string =
    tokens |> List.map formatToken |> String.concat " "

/// Tokenize input string into a token list
let lex (input: string) : Parser.token list =
    let lexbuf = LexBuffer<char>.FromString input
    let rec loop acc =
        let token = Lexer.tokenize lexbuf
        match token with
        | Parser.EOF -> List.rev (Parser.EOF :: acc)
        | t -> loop (t :: acc)
    loop []

/// Format AST as a string (without span information for readable output)
let rec formatAst (expr: Ast.Expr) : string =
    match expr with
    | Ast.Number (n, _) -> sprintf "Number %d" n
    | Ast.Bool (b, _) -> sprintf "Bool %b" b
    | Ast.String (s, _) -> sprintf "String \"%s\"" s
    | Ast.Var (name, _) -> sprintf "Var \"%s\"" name
    | Ast.Add (l, r, _) -> sprintf "Add (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Subtract (l, r, _) -> sprintf "Subtract (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Multiply (l, r, _) -> sprintf "Multiply (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Divide (l, r, _) -> sprintf "Divide (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Negate (e, _) -> sprintf "Negate (%s)" (formatAst e)
    | Ast.LessThan (l, r, _) -> sprintf "LessThan (%s, %s)" (formatAst l) (formatAst r)
    | Ast.GreaterThan (l, r, _) -> sprintf "GreaterThan (%s, %s)" (formatAst l) (formatAst r)
    | Ast.LessEqual (l, r, _) -> sprintf "LessEqual (%s, %s)" (formatAst l) (formatAst r)
    | Ast.GreaterEqual (l, r, _) -> sprintf "GreaterEqual (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Equal (l, r, _) -> sprintf "Equal (%s, %s)" (formatAst l) (formatAst r)
    | Ast.NotEqual (l, r, _) -> sprintf "NotEqual (%s, %s)" (formatAst l) (formatAst r)
    | Ast.And (l, r, _) -> sprintf "And (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Or (l, r, _) -> sprintf "Or (%s, %s)" (formatAst l) (formatAst r)
    | Ast.If (cond, t, e, _) -> sprintf "If (%s, %s, %s)" (formatAst cond) (formatAst t) (formatAst e)
    | Ast.Let (name, value, body, _) -> sprintf "Let (\"%s\", %s, %s)" name (formatAst value) (formatAst body)
    | Ast.LetRec (name, param, body, expr, _) ->
        sprintf "LetRec (\"%s\", \"%s\", %s, %s)" name param (formatAst body) (formatAst expr)
    | Ast.Lambda (param, body, _) -> sprintf "Lambda (\"%s\", %s)" param (formatAst body)
    | Ast.LambdaAnnot (param, tyExpr, body, _) ->
        sprintf "LambdaAnnot (\"%s\", %s, %s)" param (formatTypeExpr tyExpr) (formatAst body)
    | Ast.Annot (e, tyExpr, _) ->
        sprintf "Annot (%s, %s)" (formatAst e) (formatTypeExpr tyExpr)
    | Ast.App (f, a, _) -> sprintf "App (%s, %s)" (formatAst f) (formatAst a)
    | Ast.Tuple (exprs, _) ->
        let formatted = exprs |> List.map formatAst |> String.concat ", "
        sprintf "Tuple [%s]" formatted
    | Ast.LetPat (pat, value, body, _) ->
        sprintf "LetPat (%s, %s, %s)" (formatPattern pat) (formatAst value) (formatAst body)
    | Ast.EmptyList _ -> "EmptyList"
    | Ast.List (exprs, _) ->
        let formatted = exprs |> List.map formatAst |> String.concat ", "
        sprintf "List [%s]" formatted
    | Ast.Cons (h, t, _) -> sprintf "Cons (%s, %s)" (formatAst h) (formatAst t)
    | Ast.Match (scrut, clauses, _) ->
        let formattedClauses =
            clauses
            |> List.map (fun (pat, expr) -> sprintf "(%s, %s)" (formatPattern pat) (formatAst expr))
            |> String.concat "; "
        sprintf "Match (%s, [%s])" (formatAst scrut) formattedClauses

/// Format TypeExpr as string
and formatTypeExpr (te: Ast.TypeExpr) : string =
    match te with
    | Ast.TEInt -> "TEInt"
    | Ast.TEBool -> "TEBool"
    | Ast.TEString -> "TEString"
    | Ast.TEList t -> sprintf "TEList (%s)" (formatTypeExpr t)
    | Ast.TEArrow (t1, t2) -> sprintf "TEArrow (%s, %s)" (formatTypeExpr t1) (formatTypeExpr t2)
    | Ast.TETuple ts ->
        let formatted = ts |> List.map formatTypeExpr |> String.concat ", "
        sprintf "TETuple [%s]" formatted
    | Ast.TEVar name -> sprintf "TEVar \"%s\"" name

/// Format Pattern as string
and formatPattern (pat: Ast.Pattern) : string =
    match pat with
    | Ast.VarPat (name, _) -> sprintf "VarPat \"%s\"" name
    | Ast.WildcardPat _ -> "WildcardPat"
    | Ast.TuplePat (pats, _) ->
        let formatted = pats |> List.map formatPattern |> String.concat ", "
        sprintf "TuplePat [%s]" formatted
    | Ast.ConstPat (c, _) ->
        match c with
        | Ast.IntConst n -> sprintf "ConstPat (IntConst %d)" n
        | Ast.BoolConst b -> sprintf "ConstPat (BoolConst %b)" b
    | Ast.EmptyListPat _ -> "EmptyListPat"
    | Ast.ConsPat (h, t, _) -> sprintf "ConsPat (%s, %s)" (formatPattern h) (formatPattern t)
