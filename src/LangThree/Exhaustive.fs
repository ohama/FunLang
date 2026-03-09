module Exhaustive

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
            // Specialize both matrix and q for this constructor
            let ctorInfo =
                constructors
                |> List.tryFind (fun c -> c.Name = name)
                |> Option.defaultValue { Name = name; Arity = List.length args }
            let specializedMatrix = specializeMatrix ctorInfo matrix
            let specializedQ = args @ restQ
            useful constructors specializedMatrix specializedQ

        | WildcardPat ->
            // Check if the constructors in the first column form a complete signature
            let headCtors = headConstructors matrix
            let isComplete =
                constructors
                |> List.forall (fun c -> List.contains c.Name headCtors)

            if isComplete then
                // All constructors present: wildcard is useful iff useful for some constructor
                constructors |> List.exists (fun ctor ->
                    let specializedMatrix = specializeMatrix ctor matrix
                    let expandedQ = List.replicate ctor.Arity WildcardPat @ restQ
                    useful constructors specializedMatrix expandedQ)
            else
                // Not all constructors present: use default matrix
                let defMatrix = defaultMatrix matrix
                useful constructors defMatrix restQ

        | OrPat pats ->
            // Or-pattern: useful if any alternative is useful
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

/// Get constructors for a given type name
/// This will be extended when TData types are added to the type system
let getConstructors (_typeName: string) : ConstructorSet =
    failwith "Not implemented: getConstructors (requires TData integration)"
