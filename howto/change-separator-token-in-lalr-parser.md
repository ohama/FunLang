---
created: 2026-03-22
description: LALR(1) 문법에서 리스트/튜플 구분자를 다른 토큰으로 변경하는 전략
---

# LALR(1) 파서에서 구분자 토큰 변경하기

같은 구분자(`,`)를 서로 다른 문맥(리스트 vs 튜플)에서 다르게 처리하려면, 별도의 nonterminal을 만들어 토큰을 분리한다.

## The Insight

LALR(1) 파서에서 구분자를 변경할 때 핵심은 "하나의 ExprList 규칙을 공유하지 않는 것"이다. 리스트가 SEMICOLON을 쓰고 튜플이 COMMA를 쓴다면, 각각 별도의 nonterminal(`SemiExprList` vs `ExprList`)이 필요하다. 기존 nonterminal을 수정하면 다른 문맥이 깨진다.

## Why This Matters

`[1, 2, 3]`을 `[1; 2; 3]`으로 바꾸면서 `(1, 2, 3)` 튜플은 유지해야 하는 상황. ExprList 규칙의 COMMA를 SEMICOLON으로 바꾸면 튜플도 깨진다. 새 규칙을 만들지 않고 하나의 규칙을 수정하려는 유혹에 빠지기 쉽다.

## Recognition Pattern

- 같은 구분자가 여러 문맥에서 사용되는 언어의 문법을 수정할 때
- 리스트 `[]`와 튜플 `()` 또는 레코드 `{}`가 다른 구분자를 써야 할 때
- 기존 토큰이 이미 다른 문법 규칙에서 선언되어 있을 때

## The Approach

### Step 1: 기존 토큰 재사용 가능한지 확인

변경하려는 토큰이 이미 lexer/parser에 선언되어 있는지 확인한다. 예: SEMICOLON이 레코드 필드 구분자로 이미 존재하면, lexer 변경 없이 바로 parser 규칙에서 사용 가능하다.

```bash
grep "SEMICOLON" Parser.fsy  # 이미 선언되어 있나?
grep "';'" Lexer.fsl          # lexer에서 이미 매핑되어 있나?
```

### Step 2: 별도 nonterminal 생성

기존 규칙을 수정하지 않고, 새 토큰을 사용하는 별도의 nonterminal을 만든다.

```
// 기존: COMMA-separated (튜플 전용으로 유지)
ExprList:
    | Expr                        { [$1] }
    | Expr COMMA ExprList         { $1 :: $3 }

// 신규: SEMICOLON-separated (리스트 전용)
SemiExprList:
    | Expr                            { [$1] }
    | Expr SEMICOLON SemiExprList     { $1 :: $3 }
```

### Step 3: 사용처만 교체

리스트 리터럴 규칙만 새 nonterminal을 참조하도록 변경한다:

```
// Before
| LBRACKET Expr COMMA ExprList RBRACKET  { List($2 :: $4) }

// After
| LBRACKET Expr SEMICOLON SemiExprList RBRACKET  { List($2 :: $4) }
```

튜플, 레코드 등 다른 규칙은 건드리지 않는다.

### Step 4: 출력 포맷터도 변경

파서만 바꾸면 파싱은 되지만 출력이 여전히 옛 구분자를 사용한다. AST printer와 value printer 양쪽을 업데이트한다.

```fsharp
// Eval.fs — ListValue만 변경, TupleValue는 유지
| ListValue values -> sprintf "[%s]" (String.concat "; " (List.map formatValue values))
| TupleValue values -> sprintf "(%s)" (String.concat ", " (List.map formatValue values))
```

## Example

LangThree에서 실제로 수행한 변경:

```
파일 3개, 변경 ~15줄:
1. Parser.fsy: SemiExprList 규칙 추가 (6줄), 리스트 규칙 COMMA→SEMICOLON (1줄)
2. Format.fs: List case의 String.concat ", " → "; " (1줄)
3. Eval.fs: ListValue case의 String.concat ", " → "; " (1줄)
```

SEMICOLON 토큰은 레코드 구문에서 이미 존재했기 때문에 lexer 변경 0줄.

## 체크리스트

- [ ] 변경 대상 토큰이 이미 선언되어 있는지 확인
- [ ] 새 nonterminal 생성 (기존 규칙 수정 아님)
- [ ] 리스트 규칙만 새 nonterminal 참조
- [ ] 튜플/레코드 규칙은 건드리지 않음
- [ ] AST printer와 value printer 양쪽 업데이트
- [ ] shift/reduce conflict 변화 확인 (보통 없음)

## 관련 문서

- `handle-indent-dedent-in-lalr-parser.md` - LALR 파서의 토큰 필터링 패턴
- `write-bracket-stack-transform-script.md` - 구분자 변경 후 테스트/문서 일괄 변환
