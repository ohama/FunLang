module Cli

open Argu

[<CliPrefix(CliPrefix.DoubleDash)>]
type CliArgs =
    | [<AltCommandLine("-e")>] Expr of expression: string
    | Emit_Tokens
    | Emit_Ast
    | Emit_Type
    | Repl
    | [<MainCommand; Last>] File of filename: string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Expr _ -> "evaluate expression"
            | Emit_Tokens -> "show lexer tokens"
            | Emit_Ast -> "show parsed AST"
            | Emit_Type -> "show inferred type"
            | Repl -> "start interactive REPL"
            | File _ -> "evaluate program from file"
