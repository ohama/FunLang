module FunLang.Tests.DemoTests

open System
open System.IO
open Expecto
open FunLang.Ast
open FunLang.Parser
open FunLang.Interpreter
open FunLang.ConstructorResolver
open FunLang.Types
open FunLang.TypeInfer
open FunLang.Tests.TestHelpers

// =============================================================================
// File-Based Demo Tests
// =============================================================================
//
// Reads .fun files from demos/ directory and compares execution results
// with expected values specified in the file header.
//
// File format:
//   -- Expected: <value>
//   -- Optional description
//   <FunLang code>
//
// Supported expected formats:
//   -- Expected: 42          (integer)
//   -- Expected: true        (boolean)
//   -- Expected: "hello"     (string)
//   -- Expected: ()          (unit)
//   -- Expected: (1, 2, 3)   (tuple)
//   -- Expected: [1; 2; 3]   (list)
//   -- Expected: Error       (expects an error)
// =============================================================================

/// Get the demos directory path (relative to test execution)
let demosDir =
    let baseDir = AppDomain.CurrentDomain.BaseDirectory
    // Navigate from bin/Debug/net10.0 to project root, then to demos/
    Path.Combine(baseDir, "..", "..", "..", "..", "..", "demos")
    |> Path.GetFullPath

/// Parse the expected value from a string
let parseExpected (s: string) : Result<Value, string> =
    let s = s.Trim()
    match s with
    | "Error" -> Error "Expected error"
    | "()" -> Ok VUnit
    | "true" -> Ok (VBool true)
    | "false" -> Ok (VBool false)
    | s when s.StartsWith("\"") && s.EndsWith("\"") ->
        Ok (VString (s.Substring(1, s.Length - 2)))
    | s when s.StartsWith("(") && s.EndsWith(")") ->
        // Parse tuple: (1, 2, 3)
        let inner = s.Substring(1, s.Length - 2)
        let parts = inner.Split(',') |> Array.map (fun p -> p.Trim())
        let values =
            parts
            |> Array.map (fun p ->
                match Int32.TryParse(p) with
                | true, n -> Ok (VInt n)
                | false, _ ->
                    match p with
                    | "true" -> Ok (VBool true)
                    | "false" -> Ok (VBool false)
                    | _ -> Error (sprintf "Cannot parse tuple element: %s" p))
            |> Array.toList
        match values |> List.choose (function Ok v -> Some v | Error _ -> None) with
        | vs when vs.Length = parts.Length -> Ok (VTuple vs)
        | _ -> Error (sprintf "Cannot parse tuple: %s" s)
    | s when s.StartsWith("[") && s.EndsWith("]") ->
        // Parse list: [1; 2; 3]
        let inner = s.Substring(1, s.Length - 2).Trim()
        if inner = "" then Ok (VList [])
        else
            let parts = inner.Split(';') |> Array.map (fun p -> p.Trim())
            let values =
                parts
                |> Array.map (fun p ->
                    match Int32.TryParse(p) with
                    | true, n -> Ok (VInt n)
                    | false, _ ->
                        match p with
                        | "true" -> Ok (VBool true)
                        | "false" -> Ok (VBool false)
                        | _ -> Error (sprintf "Cannot parse list element: %s" p))
                |> Array.toList
            match values |> List.choose (function Ok v -> Some v | Error _ -> None) with
            | vs when vs.Length = parts.Length -> Ok (VList vs)
            | _ -> Error (sprintf "Cannot parse list: %s" s)
    | s ->
        // Try integer
        match Int32.TryParse(s) with
        | true, n -> Ok (VInt n)
        | false, _ -> Error (sprintf "Cannot parse expected value: %s" s)

/// Extract expected value from file content
let extractExpected (content: string) : Result<Value, string> option =
    let lines = content.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
    lines
    |> Array.tryFind (fun line -> line.Trim().StartsWith("-- Expected:"))
    |> Option.map (fun line ->
        let expectedStr = line.Substring(line.IndexOf("-- Expected:") + 12).Trim()
        parseExpected expectedStr)

/// Remove comment lines from content for parsing
let removeComments (content: string) : string =
    content.Split([|'\n'|])
    |> Array.filter (fun line -> not (line.TrimStart().StartsWith("--")))
    |> String.concat "\n"

/// Format a Value for display
let rec formatValue (v: Value) : string =
    match v with
    | VInt n -> string n
    | VBool b -> if b then "true" else "false"
    | VString s -> sprintf "\"%s\"" s
    | VUnit -> "()"
    | VTuple vs -> vs |> List.map formatValue |> String.concat ", " |> sprintf "(%s)"
    | VList vs -> vs |> List.map formatValue |> String.concat "; " |> sprintf "[%s]"
    | VClosure _ -> "<closure>"
    | VRecClosure _ -> "<rec-closure>"
    | VConstructed (name, None) -> name
    | VConstructed (name, Some v) -> sprintf "%s %s" name (formatValue v)

/// Run a demo file and return the result
let runDemoFile (filePath: string) : Result<Value, string> =
    let content = File.ReadAllText(filePath)
    let code = removeComments content

    // Check if it's a program with type definitions
    if code.Contains("type ") then
        runProgram code
    else
        runString code

/// Create a test for a single demo file
let createDemoTest (filePath: string) : Test =
    let fileName = Path.GetFileName(filePath)
    test fileName {
        let content = File.ReadAllText(filePath)

        match extractExpected content with
        | None ->
            failtest (sprintf "No '-- Expected:' header found in %s" fileName)
        | Some (Error "Expected error") ->
            // Expect an error
            let result = runDemoFile filePath
            Expect.isError result (sprintf "%s should produce an error" fileName)
        | Some (Error parseErr) ->
            failtest (sprintf "Cannot parse expected value in %s: %s" fileName parseErr)
        | Some (Ok expectedValue) ->
            let result = runDemoFile filePath
            match result with
            | Error e ->
                failtest (sprintf "%s failed: %s" fileName e)
            | Ok actualValue ->
                Expect.equal actualValue expectedValue
                    (sprintf "%s: expected %s but got %s"
                        fileName (formatValue expectedValue) (formatValue actualValue))
    }

/// Discover and create tests for all demo files
let discoverDemoTests () : Test list =
    if Directory.Exists(demosDir) then
        Directory.GetFiles(demosDir, "*.fun")
        |> Array.sort
        |> Array.map createDemoTest
        |> Array.toList
    else
        [ test "demos directory not found" {
            failtest (sprintf "demos/ directory not found at %s" demosDir)
        } ]

// =============================================================================
// Test List
// =============================================================================

[<Tests>]
let demoTests = testList "Demo Files" (discoverDemoTests ())
