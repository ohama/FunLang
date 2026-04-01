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
    ClassEnv: ClassEnv
    InstEnv: InstanceEnv
    Modules: Map<string, ModuleExports>
    ModuleValueEnv: Map<string, ModuleValueEnv>
}

let emptyPrelude = { Env = Map.empty; TypeEnv = Map.empty; CtorEnv = Map.empty; RecEnv = Map.empty; ClassEnv = Map.empty; InstEnv = Map.empty; Modules = Map.empty; ModuleValueEnv = Map.empty }

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

/// Cache for type-check results by absolute file path (single process lifetime).
/// Stores file's OWN exports only (not merged with caller), so diamond deps are safe.
let private tcCache = System.Collections.Generic.Dictionary<string, ConstructorEnv * RecordEnv * Map<string, ModuleExports> * TypeEnv>()

/// Cache for eval results by absolute file path (single process lifetime).
/// Stores file's OWN exports only (not merged with caller), so diamond deps are safe.
let private evalCache = System.Collections.Generic.Dictionary<string, Env * Map<string, ModuleValueEnv>>()

/// Load, parse, type-check, and evaluate a .fun file, merging its environments.
/// Used as the implementation of TypeCheck.fileImportTypeChecker.
let rec loadAndTypeCheckFileImpl
    (resolvedPath: string)
    (cEnv: ConstructorEnv)
    (rEnv: RecordEnv)
    (typeEnv: TypeEnv)
    (mods: Map<string, ModuleExports>) : TypeEnv * ConstructorEnv * RecordEnv * Map<string, ModuleExports> =
    // 1. Cycle detection must happen before cache check
    if fileLoadingStack.Contains resolvedPath then
        raise (TypeException {
            Kind = CircularModuleDependency [resolvedPath]
            Span = unknownSpan; Term = None; ContextStack = []; Trace = []; Scope = [] })
    // 2. Cache check (after cycle detection)
    match tcCache.TryGetValue(resolvedPath) with
    | true, (fileCEnv, fileREnv, fileMods, fileTypeEnv) ->
        // Return caller's env merged with cached file's own exports
        let mergedCEnv = Map.fold (fun acc k v -> Map.add k v acc) cEnv fileCEnv
        let mergedREnv = Map.fold (fun acc k v -> Map.add k v acc) rEnv fileREnv
        let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) typeEnv fileTypeEnv
        let mergedMods = Map.fold (fun acc k v -> Map.add k v acc) mods fileMods
        (mergedTypeEnv, mergedCEnv, mergedREnv, mergedMods)
    | false, _ ->
    // 3. File existence check
    if not (File.Exists resolvedPath) then
        raise (TypeException {
            Kind = UnresolvedModule resolvedPath
            Span = unknownSpan; Term = None; ContextStack = []; Trace = []; Scope = [] })
    fileLoadingStack.Add resolvedPath |> ignore
    let prevFile = TypeCheck.currentTypeCheckingFile
    try
        TypeCheck.currentTypeCheckingFile <- resolvedPath
        let source = File.ReadAllText resolvedPath
        let m = parseModuleFromString source resolvedPath
        match typeCheckModuleWithPrelude cEnv rEnv Map.empty Map.empty typeEnv mods m with
        | Ok (_warnings, fileCEnv, fileREnv, _fileClassEnv, _fileInstEnv, fileMods, fileTypeEnv) ->
            // Cache the file's own exports BEFORE merging with caller env
            tcCache.[resolvedPath] <- (fileCEnv, fileREnv, fileMods, fileTypeEnv)
            let mergedCEnv = Map.fold (fun acc k v -> Map.add k v acc) cEnv fileCEnv
            let mergedREnv = Map.fold (fun acc k v -> Map.add k v acc) rEnv fileREnv
            let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) typeEnv fileTypeEnv
            let mergedMods = Map.fold (fun acc k v -> Map.add k v acc) mods fileMods
            (mergedTypeEnv, mergedCEnv, mergedREnv, mergedMods)
        | Error diags ->
            failwithf "Type error in imported file %s:\n%s" resolvedPath (diags |> List.map formatDiagnostic |> String.concat "\n")
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
    // Eval skips cycle detection (TC phase catches cycles before eval runs).
    // Cache check at top is safe for the same reason.
    match evalCache.TryGetValue(resolvedPath) with
    | true, (fileEnv, fileModEnv) ->
        // Return caller's env merged with cached file's own exports
        let mergedEnv = Map.fold (fun acc k v -> Map.add k v acc) env fileEnv
        let mergedModEnv = Map.fold (fun acc k v -> Map.add k v acc) modEnv fileModEnv
        (mergedEnv, mergedModEnv)
    | false, _ ->
    if not (File.Exists resolvedPath) then
        failwithf "File not found during evaluation: %s" resolvedPath
    let prevFile = Eval.currentEvalFile
    try
        Eval.currentEvalFile <- resolvedPath
        let source = File.ReadAllText resolvedPath
        let m = parseModuleFromString source resolvedPath
        let decls = getDecls m
        let elaboratedDecls = Elaborate.elaborateTypeclasses decls
        let (fileEnv, fileModEnv) = Eval.evalModuleDecls recEnv modEnv env elaboratedDecls
        // Cache the file's own exports BEFORE merging with caller env
        evalCache.[resolvedPath] <- (fileEnv, fileModEnv)
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

// === Dependency-based load ordering (OCaml ocamldep style) ===

/// Extract constructor names declared in type declarations within module body
let rec private collectCtors (decls: Decl list) : Set<string> =
    (Set.empty, decls) ||> List.fold (fun acc d ->
        match d with
        | Decl.TypeDecl(Ast.TypeDecl(_, _, ctors, _, _)) ->
            (acc, ctors) ||> List.fold (fun a ct ->
                match ct with
                | ConstructorDecl(name, _, _) | GadtConstructorDecl(name, _, _, _) -> Set.add name a)
        | Decl.ModuleDecl(_, inner, _) -> Set.union acc (collectCtors inner)
        | _ -> acc)

/// Topological sort: repeatedly extract nodes whose dependencies are all resolved.
/// Uses alphabetical order as tie-breaker for deterministic output.
let private topoSort (nodes: string list) (deps: Map<string, Set<string>>) : string list =
    let mutable remaining = Set.ofList nodes
    let mutable result = []
    let mutable progress = true
    while progress && not remaining.IsEmpty do
        let ready = remaining |> Set.filter (fun f ->
            match Map.tryFind f deps with
            | None -> true
            | Some s -> (Set.intersect s remaining).IsEmpty)
        progress <- not ready.IsEmpty
        result <- result @ (ready |> Set.toList |> List.sort)
        remaining <- remaining - ready
    result @ (remaining |> Set.toList |> List.sort)

/// Determine load order for Prelude files by scanning constructor dependencies.
/// Each file is parsed to extract its declared constructors, then source text is
/// scanned for references to constructors declared in other files.
/// The resulting dependency DAG is topologically sorted.
let private resolveLoadOrder (files: string array) : string array =
    if files.Length <= 1 then files
    else
    let re = System.Text.RegularExpressions.Regex(@"\b([A-Z][a-zA-Z0-9_]*)\b")
    // 1. Parse all files, extract declared constructors
    let fileInfos =
        files |> Array.map (fun f ->
            try
                let src = File.ReadAllText f
                let m = parseModuleFromString src f
                (f, src, collectCtors (getDecls m))
            with _ -> (f, "", Set.empty))
    // 2. Map each constructor name to its declaring file
    let ctorToFile =
        fileInfos |> Array.collect (fun (f, _, ctors) ->
            ctors |> Set.toArray |> Array.map (fun c -> (c, f)))
        |> Map.ofArray
    // 3. For each file, find dependencies by scanning for constructor references
    let fileDeps =
        fileInfos |> Array.map (fun (f, src, ownCtors) ->
            let deps =
                if src = "" then Set.empty
                else
                    re.Matches(src)
                    |> Seq.cast<System.Text.RegularExpressions.Match>
                    |> Seq.map (fun m -> m.Groups.[1].Value)
                    |> Seq.choose (fun ident ->
                        if Set.contains ident ownCtors then None
                        else Map.tryFind ident ctorToFile)
                    |> Seq.filter (fun dep -> dep <> f)
                    |> Set.ofSeq
            (f, deps)) |> Map.ofArray
    // 4. Topological sort
    topoSort (Array.toList files) fileDeps |> List.toArray

/// Resolve Prelude directory using priority chain: explicit > LANGTHREE_PRELUDE env > funproj.toml > auto-discovery
let resolvePreludeDir (explicitPath: string option) (projPrelude: string option) : string =
    match explicitPath with
    | Some path ->
        if Directory.Exists path then path
        else failwithf "Prelude directory not found: %s" path
    | None ->
        match System.Environment.GetEnvironmentVariable("LANGTHREE_PRELUDE") with
        | null | "" ->
            match projPrelude with
            | Some p when p <> "" && Directory.Exists p -> p
            | _ -> findPreludeDir()
        | envPath ->
            if Directory.Exists envPath then envPath
            else failwithf "LANGTHREE_PRELUDE directory not found: %s" envPath

/// Load all Prelude/*.fun files and return accumulated environments
let loadPrelude (explicitPath: string option) (projPrelude: string option) : PreludeResult =
    let preludeDir = resolvePreludeDir explicitPath projPrelude
    if preludeDir <> "" then
        let files = Directory.GetFiles(preludeDir, "*.fun") |> resolveLoadOrder
        if files.Length = 0 then
            emptyPrelude
        else
            let mutable result = emptyPrelude
            for file in files do
                try
                    let source = File.ReadAllText file
                    let m = parseModuleFromString source file

                    // Type check with accumulated prelude environments (including accumulated modules)
                    match typeCheckModuleWithPrelude result.CtorEnv result.RecEnv result.ClassEnv result.InstEnv result.TypeEnv result.Modules m with
                    | Ok (_warnings, ctorEnv, recEnv, classEnv, instEnv, modules, typeEnv) ->
                        // Accumulate type environments (exclude built-in types)
                        let newTypeBindings = typeEnv |> Map.filter (fun k _ -> not (Map.containsKey k initialTypeEnv))
                        let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) result.TypeEnv newTypeBindings

                        // Accumulate constructor and record environments
                        let mergedCtorEnv = Map.fold (fun acc k v -> Map.add k v acc) result.CtorEnv ctorEnv
                        let mergedRecEnv = Map.fold (fun acc k v -> Map.add k v acc) result.RecEnv recEnv

                        // Accumulate class and instance environments
                        let mergedClassEnv = Map.fold (fun acc k v -> Map.add k v acc) result.ClassEnv classEnv
                        let mergedInstEnv = Map.fold (fun acc k v -> Map.add k v acc) result.InstEnv instEnv

                        // Accumulate module map
                        let mergedModules = Map.fold (fun acc k v -> Map.add k v acc) result.Modules modules

                        // Evaluate module declarations for value environment
                        let decls = getDecls m
                        let elaboratedDecls = Elaborate.elaborateTypeclasses decls
                        let evalEnv = Map.fold (fun acc k v -> Map.add k v acc) result.Env Eval.initialBuiltinEnv
                        let (finalEnv, fileModuleEnv) = Eval.evalModuleDecls mergedRecEnv result.ModuleValueEnv evalEnv elaboratedDecls
                        // Extract only new bindings (not built-ins or previous prelude)
                        let newValues = finalEnv |> Map.filter (fun k _ ->
                            not (Map.containsKey k Eval.initialBuiltinEnv) && not (Map.containsKey k result.Env))
                        let mergedEnv = Map.fold (fun acc k v -> Map.add k v acc) result.Env newValues
                        let mergedModuleValueEnv = Map.fold (fun acc k v -> Map.add k v acc) result.ModuleValueEnv fileModuleEnv

                        result <- { Env = mergedEnv; TypeEnv = mergedTypeEnv; CtorEnv = mergedCtorEnv; RecEnv = mergedRecEnv; ClassEnv = mergedClassEnv; InstEnv = mergedInstEnv; Modules = mergedModules; ModuleValueEnv = mergedModuleValueEnv }

                    | Error diags ->
                        eprintfn "Warning: Type error in %s: %s" file (diags |> List.map formatDiagnostic |> String.concat "\n")
                with ex ->
                    eprintfn "Warning: Failed to load %s: %s" file ex.Message
            result
    else
        emptyPrelude
