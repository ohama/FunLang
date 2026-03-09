module LangThree.Tests.ExhaustiveTests

open Expecto
open Exhaustive

// ============================================================================
// Helper: Constructor Sets for Testing
// ============================================================================

/// Option type: None (arity 0), Some (arity 1)
let optionConstructors : ConstructorSet = [
    { Name = "None"; Arity = 0 }
    { Name = "Some"; Arity = 1 }
]

/// Bool-like type: True (arity 0), False (arity 0)
let boolConstructors : ConstructorSet = [
    { Name = "True"; Arity = 0 }
    { Name = "False"; Arity = 0 }
]

/// Tree type: Leaf (arity 0), Node (arity 3: left, value, right)
let treeConstructors : ConstructorSet = [
    { Name = "Leaf"; Arity = 0 }
    { Name = "Node"; Arity = 3 }
]

/// List type: Nil (arity 0), Cons (arity 2: head, tail)
let listConstructors : ConstructorSet = [
    { Name = "Nil"; Arity = 0 }
    { Name = "Cons"; Arity = 2 }
]

// ============================================================================
// Helper: Pattern Construction Shortcuts
// ============================================================================

let none = ConstructorPat("None", [])
let some p = ConstructorPat("Some", [p])
let wild = WildcardPat
let leaf = ConstructorPat("Leaf", [])
let node l v r = ConstructorPat("Node", [l; v; r])
let nil = ConstructorPat("Nil", [])
let cons h t = ConstructorPat("Cons", [h; t])
let ctrue = ConstructorPat("True", [])
let cfalse = ConstructorPat("False", [])

// ============================================================================
// Tests: Usefulness Algorithm (Core)
// ============================================================================

[<Tests>]
let usefulTests = testList "Usefulness" [
    test "first pattern is always useful" {
        // Empty matrix, any pattern is useful
        let result = useful optionConstructors [] [none]
        Expect.isTrue result "First pattern should always be useful"
    }

    test "wildcard useful when constructors not fully covered" {
        // Matrix has only Some, wildcard should be useful (None not covered)
        let matrix = [[some wild]]
        let result = useful optionConstructors matrix [wild]
        Expect.isTrue result "Wildcard should be useful when Some is the only pattern"
    }

    test "wildcard not useful when all constructors covered" {
        // Matrix has None and Some, wildcard is not useful
        let matrix = [[none]; [some wild]]
        let result = useful optionConstructors matrix [wild]
        Expect.isFalse result "Wildcard should not be useful when all constructors covered"
    }

    test "constructor useful when not yet covered" {
        // Matrix has None, Some should be useful
        let matrix = [[none]]
        let result = useful optionConstructors matrix [some wild]
        Expect.isTrue result "Some should be useful when only None is covered"
    }

    test "constructor not useful when already covered" {
        // Matrix has None, another None is not useful
        let matrix = [[none]]
        let result = useful optionConstructors matrix [none]
        Expect.isFalse result "Duplicate None should not be useful"
    }

    test "nested constructor useful for deep pattern" {
        // Option<Option<_>>: [None; Some None] leaves Some(Some _) uncovered
        let matrix = [[none]; [some none]]
        let result = useful optionConstructors matrix [some (some wild)]
        Expect.isTrue result "Some(Some _) should be useful when Some(None) is covered but not Some(Some _)"
    }

    test "wildcard after full nested coverage is not useful" {
        // Option<Option<_>>: [None; Some None; Some (Some _)] covers everything
        let matrix = [[none]; [some none]; [some (some wild)]]
        let result = useful optionConstructors matrix [wild]
        Expect.isFalse result "Wildcard should not be useful after full nested coverage"
    }
]

// ============================================================================
// Tests: Exhaustiveness Checking
// ============================================================================

[<Tests>]
let exhaustivenessTests = testList "Exhaustiveness" [
    test "incomplete Option - missing None" {
        let result = checkExhaustive optionConstructors [some wild]
        match result with
        | NonExhaustive missing ->
            Expect.isNonEmpty missing "Should have missing patterns"
            // The missing pattern should represent None
            let formatted = missing |> List.map formatPattern
            Expect.contains formatted "None" "Missing patterns should include None"
        | Exhaustive ->
            failtest "Should be non-exhaustive"
    }

    test "complete Option - None and Some" {
        let result = checkExhaustive optionConstructors [none; some wild]
        Expect.equal result Exhaustive "None + Some _ should be exhaustive"
    }

    test "incomplete nested Option - missing Some(Some _)" {
        // Option<Option<_>>: patterns [None; Some None]
        let result = checkExhaustive optionConstructors [none; some none]
        match result with
        | NonExhaustive missing ->
            Expect.isNonEmpty missing "Should have missing patterns"
            let formatted = missing |> List.map formatPattern
            Expect.exists formatted (fun s -> s.Contains("Some") && s.Contains("Some"))
                "Missing pattern should involve nested Some"
        | Exhaustive ->
            failtest "Should be non-exhaustive with nested patterns"
    }

    test "wildcard makes exhaustive" {
        let result = checkExhaustive optionConstructors [some wild; wild]
        Expect.equal result Exhaustive "Some _ + _ should be exhaustive"
    }

    test "single wildcard is exhaustive" {
        let result = checkExhaustive optionConstructors [wild]
        Expect.equal result Exhaustive "Single wildcard should be exhaustive"
    }

    test "tree type - Leaf and Node exhaustive" {
        let result = checkExhaustive treeConstructors [leaf; node wild wild wild]
        Expect.equal result Exhaustive "Leaf + Node _ _ _ should be exhaustive"
    }

    test "tree type - missing Node" {
        let result = checkExhaustive treeConstructors [leaf]
        match result with
        | NonExhaustive missing ->
            let formatted = missing |> List.map formatPattern
            Expect.exists formatted (fun s -> s.Contains("Node"))
                "Missing pattern should include Node"
        | Exhaustive ->
            failtest "Should be non-exhaustive without Node"
    }

    test "bool type - all cases covered" {
        let result = checkExhaustive boolConstructors [ctrue; cfalse]
        Expect.equal result Exhaustive "True + False should be exhaustive"
    }

    test "bool type - missing False" {
        let result = checkExhaustive boolConstructors [ctrue]
        match result with
        | NonExhaustive missing ->
            let formatted = missing |> List.map formatPattern
            Expect.contains formatted "False" "Missing patterns should include False"
        | Exhaustive ->
            failtest "Should be non-exhaustive without False"
    }
]

// ============================================================================
// Tests: Redundancy Checking
// ============================================================================

[<Tests>]
let redundancyTests = testList "Redundancy" [
    test "no redundancy in complete pattern" {
        let result = checkRedundant optionConstructors [none; some wild]
        Expect.equal result NoRedundancy "None + Some _ should have no redundancy"
    }

    test "redundant duplicate pattern" {
        let result = checkRedundant optionConstructors [none; some wild; none]
        match result with
        | HasRedundancy indices ->
            Expect.contains indices 2 "Third pattern (index 2) should be redundant"
        | NoRedundancy ->
            failtest "Should detect redundant None"
    }

    test "wildcard after full coverage is redundant" {
        let result = checkRedundant optionConstructors [none; some wild; wild]
        match result with
        | HasRedundancy indices ->
            Expect.contains indices 2 "Wildcard after full coverage (index 2) should be redundant"
        | NoRedundancy ->
            failtest "Should detect redundant wildcard"
    }

    test "specific after wildcard is redundant" {
        let result = checkRedundant optionConstructors [wild; none]
        match result with
        | HasRedundancy indices ->
            Expect.contains indices 1 "None after wildcard (index 1) should be redundant"
        | NoRedundancy ->
            failtest "Should detect redundant pattern after wildcard"
    }

    test "no redundancy with wildcard only" {
        let result = checkRedundant optionConstructors [wild]
        Expect.equal result NoRedundancy "Single wildcard should have no redundancy"
    }
]

// ============================================================================
// Tests: Pattern Formatting
// ============================================================================

[<Tests>]
let formatTests = testList "PatternFormat" [
    test "format nullary constructor" {
        let result = formatPattern none
        Expect.equal result "None" "None should format as 'None'"
    }

    test "format unary constructor with wildcard" {
        let result = formatPattern (some wild)
        Expect.equal result "Some _" "Some _ should format as 'Some _'"
    }

    test "format nested constructor" {
        let result = formatPattern (some (some wild))
        Expect.equal result "Some (Some _)" "Nested Some should format with parens"
    }

    test "format wildcard" {
        let result = formatPattern wild
        Expect.equal result "_" "Wildcard should format as '_'"
    }

    test "format multi-arg constructor" {
        let result = formatPattern (node wild wild wild)
        Expect.equal result "Node(_, _, _)" "Multi-arg constructor should format with parens and commas"
    }
]

// ============================================================================
// Tests: Specialize Matrix
// ============================================================================

[<Tests>]
let specializeTests = testList "SpecializeMatrix" [
    test "specialize removes non-matching constructors" {
        let matrix = [[none]; [some wild]]
        let someInfo = { Name = "Some"; Arity = 1 }
        let result = specializeMatrix someInfo matrix
        // None row should be removed, Some row should expose its argument
        Expect.hasLength result 1 "Only Some row should remain"
        Expect.equal result [[wild]] "Some _ should specialize to [_]"
    }

    test "specialize expands wildcard to match arity" {
        let matrix = [[wild]]
        let someInfo = { Name = "Some"; Arity = 1 }
        let result = specializeMatrix someInfo matrix
        // Wildcard should expand to [_] for arity 1
        Expect.hasLength result 1 "Wildcard row should remain"
        Expect.equal result [[wild]] "Wildcard should expand to [_]"
    }

    test "specialize handles empty matrix" {
        let matrix : CasePat list list = []
        let noneInfo = { Name = "None"; Arity = 0 }
        let result = specializeMatrix noneInfo matrix
        Expect.isEmpty result "Empty matrix should specialize to empty"
    }

    test "specialize multi-arg constructor" {
        let matrix = [[node wild wild wild]; [leaf]]
        let nodeInfo = { Name = "Node"; Arity = 3 }
        let result = specializeMatrix nodeInfo matrix
        // Node row becomes [_, _, _], Leaf row is removed
        Expect.hasLength result 1 "Only Node row should remain"
        Expect.equal result [[wild; wild; wild]] "Node(_, _, _) should specialize to [_, _, _]"
    }
]
