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
        let state = { IndentStack = [0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false; JustSawTry = false; JustSawModule = false; PrevToken = None }
        let (newState, tokens) = processNewline defaultConfig state 0
        Expect.equal tokens [] "Same level should emit no tokens"
        Expect.equal newState.IndentStack [0] "Stack unchanged"
    }

    test "deeper indent emits INDENT" {
        let state = { IndentStack = [0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false; JustSawTry = false; JustSawModule = false; PrevToken = None }
        let (newState, tokens) = processNewline defaultConfig state 4
        Expect.equal tokens [INDENT] "Should emit INDENT"
        Expect.equal newState.IndentStack [4; 0] "Stack should push 4"
    }

    test "shallower indent emits DEDENT" {
        let state = { IndentStack = [4; 0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false; JustSawTry = false; JustSawModule = false; PrevToken = None }
        let (newState, tokens) = processNewline defaultConfig state 0
        Expect.equal tokens [DEDENT] "Should emit DEDENT"
        Expect.equal newState.IndentStack [0] "Stack should pop to [0]"
    }

    test "multiple dedents for big unindent" {
        let state = { IndentStack = [8; 4; 0]; LineNum = 1; Context = [TopLevel]; JustSawMatch = false; JustSawTry = false; JustSawModule = false; PrevToken = None }
        let (newState, tokens) = processNewline defaultConfig state 0
        Expect.equal tokens [DEDENT; DEDENT] "Should emit 2 DEDENTs"
        Expect.equal newState.IndentStack [0] "Stack should be [0]"
    }

    test "invalid indent throws error" {
        let state = { IndentStack = [4; 0]; LineNum = 5; Context = [TopLevel]; JustSawMatch = false; JustSawTry = false; JustSawModule = false; PrevToken = None }
        Expect.throws
            (fun () -> processNewline defaultConfig state 2 |> ignore)
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

[<Tests>]
let matchExpressionTests = testList "IndentFilter.matchExpressions" [
    test "testMatchPipeAlignment - pipes align with match keyword" {
        // match x with
        // | Some _ -> 1
        // | None -> 0
        let input = [
            Parser.MATCH; Parser.IDENT "x"; Parser.WITH; Parser.NEWLINE 0;
            Parser.PIPE; Parser.IDENT "Some"; Parser.UNDERSCORE; Parser.ARROW; Parser.NUMBER 1; Parser.NEWLINE 0;
            Parser.PIPE; Parser.IDENT "None"; Parser.ARROW; Parser.NUMBER 0; Parser.EOF
        ]
        let output = filter defaultConfig input |> Seq.toList
        // Pipes at same level as match should not emit INDENT/DEDENT
        let expected = [
            Parser.MATCH; Parser.IDENT "x"; Parser.WITH;
            Parser.PIPE; Parser.IDENT "Some"; Parser.UNDERSCORE; Parser.ARROW; Parser.NUMBER 1;
            Parser.PIPE; Parser.IDENT "None"; Parser.ARROW; Parser.NUMBER 0; Parser.EOF
        ]
        Expect.equal output expected "Pipes aligned with match should not create indentation"
    }

    test "testMatchPipeMisalignment - error when pipes don't align" {
        // match x with
        //     | Some _ -> 1  // Wrong - indented from match
        let input = [
            Parser.MATCH; Parser.IDENT "x"; Parser.WITH; Parser.NEWLINE 4;
            Parser.PIPE; Parser.IDENT "Some"; Parser.UNDERSCORE; Parser.ARROW; Parser.NUMBER 1; Parser.EOF
        ]
        Expect.throws
            (fun () -> filter defaultConfig input |> Seq.toList |> ignore)
            "Should throw for misaligned pipe"
    }

    test "testNestedMatch - nested match expressions with different base columns" {
        // match x with
        // | Some y ->
        //     match y with
        //     | 0 -> "zero"
        //     | _ -> "other"
        // | None -> "none"
        let input = [
            Parser.MATCH; Parser.IDENT "x"; Parser.WITH; Parser.NEWLINE 0;
            Parser.PIPE; Parser.IDENT "Some"; Parser.IDENT "y"; Parser.ARROW; Parser.NEWLINE 4;
            Parser.MATCH; Parser.IDENT "y"; Parser.WITH; Parser.NEWLINE 4;
            Parser.PIPE; Parser.NUMBER 0; Parser.ARROW; Parser.STRING "zero"; Parser.NEWLINE 4;
            Parser.PIPE; Parser.UNDERSCORE; Parser.ARROW; Parser.STRING "other"; Parser.NEWLINE 0;
            Parser.PIPE; Parser.IDENT "None"; Parser.ARROW; Parser.STRING "none"; Parser.EOF
        ]
        let output = filter defaultConfig input |> Seq.toList
        // Inner match at column 4 should track independently
        let expected = [
            Parser.MATCH; Parser.IDENT "x"; Parser.WITH;
            Parser.PIPE; Parser.IDENT "Some"; Parser.IDENT "y"; Parser.ARROW; Parser.INDENT;
            Parser.MATCH; Parser.IDENT "y"; Parser.WITH;
            Parser.PIPE; Parser.NUMBER 0; Parser.ARROW; Parser.STRING "zero";
            Parser.PIPE; Parser.UNDERSCORE; Parser.ARROW; Parser.STRING "other"; Parser.DEDENT;
            Parser.PIPE; Parser.IDENT "None"; Parser.ARROW; Parser.STRING "none"; Parser.EOF
        ]
        Expect.equal output expected "Nested match should track separate base columns"
    }

    test "testMatchResultIndentation - pattern results indent one level" {
        // match x with
        // | Some _ ->
        //     let y = 10
        //     y + 1
        // | None -> 0
        let input = [
            Parser.MATCH; Parser.IDENT "x"; Parser.WITH; Parser.NEWLINE 0;
            Parser.PIPE; Parser.IDENT "Some"; Parser.UNDERSCORE; Parser.ARROW; Parser.NEWLINE 4;
            Parser.LET; Parser.IDENT "y"; Parser.EQUALS; Parser.NUMBER 10; Parser.NEWLINE 4;
            Parser.IDENT "y"; Parser.PLUS; Parser.NUMBER 1; Parser.NEWLINE 0;
            Parser.PIPE; Parser.IDENT "None"; Parser.ARROW; Parser.NUMBER 0; Parser.EOF
        ]
        let output = filter defaultConfig input |> Seq.toList
        let expected = [
            Parser.MATCH; Parser.IDENT "x"; Parser.WITH;
            Parser.PIPE; Parser.IDENT "Some"; Parser.UNDERSCORE; Parser.ARROW; Parser.INDENT;
            Parser.LET; Parser.IDENT "y"; Parser.EQUALS; Parser.NUMBER 10; Parser.IN;
            Parser.IDENT "y"; Parser.PLUS; Parser.NUMBER 1; Parser.DEDENT;
            Parser.PIPE; Parser.IDENT "None"; Parser.ARROW; Parser.NUMBER 0; Parser.EOF
        ]
        Expect.equal output expected "Pattern results should indent one level from pipe with implicit IN"
    }
]

[<Tests>]
let errorMessageTests = testList "IndentFilter.errorMessages" [
    test "testIndentErrorMessage - error message includes line, column, and expected levels" {
        // Stack: [4; 0], try to indent to column 2 (not matching any level)
        let input = [
            Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.NEWLINE 4;
            Parser.NUMBER 1; Parser.NEWLINE 2;  // Invalid: col 2 doesn't match [4; 0]
            Parser.NUMBER 2; Parser.EOF
        ]
        try
            filter defaultConfig input |> Seq.toList |> ignore
            failtest "Should throw IndentationError"
        with
        | IndentationError(line, msg) ->
            Expect.equal line 2 "Error should be on line 2"
            Expect.stringContains msg "line 2" "Message should include line number"
            Expect.stringContains msg "column 2" "Message should include actual column"
            Expect.stringContains msg "[0, 4]" "Message should show expected indent levels"
    }

    test "testMatchPipeErrorMessage - match pipe alignment error is clear" {
        // match x with
        //     | Some _ -> 1  // Wrong - indented from match
        let input = [
            Parser.MATCH; Parser.IDENT "x"; Parser.WITH; Parser.NEWLINE 4;
            Parser.PIPE; Parser.IDENT "Some"; Parser.UNDERSCORE; Parser.ARROW; Parser.NUMBER 1; Parser.EOF
        ]
        try
            filter defaultConfig input |> Seq.toList |> ignore
            failtest "Should throw IndentationError for misaligned pipe"
        with
        | IndentationError(line, msg) ->
            Expect.stringContains msg "Match pipe" "Message should mention match pipe"
            Expect.stringContains msg "column 4" "Message should include actual column"
            Expect.stringContains msg "column 0" "Message should include expected column"
    }
]

[<Tests>]
let indentWidthValidationTests = testList "IndentFilter.indentWidthValidation" [
    test "testStrictIndentWidthViolation - strict mode rejects non-multiples" {
        let strictConfig = { IndentWidth = 4; StrictWidth = true }
        let input = [
            Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.NEWLINE 2;  // 2 is not multiple of 4
            Parser.NUMBER 1; Parser.EOF
        ]
        try
            filter strictConfig input |> Seq.toList |> ignore
            failtest "Should throw IndentationError for non-multiple indent"
        with
        | IndentationError(_, msg) ->
            Expect.stringContains msg "multiple of 4" "Message should mention expected width"
            Expect.stringContains msg "found 2" "Message should include actual column"
    }

    test "testLenientIndentWidth - lenient mode allows any indent" {
        let lenientConfig = { IndentWidth = 4; StrictWidth = false }
        let input = [
            Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.NEWLINE 2;  // 2 is not multiple of 4
            Parser.NUMBER 1; Parser.EOF
        ]
        let output = filter lenientConfig input |> Seq.toList
        let expected = [
            Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.INDENT;
            Parser.NUMBER 1; Parser.DEDENT; Parser.EOF
        ]
        Expect.equal output expected "Lenient mode should allow any indent level"
    }

    test "testStrictIndentWidthMultiple - strict mode allows multiples" {
        let strictConfig = { IndentWidth = 4; StrictWidth = true }
        let input = [
            Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.NEWLINE 4;  // 4 is multiple of 4
            Parser.NUMBER 1; Parser.NEWLINE 8;  // 8 is multiple of 4
            Parser.NUMBER 2; Parser.EOF
        ]
        let output = filter strictConfig input |> Seq.toList
        let expected = [
            Parser.LET; Parser.IDENT "x"; Parser.EQUALS; Parser.INDENT;
            Parser.NUMBER 1; Parser.INDENT;
            Parser.NUMBER 2; Parser.DEDENT; Parser.DEDENT; Parser.EOF
        ]
        Expect.equal output expected "Strict mode should allow exact multiples"
    }
]
