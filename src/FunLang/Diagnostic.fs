module Diagnostic

open Ast
open Type

// ============================================================================
// Source Snippet Support (Phase 83 — v11.0)
// ============================================================================

/// Cache of source file contents for error display
let mutable private sourceCache : Map<string, string[]> = Map.empty

/// Read a source line from file, with caching
let getSourceLine (fileName: string) (line: int) : string option =
    if fileName = "<unknown>" || fileName = "<expr>" || fileName = ""
       || fileName.StartsWith("<") then None
    else
        try
            let lines =
                match Map.tryFind fileName sourceCache with
                | Some l -> l
                | None ->
                    if System.IO.File.Exists(fileName) then
                        let l = System.IO.File.ReadAllLines(fileName)
                        sourceCache <- Map.add fileName l sourceCache
                        l
                    else [||]
            if line >= 1 && line <= lines.Length then Some lines.[line - 1]
            else None
        with _ -> None

/// Render source snippet with underline for a span
let renderSourceSnippet (span: Span) (label: string option) : string list =
    match getSourceLine span.FileName span.StartLine with
    | None -> []
    | Some sourceLine ->
        let gutterNum = sprintf "%d" span.StartLine
        let padding = System.String(' ', gutterNum.Length)
        let startCol = span.StartColumn
        let endCol =
            if span.StartLine = span.EndLine then span.EndColumn
            else sourceLine.Length
        let underlineLen = max 1 (endCol - startCol)
        let underlinePrefix = System.String(' ', startCol)
        let underline = System.String('^', underlineLen)
        let labelStr = match label with Some l -> " " + l | None -> ""
        [ sprintf "  %s |" padding
          sprintf "  %s | %s" gutterNum sourceLine
          sprintf "  %s | %s%s%s" padding underlinePrefix underline labelStr ]

// ============================================================================
// Suggest / "Did You Mean?" (Phase 84 — v11.0)
// ============================================================================

/// Levenshtein edit distance between two strings
let editDistance (s1: string) (s2: string) : int =
    let m, n = s1.Length, s2.Length
    if m = 0 then n
    elif n = 0 then m
    else
        let d = Array2D.create (m + 1) (n + 1) 0
        for i in 0..m do d.[i, 0] <- i
        for j in 0..n do d.[0, j] <- j
        for i in 1..m do
            for j in 1..n do
                let cost = if System.Char.ToLower(s1.[i-1]) = System.Char.ToLower(s2.[j-1]) then 0 else 1
                d.[i, j] <- min (min (d.[i-1, j] + 1) (d.[i, j-1] + 1)) (d.[i-1, j-1] + cost)
        d.[m, n]

/// Suggest a similar name from a list of candidates
let suggest (name: string) (candidates: string seq) : string option =
    let threshold = max 2 (name.Length / 3)
    candidates
    |> Seq.map (fun s -> (s, editDistance name s))
    |> Seq.filter (fun (_, d) -> d > 0 && d <= threshold)
    |> Seq.sortBy snd
    |> Seq.tryHead
    |> Option.map fst

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
    | UnboundConstructor of name: string
    | ArityMismatch of constructor: string * expected: int * actual: int
    // Phase 3 (Records): Record-specific error kinds
    | UnboundField of recordType: string * fieldName: string
    | DuplicateFieldName of fieldName: string
    | MissingFields of recordType: string * missing: string list
    | ImmutableFieldAssignment of recordType: string * fieldName: string
    | DuplicateRecordField of fieldName: string * type1: string * type2: string
    | NotARecord of typeName: string
    | FieldAccessOnNonRecord of ty: Type
    // Phase 4 (GADT): GADT-specific error kinds
    | GadtAnnotationRequired of scrutineeType: string
    | ExistentialEscape of varId: int
    | GadtReturnTypeMismatch of constructor: string * expected: string * actual: string
    // Phase 5 (Modules): Module error kinds
    | CircularModuleDependency of cycle: string list   // E0501
    | UnresolvedModule of name: string                 // E0502
    | DuplicateModuleName of name: string              // E0503
    | ForwardModuleReference of name: string           // E0504
    // Phase 6 (Exceptions): Exception error kinds
    | UndefinedExceptionConstructor of name: string              // E0601
    | ExceptionArityMismatch of constructor: string * expected: int * actual: int  // E0602
    | RaiseNotException of ty: Type                              // E0603
    | WhenGuardNotBool of ty: Type                               // E0604
    // Warning kinds (W-prefixed codes)
    | NonExhaustiveMatch of missingPatterns: string list
    | RedundantPattern of index: int
    | NonExhaustiveExceptionHandler of missingInfo: string       // W0003
    // Phase 42 (Mutable Variables): Immutable variable assignment error
    | ImmutableVariableAssignment of varName: string            // E0320
    // Phase 47 (Array/Hashtable Indexing): tried to index non-array/hashtable
    | IndexOnNonCollection of ty: Type                          // E0471
    // Phase 72 (Type Classes): Type class errors
    | NoInstance of className: string * ty: Type                // E0701
    | DuplicateInstance of className: string * ty: Type         // E0702
    | UnknownTypeClass of className: string                     // E0703
    | MethodTypeMismatch of className: string * methodName: string * expected: Type * actual: Type  // E0704
    | MissingMethod of className: string * methodName: string   // E0705
    | ExtraMethod of className: string * methodName: string     // E0706

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
    Scope: string list  // Names in scope for "Did you mean?" suggestions (Phase 84)
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
            let suggestion = suggest name err.Scope
            let hint =
                match suggestion with
                | Some s -> sprintf "Did you mean '%s'?" s
                | None -> "Make sure the variable is defined before use"
            Some "E0303",
            sprintf "Unbound variable: %s" name,
            Some hint

        | NotAFunction ty ->
            Some "E0304",
            sprintf "Type %s is not a function and cannot be applied" (formatType ty),
            Some "Check that you're calling a function, not a value"

        | UnboundConstructor name ->
            let suggestion = suggest name err.Scope
            let hint =
                match suggestion with
                | Some s -> sprintf "Did you mean '%s'?" s
                | None -> "Make sure the type declaration defining this constructor is in scope"
            Some "E0305",
            sprintf "Unbound constructor: %s" name,
            Some hint

        | ArityMismatch (ctor, expected, actual) ->
            Some "E0306",
            sprintf "Constructor %s expects %d argument(s) but was given %d" ctor expected actual,
            Some "Check the number of arguments to the constructor"

        | UnboundField (recordType, fieldName) ->
            Some "E0307",
            sprintf "Record type %s has no field named '%s'" recordType fieldName,
            Some "Check the field name matches the record type definition"

        | DuplicateFieldName fieldName ->
            Some "E0308",
            sprintf "Duplicate field name '%s' in record expression" fieldName,
            Some "Each field can only appear once in a record expression"

        | MissingFields (recordType, missing) ->
            let fieldsStr = missing |> String.concat ", "
            Some "E0309",
            sprintf "Record type %s is missing fields: %s" recordType fieldsStr,
            Some "Provide values for all required fields"

        | ImmutableFieldAssignment (recordType, fieldName) ->
            Some "E0310",
            sprintf "Field '%s' of record type %s is immutable and cannot be assigned" fieldName recordType,
            Some "Mark the field as mutable in the type declaration to allow assignment"

        | DuplicateRecordField (fieldName, type1, type2) ->
            Some "E0311",
            sprintf "Field '%s' is defined in both record types %s and %s" fieldName type1 type2,
            Some "Use explicit type annotation to disambiguate"

        | NotARecord typeName ->
            Some "E0312",
            sprintf "'%s' is not a record type" typeName,
            Some "Only record types support field access and update syntax"

        | FieldAccessOnNonRecord ty ->
            Some "E0313",
            sprintf "Cannot access field on non-record type %s" (formatType ty),
            Some "Field access is only supported on record types"

        | GadtAnnotationRequired scrutineeType ->
            Some "E0401",
            sprintf "GADT match requires type annotation on scrutinee of type %s" scrutineeType,
            Some "Add a type annotation to the match scrutinee: match (expr : Type) with ..."

        | ExistentialEscape varId ->
            Some "E0402",
            sprintf "Existential type variable '%c escapes its scope" (char (97 + varId % 26)),
            Some "Existential type variables from GADT pattern matches cannot escape the match branch"

        | GadtReturnTypeMismatch (ctor, expected, actual) ->
            Some "E0403",
            sprintf "GADT constructor %s return type mismatch: expected %s but got %s" ctor expected actual,
            Some "The constructor's return type must match the declared type"

        | CircularModuleDependency cycle ->
            let cycleStr = cycle |> String.concat " -> "
            Some "E0501",
            sprintf "Circular module dependency: %s" cycleStr,
            Some "Remove the circular dependency between modules"

        | UnresolvedModule name ->
            let suggestion = suggest name err.Scope
            let hint =
                match suggestion with
                | Some s -> sprintf "Did you mean '%s'?" s
                | None -> "Make sure the module is defined before use"
            Some "E0502",
            sprintf "Unresolved module: %s" name,
            Some hint

        | DuplicateModuleName name ->
            Some "E0503",
            sprintf "Duplicate module name: %s" name,
            Some "Each module must have a unique name"

        | ForwardModuleReference name ->
            Some "E0504",
            sprintf "Forward reference to module: %s" name,
            Some "Modules must be defined before they can be opened (top-to-bottom order)"

        | UndefinedExceptionConstructor name ->
            Some "E0601",
            sprintf "Undefined exception constructor: %s" name,
            Some "Make sure the exception is declared with 'exception' before use"

        | ExceptionArityMismatch (ctor, expected, actual) ->
            Some "E0602",
            sprintf "Exception constructor %s expects %d argument(s) but was given %d" ctor expected actual,
            Some "Check the number of arguments to the exception constructor"

        | RaiseNotException ty ->
            Some "E0603",
            sprintf "Cannot raise non-exception type %s" (formatType ty),
            Some "Only values of type exn can be raised"

        | WhenGuardNotBool ty ->
            Some "E0604",
            sprintf "When guard must be bool but got %s" (formatType ty),
            Some "The when guard expression must evaluate to a boolean"

        | NonExhaustiveExceptionHandler missingInfo ->
            Some "W0003",
            sprintf "Non-exhaustive exception handler: %s" missingInfo,
            Some "Add a catch-all handler or handle all possible exceptions"

        | ImmutableVariableAssignment varName ->
            Some "E0320",
            sprintf "Cannot assign to immutable variable '%s'. Use 'let mut' to declare mutable variables." varName,
            Some "Declare the variable with 'let mut' to allow assignment"

        | IndexOnNonCollection ty ->
            Some "E0471",
            sprintf "Cannot index into value of type %s; expected array or hashtable" (formatType ty),
            Some "Use array_create to create an array or hashtable_create to create a hashtable"

        | NoInstance (className, ty) ->
            let availableNote =
                if not (List.isEmpty err.Scope) then
                    sprintf "Available instances: %s" (err.Scope |> String.concat ", ")
                else ""
            Some "E0701",
            sprintf "No instance of %s for %s" className (formatType ty),
            Some (if availableNote <> "" then
                    sprintf "Add an instance declaration for this type (%s)" availableNote
                  else "Add an instance declaration for this type")

        | DuplicateInstance (className, ty) ->
            Some "E0702",
            sprintf "Duplicate instance declaration: %s %s" className (formatType ty),
            Some "Remove the duplicate instance declaration"

        | UnknownTypeClass className ->
            let suggestion = suggest className err.Scope
            let hint =
                match suggestion with
                | Some s -> sprintf "Did you mean '%s'?" s
                | None -> "Make sure the type class is declared before use"
            Some "E0703",
            sprintf "Unknown type class: %s" className,
            Some hint

        | MethodTypeMismatch (className, methodName, expected, actual) ->
            Some "E0704",
            sprintf "Method '%s' in instance %s has type %s but class declares %s" methodName className (formatType actual) (formatType expected),
            Some "Check that the method signature matches the type class declaration"

        | MissingMethod (className, methodName) ->
            Some "E0705",
            sprintf "Instance missing required method: %s" methodName,
            Some (sprintf "Add an implementation for method '%s' to complete the instance" methodName)

        | ExtraMethod (className, methodName) ->
            Some "E0706",
            sprintf "Instance declares unknown method '%s' for class %s" methodName className,
            Some "Remove the method or add it to the type class declaration"

        | NonExhaustiveMatch patterns ->
            let patternsStr = patterns |> String.concat ", "
            Some "W0001",
            sprintf "Incomplete pattern match. Missing cases: %s" patternsStr,
            Some "Add the missing cases or a wildcard pattern '_' to cover all values"

        | RedundantPattern idx ->
            Some "W0002",
            sprintf "Redundant pattern in clause %d. This case will never be reached." (idx + 1),
            Some "Remove the unreachable pattern"

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

    // Header: error[E0301] or warning[W0001]
    match diag.Code with
    | Some code when code.StartsWith("W") ->
        sb.AppendLine(sprintf "warning[%s]: %s" code diag.Message) |> ignore
    | Some code -> sb.AppendLine(sprintf "error[%s]: %s" code diag.Message) |> ignore
    | None -> sb.AppendLine(sprintf "error: %s" diag.Message) |> ignore

    // Primary location: --> file.fun:2:5
    sb.AppendLine(sprintf " --> %s" (formatSpan diag.PrimarySpan)) |> ignore

    // Source snippet with ^^^ underline (Phase 83)
    let snippetLines = renderSourceSnippet diag.PrimarySpan None
    for line in snippetLines do
        sb.AppendLine(line) |> ignore

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
