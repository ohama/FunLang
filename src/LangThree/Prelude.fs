module Prelude

open System.IO
open FSharp.Text.Lexing
open Ast
open Eval
open TypeCheck
open Type
open Diagnostic
open LangThree.IndentFilter

/// Result of loading prelude files — provides environments for both type checking and evaluation
type PreludeResult = {
    Env: Env
    TypeEnv: TypeEnv
    CtorEnv: ConstructorEnv
    RecEnv: RecordEnv
}

let emptyPrelude = { Env = Map.empty; TypeEnv = Map.empty; CtorEnv = Map.empty; RecEnv = Map.empty }

/// Parse a string as module with IndentFilter
let parseModuleFromString (input: string) (filename: string) : Module =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf filename
    let rec collect () =
        let tok = Lexer.tokenize lexbuf
        if tok = Parser.EOF then [Parser.EOF]
        else tok :: collect ()
    let rawTokens = collect ()
    let filteredTokens = filter defaultConfig rawTokens |> Seq.toList
    let lexbuf2 = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf2 filename
    let mutable index = 0
    let tokenizer (_: LexBuffer<_>) =
        if index < filteredTokens.Length then
            let tok = filteredTokens.[index]
            index <- index + 1
            tok
        else
            Parser.EOF
    Parser.parseModule tokenizer lexbuf2

/// Extract declarations from a module
let private getDecls (m: Module) : Decl list =
    match m with
    | Module (decls, _) | NamedModule(_, decls, _) | NamespacedModule(_, decls, _) -> decls
    | EmptyModule _ -> []

/// Find the Prelude directory using a 3-stage search strategy
let private findPreludeDir () : string =
    // Stage 1: CWD-relative (current dev workflow: dotnet run from repo root)
    if Directory.Exists "Prelude" then "Prelude"
    else
        let assemblyLoc = System.Reflection.Assembly.GetEntryAssembly().Location
        if not (System.String.IsNullOrEmpty assemblyLoc) then
            let assemblyDir = Path.GetDirectoryName assemblyLoc
            // Stage 2: Assembly-relative (dotnet publish / installed binary)
            let candidate = Path.Combine(assemblyDir, "Prelude")
            if Directory.Exists candidate then candidate
            else
                // Stage 3: Walk up from assembly dir (handles dotnet run from other dirs)
                // Binary is at src/LangThree/bin/Debug/net10.0/LangThree
                // Walking up 6 levels reaches repo root where Prelude/ lives
                let mutable dir = assemblyDir
                let mutable result = ""
                for _ in 1..6 do
                    if result = "" then
                        let c = Path.Combine(dir, "Prelude")
                        if Directory.Exists c then result <- c
                        let parent = Path.GetDirectoryName dir
                        if parent <> dir then dir <- parent  // guard: stop at filesystem root
                result
        else ""

/// Tracks file paths currently being loaded for cycle detection (single-threaded).
let private fileLoadingStack = System.Collections.Generic.HashSet<string>()

/// Load, parse, type-check, and evaluate a .fun file, merging its environments.
/// Used as the implementation of TypeCheck.fileImportTypeChecker.
let rec loadAndTypeCheckFileImpl
    (resolvedPath: string)
    (cEnv: ConstructorEnv)
    (rEnv: RecordEnv)
    (typeEnv: TypeEnv) : TypeEnv * ConstructorEnv * RecordEnv =
    if fileLoadingStack.Contains resolvedPath then
        raise (TypeException {
            Kind = CircularModuleDependency [resolvedPath]
            Span = unknownSpan; Term = None; ContextStack = []; Trace = [] })
    if not (File.Exists resolvedPath) then
        raise (TypeException {
            Kind = UnresolvedModule resolvedPath
            Span = unknownSpan; Term = None; ContextStack = []; Trace = [] })
    fileLoadingStack.Add resolvedPath |> ignore
    let prevFile = TypeCheck.currentTypeCheckingFile
    try
        TypeCheck.currentTypeCheckingFile <- resolvedPath
        let source = File.ReadAllText resolvedPath
        let m = parseModuleFromString source resolvedPath
        match typeCheckModuleWithPrelude cEnv rEnv typeEnv m with
        | Ok (_warnings, fileCEnv, fileREnv, _mods, fileTypeEnv) ->
            let mergedCEnv = Map.fold (fun acc k v -> Map.add k v acc) cEnv fileCEnv
            let mergedREnv = Map.fold (fun acc k v -> Map.add k v acc) rEnv fileREnv
            let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) typeEnv fileTypeEnv
            (mergedTypeEnv, mergedCEnv, mergedREnv)
        | Error diag ->
            failwithf "Type error in imported file %s:\n%s" resolvedPath (formatDiagnostic diag)
    finally
        TypeCheck.currentTypeCheckingFile <- prevFile
        fileLoadingStack.Remove resolvedPath |> ignore

/// Load, parse, and evaluate a .fun file, merging its value environment.
/// Used as the implementation of Eval.fileImportEvaluator.
and loadAndEvalFileImpl
    (resolvedPath: string)
    (recEnv: RecordEnv)
    (modEnv: Map<string, ModuleValueEnv>)
    (env: Env) : Env * Map<string, ModuleValueEnv> =
    // Note: fileLoadingStack guards are for TC phase; eval phase skips cycle check
    // (TC phase would have already caught cycles)
    if not (File.Exists resolvedPath) then
        failwithf "File not found during evaluation: %s" resolvedPath
    let prevFile = Eval.currentEvalFile
    try
        Eval.currentEvalFile <- resolvedPath
        let source = File.ReadAllText resolvedPath
        let m = parseModuleFromString source resolvedPath
        let decls = getDecls m
        let (fileEnv, fileModEnv) = Eval.evalModuleDecls recEnv modEnv env decls
        // Merge all file bindings into the caller's env (shadowing is acceptable)
        let mergedEnv = Map.fold (fun acc k v -> Map.add k v acc) env fileEnv
        let mergedModEnv = Map.fold (fun acc k v -> Map.add k v acc) modEnv fileModEnv
        (mergedEnv, mergedModEnv)
    finally
        Eval.currentEvalFile <- prevFile

/// Initialize the file import delegates. Must be called before any file imports are processed.
/// Called automatically at module initialization time.
do
    TypeCheck.fileImportTypeChecker <- loadAndTypeCheckFileImpl
    Eval.fileImportEvaluator <- loadAndEvalFileImpl

/// Load all Prelude/*.fun files and return accumulated environments
let loadPrelude () : PreludeResult =
    let preludeDir = findPreludeDir ()
    if preludeDir <> "" then
        let files = Directory.GetFiles(preludeDir, "*.fun") |> Array.sort
        if files.Length = 0 then
            emptyPrelude
        else
            let mutable result = emptyPrelude
            for file in files do
                try
                    let source = File.ReadAllText file
                    let m = parseModuleFromString source file

                    // Type check with accumulated prelude environments
                    match typeCheckModuleWithPrelude result.CtorEnv result.RecEnv result.TypeEnv m with
                    | Ok (_warnings, ctorEnv, recEnv, _modules, typeEnv) ->
                        // Accumulate type environments (exclude built-in types)
                        let newTypeBindings = typeEnv |> Map.filter (fun k _ -> not (Map.containsKey k initialTypeEnv))
                        let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) result.TypeEnv newTypeBindings

                        // Accumulate constructor and record environments
                        let mergedCtorEnv = Map.fold (fun acc k v -> Map.add k v acc) result.CtorEnv ctorEnv
                        let mergedRecEnv = Map.fold (fun acc k v -> Map.add k v acc) result.RecEnv recEnv

                        // Evaluate module declarations for value environment
                        let decls = getDecls m
                        let evalEnv = Map.fold (fun acc k v -> Map.add k v acc) result.Env Eval.initialBuiltinEnv
                        let (finalEnv, _moduleEnv) = Eval.evalModuleDecls mergedRecEnv Map.empty evalEnv decls
                        // Extract only new bindings (not built-ins or previous prelude)
                        let newValues = finalEnv |> Map.filter (fun k _ ->
                            not (Map.containsKey k Eval.initialBuiltinEnv) && not (Map.containsKey k result.Env))
                        let mergedEnv = Map.fold (fun acc k v -> Map.add k v acc) result.Env newValues

                        result <- { Env = mergedEnv; TypeEnv = mergedTypeEnv; CtorEnv = mergedCtorEnv; RecEnv = mergedRecEnv }

                    | Error diag ->
                        eprintfn "Warning: Type error in %s: %s" file (formatDiagnostic diag)
                with ex ->
                    eprintfn "Warning: Failed to load %s: %s" file ex.Message
            result
    else
        emptyPrelude
