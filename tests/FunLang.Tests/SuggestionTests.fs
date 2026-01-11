module FunLang.Tests.SuggestionTests

open Expecto
open FsCheck
open FunLang.Suggestions

// =============================================================================
// Unit Tests for Levenshtein Distance
// =============================================================================

let levenshteinTests = testList "Levenshtein Distance" [
    test "empty strings have distance 0" {
        Expect.equal (levenshteinDistance "" "") 0 "empty strings"
    }

    test "empty to non-empty has distance of length" {
        Expect.equal (levenshteinDistance "" "abc") 3 "empty to abc"
        Expect.equal (levenshteinDistance "abc" "") 3 "abc to empty"
    }

    test "identical strings have distance 0" {
        Expect.equal (levenshteinDistance "hello" "hello") 0 "identical"
        Expect.equal (levenshteinDistance "print" "print") 0 "print"
    }

    test "single character difference" {
        Expect.equal (levenshteinDistance "cat" "bat") 1 "cat->bat (substitution)"
        Expect.equal (levenshteinDistance "cat" "cats") 1 "cat->cats (insertion)"
        Expect.equal (levenshteinDistance "cats" "cat") 1 "cats->cat (deletion)"
    }

    test "transposition (two operations)" {
        Expect.equal (levenshteinDistance "ab" "ba") 2 "ab->ba"
        Expect.equal (levenshteinDistance "pritn" "print") 2 "pritn->print"
    }

    test "classic example: kitten to sitting" {
        Expect.equal (levenshteinDistance "kitten" "sitting") 3 "kitten->sitting"
    }

    test "common typos" {
        Expect.equal (levenshteinDistance "prnt" "print") 1 "prnt->print (missing i)"
        Expect.equal (levenshteinDistance "teh" "the") 2 "teh->the"
        Expect.equal (levenshteinDistance "lenght" "length") 2 "lenght->length"
    }
]

// =============================================================================
// Unit Tests for findSimilar
// =============================================================================

let findSimilarTests = testList "Find Similar" [
    test "finds exact match with distance 0" {
        let result = findSimilar "print" ["print"; "map"; "fold"]
        Expect.equal result ["print"] "exact match"
    }

    test "finds single typo correction" {
        let result = findSimilar "prnt" ["print"; "map"; "fold"]
        Expect.equal result ["print"] "prnt -> print"
    }

    test "finds multiple similar names" {
        let result = findSimilar "ma" ["map"; "max"; "fold"; "filter"]
        Expect.contains result "map" "should contain map"
        Expect.contains result "max" "should contain max"
        Expect.equal (List.length result) 2 "should have 2 suggestions"
    }

    test "returns empty list when no similar names" {
        let result = findSimilar "xyz" ["print"; "map"; "fold"]
        Expect.isEmpty result "no similar names"
    }

    test "respects distance threshold of 2" {
        let result = findSimilar "abc" ["abcdef"]  // distance = 3
        Expect.isEmpty result "distance 3 is too far"
    }

    test "sorts by distance then alphabetically" {
        let result = findSimilar "ma" ["max"; "map"; "mat"]  // all distance 1
        Expect.equal result ["map"; "mat"; "max"] "sorted alphabetically"
    }

    test "limits to 3 suggestions" {
        let candidates = ["aa"; "ab"; "ac"; "ad"; "ae"]
        let result = findSimilar "a" candidates  // all distance 1
        Expect.equal (List.length result) 3 "max 3 suggestions"
    }

    test "empty candidates returns empty list" {
        let result = findSimilar "foo" []
        Expect.isEmpty result "empty candidates"
    }

    test "empty name returns empty list" {
        let result = findSimilar "" ["foo"; "bar"]
        Expect.isEmpty result "empty name"
    }
]

// =============================================================================
// Property Tests
// =============================================================================

let propertyTests = testList "Properties" [
    testProperty "distance is symmetric" <| fun (s1: NonEmptyString) (s2: NonEmptyString) ->
        levenshteinDistance s1.Get s2.Get = levenshteinDistance s2.Get s1.Get

    testProperty "distance to self is 0" <| fun (s: NonEmptyString) ->
        levenshteinDistance s.Get s.Get = 0

    testProperty "distance is at most max length" <| fun (s1: NonEmptyString) (s2: NonEmptyString) ->
        let dist = levenshteinDistance s1.Get s2.Get
        dist <= max s1.Get.Length s2.Get.Length

    testProperty "distance is non-negative" <| fun (s1: NonEmptyString) (s2: NonEmptyString) ->
        levenshteinDistance s1.Get s2.Get >= 0

    testProperty "triangle inequality holds" <| fun (s1: NonEmptyString) (s2: NonEmptyString) (s3: NonEmptyString) ->
        let d12 = levenshteinDistance s1.Get s2.Get
        let d23 = levenshteinDistance s2.Get s3.Get
        let d13 = levenshteinDistance s1.Get s3.Get
        d13 <= d12 + d23

    testProperty "findSimilar returns subset of candidates" <| fun (name: NonEmptyString) (candidates: string list) ->
        let validCandidates = candidates |> List.filter (fun s -> not (isNull s) && s.Length > 0)
        let result = findSimilar name.Get validCandidates
        result |> List.forall (fun r -> List.contains r validCandidates)

    testProperty "findSimilar returns at most 3 items" <| fun (name: NonEmptyString) (candidates: string list) ->
        let validCandidates = candidates |> List.filter (fun s -> not (isNull s) && s.Length > 0)
        let result = findSimilar name.Get validCandidates
        List.length result <= 3
]

// =============================================================================
// Integration Tests - Full Error Flow
// =============================================================================

module Diag = FunLang.Diagnostic

let integrationTests = testList "Integration" [
    test "type error for typo includes suggestion" {
        // Parse and type-check an expression with a typo
        let code = "let print = 42 in prnt"
        let ast = FunLang.Parser.parseString code
        Expect.isOk ast "should parse"

        let result = FunLang.TypeInfer.inferType (Result.defaultValue (FunLang.Ast.ELiteral FunLang.Ast.LUnit) ast)
        Expect.isError result "should fail type check"

        match result with
        | Error err ->
            Expect.equal err.Kind (FunLang.Types.UnboundVariable "prnt") "should be unbound variable"
            Expect.contains err.Suggestions "print" "should suggest 'print'"
        | Ok _ -> failtest "expected error"
    }

    test "diagnostic includes 'did you mean' help" {
        // Create a type error with suggestions
        let err = FunLang.Types.TypeError.unboundVarWithSuggestions "prnt" None ["print"]
        let diag = Diag.Diagnostic.fromTypeError err

        Expect.isNonEmpty diag.Helps "should have help message"
        let helpText = List.head diag.Helps
        Expect.stringContains helpText "did you mean" "should say 'did you mean'"
        Expect.stringContains helpText "print" "should mention 'print'"
    }

    test "diagnostic with multiple suggestions" {
        let err = FunLang.Types.TypeError.unboundVarWithSuggestions "ma" None ["map"; "max"]
        let diag = Diag.Diagnostic.fromTypeError err

        Expect.hasLength diag.Helps 2 "should have 2 help messages"
        let firstHelp = diag.Helps |> List.rev |> List.head
        Expect.stringContains firstHelp "did you mean `map`" "first help is best match"
    }

    test "no suggestions when no similar names" {
        let err = FunLang.Types.TypeError.unboundVarWithSuggestions "xyz" None []
        let diag = Diag.Diagnostic.fromTypeError err

        Expect.isEmpty diag.Helps "should have no help messages"
    }

    test "formatted error output includes suggestion" {
        let code = "let length = 10 in lenght"
        let ast = FunLang.Parser.parseString code
        Expect.isOk ast "should parse"

        let result = FunLang.TypeInfer.inferType (Result.defaultValue (FunLang.Ast.ELiteral FunLang.Ast.LUnit) ast)

        match result with
        | Error err ->
            let diag = Diag.Diagnostic.fromTypeError err
            let output = FunLang.ErrorFormatter.format code FunLang.ErrorFormatter.defaultConfig diag
            Expect.stringContains output "did you mean" "output should include suggestion"
            Expect.stringContains output "length" "output should mention 'length'"
        | Ok _ -> failtest "expected error"
    }
]

// =============================================================================
// All Tests
// =============================================================================

[<Tests>]
let tests = testList "Suggestions" [
    levenshteinTests
    findSimilarTests
    propertyTests
    integrationTests
]
