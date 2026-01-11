module FunLang.Tests.LocatedTests

open Expecto
open FsCheck
open FunLang.Ast

// =============================================================================
// Unit Tests for Located<'T> wrapper type
// =============================================================================

let unitTests = testList "Located Unit Tests" [
    test "Located.create wraps value with position" {
        let pos = { Line = 1; Column = 5; File = Some "test.fun" }
        let located = Located.create pos 42
        Expect.equal located.Node 42 "Node should be 42"
        Expect.equal located.Pos pos "Position should match"
    }

    test "Located.noLoc wraps value with noPos" {
        let located = Located.noLoc "hello"
        Expect.equal located.Node "hello" "Node should be 'hello'"
        Expect.equal located.Pos noPos "Position should be noPos"
    }

    test "Located.map transforms the node" {
        let pos = { Line = 2; Column = 3; File = None }
        let located = Located.create pos 10
        let mapped = Located.map (fun x -> x * 2) located
        Expect.equal mapped.Node 20 "Node should be doubled"
        Expect.equal mapped.Pos pos "Position should be preserved"
    }

    test "Located.pos extracts position" {
        let pos = { Line = 5; Column = 10; File = Some "main.fun" }
        let located = Located.create pos "test"
        Expect.equal (Located.pos located) pos "Should extract position"
    }

    test "Located.node extracts node" {
        let located = Located.noLoc 42
        Expect.equal (Located.node located) 42 "Should extract node"
    }
]

// =============================================================================
// Property Tests for Located<'T>
// =============================================================================

let propertyTests = testList "Located Property Tests" [
    testProperty "create preserves both node and position" <| fun (line: PositiveInt) (col: PositiveInt) (n: int) ->
        let pos = { Line = line.Get; Column = col.Get; File = None }
        let located = Located.create pos n
        located.Node = n && located.Pos = pos

    testProperty "noLoc always uses noPos" <| fun (n: int) ->
        let located = Located.noLoc n
        located.Pos = noPos

    testProperty "map preserves position" <| fun (line: PositiveInt) (col: PositiveInt) (n: int) ->
        let pos = { Line = line.Get; Column = col.Get; File = None }
        let located = Located.create pos n
        let mapped = Located.map (fun x -> x + 1) located
        mapped.Pos = pos

    testProperty "map applies function correctly" <| fun (n: int) ->
        let located = Located.noLoc n
        let f = fun x -> x * 2 + 1
        let mapped = Located.map f located
        mapped.Node = f n

    testProperty "node extracts what was put in" <| fun (n: int) ->
        let located = Located.noLoc n
        Located.node located = n

    testProperty "pos extracts what was put in" <| fun (line: PositiveInt) (col: PositiveInt) ->
        let pos = { Line = line.Get; Column = col.Get; File = None }
        let located = Located.create pos 0
        Located.pos located = pos
]

// =============================================================================
// LExpr and LPattern type alias tests
// =============================================================================

let typeAliasTests = testList "LExpr and LPattern Tests" [
    test "LExpr can wrap an Expr" {
        let expr = ELiteral (LInt 42)
        let lexpr: LExpr = Located.noLoc expr
        Expect.equal lexpr.Node expr "Should contain the expression"
    }

    test "LPattern can wrap a Pattern" {
        let pattern = PVariable "x"
        let lpattern: LPattern = Located.noLoc pattern
        Expect.equal lpattern.Node pattern "Should contain the pattern"
    }

    test "LExpr with position" {
        let pos = { Line = 1; Column = 1; File = None }
        let expr = EVariable "x"
        let lexpr: LExpr = Located.create pos expr
        Expect.equal lexpr.Pos pos "Should have position"
        Expect.equal lexpr.Node expr "Should have expression"
    }
]

[<Tests>]
let tests = testList "Located" [unitTests; propertyTests; typeAliasTests]
