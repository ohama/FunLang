module Diagnostic

open Ast
open Type

/// General error representation with location, message, and helpful context
type Diagnostic = {
    Code: string option           // e.g., Some "E0301"
    Message: string               // Primary error message
    PrimarySpan: Span             // Main error location
    SecondarySpans: (Span * string) list  // Related locations with labels
    Notes: string list            // Additional context
    Hint: string option           // Suggested fix
}

/// Type error kind - what went wrong
type TypeErrorKind =
    | UnifyMismatch of expected: Type * actual: Type
    | OccursCheck of var: int * ty: Type
    | UnboundVar of name: string
    | NotAFunction of ty: Type

/// Inference context - path through the expression being type checked
/// Each case tracks where in the code we are during type inference
type InferContext =
    | InIfCond of Span
    | InIfThen of Span
    | InIfElse of Span
    | InAppFun of Span
    | InAppArg of Span
    | InLetRhs of name: string * Span
    | InLetBody of name: string * Span
    | InLetRecBody of name: string * Span
    | InMatch of Span
    | InMatchClause of index: int * Span
    | InTupleElement of index: int * Span
    | InListElement of index: int * Span
    | InConsHead of Span
    | InConsTail of Span
    | InCheckMode of expected: Type * source: string * Span

/// Unification path - where in the type structure unification failed
/// Tracks the structural location within types (e.g., 2nd arg of function)
type UnifyPath =
    | AtFunctionParam of Type
    | AtFunctionReturn of Type
    | AtTupleIndex of index: int * Type
    | AtListElement of Type

/// Rich type error with full context for diagnostics
type TypeError = {
    Kind: TypeErrorKind
    Span: Span
    Term: Expr option
    ContextStack: InferContext list
    Trace: UnifyPath list
}

/// Exception wrapper for type errors
exception TypeException of TypeError

// ============================================================================
// Helper Functions
// ============================================================================

/// Format context stack to list of strings (reversed for outer-to-inner display)
let formatContextStack (stack: InferContext list) : string list =
    stack
    |> List.rev  // Stored inner-first, display outer-first
    |> List.map (function
        | InIfCond span -> sprintf "in if condition at %s" (formatSpan span)
        | InIfThen span -> sprintf "in if then-branch at %s" (formatSpan span)
        | InIfElse span -> sprintf "in if else-branch at %s" (formatSpan span)
        | InAppFun span -> sprintf "in function position at %s" (formatSpan span)
        | InAppArg span -> sprintf "in argument position at %s" (formatSpan span)
        | InLetRhs (name, span) -> sprintf "in let %s = ... at %s" name (formatSpan span)
        | InLetBody (name, span) -> sprintf "in let %s body at %s" name (formatSpan span)
        | InLetRecBody (name, span) -> sprintf "in let rec %s body at %s" name (formatSpan span)
        | InMatch span -> sprintf "in match expression at %s" (formatSpan span)
        | InMatchClause (index, span) -> sprintf "in match clause %d at %s" index (formatSpan span)
        | InTupleElement (index, span) -> sprintf "in tuple element %d at %s" index (formatSpan span)
        | InListElement (index, span) -> sprintf "in list element %d at %s" index (formatSpan span)
        | InConsHead span -> sprintf "in cons head at %s" (formatSpan span)
        | InConsTail span -> sprintf "in cons tail at %s" (formatSpan span)
        | InCheckMode (ty, source, span) ->
            sprintf "expected %s due to %s at %s" (formatType ty) source (formatSpan span)
    )

/// Format unification trace to list of strings (reversed for outer-to-inner display)
let formatTrace (trace: UnifyPath list) : string list =
    trace
    |> List.rev  // Stored inner-first, display outer-first
    |> List.map (function
        | AtFunctionParam ty -> sprintf "at function parameter (expected %s)" (formatType ty)
        | AtFunctionReturn ty -> sprintf "at function return (expected %s)" (formatType ty)
        | AtTupleIndex (index, ty) -> sprintf "at tuple index %d (expected %s)" index (formatType ty)
        | AtListElement ty -> sprintf "at list element (expected %s)" (formatType ty)
    )

/// Extract secondary spans from context stack for related expression locations
/// Primary span is excluded to avoid duplication. Limited to 3 most relevant spans.
let contextToSecondarySpans (primarySpan: Span) (contexts: InferContext list) : (Span * string) list =
    contexts
    |> List.rev  // Stored inner-first, display outer-first (same as formatContextStack)
    |> List.map (function
        | InIfCond span -> (span, "in if condition")
        | InIfThen span -> (span, "in then branch")
        | InIfElse span -> (span, "in else branch")
        | InAppFun span -> (span, "in function position")
        | InAppArg span -> (span, "in argument position")
        | InLetRhs (name, span) -> (span, sprintf "in binding '%s'" name)
        | InLetBody (name, span) -> (span, sprintf "in body of '%s'" name)
        | InLetRecBody (name, span) -> (span, sprintf "in recursive body of '%s'" name)
        | InMatch span -> (span, "in match subject")
        | InMatchClause (idx, span) -> (span, sprintf "in clause %d" idx)
        | InTupleElement (idx, span) -> (span, sprintf "in tuple element %d" idx)
        | InListElement (idx, span) -> (span, sprintf "in list element %d" idx)
        | InConsHead span -> (span, "in cons head")
        | InConsTail span -> (span, "in cons tail")
        | InCheckMode (_, source, span) -> (span, sprintf "due to %s" source)
    )
    |> List.filter (fun (span, _) -> span <> primarySpan)  // Exclude primary span (avoid duplication)
    |> List.distinctBy fst  // Remove duplicate spans
    |> List.truncate 3  // Limit to 3 most relevant spans

// ============================================================================
// Conversion to Diagnostic
// ============================================================================

/// Find the first InCheckMode in context to extract annotation source
let findExpectedTypeSource (contexts: InferContext list) : (Type * string * Span) option =
    contexts
    |> List.tryPick (function
        | InCheckMode (ty, source, span) -> Some (ty, source, span)
        | _ -> None)

/// Convert TypeError to Diagnostic for display
let typeErrorToDiagnostic (err: TypeError) : Diagnostic =
    let code, message, hint =
        match err.Kind with
        | UnifyMismatch (expected, actual) ->
            let source = findExpectedTypeSource err.ContextStack
            let baseMsg = sprintf "Type mismatch: expected %s but got %s"
                            (formatType expected) (formatType actual)
            let hint =
                match source with
                | Some (_, "annotation", span) ->
                    Some (sprintf "The type annotation at %s expects %s"
                            (formatSpan span) (formatType expected))
                | _ ->
                    Some "Check that all branches of your expression return the same type"
            Some "E0301", baseMsg, hint

        | OccursCheck (var, ty) ->
            Some "E0302",
            sprintf "Occurs check: cannot construct infinite type '%c = %s"
                (char (97 + var % 26))
                (formatType ty),
            Some "This usually means you're trying to define a recursive type without a base case"

        | UnboundVar name ->
            Some "E0303",
            sprintf "Unbound variable: %s" name,
            Some "Make sure the variable is defined before use"

        | NotAFunction ty ->
            Some "E0304",
            sprintf "Type %s is not a function and cannot be applied" (formatType ty),
            Some "Check that you're calling a function, not a value"

    // Build notes from context stack and trace
    let contextNotes = formatContextStack err.ContextStack
    let traceNotes = formatTrace err.Trace
    let notes = contextNotes @ traceNotes

    // Extract secondary spans from context stack (Phase 3 Blame Assignment)
    let secondarySpans = contextToSecondarySpans err.Span err.ContextStack

    {
        Code = code
        Message = message
        PrimarySpan = err.Span
        SecondarySpans = secondarySpans
        Notes = notes
        Hint = hint
    }

// ============================================================================
// Diagnostic Formatting
// ============================================================================

/// Format diagnostic for display (Rust-inspired multi-line format)
/// Output format:
/// error[E0301]: Type mismatch: expected int but got bool
///  --> test.fun:3:10-14
///    = in if condition: test.fun:3:4-20
///    = note: in if then-branch at test.fun:3:4
///    = hint: Check that all branches of your expression return the same type
let formatDiagnostic (diag: Diagnostic) : string =
    let sb = System.Text.StringBuilder()

    // Error header: error[E0301]: Type mismatch
    match diag.Code with
    | Some code -> sb.AppendLine(sprintf "error[%s]: %s" code diag.Message) |> ignore
    | None -> sb.AppendLine(sprintf "error: %s" diag.Message) |> ignore

    // Primary location: --> file.fun:2:5
    sb.AppendLine(sprintf " --> %s" (formatSpan diag.PrimarySpan)) |> ignore

    // Secondary spans (related locations)
    for (span, label) in diag.SecondarySpans do
        sb.AppendLine(sprintf "   = %s: %s" label (formatSpan span)) |> ignore

    // Notes (context stack, trace)
    for note in diag.Notes do
        sb.AppendLine(sprintf "   = note: %s" note) |> ignore

    // Hint
    match diag.Hint with
    | Some hint -> sb.AppendLine(sprintf "   = hint: %s" hint) |> ignore
    | None -> ()

    sb.ToString().TrimEnd()
