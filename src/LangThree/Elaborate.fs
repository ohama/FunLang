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

    | TEName _name ->
        // Named type (e.g., Tree, Option) - will be resolved in Phase 2 type checking
        // For now, treat as a fresh type variable (placeholder)
        let idx = freshTypeVarIndex()
        (TVar idx, vars)

/// Elaborate single type expression with fresh scope
/// Each call starts with empty type variable environment
let elaborateTypeExpr (te: TypeExpr): Type =
    let (ty, _) = elaborateWithVars Map.empty te
    ty

/// Elaborate a type declaration into ConstructorEnv entries
/// Example: type Option 'a = None | Some of 'a
/// Returns: Map with "None" -> {TypeParams=[0]; ArgType=None; ResultType=TData("Option",[TVar 0])}
///                  "Some" -> {TypeParams=[0]; ArgType=Some(TVar 0); ResultType=TData("Option",[TVar 0])}
let elaborateTypeDecl (Ast.TypeDecl(name, typeParams, constructors, _): Ast.TypeDecl) : ConstructorEnv =
    // Map type parameter names to TVar indices (deterministic: 'a->0, 'b->1)
    let paramMap =
        typeParams
        |> List.mapi (fun i p ->
            let varName = if p.StartsWith("'") then p else "'" + p
            (varName, i))
        |> Map.ofList

    let typeParamVars = typeParams |> List.mapi (fun i _ -> i)
    let resultType = TData(name, List.map TVar typeParamVars)

    // Elaborate a TypeExpr within the context of this type declaration
    // Uses paramMap for type variables, produces TData for named type references
    let rec substTypeExpr = function
        | Ast.TEVar v ->
            match Map.tryFind v paramMap with
            | Some idx -> TVar idx
            | None -> failwithf "Unbound type parameter: %s" v
        | Ast.TEName n ->
            // Named type reference (e.g., Tree in recursive ADT, or other types)
            TData(n, [])
        | Ast.TEInt -> TInt
        | Ast.TEBool -> TBool
        | Ast.TEString -> TString
        | Ast.TEList te -> TList (substTypeExpr te)
        | Ast.TEArrow (t1, t2) -> TArrow (substTypeExpr t1, substTypeExpr t2)
        | Ast.TETuple ts -> TTuple (List.map substTypeExpr ts)

    // Elaborate each constructor
    constructors
    |> List.map (fun (Ast.ConstructorDecl(ctorName, dataTypeOpt, _)) ->
        let argType = dataTypeOpt |> Option.map substTypeExpr
        let info = {
            TypeParams = typeParamVars
            ArgType = argType
            ResultType = resultType
        }
        (ctorName, info))
    |> Map.ofList

/// Elaborate multiple type expressions sharing the same scope
/// Used for curried function parameters: fun (x: 'a) (y: 'a) -> ...
/// Both 'a refer to the same type variable
let elaborateScoped (tes: TypeExpr list): Type list =
    let folder (acc, env) te =
        let (ty, env') = elaborateWithVars env te
        (ty :: acc, env')
    let (revTypes, _) = List.fold folder ([], Map.empty) tes
    List.rev revTypes
