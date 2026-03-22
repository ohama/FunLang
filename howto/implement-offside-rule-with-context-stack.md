---
created: 2026-03-20
description: Counter 대신 context stack으로 F# 스타일 offside rule 구현하기
---

# Context Stack으로 Offside Rule 구현하기

들여쓰기 기반 언어에서 implicit `in`(또는 implicit semicolon)을 context stack으로 구현하는 접근법.

## The Insight

Offside rule은 "토큰의 column이 선언의 시작 column 이하이면 그 선언은 끝난 것"이라는 단순한 규칙이다. 이걸 counter로 구현하면 깨지고, context stack으로 구현하면 중첩과 문맥 구분이 자연스럽게 해결된다.

핵심: **각 let 선언이 자신의 offside column을 기억**하고, 새 토큰이 그 column에 도달하면 스코프가 끝난다.

## Why This Matters

Counter 접근법(`LetSeqDepth: int`)의 문제:
- 중첩 let은 depth를 올리지만, "어느 depth에서 IN을 삽입할지" 판단 불가
- module-level let vs expression-level let을 구분하려면 별도 플래그(`InModuleEquals`) 필요
- 플래그가 늘어나면서 edge case마다 깨짐

## Recognition Pattern

- 들여쓰기로 스코프를 결정하는 언어를 구현할 때
- `let x = 1 / let y = 2 / x + y` 같은 implicit separator가 필요할 때
- Python의 INDENT/DEDENT + 추가로 "같은 레벨에서의 선언 종료"가 필요할 때

## The Approach

F# 컴파일러의 LexFilter.fs(~2800줄)에서 핵심만 추출한 패턴:

1. **Context type 정의** — 각 context가 자신의 offside column을 가짐
2. **LET 토큰 시** — expression context이면 `InLetDecl(blockLet=true, offsideCol)` push
3. **NEWLINE 처리 시** — 다음 토큰의 column이 offsideCol 이하이면 IN 삽입 + context pop
4. **DEDENT 시** — offside column 아래로 내려가면 IN 삽입 + context pop
5. **명시적 IN 시** — 해당 InLetDecl을 pop (호환성 유지)

### Step 1: Context 타입 정의

```fsharp
type SyntaxContext =
    | TopLevel                                      // 최상위 (implicit IN 없음)
    | InLetDecl of blockLet: bool * offsideCol: int // let 선언, offside column 기억
    | InExprBlock of baseColumn: int                // expression block (let RHS, lambda body)
    | InModule                                      // module body (implicit IN 없음)
    | InMatch of baseColumn: int                    // match expression
    | InTry of baseColumn: int                      // try-with expression
```

핵심은 `InLetDecl`의 두 필드:
- `blockLet`: `true`면 expression context (implicit IN 삽입), `false`면 module/top-level (삽입 안 함)
- `offsideCol`: 이 let이 시작된 indent level — 여기까지 돌아오면 스코프 종료

### Step 2: Expression Context 판별 함수

```fsharp
let isExprContext (ctx: SyntaxContext list) : bool =
    match ctx with
    | InLetDecl _ :: _ -> true      // 다른 let 안의 let
    | InExprBlock _ :: _ -> true    // = 또는 -> 뒤의 블록
    | InMatch _ :: _ -> true        // match clause body
    | InTry _ :: _ -> true          // try body
    | InModule :: _ -> false        // module 안 → declaration
    | TopLevel :: _ -> false        // 최상위 → declaration
    | _ -> false
```

이 함수가 counter 접근법의 모든 플래그를 대체한다.

### Step 3: LET 토큰 처리

```fsharp
| Parser.LET ->
    let blockLet = state.IndentStack.Length > 1 && isExprContext state.Context
    if blockLet then
        let offsideCol = state.IndentStack.Head
        state <- { state with Context = InLetDecl(true, offsideCol) :: state.Context }
    yield token
```

두 가지 조건:
1. `IndentStack.Length > 1` — 최상위가 아님 (들여쓰기된 블록 안)
2. `isExprContext` — expression context에 있음 (module이 아님)

### Step 4: NEWLINE에서 Offside 검사

```fsharp
// 같은 레벨에서 offside 검사 (INDENT/DEDENT 없을 때)
let rec checkOffside ctx acc =
    match ctx with
    | InLetDecl(true, offsideCol) :: rest when col <= offsideCol ->
        checkOffside rest (Parser.IN :: acc)  // IN 삽입, context pop
    | _ -> (ctx, List.rev acc)

// DEDENT 시 offside 검사
let rec checkOffsideDedent ctx acc =
    match ctx with
    | InLetDecl(true, offsideCol) :: rest when newIndent <= offsideCol ->
        checkOffsideDedent rest (Parser.IN :: acc)
    | _ -> (ctx, List.rev acc)
```

중첩된 InLetDecl이 여러 개 있으면 안쪽부터 순서대로 IN이 삽입된다.

### Step 5: INDENT 시 Expression Block Push

```fsharp
if List.contains Parser.INDENT emitted then
    match state.PrevToken with
    | Some Parser.EQUALS | Some Parser.ARROW | Some Parser.IN ->
        let baseCol = match state.IndentStack with _ :: parent :: _ -> parent | _ -> 0
        state <- { state with Context = InExprBlock(baseCol) :: state.Context }
    | _ -> ()
```

`=`, `->`, `in` 뒤에 INDENT가 오면 expression block 진입.
Module 뒤의 INDENT는 `InModule`을 push (별도 처리).

## Example

```
let result =          ← InLetDecl(true, offsideCol=0) push
    let a = 10        ← InLetDecl(true, offsideCol=4) push
    let b = 20        ← col=4, offsideCol=4 → IN 삽입, InLetDecl(a) pop
                        ← InLetDecl(true, offsideCol=4) push (b용)
    a + b             ← col=4, offsideCol=4 → IN 삽입, InLetDecl(b) pop
result                ← col=0, offsideCol=0 → IN 삽입, InLetDecl(result) pop
```

토큰 스트림: `LET result = INDENT LET a = 10 IN LET b = 20 IN a + b DEDENT IN result`

파서는 explicit `in`이 있는 것처럼 처리한다.

## 체크리스트

- [ ] Module-level let은 blockLet=false (IN 삽입 안 됨)
- [ ] 중첩 let은 안쪽부터 IN 삽입
- [ ] 명시적 `in`이 있으면 implicit IN 삽입 안 함
- [ ] DEDENT 시에도 offside 검사 (블록 나갈 때)
- [ ] EOF 시 남은 InLetDecl 전부 IN 삽입

## 관련 문서

- `handle-indent-dedent-in-lalr-parser.md` - INDENT/DEDENT 토큰 생성의 기반
