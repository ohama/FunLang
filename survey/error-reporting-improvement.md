# FunLang Error Reporting Improvement Survey

## 현재 상태

FunLang의 에러 시스템은 이미 견고한 기반을 갖추고 있다:

- 60+ 에러 종류 (`E0301`~`E0706`), 3개 경고 (`W0001`~`W0003`)
- 모든 에러에 힌트 포함 (`= hint: ...`)
- 소스 위치 (Span: 파일, 줄, 컬럼)
- 최대 3개의 보조 위치 (secondary spans)
- 타입 추론 문맥 스택 (InferContext, 14종)
- 유니피케이션 경로 추적 (UnifyPath)
- Rust 스타일 포맷 (`error[E0301]: ... --> file:line:col`)

## 개선 가능한 영역 (우선순위순)

### 1. 소스 코드 스니펫 (High Impact / Medium Effort)

**현재:**
```
error[E0301]: Type mismatch: expected int but got string
 --> test.l3:3:14-21
   = hint: Check that all branches return the same type
```

**개선 후:**
```
error[E0301]: Type mismatch: expected int but got string
 --> test.l3:3:14-21
  |
3 |   if true then 42 else "hello"
  |                        ^^^^^^^ expected int, got string
  |
  = hint: Check that all branches return the same type
```

**구현 방법:**
- `Diagnostic.fs`의 `formatDiagnostic`에 소스 라인 렌더링 추가
- 파일 내용 캐싱 (파일당 한 번 읽기): `Map<string, string[]>`
- `Span`의 line/column으로 `^^^` 밑줄 생성
- 멀티라인 스팬은 첫/마지막 줄만 표시, `...`으로 연결
- 거터(gutter): 줄 번호 오른쪽 정렬 + `|` 구분자

**필요한 변경:**
```fsharp
// Diagnostic.fs 또는 새 SourceDisplay.fs
let mutable sourceCache : Map<string, string[]> = Map.empty

let getSourceLine (fileName: string) (line: int) : string option =
    if fileName = "<unknown>" || fileName = "<expr>" then None
    else
        let lines =
            match Map.tryFind fileName sourceCache with
            | Some l -> l
            | None ->
                let l = System.IO.File.ReadAllLines(fileName)
                sourceCache <- Map.add fileName l sourceCache
                l
        if line >= 1 && line <= lines.Length then Some lines.[line - 1]
        else None

let renderSourceSnippet (span: Span) : string list =
    match getSourceLine span.FileName span.StartLine with
    | None -> []
    | Some line ->
        let gutter = sprintf "%d" span.StartLine
        let padding = String.replicate gutter.Length " "
        let underline =
            String.replicate span.StartColumn " "
            + String.replicate (span.EndColumn - span.StartColumn) "^"
        [ sprintf "  %s |" padding
          sprintf "  %s | %s" gutter line
          sprintf "  %s | %s" padding underline ]
```

**예상 코드량:** ~80줄 (SourceDisplay 모듈 + formatDiagnostic 수정)

---

### 2. ANSI 컬러 출력 (High Impact / Low Effort)

**현재:** 모노크롬 텍스트

**개선 후:**
- `error` → 빨강+볼드
- `warning` → 노랑+볼드
- `-->` → 파랑
- `= hint:` → 시안
- `^^^` 밑줄 → 빨강
- 줄 번호 → 파랑

**구현 방법:**
```fsharp
// Color.fs
let mutable colorEnabled = not (Console.IsOutputRedirected)
                           && Environment.GetEnvironmentVariable("NO_COLOR") |> isNull

let red s    = if colorEnabled then sprintf "\x1b[1;31m%s\x1b[0m" s else s
let yellow s = if colorEnabled then sprintf "\x1b[1;33m%s\x1b[0m" s else s
let blue s   = if colorEnabled then sprintf "\x1b[34m%s\x1b[0m" s else s
let cyan s   = if colorEnabled then sprintf "\x1b[36m%s\x1b[0m" s else s
let bold s   = if colorEnabled then sprintf "\x1b[1m%s\x1b[0m" s else s
```

**규약:**
- `NO_COLOR` 환경 변수 존재 시 비활성화 (https://no-color.org)
- stderr가 파이프/리다이렉트 시 비활성화
- `--color always|never|auto` CLI 옵션 추가 가능

**예상 코드량:** ~30줄 (Color 모듈) + formatDiagnostic 수정 ~20줄

---

### 3. "Did you mean?" 제안 (Medium Impact / Low Effort)

**현재:**
```
error[E0303]: Unbound variable: prnt
 --> test.l3:1:0-4
   = hint: Make sure the variable is defined before use
```

**개선 후:**
```
error[E0303]: Unbound variable: prnt
 --> test.l3:1:0-4
   = hint: Did you mean 'print'?
```

**구현 방법:**
```fsharp
// Suggest.fs
let editDistance (s1: string) (s2: string) : int =
    let m, n = s1.Length, s2.Length
    let d = Array2D.create (m + 1) (n + 1) 0
    for i in 0..m do d.[i, 0] <- i
    for j in 0..n do d.[0, j] <- j
    for i in 1..m do
        for j in 1..n do
            let cost = if s1.[i-1] = s2.[j-1] then 0 else 1
            d.[i, j] <- min (min (d.[i-1, j] + 1) (d.[i, j-1] + 1)) (d.[i-1, j-1] + cost)
    d.[m, n]

let suggest (name: string) (scope: string seq) : string option =
    let threshold = max 2 (name.Length / 3)
    scope
    |> Seq.map (fun s -> (s, editDistance name s))
    |> Seq.filter (fun (_, d) -> d <= threshold)
    |> Seq.sortBy snd
    |> Seq.tryHead
    |> Option.map fst
```

**적용 대상:**
- `UnboundVar`: 현재 스코프의 모든 변수명
- `UnboundConstructor`: 현재 ConstructorEnv의 모든 생성자명
- `UnresolvedModule`: 현재 모듈 맵의 모든 모듈명
- `UnboundField`: 해당 레코드 타입의 모든 필드명
- `UnknownTypeClass`: ClassEnv의 모든 타입 클래스명

**필요한 변경:**
- `TypeError`에 `Scope: string list option` 필드 추가 (또는 별도 전달)
- `typeErrorToDiagnostic`에서 scope 기반 제안 생성
- Bidir.fs의 `UnboundVar` raise 시 `Map.keys env`를 scope로 전달

**예상 코드량:** ~40줄 (Suggest 모듈) + TypeError/Diagnostic 수정 ~30줄

---

### 4. 파서 에러 개선 (High Impact / Medium Effort)

**현재:**
```
Error: parse error
```

위치 정보 없음, "parse error"라는 메시지만 출력.

**개선 후:**
```
error: unexpected token 'EQUALS', expected one of: ARROW, RPAREN, COMMA
 --> test.l3:3:10
  |
3 |   let f x = = y
  |             ^ unexpected '='
  |
  = hint: Check for missing or extra tokens
```

**구현 방법:**

#### 4a. `parse_error_rich` 활용 (fsyacc 지원)

fsyacc는 `parse_error_rich` 콜백을 지원한다. `ParseErrorContext`에서:
- `CurrentToken`: 에러를 일으킨 토큰
- `ShiftTokens`: shift 가능한 토큰 ID 목록
- `ReduceTokens`: reduce 가능한 토큰 ID 목록

```fsharp
// Parser.fsy 상단에 추가
%{
let parse_error_rich = Some(fun (ctxt: ParseErrorContext<token>) ->
    let current = ctxt.CurrentToken |> Option.map formatToken |> Option.defaultValue "end of input"
    let expected =
        (ctxt.ShiftTokens @ ctxt.ReduceTokens)
        |> List.distinct
        |> List.map tokenName  // token ID → readable name
        |> String.concat ", "
    let msg = sprintf "unexpected %s, expected one of: %s" current expected
    raise (ParseException(msg, lexbuf.StartPos))
)
%}
```

**주의점:**
- fsyacc의 `ParseErrorContext`는 토큰 ID(int)를 제공, 읽을 수 있는 이름으로 변환 필요
- 예상 토큰이 너무 많으면 (50+) 잘라서 표시
- `lexbuf` 위치를 에러에 포함시켜야 함

#### 4b. 에러 복구 (error production)

모듈-레벨 선언 경계에서 복구:
```
Decls:
    | Decl Decls  { $1 :: $2 }
    | error NEWLINE Decls { (* skip bad declaration, continue *) $3 }
    | /* empty */ { [] }
```

**한계:**
- LALR 에러 복구는 본질적으로 제한적
- 중첩된 표현식 내부 에러에서는 복구가 어려움
- 선언 경계에서의 복구만 실용적

**예상 코드량:** ~60줄 (parse_error_rich + 토큰 이름 매핑) + Parser.fsy 수정 ~20줄

---

### 5. 타입 클래스 제약 에러 개선 (Medium Impact / Low Effort)

**현재:**
```
error[E0701]: No instance of Show for int list
 --> test.l3:1:11-17
   = hint: Add an instance declaration for this type
```

**개선 후:**
```
error[E0701]: No instance of Show for int list
 --> test.l3:1:11-17
   = available instances: Show int, Show bool, Show string, Show char
   = hint: Add 'instance Show (int list) = ...' to define how to show int lists
```

**구현 방법:**
- E0701 에러 생성 시 `currentInstEnv`에서 해당 클래스의 인스턴스 목록 포함
- `typeErrorToDiagnostic`에서 인스턴스 목록을 notes에 추가

**예상 코드량:** ~15줄

---

### 6. 다중 에러 보고 (High Impact / High Effort)

**현재:** 첫 번째 타입 에러에서 중단 (TypeException으로 즉시 throw)

**개선 후:** 여러 에러를 수집하고 한번에 보고

```
error[E0303]: Unbound variable: foo
 --> test.l3:1:10-13

error[E0301]: Type mismatch: expected int but got string
 --> test.l3:3:6-18

Found 2 errors.
```

**구현 전략:**

#### 전략 A: Poison Type (추천)
- 에러 발생 시 `TError` (poison type) 반환하고 계속 진행
- `TError`와의 유니피케이션은 항상 성공 (캐스케이딩 방지)
- 에러를 `mutable errorList`에 축적
- 최종적으로 모든 에러 반환

```fsharp
type Type = ... | TError  // Poison type

// Bidir.fs에서
let reportError (err: TypeError) =
    accumulatedErrors <- err :: accumulatedErrors

// unify에서
| (TError, _) | (_, TError) -> empty  // Always succeed with poison
```

**장점:** 선언 단위로 독립적인 에러 보고 가능
**단점:** Bidir.fs의 모든 `raise TypeException` → `reportError` + TError 반환으로 변경 필요. 대규모 리팩토링.

#### 전략 B: 선언 단위 재시도 (간단)
- `typeCheckDecls`에서 각 선언을 개별 try-catch로 감싸기
- 실패한 선언은 환경에 추가하지 않고 에러만 수집
- 후속 선언은 이전 선언의 결과 없이 진행

```fsharp
// TypeCheck.fs
let rec typeCheckDeclsMultiError decls env ... =
    decls |> List.fold (fun (env, errors, warns) decl ->
        try
            let (env', ..., w) = typeCheckSingleDecl decl env ...
            (env', errors, warns @ w)
        with
        | TypeException err ->
            let diag = typeErrorToDiagnostic err
            (env, diag :: errors, warns)  // env unchanged, error collected
    ) (env, [], [])
```

**장점:** 기존 코드 변경 최소화, 선언 수준에서 독립적
**단점:** 한 선언 내 여러 에러는 첫 번째만 보고됨, 후속 선언이 이전 바인딩 없이 추가 에러 발생 가능

**예상 코드량:** 전략 A ~200줄, 전략 B ~50줄

---

### 7. 런타임 에러 위치 (Medium Impact / Medium Effort)

**현재:** 런타임 에러 (failwith, 0으로 나누기 등)는 위치 정보 없음
```
Error: Division by zero
```

**개선 후:**
```
runtime error: Division by zero
 --> test.l3:5:14-19
  |
5 |   let x = 10 / 0
  |                ^ division by zero
```

**구현 방법:**
- `Eval.fs`의 `eval` 함수가 현재 Expr의 Span을 추적
- 모든 `failwith` → `FunLangException` with Span 정보
- 또는: `eval`에서 try-catch 후 현재 표현식의 span 추가

```fsharp
// eval 함수에서 현재 span 스레딩
let mutable currentEvalSpan : Span = unknownSpan

// 에러 발생 시
exception RuntimeError of message: string * span: Span
```

**예상 코드량:** ~60줄 (RuntimeError + eval span tracking + display)

---

### 8. JSON 구조화 출력 (Medium Impact / Low Effort)

**현재:** 텍스트만 출력

**개선 후:** `--error-format=json`으로 구조화된 출력

```json
{
  "type": "error",
  "code": "E0301",
  "message": "Type mismatch: expected int but got string",
  "primary_span": {
    "file": "test.l3",
    "start_line": 3, "start_col": 14,
    "end_line": 3, "end_col": 21
  },
  "secondary_spans": [...],
  "notes": [...],
  "hint": "Check that all branches return the same type"
}
```

**용도:** IDE 통합, CI 도구, LSP 서버

**예상 코드량:** ~40줄 (Diagnostic → JSON 변환)

---

## 구현 우선순위 매트릭스

| # | 기능 | Impact | Effort | 예상 LOC | 의존성 |
|---|------|--------|--------|---------|--------|
| 1 | 소스 코드 스니펫 | **High** | Medium | ~80 | 없음 |
| 2 | ANSI 컬러 | **High** | **Low** | ~50 | 없음 |
| 3 | "Did you mean?" | Medium | **Low** | ~70 | 없음 |
| 4 | 파서 에러 개선 | **High** | Medium | ~80 | 없음 |
| 5 | 타입 클래스 에러 | Medium | **Low** | ~15 | 없음 |
| 6 | 다중 에러 보고 | **High** | **High** | ~50-200 | 없음 |
| 7 | 런타임 에러 위치 | Medium | Medium | ~60 | 없음 |
| 8 | JSON 출력 | Medium | **Low** | ~40 | 없음 |

## 추천 구현 순서

**Phase 1 (Quick Wins):** #2 ANSI 컬러 + #3 "Did you mean?" + #5 타입 클래스 에러
- 3개 합쳐서 ~135줄, 독립적, 즉시 사용자 경험 개선

**Phase 2 (Core Improvement):** #1 소스 코드 스니펫 + #4 파서 에러
- 에러 메시지 품질의 근본적 향상, ~160줄

**Phase 3 (Advanced):** #6 다중 에러 (전략 B) + #7 런타임 에러 위치
- 가장 큰 아키텍처 변경, 전략 B로 ~110줄

**Phase 4 (Tooling):** #8 JSON 출력
- IDE/CI 통합 준비, ~40줄

## 참고: 다른 언어의 에러 포맷

### Rust (rustc) — 가장 영향력 있는 포맷
```
error[E0308]: mismatched types
 --> src/main.rs:3:14
  |
2 |     let x: i32 = "hello";
  |            ---   ^^^^^^^ expected `i32`, found `&str`
  |            |
  |            expected due to this
```
- 소스 스니펫 + 멀티 스팬 + 컬러 + `--explain E0308` + JSON 출력
- FunLang가 가장 많이 참고하는 모델

### Elm — 대화형 에러
```
-- TYPE MISMATCH --- src/Main.elm

The 2nd argument to `add` is not what I expect:

8|   add 1 "hello"
           ^^^^^^^
This argument is a String, but `add` needs it to be: Int

Hint: Try using String.toInt to convert it.
```
- 단일 에러만 보고, 대화체 문장, 구체적 힌트

### GHC (Haskell) — 제약 기반 에러
```
Main.hs:3:10: error: [GHC-83865]
    * Couldn't match expected type 'Int' with actual type '[Char]'
    * In the expression: "hello"
      In an equation for 'x': x = "hello"
```
- 다중 에러 보고, 제약 에러에 "Could not deduce" 패턴
- 경고 레벨 세밀 제어 (`-Wall`, `-Wno-*`)

### OCaml — 소스 스니펫 + 제안
```
File "main.ml", line 3, characters 14-21:
3 |   let x : int = "hello"
                     ^^^^^^^
Error: This expression has type string but type int was expected
```
- 4.08+에서 소스 스니펫 추가, "Did you mean?" 제안

### F# (fsc) — MSBuild 호환
```
/path/to/file.fs(3,14): error FS0001: type mismatch
```
- 소스 스니펫 없음 (IDE에 위임), MSBuild 진단 포맷
- fsyacc 대신 hand-written parser 사용 (에러 복구를 위해)

## fsyacc 한계와 대안

fsyacc는 `error` 토큰과 `parse_error_rich` 콜백을 지원하지만:
- LALR 에러 복구는 본질적으로 제한적 (스택 pop → error shift → sync 토큰 탐색)
- 중첩 표현식 내 에러 복구는 거의 불가능
- 선언 경계에서의 복구만 실용적

`parse_error_rich`의 `ParseErrorContext`가 제공하는 정보:
- `CurrentToken`: 에러 토큰
- `ShiftTokens` / `ReduceTokens`: 예상 토큰 ID 목록
- 이것만으로도 "expected X but got Y" 메시지 생성 가능

F#이 fsyacc 대신 hand-written parser를 쓰는 이유가 바로 에러 복구 때문이지만,
FunLang의 경우 `parse_error_rich`만으로도 상당한 개선이 가능하다.
