# CLAUDE.md

This file provides guidance to Claude Code when working with this F# project.

## Project Overview

**FunLang** is a multi-paradigm functional programming language interpreter written in F#.

- **Language Style**: F#/Scala inspired (functional + imperative)
- **Type System**: Static typing with Hindley-Milner type inference
- **Parser**: FsLexYacc (lexer/parser generator)
- **Development**: TDD with FsCheck property-based testing

See `.claude/PLAN.md` for the detailed implementation plan.

---

## ⚠️ CRITICAL: Development Guidelines

### TDD (Test-Driven Development) - 필수

**모든 기능은 반드시 TDD로 개발:**

```
1. RED   : 테스트 먼저 작성 (실패 확인)
2. GREEN : 최소한의 코드로 테스트 통과
3. REFACTOR : 코드 정리 (테스트 유지)
```

### FsCheck Property-Based Testing - 필수

**단순 예제가 아닌 속성(Property)으로 테스트:**

```fsharp
// ❌ 나쁨: 특정 값만 테스트
[<Fact>]
let ``test addition`` () =
    eval (parse "1 + 2") |> should equal (VInt 3)

// ✅ 좋음: 속성으로 테스트
[<Property>]
let ``addition is commutative`` (a: int) (b: int) =
    eval (parse $"{a} + {b}") = eval (parse $"{b} + {a}")
```

### 개발 워크플로우

```bash
# 1. 테스트 먼저 작성
# tests/FunLang.Tests/*.fs

# 2. 테스트 실행 (실패 확인)
dotnet test

# 3. 구현
# src/FunLang/*.fs

# 4. 테스트 통과 확인
dotnet test

# 5. 커밋 전 필수
dotnet build && dotnet test
```

---

## Build & Run Commands

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the project
dotnet run

# Run tests
dotnet test

# Watch mode (rebuild on file changes)
dotnet watch run

# Publish for production
dotnet publish -c Release
```

## Project Structure

```
├── src/FunLang/
│   ├── Options.fs          # CLI options (Argu)
│   ├── Logging.fs          # Serilog wrapper
│   ├── Errors.fs           # Error types
│   ├── PrettyPrint.fs      # AST/Token formatting
│   ├── Ast.fs              # AST type definitions
│   ├── Indentation.fs      # Indentation handling
│   ├── Parser.fsy          # Parser grammar (FsYacc)
│   ├── Lexer.fsl           # Lexer rules (FsLex)
│   ├── Interpreter.fs      # Evaluator
│   ├── Repl.fs             # Interactive REPL
│   ├── Program.fs          # Entry point
│   └── FunLang.fsproj      # Project file
├── tests/FunLang.Tests/
│   ├── LexerTests.fs       # Lexer property tests
│   ├── ParserTests.fs      # Parser property tests
│   ├── InterpreterTests.fs # Interpreter property tests
│   ├── TypeTests.fs        # Type inference tests
│   └── FunLang.Tests.fsproj
├── .claude/
│   ├── PLAN.md             # Implementation plan
│   └── session/            # Session state
└── CLAUDE.md               # This file
```

**Compilation Order** (F# requires explicit ordering):
`Options.fs` → `Logging.fs` → `Errors.fs` → `PrettyPrint.fs` → `Ast.fs` → `Indentation.fs` → `Parser.fsy` → `Lexer.fsl` → `Interpreter.fs` → `Repl.fs` → `Program.fs`

## F# Coding Conventions

### File Organization
- F# files compile in order listed in .fsproj - order matters
- Place types before functions that use them
- Module structure: types at top, then helper functions, then public API

### Naming Conventions
- **Types/Modules**: PascalCase (`type Customer`, `module Validation`)
- **Functions/Values**: camelCase (`let processOrder`, `let maxRetries`)
- **Parameters**: camelCase (`customerId`, `orderDate`)
- **Discriminated Union Cases**: PascalCase (`| Success | Failure`)

### Idiomatic F# Patterns
- Prefer immutability - use `let` over `let mutable`
- Use pattern matching over if/else chains
- Prefer discriminated unions for domain modeling
- Use `Result<'T, 'Error>` for error handling instead of exceptions
- Use `Option<'T>` for nullable values
- Prefer piping (`|>`) for data transformations
- Use computation expressions for async/result workflows

### Code Style
```fsharp
// Prefer this pattern matching style
let describe value =
    match value with
    | Some x -> sprintf "Has value: %A" x
    | None -> "No value"

// Use pipeline for transformations
let processItems items =
    items
    |> List.filter isValid
    |> List.map transform
    |> List.sortBy (fun x -> x.Priority)

// Prefer Result for error handling
let divide x y =
    if y = 0 then Error "Division by zero"
    else Ok (x / y)
```

## Testing (Expecto + FsCheck 필수)

### Expecto 테스트 프레임워크

**Expecto**를 기본 테스트 프레임워크로 사용, **FsCheck**로 속성 기반 테스트:

```fsharp
open Expecto
open FsCheck

// 단위 테스트
let unitTests = testList "Unit" [
    test "tokenizes integer" {
        let result = tokenize "42"
        Expect.isOk result "should succeed"
    }
]

// Property-Based 테스트 (필수)
let propertyTests = testList "Properties" [
    testProperty "addition is commutative" <| fun (a: int) (b: int) ->
        eval (parse $"{a} + {b}") = eval (parse $"{b} + {a}")

    testProperty "parse-format roundtrip" <| fun (expr: Expr) ->
        parse (format expr) = expr
]

[<Tests>]
let allTests = testList "All" [unitTests; propertyTests]
```

### 테스트 실행

```bash
# 모든 테스트 실행
dotnet run --project tests/FunLang.Tests

# 상세 출력
dotnet run --project tests/FunLang.Tests -- --debug

# 특정 테스트만
dotnet run --project tests/FunLang.Tests -- --filter "Lexer"

# 병렬 실행
dotnet run --project tests/FunLang.Tests -- --parallel
```

### 테스트 파일 구조

```
tests/FunLang.Tests/
├── Program.fs          # Expecto entry point
├── LexerTests.fs       # 토큰화 테스트
├── ParserTests.fs      # 파싱 테스트
├── InterpreterTests.fs # 평가기 테스트
└── TypeTests.fs        # 타입 추론 테스트
```

### 테스트 우선순위

1. **Expecto testProperty** (속성 기반) - 필수
2. **Expecto test { }** (경계 조건, 에러 케이스)
3. **Integration Test** (전체 파이프라인)

## Dependencies

FunLang 프로젝트 의존성:
- **FSharp.Core**: Core F# library
- **FsLexYacc**: Lexer/Parser generator
- **Expecto**: Testing framework (필수)
- **Expecto.FsCheck**: FsCheck integration
- **FsCheck**: Property-based testing
- **Serilog**: Structured logging
- **Argu**: CLI argument parsing

## ⚠️ Error Handling: Exception 금지

### 핵심 원칙

```
❌ 금지: raise, failwith, Exception, try-with (외부 라이브러리 경계 제외)
✅ 필수: Result<'T, Error>, Option<'T>, Result.bind, Option.bind
```

### Result 사용 (에러 전파)

```fsharp
// 모든 공개 함수는 Result 반환
let tokenize (input: string) : Result<Token list, FunLangError> = ...
let parse (tokens: Token list) : Result<Expr, FunLangError> = ...
let eval (env: Env) (expr: Expr) : Result<Value, FunLangError> = ...

// Result 체이닝
let run input =
    tokenize input
    |> Result.bind parse
    |> Result.bind (eval Map.empty)

// result { } CE 사용 (권장)
let run input =
    result {
        let! tokens = tokenize input
        let! ast = parse tokens
        return! eval Map.empty ast
    }
```

### Option 사용 (값 부재)

```fsharp
// 값이 없을 수 있는 경우
let lookupVar env name : Value option =
    Map.tryFind name env

// Option을 Result로 변환 (에러 메시지 필요시)
let lookupVarOrError env name pos : Result<Value, FunLangError> =
    Map.tryFind name env
    |> Option.map Ok
    |> Option.defaultValue (Error (Error.unboundVar name pos))
```

### 에러 타입 정의

```fsharp
type FunLangError = {
    Kind: ErrorKind
    Message: string
    Hint: string option
    Position: Position option
}

// 에러 생성 헬퍼
module Error =
    let lexer char pos = { Kind = LexerError (char, pos); ... }
    let parse token expected pos = { Kind = ParseError ...; ... }
    let unboundVar name pos = { Kind = UnboundVariable ...; ... }
```

### 유일한 예외: 외부 라이브러리 경계

```fsharp
// FsYacc 등 exception 사용하는 라이브러리만 경계에서 변환
let parse tokens : ParseResult =
    try Ok (Parser.parseTokens tokens)
    with :? ParseError as e -> Error (Error.parse e.Token e.Expected e.Position)
```

상세 가이드: `.claude/PLAN.md` Error Handling 섹션 참조

## Common Tasks

### Adding a new file
1. Create the .fs file
2. Add it to the .fsproj in the correct order (files compile top-to-bottom)

### Adding a dependency
```bash
dotnet add package PackageName
```

### Creating a new project
```bash
dotnet new console -lang F# -o src/ProjectName
dotnet new xunit -lang F# -o tests/ProjectName.Tests
dotnet sln add src/ProjectName/ProjectName.fsproj
dotnet sln add tests/ProjectName.Tests/ProjectName.Tests.fsproj
```
