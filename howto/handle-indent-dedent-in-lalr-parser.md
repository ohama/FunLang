---
created: 2026-03-20
description: Python/F# 스타일 들여쓰기를 LALR(1) 파서에 적용하는 IndentFilter 패턴
---

# LALR(1) 파서에서 INDENT/DEDENT 처리하기

Lexer와 Parser 사이에 IndentFilter를 넣어 NEWLINE(col)을 INDENT/DEDENT로 변환하는 패턴.

## The Insight

LALR(1) 파서는 들여쓰기를 모른다. 하지만 lexer가 `NEWLINE(column)` 토큰을 내보내고, 파서에 들어가기 전에 **토큰 필터**가 이를 `INDENT`/`DEDENT`/`IN` 같은 구조적 토큰으로 변환하면, 파서는 `{}`나 `;`를 쓰는 언어처럼 문법을 정의할 수 있다.

핵심: **Lexer → IndentFilter → Parser** 3단계 파이프라인.

## Why This Matters

직접 파서 문법에 들여쓰기를 넣으려고 하면:
- LALR(1) 문법이 ambiguous해져서 shift/reduce conflict 폭발
- 들여쓰기 레벨을 파서 상태로 추적하면 상태 수가 기하급수적으로 증가
- 에러 복구가 사실상 불가능

IndentFilter 패턴을 쓰면:
- 파서 문법은 `INDENT Expr DEDENT` 같은 단순한 규칙
- 들여쓰기 처리와 파싱이 완전히 분리
- Python, F#, Haskell 모두 이 접근법 사용

## Recognition Pattern

- fslex/fsyacc, ocamlyacc, PLY 등 LALR 파서 생성기를 쓸 때
- 들여쓰기 기반 문법을 구현할 때
- "브레이스 언어를 먼저 만들고 들여쓰기를 나중에 추가"하고 싶을 때

## The Approach

### Step 1: Lexer에서 NEWLINE(col) 토큰 생성

Lexer는 줄바꿈을 만나면 다음 줄의 첫 번째 비공백 문자 column을 기록한다.

```fsharp
// Lexer 규칙 (fslex)
| newline whitespace* {
    let col = (lexbuf.LexemeLength - 1)  // 공백 수 = column
    NEWLINE(col)
}
```

빈 줄, 주석만 있는 줄은 NEWLINE을 생성하지 않는다 (또는 필터에서 무시).

### Step 2: Indent Stack으로 INDENT/DEDENT 변환

```fsharp
let processNewline (state: FilterState) (col: int) : FilterState * token list =
    let rec unwind acc stack =
        match stack with
        | top :: rest when col < top ->
            unwind (DEDENT :: acc) rest     // col < top: DEDENT, pop
        | top :: _ when col = top ->
            (List.rev acc, stack)            // col = top: 같은 레벨
        | top :: _ when col > top && List.isEmpty acc ->
            ([INDENT], col :: stack)         // col > top: INDENT, push
        | _ ->
            raise (IndentationError "invalid indentation")
    unwind [] state.IndentStack
```

규칙:
- `col > top` → INDENT (col을 스택에 push)
- `col = top` → 아무것도 안 함 (같은 블록 계속)
- `col < top` → DEDENT (top을 pop, 반복)

DEDENT는 여러 개 연속 발생할 수 있다 (다단계 dedent).

### Step 3: Parser 문법에서 INDENT/DEDENT 사용

```fsharp
// Parser 규칙 (fsyacc)
Expr:
    | LET IDENT EQUALS Expr IN Expr       { LetIn($2, $4, $6) }
    | IF Expr THEN Expr ELSE Expr         { If($2, $4, $6) }
    | FUN IDENT ARROW Expr                { Lambda($2, $4) }
    | FUN IDENT ARROW INDENT Expr DEDENT  { Lambda($2, $5) }  // multiline lambda
    | INDENT Expr DEDENT                  { $2 }               // indented block
    | ...
```

`INDENT Expr DEDENT`는 "들여쓰기된 블록"을 의미하며, 브레이스 `{ Expr }`와 동등하다.

### Step 4: Context-Aware 토큰 삽입 (심화)

단순 INDENT/DEDENT 외에, 문맥에 따라 추가 토큰을 삽입할 수 있다:

```
NEWLINE(col) 처리 순서:
1. Indent stack 업데이트 → INDENT 또는 DEDENT 생성
2. Context stack 확인 → offside rule에 따라 IN 등 삽입
3. Match/Try pipe alignment 검증
```

이 순서가 중요하다. INDENT/DEDENT가 먼저 결정되고, 그 위에 offside rule이 동작한다.

### Step 5: EOF 처리

파일 끝에서 열린 모든 indent를 닫아야 한다:

```fsharp
| Parser.EOF ->
    // 남은 context의 implicit 토큰 삽입
    emitPendingTokens()
    // 모든 열린 indent에 대해 DEDENT 생성
    while state.IndentStack.Length > 1 do
        let (newState, tokens) = processNewline state 0
        state <- newState
        yield! tokens
    yield Parser.EOF
```

col=0으로 processNewline을 호출하면 모든 DEDENT가 자동 생성된다.

## Example

입력:
```
let f x =
    let y = x + 1
    y * 2
f 10
```

Lexer 출력:
```
LET IDENT("f") IDENT("x") EQUALS NEWLINE(4)
LET IDENT("y") EQUALS IDENT("x") PLUS NUMBER(1) NEWLINE(4)
IDENT("y") TIMES NUMBER(2) NEWLINE(0)
IDENT("f") NUMBER(10) EOF
```

IndentFilter 출력:
```
LET IDENT("f") IDENT("x") EQUALS INDENT
LET IDENT("y") EQUALS IDENT("x") PLUS NUMBER(1) IN    ← offside rule
IDENT("y") TIMES NUMBER(2) DEDENT IN                    ← DEDENT + offside rule
IDENT("f") NUMBER(10) EOF
```

Parser가 보는 토큰 스트림은 명시적 구조를 가진다.

## 체크리스트

- [ ] Lexer가 정확한 column을 계산하는지 (탭 처리 주의)
- [ ] 빈 줄이 NEWLINE을 생성하지 않는지
- [ ] DEDENT가 스택의 기존 레벨에만 맞는지 (중간 레벨 금지)
- [ ] EOF에서 모든 열린 indent가 닫히는지
- [ ] 괄호 안에서는 NEWLINE을 무시하는지 (구현에 따라)

## 관련 문서

- `implement-offside-rule-with-context-stack.md` - INDENT/DEDENT 위에 offside rule 추가
