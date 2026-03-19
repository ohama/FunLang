# Chapter 13: CLI Reference

This chapter covers all LangThree command-line modes and options.

## Expression Mode

Evaluate a single expression with `--expr`:

```
$ langthree --expr '1 + 2'
3
```

The expression is parsed, type-checked, and evaluated. The result is printed
to standard output.

Different result types:

```
$ langthree --expr '42'
42

$ langthree --expr '"hello"'
"hello"

$ langthree --expr 'true'
true

$ langthree --expr '[1, 2, 3]'
[1, 2, 3]

$ langthree --expr '(1, "hello", true)'
(1, "hello", true)

$ langthree --expr '()'
()

$ langthree --expr 'fun x -> x + 1'
<function>
```

Expression mode uses `let ... in` syntax for local bindings:

```
$ langthree --expr 'let x = 5 in let y = 10 in x + y'
15
```

Expression mode is single-line only -- no indentation support.

## File Mode

Evaluate a program file:

```
$ cat hello.l3
let greeting = "hello"
let result = greeting + " world"

$ langthree hello.l3
"hello world"
```

File mode supports indentation-based syntax. The last `let` binding's value
is printed. Use `let _ =` for side effects before the final result:

```
$ cat greet.l3
let _ = println "starting..."
let name = "world"
let result = "hello " + name

$ langthree greet.l3
starting...
"hello world"
```

File mode supports all LangThree features: type declarations, modules,
exceptions, pattern matching with indentation, and multi-line expressions.

The `.l3` extension is convention, not enforced -- any filename works.

## AST Emission

Inspect the parsed abstract syntax tree with `--emit-ast`:

```
$ langthree --emit-ast --expr '1 + 2'
Add (Number 1, Number 2)

$ langthree --emit-ast --expr 'fun x -> x + 1'
Lambda ("x", Add (Var "x", Number 1))

$ langthree --emit-ast --expr 'let x = 5 in x + 1'
Let ("x", Number 5, Add (Var "x", Number 1))
```

With file mode, each declaration is shown:

```
$ cat ast_demo.l3
let x = 42
let add a b = a + b

$ langthree --emit-ast ast_demo.l3
LetDecl ("x", Number 42)
LetDecl ("add", Lambda ("a", Lambda ("b", Add (Var "a", Var "b"))))
```

Useful for debugging parse issues -- verifies that your code is parsed as intended.

## Type Emission

Inspect inferred types with `--emit-type`:

```
$ langthree --emit-type --expr '1 + 2'
int

$ langthree --emit-type --expr 'fun x -> x + 1'
int -> int

$ langthree --emit-type --expr '"hello"'
string
```

In file mode, types of all user-defined top-level bindings are shown
(alphabetically sorted, excluding built-in and prelude bindings):

```
$ cat types_demo.l3
let x = 42
let greet name = "hello " + name
let result = greet "world"

$ langthree --emit-type types_demo.l3
greet : string -> string
result : string
x : int
```

Polymorphic types show type variables:

```
$ langthree --emit-type --expr 'fun x -> x'
'a -> 'a

$ langthree --emit-type --expr 'fun f -> fun x -> f x'
('a -> 'b) -> 'a -> 'b
```

## Token Emission

Inspect lexer output with `--emit-tokens`:

```
$ langthree --emit-tokens --expr '1 + 2'
NUMBER(1) PLUS NUMBER(2) EOF
```

In file mode, the IndentFilter processes indentation into tokens:

```
$ cat tokens_demo.l3
let result =
    if true then 1
    else 2

$ langthree --emit-tokens tokens_demo.l3
LET IDENT(result) EQUALS NEWLINE(4) IF TRUE THEN NUMBER(1) NEWLINE(4) ELSE NUMBER(2) NEWLINE(0) EOF
```

The `NEWLINE(n)` tokens show the indentation level at each line start.
This is useful for debugging indentation issues -- when a program fails to parse
in file mode, token emission reveals how the IndentFilter interprets your whitespace.

## REPL

Start an interactive session by running `langthree` with no arguments:

```
$ langthree
FunLang REPL
Type '#quit' or Ctrl+D to quit.

funlang> 1 + 2
3
funlang> let x = 5 in x * 2
10
funlang> #quit
```

The REPL evaluates single-line expressions (like `--expr` mode). Use
`let ... in` syntax for bindings. Module-level `let` (without `in`) is not
supported.

Exit with `#quit` or Ctrl+D.

## Diagnostic Modes Summary

| Flag | Description | Expression | File |
|------|-------------|-----------|------|
| *(none)* | Evaluate and print result | N/A | Last binding's value |
| `--expr` | Evaluate expression | Expression result | N/A |
| `--emit-ast` | Show parsed AST | AST of expression | All declarations |
| `--emit-type` | Show inferred types | Type of expression | All binding types |
| `--emit-tokens` | Show lexer tokens | Raw tokens | Tokens with IndentFilter |
| *(no args)* | REPL interactive session | Per-line evaluation | N/A |

Diagnostic flags combine with either `--expr` or a filename:

```
$ langthree --emit-type --expr '1 + 2'
int

$ langthree --emit-type types_demo.l3
greet : string -> string
result : string
x : int
```

## Error Messages

Type errors include error codes, source location, and hints:

```
$ langthree --expr '"hello" + 1'
error[E0301]: Type mismatch: expected string but got int
 --> <expr>:1:6-11
   = hint: Check that all branches of your expression return the same type
```

Parse errors:

```
$ langthree --expr '1 +'
Error: parse error
```

Unbound variable errors:

```
$ langthree --expr 'foo'
error[E0303]: Unbound variable: foo
 --> <expr>:1:0-3
   = hint: Make sure the variable is defined before use
```

## Warnings

LangThree emits warnings for potential issues. Warnings do not prevent
execution -- the program still runs.

### W0001: Incomplete Pattern Match

Emitted when a match expression does not cover all constructors:

```
$ cat incomplete.l3
type Color = Red | Green | Blue
let result =
    match Red with
    | Red -> 1
    | Green -> 2

$ langthree incomplete.l3
Warning: warning[W0001]: Incomplete pattern match. Missing cases: Blue
 --> :0:0-1:0
   = hint: Add the missing cases or a wildcard pattern '_' to cover all values
1
```

### W0002: Redundant Pattern

Emitted when a pattern clause can never be reached:

```
$ cat redundant.l3
type Color = Red | Green | Blue
let result =
    match Red with
    | Red -> 1
    | Green -> 2
    | Blue -> 3
    | Red -> 4

$ langthree redundant.l3
Warning: warning[W0002]: Redundant pattern in clause 4. This case will never be reached.
 --> :0:0-1:0
   = hint: Remove the unreachable pattern
1
```

### W0003: Non-exhaustive Exception Handler

Emitted when a `try ... with` block does not handle all possible exceptions:

```
$ cat handler.l3
exception MyError of string
let result =
    try
        raise (MyError "oops")
    with
    | MyError msg -> msg

$ langthree handler.l3
Warning: warning[W0003]: Non-exhaustive exception handler: not all exceptions are handled; consider adding a catch-all handler
 --> :0:0-1:0
   = hint: Add a catch-all handler or handle all possible exceptions
"oops"
```

Add a catch-all `| _ -> ...` handler to silence this warning.
