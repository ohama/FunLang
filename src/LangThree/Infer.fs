module Infer

// ============================================================
// NOTE: The main entry points (infer, inferWithContext) are
// deprecated in favor of Bidir.synth/synthTop.
//
// This module provides essential helper functions used by Bidir:
// - freshVar: Generate fresh type variables
// - instantiate: Instantiate a polymorphic scheme
// - generalize: Generalize a type to a scheme (let-polymorphism)
// - inferPattern: Infer types for pattern matching
//
// DO NOT remove this module - Bidir depends on these functions.
// ============================================================

open Type
open Unify
open Diagnostic
open Elaborate

/// Generate fresh type variable (unique ID via mutable counter)
/// Start at 1000 to avoid collision with scheme bound variable indices
let freshVar =
    let counter = ref 1000
    fun () ->
        let n = !counter
        counter := n + 1
        TVar n

/// Instantiate scheme: replace bound vars with fresh type variables
/// Example: forall 'a. 'a -> 'a becomes 'c -> 'c (with fresh 'c)
let instantiate (Scheme (vars, ty)): Type =
    match vars with
    | [] -> ty  // Monomorphic - no substitution needed
    | _ ->
        let freshVars = List.map (fun _ -> freshVar()) vars
        let subst = List.zip vars freshVars |> Map.ofList
        apply subst ty

/// Generalize type: abstract over free vars not in environment
/// Creates polymorphic scheme at let boundaries
let generalize (env: TypeEnv) (ty: Type): Scheme =
    let envFree = freeVarsEnv env
    let tyFree = freeVars ty
    let vars = Set.difference tyFree envFree |> Set.toList
    Scheme (vars, ty)

open Ast

/// Infer pattern type and extract bindings (INFER-15)
/// Returns (environment with bindings, pattern type)
let rec inferPattern (pat: Pattern): TypeEnv * Type =
    match pat with
    | VarPat (name, _) ->
        let ty = freshVar()
        (Map.ofList [(name, Scheme ([], ty))], ty)

    | WildcardPat _ ->
        (Map.empty, freshVar())

    | TuplePat (pats, _) ->
        let envTys = List.map inferPattern pats
        let env = envTys
                  |> List.map fst
                  |> List.fold (fun acc m -> Map.fold (fun a k v -> Map.add k v a) acc m) Map.empty
        let tys = envTys |> List.map snd
        (env, TTuple tys)

    | EmptyListPat _ ->
        (Map.empty, TList (freshVar()))

    | ConsPat (headPat, tailPat, _) ->
        let headEnv, headTy = inferPattern headPat
        let tailEnv, tailTy = inferPattern tailPat
        // Note: tailTy should be TList headTy, but actual unification happens in Match
        // We return TList headTy as the pattern type
        let env = Map.fold (fun acc k v -> Map.add k v acc) headEnv tailEnv
        (env, TList headTy)

    | ConstPat (IntConst _, _) ->
        (Map.empty, TInt)

    | ConstPat (BoolConst _, _) ->
        (Map.empty, TBool)

/// <summary>
/// DEPRECATED: Use Bidir.synth instead.
/// Context-aware version of Algorithm W inference.
/// See: Bidir module for the current implementation.
/// </summary>
let rec inferWithContext (ctx: InferContext list) (env: TypeEnv) (expr: Expr): Subst * Type =
    match expr with
    // === Literals (INFER-04) ===
    | Number (_, _) -> (empty, TInt)
    | Bool (_, _) -> (empty, TBool)
    | String (_, _) -> (empty, TString)

    // === Variable reference (INFER-06) ===
    | Var (name, span) ->
        match Map.tryFind name env with
        | Some scheme -> (empty, instantiate scheme)
        | None ->
            raise (TypeException {
                Kind = UnboundVar name
                Span = span
                Term = Some expr
                ContextStack = ctx
                Trace = []
            })

    // === Arithmetic operators (INFER-05) ===
    // All arithmetic: int -> int -> int
    | Add (e1, e2, _) | Subtract (e1, e2, _) | Multiply (e1, e2, _) | Divide (e1, e2, _) ->
        inferBinaryOpWithContext ctx env e1 e2 TInt TInt TInt

    // Unary minus: int -> int
    | Negate (e, _) ->
        let s, t = inferWithContext ctx env e
        let s' = unifyWithContext ctx [] (spanOf e) (apply s t) TInt
        (compose s' s, TInt)

    // === Comparison operators (INFER-05) ===
    // Comparison: int -> int -> bool
    | Equal (e1, e2, _) | NotEqual (e1, e2, _)
    | LessThan (e1, e2, _) | GreaterThan (e1, e2, _)
    | LessEqual (e1, e2, _) | GreaterEqual (e1, e2, _) ->
        inferBinaryOpWithContext ctx env e1 e2 TInt TInt TBool

    // === Logical operators (INFER-05) ===
    // Logical: bool -> bool -> bool
    | And (e1, e2, _) | Or (e1, e2, _) ->
        inferBinaryOpWithContext ctx env e1 e2 TBool TBool TBool

    // === Lambda (INFER-08) ===
    | Lambda (param, body, _) ->
        let paramTy = freshVar()
        let bodyEnv = Map.add param (Scheme ([], paramTy)) env
        let s, bodyTy = inferWithContext ctx bodyEnv body
        (s, TArrow (apply s paramTy, bodyTy))

    // === LambdaAnnot (annotated lambda) ===
    | LambdaAnnot (param, paramTyExpr, body, _) ->
        let paramTy = elaborateTypeExpr paramTyExpr
        let bodyEnv = Map.add param (Scheme ([], paramTy)) env
        let s, bodyTy = inferWithContext ctx bodyEnv body
        (s, TArrow (apply s paramTy, bodyTy))

    // === Annot (type annotation) ===
    | Annot (e, tyExpr, span) ->
        let expectedTy = elaborateTypeExpr tyExpr
        let s, inferredTy = inferWithContext ctx env e
        let s' = unifyWithContext ctx [] span (apply s inferredTy) expectedTy
        (compose s' s, apply s' expectedTy)

    // === Application (INFER-08) ===
    | App (func, arg, span) ->
        let s1, funcTy = inferWithContext (InAppFun span :: ctx) env func
        let s2, argTy = inferWithContext (InAppArg span :: ctx) (applyEnv s1 env) arg
        let appliedFuncTy = apply s2 funcTy
        // Check if we're trying to apply a non-function type
        match appliedFuncTy with
        | TInt | TBool | TString | TTuple _ | TList _ ->
            raise (TypeException {
                Kind = NotAFunction appliedFuncTy
                Span = spanOf func
                Term = Some func
                ContextStack = ctx
                Trace = []
            })
        | _ ->
            let resultTy = freshVar()
            let s3 = unifyWithContext ctx [] span appliedFuncTy (TArrow (argTy, resultTy))
            (compose s3 (compose s2 s1), apply s3 resultTy)

    // === If expression (INFER-10) ===
    | If (cond, thenExpr, elseExpr, span) ->
        let s1, condTy = inferWithContext (InIfCond span :: ctx) env cond
        let s2, thenTy = inferWithContext (InIfThen span :: ctx) (applyEnv s1 env) thenExpr
        let s3, elseTy = inferWithContext (InIfElse span :: ctx) (applyEnv (compose s2 s1) env) elseExpr
        // Condition must be bool
        let s4 = unifyWithContext ctx [] span (apply (compose s3 (compose s2 s1)) condTy) TBool
        // Branches must have same type
        let s5 = unifyWithContext ctx [] span (apply s4 thenTy) (apply s4 elseTy)
        let finalSubst = compose s5 (compose s4 (compose s3 (compose s2 s1)))
        (finalSubst, apply s5 thenTy)

    // === Let with polymorphism (INFER-07) ===
    | Let (name, value, body, span) ->
        let s1, valueTy = inferWithContext (InLetRhs (name, span) :: ctx) env value
        let env' = applyEnv s1 env
        let scheme = generalize env' (apply s1 valueTy)
        let bodyEnv = Map.add name scheme env'
        let s2, bodyTy = inferWithContext (InLetBody (name, span) :: ctx) bodyEnv body
        (compose s2 s1, bodyTy)

    // === LetRec (INFER-09) ===
    | LetRec (name, param, body, expr, span) ->
        // Pre-bind function with fresh type for recursive calls
        let funcTy = freshVar()
        let paramTy = freshVar()
        let recEnv = Map.add name (Scheme ([], funcTy)) env
        let bodyEnv = Map.add param (Scheme ([], paramTy)) recEnv
        // Infer body type
        let s1, bodyTy = inferWithContext (InLetRecBody (name, span) :: ctx) bodyEnv body
        // Unify function type with inferred arrow
        let s2 = unifyWithContext ctx [] span (apply s1 funcTy) (TArrow (apply s1 paramTy, bodyTy))
        let s = compose s2 s1
        // Generalize and add to env for expression
        let env' = applyEnv s env
        let scheme = generalize env' (apply s funcTy)
        let exprEnv = Map.add name scheme env'
        let s3, exprTy = inferWithContext ctx exprEnv expr
        (compose s3 s, exprTy)

    // === Tuple (INFER-11) ===
    | Tuple (exprs, span) ->
        let folder (s, tys, idx) e =
            let s', ty = inferWithContext (InTupleElement (idx, span) :: ctx) (applyEnv s env) e
            (compose s' s, ty :: tys, idx + 1)
        let finalS, revTys, _ = List.fold folder (empty, [], 0) exprs
        (finalS, TTuple (List.rev revTys))

    // === EmptyList (INFER-12) ===
    | EmptyList _ ->
        let elemTy = freshVar()
        (empty, TList elemTy)

    // === List literal (INFER-12) ===
    | List (exprs, span) ->
        match exprs with
        | [] ->
            let elemTy = freshVar()
            (empty, TList elemTy)
        | first :: rest ->
            let s1, elemTy = inferWithContext (InListElement (0, span) :: ctx) env first
            let folder (s, ty, idx) e =
                let s', eTy = inferWithContext (InListElement (idx, span) :: ctx) (applyEnv s env) e
                let s'' = unifyWithContext ctx [] span (apply s' ty) eTy
                (compose s'' (compose s' s), apply s'' eTy, idx + 1)
            let finalS, elemTy', _ = List.fold folder (s1, elemTy, 1) rest
            (finalS, TList elemTy')

    // === Cons (INFER-12) ===
    | Cons (head, tail, span) ->
        let s1, headTy = inferWithContext (InConsHead span :: ctx) env head
        let s2, tailTy = inferWithContext (InConsTail span :: ctx) (applyEnv s1 env) tail
        let s3 = unifyWithContext ctx [] span tailTy (TList (apply s2 headTy))
        (compose s3 (compose s2 s1), apply s3 tailTy)

    // === Match expression (INFER-13) ===
    | Match (scrutinee, clauses, span) ->
        let s1, scrutTy = inferWithContext (InMatch span :: ctx) env scrutinee
        let resultTy = freshVar()
        let folder (s, idx) (pat, expr) =
            let patEnv, patTy = inferPattern pat
            // Unify scrutinee with pattern type
            let s' = unifyWithContext ctx [] span (apply s scrutTy) patTy
            // Merge pattern env with current env (after applying substitution)
            let clauseEnv = Map.fold (fun acc k v -> Map.add k v acc)
                                     (applyEnv s' (applyEnv s env)) patEnv
            // Infer clause body
            let s'', exprTy = inferWithContext (InMatchClause (idx, span) :: ctx) clauseEnv expr
            // Unify with result type
            let s''' = unifyWithContext ctx [] span (apply s'' resultTy) exprTy
            (compose s''' (compose s'' (compose s' s)), idx + 1)
        let finalS, _ = List.fold folder (s1, 0) clauses
        (finalS, apply finalS resultTy)

    // === LetPat (INFER-14) ===
    | LetPat (pat, value, body, span) ->
        // Infer value type
        let s1, valueTy = inferWithContext ctx env value
        // Get pattern bindings and type
        let patEnv, patTy = inferPattern pat
        // Unify value type with pattern type
        let s2 = unifyWithContext ctx [] span (apply s1 valueTy) patTy
        let s = compose s2 s1
        // Apply substitution and generalize each binding
        let env' = applyEnv s env
        let generalizedPatEnv =
            patEnv
            |> Map.map (fun _ (Scheme (_, ty)) ->
                let ty' = apply s ty
                generalize env' ty')
        // Merge into environment
        let bodyEnv = Map.fold (fun acc k v -> Map.add k v acc) env' generalizedPatEnv
        let s3, bodyTy = inferWithContext ctx bodyEnv body
        (compose s3 s, bodyTy)

/// Helper: infer binary operator with context tracking
and inferBinaryOpWithContext ctx env e1 e2 leftTy rightTy resultTy =
    let s1, t1 = inferWithContext ctx env e1
    let s2, t2 = inferWithContext ctx (applyEnv s1 env) e2
    let s3 = unifyWithContext ctx [] (spanOf e1) (apply s2 t1) leftTy
    let s4 = unifyWithContext ctx [] (spanOf e2) (apply s3 t2) rightTy
    (compose s4 (compose s3 (compose s2 s1)), resultTy)

/// <summary>
/// DEPRECATED: Use Bidir.synth instead for type inference.
/// This function implements Algorithm W and is kept for reference.
/// The bidirectional type checker (Bidir module) is now the primary
/// type inference mechanism, supporting type annotations.
/// </summary>
/// <remarks>
/// Helper functions (freshVar, instantiate, generalize, inferPattern)
/// from this module are still actively used by the Bidir module.
/// </remarks>
and infer (env: TypeEnv) (expr: Expr): Subst * Type =
    inferWithContext [] env expr
