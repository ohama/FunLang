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

    | TEData (name, args) ->
        // Parameterized named type (e.g., int expr) - Phase 4 GADT
        let folder (acc, env) t =
            let (ty, env') = elaborateWithVars env t
            (ty :: acc, env')
        let (revTypes, finalVars) = List.fold folder ([], vars) args
        (TData(name, List.rev revTypes), finalVars)

/// Elaborate single type expression with fresh scope
/// Each call starts with empty type variable environment
let elaborateTypeExpr (te: TypeExpr): Type =
    let (ty, _) = elaborateWithVars Map.empty te
    ty

/// Substitute type expressions using a parameter map (shared helper for type/record elaboration)
/// Maps type variable names to TVar indices and resolves named types
let rec substTypeExprWithMap (paramMap: Map<string, int>) (te: Ast.TypeExpr) : Type =
    match te with
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
    | Ast.TEList te -> TList (substTypeExprWithMap paramMap te)
    | Ast.TEArrow (t1, t2) -> TArrow (substTypeExprWithMap paramMap t1, substTypeExprWithMap paramMap t2)
    | Ast.TETuple ts -> TTuple (List.map (substTypeExprWithMap paramMap) ts)
    | Ast.TEData(name, args) ->
        TData(name, List.map (substTypeExprWithMap paramMap) args)

/// Collect all TEVar names from a TypeExpr (for detecting constructor-local type variables)
let rec collectTypeExprVars (te: Ast.TypeExpr) : Set<string> =
    match te with
    | Ast.TEVar v -> Set.singleton v
    | Ast.TEInt | Ast.TEBool | Ast.TEString | Ast.TEName _ -> Set.empty
    | Ast.TEList t -> collectTypeExprVars t
    | Ast.TEArrow(t1, t2) -> Set.union (collectTypeExprVars t1) (collectTypeExprVars t2)
    | Ast.TETuple ts -> ts |> List.map collectTypeExprVars |> Set.unionMany
    | Ast.TEData(_, args) -> args |> List.map collectTypeExprVars |> Set.unionMany

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

    // Detect if any constructor uses GADT syntax for the IsGadt sweep
    let hasAnyGadt =
        constructors |> List.exists (fun ctor ->
            match ctor with
            | Ast.GadtConstructorDecl _ -> true
            | Ast.ConstructorDecl _ -> false)

    // Elaborate each constructor
    let ctorEntries =
        constructors
        |> List.map (fun ctor ->
            match ctor with
            | Ast.ConstructorDecl(ctorName, dataTypeOpt, _) ->
                let argType = dataTypeOpt |> Option.map (substTypeExprWithMap paramMap)
                let info = {
                    TypeParams = typeParamVars
                    ArgType = argType
                    ResultType = resultType
                    IsGadt = hasAnyGadt  // Mark ALL as GADT if any constructor uses GADT syntax
                    ExistentialVars = []
                }
                (ctorName, info)
            | Ast.GadtConstructorDecl(ctorName, argTypes, retType, _) ->
                // Collect type variable names from argument types
                let argVarNames = argTypes |> List.map collectTypeExprVars |> Set.unionMany
                // Constructor-local type variables: TEVar names in args but NOT in the type's paramMap
                let localVarNames = argVarNames |> Set.filter (fun v -> not (Map.containsKey v paramMap))
                // Allocate fresh indices for constructor-local type variables, extend paramMap
                let extendedParamMap, localIndices =
                    localVarNames
                    |> Set.toList
                    |> List.fold (fun (pm, indices) v ->
                        let idx = freshTypeVarIndex()
                        (Map.add v idx pm, idx :: indices)) (paramMap, [])
                let localIndices = List.rev localIndices
                let allTypeParams = typeParamVars @ localIndices

                let substExpr = substTypeExprWithMap extendedParamMap
                let argType =
                    match argTypes with
                    | [] -> None
                    | [t] -> Some (substExpr t)
                    | ts -> Some (TTuple (List.map substExpr ts))
                let gadtResultType = substExpr retType
                // Determine existential vars: type params in args but NOT in result type
                let resultFreeVars = Type.freeVars gadtResultType
                let argFreeVars =
                    match argType with
                    | Some t -> Type.freeVars t
                    | None -> Set.empty
                let existentials = Set.difference argFreeVars resultFreeVars |> Set.toList
                let info = {
                    TypeParams = allTypeParams
                    ArgType = argType
                    ResultType = gadtResultType
                    IsGadt = true
                    ExistentialVars = existentials
                }
                (ctorName, info))

    ctorEntries |> Map.ofList

/// Elaborate a record type declaration into RecordTypeInfo
/// Phase 3 (Records): Maps record fields to typed metadata
let elaborateRecordDecl (Ast.RecordDecl(name, typeParams, fields, _)) : string * RecordTypeInfo =
    let paramMap =
        typeParams
        |> List.mapi (fun i p ->
            let varName = if p.StartsWith("'") then p else "'" + p
            (varName, i))
        |> Map.ofList
    let typeParamVars = typeParams |> List.mapi (fun i _ -> i)
    let resultType = TData(name, List.map TVar typeParamVars)
    let fieldInfos =
        fields |> List.mapi (fun idx (Ast.RecordFieldDecl(fname, ftype, isMut, _)) ->
            { Name = fname
              FieldType = substTypeExprWithMap paramMap ftype
              IsMutable = isMut
              Index = idx })
    (name, { TypeParams = typeParamVars; Fields = fieldInfos; ResultType = resultType })

/// Elaborate multiple type expressions sharing the same scope
/// Used for curried function parameters: fun (x: 'a) (y: 'a) -> ...
/// Both 'a refer to the same type variable
let elaborateScoped (tes: TypeExpr list): Type list =
    let folder (acc, env) te =
        let (ty, env') = elaborateWithVars env te
        (ty :: acc, env')
    let (revTypes, _) = List.fold folder ([], Map.empty) tes
    List.rev revTypes
