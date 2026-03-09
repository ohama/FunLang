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

/// Module exports: collected type/constructor/record environments from a module
type ModuleExports = {
    TypeEnv: TypeEnv
    CtorEnv: ConstructorEnv
    RecEnv: RecordEnv
    SubModules: Map<string, ModuleExports>
}

let emptyModuleExports = {
    TypeEnv = Map.empty; CtorEnv = Map.empty
    RecEnv = Map.empty; SubModules = Map.empty
}

/// Merge module exports into current environments (for open directives)
let openModuleExports (exports: ModuleExports)
                      (typeEnv: TypeEnv) (ctorEnv: ConstructorEnv) (recEnv: RecordEnv)
    : TypeEnv * ConstructorEnv * RecordEnv =
    let typeEnv' = Map.fold (fun acc k v -> Map.add k v acc) typeEnv exports.TypeEnv
    let ctorEnv' = Map.fold (fun acc k v -> Map.add k v acc) ctorEnv exports.CtorEnv
    let recEnv' = Map.fold (fun acc k v -> Map.add k v acc) recEnv exports.RecEnv
    (typeEnv', ctorEnv', recEnv')

/// Resolve a module path in the modules map, raising E0502 if not found
let resolveModule (modules: Map<string, ModuleExports>) (path: string list) (span: Span) : ModuleExports =
    let rec resolve (mods: Map<string, ModuleExports>) (remaining: string list) =
        match remaining with
        | [] -> failwith "empty module path"
        | [name] ->
            match Map.tryFind name mods with
            | Some exports -> exports
            | None ->
                raise (TypeException {
                    Kind = UnresolvedModule name
                    Span = span; Term = None; ContextStack = []; Trace = [] })
        | name :: rest ->
            match Map.tryFind name mods with
            | Some exports -> resolve exports.SubModules rest
            | None ->
                raise (TypeException {
                    Kind = UnresolvedModule name
                    Span = span; Term = None; ContextStack = []; Trace = [] })
    resolve modules path

/// Detect circular module dependencies using DFS 3-color algorithm.
/// Returns Some(cycle path) if circular dependency found, None otherwise.
let detectCircularDeps (graph: Map<string, string list>) : string list option =
    // Colors: 0=white (unvisited), 1=gray (in progress), 2=black (done)
    let color = System.Collections.Generic.Dictionary<string, int>()
    let parent = System.Collections.Generic.Dictionary<string, string option>()
    for key in graph.Keys do
        color.[key] <- 0
        parent.[key] <- None

    let rec dfs (node: string) : string list option =
        color.[node] <- 1  // gray
        let neighbors = match graph.TryGetValue(node) with | true, ns -> ns | _ -> []
        let result =
            neighbors |> List.tryPick (fun neighbor ->
                match color.TryGetValue(neighbor) with
                | true, 1 ->
                    // Found cycle: reconstruct path
                    Some [neighbor; node; neighbor]
                | true, 0 ->
                    parent.[neighbor] <- Some node
                    dfs neighbor
                | _ -> None)
        color.[node] <- 2  // black
        result

    graph.Keys
    |> Seq.tryPick (fun node ->
        match color.[node] with
        | 0 -> dfs node
        | _ -> None)

/// Build dependency graph from module declarations (collect open directives per module)
let buildDependencyGraph (decls: Decl list) : Map<string, string list> =
    let rec collectOpens (ds: Decl list) : string list =
        ds |> List.collect (fun d ->
            match d with
            | OpenDecl(path, _) ->
                match path with
                | name :: _ -> [name]
                | [] -> []
            | _ -> [])
    decls
    |> List.choose (fun d ->
        match d with
        | ModuleDecl(name, innerDecls, _) ->
            Some (name, collectOpens innerDecls)
        | _ -> None)
    |> Map.ofList

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
    | RecordExpr(_, fields, _) -> fields |> List.collect (fun (_, e) -> collectMatches e)
    | FieldAccess(e, _, _) -> collectMatches e
    | RecordUpdate(src, fields, _) -> collectMatches src @ (fields |> List.collect (fun (_, e) -> collectMatches e))
    | SetField(e, _, v, _) -> collectMatches e @ collectMatches v
    | Number _ | Bool _ | String _ | Var _ | EmptyList _ | Constructor(_, None, _) -> []

/// Type check a module: build ConstructorEnv and RecordEnv from type declarations,
/// then type check all let declarations with both environments.
/// Returns Ok(warnings, RecordEnv) on success, Error(Diagnostic) on type error.
let typeCheckModule (m: Module) : Result<Diagnostic list * RecordEnv, Diagnostic> =
    try
        match m with
        | EmptyModule _ -> Ok ([], Map.empty)
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

            // Build record environment from record type declarations
            let recEnv =
                decls
                |> List.choose (function
                    | Decl.RecordTypeDecl rd -> Some rd
                    | _ -> None)
                |> List.map elaborateRecordDecl
                |> List.fold (fun acc (name, info) -> Map.add name info acc) Map.empty

            // Validate globally unique field names across all record types
            let allFieldsWithSpans =
                decls
                |> List.choose (function
                    | Decl.RecordTypeDecl (Ast.RecordDecl(typeName, _, fields, _)) ->
                        Some (typeName, fields)
                    | _ -> None)
                |> List.collect (fun (typeName, fields) ->
                    fields |> List.map (fun (Ast.RecordFieldDecl(fname, _, _, fieldSpan)) ->
                        (fname, typeName, fieldSpan)))
            let fieldCounts =
                allFieldsWithSpans
                |> List.groupBy (fun (fname, _, _) -> fname)
                |> List.filter (fun (_, occurrences) -> List.length occurrences > 1)
            match fieldCounts with
            | (fieldName, occurrences) :: _ ->
                let (_, type1, _) = occurrences.[0]
                let (_, type2, span2) = occurrences.[1]
                raise (TypeException {
                    Kind = DuplicateRecordField(fieldName, type1, type2)
                    Span = span2
                    Term = None; ContextStack = []; Trace = [] })
            | [] -> ()

            // Type check let declarations sequentially, accumulating type environment
            let _finalEnv =
                decls
                |> List.choose (function
                    | LetDecl(n, e, _) -> Some(n, e)
                    | _ -> None)
                |> List.fold (fun (env: TypeEnv) (name, body) ->
                    let s, ty = Bidir.synth ctorEnv recEnv [] env body
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
            // For GADT constructors, return the generic type (e.g., Expr<'a>) not
            // the specific result type (e.g., Expr<int>), so exhaustiveness checking works.
            let inferTypeFromPatterns (patterns: Pattern list) : Type option =
                patterns
                |> List.tryPick (fun pat ->
                    match pat with
                    | Ast.ConstructorPat(name, _, _) ->
                        match Map.tryFind name ctorEnv with
                        | Some info ->
                            if info.IsGadt then
                                // For GADT constructors, build generic type from type name
                                match info.ResultType with
                                | TData(typeName, _) ->
                                    Some (TData(typeName, info.TypeParams |> List.map TVar))
                                | _ -> Some info.ResultType
                            else
                                Some info.ResultType
                        | None -> None
                    | _ -> None)

            // Infer the specific (non-genericized) scrutinee type from GADT constructor
            // patterns. Used for filtering impossible branches in exhaustiveness checking.
            let inferSpecificScrutineeType (patterns: Pattern list) : Type option =
                patterns
                |> List.tryPick (fun pat ->
                    match pat with
                    | Ast.ConstructorPat(name, _, _) ->
                        match Map.tryFind name ctorEnv with
                        | Some info when info.IsGadt -> Some info.ResultType
                        | _ -> None
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
                            // For GADT types, filter to only possible constructors
                            // based on the specific scrutinee type
                            let filterType =
                                inferSpecificScrutineeType patterns
                                |> Option.defaultValue scrTy
                            let possibleConstructors =
                                Exhaustive.filterPossibleConstructors ctorEnv filterType constructorSet

                            // Convert AST patterns to CasePat
                            let casePats = patterns |> List.map astPatToCasePat

                            // Check exhaustiveness
                            match Exhaustive.checkExhaustive possibleConstructors casePats with
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
                            match Exhaustive.checkRedundant possibleConstructors casePats with
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

            Ok (warnings, recEnv)
    with
    | TypeException err ->
        Error(typeErrorToDiagnostic err)
