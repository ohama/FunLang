module Cli

open Argu

[<CliPrefix(CliPrefix.None)>]
type BuildArgs =
    | [<MainCommand>] Target of name: string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "name of the executable target to build (omit for all)"

[<CliPrefix(CliPrefix.None)>]
type TestArgs =
    | [<MainCommand>] Target of name: string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "name of the test target to run (omit for all)"

[<CliPrefix(CliPrefix.DoubleDash)>]
type CliArgs =
    | [<AltCommandLine("-e")>] Expr of expression: string
    | Emit_Tokens
    | Emit_Ast
    | Emit_Type
    | Check
    | Deps
    | Prelude of path: string
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Test of ParseResults<TestArgs>
    | [<MainCommand; Last>] File of filename: string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Expr _ -> "evaluate expression"
            | Emit_Tokens -> "show lexer tokens"
            | Emit_Ast -> "show parsed AST"
            | Emit_Type -> "show inferred type"
            | Check -> "type-check without executing"
            | Deps -> "show file dependency tree"
            | Prelude _ -> "set Prelude directory path"
            | Build _ -> "type-check project targets (funproj.toml)"
            | Test _ -> "run project test targets (funproj.toml)"
            | File _ -> "evaluate program from file"
