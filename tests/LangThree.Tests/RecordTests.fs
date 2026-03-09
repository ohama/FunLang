module LangThree.Tests.RecordTests

open Expecto
open LangThree.IndentFilter

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

// Helper to parse and type check a module
let parseAndTypeCheck (input: string) =
    let m = parseModule input
    TypeCheck.typeCheckModule m

// Helper to parse, type check, and evaluate a module -- returns final env
let parseTypeCheckAndEval (input: string) : Ast.Env =
    let m = parseModule input
    match TypeCheck.typeCheckModule m with
    | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
    | Ok (_warnings, recEnv, _modules) ->
        match m with
        | Ast.Module(decls, _) ->
            decls |> List.fold (fun env decl ->
                match decl with
                | Ast.LetDecl(name, body, _) ->
                    let value = Eval.eval recEnv Map.empty env body
                    Map.add name value env
                | _ -> env) Eval.emptyEnv
        | Ast.EmptyModule _ -> Eval.emptyEnv

[<Tests>]
let recordTests = testList "Records" [

    // =====================================================
    // REC-01: Record type declarations
    // =====================================================

    testList "REC-01: Record type declarations" [
        test "simple record type parses and type checks" {
            let input = "type Point = { x: int; y: int }\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
        }

        test "record with type parameters" {
            let input = "type Pair 'a = { first: 'a; second: 'a }\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
        }

        test "record with trailing semicolon" {
            let input = "type R = { x: int; }\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diag -> failtest (sprintf "Type checking failed: %s" diag.Message)
        }
    ]

    // =====================================================
    // REC-02: Record expressions (creation)
    // =====================================================

    testList "REC-02: Record expressions" [
        test "create record and access field" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 1; y = 2 }\n\nlet result = p.x\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 1) -> ()
            | other -> failtest (sprintf "Expected IntValue 1, got: %A" other)
        }

        test "create record with all fields" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 10; y = 20 }\n\nlet rx = p.x\n\nlet ry = p.y\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "rx" env, Map.tryFind "ry" env with
            | Some (Ast.IntValue 10), Some (Ast.IntValue 20) -> ()
            | other -> failtest (sprintf "Expected (10, 20), got: %A" other)
        }
    ]

    // =====================================================
    // REC-03: Field access
    // =====================================================

    testList "REC-03: Field access" [
        test "simple field access" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 42; y = 99 }\n\nlet result = p.y\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 99) -> ()
            | other -> failtest (sprintf "Expected IntValue 99, got: %A" other)
        }

        test "chained field access on nested records" {
            let input = "type Inner = { v: int }\n\ntype Outer = { inner: Inner }\n\nlet o = { inner = { v = 42 } }\n\nlet result = o.inner.v\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 42) -> ()
            | other -> failtest (sprintf "Expected IntValue 42, got: %A" other)
        }

        test "field access on non-existent field produces type error" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 1; y = 2 }\n\nlet result = p.z\n"
            let result = parseAndTypeCheck input
            match result with
            | Error _ -> ()
            | Ok _ -> failtest "Expected type error for non-existent field"
        }
    ]

    // =====================================================
    // REC-04: Copy-and-update
    // =====================================================

    testList "REC-04: Copy-and-update" [
        test "update single field" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 1; y = 2 }\n\nlet q = { p with y = 3 }\n\nlet result = q.y\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 3) -> ()
            | other -> failtest (sprintf "Expected IntValue 3, got: %A" other)
        }

        test "update preserves unchanged fields" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 1; y = 2 }\n\nlet q = { p with y = 3 }\n\nlet result = q.x\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 1) -> ()
            | other -> failtest (sprintf "Expected IntValue 1 (unchanged), got: %A" other)
        }

        test "update multiple fields" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 1; y = 2 }\n\nlet q = { p with x = 10; y = 20 }\n\nlet rx = q.x\n\nlet ry = q.y\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "rx" env, Map.tryFind "ry" env with
            | Some (Ast.IntValue 10), Some (Ast.IntValue 20) -> ()
            | other -> failtest (sprintf "Expected (10, 20), got: %A" other)
        }

        test "original record unchanged after update" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 1; y = 2 }\n\nlet q = { p with y = 99 }\n\nlet result = p.y\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 2) -> ()
            | other -> failtest (sprintf "Expected IntValue 2 (original unchanged), got: %A" other)
        }
    ]

    // =====================================================
    // REC-05: Record patterns
    // =====================================================

    testList "REC-05: Record patterns" [
        test "match on record destructures fields" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 3; y = 4 }\n\nlet result =\n    match p with\n    | { x = px; y = py } -> px + py\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 7) -> ()
            | other -> failtest (sprintf "Expected IntValue 7, got: %A" other)
        }

        test "partial record pattern binds only named fields" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 42; y = 99 }\n\nlet result =\n    match p with\n    | { x = px } -> px\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 42) -> ()
            | other -> failtest (sprintf "Expected IntValue 42, got: %A" other)
        }
    ]

    // =====================================================
    // REC-06: Structural equality
    // =====================================================

    testList "REC-06: Structural equality" [
        test "equal records are equal" {
            let input = "type Point = { x: int; y: int }\n\nlet a = { x = 1; y = 2 }\n\nlet b = { x = 1; y = 2 }\n\nlet result =\n    if a = b then 1 else 0\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 1) -> ()
            | other -> failtest (sprintf "Expected IntValue 1 (equal), got: %A" other)
        }

        test "different records are not equal" {
            let input = "type Point = { x: int; y: int }\n\nlet a = { x = 1; y = 2 }\n\nlet b = { x = 1; y = 3 }\n\nlet result =\n    if a = b then 1 else 0\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 0) -> ()
            | other -> failtest (sprintf "Expected IntValue 0 (not equal), got: %A" other)
        }

        test "not-equal operator on records" {
            let input = "type Point = { x: int; y: int }\n\nlet a = { x = 1; y = 2 }\n\nlet b = { x = 1; y = 3 }\n\nlet result =\n    if a <> b then 1 else 0\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 1) -> ()
            | other -> failtest (sprintf "Expected IntValue 1 (not equal), got: %A" other)
        }
    ]

    // =====================================================
    // REC-07: Mutable fields
    // =====================================================

    testList "REC-07: Mutable fields" [
        test "mutable field assignment and read" {
            let input = "type Counter = { mutable count: int; name: string }\n\nlet c = { count = 0; name = \"test\" }\n\nlet unused = c.count <- 5\n\nlet result = c.count\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 5) -> ()
            | other -> failtest (sprintf "Expected IntValue 5, got: %A" other)
        }

        test "immutable field assignment produces type error" {
            let input = "type Counter = { mutable count: int; name: string }\n\nlet c = { count = 0; name = \"test\" }\n\nlet result = c.name <- \"new\"\n"
            let result = parseAndTypeCheck input
            match result with
            | Error diag ->
                if diag.Message.Contains("immutable") || diag.Message.Contains("Immutable") then ()
                else failtest (sprintf "Expected ImmutableFieldAssignment error, got: %s" diag.Message)
            | Ok _ -> failtest "Expected type error for immutable field assignment"
        }

        test "mutation visible through aliases (shared ref)" {
            let input = "type Counter = { mutable count: int; name: string }\n\nlet c = { count = 0; name = \"test\" }\n\nlet c2 = c\n\nlet unused = c.count <- 10\n\nlet result = c2.count\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 10) -> ()
            | other -> failtest (sprintf "Expected IntValue 10 (shared mutation), got: %A" other)
        }

        test "read mutable field normally" {
            let input = "type Counter = { mutable count: int; name: string }\n\nlet c = { count = 42; name = \"test\" }\n\nlet result = c.count\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 42) -> ()
            | other -> failtest (sprintf "Expected IntValue 42, got: %A" other)
        }

        test "copy-and-update does not share refs with original" {
            let input = "type Counter = { mutable count: int; name: string }\n\nlet c = { count = 0; name = \"test\" }\n\nlet c2 = { c with count = 5 }\n\nlet unused = c2.count <- 99\n\nlet result = c.count\n"
            let env = parseTypeCheckAndEval input
            match Map.tryFind "result" env with
            | Some (Ast.IntValue 0) -> ()
            | other -> failtest (sprintf "Expected IntValue 0 (original unchanged after copy mutation), got: %A" other)
        }
    ]

    // =====================================================
    // Error cases
    // =====================================================

    testList "Error cases" [
        test "duplicate field names across types produces error" {
            let input = "type A = { x: int }\n\ntype B = { x: int; y: int }\n"
            let result = parseAndTypeCheck input
            match result with
            | Error _ -> ()
            | Ok _ -> failtest "Expected error for duplicate field names across types"
        }

        test "wrong field name in record expression produces error" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 1; z = 2 }\n"
            let result = parseAndTypeCheck input
            match result with
            | Error _ -> ()
            | Ok _ -> failtest "Expected error for wrong field name"
        }

        test "missing field in record expression produces error" {
            let input = "type Point = { x: int; y: int }\n\nlet p = { x = 1 }\n"
            let result = parseAndTypeCheck input
            match result with
            | Error _ -> ()
            | Ok _ -> failtest "Expected error for missing field"
        }

        test "field access on non-record produces error" {
            let input = "let x = 42\n\nlet result = x.foo\n"
            let result = parseAndTypeCheck input
            match result with
            | Error _ -> ()
            | Ok _ -> failtest "Expected error for field access on non-record"
        }
    ]
]
