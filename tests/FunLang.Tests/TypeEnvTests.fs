module FunLang.Tests.TypeEnvTests

open Expecto
open FunLang.IndentFilter

// Helper to lex and filter through IndentFilter
let lexAndFilter (input: string) =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let rec collect () =
        let tok = Lexer.tokenize lexbuf
        if tok = Parser.EOF then [Parser.EOF]
        else tok :: collect ()
    let rawTokens = collect ()
    filter defaultConfig rawTokens |> Seq.toList

// Helper to parse a module
let parseModule (input: string) : Ast.Module =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let filteredTokens = lexAndFilter input
    let mutable index = 0
    let tokenizer (lexbuf: FSharp.Text.Lexing.LexBuffer<_>) =
        if index < filteredTokens.Length then
            let tok = filteredTokens.[index]
            index <- index + 1
            tok
        else
            Parser.EOF
    Parser.parseModule tokenizer lexbuf

/// Type-check a program and return the resulting TypeEnv.
let typeCheckAndGetEnv (input: string) : TypeCheck.BindingEnv =
    let m = parseModule input
    match TypeCheck.typeCheckModule m with
    | Error errs -> failwith (sprintf "Type checking failed: %A" errs)
    | Ok (_, _recEnv, _modules, typeEnv) -> typeEnv

[<Tests>]
let typeEnvTests = testSequenced <| testList "TypeEnv" [

    // TE-01: let x = 42 → x : int in env
    test "TE-01: let x = 42 produces x with int scheme" {
        let env = typeCheckAndGetEnv "let x = 42"
        match Map.tryFind "x" env with
        | None -> failtest "Expected 'x' to be in TypeEnv"
        | Some (Type.Scheme(_, _, ty)) ->
            Expect.equal ty Type.TInt "x should have type int"
    }

    // TE-01: let f x = x + 1 → f : int -> int in env
    test "TE-01: let f x = x + 1 produces f with int->int scheme" {
        let env = typeCheckAndGetEnv "let f x = x + 1"
        match Map.tryFind "f" env with
        | None -> failtest "Expected 'f' to be in TypeEnv"
        | Some (Type.Scheme(_, _, ty)) ->
            match ty with
            | Type.TArrow(Type.TInt, Type.TInt) ->
                () // correct
            | other ->
                failtest (sprintf "f should have type int->int but got %A" other)
    }

    // TE-01: let id x = x → polymorphic id in env (forall a. a -> a)
    test "TE-01: let id x = x produces polymorphic scheme" {
        let env = typeCheckAndGetEnv "let id x = x"
        match Map.tryFind "id" env with
        | None -> failtest "Expected 'id' to be in TypeEnv"
        | Some (Type.Scheme(vars, _, ty)) ->
            // Polymorphic: should have type vars and an arrow
            Expect.isTrue (vars.Length >= 1) "id should be polymorphic (has type variables)"
            match ty with
            | Type.TArrow _ -> () // correct shape
            | other -> failtest (sprintf "id should have arrow type but got %A" other)
    }

    // TE-02: builtin print is in env
    test "TE-02: builtin print is in TypeEnv" {
        let env = typeCheckAndGetEnv "let dummy = 1"
        Expect.isSome (Map.tryFind "print" env) "print should be in initial TypeEnv"
    }

    // TE-02: builtin string_length is in env
    test "TE-02: builtin string_length is in TypeEnv" {
        let env = typeCheckAndGetEnv "let dummy = 1"
        Expect.isSome (Map.tryFind "string_length" env) "string_length should be in initial TypeEnv"
    }

    // TE-02: all initialTypeEnv builtins present in returned env
    test "TE-02: all initialTypeEnv builtins present after type-checking" {
        let env = typeCheckAndGetEnv "let dummy = 1"
        let builtins = TypeCheck.initialTypeEnv |> Map.toSeq |> Seq.map fst |> Seq.toList
        for name in builtins do
            Expect.isSome (Map.tryFind name env) (sprintf "Builtin '%s' should be in TypeEnv" name)
    }

    // TE-01+02: user bindings and builtins coexist in the same env
    test "TE-01+02: user binding and builtins coexist in env" {
        let env = typeCheckAndGetEnv "let myVal = 99"
        Expect.isSome (Map.tryFind "myVal" env) "User binding 'myVal' should be in TypeEnv"
        Expect.isSome (Map.tryFind "string_length" env) "Builtin 'string_length' should still be in TypeEnv"
    }

    // exportBindingEnv returns the same map (identity)
    test "exportBindingEnv returns the same map as typeCheckModule" {
        let m = parseModule "let x = 10"
        match TypeCheck.typeCheckModule m with
        | Error errs -> failtest (sprintf "Type checking failed: %A" errs)
        | Ok (_, _recEnv, _modules, typeEnv) ->
            let exported = TypeCheck.exportBindingEnv typeEnv
            Expect.equal exported typeEnv "exportBindingEnv should return the same map"
    }

    // env is queryable by name: let foo = "hello" → string
    test "TE-01: let foo = \"hello\" produces foo with string scheme" {
        let env = typeCheckAndGetEnv "let foo = \"hello\""
        match Map.tryFind "foo" env with
        | None -> failtest "Expected 'foo' to be in TypeEnv"
        | Some (Type.Scheme(_, _, ty)) ->
            Expect.equal ty Type.TString "foo should have type string"
    }

]
