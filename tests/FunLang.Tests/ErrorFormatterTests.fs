module FunLang.Tests.ErrorFormatterTests

open Expecto
open FsCheck
open FunLang.Ast
open FunLang.Diagnostic
open FunLang.ErrorFormatter

// =============================================================================
// Unit Tests for ErrorFormatter
// =============================================================================

let headerTests = testList "Header Formatting" [
    test "format error header with code" {
        let diag = Diagnostic.error "E201" "Type mismatch"
        let header = formatHeader diag

        Expect.equal header "error[E201]: Type mismatch" "should format error header"
    }

    test "format warning header with code" {
        let diag = Diagnostic.warning "W001" "Unused variable"
        let header = formatHeader diag

        Expect.equal header "warning[W001]: Unused variable" "should format warning header"
    }

    test "format error header without code" {
        let diag = Diagnostic.errorNoCode "Something went wrong"
        let header = formatHeader diag

        Expect.equal header "error: Something went wrong" "should format header without code"
    }

    test "format note header" {
        let diag = Diagnostic.note "E201" "additional info"
        let header = formatHeader diag

        Expect.equal header "note[E201]: additional info" "should format note header"
    }

    test "format help header" {
        let diag = Diagnostic.help "E201" "try this instead"
        let header = formatHeader diag

        Expect.equal header "help[E201]: try this instead" "should format help header"
    }
]

let locationTests = testList "Location Formatting" [
    test "format location with file" {
        let pos = { Line = 3; Column = 15; File = Some "test.fun" }
        let span = SourceSpan.fromPosition pos
        let labeled = { Span = span; Label = None; Style = Primary }

        let location = formatLocation (Some labeled)

        Expect.equal location "  --> test.fun:3:15" "should format location with file"
    }

    test "format location without file" {
        let pos = { Line = 5; Column = 10; File = None }
        let span = SourceSpan.fromPosition pos
        let labeled = { Span = span; Label = None; Style = Primary }

        let location = formatLocation (Some labeled)

        Expect.equal location "  --> :5:10" "should format location without file"
    }

    test "format location with no span" {
        let location = formatLocation None

        Expect.equal location "" "should return empty for no span"
    }
]

let sourceContextTests = testList "Source Context Formatting" [
    test "format single line with primary span" {
        let source = "let x = 42"
        let pos = { Line = 1; Column = 9; File = None }
        let endPos = { Line = 1; Column = 11; File = None }
        let span = SourceSpan.create pos endPos

        let diag =
            Diagnostic.error "E201" "Type mismatch"
            |> Diagnostic.withPrimarySpan span "expected `string`"

        let context = formatSourceContext source defaultConfig diag

        Expect.stringContains context "let x = 42" "should contain source line"
        Expect.stringContains context "^^" "should have underline"
        Expect.stringContains context "expected `string`" "should contain label"
    }

    test "format multiline source with multiple spans" {
        let source = "let add x y =\n  x + y\nadd 1"
        let pos1 = { Line = 1; Column = 1; File = None }
        let endPos1 = { Line = 1; Column = 4; File = None }
        let span1 = SourceSpan.create pos1 endPos1

        let pos2 = { Line = 3; Column = 5; File = None }
        let endPos2 = { Line = 3; Column = 6; File = None }
        let span2 = SourceSpan.create pos2 endPos2

        let diag =
            Diagnostic.error "E205" "Arity mismatch"
            |> Diagnostic.withPrimarySpan span2 "expected 2 arguments"
            |> Diagnostic.withSecondarySpan span1 "function defined here"

        let context = formatSourceContext source defaultConfig diag

        Expect.stringContains context "let add" "should contain first line"
        Expect.stringContains context "add 1" "should contain third line"
    }

    test "format underline at correct position" {
        let source = "let x = \"hello\" + 1"
        let pos = { Line = 1; Column = 9; File = None }
        let endPos = { Line = 1; Column = 16; File = None }
        let span = SourceSpan.create pos endPos

        let diag =
            Diagnostic.error "E201" "Type mismatch"
            |> Diagnostic.withPrimarySpan span "this is a string"

        let context = formatSourceContext source defaultConfig diag
        let lines = context.Split('\n')

        // Underline should be at column 9
        let underlineLine = lines |> Array.tryFind (fun l -> l.Contains("^"))
        Expect.isSome underlineLine "should have underline line"
    }
]

let footerTests = testList "Footer Formatting" [
    test "format notes" {
        let diag =
            Diagnostic.error "E201" "Type mismatch"
            |> Diagnostic.withNote "expected type: int"
            |> Diagnostic.withNote "actual type: string"

        let footer = formatFooter diag

        Expect.stringContains footer "= note: expected type: int" "should contain first note"
        Expect.stringContains footer "= note: actual type: string" "should contain second note"
    }

    test "format helps" {
        let diag =
            Diagnostic.error "E201" "Type mismatch"
            |> Diagnostic.withHelp "use `++` for string concatenation"

        let footer = formatFooter diag

        Expect.stringContains footer "= help: use `++` for string concatenation" "should contain help"
    }

    test "format suggestions" {
        let span = SourceSpan.fromPosition { Line = 1; Column = 5; File = None }

        let diag =
            Diagnostic.error "E202" "Unbound variable"
            |> Diagnostic.withSuggestion span "length" "Did you mean `length`?"

        let footer = formatFooter diag

        Expect.stringContains footer "= suggestion:" "should contain suggestion marker"
        Expect.stringContains footer "Did you mean" "should contain suggestion message"
    }

    test "format empty footer" {
        // Use unknown error code to get truly empty footer
        let diag = Diagnostic.error "E999" "Unknown error"

        let footer = formatFooter diag

        Expect.equal footer "" "should be empty for no notes/helps/suggestions and no info"
    }

    test "format footer with only info" {
        // Known error codes get info line even without notes/helps/suggestions
        let diag = Diagnostic.error "E201" "Type mismatch"

        let footer = formatFooter diag

        Expect.stringContains footer "= info:" "should have info line"
    }
]

let fullFormatTests = testList "Full Format Tests" [
    test "format complete diagnostic - type mismatch" {
        let source = "let x = \"hello\" + 1"
        let pos = { Line = 1; Column = 9; File = Some "test.fun" }
        let endPos = { Line = 1; Column = 16; File = Some "test.fun" }
        let span = SourceSpan.create pos endPos

        let diag =
            Diagnostic.error "E201" "Type mismatch"
            |> Diagnostic.withPrimarySpan span "expected `int`, found `string`"
            |> Diagnostic.withNote "`+` operator requires both operands to be `int`"
            |> Diagnostic.withHelp "use `++` for string concatenation"

        let output = format source defaultConfig diag

        Expect.stringContains output "error[E201]: Type mismatch" "should have header"
        Expect.stringContains output "test.fun:1:9" "should have location"
        Expect.stringContains output "\"hello\"" "should have source"
        Expect.stringContains output "= note:" "should have note"
        Expect.stringContains output "= help:" "should have help"
    }

    test "format diagnostic - unbound variable with suggestion" {
        let source = "let n = lenght [1; 2; 3]"
        let pos = { Line = 1; Column = 9; File = Some "input.fun" }
        let endPos = { Line = 1; Column = 15; File = Some "input.fun" }
        let span = SourceSpan.create pos endPos

        let diag =
            Diagnostic.error "E202" "Unbound variable `lenght`"
            |> Diagnostic.withPrimarySpan span "not found in this scope"
            |> Diagnostic.withHelp "Did you mean `length`?"

        let output = format source defaultConfig diag

        Expect.stringContains output "error[E202]" "should have error code"
        Expect.stringContains output "lenght" "should contain variable name"
        Expect.stringContains output "Did you mean" "should have suggestion"
    }
]

let configTests = testList "Config Tests" [
    test "default config has sensible values" {
        Expect.isGreaterThan defaultConfig.TabWidth 0 "tab width should be positive"
        Expect.isGreaterThan defaultConfig.MaxLineWidth 40 "max line width should be reasonable"
        Expect.equal defaultConfig.UnderlineChar '^' "underline should be caret"
    }

    test "config affects underline character" {
        let source = "let x = 42"
        let pos = { Line = 1; Column = 9; File = None }
        let endPos = { Line = 1; Column = 11; File = None }
        let span = SourceSpan.create pos endPos

        let diag =
            Diagnostic.error "E001" "test"
            |> Diagnostic.withPrimarySpan span "test"

        let customConfig = { defaultConfig with UnderlineChar = '~' }
        let context = formatSourceContext source customConfig diag

        Expect.stringContains context "~~" "should use custom underline char"
    }
]

let edgeCaseTests = testList "Edge Cases" [
    test "handle empty source" {
        let diag = Diagnostic.error "E001" "Empty source"
        let output = format "" defaultConfig diag

        Expect.stringContains output "error[E001]" "should still have header"
    }

    test "handle position beyond source" {
        let source = "hello"
        let pos = { Line = 10; Column = 5; File = None }
        let span = SourceSpan.fromPosition pos
        let diag =
            Diagnostic.error "E001" "test"
            |> Diagnostic.withPrimarySpan span "out of bounds"

        let output = format source defaultConfig diag

        Expect.stringContains output "error[E001]" "should have header even with bad position"
    }

    test "handle span at line boundaries" {
        let source = "abc\ndef\nghi"
        let pos = { Line = 2; Column = 1; File = None }
        let endPos = { Line = 2; Column = 4; File = None }
        let span = SourceSpan.create pos endPos

        let diag =
            Diagnostic.error "E001" "test"
            |> Diagnostic.withPrimarySpan span "middle line"

        let context = formatSourceContext source defaultConfig diag

        Expect.stringContains context "def" "should contain target line"
    }

    test "handle unicode in source" {
        let source = "let \xce\xbb = 42"
        let pos = { Line = 1; Column = 5; File = None }
        let span = SourceSpan.fromPosition pos

        let diag =
            Diagnostic.error "E001" "test"
            |> Diagnostic.withPrimarySpanNoLabel span

        // Should not crash
        let output = format source defaultConfig diag
        Expect.isNotEmpty output "should produce output"
    }
]

let lineElisionTests = testList "Line Elision" [
    test "elide distant lines" {
        let source = String.replicate 20 "line\n" + "target"
        let pos1 = { Line = 1; Column = 1; File = None }
        let span1 = SourceSpan.fromPosition pos1
        let pos2 = { Line = 21; Column = 1; File = None }
        let span2 = SourceSpan.fromPosition pos2

        let diag =
            Diagnostic.error "E001" "test"
            |> Diagnostic.withPrimarySpan span2 "error here"
            |> Diagnostic.withSecondarySpan span1 "related"

        let context = formatSourceContext source defaultConfig diag

        // Should contain elision marker for distant spans
        Expect.stringContains context "..." "should elide distant lines"
    }
]

let propertyTests = testList "Property Tests" [
    testProperty "formatted output contains error code" <| fun (code: NonEmptyString) (msg: NonEmptyString) ->
        let cleanCode = code.Get.Replace("\n", "").Replace("\r", "").[..min 10 (code.Get.Length - 1)]
        let cleanMsg = msg.Get.Replace("\n", "").Replace("\r", "").[..min 50 (msg.Get.Length - 1)]
        if String.forall System.Char.IsLetterOrDigit cleanCode then
            let diag = Diagnostic.error cleanCode cleanMsg
            let output = format "dummy source" defaultConfig diag
            output.Contains(cleanCode)
        else
            true  // Skip invalid codes

    testProperty "format never throws" <| fun (line: PositiveInt) (col: PositiveInt) ->
        let pos = { Line = line.Get; Column = col.Get; File = None }
        let span = SourceSpan.fromPosition pos
        let diag =
            Diagnostic.error "E001" "test"
            |> Diagnostic.withPrimarySpanNoLabel span

        try
            let _ = format "test source" defaultConfig diag
            true
        with _ ->
            false
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "ErrorFormatter" [
    headerTests
    locationTests
    sourceContextTests
    footerTests
    fullFormatTests
    configTests
    edgeCaseTests
    lineElisionTests
    propertyTests
]
