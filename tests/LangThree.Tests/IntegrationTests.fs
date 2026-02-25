module LangThree.Tests.IntegrationTests

open Expecto
open LangThree.IndentFilter

// Helper to lex a string and get raw tokens
let lexString (input: string) =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<_>.FromString input
    Lexer.setInitialPos lexbuf "test"
    let rec collect () =
        let tok = Lexer.tokenize lexbuf
        if tok = Parser.EOF then [Parser.EOF]
        else tok :: collect ()
    collect ()

// Helper to lex and filter through IndentFilter
let lexAndFilter (input: string) =
    let rawTokens = lexString input
    filter defaultConfig rawTokens |> Seq.toList

[<Tests>]
let integrationTests = testList "Integration" [
    test "simple let with indent produces INDENT" {
        let input = "let x =\n    42"
        let tokens = lexAndFilter input
        Expect.contains tokens Parser.INDENT "Should have INDENT"
        Expect.contains tokens (Parser.NUMBER 42) "Should have NUMBER 42"
    }

    test "tab character raises error" {
        Expect.throws
            (fun () -> lexString "let x =\n\t42" |> ignore)
            "Tab should raise error"
    }

    test "let with dedent back to column 0" {
        let input = "let x =\n    42\nlet y = 1"
        let tokens = lexAndFilter input
        Expect.contains tokens Parser.INDENT "Should have INDENT"
        Expect.contains tokens Parser.DEDENT "Should have DEDENT"
    }
]
