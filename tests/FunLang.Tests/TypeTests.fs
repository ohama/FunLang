module FunLang.Tests.TypeTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Types
open FunLang.Unification
open FunLang.TypeInfer
open FunLang.Tests.TestHelpers

// =============================================================================
// Test Helpers
// =============================================================================

/// Infer type of an Expr by wrapping it in Located.noLoc
let inferExpr (e: Expr) = inferType (Located.noLoc e)

// Aliases for Unlocated helpers
let binOp = Unlocated.binOp
let unaryOp = Unlocated.unaryOp
let elet = Unlocated.elet
let eletrec = Unlocated.eletrec
let elambda = Unlocated.elambda
let eapply = Unlocated.eapply
let eif = Unlocated.eif
let etuple = Unlocated.etuple
let elist = Unlocated.elist
let econs = Unlocated.econs
let ematch = Unlocated.ematch
let ptuple = Unlocated.ptuple
let plist = Unlocated.plist
let pcons = Unlocated.pcons

// =============================================================================
// Type Helper Tests
// =============================================================================

let typeHelperTests = testList "Type Helpers" [

    test "apply empty substitution is identity" {
        let t = TFun (TInt, TBool)
        let result = TypeHelpers.apply Map.empty t
        Expect.equal result t "empty substitution should not change type"
    }

    test "apply substitution to type variable" {
        let s = Map.ofList [(1, TInt)]
        let result = TypeHelpers.apply s (TVar 1)
        Expect.equal result TInt "should substitute type variable"
    }

    test "apply substitution to function type" {
        let s = Map.ofList [(1, TInt); (2, TBool)]
        let result = TypeHelpers.apply s (TFun (TVar 1, TVar 2))
        Expect.equal result (TFun (TInt, TBool)) "should substitute in function type"
    }

    test "apply is transitive" {
        let s = Map.ofList [(1, TVar 2); (2, TInt)]
        let result = TypeHelpers.apply s (TVar 1)
        Expect.equal result TInt "should apply transitively"
    }

    test "compose with empty is identity" {
        let s = Map.ofList [(1, TInt)]
        let r1 = TypeHelpers.compose s Map.empty
        let r2 = TypeHelpers.compose Map.empty s
        Expect.equal r1 s "compose with empty on right"
        Expect.equal r2 s "compose with empty on left"
    }

    test "freeTypeVars of ground type is empty" {
        Expect.equal (TypeHelpers.freeTypeVars TInt) Set.empty "TInt"
        Expect.equal (TypeHelpers.freeTypeVars TBool) Set.empty "TBool"
        Expect.equal (TypeHelpers.freeTypeVars TString) Set.empty "TString"
        Expect.equal (TypeHelpers.freeTypeVars TUnit) Set.empty "TUnit"
    }
]

let typeHelperTests2 = testList "Type Helpers 2" [
    test "freeTypeVars of type variable" {
        let result = TypeHelpers.freeTypeVars (TVar 1)
        Expect.equal result (Set.singleton 1) "should contain the variable"
    }

    test "freeTypeVars of function type" {
        let result = TypeHelpers.freeTypeVars (TFun (TVar 1, TVar 2))
        Expect.equal result (Set.ofList [1; 2]) "should contain both variables"
    }

    test "instantiate creates fresh variables" {
        TypeHelpers.resetCounter ()
        let scheme = Forall ([1], TFun (TVar 1, TVar 1))
        let t1 = TypeHelpers.instantiate scheme
        let t2 = TypeHelpers.instantiate scheme
        Expect.notEqual t1 t2 "should create different instances"
    }

    test "instantiate without quantified vars returns same type" {
        let scheme = Forall ([], TFun (TInt, TBool))
        let result = TypeHelpers.instantiate scheme
        Expect.equal result (TFun (TInt, TBool)) "should return same type"
    }

    test "generalize captures free variables" {
        let env = Map.empty
        let t = TFun (TVar 1, TVar 1)
        let scheme = TypeHelpers.generalize env t
        match scheme with
        | Forall (vars, _) ->
            Expect.contains vars 1 "should quantify free variable"
    }

    test "generalize does not capture env variables" {
        let env = Map.ofList [("x", Forall ([], TVar 1))]
        let t = TFun (TVar 1, TVar 2)
        let scheme = TypeHelpers.generalize env t
        match scheme with
        | Forall (vars, _) ->
            Expect.isFalse (List.contains 1 vars) "should not quantify env variable"
            Expect.contains vars 2 "should quantify non-env variable"
    }
]

// =============================================================================
// Unification Tests
// =============================================================================

let unificationTests = testList "Unification" [

    test "unify same types" {
        Expect.isOk (unify TInt TInt) "int = int"
        Expect.isOk (unify TBool TBool) "bool = bool"
        Expect.isOk (unify TString TString) "string = string"
        Expect.isOk (unify TUnit TUnit) "unit = unit"
    }

    test "unify type variable with type" {
        let result = unify (TVar 1) TInt
        let s = expectOk "unify TVar with TInt" result
        Expect.equal (Map.find 1 s) TInt "should bind to int"
    }

    test "unify type with type variable" {
        let result = unify TBool (TVar 2)
        let s = expectOk "unify TBool with TVar" result
        Expect.equal (Map.find 2 s) TBool "should bind to bool"
    }

    test "unify same type variable" {
        let result = unify (TVar 1) (TVar 1)
        let s = expectOk "unify same TVar" result
        Expect.isTrue (Map.isEmpty s) "should return empty substitution"
    }

    test "unify different type variables" {
        let result = unify (TVar 1) (TVar 2)
        let s = expectOk "unify different TVars" result
        Expect.isFalse (Map.isEmpty s) "should return non-empty substitution"
    }

    test "unify function types" {
        let t1 = TFun (TVar 1, TVar 2)
        let t2 = TFun (TInt, TBool)
        let result = unify t1 t2
        let s = expectOk "unify function types" result
        Expect.equal (TypeHelpers.apply s (TVar 1)) TInt "arg type"
        Expect.equal (TypeHelpers.apply s (TVar 2)) TBool "return type"
    }

    test "unify list types" {
        let result = unify (TList (TVar 1)) (TList TInt)
        let s = expectOk "unify list types" result
        Expect.equal (TypeHelpers.apply s (TVar 1)) TInt "element type"
    }

    test "unify tuple types" {
        let result = unify (TTuple [TVar 1; TVar 2]) (TTuple [TInt; TBool])
        let s = expectOk "unify tuple types" result
        Expect.equal (TypeHelpers.apply s (TVar 1)) TInt "first element"
        Expect.equal (TypeHelpers.apply s (TVar 2)) TBool "second element"
    }

    test "occurs check fails" {
        let result = unify (TVar 1) (TList (TVar 1))
        Expect.isError result "should fail occurs check"
    }

    test "type mismatch fails" {
        let result = unify TInt TBool
        Expect.isError result "int != bool"
    }

    test "function vs non-function fails" {
        let result = unify (TFun (TInt, TInt)) TInt
        Expect.isError result "function != int"
    }

    test "tuple arity mismatch fails" {
        let result = unify (TTuple [TInt; TBool]) (TTuple [TInt])
        Expect.isError result "different tuple arities"
    }
]

// =============================================================================
// Type Inference Tests - Literals
// =============================================================================

let literalInferenceTests = testList "Literal Inference" [

    test "infer integer literal" {
        TypeHelpers.resetCounter ()
        let result = inferExpr (ELiteral (LInt 42))
        let t = expectOk "infer int" result
        Expect.equal t TInt "should be int"
    }

    test "infer boolean literal" {
        TypeHelpers.resetCounter ()
        let result = inferExpr (ELiteral (LBool true))
        let t = expectOk "infer bool" result
        Expect.equal t TBool "should be bool"
    }

    test "infer string literal" {
        TypeHelpers.resetCounter ()
        let result = inferExpr (ELiteral (LString "hello"))
        let t = expectOk "infer string" result
        Expect.equal t TString "should be string"
    }

    test "infer unit literal" {
        TypeHelpers.resetCounter ()
        let result = inferExpr (ELiteral LUnit)
        let t = expectOk "infer unit" result
        Expect.equal t TUnit "should be unit"
    }
]

// =============================================================================
// Type Inference Tests - Lambda & Application
// =============================================================================

let lambdaInferenceTests = testList "Lambda Inference" [

    test "infer identity function" {
        TypeHelpers.resetCounter ()
        let expr = elambda "x" (EVariable "x")
        let result = inferExpr expr
        let t = expectOk "infer identity" result
        match t with
        | TFun (TVar a, TVar b) when a = b -> ()
        | _ -> failtest (sprintf "expected a -> a, got %A" t)
    }

    test "infer constant function" {
        TypeHelpers.resetCounter ()
        let expr = elambda "x" (ELiteral (LInt 42))
        let result = inferExpr expr
        let t = expectOk "infer const" result
        match t with
        | TFun (_, TInt) -> ()
        | _ -> failtest (sprintf "expected a -> int, got %A" t)
    }

    test "infer application" {
        TypeHelpers.resetCounter ()
        let expr = eapply (elambda "x" (EVariable "x")) (ELiteral (LInt 1))
        let result = inferExpr expr
        let t = expectOk "infer app" result
        Expect.equal t TInt "should be int"
    }

    test "infer curried function" {
        TypeHelpers.resetCounter ()
        let expr = elambda "x" (elambda "y" (EVariable "x"))
        let result = inferExpr expr
        let t = expectOk "infer curried" result
        match t with
        | TFun (TVar a, TFun (_, TVar b)) when a = b -> ()
        | _ -> failtest (sprintf "expected a -> b -> a, got %A" t)
    }
]

// =============================================================================
// Type Inference Tests - Let Binding
// =============================================================================

let letInferenceTests = testList "Let Inference" [

    test "infer simple let" {
        TypeHelpers.resetCounter ()
        let expr = elet "x" (ELiteral (LInt 42)) (EVariable "x")
        let result = inferExpr expr
        let t = expectOk "infer let" result
        Expect.equal t TInt "should be int"
    }

    test "infer let with function" {
        TypeHelpers.resetCounter ()
        let f = elambda "x" (EVariable "x")
        let body = eapply (EVariable "f") (ELiteral (LInt 1))
        let expr = elet "f" f body
        let result = inferExpr expr
        let t = expectOk "infer let func" result
        Expect.equal t TInt "should be int"
    }

    test "let-polymorphism: identity used at different types" {
        TypeHelpers.resetCounter ()
        // let id = fun x -> x in (id 1, id true)
        let id = elambda "x" (EVariable "x")
        let body = etuple [
            eapply (EVariable "id") (ELiteral (LInt 1))
            eapply (EVariable "id") (ELiteral (LBool true))
        ]
        let expr = elet "id" id body
        let result = inferExpr expr
        let t = expectOk "let-polymorphism" result
        match t with
        | TTuple [TInt; TBool] -> ()
        | _ -> failtest (sprintf "expected (int, bool), got %A" t)
    }
]

// =============================================================================
// Type Inference Tests - Let Rec
// =============================================================================

let letRecInferenceTests = testList "LetRec Inference" [

    ptest "infer recursive function (TODO: fix let rec unification)" {
        TypeHelpers.resetCounter ()
        // let rec fact = fun n -> if n = 0 then 1 else n * fact (n - 1) in fact
        let factBody =
            eif (binOp Eq (EVariable "n") (ELiteral (LInt 0)))
                (ELiteral (LInt 1))
                (binOp Mul (EVariable "n") (eapply (EVariable "fact") (binOp Sub (EVariable "n") (ELiteral (LInt 1)))))
        let expr = eletrec "fact" (elambda "n" factBody) (EVariable "fact")
        let result = inferExpr expr
        let t = expectOk "infer fact" result
        Expect.equal t (TFun (TInt, TInt)) "should be int -> int"
    }

    test "infer recursive list function" {
        TypeHelpers.resetCounter ()
        // let rec len = fun xs -> match xs with | [] -> 0 | _ :: rest -> 1 + len rest in len
        let matchBody = ematch (EVariable "xs") [
            (PList [], None, ELiteral (LInt 0))
            (pcons PWildcard (PVariable "rest"), None,
             binOp Add (ELiteral (LInt 1)) (eapply (EVariable "len") (EVariable "rest")))
        ]
        let expr = eletrec "len" (elambda "xs" matchBody) (EVariable "len")
        let result = inferExpr expr
        let t = expectOk "infer len" result
        match t with
        | TFun (TList _, TInt) -> ()
        | _ -> failtest (sprintf "expected list -> int, got %A" t)
    }
]

// =============================================================================
// Type Inference Tests - If
// =============================================================================

let ifInferenceTests = testList "If Inference" [

    test "infer if-then-else" {
        TypeHelpers.resetCounter ()
        let expr = eif (ELiteral (LBool true)) (ELiteral (LInt 1)) (ELiteral (LInt 2))
        let result = inferExpr expr
        let t = expectOk "infer if" result
        Expect.equal t TInt "should be int"
    }

    test "if branches must have same type" {
        TypeHelpers.resetCounter ()
        let expr = eif (ELiteral (LBool true)) (ELiteral (LInt 1)) (ELiteral (LBool false))
        let result = inferExpr expr
        Expect.isError result "branches have different types"
    }

    test "if condition must be bool" {
        TypeHelpers.resetCounter ()
        let expr = eif (ELiteral (LInt 1)) (ELiteral (LInt 2)) (ELiteral (LInt 3))
        let result = inferExpr expr
        Expect.isError result "condition not bool"
    }
]

// =============================================================================
// Type Inference Tests - Binary Operators
// =============================================================================

let binaryOpInferenceTests = testList "BinaryOp Inference" [

    test "infer addition" {
        TypeHelpers.resetCounter ()
        let expr = binOp Add (ELiteral (LInt 1)) (ELiteral (LInt 2))
        let result = inferExpr expr
        let t = expectOk "infer add" result
        Expect.equal t TInt "should be int"
    }

    test "infer comparison" {
        TypeHelpers.resetCounter ()
        let expr = binOp Lt (ELiteral (LInt 1)) (ELiteral (LInt 2))
        let result = inferExpr expr
        let t = expectOk "infer lt" result
        Expect.equal t TBool "should be bool"
    }

    test "infer equality (polymorphic)" {
        TypeHelpers.resetCounter ()
        let expr = binOp Eq (ELiteral (LInt 1)) (ELiteral (LInt 2))
        let result = inferExpr expr
        let t = expectOk "infer eq int" result
        Expect.equal t TBool "should be bool"

        TypeHelpers.resetCounter ()
        let expr2 = binOp Eq (ELiteral (LBool true)) (ELiteral (LBool false))
        let result2 = inferExpr expr2
        let t2 = expectOk "infer eq bool" result2
        Expect.equal t2 TBool "should be bool"
    }

    test "infer boolean operators" {
        TypeHelpers.resetCounter ()
        let expr = binOp And (ELiteral (LBool true)) (ELiteral (LBool false))
        let result = inferExpr expr
        let t = expectOk "infer and" result
        Expect.equal t TBool "should be bool"
    }

    test "type error: int + bool" {
        TypeHelpers.resetCounter ()
        let expr = binOp Add (ELiteral (LInt 1)) (ELiteral (LBool true))
        let result = inferExpr expr
        Expect.isError result "should fail"
    }
]

// =============================================================================
// Type Inference Tests - Lists & Tuples
// =============================================================================

let dataStructureInferenceTests = testList "DataStructure Inference" [

    test "infer empty list" {
        TypeHelpers.resetCounter ()
        let expr = EList []
        let result = inferExpr expr
        let t = expectOk "infer []" result
        match t with
        | TList _ -> ()
        | _ -> failtest (sprintf "expected list, got %A" t)
    }

    test "infer integer list" {
        TypeHelpers.resetCounter ()
        let expr = elist [ELiteral (LInt 1); ELiteral (LInt 2); ELiteral (LInt 3)]
        let result = inferExpr expr
        let t = expectOk "infer [1;2;3]" result
        Expect.equal t (TList TInt) "should be int list"
    }

    test "infer cons" {
        TypeHelpers.resetCounter ()
        let expr = econs (ELiteral (LInt 1)) (EList [])
        let result = inferExpr expr
        let t = expectOk "infer 1 :: []" result
        Expect.equal t (TList TInt) "should be int list"
    }

    test "infer tuple" {
        TypeHelpers.resetCounter ()
        let expr = etuple [ELiteral (LInt 1); ELiteral (LBool true); ELiteral (LString "hello")]
        let result = inferExpr expr
        let t = expectOk "infer tuple" result
        Expect.equal t (TTuple [TInt; TBool; TString]) "should be (int, bool, string)"
    }

    test "type error: heterogeneous list" {
        TypeHelpers.resetCounter ()
        let expr = elist [ELiteral (LInt 1); ELiteral (LBool true)]
        let result = inferExpr expr
        Expect.isError result "list elements must have same type"
    }
]

// =============================================================================
// Type Inference Tests - Pattern Matching
// =============================================================================

let patternMatchingInferenceTests = testList "PatternMatching Inference" [

    test "infer match with wildcard" {
        TypeHelpers.resetCounter ()
        let expr = ematch (ELiteral (LInt 1)) [
            (PWildcard, None, ELiteral (LInt 42))
        ]
        let result = inferExpr expr
        let t = expectOk "infer match wildcard" result
        Expect.equal t TInt "should be int"
    }

    test "infer match with variable" {
        TypeHelpers.resetCounter ()
        let expr = ematch (ELiteral (LInt 1)) [
            (PVariable "x", None, binOp Add (EVariable "x") (ELiteral (LInt 1)))
        ]
        let result = inferExpr expr
        let t = expectOk "infer match var" result
        Expect.equal t TInt "should be int"
    }

    test "infer match with list patterns" {
        TypeHelpers.resetCounter ()
        let expr = ematch (elist [ELiteral (LInt 1)]) [
            (PList [], None, ELiteral (LInt 0))
            (pcons (PVariable "x") PWildcard, None, EVariable "x")
        ]
        let result = inferExpr expr
        let t = expectOk "infer match list" result
        Expect.equal t TInt "should be int"
    }

    test "infer match with guard" {
        TypeHelpers.resetCounter ()
        let expr = ematch (ELiteral (LInt 1)) [
            (PVariable "x", Some (binOp Gt (EVariable "x") (ELiteral (LInt 0))), EVariable "x")
            (PWildcard, None, ELiteral (LInt 0))
        ]
        let result = inferExpr expr
        let t = expectOk "infer match guard" result
        Expect.equal t TInt "should be int"
    }

    test "match cases must have same type" {
        TypeHelpers.resetCounter ()
        let expr = ematch (ELiteral (LInt 1)) [
            (PLiteral (LInt 0), None, ELiteral (LInt 42))
            (PWildcard, None, ELiteral (LBool true))
        ]
        let result = inferExpr expr
        Expect.isError result "cases have different types"
    }
]

// =============================================================================
// Type Inference Tests - Unbound Variables
// =============================================================================

let errorTests = testList "Error Cases" [

    test "unbound variable" {
        TypeHelpers.resetCounter ()
        let result = inferExpr (EVariable "undefined")
        Expect.isError result "should fail for unbound variable"
    }

    test "not a function" {
        TypeHelpers.resetCounter ()
        let expr = eapply (ELiteral (LInt 42)) (ELiteral (LInt 1))
        let result = inferExpr expr
        Expect.isError result "cannot apply int"
    }
]

// =============================================================================
// Property-Based Tests
// =============================================================================

let propertyTests = ptestList "Properties (TODO: fix infinite loop)" [

    testProperty "type inference is deterministic" <| fun () ->
        TypeHelpers.resetCounter ()
        let expr = elambda "x" (EVariable "x")
        let t1 = inferExpr expr
        TypeHelpers.resetCounter ()
        let t2 = inferExpr expr
        t1 = t2

    testProperty "unification is symmetric for success/failure" <| fun () ->
        let t1 = TFun (TVar 1, TInt)
        let t2 = TFun (TBool, TVar 2)
        match unify t1 t2, unify t2 t1 with
        | Ok _, Ok _ -> true
        | Error _, Error _ -> true
        | _ -> false

    testProperty "applying empty substitution is identity" <| fun () ->
        let t = TFun (TVar 1, TList (TVar 2))
        TypeHelpers.apply Map.empty t = t

    testProperty "compose with empty substitution is identity" <| fun () ->
        let s = Map.ofList [(1, TInt); (2, TBool)]
        TypeHelpers.compose s Map.empty = s && TypeHelpers.compose Map.empty s = s
]

// =============================================================================
// All Type Tests
// =============================================================================

[<Tests>]
let allTypeTests = testList "Type System" [
    typeHelperTests
    unificationTests
    literalInferenceTests
    lambdaInferenceTests
    letInferenceTests
    letRecInferenceTests
    ifInferenceTests
    binaryOpInferenceTests
    dataStructureInferenceTests
    patternMatchingInferenceTests
    // typeInferencePropertyTests  // Enable after fixing property test generators
]
