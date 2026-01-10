module FunLang.Ast

// =============================================================================
// Position Information
// =============================================================================

type Position = {
    Line: int
    Column: int
    File: string option
}

let noPos = { Line = 0; Column = 0; File = None }

// =============================================================================
// Tokens
// =============================================================================

type Token =
    // Literals
    | INT of int
    | BOOL of bool
    | STRING of string

    // Operators
    | PLUS
    | MINUS
    | STAR
    | SLASH
    | PERCENT
    | EQ
    | NEQ
    | LT
    | GT
    | LTE
    | GTE
    | AND
    | OR
    | NOT

    // Delimiters
    | LPAREN
    | RPAREN
    | LBRACKET
    | RBRACKET
    | COMMA
    | SEMICOLON
    | COLON
    | ARROW
    | DOUBLECOLON
    | PIPE
    | UNDERSCORE

    // Keywords
    | LET
    | REC
    | IN
    | IF
    | THEN
    | ELSE
    | FUN
    | MATCH
    | WITH
    | WHEN
    | TRUE
    | FALSE
    | TYPE
    | OF

    // Identifiers
    | IDENT of string
    | TYPEVAR of string

    // Special
    | EOF
    | NEWLINE
    | INDENT
    | DEDENT

// =============================================================================
// Literals
// =============================================================================

type Literal =
    | LInt of int
    | LBool of bool
    | LString of string
    | LUnit

// =============================================================================
// Operators
// =============================================================================

type BinaryOp =
    | Add | Sub | Mul | Div | Mod
    | Eq | Neq | Lt | Gt | Lte | Gte
    | And | Or

type UnaryOp =
    | Neg | Not

// =============================================================================
// AST (Abstract Syntax Tree)
// =============================================================================

type Expr =
    | ELiteral of Literal
    | EVariable of string
    | EBinaryOp of BinaryOp * Expr * Expr
    | EUnaryOp of UnaryOp * Expr
    | ELet of string * Expr * Expr
    | ELetRec of string * Expr * Expr
    | ELambda of string * Expr
    | EApply of Expr * Expr
    | EIf of Expr * Expr * Expr
    | ETuple of Expr list
    | EList of Expr list
    | ECons of Expr * Expr
    | EBlock of Expr list
    | EMatch of Expr * (Pattern * Expr option * Expr) list

and Pattern =
    | PWildcard
    | PVariable of string
    | PLiteral of Literal
    | PTuple of Pattern list
    | PList of Pattern list
    | PCons of Pattern * Pattern
    | PConstructor of string * Pattern option

// =============================================================================
// Values (Runtime)
// =============================================================================

type Value =
    | VInt of int
    | VBool of bool
    | VString of string
    | VUnit
    | VTuple of Value list
    | VList of Value list
    | VClosure of string * Expr * Env
    | VRecClosure of string * string * Expr * Env

and Env = Map<string, Value>
