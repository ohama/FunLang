module LangThree.Tests.GadtTests

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

// Helper to parse, type-check with full ctor env, and evaluate a module.
// Returns the formatted value of the last let binding, or None if no let binding.
let parseAndEval (input: string) : string option =
    let m = parseModule input
    match TypeCheck.typeCheckModuleWithPrelude Map.empty Map.empty Map.empty Map.empty Map.empty Map.empty m with
    | Error _ -> None
    | Ok (_, _ctorEnv, recEnv, _classEnv, _instEnv, _modules, _typeEnv) ->
        let decls =
            match m with
            | Ast.Module(ds, _) -> ds
            | _ -> []
        let finalEnv, _ = Eval.evalModuleDecls recEnv Map.empty Eval.emptyEnv decls
        match decls |> List.rev |> List.tryPick (function Ast.LetDecl(name, _, _) -> Some name | _ -> None) with
        | Some lastName ->
            match Map.tryFind lastName finalEnv with
            | Some v -> Some (Eval.formatValue v)
            | None -> None
        | None -> None

[<Tests>]
let gadtTests = testList "GADT" [

    // =====================================================
    // GADT-01: GADT constructor declarations with explicit return types
    // =====================================================

    testList "GADT-01: GADT declarations" [
        test "parse GADT type with explicit return types" {
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n    | Add : int Expr * int Expr -> int Expr\n"
            let result = parseModule input
            match result with
            | Ast.Module([d], _) ->
                match d with
                | Ast.Decl.TypeDecl(Ast.TypeDecl(name, tparams, ctors, _)) ->
                    Expect.equal name "Expr" "Type name should be Expr"
                    Expect.equal tparams ["'a"] "Should have type param 'a"
                    Expect.equal (List.length ctors) 3 "Should have 3 constructors"
                    // Verify all are GadtConstructorDecl
                    ctors |> List.iter (fun ctor ->
                        match ctor with
                        | Ast.GadtConstructorDecl _ -> ()
                        | Ast.ConstructorDecl _ -> failtest "Expected GadtConstructorDecl")
                | _ -> failtest "Expected TypeDecl"
            | _ -> failtest (sprintf "Expected module with one decl, got: %A" result)
        }

        test "type check GADT declaration succeeds" {
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "mixed GADT and regular constructors" {
            // When one constructor uses GADT syntax, the IsGadt sweep marks all as GADT
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n    | If : bool Expr * 'a Expr * 'a Expr -> 'a Expr\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }
    ]

    // =====================================================
    // GADT-02: Type refinement in pattern matching
    // GADT match must be in check mode: wrap match with (match ... : ResultType)
    // =====================================================

    testList "GADT-02: Type refinement" [
        test "GADT match with annotation refines types in branches" {
            // Wrap match in annotation to enter check mode
            // IntLit branch: scrutinee is int Expr, so payload n is int
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n\nlet eval e = (match e with | IntLit n -> n + 1 | BoolLit b -> 0 : int)\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "GADT match on bool Expr refines to bool" {
            // Wrap match in annotation (: bool) to enter check mode
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n\nlet eval e = (match e with | BoolLit b -> b | IntLit n -> true : bool)\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "GADT match with If constructor" {
            // If constructor preserves type parameter
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n    | If : bool Expr * 'a Expr * 'a Expr -> 'a Expr\n\nlet eval e = (match e with | IntLit n -> n | If (c, t, f) -> 0 : int)\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }
    ]

    // =====================================================
    // GADT-03: Existential types for data hiding
    // =====================================================

    testList "GADT-03: Existential types" [
        test "GADT constructor with existential type variable" {
            // Show : 'b * ('b -> string) -> string Expr
            // 'b is existential -- it appears in args but not in result type
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | Show : 'b * ('b -> string) -> string Expr\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "existential type used correctly in match branch" {
            // Inside the Show branch, 'b is bound; f v is well-typed because both use 'b
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | Show : 'b * ('b -> string) -> string Expr\n\nlet eval e = (match e with | Show (v, f) -> f v | IntLit n -> \"impossible\" : string)\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }
    ]

    // =====================================================
    // GADT-04: Polymorphic GADT match (no annotation required)
    // =====================================================

    testList "GADT-04: Polymorphic GADT match" [
        test "GADT match without annotation type-checks via fresh type variable" {
            // v1.8: synth-mode GADT match delegates to check with a fresh TVar
            // so no annotation is required for single-branch matches
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n\nlet eval e =\n    match e with\n    | IntLit n -> n\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Expected type-check to succeed, got: %s" (List.head diags).Message)
        }

        test "GADT match result type is inferred from single branch body" {
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n\nlet eval e =\n    match e with\n    | IntLit n -> n\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "GADT match without annotation does not produce E0401" {
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n\nlet eval e =\n    match e with\n    | IntLit n -> n\n"
            let result = parseAndTypeCheck input
            match result with
            | Error diags ->
                Expect.notEqual (List.head diags).Code (Some "E0401") "Should not produce E0401 error"
            | Ok _ -> ()
        }
    ]

    // =====================================================
    // GADT-05: Cross-type polymorphic return (eval : 'a Expr -> 'a)
    // =====================================================

    testList "GADT-05: Cross-type polymorphic return (eval : 'a Expr -> 'a)" [
        test "cross-type eval type-checks without annotation" {
            // Two branches with different return types: IntLit -> int, BoolLit -> bool
            // No annotation required — polymorphic mode infers per-branch independently
            let input =
                "type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr\n" +
                "let eval e = match e with | IntLit n -> n | BoolLit b -> b\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok _ -> ()
            | Error diags -> failtest (sprintf "Expected type-check to succeed (no E0301), got: %s" (List.head diags).Message)
        }

        test "cross-type eval with IntLit returns int value 42" {
            let input =
                "type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr\n" +
                "let eval e = match e with | IntLit n -> n | BoolLit b -> b\n" +
                "let r1 = eval (IntLit 42)\n"
            match parseAndEval input with
            | Some s -> Expect.stringContains s "42" "eval (IntLit 42) should produce 42"
            | None -> failtest "parseAndEval returned None"
        }

        test "cross-type eval with BoolLit returns bool value true" {
            let input =
                "type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr\n" +
                "let eval e = match e with | IntLit n -> n | BoolLit b -> b\n" +
                "let r2 = eval (BoolLit true)\n"
            match parseAndEval input with
            | Some s -> Expect.stringContains s "true" "eval (BoolLit true) should produce true"
            | None -> failtest "parseAndEval returned None"
        }
    ]

    // =====================================================
    // GADT-aware exhaustiveness
    // =====================================================

    testList "GADT exhaustiveness" [
        test "no false exhaustiveness warning for impossible GADT branches" {
            // When matching on int Expr via check mode, BoolLit is impossible
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n\nlet eval e = (match e with | IntLit n -> n : int)\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok (warnings, _, _, _) ->
                let hasExhaustivenessWarning = warnings |> List.exists (fun d -> d.Code = Some "W0001")
                Expect.isFalse hasExhaustivenessWarning
                    "Should NOT warn about missing BoolLit case when matching on int Expr"
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "exhaustiveness warning for genuinely missing GADT case" {
            // Add constructor also returns int Expr, so it must be matched
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n    | Add : int Expr * int Expr -> int Expr\n\nlet eval e = (match e with | IntLit n -> n : int)\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok (warnings, _, _, _) ->
                let hasW0001 = warnings |> List.exists (fun d -> d.Code = Some "W0001")
                Expect.isTrue hasW0001 "Should warn about missing Add case"
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "no warning when all possible GADT constructors covered" {
            let input = "type Expr 'a =\n    | IntLit : int -> int Expr\n    | BoolLit : bool -> bool Expr\n    | Add : int Expr * int Expr -> int Expr\n\nlet eval e = (match e with | IntLit n -> n | Add (a, b) -> 0 : int)\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok (warnings, _, _, _) ->
                let hasW0001 = warnings |> List.exists (fun d -> d.Code = Some "W0001")
                Expect.isFalse hasW0001 "No warning when all int Expr constructors covered"
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }
    ]

    // =====================================================
    // Backward compatibility
    // =====================================================

    testList "Backward compatibility" [
        test "regular ADT still works with type checking" {
            let input = "type Color = Red | Green | Blue\n\nlet toInt c =\n    match c with\n    | Red -> 1\n    | Green -> 2\n    | Blue -> 3\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok (warnings, _, _, _) ->
                Expect.isEmpty warnings "No warnings for complete match"
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "parametric ADT still works" {
            let input = "type Option 'a = None | Some of 'a\n\nlet getOrZero opt =\n    match opt with\n    | None -> 0\n    | Some x -> x\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok (warnings, _, _, _) ->
                Expect.isEmpty warnings "No warnings for complete match"
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }

        test "regular ADT exhaustiveness warning still works" {
            let input = "type Color = Red | Green | Blue\n\nlet f c =\n    match c with\n    | Red -> 1\n"
            let result = parseAndTypeCheck input
            match result with
            | Ok (warnings, _, _, _) ->
                let hasW0001 = warnings |> List.exists (fun d -> d.Code = Some "W0001")
                Expect.isTrue hasW0001 "Should warn about missing Green and Blue cases"
            | Error diags -> failtest (sprintf "Type checking failed: %s" (List.head diags).Message)
        }
    ]
]
