# FunLang

Multi-paradigm 함수형 프로그래밍 언어 인터프리터

## Overview

- **Language Style**: F#/Scala 스타일 (함수형 + 일부 명령형)
- **Type System**: 정적 타입 + Hindley-Milner 타입 추론 (Algorithm W)
- **Parser**: FsLexYacc (lexer/parser generator)
- **Testing**: Expecto + FsCheck (property-based testing)
- **Logging**: Serilog (structured logging)
- **CLI**: Argu (command-line argument parsing)

## 요구사항

- .NET 9.0 이상

## 빌드 및 실행

```bash
# 의존성 복원
dotnet restore

# 빌드
dotnet build

# 테스트 실행 (필수!) - Expecto
dotnet run --project tests/FunLang.Tests

# 실행
dotnet run --project src/FunLang

# REPL 모드
dotnet run --project src/FunLang -- --interactive

# 표현식 직접 실행
dotnet run --project src/FunLang -- -e "1 + 2 * 3"
```

## CLI 옵션

```bash
funlang <file>              # 파일 실행
funlang -e "<expr>"         # 표현식 실행
funlang -i, --interactive   # REPL 모드
funlang --show-tokens       # 토큰 출력
funlang --show-ast          # AST 출력
funlang --show-types        # 타입 추론 결과 출력
funlang --show-indents      # 들여쓰기 토큰 출력
funlang -v                  # 상세 출력
funlang -d                  # 디버그 모드
funlang --log-level <level> # 로그 레벨 설정
funlang --trace <phase>     # 특정 단계 추적 (lexer, parser, typecheck, eval)
```

## 프로젝트 구조

```
├── src/FunLang/
│   ├── Options.fs          # CLI 옵션 (Argu)
│   ├── Logging.fs          # Serilog 로깅
│   ├── Errors.fs           # 에러 타입 정의
│   ├── PrettyPrint.fs      # AST/Token 출력
│   ├── Ast.fs              # AST 타입 정의
│   ├── Indentation.fs      # 들여쓰기 처리
│   ├── Parser.fsy          # FsYacc 문법 정의
│   ├── Lexer.fsl           # FsLex 렉서 정의
│   ├── Interpreter.fs      # 인터프리터
│   ├── Types.fs            # 타입 시스템
│   ├── TypeInference.fs    # Hindley-Milner 타입 추론
│   ├── Repl.fs             # Interactive REPL
│   └── Program.fs          # 진입점
├── tests/FunLang.Tests/
│   ├── LexerTests.fs       # Lexer 속성 테스트
│   ├── ParserTests.fs      # Parser 속성 테스트
│   ├── InterpreterTests.fs # 인터프리터 속성 테스트
│   └── TypeTests.fs        # 타입 추론 테스트
├── .claude/
│   └── PLAN.md             # 상세 구현 계획
├── CLAUDE.md               # 개발 가이드라인
└── README.md               # 이 파일
```

## 컴파일 순서

F#은 파일 순서가 중요합니다:

```
Options.fs → Logging.fs → Errors.fs → PrettyPrint.fs → Ast.fs →
Indentation.fs → Parser.fsy → Lexer.fsl → Interpreter.fs →
Types.fs → TypeInference.fs → Repl.fs → Program.fs
```

## 개발 가이드

### TDD (Test-Driven Development) 필수

```
1. RED   : 테스트 먼저 작성 (실패 확인)
2. GREEN : 최소한의 코드로 테스트 통과
3. REFACTOR : 코드 정리
```

### Expecto + FsCheck 테스트 필수

**Expecto**를 테스트 프레임워크로, **FsCheck**로 속성 기반 테스트:

```fsharp
open Expecto
open FsCheck

// 단위 테스트
test "tokenizes integer" {
    let result = tokenize "42"
    Expect.isOk result "should succeed"
}

// 속성 기반 테스트 (필수)
testProperty "addition is commutative" <| fun (a: int) (b: int) ->
    eval (parse $"{a} + {b}") = eval (parse $"{b} + {a}")
```

### 테스트 실행

```bash
# 모든 테스트
dotnet run --project tests/FunLang.Tests

# 상세 출력
dotnet run --project tests/FunLang.Tests -- --debug

# 특정 테스트만
dotnet run --project tests/FunLang.Tests -- --filter "Lexer"
```

## 언어 기능 (예정)

- **리터럴**: 정수, 불리언, 문자열
- **연산자**: 산술, 비교, 논리
- **바인딩**: `let`, `let rec`
- **함수**: 람다, 일급 함수, 클로저
- **제어문**: `if`/`then`/`else`
- **데이터 구조**: 튜플, 리스트
- **패턴 매칭**: `match`/`with`
- **타입 추론**: Hindley-Milner (Algorithm W)
- **사용자 정의 타입**: Discriminated Union
- **들여쓰기 기반 구문**: Offside rule

## 예시 코드

```funlang
// let 바인딩
let x = 42

// 함수 정의
let double n = n * 2

// 재귀 함수
let rec factorial n =
    if n = 0 then 1
    else n * factorial (n - 1)

// 패턴 매칭
let describe xs =
    match xs with
    | [] -> "empty"
    | [x] -> "single"
    | _ -> "multiple"

// 고차 함수
let apply f x = f x
```

## 상세 계획

자세한 구현 계획은 `.claude/PLAN.md`를 참조하세요.
