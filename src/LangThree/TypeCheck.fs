module TypeCheck

open Type
open Unify
open Infer
open Bidir
open Ast
open Elaborate
open Diagnostic

/// Initial type environment with Prelude function type schemes
/// All Prelude functions have polymorphic types using type variables 0-9
let initialTypeEnv: TypeEnv =
    Map.ofList [
        // map: ('a -> 'b) -> 'a list -> 'b list
        "map", Scheme([0; 1], TArrow(TArrow(TVar 0, TVar 1), TArrow(TList(TVar 0), TList(TVar 1))))

        // filter: ('a -> bool) -> 'a list -> 'a list
        "filter", Scheme([0], TArrow(TArrow(TVar 0, TBool), TArrow(TList(TVar 0), TList(TVar 0))))

        // fold: ('b -> 'a -> 'b) -> 'b -> 'a list -> 'b
        "fold", Scheme([0; 1], TArrow(TArrow(TVar 1, TArrow(TVar 0, TVar 1)), TArrow(TVar 1, TArrow(TList(TVar 0), TVar 1))))

        // length: 'a list -> int
        "length", Scheme([0], TArrow(TList(TVar 0), TInt))

        // reverse: 'a list -> 'a list
        "reverse", Scheme([0], TArrow(TList(TVar 0), TList(TVar 0)))

        // append: 'a list -> 'a list -> 'a list
        "append", Scheme([0], TArrow(TList(TVar 0), TArrow(TList(TVar 0), TList(TVar 0))))

        // id: 'a -> 'a
        "id", Scheme([0], TArrow(TVar 0, TVar 0))

        // const: 'a -> 'b -> 'a
        "const", Scheme([0; 1], TArrow(TVar 0, TArrow(TVar 1, TVar 0)))

        // compose: ('b -> 'c) -> ('a -> 'b) -> 'a -> 'c
        "compose", Scheme([0; 1; 2], TArrow(TArrow(TVar 1, TVar 2), TArrow(TArrow(TVar 0, TVar 1), TArrow(TVar 0, TVar 2))))

        // hd: 'a list -> 'a
        "hd", Scheme([0], TArrow(TList(TVar 0), TVar 0))

        // tl: 'a list -> 'a list
        "tl", Scheme([0], TArrow(TList(TVar 0), TList(TVar 0)))
    ]

/// Type check an expression using the initial type environment
/// Returns Ok(type) on success, Error(message) on type error
let typecheck (expr: Expr): Result<Type, string> =
    try
        let ty = synthTop initialTypeEnv expr
        Ok(ty)
    with
    | TypeException err ->
        // Convert to Diagnostic, then extract message for backward compatibility
        let diag = typeErrorToDiagnostic err
        Error(diag.Message)

/// Type check an expression and return full diagnostic on error
/// Returns Ok(type) on success, Error(Diagnostic) on type error
let typecheckWithDiagnostic (expr: Expr): Result<Type, Diagnostic> =
    try
        let ty = synthTop initialTypeEnv expr
        Ok(ty)
    with
    | TypeException err ->
        Error(typeErrorToDiagnostic err)

/// Type check a module: build ConstructorEnv from type declarations,
/// then type check all let declarations with constructor environment
/// Returns Ok(unit) on success, Error(Diagnostic) on type error
let typeCheckModule (m: Module) : Result<unit, Diagnostic> =
    try
        match m with
        | EmptyModule _ -> Ok ()
        | Module (decls, _) ->
            // Build constructor environment from type declarations
            let ctorEnv =
                decls
                |> List.choose (function
                    | Decl.TypeDecl td -> Some td
                    | _ -> None)
                |> List.map elaborateTypeDecl
                |> List.fold (fun acc map ->
                    Map.fold (fun acc' k v -> Map.add k v acc') acc map) Map.empty

            // Type check let declarations sequentially, accumulating type environment
            let _finalEnv =
                decls
                |> List.choose (function
                    | LetDecl(n, e, _) -> Some(n, e)
                    | _ -> None)
                |> List.fold (fun (env: TypeEnv) (name, body) ->
                    let s, ty = Bidir.synth ctorEnv [] env body
                    let ty' = apply s ty
                    let scheme = generalize (applyEnv s env) ty'
                    Map.add name scheme env) initialTypeEnv

            Ok ()
    with
    | TypeException err ->
        Error(typeErrorToDiagnostic err)
