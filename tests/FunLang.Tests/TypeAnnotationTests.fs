module FunLang.Tests.TypeAnnotationTests

open Expecto
open FunLang.IndentFilter

// Helper to lex and filter through IndentFilter (without position info)
let lexAndFilter (input: string) =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let rec collect () =
        let tok = Lexer.tokenize lexbuf
        if tok = Parser.EOF then [Parser.EOF]
        else tok :: collect ()
    let rawTokens = collect ()
    filter defaultConfig rawTokens |> Seq.toList

// Helper to lex and filter through IndentFilter, preserving position info
let lexAndFilterPositioned (input: string) : PositionedToken list =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let rec collect () =
        let startPos = lexbuf.StartPos
        let tok = Lexer.tokenize lexbuf
        let endPos = lexbuf.EndPos
        if tok = Parser.EOF then [{ Token = Parser.EOF; StartPos = startPos; EndPos = endPos }]
        else { Token = tok; StartPos = startPos; EndPos = endPos } :: collect ()
    let rawTokens = collect ()
    filterPositioned defaultConfig rawTokens

// Helper to parse a module (without position info — used by TA-01..TA-07)
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

// Helper to parse a module with full position info (preserves spans for annotationMap testing)
let parseModuleWithPositions (input: string) : Ast.Module =
    let filteredTokens = lexAndFilterPositioned input
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let mutable index = 0
    let tokenizer (lb: FSharp.Text.Lexing.LexBuffer<_>) =
        if index < filteredTokens.Length then
            let pt = filteredTokens.[index]
            index <- index + 1
            lb.StartPos <- pt.StartPos
            lb.EndPos <- pt.EndPos
            pt.Token
        else
            Parser.EOF
    Parser.parseModule tokenizer lexbuf

/// Type-check a program and return a snapshot of the annotation map.
/// Snapshotting immediately after type-checking avoids races with parallel tests
/// that also reset Bidir.annotationMap on entry.
let typeCheckAndSnapshot (input: string) : Map<Ast.Span, Type.Type> =
    let m = parseModule input
    let result = TypeCheck.typeCheckModule m
    // Snapshot the map immediately after type-checking (before another test can reset it)
    let snapshot =
        Bidir.annotationMap
        |> Seq.map (fun kv -> (kv.Key, kv.Value))
        |> Map.ofSeq
    match result with
    | Error errs -> failwith (sprintf "Type checking failed: %A" errs)
    | Ok _ -> snapshot

/// Collect all Expr spans in an AST (for counting distinct real spans)
let rec collectSpans (expr: Ast.Expr) : Ast.Span list =
    let span = Ast.spanOf expr
    let childSpans =
        match expr with
        | Ast.Number _ | Ast.Bool _ | Ast.String _ | Ast.Char _ | Ast.Var _ | Ast.EmptyList _ -> []
        | Ast.Add(e1, e2, _) | Ast.Subtract(e1, e2, _) | Ast.Multiply(e1, e2, _)
        | Ast.Divide(e1, e2, _) | Ast.Modulo(e1, e2, _)
        | Ast.Equal(e1, e2, _) | Ast.NotEqual(e1, e2, _)
        | Ast.LessThan(e1, e2, _) | Ast.GreaterThan(e1, e2, _)
        | Ast.LessEqual(e1, e2, _) | Ast.GreaterEqual(e1, e2, _)
        | Ast.And(e1, e2, _) | Ast.Or(e1, e2, _)
        | Ast.App(e1, e2, _) | Ast.Cons(e1, e2, _) ->
            collectSpans e1 @ collectSpans e2
        | Ast.Negate(e, _) | Ast.Raise(e, _)
        | Ast.Lambda(_, e, _) | Ast.Annot(e, _, _) -> collectSpans e
        | Ast.LambdaAnnot(_, _, e, _) -> collectSpans e
        | Ast.Let(_, v, b, _) | Ast.LetMut(_, v, b, _)
        | Ast.LetPat(_, v, b, _) -> collectSpans v @ collectSpans b
        | Ast.If(c, t, e, _) -> collectSpans c @ collectSpans t @ collectSpans e
        | Ast.List(es, _) | Ast.Tuple(es, _) -> List.collect collectSpans es
        | Ast.Match(s, cls, _) ->
            collectSpans s @ List.collect (fun (_, _, e) -> collectSpans e) cls
        | Ast.TryWith(b, hs, _) ->
            collectSpans b @ List.collect (fun (_, _, e) -> collectSpans e) hs
        | Ast.WhileExpr(c, b, _) | Ast.ForInExpr(_, c, b, _) -> collectSpans c @ collectSpans b
        | Ast.ForExpr(_, s, _, e, b, _) -> collectSpans s @ collectSpans e @ collectSpans b
        | Ast.Assign(_, v, _) -> collectSpans v
        | Ast.LetRec(bs, body, _) ->
            List.collect (fun (_, _, _, _, e, _) -> collectSpans e) bs @ collectSpans body
        | Ast.RecordExpr(_, fs, _) -> List.collect (snd >> collectSpans) fs
        | Ast.FieldAccess(e, _, _) | Ast.RecordUpdate(e, _, _) -> collectSpans e
        | Ast.SetField(e, _, v, _) | Ast.IndexGet(e, v, _) -> collectSpans e @ collectSpans v
        | Ast.IndexSet(e, i, v, _) -> collectSpans e @ collectSpans i @ collectSpans v
        | Ast.StringSliceExpr(e, s, opt, _) ->
            collectSpans e @ collectSpans s @ (Option.toList opt |> List.collect collectSpans)
        | Ast.ListCompExpr(_, c, b, _) -> collectSpans c @ collectSpans b
        | Ast.Range(s, e, opt, _) ->
            collectSpans s @ collectSpans e @ (Option.toList opt |> List.collect collectSpans)
        | Ast.Constructor(_, argOpt, _) -> Option.toList argOpt |> List.collect collectSpans
    span :: childSpans

[<Tests>]
let typeAnnotationTests = testSequenced <| testList "TypeAnnotation" [

    // TA-01: Simple integer addition is annotated TInt
    test "TA-01: 1 + 2 annotates as TInt" {
        let input = "let result = 1 + 2"
        let annots = typeCheckAndSnapshot input
        // Should have entries in the map
        Expect.isTrue (annots.Count > 0) "annotationMap should be non-empty after type-checking"
        // At least one TInt entry (the Add expression records TInt)
        let allInts =
            annots
            |> Map.toSeq
            |> Seq.filter (fun (_, ty) -> ty = Type.TInt)
            |> Seq.length
        Expect.isTrue (allInts >= 1) "Should have at least 1 TInt entry for 1 + 2"
    }

    // TA-02: Lambda annotation produces TArrow
    test "TA-02: fun x -> x + 1 annotates with TArrow" {
        let input = "let f = fun x -> x + 1"
        let annots = typeCheckAndSnapshot input
        let hasArrow =
            annots
            |> Map.toSeq
            |> Seq.exists (fun (_, ty) ->
                match ty with
                | Type.TArrow _ -> true
                | _ -> false)
        Expect.isTrue hasArrow "Should have at least one TArrow annotation for the lambda"
    }

    // TA-03: Let binding - integer literal annotated TInt
    test "TA-03: let x = 42 in x annotates as TInt" {
        let input = "let result = let x = 42 in x"
        let annots = typeCheckAndSnapshot input
        // The whole let expression should resolve to TInt
        let hasInt =
            annots
            |> Map.toSeq
            |> Seq.exists (fun (_, ty) -> ty = Type.TInt)
        Expect.isTrue hasInt "Should have TInt annotation (the let expression body is x : TInt)"
        // The annotation map should be non-empty
        Expect.isTrue (annots.Count >= 1) "Should have at least 1 annotation entry"
    }

    // TA-04: Complex expression - annotation map is populated for complex programs
    test "TA-04: complex program produces annotation entries" {
        let input = "let f x = if x > 0 then x + 1 else x - 1"
        let annots = typeCheckAndSnapshot input
        // The overall function should annotate as TArrow(TInt, TInt)
        Expect.isTrue (annots.Count >= 1) (sprintf "Should have at least 1 annotation but got %d" annots.Count)
        // The outermost result (if expression) should be TInt
        let hasIntOrArrow =
            annots
            |> Map.toSeq
            |> Seq.exists (fun (_, ty) ->
                match ty with
                | Type.TInt | Type.TArrow _ -> true
                | _ -> false)
        Expect.isTrue hasIntOrArrow "Should have TInt or TArrow annotation in map"
    }

    // TA-05: Map reflects only the most recent type-check (not previous ones)
    test "TA-05: annotationMap is reset between type-checks" {
        // Type-check a simple boolean expression
        let input1 = "let a = true"
        let annots1 = typeCheckAndSnapshot input1
        let boolCount1 =
            annots1 |> Map.toSeq |> Seq.filter (fun (_, ty) -> ty = Type.TBool) |> Seq.length
        Expect.isTrue (boolCount1 >= 1) "First check should have TBool for 'true'"

        // Type-check a completely different integer expression
        let input2 = "let b = 99"
        let annots2 = typeCheckAndSnapshot input2
        let intCount2 =
            annots2 |> Map.toSeq |> Seq.filter (fun (_, ty) -> ty = Type.TInt) |> Seq.length
        Expect.isTrue (intCount2 >= 1) "Second check should have TInt for 99"

        // After second check, the snapshot should contain integer annotations
        // (annotationMap was reset on entry to typeCheckModule)
        Expect.isTrue (annots2.Count >= 1) "Second snapshot should be non-empty"
    }

    // TA-06: Boolean literal annotated TBool
    test "TA-06: boolean literal annotates as TBool" {
        let input = "let flag = true"
        let annots = typeCheckAndSnapshot input
        let hasBool =
            annots |> Map.toSeq |> Seq.exists (fun (_, ty) -> ty = Type.TBool)
        Expect.isTrue hasBool "true should be annotated as TBool"
    }

    // TA-07: String literal annotated TString
    test "TA-07: string literal annotates as TString" {
        let input = "let s = \"hello\""
        let annots = typeCheckAndSnapshot input
        let hasStr =
            annots |> Map.toSeq |> Seq.exists (fun (_, ty) -> ty = Type.TString)
        Expect.isTrue hasStr "\"hello\" should be annotated as TString"
    }

    // TA-08: Multi-param annotated let has distinct spans per LambdaAnnot
    // Uses parseModuleWithPositions to preserve token position info so that
    // ruleSpan/symSpan in the parser produce distinct spans for each parameter.
    test "TA-08: multi-param annotated let has distinct spans per LambdaAnnot" {
        let input = "let f (x : int) (y : string) (z : bool) : int = 42"
        // Parse with full position info so each LambdaAnnot node gets a distinct span
        let m = parseModuleWithPositions input
        let result = TypeCheck.typeCheckModule m
        let annots =
            Bidir.annotationMap
            |> Seq.map (fun kv -> (kv.Key, kv.Value))
            |> Map.ofSeq
        match result with
        | Error errs -> failwith (sprintf "Type checking failed: %A" errs)
        | Ok _ -> ()
        // Each LambdaAnnot should produce a distinct annotationMap entry with TArrow type
        // Outermost: int -> string -> bool -> int
        // Middle: string -> bool -> int
        // Innermost: bool -> int
        let arrowEntries =
            annots
            |> Map.toSeq
            |> Seq.filter (fun (_, ty) ->
                match ty with
                | Type.TArrow _ -> true
                | _ -> false)
            |> Seq.toList
        // There should be at least 3 distinct TArrow entries (one per annotated param)
        Expect.isTrue (arrowEntries.Length >= 3)
            (sprintf "Should have at least 3 TArrow entries for 3 annotated params, got %d" arrowEntries.Length)
        // All spans should be distinct (no collisions)
        let spans = arrowEntries |> List.map fst
        let distinctSpans = spans |> List.distinct
        Expect.equal distinctSpans.Length spans.Length
            "All TArrow spans should be distinct (no span collisions)"
    }

    // TA-09: let rec with multiple annotated params records ALL LambdaAnnot spans in annotationMap
    // Regression test for Issue #19: Bidir.fs must record TArrow at each param's own span,
    // not just the body span. Uses parseModuleWithPositions for position-aware parsing.
    test "TA-09: let rec multi-param annotated records all LambdaAnnot spans" {
        let input = "let rec f (x : int) (y : string) (z : bool) = 42"
        let m = parseModuleWithPositions input
        Bidir.annotationMap.Clear()
        let result = TypeCheck.typeCheckModule m
        match result with
        | Error errs -> failwith (sprintf "Type check error: %A" errs)
        | Ok _ -> ()
        let arrowEntries =
            Bidir.annotationMap
            |> Seq.map (fun kv -> (kv.Key, kv.Value))
            |> Seq.filter (fun (_, ty) -> match ty with | Type.TArrow _ -> true | _ -> false)
            |> Seq.toList
        Expect.isTrue (arrowEntries.Length >= 3)
            (sprintf "Expected >= 3 TArrow for let rec with 3 annotated params, got %d" arrowEntries.Length)
        let spans = arrowEntries |> List.map fst
        let distinctSpans = spans |> List.distinct
        Expect.equal distinctSpans.Length spans.Length
            "All TArrow spans should be distinct"
    }

    // TA-09b: let rec mutual recursion with annotated params records first-param spans
    // Regression test for Issue #19: both `f` and `g` in a mutual rec block must have
    // their first annotated param recorded in annotationMap.
    test "TA-09b: let rec mutual with annotated params records first-param spans" {
        let input = "let rec f (x : int) = g (x > 0)\nand g (y : bool) = 0"
        let m = parseModuleWithPositions input
        Bidir.annotationMap.Clear()
        let result = TypeCheck.typeCheckModule m
        match result with
        | Error errs -> failwith (sprintf "Type check error: %A" errs)
        | Ok _ -> ()
        let arrowEntries =
            Bidir.annotationMap
            |> Seq.map (fun kv -> (kv.Key, kv.Value))
            |> Seq.filter (fun (_, ty) -> match ty with | Type.TArrow _ -> true | _ -> false)
            |> Seq.toList
        Expect.isTrue (arrowEntries.Length >= 2)
            (sprintf "Expected >= 2 TArrow for mutual rec with 2 annotated params, got %d" arrowEntries.Length)
    }

    // TA-10: FieldAccess records record type at accessExpr span (Issue #20)
    // FunLangCompiler needs TData at the sub-expression span to disambiguate
    // same-named fields across record types.
    test "TA-10: FieldAccess records TData at accessExpr span" {
        let input = "type Foo = { x : int; y : string }\nlet _ =\n    let f = { x = 42; y = \"hello\" }\n    f.x"
        let m = parseModuleWithPositions input
        Bidir.annotationMap.Clear()
        let result = TypeCheck.typeCheckModule m
        match result with
        | Error errs -> failwith (sprintf "Type check error: %A" errs)
        | Ok _ -> ()
        // Find the TData("Foo", []) entry that corresponds to the Var "f" in "f.x"
        // The Var "f" in "f.x" is on line 4, and should have TData("Foo", []) recorded
        let fooEntries =
            Bidir.annotationMap
            |> Seq.map (fun kv -> (kv.Key, kv.Value))
            |> Seq.filter (fun (_, ty) ->
                match ty with
                | Type.TData("Foo", _) -> true
                | _ -> false)
            |> Seq.toList
        // Should have TData("Foo") at the Var span (for FunLangCompiler field disambiguation)
        Expect.isTrue (fooEntries.Length >= 1)
            (sprintf "Expected TData(\"Foo\", []) at accessExpr span, got %d TData entries" fooEntries.Length)
        // The Var "f" span (line 4) must be among TData entries
        let hasVarSpan = fooEntries |> List.exists (fun (span, _) -> span.StartLine = 4)
        Expect.isTrue hasVarSpan
            "TData(\"Foo\", []) should be recorded at the Var 'f' span on line 4"
    }

    // TA-11: TEName annotation resolves to TData for record types (Issue #22)
    // Previously `(p : SrcLoc) -> p.field` failed because TEName elaborated to fresh TVar.
    test "TA-11: TEName record annotation resolves so field access typechecks" {
        let input = "type SrcLoc = { file : string; line : int; col : int }\nlet formatLoc (loc : SrcLoc) : string = loc.file"
        let m = parseModuleWithPositions input
        Bidir.annotationMap.Clear()
        let result = TypeCheck.typeCheckModule m
        match result with
        | Error errs -> failwith (sprintf "Type check error: %A" errs)
        | Ok _ -> ()
    }

    // TA-12: Type alias annotation expands to the aliased type (Issue #22)
    // `type Name = string; let f (n : Name) : string = n` — Name resolves to TString.
    test "TA-12: Type alias annotation expands to aliased type" {
        let input = "type Name = string\nlet greet (n : Name) : string = n"
        let m = parseModuleWithPositions input
        Bidir.annotationMap.Clear()
        let result = TypeCheck.typeCheckModule m
        match result with
        | Error errs -> failwith (sprintf "Type check error: %A" errs)
        | Ok _ -> ()
    }

    // TA-13: Type alias with parameters expands properly
    test "TA-13: Parameterized type alias (type Pair = int * int) expands to TTuple" {
        let input = "type Pair = int * int\nlet swap (p : Pair) : Pair =\n    let (a, b) = p\n    (b, a)"
        let m = parseModuleWithPositions input
        Bidir.annotationMap.Clear()
        let result = TypeCheck.typeCheckModule m
        match result with
        | Error errs -> failwith (sprintf "Type check error: %A" errs)
        | Ok _ -> ()
    }

    // TA-14: Issue #26 — typeCheckFile includes imported file spans in AnnotationMap.
    // Previously Prelude.parseModuleFromString used a non-positional tokenizer,
    // leaving imported file AST spans at initial position and annotationMap entries
    // colliding/overwriting.
    test "TA-14: typeCheckFile AnnotationMap contains spans from imported files" {
        let tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "funlang-issue26")
        System.IO.Directory.CreateDirectory(tmpDir) |> ignore
        let libPath = System.IO.Path.Combine(tmpDir, "lib.fun")
        let mainPath = System.IO.Path.Combine(tmpDir, "main.fun")
        let libSrc = "type Foo = { items : int list; start : int }\nlet getFooStart (f : Foo) : int = f.start\n"
        let mainSrc = "import \"lib.fun\"\n\nlet _ =\n    let f : Foo = { items = [1]; start = 42 }\n    printfn \"%d\" (getFooStart f)\n"
        System.IO.File.WriteAllText(libPath, libSrc)
        System.IO.File.WriteAllText(mainPath, mainSrc)
        let tm = ExportApi.typeCheckFile mainPath
        let absLib = System.IO.Path.GetFullPath(libPath)
        let absMain = System.IO.Path.GetFullPath(mainPath)
        let libSpans = tm.AnnotationMap |> Map.filter (fun k _ -> k.FileName = absLib)
        let mainSpans = tm.AnnotationMap |> Map.filter (fun k _ -> k.FileName = absMain)
        Expect.isTrue (libSpans.Count >= 4)
            (sprintf "Expected >=4 lib.fun spans, got %d (total %d)" libSpans.Count tm.AnnotationMap.Count)
        Expect.isTrue (mainSpans.Count >= 10)
            (sprintf "Expected >=10 main.fun spans, got %d" mainSpans.Count)
    }

    // TA-15: Issue #27 — multiple imports all survive in AnnotationMap.
    // Previously only the last-imported file's spans (plus main) survived,
    // because each recursive typeCheckModuleWithPrelude reset Bidir.annotationMap
    // and overwrote entries from earlier imports.
    test "TA-15: typeCheckFile AnnotationMap contains spans from ALL imported files" {
        let tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "funlang-issue27")
        System.IO.Directory.CreateDirectory(tmpDir) |> ignore
        let aPath = System.IO.Path.Combine(tmpDir, "a.fun")
        let bPath = System.IO.Path.Combine(tmpDir, "b.fun")
        let cPath = System.IO.Path.Combine(tmpDir, "c.fun")
        let mainPath = System.IO.Path.Combine(tmpDir, "main.fun")
        System.IO.File.WriteAllText(aPath, "type AA = { a1 : int; a2 : string }\nlet getA1 (x : AA) : int = x.a1\n")
        System.IO.File.WriteAllText(bPath, "type BB = { b1 : int }\nlet getB1 (x : BB) : int = x.b1\n")
        System.IO.File.WriteAllText(cPath, "type CC = { c1 : string }\nlet getC1 (x : CC) : string = x.c1\n")
        System.IO.File.WriteAllText(mainPath, "import \"a.fun\"\nimport \"b.fun\"\nimport \"c.fun\"\n\nlet _ =\n    let a : AA = { a1 = 1; a2 = \"x\" }\n    let b : BB = { b1 = 2 }\n    let c : CC = { c1 = \"y\" }\n    printfn \"%d %d %s\" (getA1 a) (getB1 b) (getC1 c)\n")
        let tm = ExportApi.typeCheckFile mainPath
        let absA = System.IO.Path.GetFullPath(aPath)
        let absB = System.IO.Path.GetFullPath(bPath)
        let absC = System.IO.Path.GetFullPath(cPath)
        let absMain = System.IO.Path.GetFullPath(mainPath)
        let countFor path = tm.AnnotationMap |> Map.filter (fun k _ -> k.FileName = path) |> Map.count
        let nA = countFor absA
        let nB = countFor absB
        let nC = countFor absC
        let nMain = countFor absMain
        Expect.isTrue (nA >= 3) (sprintf "a.fun: expected >=3 spans, got %d" nA)
        Expect.isTrue (nB >= 3) (sprintf "b.fun: expected >=3 spans, got %d" nB)
        Expect.isTrue (nC >= 3) (sprintf "c.fun: expected >=3 spans, got %d" nC)
        Expect.isTrue (nMain >= 10) (sprintf "main.fun: expected >=10 spans, got %d" nMain)
    }

]
