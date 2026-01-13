# FunLang Interpreter Implementation Plan

## Overview

**Goal:** Multi-paradigm 함수형 언어 인터프리터 (F#/Scala 스타일)
**Type System:** 정적 타입 + Hindley-Milner 타입 추론
**Parser:** FsLexYacc (이미 프로젝트에 설정됨)
**Testing:** Expecto + FsCheck (property-based testing)
**Logging:** Serilog

---

## ⚠️ Development Guidelines (중요 지침)

### 1. TDD (Test-Driven Development) 필수

모든 기능 구현은 **반드시 TDD 방식**으로 진행:

```
1. RED   : 실패하는 테스트 먼저 작성
2. GREEN : 테스트를 통과하는 최소한의 코드 작성
3. REFACTOR : 코드 정리 (테스트는 계속 통과해야 함)
```

**TDD 워크플로우 (Expecto):**
```bash
# 1. 테스트 작성
# tests/FunLang.Tests/LexerTests.fs 에 새 테스트 추가

# 2. 테스트 실행 (실패 확인)
dotnet run --project tests/FunLang.Tests

# 3. 구현
# src/FunLang/Lexer.fsl 수정

# 4. 테스트 실행 (통과 확인)
dotnet run --project tests/FunLang.Tests

# 5. 리팩토링 후 테스트 재실행
dotnet run --project tests/FunLang.Tests
```

### 2. Expecto + FsCheck Property-Based Testing 필수

**Expecto**를 기본 테스트 프레임워크로, **FsCheck**로 속성 기반 테스트:

```fsharp
open Expecto
open FsCheck

// ❌ 나쁜 예: 하드코딩된 값
test "addition works" {
    let result = eval (parse "1 + 2")
    Expect.equal result (Ok (VInt 3)) "should be 3"
}

// ✅ 좋은 예: 속성으로 테스트 (모든 정수에 대해)
testProperty "addition is commutative" <| fun (a: int) (b: int) ->
    eval (parse $"{a} + {b}") = eval (parse $"{b} + {a}")

testProperty "addition is associative" <| fun (a: int) (b: int) (c: int) ->
    eval (parse $"({a} + {b}) + {c}") = eval (parse $"{a} + ({b} + {c})")
```

**테스트해야 할 속성 유형:**
- **대수적 속성**: 교환법칙, 결합법칙, 항등원, 역원
- **라운드트립**: `parse → format → parse = parse`
- **불변성**: 평가 전후 환경 상태
- **타입 안전성**: 잘 타입된 표현식은 런타임 에러 없음

### 3. 테스트 우선 순위

```
1순위: Expecto + FsCheck Property 테스트 (속성 기반)
2순위: Expecto test { } 단위 테스트 (경계 조건, 에러 케이스)
3순위: 통합 테스트 (전체 파이프라인)
```

### 4. Expecto 테스트 구조

```fsharp
// 테스트 파일 구조
module FunLang.Tests.LexerTests

open Expecto

let unitTests = testList "Unit Tests" [
    test "tokenizes keyword" { ... }
    test "handles error" { ... }
]

let propertyTests = testList "Property Tests" [
    testProperty "roundtrip" <| fun input -> ...
    testProperty "deterministic" <| fun input -> ...
]

[<Tests>]
let tests = testList "Lexer" [unitTests; propertyTests]
```

### 5. 각 Phase별 테스트 체크리스트

새 기능 구현 시 다음을 확인:
- [ ] Expecto testProperty 테스트 작성됨
- [ ] 경계 조건 test { } 작성됨
- [ ] 에러 케이스 테스트 작성됨
- [ ] 모든 테스트 통과 (`dotnet run --project tests/FunLang.Tests`)
- [ ] 코드 커버리지 확인

### 6. 커밋 전 필수 확인

```bash
# 모든 테스트 통과 확인
dotnet run --project tests/FunLang.Tests

# 빌드 성공 확인
dotnet build

# 상세 테스트 출력
dotnet run --project tests/FunLang.Tests -- --debug

# 특정 테스트만 실행
dotnet run --project tests/FunLang.Tests -- --filter "Lexer"
```

---

## Phase 0: Infrastructure Setup

### 목표
로깅, CLI 옵션, 테스트 인프라 구축

### 패키지 추가
```bash
dotnet add src/FunLang package Serilog
dotnet add src/FunLang package Serilog.Sinks.Console
dotnet add src/FunLang package Serilog.Sinks.File
dotnet add src/FunLang package Argu  # CLI argument parsing
```

### CLI Options

**새 파일: Options.fs**
```fsharp
module FunLang.Options

open Argu

type Phase =
    | Lexer
    | Parser
    | TypeCheck
    | Eval

type CliArgs =
    | [<MainCommand>] File of path: string
    | [<AltCommandLine("-e"); AltCommandLine("--expr")>] Expression of code: string
    | [<AltCommandLine("-i"); AltCommandLine("--interactive")>] Interactive
    | [<AltCommandLine("-v")>] Verbose
    | [<AltCommandLine("-d")>] Debug
    | [<AltCommandLine("--log-level")>] LogLevel of level: string
    | [<AltCommandLine("--log-file")>] LogFile of path: string
    | [<AltCommandLine("--show-tokens")>] ShowTokens
    | [<AltCommandLine("--show-ast")>] ShowAst
    | [<AltCommandLine("--show-types")>] ShowTypes
    | [<AltCommandLine("--show-indents")>] ShowIndents
    | [<AltCommandLine("--trace")>] Trace of Phase list
    | [<AltCommandLine("--no-color")>] NoColor
    | [<AltCommandLine("--no-prelude")>] NoPrelude
    | Version
    | Help
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | File _ -> "FunLang source file to execute"
            | Expression _ -> "Execute expression directly (e.g., -e '1 + 2')"
            | Interactive -> "Start interactive REPL mode"
            | Verbose -> "Enable verbose output"
            | Debug -> "Enable debug mode (all phases)"
            | LogLevel _ -> "Set log level (debug|info|warning|error)"
            | LogFile _ -> "Write logs to file"
            | ShowTokens -> "Display lexer tokens"
            | ShowAst -> "Display parsed AST"
            | ShowTypes -> "Display inferred types"
            | ShowIndents -> "Display indentation tokens"
            | Trace _ -> "Trace specific phases (lexer,parser,typecheck,eval)"
            | NoColor -> "Disable colored output"
            | NoPrelude -> "Don't load standard prelude"
            | Version -> "Show version"
            | Help -> "Show help"

type RunOptions = {
    Verbose: bool
    Debug: bool
    Interactive: bool
    LogLevel: Serilog.Events.LogEventLevel
    LogFile: string option
    ShowTokens: bool
    ShowAst: bool
    ShowTypes: bool
    ShowIndents: bool
    TracePhases: Set<Phase>
    NoColor: bool
    NoPrelude: bool
}

let defaultOptions = {
    Verbose = false
    Debug = false
    Interactive = false
    LogLevel = Serilog.Events.LogEventLevel.Information
    LogFile = None
    ShowTokens = false
    ShowAst = false
    ShowTypes = false
    ShowIndents = false
    TracePhases = Set.empty
    NoColor = false
    NoPrelude = false
}
```

### CLI 사용 예시
```bash
# 기본 실행
funlang program.fun

# 표현식 직접 실행
funlang -e "1 + 2 * 3"
funlang --expr "let x = 10 in x * 2"

# Interactive REPL 모드
funlang -i
funlang --interactive
funlang -i --show-types   # REPL에서 타입도 표시

# 토큰 출력
funlang --show-tokens program.fun

# AST 출력
funlang --show-ast program.fun

# 들여쓰기 토큰 출력
funlang --show-indents program.fun

# 디버그 모드 (모든 phase 추적)
funlang -d program.fun

# 특정 phase만 추적
funlang --trace lexer --trace parser program.fun

# 로그 레벨 설정
funlang --log-level debug --log-file debug.log program.fun

# 조합
funlang -v --show-tokens --show-ast -e "let x = 1 in x + 1"

# Prelude 없이 실행
funlang --no-prelude program.fun
```

### Interactive REPL Mode

**새 파일: Repl.fs**
```fsharp
module FunLang.Repl

open System
open FunLang.Ast
open FunLang.Interpreter
open FunLang.TypeInference

type ReplState = {
    Environment: Env
    TypeContext: TypeContext
    History: string list
    Counter: int
}

let initialState = {
    Environment = Map.empty
    TypeContext = Map.empty
    History = []
    Counter = 1
}

let prompt state = sprintf "fun[%d]> " state.Counter

let printResult (opts: RunOptions) (value: Value) (inferredType: Type option) =
    match inferredType with
    | Some t when opts.ShowTypes ->
        printfn "val it : %s = %A" (formatType t) value
    | _ ->
        printfn "%A" value

let rec replLoop (opts: RunOptions) (state: ReplState) =
    printf "%s" (prompt state)
    match Console.ReadLine() with
    | null | ":quit" | ":q" ->
        printfn "Goodbye!"
    | ":help" | ":h" ->
        printHelp ()
        replLoop opts state
    | ":env" ->
        printEnv state.Environment
        replLoop opts state
    | ":type" | ":t" ->
        printf "expr> "
        let expr = Console.ReadLine()
        showType opts state expr
        replLoop opts state
    | ":clear" ->
        replLoop opts initialState
    | ":history" ->
        state.History |> List.rev |> List.iteri (fun i h -> printfn "[%d] %s" (i+1) h)
        replLoop opts state
    | input when input.StartsWith(":load ") ->
        let file = input.Substring(6).Trim()
        let newState = loadFile opts state file
        replLoop opts newState
    | input ->
        let newState = evalInput opts state input
        replLoop opts newState

and evalInput (opts: RunOptions) (state: ReplState) (input: string) =
    try
        let ast = parse input

        if opts.ShowTokens then printTokens (tokenize input)
        if opts.ShowAst then printAst ast

        let inferredType =
            if opts.ShowTypes then
                Some (infer state.TypeContext ast |> fst)
            else None

        let value = eval state.Environment ast
        printResult opts value inferredType

        // let 바인딩이면 환경에 추가
        match ast with
        | ELet (name, _, _) ->
            { state with
                Environment = Map.add name value state.Environment
                History = input :: state.History
                Counter = state.Counter + 1 }
        | _ ->
            { state with
                History = input :: state.History
                Counter = state.Counter + 1 }
    with
    | e ->
        printfn "Error: %s" e.Message
        state

let printHelp () =
    printfn """
FunLang REPL Commands:
  :help, :h       Show this help
  :quit, :q       Exit REPL
  :env            Show current environment
  :type, :t       Show type of expression
  :clear          Clear environment
  :history        Show command history
  :load <file>    Load and execute file
"""

let startRepl (opts: RunOptions) =
    printfn "FunLang Interactive Mode (v0.1.0)"
    printfn "Type :help for commands, :quit to exit"
    printfn ""
    replLoop opts initialState
```

### REPL 세션 예시
```
$ funlang -i --show-types

FunLang Interactive Mode (v0.1.0)
Type :help for commands, :quit to exit

fun[1]> let x = 42
val it : int = 42

fun[2]> let double n = n * 2
val it : int -> int = <function>

fun[3]> double x
val it : int = 84

fun[4]> let rec factorial n =
    ..>     if n = 0 then 1
    ..>     else n * factorial (n - 1)
val it : int -> int = <function>

fun[5]> factorial 5
val it : int = 120

fun[6]> :type factorial
int -> int

fun[7]> :env
x : int = 42
double : int -> int = <function>
factorial : int -> int = <function>

fun[8]> :quit
Goodbye!
```

### 새 파일: Logging.fs
```fsharp
module FunLang.Logging

open Serilog
open Serilog.Events

type Phase =
    | Lexer
    | Parser
    | TypeCheck
    | Eval
    | Runtime

let phaseToString = function
    | Lexer -> "LEXER"
    | Parser -> "PARSER"
    | TypeCheck -> "TYPECHECK"
    | Eval -> "EVAL"
    | Runtime -> "RUNTIME"

let mutable private logger : ILogger option = None
let mutable private options : RunOptions option = None

let initialize (opts: RunOptions) =
    options <- Some opts
    let config =
        LoggerConfiguration()
            .MinimumLevel.Is(opts.LogLevel)
            .Enrich.WithProperty("Application", "FunLang")

    let config =
        if opts.NoColor then
            config.WriteTo.Console(outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [{Phase}] {Message:lj}{NewLine}{Exception}")
        else
            config.WriteTo.Console(
                outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [{Phase}] {Message:lj}{NewLine}{Exception}",
                theme = Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)

    let config =
        match opts.LogFile with
        | Some path -> config.WriteTo.File(path, rollingInterval = RollingInterval.Day)
        | None -> config

    logger <- Some (config.CreateLogger())

let private log level phase msg =
    match logger with
    | Some l -> l.Write(level, "[{Phase}] {Message}", phaseToString phase, msg)
    | None -> ()

let shouldTrace phase =
    match options with
    | Some opts -> opts.Debug || Set.contains phase opts.TracePhases
    | None -> false

let logDebug phase msg = log LogEventLevel.Debug phase msg
let logInfo phase msg = log LogEventLevel.Information phase msg
let logWarning phase msg = log LogEventLevel.Warning phase msg
let logError phase msg = log LogEventLevel.Error phase msg

// Phase-specific trace logging
let trace phase msg =
    if shouldTrace phase then
        log LogEventLevel.Debug phase msg
```

### Phase별 디버그 출력

**--show-tokens 출력 예시:**
```
=== LEXER TOKENS ===
[1:1]  LET      "let"
[1:5]  IDENT    "x"
[1:7]  EQ       "="
[1:9]  INT      "42"
[1:12] IN       "in"
[1:15] IDENT    "x"
[1:17] PLUS     "+"
[1:19] INT      "1"
[1:20] EOF
====================
```

**--show-ast 출력 예시:**
```
=== PARSED AST ===
ELet
├── name: "x"
├── value: ELiteral (LInt 42)
└── body: EBinaryOp
          ├── op: Add
          ├── left: EVariable "x"
          └── right: ELiteral (LInt 1)
==================
```

**--show-types 출력 예시:**
```
=== TYPE INFERENCE ===
Expression: let x = 42 in x + 1
Inferred type: int

Bindings:
  x : int
======================
```

### 새 파일: PrettyPrint.fs
```fsharp
module FunLang.PrettyPrint

open FunLang.Ast

// 토큰 출력
let formatToken (pos: Position) (tokenType: string) (value: string) =
    sprintf "[%d:%d]\t%-8s %s" pos.Line pos.Column tokenType (sprintf "\"%s\"" value)

let printTokens (tokens: (Token * Position) list) =
    printfn "=== LEXER TOKENS ==="
    tokens |> List.iter (fun (tok, pos) ->
        printfn "%s" (formatToken pos (tokenName tok) (tokenValue tok)))
    printfn "===================="

// AST 트리 출력
let rec printAst (indent: string) (isLast: bool) (expr: Expr) =
    let prefix = if isLast then "└── " else "├── "
    let childIndent = indent + (if isLast then "    " else "│   ")
    match expr with
    | ELiteral lit ->
        printfn "%s%sELiteral (%A)" indent prefix lit
    | EVariable name ->
        printfn "%s%sEVariable \"%s\"" indent prefix name
    | EBinaryOp (op, left, right) ->
        printfn "%s%sEBinaryOp" indent prefix
        printfn "%s├── op: %A" childIndent op
        printfn "%s├── left:" childIndent
        printAst (childIndent + "│   ") false left
        printfn "%s└── right:" childIndent
        printAst (childIndent + "    ") true right
    | ELet (name, value, body) ->
        printfn "%s%sELet" indent prefix
        printfn "%s├── name: \"%s\"" childIndent name
        printfn "%s├── value:" childIndent
        printAst (childIndent + "│   ") false value
        printfn "%s└── body:" childIndent
        printAst (childIndent + "    ") true body
    // ... 다른 케이스들

let showAst (expr: Expr) =
    printfn "=== PARSED AST ==="
    printAst "" true expr
    printfn "=================="

// 타입 출력
let showTypes (expr: Expr) (inferredType: Type) (bindings: Map<string, Type>) =
    printfn "=== TYPE INFERENCE ==="
    printfn "Inferred type: %s" (formatType inferredType)
    if not (Map.isEmpty bindings) then
        printfn "\nBindings:"
        bindings |> Map.iter (fun name t ->
            printfn "  %s : %s" name (formatType t))
    printfn "======================"
```

### Program.fs 수정
```fsharp
open Argu
open FunLang.Options
open FunLang.Logging
open FunLang.PrettyPrint

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CliArgs>(programName = "funlang")

    try
        let results = parser.Parse(argv)
        let opts = parseOptions results

        Logging.initialize opts

        let input = getInput results

        // Lexer phase
        Logging.logInfo Lexer "Starting tokenization"
        let tokens = Lexer.tokenize input
        Logging.logInfo Lexer (sprintf "Tokenization complete: %d tokens" (List.length tokens))

        if opts.ShowTokens then
            PrettyPrint.printTokens tokens

        // Parser phase
        Logging.logInfo Parser "Starting parsing"
        let ast = Parser.parse tokens
        Logging.logInfo Parser "Parsing complete"

        if opts.ShowAst then
            PrettyPrint.showAst ast

        // Type check phase (Phase 5+)
        if opts.ShowTypes then
            Logging.logInfo TypeCheck "Starting type inference"
            let (inferredType, bindings) = TypeInference.infer ast
            Logging.logInfo TypeCheck "Type inference complete"
            PrettyPrint.showTypes ast inferredType bindings

        // Eval phase
        Logging.logInfo Eval "Starting evaluation"
        let result = Interpreter.eval Map.empty ast
        Logging.logInfo Eval (sprintf "Evaluation complete: %A" result)

        printfn "%A" result
        0
    with
    | :? ArguParseException as e ->
        printfn "%s" e.Message
        1
    | e ->
        Logging.logError Runtime e.Message
        printfn "Error: %s" e.Message
        1
```

---

## Phase 1: Core Expressions (기초)

### 목표
정수, 산술 연산, let 바인딩 파싱 및 평가

### 파일별 작업

**Ast.fs**
```fsharp
type Literal = LInt of int | LBool of bool | LString of string | LUnit
type BinaryOp = Add | Sub | Mul | Div | Mod | Eq | Neq | Lt | Gt | Lte | Gte | And | Or
type UnaryOp = Neg | Not
type Expr =
    | ELiteral of Literal
    | EVariable of string
    | EBinaryOp of BinaryOp * Expr * Expr
    | EUnaryOp of UnaryOp * Expr
    | ELet of string * Expr * Expr
```

**Lexer.fsl** - 토큰: `INT`, `IDENT`, `LET`, `IN`, `+`, `-`, `*`, `/`, `(`, `)`, `=`

**Parser.fsy** - 연산자 우선순위와 함께 표현식 문법 정의

**Interpreter.fs** - `Value` 타입과 `eval` 함수 구현 (로깅 포함)

**Program.fs** - 파싱 → 평가 파이프라인 연결

### 로깅 포인트
- 토큰화 시작/완료
- 파싱 시작/완료
- 평가 단계별 추적 (Debug 레벨)

---

## Phase 1.2: Indentation-Based Syntax

### 목표
Python/Haskell/F# 스타일의 들여쓰기 기반 블록 구문 지원

### 설계 원칙

FunLang은 **Offside Rule** (들여쓰기 규칙)을 사용:
- 블록의 시작은 키워드 다음 줄의 들여쓰기로 결정
- 같은 들여쓰기 레벨 = 같은 블록
- 더 깊은 들여쓰기 = 중첩 블록
- 더 얕은 들여쓰기 = 블록 종료

### 문법 예시

```funlang
// let 바인딩 (여러 줄)
let result =
    let x = 10
    let y = 20
    x + y

// 함수 정의
let factorial n =
    if n = 0 then
        1
    else
        n * factorial (n - 1)

// match 표현식
let describe xs =
    match xs with
    | [] -> "empty"
    | [x] -> "single"
    | _ -> "multiple"

// 중첩 let
let outer =
    let inner1 = 1
    let inner2 =
        let deep = 2
        deep + 1
    inner1 + inner2
```

### Lexer 수정: IndentationTracker

**새 파일: Indentation.fs**
```fsharp
module FunLang.Indentation

type IndentToken =
    | INDENT      // 들여쓰기 증가
    | DEDENT      // 들여쓰기 감소
    | NEWLINE     // 같은 레벨의 새 줄

type IndentState = {
    IndentStack: int list    // 들여쓰기 레벨 스택
    PendingTokens: Token list // 버퍼된 토큰들
    AtLineStart: bool        // 줄 시작 여부
    ParenDepth: int          // 괄호 깊이 (괄호 안에선 들여쓰기 무시)
}

let initialState = {
    IndentStack = [0]
    PendingTokens = []
    AtLineStart = true
    ParenDepth = 0
}

// 들여쓰기 처리 로직
let processIndentation (state: IndentState) (currentIndent: int) : IndentState * Token list =
    match state.IndentStack with
    | [] -> failwith "Empty indent stack"
    | top :: rest ->
        if currentIndent > top then
            // INDENT: 더 깊은 들여쓰기
            { state with IndentStack = currentIndent :: state.IndentStack }, [INDENT]
        elif currentIndent = top then
            // 같은 레벨
            state, []
        else
            // DEDENT: 더 얕은 들여쓰기 (여러 레벨 한번에 닫힐 수 있음)
            let rec popLevels stack dedents =
                match stack with
                | [] -> failwith "Indentation error: no matching indent level"
                | level :: rest when level = currentIndent ->
                    stack, dedents
                | level :: rest when level > currentIndent ->
                    popLevels rest (DEDENT :: dedents)
                | _ -> failwith "Indentation error: inconsistent indentation"
            let newStack, dedents = popLevels state.IndentStack []
            { state with IndentStack = newStack }, dedents
```

### Lexer.fsl 수정

```fsl
{
open FunLang.Indentation

let mutable indentState = initialState

// 줄 시작 시 공백 수 계산
let countIndent (s: string) =
    s |> Seq.takeWhile ((=) ' ') |> Seq.length

// 괄호 깊이 추적
let enterParen () = indentState <- { indentState with ParenDepth = indentState.ParenDepth + 1 }
let exitParen () = indentState <- { indentState with ParenDepth = indentState.ParenDepth - 1 }
let inParens () = indentState.ParenDepth > 0
}

let whitespace = [' ' '\t']*
let newline = '\n' | '\r\n'

rule tokenize = parse
| newline whitespace {
    if inParens() then
        tokenize lexbuf  // 괄호 안에선 들여쓰기 무시
    else
        let indent = countIndent (lexeme lexbuf)
        let state', tokens = processIndentation indentState indent
        indentState <- { state' with AtLineStart = true }
        // tokens를 버퍼에 추가하고 다음 토큰 반환
        emitTokens tokens lexbuf
  }
| '(' { enterParen(); LPAREN }
| ')' { exitParen(); RPAREN }
| '[' { enterParen(); LBRACKET }
| ']' { exitParen(); RBRACKET }
// ... 기존 토큰들
```

### Parser.fsy 수정

```fsy
%token INDENT DEDENT NEWLINE

// 블록 표현식
block:
    | INDENT stmtList DEDENT { EBlock $2 }
    ;

stmtList:
    | stmt { [$1] }
    | stmt NEWLINE stmtList { $1 :: $3 }
    ;

// let with block
letExpr:
    | LET IDENT EQ expr { ELet($2, $4, ...) }
    | LET IDENT EQ block { ELet($2, $4, ...) }
    | LET IDENT params EQ block { ELetFun($2, $3, $5, ...) }
    ;

// if with indentation
ifExpr:
    | IF expr THEN block ELSE block { EIf($2, $4, $6) }
    | IF expr THEN expr ELSE expr { EIf($2, $4, $6) }
    ;
```

### AST 추가
```fsharp
type Expr =
    // ... 기존
    | EBlock of Expr list   // 들여쓰기 블록 (마지막 표현식이 값)
```

### CLI 옵션 추가
```bash
funlang --show-indents program.fun  # 들여쓰기 토큰 표시
```

**--show-indents 출력 예시:**
```
=== INDENTATION TOKENS ===
[1:1]  LET        "let"
[1:5]  IDENT      "x"
[1:7]  EQ         "="
[2:1]  INDENT     (level 4)
[2:5]  LET        "let"
[2:9]  IDENT      "y"
...
[4:1]  DEDENT     (level 0)
===========================
```

### 들여쓰기 에러 처리

```fsharp
type ErrorKind =
    // ... 기존
    | IndentationError of expected: int * actual: int * Position
    | MixedTabsSpaces of Position
    | InconsistentIndent of Position

// 탭/스페이스 혼용 검사
let validateIndentation (line: string) (pos: Position) =
    let hasTab = line |> Seq.exists ((=) '\t')
    let hasSpace = line |> Seq.exists ((=) ' ')
    if hasTab && hasSpace then
        Error { Kind = MixedTabsSpaces pos; Message = "Mixed tabs and spaces"; Hint = Some "Use spaces only" }
    else Ok ()
```

### 로깅 포인트
- 들여쓰기 레벨 변경 추적
- INDENT/DEDENT 토큰 발행
- 들여쓰기 스택 상태

### FsCheck 테스트
```fsharp
[<Property>]
let ``block returns last expression`` (values: int list) =
    values.Length > 0 ==>
    let block = values |> List.map string |> String.concat "\n    "
    let code = sprintf "let x =\n    %s\nin x" block
    eval (parse code) = VInt (List.last values)

[<Property>]
let ``dedent closes block correctly`` (a: int) (b: int) =
    let code = sprintf """
let outer =
    let inner = %d
    inner + 1
outer + %d""" a b
    eval (parse code) = VInt (a + 1 + b)
```

---

## Phase 1.5: Property-Based Testing with Expecto

### 목표
Expecto + FsCheck로 인터프리터 속성 검증

### 패키지 추가
```bash
dotnet new console -lang F# -o tests/FunLang.Tests
dotnet add tests/FunLang.Tests package Expecto
dotnet add tests/FunLang.Tests package Expecto.FsCheck
dotnet add tests/FunLang.Tests package FsCheck
dotnet sln add tests/FunLang.Tests
```

### Expecto 테스트 구조

**tests/FunLang.Tests/Program.fs**
```fsharp
open Expecto

[<EntryPoint>]
let main argv =
    runTestsInAssemblyWithCLIArgs [] argv
```

**tests/FunLang.Tests/LexerTests.fs**
```fsharp
module FunLang.Tests.LexerTests

open Expecto
open Expecto.Flip
open FsCheck
open FunLang.Lexer

// 단위 테스트
let unitTests = testList "Lexer Unit Tests" [
    test "tokenizes integer literal" {
        let result = tokenize "42"
        Expect.isOk result "should succeed"
        result |> Result.map (fun tokens ->
            Expect.equal tokens [INT 42; EOF] "should be INT token"
        ) |> ignore
    }

    test "tokenizes let keyword" {
        let result = tokenize "let"
        Expect.isOk result "should succeed"
    }
]

// Property-Based 테스트
let propertyTests = testList "Lexer Property Tests" [
    testProperty "integer literals tokenize correctly" <| fun (n: int) ->
        let result = tokenize (string n)
        match result with
        | Ok tokens -> List.exists (function INT _ -> true | _ -> false) tokens
        | Error _ -> n < 0  // 음수는 MINUS + INT로 파싱될 수 있음

    testProperty "tokenize is deterministic" <| fun (input: NonEmptyString) ->
        let r1 = tokenize input.Get
        let r2 = tokenize input.Get
        r1 = r2
]

[<Tests>]
let tests = testList "Lexer" [unitTests; propertyTests]
```

### Expecto + FsCheck 통합 패턴

```fsharp
open Expecto
open FsCheck

// 기본 property 테스트
testProperty "addition is commutative" <| fun (a: int) (b: int) ->
    eval (parse $"{a} + {b}") = eval (parse $"{b} + {a}")

// 커스텀 config로 property 테스트
testPropertyWithConfig { FsCheckConfig.defaultConfig with maxTest = 1000 }
    "multiplication distributes over addition" <| fun (a: int) (b: int) (c: int) ->
    eval (parse $"{a} * ({b} + {c})") = eval (parse $"{a} * {b} + {a} * {c}")

// 커스텀 Generator 사용
let smallIntGen = Gen.choose (-100, 100)
let smallIntArb = Arb.fromGen smallIntGen

testPropertyWithConfigs
    { FsCheckConfig.defaultConfig with arbitrary = [typeof<SmallIntArb>] }
    "small integer operations" <| fun (a: SmallInt) (b: SmallInt) ->
    // 오버플로우 없는 테스트
    ...
```

### 테스트 구성 패턴

```fsharp
// 테스트 모듈 구조
module FunLang.Tests.InterpreterTests

open Expecto

let arithmeticTests = testList "Arithmetic" [
    testProperty "addition is commutative" <| fun a b -> ...
    testProperty "multiplication is associative" <| fun a b c -> ...
]

let bindingTests = testList "Bindings" [
    testProperty "let binding substitutes correctly" <| fun x -> ...
    testProperty "nested let bindings work" <| fun x y -> ...
]

let functionTests = testList "Functions" [
    testProperty "identity function returns input" <| fun x -> ...
    testProperty "closure captures environment" <| fun x y -> ...
]

[<Tests>]
let allTests = testList "Interpreter" [
    arithmeticTests
    bindingTests
    functionTests
]
```

### 커스텀 Generator

```fsharp
// 유효한 AST 표현식 생성기
type ExprGenerators =
    static member Expr() =
        let rec exprGen size =
            if size <= 0 then
                Gen.map (LInt >> ELiteral) Arb.generate<int>
            else
                Gen.oneof [
                    Gen.map (LInt >> ELiteral) Arb.generate<int>
                    Gen.map EVariable (Gen.elements ["x"; "y"; "z"])
                    Gen.map2 (fun l r -> EBinaryOp(Add, l, r))
                        (exprGen (size/2)) (exprGen (size/2))
                ]
        Gen.sized exprGen |> Arb.fromGen

// 사용
testPropertyWithConfig
    { FsCheckConfig.defaultConfig with arbitrary = [typeof<ExprGenerators>] }
    "well-formed expressions evaluate" <| fun (expr: Expr) ->
    ...
```

### 테스트 실행

```bash
# 모든 테스트 실행
dotnet run --project tests/FunLang.Tests

# 상세 출력
dotnet run --project tests/FunLang.Tests -- --debug

# 특정 테스트만 실행
dotnet run --project tests/FunLang.Tests -- --filter "Lexer"

# 병렬 실행
dotnet run --project tests/FunLang.Tests -- --parallel

# 실패시 즉시 중단
dotnet run --project tests/FunLang.Tests -- --fail-on-focused-tests --sequenced
```

### Expect 헬퍼 함수

```fsharp
open Expecto

// Result 검증
let expectOk msg result =
    match result with
    | Ok v -> v
    | Error e -> failtest $"{msg}: {e}"

let expectError msg result =
    match result with
    | Ok v -> failtest $"{msg}: expected error but got {v}"
    | Error e -> e

// 사용
test "parse succeeds" {
    let ast = parse "1 + 2" |> expectOk "parse failed"
    Expect.equal ast (EBinaryOp(Add, ELiteral(LInt 1), ELiteral(LInt 2))) "AST mismatch"
}
```

---

## Phase 2: Functions & Control Flow

### 목표
일급 함수(람다), 함수 호출, 재귀, if/else

### AST 추가
```fsharp
| ELambda of string * Expr
| EApply of Expr * Expr
| EIf of Expr * Expr * Expr
| ELetRec of string * Expr * Expr
```

### 새 토큰
`FUN`, `ARROW (->)`, `IF`, `THEN`, `ELSE`, `TRUE`, `FALSE`, `REC`

### Interpreter
- `VClosure` (클로저)와 `VRecClosure` (재귀 클로저) 추가
- 함수 적용시 환경 캡처 및 확장

### 로깅 포인트
- 클로저 생성
- 함수 호출 (인자값 포함)
- 재귀 호출 깊이

### FsCheck 테스트
```fsharp
[<Property>]
let ``identity function returns input`` (x: int) =
    eval (parse $"(fun x -> x) {x}") = VInt x

[<Property>]
let ``if-true branch is taken`` (a: int) (b: int) =
    eval (parse $"if true then {a} else {b}") = VInt a
```

---

## Phase 3: Data Structures

### 목표
튜플과 리스트

### AST 추가
```fsharp
| ETuple of Expr list
| EList of Expr list
| ECons of Expr * Expr
```

### 새 토큰
`[`, `]`, `,`, `::`, `;`

### Value 추가
`VTuple of Value list`, `VList of Value list`

### FsCheck 테스트
```fsharp
[<Property>]
let ``cons prepends element to list`` (x: int) (xs: int list) =
    let listStr = xs |> List.map string |> String.concat "; "
    eval (parse $"{x} :: [{listStr}]") = VList (VInt x :: List.map VInt xs)
```

---

## Phase 4: Pattern Matching

### 목표
match 표현식과 패턴

### AST 추가
```fsharp
type Pattern =
    | PWildcard | PVariable of string | PLiteral of Literal
    | PTuple of Pattern list | PList of Pattern list
    | PCons of Pattern * Pattern | PConstructor of string * Pattern option

type Expr = ... | EMatch of Expr * (Pattern * Expr option * Expr) list
```

### 새 토큰
`MATCH`, `WITH`, `|`, `_`, `WHEN`

### Interpreter
- `matchPattern: Pattern -> Value -> Map<string,Value> option`
- 패턴 매칭 실패시 다음 케이스로 이동

### 로깅 포인트
- 매칭 시도 (패턴, 값)
- 매칭 성공/실패
- 바인딩된 변수

### FsCheck 테스트
```fsharp
[<Property>]
let ``wildcard matches anything`` (x: int) =
    eval (parse $"match {x} with | _ -> 42") = VInt 42

[<Property>]
let ``variable pattern binds value`` (x: int) =
    eval (parse $"match {x} with | n -> n + 1") = VInt (x + 1)
```

---

## Phase 5: Type System (Hindley-Milner)

### 목표
정적 타입 검사 + Hindley-Milner 타입 추론 (Algorithm W)

> **상세 알고리즘:** [algorithms/HINDLEY_MILNER.md](algorithms/HINDLEY_MILNER.md)

### 핵심 구성요소

| 파일 | 설명 |
|------|------|
| `Types.fs` | Type, TypeScheme, TypeEnv, Substitution 타입 정의 |
| `TypeInference.fs` | Algorithm W 구현 |

### 주요 함수

- `applySubst`: 타입에 치환 적용
- `freeTypeVars`: 자유 타입 변수 추출
- `occursCheck`: 무한 타입 방지
- `unify`: 두 타입의 유니피케이션
- `generalize`: let 바인딩에서 타입 일반화
- `instantiate`: 다형 타입 인스턴스화
- `infer`: Algorithm W 핵심 함수

### .fsproj 수정
Types.fs, TypeInference.fs를 컴파일 순서에 추가

---

## Phase 6: User-Defined Types

### 목표
discriminated union 정의

### 문법 예시
```
type Option 'a = None | Some of 'a
type List 'a = Nil | Cons of 'a * List 'a
```

### AST 추가
```fsharp
type TypeDef = UnionDef of string * string list * (string * Type option) list
type Program = { TypeDefs: TypeDef list; MainExpr: Expr }
| EConstructor of string * Expr option
```

### 새 토큰
`TYPE`, `OF`, `'` (타입 파라미터용)

---

## Critical Files

| 파일 | 역할 | Phase |
|------|------|-------|
| `src/FunLang/Options.fs` | CLI 옵션 정의 (Argu) | 0 |
| `src/FunLang/Logging.fs` | Serilog 래퍼 + Phase 추적 | 0 |
| `src/FunLang/Errors.fs` | 에러 타입 및 포맷팅 | 0 |
| `src/FunLang/PrettyPrint.fs` | 토큰/AST/타입 출력 | 0 |
| `src/FunLang/Repl.fs` | Interactive REPL 모드 | 0 |
| `src/FunLang/Ast.fs` | AST 타입 정의 | 1 |
| `src/FunLang/Parser.fsy` | 문법 규칙 | 1 |
| `src/FunLang/Lexer.fsl` | 토큰화 규칙 | 1 |
| `src/FunLang/Indentation.fs` | 들여쓰기 처리 | 1.2 |
| `src/FunLang/Interpreter.fs` | 평가기 | 1 |
| `src/FunLang/Program.fs` | 진입점 | 0 |
| `src/FunLang/Types.fs` | 타입 정의 | 5 |
| `src/FunLang/TypeInference.fs` | Algorithm W 타입 추론 | 5 |
| `tests/FunLang.Tests/` | FsCheck 테스트 | 1.5 |

---

## Implementation Order

```
Phase 0 (Infra) ──> Phase 1 (Core) ──> Phase 1.2 (Indent) ──> Phase 1.5 (Tests)
                                                │
                                                ├──> Phase 2 (Func) ───┐
                                                │                      ├──> Phase 4 ──> Phase 5 ──> Phase 6
                                                └──> Phase 3 (Data) ───┘
```

### Phase 순서 및 의존성

| Phase | 이름 | 선행 조건 | 설명 |
|-------|------|-----------|------|
| 0 | Infrastructure | - | 로깅, CLI, 에러 처리 |
| 1 | Core Expressions | Phase 0 | 기본 표현식, 산술 연산 |
| 1.2 | Indentation | Phase 1 | 들여쓰기 기반 블록 구문 |
| 1.5 | Testing | Phase 1.2 | FsCheck 속성 테스트 |
| 2 | Functions | Phase 1.2 | 람다, 재귀, if/else |
| 3 | Data Structures | Phase 1.2 | 리스트, 튜플 |
| 4 | Pattern Matching | Phase 2, 3 | match 표현식 |
| 5 | Type System | Phase 4 | Hindley-Milner 추론 |
| 6 | User Types | Phase 5 | Discriminated Union |

- Phase 1.2 (Indentation)는 함수/제어문에서 필요하므로 Phase 2 전에 완료
- Phase 2, 3은 Phase 1.2 이후 병렬 진행 가능
- 각 Phase 완료 후 테스트 추가 권장

---

## Verification

각 Phase 완료 후:
1. `dotnet build` - 컴파일 성공 확인
2. `dotnet test` - FsCheck 테스트 통과
3. `dotnet run` - REPL 또는 테스트 코드 실행
4. 로그 파일 확인 (`logs/funlang-*.log`)

### 예시 프로그램
- Phase 1: `let x = 1 + 2 in x * 3` → `9`
- Phase 2: `let rec fact = fun n -> if n = 0 then 1 else n * fact (n - 1) in fact 5` → `120`
- Phase 3: `[1; 2; 3]`, `(1, "hello", true)`
- Phase 4: `match [1;2] with | [] -> 0 | x::_ -> x` → `1`
- Phase 5: 타입 추론 결과 출력
- Phase 6: `type Option 'a = None | Some of 'a` 정의 및 사용

---

## Error Handling

### ⚠️ 핵심 원칙: Exception 사용 금지

**모든 에러는 `Result<'T, Error>` 또는 `Option<'T>`으로 전파:**

```
❌ 금지: raise, failwith, Exception
✅ 필수: Result.Error, None, Result.bind, Option.bind
```

**이유:**
- 타입 시스템으로 에러 처리 강제
- 에러 경로가 명시적으로 드러남
- 컴파일 타임에 에러 처리 누락 감지
- 함수 합성과 파이프라인에 적합

---

### 에러 타입 정의

**새 파일: Errors.fs** (Phase 0에서 추가)
```fsharp
module FunLang.Errors

type Position = {
    Line: int
    Column: int
    File: string option
}

type ErrorKind =
    | LexerError of char: char * position: Position
    | ParseError of token: string * expected: string list * position: Position
    | UnboundVariable of name: string * position: Position
    | TypeError of expected: string * actual: string * position: Position
    | RuntimeError of message: string * position: Position option
    | DivisionByZero of position: Position
    | NonExhaustiveMatch of position: Position
    | IndentationError of expected: int * actual: int * position: Position

type FunLangError = {
    Kind: ErrorKind
    Message: string
    Hint: string option
    Position: Position option
}

/// 에러 생성 헬퍼 함수들
module Error =
    let lexer char pos =
        { Kind = LexerError (char, pos)
          Message = sprintf "Unexpected character '%c'" char
          Hint = Some "Check for typos or unsupported characters"
          Position = Some pos }

    let parse token expected pos =
        { Kind = ParseError (token, expected, pos)
          Message = sprintf "Unexpected token '%s'" token
          Hint = if List.isEmpty expected then None
                 else Some (sprintf "Expected: %s" (String.concat ", " expected))
          Position = Some pos }

    let unboundVar name pos =
        { Kind = UnboundVariable (name, pos)
          Message = sprintf "Unbound variable '%s'" name
          Hint = None  // TODO: suggest similar names
          Position = Some pos }

    let typeError expected actual pos =
        { Kind = TypeError (expected, actual, pos)
          Message = sprintf "Type mismatch: expected %s, got %s" expected actual
          Hint = None
          Position = Some pos }

    let divisionByZero pos =
        { Kind = DivisionByZero pos
          Message = "Division by zero"
          Hint = Some "Check divisor is not zero"
          Position = Some pos }

    let nonExhaustive pos =
        { Kind = NonExhaustiveMatch pos
          Message = "Non-exhaustive pattern match"
          Hint = Some "Add missing pattern cases"
          Position = Some pos }

/// 에러 포맷팅
let formatError (err: FunLangError) : string =
    match err.Position with
    | Some p -> sprintf "Error at line %d, column %d: %s" p.Line p.Column err.Message
    | None -> sprintf "Error: %s" err.Message
```

---

### Result 타입 중심 API

**모든 공개 함수는 Result 반환:**

```fsharp
/// 결과 타입 별칭
type LexResult = Result<Token list, FunLangError>
type ParseResult = Result<Expr, FunLangError>
type TypeResult = Result<Type, FunLangError>
type EvalResult = Result<Value, FunLangError>

/// Lexer: string -> Result<Token list, FunLangError>
let tokenize (input: string) : LexResult = ...

/// Parser: Token list -> Result<Expr, FunLangError>
let parse (tokens: Token list) : ParseResult = ...

/// Type Inference: Expr -> Result<Type, FunLangError>
let inferType (expr: Expr) : TypeResult = ...

/// Evaluator: Env -> Expr -> Result<Value, FunLangError>
let eval (env: Env) (expr: Expr) : EvalResult = ...
```

---

### Result 합성 패턴

**파이프라인으로 에러 전파:**

```fsharp
/// Result.bind를 사용한 체이닝
let run (input: string) : EvalResult =
    tokenize input
    |> Result.bind parse
    |> Result.bind (inferType >> Result.map ignore)  // 타입 체크만
    |> Result.bind (eval Map.empty)

/// result computation expression 사용 (권장)
let run (input: string) : EvalResult =
    result {
        let! tokens = tokenize input
        let! ast = parse tokens
        let! _ = inferType ast
        return! eval Map.empty ast
    }
```

**Result 헬퍼 함수:**

```fsharp
module Result =
    /// 여러 Result를 순서대로 처리
    let sequence (results: Result<'a, 'e> list) : Result<'a list, 'e> =
        List.foldBack (fun r acc ->
            match r, acc with
            | Ok x, Ok xs -> Ok (x :: xs)
            | Error e, _ -> Error e
            | _, Error e -> Error e
        ) results (Ok [])

    /// Result에 함수 적용
    let map2 f r1 r2 =
        match r1, r2 with
        | Ok x, Ok y -> Ok (f x y)
        | Error e, _ -> Error e
        | _, Error e -> Error e
```

---

### Option 사용 패턴

**값이 없을 수 있는 경우:**

```fsharp
/// 환경에서 변수 조회
let lookupVar (env: Env) (name: string) : Value option =
    Map.tryFind name env

/// Option을 Result로 변환
let lookupVarOrError (env: Env) (name: string) (pos: Position) : Result<Value, FunLangError> =
    match Map.tryFind name env with
    | Some v -> Ok v
    | None -> Error (Error.unboundVar name pos)

/// Option 체이닝
let findAndApply (env: Env) (name: string) (arg: Value) : Value option =
    option {
        let! func = lookupVar env name
        let! closure =
            match func with
            | VClosure (param, body, closureEnv) -> Some (param, body, closureEnv)
            | _ -> None
        return eval (Map.add (fst closure) arg (thd closure)) (snd closure)
    }
```

---

### Lexer: Result 기반 구현

**Lexer.fsl 수정 - Exception 없이:**

```fsharp
/// Lexer 상태 (에러 수집용)
type LexerState = {
    Tokens: Token list
    Position: Position
    Errors: FunLangError list
}

/// 단일 문자 처리 (에러 시 Error 반환)
let lexChar (state: LexerState) (c: char) : Result<LexerState, FunLangError> =
    match c with
    | '+' -> Ok { state with Tokens = PLUS :: state.Tokens }
    | '-' -> Ok { state with Tokens = MINUS :: state.Tokens }
    // ... 다른 토큰들
    | c when Char.IsLetter c -> lexIdentifier state c
    | c when Char.IsDigit c -> lexNumber state c
    | c -> Error (Error.lexer c state.Position)

/// 전체 토큰화 (Result 반환)
let tokenize (input: string) : LexResult =
    let rec loop state chars =
        match chars with
        | [] -> Ok (List.rev state.Tokens)
        | c :: rest ->
            match lexChar state c with
            | Ok newState -> loop newState rest
            | Error e -> Error e
    loop initialState (Seq.toList input)
```

---

### Parser: Result 기반 구현

**Parser.fsy는 내부적으로 exception 사용 (FsYacc 한계)**
**하지만 외부 API는 Result로 래핑:**

```fsharp
/// Parser 래퍼 (exception을 Result로 변환)
let parse (tokens: Token list) : ParseResult =
    try
        Ok (Parser.parseTokens tokens)
    with
    | :? FsYacc.ParseError as e ->
        Error (Error.parse e.Token e.Expected e.Position)

// 참고: FsYacc 내부는 exception 사용 불가피
// 하지만 이 경계 지점에서만 try-with 사용
// 나머지 코드는 모두 Result 사용
```

---

### Interpreter: Result 기반 구현

```fsharp
let rec eval (env: Env) (expr: Expr) : EvalResult =
    match expr with
    | ELiteral (LInt n) -> Ok (VInt n)
    | ELiteral (LBool b) -> Ok (VBool b)

    | EVariable name ->
        match Map.tryFind name env with
        | Some v -> Ok v
        | None -> Error (Error.unboundVar name expr.Position)

    | EBinaryOp (Div, left, right) ->
        result {
            let! l = eval env left
            let! r = eval env right
            match l, r with
            | VInt _, VInt 0 -> return! Error (Error.divisionByZero right.Position)
            | VInt a, VInt b -> return VInt (a / b)
            | _ -> return! Error (Error.typeError "int" (typeOf l) expr.Position)
        }

    | EBinaryOp (op, left, right) ->
        result {
            let! l = eval env left
            let! r = eval env right
            return! applyBinaryOp op l r expr.Position
        }

    | EIf (cond, thenBr, elseBr) ->
        result {
            let! c = eval env cond
            match c with
            | VBool true -> return! eval env thenBr
            | VBool false -> return! eval env elseBr
            | _ -> return! Error (Error.typeError "bool" (typeOf c) cond.Position)
        }

    | ELet (name, value, body) ->
        result {
            let! v = eval env value
            return! eval (Map.add name v env) body
        }

    | ELambda (param, body) ->
        Ok (VClosure (param, body, env))

    | EApply (func, arg) ->
        result {
            let! f = eval env func
            let! a = eval env arg
            match f with
            | VClosure (param, body, closureEnv) ->
                return! eval (Map.add param a closureEnv) body
            | VRecClosure (name, param, body, closureEnv) ->
                let env' = Map.add name f (Map.add param a closureEnv)
                return! eval env' body
            | _ -> return! Error (Error.typeError "function" (typeOf f) func.Position)
        }

    | EMatch (scrutinee, cases) ->
        result {
            let! value = eval env scrutinee
            return! matchCases env value cases expr.Position
        }

and matchCases env value cases pos =
    match cases with
    | [] -> Error (Error.nonExhaustive pos)
    | (pattern, guard, body) :: rest ->
        match matchPattern pattern value with
        | None -> matchCases env value rest pos
        | Some bindings ->
            let env' = Map.fold (fun acc k v -> Map.add k v acc) env bindings
            match guard with
            | None -> eval env' body
            | Some guardExpr ->
                result {
                    let! g = eval env' guardExpr
                    match g with
                    | VBool true -> return! eval env' body
                    | VBool false -> return! matchCases env value rest pos
                    | _ -> return! Error (Error.typeError "bool" (typeOf g) guardExpr.Position)
                }
```

---

### Result Computation Expression

**result { } 사용을 위한 빌더:**

```fsharp
type ResultBuilder() =
    member _.Return(x) = Ok x
    member _.ReturnFrom(m) = m
    member _.Bind(m, f) = Result.bind f m
    member _.Zero() = Ok ()
    member _.Combine(m1, m2) = Result.bind (fun () -> m2) m1
    member _.Delay(f) = f
    member _.Run(f) = f()

let result = ResultBuilder()

// 사용 예시
let example () =
    result {
        let! x = Ok 1
        let! y = Ok 2
        return x + y  // Ok 3
    }
```

---

### 에러 메시지 예시

```
Error at line 3, column 15: Unexpected token '+'
  |
3 | let x = 1 + + 2
  |               ^
Hint: Expected: INT, IDENT, LPAREN

Error at line 5, column 8: Unbound variable 'foo'
  |
5 | let y = foo + 1
  |         ^^^

Error at line 7, column 1: Non-exhaustive pattern match
  |
7 | match xs with
  | ^^^^^
Hint: Add missing pattern cases

Error at line 10, column 12: Division by zero
  |
10 | let x = 10 / 0
   |            ^^^
Hint: Check divisor is not zero
```

---

### 소스 코드 스니펫 표시

```fsharp
let showSourceSnippet (source: string) (pos: Position) : string =
    let lines = source.Split('\n')
    if pos.Line <= lines.Length then
        let line = lines.[pos.Line - 1]
        let pointer = String.replicate pos.Column " " + "^"
        sprintf "  |\n%d | %s\n  | %s" pos.Line line pointer
    else ""
```

---

### 에러 처리 체크리스트

새 함수 작성 시:

- [ ] 반환 타입이 `Result<'T, FunLangError>` 또는 `Option<'T>`인가?
- [ ] `raise`, `failwith`, `exception` 사용하지 않았는가?
- [ ] 에러 경로가 명시적으로 처리되는가?
- [ ] `result { }` CE 또는 `Result.bind` 체이닝 사용했는가?
- [ ] 에러 메시지에 위치 정보가 포함되는가?

---

---

## Phase 9: Pattern Matching Improvements

### 개요

현재 FunLang의 pattern matching은 단순 순차 매칭 방식으로 구현되어 있음.
이를 함수형 언어 컴파일러 수준으로 개선하기 위한 계획.

> **상세 알고리즘:** [algorithms/PATTERN_ANALYSIS.md](algorithms/PATTERN_ANALYSIS.md)

**참고 논문:**
- [Compiling Pattern Matching to Good Decision Trees](http://moscova.inria.fr/~maranget/papers/ml05e-maranget.pdf) - Luc Maranget (2008)
- [Warnings for Pattern Matching](http://moscova.inria.fr/~maranget/papers/warn/warn.pdf) - Maranget

### Sub-Phases

| Phase | 목표 | 상태 |
|-------|------|------|
| 9.1 | Exhaustiveness Check (완전성 검사) | Complete |
| 9.2 | Redundancy Check (중복 패턴 검사) | Complete |
| 9.3 | TypeInfer Integration & Program.fs | In Progress |
| 9.4 | Guard 지원 개선 | Planned |

### 구현 파일

| 파일 | 설명 |
|------|------|
| `PatternAnalysis.fs` | Maranget Usefulness algorithm |
| `TypeInfer.fs` | Match 표현식에서 warning 생성 |
| `Program.fs` | Warning 출력 처리 |

### 주요 함수

- `specialize`: 생성자에 대해 행렬 특수화
- `defaultMatrix`: Default 연산
- `isUseful`: Maranget Usefulness 알고리즘
- `findMissing`: 누락된 패턴 찾기
- `checkRedundancy`: 중복 패턴 검사
- `analyzeMatch`: Match 표현식 분석 API

### 구현 계획

| 단계 | 내용 | 상태 |
|------|------|------|
| 9.1.1 | SimplePattern, PatternMatrix types | Done |
| 9.1.2 | specialize, defaultMatrix | Done |
| 9.1.3 | isUseful algorithm | Done |
| 9.1.4 | findMissing function | Done |
| 9.1.5 | Warning message format | Done |
| 9.2 | checkRedundancy | Done |
| 9.3 | TypeInfer integration | In Progress |

---

## Notes

- FsLexYacc는 LALR(1) 파서 - shift/reduce 충돌 주의
- 연산자 우선순위는 `%left`, `%right`, `%nonassoc`로 선언
- 컴파일 순서: Options.fs → Logging.fs → Errors.fs → PrettyPrint.fs → Ast.fs → Indentation.fs → Parser.fsy → Lexer.fsl → Interpreter.fs → Repl.fs → Program.fs
- 에러 메시지에 위치 정보 포함 권장
- Serilog 로그 레벨: Debug (상세 추적), Info (주요 단계), Warning (잠재적 문제), Error (실패)
- 모든 공개 함수는 `Result<'T, FunLangError>` 반환하여 에러 전파
