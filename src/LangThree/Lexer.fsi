module Lexer

open System
open FSharp.Text.Lexing
open Parser  // Import token types from generated Parser module/// Rule tokenize
val tokenize: lexbuf: LexBuffer<char> -> token
/// Rule block_comment
val block_comment: depth: obj -> lexbuf: LexBuffer<char> -> token
/// Rule read_string
val read_string: buf: obj -> lexbuf: LexBuffer<char> -> token
