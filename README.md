# LangThree

FunLang v6.0을 기반으로 한 실용적인 ML 스타일 함수형 프로그래밍 언어.

F# 스타일의 들여쓰기 기반 문법, ADT/GADT/Records 타입 시스템, 모듈, 예외 처리, 파이프/합성 연산자, 문자열 내장 함수, printf 포맷 출력, 사용자 정의 연산자, 다형적 GADT 반환 타입, 파일 임포트(open), char 타입, 파일 I/O 시스템 내장 함수, 가변 배열/해시테이블, 가변 변수(`let mut`/`<-`), Prelude 표준 라이브러리를 갖춘 완전한 인터프리터.

## Documentation

> **GitHub Pages 설정 후 바로 볼 수 있습니다:**
>
> https://ohama.github.io/LangThree/
>
> Settings → Pages → Source: `Deploy from a branch` → Branch: `master` / `/docs` → Save

[LangThree Tutorial](https://ohama.github.io/LangThree/) — 20 chapters, 250+ runnable examples

## Features

| Feature | Description | Version |
|---------|-------------|---------|
| **Indentation Syntax** | F# 스타일 들여쓰기 기반 파싱 (offside rule, implicit `in`) | v1.0-v1.7 |
| **Algebraic Data Types** | Sum types, pattern matching, exhaustiveness checking | v1.0 |
| **GADT** | Type refinement, polymorphic return (`eval : 'a Expr -> 'a`) | v1.0+v1.8 |
| **Records** | Named fields, mutable fields, copy-and-update, pattern matching | v1.0 |
| **Modules** | Namespace, open, qualified names, nested modules | v1.0 |
| **Exceptions** | try...with, when guards, custom exception types | v1.0 |
| **Pattern Compilation** | Decision tree compilation (Jules Jacobs algorithm) | v1.0 |
| **Pipe & Composition** | `\|>`, `>>`, `<<` operators | v1.2 |
| **Unit Type** | `()` literal, `unit` type, side-effect sequencing | v1.2 |
| **String Operations** | string_length, string_concat, string_sub, to_string 등 | v1.2 |
| **Printf Output** | print, println, printf, printfn, sprintf | v1.2+v1.5 |
| **Prelude** | Option, Result, List, Core, Operators (Prelude/*.fun) | v1.2 |
| **Tail Call Optimization** | Trampoline-based TCO | v1.4 |
| **Or-Patterns** | `\| 1 \| 2 \| 3 ->` in match | v1.4 |
| **List Ranges** | `[1..10]`, `[0..2..20]` | v1.4 |
| **Mutual Recursion** | `let rec f = ... and g = ...` | v1.4 |
| **User-Defined Operators** | `let (op)`, INFIXOP0-4, `(op)` function form | v1.5 |
| **Implicit `in`** | F#-style offside rule — `let x = 1 / let y = 2 / x + y` | v1.7 |
| **Semicolon Lists** | F# convention: `[1; 2; 3]` (tuples keep commas) | v1.7 |
| **Polymorphic GADT** | `eval : 'a Expr -> 'a` — per-branch independent type refinement | v1.8 |
| **File Import** | `open "file.fun"` — 순환 임포트 감지 포함 | v2.0 |
| **Char Type** | char 리터럴, char_to_int/int_to_char, 비교 확장 | v2.0 |
| **N-Tuples** | 3개 이상 요소 튜플, 모듈 레벨 구조 분해 | v2.0 |
| **File I/O** | read_file, write_file, get_env, get_args + 10개 추가 내장 함수 | v2.0 |
| **Mutable Array** | Array.create/get/set/length + ofList/toList + iter/map/fold/init | v3.0 |
| **Mutable Hashtable** | Hashtable.create/get/set/containsKey/keys/remove | v3.0 |
| **Mutable Variables** | `let mut x = expr`, `x <- value` (RefValue, closure capture) | v4.0 |

## Quick Start

```bash
# Build
dotnet build src/LangThree/LangThree.fsproj -c Release

# REPL
src/LangThree/bin/Release/net10.0/LangThree

# Expression mode
src/LangThree/bin/Release/net10.0/LangThree --expr '1 + 2 * 3'
# => 7

# File mode
src/LangThree/bin/Release/net10.0/LangThree myfile.l3

# Type inference
src/LangThree/bin/Release/net10.0/LangThree --emit-type --expr 'fun x -> x + 1'
# => int -> int
```

## Example

```fsharp
// Prelude Option type available automatically
let safeDivide a b =
    if b = 0 then None
    else Some (a / b)

// ADT with pattern matching
type Tree =
    | Leaf
    | Node of Tree * int * Tree

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

// User-defined operator
let result = [1; 2] ++ [3; 4]

// Implicit in (offside rule)
let answer =
    let x = 10
    let y = 20
    x + y

// Polymorphic GADT return
type Expr 'a =
    | IntLit : int -> int Expr
    | BoolLit : bool -> bool Expr

let eval e =
    match e with
    | IntLit n -> n
    | BoolLit b -> b

let r1 = eval (IntLit 42)     // r1 : int = 42
let r2 = eval (BoolLit true)  // r2 : bool = true

// Mutable Array
let arr = Array.create 3 0
let _ = Array.set arr 0 10
let _ = Array.set arr 1 20
let _ = Array.set arr 2 30
let sum = Array.fold (fun acc x -> acc + x) 0 arr
// sum = 60

// Mutable Hashtable
let ht = Hashtable.create ()
let _ = Hashtable.set ht "x" 42
let _ = Hashtable.set ht "y" 99
let found = Hashtable.containsKey ht "x"   // true
let keys  = Hashtable.keys ht              // ["x"; "y"]
```

## Algorithms (from Tutorial Ch.15)

```fsharp
// Quicksort with Prelude
let rec qsort xs =
    match xs with
    | [] -> []
    | p :: rest ->
        qsort (filter (fun x -> x < p) rest)
        ++ [p]
        ++ qsort (filter (fun x -> x >= p) rest)

let result = qsort [5; 3; 8; 1; 9; 2; 7]
// => [1; 2; 3; 5; 7; 8; 9]

// BST Tree Sort
type Tree =
    | Leaf
    | Node of Tree * int * Tree

let rec treeInsert x = fun t ->
    match t with
    | Leaf -> Node (Leaf, x, Leaf)
    | Node (l, v, r) ->
        if x <= v then Node (treeInsert x l, v, r)
        else Node (l, v, treeInsert x r)

let rec buildTree xs = match xs with | [] -> Leaf | h :: t -> treeInsert h (buildTree t)
let rec inorder t = match t with | Leaf -> [] | Node (l, v, r) -> append (inorder l) (v :: inorder r)

let result = inorder (buildTree [5; 3; 8; 1; 9; 2; 7])
// => [1; 2; 3; 5; 7; 8; 9]

// Mutual recursion (state machine)
let rec stateA xs = match xs with | [] -> "ended in A" | 0 :: rest -> stateB rest | _ :: rest -> stateA rest
and stateB xs = match xs with | [] -> "ended in B" | 1 :: rest -> stateA rest | _ :: rest -> stateB rest
```

## Project Structure

```
LangThree/
├── src/LangThree/       # Interpreter source (~12,000 LOC F#)
├── tests/
│   ├── LangThree.Tests/ # F# unit tests (224 tests)
│   └── flt/             # fslit integration tests (551 tests)
│       ├── expr/        # Expression-mode tests (16 subdirs)
│       ├── file/        # File-mode tests (21 subdirs)
│       ├── emit/        # AST/type emission tests
│       └── error/       # Error case tests
├── tutorial/            # mdBook tutorial (20 chapters, Korean)
├── Prelude/             # Standard library (Core, List, Option, Result, Array, Hashtable)
├── howto/               # Developer knowledge base (5 documents)
├── docs/                # Built tutorial site (GitHub Pages)
└── .planning/           # GSD project management
```

## Tech Stack

- **F#** (.NET 10) — Implementation language
- **FsLexYacc** — Lexer/parser generator (fslex + fsyacc)
- **Expecto** — Unit test framework
- **fslit** — File-based literate testing
- **mdBook** — Tutorial documentation

## Tests

```bash
# F# unit tests (224)
dotnet test tests/LangThree.Tests/LangThree.Tests.fsproj

# fslit integration tests (551)
/path/to/fslit tests/flt/

# Total: ~775 tests
```

## Milestones

| Version | Name | Phases | Plans | Date |
|---------|------|--------|-------|------|
| v1.0 | Core Language | 1-7 | 32 | 2026-03-10 |
| v1.2 | Practical Language Features | 8-12 | 12 | 2026-03-18 |
| v1.3 | Tutorial Documentation | 13-14 | 4 | 2026-03-19 |
| v1.4 | Language Completion | 15-18 | 6 | 2026-03-20 |
| v1.5 | Operators & Utilities | 19-22 | 4 | 2026-03-20 |
| v1.7 | Offside Rule & List Syntax | 23-24 | 4 | 2026-03-22 |
| v1.8 | Polymorphic GADT | 25 | 5 | 2026-03-23 |
| v2.0 | File Import, Char, N-Tuples, File I/O | 26-32 | 14 | 2026-03-24 |
| v2.1 | Tutorial Ch.17-18 | 33 | 2 | 2026-03-24 |
| v2.2 | AbstractGrammar, SPEC 정리 | 34 | 2 | 2026-03-24 |
| v3.0 | Mutable Array & Hashtable | 38-41 | 6 | 2026-03-25 |
| v4.0 | Mutable Variables (`let mut`) | 42-44 | 5 | 2026-03-27 |

**Total:** 44 phases, 103 plans across 13 milestones

## Reference Documents

| Document | Description |
|----------|-------------|
| [SPEC.md](SPEC.md) | Language specification — tokens, BNF grammar, operator precedence, type system |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Pipeline design — Lexer → IndentFilter → Parser → TypeCheck → Eval |
| [TESTS.md](TESTS.md) | Test cases — tokenization, parsing, indentation edge cases, runtime |
| [AbstractGrammar.md](AbstractGrammar.md) | Abstract grammar — AST node definitions, expression and declaration forms |

## License

MIT
