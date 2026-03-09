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

/// Get constructors for a given type
/// This will be extended when TData types are added
let getConstructors (_typeName: string) : ConstructorSet =
    failwith "Not implemented: getConstructors"

/// Specialize a pattern matrix for constructor C
/// Filters rows matching C and expands constructor arguments
let specializeMatrix (_ctor: ConstructorInfo) (_matrix: CasePat list list) : CasePat list list =
    failwith "Not implemented: specializeMatrix"

/// Default matrix: rows where first column is a wildcard
let defaultMatrix (_matrix: CasePat list list) : CasePat list list =
    failwith "Not implemented: defaultMatrix"

/// Check if pattern vector q is useful given pattern matrix P
/// A pattern is useful if there exists a value matched by q but not by any row in P
let useful (_constructors: ConstructorSet) (_matrix: CasePat list list) (_q: CasePat list) : bool =
    failwith "Not implemented: useful"

/// Check exhaustiveness: is the wildcard pattern useful after all given patterns?
let checkExhaustive (_constructors: ConstructorSet) (_patterns: CasePat list) : ExhaustivenessResult =
    failwith "Not implemented: checkExhaustive"

/// Check redundancy: is each pattern useful given the patterns before it?
let checkRedundant (_constructors: ConstructorSet) (_patterns: CasePat list) : RedundancyResult =
    failwith "Not implemented: checkRedundant"

/// Format a CasePat for display in warning messages
let rec formatPattern (pat: CasePat) : string =
    failwith "Not implemented: formatPattern"
