# FunLang

A statically-typed functional programming language with Hindley-Milner type inference.

## Features

### Functional Programming
- **First-class functions**: Lambda expressions, closures, higher-order functions
- **Pattern matching**: Destructure data with `match` expressions
- **Algebraic data types**: Define your own types with discriminated unions
- **Immutable by default**: All bindings are immutable

### Indentation-Based Syntax

Like Python and Haskell, FunLang uses **indentation to define code blocks** - no braces, no semicolons.

**Function with if/else:**
```funlang
let rec factorial = fun n ->
  if n = 0 then 1
  else n * factorial (n - 1)

factorial 5  // => 120
```

**Pattern matching:**
```funlang
let rec sum = fun xs ->
  match xs with
  | [] -> 0
  | h :: t -> h + sum t

sum [1; 2; 3]  // => 6
```

**Nested let bindings:**
```funlang
let result =
  let x = 10
  let y = 20
  x + y

result  // => 30
```

**Multi-line type definitions:**
```funlang
type List 'a =
  | Nil
  | Cons of 'a * List 'a
```

### Static Type Inference

Hindley-Milner type inference (Algorithm W) - you rarely need type annotations:

```funlang
let double = fun x -> x * 2           // int -> int
let id = fun x -> x                   // 'a -> 'a (polymorphic)
```

### Rust-Style Error Messages

Clear, helpful error messages with source location and hints:

**Lexer error:**
```
error[E001]: Unexpected character: @
  --> :1:1
  |
1 | @bad
  | ^
   = info: character not recognized by lexer
```

**Parser error:**
```
Parse error at line 1, column 11: unexpected 'ELSE', expected 'then'
```

**Runtime errors:**
```
error[E303]: Division by zero
   = info: operation not supported
```

```
error[E303]: Cannot add int and bool
   = info: operation not supported
```

```
error[E303]: No pattern matched
   = info: operation not supported
```

## Quick Start

### Requirements
- .NET 9.0 or later

### Build & Run

```bash
dotnet build                                          # Build
dotnet run --project src/FunLang -- myprogram.fun     # Run file
dotnet run --project src/FunLang -- -e "1 + 2 * 3"    # Expression
dotnet run --project src/FunLang -- -i                # REPL
```

## Language at a Glance

```funlang
// Literals
42                      // int
true                    // bool
"hello"                 // string
(1, "a", true)          // tuple
[1; 2; 3]               // list

// Functions
let double = fun x -> x * 2
let add = fun x -> fun y -> x + y

// Recursion
let rec fib = fun n ->
  if n <= 1 then n
  else fib (n - 1) + fib (n - 2)

// Pattern matching
let rec length = fun xs ->
  match xs with
  | [] -> 0
  | _ :: t -> 1 + length t

// User-defined types
type Option 'a = None | Some of 'a
type Tree 'a = Leaf of 'a | Node of Tree 'a * Tree 'a
```

## CLI Options

```bash
funlang <file>              # Run a file
funlang -e "<expr>"         # Evaluate expression
funlang -i                  # Interactive REPL
funlang --show-tokens       # Display lexer tokens
funlang --show-ast          # Display parsed AST
funlang --show-types        # Display inferred types
funlang -d                  # Full debug mode
```

## Learning More

### Grammar & Syntax
- `docs/grammar.md` - Language grammar documentation
- `docs/funlang.ebnf` - Formal EBNF grammar

### Examples
The `tests/file-tests/` directory contains many examples:

| Directory | Description |
|-----------|-------------|
| `eval-tests/` | Evaluation examples (17 tests) |
| `parse-tests/` | Parsing examples |
| `error-tests/` | Error message examples (26 tests) |
| `integrated-tests/` | Complex programs |

**Notable examples:**
- `eval-tests/006-fibonacci.test` - Fibonacci with recursion
- `eval-tests/016-list-map.test` - Custom List type with map
- `eval-tests/017-tree-type.test` - Binary tree operations

## License

MIT
