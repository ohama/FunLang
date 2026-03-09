module Exhaustive

open Type

// ============================================================================
// Pattern Exhaustiveness and Redundancy Checking
// ============================================================================
// Implements the Maranget usefulness algorithm for checking:
// - Exhaustiveness: Are all possible values covered by patterns?
// - Redundancy: Are there unreachable patterns?
//
// This module uses its own abstract pattern representation (CasePat)
// to decouple from the AST Pattern type. Conversion functions translate
// AST patterns to CasePat for analysis.
//
// Reference: Luc Maranget, "Warnings for pattern matching" (JFP 2007)
// ============================================================================

/// Constructor information: name and arity (number of sub-patterns)
type ConstructorInfo = {
    Name: string
    Arity: int
}

/// Abstract pattern representation for exhaustiveness analysis
type CasePat =
    | ConstructorPat of name: string * args: CasePat list
    | WildcardPat
    | OrPat of CasePat list  // Future: or-patterns

/// Type signature for constructor sets (all constructors of a type)
type ConstructorSet = ConstructorInfo list

/// Result of exhaustiveness check
type ExhaustivenessResult =
    | Exhaustive
    | NonExhaustive of missing: CasePat list

/// Result of redundancy check
type RedundancyResult =
    | NoRedundancy
    | HasRedundancy of redundantIndices: int list

// ============================================================================
// Pattern Formatting
// ============================================================================

/// Format a CasePat for display in warning messages
let rec formatPattern (pat: CasePat) : string =
    match pat with
    | WildcardPat -> "_"
    | ConstructorPat (name, []) -> name
    | ConstructorPat (name, [arg]) ->
        let argStr = formatPattern arg
        let needsParens =
            match arg with
            | ConstructorPat (_, _ :: _) -> true
            | _ -> false
        if needsParens then
            sprintf "%s (%s)" name argStr
        else
            sprintf "%s %s" name argStr
    | ConstructorPat (name, args) ->
        let argsStr = args |> List.map formatPattern |> String.concat ", "
        sprintf "%s(%s)" name argsStr
    | OrPat pats ->
        pats |> List.map formatPattern |> String.concat " | "

// ============================================================================
// Matrix Specialization
// ============================================================================

/// Specialize a single row for constructor C.
/// Returns None if the row doesn't match C, Some of the expanded row if it does.
let specializeRow (ctor: ConstructorInfo) (row: CasePat list) : CasePat list option =
    match row with
    | [] -> None
    | firstPat :: rest ->
        match firstPat with
        | ConstructorPat (name, args) when name = ctor.Name ->
            // Constructor matches: replace first column with constructor arguments
            Some (args @ rest)
        | ConstructorPat _ ->
            // Different constructor: row is removed
            None
        | WildcardPat ->
            // Wildcard matches any constructor: expand to arity wildcards
            let wildcards = List.replicate ctor.Arity WildcardPat
            Some (wildcards @ rest)
        | OrPat _ ->
            // Future: handle or-patterns
            None

/// Specialize a pattern matrix for constructor C
/// Filters rows matching C and expands constructor arguments
let specializeMatrix (ctor: ConstructorInfo) (matrix: CasePat list list) : CasePat list list =
    matrix |> List.choose (specializeRow ctor)

/// Default matrix: rows where first column is a wildcard, with that column removed
let defaultMatrix (matrix: CasePat list list) : CasePat list list =
    matrix |> List.choose (fun row ->
        match row with
        | [] -> None
        | WildcardPat :: rest -> Some rest
        | _ -> None)

// ============================================================================
// Usefulness Algorithm (Maranget)
// ============================================================================

/// Collect the set of constructor names appearing in the first column of the matrix
let headConstructors (matrix: CasePat list list) : string list =
    matrix
    |> List.choose (fun row ->
        match row with
        | ConstructorPat (name, _) :: _ -> Some name
        | _ -> None)
    |> List.distinct

/// Check if all constructors in the set appear in the first column of the matrix
let isCompleteSignature (constructors: ConstructorSet) (matrix: CasePat list list) : bool =
    let headCtors = headConstructors matrix
    constructors |> List.forall (fun c -> List.contains c.Name headCtors)

/// Look up constructor info from the set, falling back to pattern arity
let lookupConstructor (constructors: ConstructorSet) (name: string) (fallbackArity: int) : ConstructorInfo =
    constructors
    |> List.tryFind (fun c -> c.Name = name)
    |> Option.defaultValue { Name = name; Arity = fallbackArity }

/// Check if pattern vector q is useful given pattern matrix P and constructor set.
/// A pattern is useful if there exists a value matched by q but not by any row in P.
let rec useful (constructors: ConstructorSet) (matrix: CasePat list list) (q: CasePat list) : bool =
    match q with
    | [] ->
        // Base case: empty pattern vector is useful iff matrix has no rows
        List.isEmpty matrix
    | firstQ :: restQ ->
        match firstQ with
        | ConstructorPat (name, args) ->
            let ctorInfo = lookupConstructor constructors name (List.length args)
            useful constructors (specializeMatrix ctorInfo matrix) (args @ restQ)

        | WildcardPat ->
            if isCompleteSignature constructors matrix then
                // All constructors present: wildcard useful iff useful for some constructor
                constructors |> List.exists (fun ctor ->
                    let expandedQ = List.replicate ctor.Arity WildcardPat @ restQ
                    useful constructors (specializeMatrix ctor matrix) expandedQ)
            else
                // Incomplete signature: use default matrix (wildcard rows only)
                useful constructors (defaultMatrix matrix) restQ

        | OrPat pats ->
            pats |> List.exists (fun altPat ->
                useful constructors matrix (altPat :: restQ))

// ============================================================================
// Exhaustiveness and Redundancy Checks
// ============================================================================

/// Build a missing pattern witness from constructor set when wildcard is useful
let rec buildMissingWitness (constructors: ConstructorSet) (matrix: CasePat list list) : CasePat list =
    // Find which constructors are not covered
    let headCtors = headConstructors matrix
    let uncovered =
        constructors
        |> List.filter (fun c -> not (List.contains c.Name headCtors))

    match uncovered with
    | [] ->
        // All constructors present but nested patterns may be incomplete
        // Try each constructor to find one with a missing nested pattern
        constructors
        |> List.tryPick (fun ctor ->
            let specializedMatrix = specializeMatrix ctor matrix
            let expandedWild = List.replicate ctor.Arity WildcardPat
            if useful constructors specializedMatrix expandedWild then
                let nestedMissing = buildMissingWitness constructors specializedMatrix
                match nestedMissing with
                | [] -> None
                | nested ->
                    // Reconstruct the constructor with the missing nested pattern
                    let args = List.take ctor.Arity nested
                    let rest = List.skip ctor.Arity nested
                    Some (ConstructorPat(ctor.Name, args) :: rest)
            else
                None)
        |> Option.defaultValue []
    | ctor :: _ ->
        // This constructor is uncovered - build a witness
        let args = List.replicate ctor.Arity WildcardPat
        [ConstructorPat(ctor.Name, args)]

/// Check exhaustiveness: is the wildcard pattern useful after all given patterns?
let checkExhaustive (constructors: ConstructorSet) (patterns: CasePat list) : ExhaustivenessResult =
    // Convert pattern list to matrix (each pattern is a single-column row)
    let matrix = patterns |> List.map (fun p -> [p])
    let wildcardQ = [WildcardPat]

    if useful constructors matrix wildcardQ then
        // Not exhaustive - find missing patterns
        let missing = buildMissingWitness constructors matrix
        if List.isEmpty missing then
            // Fallback: we know it's non-exhaustive but can't construct a witness
            NonExhaustive [WildcardPat]
        else
            NonExhaustive missing
    else
        Exhaustive

/// Check redundancy: is each pattern useful given the patterns before it?
let checkRedundant (constructors: ConstructorSet) (patterns: CasePat list) : RedundancyResult =
    let redundant =
        patterns
        |> List.mapi (fun i pat ->
            let prevMatrix = patterns |> List.take i |> List.map (fun p -> [p])
            let isUseful = useful constructors prevMatrix [pat]
            if isUseful then None else Some i)
        |> List.choose id

    if List.isEmpty redundant then
        NoRedundancy
    else
        HasRedundancy redundant

// ============================================================================
// GADT-Aware Constructor Filtering
// ============================================================================

/// Filter constructors to only those whose return type head can match the scrutinee type.
/// For GADT types, constructors with incompatible return type arguments are eliminated
/// (e.g., matching on `int expr` does not require the `Bool` branch).
/// For non-GADT types or generic scrutinee types (with type variables), all constructors
/// are returned unchanged.
let filterPossibleConstructors
    (ctorEnv: Type.ConstructorEnv)
    (scrutineeType: Type.Type)
    (fullConstructorSet: ConstructorSet)
    : ConstructorSet =
    match scrutineeType with
    | Type.TData(typeName, scrutArgs) ->
        let hasGadt =
            ctorEnv
            |> Map.exists (fun _ info ->
                match info.ResultType with
                | Type.TData(n, _) when n = typeName -> info.IsGadt
                | _ -> false)
        if not hasGadt then
            fullConstructorSet
        else
            let scrutHasVars = scrutArgs |> List.exists (fun arg ->
                not (Set.isEmpty (Type.freeVars arg)))
            if scrutHasVars then
                fullConstructorSet
            else
                fullConstructorSet
                |> List.filter (fun ctor ->
                    match Map.tryFind ctor.Name ctorEnv with
                    | Some info when info.IsGadt ->
                        match info.ResultType with
                        | Type.TData(_, ctorArgs) ->
                            if List.length scrutArgs <> List.length ctorArgs then
                                true // arity mismatch, keep conservatively
                            else
                                List.forall2 (fun scrutArg ctorArg ->
                                    if not (Set.isEmpty (Type.freeVars ctorArg)) then true
                                    elif not (Set.isEmpty (Type.freeVars scrutArg)) then true
                                    else scrutArg = ctorArg
                                ) scrutArgs ctorArgs
                        | _ -> true
                    | _ -> true)
    | _ -> fullConstructorSet

// ============================================================================
// Constructor Environment Integration
// ============================================================================

/// Get constructors from a ConstructorEnv for a given scrutinee type.
/// For TData types, filters the ConstructorEnv to find all constructors
/// that produce the same type name.
let getConstructorsFromEnv (ctorEnv: Type.ConstructorEnv) (scrutineeType: Type.Type) : ConstructorSet =
    match scrutineeType with
    | Type.TData(typeName, _) ->
        ctorEnv
        |> Map.toList
        |> List.choose (fun (ctorName, info: Type.ConstructorInfo) ->
            match info.ResultType with
            | Type.TData(resultTypeName, _) when resultTypeName = typeName ->
                Some { Name = ctorName; Arity = if info.ArgType.IsSome then 1 else 0 }
            | _ -> None)
    | _ -> []

/// Convert an AST Pattern to a CasePat for exhaustiveness analysis.
/// Non-ADT patterns (tuples, lists, constants) are treated as wildcards
/// since they are not relevant for ADT exhaustiveness checking.
let rec astPatToCasePat (pat: Ast.Pattern) : CasePat =
    match pat with
    | Ast.ConstructorPat(name, Some argPat, _) ->
        ConstructorPat(name, [astPatToCasePat argPat])
    | Ast.ConstructorPat(name, None, _) ->
        ConstructorPat(name, [])
    | Ast.WildcardPat _ -> WildcardPat
    | Ast.VarPat _ -> WildcardPat
    | Ast.TuplePat _ -> WildcardPat
    | Ast.ConsPat _ -> WildcardPat
    | Ast.EmptyListPat _ -> WildcardPat
    | Ast.ConstPat _ -> WildcardPat
