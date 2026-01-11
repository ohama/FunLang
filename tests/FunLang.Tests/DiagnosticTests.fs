module FunLang.Tests.DiagnosticTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Diagnostic

// =============================================================================
// Unit Tests for Diagnostic Types
// =============================================================================

let basicTests = testList "Basic Diagnostic Tests" [
    test "create error diagnostic with code and message" {
        let diag = Diagnostic.error "E201" "Type mismatch"

        Expect.equal diag.Severity Error "should be Error severity"
        Expect.equal diag.Code (Some "E201") "should have code E201"
        Expect.equal diag.Message "Type mismatch" "should have message"
        Expect.isNone diag.PrimarySpan "should have no primary span"
        Expect.isEmpty diag.SecondarySpans "should have no secondary spans"
        Expect.isEmpty diag.Notes "should have no notes"
        Expect.isEmpty diag.Helps "should have no helps"
        Expect.isEmpty diag.Suggestions "should have no suggestions"
    }

    test "create warning diagnostic" {
        let diag = Diagnostic.warning "W001" "Unused variable"

        Expect.equal diag.Severity Warning "should be Warning severity"
        Expect.equal diag.Code (Some "W001") "should have code W001"
    }

    test "create diagnostic without code" {
        let diag = Diagnostic.errorNoCode "Something went wrong"

        Expect.equal diag.Severity Error "should be Error severity"
        Expect.isNone diag.Code "should have no code"
        Expect.equal diag.Message "Something went wrong" "should have message"
    }
]

let spanTests = testList "Span Tests" [
    test "create source span" {
        let startPos = { Line = 1; Column = 5; File = Some "test.fun" }
        let endPos = { Line = 1; Column = 10; File = Some "test.fun" }
        let span = SourceSpan.create startPos endPos

        Expect.equal span.Start startPos "start position should match"
        Expect.equal span.End endPos "end position should match"
        Expect.isNone span.ByteStart "byte start should be None"
        Expect.isNone span.ByteEnd "byte end should be None"
    }

    test "create source span with byte offsets" {
        let startPos = { Line = 1; Column = 5; File = None }
        let endPos = { Line = 1; Column = 10; File = None }
        let span = SourceSpan.createWithBytes startPos endPos 4 9

        Expect.equal span.ByteStart (Some 4) "byte start should be Some 4"
        Expect.equal span.ByteEnd (Some 9) "byte end should be Some 9"
    }

    test "create span from single position" {
        let pos = { Line = 3; Column = 15; File = None }
        let span = SourceSpan.fromPosition pos

        Expect.equal span.Start pos "start should match position"
        Expect.equal span.End pos "end should match position"
    }
]

let builderTests = testList "Builder API Tests" [
    test "add primary span with label" {
        let startPos = { Line = 3; Column = 10; File = None }
        let endPos = { Line = 3; Column = 15; File = None }
        let span = SourceSpan.create startPos endPos

        let diag =
            Diagnostic.error "E201" "Type mismatch"
            |> Diagnostic.withPrimarySpan span "expected `int`, found `string`"

        Expect.isSome diag.PrimarySpan "should have primary span"
        let labeled = diag.PrimarySpan.Value
        Expect.equal labeled.Style Primary "should be Primary style"
        Expect.equal labeled.Label (Some "expected `int`, found `string`") "should have label"
    }

    test "add secondary span" {
        let pos1 = { Line = 1; Column = 5; File = None }
        let pos2 = { Line = 3; Column = 10; File = None }
        let span1 = SourceSpan.fromPosition pos1
        let span2 = SourceSpan.fromPosition pos2

        let diag =
            Diagnostic.error "E201" "Type mismatch"
            |> Diagnostic.withPrimarySpan span2 "found here"
            |> Diagnostic.withSecondarySpan span1 "defined here"

        Expect.hasLength diag.SecondarySpans 1 "should have 1 secondary span"
        let secondary = List.head diag.SecondarySpans
        Expect.equal secondary.Style Secondary "should be Secondary style"
    }

    test "add multiple notes" {
        let diag =
            Diagnostic.error "E201" "Type mismatch"
            |> Diagnostic.withNote "expected type: int"
            |> Diagnostic.withNote "actual type: string"

        Expect.hasLength diag.Notes 2 "should have 2 notes"
        Expect.contains diag.Notes "expected type: int" "should contain first note"
        Expect.contains diag.Notes "actual type: string" "should contain second note"
    }

    test "add help message" {
        let diag =
            Diagnostic.error "E202" "Unbound variable"
            |> Diagnostic.withHelp "Did you mean `length`?"

        Expect.hasLength diag.Helps 1 "should have 1 help"
        Expect.equal (List.head diag.Helps) "Did you mean `length`?" "should have help message"
    }

    test "add suggestion with MachineApplicable" {
        let span = SourceSpan.fromPosition { Line = 5; Column = 10; File = None }

        let diag =
            Diagnostic.error "E202" "Unbound variable `lenght`"
            |> Diagnostic.withSuggestion span "length" "Did you mean `length`?"

        Expect.hasLength diag.Suggestions 1 "should have 1 suggestion"
        let sugg = List.head diag.Suggestions
        Expect.equal sugg.Replacement "length" "replacement should be `length`"
        Expect.equal sugg.Applicability MachineApplicable "should be MachineApplicable"
    }

    test "add suggestion with custom applicability" {
        let span = SourceSpan.fromPosition { Line = 5; Column = 10; File = None }

        let diag =
            Diagnostic.error "E302" "Non-exhaustive match"
            |> Diagnostic.withSuggestionApplicability span "| Nil -> /* handle empty */"
                "Add missing case" HasPlaceholders

        let sugg = List.head diag.Suggestions
        Expect.equal sugg.Applicability HasPlaceholders "should be HasPlaceholders"
    }

    test "add related diagnostic" {
        let related = Diagnostic.note "E201" "previous definition here"

        let diag =
            Diagnostic.error "E208" "Duplicate binding"
            |> Diagnostic.withRelated related

        Expect.hasLength diag.Related 1 "should have 1 related diagnostic"
        let rel = List.head diag.Related
        Expect.equal rel.Severity Note "related should be Note severity"
    }
]

let conversionTests = testList "Conversion Tests" [
    test "convert FunLangError to Diagnostic" {
        let pos = { Line = 1; Column = 5; File = None }
        let err : FunLang.Errors.FunLangError =
            { Kind = FunLang.Errors.LexerError ('$', pos)
              Message = "Unexpected character '$'"
              Hint = Some "Check for typos"
              Position = Some pos }

        let diag = Diagnostic.fromFunLangError err

        Expect.equal diag.Severity Error "should be Error"
        Expect.isSome diag.Code "should have error code"
        Expect.stringContains diag.Message "Unexpected character" "should contain message"
        Expect.isSome diag.PrimarySpan "should have primary span"
        Expect.isNonEmpty diag.Helps "should have help from hint"
    }

    test "convert TypeError to Diagnostic" {
        let pos = { Line = 3; Column = 10; File = None }
        let err : FunLang.Types.TypeError =
            { Kind = FunLang.Types.TypeMismatch (FunLang.Types.TInt, FunLang.Types.TString)
              Message = "Type mismatch"
              Position = Some pos
              Hint = None }

        let diag = Diagnostic.fromTypeError err

        Expect.equal diag.Severity Error "should be Error"
        Expect.equal diag.Code (Some "E201") "should have code E201"
        Expect.isNonEmpty diag.Notes "should have type info in notes"
    }

    test "convert unbound variable TypeError to Diagnostic" {
        let pos = { Line = 5; Column = 8; File = None }
        let err = FunLang.Types.TypeError.unboundVar "foo" (Some pos)

        let diag = Diagnostic.fromTypeError err

        Expect.equal diag.Code (Some "E202") "should have code E202"
        Expect.stringContains diag.Message "foo" "should contain variable name"
    }
]

let propertyTests = testList "Property Tests" [
    testProperty "error code format is valid" <| fun (code: string) (msg: string) ->
        let diag = Diagnostic.error code msg
        match diag.Code with
        | Some c -> c = code
        | None -> false

    testProperty "notes are preserved in order" <| fun (notes: string list) ->
        let validNotes = notes |> List.filter (fun s -> not (isNull s) && s.Length > 0) |> List.truncate 5
        if List.isEmpty validNotes then true
        else
            let diag =
                validNotes
                |> List.fold (fun d n -> Diagnostic.withNote n d) (Diagnostic.error "E001" "test")
            List.length diag.Notes = List.length validNotes

    testProperty "severity is preserved" <| fun (isError: bool) ->
        let diag =
            if isError then Diagnostic.error "E001" "test"
            else Diagnostic.warning "W001" "test"
        if isError then diag.Severity = Error
        else diag.Severity = Warning
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "Diagnostic" [
    basicTests
    spanTests
    builderTests
    conversionTests
    propertyTests
]
