module FunLang.Tests.ErrorExplanationTests

open Expecto
open FunLang.ErrorExplanations
module ErrorCodes = FunLang.Diagnostic.Diagnostic.ErrorCodes

// =============================================================================
// Unit Tests for ErrorExplanations API
// =============================================================================

let apiTests = testList "API" [
    test "getBrief returns None for unknown code" {
        let result = getBrief "E999"
        Expect.isNone result "unknown code should return None"
    }

    test "get returns None for unknown code" {
        let result = get "E999"
        Expect.isNone result "unknown code should return None"
    }

    test "getBrief returns Some for E202" {
        let result = getBrief ErrorCodes.unboundVariable
        Expect.isSome result "E202 should have a brief"
    }

    test "get returns complete explanation for E202" {
        let result = get ErrorCodes.unboundVariable
        Expect.isSome result "E202 should have explanation"

        let explanation = result.Value
        Expect.equal explanation.Code "E202" "code should be E202"
        Expect.isNotEmpty explanation.Title "title should not be empty"
        Expect.isNotEmpty explanation.Brief "brief should not be empty"
        Expect.isNotEmpty explanation.Explanation "explanation should not be empty"
        Expect.isNotEmpty explanation.BadExample "bad example should not be empty"
        Expect.isNotEmpty explanation.GoodExample "good example should not be empty"
    }

    test "hasExplanation returns true for E202" {
        Expect.isTrue (hasExplanation ErrorCodes.unboundVariable) "E202 should have explanation"
    }

    test "hasExplanation returns false for unknown code" {
        Expect.isFalse (hasExplanation "E999") "unknown code should not have explanation"
    }

    test "allCodes returns non-empty list" {
        let codes = allCodes ()
        Expect.isNonEmpty codes "should have at least one code"
    }

    test "allCodes returns sorted list" {
        let codes = allCodes ()
        let sorted = List.sort codes
        Expect.equal codes sorted "codes should be sorted"
    }
]

// =============================================================================
// Property Tests
// =============================================================================

let propertyTests = testList "Properties" [
    test "all defined error codes have explanations" {
        let requiredCodes = [
            // Lexer errors
            ErrorCodes.unexpectedChar
            ErrorCodes.unterminatedString
            ErrorCodes.invalidEscape
            ErrorCodes.invalidNumber
            // Parser errors
            ErrorCodes.unexpectedToken
            ErrorCodes.missingToken
            ErrorCodes.invalidSyntax
            ErrorCodes.indentationError
            ErrorCodes.unclosedDelimiter
            ErrorCodes.emptyBlock
            // Type errors
            ErrorCodes.typeMismatch
            ErrorCodes.unboundVariable
            ErrorCodes.infiniteType
            ErrorCodes.notAFunction
            ErrorCodes.arityMismatch
            ErrorCodes.patternTypeMismatch
            ErrorCodes.undefinedConstructor
            ErrorCodes.duplicateBinding
            // Runtime errors
            ErrorCodes.divisionByZero
            ErrorCodes.nonExhaustiveMatch
            ErrorCodes.invalidOperation
            ErrorCodes.stackOverflow
        ]

        for code in requiredCodes do
            Expect.isTrue (hasExplanation code) (sprintf "code %s should have explanation" code)
    }

    test "all briefs are concise (under 80 chars)" {
        let codes = allCodes ()
        for code in codes do
            let brief = getBrief code |> Option.get
            Expect.isLessThan brief.Length 80 (sprintf "brief for %s should be under 80 chars" code)
    }

    test "all explanations have non-empty fields" {
        let codes = allCodes ()
        for code in codes do
            let explanation = get code |> Option.get
            Expect.isNotEmpty explanation.Title (sprintf "%s title" code)
            Expect.isNotEmpty explanation.Brief (sprintf "%s brief" code)
            Expect.isNotEmpty explanation.Explanation (sprintf "%s explanation" code)
            Expect.isNotEmpty explanation.BadExample (sprintf "%s bad example" code)
            Expect.isNotEmpty explanation.GoodExample (sprintf "%s good example" code)
    }

    test "all codes in allCodes() are retrievable" {
        let codes = allCodes ()
        for code in codes do
            Expect.isTrue (hasExplanation code) (sprintf "%s should be retrievable" code)
            Expect.isSome (get code) (sprintf "%s should have full explanation" code)
            Expect.isSome (getBrief code) (sprintf "%s should have brief" code)
    }
]

// =============================================================================
// Format Tests
// =============================================================================

let formatTests = testList "Format" [
    test "explanation has proper structure" {
        let explanation = get ErrorCodes.unboundVariable
        Expect.isSome explanation "should have explanation"

        let e = explanation.Value
        // Code format: E followed by 3 digits
        Expect.isTrue (e.Code.StartsWith("E")) "code should start with E"
        Expect.equal e.Code.Length 4 "code should be 4 characters"

        // Title should be human-readable
        Expect.isFalse (e.Title.Contains("_")) "title should not contain underscores"

        // Examples should look like code
        Expect.isFalse (System.String.IsNullOrWhiteSpace e.BadExample) "bad example should not be empty"
        Expect.isFalse (System.String.IsNullOrWhiteSpace e.GoodExample) "good example should not be empty"
    }
]

// =============================================================================
// Integration Tests - Inline Explanation in Error Output
// =============================================================================

module Diag = FunLang.Diagnostic

let integrationTests = testList "Integration" [
    test "error output includes info line" {
        // Create a diagnostic for E202
        let diag = Diag.Diagnostic.error ErrorCodes.unboundVariable "Unbound variable 'x'"
        let output = FunLang.ErrorFormatter.format "" FunLang.ErrorFormatter.defaultConfig diag

        Expect.stringContains output "= info:" "output should have info line"
        Expect.stringContains output "variables must be defined" "info should explain the error"
    }

    test "error output with suggestion and info" {
        let diag =
            Diag.Diagnostic.error ErrorCodes.unboundVariable "Unbound variable 'prnt'"
            |> Diag.Diagnostic.withHelp "did you mean `print`?"

        let output = FunLang.ErrorFormatter.format "" FunLang.ErrorFormatter.defaultConfig diag

        Expect.stringContains output "= help: did you mean" "should have help"
        Expect.stringContains output "= info:" "should have info"
    }

    test "no info line for unknown error code" {
        let diag = Diag.Diagnostic.error "E999" "Unknown error"
        let output = FunLang.ErrorFormatter.format "" FunLang.ErrorFormatter.defaultConfig diag

        Expect.isFalse (output.Contains("= info:")) "should not have info line for unknown code"
    }

    test "formatExplanation produces expected output" {
        let explanation = get ErrorCodes.unboundVariable |> Option.get
        let output = formatExplanation explanation

        Expect.stringContains output "Error E202: Unbound variable" "should have title"
        Expect.stringContains output "Example of incorrect code:" "should have bad example section"
        Expect.stringContains output "How to fix:" "should have fix section"
        Expect.stringContains output "Related errors:" "should have related errors section"
    }

    test "formatAllCodes lists all codes" {
        let output = formatAllCodes ()

        Expect.stringContains output "FunLang Error Codes" "should have header"
        Expect.stringContains output "E001" "should list E001"
        Expect.stringContains output "E202" "should list E202"
        Expect.stringContains output "E301" "should list E301"
    }
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "ErrorExplanations" [
    apiTests
    propertyTests
    formatTests
    integrationTests
]
