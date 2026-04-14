---
created: 2026-04-09
description: FunLang의 fsyacc 파서에 새 문법 규칙을 추가하는 방법과 주의사항
---

# fsyacc 파서 규칙 확장하기

Parser.fsy에 새 문법 규칙을 추가할 때의 패턴과 LALR 충돌 방지 방법.

## The Insight

fsyacc는 LALR(1) 파서 생성기다. 규칙을 추가할 때 shift/reduce 또는 reduce/reduce 충돌이 발생할 수 있다. 충돌은 빌드 시 warning으로 나타나며, 0 warnings를 유지해야 한다. 기존 규칙 변경 시 `SeqExpr` vs `Expr`의 차이가 중요하다.

## Why This Matters

파서 충돌을 무시하면 예상치 못한 파싱 결과가 나온다. 예: Issue #14에서 else 브랜치를 `SeqExpr` → `Expr`로 변경했을 때, `fib (n-1) + fib (n-2)` 같은 이항 연산이 파싱 실패했다. `Expr`은 단일 표현식만 포함하고, `SeqExpr`은 세미콜론 체인을 포함하기 때문이다.

## Recognition Pattern

- 새 키워드나 문법 구조를 추가할 때
- 기존 규칙의 nonterminal을 변경할 때 (예: `Expr` ↔ `SeqExpr`)
- 연산자/선언에 타입 어노테이션 문법을 추가할 때

## The Approach

### Step 1: 기존 패턴 파악

비슷한 기존 규칙을 찾아 패턴을 복사한다.

```bash
# 비슷한 규칙 찾기
grep "LET IDENT MixedParamList" src/FunLang/Parser.fsy
```

### Step 2: 새 규칙 추가

**항상 INDENT/DEDENT 변형을 함께 추가한다** — FunLang은 들여쓰기 기반이므로 모든 body 규칙에 `INDENT body DEDENT` 변형이 필요하다.

```
// 단일 줄
| LET OpName MixedParamList EQUALS SeqExpr
    { let lambda = desugarMixedParams $3 $5 (ruleSpan parseState 1 5)
      LetDecl($2, lambda, ruleSpan parseState 1 5) }
// 들여쓰기 블록
| LET OpName MixedParamList EQUALS INDENT SeqExpr DEDENT
    { let lambda = desugarMixedParams $3 $6 (ruleSpan parseState 1 7)
      LetDecl($2, lambda, ruleSpan parseState 1 7) }
```

### Step 3: 리턴 타입 어노테이션 변형

`COLON TypeExpr` 를 추가할 때는 `Annot` 노드로 래핑한다:

```
| LET OpName MixedParamList COLON TypeExpr EQUALS SeqExpr
    { let body = Annot($7, $5, ruleSpan parseState 1 7)
      let lambda = desugarMixedParams $3 body (ruleSpan parseState 1 7)
      LetDecl($2, lambda, ruleSpan parseState 1 7) }
```

### Step 4: 빌드 및 충돌 확인

```bash
dotnet build src/FunLang/FunLang.fsproj -c Release 2>&1 | grep -i conflict
# 반드시 0건이어야 한다
```

### Step 5: 전체 테스트

```bash
scripts/fslit tests/flt/
# 새 규칙 + 기존 규칙 모두 통과 확인
```

## Example

v14.0에서 연산자 정의에 타입 어노테이션을 추가한 사례:

**Before (ParamList만 지원):**
```
| LET OpName ParamList EQUALS SeqExpr
```

**After (MixedParamList + 리턴 타입):**
```
| LET OpName MixedParamList EQUALS SeqExpr
| LET OpName MixedParamList COLON TypeExpr EQUALS SeqExpr
| AttributeList LET OpName MixedParamList EQUALS SeqExpr
| AttributeList LET OpName MixedParamList COLON TypeExpr EQUALS SeqExpr
```

각각에 INDENT/DEDENT 변형 포함하여 총 8개 규칙 추가.

## 주의사항

**SeqExpr vs Expr:**
- `SeqExpr` = `Expr (SEMICOLON Expr)*` — 세미콜론 체인, 이항 연산 포함
- `Expr` = 단일 표현식 — `+` 같은 이항 연산을 포함하지 않을 수 있음
- **else 브랜치, let body 등에는 반드시 `SeqExpr` 사용**

**`%prec` 지시어:**
- `IF Expr THEN Expr %prec IFTHEN` — if-then-without-else에서 then 브랜치가 후속 문을 greedy하게 소비하지 않도록 제한
- 새 규칙에 `%prec`를 추가할 때는 precedence 테이블과의 상호작용을 확인

## 체크리스트

- [ ] INDENT/DEDENT 변형 포함
- [ ] 리턴 타입 어노테이션 변형 포함 (필요 시)
- [ ] `ruleSpan` 인덱스가 토큰 수와 일치
- [ ] `$N` 참조가 올바른 위치
- [ ] `dotnet build` 0 warnings (LALR 충돌 없음)
- [ ] flt 테스트 추가
- [ ] 전체 테스트 통과
