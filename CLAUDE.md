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

### Session & Context Management - 필수

**세션 관리 명령어:**
```
/startsession    # 세션 시작 (컨텍스트 복원)
/endsession      # 세션 종료 (컨텍스트 저장)
```

**컨텍스트 리셋 워크플로우:**
```
/endsession          ← 컨텍스트를 파일에 저장
    ↓
[새 대화 시작]        ← AI 컨텍스트 리셋됨
    ↓
/startsession        ← HISTORY.md에서 컨텍스트 복원
```

**컨텍스트 저장 위치:**
- `.claude/HISTORY.md` - 세션 히스토리 & 축적된 지식
- `.claude/session/state.json` - 현재 세션 상태
- `.claude/PLAN.md` - 구현 계획 & 현재 phase

**HISTORY.md 세션 엔트리 형식:**
```markdown
### YYYY-MM-DD (Session: id)

**주요 변경 사항:**
- 완료한 작업, 구현한 기능

**시도한 실험:**
- 시도한 접근법, 실패한 방법

**배운 점:**
- 인사이트, 발견한 패턴

**Key Decisions:**
- 중요한 결정

**Unresolved Issues:**
- 미해결 이슈
```

⚠️ **중요**:
- AI 컨텍스트는 대화 단위로 관리됨
- 새 대화 시작 = 컨텍스트 리셋
- `/startsession`이 HISTORY.md에서 필요한 컨텍스트 복원

### Issue Tracking - 필수

**빌드/테스트 실패 시 반드시 이슈 기록:**

```
빌드 실패 → /issue add "빌드 에러: [에러 내용]"
테스트 실패 → /issue add "테스트 실패: [테스트명]"
해결 시 → /issue resolve <id>
```

⚠️ **중요**: 이슈를 기록하지 않으면 같은 문제를 반복할 수 있습니다!

**상세 가이드:** `.claude/ISSUES.md` 참조

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
│   ├── HISTORY.md          # Session history & accumulated knowledge
│   └── session/            # Session state (state.json)
├── docs/
│   ├── issues/             # Issue tracking (unresolved/, resolved/)
│   └── prompt/             # Session prompt logs
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

---

## ⚠️ FsLexYacc 설정 원칙

### 필수 설정 (.fsproj)

```xml
<!-- FsYacc: --module 플래그로 모듈명 지정 (OtherFlags 사용 필수) -->
<FsYacc Include="Parser.fsy">
  <OtherFlags>--module FunLang.GeneratedParser</OtherFlags>
</FsYacc>

<!-- FsLex: --unicode 플래그 필수 (char-based lexing) -->
<FsLex Include="Lexer.fsl">
  <OtherFlags>--module FunLang.GeneratedLexer --unicode</OtherFlags>
</FsLex>
```

### 컴파일 순서 (중요!)

```xml
<!-- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함 -->
<!-- (Lexer가 Parser의 token 타입 사용) -->
<ItemGroup>
  <Compile Include="Parser.fs" />   <!-- 1. Parser 먼저 -->
  <Compile Include="Lexer.fs" />    <!-- 2. Lexer 나중 -->
  ...
</ItemGroup>
```

### Lexer.fsl 헤더 규칙

```fsl
{
// ❌ 금지: --module 플래그 사용시 헤더에 module 선언 금지
// module FunLang.GeneratedLexer  <- 이거 쓰면 안됨!

// ✅ 필수: open 문만 사용
open FSharp.Text.Lexing
open FunLang.GeneratedParser
...
}
```

### 생성 파일 (.gitignore 필수)

```gitignore
# FsLexYacc 자동 생성 파일 - 절대 commit 금지
src/FunLang/Parser.fs
src/FunLang/Lexer.fs
*.fsi
```

상세 이슈: `docs/build-issues.md` 참조

---

## ⚠️ FsCheck 테스트 주의사항

### 음수 테스트 금지

```fsharp
// ❌ 문제: 음수는 MINUS INT로 파싱됨
// "(fun x -> x) -1" → "(fun x -> x) - 1" (뺄셈으로 해석)
testProperty "identity" <| fun (n: int) ->  // -1 입력시 실패!

// ✅ 해결: NonNegativeInt 사용
testProperty "identity" <| fun (n: NonNegativeInt) ->
    let input = $"(fun x -> x) {n.Get}"
    ...
```

### Null 입력 처리

```fsharp
// FsCheck가 null 문자열 생성할 수 있음 - 반드시 처리
let tokenize (input: string) =
    if isNull input then Error (Error.lexerMsg "null input" pos)
    else ...
```

상세 이슈: `docs/build-issues.md` 참조

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
