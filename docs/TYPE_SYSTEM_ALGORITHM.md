# Hindley-Milner Type System Algorithm

FunLang의 타입 시스템은 Hindley-Milner (HM) 타입 추론 알고리즘을 기반으로 합니다.

## 1. 개요

HM 타입 시스템은 ML 계열 언어(OCaml, Haskell, F#)의 타입 추론 기반입니다.

**핵심 특징:**
- 타입 어노테이션 없이 타입 추론
- Let-polymorphism (다형성)
- Principal type (가장 일반적인 타입 추론)
- 결정적(deterministic) 알고리즘

## 2. 타입 정의

### 2.1 Monotype (단형 타입)

```
τ ::= int                    -- 정수 타입
    | bool                   -- 불리언 타입
    | string                 -- 문자열 타입
    | unit                   -- 유닛 타입
    | α                      -- 타입 변수
    | τ₁ → τ₂                -- 함수 타입
    | list τ                 -- 리스트 타입
    | (τ₁, τ₂, ...)          -- 튜플 타입
```

### 2.2 Polytype / Type Scheme (다형 타입)

```
σ ::= τ                      -- 단형 타입
    | ∀α₁...αₙ. τ            -- 양화된 타입
```

예시:
- `∀α. α → α` : identity 함수의 타입
- `∀α. list α → int` : length 함수의 타입

## 3. 핵심 연산

### 3.1 Substitution (치환)

타입 변수를 타입으로 대체하는 매핑:

```
S = [α₁ ↦ τ₁, α₂ ↦ τ₂, ...]
```

**적용:**
```
S(int) = int
S(α) = S(α) if α ∈ dom(S), else α
S(τ₁ → τ₂) = S(τ₁) → S(τ₂)
S(list τ) = list S(τ)
```

**합성:**
```
(S₁ ∘ S₂)(τ) = S₁(S₂(τ))
```

### 3.2 Free Type Variables (자유 타입 변수)

```
FV(int) = ∅
FV(α) = {α}
FV(τ₁ → τ₂) = FV(τ₁) ∪ FV(τ₂)
FV(∀α.τ) = FV(τ) - {α}
FV(Γ) = ∪{FV(σ) | x:σ ∈ Γ}
```

### 3.3 Generalization (일반화)

타입을 타입 스킴으로 변환:

```
generalize(Γ, τ) = ∀α₁...αₙ.τ
  where {α₁,...,αₙ} = FV(τ) - FV(Γ)
```

환경에 없는 자유 변수만 양화됩니다.

### 3.4 Instantiation (인스턴스화)

타입 스킴에서 새로운 타입 인스턴스 생성:

```
instantiate(∀α₁...αₙ.τ) = [α₁↦β₁,...,αₙ↦βₙ]τ
  where β₁,...,βₙ are fresh type variables
```

## 4. Unification Algorithm (통합 알고리즘)

두 타입을 같게 만드는 치환을 찾습니다.

```
unify : Type → Type → Result<Substitution, Error>
```

### 4.1 규칙

```
unify(α, τ) =
    if α = τ then ∅
    else if α ∈ FV(τ) then Error "occurs check"
    else [α ↦ τ]

unify(τ, α) = unify(α, τ)

unify(int, int) = ∅
unify(bool, bool) = ∅
unify(string, string) = ∅

unify(τ₁ → τ₂, τ'₁ → τ'₂) =
    S₁ = unify(τ₁, τ'₁)
    S₂ = unify(S₁(τ₂), S₁(τ'₂))
    return S₂ ∘ S₁

unify(list τ, list τ') = unify(τ, τ')

unify((τ₁,...,τₙ), (τ'₁,...,τ'ₙ)) =
    S₁ = unify(τ₁, τ'₁)
    S₂ = unify(S₁(τ₂), S₁(τ'₂))
    ...
    return Sₙ ∘ ... ∘ S₁

unify(τ₁, τ₂) = Error "type mismatch" otherwise
```

### 4.2 Occurs Check

무한 타입 방지:
```
unify(α, list α)  -- Error: α = list α = list (list α) = ...
```

## 5. Algorithm W (타입 추론)

```
W : TypeEnv → Expr → Result<Substitution × Type, Error>
```

### 5.1 리터럴

```
W(Γ, n)     = (∅, int)      where n is integer
W(Γ, true)  = (∅, bool)
W(Γ, false) = (∅, bool)
W(Γ, "s")   = (∅, string)
W(Γ, ())    = (∅, unit)
```

### 5.2 변수

```
W(Γ, x) =
    if x ∉ dom(Γ) then Error "unbound variable"
    else (∅, instantiate(Γ(x)))
```

### 5.3 Lambda (fun x -> e)

```
W(Γ, fun x -> e) =
    α = fresh type variable
    (S, τ) = W(Γ ∪ {x: α}, e)
    return (S, S(α) → τ)
```

### 5.4 Application (e₁ e₂)

```
W(Γ, e₁ e₂) =
    (S₁, τ₁) = W(Γ, e₁)
    (S₂, τ₂) = W(S₁(Γ), e₂)
    α = fresh type variable
    S₃ = unify(S₂(τ₁), τ₂ → α)
    return (S₃ ∘ S₂ ∘ S₁, S₃(α))
```

### 5.5 Let (let x = e₁ in e₂)

```
W(Γ, let x = e₁ in e₂) =
    (S₁, τ₁) = W(Γ, e₁)
    σ = generalize(S₁(Γ), τ₁)
    (S₂, τ₂) = W(S₁(Γ) ∪ {x: σ}, e₂)
    return (S₂ ∘ S₁, τ₂)
```

**Let-polymorphism**: let에서 일반화가 일어나므로:
```funlang
let id = fun x -> x in
(id 1, id true)    -- OK: id는 ∀α. α → α
```

### 5.6 If-then-else

```
W(Γ, if e₁ then e₂ else e₃) =
    (S₁, τ₁) = W(Γ, e₁)
    S₂ = unify(τ₁, bool)
    (S₃, τ₂) = W(S₂∘S₁(Γ), e₂)
    (S₄, τ₃) = W(S₃∘S₂∘S₁(Γ), e₃)
    S₅ = unify(S₄(τ₂), τ₃)
    return (S₅∘S₄∘S₃∘S₂∘S₁, S₅(τ₃))
```

### 5.7 Tuple

```
W(Γ, (e₁, e₂, ...)) =
    (S₁, τ₁) = W(Γ, e₁)
    (S₂, τ₂) = W(S₁(Γ), e₂)
    ...
    (Sₙ, τₙ) = W(Sₙ₋₁∘...∘S₁(Γ), eₙ)
    S = Sₙ ∘ ... ∘ S₁
    return (S, (S(τ₁), S(τ₂), ..., τₙ))
```

### 5.8 List

```
W(Γ, []) = (∅, list α)  where α is fresh

W(Γ, [e₁; e₂; ...]) =
    (S₁, τ₁) = W(Γ, e₁)
    (S₂, τ₂) = W(S₁(Γ), e₂)
    S₃ = unify(S₂(τ₁), τ₂)
    ... (모든 원소 통합)
    return (S, list S(τ₁))

W(Γ, e₁ :: e₂) =
    (S₁, τ₁) = W(Γ, e₁)
    (S₂, τ₂) = W(S₁(Γ), e₂)
    S₃ = unify(τ₂, list S₂(τ₁))
    return (S₃∘S₂∘S₁, S₃(τ₂))
```

### 5.9 Binary Operators

| Operator | Type |
|----------|------|
| `+`, `-`, `*`, `/`, `%` | `int → int → int` |
| `<`, `>`, `<=`, `>=` | `int → int → bool` |
| `=`, `<>` | `∀α. α → α → bool` |
| `&&`, `\|\|` | `bool → bool → bool` |
| `^` | `string → string → string` |

```
W(Γ, e₁ + e₂) =
    (S₁, τ₁) = W(Γ, e₁)
    S₂ = unify(τ₁, int)
    (S₃, τ₂) = W(S₂∘S₁(Γ), e₂)
    S₄ = unify(τ₂, int)
    return (S₄∘S₃∘S₂∘S₁, int)
```

### 5.10 Pattern Matching

```
W(Γ, match e with | p₁ -> e₁ | p₂ -> e₂ ...) =
    (S₀, τₑ) = W(Γ, e)

    for each case (pᵢ, eᵢ):
        (bindingsᵢ, τ_pᵢ) = inferPattern(pᵢ)
        Sᵢ = unify(τₑ, τ_pᵢ)
        (Sᵢ', τᵢ) = W(Γ ∪ bindingsᵢ, eᵢ)

    -- 모든 결과 타입 통합
    S_result = unify all τᵢ
    return (composed substitution, unified result type)
```

**Pattern Type Inference:**

```
inferPattern : Pattern → (Bindings, Type)

inferPattern(_) = (∅, α)  -- fresh α
inferPattern(x) = ({x: α}, α)  -- fresh α, binding
inferPattern(n) = (∅, int)
inferPattern(true) = (∅, bool)
inferPattern([]) = (∅, list α)  -- fresh α
inferPattern(p₁ :: p₂) =
    (b₁, τ₁) = inferPattern(p₁)
    (b₂, τ₂) = inferPattern(p₂)
    S = unify(τ₂, list τ₁)
    return (b₁ ∪ b₂, list S(τ₁))
inferPattern((p₁, p₂)) =
    (b₁, τ₁) = inferPattern(p₁)
    (b₂, τ₂) = inferPattern(p₂)
    return (b₁ ∪ b₂, (τ₁, τ₂))
```

## 6. Let-Rec (재귀 함수)

```
W(Γ, let rec f = e₁ in e₂) =
    α = fresh type variable
    (S₁, τ₁) = W(Γ ∪ {f: α}, e₁)
    S₂ = unify(S₁(α), τ₁)
    σ = generalize(S₂∘S₁(Γ), S₂(τ₁))
    (S₃, τ₂) = W(S₂∘S₁(Γ) ∪ {f: σ}, e₂)
    return (S₃∘S₂∘S₁, τ₂)
```

## 7. Type Annotations

타입 어노테이션이 있으면 추론된 타입과 통합:

```
W(Γ, (e : τ_ann)) =
    (S, τ) = W(Γ, e)
    S' = unify(τ, τ_ann)
    return (S' ∘ S, S'(τ_ann))
```

## 8. 예제

### 8.1 Identity Function

```funlang
fun x -> x
```

추론:
1. α = fresh (x의 타입)
2. W({x: α}, x) = (∅, α)
3. 결과: α → α

일반화 후: `∀α. α → α`

### 8.2 Let-Polymorphism

```funlang
let id = fun x -> x in
(id 1, id true)
```

1. id의 타입: ∀α. α → α
2. `id 1`: instantiate → β → β, unify with int, 결과 int
3. `id true`: instantiate → γ → γ, unify with bool, 결과 bool
4. 최종: (int, bool)

### 8.3 Type Error

```funlang
1 + true
```

1. W(Γ, 1) = (∅, int)
2. unify(int, int) = ∅
3. W(Γ, true) = (∅, bool)
4. unify(bool, int) = Error "type mismatch: expected int, got bool"

## 9. 참고 자료

- Damas, L., Milner, R. (1982). "Principal type-schemes for functional programs"
- Pierce, B.C. (2002). "Types and Programming Languages"
- Cardelli, L. (1987). "Basic Polymorphic Typechecking"
