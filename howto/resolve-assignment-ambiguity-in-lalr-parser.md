---
created: 2026-03-27
description: LALR(1) 파서에서 변수 할당(IDENT <-)과 필드 할당(Atom.IDENT <-)의 모호성을 shift 우선으로 해결하는 방법
---

# LALR 파서에서 할당 구문 모호성 해결

변수 할당(`x <- expr`)과 레코드 필드 할당(`obj.field <- expr`)이 같은 `<-` 토큰을 사용할 때, LALR(1) 파서의 기본 shift 우선 규칙으로 자연스럽게 구분된다.

## The Insight

두 할당 구문의 핵심 차이는 **DOT 토큰의 유무**다:

```
x <- 10           -- 변수 할당: IDENT LARROW Expr
obj.field <- 10   -- 필드 할당: Atom DOT IDENT LARROW Expr
```

파서가 `IDENT`를 스택에 올린 후 다음 토큰을 볼 때:
- `LARROW` → shift하여 `IDENT LARROW Expr` 규칙 매칭 (변수 할당)
- `DOT` → `IDENT`를 `Atom`으로 reduce한 후 `Atom DOT IDENT LARROW Expr` 진행 (필드 할당)
- 다른 토큰 → `IDENT`를 `Atom`으로 reduce (일반 변수 참조)

shift-reduce conflict가 발생하지만, fsyacc의 기본 동작(shift 우선)이 정확히 원하는 결과를 준다.

## Why This Matters

이 패턴을 모르면:
- `IDENT LARROW Expr` 규칙을 `Atom` 레벨에 넣어서 파싱 실패
- 별도 토큰(`ASSIGN` vs `SET_FIELD`)을 만들어 lexer를 복잡하게 함
- `<-`를 문맥에 따라 다르게 처리하는 해킹을 시도

## Recognition Pattern

다음 상황에서 이 패턴이 적용된다:
- LALR(1) 파서에 새 구문을 추가하려 함
- 기존 구문과 같은 연산자를 공유하지만, 접두사가 다름
- 1-토큰 lookahead로 구분 가능

## The Approach

### Step 1: 문법 규칙의 레벨 결정

변수 할당 규칙은 **Expr 레벨**에 놓는다 (Atom이 아님):

```
// Parser.fsy — Expr production
Expr:
    | IDENT LARROW Expr           { Assign($1, $3, ...) }  // 변수 할당
    | ... 기존 규칙들 ...

    // 기존 필드 할당 (이미 Expr 레벨)
    | Atom DOT IDENT LARROW Expr  { SetField($1, $3, $5, ...) }
```

### Step 2: Shift-Reduce 분석

파서 상태에서 스택 top이 `IDENT`이고 lookahead가 `LARROW`일 때:

| 선택 | 동작 | 결과 |
|------|------|------|
| **Shift** | `LARROW`를 스택에 push | `IDENT LARROW Expr` 규칙 진행 → 변수 할당 |
| Reduce | `IDENT` → `Atom` | 이후 `LARROW`가 `Atom` 다음에 오므로 `Atom DOT IDENT LARROW` 매칭 불가 (DOT 없음) → 파싱 에러 |

fsyacc 기본: **shift 우선** → 올바른 동작.

### Step 3: 빌드 후 확인

```bash
dotnet build 2>&1 | grep -i "conflict\|warning"
```

"shift-reduce conflict" 경고가 나올 수 있다. 이 특정 conflict는 의도된 것이며, shift 우선이 정확하다. 확인 방법:

```bash
# 변수 할당 파싱 확인
echo 'let mut x = 0 in let _ = x <- 10 in x' | langthree --emit-ast --expr
# Assign("x", ...) 노드가 나와야 함

# 필드 할당 파싱 확인
echo 'let p = {x=1} in let _ = p.x <- 10 in p' | langthree --emit-ast --expr
# SetField(..., "x", ...) 노드가 나와야 함
```

## Example

LangThree의 실제 Parser.fsy에서:

```
// 기존: 레코드 필드 할당 (Atom DOT IDENT LARROW Expr)
| Atom DOT IDENT LARROW Expr     { SetField($1, $3, $5, ruleSpan parseState 1 5) }

// 추가: 변수 할당 (IDENT LARROW Expr)
| IDENT LARROW Expr              { Assign($1, $3, ruleSpan parseState 1 3) }
```

파서는 `x <- 10`에서:
1. `x` (IDENT)를 스택에 올림
2. lookahead = `<-` (LARROW)
3. shift 선택 → `IDENT LARROW` 상태로 진입
4. `10` 파싱 → `Assign("x", IntLit(10))` 생성

`p.x <- 10`에서:
1. `p` (IDENT) → reduce to `Atom`
2. `.` (DOT) shift
3. `x` (IDENT) shift
4. `<-` (LARROW) shift
5. `10` 파싱 → `SetField(Var("p"), "x", IntLit(10))` 생성

DOT의 유무가 두 경로를 자연스럽게 분기한다.

## 체크리스트

- [ ] 새 할당 규칙을 Expr 레벨에 배치 (Atom 아님)
- [ ] 빌드 후 shift-reduce conflict 확인 (LARROW on IDENT)
- [ ] 변수 할당 테스트: `x <- expr` → Assign 노드
- [ ] 필드 할당 테스트: `obj.field <- expr` → SetField 노드 (기존 동작 유지)
- [ ] 기존 테스트 regression 없음 확인

## 관련 문서

- `change-separator-token-in-lalr-parser.md` - LALR 파서에서 토큰 변경 시 conflict 해결
- `handle-indent-dedent-in-lalr-parser.md` - INDENT/DEDENT 토큰 처리
