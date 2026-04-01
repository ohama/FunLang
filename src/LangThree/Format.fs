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
    | Parser.PERCENT -> "PERCENT"
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
    // Phase 29: Char token
    | Parser.CHAR c -> sprintf "CHAR('%c')" c
    | Parser.TYPE_CHAR -> "TYPE_CHAR"
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
    // Phase 18 (Ranges): DOTDOT token
    | Parser.DOTDOT -> "DOTDOT"
    // Phase 47 (Array/Hashtable Indexing): DOTLBRACKET token
    | Parser.DOTLBRACKET -> "DOTLBRACKET"
    // Phase 3 (Records-06): Mutable field tokens
    | Parser.MUTABLE -> "MUTABLE"
    | Parser.LARROW -> "LARROW"
    // Phase 6 (Exceptions): Exception tokens
    | Parser.EXCEPTION -> "EXCEPTION"
    | Parser.RAISE -> "RAISE"
    | Parser.TRY -> "TRY"
    | Parser.WHEN -> "WHEN"
    // Phase 46 (Loop Constructs): Loop keyword tokens
    | Parser.WHILE -> "WHILE"
    | Parser.FOR -> "FOR"
    | Parser.TO -> "TO"
    | Parser.DOWNTO -> "DOWNTO"
    | Parser.DO -> "DO"
    // Phase 71 (Type Classes): typeclass/instance tokens
    | Parser.TYPECLASS -> "TYPECLASS"
    | Parser.INSTANCE -> "INSTANCE"
    | Parser.FATARROW -> "FATARROW"
    // Phase 9 (Pipe & Composition): Pipe and composition tokens
    | Parser.PIPE_RIGHT -> "PIPE_RIGHT"
    | Parser.COMPOSE_RIGHT -> "COMPOSE_RIGHT"
    | Parser.COMPOSE_LEFT -> "COMPOSE_LEFT"
    // Phase 19 (User-Defined Operators): INFIXOP tokens
    | Parser.INFIXOP0 s -> sprintf "INFIXOP0(%s)" s
    | Parser.INFIXOP1 s -> sprintf "INFIXOP1(%s)" s
    | Parser.INFIXOP2 s -> sprintf "INFIXOP2(%s)" s
    | Parser.INFIXOP3 s -> sprintf "INFIXOP3(%s)" s
    | Parser.INFIXOP4 s -> sprintf "INFIXOP4(%s)" s
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
    | Ast.Char (c, _) -> sprintf "Char '%c'" c
    | Ast.Var (name, _) -> sprintf "Var \"%s\"" name
    | Ast.Add (l, r, _) -> sprintf "Add (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Subtract (l, r, _) -> sprintf "Subtract (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Multiply (l, r, _) -> sprintf "Multiply (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Divide (l, r, _) -> sprintf "Divide (%s, %s)" (formatAst l) (formatAst r)
    | Ast.Modulo (l, r, _) -> sprintf "Modulo (%s, %s)" (formatAst l) (formatAst r)
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
    | Ast.LetRec (bindings, inExpr, _) ->
        let bindingsStr =
            bindings
            |> List.map (fun (name, param, paramTyOpt, body, _) ->
                match paramTyOpt with
                | Some ty -> sprintf "%s (%s : %s) = %s" name param (formatTypeExpr ty) (formatAst body)
                | None -> sprintf "%s %s = %s" name param (formatAst body))
            |> String.concat " and "
        sprintf "LetRec (%s) in %s" bindingsStr (formatAst inExpr)
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
        let formatted = exprs |> List.map formatAst |> String.concat "; "
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
    // Phase 18 (Ranges)
    | Ast.Range (start, stop, stepOpt, _) ->
        match stepOpt with
        | None -> sprintf "Range (%s, %s)" (formatAst start) (formatAst stop)
        | Some step -> sprintf "Range (%s, %s, %s)" (formatAst start) (formatAst stop) (formatAst step)
    // Phase 42 (Mutable Variables)
    | Ast.LetMut (name, value, body, _) ->
        sprintf "LetMut (\"%s\", %s, %s)" name (formatAst value) (formatAst body)
    | Ast.Assign (name, value, _) ->
        sprintf "Assign (\"%s\", %s)" name (formatAst value)
    // Phase 46 (Loop Constructs)
    | Ast.WhileExpr (cond, body, _) ->
        sprintf "WhileExpr (%s, %s)" (formatAst cond) (formatAst body)
    | Ast.ForExpr (var, start, isTo, stop, body, _) ->
        let dir = if isTo then "to" else "downto"
        sprintf "ForExpr (\"%s\", %s, %s, %s, %s)" var (formatAst start) dir (formatAst stop) (formatAst body)
    | Ast.ForInExpr (pat, coll, body, _) ->
        sprintf "ForInExpr (%s, %s, %s)" (formatPattern pat) (formatAst coll) (formatAst body)
    | Ast.IndexGet (coll, idx, _) ->
        sprintf "IndexGet (%s, %s)" (formatAst coll) (formatAst idx)
    | Ast.IndexSet (coll, idx, v, _) ->
        sprintf "IndexSet (%s, %s, %s)" (formatAst coll) (formatAst idx) (formatAst v)
    // Phase 58
    | Ast.StringSliceExpr (str, start, stopOpt, _) ->
        let stopStr = stopOpt |> Option.map (fun e -> sprintf " .. %s" (formatAst e)) |> Option.defaultValue ""
        sprintf "StringSliceExpr (%s.[%s%s])" (formatAst str) (formatAst start) stopStr
    | Ast.ListCompExpr (var, coll, body, _) ->
        sprintf "ListCompExpr ([for %s in %s -> %s])" var (formatAst coll) (formatAst body)

/// Format TypeExpr as string
and formatTypeExpr (te: Ast.TypeExpr) : string =
    match te with
    | Ast.TEInt -> "TEInt"
    | Ast.TEBool -> "TEBool"
    | Ast.TEString -> "TEString"
    | Ast.TEChar -> "TEChar"
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
    // Phase 71 (Type Classes): constrained type rendering
    | Ast.TEConstrained(constraints, ty) ->
        let cs = constraints |> List.map (fun (cls, tv) -> sprintf "%s %s" cls (formatTypeExpr tv)) |> String.concat ", "
        sprintf "TEConstrained ([%s], %s)" cs (formatTypeExpr ty)

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
        | Ast.CharConst c -> sprintf "ConstPat (CharConst '%c')" c
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

    | Ast.TypeAliasDecl(name, typeParams, body, _) ->
        let paramsStr = if typeParams.IsEmpty then "" else sprintf " %s" (typeParams |> List.map (sprintf "'%s") |> String.concat " ")
        sprintf "TypeAliasDecl \"%s%s\" = %s" name paramsStr (formatTypeExpr body)

    | Ast.LetRecDecl(bindings, _) ->
        let bindingsStr =
            bindings
            |> List.map (fun (name, param, paramTyOpt, body, _) ->
                match paramTyOpt with
                | Some ty -> sprintf "%s (%s : %s) = %s" name param (formatTypeExpr ty) (formatAst body)
                | None -> sprintf "%s %s = %s" name param (formatAst body))
            |> String.concat " and "
        sprintf "LetRecDecl (%s)" bindingsStr

    | Ast.LetPatDecl(pat, body, _) ->
        sprintf "LetPatDecl (%s, %s)" (formatPattern pat) (formatAst body)

    | Ast.FileImportDecl(path, _) ->
        sprintf "FileImportDecl \"%s\"" path

    | Ast.LetMutDecl(name, body, _) ->
        sprintf "LetMutDecl (\"%s\", %s)" name (formatAst body)

    // Phase 71 (Type Classes): render new decl nodes
    | Ast.TypeClassDecl(className, typeVar, methods, _) ->
        let methodsStr = methods |> List.map (fun (name, ty) -> sprintf "%s : %s" name (formatTypeExpr ty)) |> String.concat ", "
        sprintf "TypeClassDecl \"%s\" %s [%s]" className typeVar methodsStr
    | Ast.InstanceDecl(className, instType, methods, _, _) ->
        let methodsStr = methods |> List.map (fun (name, body) -> sprintf "%s = %s" name (formatAst body)) |> String.concat ", "
        sprintf "InstanceDecl \"%s\" %s [%s]" className (formatTypeExpr instType) methodsStr

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
