module LangThree.Tests.IndentFilterTests

open Expecto
open LangThree.IndentFilter
open Parser

[<Tests>]
let configTests = testList "IndentFilter.Config" [
    test "defaultConfig has IndentWidth 4" {
        Expect.equal defaultConfig.IndentWidth 4 "Default indent should be 4"
    }

    test "initialState starts with stack [0]" {
        Expect.equal initialState.IndentStack [0] "Initial stack should be [0]"
    }
]

[<Tests>]
let processNewlineTests = testList "IndentFilter.processNewline" [
    test "same indent level emits no tokens" {
        let state = { IndentStack = [0]; LineNum = 1 }
        let (newState, tokens) = processNewline state 0
        Expect.equal tokens [] "Same level should emit no tokens"
        Expect.equal newState.IndentStack [0] "Stack unchanged"
    }

    test "deeper indent emits INDENT" {
        let state = { IndentStack = [0]; LineNum = 1 }
        let (newState, tokens) = processNewline state 4
        Expect.equal tokens [INDENT] "Should emit INDENT"
        Expect.equal newState.IndentStack [4; 0] "Stack should push 4"
    }

    test "shallower indent emits DEDENT" {
        let state = { IndentStack = [4; 0]; LineNum = 1 }
        let (newState, tokens) = processNewline state 0
        Expect.equal tokens [DEDENT] "Should emit DEDENT"
        Expect.equal newState.IndentStack [0] "Stack should pop to [0]"
    }

    test "multiple dedents for big unindent" {
        let state = { IndentStack = [8; 4; 0]; LineNum = 1 }
        let (newState, tokens) = processNewline state 0
        Expect.equal tokens [DEDENT; DEDENT] "Should emit 2 DEDENTs"
        Expect.equal newState.IndentStack [0] "Stack should be [0]"
    }

    test "invalid indent throws error" {
        let state = { IndentStack = [4; 0]; LineNum = 5 }
        Expect.throws
            (fun () -> processNewline state 2 |> ignore)
            "Should throw for misaligned indent"
    }
]

[<Tests>]
let filterTests = testList "IndentFilter.filter" [
    test "passes through non-NEWLINE tokens" {
        let input = [Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.NUMBER 1]
        let output = filter defaultConfig input |> Seq.toList
        Expect.equal output input "Should pass through unchanged"
    }

    test "converts NEWLINE to INDENT on deeper" {
        let input = [Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.NEWLINE 4; Parser.NUMBER 1]
        let output = filter defaultConfig input |> Seq.toList
        let expected = [Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.INDENT; Parser.NUMBER 1]
        Expect.equal output expected "NEWLINE(4) should become INDENT"
    }

    test "emits DEDENT at end for open indents" {
        let input = [Parser.LET; Parser.NEWLINE 4; Parser.NUMBER 1; Parser.EOF]
        let output = filter defaultConfig input |> Seq.toList
        let expected = [Parser.LET; Parser.INDENT; Parser.NUMBER 1; Parser.DEDENT; Parser.EOF]
        Expect.equal output expected "Should emit DEDENT before EOF"
    }
]
