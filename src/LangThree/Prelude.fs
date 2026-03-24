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
let private parseModuleFromString (input: string) (filename: string) : Module =
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
