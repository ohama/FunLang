---
created: 2026-03-23
description: GADT match에서 분기마다 다른 타입을 반환하는 다형적 함수 구현하기
---

# GADT에서 분기별 독립적 타입 반환 구현하기

bidirectional type checker에서 GADT match의 각 분기가 서로 다른 타입을 반환할 수 있게 하려면, 분기 간 substitution 격리가 필요하다.

## The Insight

GADT 타입 정제(type refinement)는 분기별로 독립적이어야 한다. `IntLit` 분기에서 `'a = int`이 되고 `BoolLit` 분기에서 `'a = bool`이 되는 것은 GADT의 핵심 능력이다. 그런데 타입 체커의 check 모드에서 substitution을 분기 간에 축적(accumulate)하면, 첫 분기의 타입 결정이 다음 분기를 오염시킨다.

핵심 구분: **expected 타입이 아직 결정되지 않은 변수인가(다형적), 이미 구체적 타입인가(단형적)**. 이 두 경우를 다르게 처리해야 한다.

## Why This Matters

`eval : 'a Expr -> 'a` 패턴을 구현하려고 synth 모드에서 fresh type variable을 만들어 check로 위임하면, 자연스럽게 작동할 것 같지만 실패한다:

```
1. Branch 1 (IntLit n -> n): bodyS가 freshTy를 int로 고정
2. bodyS가 accumulator s에 합성됨
3. Branch 2 (BoolLit b -> b): apply s expected → 이미 int
4. bool을 int에 맞추려 하면 E0301 타입 불일치
```

에러 메시지: `Type mismatch: expected int but got bool`

## Recognition Pattern

- Bidirectional type checker에서 GADT를 구현할 때
- "같은 함수가 입력에 따라 다른 타입을 반환"하는 패턴이 필요할 때
- OCaml의 `type a.` 또는 Haskell의 `eval :: Expr a -> a`를 구현할 때
- Check 모드에서 fold/accumulate로 분기를 처리하는 구조일 때

## The Approach

### Step 1: Synth 모드에서 GADT match를 fresh var로 check 위임

```fsharp
// synth 모드 Match 분기
| Match (scrutinee, clauses, span) ->
    if isGadtMatch ctorEnv clauses then
        let freshTy = freshVar()
        let s = check ctorEnv recEnv ctx env expr freshTy
        (s, apply s freshTy)
```

이것만으로는 부족하다. check 모드 핸들러가 분기 간 substitution을 축적하기 때문이다.

### Step 2: Check 모드에서 polymorphic expected 감지

```fsharp
// GADT check 모드 핸들러 앞에서
let isPolyExpected =
    match apply s1 expected with
    | TVar _ -> true   // expected가 아직 미결정 변수 → 다형적 모드
    | _ -> false       // expected가 구체적 타입 → 기존 동작
```

`s1`은 scrutinee 추론에서 나온 substitution. `apply s1 expected`가 여전히 `TVar`이면, expected가 외부에서 고정되지 않은 것이다.

### Step 3: 다형적 모드에서 분기별 독립적 expected

```fsharp
// 각 GADT 분기 처리 (folder 함수 내)
if isPolyExpected then
    // 다형적 모드: localS를 원본 expected에 직접 적용
    // accumulator s를 통하지 않으므로 이전 분기의 결정이 전파되지 않음
    let localExpected = apply combinedLocalS expected
    let bodyS = check ctorEnv recEnv clauseCtx branchEnv' body localExpected
    // bodyS를 accumulator에 합성하지만, expected 변수의 바인딩은 격리됨
    (compose bodyS s, idx + 1)
else
    // 구체적 모드: 기존 동작 (accumulator를 통해 expected 해석)
    let refinedExpected = apply sGuard (apply (compose combinedLocalS s) expected)
    let bodyS = check ctorEnv recEnv clauseCtx branchEnv' body refinedExpected
    (compose bodyS s, idx + 1)
```

핵심 차이: `apply combinedLocalS expected` vs `apply (compose combinedLocalS s) expected`

- 다형적 모드: `combinedLocalS`만 적용 → 이 분기의 GADT 정제만 반영
- 구체적 모드: `s`도 함께 적용 → 이전 분기의 결정도 반영

## Example

```
type Expr 'a =
    | IntLit : int -> int Expr
    | BoolLit : bool -> bool Expr

let eval e =
    match e with
    | IntLit n -> n      // localS: 'a → int, localExpected: int, n: int ✓
    | BoolLit b -> b     // localS: 'a → bool, localExpected: bool, b: bool ✓

eval (IntLit 42)    → 42 : int
eval (BoolLit true) → true : bool
```

다형적 모드에서 각 분기의 흐름:

```
Branch 1 (IntLit n -> n):
  combinedLocalS = { 'a → int }
  localExpected = apply { 'a → int } 'a = int
  check body n against int → OK

Branch 2 (BoolLit b -> b):
  combinedLocalS = { 'a → bool }
  localExpected = apply { 'a → bool } 'a = bool  ← s를 안 거치므로 int가 아님!
  check body b against bool → OK
```

## 체크리스트

- [ ] synth 모드에서 GADT match를 fresh var로 check 위임
- [ ] check 모드에서 `isPolyExpected` 플래그로 모드 분기
- [ ] 다형적 모드: `apply combinedLocalS expected` (accumulator 무시)
- [ ] 구체적 모드: 기존 동작 유지 (`apply (compose combinedLocalS s) expected`)
- [ ] 기존 `(match ... : int)` 주석 호환성 테스트
- [ ] `eval (IntLit 42)` → int, `eval (BoolLit true)` → bool 테스트
- [ ] Existential escape check도 다형적 모드 대응

## 관련 문서

- `implement-offside-rule-with-context-stack.md` — 같은 프로젝트의 다른 핵심 패턴
- `handle-indent-dedent-in-lalr-parser.md` — Lexer/Parser 파이프라인 기반
