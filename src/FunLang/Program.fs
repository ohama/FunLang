open System
open System.IO
open System.Text.Json.Nodes
open FSharp.Text.Lexing
open Argu
open Cli
open Ast
open Eval
open Format
open TypeCheck
open Diagnostic
open FunLang.IndentFilter

/// Parse a string input as expression (no indentation filtering needed for single-line expressions)
let parse (input: string) (filename: string) : Expr =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf filename
    Parser.start Lexer.tokenize lexbuf

/// Tokenize input and apply IndentFilter, capturing lexbuf positions per token
let lexAndFilter (input: string) (filename: string) : PositionedToken list =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf filename
    let rec collect () =
        let startPos = lexbuf.StartPos
        let tok = Lexer.tokenize lexbuf
        let endPos = lexbuf.EndPos
        if tok = Parser.EOF then
            [{ Token = Parser.EOF; StartPos = startPos; EndPos = endPos }]
        else
            { Token = tok; StartPos = startPos; EndPos = endPos } :: collect ()
    let rawTokens = collect ()
    filterPositioned defaultConfig rawTokens

/// Parse a string input as module (with IndentFilter for indentation-based syntax)
let parseModuleFromString (input: string) (filename: string) : Module =
    let filteredTokens = lexAndFilter input filename
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf filename
    let mutable index = 0
    let mutable lastToken : Parser.token option = None
    let tokenizer (lb: LexBuffer<_>) =
        if index < filteredTokens.Length then
            let pt = filteredTokens.[index]
            index <- index + 1
            lb.StartPos <- pt.StartPos
            lb.EndPos <- pt.EndPos
            lastToken <- Some pt.Token
            pt.Token
        else
            Parser.EOF
    try
        Parser.parseModule tokenizer lexbuf
    with _ex ->
        // Enhanced parse error with location and token info
        let pos = lexbuf.StartPos
        let line = pos.Line
        let col = pos.Column
        let tokenStr =
            match lastToken with
            | Some tok -> Format.formatToken tok
            | None -> "end of input"
        let snippet =
            match Diagnostic.getSourceLine filename line with
            | Some srcLine ->
                let pad = System.String(' ', (sprintf "%d" line).Length)
                sprintf "\n  %s |\n  %d | %s\n  %s | %s^" pad line srcLine pad (System.String(' ', col))
            | None -> ""
        let msg = sprintf "parse error: unexpected %s at %s:%d:%d%s" tokenStr filename line col snippet
        failwith msg

/// Collect recursive file import dependencies via AST walk (parse only, no type-check)
let rec private collectDeps (filePath: string) (visited: Set<string>) (depth: int) : (string * int * bool) list =
    let absPath = Path.GetFullPath(filePath)
    if Set.contains absPath visited then
        [(absPath, depth, true)]  // circular reference
    elif not (File.Exists absPath) then
        [(absPath, depth, false)]  // missing file (still show it)
    else
        let source = File.ReadAllText(absPath)
        let m = parseModuleFromString source absPath
        let decls =
            match m with
            | Module(ds, _) | NamedModule(_, ds, _) -> ds
            | EmptyModule _ -> []
        let imports = decls |> List.choose (function
            | FileImportDecl(path, _) -> Some path
            | _ -> None)
        let childResults =
            imports |> List.collect (fun importPath ->
                let resolved = TypeCheck.resolveImportPath importPath absPath
                collectDeps resolved (Set.add absPath visited) (depth + 1))
        (absPath, depth, false) :: childResults

/// Serialize a TypedModule to JSON, filtering to user-file spans and non-builtin/non-prelude bindings.
let private serializeTypedModule (userFile: string) (tm: ExportApi.TypedModule) : string =
    let absUserFile = Path.GetFullPath(userFile)
    // Load prelude TypeEnv to know which keys to exclude from bindings output
    let preludeTypeEnv = (Prelude.loadPrelude None None).TypeEnv
    let root = JsonObject()
    // Build "annotations" array filtered to spans in the user file
    let annotations = JsonArray()
    for kv in tm.AnnotationMap do
        if kv.Key.FileName = absUserFile then
            let spanObj = JsonObject()
            spanObj["startLine"] <- JsonValue.Create(kv.Key.StartLine)
            spanObj["startCol"] <- JsonValue.Create(kv.Key.StartColumn)
            spanObj["endLine"] <- JsonValue.Create(kv.Key.EndLine)
            spanObj["endCol"] <- JsonValue.Create(kv.Key.EndColumn)
            let entry = JsonObject()
            entry["span"] <- spanObj
            entry["type"] <- JsonValue.Create(Type.formatTypeNormalized kv.Value)
            annotations.Add(entry)
    root["annotations"] <- annotations
    // Build "bindings" object: exclude builtins and prelude bindings
    let bindings = JsonObject()
    for kv in tm.BindingEnv do
        if not (Map.containsKey kv.Key tm.BuiltinSchemes) && not (Map.containsKey kv.Key preludeTypeEnv) then
            bindings[kv.Key] <- JsonValue.Create(Type.formatSchemeNormalized kv.Value)
    root["bindings"] <- bindings
    root.ToJsonString()

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CliArgs>(
        programName = "fn",
        errorHandler = ProcessExiter(colorizer = function
            | ErrorCode.HelpText -> None
            | _ -> Some ConsoleColor.Red))

    try
        let results = parser.Parse(argv, raiseOnUsage = false)

        // Extract --prelude path before loading (must happen before loadPrelude)
        let preludePath =
            if results.Contains Prelude then Some (results.GetResult Prelude)
            else None

        // Check if help was requested
        if results.IsUsageRequested then
            printfn "%s" (parser.PrintUsage())
            0
        // build subcommand: type-check project executable targets
        elif results.Contains Build then
            let buildResults = results.GetResult Build
            let targetName = buildResults.TryGetResult BuildArgs.Target
            match ProjectFile.findFunProj() with
            | None ->
                eprintfn "Error: funproj.toml not found in current directory"
                1
            | Some projPath ->
                match ProjectFile.loadFunProj projPath with
                | Error msg -> eprintfn "%s" msg; 1
                | Ok config ->
                    let targets =
                        match targetName with
                        | None -> config.Executables
                        | Some name ->
                            match config.Executables |> List.tryFind (fun t -> t.Name = name) with
                            | Some t -> [t]
                            | None -> eprintfn "Error: no executable target named '%s'" name; []
                    if targets.IsEmpty && targetName.IsSome then 1
                    elif targets.IsEmpty then
                        eprintfn "No executable targets defined in funproj.toml"
                        0
                    else
                        let prelude = Prelude.loadPrelude preludePath config.PreludePath
                        let mutable exitCode = 0
                        for target in targets do
                            try
                                if not (System.IO.File.Exists target.Main) then
                                    eprintfn "Error: target file not found: %s" target.Main
                                    exitCode <- 1
                                else
                                    let input = System.IO.File.ReadAllText target.Main
                                    TypeCheck.currentTypeCheckingFile <- target.Main
                                    let m = parseModuleFromString input target.Main
                                    let mDecls = match m with | Module(ds,_) | NamedModule(_,ds,_) -> ds | EmptyModule _ -> []
                                    let combinedFix = FixityEnv.collectFixity prelude.FixityEnv mDecls
                                    let m = FixityEnv.rewriteFixity combinedFix m
                                    match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv prelude.TypeEnv prelude.Modules m with
                                    | Ok (warnings, _, _, _, _, _, _) ->
                                        for w in warnings do
                                            eprintfn "Warning: %s" (formatDiagnostic w)
                                        eprintfn "OK: %s (%d warnings)" target.Name (List.length warnings)
                                    | Error diags ->
                                        for d in diags do eprintfn "Error in %s: %s" target.Name (formatDiagnostic d)
                                        exitCode <- 1
                            with ex ->
                                eprintfn "Error in %s: %s" target.Name ex.Message
                                exitCode <- 1
                        exitCode
        // test subcommand: evaluate project test targets
        elif results.Contains Test then
            let testResults = results.GetResult Test
            let targetName = testResults.TryGetResult TestArgs.Target
            match ProjectFile.findFunProj() with
            | None ->
                eprintfn "Error: funproj.toml not found in current directory"
                1
            | Some projPath ->
                match ProjectFile.loadFunProj projPath with
                | Error msg -> eprintfn "%s" msg; 1
                | Ok config ->
                    let targets =
                        match targetName with
                        | None -> config.Tests
                        | Some name ->
                            match config.Tests |> List.tryFind (fun t -> t.Name = name) with
                            | Some t -> [t]
                            | None -> eprintfn "Error: no test target named '%s'" name; []
                    if targets.IsEmpty && targetName.IsSome then 1
                    elif targets.IsEmpty then
                        eprintfn "No test targets defined in funproj.toml"
                        0
                    else
                        let prelude = Prelude.loadPrelude preludePath config.PreludePath
                        let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.Env Eval.initialBuiltinEnv
                        let mutable exitCode = 0
                        for target in targets do
                            try
                                if not (System.IO.File.Exists target.Main) then
                                    eprintfn "Error: target file not found: %s" target.Main
                                    exitCode <- 1
                                else
                                    let input = System.IO.File.ReadAllText target.Main
                                    TypeCheck.currentTypeCheckingFile <- target.Main
                                    Eval.currentEvalFile <- target.Main
                                    Eval.scriptArgs <- []
                                    let m = parseModuleFromString input target.Main
                                    let mDecls2 = match m with | Module(ds,_) | NamedModule(_,ds,_) -> ds | EmptyModule _ -> []
                                    let combinedFix2 = FixityEnv.collectFixity prelude.FixityEnv mDecls2
                                    let m = FixityEnv.rewriteFixity combinedFix2 m
                                    match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv prelude.TypeEnv prelude.Modules m with
                                    | Error diags ->
                                        for d in diags do eprintfn "Error in %s: %s" target.Name (formatDiagnostic d)
                                        exitCode <- 1
                                    | Ok (warnings, _ctorEnv, recEnv, _classEnv, _instEnv, _modules, _typeEnv) ->
                                        for w in warnings do
                                            eprintfn "Warning: %s" (formatDiagnostic w)
                                        let moduleDecls =
                                            match m with
                                            | Module (decls, _) | NamedModule(_, decls, _) -> decls
                                            | EmptyModule _ -> []
                                        let mergedRecEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.RecEnv recEnv
                                        let elaboratedDecls = Elaborate.elaborateTypeclasses moduleDecls
                                        let _finalEnv, _moduleEnv =
                                            Eval.evalModuleDecls mergedRecEnv prelude.ModuleValueEnv initialEnv elaboratedDecls
                                        eprintfn "OK: %s" target.Name
                            with ex ->
                                eprintfn "Error in %s: %s" target.Name ex.Message
                                exitCode <- 1
                        exitCode
        // All other branches: load prelude with None for projPrelude
        else

        // Load prelude from Prelude/*.fun directory
        let prelude = Prelude.loadPrelude preludePath None
        let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.Env Eval.initialBuiltinEnv

        // --emit-tokens with --expr
        if results.Contains Emit_Tokens && results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let tokens = lex expr
                printfn "%s" (formatTokens tokens)
                0
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // --emit-filtered-tokens with --expr
        elif results.Contains Emit_Filtered_Tokens && results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let filtered = lexAndFilter expr "<expr>"
                let tokenStrs = filtered |> List.map (fun pt -> formatToken pt.Token)
                printfn "%s" (String.concat " " tokenStrs)
                0
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // --emit-ast with --expr
        elif results.Contains Emit_Ast && results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let ast = parse expr "<expr>"
                printfn "%s" (formatAst ast)
                0
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // --emit-type with --expr
        elif results.Contains Emit_Type && results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let ast = parse expr "<expr>"
                match typecheckExprWithPrelude prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv prelude.TypeEnv ast with
                | Ok ty ->
                    printfn "%s" (Type.formatTypeNormalized ty)
                    0
                | Error diags ->
                    for d in diags do eprintfn "%s" (formatDiagnostic d)
                    1
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // --check with file (type-check only, no execution)
        elif results.Contains Check && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    TypeCheck.currentTypeCheckingFile <- System.IO.Path.GetFullPath filename
                    let m = parseModuleFromString input filename
                    // Apply fixity rewrite before type-check
                    let chkDecls = match m with | Module(ds,_) | NamedModule(_,ds,_) -> ds | EmptyModule _ -> []
                    let chkFix = FixityEnv.collectFixity prelude.FixityEnv chkDecls
                    let m = FixityEnv.rewriteFixity chkFix m
                    // Filter out modules declared in this file to avoid duplicate errors
                    // when --check is run on a Prelude file that was already loaded
                    let declaredNames =
                        match m with
                        | Module (decls, _) | NamedModule(_, decls, _) ->
                            decls |> List.choose (function Decl.ModuleDecl(name, _, _) -> Some name | _ -> None) |> Set.ofList
                        | EmptyModule _ -> Set.empty
                    let filteredModules = prelude.Modules |> Map.filter (fun k _ -> not (Set.contains k declaredNames))
                    match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv prelude.TypeEnv filteredModules m with
                    | Ok (warnings, _, _, _, _, _, _) ->
                        for w in warnings do
                            eprintfn "Warning: %s" (formatDiagnostic w)
                        eprintfn "OK (%d warnings)" (List.length warnings)
                        0
                    | Error diags ->
                        for d in diags do eprintfn "%s" (formatDiagnostic d)
                        1
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --check without file
        elif results.Contains Check then
            eprintfn "Usage: fn --check <file>"
            1
        // --deps with file (recursive dependency tree)
        elif results.Contains Deps && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let deps = collectDeps filename Set.empty 0
                    for (path, depth, isCircular) in deps do
                        let indent = String.replicate (depth * 2) " "
                        let marker = if isCircular then " (circular)" else ""
                        printfn "%s%s%s" indent (Path.GetFileName path) marker
                    0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --deps without file
        elif results.Contains Deps then
            eprintfn "Usage: fn --deps <file>"
            1
        // --emit-typed-ast with file: type-check and emit JSON annotation map + bindings
        elif results.Contains Emit_Typed_Ast && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let typed = ExportApi.typeCheckFile filename
                    let json = serializeTypedModule filename typed
                    printfn "%s" json
                    0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --emit-type with file (module pipeline)
        elif results.Contains Emit_Type && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    // Set current file path for FileImportDecl relative path resolution
                    TypeCheck.currentTypeCheckingFile <- System.IO.Path.GetFullPath filename
                    let m = parseModuleFromString input filename
                    match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv prelude.TypeEnv prelude.Modules m with
                    | Ok (warnings, _ctorEnv, _recEnv, _classEnv, _instEnv, _modules, typeEnv) ->
                        for w in warnings do
                            eprintfn "Warning: %s" (formatDiagnostic w)
                        // Print types of user-defined top-level bindings (exclude built-in and prelude)
                        let userBindings =
                            typeEnv
                            |> Map.filter (fun k _ -> not (Map.containsKey k TypeCheck.initialTypeEnv) && not (Map.containsKey k prelude.TypeEnv))
                        userBindings
                        |> Map.iter (fun name scheme ->
                            printfn "%s : %s" name (Type.formatSchemeNormalized scheme))
                        0
                    | Error diags ->
                        for d in diags do eprintfn "%s" (formatDiagnostic d)
                        1
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --emit-tokens with file
        elif results.Contains Emit_Tokens && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let tokens = lex input
                    printfn "%s" (formatTokens tokens)
                    0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --emit-filtered-tokens with file
        elif results.Contains Emit_Filtered_Tokens && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let filtered = lexAndFilter input filename
                    let tokenStrs = filtered |> List.map (fun pt -> formatToken pt.Token)
                    printfn "%s" (String.concat " " tokenStrs)
                    0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --emit-ast with file (module pipeline)
        elif results.Contains Emit_Ast && results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    let m = parseModuleFromString input filename
                    printfn "%s" (formatModule m)
                    0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // --expr only
        elif results.Contains Expr then
            let expr = results.GetResult Expr
            try
                let ast = parse expr "<expr>"
                // Type check with prelude environments
                match typecheckExprWithPrelude prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv prelude.TypeEnv ast with
                | Error diags ->
                    for d in diags do eprintfn "%s" (formatDiagnostic d)
                    1
                | Ok _ ->
                    // Type check passed, evaluate with prelude environments
                    let result = eval prelude.RecEnv prelude.ModuleValueEnv initialEnv false ast
                    printfn "%s" (formatValue result)
                    0
            with ex ->
                eprintfn "Error: %s" ex.Message
                1
        // file only -- use module pipeline with IndentFilter
        elif results.Contains File then
            let filename = results.GetResult File
            if File.Exists filename then
                try
                    let input = File.ReadAllText filename
                    // Guard: empty or whitespace-only files produce no declarations
                    if System.String.IsNullOrWhiteSpace input then
                        printfn "()"
                        0
                    else
                    // Set current file path for FileImportDecl relative path resolution
                    let absFilename = System.IO.Path.GetFullPath filename
                    TypeCheck.currentTypeCheckingFile <- absFilename
                    Eval.currentEvalFile <- absFilename
                    // Populate scriptArgs: everything after the script filename in raw argv
                    let rawArgv = System.Environment.GetCommandLineArgs()
                    let idxInRaw = rawArgv |> Array.tryFindIndex (fun a -> a = absFilename || a = filename)
                    Eval.scriptArgs <-
                        match idxInRaw with
                        | Some i -> rawArgv |> Array.skip (i + 1) |> Array.toList
                        | None -> []
                    let m = parseModuleFromString input filename
                    // Apply fixity rewrite before type-check
                    let fileDecls = match m with | Module(ds,_) | NamedModule(_,ds,_) -> ds | EmptyModule _ -> []
                    let combinedFixityEnv = FixityEnv.collectFixity prelude.FixityEnv fileDecls
                    let m = FixityEnv.rewriteFixity combinedFixityEnv m
                    match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv prelude.TypeEnv prelude.Modules m with
                    | Error diags ->
                        for d in diags do eprintfn "%s" (formatDiagnostic d)
                        1
                    | Ok (warnings, _ctorEnv, recEnv, _classEnv, _instEnv, _modules, _typeEnv) ->
                        // Print any warnings (non-exhaustive matches, etc.)
                        for w in warnings do
                            eprintfn "Warning: %s" (formatDiagnostic w)
                        // Extract declarations from any module variant
                        let moduleDecls =
                            match m with
                            | Module (decls, _) | NamedModule(_, decls, _) -> decls
                            | EmptyModule _ -> []
                        // Guard: no declarations means nothing to evaluate
                        if List.isEmpty moduleDecls then
                            printfn "()"
                            0
                        else
                        // Evaluate module declarations with module-aware pipeline
                        let mergedRecEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.RecEnv recEnv
                        let elaboratedDecls = Elaborate.elaborateTypeclasses moduleDecls
                        let finalEnv, moduleEnv =
                            Eval.evalModuleDecls mergedRecEnv prelude.ModuleValueEnv initialEnv elaboratedDecls
                        // Print the last let binding's value (look up from env to avoid re-evaluating side effects)
                        match elaboratedDecls |> List.rev |> List.tryPick (function LetDecl(name, _, _) -> Some name | InfixDecl(_, name, _, _) -> Some name | _ -> None) with
                        | Some lastName ->
                            match Map.tryFind lastName finalEnv with
                            | Some result -> printfn "%s" (formatValue result)
                            | None -> ()
                        | None -> ()
                        0
                with ex ->
                    eprintfn "Error: %s" ex.Message
                    1
            else
                eprintfn "File not found: %s" filename
                1
        // no arguments - start REPL
        else
            Repl.startRepl()
    with
    | :? ArguParseException as ex ->
        eprintfn "%s" ex.Message
        1
