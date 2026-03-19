# LangThree

FunLang v6.0을 기반으로 한 실용적인 ML 스타일 함수형 프로그래밍 언어.

F# 스타일의 들여쓰기 기반 문법, ADT/GADT/Records 타입 시스템, 모듈, 예외 처리, 파이프/합성 연산자, 문자열 내장 함수, printf 포맷 출력, Prelude 표준 라이브러리를 갖춘 완전한 인터프리터.

## Documentation

[LangThree Tutorial](docs/index.html) — 13 chapters, 224 runnable examples

## Features

| Feature | Description | Status |
|---------|-------------|--------|
| **Indentation Syntax** | F# 스타일 들여쓰기 기반 파싱 (offside rule) | v1.0 ✓ |
| **Algebraic Data Types** | Sum types, pattern matching, exhaustiveness checking | v1.0 ✓ |
| **GADT** | Type refinement, bidirectional checking, existential types | v1.0 ✓ |
| **Records** | Named fields, mutable fields, copy-and-update, pattern matching | v1.0 ✓ |
| **Modules** | Namespace, open, qualified names, nested modules | v1.0 ✓ |
| **Exceptions** | try...with, when guards, custom exception types | v1.0 ✓ |
| **Pattern Compilation** | Decision tree compilation (Jules Jacobs algorithm) | v1.0 ✓ |
| **Pipe & Composition** | `\|>`, `>>`, `<<` operators | v1.2 ✓ |
| **Unit Type** | `()` literal, `unit` type, side-effect sequencing | v1.2 ✓ |
| **String Operations** | string_length, string_concat, string_sub, to_string 등 | v1.2 ✓ |
| **Printf Output** | print, println, printf with format specifiers | v1.2 ✓ |
| **Prelude** | Prelude/*.fun directory loading, Option type | v1.2 ✓ |
| **Tutorial** | 13 chapters, 224 CLI-verified examples | v1.3 ✓ |

## Quick Start

```bash
# Build
dotnet build src/LangThree/LangThree.fsproj -c Release

# Expression mode
src/LangThree/bin/Release/net10.0/LangThree --expr '1 + 2 * 3'
# => 7

# File mode
src/LangThree/bin/Release/net10.0/LangThree myfile.lt

# Type inference
src/LangThree/bin/Release/net10.0/LangThree --emit-type --expr 'fun x -> x + 1'
# => int -> int

# REPL
src/LangThree/bin/Release/net10.0/LangThree --repl
```

## Example

```fsharp
// Prelude Option type available automatically
let safeDivide a b =
    if b = 0 then None
    else Some (a / b)

// ADT with pattern matching
type Tree = Leaf | Node of Tree * int * Tree

// Records with mutable fields
type Counter = { mutable count: int }

// Modules
module Math =
    let square x = x * x

open Math

// Pipe operator
let result = 5 |> square |> (fun x -> x + 1)

// Exception handling
exception DivisionByZero of string

let divide x y =
    if y = 0 then raise (DivisionByZero "cannot divide by zero")
    else x / y

let safe = try
    divide 10 0
with
| DivisionByZero msg -> 0

// Printf
let _ = printf "result=%d safe=%d\n" result safe

// String operations
let len = string_length "hello"
let msg = "hello" |> string_length |> to_string
```

## Project Structure

```
LangThree/
├── src/LangThree/       # Interpreter source (23 files, 9,112 LOC F#)
├── tests/
│   ├── LangThree.Tests/ # F# unit tests (196 tests)
│   └── flt/             # fslit integration tests (260 tests)
├── tutorial/            # mdBook tutorial (13 chapters)
├── Prelude/             # Standard library (Option.fun)
├── docs/                # Built tutorial site (GitHub Pages)
└── plans/               # Design documents
```

## Tech Stack

- **F#** (.NET 10) — Implementation language
- **FsLexYacc** — Lexer/parser generator (fslex + fsyacc)
- **Expecto** — Unit test framework
- **fslit** — File-based literate testing
- **mdBook** — Tutorial documentation

## Tests

```bash
# F# unit tests (196)
dotnet test tests/LangThree.Tests/LangThree.Tests.fsproj

# fslit integration tests (260)
/path/to/fslit tests/flt/

# Total: 456 tests
```

## Milestones

| Version | Name | Phases | Plans | Date |
|---------|------|--------|-------|------|
| v1.0 | Core Language | 1-7 | 32 | 2026-03-10 |
| v1.2 | Practical Language Features | 8-12 | 12 | 2026-03-18 |
| v1.3 | Tutorial Documentation | 13-14 | 4 | 2026-03-19 |

## License

MIT
