# FunLang Grammar Specification

FunLang 언어의 문법 명세입니다.

## Table of Contents

- [Lexical Grammar](#lexical-grammar)
- [Syntax Grammar](#syntax-grammar)
- [Operator Precedence](#operator-precedence)
- [AST Types](#ast-types)
- [Examples](#examples)

---

## Lexical Grammar

### Whitespace

```
whitespace  ::= ' ' | '\t' | '\r'
newline     ::= '\n'
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
```

### Operators

| Token | Symbol | Description |
|-------|--------|-------------|
| PLUS | `+` | Addition |
| MINUS | `-` | Subtraction / Unary negation |
| STAR | `*` | Multiplication / Tuple type |
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
program     ::= type_defs? expr

type_defs   ::= type_def (NEWLINE type_def)*
```

### Type Definitions

```ebnf
type_def    ::= 'type' IDENT type_params '=' constructor_list

type_params ::= TYPEVAR*

constructor_list ::= constructor_def ('|' constructor_def)*

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
expr        ::= 'let' IDENT '=' expr 'in' expr
              | 'let' 'rec' IDENT '=' expr 'in' expr
              | 'if' expr 'then' expr 'else' expr
              | 'fun' IDENT '->' expr
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

match_case  ::= '|' pattern guard? '->' expr

guard       ::= 'when' expr

pattern     ::= cons_pattern

cons_pattern ::= IDENT pattern_atom          (* constructor *)
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
cons_expr   ::= or_expr ('::' cons_expr)?    (* right-associative *)

or_expr     ::= and_expr ('or' and_expr)*

and_expr    ::= eq_expr ('and' eq_expr)*

eq_expr     ::= cmp_expr (('==' | '!=') cmp_expr)*

cmp_expr    ::= add_expr (('<' | '>' | '<=' | '>=') add_expr)*

add_expr    ::= mul_expr (('+' | '-') mul_expr)*

mul_expr    ::= unary_expr (('*' | '/' | '%') unary_expr)*

unary_expr  ::= '-' unary_expr
              | 'not' unary_expr
              | app_expr

app_expr    ::= atom+                        (* left-associative *)

atom        ::= INT
              | BOOL
              | STRING
              | IDENT
              | '(' ')'                      (* unit *)
              | '(' expr ')'
              | '(' expr (',' expr)+ ')'     (* tuple *)
              | '[' ']'                      (* empty list *)
              | '[' expr (';' expr)* ']'     (* list *)
```

---

## Operator Precedence

Lowest to highest:

| Level | Operators | Associativity | Description |
|-------|-----------|---------------|-------------|
| 1 | `::` | Right | Cons |
| 2 | `or` | Left | Logical OR |
| 3 | `and` | Left | Logical AND |
| 4 | `==` `!=` | Left | Equality |
| 5 | `<` `>` `<=` `>=` | Left | Comparison |
| 6 | `+` `-` | Left | Additive |
| 7 | `*` `/` `%` | Left | Multiplicative |
| 8 | `-` `not` | Right (unary) | Unary |
| 9 | function application | Left | Application |

---

## AST Types

### Expressions

```fsharp
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
    | EConstructor of string * Expr option

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
    | PTuple of Pattern list
    | PList of Pattern list
    | PCons of Pattern * Pattern
    | PConstructor of string * Pattern option
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
    | VClosure of string * Expr * Env
    | VRecClosure of string * string * Expr * Env
    | VConstructed of string * Value option

type Env = Map<string, Value>
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
    TypeDefs: TypeDef list
    MainExpr: Expr option
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

### Literals and Operators

```funlang
(* Arithmetic *)
1 + 2 * 3                    (* 7 *)
10 / 3                       (* 3 *)
10 % 3                       (* 1 *)

(* Comparison *)
5 < 10                       (* true *)
3 == 3                       (* true *)
3 != 4                       (* true *)

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
    if n == 0 then 1
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

(* Binary tree *)
type Tree 'a = Leaf | Node of Tree 'a * 'a * Tree 'a
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
