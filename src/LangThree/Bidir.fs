module Bidir

open Ast
open Type
open Unify
open Elaborate
open Diagnostic
open Infer  // Reuse freshVar, instantiate, generalize

/// Mutable variables currently in scope (Phase 42)
let mutable mutableVars : Set<string> = Set.empty

// ============================================================================
// GADT Detection
// ============================================================================

/// Detect if a match involves GADT constructors
let isGadtMatch (ctorEnv: ConstructorEnv) (clauses: MatchClause list) : bool =
    clauses |> List.exists (fun (pat, _, _) ->
        match pat with
        | ConstructorPat(name, _, _) ->
            match Map.tryFind name ctorEnv with
            | Some info -> info.IsGadt
            | None -> false
        | _ -> false)

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
let rec synth (ctorEnv: ConstructorEnv) (recEnv: RecordEnv) (ctx: InferContext list) (env: TypeEnv) (expr: Expr): Subst * Type =
    match expr with
    // === Literals (BIDIR-03) ===
    | Number (_, _) -> (empty, TInt)
    | Bool (_, _) -> (empty, TBool)
    | String (_, _) -> (empty, TString)
    | Char (_, _) -> (empty, TChar)

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

    // === Constructor expressions (ADT) ===
    | Constructor (name, argOpt, span) ->
        // Phase 55: StringBuilder() type interception
        match name with
        | "StringBuilder" ->
            match argOpt with
            | Some argExpr ->
                let s, argTy = synth ctorEnv recEnv ctx env argExpr
                let s2 = unifyWithContext ctx [] span (apply s argTy) (TTuple [])
                (compose s2 s, TData("StringBuilder", []))
            | None ->
                (empty, TData("StringBuilder", []))
        | "HashSet" ->
            match argOpt with
            | Some argExpr ->
                let s, argTy = synth ctorEnv recEnv ctx env argExpr
                let s2 = unifyWithContext ctx [] span (apply s argTy) (TTuple [])
                (compose s2 s, TData("HashSet", []))
            | None ->
                (empty, TData("HashSet", []))
        | "Queue" ->
            match argOpt with
            | Some argExpr ->
                let s, argTy = synth ctorEnv recEnv ctx env argExpr
                let s2 = unifyWithContext ctx [] span (apply s argTy) (TTuple [])
                (compose s2 s, TData("Queue", []))
            | None ->
                (empty, TData("Queue", []))
        | _ ->
        match Map.tryFind name ctorEnv with
        | None ->
            // If not in ctorEnv, treat as unbound (may be used without type decl in eval-only tests)
            // Return a fresh TData-like type
            let resultTy = freshVar()
            match argOpt with
            | None -> (empty, resultTy)
            | Some argExpr ->
                let s, _argTy = synth ctorEnv recEnv ctx env argExpr
                (s, apply s resultTy)
        | Some ctorInfo ->
            // Instantiate type params with fresh variables
            let freshVars = ctorInfo.TypeParams |> List.map (fun _ -> freshVar())
            let subst = List.zip ctorInfo.TypeParams freshVars |> Map.ofList
            let resultType = apply subst ctorInfo.ResultType
            match (ctorInfo.ArgType, argOpt) with
            | (None, None) ->
                // Nullary constructor (e.g., None)
                (empty, resultType)
            | (Some argType, Some argExpr) ->
                // Constructor with argument (e.g., Some 42)
                let expectedArgTy = apply subst argType
                let s1, actualArgTy = synth ctorEnv recEnv ctx env argExpr
                let s2 = unifyWithContext ctx [] span expectedArgTy actualArgTy
                (compose s2 s1, apply (compose s2 s1) resultType)
            | (None, Some _) ->
                raise (TypeException {
                    Kind = ArityMismatch (name, 0, 1)
                    Span = span
                    Term = Some expr
                    ContextStack = ctx
                    Trace = []
                })
            | (Some _, None) ->
                raise (TypeException {
                    Kind = ArityMismatch (name, 1, 0)
                    Span = span
                    Term = Some expr
                    ContextStack = ctx
                    Trace = []
                })

    // === Application (BIDIR-03) ===
    | App (func, arg, span) ->
        let s1, funcTy = synth ctorEnv recEnv (InAppFun span :: ctx) env func
        let s2, argTy = synth ctorEnv recEnv (InAppArg span :: ctx) (applyEnv s1 env) arg
        let appliedFuncTy = apply s2 funcTy
        // Check if we're trying to apply a non-function type
        match appliedFuncTy with
        | TInt | TBool | TString | TTuple _ | TList _ | TArray _ | THashtable _ | TData _ ->
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
        let s, bodyTy = synth ctorEnv recEnv ctx bodyEnv body
        (s, TArrow (apply s paramTy, bodyTy))

    // === LambdaAnnot (annotated lambda) ===
    | LambdaAnnot (param, paramTyExpr, body, span) ->
        let paramTy = elaborateTypeExpr paramTyExpr
        let ctx' = InCheckMode (paramTy, "annotation", span) :: ctx
        let bodyEnv = Map.add param (Scheme ([], paramTy)) env
        let s, bodyTy = synth ctorEnv recEnv ctx' bodyEnv body
        (s, TArrow (apply s paramTy, bodyTy))

    // === Annot (type annotation) ===
    | Annot (e, tyExpr, span) ->
        let expectedTy = elaborateTypeExpr tyExpr
        let ctx' = InCheckMode (expectedTy, "annotation", span) :: ctx
        let s = check ctorEnv recEnv ctx' env e expectedTy
        (s, apply s expectedTy)

    // === Let (BIDIR-07 - let-polymorphism) ===
    | Let (name, value, body, span) ->
        let s1, valueTy = synth ctorEnv recEnv (InLetRhs (name, span) :: ctx) env value
        let env' = applyEnv s1 env
        let scheme = generalize env' (apply s1 valueTy)
        let bodyEnv = Map.add name scheme env'
        let s2, bodyTy = synth ctorEnv recEnv (InLetBody (name, span) :: ctx) bodyEnv body
        (compose s2 s1, bodyTy)

    // === LetMut (Phase 42 - mutable variable, NO generalization) ===
    | LetMut (name, value, body, span) ->
        let s1, valueTy = synth ctorEnv recEnv (InLetRhs (name, span) :: ctx) env value
        let env' = applyEnv s1 env
        // NO generalization -- mutable variables must be monomorphic
        let scheme = Scheme([], apply s1 valueTy)
        let bodyEnv = Map.add name scheme env'
        let savedMutableVars = mutableVars
        mutableVars <- Set.add name mutableVars
        let s2, bodyTy = synth ctorEnv recEnv (InLetBody (name, span) :: ctx) bodyEnv body
        mutableVars <- savedMutableVars  // restore (name goes out of scope)
        (compose s2 s1, bodyTy)

    // === WhileExpr (Phase 46 - while loop) ===
    | WhileExpr (cond, body, span) ->
        let s1, condTy = synth ctorEnv recEnv ctx env cond
        let s2 = unifyWithContext ctx [] span (apply s1 condTy) TBool
        let s12 = compose s2 s1
        let env' = applyEnv s12 env
        let s3, _bodyTy = synth ctorEnv recEnv ctx env' body
        (compose s3 s12, TTuple [])  // while always returns unit

    // === ForExpr (Phase 46 - for loop) ===
    | ForExpr (var, startExpr, _isTo, stopExpr, body, span) ->
        let s1, startTy = synth ctorEnv recEnv ctx env startExpr
        let s2 = unifyWithContext ctx [] span (apply s1 startTy) TInt
        let s12 = compose s2 s1
        let env1 = applyEnv s12 env
        let s3, stopTy = synth ctorEnv recEnv ctx env1 stopExpr
        let s4 = unifyWithContext ctx [] span (apply s3 stopTy) TInt
        let s1234 = compose s4 (compose s3 s12)
        let env2 = applyEnv s1234 env
        // Bind loop variable as immutable int — NOT in mutableVars (LOOP-04)
        let loopEnv = Map.add var (Scheme([], TInt)) env2
        // Do NOT add var to mutableVars — loop variable must be immutable
        let s5, _bodyTy = synth ctorEnv recEnv ctx loopEnv body
        (compose s5 s1234, TTuple [])  // for always returns unit

    // === ForInExpr (Phase 51 - for-in collection loop) ===
    | ForInExpr (var, collExpr, body, span) ->
        let s1, collTy = synth ctorEnv recEnv ctx env collExpr
        // Try to unify collection type with TList(elemTv); if that fails, try TArray(elemTv)
        let elemTv = freshVar ()
        let s2 =
            try unifyWithContext ctx [] span (apply s1 collTy) (TList elemTv)
            with _ ->
                let elemTv2 = freshVar ()
                unifyWithContext ctx [] span (apply s1 collTy) (TArray elemTv2)
        let s12 = compose s2 s1
        let collTyResolved = apply s12 collTy
        let elemTy =
            match collTyResolved with
            | TList t -> t
            | TArray t -> t
            | _ -> freshVar ()
        let env2 = applyEnv s12 env
        // Bind loop variable as immutable — NOT in mutableVars (FORIN-03)
        let loopEnv = Map.add var (Scheme([], elemTy)) env2
        let s3, _bodyTy = synth ctorEnv recEnv ctx loopEnv body
        (compose s3 s12, TTuple [])  // for-in always returns unit

    // === Assign (Phase 42 - mutable variable assignment) ===
    | Assign (name, value, span) ->
        if not (Set.contains name mutableVars) then
            raise (TypeException {
                Kind = ImmutableVariableAssignment name
                Span = span
                Term = Some expr
                ContextStack = ctx
                Trace = []
            })
        match Map.tryFind name env with
        | Some scheme ->
            let varTy = instantiate scheme
            let s1, valTy = synth ctorEnv recEnv ctx env value
            let s2 = unifyWithContext ctx [] span (apply s1 varTy) valTy
            (compose s2 s1, TTuple [])  // returns unit
        | None ->
            raise (TypeException {
                Kind = UnboundVar name
                Span = span
                Term = Some expr
                ContextStack = ctx
                Trace = []
            })

    // === LetRec ===
    | LetRec (name, param, body, expr, span) ->
        // Pre-bind function with fresh type for recursive calls
        let funcTy = freshVar()
        let paramTy = freshVar()
        let recTypeEnv = Map.add name (Scheme ([], funcTy)) env
        let bodyEnv = Map.add param (Scheme ([], paramTy)) recTypeEnv
        // Infer body type
        let s1, bodyTy = synth ctorEnv recEnv (InLetRecBody (name, span) :: ctx) bodyEnv body
        // Unify function type with inferred arrow
        let s2 = unifyWithContext ctx [] span (apply s1 funcTy) (TArrow (apply s1 paramTy, bodyTy))
        let s = compose s2 s1
        // Generalize and add to env for expression
        let env' = applyEnv s env
        let scheme = generalize env' (apply s funcTy)
        let exprEnv = Map.add name scheme env'
        let s3, exprTy = synth ctorEnv recEnv ctx exprEnv expr
        (compose s3 s, exprTy)

    // === If ===
    | If (cond, thenExpr, elseExpr, span) ->
        let s1, condTy = synth ctorEnv recEnv (InIfCond span :: ctx) env cond
        let s2, thenTy = synth ctorEnv recEnv (InIfThen span :: ctx) (applyEnv s1 env) thenExpr
        let s3, elseTy = synth ctorEnv recEnv (InIfElse span :: ctx) (applyEnv (compose s2 s1) env) elseExpr
        // Condition must be bool
        let s4 = unifyWithContext ctx [] span (apply (compose s3 (compose s2 s1)) condTy) TBool
        // Branches must have same type
        let s5 = unifyWithContext ctx [] span (apply s4 thenTy) (apply s4 elseTy)
        let finalSubst = compose s5 (compose s4 (compose s3 (compose s2 s1)))
        (finalSubst, apply s5 thenTy)

    // === Binary operators ===
    // Add supports both int and string (overloaded)
    | Add (e1, e2, span) ->
        let s1, t1 = synth ctorEnv recEnv ctx env e1
        let s2, t2 = synth ctorEnv recEnv ctx (applyEnv s1 env) e2
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

    | Subtract (e1, e2, _) | Multiply (e1, e2, _) | Divide (e1, e2, _) | Modulo (e1, e2, _) ->
        let s = inferBinaryOp ctorEnv recEnv ctx env e1 e2 TInt TInt
        (s, TInt)

    | Negate (e, _) ->
        let s, t = synth ctorEnv recEnv ctx env e
        let s' = unifyWithContext ctx [] (spanOf e) (apply s t) TInt
        (compose s' s, TInt)

    // Equality supports any type (polymorphic equality)
    | Equal (e1, e2, span) | NotEqual (e1, e2, span) ->
        let s1, t1 = synth ctorEnv recEnv ctx env e1
        let s2, t2 = synth ctorEnv recEnv ctx (applyEnv s1 env) e2
        let s3 = unifyWithContext ctx [] span (apply s2 t1) t2
        (compose s3 (compose s2 s1), TBool)

    // Comparison operators work on int, string, or char (ordered types)
    | LessThan (e1, e2, span) | GreaterThan (e1, e2, span)
    | LessEqual (e1, e2, span) | GreaterEqual (e1, e2, span) ->
        let s1, t1 = synth ctorEnv recEnv ctx env e1
        let s2, t2 = synth ctorEnv recEnv ctx (applyEnv s1 env) e2
        let appliedT1 = apply s2 t1
        let s3 = unifyWithContext ctx [] span appliedT1 t2
        let resultTy = apply s3 appliedT1
        match resultTy with
        | TInt | TString | TChar -> (compose s3 (compose s2 s1), TBool)
        | TVar _ ->
            // Ambiguous - default to int (backward compatible)
            let s4 = unifyWithContext ctx [] span resultTy TInt
            (compose s4 (compose s3 (compose s2 s1)), TBool)
        | _ ->
            raise (TypeException {
                Kind = UnifyMismatch (TInt, resultTy)
                Span = span
                Term = Some expr
                ContextStack = ctx
                Trace = []
            })

    | And (e1, e2, _) | Or (e1, e2, _) ->
        let s = inferBinaryOp ctorEnv recEnv ctx env e1 e2 TBool TBool
        (s, TBool)

    // === Tuple (BIDIR-03) ===
    | Tuple (exprs, span) ->
        let folder (s, tys, idx) e =
            let s', ty = synth ctorEnv recEnv (InTupleElement (idx, span) :: ctx) (applyEnv s env) e
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
            let s1, elemTy = synth ctorEnv recEnv (InListElement (0, span) :: ctx) env first
            let folder (s, ty, idx) e =
                let s', eTy = synth ctorEnv recEnv (InListElement (idx, span) :: ctx) (applyEnv s env) e
                let s'' = unifyWithContext ctx [] span (apply s' ty) eTy
                (compose s'' (compose s' s), apply s'' eTy, idx + 1)
            let finalS, elemTy', _ = List.fold folder (s1, elemTy, 1) rest
            (finalS, TList elemTy')

    // === Cons ===
    | Cons (head, tail, span) ->
        let s1, headTy = synth ctorEnv recEnv (InConsHead span :: ctx) env head
        let s2, tailTy = synth ctorEnv recEnv (InConsTail span :: ctx) (applyEnv s1 env) tail
        let s3 = unifyWithContext ctx [] span tailTy (TList (apply s2 headTy))
        (compose s3 (compose s2 s1), apply s3 tailTy)

    // === Match expression ===
    | Match (scrutinee, clauses, span) ->
        // GADT matches: delegate to check mode with a fresh type variable.
        // The check-mode handler (lines 540+) performs per-branch type refinement,
        // which works correctly for any expected type including TVar.
        if isGadtMatch ctorEnv clauses then
            let freshTy = freshVar()
            let s = check ctorEnv recEnv (InCheckMode (freshTy, "gadt-match", span) :: ctx) env expr freshTy
            (s, apply s freshTy)
        else
        let s1, scrutTy = synth ctorEnv recEnv (InMatch span :: ctx) env scrutinee
        let resultTy = freshVar()
        let folder (s, idx) (pat, guard, expr) =
            let patEnv, patTy = inferPattern ctorEnv pat
            // Unify scrutinee with pattern type
            let s' = unifyWithContext ctx [] span (apply s scrutTy) patTy
            // Merge pattern env with current env
            let clauseEnv = Map.fold (fun acc k v -> Map.add k v acc)
                                     (applyEnv s' (applyEnv s env)) patEnv
            // Type-check when guard if present
            let sGuard =
                match guard with
                | Some g ->
                    let sg, gTy = synth ctorEnv recEnv ctx clauseEnv g
                    let sg' = unifyWithContext ctx [] span (apply sg gTy) TBool
                    compose sg' sg
                | None -> Type.empty
            let clauseEnv' = applyEnv sGuard clauseEnv
            // Synth clause body
            let s'', exprTy = synth ctorEnv recEnv (InMatchClause (idx, span) :: ctx) clauseEnv' expr
            // Unify with result type
            let s''' = unifyWithContext ctx [] span (apply s'' resultTy) exprTy
            (compose s''' (compose s'' (compose sGuard (compose s' s))), idx + 1)
        let finalS, _ = List.fold folder (s1, 0) clauses
        (finalS, apply finalS resultTy)

    // === Phase 6: Raise expression ===
    // raise expr: argument must be TExn, result is a fresh type variable (diverges)
    | Raise(arg, span) ->
        let s1, argTy = synth ctorEnv recEnv ctx env arg
        let s2 = unifyWithContext ctx [] span (apply s1 argTy) TExn
        let resultTy = freshVar()
        (compose s2 s1, resultTy)

    // === Phase 6: Try-with expression ===
    // try body with | pat -> handler | pat when guard -> handler
    | TryWith(body, handlers, span) ->
        let s1, bodyTy = synth ctorEnv recEnv ctx env body
        let folder (s, idx) (pat, guard, handlerExpr) =
            let patEnv, patTy = inferPattern ctorEnv pat
            // Handler patterns must match TExn
            let s' = unifyWithContext ctx [] span (apply s patTy) TExn
            let clauseEnv = Map.fold (fun acc k v -> Map.add k v acc)
                                (applyEnv s' (applyEnv s env)) patEnv
            // Type-check when guard if present
            let sGuard =
                match guard with
                | Some g ->
                    let sg, gTy = synth ctorEnv recEnv ctx clauseEnv g
                    let sg' = unifyWithContext ctx [] span (apply sg gTy) TBool
                    compose sg' sg
                | None -> Type.empty
            let clauseEnv' = applyEnv sGuard clauseEnv
            // Type-check handler body and unify with try body type
            let s'', exprTy = synth ctorEnv recEnv (InMatchClause(idx, span) :: ctx) clauseEnv' handlerExpr
            let sUnify = unifyWithContext ctx [] span
                            (apply s'' (apply sGuard (apply s' (apply s bodyTy))))
                            (apply s'' exprTy)
            (compose sUnify (compose s'' (compose sGuard (compose s' s))), idx + 1)
        let finalS, _ = List.fold folder (s1, 0) handlers
        (finalS, apply finalS bodyTy)

    // === Phase 9: Pipe and composition operators ===
    | PipeRight (left, right, span) ->
        // x |> f  ===  f x
        let s1, leftTy = synth ctorEnv recEnv ctx env left
        let s2, rightTy = synth ctorEnv recEnv ctx (applyEnv s1 env) right
        let resultTy = freshVar()
        let s3 = unifyWithContext ctx [] span (apply s2 rightTy) (TArrow(apply s2 leftTy, resultTy))
        (compose s3 (compose s2 s1), apply s3 resultTy)

    | ComposeRight (left, right, span) ->
        // f >> g : ('a -> 'b) -> ('b -> 'c) -> ('a -> 'c)
        let s1, leftTy = synth ctorEnv recEnv ctx env left
        let s2, rightTy = synth ctorEnv recEnv ctx (applyEnv s1 env) right
        let a = freshVar()
        let b = freshVar()
        let c = freshVar()
        let s3 = unifyWithContext ctx [] span (apply s2 leftTy) (TArrow(a, b))
        let s4 = unifyWithContext ctx [] span (apply s3 rightTy) (TArrow(apply s3 b, c))
        let finalS = compose s4 (compose s3 (compose s2 s1))
        (finalS, TArrow(apply finalS a, apply s4 c))

    | ComposeLeft (left, right, span) ->
        // f << g : ('b -> 'c) -> ('a -> 'b) -> ('a -> 'c)
        let s1, leftTy = synth ctorEnv recEnv ctx env left
        let s2, rightTy = synth ctorEnv recEnv ctx (applyEnv s1 env) right
        let a = freshVar()
        let b = freshVar()
        let c = freshVar()
        let s3 = unifyWithContext ctx [] span (apply s2 leftTy) (TArrow(b, c))
        let s4 = unifyWithContext ctx [] span (apply s3 rightTy) (TArrow(a, apply s3 b))
        let finalS = compose s4 (compose s3 (compose s2 s1))
        (finalS, TArrow(apply s4 a, apply finalS c))

    // === Record expressions (Phase 3) ===
    | RecordExpr (_, fields, span) ->
        // Resolve record type from field names (globally unique field names)
        let fieldNames = fields |> List.map fst |> Set.ofList
        let matchingTypes =
            recEnv
            |> Map.filter (fun _ info ->
                let declFields = info.Fields |> List.map (fun f -> f.Name) |> Set.ofList
                fieldNames = declFields)
            |> Map.toList
        match matchingTypes with
        | [(_, recInfo)] ->
            // Instantiate type params
            let freshVars = recInfo.TypeParams |> List.map (fun _ -> freshVar())
            let paramSubst = List.zip recInfo.TypeParams freshVars |> Map.ofList
            // Type check each field expression
            let folder s (fieldName, fieldExpr) =
                let fieldInfo = recInfo.Fields |> List.find (fun f -> f.Name = fieldName)
                let expectedTy = apply paramSubst fieldInfo.FieldType
                let s1, actualTy = synth ctorEnv recEnv ctx (applyEnv s env) fieldExpr
                let s2 = unifyWithContext ctx [] span expectedTy actualTy
                compose s2 (compose s1 s)
            let finalS = List.fold folder empty fields
            let resultTy = apply (compose finalS paramSubst) recInfo.ResultType
            (finalS, resultTy)
        | [] ->
            raise (TypeException { Kind = UnboundField("?", List.head fields |> fst); Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        | _ ->
            raise (TypeException { Kind = DuplicateFieldName(List.head fields |> fst); Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })

    | FieldAccess (accessExpr, fieldName, span) ->
        let s1, exprTy = synth ctorEnv recEnv ctx env accessExpr
        let resolvedTy = apply s1 exprTy
        match resolvedTy with
        // Phase 54: String property/method types
        | TString ->
            match fieldName with
            | "Length" -> (s1, TInt)
            | "Contains" -> (s1, TArrow(TString, TBool))
            | "EndsWith" -> (s1, TArrow(TString, TBool))
            | "StartsWith" -> (s1, TArrow(TString, TBool))
            | "Trim" -> (s1, TArrow(TTuple [], TString))
            | _ ->
                raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        // Phase 54: Array property types
        | TArray _ ->
            match fieldName with
            | "Length" -> (s1, TInt)
            | _ ->
                raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        // Phase 55: StringBuilder field access types
        | TData("StringBuilder", []) ->
            match fieldName with
            | "Append" ->
                let tv = freshVar()
                (s1, TArrow(tv, TData("StringBuilder", [])))
            | "ToString" ->
                (s1, TArrow(TTuple [], TString))
            | _ ->
                raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        // Phase 56: HashSet field access types
        | TData("HashSet", []) ->
            match fieldName with
            | "Add" ->
                let tv = freshVar()
                (s1, TArrow(tv, TBool))
            | "Contains" ->
                let tv = freshVar()
                (s1, TArrow(tv, TBool))
            | "Count" -> (s1, TInt)
            | _ ->
                raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        // Phase 56: Queue field access types
        | TData("Queue", []) ->
            match fieldName with
            | "Enqueue" ->
                let tv = freshVar()
                (s1, TArrow(tv, TTuple []))
            | "Dequeue" ->
                let tv = freshVar()
                (s1, TArrow(TTuple [], tv))
            | "Count" -> (s1, TInt)
            | _ ->
                raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        | TData (typeName, typeArgs) ->
            match Map.tryFind typeName recEnv with
            | Some recInfo ->
                match recInfo.Fields |> List.tryFind (fun f -> f.Name = fieldName) with
                | Some fieldInfo ->
                    let subst = List.zip recInfo.TypeParams typeArgs |> Map.ofList
                    let fieldTy = apply subst fieldInfo.FieldType
                    (s1, fieldTy)
                | None ->
                    raise (TypeException { Kind = UnboundField(typeName, fieldName); Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
            | None ->
                raise (TypeException { Kind = NotARecord typeName; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        | _ ->
            raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })

    | RecordUpdate (source, updates, span) ->
        let s1, srcTy = synth ctorEnv recEnv ctx env source
        let resolvedSrcTy = apply s1 srcTy
        match resolvedSrcTy with
        | TData (typeName, typeArgs) ->
            match Map.tryFind typeName recEnv with
            | Some recInfo ->
                let paramSubst = List.zip recInfo.TypeParams typeArgs |> Map.ofList
                // Validate update field names exist in record
                let folder s (fieldName, fieldExpr) =
                    match recInfo.Fields |> List.tryFind (fun f -> f.Name = fieldName) with
                    | Some fieldInfo ->
                        let expectedTy = apply paramSubst fieldInfo.FieldType
                        let s', actualTy = synth ctorEnv recEnv ctx (applyEnv s env) fieldExpr
                        let s'' = unifyWithContext ctx [] span expectedTy actualTy
                        compose s'' (compose s' s)
                    | None ->
                        raise (TypeException { Kind = UnboundField(typeName, fieldName); Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
                let finalS = List.fold folder s1 updates
                (finalS, apply finalS resolvedSrcTy)
            | None ->
                raise (TypeException { Kind = NotARecord typeName; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        | _ ->
            raise (TypeException { Kind = FieldAccessOnNonRecord resolvedSrcTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })

    // === SetField (mutable field assignment) ===
    | SetField (expr, fieldName, value, span) ->
        let s1, exprTy = synth ctorEnv recEnv ctx env expr
        let resolvedTy = apply s1 exprTy
        match resolvedTy with
        | TData (typeName, typeArgs) ->
            match Map.tryFind typeName recEnv with
            | Some recInfo ->
                match recInfo.Fields |> List.tryFind (fun f -> f.Name = fieldName) with
                | Some fieldInfo ->
                    if not fieldInfo.IsMutable then
                        raise (TypeException { Kind = ImmutableFieldAssignment(typeName, fieldName); Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
                    let subst = List.zip recInfo.TypeParams typeArgs |> Map.ofList
                    let fieldTy = apply subst fieldInfo.FieldType
                    let s2, valTy = synth ctorEnv recEnv ctx (applyEnv s1 env) value
                    let s3 = unifyWithContext ctx [] span fieldTy valTy
                    (compose s3 (compose s2 s1), TTuple [])
                | None ->
                    raise (TypeException { Kind = UnboundField(typeName, fieldName); Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
            | None ->
                raise (TypeException { Kind = NotARecord typeName; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
        | _ ->
            raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })

    // === IndexGet (Phase 47 - array/hashtable index read) ===
    | IndexGet (collExpr, idxExpr, span) ->
        let s1, collTy = synth ctorEnv recEnv ctx env collExpr
        let resolvedCollTy = apply s1 collTy
        match resolvedCollTy with
        | TArray elemTy ->
            let s2, idxTy = synth ctorEnv recEnv ctx (applyEnv s1 env) idxExpr
            let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
            (compose s3 (compose s2 s1), apply (compose s3 s2) elemTy)
        | THashtable (keyTy, valTy) ->
            let s2, idxTy = synth ctorEnv recEnv ctx (applyEnv s1 env) idxExpr
            let s3 = unifyWithContext ctx [] span (apply s2 idxTy) keyTy
            (compose s3 (compose s2 s1), apply s3 valTy)
        | ty ->
            raise (TypeException {
                Kind = IndexOnNonCollection ty
                Span = span; Term = Some expr; ContextStack = ctx; Trace = []
            })

    // === IndexSet (Phase 47 - array/hashtable index write) ===
    | IndexSet (collExpr, idxExpr, valExpr, span) ->
        let s1, collTy = synth ctorEnv recEnv ctx env collExpr
        let resolvedCollTy = apply s1 collTy
        match resolvedCollTy with
        | TArray elemTy ->
            let env1 = applyEnv s1 env
            let s2, idxTy = synth ctorEnv recEnv ctx env1 idxExpr
            let s3 = unifyWithContext ctx [] span (apply s2 idxTy) TInt
            let env2 = applyEnv (compose s3 s2) env1
            let s4, valTy = synth ctorEnv recEnv ctx env2 valExpr
            let s5 = unifyWithContext ctx [] span (apply s4 valTy) (apply (compose s4 (compose s3 s2)) elemTy)
            (compose s5 (compose s4 (compose s3 (compose s2 s1))), TTuple [])
        | THashtable (keyTy, valTy) ->
            let env1 = applyEnv s1 env
            let s2, idxTy = synth ctorEnv recEnv ctx env1 idxExpr
            let s3 = unifyWithContext ctx [] span (apply s2 idxTy) keyTy
            let env2 = applyEnv (compose s3 s2) env1
            let s4, valTy' = synth ctorEnv recEnv ctx env2 valExpr
            let s5 = unifyWithContext ctx [] span (apply s4 valTy') (apply (compose s4 s3) valTy)
            (compose s5 (compose s4 (compose s3 (compose s2 s1))), TTuple [])
        | ty ->
            raise (TypeException {
                Kind = IndexOnNonCollection ty
                Span = span; Term = Some expr; ContextStack = ctx; Trace = []
            })

    // === Phase 18: Range expression ===
    // [start..stop] or [start..step..stop] — all components must be int, result is int list
    | Range (startExpr, stopExpr, stepOpt, span) ->
        let s1, startTy = synth ctorEnv recEnv ctx env startExpr
        let s2 = unifyWithContext ctx [] span (apply s1 startTy) TInt
        let s12 = compose s2 s1
        let s3, stopTy = synth ctorEnv recEnv ctx (applyEnv s12 env) stopExpr
        let s4 = unifyWithContext ctx [] span (apply s3 stopTy) TInt
        let s34 = compose s4 s3
        let sStep =
            match stepOpt with
            | Some stepExpr ->
                let s5, stepTy = synth ctorEnv recEnv ctx (applyEnv (compose s34 s12) env) stepExpr
                let s6 = unifyWithContext ctx [] span (apply s5 stepTy) TInt
                compose s6 s5
            | None -> empty
        let finalS = compose sStep (compose s34 s12)
        (finalS, TList TInt)

    // === LetPat ===
    | LetPat (pat, value, body, span) ->
        let s1, valueTy = synth ctorEnv recEnv ctx env value
        let patEnv, patTy = inferPattern ctorEnv pat
        let s2 = unifyWithContext ctx [] span (apply s1 valueTy) patTy
        let s = compose s2 s1
        let env' = applyEnv s env
        let generalizedPatEnv =
            patEnv
            |> Map.map (fun _ (Scheme (_, ty)) ->
                let ty' = apply s ty
                generalize env' ty')
        let bodyEnv = Map.fold (fun acc k v -> Map.add k v acc) env' generalizedPatEnv
        let s3, bodyTy = synth ctorEnv recEnv ctx bodyEnv body
        (compose s3 s, bodyTy)

/// Check expression against expected type (checking mode)
/// Returns: substitution that makes expression have expected type
and check (ctorEnv: ConstructorEnv) (recEnv: RecordEnv) (ctx: InferContext list) (env: TypeEnv) (expr: Expr) (expected: Type): Subst =
    match expr with
    // === Lambda against TArrow (BIDIR-04) ===
    | Lambda (param, body, _) ->
        match expected with
        | TArrow (paramTy, resultTy) ->
            let bodyEnv = Map.add param (Scheme ([], paramTy)) env
            let s = check ctorEnv recEnv ctx bodyEnv body resultTy
            let s' = unifyWithContext ctx [] (spanOf expr) (apply s paramTy) paramTy
            compose s' s
        | _ ->
            // Not an arrow type - fall through to subsumption
            let s, actual = synth ctorEnv recEnv ctx env expr
            let s' = unifyWithContext ctx [] (spanOf expr) (apply s expected) actual
            compose s' s

    // === If against expected (BIDIR-04) ===
    | If (cond, thenExpr, elseExpr, span) ->
        let s1, condTy = synth ctorEnv recEnv (InIfCond span :: ctx) env cond
        let s2 = unifyWithContext ctx [] span (apply s1 condTy) TBool
        let s12 = compose s2 s1
        let s3 = check ctorEnv recEnv (InIfThen span :: ctx) (applyEnv s12 env) thenExpr (apply s12 expected)
        let s4 = check ctorEnv recEnv (InIfElse span :: ctx) (applyEnv (compose s3 s12) env) elseExpr (apply (compose s3 s12) expected)
        compose s4 (compose s3 s12)

    // === GADT Match in check mode: local type refinement per branch ===
    | Match (scrutinee, clauses, span) when isGadtMatch ctorEnv clauses ->
        let s1, scrutTy = synth ctorEnv recEnv (InMatch span :: ctx) env scrutinee

        // Detect polymorphic mode: expected is still an unbound TVar after s1.
        // In this mode each branch must refine the result type independently —
        // we must NOT compose one branch's bodyS into the accumulator s, or the
        // first branch's result type will "poison" the next branch.
        let isPolyExpected =
            match apply s1 expected with
            | TVar _ -> true
            | _ -> false

        let folder (s, idx) (pat, guard, body) =
            let clauseCtx = InMatchClause(idx, span) :: ctx
            match pat with
            | ConstructorPat(name, argPatOpt, patSpan) ->
                match Map.tryFind name ctorEnv with
                | Some ctorInfo when ctorInfo.IsGadt ->
                    // === GADT branch with type refinement ===

                    // 1. Instantiate constructor type params with fresh vars
                    let freshVars = ctorInfo.TypeParams |> List.map (fun _ -> freshVar())
                    let ctorSubst = List.zip ctorInfo.TypeParams freshVars |> Map.ofList
                    let ctorResultType = apply ctorSubst ctorInfo.ResultType
                    let ctorArgType = ctorInfo.ArgType |> Option.map (apply ctorSubst)

                    // 2. Compute local constraints by unifying scrutinee with constructor return type
                    let currentScrutTy = apply s scrutTy
                    let localS = unifyWithContext clauseCtx [] patSpan currentScrutTy ctorResultType

                    // 3. Identify existential fresh vars (constructor-local type params)
                    let existentialFreshVars =
                        ctorInfo.ExistentialVars
                        |> List.choose (fun origIdx ->
                            List.zip ctorInfo.TypeParams freshVars
                            |> List.tryFind (fun (p, _) -> p = origIdx)
                            |> Option.map snd)
                        |> List.choose (fun tv ->
                            match tv with TVar n -> Some n | _ -> None)

                    // 4. Bind pattern variables from constructor argument
                    let combinedLocalS, patEnv =
                        match (ctorArgType, argPatOpt) with
                        | (Some argTy, Some argPat) ->
                            let patEnv, patTy = inferPattern ctorEnv argPat
                            let localArgTy = apply localS argTy
                            let patS = unifyWithContext clauseCtx [] patSpan localArgTy patTy
                            let patEnv' = Map.map (fun _ scheme -> applyScheme (compose patS localS) scheme) patEnv
                            (compose patS localS, patEnv')
                        | (None, None) -> (localS, Map.empty)
                        | (None, Some _) ->
                            raise (TypeException {
                                Kind = ArityMismatch(name, 0, 1); Span = patSpan
                                Term = None; ContextStack = clauseCtx; Trace = [] })
                        | (Some _, None) ->
                            raise (TypeException {
                                Kind = ArityMismatch(name, 1, 0); Span = patSpan
                                Term = None; ContextStack = clauseCtx; Trace = [] })

                    // 5. Build branch environment with pattern bindings + refined types
                    let branchEnv =
                        Map.fold (fun acc k v -> Map.add k v acc)
                            (applyEnv (compose combinedLocalS s) env)
                            patEnv

                    // 5b. Type-check when guard if present
                    let sGuard =
                        match guard with
                        | Some g ->
                            let sg, gTy = synth ctorEnv recEnv clauseCtx branchEnv g
                            let sg' = unifyWithContext clauseCtx [] span (apply sg gTy) TBool
                            compose sg' sg
                        | None -> Type.empty
                    let branchEnv' = applyEnv sGuard branchEnv

                    // 6. Check branch body against refined expected type
                    if isPolyExpected then
                        // Polymorphic mode: each branch gets an independently refined expected.
                        // Apply only combinedLocalS (the GADT constructor unification) to the
                        // original expected TVar — NOT the accumulated s — so each branch starts
                        // fresh. bodyS is branch-local and not composed into the cross-branch s.
                        let localExpected = apply combinedLocalS expected
                        let bodyS = check ctorEnv recEnv clauseCtx branchEnv' body localExpected

                        // 7p. Existential escape check in polymorphic mode
                        let resultTy = apply bodyS localExpected
                        for ev in existentialFreshVars do
                            if Set.contains ev (freeVars resultTy) then
                                raise (TypeException {
                                    Kind = ExistentialEscape ev
                                    Span = span; Term = None
                                    ContextStack = clauseCtx; Trace = [] })

                        // Do NOT compose bodyS into s — branch result stays independent
                        (s, idx + 1)
                    else
                        // Concrete mode: existing behavior — accumulate substitution across branches
                        let refinedExpected = apply sGuard (apply (compose combinedLocalS s) expected)
                        let bodyS = check ctorEnv recEnv clauseCtx branchEnv' body refinedExpected

                        // 7. Check existential escape: result type must not mention existential vars
                        let resultTy = apply bodyS refinedExpected
                        for ev in existentialFreshVars do
                            if Set.contains ev (freeVars resultTy) then
                                raise (TypeException {
                                    Kind = ExistentialEscape ev
                                    Span = span; Term = None
                                    ContextStack = clauseCtx; Trace = [] })

                        // 8. Local constraints stay local -- only body substitution propagates
                        (compose bodyS s, idx + 1)

                | _ ->
                    // Regular ADT constructor in a mixed GADT type
                    let patEnv, patTy = inferPattern ctorEnv pat
                    let s' = unifyWithContext clauseCtx [] span (apply s scrutTy) patTy
                    let clauseEnv = Map.fold (fun acc k v -> Map.add k v acc)
                                        (applyEnv s' (applyEnv s env)) patEnv
                    let sGuard =
                        match guard with
                        | Some g ->
                            let sg, gTy = synth ctorEnv recEnv clauseCtx clauseEnv g
                            let sg' = unifyWithContext clauseCtx [] span (apply sg gTy) TBool
                            compose sg' sg
                        | None -> Type.empty
                    let clauseEnv' = applyEnv sGuard clauseEnv
                    let bodyS = check ctorEnv recEnv clauseCtx clauseEnv' body (apply sGuard (apply (compose s' s) expected))
                    (compose bodyS (compose sGuard (compose s' s)), idx + 1)

            | _ ->
                // Non-constructor pattern (wildcard, var, etc.)
                let patEnv, patTy = inferPattern ctorEnv pat
                let s' = unifyWithContext clauseCtx [] span (apply s scrutTy) patTy
                let clauseEnv = Map.fold (fun acc k v -> Map.add k v acc)
                                    (applyEnv s' (applyEnv s env)) patEnv
                let sGuard =
                    match guard with
                    | Some g ->
                        let sg, gTy = synth ctorEnv recEnv clauseCtx clauseEnv g
                        let sg' = unifyWithContext clauseCtx [] span (apply sg gTy) TBool
                        compose sg' sg
                    | None -> Type.empty
                let clauseEnv' = applyEnv sGuard clauseEnv
                let bodyS = check ctorEnv recEnv clauseCtx clauseEnv' body (apply sGuard (apply (compose s' s) expected))
                (compose bodyS (compose sGuard (compose s' s)), idx + 1)

        let finalS, _ = List.fold folder (s1, 0) clauses
        finalS

    // === Fallback subsumption (BIDIR-06) ===
    | _ ->
        let s, actual = synth ctorEnv recEnv ctx env expr
        let s' = unifyWithContext ctx [] (spanOf expr) (apply s expected) actual
        compose s' s

/// Helper: infer binary operator
and inferBinaryOp ctorEnv recEnv ctx env e1 e2 leftTy rightTy =
    let s1, t1 = synth ctorEnv recEnv ctx env e1
    let s2, t2 = synth ctorEnv recEnv ctx (applyEnv s1 env) e2
    let s3 = unifyWithContext ctx [] (spanOf e1) (apply s2 t1) leftTy
    let s4 = unifyWithContext ctx [] (spanOf e2) (apply s3 t2) rightTy
    compose s4 (compose s3 (compose s2 s1))

/// Top-level entry: infer type for expression (no constructor env)
let synthTop (env: TypeEnv) (expr: Expr): Type =
    let s, ty = synth Map.empty Map.empty [] env expr
    apply s ty

/// Top-level entry: infer type for expression with constructor env
let synthTopWithCtors (ctorEnv: ConstructorEnv) (env: TypeEnv) (expr: Expr): Type =
    let s, ty = synth ctorEnv Map.empty [] env expr
    apply s ty
