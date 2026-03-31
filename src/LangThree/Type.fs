module Type

/// Type representation for Hindley-Milner type inference
/// Phase 1: Type definition (v4.0)
type Type =
    | TInt                           // int
    | TBool                          // bool
    | TString                        // string
    | TChar                          // char (Phase 29)
    | TVar of int                    // type variable 'a, 'b, ... (using int for simplicity)
    | TArrow of Type * Type          // function type 'a -> 'b
    | TTuple of Type list            // tuple type 'a * 'b
    | TList of Type                  // list type 'a list
    | TArray of Type                 // array type 'a array (Phase 38)
    | THashtable of Type * Type      // hashtable type (Phase 39)
    | TData of name: string * typeArgs: Type list  // Named ADT type: Option<'a>, Tree, etc.
    | TExn                                        // Exception base type

/// Constraint for type class requirements
/// Example: { ClassName = "Show"; TypeArg = TVar 0 } means "Show 'a"
type Constraint = { ClassName: string; TypeArg: Type }

/// Type scheme for polymorphism, optionally carrying type class constraints
/// forall 'a 'b. 'a -> 'b -> 'a
/// forall 'a. Show 'a => 'a -> string
type Scheme = Scheme of vars: int list * constraints: Constraint list * ty: Type

/// Create a simple scheme with no constraints (backward compat helper)
let mkScheme (vars: int list) (ty: Type) : Scheme = Scheme(vars, [], ty)

/// Extract the type from a scheme (ignoring constraints)
let schemeType (Scheme(_, _, ty)) : Type = ty

/// Type environment: variable name -> type scheme
type TypeEnv = Map<string, Scheme>

/// Constructor type information for ADT constructors
/// Example: Some : 'a -> Option<'a>
type ConstructorInfo = {
    TypeParams: int list        // Type variables [0] for 'a
    ArgType: Type option        // None for nullary constructors (e.g., None)
    ResultType: Type            // Always TData("TypeName", [TVar ...])
    IsGadt: bool                // True if this is a GADT constructor with refined return type
    ExistentialVars: int list   // Type variables existentially bound (not in result type)
}

/// Constructor environment: constructor name -> type information
type ConstructorEnv = Map<string, ConstructorInfo>

/// Record field type information
/// Phase 3 (Records): field metadata for type checking
type RecordFieldInfo = {
    Name: string
    FieldType: Type
    IsMutable: bool
    Index: int
}

/// Record type information
/// Phase 3 (Records): full record type metadata
type RecordTypeInfo = {
    TypeParams: int list
    Fields: RecordFieldInfo list
    ResultType: Type
}

/// Record environment: record type name -> type information
type RecordEnv = Map<string, RecordTypeInfo>

/// Type class declaration info (Phase 70 — class body populated in Phase 71)
type ClassInfo = {
    Name: string
    TypeVar: int
    Methods: (string * Scheme) list
}

/// Type class instance info (Phase 70 — method bodies populated in Phase 71)
type InstanceInfo = {
    ClassName: string
    InstanceType: Type
}

/// Class environment: class name -> ClassInfo
type ClassEnv = Map<string, ClassInfo>

/// Instance environment: class name -> list of InstanceInfo
type InstanceEnv = Map<string, InstanceInfo list>

/// Type substitution: type variable -> type
type Subst = Map<int, Type>

/// Format type to string representation
let rec formatType = function
    | TInt -> "int"
    | TBool -> "bool"
    | TString -> "string"
    | TChar -> "char"
    | TVar n -> sprintf "'%c" (char (97 + n % 26))  // 'a, 'b, ...
    | TArrow (t1, t2) ->
        let left = match t1 with TArrow _ -> sprintf "(%s)" (formatType t1) | _ -> formatType t1
        sprintf "%s -> %s" left (formatType t2)
    | TTuple [] -> "unit"
    | TTuple ts -> ts |> List.map formatType |> String.concat " * "
    | TList t -> sprintf "%s list" (formatType t)
    | TArray t -> sprintf "%s array" (formatType t)
    | THashtable (k, v) -> sprintf "hashtable<%s, %s>" (formatType k) (formatType v)
    | TData (name, []) -> name
    | TData (name, args) ->
        let argStr = args |> List.map formatType |> String.concat ", "
        sprintf "%s<%s>" name argStr
    | TExn -> "exn"

/// Format type with normalized type variables ('a, 'b, 'c instead of raw indices)
/// TVar 1000, TVar 1001 -> 'a, 'b (based on order of first appearance)
let formatTypeNormalized (ty: Type) : string =
    // Collect all type variables in order of first appearance
    let rec collectVars acc = function
        | TVar n -> if List.contains n acc then acc else acc @ [n]
        | TArrow(t1, t2) -> collectVars (collectVars acc t1) t2
        | TTuple ts -> List.fold collectVars acc ts
        | TList t -> collectVars acc t
        | TArray t -> collectVars acc t
        | THashtable (k, v) -> collectVars (collectVars acc k) v
        | TData (_, args) -> List.fold collectVars acc args
        | TInt | TBool | TString | TChar | TExn -> acc

    let vars = collectVars [] ty
    let varMap = vars |> List.mapi (fun i v -> (v, i)) |> Map.ofList

    let rec format = function
        | TInt -> "int"
        | TBool -> "bool"
        | TString -> "string"
        | TChar -> "char"
        | TVar n ->
            match Map.tryFind n varMap with
            | Some idx -> sprintf "'%c" (char (97 + idx % 26))
            | None -> sprintf "'%c" (char (97 + n % 26))
        | TArrow(t1, t2) ->
            let left = match t1 with TArrow _ -> sprintf "(%s)" (format t1) | _ -> format t1
            sprintf "%s -> %s" left (format t2)
        | TTuple [] -> "unit"
        | TTuple ts -> ts |> List.map format |> String.concat " * "
        | TList t -> sprintf "%s list" (format t)
        | TArray t -> sprintf "%s array" (format t)
        | THashtable (k, v) -> sprintf "hashtable<%s, %s>" (format k) (format v)
        | TData (name, []) -> name
        | TData (name, args) ->
            let argStr = args |> List.map format |> String.concat ", "
            sprintf "%s<%s>" name argStr
        | TExn -> "exn"

    format ty

/// Format a type scheme with normalized variables
/// Displays constraints when present: "Show 'a => 'a -> string"
/// For unconstrained schemes, output is identical to before
let formatSchemeNormalized (Scheme (_vars, constraints, ty)) : string =
    // Collect vars from both constraints and ty for a unified normalization map
    let rec collectVars acc = function
        | TVar n -> if List.contains n acc then acc else acc @ [n]
        | TArrow(t1, t2) -> collectVars (collectVars acc t1) t2
        | TTuple ts -> List.fold collectVars acc ts
        | TList t -> collectVars acc t
        | TArray t -> collectVars acc t
        | THashtable (k, v) -> collectVars (collectVars acc k) v
        | TData (_, args) -> List.fold collectVars acc args
        | TInt | TBool | TString | TChar | TExn -> acc
    let constraintTypes = constraints |> List.map (fun c -> c.TypeArg)
    let allVarsFromConstraints = List.fold collectVars [] constraintTypes
    let allVars = collectVars allVarsFromConstraints ty
    let varMap = allVars |> List.mapi (fun i v -> (v, i)) |> Map.ofList
    let rec format = function
        | TInt -> "int"
        | TBool -> "bool"
        | TString -> "string"
        | TChar -> "char"
        | TVar n ->
            match Map.tryFind n varMap with
            | Some idx -> sprintf "'%c" (char (97 + idx % 26))
            | None -> sprintf "'%c" (char (97 + n % 26))
        | TArrow(t1, t2) ->
            let left = match t1 with TArrow _ -> sprintf "(%s)" (format t1) | _ -> format t1
            sprintf "%s -> %s" left (format t2)
        | TTuple [] -> "unit"
        | TTuple ts -> ts |> List.map format |> String.concat " * "
        | TList t -> sprintf "%s list" (format t)
        | TArray t -> sprintf "%s array" (format t)
        | THashtable (k, v) -> sprintf "hashtable<%s, %s>" (format k) (format v)
        | TData (name, []) -> name
        | TData (name, args) ->
            let argStr = args |> List.map format |> String.concat ", "
            sprintf "%s<%s>" name argStr
        | TExn -> "exn"
    let tyStr = format ty
    match constraints with
    | [] -> tyStr
    | cs ->
        let constraintStr =
            cs |> List.map (fun c -> sprintf "%s %s" c.ClassName (format c.TypeArg))
            |> String.concat ", "
        sprintf "%s => %s" constraintStr tyStr

// ============================================================================
// Substitution Operations
// ============================================================================

/// Empty substitution
let empty: Subst = Map.empty

/// Create a single variable substitution
let singleton (v: int) (t: Type): Subst = Map.ofList [(v, t)]

/// Apply substitution to type
/// CRITICAL: TVar case recursively applies for transitive chains
/// Example: {0 -> TVar 1, 1 -> TInt} applied to TVar 0 -> TInt
let rec apply (s: Subst) = function
    | TInt -> TInt
    | TBool -> TBool
    | TString -> TString
    | TChar -> TChar
    | TExn -> TExn
    | TVar n ->
        match Map.tryFind n s with
        | Some t -> apply s t  // Recursive for transitive substitution
        | None -> TVar n
    | TArrow (t1, t2) -> TArrow (apply s t1, apply s t2)
    | TTuple ts -> TTuple (List.map (apply s) ts)
    | TList t -> TList (apply s t)
    | TArray t -> TArray (apply s t)
    | THashtable (k, v) -> THashtable (apply s k, apply s v)
    | TData (name, args) -> TData (name, List.map (apply s) args)

/// Compose two substitutions: s2 after s1 (like function composition)
/// Apply s2 to all values in s1, then merge s2 bindings
let compose (s2: Subst) (s1: Subst): Subst =
    let s1' = Map.map (fun _ t -> apply s2 t) s1
    Map.fold (fun acc k v -> Map.add k v acc) s1' s2

/// Apply substitution to scheme
/// CRITICAL: Remove bound vars from substitution before applying
let applyScheme (s: Subst) (Scheme (vars, constraints, ty)): Scheme =
    let s' = List.fold (fun acc v -> Map.remove v acc) s vars
    let constraints' = constraints |> List.map (fun c -> { c with TypeArg = apply s' c.TypeArg })
    Scheme (vars, constraints', apply s' ty)

/// Apply substitution to all schemes in environment
let applyEnv (s: Subst) (env: TypeEnv): TypeEnv =
    Map.map (fun _ scheme -> applyScheme s scheme) env

// ============================================================================
// Free Variable Operations
// ============================================================================

/// Collect free type variables in a type
let rec freeVars = function
    | TInt | TBool | TString | TChar | TExn -> Set.empty
    | TVar n -> Set.singleton n
    | TArrow (t1, t2) -> Set.union (freeVars t1) (freeVars t2)
    | TTuple ts -> ts |> List.map freeVars |> Set.unionMany
    | TList t -> freeVars t
    | TArray t -> freeVars t
    | THashtable (k, v) -> Set.union (freeVars k) (freeVars v)
    | TData (_, args) -> args |> List.map freeVars |> Set.unionMany

/// Free variables in a type scheme (excludes bound variables)
let freeVarsScheme (Scheme (vars, constraints, ty)) =
    let constraintVars = constraints |> List.map (fun c -> freeVars c.TypeArg) |> Set.unionMany
    Set.difference (Set.union (freeVars ty) constraintVars) (Set.ofList vars)

/// Free variables in entire type environment
let freeVarsEnv (env: TypeEnv) =
    env |> Map.values |> Seq.map freeVarsScheme |> Set.unionMany
