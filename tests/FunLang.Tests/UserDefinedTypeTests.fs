module FunLang.Tests.UserDefinedTypeTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Types
open FunLang.TypeInfer
open FunLang.Interpreter
open FunLang.Parser
open FunLang.GeneratedParser
open FunLang.Tests.TestHelpers

// =============================================================================
// Helper Functions for Located Types
// =============================================================================

/// Wrap an Expr in Located.noLoc for evaluation
let loc (e: Expr) : LExpr = Located.noLoc e

/// Wrap a Pattern in Located.noLoc
let locP (p: Pattern) : LPattern = Located.noLoc p

/// Infer type of an Expr by wrapping it in Located.noLoc
let inferExpr (typeDefEnv: TypeEnv) (e: Expr) =
    inferTypeWithTypeDefEnv typeDefEnv (loc e)

/// Evaluate an Expr by wrapping it in Located.noLoc
let evalExpr env e = eval env (loc e)

/// Create a located match case from pattern, guard, and body expressions
let matchCase (p: Pattern) (guard: Expr option) (body: Expr) : LPattern * LExpr option * LExpr =
    (locP p, Option.map loc guard, loc body)

// =============================================================================
// Phase 6: User-Defined Types (Discriminated Unions)
// =============================================================================
//
// Syntax: type Option 'a = None | Some of 'a
//         type List 'a = Nil | Cons of 'a * List 'a
//
// This file tests:
// 1. Lexer: TYPEVAR token ('a, 'b, etc.)
// 2. Parser: Type declarations, constructor expressions
// 3. Type inference: User-defined type constructors
// 4. Interpreter: Constructor creation and pattern matching
// =============================================================================

// =============================================================================
// Lexer Tests - TYPEVAR token
// =============================================================================

let lexerTests = testList "Lexer - TYPEVAR" [

    test "tokenize single type variable 'a" {
        let result = tokenizeString "'a"
        let tokens = expectOk "tokenize 'a" result
        Expect.exists tokens (function TYPEVAR "a" -> true | _ -> false) "should have TYPEVAR a"
    }

    test "tokenize type variable 'abc" {
        let result = tokenizeString "'abc"
        let tokens = expectOk "tokenize 'abc" result
        Expect.exists tokens (function TYPEVAR "abc" -> true | _ -> false) "should have TYPEVAR abc"
    }

    test "tokenize multiple type variables" {
        let result = tokenizeString "'a 'b 'c"
        let tokens = expectOk "tokenize 'a 'b 'c" result
        Expect.exists tokens (function TYPEVAR "a" -> true | _ -> false) "should have TYPEVAR a"
        Expect.exists tokens (function TYPEVAR "b" -> true | _ -> false) "should have TYPEVAR b"
        Expect.exists tokens (function TYPEVAR "c" -> true | _ -> false) "should have TYPEVAR c"
    }

    test "tokenize type declaration keywords" {
        let result = tokenizeString "type Option of"
        let tokens = expectOk "tokenize type of" result
        Expect.exists tokens (function TYPE -> true | _ -> false) "should have TYPE"
        Expect.exists tokens (function OF -> true | _ -> false) "should have OF"
    }

    test "tokenize type with type variable" {
        let result = tokenizeString "type Option 'a"
        let tokens = expectOk "tokenize type Option 'a" result
        Expect.exists tokens (function TYPE -> true | _ -> false) "should have TYPE"
        Expect.exists tokens (function IDENT "Option" -> true | _ -> false) "should have IDENT Option"
        Expect.exists tokens (function TYPEVAR "a" -> true | _ -> false) "should have TYPEVAR a"
    }
]

// =============================================================================
// AST Tests - Constructor Expressions
// =============================================================================

let astTests = testList "AST - Constructors" [

    test "EConstructor with no argument" {
        let expr = EConstructor ("None", None)
        match expr with
        | EConstructor ("None", None) -> ()
        | _ -> failtest "should be EConstructor None"
    }

    test "EConstructor with argument" {
        let arg = loc (ELiteral (LInt 42))
        let expr = EConstructor ("Some", Some arg)
        match expr with
        | EConstructor ("Some", Some larg) when larg.Node = ELiteral (LInt 42) -> ()
        | _ -> failtest "should be EConstructor Some with int"
    }

    test "PConstructor pattern with no argument" {
        let pat = PConstructor ("None", None)
        match pat with
        | PConstructor ("None", None) -> ()
        | _ -> failtest "should be PConstructor None"
    }

    test "PConstructor pattern with argument" {
        let argPat = locP (PVariable "x")
        let pat = PConstructor ("Some", Some argPat)
        match pat with
        | PConstructor ("Some", Some lpat) when lpat.Node = PVariable "x" -> ()
        | _ -> failtest "should be PConstructor Some with variable"
    }

    test "VConstructed value with no argument" {
        let value = VConstructed ("None", None)
        match value with
        | VConstructed ("None", None) -> ()
        | _ -> failtest "should be VConstructed None"
    }

    test "VConstructed value with argument" {
        let value = VConstructed ("Some", Some (VInt 42))
        match value with
        | VConstructed ("Some", Some (VInt 42)) -> ()
        | _ -> failtest "should be VConstructed Some with int"
    }
]

// =============================================================================
// Interpreter Tests - Constructor Values
// =============================================================================

let interpreterTests = testList "Interpreter - Constructors" [

    test "evaluate constructor without argument" {
        let expr = EConstructor ("None", None)
        let result = evalExpr Map.empty expr
        let value = expectOk "eval None" result
        match value with
        | VConstructed ("None", None) -> ()
        | _ -> failtest (sprintf "expected VConstructed None, got %A" value)
    }

    test "evaluate constructor with argument" {
        let expr = EConstructor ("Some", Some (loc (ELiteral (LInt 42))))
        let result = evalExpr Map.empty expr
        let value = expectOk "eval Some 42" result
        match value with
        | VConstructed ("Some", Some (VInt 42)) -> ()
        | _ -> failtest (sprintf "expected VConstructed Some 42, got %A" value)
    }

    test "pattern match on constructor without argument" {
        // match None with | None -> 1 | Some _ -> 2
        let expr = EMatch (
            loc (EConstructor ("None", None)),
            [
                matchCase (PConstructor ("None", None)) None (ELiteral (LInt 1))
                matchCase (PConstructor ("Some", Some (locP PWildcard))) None (ELiteral (LInt 2))
            ])
        let result = evalExpr Map.empty expr
        let value = expectOk "match None" result
        Expect.equal value (VInt 1) "should match None case"
    }

    test "pattern match on constructor with argument" {
        // match Some 42 with | None -> 0 | Some x -> x
        let expr = EMatch (
            loc (EConstructor ("Some", Some (loc (ELiteral (LInt 42))))),
            [
                matchCase (PConstructor ("None", None)) None (ELiteral (LInt 0))
                matchCase (PConstructor ("Some", Some (locP (PVariable "x")))) None (EVariable "x")
            ])
        let result = evalExpr Map.empty expr
        let value = expectOk "match Some 42" result
        Expect.equal value (VInt 42) "should extract value from Some"
    }

    test "nested constructor pattern matching" {
        // match Some (Some 1) with | Some (Some x) -> x | _ -> 0
        let innerSome = EConstructor ("Some", Some (loc (ELiteral (LInt 1))))
        let outerSome = EConstructor ("Some", Some (loc innerSome))
        let nestedPattern = PConstructor ("Some", Some (locP (PConstructor ("Some", Some (locP (PVariable "x"))))))
        let expr = EMatch (
            loc outerSome,
            [
                matchCase nestedPattern None (EVariable "x")
                matchCase PWildcard None (ELiteral (LInt 0))
            ])
        let result = evalExpr Map.empty expr
        let value = expectOk "match nested Some" result
        Expect.equal value (VInt 1) "should extract nested value"
    }
]

// =============================================================================
// Parser Tests - Type Declarations (TODO: implement parsing)
// =============================================================================

let parserTests = testList "Parser - Type Declarations" [

    test "parse simple type declaration" {
        // type Bool = True | False
        let result = parseStringToProgram "type Bool = True | False"
        let program = expectOk "parse type Bool" result
        Expect.equal program.TypeDefs.Length 1 "should have 1 type def"
        let typeDef = program.TypeDefs.[0]
        Expect.equal typeDef.Name "Bool" "type name should be Bool"
        Expect.equal typeDef.TypeParams [] "should have no type params"
        Expect.equal typeDef.Constructors.Length 2 "should have 2 constructors"
        Expect.equal typeDef.Constructors.[0] ("True", None) "first constructor should be True"
        Expect.equal typeDef.Constructors.[1] ("False", None) "second constructor should be False"
    }

    test "parse type declaration with type parameter" {
        // type Option 'a = None | Some of 'a
        let result = parseStringToProgram "type Option 'a = None | Some of 'a"
        let program = expectOk "parse type Option" result
        Expect.equal program.TypeDefs.Length 1 "should have 1 type def"
        let typeDef = program.TypeDefs.[0]
        Expect.equal typeDef.Name "Option" "type name should be Option"
        Expect.equal typeDef.TypeParams ["a"] "should have type param 'a"
        Expect.equal typeDef.Constructors.Length 2 "should have 2 constructors"
        Expect.equal typeDef.Constructors.[0] ("None", None) "first constructor should be None"
        Expect.equal typeDef.Constructors.[1] ("Some", Some (TEVar "a")) "second constructor should be Some of 'a"
    }

    test "parse constructor expression without argument" {
        // None
        let result = parseStringToAst "None"
        let lexpr = expectOk "parse None" result
        match lexpr.Node with
        | EConstructor ("None", None) -> ()
        | EVariable "None" -> () // Initially might be parsed as variable
        | _ -> failtest (sprintf "expected constructor, got %A" lexpr.Node)
    }

    test "parse constructor expression with argument" {
        // Some 42
        let result = parseStringToAst "Some 42"
        let lexpr = expectOk "parse Some 42" result
        match lexpr.Node with
        | EConstructor ("Some", Some larg) when larg.Node = ELiteral (LInt 42) -> ()
        | EApply (lfn, larg) when lfn.Node = EVariable "Some" && larg.Node = ELiteral (LInt 42) -> ()
        | _ -> failtest (sprintf "expected constructor application, got %A" lexpr.Node)
    }
]

// =============================================================================
// Type Definition Environment Tests
// =============================================================================

let typeDefEnvTests = testList "Type Definition Environment" [

    test "create TConstructor type" {
        // TConstructor ("Option", [TInt]) represents Option int
        let optionInt = TConstructor ("Option", [TInt])
        match optionInt with
        | TConstructor ("Option", [TInt]) -> ()
        | _ -> failtest "should be TConstructor Option int"
    }

    test "create polymorphic TConstructor" {
        // TConstructor ("Option", [TVar 1]) represents Option 'a
        let optionA = TConstructor ("Option", [TVar 1])
        match optionA with
        | TConstructor ("Option", [TVar 1]) -> ()
        | _ -> failtest "should be TConstructor Option 'a1"
    }

    test "buildTypeDefEnv creates constructor schemes" {
        // type Bool = True | False
        let typeDef: TypeDef = {
            Name = "Bool"
            TypeParams = []
            Constructors = [("True", None); ("False", None)]
        }
        let env = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]

        // True : Bool, False : Bool
        Expect.isTrue (Map.containsKey "True" env) "should have True"
        Expect.isTrue (Map.containsKey "False" env) "should have False"
    }

    test "buildTypeDefEnv with type parameter" {
        // type Option 'a = None | Some of 'a
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let env = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]

        // None : forall 'a. Option 'a
        // Some : forall 'a. 'a -> Option 'a
        Expect.isTrue (Map.containsKey "None" env) "should have None"
        Expect.isTrue (Map.containsKey "Some" env) "should have Some"
    }

    test "constructor type lookup for nullary constructor" {
        // type Bool = True | False
        // True should have type Bool
        let typeDef: TypeDef = {
            Name = "Bool"
            TypeParams = []
            Constructors = [("True", None); ("False", None)]
        }
        let env = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]

        match Map.tryFind "True" env with
        | Some (Forall ([], TConstructor ("Bool", []))) -> ()
        | Some scheme -> failtest (sprintf "unexpected scheme: %A" scheme)
        | None -> failtest "True not found in env"
    }

    test "constructor type lookup for unary constructor" {
        // type Option 'a = None | Some of 'a
        // Some should have type forall 'a. 'a -> Option 'a
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let env = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]

        match Map.tryFind "Some" env with
        | Some (Forall (vars, TFun (TVar argVar, TConstructor ("Option", [TVar resultVar])))) ->
            Expect.equal vars.Length 1 "should have 1 quantified var"
            Expect.equal argVar resultVar "argument and result type var should match"
        | Some scheme -> failtest (sprintf "unexpected scheme: %A" scheme)
        | None -> failtest "Some not found in env"
    }
]

// =============================================================================
// Type Inference Tests - Constructors
// =============================================================================

let typeInferenceTests = testList "Type Inference - Constructors" [

    test "infer type of nullary constructor (Bool)" {
        // type Bool = True | False
        // True : Bool
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Bool"
            TypeParams = []
            Constructors = [("True", None); ("False", None)]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let expr = EConstructor ("True", None)
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer True" result
        Expect.equal t (TConstructor ("Bool", [])) "True should have type Bool"
    }

    test "infer type of nullary constructor (Option None)" {
        // type Option 'a = None | Some of 'a
        // None : Option 'a (polymorphic)
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let expr = EConstructor ("None", None)
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer None" result
        match t with
        | TConstructor ("Option", [TVar _]) -> ()  // Option 'a with fresh var
        | _ -> failtest (sprintf "expected Option<'a>, got %A" t)
    }

    test "infer type of unary constructor (Some 42)" {
        // type Option 'a = None | Some of 'a
        // Some 42 : Option int
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let expr = EConstructor ("Some", Some (loc (ELiteral (LInt 42))))
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer Some 42" result
        Expect.equal t (TConstructor ("Option", [TInt])) "Some 42 should have type Option<int>"
    }

    test "infer type of nested constructor (Some (Some 1))" {
        // Some (Some 1) : Option (Option int)
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let inner = EConstructor ("Some", Some (loc (ELiteral (LInt 1))))
        let outer = EConstructor ("Some", Some (loc inner))
        let result = inferExpr typeDefEnv outer
        let t = expectOk "infer Some (Some 1)" result
        Expect.equal t (TConstructor ("Option", [TConstructor ("Option", [TInt])])) "nested Option"
    }

    test "unknown constructor returns error" {
        // Unknown constructor should fail
        TypeHelpers.resetCounter ()
        let typeDefEnv = Map.empty  // No types defined
        let expr = EConstructor ("Unknown", None)
        let result = inferExpr typeDefEnv expr
        Expect.isError result "unknown constructor should fail"
    }
]

// =============================================================================
// Pattern Type Inference Tests - PConstructor
// =============================================================================

let patternTypeInferenceTests = testList "Type Inference - PConstructor Patterns" [

    test "infer type of match with nullary constructor pattern" {
        // type Bool = True | False
        // match True with | True -> 1 | False -> 0
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Bool"
            TypeParams = []
            Constructors = [("True", None); ("False", None)]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let expr = EMatch (
            loc (EConstructor ("True", None)),
            [
                matchCase (PConstructor ("True", None)) None (ELiteral (LInt 1))
                matchCase (PConstructor ("False", None)) None (ELiteral (LInt 0))
            ])
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer match on Bool" result
        Expect.equal t TInt "match should return int"
    }

    test "infer type of match with unary constructor pattern" {
        // type Option 'a = None | Some of 'a
        // match Some 42 with | None -> 0 | Some x -> x
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let expr = EMatch (
            loc (EConstructor ("Some", Some (loc (ELiteral (LInt 42))))),
            [
                matchCase (PConstructor ("None", None)) None (ELiteral (LInt 0))
                matchCase (PConstructor ("Some", Some (locP (PVariable "x")))) None (EVariable "x")
            ])
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer match on Option int" result
        Expect.equal t TInt "match should return int"
    }

    test "infer type bindings in constructor pattern" {
        // type Option 'a = None | Some of 'a
        // match Some true with | Some x -> x | None -> false
        // x should be bound to bool
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let expr = EMatch (
            loc (EConstructor ("Some", Some (loc (ELiteral (LBool true))))),
            [
                matchCase (PConstructor ("Some", Some (locP (PVariable "x")))) None (EVariable "x")
                matchCase (PConstructor ("None", None)) None (ELiteral (LBool false))
            ])
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer match with bool" result
        Expect.equal t TBool "x should be bool"
    }

    test "infer nested constructor pattern" {
        // type Option 'a = None | Some of 'a
        // match Some (Some 1) with | Some (Some x) -> x | _ -> 0
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Option"
            TypeParams = ["a"]
            Constructors = [("None", None); ("Some", Some (TEVar "a"))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let innerSome = EConstructor ("Some", Some (loc (ELiteral (LInt 1))))
        let outerSome = EConstructor ("Some", Some (loc innerSome))
        let nestedPattern = PConstructor ("Some", Some (locP (PConstructor ("Some", Some (locP (PVariable "x"))))))
        let expr = EMatch (
            loc outerSome,
            [
                matchCase nestedPattern None (EVariable "x")
                matchCase PWildcard None (ELiteral (LInt 0))
            ])
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer nested pattern" result
        Expect.equal t TInt "nested pattern should infer int"
    }

    test "type error on constructor pattern mismatch" {
        // type Bool = True | False
        // match True with | Some x -> x | _ -> 0
        // Should fail: Some is not a Bool constructor
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "Bool"
            TypeParams = []
            Constructors = [("True", None); ("False", None)]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let expr = EMatch (
            loc (EConstructor ("True", None)),
            [
                matchCase (PConstructor ("Some", Some (locP (PVariable "x")))) None (EVariable "x")
                matchCase PWildcard None (ELiteral (LInt 0))
            ])
        let result = inferExpr typeDefEnv expr
        Expect.isError result "mismatched constructor should fail type check"
    }
]

// =============================================================================
// Integration Tests - Full Pipeline (TODO: implement full pipeline)
// =============================================================================

let integrationTests = testList "Integration - User Types" [

    test "debug: parse option type only" {
        // Just test parsing the type definition
        let code = "type Option 'a = None | Some of 'a"
        let result = parseProgramString code
        match result with
        | Ok program ->
            Expect.equal program.TypeDefs.Length 1 "should have 1 type def"
        | Error e ->
            failtest (sprintf "parse failed: %s" e)
    }

    test "debug: parse option type with simple expr" {
        let code = "type Option 'a = None | Some of 'a\n42"
        let result = parseProgramString code
        match result with
        | Ok program ->
            Expect.equal program.TypeDefs.Length 1 "should have 1 type def"
            Expect.isSome program.MainExpr "should have main expr"
        | Error e ->
            failtest (sprintf "parse failed: %s" e)
    }

    test "debug: parse option type with None expr" {
        let code = "type Option 'a = None | Some of 'a\nNone"
        let result = parseProgramString code
        match result with
        | Ok program ->
            Expect.equal program.TypeDefs.Length 1 "should have 1 type def"
            Expect.isSome program.MainExpr "should have main expr"
        | Error e ->
            failtest (sprintf "parse failed: %s" e)
    }

    test "define and use Bool type" {
        // Nullary constructors work as patterns
        let code = "type Bool = True | False\nmatch True with | True -> 1 | False -> 0"
        let result = runProgram code
        let value = expectOk "run Bool example" result
        Expect.equal value (VInt 1) "should evaluate to 1"
    }

    test "pattern match on None" {
        // Nullary constructor patterns work, but unary (Some n) doesn't
        // because the parser grammar doesn't support constructor application patterns
        let code = "type Option 'a = None | Some of 'a\nmatch None with | None -> 0 | _ -> 1"
        let result = runProgram code
        let value = expectOk "run None match" result
        Expect.equal value (VInt 0) "should evaluate to 0"
    }

    // Note: Full constructor pattern syntax (e.g., "Some n" as a pattern)
    // requires grammar extension - see PatternTests for AST-level tests
]

// =============================================================================
// Phase 7: Constructor Application Patterns in Parser
// =============================================================================
// These tests require parser grammar extension to support patterns like "Some n"

let constructorPatternParserTests = testList "Parser - Constructor Application Patterns" [

    test "parse constructor pattern with variable (Some x)" {
        // match e with | Some x -> x
        // Should parse "Some x" as PConstructor("Some", Some(PVariable "x"))
        let code = "type Option 'a = None | Some of 'a\nmatch None with | Some x -> x | None -> 0"
        let result = parseProgramString code
        match result with
        | Error e -> failtest (sprintf "parse failed: %s" e)
        | Ok program ->
            match program.MainExpr with
            | Some lexpr ->
                match lexpr.Node with
                | EMatch (_, cases) ->
                    // First case should have pattern PVariable "Some" (before grammar fix)
                    // or PConstructor ("Some", Some (PVariable "x")) (after grammar fix)
                    let firstPattern = match cases with (lp, _, _) :: _ -> lp.Node | [] -> PWildcard
                    match firstPattern with
                    | PConstructor ("Some", Some lpat) when lpat.Node = PVariable "x" ->
                        () // Grammar is fixed!
                    | PVariable "Some" ->
                        failtest "Parser treats 'Some x' as separate tokens - grammar extension needed"
                    | other ->
                        failtest (sprintf "unexpected pattern: %A" other)
                | _ -> failtest "expected match expression"
            | _ -> failtest "expected main expression"
    }

    test "parse constructor pattern with literal (Some 42)" {
        let code = "type Option 'a = None | Some of 'a\nmatch None with | Some 42 -> 1 | _ -> 0"
        let result = parseProgramString code
        match result with
        | Error e -> failtest (sprintf "parse failed: %s" e)
        | Ok program ->
            match program.MainExpr with
            | Some lexpr ->
                match lexpr.Node with
                | EMatch (_, cases) ->
                    let firstPattern = match cases with (lp, _, _) :: _ -> lp.Node | [] -> PWildcard
                    match firstPattern with
                    | PConstructor ("Some", Some lpat) when lpat.Node = PLiteral (LInt 42) -> ()
                    | _ -> failtest (sprintf "expected PConstructor Some 42, got: %A" firstPattern)
                | _ -> failtest "expected match expression"
            | _ -> failtest "expected main expression"
    }

    test "parse nested constructor pattern (Some (Some x))" {
        let code = "type Option 'a = None | Some of 'a\nmatch None with | Some (Some x) -> x | _ -> 0"
        let result = parseProgramString code
        match result with
        | Error e -> failtest (sprintf "parse failed: %s" e)
        | Ok program ->
            match program.MainExpr with
            | Some lexpr ->
                match lexpr.Node with
                | EMatch (_, cases) ->
                    let firstPattern = match cases with (lp, _, _) :: _ -> lp.Node | [] -> PWildcard
                    match firstPattern with
                    | PConstructor ("Some", Some lpat) ->
                        match lpat.Node with
                        | PConstructor ("Some", Some lpat2) when lpat2.Node = PVariable "x" -> ()
                        | _ -> failtest (sprintf "expected nested constructor pattern, got: %A" firstPattern)
                    | _ -> failtest (sprintf "expected nested constructor pattern, got: %A" firstPattern)
                | _ -> failtest "expected match expression"
            | _ -> failtest "expected main expression"
    }

    test "full pipeline: match Some 42 with Some x" {
        let code = """type Option 'a = None | Some of 'a
match Some 42 with | Some x -> x | None -> 0"""
        let result = runProgram code
        let value = expectOk "run Some pattern match" result
        Expect.equal value (VInt 42) "should extract 42 from Some"
    }

    test "full pipeline: nested Some pattern" {
        let code = """type Option 'a = None | Some of 'a
match Some (Some 1) with | Some (Some x) -> x | _ -> 0"""
        let result = runProgram code
        let value = expectOk "run nested Some" result
        Expect.equal value (VInt 1) "should extract 1 from nested Some"
    }
]

// =============================================================================
// Phase 7: Recursive Types
// =============================================================================
// Recursive types like:
//   type List 'a = Nil | Cons of 'a * List 'a
//   type Tree 'a = Leaf of 'a | Node of Tree 'a * Tree 'a

let recursiveTypeTests = testList "Recursive Types" [

    test "parse recursive list type definition" {
        // type List 'a = Nil | Cons of 'a * List 'a
        let code = "type List 'a = Nil | Cons of 'a * List 'a"
        let result = parseStringToProgram code
        let program = expectOk "parse List type" result
        Expect.equal program.TypeDefs.Length 1 "should have 1 type def"
        let typeDef = program.TypeDefs.[0]
        Expect.equal typeDef.Name "List" "type name should be List"
        Expect.equal typeDef.TypeParams ["a"] "should have type param 'a"
        Expect.equal typeDef.Constructors.Length 2 "should have 2 constructors"
    }

    test "build type env for recursive list type" {
        // type List 'a = Nil | Cons of 'a * List 'a
        // Nil : forall 'a. List 'a
        // Cons : forall 'a. 'a * List 'a -> List 'a
        let typeDef: TypeDef = {
            Name = "List"
            TypeParams = ["a"]
            Constructors = [("Nil", None); ("Cons", Some (TETuple [TEVar "a"; TEApp ("List", [TEVar "a"])]))]
        }
        let env = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]

        Expect.isTrue (Map.containsKey "Nil" env) "should have Nil"
        Expect.isTrue (Map.containsKey "Cons" env) "should have Cons"

        // Check Cons type: forall 'a. ('a * List 'a) -> List 'a
        match Map.tryFind "Cons" env with
        | Some (Forall (vars, TFun (argType, resultType))) ->
            Expect.equal vars.Length 1 "should have 1 quantified var"
            // argType should be TTuple ['a, List 'a]
            match argType with
            | TTuple [TVar _; TConstructor ("List", [TVar _])] -> ()
            | _ -> failtest (sprintf "expected tuple type, got %A" argType)
        | Some scheme -> failtest (sprintf "unexpected scheme: %A" scheme)
        | None -> failtest "Cons not found in env"
    }

    test "infer type of Nil constructor" {
        // type List 'a = Nil | Cons of 'a * List 'a
        // Nil : List 'a
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "List"
            TypeParams = ["a"]
            Constructors = [("Nil", None); ("Cons", Some (TETuple [TEVar "a"; TEApp ("List", [TEVar "a"])]))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let expr = EConstructor ("Nil", None)
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer Nil" result
        match t with
        | TConstructor ("List", [TVar _]) -> ()
        | _ -> failtest (sprintf "expected List<'a>, got %A" t)
    }

    test "infer type of Cons constructor" {
        // Cons (1, Nil) : List int
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "List"
            TypeParams = ["a"]
            Constructors = [("Nil", None); ("Cons", Some (TETuple [TEVar "a"; TEApp ("List", [TEVar "a"])]))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let nil = EConstructor ("Nil", None)
        let pair = ETuple [loc (ELiteral (LInt 1)); loc nil]
        let cons = EConstructor ("Cons", Some (loc pair))
        let result = inferExpr typeDefEnv cons
        let t = expectOk "infer Cons" result
        Expect.equal t (TConstructor ("List", [TInt])) "Cons (1, Nil) should be List<int>"
    }

    test "pattern match on recursive list" {
        // match Nil with | Nil -> 0 | Cons (h, t) -> h
        TypeHelpers.resetCounter ()
        let typeDef: TypeDef = {
            Name = "List"
            TypeParams = ["a"]
            Constructors = [("Nil", None); ("Cons", Some (TETuple [TEVar "a"; TEApp ("List", [TEVar "a"])]))]
        }
        let typeDefEnv = TypeDefEnvBuilder.buildTypeDefEnv [typeDef]
        let tuplePat = PTuple [locP (PVariable "h"); locP (PVariable "t")]
        let expr = EMatch (
            loc (EConstructor ("Nil", None)),
            [
                matchCase (PConstructor ("Nil", None)) None (ELiteral (LInt 0))
                matchCase (PConstructor ("Cons", Some (locP tuplePat))) None (EVariable "h")
            ])
        let result = inferExpr typeDefEnv expr
        let t = expectOk "infer match on List" result
        Expect.equal t TInt "match should return int"
    }
]

// =============================================================================
// Property-Based Tests
// =============================================================================

let propertyTests = testList "Properties - User Types" [

    testProperty "constructor creates VConstructed" <| fun (name: NonEmptyString) ->
        // Filter to only allow valid identifier names
        let validName = name.Get |> String.filter System.Char.IsLetter
        if validName.Length > 0 then
            let expr = EConstructor (validName, None)
            match evalExpr Map.empty expr with
            | Ok (VConstructed (n, None)) -> n = validName
            | _ -> false
        else true  // Skip invalid names

    testProperty "Some x pattern extracts x" <| fun (n: int) ->
        let expr = EMatch (
            loc (EConstructor ("Some", Some (loc (ELiteral (LInt n))))),
            [
                matchCase (PConstructor ("Some", Some (locP (PVariable "x")))) None (EVariable "x")
                matchCase PWildcard None (ELiteral (LInt 0))
            ])
        match evalExpr Map.empty expr with
        | Ok (VInt m) -> m = n
        | _ -> false
]

// =============================================================================
// All User-Defined Type Tests
// =============================================================================

[<Tests>]
let allUserTypeTests = testList "User-Defined Types" [
    lexerTests
    astTests
    interpreterTests
    parserTests                       // Enabled: type declaration parsing
    typeDefEnvTests                   // Type definition environment tests
    typeInferenceTests                // Constructor type inference tests
    patternTypeInferenceTests         // PConstructor pattern type inference tests
    integrationTests                  // Full pipeline tests
    constructorPatternParserTests     // Phase 7: Constructor application patterns
    recursiveTypeTests                // Phase 7: Recursive types
    propertyTests
]
