# FunLang `--emit` Algorithm

## 개요

`--emit` 옵션은 FunLang 소스 코드를 파싱한 후 정규화된 형태로 다시 출력하는 기능이다. 핵심 기능은 **주석 보존(comment preservation)**으로, 원본 소스의 주석을 AST 위치 정보를 활용하여 출력에 그대로 유지한다.

```bash
# stdout으로 출력
dotnet run --project src/FunLang -- source.fun --emit

# 파일로 저장
dotnet run --project src/FunLang -- source.fun --emit output.fun
```

---

## 아키텍처

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           --emit Pipeline                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   Source Code                                                            │
│       │                                                                  │
│       ▼                                                                  │
│   ┌──────────────────────────────────────────────────────────────────┐  │
│   │  Lexer (Lexer.fsl)                                                │  │
│   │  - 토큰화 수행                                                     │  │
│   │  - 주석을 commentBuffer에 저장 (버퍼 메커니즘)                      │  │
│   │  - NEWLINE/EOF 직후 getAndClearComment()로 수집                   │  │
│   └──────────────────────────────────────────────────────────────────┘  │
│       │                                                                  │
│       ▼ (Token * Position) list + Comment list                          │
│   ┌──────────────────────────────────────────────────────────────────┐  │
│   │  Indentation Processor (Indentation.fs)                           │  │
│   │  - INDENT/DEDENT 토큰 삽입                                         │  │
│   │  - 주석은 영향받지 않음 (별도 리스트)                               │  │
│   └──────────────────────────────────────────────────────────────────┘  │
│       │                                                                  │
│       ▼ (Token * Position) list                                         │
│   ┌──────────────────────────────────────────────────────────────────┐  │
│   │  Parser (Parser.fsy)                                               │  │
│   │  - 토큰을 AST로 변환                                               │  │
│   │  - 각 노드에 Position 정보 저장                                    │  │
│   │  - blockToExpr: 위치 전파 (locateFromRest 사용)                   │  │
│   └──────────────────────────────────────────────────────────────────┘  │
│       │                                                                  │
│       ▼ Program (TypeDefs + MainExpr) + Comment list                    │
│   ┌──────────────────────────────────────────────────────────────────┐  │
│   │  Formatter (Formatter.fs)                                          │  │
│   │  - AST Position과 Comment Position 매칭                           │  │
│   │  - Leading/Trailing 주석 분류                                      │  │
│   │  - 연산자 우선순위 기반 괄호 삽입                                   │  │
│   │  - 들여쓰기 기반 출력                                              │  │
│   └──────────────────────────────────────────────────────────────────┘  │
│       │                                                                  │
│       ▼ Formatted String                                                 │
│   stdout 또는 파일로 출력                                                │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 주요 모듈

### 1. CommentCollector.fs - 주석 수집

**파일**: `src/FunLang/CommentCollector.fs`

```fsharp
/// 주석 정보
type Comment = {
    Text: string       // 주석 내용 (// 제외)
    Pos: Position      // 시작 위치
    Kind: CommentKind  // LineComment
}

/// 주석 분류
type CommentAttachment =
    | Leading   // 다음 노드에 속함 (노드 위에 출력)
    | Trailing  // 이전 노드에 속함 (노드 옆에 출력)
```

**주석 분류 규칙:**

| 분류 | 조건 | 예시 |
|------|------|------|
| **Trailing** | 같은 줄에 코드가 있음 | `let x = 1  // comment` |
| **Leading** | 다음 줄에 코드가 있음 | `// comment`<br>`let x = 1` |

---

### 2. Lexer (Lexer.fsl) - 주석 버퍼 메커니즘

FsLex는 한 번에 하나의 토큰만 반환할 수 있다. 따라서 주석과 NEWLINE을 동시에 반환할 수 없어 **버퍼 메커니즘**을 사용한다.

```fsl
{
// 주석 버퍼 (line, column, text)
let mutable private commentBuffer: (string * int * int) option = None

/// 저장된 주석을 가져오고 버퍼 클리어
let getAndClearComment () =
    let result = commentBuffer
    commentBuffer <- None
    result
}

// 주석 규칙
and lineComment startLine startCol acc = parse
    | newline {
        // 주석 내용을 버퍼에 저장
        commentBuffer <- Some (acc.ToString(), startLine, startCol)
        lexbuf.EndPos <- lexbuf.EndPos.NextLine
        NEWLINE
    }
    | _ {
        acc.Append(lexeme lexbuf) |> ignore
        lineComment startLine startCol acc lexbuf
    }
```

**흐름:**
1. `//` 발견 → `lineComment` 규칙 진입
2. 줄 끝까지 내용 수집 → `commentBuffer`에 저장
3. `NEWLINE` 토큰 반환
4. 다음 토큰화 시 `getAndClearComment()`로 주석 수집

---

### 3. ParserWrapper.fs - 주석 포함 파싱

```fsharp
/// Parse with comment collection
let parseProgramWithComments (input: string) : Result<Program * Comment list, string> =
    match tokenizeRawWithComments input with
    | Error e -> Error e.Message
    | Ok (tokensWithPos, comments) ->
        // 인덴테이션 처리 (주석은 별도 리스트)
        match Indentation.processIndentationWithPositions tokensWithPos with
        | Error e -> Error e.Message
        | Ok processedTokens ->
            // 파싱
            match parseProgramWithPositions processedTokens with
            | Error e -> Error e
            | Ok program -> Ok (program, comments)
```

**핵심 포인트:**
- 주석은 토큰 스트림에서 **분리되어 별도 리스트로 관리**
- 파서는 주석을 보지 않음 (문법에 영향 없음)
- 주석은 Position 정보와 함께 보존

---

### 4. Parser.fsy - AST 위치 전파

**문제:** `blockToExpr` 함수가 `noLoc`을 사용하면 중첩된 표현식의 위치가 모두 `(1, 1)`이 됨

**해결:** `locateFromRest` 헬퍼로 다음 블록 아이템의 위치 전파

```fsharp
/// Get the position of the first item in a block item list
let blockItemPos (item: BlockItem) : Position =
    match item with
    | BILet(_, value) -> value.Pos
    | BILetRec(_, value) -> value.Pos
    | BIExpr e -> e.Pos

/// Create a Located value with position from the next block item
let locateFromRest (rest: BlockItem list) (node: 'T) : Located<'T> =
    match rest with
    | first :: _ -> { Node = node; Pos = blockItemPos first }
    | [] -> noLoc node

let rec blockToExpr (items: BlockItem list) : Expr =
    match items with
    | [BIExpr e] -> e.Node
    | BILet(name, value) :: rest ->
        // locateFromRest로 body의 위치를 다음 아이템에서 가져옴
        ELet(name, value, locateFromRest rest (blockToExpr rest))
    | BILetRec(name, value) :: rest ->
        ELetRec(name, value, locateFromRest rest (blockToExpr rest))
    | BIExpr e :: rest ->
        EBlock [e; locateFromRest rest (blockToExpr rest) |> fun x -> { Node = x.Node; Pos = x.Pos }]
    | [] -> ELiteral LUnit
```

**이 수정이 중요한 이유:**

```funlang
// comment1          ← Line 1
let x = 1            ← Line 2
// comment2          ← Line 3
let y = 2            ← Line 4
x + y                ← Line 5
```

수정 전:
- `ELet(x, ...)` → Position Line 2 ✓
- `ELet(y, ...)` → Position Line 1 ✗ (`noLoc` 사용)
- `comment2`가 Line 3인데 `ELet(y)`가 Line 1이라 매칭 실패

수정 후:
- `ELet(x, ...)` → Position Line 2 ✓
- `ELet(y, ...)` → Position Line 4 ✓
- `comment2` (Line 3) < `ELet(y)` (Line 4) → Leading 주석으로 올바르게 매칭

---

### 5. Formatter.fs - 주석 포함 포매팅

**핵심 함수:**

```fsharp
/// 프로그램 포매팅 (주석 포함)
let formatProgramWithComments (program: Program) (comments: Comment list) : string

/// 표현식 재귀 포매팅 (주석 삽입)
let rec formatExprWithCommentsRecursive (indent: int) (lexpr: LExpr) (comments: Comment list) : string
```

**주석 매칭 알고리즘:**

```fsharp
let rec formatExprWithCommentsRecursive indent lexpr comments =
    let exprLine = lexpr.Pos.Line

    // 1. Leading 주석 분리 (현재 표현식 위에 있는 주석)
    let (leadingComments, remainingComments) =
        comments |> List.partition (fun c -> c.Pos.Line < exprLine)

    // 2. Trailing 주석 분리 (같은 줄에 있는 주석)
    let (trailingCommentOpt, childComments) =
        let onSameLine = remainingComments |> List.tryFind (fun c -> c.Pos.Line = exprLine)
        let rest = remainingComments |> List.filter (fun c -> c.Pos.Line <> exprLine)
        (onSameLine, rest)

    // 3. Leading 주석 출력
    let leadingStr =
        leadingComments
        |> List.sortBy (fun c -> c.Pos.Line)
        |> List.map (fun c -> sprintf "%s//%s\n" spaces c.Text)
        |> String.concat ""

    // 4. 표현식 포매팅 (자식 주석 전달)
    let exprStr = match lexpr.Node with
        | ELet ... -> formatLetWithComments indent ... trailingCommentOpt childComments
        | EMatch ... -> formatMatchWithComments indent ... trailingCommentOpt childComments
        | _ -> formatExprIndent indent lexpr + trailing

    leadingStr + exprStr
```

**Let 표현식 주석 처리:**

```fsharp
and formatLetWithComments indent keyword name value body trailingComment comments =
    // Trailing 주석: let 라인 끝에 배치
    let trailingStr = match trailingComment with
        | Some c -> sprintf "  //%s" c.Text
        | None -> ""

    // Value가 multi-line이면 trailing은 = 뒤에
    let valueStr = match value.Node with
        | ELambda _ | EMatch _ -> sprintf "%s\n%s..." trailingStr nextSpaces
        | _ -> sprintf "%s%s" (formatExpr 0 value) trailingStr

    // Body는 재귀 호출 (남은 주석 전달)
    let bodyStr = formatExprWithCommentsRecursive indent body comments

    sprintf "%s %s = %s\n%s" keyword name valueStr bodyStr
```

---

### 6. Program.fs - emit 엔트리 포인트

```fsharp
/// Emit formatted source code with comment preservation
let emitFormatted (opts: RunOptions) (input: string) : bool =
    match opts.EmitPath with
    | None -> false  // No emit requested
    | Some pathOpt ->
        // Parse with comments for comment-aware formatting
        let formatted =
            match parseProgramWithComments input with
            | Ok (program, comments) ->
                Fmt.formatProgramWithComments program comments
            | Error _ ->
                // Fallback: parse without comments
                match parseProgramString input with
                | Ok program -> Fmt.formatProgram program
                | Error _ -> ""

        match pathOpt with
        | None -> printfn "%s" formatted      // stdout
        | Some path ->
            IO.File.WriteAllText(path, formatted)
            printfn "Formatted source written to: %s" path
        true
```

---

## 연산자 우선순위 (괄호 삽입)

Formatter는 AST를 다시 소스 코드로 변환할 때 **연산자 우선순위**에 따라 필요한 괄호만 삽입한다.

```fsharp
let opPrecedence = function
    | Or -> (1, Left)           // 가장 낮음
    | And -> (2, Left)
    | Eq | Neq -> (3, NonAssoc)
    | Lt | Gt | Lte | Gte -> (4, NonAssoc)
    | Add | Sub -> (5, Left)
    | Mul | Div | Mod -> (6, Left)   // 가장 높음

let appPrecedence = 9       // 함수 적용
let atomPrecedence = 10     // 리터럴, 변수 (최고)
let consPrecedence = 7      // :: 연산자
let lambdaPrecedence = 0    // 람다 (최저)
```

**예시:**

```funlang
// 입력
1 + 2 * 3

// AST
EBinaryOp(Add, ELiteral 1, EBinaryOp(Mul, ELiteral 2, ELiteral 3))

// 출력 (괄호 불필요)
1 + 2 * 3
```

```funlang
// 입력
(1 + 2) * 3

// AST
EBinaryOp(Mul, EBinaryOp(Add, ELiteral 1, ELiteral 2), ELiteral 3)

// 출력 (괄호 필요)
(1 + 2) * 3
```

---

## 들여쓰기 기반 포매팅

복합 표현식은 들여쓰기와 함께 multi-line으로 출력된다:

```fsharp
let defaultIndent = 2

let formatLetIndent indent keyword name value body =
    let nextIndent = indent + defaultIndent
    match value.Node with
    | ELambda _ | EMatch _ ->
        // Multi-line: 값을 다음 줄에 들여쓰기
        sprintf "%s %s =\n%s%s\n%s"
            keyword name
            (spaces nextIndent)
            (formatExprIndent nextIndent value)
            (formatExprIndent indent body)
    | _ ->
        // Single-line: 값을 같은 줄에
        sprintf "%s %s = %s\n%s"
            keyword name
            (formatExpr 0 value)
            (formatExprIndent indent body)
```

---

## 테스트

### File-Based 테스트

`tests/file-tests/format-tests/` 디렉토리에 `.test` 파일 추가:

```
// --COMMAND: dotnet run --project src/FunLang -- %s --emit
// --INPUT
// comment1
let x = 1
// comment2
let y = 2
x + y

// --EXPECTED

// comment1
let x = 1
// comment2
let y = 2
x + y
```

### 테스트 실행

```bash
# 모든 format 테스트
dotnet run --project tests/FunLang.Tests -- --filter-test-list "format-tests"

# 특정 테스트
dotnet run --project tests/FunLang.Tests -- --filter-test-case "050-bug-comment-between-lets"
```

---

## 제한사항 및 향후 개선

### 현재 제한사항

1. **Block Comment 미지원**: `/* ... */` 형태의 블록 주석은 아직 지원하지 않음
2. **Doc Comment 미지원**: `///` 형태의 문서 주석은 일반 주석으로 처리
3. **Type Definition 주석**: 타입 정의 내의 주석은 보존되지 않음
4. **Dangling Comment**: 빈 블록 내의 주석 처리 미흡

### 향후 개선 계획

- [ ] Block comment (`/* ... */`) 지원
- [ ] Doc comment (`///`) 특별 처리
- [ ] Type definition 내 주석 보존
- [ ] Dangling comment 처리 개선

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `src/FunLang/Lexer.fsl` | 주석 캡처 (commentBuffer) |
| `src/FunLang/Parser.fsy` | AST 위치 전파 (locateFromRest) |
| `src/FunLang/CommentCollector.fs` | 주석 수집/분류 |
| `src/FunLang/ParserWrapper.fs` | 주석 포함 파싱 API |
| `src/FunLang/Formatter.fs` | 주석 포함 포매팅 |
| `src/FunLang/Program.fs` | --emit 엔트리 포인트 |
