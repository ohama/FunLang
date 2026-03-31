module Cli

open Argu

[<CliPrefix(CliPrefix.DoubleDash)>]
type CliArgs =
    | [<AltCommandLine("-e")>] Expr of expression: string
    | Emit_Tokens
    | Emit_Ast
    | Emit_Type
    | Check
    | Deps
    | Prelude of path: string
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
            | File _ -> "evaluate program from file"
