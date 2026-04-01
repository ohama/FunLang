module Repl

open System
open FSharp.Text.Lexing
open Ast
open Eval
open Type
open Format
open LangThree.IndentFilter

/// Parse a string input as expression and return the AST
let private parseExpr (input: string) : Expr =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf "<repl>"
    Parser.start Lexer.tokenize lexbuf

/// Parse a string input as module declarations (for persistent let bindings)
/// Reuses the same lexAndFilter + parseModule pattern as Program.fs
let private parseModule (input: string) : Module =
    let lexbuf = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf "<repl>"
    let rec collect () =
        let startPos = lexbuf.StartPos
        let tok = Lexer.tokenize lexbuf
        let endPos = lexbuf.EndPos
        if tok = Parser.EOF then
            [{ Token = Parser.EOF; StartPos = startPos; EndPos = endPos }]
        else
            { Token = tok; StartPos = startPos; EndPos = endPos } :: collect ()
    let rawTokens = collect ()
    let filteredTokens = filterPositioned defaultConfig rawTokens
    let lexbuf2 = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf2 "<repl>"
    let mutable index = 0
    let tokenizer (lb: LexBuffer<_>) =
        if index < filteredTokens.Length then
            let pt = filteredTokens.[index]
            index <- index + 1
            lb.StartPos <- pt.StartPos
            lb.EndPos <- pt.EndPos
            pt.Token
        else
            Parser.EOF
    Parser.parseModule tokenizer lexbuf2

/// Simple history buffer
let private history = System.Collections.Generic.List<string>()
let mutable private historyIndex = 0

/// Read a line with basic history support (up/down arrows)
let private readLineWithHistory (prompt: string) : string option =
    Console.Write prompt
    Console.Out.Flush()
    // Use Console.ReadLine for simplicity (ReadLine NuGet not available)
    // History is tracked but arrow key navigation requires raw mode
    match Console.ReadLine() with
    | null -> None
    | line ->
        if line.Length > 0 && (history.Count = 0 || history.[history.Count - 1] <> line) then
            history.Add(line)
        historyIndex <- history.Count
        Some line

/// REPL state: environments that persist across lines
type ReplState = {
    Env: Env
    TypeEnv: TypeEnv
    CtorEnv: ConstructorEnv
    RecEnv: RecordEnv
    ClassEnv: ClassEnv
    InstEnv: InstanceEnv
    Modules: Map<string, TypeCheck.ModuleExports>
    ModuleValueEnv: Map<string, ModuleValueEnv>
}

/// Try to evaluate as a module-level declaration (let, type, etc.)
let private tryEvalDecl (state: ReplState) (input: string) : ReplState option =
    try
        let m = parseModule input
        let decls =
            match m with
            | Module(decls, _) | NamedModule(_, decls, _) | NamespacedModule(_, decls, _) -> decls
            | EmptyModule _ -> []
        if List.isEmpty decls then None
        else
            // Type check
            Bidir.mutableVars <- Set.empty
            Bidir.currentClassEnv <- state.ClassEnv
            Bidir.currentInstEnv <- state.InstEnv
            Bidir.pendingConstraints <- []
            Bidir.accumulatedErrors <- []
            let (typeEnv, ctorEnv, recEnv, classEnv, instEnv, modules, _warnings) =
                TypeCheck.typeCheckDecls decls state.TypeEnv state.CtorEnv state.RecEnv state.ClassEnv state.InstEnv state.Modules
            // Elaborate typeclasses
            let elaborated = Elaborate.elaborateTypeclasses decls
            // Evaluate
            let mergedRecEnv = Map.fold (fun acc k v -> Map.add k v acc) state.RecEnv recEnv
            let (finalEnv, moduleEnv) = Eval.evalModuleDecls mergedRecEnv state.ModuleValueEnv state.Env elaborated
            // Print the last binding's value
            match decls |> List.rev |> List.tryPick (function LetDecl(name, _, _) -> Some name | _ -> None) with
            | Some lastName ->
                match Map.tryFind lastName finalEnv with
                | Some value ->
                    // Show type + value like F# Interactive
                    let scheme = Map.tryFind lastName typeEnv
                    match scheme with
                    | Some s -> printfn "val %s : %s = %s" lastName (Type.formatSchemeNormalized s) (formatValue value)
                    | None -> printfn "val %s = %s" lastName (formatValue value)
                | None -> ()
            | None ->
                // Type declaration or other — just acknowledge
                match decls |> List.tryHead with
                | Some (Decl.TypeDecl _) -> printfn "type defined"
                | Some (TypeClassDecl _) -> printfn "typeclass defined"
                | Some (InstanceDecl _) -> printfn "instance defined"
                | Some (DerivingDecl _) -> printfn "deriving applied"
                | _ -> ()
            Some {
                Env = finalEnv
                TypeEnv = typeEnv
                CtorEnv = ctorEnv
                RecEnv = mergedRecEnv
                ClassEnv = classEnv
                InstEnv = instEnv
                Modules = modules
                ModuleValueEnv = moduleEnv
            }
    with _ -> None

/// REPL loop with persistent state
let rec private replLoop (state: ReplState) : unit =
    match readLineWithHistory "funlang> " with
    | None ->
        // EOF (Ctrl+D)
        printfn ""
    | Some "#quit" | Some "#exit" ->
        ()
    | Some "" ->
        replLoop state
    | Some line when line.StartsWith(":type ") ->
        // :type command — show inferred type of expression
        let expr = line.Substring(6).Trim()
        try
            let ast = parseExpr expr
            Bidir.mutableVars <- Set.empty
            Bidir.currentClassEnv <- state.ClassEnv
            Bidir.currentInstEnv <- state.InstEnv
            Bidir.pendingConstraints <- []
            let _, ty = Bidir.synth state.CtorEnv state.RecEnv [] state.TypeEnv ast
            printfn "%s" (Type.formatTypeNormalized ty)
        with ex ->
            eprintfn "Error: %s" ex.Message
        replLoop state
    | Some line when line.StartsWith(":load ") ->
        // :load command — load a file into REPL environment
        let path = line.Substring(6).Trim().Trim('"')
        if System.IO.File.Exists(path) then
            try
                let input = System.IO.File.ReadAllText(path)
                match tryEvalDecl state input with
                | Some newState ->
                    printfn "Loaded: %s" path
                    replLoop newState
                | None ->
                    eprintfn "Error: failed to load %s" path
                    replLoop state
            with ex ->
                eprintfn "Error loading %s: %s" path ex.Message
                replLoop state
        else
            eprintfn "File not found: %s" path
            replLoop state
    | Some line when line.StartsWith(":help") ->
        printfn "Commands:"
        printfn "  :type <expr>   — show inferred type"
        printfn "  :load <file>   — load file into environment"
        printfn "  :help          — show this help"
        printfn "  #quit          — exit REPL"
        printfn ""
        printfn "You can define persistent bindings:"
        printfn "  let x = 42"
        printfn "  type Color = | Red | Green | Blue"
        replLoop state
    | Some line ->
        // Try as declaration first (let, type, module, etc.)
        match tryEvalDecl state line with
        | Some newState ->
            replLoop newState
        | None ->
            // Fall back to expression evaluation
            try
                let ast = parseExpr line
                let result = eval state.RecEnv state.ModuleValueEnv state.Env false ast
                // Infer type for display
                try
                    Bidir.mutableVars <- Set.empty
                    Bidir.currentClassEnv <- state.ClassEnv
                    Bidir.currentInstEnv <- state.InstEnv
                    Bidir.pendingConstraints <- []
                    let _, ty = Bidir.synth state.CtorEnv state.RecEnv [] state.TypeEnv ast
                    printfn "- : %s = %s" (Type.formatTypeNormalized ty) (formatValue result)
                with _ ->
                    printfn "%s" (formatValue result)
                replLoop state
            with ex ->
                eprintfn "Error: %s" ex.Message
                replLoop state

/// Start the REPL with welcome message
let startRepl () : int =
    printfn "LangThree REPL v14.0"
    printfn "Type :help for commands, #quit or Ctrl+D to exit."
    printfn ""
    let prelude = Prelude.loadPrelude None None
    let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) prelude.Env Eval.initialBuiltinEnv
    let state = {
        Env = initialEnv
        TypeEnv = Map.fold (fun acc k v -> Map.add k v acc) TypeCheck.initialTypeEnv prelude.TypeEnv
        CtorEnv = prelude.CtorEnv
        RecEnv = prelude.RecEnv
        ClassEnv = prelude.ClassEnv
        InstEnv = prelude.InstEnv
        Modules = prelude.Modules
        ModuleValueEnv = prelude.ModuleValueEnv
    }
    replLoop state
    0
