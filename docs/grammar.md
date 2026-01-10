# FunLang Grammar Specification

This document describes the grammar of FunLang as currently implemented.

## Table of Contents

- [Lexical Grammar](#lexical-grammar)
- [Expression Grammar](#expression-grammar)
- [Operator Precedence](#operator-precedence)
- [Implementation Status](#implementation-status)
- [Examples](#examples)

---

## Lexical Grammar

### Whitespace and Comments

```
whitespace  ::= ' ' | '\t' | '\r'
newline     ::= '\n'
```

> Note: Newlines are currently skipped. Indentation-based syntax (Phase 1.2) will add INDENT/DEDENT tokens.

### Literals

```
digit       ::= '0' | '1' | ... | '9'
alpha       ::= 'a' | ... | 'z' | 'A' | ... | 'Z' | '_'
alphanum    ::= alpha | digit

integer     ::= digit+
boolean     ::= 'true' | 'false'
string      ::= '"' (char | escape)* '"'
escape      ::= '\' char
```

### Identifiers and Keywords

```
identifier  ::= alpha alphanum*

keyword     ::= 'let' | 'rec' | 'in' | 'if' | 'then' | 'else'
              | 'fun' | 'match' | 'with' | 'when'
              | 'true' | 'false' | 'not' | 'and' | 'or'
              | 'type' | 'of'
```

### Operators

| Token | Symbol | Description |
|-------|--------|-------------|
| PLUS | `+` | Addition |
| MINUS | `-` | Subtraction / Unary negation |
| STAR | `*` | Multiplication |
| SLASH | `/` | Division |
| PERCENT | `%` | Modulo |
| EQ | `=` or `==` | Equality |
| NEQ | `!=` or `<>` | Inequality |
| LT | `<` | Less than |
| GT | `>` | Greater than |
| LTE | `<=` | Less than or equal |
| GTE | `>=` | Greater than or equal |
| AND | `and` | Logical AND |
| OR | `or` | Logical OR |
| NOT | `not` | Logical NOT |

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

### Token Summary

```
Token       ::= INT integer
              | BOOL boolean
              | STRING string
              | IDENT identifier
              | keyword
              | operator
              | delimiter
              | EOF
```

---

## Expression Grammar

### Currently Implemented (Parser)

```ebnf
program     ::= expr

expr        ::= let_expr
              | let_rec_expr
              | if_expr
              | lambda_expr
              | cons_expr

let_expr    ::= 'let' IDENT '=' expr 'in' expr

let_rec_expr ::= 'let' 'rec' IDENT '=' expr 'in' expr

if_expr     ::= 'if' expr 'then' expr 'else' expr

lambda_expr ::= 'fun' IDENT '->' expr

cons_expr   ::= or_expr ('::' cons_expr)?     (* right-associative *)

or_expr     ::= and_expr ('or' and_expr)*

and_expr    ::= eq_expr ('and' eq_expr)*

eq_expr     ::= cmp_expr (('==' | '!=') cmp_expr)*

cmp_expr    ::= add_expr (('<' | '>' | '<=' | '>=') add_expr)*

add_expr    ::= mul_expr (('+' | '-') mul_expr)*

mul_expr    ::= unary_expr (('*' | '/' | '%') unary_expr)*

unary_expr  ::= '-' unary_expr
              | 'not' unary_expr
              | apply_expr

apply_expr  ::= atom atom*                    (* left-associative *)

atom        ::= INT
              | BOOL
              | STRING
              | IDENT
              | '(' ')'                       (* unit *)
              | '(' expr ')'                  (* parenthesized *)
              | '(' expr ',' expr (',' expr)* ')'  (* tuple *)
              | '[' ']'                       (* empty list *)
              | '[' expr (';' expr)* ']'      (* list *)
```

### Not Yet Implemented

```ebnf
(* Pattern matching - AST defined but parser returns error *)

match_expr  ::= 'match' expr 'with' match_cases

match_cases ::= '|'? pattern guard? '->' expr ('|' pattern guard? '->' expr)*

guard       ::= 'when' expr

pattern     ::= '_'                           (* wildcard *)
              | IDENT                         (* variable *)
              | INT | BOOL | STRING           (* literal *)
              | '(' pattern (',' pattern)+ ')'  (* tuple *)
              | '[' ']'                       (* empty list *)
              | '[' pattern (';' pattern)* ']'  (* list *)
              | pattern '::' pattern          (* cons *)
              | IDENT pattern?                (* constructor *)
```

---

## Operator Precedence

Operators are listed from **lowest** to **highest** precedence:

| Level | Operators | Associativity | Description |
|-------|-----------|---------------|-------------|
| 1 | `or` | Left | Logical OR |
| 2 | `and` | Left | Logical AND |
| 3 | `==` `!=` | Left | Equality |
| 4 | `<` `>` `<=` `>=` | Left | Comparison |
| 5 | `+` `-` | Left | Additive |
| 6 | `*` `/` `%` | Left | Multiplicative |
| 7 | `-` `not` | Right | Unary |
| 8 | function application | Left | Application |

### Precedence Examples

```funlang
1 + 2 * 3           (* = 1 + (2 * 3) = 7 *)
(1 + 2) * 3         (* = 9 *)

true or false and false   (* = true or (false and false) = true *)
not true and false        (* = (not true) and false = false *)

1 < 2 and 3 > 0           (* = (1 < 2) and (3 > 0) = true *)
```

---

## Implementation Status

### ✅ Fully Implemented

| Feature | Lexer | Parser | Interpreter | Tests |
|---------|-------|--------|-------------|-------|
| Integer literals | ✅ | ✅ | ✅ | ✅ |
| Boolean literals | ✅ | ✅ | ✅ | ✅ |
| String literals | ✅ | ✅ | ✅ | ✅ |
| Variables | ✅ | ✅ | ✅ | ✅ |
| Arithmetic (`+` `-` `*` `/` `%`) | ✅ | ✅ | ✅ | ✅ |
| Comparison (`<` `>` `<=` `>=`) | ✅ | ✅ | ✅ | ✅ |
| Equality (`==` `!=`) | ✅ | ✅ | ✅ | ✅ |
| Boolean ops (`and` `or` `not`) | ✅ | ✅ | ✅ | ✅ |
| Unary negation (`-`) | ✅ | ✅ | ✅ | ✅ |
| Let binding | ✅ | ✅ | ✅ | ✅ |
| Recursive let (`let rec`) | ✅ | ✅ | ✅ | ✅ |
| If-then-else | ✅ | ✅ | ✅ | ✅ |
| Lambda (`fun x -> e`) | ✅ | ✅ | ✅ | ✅ |
| Function application | ✅ | ✅ | ✅ | ✅ |
| Tuples | ✅ | ✅ | ✅ | ✅ |
| Lists | ✅ | ✅ | ✅ | ✅ |
| Cons (`::`) | ✅ | ✅ | ✅ | ✅ |
| Unit `()` | ✅ | ✅ | ✅ | ✅ |
| Parentheses | ✅ | ✅ | ✅ | ✅ |

### 🔶 Partially Implemented

| Feature | Lexer | Parser | Interpreter | Tests |
|---------|-------|--------|-------------|-------|
| Block expressions | ✅ | ❌ | ✅ | ❌ |

### ❌ Not Yet Implemented

| Feature | Status |
|---------|--------|
| Pattern matching (`match`) | AST defined, returns error |
| Indentation syntax (INDENT/DEDENT) | Planned (Phase 1.2) |
| Type inference | Planned (Phase 5) |
| User-defined types | Planned (Phase 6) |

---

## Examples

### Currently Working

```funlang
(* Arithmetic *)
1 + 2 * 3                    (* => 7 *)
(1 + 2) * 3                  (* => 9 *)
10 / 3                       (* => 3 *)
10 % 3                       (* => 1 *)

(* Comparison *)
5 < 10                       (* => true *)
5 >= 5                       (* => true *)
3 == 3                       (* => true *)
3 != 4                       (* => true *)

(* Boolean logic *)
true and false               (* => false *)
true or false                (* => true *)
not true                     (* => false *)

(* Let bindings *)
let x = 42 in x              (* => 42 *)
let x = 1 in let y = 2 in x + y   (* => 3 *)

(* If expressions *)
if true then 1 else 2        (* => 1 *)
if 5 < 10 then 10 else 5     (* => 10 *)

(* Lambda expressions *)
fun x -> x + 1               (* => <function> *)
(fun x -> x) 42              (* => 42 *)
(fun x -> fun y -> x + y) 2 3  (* => 5 *)

(* Function application *)
let double = fun x -> x * 2 in double 21  (* => 42 *)
let apply = fun f -> fun x -> f x in
  let sq = fun n -> n * n in
  apply sq 3                 (* => 9 *)

(* Closures *)
let x = 10 in
  let addX = fun y -> x + y in
  addX 5                     (* => 15 *)

(* Recursive functions *)
let rec factorial = fun n ->
  if n == 0 then 1
  else n * factorial (n - 1)
in factorial 5               (* => 120 *)

let rec fib = fun n ->
  if n < 2 then n
  else fib (n - 1) + fib (n - 2)
in fib 10                    (* => 55 *)

(* Tuples *)
(1, 2, 3)                    (* => (1, 2, 3) *)
((1, 2), 3)                  (* => ((1, 2), 3) *)
let pair = (10, 20) in pair  (* => (10, 20) *)

(* Lists *)
[]                           (* => [] *)
[1; 2; 3]                    (* => [1; 2; 3] *)
[[1]; [2; 3]]                (* => [[1]; [2; 3]] *)

(* Cons operator *)
1 :: []                      (* => [1] *)
1 :: 2 :: 3 :: []            (* => [1; 2; 3] *)
0 :: [1; 2]                  (* => [0; 1; 2] *)

(* Unit *)
()                           (* => () *)
```

### Planned Syntax (Not Yet Implemented)

```funlang
(* Pattern matching *)
match xs with
| [] -> 0
| x :: rest -> x + sum rest

(* Indentation-based blocks *)
let result =
    let x = 10
    let y = 20
    x + y
```

---

## AST Types

### Expressions (`Expr`)

```fsharp
type Expr =
    | ELiteral of Literal           // 42, true, "hello"
    | EVariable of string           // x, foo, bar
    | EBinaryOp of BinaryOp * Expr * Expr  // a + b
    | EUnaryOp of UnaryOp * Expr    // -x, not b
    | ELet of string * Expr * Expr  // let x = e1 in e2
    | ELetRec of string * Expr * Expr  // let rec f = e1 in e2
    | ELambda of string * Expr      // fun x -> e
    | EApply of Expr * Expr         // f x
    | EIf of Expr * Expr * Expr     // if c then e1 else e2
    | ETuple of Expr list           // (a, b, c)
    | EList of Expr list            // [1; 2; 3]
    | ECons of Expr * Expr          // h :: t
    | EBlock of Expr list           // indented block
    | EMatch of Expr * (Pattern * Expr option * Expr) list
```

### Patterns (`Pattern`)

```fsharp
type Pattern =
    | PWildcard                     // _
    | PVariable of string           // x
    | PLiteral of Literal           // 42, true
    | PTuple of Pattern list        // (a, b)
    | PList of Pattern list         // [a; b]
    | PCons of Pattern * Pattern    // h :: t
    | PConstructor of string * Pattern option  // Some x
```

### Values (`Value`)

```fsharp
type Value =
    | VInt of int                   // 42
    | VBool of bool                 // true
    | VString of string             // "hello"
    | VUnit                         // ()
    | VTuple of Value list          // (1, 2)
    | VList of Value list           // [1; 2]
    | VClosure of string * Expr * Env        // closure
    | VRecClosure of string * string * Expr * Env  // recursive closure
```

---

## REPL Commands

```
:help, :h       Show help
:quit, :q       Exit REPL
:tokens <expr>  Show tokens for expression
:ast <expr>     Show AST for expression
:env            Show current environment
:clear          Clear environment
```

---

## Version History

| Version | Features |
|---------|----------|
| 0.1.0 | Phase 0 + Phase 1: Lexer, Parser, Interpreter for core expressions |
| 0.2.0 | Phase 2 + Phase 3: Lambda, function application, let rec, tuples, lists, cons |
