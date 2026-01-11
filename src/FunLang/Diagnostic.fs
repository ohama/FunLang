module FunLang.Diagnostic

open FunLang.Ast

// =============================================================================
// Severity Levels (Rust: Level)
// =============================================================================

type Severity =
    | Error      // Compilation/execution failure
    | Warning    // Suspicious code
    | Note       // Additional information
    | Help       // Suggested fix

// =============================================================================
// Source Span Types
// =============================================================================

/// Source span with optional byte offsets (Miette-style)
type SourceSpan = {
    Start: Position
    End: Position
    ByteStart: int option
    ByteEnd: int option
}

/// Span style for annotations
type SpanStyle =
    | Primary    // Core error location
    | Secondary  // Related information

/// Labeled span for annotations
type LabeledSpan = {
    Span: SourceSpan
    Label: string option
    Style: SpanStyle
}

// =============================================================================
// Suggestion Types
// =============================================================================

/// Suggestion applicability (Rust: Applicability)
type SuggestionApplicability =
    | MachineApplicable  // Safe to auto-apply
    | HasPlaceholders    // Requires user input (e.g., "/* type */")
    | MaybeIncorrect     // Speculative suggestion
    | Unspecified        // Unknown confidence

/// Suggestion with replacement and applicability
type Suggestion = {
    Span: SourceSpan
    Replacement: string
    Message: string
    Applicability: SuggestionApplicability
}

// =============================================================================
// Main Diagnostic Type
// =============================================================================

/// Main diagnostic type (Rust: Diagnostic, Miette: Diagnostic trait)
type Diagnostic = {
    Severity: Severity
    Code: string option           // E001, E201, etc.
    Message: string               // Self-contained message
    PrimarySpan: LabeledSpan option
    SecondarySpans: LabeledSpan list
    Notes: string list            // = note: ...
    Helps: string list            // = help: ...
    Suggestions: Suggestion list  // Suggested fixes
    Related: Diagnostic list      // Related diagnostics (Miette: #[related])
}

// =============================================================================
// SourceSpan Module
// =============================================================================

module SourceSpan =
    /// Create a source span from start and end positions
    let create (startPos: Position) (endPos: Position) : SourceSpan =
        { Start = startPos
          End = endPos
          ByteStart = None
          ByteEnd = None }

    /// Create a source span with byte offsets
    let createWithBytes (startPos: Position) (endPos: Position) (byteStart: int) (byteEnd: int) : SourceSpan =
        { Start = startPos
          End = endPos
          ByteStart = Some byteStart
          ByteEnd = Some byteEnd }

    /// Create a span from a single position (point span)
    let fromPosition (pos: Position) : SourceSpan =
        { Start = pos
          End = pos
          ByteStart = None
          ByteEnd = None }

    /// Create a dummy span (for testing or when position is unknown)
    let dummy : SourceSpan =
        { Start = noPos
          End = noPos
          ByteStart = None
          ByteEnd = None }

    /// Merge two spans (for compound expressions)
    let merge (span1: SourceSpan) (span2: SourceSpan) : SourceSpan =
        { Start = span1.Start
          End = span2.End
          ByteStart = span1.ByteStart
          ByteEnd = span2.ByteEnd }

// =============================================================================
// Diagnostic Builder Module
// =============================================================================

module Diagnostic =
    /// Create a new error diagnostic
    let error (code: string) (message: string) : Diagnostic =
        { Severity = Error
          Code = Some code
          Message = message
          PrimarySpan = None
          SecondarySpans = []
          Notes = []
          Helps = []
          Suggestions = []
          Related = [] }

    /// Create a new warning diagnostic
    let warning (code: string) (message: string) : Diagnostic =
        { Severity = Warning
          Code = Some code
          Message = message
          PrimarySpan = None
          SecondarySpans = []
          Notes = []
          Helps = []
          Suggestions = []
          Related = [] }

    /// Create a new note diagnostic
    let note (code: string) (message: string) : Diagnostic =
        { Severity = Note
          Code = Some code
          Message = message
          PrimarySpan = None
          SecondarySpans = []
          Notes = []
          Helps = []
          Suggestions = []
          Related = [] }

    /// Create a new help diagnostic
    let help (code: string) (message: string) : Diagnostic =
        { Severity = Help
          Code = Some code
          Message = message
          PrimarySpan = None
          SecondarySpans = []
          Notes = []
          Helps = []
          Suggestions = []
          Related = [] }

    /// Create an error diagnostic without a code
    let errorNoCode (message: string) : Diagnostic =
        { Severity = Error
          Code = None
          Message = message
          PrimarySpan = None
          SecondarySpans = []
          Notes = []
          Helps = []
          Suggestions = []
          Related = [] }

    /// Create a warning diagnostic without a code
    let warningNoCode (message: string) : Diagnostic =
        { Severity = Warning
          Code = None
          Message = message
          PrimarySpan = None
          SecondarySpans = []
          Notes = []
          Helps = []
          Suggestions = []
          Related = [] }

    // -------------------------------------------------------------------------
    // Span Builders
    // -------------------------------------------------------------------------

    /// Add primary span with label
    let withPrimarySpan (span: SourceSpan) (label: string) (diag: Diagnostic) : Diagnostic =
        { diag with
            PrimarySpan = Some { Span = span; Label = Some label; Style = Primary } }

    /// Add primary span without label
    let withPrimarySpanNoLabel (span: SourceSpan) (diag: Diagnostic) : Diagnostic =
        { diag with
            PrimarySpan = Some { Span = span; Label = None; Style = Primary } }

    /// Add secondary span with label
    let withSecondarySpan (span: SourceSpan) (label: string) (diag: Diagnostic) : Diagnostic =
        let labeled = { Span = span; Label = Some label; Style = Secondary }
        { diag with SecondarySpans = labeled :: diag.SecondarySpans }

    /// Add secondary span without label
    let withSecondarySpanNoLabel (span: SourceSpan) (diag: Diagnostic) : Diagnostic =
        let labeled = { Span = span; Label = None; Style = Secondary }
        { diag with SecondarySpans = labeled :: diag.SecondarySpans }

    // -------------------------------------------------------------------------
    // Note and Help Builders
    // -------------------------------------------------------------------------

    /// Add a note
    let withNote (note: string) (diag: Diagnostic) : Diagnostic =
        { diag with Notes = note :: diag.Notes }

    /// Add a help message
    let withHelp (help: string) (diag: Diagnostic) : Diagnostic =
        { diag with Helps = help :: diag.Helps }

    // -------------------------------------------------------------------------
    // Suggestion Builders
    // -------------------------------------------------------------------------

    /// Add a machine-applicable suggestion
    let withSuggestion (span: SourceSpan) (replacement: string) (message: string) (diag: Diagnostic) : Diagnostic =
        let sugg = {
            Span = span
            Replacement = replacement
            Message = message
            Applicability = MachineApplicable
        }
        { diag with Suggestions = sugg :: diag.Suggestions }

    /// Add a suggestion with custom applicability
    let withSuggestionApplicability
        (span: SourceSpan)
        (replacement: string)
        (message: string)
        (applicability: SuggestionApplicability)
        (diag: Diagnostic) : Diagnostic =
        let sugg = {
            Span = span
            Replacement = replacement
            Message = message
            Applicability = applicability
        }
        { diag with Suggestions = sugg :: diag.Suggestions }

    // -------------------------------------------------------------------------
    // Related Diagnostic Builders
    // -------------------------------------------------------------------------

    /// Add a related diagnostic
    let withRelated (related: Diagnostic) (diag: Diagnostic) : Diagnostic =
        { diag with Related = related :: diag.Related }

    // -------------------------------------------------------------------------
    // Error Code Mapping
    // -------------------------------------------------------------------------

    /// Error codes by category
    module ErrorCodes =
        // Lexer errors: E001-E099
        let unexpectedChar = "E001"
        let unterminatedString = "E002"
        let invalidEscape = "E003"
        let invalidNumber = "E004"

        // Parser errors: E100-E199
        let unexpectedToken = "E101"
        let missingToken = "E102"
        let invalidSyntax = "E103"
        let indentationError = "E104"
        let unclosedDelimiter = "E105"
        let emptyBlock = "E106"

        // Type errors: E200-E299
        let typeMismatch = "E201"
        let unboundVariable = "E202"
        let infiniteType = "E203"
        let notAFunction = "E204"
        let arityMismatch = "E205"
        let patternTypeMismatch = "E206"
        let undefinedConstructor = "E207"
        let duplicateBinding = "E208"

        // Runtime errors: E300-E399
        let divisionByZero = "E301"
        let nonExhaustiveMatch = "E302"
        let invalidOperation = "E303"
        let stackOverflow = "E304"

    // -------------------------------------------------------------------------
    // Conversion from FunLangError
    // -------------------------------------------------------------------------

    /// Convert FunLangError to Diagnostic
    let fromFunLangError (err: FunLang.Errors.FunLangError) : Diagnostic =
        let code, message =
            match err.Kind with
            | FunLang.Errors.LexerError (c, _) ->
                ErrorCodes.unexpectedChar, sprintf "Unexpected character '%c'" c
            | FunLang.Errors.ParseError (tok, expected, _) ->
                let expectedStr =
                    if List.isEmpty expected then ""
                    else sprintf " (expected: %s)" (String.concat ", " expected)
                ErrorCodes.unexpectedToken, sprintf "Unexpected token '%s'%s" tok expectedStr
            | FunLang.Errors.UnboundVariable (name, _) ->
                ErrorCodes.unboundVariable, sprintf "Unbound variable '%s'" name
            | FunLang.Errors.TypeError (expected, actual, _) ->
                ErrorCodes.typeMismatch, sprintf "Type mismatch: expected %s, got %s" expected actual
            | FunLang.Errors.RuntimeError (msg, _) ->
                ErrorCodes.invalidOperation, msg
            | FunLang.Errors.DivisionByZero _ ->
                ErrorCodes.divisionByZero, "Division by zero"
            | FunLang.Errors.NonExhaustiveMatch _ ->
                ErrorCodes.nonExhaustiveMatch, "Non-exhaustive pattern match"
            | FunLang.Errors.IndentationError (expected, actual, _) ->
                ErrorCodes.indentationError, sprintf "Indentation error: expected %d spaces, got %d" expected actual
            | FunLang.Errors.MixedTabsSpaces _ ->
                ErrorCodes.indentationError, "Mixed tabs and spaces in indentation"

        let diag = error code message

        // Add primary span if position is available
        let diag =
            match err.Position with
            | Some pos ->
                let span = SourceSpan.fromPosition pos
                withPrimarySpanNoLabel span diag
            | None -> diag

        // Add hint as help if available
        let diag =
            match err.Hint with
            | Some hint -> withHelp hint diag
            | None -> diag

        diag

    // -------------------------------------------------------------------------
    // Conversion from TypeError
    // -------------------------------------------------------------------------

    /// Convert TypeError to Diagnostic
    let fromTypeError (err: FunLang.Types.TypeError) : Diagnostic =
        let code, message, notes =
            match err.Kind with
            | FunLang.Types.UnboundVariable name ->
                ErrorCodes.unboundVariable,
                sprintf "Unbound variable '%s'" name,
                []
            | FunLang.Types.TypeMismatch (expected, actual) ->
                ErrorCodes.typeMismatch,
                "Type mismatch",
                [ sprintf "expected: %s" (FunLang.Types.formatType expected)
                  sprintf "found: %s" (FunLang.Types.formatType actual) ]
            | FunLang.Types.OccursCheck (v, t) ->
                ErrorCodes.infiniteType,
                "Infinite type",
                [ sprintf "type variable 'a%d occurs in %s" v (FunLang.Types.formatType t) ]
            | FunLang.Types.ArityMismatch (expected, actual) ->
                ErrorCodes.arityMismatch,
                sprintf "Wrong number of arguments: expected %d, got %d" expected actual,
                []
            | FunLang.Types.NotAFunction t ->
                ErrorCodes.notAFunction,
                sprintf "Not a function: %s" (FunLang.Types.formatType t),
                [ "Cannot apply arguments to non-function" ]
            | FunLang.Types.PatternTypeMismatch (expected, actual) ->
                ErrorCodes.patternTypeMismatch,
                "Pattern type mismatch",
                [ sprintf "pattern expects: %s" (FunLang.Types.formatType expected)
                  sprintf "actual: %s" (FunLang.Types.formatType actual) ]

        let diag = error code message

        // Add notes
        let diag = notes |> List.fold (fun d n -> withNote n d) diag

        // Add primary span if position is available
        let diag =
            match err.Position with
            | Some pos ->
                let span = SourceSpan.fromPosition pos
                withPrimarySpanNoLabel span diag
            | None -> diag

        // Add hint as help if available
        let diag =
            match err.Hint with
            | Some hint -> withHelp hint diag
            | None -> diag

        diag
