# FunLang Grammar Specification

FunLang 언어의 문법 명세입니다. (Version: v0.6.0)

## Table of Contents

- [Lexical Grammar](#lexical-grammar)
- [Syntax Grammar](#syntax-grammar)
- [Module System](#module-system)
- [Operator Precedence](#operator-precedence)
- [AST Types](#ast-types)
- [Examples](#examples)

---

## Lexical Grammar

### Whitespace and Comments

```
whitespace  ::= ' ' | '\t' | '\r'
newline     ::= '\n'
comment     ::= '//' (any char except newline)* newline
```

Newline은 `NEWLINE` 토큰으로 변환되며, Indentation Processor가 `INDENT`/`DEDENT` 토큰을 생성합니다.

### Literals

```
digit       ::= '0' | '1' | ... | '9'
alpha       ::= 'a' | ... | 'z' | 'A' | ... | 'Z' | '_'
alphanum    ::= alpha | digit

integer     ::= digit+
boolean     ::= 'true' | 'false'
string      ::= '"' (char | escape)* '"'
escape      ::= '\n' | '\t' | '\r' | '\"' | '\\'
typevar     ::= '\'' alpha+
```

### Identifiers and Keywords

```
identifier  ::= alpha alphanum*

keyword     ::= 'let' | 'rec' | 'in' | 'if' | 'then' | 'else'
              | 'fun' | 'match' | 'with' | 'when'
              | 'type' | 'of'
              | 'true' | 'false' | 'not' | 'and' | 'or'
              (* Module system keywords *)
              | 'module' | 'export' | 'import'
              | 'open' | 'qualified' | 'as' | 'hiding'
```

### Operators

| Token | Symbol | Description |
|-------|--------|-------------|
| PLUS | `+` | Addition |
| MINUS | `-` | Subtraction / Unary negation |
| STAR | `*` | Multiplication / Tuple type / Export all |
| SLASH | `/` | Division |
| PERCENT | `%` | Modulo |
| EQ | `=` `==` | Equality / Binding |
| NEQ | `!=` `<>` | Inequality |
| LT | `<` | Less than |
| GT | `>` | Greater than |
| LTE | `<=` | Less than or equal |
| GTE | `>=` | Greater than or equal |
| AND_KW | `and` | Logical AND |
| OR_KW | `or` | Logical OR |
| NOT_KW | `not` | Logical NOT |
| DOT | `.` | Qualified access |
| DOTDOT | `..` | Export all constructors |

### Delimiters

| Token | Symbol | Description |
|-------|--------|-------------|
| LPAREN | `(` | Left parenthesis |
| RPAREN | `)` | Right parenthesis |
| LBRACKET | `[` | Left bracket |
| RBRACKET | `]` | Right bracket |
| COMMA | `,` | Comma |
| SEMICOLON | `;` | Semicolon |
| COLON | `:` | Colon |
| DOUBLECOLON | `::` | Cons operator |
| ARROW | `->` | Function arrow |
| PIPE | `\|` | Pattern separator |
| UNDERSCORE | `_` | Wildcard |

### Special Tokens

| Token | Description |
|-------|-------------|
| NEWLINE | Line break |
| INDENT | Indentation increase |
| DEDENT | Indentation decrease |
| EOF | End of file |

---

## Syntax Grammar

### Program

```ebnf
program     ::= module_decls? import_decls? type_defs? top_level_body?

type_defs   ::= type_def (NEWLINE type_def)*

top_level_body ::= top_level_item (NEWLINE top_level_item)*

top_level_item ::= 'let' IDENT '=' expr
                 | 'let' 'rec' IDENT '=' expr
                 | expr
```

### Type Definitions

```ebnf
type_def    ::= 'type' IDENT type_params '=' constructor_list
              | 'type' IDENT type_params '=' INDENT piped_constructor_list DEDENT

type_params ::= TYPEVAR*

constructor_list ::= constructor_def ('|' constructor_def)*

piped_constructor_list ::= '|' constructor_def (NEWLINE '|' constructor_def)*

constructor_def  ::= IDENT ('of' type_expr)?
```

### Type Expressions

```ebnf
type_expr   ::= type_app (STAR type_app)*    (* tuple type *)

type_app    ::= IDENT type_atom+             (* type application *)
              | type_atom

type_atom   ::= TYPEVAR                      (* 'a, 'b *)
              | IDENT                        (* int, bool *)
              | '(' type_expr ')'
```

### Expressions

```ebnf
expr        ::= 'let' IDENT '=' expr 'in' NEWLINE? expr
              | 'let' 'rec' IDENT '=' expr 'in' NEWLINE? expr
              | 'if' expr 'then' NEWLINE? expr NEWLINE? 'else' NEWLINE? expr
              | 'fun' IDENT '->' NEWLINE? expr
              | 'match' expr 'with' match_cases
              | block
              | cons_expr

block       ::= INDENT block_body DEDENT

block_body  ::= block_item (NEWLINE block_item)*

block_item  ::= 'let' IDENT '=' expr         (* block-local binding *)
              | 'let' 'rec' IDENT '=' expr
              | expr
```

### Pattern Matching

```ebnf
match_cases ::= NEWLINE? match_case+

match_case  ::= '|' pattern guard? '->' NEWLINE? expr

guard       ::= 'when' expr

pattern     ::= cons_pattern

cons_pattern ::= IDENT pattern_atom             (* constructor: Some x *)
               | qualified_path pattern_atom    (* qualified: Option.Some x *)
               | qualified_path                 (* nullary: Option.None *)
               | pattern_atom '::' cons_pattern
               | pattern_atom

pattern_atom ::= '_'                         (* wildcard *)
               | IDENT                       (* variable *)
               | INT | BOOL | STRING         (* literal *)
               | '(' ')'                     (* unit *)
               | '(' pattern ')'
               | '(' pattern (',' pattern)+ ')'  (* tuple *)
               | '[' ']'                     (* empty list *)
               | '[' pattern (';' pattern)* ']'  (* list *)
```

### Expression Hierarchy

```ebnf
cons_expr   ::= or_expr ('::' NEWLINE? cons_expr)?    (* right-associative *)

or_expr     ::= and_expr ('or' NEWLINE? and_expr)*

and_expr    ::= eq_expr ('and' NEWLINE? eq_expr)*

eq_expr     ::= cmp_expr (('=' | '<>') NEWLINE? cmp_expr)*

cmp_expr    ::= add_expr (('<' | '>' | '<=' | '>=') NEWLINE? add_expr)*

add_expr    ::= mul_expr (('+' | '-') NEWLINE? mul_expr)*

mul_expr    ::= unary_expr (('*' | '/' | '%') NEWLINE? unary_expr)*

unary_expr  ::= '-' unary_expr
              | 'not' unary_expr
              | app_expr

app_expr    ::= atom+                        (* left-associative *)

atom        ::= INT
              | BOOL
              | STRING
              | IDENT
              | qualified_path               (* Math.add *)
              | '(' ')'                      (* unit *)
              | '(' expr ')'
              | '(' expr (',' expr)+ ')'     (* tuple *)
              | '[' ']'                      (* empty list *)
              | '[' expr (';' expr)* ']'     (* list *)
```

### Qualified Path

```ebnf
qualified_path ::= IDENT ('.' IDENT)+        (* Math.add, Outer.Inner.value *)
```

---

## Module System

### Module Declarations

```ebnf
module_decls ::= module_decl (NEWLINE module_decl)*

module_decl  ::= 'module' IDENT '=' INDENT module_body DEDENT

module_body  ::= export_decl? import_decls? module_items

module_items ::= module_item (NEWLINE module_item)*

module_item  ::= 'let' IDENT '=' expr
               | 'let' 'rec' IDENT '=' expr
               | type_def
```

### Export Declarations

```ebnf
export_decl  ::= 'export' export_list NEWLINE

export_list  ::= export_item (',' export_item)*

export_item  ::= IDENT                                (* value: export add *)
               | 'type' IDENT                         (* opaque type *)
               | 'type' IDENT '(' '..' ')'            (* all constructors *)
               | 'type' IDENT '(' IDENT (',' IDENT)* ')' (* specific constructors *)
               | '*'                                  (* export all *)
```

### Import Declarations

```ebnf
import_decls ::= import_decl+

import_decl  ::= 'open' qualified_path NEWLINE
                 (* open Math → brings all into scope *)
               | 'import' qualified_path '(' IDENT (',' IDENT)* ')' NEWLINE
                 (* import Math (add) → selective *)
               | 'import' 'qualified' qualified_path 'as' IDENT NEWLINE
                 (* import qualified Math as M → M.add *)
               | 'open' qualified_path 'hiding' '(' IDENT (',' IDENT)* ')' NEWLINE
                 (* open Math hiding (add) → all except *)
```

### Module Features

| Feature | Status | Description |
|---------|--------|-------------|
| Module declaration | ✅ | `module Name = ...` |
| Export values | ✅ | `export add, multiply` |
| Qualified access | ✅ | `Math.add 1 2` |
| Recursive functions in modules | ✅ | `let rec fib = ...` in module |
| Nested modules | ❌ | `module Outer = module Inner = ...` |
| Module self-reference | ❌ | `Math.foo` calling `Math.bar` inside Math |
| open/import execution | ❌ | Parsing only, not executed |
| Multi-file modules | ❌ | Planned for Phase 10.3 |

---

## Operator Precedence

Lowest to highest:

| Level | Operators | Associativity | Description |
|-------|-----------|---------------|-------------|
| 1 | `::` | Right | Cons |
| 2 | `or` | Left | Logical OR |
| 3 | `and` | Left | Logical AND |
| 4 | `=` `<>` | Left | Equality |
| 5 | `<` `>` `<=` `>=` | Left | Comparison |
| 6 | `+` `-` | Left | Additive |
| 7 | `*` `/` `%` | Left | Multiplicative |
| 8 | `-` `not` | Right (unary) | Unary |
| 9 | function application | Left | Application |
| 10 | `.` | Left | Qualified access |

---

## AST Types

### Expressions

```fsharp
type Expr =
    | ELiteral of Literal
    | EVariable of string
    | EQualifiedVar of string list          (* Math.add → ["Math"; "add"] *)
    | EBinaryOp of BinaryOp * LExpr * LExpr
    | EUnaryOp of UnaryOp * LExpr
    | ELet of string * LExpr * LExpr
    | ELetRec of string * LExpr * LExpr
    | ELambda of string * LExpr
    | EApply of LExpr * LExpr
    | EIf of LExpr * LExpr * LExpr
    | ETuple of LExpr list
    | EList of LExpr list
    | ECons of LExpr * LExpr
    | EMatch of LExpr * (LPattern * LExpr option * LExpr) list
    | EConstructor of string * LExpr option

type LExpr = Located<Expr>                   (* Expr with position info *)

type Literal =
    | LInt of int
    | LBool of bool
    | LString of string
    | LUnit

type BinaryOp =
    | Add | Sub | Mul | Div | Mod
    | Eq | Neq | Lt | Gt | Lte | Gte
    | And | Or

type UnaryOp = Neg | Not
```

### Patterns

```fsharp
type Pattern =
    | PWildcard
    | PVariable of string
    | PLiteral of Literal
    | PTuple of LPattern list
    | PList of LPattern list
    | PCons of LPattern * LPattern
    | PConstructor of string * LPattern option
    | PQualifiedCons of string list * LPattern option  (* Option.Some x *)

type LPattern = Located<Pattern>
```

### Values

```fsharp
type Value =
    | VInt of int
    | VBool of bool
    | VString of string
    | VUnit
    | VTuple of Value list
    | VList of Value list
    | VClosure of string * LExpr * Env
    | VRecClosure of string * string * LExpr * Env
    | VConstructed of string * Value option

type Env = Map<string, Value>
```

### Module System Types

```fsharp
type ExportSpec =
    | OpaqueType                         (* export type T *)
    | AllConstructors                    (* export type T(..) *)
    | SomeConstructors of string list    (* export type T(A, B) *)

type ExportItem =
    | ExportValue of string              (* export foo *)
    | ExportType of string * ExportSpec  (* export type T(..) *)
    | ExportAll                          (* export * *)

type ImportDecl =
    | OpenModule of string list          (* open Math *)
    | ImportItems of string list * string list  (* import Math (add) *)
    | ImportQualified of string list * string   (* import qualified Math as M *)
    | ImportHiding of string list * string list (* open Math hiding (add) *)

type Visibility = Public | Private

type ModuleItem =
    | MIValue of string * LExpr * Visibility
    | MIRecValue of string * LExpr * Visibility
    | MIType of TypeDef * Visibility

type ModuleDecl = {
    Name: string
    Exports: ExportItem list option
    Imports: ImportDecl list
    Items: ModuleItem list
    Pos: Position
}
```

### Type Definitions

```fsharp
type TypeExpr =
    | TEVar of string              (* 'a *)
    | TEName of string             (* int *)
    | TEApp of string * TypeExpr list  (* List 'a *)
    | TETuple of TypeExpr list     (* 'a * 'b *)

type ConstructorDef = string * TypeExpr option

type TypeDef = {
    Name: string
    TypeParams: string list
    Constructors: ConstructorDef list
}

type Program = {
    Modules: ModuleDecl list
    Imports: ImportDecl list
    TypeDefs: TypeDef list
    MainExpr: LExpr option
}
```

### Type System

```fsharp
type Type =
    | TInt | TBool | TString | TUnit
    | TVar of int
    | TFun of Type * Type
    | TList of Type
    | TTuple of Type list
    | TConstructor of string * Type list

type TypeScheme = Forall of int list * Type
```

---

## Examples

### Comments

```funlang
// This is a single-line comment
let x = 42  // inline comment
```

### Literals and Operators

```funlang
(* Arithmetic *)
1 + 2 * 3                    (* 7 *)
10 / 3                       (* 3 *)
10 % 3                       (* 1 *)

(* Comparison *)
5 < 10                       (* true *)
3 = 3                        (* true *)
3 <> 4                       (* true *)

(* Boolean *)
true and false               (* false *)
true or false                (* true *)
not true                     (* false *)
```

### Bindings and Functions

```funlang
(* Let binding *)
let x = 42 in x + 1          (* 43 *)

(* Lambda *)
fun x -> x + 1

(* Function application *)
(fun x -> x * 2) 21          (* 42 *)

(* Recursive function *)
let rec factorial = fun n ->
    if n = 0 then 1
    else n * factorial (n - 1)
in factorial 5               (* 120 *)
```

### Data Structures

```funlang
(* Tuples *)
(1, 2, 3)
let pair = (10, 20) in pair

(* Lists *)
[1; 2; 3]
1 :: 2 :: 3 :: []            (* [1; 2; 3] *)
```

### Pattern Matching

```funlang
(* Simple match *)
match x with
| 0 -> "zero"
| 1 -> "one"
| _ -> "other"

(* List patterns *)
match xs with
| [] -> 0
| x :: rest -> x + sum rest

(* Tuple pattern *)
match pair with
| (a, b) -> a + b

(* Guard *)
match n with
| x when x > 0 -> "positive"
| x when x < 0 -> "negative"
| _ -> "zero"
```

### User-Defined Types

```funlang
(* Option type *)
type Option 'a = None | Some of 'a

match Some 42 with
| None -> 0
| Some x -> x

(* List type *)
type List 'a = Nil | Cons of 'a * List 'a

let rec length = fun xs ->
    match xs with
    | Nil -> 0
    | Cons (_, rest) -> 1 + length rest
in length (Cons (1, Cons (2, Nil)))  (* 2 *)

(* Multiline type definition *)
type Tree 'a =
    | Leaf
    | Node of Tree 'a * 'a * Tree 'a
```

### Indentation-Based Blocks

```funlang
(* Block expression *)
let result =
    let x = 10
    let y = 20
    x + y
in result                    (* 30 *)

(* Multiline if *)
if condition then
    let a = compute1 ()
    let b = compute2 ()
    a + b
else
    defaultValue
```

### Module System

```funlang
(* Module declaration *)
module Math =
    export add, multiply

    let add = fun x -> fun y -> x + y
    let multiply = fun x -> fun y -> x * y

(* Using qualified access *)
Math.add 1 2                 (* 3 *)
Math.multiply 3 4            (* 12 *)

(* Module with recursive function *)
module Fib =
    export fib

    let rec fib = fun n ->
        if n < 2 then n
        else fib (n - 1) + fib (n - 2)

Fib.fib 10                   (* 55 *)

(* Module with type definition *)
module Option =
    export type Option(..), none, some

    type Option 'a = None | Some of 'a

    let none = None
    let some = fun x -> Some x

(* Qualified constructor in pattern (planned) *)
match value with
| Option.None -> "empty"
| Option.Some x -> "has value"
```

### Import Declarations (Syntax Only)

```funlang
(* These are parsed but not yet executed *)

open Math                    (* brings all exports into scope *)
import Math (add, multiply)  (* selective import *)
import qualified Math as M   (* M.add, M.multiply *)
open Math hiding (multiply)  (* all except multiply *)
```

---

## REPL Commands

```
:help, :h       Show help
:quit, :q       Exit REPL
:tokens <expr>  Show tokens
:ast <expr>     Show AST
:env            Show environment
:clear          Clear environment
```

---

## Error Codes

| Range | Category | Examples |
|-------|----------|----------|
| E001-E099 | Lexer | E001 Unexpected character |
| E100-E199 | Parser | E101 Unexpected token |
| E200-E299 | Type | E201 Type mismatch, E202 Unbound variable |
| E300-E399 | Runtime | E301 Division by zero |
