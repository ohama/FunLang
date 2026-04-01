module TypeCheck

open System.IO
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
        // Prelude functions (map, filter, fold, length, reverse, append, hd, tl,
        // id, const_, compose) are now defined in Prelude/*.fun files.
        // They are type-checked and evaluated from those files at startup.

        // Phase 11: String built-in functions
        // string_length: string -> int
        "string_length", Scheme([], [], TArrow(TString, TInt))

        // string_concat: string -> string -> string
        "string_concat", Scheme([], [], TArrow(TString, TArrow(TString, TString)))

        // string_sub: string -> int -> int -> string  (start index, length)
        "string_sub", Scheme([], [], TArrow(TString, TArrow(TInt, TArrow(TInt, TString))))

        // string_contains: string -> string -> bool
        "string_contains", Scheme([], [], TArrow(TString, TArrow(TString, TBool)))

        // Phase 60: String operation builtins (BLT-01, BLT-02, BLT-03)
        "string_endswith",   Scheme([], [], TArrow(TString, TArrow(TString, TBool)))
        "string_startswith", Scheme([], [], TArrow(TString, TArrow(TString, TBool)))
        "string_trim",       Scheme([], [], TArrow(TString, TString))

        // to_string: 'a -> string  (permissively polymorphic; runtime enforces int/bool/string)
        "to_string", Scheme([0], [], TArrow(TVar 0, TString))

        // string_to_int: string -> int
        "string_to_int", Scheme([], [], TArrow(TString, TInt))

        // Phase 12: Output functions
        // print : string -> unit
        "print",   Scheme([], [], TArrow(TString, TTuple []))

        // println : string -> unit
        "println", Scheme([], [], TArrow(TString, TTuple []))

        // printf : string -> 'a  (permissively polymorphic — runtime enforces arity from format string)
        "printf", Scheme([0], [], TArrow(TString, TVar 0))

        // printfn : string -> 'a  (like printf but appends newline)
        "printfn", Scheme([0], [], TArrow(TString, TVar 0))

        // sprintf : string -> 'a  (like printf but returns string; runtime enforces arity)
        "sprintf", Scheme([0], [], TArrow(TString, TVar 0))

        // eprintfn : string -> 'a  (like printfn but writes to stderr)
        "eprintfn", Scheme([0], [], TArrow(TString, TVar 0))

        // failwith : string -> 'a  (polymorphic return — unifies with any expected type, like raise)
        "failwith", Scheme([0], [], TArrow(TString, TVar 0))

        // Phase 29: char conversion builtins
        // char_to_int : char -> int
        "char_to_int", Scheme([], [], TArrow(TChar, TInt))
        // int_to_char : int -> char
        "int_to_char", Scheme([], [], TArrow(TInt, TChar))

        // Phase 55: Char module builtins (STR-02)
        "char_is_digit",  Scheme([], [], TArrow(TChar, TBool))
        "char_to_upper",  Scheme([], [], TArrow(TChar, TChar))
        "char_is_letter", Scheme([], [], TArrow(TChar, TBool))
        "char_is_upper",  Scheme([], [], TArrow(TChar, TBool))
        "char_is_lower",  Scheme([], [], TArrow(TChar, TBool))
        "char_to_lower",  Scheme([], [], TArrow(TChar, TChar))

        // Phase 55: String.concat builtin (STR-03)
        "string_concat_list", Scheme([], [], TArrow(TString, TArrow(TList TString, TString)))

        // Phase 32: File I/O builtins (STD-02 through STD-09)
        // STD-02: read_file : string -> string
        "read_file", Scheme([], [], TArrow(TString, TString))

        // STD-03: stdin_read_all : unit -> string
        "stdin_read_all", Scheme([], [], TArrow(TTuple [], TString))

        // STD-04: stdin_read_line : unit -> string
        "stdin_read_line", Scheme([], [], TArrow(TTuple [], TString))

        // STD-05: write_file : string -> string -> unit
        "write_file", Scheme([], [], TArrow(TString, TArrow(TString, TTuple [])))

        // STD-06: append_file : string -> string -> unit
        "append_file", Scheme([], [], TArrow(TString, TArrow(TString, TTuple [])))

        // STD-07: file_exists : string -> bool
        "file_exists", Scheme([], [], TArrow(TString, TBool))

        // STD-08: read_lines : string -> string list
        "read_lines", Scheme([], [], TArrow(TString, TList TString))

        // STD-09: write_lines : string -> string list -> unit
        "write_lines", Scheme([], [], TArrow(TString, TArrow(TList TString, TTuple [])))

        // Phase 32: System builtins (STD-10 through STD-15)
        // STD-10: get_args : unit -> string list
        "get_args", Scheme([], [], TArrow(TTuple [], TList TString))

        // STD-11: get_env : string -> string
        "get_env", Scheme([], [], TArrow(TString, TString))

        // STD-12: get_cwd : unit -> string
        "get_cwd", Scheme([], [], TArrow(TTuple [], TString))

        // STD-13: path_combine : string -> string -> string
        "path_combine", Scheme([], [], TArrow(TString, TArrow(TString, TString)))

        // STD-14: dir_files : string -> string list
        "dir_files", Scheme([], [], TArrow(TString, TList TString))

        // STD-15: eprint : string -> unit
        "eprint",   Scheme([], [], TArrow(TString, TTuple []))
        // STD-15: eprintln : string -> unit
        "eprintln", Scheme([], [], TArrow(TString, TTuple []))

        // Phase 38: Array builtins (ARR-01 through ARR-06)
        // array_create : int -> 'a -> 'a array
        "array_create", Scheme([0], [], TArrow(TInt, TArrow(TVar 0, TArray (TVar 0))))
        // array_get : 'a array -> int -> 'a
        "array_get",    Scheme([0], [], TArrow(TArray (TVar 0), TArrow(TInt, TVar 0)))
        // array_set : 'a array -> int -> 'a -> unit
        "array_set",    Scheme([0], [], TArrow(TArray (TVar 0), TArrow(TInt, TArrow(TVar 0, TTuple []))))
        // array_length : 'a array -> int
        "array_length", Scheme([0], [], TArrow(TArray (TVar 0), TInt))
        // array_of_list : 'a list -> 'a array
        "array_of_list", Scheme([0], [], TArrow(TList (TVar 0), TArray (TVar 0)))
        // array_to_list : 'a array -> 'a list
        "array_to_list", Scheme([0], [], TArrow(TArray (TVar 0), TList (TVar 0)))

        // Phase 40: Array higher-order function builtins (ARR-07 through ARR-10)
        // array_iter : ('a -> unit) -> 'a array -> unit
        "array_iter",  Scheme([0], [], TArrow(TArrow(TVar 0, TTuple []), TArrow(TArray (TVar 0), TTuple [])))
        // array_map : ('a -> 'b) -> 'a array -> 'b array
        "array_map",   Scheme([0; 1], [], TArrow(TArrow(TVar 0, TVar 1), TArrow(TArray (TVar 0), TArray (TVar 1))))
        // array_fold : ('acc -> 'a -> 'acc) -> 'acc -> 'a array -> 'acc
        "array_fold",  Scheme([0; 1], [], TArrow(TArrow(TVar 0, TArrow(TVar 1, TVar 0)), TArrow(TVar 0, TArrow(TArray (TVar 1), TVar 0))))
        // array_init : int -> (int -> 'a) -> 'a array
        "array_init",  Scheme([0], [], TArrow(TInt, TArrow(TArrow(TInt, TVar 0), TArray (TVar 0))))

        // Phase 39: Hashtable builtins (HT-01 through HT-06)
        // hashtable_create : unit -> hashtable<'k, 'v>
        "hashtable_create",    Scheme([0; 1], [], TArrow(TTuple [], THashtable (TVar 0, TVar 1)))
        // hashtable_get : hashtable<'k, 'v> -> 'k -> 'v
        "hashtable_get",       Scheme([0; 1], [], TArrow(THashtable (TVar 0, TVar 1), TArrow(TVar 0, TVar 1)))
        // hashtable_set : hashtable<'k, 'v> -> 'k -> 'v -> unit
        "hashtable_set",       Scheme([0; 1], [], TArrow(THashtable (TVar 0, TVar 1), TArrow(TVar 0, TArrow(TVar 1, TTuple []))))
        // hashtable_containsKey : hashtable<'k, 'v> -> 'k -> bool
        "hashtable_containsKey", Scheme([0; 1], [], TArrow(THashtable (TVar 0, TVar 1), TArrow(TVar 0, TBool)))
        // hashtable_keys : hashtable<'k, 'v> -> 'k list
        "hashtable_keys",      Scheme([0; 1], [], TArrow(THashtable (TVar 0, TVar 1), TList (TVar 0)))
        // hashtable_remove : hashtable<'k, 'v> -> 'k -> unit
        "hashtable_remove",    Scheme([0; 1], [], TArrow(THashtable (TVar 0, TVar 1), TArrow(TVar 0, TTuple [])))

        // Phase 60: Hashtable operation builtins (BLT-04, BLT-05)
        "hashtable_trygetvalue", Scheme([0; 1], [], TArrow(THashtable (TVar 0, TVar 1), TArrow(TVar 0, TTuple [TBool; TVar 1])))
        "hashtable_count",       Scheme([0; 1], [], TArrow(THashtable (TVar 0, TVar 1), TInt))

        // Phase 55: StringBuilder builtins
        // stringbuilder_create : unit -> StringBuilder
        "stringbuilder_create",   Scheme([], [], TArrow(TTuple [], TData("StringBuilder", [])))
        // stringbuilder_append : StringBuilder -> string -> StringBuilder
        "stringbuilder_append",   Scheme([0], [], TArrow(TData("StringBuilder", []), TArrow(TVar 0, TData("StringBuilder", []))))
        // stringbuilder_tostring : StringBuilder -> string
        "stringbuilder_tostring", Scheme([], [], TArrow(TData("StringBuilder", []), TString))

        // Phase 56: HashSet builtins
        // hashset_create : unit -> HashSet
        "hashset_create",    Scheme([], [], TArrow(TTuple [], TData("HashSet", [])))
        // hashset_add : HashSet -> 'a -> bool
        "hashset_add",       Scheme([0], [], TArrow(TData("HashSet", []), TArrow(TVar 0, TBool)))
        // hashset_contains : HashSet -> 'a -> bool
        "hashset_contains",  Scheme([0], [], TArrow(TData("HashSet", []), TArrow(TVar 0, TBool)))
        // hashset_count : HashSet -> int
        "hashset_count",     Scheme([], [], TArrow(TData("HashSet", []), TInt))

        // Phase 56: Queue builtins
        // queue_create : unit -> Queue
        "queue_create",    Scheme([], [], TArrow(TTuple [], TData("Queue", [])))
        // queue_enqueue : Queue -> 'a -> unit
        "queue_enqueue",   Scheme([0], [], TArrow(TData("Queue", []), TArrow(TVar 0, TTuple [])))
        // queue_dequeue : Queue -> unit -> 'a
        "queue_dequeue",   Scheme([0], [], TArrow(TData("Queue", []), TArrow(TTuple [], TVar 0)))
        // queue_count : Queue -> int
        "queue_count",     Scheme([], [], TArrow(TData("Queue", []), TInt))

        // Phase 57: MutableList builtins
        // mutablelist_create : unit -> MutableList
        "mutablelist_create",  Scheme([], [], TArrow(TTuple [], TData("MutableList", [])))
        // mutablelist_add : MutableList -> 'a -> unit
        "mutablelist_add",     Scheme([0], [], TArrow(TData("MutableList", []), TArrow(TVar 0, TTuple [])))
        // mutablelist_get : MutableList -> int -> 'a
        "mutablelist_get",     Scheme([0], [], TArrow(TData("MutableList", []), TArrow(TInt, TVar 0)))
        // mutablelist_set : MutableList -> int -> 'a -> unit
        "mutablelist_set",     Scheme([0], [], TArrow(TData("MutableList", []), TArrow(TInt, TArrow(TVar 0, TTuple []))))
        // mutablelist_count : MutableList -> int
        "mutablelist_count",   Scheme([], [], TArrow(TData("MutableList", []), TInt))
        // list_sort_by : ('a -> 'b) -> 'a list -> 'a list
        "list_sort_by",        Scheme([0; 1], [], TArrow(TArrow(TVar 0, TVar 1), TArrow(TList (TVar 0), TList (TVar 0))))
        // list_of_seq : 'a -> 'b list  (accepts any seq-like collection: list, array, HashSet, Queue, MutableList)
        "list_of_seq",         Scheme([0; 1], [], TArrow(TVar 0, TList (TVar 1)))
        // array_sort : 'a array -> unit
        "array_sort",          Scheme([0], [], TArrow(TArray (TVar 0), TTuple []))
        // array_of_seq : 'a -> 'b array  (accepts any seq-like collection: list, array, HashSet, Queue, MutableList)
        "array_of_seq",        Scheme([0; 1], [], TArrow(TVar 0, TArray (TVar 1)))
    ]

/// Module exports: collected type/constructor/record environments from a module
type ModuleExports = {
    TypeEnv: TypeEnv
    CtorEnv: ConstructorEnv
    RecEnv: RecordEnv
    ClassEnv: ClassEnv
    InstanceEnv: InstanceEnv
    SubModules: Map<string, ModuleExports>
}

let emptyModuleExports = {
    TypeEnv = Map.empty; CtorEnv = Map.empty
    RecEnv = Map.empty; ClassEnv = Map.empty; InstanceEnv = Map.empty
    SubModules = Map.empty
}

/// Merge module exports into current environments (for open directives)
let openModuleExports (exports: ModuleExports)
                      (typeEnv: TypeEnv) (ctorEnv: ConstructorEnv) (recEnv: RecordEnv)
                      (classEnv: ClassEnv) (instEnv: InstanceEnv)
    : TypeEnv * ConstructorEnv * RecordEnv * ClassEnv * InstanceEnv =
    let typeEnv' = Map.fold (fun acc k v -> Map.add k v acc) typeEnv exports.TypeEnv
    let ctorEnv' = Map.fold (fun acc k v -> Map.add k v acc) ctorEnv exports.CtorEnv
    let recEnv' = Map.fold (fun acc k v -> Map.add k v acc) recEnv exports.RecEnv
    let classEnv' = Map.fold (fun acc k v -> Map.add k v acc) classEnv exports.ClassEnv
    let instEnv' =
        Map.fold (fun acc k v ->
            let existing = Map.tryFind k acc |> Option.defaultValue []
            Map.add k (v @ existing) acc) instEnv exports.InstanceEnv
    // Update Bidir mutable refs so constraint resolution sees the opened classes/instances
    Bidir.currentClassEnv <- classEnv'
    Bidir.currentInstEnv <- instEnv'
    (typeEnv', ctorEnv', recEnv', classEnv', instEnv')

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
                    Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
        | name :: rest ->
            match Map.tryFind name mods with
            | Some exports -> resolve exports.SubModules rest
            | None ->
                raise (TypeException {
                    Kind = UnresolvedModule name
                    Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
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
let typecheckWithDiagnostic (expr: Expr): Result<Type, Diagnostic list> =
    try
        let ty = synthTop initialTypeEnv expr
        Ok(ty)
    with
    | TypeException err ->
        Error([typeErrorToDiagnostic err])

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
    | LetRec(bindings, body, _) ->
        (bindings |> List.collect (fun (_, _, _, rhs, _) -> collectMatches rhs)) @ collectMatches body
    | Lambda(_, body, _) | LambdaAnnot(_, _, body, _) -> collectMatches body
    | App(f, arg, _) -> collectMatches f @ collectMatches arg
    | If(cond, thenE, elseE, _) -> collectMatches cond @ collectMatches thenE @ collectMatches elseE
    | Add(a, b, _) | Subtract(a, b, _) | Multiply(a, b, _) | Divide(a, b, _) | Modulo(a, b, _) ->
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
    | LetMut(_, rhs, body, _) -> collectMatches rhs @ collectMatches body
    | Assign(_, value, _) -> collectMatches value
    | Raise(e, _) -> collectMatches e
    | TryWith(body, clauses, _) ->
        collectMatches body
        @ (clauses |> List.collect (fun (_, _, handler) -> collectMatches handler))
    | PipeRight(a, b, _) | ComposeRight(a, b, _) | ComposeLeft(a, b, _) ->
        collectMatches a @ collectMatches b
    | Range(start, stop, stepOpt, _) ->
        collectMatches start @ collectMatches stop @ (stepOpt |> Option.map collectMatches |> Option.defaultValue [])
    // Phase 46 (Loop Constructs)
    | WhileExpr(cond, body, _) -> collectMatches cond @ collectMatches body
    | ForExpr(_, start, _, stop, body, _) -> collectMatches start @ collectMatches stop @ collectMatches body
    | ForInExpr(_, coll, body, _) -> collectMatches coll @ collectMatches body
    // Phase 47 (Array/Hashtable Indexing)
    | IndexGet(coll, idx, _) -> collectMatches coll @ collectMatches idx
    | IndexSet(coll, idx, v, _) -> collectMatches coll @ collectMatches idx @ collectMatches v
    // Phase 58 (String slicing / list comprehension)
    | StringSliceExpr(str, start, stopOpt, _) ->
        collectMatches str @ collectMatches start @ (stopOpt |> Option.map collectMatches |> Option.defaultValue [])
    | ListCompExpr(_, coll, body, _) -> collectMatches coll @ collectMatches body
    | Number _ | Bool _ | String _ | Char _ | Var _ | EmptyList _ | Constructor(_, None, _) -> []

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
                              Trace = []; Scope = [] }
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
                                  Trace = []; Scope = [] }
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
            | LetRec(bindings, body, _) ->
                (bindings |> List.collect (fun (_, _, _, rhs, _) -> collectTryWiths rhs)) @ collectTryWiths body
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
            // Phase 47 (Array/Hashtable Indexing)
            | IndexGet(coll, idx, _) -> collectTryWiths coll @ collectTryWiths idx
            | IndexSet(coll, idx, v, _) -> collectTryWiths coll @ collectTryWiths idx @ collectTryWiths v
            // Phase 58 (String slicing / list comprehension)
            | StringSliceExpr(str, start, stopOpt, _) ->
                collectTryWiths str @ collectTryWiths start @ (stopOpt |> Option.map collectTryWiths |> Option.defaultValue [])
            | ListCompExpr(_, coll, body, _) -> collectTryWiths coll @ collectTryWiths body
            | LetMut(_, rhs, body, _) -> collectTryWiths rhs @ collectTryWiths body
            | Assign(_, value, _) -> collectTryWiths value
            | Raise(e, _) -> collectTryWiths e
            | PipeRight(a, b, _) | ComposeRight(a, b, _) | ComposeLeft(a, b, _) ->
                collectTryWiths a @ collectTryWiths b
            | Range(start, stop, stepOpt, _) ->
                collectTryWiths start @ collectTryWiths stop @ (stepOpt |> Option.map collectTryWiths |> Option.defaultValue [])
            | ForInExpr(_, coll, body, _) -> collectTryWiths coll @ collectTryWiths body
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
                   Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] }
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
            Term = None; ContextStack = []; Trace = []; Scope = [] })
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
    | LetRec(bindings, body, _) ->
        let bindingRefs = bindings |> List.map (fun (_, _, _, rhs, _) -> collectModuleRefs modules rhs) |> Set.unionMany
        Set.union bindingRefs (collectModuleRefs modules body)
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
    // Phase 47 (Array/Hashtable Indexing)
    | IndexGet(coll, idx, _) -> Set.union (collectModuleRefs modules coll) (collectModuleRefs modules idx)
    | IndexSet(coll, idx, v, _) -> Set.unionMany [collectModuleRefs modules coll; collectModuleRefs modules idx; collectModuleRefs modules v]
    | LetMut(_, rhs, body, _) -> Set.union (collectModuleRefs modules rhs) (collectModuleRefs modules body)
    | Assign(_, value, _) -> collectModuleRefs modules value
    | Raise(e, _) -> collectModuleRefs modules e
    | TryWith(body, clauses, _) ->
        let clauseRefs = clauses |> List.map (fun (_, _, handler) -> collectModuleRefs modules handler)
        Set.unionMany (collectModuleRefs modules body :: clauseRefs)
    | Constructor(_, Some arg, _) -> collectModuleRefs modules arg
    | PipeRight(a, b, _) | ComposeRight(a, b, _) | ComposeLeft(a, b, _) ->
        Set.union (collectModuleRefs modules a) (collectModuleRefs modules b)
    | Range(start, stop, stepOpt, _) ->
        Set.unionMany [collectModuleRefs modules start; collectModuleRefs modules stop; stepOpt |> Option.map (collectModuleRefs modules) |> Option.defaultValue Set.empty]
    | ForInExpr(_, coll, body, _) -> Set.union (collectModuleRefs modules coll) (collectModuleRefs modules body)
    // Phase 58 (String slicing / list comprehension)
    | StringSliceExpr(str, start, stopOpt, _) ->
        Set.unionMany [collectModuleRefs modules str; collectModuleRefs modules start; stopOpt |> Option.map (collectModuleRefs modules) |> Option.defaultValue Set.empty]
    | ListCompExpr(_, coll, body, _) -> Set.union (collectModuleRefs modules coll) (collectModuleRefs modules body)
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
        | None ->
            raise (TypeException {
                Kind = UnresolvedModule (sprintf "%s.%s" modName subModName)
                Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })

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
    | LetRec(bindings, body, s) -> LetRec(bindings |> List.map (fun (n, p, pty, rhs, bs) -> (n, p, pty, rewriteModuleAccess modules rhs, bs)), rewriteModuleAccess modules body, s)
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
    | Modulo(a, b, s) -> Modulo(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
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
    // Phase 47 (Array/Hashtable Indexing)
    | IndexGet(coll, idx, s) -> IndexGet(rewriteModuleAccess modules coll, rewriteModuleAccess modules idx, s)
    | IndexSet(coll, idx, v, s) -> IndexSet(rewriteModuleAccess modules coll, rewriteModuleAccess modules idx, rewriteModuleAccess modules v, s)
    | LetMut(n, rhs, body, s) -> LetMut(n, rewriteModuleAccess modules rhs, rewriteModuleAccess modules body, s)
    | ForInExpr(var, coll, body, s) -> ForInExpr(var, rewriteModuleAccess modules coll, rewriteModuleAccess modules body, s)
    // Phase 58 (String slicing / list comprehension)
    | StringSliceExpr(str, start, stopOpt, s) ->
        StringSliceExpr(rewriteModuleAccess modules str, rewriteModuleAccess modules start, stopOpt |> Option.map (rewriteModuleAccess modules), s)
    | ListCompExpr(var, coll, body, s) ->
        ListCompExpr(var, rewriteModuleAccess modules coll, rewriteModuleAccess modules body, s)
    | Assign(n, value, s) -> Assign(n, rewriteModuleAccess modules value, s)
    | Raise(e, s) -> Raise(rewriteModuleAccess modules e, s)
    | TryWith(body, clauses, s) ->
        TryWith(rewriteModuleAccess modules body,
                clauses |> List.map (fun (p, g, handler) -> (p, g, rewriteModuleAccess modules handler)), s)
    | Constructor(n, Some arg, s) -> Constructor(n, Some(rewriteModuleAccess modules arg), s)
    | PipeRight(a, b, s) -> PipeRight(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | ComposeRight(a, b, s) -> ComposeRight(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | ComposeLeft(a, b, s) -> ComposeLeft(rewriteModuleAccess modules a, rewriteModuleAccess modules b, s)
    | Range(start, stop, stepOpt, s) ->
        Range(rewriteModuleAccess modules start, rewriteModuleAccess modules stop, stepOpt |> Option.map (rewriteModuleAccess modules), s)
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

/// Resolve an import path relative to the importing file's directory.
/// Absolute paths are returned as-is. Relative paths are resolved relative to
/// the importing file's directory.
let resolveImportPath (importPath: string) (importingFile: string) : string =
    if Path.IsPathRooted importPath then
        importPath
    else
        let dir = Path.GetDirectoryName(Path.GetFullPath(importingFile))
        Path.GetFullPath(Path.Combine(dir, importPath))

/// Tracks the path of the file currently being type-checked.
/// Set by the file loading pipeline before calling typeCheckModuleWithPrelude.
/// Used by the FileImportDecl arm to resolve relative import paths.
let mutable currentTypeCheckingFile : string = ""

/// Mutable delegate for loading and type-checking a file import.
/// Set by Prelude.fs (or Program.fs) after the parser and lexer are available.
/// Signature: (resolvedPath, cEnv, rEnv, typeEnv, mods) -> (typeEnv', cEnv', rEnv', mods')
/// Raises TypeException on error.
let mutable fileImportTypeChecker :
    (string -> ConstructorEnv -> RecordEnv -> TypeEnv -> Map<string, ModuleExports>
        -> TypeEnv * ConstructorEnv * RecordEnv * Map<string, ModuleExports>) =
    fun resolvedPath _ _ _ _ ->
        failwithf "FileImport type checker not initialized. Cannot import '%s'." resolvedPath

// Phase 72: currentClassEnv/currentInstEnv live in Bidir.fs (not here) to avoid
// circular dependency Infer -> TypeCheck. Access via Bidir.currentClassEnv / Bidir.currentInstEnv.

/// Type check declarations sequentially, building up environments and collecting warnings.
/// Returns (typeEnv, ctorEnv, recEnv, classEnv, instEnv, modules, warnings)
let rec typeCheckDecls
    (decls: Decl list)
    (typeEnv: TypeEnv) (ctorEnv: ConstructorEnv) (recEnv: RecordEnv)
    (classEnv: ClassEnv) (instEnv: InstanceEnv)
    (modules: Map<string, ModuleExports>)
    : TypeEnv * ConstructorEnv * RecordEnv * ClassEnv * InstanceEnv * Map<string, ModuleExports> * Diagnostic list =

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
                    | Some argTy -> Scheme([], [], TArrow(argTy, TExn))
                    | None -> Scheme([], [], TExn)
                let tEnv' = Map.add ctorName scheme tEnv
                (cEnv', tEnv')
            | _ -> (cEnv, tEnv)
        ) (ctorEnv, typeEnv)

    // Validate globally unique field names
    validateUniqueRecordFields decls

    // Second pass: process declarations sequentially
    let (typeEnv', ctorEnv', recEnv', classEnv', instEnv', modules', warnings') =
        decls
        |> List.fold (fun (env, cEnv, rEnv, clsEnv, iEnv, mods, warns) decl ->
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
                // Apply substitution to pending constraints so TVar refs are resolved (Phase 72)
                Bidir.applySubstToConstraints s
                let scheme = generalize (applyEnv s env) ty'
                let env' = Map.add name scheme env
                // Collect match warnings from this let body
                let matchWarnings = checkMatchWarnings cEnv body
                (env', cEnv, rEnv, clsEnv, iEnv, mods, warns @ matchWarnings)

            | LetMutDecl(name, body, _) ->
                // Phase 42: Mutable variable at module level (monomorphic, no generalization)
                let refsInBody = collectModuleRefs mods body
                let rewrittenBody = rewriteModuleAccess mods body
                let (envForSynth, ctorEnvForSynth, recEnvForSynth) =
                    if Set.isEmpty refsInBody then (env, cEnv, rEnv)
                    else mergeModuleExportsForTypeCheck mods refsInBody env cEnv rEnv
                let s, ty = Bidir.synth ctorEnvForSynth recEnvForSynth [] envForSynth rewrittenBody
                let ty' = apply s ty
                // Apply substitution to pending constraints so TVar refs are resolved (Phase 72)
                Bidir.applySubstToConstraints s
                // NO generalization -- mutable variables must be monomorphic
                let scheme = Scheme([], [], ty')
                let env' = Map.add name scheme env
                // Track as mutable for Assign checks
                Bidir.mutableVars <- Set.add name Bidir.mutableVars
                let matchWarnings = checkMatchWarnings cEnv body
                (env', cEnv, rEnv, clsEnv, iEnv, mods, warns @ matchWarnings)

            | LetPatDecl(pat, body, _) ->
                let refsInBody = collectModuleRefs mods body
                let rewrittenBody = rewriteModuleAccess mods body
                let (envForSynth, ctorEnvForSynth, recEnvForSynth) =
                    if Set.isEmpty refsInBody then (env, cEnv, rEnv)
                    else mergeModuleExportsForTypeCheck mods refsInBody env cEnv rEnv
                let s, valueTy = Bidir.synth ctorEnvForSynth recEnvForSynth [] envForSynth rewrittenBody
                let patEnv, patTy = Infer.inferPattern cEnv pat
                let s2 = Unify.unify (apply s valueTy) patTy
                let s' = compose s2 s
                // Apply substitution to pending constraints so TVar refs are resolved (Phase 72)
                Bidir.applySubstToConstraints s'
                let env' = applyEnv s' env
                let generalizedPatEnv =
                    patEnv |> Map.map (fun _ (Scheme(_, _, ty)) ->
                        generalize env' (apply s' ty))
                let env'' = Map.fold (fun acc k v -> Map.add k v acc) env' generalizedPatEnv
                let matchWarnings = checkMatchWarnings cEnv body
                (env'', cEnv, rEnv, clsEnv, iEnv, mods, warns @ matchWarnings)

            | LetRecDecl(bindings, _) ->
                // Phase 18: Mutual recursive function type checking
                // 1. Create fresh type variables for each function
                let funcTypes =
                    bindings |> List.map (fun (name, param, paramTyOpt, _body, _) ->
                        let paramTy =
                            match paramTyOpt with
                            | Some tyExpr -> elaborateTypeExpr tyExpr
                            | None -> Infer.freshVar()
                        let retTy = Infer.freshVar()
                        (name, param, TArrow(paramTy, retTy), paramTy))

                // 2. Add all functions to env with monomorphic types
                let recEnvTC =
                    funcTypes |> List.fold (fun acc (name, _, funcTy, _) ->
                        Map.add name (Scheme([], [], funcTy)) acc) env

                // 3. Type-check each body in the extended env and unify
                let finalSubst =
                    List.map2 (fun (_, _, _, body, _) (_, param, funcTy, paramTy) ->
                        // Add param to env
                        let bodyEnv = Map.add param (Scheme([], [], paramTy)) recEnvTC
                        // Resolve qualified module access
                        let refsInBody = collectModuleRefs mods body
                        let rewrittenBody = rewriteModuleAccess mods body
                        let (envForSynth, ctorForSynth, recForSynth) =
                            if Set.isEmpty refsInBody then (bodyEnv, cEnv, rEnv)
                            else mergeModuleExportsForTypeCheck mods refsInBody bodyEnv cEnv rEnv
                        let s, bodyTy = Bidir.synth ctorForSynth recForSynth [] envForSynth rewrittenBody
                        // Extract expected return type from funcTy
                        let expectedRetTy =
                            match apply s funcTy with
                            | TArrow(_, ret) -> ret
                            | t -> t
                        let s2 = unify (apply s bodyTy) expectedRetTy
                        compose s2 s
                    ) bindings funcTypes
                    |> List.fold compose empty

                // 4. Generalize all function types and add to env
                let env' = applyEnv finalSubst env
                // Apply final substitution to pending constraints so TVar refs are resolved (Phase 72)
                Bidir.applySubstToConstraints finalSubst
                let env'' =
                    funcTypes |> List.fold (fun acc (name, _, funcTy, _) ->
                        let resolvedTy = apply finalSubst funcTy
                        let scheme = generalize env' resolvedTy
                        Map.add name scheme acc) env'

                // Collect match warnings from all bodies
                let matchWarnings =
                    bindings |> List.collect (fun (_, _, _, body, _) -> checkMatchWarnings cEnv body)

                (env'', cEnv, rEnv, clsEnv, iEnv, mods, warns @ matchWarnings)

            | Decl.TypeDecl _ | Decl.RecordTypeDecl _ | ExceptionDecl _ ->
                // Already processed in first pass (ExceptionDecl: TODO in Plan 02)
                (env, cEnv, rEnv, clsEnv, iEnv, mods, warns)

            | TypeAliasDecl _ ->
                // Type aliases are transparent -- they don't create new types.
                // TEName "AliasName" in type annotations elaborates to a fresh TVar
                // which unifies with the actual type at use sites.
                (env, cEnv, rEnv, clsEnv, iEnv, mods, warns)

            | ModuleDecl(name, innerDecls, span) ->
                // Check duplicate module name
                if Map.containsKey name mods then
                    raise (TypeException {
                        Kind = DuplicateModuleName name
                        Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
                // Recurse into inner declarations
                let (innerTypeEnv, innerCtorEnv, innerRecEnv, innerClsEnv, innerInstEnv, innerMods, innerWarns) =
                    typeCheckDecls innerDecls env cEnv rEnv clsEnv iEnv mods
                // Build module exports (only bindings defined in this module, not inherited).
                // Include a binding if it's new OR if it shadows an outer binding with a different type.
                // This allows e.g. String.length (string -> int) to be exported even though
                // List.length ('a list -> int) already exists in the outer env with the same name.
                let moduleTypeEnv =
                    Map.fold (fun acc k v ->
                        match Map.tryFind k env with
                        | None -> Map.add k v acc          // new binding
                        | Some outerV when outerV <> v -> Map.add k v acc  // shadow with different type
                        | _ -> acc) Map.empty innerTypeEnv
                let moduleCtorEnv =
                    Map.fold (fun acc k v ->
                        if Map.containsKey k cEnv then acc
                        else Map.add k v acc) Map.empty innerCtorEnv
                let moduleRecEnv =
                    Map.fold (fun acc k v ->
                        if Map.containsKey k rEnv then acc
                        else Map.add k v acc) Map.empty innerRecEnv
                // ClassEnv/InstanceEnv: only classes/instances newly defined in this module
                let moduleClsEnv =
                    Map.fold (fun acc k v ->
                        if Map.containsKey k clsEnv then acc
                        else Map.add k v acc) Map.empty innerClsEnv
                let moduleInstEnv =
                    Map.fold (fun acc k v ->
                        let outerInsts = Map.tryFind k iEnv |> Option.defaultValue []
                        let newInsts = v |> List.filter (fun inst -> not (List.contains inst outerInsts))
                        if List.isEmpty newInsts then acc
                        else Map.add k newInsts acc) Map.empty innerInstEnv
                // SubModules: only modules newly defined INSIDE this module (not outer mods)
                let newSubMods =
                    Map.fold (fun acc k v ->
                        if Map.containsKey k mods then acc
                        else Map.add k v acc) Map.empty innerMods
                let exports = {
                    TypeEnv = moduleTypeEnv
                    CtorEnv = moduleCtorEnv
                    RecEnv = moduleRecEnv
                    ClassEnv = moduleClsEnv
                    InstanceEnv = moduleInstEnv
                    SubModules = newSubMods
                }
                // Propagate ClassEnv and InstanceEnv to outer scope (typeclass effects are global)
                let clsEnv' = Map.fold (fun acc k v -> Map.add k v acc) clsEnv innerClsEnv
                let iEnv' = Map.fold (fun acc k v -> Map.add k v acc) iEnv innerInstEnv
                Bidir.currentClassEnv <- clsEnv'
                Bidir.currentInstEnv <- iEnv'
                (env, cEnv, rEnv, clsEnv', iEnv', Map.add name exports mods, warns @ innerWarns)

            | OpenDecl(path, span) ->
                // Look up module in current modules map
                match path with
                | [name] ->
                    match Map.tryFind name mods with
                    | None ->
                        raise (TypeException {
                            Kind = UnresolvedModule name
                            Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
                    | Some exports ->
                        let (env', cEnv', rEnv', clsEnv', iEnv') = openModuleExports exports env cEnv rEnv clsEnv iEnv
                        (env', cEnv', rEnv', clsEnv', iEnv', mods, warns)
                | _ ->
                    let exports = resolveModule mods path span
                    let (env', cEnv', rEnv', clsEnv', iEnv') = openModuleExports exports env cEnv rEnv clsEnv iEnv
                    (env', cEnv', rEnv', clsEnv', iEnv', mods, warns)

            | FileImportDecl(path, importSpan) ->
                // Use currentTypeCheckingFile for path resolution since span.FileName
                // may be empty (fsyacc positions use lexbuf.StartPos which isn't set)
                let resolvedPath = resolveImportPath path currentTypeCheckingFile
                try
                    let (env', cEnv', rEnv', fileMods) = fileImportTypeChecker resolvedPath cEnv rEnv env mods
                    let mods' = Map.fold (fun acc k v -> Map.add k v acc) mods fileMods
                    (env', cEnv', rEnv', clsEnv, iEnv, mods', warns)
                with
                | TypeException err when err.Span = Ast.unknownSpan ->
                    // Re-raise with the open statement's span for better error location (MERR-03/04)
                    raise (TypeException { err with Span = importSpan })

            // Phase 72 (Type Classes): TypeClassDecl and InstanceDecl processing
            | TypeClassDecl(className, typeVarName, methods, span) ->
                // If class is already defined (e.g. from Prelude), skip redeclaration silently.
                // This allows user code to re-declare a prelude class without error.
                // Duplicate instance declarations are still caught below.
                if Map.containsKey className clsEnv then
                    (env, cEnv, rEnv, clsEnv, iEnv, mods, warns)
                else
                    // Create a fresh type variable for the class param
                    let classTypeVar = Infer.freshVar()
                    let classVarId = match classTypeVar with TVar n -> n | _ -> failwith "impossible"
                    // Elaborate each method signature with this shared type var env
                    let methodSchemes =
                        methods |> List.map (fun (methodName, methodTypeExpr) ->
                            let (methodTy, _) = Elaborate.elaborateWithVars (Map.ofList [(typeVarName, classVarId)]) methodTypeExpr
                            // Method scheme: forall [classVarId]. ClassName classVarId => methodTy
                            let methodConstraint = { ClassName = className; TypeArg = TVar classVarId; SourceSpan = span }
                            let scheme = Scheme([classVarId], [methodConstraint], methodTy)
                            (methodName, scheme))
                    // Build ClassInfo and add to classEnv
                    let classInfo = { Name = className; TypeVar = classVarId; Methods = methodSchemes }
                    let clsEnv' = Map.add className classInfo clsEnv
                    // Update Bidir mutable ref for constraint resolution access
                    Bidir.currentClassEnv <- clsEnv'
                    // Add method schemes to typeEnv (so methods are callable as regular functions)
                    let env' =
                        methodSchemes |> List.fold (fun acc (methodName, scheme) ->
                            Map.add methodName scheme acc) env
                    (env', cEnv, rEnv, clsEnv', iEnv, mods, warns)

            | InstanceDecl(className, instTypeExpr, methods, span) ->
                // Look up class in classEnv
                let classInfo =
                    match Map.tryFind className clsEnv with
                    | Some ci -> ci
                    | None ->
                        raise (TypeException {
                            Kind = UnknownTypeClass className
                            Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
                // Elaborate the instance type (e.g., TEName "int" -> TInt)
                // TEName for user-defined types must resolve to TData, not fresh TVar.
                // Check if any constructor resolves to this type, or if it's a record type.
                let instType =
                    match instTypeExpr with
                    | Ast.TEName name ->
                        let isAdt = cEnv |> Map.exists (fun _ info ->
                            match info.ResultType with
                            | TData(n, _) when n = name -> true
                            | _ -> false)
                        let isRecord = Map.containsKey name rEnv
                        if isAdt || isRecord then TData(name, [])
                        else Elaborate.elaborateTypeExpr instTypeExpr
                    | _ -> Elaborate.elaborateTypeExpr instTypeExpr
                // Check for duplicate instance
                let existingInstances = Map.tryFind className iEnv |> Option.defaultValue []
                if existingInstances |> List.exists (fun ii -> ii.InstanceType = instType) then
                    raise (TypeException {
                        Kind = DuplicateInstance(className, instType)
                        Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
                // Check methods match class declaration (same set of names)
                let classMethodNames = classInfo.Methods |> List.map fst |> Set.ofList
                let instMethodNames = methods |> List.map fst |> Set.ofList
                let missing = Set.difference classMethodNames instMethodNames
                let extra = Set.difference instMethodNames classMethodNames
                if not (Set.isEmpty missing) then
                    let missingName = Set.minElement missing
                    raise (TypeException {
                        Kind = MissingMethod(className, missingName)
                        Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
                if not (Set.isEmpty extra) then
                    let extraName = Set.minElement extra
                    raise (TypeException {
                        Kind = ExtraMethod(className, extraName)
                        Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
                // Type-check each method body against the class method signature
                // instantiated with the concrete instance type
                methods |> List.iter (fun (methodName, methodBody) ->
                    let classScheme =
                        classInfo.Methods |> List.find (fun (n, _) -> n = methodName) |> snd
                    // Instantiate: replace the class type var with the concrete instance type
                    let (Scheme(_, _, methodTy)) = classScheme
                    let subst = Map.ofList [classInfo.TypeVar, instType]
                    let expectedTy = apply subst methodTy
                    // Synth the body and unify with expected type
                    let rewrittenBody = rewriteModuleAccess mods methodBody
                    let s, actualTy = Bidir.synth cEnv rEnv [] env rewrittenBody
                    let s2 = Unify.unifyWithContext [] [] span (apply s actualTy) (apply s expectedTy)
                    ignore s2  // We only care about errors, not the resulting substitution
                )
                // Add to instanceEnv
                let newInst = { ClassName = className; InstanceType = instType }
                let iEnv' = Map.add className (newInst :: existingInstances) iEnv
                // Update Bidir mutable ref for constraint resolution access
                Bidir.currentInstEnv <- iEnv'
                (env, cEnv, rEnv, clsEnv, iEnv', mods, warns)

            | NamespaceDecl(_path, innerDecls, _span) ->
                // Namespace is just a naming prefix, process inner decls in current scope
                let (env', cEnv'', rEnv'', clsEnv'', iEnv'', mods', innerWarns) =
                    innerDecls
                    |> List.fold (fun (e, ce, re, cls, inst, ms, ws) d ->
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
                            (Map.add n scheme e, ce, re, cls, inst, ms, ws @ matchWarnings)
                        | ModuleDecl(name, mInnerDecls, span) ->
                            if Map.containsKey name ms then
                                raise (TypeException {
                                    Kind = DuplicateModuleName name
                                    Span = span; Term = None; ContextStack = []; Trace = []; Scope = [] })
                            let (iTypeEnv, iCtor, iRec, iCls, iInst, iMods, iWarns) =
                                typeCheckDecls mInnerDecls e ce re cls inst ms
                            let mClsEnv = Map.fold (fun acc k v -> if Map.containsKey k cls then acc else Map.add k v acc) Map.empty iCls
                            let mInstEnv =
                                Map.fold (fun acc k v ->
                                    let outerInsts = Map.tryFind k inst |> Option.defaultValue []
                                    let newInsts = v |> List.filter (fun i -> not (List.contains i outerInsts))
                                    if List.isEmpty newInsts then acc
                                    else Map.add k newInsts acc) Map.empty iInst
                            let mExports = {
                                TypeEnv = Map.fold (fun acc k v -> if Map.containsKey k e then acc else Map.add k v acc) Map.empty iTypeEnv
                                CtorEnv = Map.fold (fun acc k v -> if Map.containsKey k ce then acc else Map.add k v acc) Map.empty iCtor
                                RecEnv = Map.fold (fun acc k v -> if Map.containsKey k re then acc else Map.add k v acc) Map.empty iRec
                                ClassEnv = mClsEnv
                                InstanceEnv = mInstEnv
                                SubModules = iMods
                            }
                            // Propagate ClassEnv/InstanceEnv to outer scope
                            let cls' = Map.fold (fun acc k v -> Map.add k v acc) cls iCls
                            let inst' = Map.fold (fun acc k v -> Map.add k v acc) inst iInst
                            Bidir.currentClassEnv <- cls'
                            Bidir.currentInstEnv <- inst'
                            (e, ce, re, cls', inst', Map.add name mExports ms, ws @ iWarns)
                        | _ -> (e, ce, re, cls, inst, ms, ws)
                    ) (env, cEnv, rEnv, clsEnv, iEnv, mods, warns)
                (env', cEnv'', rEnv'', clsEnv'', iEnv'', mods', innerWarns)
        ) (typeEnv, ctorEnv, recEnv, classEnv, instEnv, modules, [])

    (typeEnv', ctorEnv', recEnv', classEnv', instEnv', modules', warnings')

/// Type check a module: build environments from declarations,
/// type check all bindings with exhaustiveness/redundancy checking.
/// Returns Ok(warnings, CtorEnv, RecordEnv, ClassEnv, InstanceEnv, modules, TypeEnv) on success, Error(Diagnostic list) on type error.
let typeCheckModuleWithPrelude
    (preludeCtorEnv: ConstructorEnv) (preludeRecEnv: RecordEnv)
    (preludeClassEnv: ClassEnv) (preludeInstEnv: InstanceEnv)
    (preludeTypeEnv: TypeEnv)
    (initialModules: Map<string, ModuleExports>)
    (m: Module)
    : Result<Diagnostic list * ConstructorEnv * RecordEnv * ClassEnv * InstanceEnv * Map<string, ModuleExports> * TypeEnv, Diagnostic list> =
    try
        // Phase 42: Reset mutable variable tracking for each top-level type check
        Bidir.mutableVars <- Set.empty
        // Phase 72: Initialize class/instance env and pending constraints for Bidir
        Bidir.currentClassEnv <- preludeClassEnv
        Bidir.currentInstEnv <- preludeInstEnv
        Bidir.pendingConstraints <- []
        match m with
        | EmptyModule _ -> Ok ([], Map.empty, Map.empty, Map.empty, Map.empty, Map.empty, Map.empty)
        | Module (decls, _) | NamedModule(_, decls, _) | NamespacedModule(_, decls, _) ->
            // Check for circular module dependencies
            let depGraph = buildDependencyGraph decls
            match detectCircularDeps depGraph with
            | Some cycle ->
                raise (TypeException {
                    Kind = CircularModuleDependency cycle
                    Span = unknownSpan; Term = None; ContextStack = []; Trace = []; Scope = [] })
            | None -> ()

            let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) initialTypeEnv preludeTypeEnv
            let (typeEnv, ctorEnv, recEnv, classEnv, instEnv, modules, warnings) =
                typeCheckDecls decls mergedTypeEnv preludeCtorEnv preludeRecEnv preludeClassEnv preludeInstEnv initialModules
            Ok (warnings, ctorEnv, recEnv, classEnv, instEnv, modules, typeEnv)
    with
    | TypeException err ->
        Error([typeErrorToDiagnostic err])

let typeCheckModule (m: Module) : Result<Diagnostic list * RecordEnv * Map<string, ModuleExports> * TypeEnv, Diagnostic list> =
    match typeCheckModuleWithPrelude Map.empty Map.empty Map.empty Map.empty Map.empty Map.empty m with
    | Ok (warnings, _ctorEnv, recEnv, _classEnv, _instEnv, modules, typeEnv) -> Ok (warnings, recEnv, modules, typeEnv)
    | Error e -> Error e
