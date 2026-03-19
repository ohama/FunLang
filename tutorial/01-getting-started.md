# Chapter 1: Getting Started

LangThree is a statically-typed functional language with F#-style indentation syntax,
algebraic data types (including GADTs), pattern matching with decision tree compilation,
and a module system.

## Running LangThree

**Expression mode** evaluates a single expression:

```
$ langthree --expr '1 + 2'
3
```

**File mode** evaluates a program file. The last binding's value is printed:

```
$ cat hello.l3
let greeting = "hello"
let result = greeting + " world"

$ langthree hello.l3
"hello world"
```

**Diagnostic modes** inspect compilation without evaluating:

```
$ langthree --emit-ast --expr '1 + 2'
Add (Number 1, Number 2)

$ langthree --emit-type --expr '1 + 2'
int
```

**REPL** starts an interactive session:

```
$ langthree --repl
```

## Integers and Arithmetic

Standard arithmetic operators with usual precedence. Division is integer division.

```
$ langthree --expr '1 + 2 * 3'
7

$ langthree --expr '10 - 3'
7

$ langthree --expr '10 / 3'
3

$ langthree --expr '-5'
-5
```

## Booleans

`true` and `false` with short-circuit `&&` and `||`.

```
$ langthree --expr 'true && false'
false

$ langthree --expr 'true || false'
true
```

Short-circuit evaluation means the right operand is not evaluated when unnecessary:

```
$ langthree --expr 'false && (1/0 = 0)'
false

$ langthree --expr 'true || (1/0 = 0)'
true
```

There is no `not` function. Negate booleans with `if`:

```
$ langthree --expr 'if true then false else true'
false
```

## Strings

String literals use double quotes with standard escape sequences (`\n`, `\t`, `\\`, `\"`).
Concatenation uses the `+` operator.

```
$ langthree --expr '"hello" + " world"'
"hello world"

$ langthree --expr '"line1\nline2"'
"line1
line2"
```

Built-in string functions:

```
$ langthree --expr 'string_length "hello"'
5

$ langthree --expr 'string_sub "hello" 1 3'
"ell"

$ langthree --expr 'to_string 42'
"42"
```

## Comparison Operators

Equality is `=` (not `==`). Inequality is `<>`.

```
$ langthree --expr '1 = 1'
true

$ langthree --expr '1 <> 2'
true

$ langthree --expr '3 < 5'
true

$ langthree --expr '3 >= 3'
true
```

## Conditionals

```
$ langthree --expr 'if 1 < 2 then "yes" else "no"'
"yes"
```

Both branches must have the same type. The `else` branch is required.

## Comments

Line comments with `//` and block comments with `(* ... *)`:

```
$ langthree --expr '1 + 2 // this is ignored'
3

$ langthree --expr '(* block comment *) 1 + 2'
3
```

## Unit Type

The unit type `()` represents "no meaningful value", used for side effects:

```
$ langthree --expr '()'
()

$ langthree --expr 'println "hello"'
hello
()
```

## Syntax Notes

- **Indentation-based** -- no semicolons or braces for blocks (F#-style)
- **File mode**: `let` bindings at top level, no `in` needed; last binding's value is printed
- **Expression mode**: use `let x = ... in body` for local bindings
- `match` pipes must align with the `match` keyword column
