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

### Pattern Matching Analysis

FunLang analyzes pattern matches for **exhaustiveness** and **redundancy**, helping you write safer code.

**Non-exhaustive pattern warning:**
```funlang
type Option 'a = None | Some of 'a

let getValue = fun opt ->
  match opt with
  | Some x -> x
```
```
Warning: Non-exhaustive pattern match at line 4, column 3
  Missing case(s): None
```

**Redundant pattern warning:**
```funlang
let test = fun x ->
  match x with
  | true -> 1
  | false -> 2
  | true -> 3   // This pattern is never reached
```
```
Warning: Pattern 3 at line 5, column 5 is redundant (never matches)
```

**Writing exhaustive patterns:**
```funlang
// Option 1: Cover all cases explicitly
let getValue = fun opt ->
  match opt with
  | Some x -> x
  | None -> 0

// Option 2: Use wildcard as fallback
let head = fun xs ->
  match xs with
  | h :: _ -> h
  | _ -> 0          // Covers empty list
```

**Exhaustive bool matching:**
```funlang
// Use true and false explicitly (not wildcard)
let describe = fun b ->
  match b with
  | true -> "yes"
  | false -> "no"
```

### Module System

Organize code into **modules** with explicit exports and qualified access:

```funlang
module Math =
  export add, multiply

  let add = fun x -> fun y -> x + y
  let multiply = fun x -> fun y -> x * y

module Utils =
  export double

  let double = fun x -> x * 2

// Use qualified names to access module functions
Math.multiply (Math.add 2 3) (Utils.double 4)  // => 40
```

**Recursive functions in modules:**
```funlang
module Math =
  export factorial

  let rec factorial = fun n ->
    if n <= 1 then 1
    else n * factorial (n - 1)

Math.factorial 5  // => 120
```

**Module syntax:**
- `module Name = ...` - Define a module
- `export fn1, fn2` - Declare exported functions
- `Module.function` - Access module members

See `docs/module-system-design.md` for the full design specification.

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
funlang --emit              # Output formatted source to stdout
funlang --emit output.fun   # Output formatted source to file
funlang --show-tokens       # Display lexer tokens
funlang --show-ast          # Display parsed AST
funlang --show-types        # Display inferred types
funlang -d                  # Full debug mode
```

### Source Formatting (`--emit`)

The `--emit` option parses your source code and outputs it in a normalized format, **preserving comments**.

```bash
# Format to stdout
dotnet run --project src/FunLang -- myprogram.fun --emit

# Format to file
dotnet run --project src/FunLang -- myprogram.fun --emit formatted.fun
```

**Comment preservation:**
```funlang
// Input
// Calculate factorial
let rec fact = fun n ->
  if n = 0 then 1
  else n * fact (n - 1)  // recursive case

fact 5

// Output (--emit) - comments preserved
// Calculate factorial
let rec fact = fun n ->
  if n = 0 then 1
  else n * fact (n - 1)  // recursive case

fact 5
```

See `docs/emit-algorithm.md` for implementation details.

## Learning More

### Grammar & Syntax
- `docs/grammar.md` - Language grammar documentation
- `docs/funlang.ebnf` - Formal EBNF grammar

### Internals
- `docs/emit-algorithm.md` - Source formatting (`--emit`) algorithm
- `docs/indentation.md` - Indentation-based parsing
- `docs/TYPE_SYSTEM_ALGORITHM.md` - Type inference (Algorithm W)
- `docs/module-system-design.md` - Module system design specification

### Examples
The `tests/file-tests/` directory contains many examples:

| Directory | Description |
|-----------|-------------|
| `eval-tests/` | Evaluation examples |
| `parse-tests/` | Parsing examples |
| `error-tests/` | Error message examples |
| `warning-tests/` | Pattern matching warnings |
| `format-tests/` | Source formatting (`--emit`) tests |
| `integrated-tests/` | Complex programs (sorting algorithms) |

**Notable examples:**
- `eval-tests/006-fibonacci.test` - Fibonacci with recursion
- `eval-tests/016-list-map.test` - Custom List type with map
- `eval-tests/017-tree-type.test` - Binary tree operations

## Development

### Version Management

Use `upgrade_version.sh` to release new versions:

```bash
./upgrade_version.sh [major|minor|patch] [--push]

# Examples
./upgrade_version.sh patch          # 0.3.0 -> 0.3.1 (local only)
./upgrade_version.sh minor          # 0.3.0 -> 0.4.0 (local only)
./upgrade_version.sh major          # 0.3.0 -> 1.0.0 (local only)
./upgrade_version.sh minor --push   # 0.3.0 -> 0.4.0 + git push
```

The script automatically:
1. Bumps version in `VERSION` file
2. Generates changelog from commits (categorizes as Added/Changed/Fixed/Removed)
3. Updates `CHANGELOG.md`
4. Creates git commit and tag (`vX.Y.Z`)
5. Pushes to remote (with `--push` flag)

### Running Tests

```bash
dotnet test                                           # All tests
dotnet run --project tests/FunLang.Tests              # With Expecto
dotnet run --project tests/FunLang.Tests -- --filter-test-list "File-Based"
```

## License

MIT
