module FunLang.PatternAnalysis

open FunLang.Ast
open FunLang.Types

// =============================================================================
// Pattern Matching Analysis
// =============================================================================
//
// Implements Maranget's Usefulness algorithm for:
// 1. Exhaustiveness checking (non-exhaustive pattern warnings)
// 2. Redundancy checking (unreachable pattern warnings)
//
// Reference: "Warnings for Pattern Matching" - Luc Maranget
// =============================================================================

// =============================================================================
// Simplified Pattern Representation
// =============================================================================

/// Simplified pattern for analysis (no position information)
type SimplePattern =
    | SPWildcard                                    // _ or variable
    | SPLiteral of Literal                          // 1, true, "str", ()
    | SPConstructor of string * SimplePattern list  // Name args
    | SPOr of SimplePattern list                    // For representing missing patterns

/// Pattern row: a list of patterns (one per column)
type PatternRow = SimplePattern list

/// Pattern matrix: list of rows
type PatternMatrix = PatternRow list

/// Pattern vector: a single row for usefulness check
type PatternVector = SimplePattern list

// =============================================================================
// Pattern Simplification
// =============================================================================

/// Convert Located<Pattern> to SimplePattern
let rec simplify (lp: LPattern) : SimplePattern =
    match lp.Node with
    | PWildcard -> SPWildcard
    | PVariable _ -> SPWildcard  // Variables are equivalent to wildcards for analysis
    | PLiteral lit -> SPLiteral lit
    | PTuple ps ->
        // Tuple is a constructor with the tuple elements as arguments
        SPConstructor ("tuple", ps |> List.map simplify)
    | PList ps ->
        // [a; b] → Cons(a, Cons(b, Nil))
        // [] → Nil
        List.foldBack
            (fun p acc -> SPConstructor ("::", [simplify p; acc]))
            ps
            (SPConstructor ("[]", []))
    | PCons (h, t) ->
        // h :: t → Cons(h, t)
        SPConstructor ("::", [simplify h; simplify t])
    | PConstructor (name, None) ->
        SPConstructor (name, [])
    | PConstructor (name, Some arg) ->
        SPConstructor (name, [simplify arg])
    | PQualifiedCons (path, None) ->
        // Qualified constructor: treat as constructor with full path name
        SPConstructor (String.concat "." path, [])
    | PQualifiedCons (path, Some arg) ->
        SPConstructor (String.concat "." path, [simplify arg])

// =============================================================================
// Pattern to String (for error messages)
// =============================================================================

/// Convert SimplePattern to readable string
let rec patternToString (p: SimplePattern) : string =
    match p with
    | SPWildcard -> "_"
    | SPLiteral (LInt n) -> string n
    | SPLiteral (LBool b) -> if b then "true" else "false"
    | SPLiteral (LString s) -> sprintf "\"%s\"" s
    | SPLiteral LUnit -> "()"
    | SPConstructor ("[]", []) -> "[]"
    | SPConstructor ("::", [h; t]) ->
        sprintf "%s :: %s" (patternToString h) (patternToString t)
    | SPConstructor ("tuple", args) ->
        sprintf "(%s)" (args |> List.map patternToString |> String.concat ", ")
    | SPConstructor (name, []) -> name
    | SPConstructor (name, [arg]) ->
        let argStr = patternToString arg
        // Wrap complex patterns in parentheses
        if argStr.Contains(" ") && not (argStr.StartsWith("(")) then
            sprintf "%s (%s)" name argStr
        else
            sprintf "%s %s" name argStr
    | SPConstructor (name, args) ->
        sprintf "%s (%s)" name (args |> List.map patternToString |> String.concat ", ")
    | SPOr ps ->
        ps |> List.map patternToString |> String.concat " | "

// =============================================================================
// Matrix Operations
// =============================================================================

/// Specialize the matrix for constructor `ctor` with given arity.
/// - Keeps rows where first column matches `ctor` or is wildcard
/// - Expands constructor arguments into new columns
/// - Removes first column and adds expanded args
let specialize (ctor: string) (arity: int) (matrix: PatternMatrix) : PatternMatrix =
    matrix |> List.choose (fun row ->
        match row with
        | [] -> None  // Empty row - skip
        | SPConstructor (name, args) :: rest when name = ctor ->
            // Matching constructor: expand args and append rest
            Some (args @ rest)
        | SPWildcard :: rest ->
            // Wildcard matches any constructor: expand to `arity` wildcards
            Some (List.replicate arity SPWildcard @ rest)
        | SPConstructor _ :: _ ->
            // Non-matching constructor: remove this row
            None
        | SPLiteral _ :: _ ->
            // Literal doesn't match constructor: remove
            None
        | SPOr _ :: _ ->
            // Or pattern: shouldn't appear in input (used for output only)
            None)

/// Default matrix: keeps only rows where first column is wildcard.
/// Used when the constructors in the matrix don't form a complete signature.
let defaultMatrix (matrix: PatternMatrix) : PatternMatrix =
    matrix |> List.choose (fun row ->
        match row with
        | [] -> None  // Empty row - skip
        | SPWildcard :: rest ->
            // Wildcard row: keep it, remove first column
            Some rest
        | SPConstructor _ :: _ ->
            // Constructor row: remove (not default)
            None
        | SPLiteral _ :: _ ->
            // Literal row: remove (not default)
            None
        | SPOr _ :: _ ->
            // Or pattern: shouldn't appear
            None)

// =============================================================================
// Usefulness Algorithm
// =============================================================================

/// Get constructors appearing in the first column of the matrix
let getHeadConstructors (matrix: PatternMatrix) : Set<string> =
    matrix
    |> List.choose (fun row ->
        match row with
        | SPConstructor (name, _) :: _ -> Some name
        | SPLiteral (LBool true) :: _ -> Some "true"
        | SPLiteral (LBool false) :: _ -> Some "false"
        | _ -> None)
    |> Set.ofList

/// Specialize for a literal (treat as constructor with arity 0)
let specializeLiteral (lit: Literal) (matrix: PatternMatrix) : PatternMatrix =
    matrix |> List.choose (fun row ->
        match row with
        | [] -> None
        | SPLiteral l :: rest when l = lit -> Some rest
        | SPWildcard :: rest -> Some rest
        | _ -> None)

/// Specialize for a bool literal (treat as constructor)
let specializeBool (b: bool) (matrix: PatternMatrix) : PatternMatrix =
    matrix |> List.choose (fun row ->
        match row with
        | [] -> None
        | SPLiteral (LBool bval) :: rest when bval = b -> Some rest
        | SPWildcard :: rest -> Some rest
        | _ -> None)

/// Check if a pattern vector is useful against a pattern matrix.
/// A vector is useful if there exists a value matching the vector
/// that doesn't match any row in the matrix.
let rec isUseful
    (matrix: PatternMatrix)
    (vector: PatternVector)
    (registry: TypeDefRegistry)
    (colTypes: Type list)
    : bool =

    match vector, colTypes with
    // Base case 1: empty vector
    | [], _ ->
        // Vector is useful iff the matrix has no rows
        // If matrix has a row with empty pattern list, it matches → not useful
        not (matrix |> List.exists (fun row -> List.isEmpty row))
        && List.isEmpty matrix

    // Empty matrix: any non-empty vector is useful
    | _, _ when List.isEmpty matrix -> true

    // Recursive cases: look at first pattern in vector
    | SPWildcard :: restVector, colType :: restTypes ->
        // Wildcard: check if signature is complete
        let headCtors = getHeadConstructors matrix

        match TypeDefRegistryBuilder.getConstructors colType registry with
        | None ->
            // Unknown or infinite type (like int): use default matrix
            isUseful (defaultMatrix matrix) restVector registry restTypes

        | Some ctors ->
            let allCtorNames = ctors |> List.map fst |> Set.ofList

            if Set.isSubset allCtorNames headCtors then
                // Complete signature: wildcard useful only if useful for some constructor
                ctors |> List.exists (fun (name, arity) ->
                    let matrix' =
                        if name = "true" then specializeBool true matrix
                        elif name = "false" then specializeBool false matrix
                        else specialize name arity matrix
                    let vector' = List.replicate arity SPWildcard @ restVector
                    let types' = List.replicate arity (TVar 0) @ restTypes
                    isUseful matrix' vector' registry types')
            else
                // Incomplete signature: check default matrix
                isUseful (defaultMatrix matrix) restVector registry restTypes

    | SPConstructor (name, args) :: restVector, _ :: restTypes ->
        // Constructor: specialize for this constructor
        let arity = List.length args
        let matrix' = specialize name arity matrix
        let vector' = args @ restVector
        let types' = List.replicate arity (TVar 0) @ restTypes
        isUseful matrix' vector' registry types'

    | SPLiteral lit :: restVector, colType :: restTypes ->
        match lit with
        | LBool b ->
            // Bool literal: treat as constructor
            let matrix' = specializeBool b matrix
            isUseful matrix' restVector registry restTypes
        | _ ->
            // Other literals (int, string): infinite domain
            // Specialize to check if this specific literal matches
            let matrix' = specializeLiteral lit matrix
            if List.isEmpty matrix' then
                // No matching literal or wildcard: useful
                true
            else
                // Has matching patterns: check rest
                isUseful matrix' restVector registry restTypes

    | SPOr _ :: _, _ ->
        // Or pattern shouldn't appear in input
        false

    | _, [] ->
        // Types exhausted but vector not empty: shouldn't happen
        false

// =============================================================================
// Find Missing Pattern
// =============================================================================

/// Find a missing pattern if the matrix is not exhaustive.
/// Returns Some [pattern] if there's a missing case, None if exhaustive.
let rec findMissing
    (matrix: PatternMatrix)
    (registry: TypeDefRegistry)
    (colTypes: Type list)
    : SimplePattern list option =

    match colTypes with
    | [] ->
        // No more columns to check
        // Missing iff matrix has no empty row (nothing matches empty vector)
        if matrix |> List.exists List.isEmpty then
            None  // Exhaustive
        else
            Some []  // Missing: empty pattern

    | colType :: restTypes ->
        // Check if first column is completely covered
        let headCtors = getHeadConstructors matrix

        match TypeDefRegistryBuilder.getConstructors colType registry with
        | None ->
            // Infinite domain (int, string, etc.)
            // Check default matrix
            match findMissing (defaultMatrix matrix) registry restTypes with
            | None -> None  // Default handles all
            | Some rest -> Some (SPWildcard :: rest)  // Missing with wildcard

        | Some ctors ->
            let allCtorNames = ctors |> List.map fst |> Set.ofList
            let missingCtors = Set.difference allCtorNames headCtors |> Set.toList

            match missingCtors with
            | name :: _ ->
                // Found a missing constructor
                let (_, arity) = ctors |> List.find (fun (n, _) -> n = name)
                let args = List.replicate arity SPWildcard
                let pattern =
                    if name = "true" then SPLiteral (LBool true)
                    elif name = "false" then SPLiteral (LBool false)
                    else SPConstructor (name, args)
                Some (pattern :: List.replicate (List.length restTypes) SPWildcard)
            | [] ->
                // All constructors present - check each for nested missing
                ctors |> List.tryPick (fun (name, arity) ->
                    let matrix' =
                        if name = "true" then specializeBool true matrix
                        elif name = "false" then specializeBool false matrix
                        else specialize name arity matrix
                    let types' = List.replicate arity (TVar 0) @ restTypes
                    match findMissing matrix' registry types' with
                    | None -> None
                    | Some rest ->
                        // Reconstruct the pattern with this constructor
                        let (args, restPat) = List.splitAt arity rest
                        let pattern =
                            if name = "true" then SPLiteral (LBool true)
                            elif name = "false" then SPLiteral (LBool false)
                            else SPConstructor (name, args)
                        Some (pattern :: restPat))

// =============================================================================
// Redundancy Check
// =============================================================================

/// Check each pattern for redundancy (covered by previous patterns).
/// Returns list of indices (0-based) of redundant patterns.
let checkRedundancy
    (patterns: LPattern list)
    (registry: TypeDefRegistry)
    (scrutineeType: Type)
    : int list =

    let rec check (idx: int) (matrix: PatternMatrix) (remaining: LPattern list) =
        match remaining with
        | [] -> []
        | p :: rest ->
            let simplified = simplify p
            let vector = [simplified]
            let isRedundant = not (isUseful matrix vector registry [scrutineeType])
            let newMatrix = matrix @ [[simplified]]
            if isRedundant then
                idx :: check (idx + 1) newMatrix rest
            else
                check (idx + 1) newMatrix rest

    check 0 [] patterns

// =============================================================================
// Pattern Warnings
// =============================================================================

/// Warning types for pattern matching
type PatternWarning =
    | NonExhaustive of missing: string list * position: Position
    | RedundantPattern of index: int * position: Position

/// Format a position for display
let private formatPosition (pos: Position) : string =
    match pos.File with
    | Some f -> sprintf "%s:%d:%d" f pos.Line pos.Column
    | None -> sprintf "line %d, column %d" pos.Line pos.Column

/// Format a warning for display
let formatWarning (warning: PatternWarning) : string =
    match warning with
    | NonExhaustive (missing, pos) ->
        let missingStr = String.concat ", " missing
        sprintf "Warning: Non-exhaustive pattern match at %s\n  Missing case(s): %s"
            (formatPosition pos) missingStr
    | RedundantPattern (idx, pos) ->
        sprintf "Warning: Pattern %d at %s is redundant (never matches)"
            (idx + 1) (formatPosition pos)

// =============================================================================
// Public API
// =============================================================================

/// Analyze a match expression for exhaustiveness and redundancy.
/// Returns list of warnings.
let analyzeMatch
    (scrutineeType: Type)
    (cases: (LPattern * LExpr option * LExpr) list)
    (registry: TypeDefRegistry)
    (matchPos: Position)
    : PatternWarning list =

    let patterns = cases |> List.map (fun (p, _, _) -> p)

    // Check exhaustiveness
    let exhaustivenessWarnings =
        let matrix = patterns |> List.map (fun p -> [simplify p])
        match findMissing matrix registry [scrutineeType] with
        | None -> []
        | Some missingPatterns ->
            let missingStrs = missingPatterns |> List.map patternToString
            [NonExhaustive (missingStrs, matchPos)]

    // Check redundancy
    let redundancyWarnings =
        let redundantIndices = checkRedundancy patterns registry scrutineeType
        redundantIndices |> List.map (fun idx ->
            let (pattern, _, _) = List.item idx cases
            RedundantPattern (idx, pattern.Pos))

    exhaustivenessWarnings @ redundancyWarnings
