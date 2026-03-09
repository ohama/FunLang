module LangThree.Tests.ExceptionTests

open Expecto
open LangThree.Tests.ModuleTests  // reuse parseModule, evalModule, expectTypeError

[<Tests>]
let exceptionTests = testList "Exceptions" [

    // =====================================================
    // EXC-01: Exception declarations
    // =====================================================

    testList "EXC-01: Exception declarations" [
        test "nullary exception declaration" {
            let result = evalModule "exception DivideByZero\nlet result = 0\n"
            Expect.equal result (Ast.IntValue 0) "nullary exception parses and evals"
        }

        test "exception with data" {
            let result = evalModule "exception ParseError of string\nlet result = 0\n"
            Expect.equal result (Ast.IntValue 0) "exception with data parses and evals"
        }

        test "exception with tuple data" {
            let result = evalModule "exception NetworkError of string * int\nlet result = 0\n"
            Expect.equal result (Ast.IntValue 0) "exception with tuple data parses and evals"
        }

        test "multiple exception declarations" {
            let result = evalModule "exception A\nexception B of int\nexception C of string * int\nlet result = 0\n"
            Expect.equal result (Ast.IntValue 0) "multiple exceptions parse and eval"
        }
    ]

    // =====================================================
    // EXC-02: raise
    // =====================================================

    testList "EXC-02: raise" [
        test "raise nullary exception" {
            let result = evalModule "exception DivideByZero\nlet result =\n    try\n        raise DivideByZero\n    with\n    | DivideByZero -> 42\n"
            Expect.equal result (Ast.IntValue 42) "raise nullary exception caught"
        }

        test "raise exception with data" {
            let result = evalModule "exception ParseError of string\nlet result =\n    try\n        raise (ParseError \"bad input\")\n    with\n    | ParseError msg -> msg\n"
            Expect.equal result (Ast.StringValue "bad input") "raise exception with data"
        }

        test "raise in if-else branch no exception" {
            let result = evalModule "exception DivideByZero\nlet safediv x y =\n    if y = 0 then raise DivideByZero else x / y\nlet result =\n    try\n        safediv 10 2\n    with\n    | DivideByZero -> 0\n"
            Expect.equal result (Ast.IntValue 5) "raise in if-else, no exception path"
        }

        test "raise in if-else branch triggers exception" {
            let result = evalModule "exception DivideByZero\nlet safediv x y =\n    if y = 0 then raise DivideByZero else x / y\nlet result =\n    try\n        safediv 10 0\n    with\n    | DivideByZero -> 0\n"
            Expect.equal result (Ast.IntValue 0) "raise in if-else, exception path"
        }

        test "raise non-exception type error" {
            expectTypeError "let result = raise 42\n" "E0301"
        }
    ]

    // =====================================================
    // EXC-03: try-with expressions
    // =====================================================

    testList "EXC-03: try-with" [
        test "basic try-with catches exception" {
            let result = evalModule "exception DivideByZero\nlet result =\n    try\n        raise DivideByZero\n    with\n    | DivideByZero -> 0\n"
            Expect.equal result (Ast.IntValue 0) "basic try-with"
        }

        test "try-with with data extraction" {
            let result = evalModule "exception ParseError of string\nlet result =\n    try\n        raise (ParseError \"oops\")\n    with\n    | ParseError msg -> msg\n"
            Expect.equal result (Ast.StringValue "oops") "extract data from caught exception"
        }

        test "try-with no exception returns body value" {
            let result = evalModule "exception DivideByZero\nlet result =\n    try\n        42\n    with\n    | DivideByZero -> 0\n"
            Expect.equal result (Ast.IntValue 42) "no exception returns body"
        }

        test "try-with body with let bindings" {
            let result = evalModule "exception DivideByZero\nlet result =\n    try\n        let x = 10 in let y = 20 in x + y\n    with\n    | DivideByZero -> 0\n"
            Expect.equal result (Ast.IntValue 30) "try body with let bindings"
        }
    ]

    // =====================================================
    // EXC-04: Pattern matching on exception types
    // =====================================================

    testList "EXC-04: Exception pattern matching" [
        test "multiple exception handlers second matches" {
            let result = evalModule "exception DivideByZero\nexception ParseError of string\nlet result =\n    try\n        raise (ParseError \"bad\")\n    with\n    | DivideByZero -> 0\n    | ParseError msg -> 1\n"
            Expect.equal result (Ast.IntValue 1) "second handler matches"
        }

        test "multiple exception handlers first matches" {
            let result = evalModule "exception DivideByZero\nexception ParseError of string\nlet result =\n    try\n        raise DivideByZero\n    with\n    | DivideByZero -> 0\n    | ParseError msg -> 1\n"
            Expect.equal result (Ast.IntValue 0) "first handler matches"
        }

        test "wildcard handler catches any exception" {
            let result = evalModule "exception DivideByZero\nlet result =\n    try\n        raise DivideByZero\n    with\n    | _ -> 99\n"
            Expect.equal result (Ast.IntValue 99) "wildcard catches all"
        }

        test "nested try-with inner catches" {
            let result = evalModule "exception A\nexception B\nlet result =\n    try\n        try\n            raise A\n        with\n        | A -> 10\n    with\n    | B -> 20\n"
            Expect.equal result (Ast.IntValue 10) "inner try catches A"
        }

        test "nested try-with outer catches" {
            let result = evalModule "exception A\nexception B\nlet result =\n    try\n        try\n            raise B\n        with\n        | A -> 10\n    with\n    | B -> 20\n"
            Expect.equal result (Ast.IntValue 20) "outer try catches B"
        }
    ]

    // =====================================================
    // EXC-05: when guards in exception handlers
    // =====================================================

    testList "EXC-05: when guards" [
        test "when guard selects server error handler" {
            let result = evalModule "exception HttpError of int\nlet result =\n    try\n        raise (HttpError 503)\n    with\n    | HttpError code when code >= 500 -> 1\n    | HttpError code when code >= 400 -> 2\n    | _ -> 0\n"
            Expect.equal result (Ast.IntValue 1) "when guard selects server error"
        }

        test "when guard falls through to client error" {
            let result = evalModule "exception HttpError of int\nlet result =\n    try\n        raise (HttpError 404)\n    with\n    | HttpError code when code >= 500 -> 1\n    | HttpError code when code >= 400 -> 2\n    | _ -> 0\n"
            Expect.equal result (Ast.IntValue 2) "when guard falls through to client error"
        }

        test "when guard falls through to wildcard" {
            let result = evalModule "exception HttpError of int\nlet result =\n    try\n        raise (HttpError 301)\n    with\n    | HttpError code when code >= 500 -> 1\n    | HttpError code when code >= 400 -> 2\n    | _ -> 0\n"
            Expect.equal result (Ast.IntValue 0) "when guard falls through to wildcard"
        }
    ]

    // =====================================================
    // When guards in standard match expressions
    // =====================================================

    testList "When guards in match" [
        test "when guard in match positive" {
            let result = evalModule "let classify x =\n    match x with\n    | n when n < 0 -> 0\n    | n when n = 0 -> 1\n    | _ -> 2\nlet result = classify 5\n"
            Expect.equal result (Ast.IntValue 2) "positive classified"
        }

        test "when guard in match negative" {
            let result = evalModule "let classify x =\n    match x with\n    | n when n < 0 -> 0\n    | n when n = 0 -> 1\n    | _ -> 2\nlet result = classify (0 - 3)\n"
            Expect.equal result (Ast.IntValue 0) "negative classified"
        }

        test "when guard in match zero" {
            let result = evalModule "let classify x =\n    match x with\n    | n when n < 0 -> 0\n    | n when n = 0 -> 1\n    | _ -> 2\nlet result = classify 0\n"
            Expect.equal result (Ast.IntValue 1) "zero classified"
        }

        test "when guard with list pattern positive head" {
            let result = evalModule "let check lst =\n    match lst with\n    | x :: _ when x > 0 -> 1\n    | _ -> 0\nlet result = check [5, 3]\n"
            Expect.equal result (Ast.IntValue 1) "positive head matched"
        }

        test "when guard with list pattern negative head" {
            let result = evalModule "let check lst =\n    match lst with\n    | x :: _ when x > 0 -> 1\n    | _ -> 0\nlet result = check [0 - 1, 3]\n"
            Expect.equal result (Ast.IntValue 0) "negative head falls through"
        }
    ]

    // =====================================================
    // Edge cases
    // =====================================================

    testList "Edge cases" [
        test "exception in module accessed via open" {
            let result = evalModule "module Errors =\n    exception NotFound of string\n\nopen Errors\nlet result =\n    try\n        raise (NotFound \"page\")\n    with\n    | NotFound msg -> msg\n"
            Expect.equal result (Ast.StringValue "page") "exception from opened module"
        }

        test "exception with tuple data" {
            let result = evalModule "exception NetworkError of string * int\nlet result =\n    try\n        raise (NetworkError (\"timeout\", 504))\n    with\n    | NetworkError (msg, code) -> code\n"
            Expect.equal result (Ast.IntValue 504) "exception with tuple data extraction"
        }

        test "re-raise when no handler matches" {
            let result = evalModule "exception A\nexception B\nlet result =\n    try\n        try\n            raise A\n        with\n        | B -> 0\n    with\n    | A -> 42\n"
            Expect.equal result (Ast.IntValue 42) "unmatched exception propagates to outer handler"
        }
    ]
]
