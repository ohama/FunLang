---
created: 2026-03-28
description: 새 구문을 기존 AST 노드로 desugar하여 Eval/TypeChecker 변경 없이 기능을 추가하는 패턴
---

# Parser-only Desugar로 AST 변경 없이 구문 추가하기

새 구문을 추가할 때, 의미가 기존 AST 노드의 조합으로 표현 가능하면 **파서에서 직접 desugar**한다. AST, Eval, TypeChecker, Format 등 하류 패스의 변경이 0이 된다.

## The Insight

언어에 새 구문을 추가하는 비용은 "파이프라인에서 몇 개 패스를 건드려야 하는가"로 결정된다. 새 AST 노드를 만들면 Eval, Bidir, Infer, Format, TypeCheck 모든 곳에 match arm을 추가해야 한다. 하지만 새 구문의 의미가 **기존 노드의 조합과 동일**하면, 파서의 grammar action에서 기존 노드를 생성하는 것만으로 끝난다. 하류 패스는 이미 그 노드를 처리할 줄 안다.

## Why This Matters

새 AST 노드를 추가하면:
- `Eval.fs` — 새 match arm + 실행 로직
- `Bidir.fs` — 새 match arm + 타입 체킹 로직
- `Infer.fs` — 새 match arm (stub라도)
- `Format.fs` — 새 match arm + 포맷 출력
- `TypeCheck.fs` — 여러 helper 함수에 재귀 arm 추가
- F# 컴파일러가 "incomplete pattern match" 경고

Desugar 방식이면 위의 모든 변경이 0이다. 파서 파일 하나만 수정.

## Recognition Pattern

다음 조건이 **모두** 충족되면 parser desugar가 적합하다:

1. **의미가 동일**: 새 구문 = 기존 구문의 축약형
2. **정보 손실 없음**: desugar 후 원래 구문을 복원할 필요 없음 (에러 메시지, 포매팅 등)
3. **타입 체킹이 동일**: 기존 노드의 타입 규칙이 새 구문에 그대로 적용

반대로, 다음 경우에는 새 AST 노드가 필요하다:
- 에러 메시지에 원래 구문 형태를 보여줘야 할 때
- 타입 체킹 규칙이 기존 노드와 다를 때 (예: `arr.[i]`는 TArray/THashtable 분기 필요)
- 최적화 패스에서 구문을 구분해야 할 때

## The Approach

### Step 1: 대응하는 기존 AST 노드 찾기

새 구문이 어떤 기존 표현식과 동치인지 확인한다:

| 새 구문 | 기존 동치 | 대응 AST 노드 |
|---------|----------|---------------|
| `e1; e2` | `let _ = e1 in e2` | `LetPat(WildcardPat, e1, e2)` |
| `if c then e` | `if c then e else ()` | `If(c, e, Tuple([]))` |
| `f x y` | `(f x) y` | `App(App(f, x), y)` — 이미 존재 |

### Step 2: 문법 규칙의 action에서 기존 노드 생성

```fsharp
// 예: e1; e2 → LetPat(WildcardPat, e1, e2)
SeqExpr:
    | Expr SEMICOLON SeqExpr
        { LetPat(WildcardPat(symSpan parseState 2), $1, $3, ruleSpan parseState 1 3) }

// 예: if cond then expr → If(cond, expr, Tuple([]))
Expr:
    | IF Expr THEN SeqExpr
        { If($2, $4, Tuple([], symSpan parseState 4), ruleSpan parseState 1 4) }
```

### Step 3: 타입 체킹이 자연스러운지 검증

desugar 결과가 기존 타입 체커에서 올바르게 처리되는지 확인:

- `LetPat(WildcardPat, e1, e2)`: 이미 `let _ = e1 in e2`로 체크됨. e1은 아무 타입, 결과는 e2 타입. ✓
- `If(cond, thenExpr, Tuple([]))`: then/else 타입 통합. then이 unit이면 통과, int이면 `int ≠ unit` 에러 발생. ✓ (IFTHEN-02 무료)

### Step 4: 에러 메시지 품질 확인

desugar의 단점은 에러 메시지에 "원래 구문"이 보이지 않을 수 있다는 것. 예:

```
// if true then 42 → 에러 메시지:
error[E0301]: Type mismatch: expected int but got unit
                              ^^^^^^^^^^^^^^^^^^^^^^^^
                              if-then-else 통합 에러 (else가 unit이므로)
```

이 에러가 사용자에게 충분히 이해 가능한지 판단한다. "expected int but got unit"이면 "else가 없어서 unit인데 then이 int"라고 추론 가능 — 수용 가능.

만약 에러가 혼란스러우면 새 AST 노드 + 전용 에러 메시지가 필요하다.

## Example

LangThree v5.0에서 실제로 적용한 2가지 사례:

### 사례 1: 표현식 시퀀싱 (`e1; e2`)

```fsharp
// Parser.fsy — SeqExpr nonterminal
| Expr SEMICOLON SeqExpr
    { LetPat(WildcardPat(symSpan parseState 2), $1, $3, ruleSpan parseState 1 3) }
```

변경 파일: `Parser.fsy` 1개. Eval/Bidir/Infer/Format/TypeCheck 변경 0개.

### 사례 2: else 없는 if (`if cond then expr`)

```fsharp
// Parser.fsy — 기존 if-then-else 규칙 바로 뒤에 추가
| IF Expr THEN SeqExpr
    { If($2, $4, Tuple([], symSpan parseState 4), ruleSpan parseState 1 4) }
```

변경 파일: `Parser.fsy` 1개. 나머지 6개 파일 변경 0개.

### 비교: 새 AST 노드가 필요했던 사례 (IndexGet)

`arr.[i]`는 desugar로 해결할 수 없었다:
- `array_get arr i`로 desugar하려면 파서 시점에서 `arr`가 배열인지 해시테이블인지 알아야 함
- 타입 정보 없이는 `array_get` vs `hashtable_get` 선택 불가
- → 새 `IndexGet` AST 노드 + Bidir.fs에서 타입 기반 분기 필요

## 체크리스트

- [ ] 새 구문의 의미가 기존 AST 노드 조합과 완전히 동일한가?
- [ ] desugar 후 타입 체킹이 올바르게 작동하는가?
- [ ] 에러 메시지가 사용자에게 이해 가능한가?
- [ ] 포매터/프리티프린터에서 원래 구문 복원이 불필요한가?
- [ ] 빌드 후 모든 기존 테스트 통과 (회귀 없음)?

## 관련 문서

- `add-sequencing-without-lalr-conflict.md` - SeqExpr desugar의 LALR 충돌 회피 측면
- `change-separator-token-in-lalr-parser.md` - 토큰 역할 변경 시 nonterminal 분리 패턴
