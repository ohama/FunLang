# FunLang Debugging Guide

FunLang 인터프리터 개발 및 디버깅을 위한 가이드

---

## ⚠️ 에러 처리 원칙: Exception 금지, Result/Option 필수

### 핵심 규칙

```
❌ 금지: raise, failwith, Exception, try-with (경계 제외)
✅ 필수: Result<'T, Error>, Option<'T>, Result.bind, Option.bind
```

### Result 패턴

```fsharp
// 모든 함수는 Result 반환
let tokenize (input: string) : Result<Token list, FunLangError> = ...
let parse (tokens: Token list) : Result<Expr, FunLangError> = ...
let eval (env: Env) (expr: Expr) : Result<Value, FunLangError> = ...

// Result 체이닝
let run input =
    tokenize input
    |> Result.bind parse
    |> Result.bind (eval Map.empty)

// result { } Computation Expression (권장)
let run input =
    result {
        let! tokens = tokenize input
        let! ast = parse tokens
        return! eval Map.empty ast
    }
```

### Option 패턴

```fsharp
// 값이 없을 수 있는 경우
let lookupVar env name : Value option =
    Map.tryFind name env

// Option을 Result로 변환
let lookupVarOrError env name pos : Result<Value, FunLangError> =
    match Map.tryFind name env with
    | Some v -> Ok v
    | None -> Error (Error.unboundVar name pos)
```

### 유일한 예외: 외부 라이브러리 경계

```fsharp
// FsYacc는 내부적으로 exception 사용 (불가피)
// 경계에서만 try-with로 Result 변환
let parse tokens : ParseResult =
    try
        Ok (Parser.parseTokens tokens)
    with
    | :? ParseError as e -> Error (Error.parse e.Token e.Expected e.Position)
```

### 디버깅 시 확인사항

- [ ] `raise`, `failwith` 사용하지 않았는가?
- [ ] 함수 반환 타입이 `Result` 또는 `Option`인가?
- [ ] 에러 경로가 명시적으로 처리되는가?
- [ ] `result { }` 또는 `Result.bind` 체이닝 사용했는가?

---

## CLI 디버깅 옵션

### 기본 옵션

| 옵션 | 설명 |
|------|------|
| `-v`, `--verbose` | 상세 출력 활성화 |
| `-d`, `--debug` | 디버그 모드 (모든 phase 추적) |
| `--log-level <level>` | 로그 레벨 설정 (debug\|info\|warning\|error) |
| `--log-file <path>` | 로그 파일 경로 지정 |
| `--no-color` | 컬러 출력 비활성화 |

### Phase별 추적 옵션

| 옵션 | 설명 |
|------|------|
| `--show-tokens` | Lexer 토큰 출력 |
| `--show-ast` | 파싱된 AST 출력 |
| `--show-types` | 타입 추론 결과 출력 |
| `--show-indents` | 들여쓰기 토큰 출력 |
| `--trace <phase>` | 특정 phase 추적 (lexer, parser, typecheck, eval) |

---

## 디버깅 시나리오별 가이드

### 1. Lexer 디버깅

토큰화 문제 해결:

```bash
# 토큰 목록 확인
funlang --show-tokens program.fun

# 상세 로그와 함께
funlang -d --trace lexer --show-tokens program.fun

# 표현식 직접 테스트
funlang -e "let x = 42" --show-tokens
```

**출력 예시:**
```
=== LEXER TOKENS ===
[1:1]  LET      "let"
[1:5]  IDENT    "x"
[1:7]  EQ       "="
[1:9]  INT      "42"
[1:11] EOF
====================
```

**확인 포인트:**
- 토큰 위치 (line:column)가 올바른가?
- 예상하지 못한 토큰이 있는가?
- EOF가 정상적으로 생성되는가?

### 2. Parser 디버깅

파싱 문제 해결:

```bash
# AST 확인
funlang --show-ast program.fun

# Parser phase 추적
funlang -d --trace parser --show-ast program.fun

# 토큰과 AST 함께 확인
funlang --show-tokens --show-ast program.fun
```

**출력 예시:**
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

**확인 포인트:**
- AST 구조가 예상과 일치하는가?
- 연산자 우선순위가 올바른가?
- 괄호 처리가 정확한가?

### 3. Indentation 디버깅

들여쓰기 문제 해결:

```bash
# 들여쓰기 토큰 확인
funlang --show-indents program.fun

# 상세 추적
funlang -d --show-indents --show-tokens program.fun
```

**출력 예시:**
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

**확인 포인트:**
- INDENT/DEDENT가 올바른 위치에 발생하는가?
- 탭과 스페이스 혼용 에러가 있는가?
- 괄호 안에서 들여쓰기가 무시되는가?

### 4. Type Inference 디버깅

타입 추론 문제 해결:

```bash
# 타입 정보 확인
funlang --show-types program.fun

# TypeCheck phase 추적
funlang -d --trace typecheck --show-types program.fun

# REPL에서 타입 확인
funlang -i --show-types
fun[1]> :type let id = fun x -> x in id
```

**출력 예시:**
```
=== TYPE INFERENCE ===
Expression: let id = fun x -> x in id 42
Inferred type: int

Bindings:
  id : 't1 -> 't1  (polymorphic)
======================
```

**확인 포인트:**
- 추론된 타입이 예상과 일치하는가?
- 다형성이 올바르게 처리되는가?
- 타입 에러 메시지가 명확한가?

### 5. Evaluation 디버깅

평가 문제 해결:

```bash
# Eval phase 추적
funlang -d --trace eval program.fun

# 전체 파이프라인 디버그
funlang -d --show-tokens --show-ast --show-types program.fun
```

**확인 포인트:**
- 클로저 환경이 올바르게 캡처되는가?
- 재귀 호출이 정상 동작하는가?
- 패턴 매칭 분기가 올바른가?

---

## 로그 레벨 가이드

| 레벨 | 용도 |
|------|------|
| `debug` | 모든 상세 정보 (개발 중 사용) |
| `info` | 주요 단계 진행 상황 |
| `warning` | 잠재적 문제 (deprecated 사용 등) |
| `error` | 실패 및 에러 |

```bash
# 디버그 레벨 + 파일 로깅
funlang --log-level debug --log-file debug.log program.fun

# 에러만 표시
funlang --log-level error program.fun
```

---

## REPL 디버깅

Interactive 모드에서 디버깅:

```bash
funlang -i --show-types
```

**REPL 명령:**
```
:help       도움말
:env        현재 환경 (바인딩된 변수들)
:type <e>   표현식의 타입 확인
:clear      환경 초기화
:history    명령 히스토리
:load <f>   파일 로드
:quit       종료
```

**REPL 세션 예시:**
```
fun[1]> let x = 42
val it : int = 42

fun[2]> :env
x : int = 42

fun[3]> :type fun n -> n * 2
int -> int

fun[4]> let double = fun n -> n * 2
val it : int -> int = <function>

fun[5]> double x
val it : int = 84
```

---

## 흔한 에러와 해결책

### 1. Lexer 에러

**증상:** `Unexpected character 'X'`

```bash
# 진단
funlang --show-tokens -e "let x = @invalid"
```

**해결:** 지원하지 않는 문자 확인 및 수정

### 2. Parser 에러

**증상:** `Unexpected token 'X', expected Y`

```bash
# 진단
funlang --show-tokens --show-ast -e "let x = 1 + + 2"
```

**해결:** 토큰 순서 확인, 문법 오류 수정

### 3. Indentation 에러

**증상:** `Mixed tabs and spaces` 또는 `Inconsistent indentation`

```bash
# 진단
funlang --show-indents program.fun
```

**해결:** 스페이스만 사용, 일관된 들여쓰기 레벨

### 4. Type 에러

**증상:** `Type mismatch: expected X, got Y`

```bash
# 진단
funlang --show-types -e "if 1 then 2 else 3"
```

**해결:** 타입 불일치 수정 (예: if 조건은 bool이어야 함)

### 5. Runtime 에러

**증상:** `Division by zero` 또는 `Non-exhaustive match`

```bash
# 진단
funlang -d --trace eval program.fun
```

**해결:** 예외 케이스 처리, 패턴 매칭 완전성 확인

---

## Expecto + FsCheck 테스트 디버깅

### 테스트 실행 명령

```bash
# 모든 테스트 실행
dotnet run --project tests/FunLang.Tests

# 상세 출력 (디버그)
dotnet run --project tests/FunLang.Tests -- --debug

# 특정 테스트만 실행
dotnet run --project tests/FunLang.Tests -- --filter "Lexer"
dotnet run --project tests/FunLang.Tests -- --filter "Property"

# 병렬 실행
dotnet run --project tests/FunLang.Tests -- --parallel

# 순차 실행 (디버깅 시)
dotnet run --project tests/FunLang.Tests -- --sequenced

# 실패 즉시 중단
dotnet run --project tests/FunLang.Tests -- --fail-on-focused-tests
```

### Expecto 테스트 구조

```fsharp
open Expecto

// 단위 테스트
let unitTests = testList "Unit" [
    test "specific case" {
        Expect.equal (tokenize "42") (Ok [INT 42]) "should tokenize"
    }
]

// Property 테스트
let propTests = testList "Properties" [
    testProperty "roundtrip" <| fun input -> ...
]

// 테스트 등록
[<Tests>]
let allTests = testList "All" [unitTests; propTests]
```

### FsCheck 실패 분석

FsCheck가 실패하면 **shrunk counterexample**을 제공합니다:

```
Falsifiable, after 42 tests (3 shrinks):
Original: (12345, -9876)
Shrunk: (1, -1)
```

이 최소 반례로 문제를 재현:

```fsharp
// 실패한 케이스를 단위 테스트로 변환
test "regression: shrunk case" {
    let result = eval (parse "1 + -1")
    Expect.equal result (Ok (VInt 0)) "should handle"
}
```

### Expecto Expect 함수

```fsharp
Expect.equal actual expected "message"
Expect.isOk result "should succeed"
Expect.isError result "should fail"
Expect.isTrue condition "should be true"
Expect.throws (fun () -> ...) "should throw"
Expect.containsAll actual expected "should contain"
```

---

## 개발 Phase별 디버깅 체크리스트

### Phase 0 (Infrastructure)
- [ ] Serilog 로그가 올바르게 출력되는가?
- [ ] CLI 옵션이 파싱되는가?
- [ ] 에러 메시지에 위치 정보가 포함되는가?

### Phase 1 (Core)
- [ ] 기본 토큰이 올바르게 인식되는가?
- [ ] 연산자 우선순위가 맞는가?
- [ ] let 바인딩이 동작하는가?

### Phase 1.2 (Indentation)
- [ ] INDENT/DEDENT 토큰이 올바르게 생성되는가?
- [ ] 블록 표현식이 올바르게 파싱되는가?
- [ ] 괄호 안에서 들여쓰기가 무시되는가?

### Phase 2 (Functions)
- [ ] 람다가 클로저를 올바르게 캡처하는가?
- [ ] 재귀 함수가 동작하는가?
- [ ] if/else 분기가 올바른가?

### Phase 3 (Data Structures)
- [ ] 튜플/리스트가 올바르게 생성되는가?
- [ ] cons(::)가 동작하는가?

### Phase 4 (Pattern Matching)
- [ ] 패턴 매칭이 올바른 순서로 시도되는가?
- [ ] 바인딩이 올바르게 추출되는가?
- [ ] when 가드가 동작하는가?

### Phase 5 (Type System)
- [ ] 기본 타입 추론이 동작하는가?
- [ ] 다형성(let polymorphism)이 동작하는가?
- [ ] 타입 에러 메시지가 명확한가?

### Phase 6 (User Types)
- [ ] 타입 정의가 파싱되는가?
- [ ] 생성자가 동작하는가?
- [ ] 패턴 매칭과 연동되는가?

---

## 로그 파일 분석

```bash
# 최근 로그 확인
tail -f logs/funlang-*.log

# 에러만 필터링
grep "ERR" logs/funlang-*.log

# 특정 phase만 필터링
grep "\[LEXER\]" logs/funlang-*.log
grep "\[PARSER\]" logs/funlang-*.log
grep "\[TYPECHECK\]" logs/funlang-*.log
grep "\[EVAL\]" logs/funlang-*.log
```

---

## 추가 리소스

- `.claude/PLAN.md` - 상세 구현 계획
- `CLAUDE.md` - 개발 가이드라인
- `README.md` - 프로젝트 개요
