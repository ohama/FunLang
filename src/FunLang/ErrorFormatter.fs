module FunLang.ErrorFormatter

open System
open FunLang.Ast
open FunLang.Diagnostic

// =============================================================================
// Configuration
// =============================================================================

/// Configuration options for error formatting (Ariadne-inspired)
type Config = {
    TabWidth: int
    MaxLineWidth: int
    UseColors: bool
    UnderlineChar: char      // '^' or '~' or '-'
    MultilineStyle: bool     // Whether to use multi-line span style
    LineElisionThreshold: int // Lines between spans before eliding
}

let defaultConfig = {
    TabWidth = 4
    MaxLineWidth = 140
    UseColors = false  // Will be enabled in Phase 8.6
    UnderlineChar = '^'
    MultilineStyle = true
    LineElisionThreshold = 3
}

// =============================================================================
// Header Formatting
// =============================================================================

/// Format the header line: severity[code]: message
let formatHeader (diag: Diagnostic) : string =
    let level =
        match diag.Severity with
        | Error -> "error"
        | Warning -> "warning"
        | Note -> "note"
        | Help -> "help"

    let code =
        match diag.Code with
        | Some c -> sprintf "[%s]" c
        | None -> ""

    sprintf "%s%s: %s" level code diag.Message

// =============================================================================
// Location Formatting
// =============================================================================

/// Format the location line: --> file:line:column
let formatLocation (primarySpan: LabeledSpan option) : string =
    match primarySpan with
    | None -> ""
    | Some labeled ->
        let pos = labeled.Span.Start
        let file = pos.File |> Option.defaultValue ""
        sprintf "  --> %s:%d:%d" file pos.Line pos.Column

// =============================================================================
// Source Context Formatting
// =============================================================================

/// Get all spans that need to be displayed
let private collectAllSpans (diag: Diagnostic) : LabeledSpan list =
    let primary = diag.PrimarySpan |> Option.toList
    primary @ diag.SecondarySpans

/// Group spans by line number
let private groupSpansByLine (spans: LabeledSpan list) : Map<int, LabeledSpan list> =
    spans
    |> List.groupBy (fun s -> s.Span.Start.Line)
    |> Map.ofList

/// Get relevant line numbers from source
let private getRelevantLineNumbers (spansByLine: Map<int, LabeledSpan list>) : int list =
    spansByLine
    |> Map.toList
    |> List.map fst
    |> List.sort

/// Check if we need to elide lines between two line numbers
let private needsElision (threshold: int) (line1: int) (line2: int) : bool =
    line2 - line1 > threshold

/// Calculate the width of line number column
let private lineNumWidth (maxLine: int) : int =
    max 1 (string maxLine).Length

/// Format a single source line with underline annotations
let private formatLine
    (config: Config)
    (lineNum: int)
    (lineContent: string)
    (lineNumPadding: int)
    (spans: LabeledSpan list)
    : string list =

    let lineNumStr = sprintf "%*d" lineNumPadding lineNum
    let padding = String.replicate lineNumPadding " "

    // Main content line
    let contentLine = sprintf "%s | %s" lineNumStr lineContent

    // Generate underlines for each span on this line
    let underlines =
        spans
        |> List.sortBy (fun s -> s.Span.Start.Column)
        |> List.collect (fun span ->
            let startCol = max 0 (span.Span.Start.Column - 1)
            let endCol =
                if span.Span.End.Line = lineNum then
                    span.Span.End.Column - 1
                else
                    String.length lineContent

            let width = max 1 (endCol - startCol)
            let underlineChar = string config.UnderlineChar
            let underline = String.replicate width underlineChar
            let spaces = String.replicate startCol " "

            match span.Label with
            | Some label ->
                [ sprintf "%s | %s%s %s" padding spaces underline label ]
            | None ->
                [ sprintf "%s | %s%s" padding spaces underline ])

    contentLine :: underlines

/// Add elision marker
let private formatElision (lineNumPadding: int) : string =
    let padding = String.replicate lineNumPadding " "
    sprintf "%s :" padding

/// Format source context with all spans
let formatSourceContext (source: string) (config: Config) (diag: Diagnostic) : string =
    if String.IsNullOrEmpty source then ""
    else

    let lines = source.Split('\n')
    let allSpans = collectAllSpans diag
    if List.isEmpty allSpans then ""
    else

    let spansByLine = groupSpansByLine allSpans
    let relevantLines = getRelevantLineNumbers spansByLine
    if List.isEmpty relevantLines then ""
    else

    let maxLine = List.max relevantLines
    let numWidth = lineNumWidth maxLine
    let padding = String.replicate numWidth " "

    // Header separator line
    let headerSep = sprintf "%s |" padding

    // Format each relevant line with potential elision
    let formattedLines =
        relevantLines
        |> List.indexed
        |> List.collect (fun (idx, lineNum) ->
            let prevLineNum =
                if idx = 0 then lineNum
                else relevantLines.[idx - 1]

            // Add elision if needed
            let elision =
                if idx > 0 && needsElision config.LineElisionThreshold prevLineNum lineNum then
                    [ formatElision numWidth
                      sprintf "%s : ..." padding ]
                else
                    []

            // Get the line content (check bounds)
            let lineContent =
                if lineNum > 0 && lineNum <= Array.length lines then
                    lines.[lineNum - 1]
                else
                    ""

            // Get spans for this line
            let lineSpans = spansByLine |> Map.tryFind lineNum |> Option.defaultValue []

            // Format the line
            let formatted = formatLine config lineNum lineContent numWidth lineSpans

            elision @ formatted)

    headerSep :: formattedLines |> String.concat "\n"

// =============================================================================
// Footer Formatting
// =============================================================================

/// Format notes, helps, and suggestions
let formatFooter (diag: Diagnostic) : string =
    let notes =
        diag.Notes
        |> List.rev  // Restore original order (notes are prepended)
        |> List.map (sprintf "   = note: %s")

    let helps =
        diag.Helps
        |> List.rev  // Restore original order
        |> List.map (sprintf "   = help: %s")

    let suggestions =
        diag.Suggestions
        |> List.rev
        |> List.map (fun s ->
            let applicabilityNote =
                match s.Applicability with
                | MachineApplicable -> ""
                | HasPlaceholders -> " (contains placeholders)"
                | MaybeIncorrect -> " (may be incorrect)"
                | Unspecified -> ""
            sprintf "   = suggestion: %s%s\n     %s" s.Message applicabilityNote s.Replacement)

    // Add brief explanation from ErrorExplanations if available
    let info =
        diag.Code
        |> Option.bind FunLang.ErrorExplanations.getBrief
        |> Option.map (sprintf "   = info: %s")
        |> Option.toList

    [ yield! notes
      yield! helps
      yield! suggestions
      yield! info ]
    |> String.concat "\n"

// =============================================================================
// Full Format Function
// =============================================================================

/// Format a complete diagnostic with source context
let format (source: string) (config: Config) (diag: Diagnostic) : string =
    let header = formatHeader diag
    let location = formatLocation diag.PrimarySpan
    let sourceContext = formatSourceContext source config diag
    let footer = formatFooter diag

    [ header
      location
      sourceContext
      footer ]
    |> List.filter (not << System.String.IsNullOrEmpty)
    |> String.concat "\n"

/// Format a diagnostic without source context (compact)
let formatCompact (diag: Diagnostic) : string =
    let header = formatHeader diag
    let location = formatLocation diag.PrimarySpan

    [ header
      location ]
    |> List.filter (not << System.String.IsNullOrEmpty)
    |> String.concat "\n"

/// Format a diagnostic for REPL (inline)
let formatRepl (source: string) (diag: Diagnostic) : string =
    format source { defaultConfig with LineElisionThreshold = 5 } diag
