module ExportApi

open System.IO
open FSharp.Text.Lexing
open Ast
open TypeCheck
open Diagnostic
open FunLang.IndentFilter

/// The result of type-checking a FunLang source file.
/// Contains the per-expression annotation map, the merged binding environment,
/// and the initial builtin schemes.
type TypedModule = {
    /// Map from every source Span to its inferred Type.
    /// Snapshot taken immediately after typeCheckModuleWithPrelude succeeds.
    AnnotationMap: Map<Ast.Span, Type.Type>
    /// The complete binding environment: builtins + prelude + user top-level bindings.
    BindingEnv: TypeCheck.BindingEnv
    /// The initial built-in type environment (before prelude is loaded).
    BuiltinSchemes: Type.TypeEnv
}

/// Tokenize input with position tracking and apply the IndentFilter.
let private lexAndFilter (input: string) (filename: string) : PositionedToken list =
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

/// Parse a string input as a module using the IndentFilter with position tracking.
let private parseModuleFromString (input: string) (filename: string) : Module =
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

/// Type-check a FunLang source file and return a TypedModule.
/// Raises an exception with formatted diagnostics on type error.
let typeCheckFile (filePath: string) : TypedModule =
    let absPath = Path.GetFullPath(filePath)
    let input = File.ReadAllText(absPath)
    let prelude = Prelude.loadPrelude None None
    TypeCheck.currentTypeCheckingFile <- absPath
    let m = parseModuleFromString input absPath
    match TypeCheck.typeCheckModuleWithPrelude
              prelude.CtorEnv prelude.RecEnv prelude.ClassEnv
              prelude.InstEnv prelude.TypeEnv prelude.Modules m with
    | Error diags ->
        let msgs = diags |> List.map formatDiagnostic |> String.concat "\n"
        failwith msgs
    | Ok (_warnings, _ctorEnv, _recEnv, _classEnv, _instEnv, _modules, typeEnv) ->
        // Snapshot annotation map immediately, before any subsequent typeCheckFile call
        // could reset Bidir.annotationMap.
        let annotationMap =
            Bidir.annotationMap
            |> Seq.map (fun kv -> kv.Key, kv.Value)
            |> Map.ofSeq
        {
            AnnotationMap = annotationMap
            BindingEnv = exportBindingEnv typeEnv
            BuiltinSchemes = initialTypeEnv
        }
