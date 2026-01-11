# Better Error Messages - Design Document

> **Status:** Design
> **Phase:** 8
> **Created:** 2026-01-11
> **Updated:** 2026-01-11 (Rust patterns 추가)

---

## Executive Summary

FunLang의 에러 메시지를 Rust 수준으로 개선하기 위한 설계 문서입니다. Rust 컴파일러(rustc)의 진단 시스템, [annotate-snippets](https://rust-lang.github.io/rust-project-goals/2024h2/annotate-snippets.html), [Ariadne](https://lib.rs/crates/ariadne), [Miette](https://docs.rs/miette) 라이브러리의 설계 원칙을 참고합니다.

---

## 1. Current State Analysis

### 1.1 Error Type System

**현재 구조:**
```
FunLangError (Errors.fs)    TypeError (Types.fs)
     |                            |
     v                            v
  Lexer, Parser             Type Inference
  Interpreter
```

**문제점:**
- 두 개의 분리된 에러 타입 (`FunLangError`, `TypeError`)
- Program.fs에서 다른 처리 로직 필요
- 에러 정보 불일치

### 1.2 Position Tracking

**현재 흐름:**
```
Lexer (position O) → Parser (position X) → TypeInfer (None) → Eval (None)
```

| Phase | Position Info | Note |
|-------|--------------|------|
| Lexer | O | `tokenizeRawWithPositions` 사용 |
| Parser | X | `Result<Expr, string>` 반환, position 손실 |
| TypeInfer | None | AST에 position 없음 |
| Eval | None | AST에 position 없음 |

**핵심 문제:** AST 노드에 Position 정보가 없어서 타입 에러와 런타임 에러의 소스 위치를 표시할 수 없음.

### 1.3 Error Display

**현재:**
```
Error at line 3, column 15: Type mismatch: expected int, got string
Hint: Check for typos or unsupported characters
```

**목표 (Rust-style):**
```
error[E201]: Type mismatch
  --> file.fun:3:15
   |
3  |     let x = "hello" + 1
   |             ^^^^^^^ expected `int`, found `string`
   |
   = help: `+` operator requires both operands to be `int`
   = note: string concatenation uses `++` operator
```

### 1.4 Code References

| File | Description |
|------|-------------|
| `src/FunLang/Errors.fs` | FunLangError, ErrorKind, formatError |
| `src/FunLang/Types.fs` | TypeError, TypeErrorKind, formatTypeError |
| `src/FunLang/Program.fs` | Error display logic |
| `src/FunLang/ParserWrapper.fs` | Parser returns `Result<Expr, string>` |
| `src/FunLang/Interpreter.fs` | Runtime errors with `None` position |
| `src/FunLang/TypeInfer.fs` | Type errors with `None` position |

---

## 2. Rust Diagnostic System Analysis

> Reference: [Rust Compiler Development Guide - Diagnostics](https://rustc-dev-guide.rust-lang.org/diagnostics.html)

### 2.1 Rust Diagnostic Principles

Rust의 진단 시스템은 다음 원칙을 따릅니다:

1. **메시지 독립성**: "The main error message should be general and able to stand on its own, so that it can make sense even in isolation."
2. **평이한 언어**: "Write in plain simple English... if it cannot be understood by a normal programmer, it's too complex."
3. **Primary Span 자족성**: Primary span은 IDE 통합을 위해 충분한 컨텍스트를 제공해야 함
4. **계층적 구조**: Level → Code → Message → Diagnostic Window → Sub-diagnostics

### 2.2 Diagnostic Levels

| Level | 용도 | FunLang 매핑 |
|-------|------|-------------|
| `error` | 컴파일 방해 | 모든 현재 에러 |
| `warning` | 의심스러운 코드 | (향후) 미사용 변수, deprecated 등 |
| `note` | 추가 컨텍스트 | 관련 위치, 타입 정보 |
| `help` | 수정 방법 제안 | "Did you mean?", fix 제안 |

### 2.3 Primary vs Secondary Spans

```rust
error[E0308]: mismatched types
  --> src/main.rs:4:18
   |
3  | fn foo() -> i32 {
   |             --- expected `i32` because of return type  // Secondary
4  |     "hello"
   |     ^^^^^^^ expected `i32`, found `&str`               // Primary
```

**Primary Span:**
- 에러의 핵심 위치
- 충분한 컨텍스트 제공 (IDE에서 단독 표시 가능)
- 보통 하나만 존재

**Secondary Span:**
- 관련 정보 제공
- "expected due to this" 등의 설명
- 여러 개 가능

### 2.4 Suggestion Applicability

Rust는 제안(suggestion)에 신뢰도 레벨을 부여합니다:

| Level | 설명 | 자동 적용 |
|-------|------|----------|
| `MachineApplicable` | 확실히 올바른 수정 | O (rustfix) |
| `HasPlaceholders` | 템플릿 포함 | X |
| `MaybeIncorrect` | 올바를 수도 있음 | X |
| `Unspecified` | 신뢰도 불명 | X |

**FunLang 적용:**
```fsharp
type SuggestionApplicability =
    | MachineApplicable  // 자동 적용 가능
    | HasPlaceholders    // 사용자 입력 필요
    | MaybeIncorrect     // 추측성 제안
    | Unspecified        // 기본값
```

### 2.5 Multi-Span Diagnostics

복잡한 에러는 여러 위치를 가리킵니다:

```rust
error[E0308]: mismatched types
  --> src/main.rs:10:5
   |
5  |     let x: i32 = ...;
   |            --- expected due to this
...
10 |     x = "hello";
   |         ^^^^^^^ expected `i32`, found `&str`
```

**구현 고려사항:**
- 줄 생략 (`...`) 처리
- 라벨 겹침 방지 알고리즘
- 파일 간 span 지원 (향후)

### 2.6 Error Explanations

Rust는 `rustc --explain E0308`으로 상세 설명 제공:

```
$ rustc --explain E0308

Expected type did not match the received type.

Erroneous code examples:

    fn plus_one(x: i32) -> i32 {
        x + 1
    }
    plus_one("Not a number"); // error!

This error occurs when an expression was used in a place where the compiler
expected an expression of a different type...
```

**FunLang 적용:**
- `funlang --explain E201` 명령 추가
- `docs/errors/E201.md` 파일로 상세 설명 관리
- 에러 메시지에 `= see: funlang --explain E201` 추가

---

## 3. Design (Updated)

### 3.1 Diagnostic Type (Rust-inspired)

```fsharp
/// Diagnostic severity level (Rust: Level)
type Severity =
    | Error      // 컴파일/실행 불가
    | Warning    // 의심스러운 코드
    | Note       // 추가 정보
    | Help       // 수정 제안

/// Source span with byte offsets (Miette-style)
type SourceSpan = {
    Start: Position
    End: Position
    /// Byte offset for precise slicing
    ByteStart: int option
    ByteEnd: int option
}

/// Labeled span for annotations
type LabeledSpan = {
    Span: SourceSpan
    Label: string option
    Style: SpanStyle
}

and SpanStyle =
    | Primary    // 핵심 에러 위치
    | Secondary  // 관련 정보

/// Suggestion with applicability
type Suggestion = {
    Span: SourceSpan
    Replacement: string
    Message: string
    Applicability: SuggestionApplicability
}

and SuggestionApplicability =
    | MachineApplicable  // 자동 적용 안전
    | HasPlaceholders    // 사용자 입력 필요 (e.g., "/* type */")
    | MaybeIncorrect     // 추측성 제안
    | Unspecified

/// Main diagnostic type (Rust: Diagnostic, Miette: Diagnostic trait)
type Diagnostic = {
    Severity: Severity
    Code: string option           // E001, E201, etc.
    Message: string               // 독립적으로 이해 가능한 메시지
    PrimarySpan: LabeledSpan option
    SecondarySpans: LabeledSpan list
    Notes: string list            // = note: ...
    Helps: string list            // = help: ...
    Suggestions: Suggestion list  // 수정 제안
    Related: Diagnostic list      // 관련 진단 (Miette: #[related])
}
```

### 3.2 Diagnostic Builder API

Rust의 `DiagnosticBuilder` 패턴 적용:

```fsharp
module Diagnostic =
    /// Create new diagnostic
    let error code message = {
        Severity = Error
        Code = Some code
        Message = message
        PrimarySpan = None
        SecondarySpans = []
        Notes = []
        Helps = []
        Suggestions = []
        Related = []
    }

    /// Add primary span with label
    let withPrimarySpan span label diag =
        { diag with PrimarySpan = Some { Span = span; Label = Some label; Style = Primary } }

    /// Add secondary span
    let withSecondarySpan span label diag =
        let labeled = { Span = span; Label = Some label; Style = Secondary }
        { diag with SecondarySpans = labeled :: diag.SecondarySpans }

    /// Add note
    let withNote note diag =
        { diag with Notes = note :: diag.Notes }

    /// Add help
    let withHelp help diag =
        { diag with Helps = help :: diag.Helps }

    /// Add machine-applicable suggestion
    let withSuggestion span replacement message diag =
        let sugg = {
            Span = span
            Replacement = replacement
            Message = message
            Applicability = MachineApplicable
        }
        { diag with Suggestions = sugg :: diag.Suggestions }

    /// Add related diagnostic
    let withRelated related diag =
        { diag with Related = related :: diag.Related }

// Usage example
let typeMismatchError span expected actual =
    Diagnostic.error "E201" "Type mismatch"
    |> Diagnostic.withPrimarySpan span (sprintf "expected `%s`, found `%s`" expected actual)
    |> Diagnostic.withNote (sprintf "expected type: %s" expected)
    |> Diagnostic.withHelp "Check that the types match"
```

### 3.3 AST Position Tracking

**Located Wrapper (권장):**

```fsharp
/// Generic wrapper for located nodes
type Located<'a> = {
    Node: 'a
    Span: SourceSpan
}

/// Expression with location
type Expr = Located<ExprNode>

type ExprNode =
    | ELiteral of Literal
    | EVariable of string
    | EBinaryOp of BinaryOp * Expr * Expr
    | EUnaryOp of UnaryOp * Expr
    | ELet of string * Expr * Expr
    | ELetRec of string * Expr * Expr
    | ELambda of string * Expr
    | EApply of Expr * Expr
    | EIf of Expr * Expr * Expr
    | ETuple of Expr list
    | EList of Expr list
    | ECons of Expr * Expr
    | EMatch of Expr * (Pattern * Expr option * Expr) list
    | EBlock of Expr list
    | EConstructor of string * Expr option

/// Helper module
module Located =
    let create node span = { Node = node; Span = span }
    let map f loc = { Node = f loc.Node; Span = loc.Span }
    let node loc = loc.Node
    let span loc = loc.Span

    /// Create with dummy span (for testing)
    let dummy node = {
        Node = node
        Span = { Start = noPos; End = noPos; ByteStart = None; ByteEnd = None }
    }

    /// Merge spans (for compound expressions)
    let merge loc1 loc2 = {
        Start = loc1.Span.Start
        End = loc2.Span.End
        ByteStart = loc1.Span.ByteStart
        ByteEnd = loc2.Span.ByteEnd
    }
```

### 3.4 Error Formatter (Rust-style Output)

```fsharp
module ErrorFormatter =
    /// Configuration options (Ariadne-inspired)
    type Config = {
        TabWidth: int
        MaxLineWidth: int
        UseColors: bool
        UnderlineChar: char      // '^' or '─'
        MultilineStyle: bool     // Ariadne's multi-line span style
    }

    let defaultConfig = {
        TabWidth = 4
        MaxLineWidth = 140
        UseColors = true
        UnderlineChar = '^'
        MultilineStyle = true
    }

    /// Format a single diagnostic
    let format (source: string) (config: Config) (diag: Diagnostic) : string =
        let lines = source.Split('\n')

        // Header: error[E201]: Type mismatch
        let header = formatHeader diag

        // Location: --> file.fun:3:15
        let location = formatLocation diag.PrimarySpan

        // Source context with labels
        let sourceContext = formatSourceContext lines config diag

        // Notes and helps
        let footer = formatFooter diag

        [header; location; sourceContext; footer]
        |> List.filter (not << String.IsNullOrEmpty)
        |> String.concat "\n"

    /// Format header line
    let private formatHeader diag =
        let level =
            match diag.Severity with
            | Error -> "error"
            | Warning -> "warning"
            | Note -> "note"
            | Help -> "help"
        let code =
            match diag.Code with
            | Some c -> sprintf "[%s]" c
            | None -> ""
        sprintf "%s%s: %s" level code diag.Message

    /// Format source context with underlines and labels
    let private formatSourceContext lines config diag =
        // Collect all spans to display
        let allSpans =
            [ yield! diag.PrimarySpan |> Option.toList
              yield! diag.SecondarySpans ]

        // Group by line number
        let spansByLine =
            allSpans
            |> List.groupBy (fun s -> s.Span.Start.Line)
            |> Map.ofList

        // Format each relevant line
        let relevantLines =
            spansByLine
            |> Map.toList
            |> List.collect (fun (lineNum, spans) ->
                formatLine lines lineNum spans config)

        relevantLines |> String.concat "\n"

    /// Format a single line with its annotations
    let private formatLine lines lineNum spans config =
        if lineNum <= 0 || lineNum > Array.length lines then []
        else
            let lineContent = lines.[lineNum - 1]
            let lineNumStr = sprintf "%d" lineNum
            let padding = String.replicate (lineNumStr.Length) " "

            // Line with content
            let contentLine = sprintf "%s | %s" lineNumStr lineContent

            // Underline with labels
            let underlines =
                spans
                |> List.map (fun span ->
                    let startCol = span.Span.Start.Column - 1
                    let endCol =
                        match span.Span.End with
                        | pos when pos.Line = lineNum -> pos.Column - 1
                        | _ -> String.length lineContent
                    let width = max 1 (endCol - startCol)
                    let underline = String.replicate width (string config.UnderlineChar)
                    let spaces = String.replicate startCol " "
                    let label = span.Label |> Option.defaultValue ""
                    spaces + underline + " " + label)

            [ sprintf "%s |" padding
              contentLine
              yield! underlines |> List.map (sprintf "%s | %s" padding) ]

    /// Format notes and helps
    let private formatFooter diag =
        let notes = diag.Notes |> List.map (sprintf "   = note: %s")
        let helps = diag.Helps |> List.map (sprintf "   = help: %s")
        let suggestions =
            diag.Suggestions
            |> List.map (fun s ->
                sprintf "   = suggestion: %s\n     %s" s.Message s.Replacement)

        [ yield! notes
          yield! helps
          yield! suggestions ]
        |> String.concat "\n"
```

### 3.5 Smart Suggestions

```fsharp
module Suggestions =
    /// Levenshtein distance for typo detection
    let levenshteinDistance (s1: string) (s2: string) : int =
        let len1, len2 = s1.Length, s2.Length
        let d = Array2D.create (len1 + 1) (len2 + 1) 0

        for i in 0..len1 do d.[i, 0] <- i
        for j in 0..len2 do d.[0, j] <- j

        for i in 1..len1 do
            for j in 1..len2 do
                let cost = if s1.[i-1] = s2.[j-1] then 0 else 1
                d.[i, j] <- min3
                    (d.[i-1, j] + 1)       // deletion
                    (d.[i, j-1] + 1)       // insertion
                    (d.[i-1, j-1] + cost)  // substitution
        d.[len1, len2]

    /// Find similar names (threshold based on name length)
    let findSimilar (target: string) (candidates: string seq) : string list =
        let threshold = max 2 (target.Length / 3)  // 적응형 임계값
        candidates
        |> Seq.map (fun c -> (c, levenshteinDistance target c))
        |> Seq.filter (fun (_, d) -> d <= threshold && d > 0)
        |> Seq.sortBy snd
        |> Seq.map fst
        |> Seq.truncate 3
        |> Seq.toList

    /// Generate "Did you mean?" suggestion
    let didYouMean (name: string) (scope: Map<string, 'a>) : Suggestion option =
        let similar = findSimilar name (Map.keys scope)
        match similar with
        | [] -> None
        | [s] ->
            Some {
                Span = { Start = noPos; End = noPos; ByteStart = None; ByteEnd = None }
                Replacement = s
                Message = sprintf "Did you mean `%s`?" s
                Applicability = MaybeIncorrect
            }
        | s :: _ ->
            Some {
                Span = { Start = noPos; End = noPos; ByteStart = None; ByteEnd = None }
                Replacement = s
                Message = sprintf "Did you mean `%s`?" s
                Applicability = MaybeIncorrect
            }

    /// Common mistake patterns
    type MistakePattern = {
        Detect: Diagnostic -> bool
        Suggest: Diagnostic -> string option
    }

    let commonMistakes = [
        // String + int
        { Detect = fun d ->
            d.Code = Some "E201" &&
            d.Message.Contains("string") && d.Message.Contains("int")
          Suggest = fun _ ->
            Some "`+` requires int operands. For string concatenation, use `++`." }

        // Missing semicolon causing implicit return
        { Detect = fun d ->
            d.Code = Some "E201" &&
            d.Message.Contains("unit") && d.Message.Contains("expected")
          Suggest = fun _ ->
            Some "The expression returns a value, but `unit` was expected. Did you add an extra semicolon?" }

        // Function without arguments
        { Detect = fun d ->
            d.Code = Some "E204"  // Not a function
          Suggest = fun _ ->
            Some "This is a value, not a function. You cannot apply arguments to it." }
    ]
```

### 3.6 Error Explanation System

```fsharp
module ErrorExplanations =
    /// Load explanation for error code
    let getExplanation (code: string) : string option =
        let path = sprintf "docs/errors/%s.md" code
        if System.IO.File.Exists(path) then
            Some (System.IO.File.ReadAllText(path))
        else
            // Fallback to embedded explanations
            embeddedExplanations |> Map.tryFind code

    /// Embedded explanations for common errors
    let private embeddedExplanations = Map.ofList [
        ("E201", """
# E201: Type Mismatch

This error occurs when the type of an expression doesn't match what was expected.

## Common Causes

1. **Wrong argument type:**
   ```funlang
   let add x y = x + y
   add "hello" 1  -- Error: expected int, found string
   ```

2. **Mismatched return type:**
   ```funlang
   let f x = if x > 0 then 1 else "no"  -- Error: branches have different types
   ```

## How to Fix

- Check that argument types match function parameters
- Ensure all branches of `if` expressions have the same type
- Use type conversion functions if needed
""")

        ("E202", """
# E202: Unbound Variable

This error occurs when you reference a variable that hasn't been defined.

## Common Causes

1. **Typo in variable name:**
   ```funlang
   let length xs = ...
   lenght [1; 2; 3]  -- Error: did you mean `length`?
   ```

2. **Variable out of scope:**
   ```funlang
   let f x =
       let y = x + 1
       y
   y  -- Error: y is not in scope here
   ```

## How to Fix

- Check spelling of variable names
- Ensure the variable is defined before use
- Check that the variable is in scope
""")
    ]

    /// Format explanation for CLI output
    let formatExplanation (code: string) : string =
        match getExplanation code with
        | Some explanation -> explanation
        | None -> sprintf "No explanation available for error code %s" code
```

### 3.7 Error Code Catalog (Expanded)

| Code | Category | Description | Explanation |
|------|----------|-------------|-------------|
| **E001-E099** | **Lexer** | | |
| E001 | Lexer | Unexpected character | Invalid character in source |
| E002 | Lexer | Unterminated string | String literal not closed |
| E003 | Lexer | Invalid escape sequence | Unknown escape in string |
| E004 | Lexer | Invalid number format | Malformed numeric literal |
| **E100-E199** | **Parser** | | |
| E101 | Parser | Unexpected token | Token not expected here |
| E102 | Parser | Missing token | Expected token not found |
| E103 | Parser | Invalid syntax | General syntax error |
| E104 | Parser | Indentation error | Wrong indentation level |
| E105 | Parser | Unclosed delimiter | Missing ), ], or } |
| E106 | Parser | Empty block | Block has no expressions |
| **E200-E299** | **Type** | | |
| E201 | Type | Type mismatch | Expected vs actual type differ |
| E202 | Type | Unbound variable | Variable not defined |
| E203 | Type | Infinite type | Occurs check failed |
| E204 | Type | Not a function | Applying non-function |
| E205 | Type | Arity mismatch | Wrong argument count |
| E206 | Type | Pattern type mismatch | Pattern doesn't match type |
| E207 | Type | Undefined constructor | Unknown type constructor |
| E208 | Type | Duplicate binding | Name bound twice |
| **E300-E399** | **Runtime** | | |
| E301 | Runtime | Division by zero | Divide/modulo by zero |
| E302 | Runtime | Non-exhaustive match | No pattern matched |
| E303 | Runtime | Invalid operation | Type error at runtime |
| E304 | Runtime | Stack overflow | Infinite recursion |

---

## 4. Implementation Phases (Updated)

### Phase 8.1: Diagnostic Type & Builder (Foundation)

**목표:** Rust-style `Diagnostic` 타입과 빌더 API

**작업:**
1. `Diagnostic` 타입 정의 (Severity, LabeledSpan, Suggestion)
2. `Diagnostic` 모듈 빌더 함수
3. 기존 `FunLangError` → `Diagnostic` 변환
4. `TypeError` → `Diagnostic` 변환
5. `ParserWrapper.fs`: `Result<Expr, Diagnostic>`

**신규 파일:** `src/FunLang/Diagnostic.fs`

**복잡도:** Medium

### Phase 8.2: Error Formatter (Rust-style Output)

**목표:** 소스 컨텍스트와 라벨이 포함된 에러 출력

**작업:**
1. `ErrorFormatter` 모듈 구현
2. Primary/Secondary span 렌더링
3. Underline + label 출력
4. Multi-line span 지원
5. 줄 생략 (`...`) 처리
6. Notes/Helps 포맷팅

**신규 파일:** `src/FunLang/ErrorFormatter.fs`

**복잡도:** Medium

### Phase 8.3: AST Position Tracking

**목표:** `Located<'a>` 래퍼로 AST에 span 추가

**작업:**
1. `Located<'a>` 타입 정의
2. `Expr` → `Located<ExprNode>` 변경
3. `Parser.fsy` span 캡처
4. `Interpreter.fs` 패턴 매칭 업데이트
5. `TypeInfer.fs` span 전파

**영향:** Core AST 변경 → 대부분 파일 수정

**복잡도:** High (별도 브랜치 권장)

### Phase 8.4: Smart Suggestions

**목표:** "Did you mean?" 및 common mistake 힌트

**작업:**
1. Levenshtein distance 구현
2. `Suggestions` 모듈 생성
3. 에러 생성 시 유사 이름 검색
4. Common mistake 패턴 매칭
5. `MachineApplicable` 제안 표시

**신규 파일:** `src/FunLang/Suggestions.fs`

**복잡도:** Medium

### Phase 8.5: Error Explanations

**목표:** `funlang --explain E201` 명령

**작업:**
1. `docs/errors/Exxx.md` 파일 생성
2. CLI 옵션 추가 (`--explain`)
3. 에러 메시지에 `= see: funlang --explain Exxx` 추가
4. Embedded fallback explanations

**복잡도:** Low

### Phase 8.6: REPL & Color Integration

**목표:** REPL에서 컬러 에러 표시

**작업:**
1. ANSI 컬러 출력 (error: red, warning: yellow, etc.)
2. `--no-color` 옵션
3. 터미널 감지 (isatty)
4. REPL inline 에러 표시

**복잡도:** Low

### Implementation Order (권장)

```
Phase 8.1 (Diagnostic) ──→ Phase 8.2 (Formatter) ──→ Phase 8.4 (Suggestions)
                                    │                         │
                                    v                         v
                              Phase 8.5 (Explain) ──→ Phase 8.6 (REPL)
                                    │
                                    v
                              Phase 8.3 (AST) [optional, high impact]
```

---

## 5. Examples (Rust-style Output)

### 5.1 Type Mismatch (E201)

```
error[E201]: Type mismatch
  --> input.fun:3:15
   |
2  | let add x y = x + y
   |               ----- `add` defined here with type `int -> int -> int`
3  | add "hello" 1
   |     ^^^^^^^ expected `int`, found `string`
   |
   = note: first argument has type `string`
   = help: `+` operator requires both operands to be `int`
   = see: funlang --explain E201
```

### 5.2 Unbound Variable with Suggestion (E202)

```
error[E202]: Unbound variable `lenght`
  --> input.fun:5:9
   |
5  | let n = lenght [1; 2; 3]
   |         ^^^^^^ not found in this scope
   |
   = help: Did you mean `length`?
   = see: funlang --explain E202
```

### 5.3 Non-exhaustive Match (E302)

```
error[E302]: Non-exhaustive pattern match
  --> input.fun:5:1
   |
5  | match xs with
   | ^^^^^^^^^^^^
6  | | Cons (x, _) -> x
   |
   = note: pattern `Nil` not covered
   = help: Add missing case:
     | Nil -> /* handle empty list */
```

### 5.4 Multi-span Error

```
error[E201]: Type mismatch in if expression
  --> input.fun:3:5
   |
3  |     if x > 0 then
   |        ----- condition is `bool`, ok
4  |         1
   |         - this branch has type `int`
5  |     else
6  |         "negative"
   |         ^^^^^^^^^^ expected `int`, found `string`
   |
   = note: `if` branches must have the same type
   = help: Both branches should return `int`
```

### 5.5 Suggestion with Replacement

```
error[E103]: Expected `in` after let binding
  --> input.fun:2:1
   |
1  | let x = 10
   |           - expected `in` here
2  | x + 1
   | ^ unexpected expression
   |
   = help: Add `in` before the body:
     let x = 10 in
     x + 1
```

---

## 6. API Summary

### 6.1 Creating Diagnostics

```fsharp
// Simple error
Diagnostic.error "E201" "Type mismatch"

// With spans and labels
Diagnostic.error "E201" "Type mismatch"
|> Diagnostic.withPrimarySpan exprSpan "expected `int`, found `string`"
|> Diagnostic.withSecondarySpan defSpan "defined here"
|> Diagnostic.withNote "first argument has wrong type"
|> Diagnostic.withHelp "check argument types match"

// With suggestion
Diagnostic.error "E202" "Unbound variable"
|> Diagnostic.withSuggestion nameSpan "length" "Did you mean `length`?"
```

### 6.2 Formatting Output

```fsharp
// Full format with source
let output = ErrorFormatter.format source config diagnostic

// Compact format (no source)
let compact = ErrorFormatter.formatCompact diagnostic

// REPL format (with colors)
let replOutput = ErrorFormatter.formatRepl source diagnostic
```

### 6.3 Getting Explanations

```fsharp
// CLI: funlang --explain E201
let explanation = ErrorExplanations.formatExplanation "E201"
printfn "%s" explanation
```

---

## 7. Testing Strategy

### 7.1 Snapshot Tests

```fsharp
[<Test>]
let ``E201 type mismatch format`` () =
    let diag =
        Diagnostic.error "E201" "Type mismatch"
        |> Diagnostic.withPrimarySpan span "expected `int`, found `string`"

    let output = ErrorFormatter.format source defaultConfig diag
    Expect.equal output expectedSnapshot "format matches"
```

### 7.2 Property Tests

```fsharp
testProperty "all diagnostics have valid error code format" <| fun (diag: Diagnostic) ->
    match diag.Code with
    | Some code -> Regex.IsMatch(code, @"^E\d{3}$")
    | None -> true

testProperty "suggestions respect Levenshtein threshold" <| fun (target: string) (candidates: string list) ->
    let suggestions = Suggestions.findSimilar target candidates
    suggestions |> List.forall (fun s -> levenshteinDistance target s <= max 2 (target.Length / 3))
```

### 7.3 Golden Files

```
tests/golden/errors/
├── E001-unexpected-char.txt
├── E201-type-mismatch.txt
├── E201-type-mismatch-multispan.txt
├── E202-unbound-variable.txt
├── E202-unbound-with-suggestion.txt
└── E302-non-exhaustive.txt
```

---

## 8. References

- [Rust Compiler Development Guide - Diagnostics](https://rustc-dev-guide.rust-lang.org/diagnostics.html)
- [Rust Error Codes Index](https://doc.rust-lang.org/error_codes/E0308.html)
- [annotate-snippets Goal](https://rust-lang.github.io/rust-project-goals/2024h2/annotate-snippets.html)
- [Ariadne - Rust Diagnostics Library](https://lib.rs/crates/ariadne)
- [Miette - Fancy Diagnostic Library](https://docs.rs/miette)
- [Elm Compiler Errors](https://elm-lang.org/news/compiler-errors-for-humans)

---

## Appendix A: Error Code Quick Reference

```
Lexer (E001-E099)
  E001  Unexpected character
  E002  Unterminated string
  E003  Invalid escape sequence
  E004  Invalid number format

Parser (E100-E199)
  E101  Unexpected token
  E102  Missing token
  E103  Invalid syntax
  E104  Indentation error
  E105  Unclosed delimiter
  E106  Empty block

Type (E200-E299)
  E201  Type mismatch
  E202  Unbound variable
  E203  Infinite type (occurs check)
  E204  Not a function
  E205  Arity mismatch
  E206  Pattern type mismatch
  E207  Undefined constructor
  E208  Duplicate binding

Runtime (E300-E399)
  E301  Division by zero
  E302  Non-exhaustive match
  E303  Invalid operation
  E304  Stack overflow
```

---

## Appendix B: File Changes Summary

| Phase | New Files | Modified Files |
|-------|-----------|----------------|
| 8.1 | `Diagnostic.fs` | `Errors.fs`, `Types.fs`, `ParserWrapper.fs`, `Program.fs` |
| 8.2 | `ErrorFormatter.fs` | `Program.fs`, `Repl.fs` |
| 8.3 | - | `Ast.fs`, `Parser.fsy`, `Interpreter.fs`, `TypeInfer.fs`, tests |
| 8.4 | `Suggestions.fs` | `Diagnostic.fs`, `TypeInfer.fs` |
| 8.5 | `docs/errors/*.md` | `Options.fs`, `Program.fs` |
| 8.6 | - | `Repl.fs`, `ErrorFormatter.fs` |
