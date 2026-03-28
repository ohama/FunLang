---
created: 2026-03-28
description: 이미 구분자로 쓰이는 토큰(;)을 시퀀싱 연산자로 추가할 때 LALR 충돌을 피하는 SeqExpr 패턴
---

# LALR 충돌 없이 세미콜론 시퀀싱 추가하기

세미콜론(`;`)이 이미 리스트/레코드 구분자로 쓰이는 문법에서, `e1; e2` 표현식 시퀀싱을 추가하려면 별도의 `SeqExpr` nonterminal을 만들어 "문장 위치"에서만 시퀀싱을 허용한다.

## The Insight

`Expr SEMICOLON Expr`를 `Expr` 규칙에 직접 추가하면, `[1; 2; 3]`에서 shift/reduce 충돌이 발생한다. 파서가 `[1`까지 본 후 `;`를 만나면 "리스트 구분자"인지 "시퀀싱 시작"인지 결정할 수 없다. 해결책은 **문법 구조로 문맥을 구분하는 것**: 시퀀싱이 허용되는 위치(함수 본체, match arm, let-in 본체)와 허용되지 않는 위치(리스트 내부, 레코드 필드)를 별도 nonterminal로 나눈다. OCaml의 production parser(`parser.mly`)가 `seq_expr` / `fun_seq_expr`로 이 문제를 정확히 같은 방식으로 해결한다.

## Why This Matters

나이브하게 `Expr`에 시퀀싱을 추가하면:
- `[1; 2; 3]` 파싱이 깨진다 (shift/reduce conflict)
- `{x = 1; y = 2}` 레코드 리터럴이 깨진다
- `%prec` 선언으로는 해결 불가 — 충돌이 문맥 의존적(괄호 안 vs 바깥)이라 우선순위로 구분할 수 없다

## Recognition Pattern

- 하나의 토큰이 **구분자**와 **연산자** 두 역할을 해야 할 때
- LALR(1) 파서에서 같은 토큰의 의미가 위치에 따라 달라져야 할 때
- `e1; e2` 시퀀싱, `e1, e2` 튜플+함수인자 같은 이중 용도 토큰

## The Approach

핵심 원리: "시퀀싱이 허용되는 위치"를 별도 nonterminal `SeqExpr`로 분리하고, 나머지는 기존 `Expr`를 유지한다.

### Step 1: SeqExpr nonterminal 추가

`Expr` 규칙과 별도로 `SeqExpr`를 정의한다:

```
SeqExpr:
    | Expr SEMICOLON SeqExpr    -- e1; e2 (오른쪽 결합)
    | Expr SEMICOLON            -- trailing semicolon 허용
    | Expr                      -- 단일 표현식
```

시퀀싱 액션은 기존 AST 노드로 desugar한다:
```fsharp
| Expr SEMICOLON SeqExpr
    { LetPat(WildcardPat(span), $1, $3, fullSpan) }
    // e1; e2 → let _ = e1 in e2
```

### Step 2: "문장 위치"를 SeqExpr로 교체

문법 전체에서 "이 Expr는 본체/결과 위치다"라는 곳을 `SeqExpr`로 바꾼다:

| 위치 | 예시 | 변경 |
|------|------|------|
| `start` 규칙 | `Expr EOF` → `SeqExpr EOF` | 최상위 |
| `INDENT _ DEDENT` 블록 | 함수/let 본체 | `INDENT SeqExpr DEDENT` |
| `IN _` 뒤 (let-in 본체) | `let x = 1 in _` | `IN SeqExpr` |
| if-then-else 브랜치 | `THEN _ ELSE _` | `THEN SeqExpr ELSE SeqExpr` |
| lambda 본체 | `FUN x ARROW _` | `ARROW SeqExpr` |
| match arm 본체 | `PIPE pat ARROW _` | `ARROW SeqExpr` |
| try 본체/핸들러 | `TRY _ WITH` | `TRY SeqExpr WITH` |
| Decl RHS | `LET x = _` | `LET x = SeqExpr` |

### Step 3: 구분자 문맥은 Expr 유지

이 위치들은 절대 `SeqExpr`로 바꾸지 않는다:

- `LBRACKET Expr SEMICOLON SemiExprList RBRACKET` — 리스트 리터럴
- `SemiExprList` — 리스트 원소
- `RecordFieldBindings` — 레코드 필드
- `WHEN Expr` — match guard
- `MATCH Expr WITH` — scrutinee
- `IF Expr THEN` — 조건식
- `Atom` 내부 — 함수 인자

### Step 4: 빌드 후 충돌 확인

```bash
dotnet build src/LangThree/LangThree.fsproj -c Release
```

fsyacc가 SEMICOLON 관련 shift/reduce 충돌을 보고하면, Step 3의 "Expr 유지" 목록에서 누락된 곳이 있다. 에러 메시지가 어떤 규칙에서 충돌하는지 알려주므로 해당 위치를 `Expr`로 되돌린다.

## Example

LangThree의 실제 구현. 30개 규칙을 업데이트했지만 Eval.fs, Bidir.fs, TypeCheck.fs 변경은 0이다:

```fsharp
// Parser.fsy

// SeqExpr: 시퀀싱 허용 위치
SeqExpr:
    | Expr SEMICOLON SeqExpr
        { LetPat(WildcardPat(symSpan parseState 2), $1, $3, ruleSpan parseState 1 3) }
    | Expr SEMICOLON
        { $1 }
    | Expr
        { $1 }

// 사용 예: lambda 본체
| FUN IDENT ARROW SeqExpr    { ... }

// 사용 예: match arm
| PIPE OrPattern ARROW SeqExpr    { ... }

// 리스트는 Expr 유지 → 충돌 없음
| LBRACKET Expr SEMICOLON SemiExprList RBRACKET    { ... }
```

결과: `print "a"; print "b"`는 시퀀싱, `[1; 2; 3]`은 리스트 — 같은 `;` 토큰, 충돌 없음.

## 체크리스트

- [ ] `SeqExpr` nonterminal이 `Expr` 규칙과 별도로 정의됨
- [ ] 모든 "본체 위치"가 `SeqExpr`로 변경됨
- [ ] 리스트/레코드/패턴 문맥은 `Expr` 유지
- [ ] fsyacc 빌드에서 shift/reduce 충돌 없음
- [ ] `[1; 2; 3]` 리스트 리터럴 테스트 통과 (회귀 방지)
- [ ] `e1; e2; e3` 체이닝 테스트 통과

## 관련 문서

- `change-separator-token-in-lalr-parser.md` - 구분자 토큰 변경 전략 (Phase 24에서 `,` → `;`)
