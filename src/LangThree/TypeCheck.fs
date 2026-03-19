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

        // Phase 11: String built-in functions
        // string_length: string -> int
        "string_length", Scheme([], TArrow(TString, TInt))

        // string_concat: string -> string -> string
        "string_concat", Scheme([], TArrow(TString, TArrow(TString, TString)))

        // string_sub: string -> int -> int -> string  (start index, length)
        "string_sub", Scheme([], TArrow(TString, TArrow(TInt, TArrow(TInt, TString))))

        // string_contains: string -> string -> bool
        "string_contains", Scheme([], TArrow(TString, TArrow(TString, TBool)))

        // to_string: 'a -> string  (permissively polymorphic; runtime enforces int/bool/string)
        "to_string", Scheme([0], TArrow(TVar 0, TString))

        // string_to_int: string -> int
        "string_to_int", Scheme([], TArrow(TString, TInt))

        // Phase 12: Output functions
        // print : string -> unit
        "print",   Scheme([], TArrow(TString, TTuple []))

        // println : string -> unit
        "println", Scheme([], TArrow(TString, TTuple []))

        // printf : string -> 'a  (permissively polymorphic — runtime enforces arity from format string)
        "printf", Scheme([0], TArrow(TString, TVar 0))
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
        // Exclude guarded patterns from exhaustiveness checking (they may not match)
        let patterns = clauses |> List.choose (fun (p, guard, _) ->
            match guard with None -> Some p | Some _ -> None)
        let nested =
            collectMatches scrutinee
            @ (clauses |> List.collect (fun (_, _, body) -> collectMatches body))
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
    | Raise(e, _) -> collectMatches e
    | TryWith(body, clauses, _) ->
        collectMatches body
        @ (clauses |> List.collect (fun (_, _, handler) -> collectMatches handler))
    | PipeRight(a, b, _) | ComposeRight(a, b, _) | ComposeLeft(a, b, _) ->
        collectMatches a @ collectMatches b
    | Number _ | Bool _ | String _ | Var _ | EmptyList _ | Constructor(_, None, _) -> []

/// Check exhaustiveness and redundancy warnings for match expressions in a body
let checkMatchWarnings (ctorEnv: ConstructorEnv) (body: Expr) : Diagnostic list =
    let allMatches = collectMatches body

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

    let matchWarnings =
        allMatches
        |> List.collect (fun (patterns, _scrutinee, matchSpan) ->
            let mutable scrWarnings = []

            match inferTypeFromPatterns patterns with
            | Some scrTy ->
                let constructorSet = getConstructorsFromEnv ctorEnv scrTy

                if not (List.isEmpty constructorSet) then
                    let filterType =
                        inferSpecificScrutineeType patterns
                        |> Option.defaultValue scrTy
                    let possibleConstructors =
                        Exhaustive.filterPossibleConstructors ctorEnv filterType constructorSet

                    let casePats = patterns |> List.map astPatToCasePat

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

    // Also check try-with handlers for non-exhaustive exception handling (W0003)
    let tryWithWarnings =
        let rec collectTryWiths (expr: Expr) : (MatchClause list * Span) list =
            match expr with
            | TryWith(body, handlers, span) ->
                (handlers, span) :: collectTryWiths body
                @ (handlers |> List.collect (fun (_, _, h) -> collectTryWiths h))
            | Match(s, clauses, _) ->
                collectTryWiths s @ (clauses |> List.collect (fun (_, _, b) -> collectTryWiths b))
            | Let(_, rhs, body, _) -> collectTryWiths rhs @ collectTryWiths body
            | LetPat(_, rhs, body, _) -> collectTryWiths rhs @ collectTryWiths body
            | LetRec(_, _, rhs, body, _) -> collectTryWiths rhs @ collectTryWiths body
            | Lambda(_, body, _) | LambdaAnnot(_, _, body, _) -> collectTryWiths body
            | App(f, arg, _) -> collectTryWiths f @ collectTryWiths arg
            | If(c, t, e, _) -> collectTryWiths c @ collectTryWiths t @ collectTryWiths e
            | Add(a, b, _) | Subtract(a, b, _) | Multiply(a, b, _) | Divide(a, b, _)
            | Equal(a, b, _) | NotEqual(a, b, _) | LessThan(a, b, _) | GreaterThan(a, b, _)
            | LessEqual(a, b, _) | GreaterEqual(a, b, _) | And(a, b, _) | Or(a, b, _) | Cons(a, b, _) ->
                collectTryWiths a @ collectTryWiths b
            | Negate(e, _) | Annot(e, _, _) -> collectTryWiths e
            | Tuple(es, _) | List(es, _) -> es |> List.collect collectTryWiths
            | Constructor(_, Some arg, _) -> collectTryWiths arg
            | RecordExpr(_, fields, _) -> fields |> List.collect (fun (_, e) -> collectTryWiths e)
            | FieldAccess(e, _, _) -> collectTryWiths e
            | RecordUpdate(src, fields, _) -> collectTryWiths src @ (fields |> List.collect (fun (_, e) -> collectTryWiths e))
            | SetField(e, _, v, _) -> collectTryWiths e @ collectTryWiths v
            | Raise(e, _) -> collectTryWiths e
            | PipeRight(a, b, _) | ComposeRight(a, b, _) | ComposeLeft(a, b, _) ->
                collectTryWiths a @ collectTryWiths b
            | _ -> []

        let allTryWiths = collectTryWiths body

        allTryWiths
        |> List.collect (fun (handlers, span) ->
            // Check if any handler is a catch-all (wildcard or var pattern without guard)
            let hasCatchAll =
                handlers |> List.exists (fun (pat, guard, _) ->
                    match pat, guard with
                    | Ast.WildcardPat _, None -> true
                    | Ast.VarPat _, None -> true
                    | _ -> false)
            if hasCatchAll then []
            else
                [{ Kind = NonExhaustiveExceptionHandler("not all exceptions are handled; consider adding a catch-all handler")
                   Span = span; Term = None; ContextStack = []; Trace = [] }
                 |> typeErrorToDiagnostic])

    matchWarnings @ tryWithWarnings

/// Validate globally unique field names across all record types in a decl list
let validateUniqueRecordFields (decls: Decl list) =
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

/// Collect all module names referenced via qualified access in an expression.
/// Returns set of module names that are accessed as Module.member.
let rec collectModuleRefs (modules: Map<string, ModuleExports>) (expr: Expr) : Set<string> =
    match expr with
    | FieldAccess(Constructor(modName, None, _), _, _) when Map.containsKey modName modules ->
        Set.singleton modName
    | FieldAccess(FieldAccess(Constructor(modName, None, _), subMod, _), _, _) when Map.containsKey modName modules ->
        Set.singleton modName
    | Let(_, rhs, body, _) -> Set.union (collectModuleRefs modules rhs) (collectModuleRefs modules body)
    | LetPat(_, rhs, body, _) -> Set.union (collectModuleRefs modules rhs) (collectModuleRefs modules body)
    | LetRec(_, _, rhs, body, _) -> Set.union (collectModuleRefs modules rhs) (collectModuleRefs modules body)
    | Lambda(_, body, _) | LambdaAnnot(_, _, body, _) -> collectModuleRefs modules body
    | App(f, arg, _) -> Set.union (collectModuleRefs modules f) (collectModuleRefs modules arg)
    | If(c, t, e, _) -> Set.unionMany [collectModuleRefs modules c; collectModuleRefs modules t; collectModuleRefs modules e]
    | Match(s, clauses, _) ->
        let clauseRefs = clauses |> List.map (fun (_, _, body) -> collectModuleRefs modules body)
        Set.unionMany (collectModuleRefs modules s :: clauseRefs)
    | Add(a, b, _) | Subtract(a, b, _) | Multiply(a, b, _) | Divide(a, b, _)
    | Equal(a, b, _) | NotEqual(a, b, _) | LessThan(a, b, _) | GreaterThan(a, b, _)
    | LessEqual(a, b, _) | GreaterEqual(a, b, _) | And(a, b, _) | Or(a, b, _) | Cons(a, b, _) ->
        Set.union (collectModuleRefs modules a) (collectModuleRefs modules b)
    | Negate(e, _) | Annot(e, _, _) -> collectModuleRefs modules e
    | Tuple(es, _) | List(es, _) -> es |> List.map (collectModuleRefs modules) |> Set.unionMany
    | FieldAccess(e, _, _) -> collectModuleRefs modules e
    | RecordExpr(_, fields, _) -> fields |> List.map (fun (_, e) -> collectModuleRefs modules e) |> Set.unionMany
    | RecordUpdate(src, fields, _) ->
        Set.unionMany (collectModuleRefs modules src :: (fields |> List.map (fun (_, e) -> collectModuleRefs modules e)))
    | SetField(e, _, v, _) -> Set.union (collectModuleRefs modules e) (collectModuleRefs modules v)
    | Raise(e, _) -> collectModuleRefs modules e
    | TryWith(body, clauses, _) ->
        let clauseRefs = clauses |> List.map (fun (_, _, handler) -> collectModuleRefs modules handler)
        Set.unionMany (collectModuleRefs modules body :: clauseRefs)
    | Constructor(_, Some arg, _) -> collectModuleRefs modules arg
    | PipeRight(a, b, _) | ComposeRight(a, b, _) | ComposeLeft(a, b, _) ->
        Set.union (collectModuleRefs modules a) (collectModuleRefs modules b)
    | _ -> Set.empty

/// Rewrite qualified module access in an expression tree.
/// Converts Module.member patterns to direct references so Bidir.synth can handle them.
let rec rewriteModuleAccess (modules: Map<string, ModuleExports>) (expr: Expr) : Expr =
    match expr with
    // Module.member where member is a value/function
    | FieldAccess(Constructor(modName, None, _), fieldName, span) when Map.containsKey modName modules ->
        let exports = Map.find modName modules
        // Check if fieldName is a constructor in the module
        if Map.containsKey fieldName exports.CtorEnv then
            Constructor(fieldName, None, span)
        else
            Var(fieldName, span)

    // Outer.Inner.member (chained module access)
    | FieldAccess(FieldAccess(Constructor(modName, None, s1), subModName, s2), fieldName, span)
        when Map.containsKey modName modules ->
        let outerExports = Map.find modName modules
        match Map.tryFind subModName outerExports.SubModules with
        | Some innerExports ->
            if Map.containsKey fieldName innerExports.CtorEnv then
                Constructor(fieldName, None, span)
            else
                Var(fieldName, span)
        | None -> expr  // Let type checker report the error

    // App(Module.Ctor, arg) -- constructor application via qualified access
    | App(FieldAccess(Constructor(modName, None, _), ctorName, s2), arg, appSpan)
        when Map.containsKey modName modules ->
        let exports = Map.find modName modules
        let rewrittenArg = rewriteModuleAccess modules arg
        if Map.containsKey ctorName exports.CtorEnv then
            // Rewrite to Constructor with argument (not App of nullary Constructor)
            Constructor(ctorName, Some rewrittenArg, appSpan)
        else
            App(Var(ctorName, s2), rewrittenArg, appSpan)

    // Recurse into subexpressions
    | Let(n, rhs, body, s) -> Let(n, rewriteModuleAccess modules rhs, rewriteModuleAccess modules body, s)
    | LetPat(p, rhs, body, s) -> LetPat(p, rewriteModuleAccess modules rhs, rewriteModuleAccess modules body, s)
    | LetRec(n, p, rhs, body, s) -> LetRec(n, p, rewriteModuleAccess modules rhs, rewriteModuleAccess modules body, s)
    | Lambda(p, body, s) -> Lambda(p, rewriteModuleAccess modules body, s)
    | LambdaAnnot(p, t, body, s) -> LambdaAnnot(p, t, rewriteModuleAccess modules body, s)
    | App(f, arg, s) -> App(rewriteModuleAccess modules f, rewriteModuleAccess modules arg, s)
    | If(c, t, e, s) -> If(rewriteModuleAccess modules c, rewriteModuleAccess modules t, rewriteModuleAccess modules e, s)
    | Match(scr, clauses, s) ->
        Match(rewriteModuleAccess modules scr,
              clauses |> List.map (fun (p, g, body) -> (p, g, rewriteModuleAccess modules body)), s)
    | Add(a, b, s) -> Add(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | Subtract(a, b, s) -> Subtract(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | Multiply(a, b, s) -> Multiply(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | Divide(a, b, s) -> Divide(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | Equal(a, b, s) -> Equal(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | NotEqual(a, b, s) -> NotEqual(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | LessThan(a, b, s) -> LessThan(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | GreaterThan(a, b, s) -> GreaterThan(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | LessEqual(a, b, s) -> LessEqual(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | GreaterEqual(a, b, s) -> GreaterEqual(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | And(a, b, s) -> And(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | Or(a, b, s) -> Or(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | Cons(a, b, s) -> Cons(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | Negate(e, s) -> Negate(rewriteModuleAccess modules e, s)
    | Annot(e, t, s) -> Annot(rewriteModuleAccess modules e, t, s)
    | Tuple(es, s) -> Tuple(es |> List.map (rewriteModuleAccess modules), s)
    | List(es, s) -> List(es |> List.map (rewriteModuleAccess modules), s)
    | FieldAccess(e, f, s) -> FieldAccess(rewriteModuleAccess modules e, f, s)
    | RecordExpr(base_, fields, s) ->
        RecordExpr(base_, fields |> List.map (fun (n, e) -> (n, rewriteModuleAccess modules e)), s)
    | RecordUpdate(src, fields, s) ->
        RecordUpdate(rewriteModuleAccess modules src, fields |> List.map (fun (n, e) -> (n, rewriteModuleAccess modules e)), s)
    | SetField(e, f, v, s) -> SetField(rewriteModuleAccess modules e, f, rewriteModuleAccess modules v, s)
    | Raise(e, s) -> Raise(rewriteModuleAccess modules e, s)
    | TryWith(body, clauses, s) ->
        TryWith(rewriteModuleAccess modules body,
                clauses |> List.map (fun (p, g, handler) -> (p, g, rewriteModuleAccess modules handler)), s)
    | Constructor(n, Some arg, s) -> Constructor(n, Some(rewriteModuleAccess modules arg), s)
    | PipeRight(a, b, s) -> PipeRight(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | ComposeRight(a, b, s) -> ComposeRight(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | ComposeLeft(a, b, s) -> ComposeLeft(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | _ -> expr  // Literals, Var, Constructor(None), EmptyList -- no rewrite needed

/// Merge module exports into type/constructor environments for qualified access type checking.
let mergeModuleExportsForTypeCheck
    (modules: Map<string, ModuleExports>)
    (referencedModules: Set<string>)
    (env: TypeEnv) (ctorEnv: ConstructorEnv) (recEnv: RecordEnv)
    : TypeEnv * ConstructorEnv * RecordEnv =
    referencedModules
    |> Set.fold (fun (e, c, r) modName ->
        match Map.tryFind modName modules with
        | Some exports ->
            let e' = Map.fold (fun acc k v -> Map.add k v acc) e exports.TypeEnv
            let c' = Map.fold (fun acc k v -> Map.add k v acc) c exports.CtorEnv
            let r' = Map.fold (fun acc k v -> Map.add k v acc) r exports.RecEnv
            // Also merge submodule exports
            exports.SubModules
            |> Map.fold (fun (e2, c2, r2) _ subExports ->
                let e3 = Map.fold (fun acc k v -> Map.add k v acc) e2 subExports.TypeEnv
                let c3 = Map.fold (fun acc k v -> Map.add k v acc) c2 subExports.CtorEnv
                let r3 = Map.fold (fun acc k v -> Map.add k v acc) r2 subExports.RecEnv
                (e3, c3, r3)) (e', c', r')
        | None -> (e, c, r)) (env, ctorEnv, recEnv)

/// Type check declarations sequentially, building up environments and collecting warnings.
/// Returns (typeEnv, ctorEnv, recEnv, modules, warnings)
let rec typeCheckDecls
    (decls: Decl list)
    (typeEnv: TypeEnv) (ctorEnv: ConstructorEnv) (recEnv: RecordEnv)
    (modules: Map<string, ModuleExports>)
    : TypeEnv * ConstructorEnv * RecordEnv * Map<string, ModuleExports> * Diagnostic list =

    // First pass: collect all type and record declarations for the scope
    let ctorEnv =
        decls
        |> List.choose (function
            | Decl.TypeDecl td -> Some td
            | _ -> None)
        |> List.map elaborateTypeDecl
        |> List.fold (fun acc map ->
            Map.fold (fun acc' k v -> Map.add k v acc') acc map) ctorEnv

    let recEnv =
        decls
        |> List.choose (function
            | Decl.RecordTypeDecl rd -> Some rd
            | _ -> None)
        |> List.map elaborateRecordDecl
        |> List.fold (fun acc (name, info) -> Map.add name info acc) recEnv

    // First pass: collect exception declarations into ctorEnv and typeEnv
    let ctorEnv, typeEnv =
        decls
        |> List.fold (fun (cEnv, tEnv) decl ->
            match decl with
            | ExceptionDecl(name, dataTypeOpt, _) ->
                let (ctorName, ctorInfo) = Elaborate.elaborateExceptionDecl name dataTypeOpt
                let cEnv' = Map.add ctorName ctorInfo cEnv
                // Add exception constructor to type env as a function
                let scheme =
                    match ctorInfo.ArgType with
                    | Some argTy -> Scheme([], TArrow(argTy, TExn))
                    | None -> Scheme([], TExn)
                let tEnv' = Map.add ctorName scheme tEnv
                (cEnv', tEnv')
            | _ -> (cEnv, tEnv)
        ) (ctorEnv, typeEnv)

    // Validate globally unique field names
    validateUniqueRecordFields decls

    // Second pass: process declarations sequentially
    let (typeEnv', ctorEnv', recEnv', modules', warnings') =
        decls
        |> List.fold (fun (env, cEnv, rEnv, mods, warns) decl ->
            match decl with
            | LetDecl(name, body, _) ->
                // Resolve qualified module access before type checking
                let refsInBody = collectModuleRefs mods body
                let rewrittenBody = rewriteModuleAccess mods body
                let (envForSynth, ctorEnvForSynth, recEnvForSynth) =
                    if Set.isEmpty refsInBody then (env, cEnv, rEnv)
                    else mergeModuleExportsForTypeCheck mods refsInBody env cEnv rEnv
                // Type check the let binding (with module-resolved body)
                let s, ty = Bidir.synth ctorEnvForSynth recEnvForSynth [] envForSynth rewrittenBody
                let ty' = apply s ty
                let scheme = generalize (applyEnv s env) ty'
                let env' = Map.add name scheme env
                // Collect match warnings from this let body
                let matchWarnings = checkMatchWarnings cEnv body
                (env', cEnv, rEnv, mods, warns @ matchWarnings)

            | Decl.TypeDecl _ | Decl.RecordTypeDecl _ | ExceptionDecl _ ->
                // Already processed in first pass (ExceptionDecl: TODO in Plan 02)
                (env, cEnv, rEnv, mods, warns)

            | TypeAliasDecl _ ->
                // Type aliases are transparent -- they don't create new types.
                // TEName "AliasName" in type annotations elaborates to a fresh TVar
                // which unifies with the actual type at use sites.
                (env, cEnv, rEnv, mods, warns)

            | ModuleDecl(name, innerDecls, span) ->
                // Check duplicate module name
                if Map.containsKey name mods then
                    raise (TypeException {
                        Kind = DuplicateModuleName name
                        Span = span; Term = None; ContextStack = []; Trace = [] })
                // Recurse into inner declarations
                let (innerTypeEnv, innerCtorEnv, innerRecEnv, innerMods, innerWarns) =
                    typeCheckDecls innerDecls env cEnv rEnv mods
                // Build module exports (only bindings defined in this module, not inherited)
                let moduleTypeEnv =
                    Map.fold (fun acc k v ->
                        if Map.containsKey k env then acc
                        else Map.add k v acc) Map.empty innerTypeEnv
                let moduleCtorEnv =
                    Map.fold (fun acc k v ->
                        if Map.containsKey k cEnv then acc
                        else Map.add k v acc) Map.empty innerCtorEnv
                let moduleRecEnv =
                    Map.fold (fun acc k v ->
                        if Map.containsKey k rEnv then acc
                        else Map.add k v acc) Map.empty innerRecEnv
                let exports = {
                    TypeEnv = moduleTypeEnv
                    CtorEnv = moduleCtorEnv
                    RecEnv = moduleRecEnv
                    SubModules = innerMods
                }
                (env, cEnv, rEnv, Map.add name exports mods, warns @ innerWarns)

            | OpenDecl(path, span) ->
                // Look up module in current modules map
                match path with
                | [name] ->
                    match Map.tryFind name mods with
                    | None ->
                        raise (TypeException {
                            Kind = ForwardModuleReference name
                            Span = span; Term = None; ContextStack = []; Trace = [] })
                    | Some exports ->
                        let (env', cEnv', rEnv') = openModuleExports exports env cEnv rEnv
                        (env', cEnv', rEnv', mods, warns)
                | _ ->
                    let exports = resolveModule mods path span
                    let (env', cEnv', rEnv') = openModuleExports exports env cEnv rEnv
                    (env', cEnv', rEnv', mods, warns)

            | NamespaceDecl(_path, innerDecls, _span) ->
                // Namespace is just a naming prefix, process inner decls in current scope
                let (env', cEnv'', rEnv'', mods', innerWarns) =
                    innerDecls
                    |> List.fold (fun (e, ce, re, ms, ws) d ->
                        match d with
                        | LetDecl(n, body, _) ->
                            let refsInBody = collectModuleRefs ms body
                            let rewrittenBody = rewriteModuleAccess ms body
                            let (eForSynth, cForSynth, rForSynth) =
                                if Set.isEmpty refsInBody then (e, ce, re)
                                else mergeModuleExportsForTypeCheck ms refsInBody e ce re
                            let s, ty = Bidir.synth cForSynth rForSynth [] eForSynth rewrittenBody
                            let ty' = apply s ty
                            let scheme = generalize (applyEnv s e) ty'
                            let matchWarnings = checkMatchWarnings ce body
                            (Map.add n scheme e, ce, re, ms, ws @ matchWarnings)
                        | ModuleDecl(name, mInnerDecls, span) ->
                            if Map.containsKey name ms then
                                raise (TypeException {
                                    Kind = DuplicateModuleName name
                                    Span = span; Term = None; ContextStack = []; Trace = [] })
                            let (iEnv, iCtor, iRec, iMods, iWarns) =
                                typeCheckDecls mInnerDecls e ce re ms
                            let mExports = {
                                TypeEnv = Map.fold (fun acc k v -> if Map.containsKey k e then acc else Map.add k v acc) Map.empty iEnv
                                CtorEnv = Map.fold (fun acc k v -> if Map.containsKey k ce then acc else Map.add k v acc) Map.empty iCtor
                                RecEnv = Map.fold (fun acc k v -> if Map.containsKey k re then acc else Map.add k v acc) Map.empty iRec
                                SubModules = iMods
                            }
                            (e, ce, re, Map.add name mExports ms, ws @ iWarns)
                        | _ -> (e, ce, re, ms, ws)
                    ) (env, cEnv, rEnv, mods, warns)
                (env', cEnv'', rEnv'', mods', innerWarns)
        ) (typeEnv, ctorEnv, recEnv, modules, [])

    (typeEnv', ctorEnv', recEnv', modules', warnings')

/// Type check a module: build environments from declarations,
/// type check all bindings with exhaustiveness/redundancy checking.
/// Returns Ok(warnings, RecordEnv, modules) on success, Error(Diagnostic) on type error.
let typeCheckModuleWithPrelude
    (preludeCtorEnv: ConstructorEnv) (preludeRecEnv: RecordEnv) (preludeTypeEnv: TypeEnv)
    (m: Module)
    : Result<Diagnostic list * ConstructorEnv * RecordEnv * Map<string, ModuleExports> * TypeEnv, Diagnostic> =
    try
        match m with
        | EmptyModule _ -> Ok ([], Map.empty, Map.empty, Map.empty, Map.empty)
        | Module (decls, _) | NamedModule(_, decls, _) | NamespacedModule(_, decls, _) ->
            // Check for circular module dependencies
            let depGraph = buildDependencyGraph decls
            match detectCircularDeps depGraph with
            | Some cycle ->
                raise (TypeException {
                    Kind = CircularModuleDependency cycle
                    Span = unknownSpan; Term = None; ContextStack = []; Trace = [] })
            | None -> ()

            let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) initialTypeEnv preludeTypeEnv
            let (typeEnv, ctorEnv, recEnv, modules, warnings) =
                typeCheckDecls decls mergedTypeEnv preludeCtorEnv preludeRecEnv Map.empty
            Ok (warnings, ctorEnv, recEnv, modules, typeEnv)
    with
    | TypeException err ->
        Error(typeErrorToDiagnostic err)

let typeCheckModule (m: Module) : Result<Diagnostic list * RecordEnv * Map<string, ModuleExports> * TypeEnv, Diagnostic> =
    match typeCheckModuleWithPrelude Map.empty Map.empty Map.empty m with
    | Ok (warnings, _ctorEnv, recEnv, modules, typeEnv) -> Ok (warnings, recEnv, modules, typeEnv)
    | Error e -> Error e
