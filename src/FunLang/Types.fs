module FunLang.Types

open FunLang.Ast

// =============================================================================
// Type Definitions
// =============================================================================

/// Type variable ID
type TypeVar = int

/// Monotype (단형 타입)
type Type =
    | TInt
    | TBool
    | TString
    | TUnit
    | TVar of TypeVar              // 타입 변수 α, β, ...
    | TFun of Type * Type          // τ₁ → τ₂
    | TList of Type                // list τ
    | TTuple of Type list          // (τ₁, τ₂, ...)
    | TConstructor of string * Type list  // 사용자 정의 타입: Option int, List bool, etc.

/// Type Scheme (다형 타입) - ∀α₁...αₙ. τ
type TypeScheme = Forall of TypeVar list * Type

/// Type Environment: 변수명 → TypeScheme
type TypeEnv = Map<string, TypeScheme>

/// Substitution: 타입 변수 → 타입
type Substitution = Map<TypeVar, Type>

// =============================================================================
// Type Error Definitions
// =============================================================================

type TypeErrorKind =
    | UnboundVariable of string
    | TypeMismatch of expected: Type * actual: Type
    | OccursCheck of TypeVar * Type
    | ArityMismatch of expected: int * actual: int
    | NotAFunction of Type
    | PatternTypeMismatch of expected: Type * actual: Type

type TypeError = {
    Kind: TypeErrorKind
    Message: string
    Position: Position option
    Hint: string option
    Suggestions: string list  // "Did you mean?" suggestions
}

// =============================================================================
// Type Error Creation Helpers
// =============================================================================

module TypeError =
    /// Create unbound variable error with optional suggestions
    let unboundVarWithSuggestions name pos (suggestions: string list) =
        { Kind = UnboundVariable name
          Message = sprintf "Unbound variable: %s" name
          Position = pos
          Hint = None
          Suggestions = suggestions }

    /// Create unbound variable error (no suggestions)
    let unboundVar name pos =
        unboundVarWithSuggestions name pos []

    let mismatch expected actual pos =
        { Kind = TypeMismatch (expected, actual)
          Message = "Type mismatch"
          Position = pos
          Hint = None
          Suggestions = [] }

    let occursCheck v t pos =
        { Kind = OccursCheck (v, t)
          Message = sprintf "Infinite type: type variable occurs in its own definition"
          Position = pos
          Hint = Some "Cannot construct infinite type"
          Suggestions = [] }

    let notAFunction t pos =
        { Kind = NotAFunction t
          Message = "Not a function"
          Position = pos
          Hint = Some "Cannot apply arguments to non-function"
          Suggestions = [] }

    let arityMismatch expected actual pos =
        { Kind = ArityMismatch (expected, actual)
          Message = sprintf "Wrong number of arguments: expected %d, got %d" expected actual
          Position = pos
          Hint = None
          Suggestions = [] }

    let patternMismatch expected actual pos =
        { Kind = PatternTypeMismatch (expected, actual)
          Message = "Pattern type mismatch"
          Position = pos
          Hint = None
          Suggestions = [] }

// =============================================================================
// Type Helper Functions
// =============================================================================

module TypeHelpers =
    /// Fresh type variable 생성 (thread-local counter for parallel test safety)
    let private counter = new System.Threading.ThreadLocal<int>(fun () -> 0)

    let freshTypeVar () : Type =
        counter.Value <- counter.Value + 1
        TVar counter.Value

    let resetCounter () =
        counter.Value <- 0

    let getCounter () = counter.Value

    // -------------------------------------------------------------------------
    // Substitution Operations
    // -------------------------------------------------------------------------

    /// Apply substitution to a type
    let rec apply (s: Substitution) (t: Type) : Type =
        match t with
        | TInt | TBool | TString | TUnit -> t
        | TVar v ->
            match Map.tryFind v s with
            | Some t' -> apply s t'  // Transitive application
            | None -> t
        | TFun (t1, t2) -> TFun (apply s t1, apply s t2)
        | TList t1 -> TList (apply s t1)
        | TTuple ts -> TTuple (List.map (apply s) ts)
        | TConstructor (name, ts) -> TConstructor (name, List.map (apply s) ts)

    /// Compose two substitutions: (s1 ∘ s2)(t) = s1(s2(t))
    let compose (s1: Substitution) (s2: Substitution) : Substitution =
        let s2' = Map.map (fun _ t -> apply s1 t) s2
        Map.fold (fun acc k v -> Map.add k v acc) s2' s1

    // -------------------------------------------------------------------------
    // Free Type Variables
    // -------------------------------------------------------------------------

    /// Get free type variables of a type
    let rec freeTypeVars (t: Type) : Set<TypeVar> =
        match t with
        | TInt | TBool | TString | TUnit -> Set.empty
        | TVar v -> Set.singleton v
        | TFun (t1, t2) -> Set.union (freeTypeVars t1) (freeTypeVars t2)
        | TList t1 -> freeTypeVars t1
        | TTuple ts -> ts |> List.map freeTypeVars |> Set.unionMany
        | TConstructor (_, ts) -> ts |> List.map freeTypeVars |> Set.unionMany

    /// Get free type variables of a type scheme
    let freeTypeVarsScheme (Forall (vars, t)) : Set<TypeVar> =
        Set.difference (freeTypeVars t) (Set.ofList vars)

    /// Get free type variables of a type environment
    let freeTypeVarsEnv (env: TypeEnv) : Set<TypeVar> =
        env |> Map.toSeq |> Seq.map (snd >> freeTypeVarsScheme) |> Set.unionMany

    // -------------------------------------------------------------------------
    // Generalization & Instantiation
    // -------------------------------------------------------------------------

    /// Generalize a type to a type scheme
    /// Quantifies type variables that are free in the type but not in the environment
    let generalize (env: TypeEnv) (t: Type) : TypeScheme =
        let envFV = freeTypeVarsEnv env
        let tFV = freeTypeVars t
        let vars = Set.difference tFV envFV |> Set.toList
        Forall (vars, t)

    /// Instantiate a type scheme with fresh type variables
    let instantiate (Forall (vars, t)) : Type =
        if List.isEmpty vars then t
        else
            let subst = vars |> List.map (fun v -> (v, freshTypeVar ())) |> Map.ofList
            apply subst t

    // -------------------------------------------------------------------------
    // Apply Substitution to Environment
    // -------------------------------------------------------------------------

    /// Apply substitution to a type scheme
    let applyScheme (s: Substitution) (Forall (vars, t)) : TypeScheme =
        // Don't substitute quantified variables
        let s' = Map.filter (fun k _ -> not (List.contains k vars)) s
        Forall (vars, apply s' t)

    /// Apply substitution to a type environment
    let applyEnv (s: Substitution) (env: TypeEnv) : TypeEnv =
        Map.map (fun _ scheme -> applyScheme s scheme) env

// =============================================================================
// Type Formatting
// =============================================================================

let rec formatType (t: Type) : string =
    match t with
    | TInt -> "int"
    | TBool -> "bool"
    | TString -> "string"
    | TUnit -> "unit"
    | TVar v -> sprintf "'a%d" v
    | TFun (t1, t2) ->
        let left =
            match t1 with
            | TFun _ -> sprintf "(%s)" (formatType t1)
            | _ -> formatType t1
        sprintf "%s -> %s" left (formatType t2)
    | TList t1 -> sprintf "%s list" (formatType t1)
    | TTuple ts ->
        ts |> List.map formatType |> String.concat " * " |> sprintf "(%s)"
    | TConstructor (name, []) -> name
    | TConstructor (name, ts) ->
        let args = ts |> List.map formatType |> String.concat ", "
        sprintf "%s<%s>" name args

let formatTypeScheme (Forall (vars, t)) : string =
    if List.isEmpty vars then
        formatType t
    else
        let varStr = vars |> List.map (sprintf "'a%d") |> String.concat " "
        sprintf "forall %s. %s" varStr (formatType t)

let formatTypeError (err: TypeError) : string =
    let main =
        match err.Kind with
        | UnboundVariable name ->
            sprintf "Unbound variable '%s'" name
        | TypeMismatch (expected, actual) ->
            sprintf "Type mismatch\n  Expected: %s\n  Actual: %s"
                (formatType expected) (formatType actual)
        | OccursCheck (v, t) ->
            sprintf "Infinite type: 'a%d = %s" v (formatType t)
        | ArityMismatch (expected, actual) ->
            sprintf "Wrong number of arguments\n  Expected: %d\n  Actual: %d"
                expected actual
        | NotAFunction t ->
            sprintf "Not a function: %s\n  Cannot apply arguments to non-function"
                (formatType t)
        | PatternTypeMismatch (expected, actual) ->
            sprintf "Pattern type mismatch\n  Pattern expects: %s\n  Actual: %s"
                (formatType expected) (formatType actual)

    let position =
        match err.Position with
        | Some pos -> sprintf " at line %d, column %d" pos.Line pos.Column
        | None -> ""

    let hint =
        match err.Hint with
        | Some h -> sprintf "\n  Hint: %s" h
        | None -> ""

    sprintf "Type Error%s: %s%s" position main hint

// =============================================================================
// Result Type Alias
// =============================================================================

type TypeResult<'a> = Result<'a, TypeError>
type InferResult = Result<Substitution * Type, TypeError>

// =============================================================================
// Type Definition Environment Builder
// =============================================================================

/// Build a type environment from type definitions
/// Maps constructor names to their type schemes
module TypeDefEnvBuilder =
    open FunLang.Ast

    /// Convert a TypeExpr to a Type, using the given type variable mapping
    let rec typeExprToType (typeVarMap: Map<string, TypeVar>) (te: TypeExpr) : Type =
        match te with
        | TEVar name ->
            // Type variable: 'a -> TVar (lookup from map)
            match Map.tryFind name typeVarMap with
            | Some v -> TVar v
            | None -> TVar 0  // Shouldn't happen if type definition is valid

        | TEName name ->
            // Primitive type name or nullary type constructor
            match name with
            | "int" -> TInt
            | "bool" -> TBool
            | "string" -> TString
            | "unit" -> TUnit
            | _ -> TConstructor (name, [])

        | TEApp (name, args) ->
            // Type application: List 'a, Option int
            let argTypes = args |> List.map (typeExprToType typeVarMap)
            TConstructor (name, argTypes)

        | TETuple exprs ->
            // Tuple type: 'a * 'b
            let elemTypes = exprs |> List.map (typeExprToType typeVarMap)
            TTuple elemTypes

    /// Build constructor type from a type definition
    /// For `type Option 'a = None | Some of 'a`:
    /// - None : forall 'a. Option 'a
    /// - Some : forall 'a. 'a -> Option 'a
    let buildConstructorType
        (typeName: string)
        (typeParams: string list)
        (typeVarMap: Map<string, TypeVar>)
        (constructorName: string, argTypeOpt: TypeExpr option)
        : string * TypeScheme =

        // Build the result type: TypeName<'a1, 'a2, ...>
        let resultTypeArgs = typeParams |> List.map (fun p -> TVar (Map.find p typeVarMap))
        let resultType = TConstructor (typeName, resultTypeArgs)

        // Build the constructor type
        let constructorType =
            match argTypeOpt with
            | None ->
                // Nullary constructor: None : Option 'a
                resultType
            | Some typeExpr ->
                // Unary constructor: Some : 'a -> Option 'a
                // or Cons : 'a * List 'a -> List 'a
                let argType = typeExprToType typeVarMap typeExpr
                TFun (argType, resultType)

        // Quantified variables
        let quantifiedVars = typeParams |> List.map (fun p -> Map.find p typeVarMap)

        (constructorName, Forall (quantifiedVars, constructorType))

    /// Build type environment from a single type definition
    let buildFromTypeDef (typeDef: TypeDef) : Map<string, TypeScheme> =
        // Create type variables using negative IDs to avoid collision with freshTypeVar()
        // freshTypeVar() generates 1, 2, 3, ... so we use -1, -2, -3, ...
        let typeVarMap =
            typeDef.TypeParams
            |> List.mapi (fun i p -> (p, -(i + 1)))  // Use negative IDs: -1, -2, -3, ...
            |> Map.ofList

        // Build constructor schemes
        typeDef.Constructors
        |> List.map (buildConstructorType typeDef.Name typeDef.TypeParams typeVarMap)
        |> Map.ofList

    /// Build type environment from multiple type definitions
    let buildTypeDefEnv (typeDefs: TypeDef list) : TypeEnv =
        typeDefs
        |> List.map buildFromTypeDef
        |> List.fold (fun acc env -> Map.fold (fun a k v -> Map.add k v a) acc env) Map.empty

// =============================================================================
// Type Definition Registry (for Pattern Analysis)
// =============================================================================
//
// Maps type names to their constructor information.
// Used for exhaustiveness and redundancy checking.
// =============================================================================

/// Information about a user-defined type
type TypeDefInfo = {
    Name: string
    TypeParams: string list
    Constructors: (string * int) list  // (constructor name, arity)
}

/// Registry mapping type names to their definitions
type TypeDefRegistry = Map<string, TypeDefInfo>

module TypeDefRegistryBuilder =
    open FunLang.Ast

    /// Build a type definition registry from type definitions
    let buildTypeDefRegistry (typeDefs: TypeDef list) : TypeDefRegistry =
        typeDefs
        |> List.map (fun td ->
            let ctors =
                td.Constructors
                |> List.map (fun (name, argOpt: TypeExpr option) ->
                    let arity = match argOpt with Some _ -> 1 | None -> 0
                    (name, arity))
            let info: TypeDefInfo = {
                Name = td.Name
                TypeParams = td.TypeParams
                Constructors = ctors
            }
            (td.Name, info))
        |> Map.ofList

    /// Get all constructors for a type (returns None if type has infinite domain)
    let getConstructors (t: Type) (registry: TypeDefRegistry) : (string * int) list option =
        match t with
        | TConstructor (typeName, _) ->
            Map.tryFind typeName registry
            |> Option.map (fun info -> info.Constructors)
        | TBool -> Some [("true", 0); ("false", 0)]
        | TList _ -> Some [("[]", 0); ("::", 2)]
        | TUnit -> Some [("()", 0)]
        | TTuple _ -> Some [("tuple", 0)]  // Tuple has single constructor (conceptually)
        | TInt -> None     // Infinite domain
        | TString -> None  // Infinite domain
        | TVar _ -> None   // Unknown type
        | TFun _ -> None   // Functions can't be pattern matched
