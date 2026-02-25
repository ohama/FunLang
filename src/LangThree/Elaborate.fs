module Elaborate

open Ast
open Type

/// Type variable environment: maps type variable names to TVar indices
/// Example: 'a -> 0, 'b -> 1
type TypeVarEnv = Map<string, int>

/// Fresh type variable index generator for elaboration
/// Start at 0 (separate range from inference's 1000+)
let freshTypeVarIndex =
    let counter = ref 0
    fun () ->
        let n = !counter
        counter := n + 1
        n

/// Elaborate type expression to type, threading type variable environment
/// Returns: (elaborated type, updated environment)
let rec elaborateWithVars (vars: TypeVarEnv) (te: TypeExpr): Type * TypeVarEnv =
    match te with
    | TEInt -> (TInt, vars)
    | TEBool -> (TBool, vars)
    | TEString -> (TString, vars)

    | TEList t ->
        let (ty, vars') = elaborateWithVars vars t
        (TList ty, vars')

    | TEArrow (t1, t2) ->
        let (ty1, vars1) = elaborateWithVars vars t1
        let (ty2, vars2) = elaborateWithVars vars1 t2
        (TArrow (ty1, ty2), vars2)

    | TETuple ts ->
        // Fold over tuple elements, threading environment
        let folder (acc, env) t =
            let (ty, env') = elaborateWithVars env t
            (ty :: acc, env')
        let (revTypes, finalVars) = List.fold folder ([], vars) ts
        (TTuple (List.rev revTypes), finalVars)

    | TEVar name ->
        // Type variable: 'a, 'b, etc.
        // If already seen in this scope, reuse index
        // If new, allocate fresh index and record it
        match Map.tryFind name vars with
        | Some idx -> (TVar idx, vars)
        | None ->
            let idx = freshTypeVarIndex()
            let vars' = Map.add name idx vars
            (TVar idx, vars')

/// Elaborate single type expression with fresh scope
/// Each call starts with empty type variable environment
let elaborateTypeExpr (te: TypeExpr): Type =
    let (ty, _) = elaborateWithVars Map.empty te
    ty

/// Elaborate multiple type expressions sharing the same scope
/// Used for curried function parameters: fun (x: 'a) (y: 'a) -> ...
/// Both 'a refer to the same type variable
let elaborateScoped (tes: TypeExpr list): Type list =
    let folder (acc, env) te =
        let (ty, env') = elaborateWithVars env te
        (ty :: acc, env')
    let (revTypes, _) = List.fold folder ([], Map.empty) tes
    List.rev revTypes
