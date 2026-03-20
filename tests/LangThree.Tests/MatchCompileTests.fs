module LangThree.Tests.MatchCompileTests

open Expecto
open LangThree.Tests.ModuleTests  // reuse evalModule

[<Tests>]
let matchCompileTests = testList "Match Compilation (Decision Tree)" [

    // =====================================================
    // PMATCH-01: ADT Constructor Patterns
    // =====================================================

    testList "PMATCH-01: ADT Constructor Patterns" [
        test "simple Option match - Some" {
            let result = evalModule "type Option = None | Some of int\nlet result =\n    match Some 42 with\n    | None -> 0\n    | Some n -> n\n"
            Expect.equal result (Ast.IntValue 42) "Some 42 should extract 42"
        }

        test "multi-constructor Color match" {
            let result = evalModule "type Color = Red | Green | Blue\nlet result =\n    match Green with\n    | Red -> 1\n    | Green -> 2\n    | Blue -> 3\n"
            Expect.equal result (Ast.IntValue 2) "Green should match second arm"
        }

        test "constructor with nested data - Tree" {
            let result = evalModule "type Tree = Leaf | Node of Tree * int * Tree\nlet result =\n    match Node (Leaf, 42, Leaf) with\n    | Leaf -> 0\n    | Node (_, v, _) -> v\n"
            Expect.equal result (Ast.IntValue 42) "Node root value should be 42"
        }
    ]

    // =====================================================
    // PMATCH-01/02: Nested Patterns
    // =====================================================

    testList "Nested Patterns" [
        test "deep nested constructor match" {
            let result = evalModule "type Tree = Leaf | Node of Tree * int * Tree\nlet result =\n    match Node (Node (Leaf, 10, Leaf), 20, Leaf) with\n    | Node (Node (_, x, _), y, _) -> x + y\n    | _ -> 0\n"
            Expect.equal result (Ast.IntValue 30) "should extract nested values 10 + 20"
        }

        test "nested wrapper types" {
            let result = evalModule "type Pair = MkPair of int * int\ntype Wrapper = Wrap of Pair\nlet result =\n    match Wrap (MkPair (3, 7)) with\n    | Wrap (MkPair (a, b)) -> a + b\n"
            Expect.equal result (Ast.IntValue 10) "should unwrap nested constructors"
        }
    ]

    // =====================================================
    // PMATCH-02: List Patterns
    // =====================================================

    testList "List Patterns" [
        test "empty vs cons" {
            let result = evalModule "let result =\n    match [5; 6; 7] with\n    | [] -> 0\n    | h :: _ -> h\n"
            Expect.equal result (Ast.IntValue 5) "head of [5;6;7] should be 5"
        }

        test "nested cons pattern" {
            let result = evalModule "let result =\n    match [10; 20; 30] with\n    | [] -> 0\n    | _ :: [] -> 1\n    | a :: b :: _ -> a + b\n"
            Expect.equal result (Ast.IntValue 30) "10 + 20 = 30"
        }
    ]

    // =====================================================
    // PMATCH-02: Tuple Patterns
    // =====================================================

    testList "Tuple Patterns" [
        test "simple triple tuple" {
            let result = evalModule "let result =\n    match (1, 2, 3) with\n    | (a, b, c) -> a + b + c\n"
            Expect.equal result (Ast.IntValue 6) "(1,2,3) -> 1+2+3 = 6"
        }

        test "tuple with wildcards" {
            let result = evalModule "let result =\n    match (42, 99) with\n    | (x, _) -> x\n"
            Expect.equal result (Ast.IntValue 42) "extract first element"
        }
    ]

    // =====================================================
    // PMATCH-02: Constant Patterns
    // =====================================================

    testList "Constant Patterns" [
        test "integer constants with wildcard default" {
            let result = evalModule "let result =\n    match 3 with\n    | 1 -> 10\n    | 2 -> 20\n    | _ -> 0\n"
            Expect.equal result (Ast.IntValue 0) "3 matches wildcard"
        }

        test "boolean constants" {
            let result = evalModule "let result =\n    match true with\n    | true -> 1\n    | false -> 0\n"
            Expect.equal result (Ast.IntValue 1) "true -> 1"
        }
    ]

    // =====================================================
    // PMATCH-02: Record Patterns
    // =====================================================

    testList "Record Patterns" [
        test "simple record match" {
            let result = evalModule "type Point = { x: int; y: int }\nlet result =\n    match { x = 3; y = 7 } with\n    | { x = a; y = b } -> a + b\n"
            Expect.equal result (Ast.IntValue 10) "record field extraction"
        }
    ]

    // =====================================================
    // When Guards
    // =====================================================

    testList "When Guards" [
        test "guard with fallthrough" {
            let result = evalModule "type Option = None | Some of int\nlet result =\n    match Some 5 with\n    | Some n when n > 10 -> 1\n    | Some n -> n\n    | None -> 0\n"
            Expect.equal result (Ast.IntValue 5) "guard fails, falls through to next Some"
        }

        test "multiple guard fallthrough" {
            let result = evalModule "let result =\n    match 3 with\n    | x when x > 10 -> 100\n    | x when x > 5 -> 50\n    | x -> x\n"
            Expect.equal result (Ast.IntValue 3) "both guards fail, catch-all binds x=3"
        }
    ]

    // =====================================================
    // Wildcard/Variable Catch-all
    // =====================================================

    testList "Wildcard Catch-all" [
        test "wildcard catches unmatched constructor" {
            let result = evalModule "type AB = A | B | C\nlet result =\n    match C with\n    | A -> 1\n    | B -> 2\n    | _ -> 3\n"
            Expect.equal result (Ast.IntValue 3) "C matches wildcard"
        }
    ]

    // =====================================================
    // PMATCH-03: Heuristic/Correctness
    // =====================================================

    testList "Heuristic Correctness" [
        test "tuple of ADTs" {
            let result = evalModule "type AB = A | B\nlet result =\n    match (A, B) with\n    | (A, A) -> 1\n    | (A, B) -> 2\n    | (B, A) -> 3\n    | (B, B) -> 4\n"
            Expect.equal result (Ast.IntValue 2) "(A, B) -> 2"
        }
    ]

    // =====================================================
    // PMATCH-01: No Redundant Constructor Tests (unit test)
    // =====================================================

    testList "No Redundant Tests (structural)" [
        test "decision tree has no redundant constructor tests" {
            // Build clauses for: match x with A -> 1 | B -> 2 | C -> 3
            let dummySpan : Ast.Span = { FileName = "test"; StartLine = 0; StartColumn = 0; EndLine = 0; EndColumn = 0 }
            let clauses : Ast.MatchClause list = [
                (Ast.ConstructorPat("A", None, dummySpan), None, Ast.Number(1, dummySpan))
                (Ast.ConstructorPat("B", None, dummySpan), None, Ast.Number(2, dummySpan))
                (Ast.ConstructorPat("C", None, dummySpan), None, Ast.Number(3, dummySpan))
            ]
            let tree, _rootVar = MatchCompile.compileMatch clauses

            // Walk the tree: on any root-to-leaf path, no (testVar, ctorName) pair should appear twice
            let rec checkNoDuplicates (path: Set<int * string>) (node: MatchCompile.DecisionTree) =
                match node with
                | MatchCompile.Fail -> ()
                | MatchCompile.Leaf(_, _, _, fallback) ->
                    match fallback with
                    | Some fb -> checkNoDuplicates path fb
                    | None -> ()
                | MatchCompile.Switch(tv, ctor, _, ifMatch, ifNoMatch) ->
                    let key = (tv, ctor)
                    Expect.isFalse (Set.contains key path)
                        (sprintf "Redundant test: var %d against %s" tv ctor)
                    let path' = Set.add key path
                    checkNoDuplicates path' ifMatch
                    checkNoDuplicates path' ifNoMatch

            checkNoDuplicates Set.empty tree
        }
    ]

    // =====================================================
    // Additional edge case: empty list match
    // =====================================================

    testList "Edge Cases" [
        test "empty list match" {
            let result = evalModule "let result =\n    match [] with\n    | [] -> 1\n    | _ :: _ -> 2\n"
            Expect.equal result (Ast.IntValue 1) "empty list matches []"
        }
    ]
]
