---
created: 2026-03-28
description: 복합 토큰(.[)을 단일 토큰으로 lexing하여 기존 dot-access 문법과 LALR 충돌을 피하는 방법
---

# 단일 토큰으로 인덱싱 구문 추가하기 (DOTLBRACKET)

`arr.[i]` 인덱싱 구문을 추가할 때, `.`과 `[`를 별도 토큰으로 lexing하면 기존 `.IDENT` 필드접근 규칙과 LALR 충돌이 발생한다. `.[`를 하나의 토큰(`DOTLBRACKET`)으로 lexing하면 충돌이 사라진다.

## The Insight

LALR(1) 파서에서 `Atom DOT IDENT`(필드접근)과 `Atom DOT LBRACKET Expr RBRACKET`(인덱싱)이 공존하면, 파서가 `Atom DOT`까지 본 후 다음 토큰이 `IDENT`인지 `LBRACKET`인지에 따라 분기해야 하는데, 이것이 shared prefix로 인한 shift/reduce 충돌을 만든다. **lexer 단계에서 `.[`를 단일 토큰으로 합치면** 파서가 `DOT` 대신 `DOTLBRACKET`을 바로 보게 되므로 분기가 필요 없다. F#도 동일한 접근을 사용한다.

## Why This Matters

`DOT` + `LBRACKET` 두 토큰으로 파싱하면:
- `Atom DOT ...`에서 다음 토큰이 `IDENT`면 필드접근, `LBRACKET`이면 인덱싱 — lookahead로 구분 가능해 보이지만
- fsyacc의 LALR(1) 테이블 생성에서 `Atom DOT` 상태에서 shift/reduce 충돌이 보고된다
- `%prec`으로 해결하려 하면 다른 문맥에서 부작용이 생긴다

## Recognition Pattern

- `prefix.suffix` 형태의 기존 문법에 `prefix.[expr]` 변형을 추가할 때
- 두 토큰의 조합이 기존 규칙의 접두어와 겹칠 때
- F#/OCaml 스타일 `array.[index]` 구문을 LALR(1) 파서에 추가할 때

## The Approach

### Step 1: Lexer에 복합 토큰 추가

fslex에서 `.[`를 단일 토큰으로 정의한다. **반드시 `..`과 `.` 규칙보다 먼저** 배치한다 (longest match가 기본이지만 순서가 의도를 명확하게 한다):

```fsharp
// Lexer.fsl — 순서 중요: .[ → .. → .
| ".["    { DOTLBRACKET }
| ".."    { DOTDOT }
| '.'     { DOT }
```

### Step 2: Parser에서 토큰 선언 및 문법 규칙

```fsharp
// Parser.fsy

%token DOTLBRACKET

// IndexGet — Atom 규칙에 추가 (왼쪽 재귀, 체이닝 가능)
Atom:
    | Atom DOTLBRACKET Expr RBRACKET
        { IndexGet($1, $3, span) }

// IndexSet — Expr 규칙에 추가 (SetField과 동일 위치)
Expr:
    | Atom DOTLBRACKET Expr RBRACKET LARROW Expr
        { IndexSet($1, $3, $6, span) }
```

**왜 get은 Atom, set은 Expr인가:**
- `Atom`에 넣으면 함수 적용보다 우선순위가 높다: `f arr.[i]`가 `f (arr.[i])`로 파싱
- 체이닝이 자동: `matrix.[r].[c]`는 `(matrix.[r]).[c]`로 왼쪽 재귀
- `Expr`에 넣으면 `arr.[i] <- v`가 최상위 표현식으로 파싱 (함수 인자가 되지 않음)
- 이 패턴은 기존 `FieldAccess`(Atom) / `SetField`(Expr)과 정확히 일치

### Step 3: IndentFilter에 bracket depth 추가

`DOTLBRACKET`이 여는 괄호 역할을 하므로, IndentFilter의 bracket depth 추적에 추가한다:

```fsharp
// DOTLBRACKET도 bracket depth에 포함
| Parser.LBRACKET | Parser.LPAREN | Parser.LBRACE | Parser.DOTLBRACKET ->
    state <- { state with BracketDepth = state.BracketDepth + 1 }
```

이렇게 하면 `arr.[\n  i\n]` 같은 멀티라인 인덱스 표현식에서 INDENT/DEDENT 토큰이 삽입되지 않는다.

### Step 4: AST 노드와 타입 체킹

새로운 AST 노드를 추가하고, 타입 체커에서 컬렉션 타입을 구분한다:

```fsharp
// Ast.fs
| IndexGet of collection: Expr * index: Expr * span: Span
| IndexSet of collection: Expr * index: Expr * value: Expr * span: Span

// Bidir.fs — 타입에 따라 분기
| IndexGet (coll, idx, span) ->
    match resolvedCollType with
    | TArray elemTy  -> unify(idxTy, TInt); return elemTy
    | THashtable(k,v) -> unify(idxTy, k); return v
    | other -> error "IndexOnNonCollection"
```

## Example

체이닝이 자동으로 작동하는 2D 배열 예시:

```
let matrix = array_create 2 (array_create 2 0)
let _ = matrix.[0].[1] <- 42
let val = matrix.[0].[1]   // 42
```

파서가 보는 토큰 스트림:
```
IDENT("matrix") DOTLBRACKET INT(0) RBRACKET DOTLBRACKET INT(1) RBRACKET
```

`DOTLBRACKET`이 `DOT`과 별개이므로 `DOT IDENT` 필드접근 규칙과 절대 충돌하지 않는다.

## 체크리스트

- [ ] Lexer에서 `.[`가 `..`과 `.`보다 먼저 정의됨
- [ ] `DOTLBRACKET` 토큰이 Parser에 선언됨
- [ ] IndexGet이 `Atom` 규칙에, IndexSet이 `Expr` 규칙에 배치됨
- [ ] IndentFilter의 bracket depth에 `DOTLBRACKET` 포함
- [ ] `record.field` 필드접근 회귀 테스트 통과
- [ ] `matrix.[r].[c]` 체이닝 테스트 통과
- [ ] `arr.[i] <- v` 쓰기 테스트 통과

## 관련 문서

- `add-sequencing-without-lalr-conflict.md` - 같은 세션의 SeqExpr 패턴
- `resolve-assignment-ambiguity-in-lalr-parser.md` - SetField 문법 규칙 설계
