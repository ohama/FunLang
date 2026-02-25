module Bidir

open Ast
open Type
open Unify
open Elaborate
open Diagnostic
open Infer  // Reuse freshVar, instantiate, generalize

// ============================================================================
// Bidirectional Type Checking
// ============================================================================
// Core algorithm with synthesis (⇒) and checking (⇐) modes
// BIDIR-01: Check mode verifies expression has expected type
// BIDIR-02: Synth mode infers type from expression
// BIDIR-03: Literals, variables, applications synthesize
// BIDIR-04: Lambdas check against arrow types
// BIDIR-05: Unannotated lambdas use fresh type variables (hybrid approach)
// BIDIR-06: Subsumption bridges synthesis to checking via unification
// BIDIR-07: Let-polymorphism preserved with generalize at let boundaries
// ============================================================================

/// Synthesize type for expression (inference mode)
/// Returns: (substitution, inferred type)
let rec synth (ctx: InferContext list) (env: TypeEnv) (expr: Expr): Subst * Type =
    match expr with
    // === Literals (BIDIR-03) ===
    | Number (_, _) -> (empty, TInt)
    | Bool (_, _) -> (empty, TBool)
    | String (_, _) -> (empty, TString)

    // === Variables (BIDIR-03) ===
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

    // === Application (BIDIR-03) ===
    | App (func, arg, span) ->
        let s1, funcTy = synth (InAppFun span :: ctx) env func
        let s2, argTy = synth (InAppArg span :: ctx) (applyEnv s1 env) arg
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

    // === Lambda (unannotated) (BIDIR-05 - HYBRID approach) ===
    | Lambda (param, body, _) ->
        let paramTy = freshVar()
        let bodyEnv = Map.add param (Scheme ([], paramTy)) env
        let s, bodyTy = synth ctx bodyEnv body
        (s, TArrow (apply s paramTy, bodyTy))

    // === LambdaAnnot (annotated lambda) ===
    | LambdaAnnot (param, paramTyExpr, body, span) ->
        let paramTy = elaborateTypeExpr paramTyExpr
        let ctx' = InCheckMode (paramTy, "annotation", span) :: ctx
        let bodyEnv = Map.add param (Scheme ([], paramTy)) env
        let s, bodyTy = synth ctx' bodyEnv body
        (s, TArrow (apply s paramTy, bodyTy))

    // === Annot (type annotation) ===
    | Annot (e, tyExpr, span) ->
        let expectedTy = elaborateTypeExpr tyExpr
        let ctx' = InCheckMode (expectedTy, "annotation", span) :: ctx
        let s = check ctx' env e expectedTy
        (s, apply s expectedTy)

    // === Let (BIDIR-07 - let-polymorphism) ===
    | Let (name, value, body, span) ->
        let s1, valueTy = synth (InLetRhs (name, span) :: ctx) env value
        let env' = applyEnv s1 env
        let scheme = generalize env' (apply s1 valueTy)
        let bodyEnv = Map.add name scheme env'
        let s2, bodyTy = synth (InLetBody (name, span) :: ctx) bodyEnv body
        (compose s2 s1, bodyTy)

    // === LetRec ===
    | LetRec (name, param, body, expr, span) ->
        // Pre-bind function with fresh type for recursive calls
        let funcTy = freshVar()
        let paramTy = freshVar()
        let recEnv = Map.add name (Scheme ([], funcTy)) env
        let bodyEnv = Map.add param (Scheme ([], paramTy)) recEnv
        // Infer body type
        let s1, bodyTy = synth (InLetRecBody (name, span) :: ctx) bodyEnv body
        // Unify function type with inferred arrow
        let s2 = unifyWithContext ctx [] span (apply s1 funcTy) (TArrow (apply s1 paramTy, bodyTy))
        let s = compose s2 s1
        // Generalize and add to env for expression
        let env' = applyEnv s env
        let scheme = generalize env' (apply s funcTy)
        let exprEnv = Map.add name scheme env'
        let s3, exprTy = synth ctx exprEnv expr
        (compose s3 s, exprTy)

    // === If ===
    | If (cond, thenExpr, elseExpr, span) ->
        let s1, condTy = synth (InIfCond span :: ctx) env cond
        let s2, thenTy = synth (InIfThen span :: ctx) (applyEnv s1 env) thenExpr
        let s3, elseTy = synth (InIfElse span :: ctx) (applyEnv (compose s2 s1) env) elseExpr
        // Condition must be bool
        let s4 = unifyWithContext ctx [] span (apply (compose s3 (compose s2 s1)) condTy) TBool
        // Branches must have same type
        let s5 = unifyWithContext ctx [] span (apply s4 thenTy) (apply s4 elseTy)
        let finalSubst = compose s5 (compose s4 (compose s3 (compose s2 s1)))
        (finalSubst, apply s5 thenTy)

    // === Binary operators ===
    // Add supports both int and string (overloaded)
    | Add (e1, e2, span) ->
        let s1, t1 = synth ctx env e1
        let s2, t2 = synth ctx (applyEnv s1 env) e2
        let appliedT1 = apply s2 t1
        let appliedT2 = t2
        // Try to unify both operands - they must be the same type (int or string)
        let s3 = unifyWithContext ctx [] span appliedT1 appliedT2
        let resultTy = apply s3 appliedT1
        // Verify result is int or string
        match resultTy with
        | TInt | TString -> (compose s3 (compose s2 s1), resultTy)
        | TVar _ ->
            // Ambiguous - default to int (backward compatible)
            let s4 = unifyWithContext ctx [] span resultTy TInt
            (compose s4 (compose s3 (compose s2 s1)), TInt)
        | _ ->
            raise (TypeException {
                Kind = UnifyMismatch (TInt, resultTy)
                Span = span
                Term = Some expr
                ContextStack = ctx
                Trace = []
            })

    | Subtract (e1, e2, _) | Multiply (e1, e2, _) | Divide (e1, e2, _) ->
        let s = inferBinaryOp ctx env e1 e2 TInt TInt
        (s, TInt)

    | Negate (e, _) ->
        let s, t = synth ctx env e
        let s' = unifyWithContext ctx [] (spanOf e) (apply s t) TInt
        (compose s' s, TInt)

    // Equality supports any type (polymorphic equality)
    | Equal (e1, e2, span) | NotEqual (e1, e2, span) ->
        let s1, t1 = synth ctx env e1
        let s2, t2 = synth ctx (applyEnv s1 env) e2
        let s3 = unifyWithContext ctx [] span (apply s2 t1) t2
        (compose s3 (compose s2 s1), TBool)

    // Comparison operators only work on int
    | LessThan (e1, e2, _) | GreaterThan (e1, e2, _)
    | LessEqual (e1, e2, _) | GreaterEqual (e1, e2, _) ->
        let s = inferBinaryOp ctx env e1 e2 TInt TInt
        (s, TBool)

    | And (e1, e2, _) | Or (e1, e2, _) ->
        let s = inferBinaryOp ctx env e1 e2 TBool TBool
        (s, TBool)

    // === Tuple (BIDIR-03) ===
    | Tuple (exprs, span) ->
        let folder (s, tys, idx) e =
            let s', ty = synth (InTupleElement (idx, span) :: ctx) (applyEnv s env) e
            (compose s' s, ty :: tys, idx + 1)
        let finalS, revTys, _ = List.fold folder (empty, [], 0) exprs
        (finalS, TTuple (List.rev revTys))

    // === EmptyList ===
    | EmptyList _ ->
        let elemTy = freshVar()
        (empty, TList elemTy)

    // === List literal ===
    | List (exprs, span) ->
        match exprs with
        | [] ->
            let elemTy = freshVar()
            (empty, TList elemTy)
        | first :: rest ->
            let s1, elemTy = synth (InListElement (0, span) :: ctx) env first
            let folder (s, ty, idx) e =
                let s', eTy = synth (InListElement (idx, span) :: ctx) (applyEnv s env) e
                let s'' = unifyWithContext ctx [] span (apply s' ty) eTy
                (compose s'' (compose s' s), apply s'' eTy, idx + 1)
            let finalS, elemTy', _ = List.fold folder (s1, elemTy, 1) rest
            (finalS, TList elemTy')

    // === Cons ===
    | Cons (head, tail, span) ->
        let s1, headTy = synth (InConsHead span :: ctx) env head
        let s2, tailTy = synth (InConsTail span :: ctx) (applyEnv s1 env) tail
        let s3 = unifyWithContext ctx [] span tailTy (TList (apply s2 headTy))
        (compose s3 (compose s2 s1), apply s3 tailTy)

    // === Match expression ===
    | Match (scrutinee, clauses, span) ->
        let s1, scrutTy = synth (InMatch span :: ctx) env scrutinee
        let resultTy = freshVar()
        let folder (s, idx) (pat, expr) =
            let patEnv, patTy = inferPattern pat
            // Unify scrutinee with pattern type
            let s' = unifyWithContext ctx [] span (apply s scrutTy) patTy
            // Merge pattern env with current env
            let clauseEnv = Map.fold (fun acc k v -> Map.add k v acc)
                                     (applyEnv s' (applyEnv s env)) patEnv
            // Synth clause body
            let s'', exprTy = synth (InMatchClause (idx, span) :: ctx) clauseEnv expr
            // Unify with result type
            let s''' = unifyWithContext ctx [] span (apply s'' resultTy) exprTy
            (compose s''' (compose s'' (compose s' s)), idx + 1)
        let finalS, _ = List.fold folder (s1, 0) clauses
        (finalS, apply finalS resultTy)

    // === LetPat ===
    | LetPat (pat, value, body, span) ->
        let s1, valueTy = synth ctx env value
        let patEnv, patTy = inferPattern pat
        let s2 = unifyWithContext ctx [] span (apply s1 valueTy) patTy
        let s = compose s2 s1
        let env' = applyEnv s env
        let generalizedPatEnv =
            patEnv
            |> Map.map (fun _ (Scheme (_, ty)) ->
                let ty' = apply s ty
                generalize env' ty')
        let bodyEnv = Map.fold (fun acc k v -> Map.add k v acc) env' generalizedPatEnv
        let s3, bodyTy = synth ctx bodyEnv body
        (compose s3 s, bodyTy)

/// Check expression against expected type (checking mode)
/// Returns: substitution that makes expression have expected type
and check (ctx: InferContext list) (env: TypeEnv) (expr: Expr) (expected: Type): Subst =
    match expr with
    // === Lambda against TArrow (BIDIR-04) ===
    | Lambda (param, body, _) ->
        match expected with
        | TArrow (paramTy, resultTy) ->
            let bodyEnv = Map.add param (Scheme ([], paramTy)) env
            let s = check ctx bodyEnv body resultTy
            let s' = unifyWithContext ctx [] (spanOf expr) (apply s paramTy) paramTy
            compose s' s
        | _ ->
            // Not an arrow type - fall through to subsumption
            let s, actual = synth ctx env expr
            let s' = unifyWithContext ctx [] (spanOf expr) (apply s expected) actual
            compose s' s

    // === If against expected (BIDIR-04) ===
    | If (cond, thenExpr, elseExpr, span) ->
        let s1, condTy = synth (InIfCond span :: ctx) env cond
        let s2 = unifyWithContext ctx [] span (apply s1 condTy) TBool
        let s12 = compose s2 s1
        let s3 = check (InIfThen span :: ctx) (applyEnv s12 env) thenExpr (apply s12 expected)
        let s4 = check (InIfElse span :: ctx) (applyEnv (compose s3 s12) env) elseExpr (apply (compose s3 s12) expected)
        compose s4 (compose s3 s12)

    // === Fallback subsumption (BIDIR-06) ===
    | _ ->
        let s, actual = synth ctx env expr
        let s' = unifyWithContext ctx [] (spanOf expr) (apply s expected) actual
        compose s' s

/// Helper: infer binary operator
and inferBinaryOp ctx env e1 e2 leftTy rightTy =
    let s1, t1 = synth ctx env e1
    let s2, t2 = synth ctx (applyEnv s1 env) e2
    let s3 = unifyWithContext ctx [] (spanOf e1) (apply s2 t1) leftTy
    let s4 = unifyWithContext ctx [] (spanOf e2) (apply s3 t2) rightTy
    compose s4 (compose s3 (compose s2 s1))

/// Top-level entry: infer type for expression
let synthTop (env: TypeEnv) (expr: Expr): Type =
    let s, ty = synth [] env expr
    apply s ty
