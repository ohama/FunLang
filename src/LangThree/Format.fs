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
    // Phase 10 (Unit): Unit type token
    | Parser.TYPE_UNIT -> "TYPE_UNIT"
    | Parser.TYPE_VAR s -> sprintf "TYPE_VAR(%s)" s
    // Phase 2 (ADT-01): Type declaration tokens
    | Parser.TYPE -> "TYPE"
    | Parser.OF -> "OF"
    | Parser.AND_KW -> "AND_KW"
    // Phase 3 (Records): Record tokens
    | Parser.LBRACE -> "LBRACE"
    | Parser.RBRACE -> "RBRACE"
    | Parser.SEMICOLON -> "SEMICOLON"
    | Parser.DOT -> "DOT"
    // Phase 3 (Records-06): Mutable field tokens
    | Parser.MUTABLE -> "MUTABLE"
    | Parser.LARROW -> "LARROW"
    // Phase 6 (Exceptions): Exception tokens
    | Parser.EXCEPTION -> "EXCEPTION"
    | Parser.RAISE -> "RAISE"
    | Parser.TRY -> "TRY"
    | Parser.WHEN -> "WHEN"
    // Phase 9 (Pipe & Composition): Pipe and composition tokens
    | Parser.PIPE_RIGHT -> "PIPE_RIGHT"
    | Parser.COMPOSE_RIGHT -> "COMPOSE_RIGHT"
    | Parser.COMPOSE_LEFT -> "COMPOSE_LEFT"
    // Phase 5 (Modules): Module system tokens
    | Parser.MODULE -> "MODULE"
    | Parser.NAMESPACE -> "NAMESPACE"
    | Parser.OPEN -> "OPEN"
    | Parser.INDENT -> "INDENT"
    | Parser.DEDENT -> "DEDENT"
    | Parser.NEWLINE n -> sprintf "NEWLINE(%d)" n
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
            |> List.map (fun (pat, _guard, expr) -> sprintf "(%s, %s)" (formatPattern pat) (formatAst expr))
            |> String.concat "; "
        sprintf "Match (%s, [%s])" (formatAst scrut) formattedClauses
    | Ast.Constructor (name, None, _) -> sprintf "Constructor \"%s\"" name
    | Ast.Constructor (name, Some arg, _) -> sprintf "Constructor (\"%s\", %s)" name (formatAst arg)
    // Phase 3 (Records): Record expressions
    | Ast.RecordExpr (tyOpt, fields, _) ->
        let fieldsStr = fields |> List.map (fun (n, e) -> sprintf "%s = %s" n (formatAst e)) |> String.concat "; "
        match tyOpt with
        | Some ty -> sprintf "RecordExpr (%s, {%s})" ty fieldsStr
        | None -> sprintf "RecordExpr ({%s})" fieldsStr
    | Ast.FieldAccess (e, field, _) ->
        sprintf "FieldAccess (%s, %s)" (formatAst e) field
    | Ast.RecordUpdate (src, fields, _) ->
        let fieldsStr = fields |> List.map (fun (n, e) -> sprintf "%s = %s" n (formatAst e)) |> String.concat "; "
        sprintf "RecordUpdate (%s, {%s})" (formatAst src) fieldsStr
    // Phase 3 (Records-06): Mutable field assignment
    | Ast.SetField (e, field, value, _) ->
        sprintf "SetField (%s, %s, %s)" (formatAst e) field (formatAst value)
    // Phase 6 (Exceptions)
    | Ast.Raise (e, _) -> sprintf "Raise (%s)" (formatAst e)
    | Ast.TryWith (body, clauses, _) ->
        let formattedClauses =
            clauses
            |> List.map (fun (pat, _guard, expr) -> sprintf "(%s, %s)" (formatPattern pat) (formatAst expr))
            |> String.concat "; "
        sprintf "TryWith (%s, [%s])" (formatAst body) formattedClauses
    // Phase 9 (Pipe & Composition)
    | Ast.PipeRight (l, r, _) -> sprintf "PipeRight (%s, %s)" (formatAst l) (formatAst r)
    | Ast.ComposeRight (l, r, _) -> sprintf "ComposeRight (%s, %s)" (formatAst l) (formatAst r)
    | Ast.ComposeLeft (l, r, _) -> sprintf "ComposeLeft (%s, %s)" (formatAst l) (formatAst r)

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
    | Ast.TEName name -> sprintf "TEName \"%s\"" name
    | Ast.TEData (name, args) ->
        let formatted = args |> List.map formatTypeExpr |> String.concat ", "
        sprintf "TEData (\"%s\", [%s])" name formatted

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
        | Ast.StringConst s -> sprintf "ConstPat (StringConst \"%s\")" s
    | Ast.EmptyListPat _ -> "EmptyListPat"
    | Ast.ConsPat (h, t, _) -> sprintf "ConsPat (%s, %s)" (formatPattern h) (formatPattern t)
    | Ast.ConstructorPat (name, argOpt, _) ->
        match argOpt with
        | None -> sprintf "ConstructorPat \"%s\"" name
        | Some arg -> sprintf "ConstructorPat (\"%s\", %s)" name (formatPattern arg)
    // Phase 3 (Records): Record pattern
    | Ast.RecordPat (fields, _) ->
        let fieldsStr = fields |> List.map (fun (n, p) -> sprintf "%s = %s" n (formatPattern p)) |> String.concat "; "
        sprintf "RecordPat {%s}" fieldsStr
    // Phase 16: Or-pattern
    | Ast.OrPat (pats, _) ->
        let formatted = pats |> List.map formatPattern |> String.concat " | "
        sprintf "OrPat [%s]" formatted

/// Format a constructor declaration as string
let formatConstructorDecl (cd: Ast.ConstructorDecl) : string =
    match cd with
    | Ast.ConstructorDecl(name, None, _) -> sprintf "| %s" name
    | Ast.ConstructorDecl(name, Some ty, _) -> sprintf "| %s of %s" name (formatTypeExpr ty)
    | Ast.GadtConstructorDecl(name, argTypes, retType, _) ->
        let args = argTypes |> List.map formatTypeExpr |> String.concat " * "
        sprintf "| %s : %s -> %s" name args (formatTypeExpr retType)

/// Format a record field declaration as string
let formatRecordFieldDecl (fd: Ast.RecordFieldDecl) : string =
    match fd with
    | Ast.RecordFieldDecl(name, ty, isMut, _) ->
        if isMut then sprintf "mutable %s: %s" name (formatTypeExpr ty)
        else sprintf "%s: %s" name (formatTypeExpr ty)

/// Format a declaration as string
let rec formatDecl (decl: Ast.Decl) : string =
    match decl with
    | Ast.LetDecl(name, body, _) ->
        sprintf "LetDecl (\"%s\", %s)" name (formatAst body)
    | Ast.Decl.TypeDecl(Ast.TypeDecl(name, typeParams, ctors, _)) ->
        let paramsStr = if typeParams.IsEmpty then "" else sprintf " %s" (typeParams |> List.map (sprintf "'%s") |> String.concat " ")
        let ctorsStr = ctors |> List.map formatConstructorDecl |> String.concat " "
        sprintf "TypeDecl \"%s%s\" [%s]" name paramsStr ctorsStr
    | Ast.Decl.RecordTypeDecl(Ast.RecordDecl(name, typeParams, fields, _)) ->
        let paramsStr = if typeParams.IsEmpty then "" else sprintf " %s" (typeParams |> List.map (sprintf "'%s") |> String.concat " ")
        let fieldsStr = fields |> List.map formatRecordFieldDecl |> String.concat "; "
        sprintf "RecordDecl \"%s%s\" {%s}" name paramsStr fieldsStr
    | Ast.ModuleDecl(name, decls, _) ->
        let declsStr = decls |> List.map formatDecl |> String.concat "\n  "
        sprintf "ModuleDecl \"%s\" [\n  %s\n]" name declsStr
    | Ast.OpenDecl(path, _) ->
        sprintf "OpenDecl [%s]" (path |> String.concat ".")
    | Ast.NamespaceDecl(path, decls, _) ->
        let declsStr = decls |> List.map formatDecl |> String.concat "\n  "
        sprintf "NamespaceDecl [%s] [\n  %s\n]" (path |> String.concat ".") declsStr
    | Ast.ExceptionDecl(name, None, _) ->
        sprintf "ExceptionDecl \"%s\"" name
    | Ast.ExceptionDecl(name, Some ty, _) ->
        sprintf "ExceptionDecl \"%s\" of %s" name (formatTypeExpr ty)

/// Format a module as string
let formatModule (m: Ast.Module) : string =
    let formatDecls decls =
        decls |> List.map formatDecl |> String.concat "\n"
    match m with
    | Ast.Module(decls, _) -> formatDecls decls
    | Ast.NamedModule(name, decls, _) ->
        sprintf "module %s\n%s" (name |> String.concat ".") (formatDecls decls)
    | Ast.NamespacedModule(name, decls, _) ->
        sprintf "namespace %s\n%s" (name |> String.concat ".") (formatDecls decls)
    | Ast.EmptyModule _ -> "(empty module)"
