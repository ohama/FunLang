module FunLang.Tests.ExportApiTests

open System
open System.IO
open Expecto

/// Create a temporary .fun file, call f with its path, delete in finally.
let withTempFile (content: string) (f: string -> 'a) : 'a =
    let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".fun")
    try
        File.WriteAllText(path, content)
        f path
    finally
        if File.Exists(path) then File.Delete(path)

[<Tests>]
let exportApiTests = testSequenced <| testList "ExportApi" [

    // API-01: typeCheckFile returns a TypedModule without error for a simple let binding
    test "API-01: typeCheckFile returns TypedModule for let x = 42" {
        withTempFile "let x = 42" (fun path ->
            let result = ExportApi.typeCheckFile path
            // Should not throw; just verify the record was returned
            Expect.isNotNull (box result) "typeCheckFile should return a TypedModule"
        )
    }

    // API-02: AnnotationMap is non-empty for files with expressions
    test "API-02: AnnotationMap non-empty for let x = 1 + 2" {
        withTempFile "let x = 1 + 2" (fun path ->
            let result = ExportApi.typeCheckFile path
            Expect.isGreaterThan result.AnnotationMap.Count 0
                "AnnotationMap should be non-empty for a file with expressions"
        )
    }

    // API-02: BindingEnv contains the user top-level binding "answer"
    test "API-02: BindingEnv contains user binding 'answer' for let answer = 42" {
        withTempFile "let answer = 42" (fun path ->
            let result = ExportApi.typeCheckFile path
            Expect.isSome (Map.tryFind "answer" result.BindingEnv)
                "BindingEnv should contain user binding 'answer'"
        )
    }

    // API-02: BuiltinSchemes contains "print"
    test "API-02: BuiltinSchemes contains 'print'" {
        withTempFile "let x = 1" (fun path ->
            let result = ExportApi.typeCheckFile path
            Expect.isSome (Map.tryFind "print" result.BuiltinSchemes)
                "BuiltinSchemes should contain 'print'"
        )
    }

    // API-02: BindingEnv includes builtins ("print")
    test "API-02: BindingEnv includes builtin 'print'" {
        withTempFile "let x = 1" (fun path ->
            let result = ExportApi.typeCheckFile path
            Expect.isSome (Map.tryFind "print" result.BindingEnv)
                "BindingEnv should include builtin 'print'"
        )
    }

]
