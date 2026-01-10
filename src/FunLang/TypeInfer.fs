module FunLang.TypeInfer

open FunLang.Ast
open FunLang.Types
open FunLang.Unification
open FunLang.Errors

// =============================================================================
// Built-in Operator Types
// =============================================================================

/// Get the type signature of a binary operator
let builtinBinaryOpType (op: BinaryOp) : Type * Type * Type =
    match op with
    | Add | Sub | Mul | Div | Mod -> (TInt, TInt, TInt)
    | Lt | Gt | Lte | Gte -> (TInt, TInt, TBool)
    | Eq | Neq ->
        let α = TypeHelpers.freshTypeVar ()
        (α, α, TBool)
    | And | Or -> (TBool, TBool, TBool)

/// Get the type signature of a unary operator
let builtinUnaryOpType (op: UnaryOp) : Type * Type =
    match op with
    | Neg -> (TInt, TInt)
    | Not -> (TBool, TBool)

// =============================================================================
// Algorithm W - Core Type Inference
// =============================================================================

/// Constructor type environment (for user-defined types)
/// Maps constructor names to their type schemes
/// Uses ThreadLocal for parallel test safety
let private ctorEnv = new System.Threading.ThreadLocal<TypeEnv>(fun () -> Map.empty)

/// Set the constructor type environment
let setConstructorEnv (env: TypeEnv) = ctorEnv.Value <- env

/// Get constructor type from environment
let private lookupConstructor (name: string) : TypeScheme option =
    Map.tryFind name ctorEnv.Value

/// Infer the type of an expression
/// Returns (Substitution, Type) on success
let rec infer (env: TypeEnv) (expr: Expr) : InferResult =
    match expr with
    // -------------------------------------------------------------------------
    // Literals
    // -------------------------------------------------------------------------
    | ELiteral lit ->
        let t =
            match lit with
            | LInt _ -> TInt
            | LBool _ -> TBool
            | LString _ -> TString
            | LUnit -> TUnit
        Ok (Map.empty, t)

    // -------------------------------------------------------------------------
    // Variable
    // -------------------------------------------------------------------------
    | EVariable name ->
        match Map.tryFind name env with
        | Some scheme ->
            let t = TypeHelpers.instantiate scheme
            Ok (Map.empty, t)
        | None ->
            Error (TypeError.unboundVar name None)

    // -------------------------------------------------------------------------
    // Lambda: fun x -> e
    // -------------------------------------------------------------------------
    | ELambda (param, body) ->
        let α = TypeHelpers.freshTypeVar ()
        let env' = Map.add param (Forall ([], α)) env
        result {
            let! (s, τ) = infer env' body
            return (s, TFun (TypeHelpers.apply s α, τ))
        }

    // -------------------------------------------------------------------------
    // Application: e1 e2
    // -------------------------------------------------------------------------
    | EApply (e1, e2) ->
        result {
            let! (s1, τ1) = infer env e1
            let! (s2, τ2) = infer (TypeHelpers.applyEnv s1 env) e2
            let α = TypeHelpers.freshTypeVar ()
            let! s3 = unify (TypeHelpers.apply s2 τ1) (TFun (τ2, α))
            let finalSubst = TypeHelpers.compose s3 (TypeHelpers.compose s2 s1)
            return (finalSubst, TypeHelpers.apply s3 α)
        }

    // -------------------------------------------------------------------------
    // Let: let x = e1 in e2
    // -------------------------------------------------------------------------
    | ELet (name, e1, e2) ->
        result {
            let! (s1, τ1) = infer env e1
            let env' = TypeHelpers.applyEnv s1 env
            let σ = TypeHelpers.generalize env' τ1
            let! (s2, τ2) = infer (Map.add name σ env') e2
            return (TypeHelpers.compose s2 s1, τ2)
        }

    // -------------------------------------------------------------------------
    // Let Rec: let rec f = e1 in e2
    // -------------------------------------------------------------------------
    | ELetRec (name, e1, e2) ->
        result {
            let α = TypeHelpers.freshTypeVar ()
            let env' = Map.add name (Forall ([], α)) env
            let! (s1, τ1) = infer env' e1
            let! s2 = unify (TypeHelpers.apply s1 α) τ1
            let s = TypeHelpers.compose s2 s1
            let env'' = TypeHelpers.applyEnv s env
            let σ = TypeHelpers.generalize env'' (TypeHelpers.apply s τ1)
            let! (s3, τ2) = infer (Map.add name σ env'') e2
            return (TypeHelpers.compose s3 s, τ2)
        }

    // -------------------------------------------------------------------------
    // If-then-else
    // -------------------------------------------------------------------------
    | EIf (cond, thenE, elseE) ->
        result {
            let! (s1, τ1) = infer env cond
            let! s2 = unify τ1 TBool
            let s12 = TypeHelpers.compose s2 s1
            let! (s3, τ2) = infer (TypeHelpers.applyEnv s12 env) thenE
            let s123 = TypeHelpers.compose s3 s12
            let! (s4, τ3) = infer (TypeHelpers.applyEnv s123 env) elseE
            let! s5 = unify (TypeHelpers.apply s4 τ2) τ3
            let finalSubst = TypeHelpers.compose s5 (TypeHelpers.compose s4 s123)
            return (finalSubst, TypeHelpers.apply s5 τ3)
        }

    // -------------------------------------------------------------------------
    // Binary Operator
    // -------------------------------------------------------------------------
    | EBinaryOp (op, e1, e2) ->
        let (argT1, argT2, resultT) = builtinBinaryOpType op
        result {
            let! (s1, τ1) = infer env e1
            let! s2 = unify τ1 argT1
            let s12 = TypeHelpers.compose s2 s1
            let! (s3, τ2) = infer (TypeHelpers.applyEnv s12 env) e2
            let! s4 = unify τ2 (TypeHelpers.apply s3 argT2)
            let finalSubst = TypeHelpers.compose s4 (TypeHelpers.compose s3 s12)
            return (finalSubst, TypeHelpers.apply finalSubst resultT)
        }

    // -------------------------------------------------------------------------
    // Unary Operator
    // -------------------------------------------------------------------------
    | EUnaryOp (op, e) ->
        let (argT, resultT) = builtinUnaryOpType op
        result {
            let! (s1, τ1) = infer env e
            let! s2 = unify τ1 argT
            let finalSubst = TypeHelpers.compose s2 s1
            return (finalSubst, resultT)
        }

    // -------------------------------------------------------------------------
    // Tuple: (e1, e2, ...)
    // -------------------------------------------------------------------------
    | ETuple exprs ->
        result {
            let! (s, ts) = inferList env exprs
            return (s, TTuple ts)
        }

    // -------------------------------------------------------------------------
    // List: []
    // -------------------------------------------------------------------------
    | EList [] ->
        let α = TypeHelpers.freshTypeVar ()
        Ok (Map.empty, TList α)

    // -------------------------------------------------------------------------
    // List: [e1; e2; ...]
    // -------------------------------------------------------------------------
    | EList (e :: es) ->
        result {
            let! (s1, τ1) = infer env e
            let! (s2, ts) = inferList (TypeHelpers.applyEnv s1 env) es
            let allTypes = TypeHelpers.apply s2 τ1 :: ts
            let! (s3, elemType) = unifyAll allTypes
            let finalSubst = TypeHelpers.compose s3 (TypeHelpers.compose s2 s1)
            return (finalSubst, TList (TypeHelpers.apply s3 elemType))
        }

    // -------------------------------------------------------------------------
    // Cons: e1 :: e2
    // -------------------------------------------------------------------------
    | ECons (head, tail) ->
        result {
            let! (s1, τ1) = infer env head
            let! (s2, τ2) = infer (TypeHelpers.applyEnv s1 env) tail
            let! s3 = unify τ2 (TList (TypeHelpers.apply s2 τ1))
            let finalSubst = TypeHelpers.compose s3 (TypeHelpers.compose s2 s1)
            return (finalSubst, TypeHelpers.apply s3 τ2)
        }

    // -------------------------------------------------------------------------
    // Block: indentation-based sequence of expressions
    // -------------------------------------------------------------------------
    | EBlock exprs ->
        inferBlock env exprs

    // -------------------------------------------------------------------------
    // Match: pattern matching
    // -------------------------------------------------------------------------
    | EMatch (scrutinee, cases) ->
        inferMatch env scrutinee cases

    // -------------------------------------------------------------------------
    // Constructor: user-defined type constructor
    // -------------------------------------------------------------------------
    | EConstructor (name, argOpt) ->
        match lookupConstructor name with
        | None ->
            // Unknown constructor - return error
            Error (TypeError.unboundVar name None)
        | Some scheme ->
            // Instantiate the constructor's type scheme
            let ctorType = TypeHelpers.instantiate scheme
            match argOpt, ctorType with
            // Constructor expects argument but none provided
            | None, TFun _ ->
                Error (TypeError.arityMismatch 1 0 None)
            // Nullary constructor: True : Bool, None : Option 'a
            | None, resultType ->
                Ok (Map.empty, resultType)
            // Unary constructor: Some : 'a -> Option 'a
            | Some arg, TFun (argType, resultType) ->
                result {
                    let! (s1, τArg) = infer env arg
                    let! s2 = unify τArg (TypeHelpers.apply s1 argType)
                    let finalSubst = TypeHelpers.compose s2 s1
                    return (finalSubst, TypeHelpers.apply finalSubst resultType)
                }
            // Constructor doesn't expect argument but one provided
            | Some _, _ ->
                Error (TypeError.arityMismatch 0 1 None)

/// Infer types for a list of expressions, threading substitutions
and inferList (env: TypeEnv) (exprs: Expr list) : TypeResult<Substitution * Type list> =
    match exprs with
    | [] -> Ok (Map.empty, [])
    | e :: rest ->
        result {
            let! (s1, τ1) = infer env e
            let! (s2, ts) = inferList (TypeHelpers.applyEnv s1 env) rest
            let finalSubst = TypeHelpers.compose s2 s1
            return (finalSubst, TypeHelpers.apply s2 τ1 :: ts)
        }

/// Infer type of a block (returns type of last expression)
and inferBlock (env: TypeEnv) (exprs: Expr list) : InferResult =
    match exprs with
    | [] -> Ok (Map.empty, TUnit)
    | [e] -> infer env e
    | e :: rest ->
        result {
            let! (s1, _) = infer env e
            let! (s2, τ2) = inferBlock (TypeHelpers.applyEnv s1 env) rest
            return (TypeHelpers.compose s2 s1, τ2)
        }

// =============================================================================
// Pattern Type Inference
// =============================================================================

/// Infer the type of a pattern and return bindings
/// Returns (bindings, type) where bindings maps pattern variables to types
and inferPattern (pattern: Pattern) : TypeResult<Map<string, Type> * Type> =
    match pattern with
    | PWildcard ->
        let α = TypeHelpers.freshTypeVar ()
        Ok (Map.empty, α)

    | PVariable name ->
        let α = TypeHelpers.freshTypeVar ()
        Ok (Map.ofList [(name, α)], α)

    | PLiteral lit ->
        let t =
            match lit with
            | LInt _ -> TInt
            | LBool _ -> TBool
            | LString _ -> TString
            | LUnit -> TUnit
        Ok (Map.empty, t)

    | PTuple patterns ->
        result {
            let! results = patterns |> List.map inferPattern |> Result.sequence
            let (bindings, types) = results |> List.unzip
            let mergedBindings = bindings |> List.fold (fun acc b -> Map.fold (fun a k v -> Map.add k v a) acc b) Map.empty
            return (mergedBindings, TTuple types)
        }

    | PList [] ->
        let α = TypeHelpers.freshTypeVar ()
        Ok (Map.empty, TList α)

    | PList patterns ->
        result {
            let! results = patterns |> List.map inferPattern |> Result.sequence
            let (bindings, types) = results |> List.unzip
            let mergedBindings = bindings |> List.fold (fun acc b -> Map.fold (fun a k v -> Map.add k v a) acc b) Map.empty
            let! (s, elemType) = unifyAll types
            return (Map.map (fun _ t -> TypeHelpers.apply s t) mergedBindings, TList elemType)
        }

    | PCons (headP, tailP) ->
        result {
            let! (b1, τ1) = inferPattern headP
            let! (b2, τ2) = inferPattern tailP
            let! s = unify τ2 (TList τ1)
            let mergedBindings =
                Map.fold (fun acc k v -> Map.add k v acc) b1 b2
                |> Map.map (fun _ t -> TypeHelpers.apply s t)
            return (mergedBindings, TypeHelpers.apply s τ2)
        }

    | PConstructor (name, argPatOpt) ->
        match lookupConstructor name with
        | None ->
            // Unknown constructor - return error
            Error (TypeError.unboundVar name None)
        | Some scheme ->
            // Instantiate the constructor's type scheme
            let ctorType = TypeHelpers.instantiate scheme
            match argPatOpt, ctorType with
            // Constructor expects argument but none provided in pattern
            | None, TFun _ ->
                Error (TypeError.arityMismatch 1 0 None)
            // Nullary constructor pattern: None, True, etc.
            | None, resultType ->
                Ok (Map.empty, resultType)
            // Unary constructor pattern: Some x, Cons h t, etc.
            | Some argPat, TFun (argType, resultType) ->
                result {
                    let! (innerBindings, τInner) = inferPattern argPat
                    let! s = unify τInner argType
                    let bindings = Map.map (fun _ t -> TypeHelpers.apply s t) innerBindings
                    return (bindings, TypeHelpers.apply s resultType)
                }
            // Constructor doesn't expect argument but pattern has one
            | Some _, _ ->
                Error (TypeError.arityMismatch 0 1 None)

/// Infer type of match expression
and inferMatch (env: TypeEnv) (scrutinee: Expr) (cases: (Pattern * Expr option * Expr) list) : InferResult =
    match cases with
    | [] ->
        // Empty match - return fresh type variable
        result {
            let! (s, _) = infer env scrutinee
            let α = TypeHelpers.freshTypeVar ()
            return (s, α)
        }
    | _ ->
        result {
            // Infer scrutinee type
            let! (s0, τScrutinee) = infer env scrutinee

            // Process cases sequentially, threading substitutions through
            // This ensures pattern types from different cases are properly unified
            let processCase (accSubst: Substitution, bodyTypes: Type list) (pattern, guard, body) =
                result {
                    // Infer pattern type and get bindings
                    let! (patternBindings, τPattern) = inferPattern pattern

                    // Unify scrutinee type (with accumulated substitution) with pattern type
                    let! s1 = unify (TypeHelpers.apply accSubst τScrutinee) τPattern
                    let accSubst' = TypeHelpers.compose s1 accSubst

                    // Create environment with pattern bindings
                    let patternEnv =
                        patternBindings
                        |> Map.map (fun _ t -> Forall ([], TypeHelpers.apply accSubst' t))
                    let env' =
                        Map.fold (fun acc k v -> Map.add k v acc)
                            (TypeHelpers.applyEnv accSubst' env)
                            patternEnv

                    // Check guard if present (must be bool)
                    let! s2 =
                        match guard with
                        | Some guardExpr ->
                            result {
                                let! (sg, τGuard) = infer env' guardExpr
                                let! su = unify τGuard TBool
                                return TypeHelpers.compose su sg
                            }
                        | None -> Ok Map.empty

                    let accSubst'' = TypeHelpers.compose s2 accSubst'
                    let env'' = TypeHelpers.applyEnv s2 env'

                    // Infer body type
                    let! (s3, τBody) = infer env'' body

                    let finalAccSubst = TypeHelpers.compose s3 accSubst''
                    return (finalAccSubst, τBody :: bodyTypes)
                }

            // Fold through all cases, threading substitutions
            let! (finalSubst, reversedBodyTypes) =
                cases |> List.fold (fun accResult case ->
                    result {
                        let! acc = accResult
                        return! processCase acc case
                    }) (Ok (s0, []))

            let bodyTypes = List.rev reversedBodyTypes

            // Unify all case result types
            let! (sUnified, resultType) = unifyAll (List.map (TypeHelpers.apply finalSubst) bodyTypes)

            let totalSubst = TypeHelpers.compose sUnified finalSubst
            return (totalSubst, TypeHelpers.apply sUnified resultType)
        }

// =============================================================================
// Public API
// =============================================================================

/// Infer the type of an expression (main entry point)
let inferType (expr: Expr) : TypeResult<Type> =
    TypeHelpers.resetCounter ()
    result {
        let! (subst, t) = infer Map.empty expr
        return TypeHelpers.apply subst t
    }

/// Infer type with a given environment
let inferTypeWithEnv (env: TypeEnv) (expr: Expr) : TypeResult<Type> =
    result {
        let! (subst, t) = infer env expr
        return TypeHelpers.apply subst t
    }

/// Infer type with a given type definition environment (for user-defined types)
/// The typeDefEnv maps constructor names to their type schemes
let inferTypeWithTypeDefEnv (typeDefEnv: TypeEnv) (expr: Expr) : TypeResult<Type> =
    TypeHelpers.resetCounter ()
    setConstructorEnv typeDefEnv
    result {
        let! (subst, t) = infer Map.empty expr
        return TypeHelpers.apply subst t
    }
