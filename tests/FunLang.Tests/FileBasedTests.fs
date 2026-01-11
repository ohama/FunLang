module FunLang.Tests.FileBasedTests

open System
open System.IO
open System.Diagnostics
open System.Security.Cryptography
open System.Text
open Expecto

// =============================================================================
// File-Based Testing Framework
// =============================================================================
//
// Reads test files with the following format:
//
//   // --COMMAND: <command with %s placeholder>
//   // --INPUT
//   <input content>
//   // --EXPECTED
//   <expected result>
//
// Supports piped commands:
//   // --COMMAND: commandA %s | commandB %s
//
// See docs/FILE_BASED_TESTING.md for full specification.
// =============================================================================

/// Test file sections parsed from content
type TestFileContent = {
    Command: string
    Input: string
    Expected: string
}

/// Parse test file content into sections
let parseTestFile (content: string) : Result<TestFileContent, string> =
    let lines = content.Split([|'\n'|])

    // Find COMMAND line
    let commandLine =
        lines
        |> Array.tryFind (fun line -> line.TrimStart().StartsWith("// --COMMAND:"))

    match commandLine with
    | None -> Error "Missing // --COMMAND: section"
    | Some cmdLine ->
        let command = cmdLine.Substring(cmdLine.IndexOf("// --COMMAND:") + 13).Trim()

        // Find section indices
        let inputIndex =
            lines
            |> Array.tryFindIndex (fun line -> line.TrimStart().StartsWith("// --INPUT"))

        let expectedIndex =
            lines
            |> Array.tryFindIndex (fun line -> line.TrimStart().StartsWith("// --EXPECTED"))

        match inputIndex, expectedIndex with
        | None, _ -> Error "Missing // --INPUT section"
        | _, None -> Error "Missing // --EXPECTED section"
        | Some iIdx, Some eIdx when eIdx <= iIdx -> Error "// --EXPECTED must come after // --INPUT"
        | Some iIdx, Some eIdx ->
            // Extract input: lines between INPUT and EXPECTED
            let inputLines =
                lines
                |> Array.skip (iIdx + 1)
                |> Array.take (eIdx - iIdx - 1)
                |> String.concat "\n"

            // Extract expected: lines after EXPECTED
            let expectedLines =
                lines
                |> Array.skip (eIdx + 1)
                |> String.concat "\n"

            Ok {
                Command = command
                Input = inputLines
                Expected = expectedLines
            }

/// Trim empty lines from start and end of string
let trimEmptyLines (s: string) : string =
    let lines = s.Split([|'\n'|])

    // Find first non-empty line
    let startIdx =
        lines
        |> Array.tryFindIndex (fun line -> not (String.IsNullOrWhiteSpace(line)))
        |> Option.defaultValue lines.Length

    // Find last non-empty line
    let endIdx =
        lines
        |> Array.tryFindIndexBack (fun line -> not (String.IsNullOrWhiteSpace(line)))
        |> Option.map ((+) 1)
        |> Option.defaultValue 0

    if startIdx >= endIdx then
        ""
    else
        lines
        |> Array.skip startIdx
        |> Array.take (endIdx - startIdx)
        |> String.concat "\n"

/// Compute hash for temp file naming
let computeHash (s: string) : string =
    use sha256 = SHA256.Create()
    let bytes = Encoding.UTF8.GetBytes(s)
    let hash = sha256.ComputeHash(bytes)
    BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLower()

/// Execute command with shell
/// Returns Ok () if process ran (regardless of exit code), Error only if process couldn't start
let executeCommand (command: string) (inputFile: string) (outputFile: string) : Result<unit, string> =
    // Replace %s with input file path
    let cmd = command.Replace("%s", inputFile)

    // Construct shell command with output redirection (stderr merged to stdout)
    let shellCmd = sprintf "%s > \"%s\" 2>&1" cmd outputFile

    let isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT
    let shell, shellArg =
        if isWindows then "cmd", "/c"
        else "/bin/sh", "-c"

    let startInfo = ProcessStartInfo(
        FileName = shell,
        Arguments = sprintf "%s \"%s\"" shellArg (shellCmd.Replace("\"", "\\\"")),
        UseShellExecute = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        CreateNoWindow = true,
        WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..")
    )

    try
        use proc = Process.Start(startInfo)
        proc.WaitForExit(60000) |> ignore  // 60 second timeout
        // Accept any exit code - output comparison will determine pass/fail
        Ok ()
    with ex ->
        Error (sprintf "Failed to execute command: %s" ex.Message)

/// Run a single file-based test
let runFileBasedTest (filePath: string) : Result<unit, string> =
    let content = File.ReadAllText(filePath)

    match parseTestFile content with
    | Error e -> Error e
    | Ok testContent ->
        let hash = computeHash filePath
        let tempDir = Path.GetTempPath()
        let inputFile = Path.Combine(tempDir, sprintf "funlang_test_%s_input.txt" hash)
        let expectedFile = Path.Combine(tempDir, sprintf "funlang_test_%s_expected.txt" hash)
        let actualFile = Path.Combine(tempDir, sprintf "funlang_test_%s_actual.txt" hash)

        try
            // Write input and expected files
            File.WriteAllText(inputFile, testContent.Input)
            File.WriteAllText(expectedFile, testContent.Expected)

            // Execute command
            match executeCommand testContent.Command inputFile actualFile with
            | Error e -> Error e
            | Ok () ->
                // Read and compare results
                let actual =
                    if File.Exists(actualFile) then File.ReadAllText(actualFile)
                    else ""

                let expectedTrimmed = trimEmptyLines testContent.Expected
                let actualTrimmed = trimEmptyLines actual

                if expectedTrimmed = actualTrimmed then
                    Ok ()
                else
                    Error (sprintf "Output mismatch.\nExpected:\n%s\n\nActual:\n%s" expectedTrimmed actualTrimmed)
        finally
            // Cleanup temp files
            [ inputFile; expectedFile; actualFile ]
            |> List.iter (fun f -> try File.Delete(f) with _ -> ())

/// Create an Expecto test for a test file
let createFileTest (baseDir: string) (filePath: string) : Test =
    // Get relative path from base directory for unique test name
    let relativePath = Path.GetRelativePath(baseDir, filePath)
    let testName = relativePath.Replace(Path.DirectorySeparatorChar, '/')
    test testName {
        match runFileBasedTest filePath with
        | Ok () -> ()
        | Error e -> failtest (sprintf "%s: %s" testName e)
    }

/// Get the file-tests directory path
let fileTestsDir =
    let baseDir = AppDomain.CurrentDomain.BaseDirectory
    Path.Combine(baseDir, "..", "..", "..", "..", "..", "tests", "file-tests")
    |> Path.GetFullPath

/// Discover and create tests for all .test files
let discoverFileTests () : Test list =
    if Directory.Exists(fileTestsDir) then
        Directory.GetFiles(fileTestsDir, "*.test", SearchOption.AllDirectories)
        |> Array.sort
        |> Array.map (createFileTest fileTestsDir)
        |> Array.toList
    else
        []

// =============================================================================
// Test List
// =============================================================================

[<Tests>]
let fileBasedTests = testList "File-Based Tests" (discoverFileTests ())
