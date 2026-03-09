module TypeCheck

open Type
open Unify
open Infer
open Bidir
open Ast
open Elaborate
open Diagnostic
open Exhaustive

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

/// Recursively collect all match expressions from an expression.
/// Returns list of (patterns, scrutinee, span) for each Match node.
let rec collectMatches (expr: Expr) : (Pattern list * Expr * Span) list =
    match expr with
    | Match(scrutinee, clauses, span) ->
        let patterns = clauses |> List.map fst
        let nested =
            collectMatches scrutinee
            @ (clauses |> List.collect (fun (_, body) -> collectMatches body))
        (patterns, scrutinee, span) :: nested
    | Let(_, rhs, body, _) -> collectMatches rhs @ collectMatches body
    | LetPat(_, rhs, body, _) -> collectMatches rhs @ collectMatches body
    | LetRec(_, _, rhs, body, _) -> collectMatches rhs @ collectMatches body
    | Lambda(_, body, _) | LambdaAnnot(_, _, body, _) -> collectMatches body
    | App(f, arg, _) -> collectMatches f @ collectMatches arg
    | If(cond, thenE, elseE, _) -> collectMatches cond @ collectMatches thenE @ collectMatches elseE
    | Add(a, b, _) | Subtract(a, b, _) | Multiply(a, b, _) | Divide(a, b, _) ->
        collectMatches a @ collectMatches b
    | Equal(a, b, _) | NotEqual(a, b, _) | LessThan(a, b, _) | GreaterThan(a, b, _)
    | LessEqual(a, b, _) | GreaterEqual(a, b, _) | And(a, b, _) | Or(a, b, _) ->
        collectMatches a @ collectMatches b
    | Cons(a, b, _) -> collectMatches a @ collectMatches b
    | Negate(e, _) | Annot(e, _, _) -> collectMatches e
    | Tuple(es, _) | List(es, _) -> es |> List.collect collectMatches
    | Constructor(_, Some arg, _) -> collectMatches arg
    | Number _ | Bool _ | String _ | Var _ | EmptyList _ | Constructor(_, None, _) -> []

/// Type check a module: build ConstructorEnv from type declarations,
/// then type check all let declarations with constructor environment.
/// Returns Ok(warnings) on success, Error(Diagnostic) on type error.
let typeCheckModule (m: Module) : Result<Diagnostic list, Diagnostic> =
    try
        match m with
        | EmptyModule _ -> Ok []
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

            // Collect all match expressions from let declarations
            let allMatches =
                decls
                |> List.choose (function
                    | LetDecl(_, body, _) -> Some body
                    | _ -> None)
                |> List.collect collectMatches

            // Determine ADT type from constructor patterns in a match expression.
            // Looks at the first constructor pattern and resolves its result type.
            let inferTypeFromPatterns (patterns: Pattern list) : Type option =
                patterns
                |> List.tryPick (fun pat ->
                    match pat with
                    | Ast.ConstructorPat(name, _, _) ->
                        match Map.tryFind name ctorEnv with
                        | Some info -> Some info.ResultType
                        | None -> None
                    | _ -> None)

            // Check exhaustiveness and redundancy for ADT matches
            let warnings =
                allMatches
                |> List.collect (fun (patterns, _scrutinee, matchSpan) ->
                    let mutable scrWarnings = []

                    // Determine the scrutinee type from constructor patterns
                    match inferTypeFromPatterns patterns with
                    | Some scrTy ->
                        let constructorSet = getConstructorsFromEnv ctorEnv scrTy

                        if not (List.isEmpty constructorSet) then
                            // Convert AST patterns to CasePat
                            let casePats = patterns |> List.map astPatToCasePat

                            // Check exhaustiveness
                            match Exhaustive.checkExhaustive constructorSet casePats with
                            | Exhaustive.NonExhaustive missing ->
                                let diag =
                                    { Kind = NonExhaustiveMatch(missing |> List.map Exhaustive.formatPattern)
                                      Span = matchSpan
                                      Term = None
                                      ContextStack = []
                                      Trace = [] }
                                    |> typeErrorToDiagnostic
                                scrWarnings <- diag :: scrWarnings
                            | Exhaustive.Exhaustive -> ()

                            // Check redundancy
                            match Exhaustive.checkRedundant constructorSet casePats with
                            | Exhaustive.HasRedundancy indices ->
                                for idx in indices do
                                    let diag =
                                        { Kind = RedundantPattern(idx)
                                          Span = matchSpan
                                          Term = None
                                          ContextStack = []
                                          Trace = [] }
                                        |> typeErrorToDiagnostic
                                    scrWarnings <- diag :: scrWarnings
                            | Exhaustive.NoRedundancy -> ()
                    | None -> ()

                    scrWarnings |> List.rev)

            Ok warnings
    with
    | TypeException err ->
        Error(typeErrorToDiagnostic err)
