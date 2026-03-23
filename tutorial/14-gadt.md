# 14장: GADT (일반화된 대수적 데이터 타입)

5장에서 대수적 데이터 타입(ADT)을 배웠습니다. ADT만으로도 대부분의 데이터 모델링은 충분합니다. 하지만 특정 상황에서 "타입 시스템이 더 많은 것을 보장해줬으면 좋겠다"는 생각이 드는 순간이 옵니다. GADT(Generalized Algebraic Data Types, 일반화된 대수적 데이터 타입)는 바로 그 지점에서 등장합니다.

이 장은 다른 장들보다 추상적인 내용을 다룹니다. 처음 읽을 때 모든 것이 한 번에 이해되지 않아도 괜찮습니다. 예제를 직접 실행해보면서, "일반 ADT로는 왜 안 되는가?"라는 질문에 초점을 맞추면 GADT의 존재 이유가 자연스럽게 보일 것입니다.

## ADT의 한계: 왜 GADT가 필요한가

먼저 일반 ADT가 어디까지 할 수 있고, 어디서 한계에 부딪히는지 살펴봅시다.

### ADT 복습

5장에서 배운 ADT는 "이 값은 A이거나 B이거나 C다"라는 합 타입(sum type)을 정의합니다:

```
type Shape = Circle of int | Rect of int * int
```

`Circle 5`와 `Rect (3, 4)` 모두 `Shape` 타입입니다. 패턴 매칭으로 어떤 생성자인지 확인하고 내부 데이터를 꺼낼 수 있습니다. 이것만으로도 트리, 리스트, 옵션 같은 재귀적 데이터 구조를 표현하기에 충분합니다.

매개변수화된 ADT도 가능합니다:

```
type Option 'a = None | Some of 'a
```

여기서 `'a`는 타입 매개변수입니다. `Some 42`는 `Option<int>`이고 `Some "hello"`는 `Option<string>`입니다. 하지만 핵심적인 제약이 있습니다: **`None`과 `Some` 모두 정확히 같은 `Option<'a>` 타입을 만들어냅니다.** 생성자가 타입 매개변수 `'a`를 특정 타입으로 고정할 수 없습니다.

### 문제 상황: 타입이 섞이는 표현식 언어

작은 수식 언어를 만든다고 생각해봅시다. 정수 리터럴과 불리언 리터럴, 그리고 덧셈을 지원하고 싶습니다.

일반 ADT로 시도해봅니다:

```
type Expr = IntLit of int | BoolLit of bool | Add of Expr * Expr
```

이 정의에는 치명적인 문제가 있습니다. `Add (IntLit 1, BoolLit true)`가 타입 오류 없이 만들어집니다. 정수와 불리언을 더하는 것은 의미가 없지만, 타입 시스템은 이를 막지 못합니다 — `IntLit 1`과 `BoolLit true` 모두 `Expr` 타입이니까요.

`eval` 함수도 문제입니다:

```
// 반환 타입이 뭐가 되어야 하나? int? bool?
let eval e = match e with
    | IntLit n -> ???
    | BoolLit b -> ???
    | Add (a, b) -> ???
```

`IntLit n`에서는 `int`를, `BoolLit b`에서는 `bool`을 반환하고 싶지만, 함수의 반환 타입은 하나여야 합니다. 결국 별도의 `Value` 유니온을 만들어야 합니다:

```
type Value = IntVal of int | BoolVal of bool
```

그리고 `eval (IntLit 42)`는 `IntVal 42`를 반환합니다. 이걸 정수로 쓰려면 다시 패턴 매칭을 해야 합니다. 래핑하고 언래핑하는 보일러플레이트가 계속 쌓입니다.

**핵심 문제를 정리하면:**
1. 타입 시스템이 잘못된 표현(`Add(IntLit, BoolLit)`)을 막지 못합니다
2. 평가 결과의 타입이 생성자에 따라 달라져야 하는데, ADT로는 이를 표현할 수 없습니다
3. 유니온 래핑/언래핑이라는 런타임 보일러플레이트가 필요합니다

## GADT란 무엇인가

GADT는 이 세 가지 문제를 모두 해결합니다. 핵심 아이디어는 단순합니다: **각 생성자가 타입 매개변수를 구체적인 타입으로 고정할 수 있다.**

일반 ADT에서는 모든 생성자가 동일한 `'a`를 공유합니다:

```
// 일반 ADT — 모든 생성자가 Option<'a>를 만듦
type Option 'a = None | Some of 'a
// None  : Option<'a>     ('a가 뭔지 모름)
// Some x : Option<'a>    ('a가 x의 타입)
```

GADT에서는 각 생성자가 `'a`를 특정 타입으로 고정합니다:

```
// GADT — 각 생성자가 다른 타입의 Expr을 만듦
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
// IntLit 42   : int Expr     ('a = int으로 고정)
// BoolLit true : bool Expr   ('a = bool로 고정)
```

`IntLit`은 반드시 `int Expr`을, `BoolLit`은 반드시 `bool Expr`을 만듭니다. `'a`가 "어떤 타입이든 될 수 있는 변수"가 아니라, 생성자마다 구체적인 타입으로 결정됩니다.

이것이 가능해지면, 패턴 매칭에서 **타입 정제(type refinement)**라는 강력한 능력이 생깁니다.

## GADT 생성자 구문

각 생성자는 `:`와 `->`를 사용하여 인자 타입과 반환 타입을 선언합니다:

```
$ cat expr.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let result = IntLit 42

$ langthree expr.l3
IntLit 42
```

일반 ADT 구문과 비교해봅시다:

| | 일반 ADT | GADT |
|---|---------|------|
| 구문 | `IntLit of int` | `IntLit : int -> int Expr` |
| 의미 | "`int`를 받아 `'a Expr` 생성" | "`int`를 받아 **`int Expr`** 생성" |
| 타입 매개변수 | 모든 생성자가 같은 `'a` | 각 생성자가 `'a`를 고정 |

`IntLit : int -> int Expr`를 읽는 방법: "int를 받아서 int Expr을 돌려주는 함수". 생성자를 일종의 타입이 정해진 함수로 보는 관점입니다. Haskell에서도 같은 시각을 씁니다.

타입 매개변수는 타입 이름 뒤에 위치합니다: `type Expr 'a`. 반환 타입은 `int Expr` 형태입니다 (`Expr int`이나 `Expr<int>`가 아님).

**중요:** 하나의 타입 선언에서 한 생성자라도 GADT 구문(`: ... -> ...`)을 쓰면, 모든 생성자가 GADT로 취급됩니다. 일반 ADT 구문(`of`)과 GADT 구문(`:`)을 섞어 쓸 수 없습니다.

## 타입 정제 (Type Refinement)

GADT의 진짜 힘은 패턴 매칭에서 드러납니다. 생성자를 매치하는 순간, 컴파일러는 타입 매개변수가 무엇인지 정확히 알게 됩니다. 이것을 **타입 정제**라고 합니다.

단계별로 살펴봅시다:

```
$ cat eval.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let eval e =
    (match e with
    | IntLit n -> n
    | BoolLit b -> if b then 1 else 0
    : int)
let result = eval (IntLit 42)

$ langthree eval.l3
42
```

이 코드에서 일어나는 일을 단계별로 추적해봅시다:

1. `eval`이 `e`를 받습니다. `e`의 타입은 `'a Expr` — 아직 `'a`가 뭔지 모릅니다
2. `match e with` — 패턴 매칭 시작
3. `IntLit n` 분기에 진입: 컴파일러는 `IntLit`이 `int -> int Expr` 생성자임을 압니다. 따라서 이 분기 안에서 `'a = int`이 확정됩니다. `n`은 반드시 `int`입니다
4. `BoolLit b` 분기에 진입: 마찬가지로 `'a = bool`이 확정됩니다. `b`는 반드시 `bool`입니다
5. 각 분기는 독립적으로 타입이 정제됩니다 — `IntLit` 분기의 `'a = int`이 `BoolLit` 분기에 영향을 주지 않습니다

일반 ADT였다면 컴파일러는 3번과 4번 단계에서 "그냥 `int`와 `bool`"밖에 모릅니다. 하지만 GADT에서는 "이 분기에서 `'a`가 `int`로 확정되었으므로, `n`이 `int`이고, 이 분기의 반환값도 `int`와 호환되어야 한다"는 추론까지 합니다.

### 타입 주석의 역할

GADT match에서 타입 주석은 **선택사항**입니다. 주석의 존재 여부와 종류에 따라 동작이 달라집니다:

**주석 없음 (권장 — 다형적 반환)**
주석 없이 match를 쓰면 컴파일러가 결과 타입을 자동으로 추론합니다. 각 분기는 독립적으로 정제됩니다:

```
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr

let eval e = match e with | IntLit n -> n | BoolLit b -> b

let r1 = eval (IntLit 42)    // r1 : int = 42
let r2 = eval (BoolLit true) // r2 : bool = true
```

**타입 변수 주석 `'a` (명시적 다형성)**
`'a`를 주석으로 쓰면 "이 match는 다형적 결과를 반환한다"는 의도를 명시적으로 표현합니다:

```
let result = (match IntLit 42 with | IntLit n -> n | BoolLit b -> if b then 1 else 0 : 'a)
// result : int = 42
```

**구체적 타입 주석 `: int` (단일 타입 강제)**
구체적 타입을 지정하면 모든 분기가 그 타입을 반환해야 합니다. 재귀 평가기에서 유용합니다:

```
let rec eval e = (match e with | IntLit n -> n | BoolLit b -> if b then 1 else 0 : int)
```

`:  int` 주석은 match의 끝에, 괄호 안에 위치합니다. 컴파일러는 `e`의 타입이 `'a Expr`이라는 것을 알지만, `'a`가 무엇인지는 모릅니다. 타입 주석 `: int`를 통해 "이 match의 결과가 `int`가 되어야 한다"고 알려주면, 컴파일러는 **검사 모드(check mode)**에 진입하여 각 분기를 분석할 때 GADT의 생성자 타입 정보를 활용할 수 있습니다.

OCaml에서는 이것을 "locally abstract types"라는 메커니즘으로 처리하고, Haskell에서는 더 깊이 통합되어 있어서 명시적 주석이 불필요한 경우가 많습니다. LangThree는 주석 없이도 다형적 반환이 가능하며, 명시적 주석은 반환 타입을 하나로 고정하고 싶을 때 사용합니다.

**참고:** 주석 없이도 동작하지만, 재귀 함수에서 반환 타입을 명시해야 할 때는 `(match e with | ... : ResultType)` 형태를 씁니다.

## 재귀 GADT

실용적인 표현식 언어는 중첩이 가능해야 합니다. `1 + (2 + 3)` 같은 표현을 위해 재귀 생성자가 필요합니다:

```
$ cat calc.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr | Add : int Expr * int Expr -> int Expr
let result =
    // GADT 재귀 평가기: 각 생성자를 매칭하여 int로 변환
    let rec eval e = (match e with | IntLit n -> n | BoolLit b -> if b then 1 else 0 | Add (a, b) -> eval a + eval b : int) in
    eval (Add (IntLit 10, Add (IntLit 20, IntLit 12)))

$ langthree calc.l3
42
```

`Add : int Expr * int Expr -> int Expr`의 의미를 잘 보세요:
- 두 인자 모두 **`int Expr`**이어야 합니다 (단순한 `'a Expr`이 아님)
- 결과도 `int Expr`입니다

이것이 ADT와의 결정적 차이입니다. ADT에서 `Add of Expr * Expr`이라고 쓰면 `Add (IntLit 1, BoolLit true)`가 가능합니다. GADT에서 `Add : int Expr * int Expr -> int Expr`이라고 쓰면 `Add (IntLit 1, BoolLit true)`는 타입 오류입니다 — `BoolLit true`는 `bool Expr`이지 `int Expr`이 아니니까요.

**컴파일 시점에 잘못된 표현을 차단한다는 것은**, 런타임에 "정수와 불리언을 더할 수 없습니다" 같은 에러를 처리하는 코드가 필요 없다는 뜻입니다. 파이썬 같은 동적 언어에서는 이런 검사를 직접 구현해야 했을 것입니다.

## 다형적 반환 타입

가장 강력한 GADT 패턴 중 하나는 함수의 반환 타입이 입력 GADT의 타입 매개변수와 일치하는 것입니다. OCaml에서 `eval : 'a expr -> 'a`로 알려진 패턴입니다.

```
$ cat poly-eval.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr

let eval e = match e with | IntLit n -> n | BoolLit b -> b

let r1 = eval (IntLit 42)
let r2 = eval (BoolLit true)

$ langthree poly-eval.l3
42
true
```

`eval`의 타입은 `'a Expr -> 'a`입니다. `IntLit 42`를 넘기면 `int`를, `BoolLit true`를 넘기면 `bool`을 반환합니다. 단일 함수가 입력의 GADT 타입 매개변수에 따라 다른 반환 타입을 가집니다.

이것이 가능한 이유: 컴파일러는 `match e with | IntLit n -> n | BoolLit b -> b`를 볼 때, 각 분기에서 타입 정제를 통해 독립적으로 타입을 확인합니다. `IntLit` 분기에서는 `n : int`이므로 결과가 `int`, `BoolLit` 분기에서는 `b : bool`이므로 결과가 `bool`. 두 분기가 서로 다른 타입을 반환해도 되는 것은 GADT의 타입 정제 덕분입니다 — 일반 ADT나 non-GADT match에서는 모든 분기가 같은 타입을 반환해야 합니다.

## GADT 완전성 검사

일반 ADT에서는 패턴 매칭이 모든 생성자를 다뤄야 합니다. `Shape = Circle | Rect`이면 `Circle`과 `Rect` 모두 분기가 있어야 합니다.

GADT에서는 타입 정보를 기반으로 **불가능한 생성자를 컴파일러가 자동으로 걸러냅니다:**

```
$ cat filter.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let eval_int e =
    (match e with
    | IntLit n -> n
    : int)
let result = eval_int (IntLit 7)

$ langthree filter.l3
7
```

`BoolLit` 분기가 없는데도 불완전 경고가 나타나지 않습니다. 왜일까요?

`: int` 주석 때문에 컴파일러는 `e`가 `int Expr` 타입임을 압니다. `BoolLit`은 `bool Expr`을 생성하는 생성자이므로, `int Expr` 값이 `BoolLit`일 수는 없습니다. 컴파일러가 이것을 증명해주기 때문에, 그 분기를 쓸 필요 자체가 없습니다.

이 특성은 타입 안전한 인터프리터를 만들 때 큰 가치가 있습니다. "정수 표현식을 평가하는 함수에 불리언 표현식이 들어왔을 때" 같은 불가능한 케이스를 예외로 처리하거나, `unreachable!()` 매크로로 표시하는 대신, 타입 시스템이 그런 상황을 원천 차단합니다.

## 여러 타입 매개변수의 활용

GADT는 하나의 타입으로 여러 종류의 값을 담으면서, 각 값의 구체적인 타입을 타입 시스템이 추적하게 합니다:

```
$ cat typed.l3
type Val 'a = VInt : int -> int Val | VBool : bool -> bool Val | VStr : string -> string Val
let show_int v =
    (match v with
    | VInt n -> to_string n
    : string)
let result = show_int (VInt 99)

$ langthree typed.l3
"99"
```

`Val 'a`는 세 가지 종류의 값을 담을 수 있지만, 타입 매개변수 `'a`가 각 값의 구체적인 타입을 보존합니다:
- `VInt 99`는 `int Val` — 정수를 담고 있다는 것이 타입에 나타남
- `VBool true`는 `bool Val` — 불리언을 담고 있다는 것이 타입에 나타남
- `VStr "hi"`는 `string Val` — 문자열을 담고 있다는 것이 타입에 나타남

`show_int`가 `int Val`만 받으므로, `show_int (VBool true)`는 컴파일 시점에 타입 오류입니다. 런타임에 "expected int but got bool" 같은 에러를 던질 필요가 없습니다.

이 패턴이 유용한 실제 사례:
- **타입 안전한 설정 시스템**: 설정 키의 타입이 값의 타입을 결정 (`IntKey : string -> int Setting`, `StrKey : string -> string Setting`)
- **타입 안전한 직렬화/역직렬화**: 데이터 형식의 타입 정보를 보존
- **컴파일러/인터프리터의 타입 안전한 IR(Intermediate Representation)**: GHC의 Core IR이 이 방식으로 설계됨

## 실용 예제: 타입 안전한 평가기

지금까지 배운 모든 것을 결합합니다 — 정수 연산과 부정(negation)을 가진 표현식 언어:

```
$ cat typed_eval.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr | Add : int Expr * int Expr -> int Expr | Neg : int Expr -> int Expr
let result =
    // Neg 포함 GADT 평가기: Add(10, Neg(3)) = 10 + (-3) = 7
    let rec eval e = (match e with | IntLit n -> n | BoolLit b -> if b then 1 else 0 | Add (a, b) -> eval a + eval b | Neg x -> 0 - eval x : int) in
    eval (Add (IntLit 10, Neg (IntLit 3)))

$ langthree typed_eval.l3
7
```

이 평가기가 보장하는 것들을 정리합시다:

1. **`Add`의 인자는 반드시 `int Expr`이다** — `Add (IntLit 1, BoolLit true)`는 컴파일 오류
2. **`Neg`의 인자도 반드시 `int Expr`이다** — 불리언을 부정하려는 시도는 컴파일 오류
3. **평가 결과는 반드시 `int`이다** — `eval`의 모든 분기가 `int`를 반환함이 컴파일 시점에 증명됨
4. **불가능한 분기를 처리할 필요 없다** — `int Expr` 값에 대해 `BoolLit` 분기는 도달 불가능

이 구조는 실제 언어 인터프리터나 컴파일러의 작동 방식과 동일합니다. 타입 안전한 AST(Abstract Syntax Tree)를 GADT로 표현하고, 재귀 평가기로 값을 계산합니다.

## 다른 언어의 GADT

GADT는 LangThree만의 기능이 아닙니다. 여러 언어가 같은 아이디어를 각자의 방식으로 표현합니다. 다른 언어의 접근법을 비교하면 GADT의 본질이 더 명확해집니다.

### Haskell — GADT의 원조

Haskell은 GADT를 가장 먼저 실용화한 언어입니다. `{-# LANGUAGE GADTs #-}` 확장으로 사용합니다:

```haskell
{-# LANGUAGE GADTs #-}

data Expr a where
    IntLit  :: Int  -> Expr Int
    BoolLit :: Bool -> Expr Bool
    Add     :: Expr Int -> Expr Int -> Expr Int

eval :: Expr a -> a
eval (IntLit n)  = n
eval (BoolLit b) = b
eval (Add x y)   = eval x + eval y
```

Haskell에서 주목할 점은 `eval`의 반환 타입이 `a`라는 것입니다. `IntLit` 분기에서 `a = Int`로 정제되므로 `n :: Int`를 그대로 반환할 수 있고, `BoolLit` 분기에서는 `a = Bool`로 정제되므로 `b :: Bool`을 반환할 수 있습니다. **반환 타입이 입력에 따라 달라지는 함수를 타입 안전하게 정의한 것입니다.** Haskell은 타입 추론이 강력해서 `(match ... : int)` 같은 명시적 주석 없이도 동작하는 경우가 많습니다.

LangThree와 비교하면:

| | Haskell | LangThree |
|---|---------|-----------|
| 구문 | `IntLit :: Int -> Expr Int` | `IntLit : int -> int Expr` |
| 타입 매개변수 위치 | 뒤 (`Expr Int`) | 앞 (`int Expr`) |
| match 주석 | 대부분 불필요 (타입 추론) | 선택사항 (생략 시 다형적 반환) |
| 다형적 반환 | `eval :: Expr a -> a` 가능 | `eval : 'a Expr -> 'a` 가능 |

Haskell의 `eval :: Expr a -> a`는 "정수 표현식을 넣으면 정수가 나오고, 불리언 표현식을 넣으면 불리언이 나온다"를 타입 하나로 표현합니다. LangThree도 주석 없이 `let eval e = match e with | IntLit n -> n | BoolLit b -> b`처럼 쓰면 `eval : 'a Expr -> 'a` 타입이 추론됩니다 — Haskell과 동등한 수준의 다형적 반환을 지원합니다. 반환 타입을 하나로 고정하고 싶을 때는 `(match ... : int)` 주석을 추가하면 됩니다.

### OCaml — LangThree의 직접적 영감

OCaml은 4.0부터 GADT를 지원합니다. LangThree의 GADT 구현은 OCaml의 접근법에서 직접적인 영감을 받았습니다:

```ocaml
type _ expr =
  | IntLit  : int  -> int expr
  | BoolLit : bool -> bool expr
  | Add     : int expr * int expr -> int expr

let eval : type a. a expr -> a = function
  | IntLit n  -> n
  | BoolLit b -> b
  | Add (x, y) -> eval x + eval y
```

OCaml에서 `type a.`는 **locally abstract type** 선언입니다. 컴파일러에게 "이 함수 안에서 GADT 타입 정제를 수행하라"고 알려줍니다. 주석 없이는 OCaml도 타입 정제를 할 수 없습니다. LangThree는 주석 없이도 자동으로 다형적 타입 변수를 생성하여 타입 정제를 수행하는 방식으로, OCaml보다 한 걸음 더 나아갔습니다. 반환 타입을 하나로 고정하고 싶을 때는 여전히 `(match ... : int)` 주석을 사용할 수 있습니다.

OCaml 구문에서 `type _ expr`의 `_`는 타입 매개변수를 익명으로 선언합니다. 각 생성자가 자신의 반환 타입에서 이 매개변수를 구체화합니다. LangThree에서는 `type Expr 'a`로 매개변수에 이름을 줍니다.

### Scala — sealed trait으로 유사 구현

Scala는 언어 자체에 GADT 키워드가 없지만, sealed trait과 제네릭으로 비슷한 패턴을 만들 수 있습니다:

```scala
sealed trait Expr[A]
case class IntLit(n: Int) extends Expr[Int]
case class BoolLit(b: Boolean) extends Expr[Boolean]
case class Add(x: Expr[Int], y: Expr[Int]) extends Expr[Int]

def eval[A](e: Expr[A]): A = e match {
  case IntLit(n) => n
  case BoolLit(b) => b
  case Add(x, y) => eval(x) + eval(y)
}
```

Scala에서 `IntLit extends Expr[Int]`은 "IntLit은 Expr의 타입 매개변수를 Int로 고정한다"는 뜻입니다. 이것이 LangThree의 `IntLit : int -> int Expr`과 정확히 같은 의미입니다. Scala의 패턴 매칭에서도 `case IntLit(n) =>`에 진입하면 컴파일러가 `A = Int`를 추론합니다.

객체지향 배경에서 왔다면 이 Scala 코드가 가장 친숙하게 느껴질 것입니다. `extends Expr[Int]`는 상속처럼 보이지만, 실제로는 GADT의 "생성자가 반환 타입을 고정한다"는 것과 동일한 효과를 냅니다.

### TypeScript — 판별 유니온으로 흉내내기

TypeScript에는 GADT가 없지만, 판별 유니온(discriminated union)과 타입 좁히기(type narrowing)로 비슷한 효과를 낼 수 있습니다:

```typescript
type Expr =
  | { tag: "int"; value: number }
  | { tag: "bool"; value: boolean }
  | { tag: "add"; left: Expr; right: Expr }

function eval(e: Expr): number | boolean {
  switch (e.tag) {
    case "int": return e.value;   // TypeScript knows: e.value is number
    case "bool": return e.value;  // TypeScript knows: e.value is boolean
    case "add": return (eval(e.left) as number) + (eval(e.right) as number);
  }
}
```

TypeScript의 `switch (e.tag)`는 각 분기에서 `e`의 타입을 좁혀줍니다. `case "int":`에서 TypeScript는 `e.value`가 `number`임을 압니다. 이것은 GADT의 타입 정제와 개념적으로 같습니다.

하지만 결정적인 차이가 있습니다:
- **`eval`의 반환 타입이 `number | boolean`이다** — GADT처럼 "int 표현식이면 number, bool 표현식이면 boolean"이라고 타입에 표현할 수 없습니다
- **`Add`의 인자를 `Expr`로만 제한할 수 있다** — "int Expr만 받는다"고 표현할 수 없으므로 `Add(BoolLit, IntLit)` 같은 잘못된 조합을 컴파일 시점에 막지 못합니다
- **`as number` 캐스팅이 필요하다** — 타입 시스템이 `eval(e.left)`의 결과가 `number`임을 증명하지 못하므로, 프로그래머가 직접 보장해야 합니다

이 차이가 바로 "타입 좁히기"와 "GADT 타입 정제"의 본질적 차이입니다. 타입 좁히기는 런타임 태그(`tag`)를 보고 해당 분기의 타입을 좁히지만, 타입 매개변수 자체를 고정하지는 못합니다. GADT는 타입 매개변수를 고정하여, 반환 타입까지 입력 타입에 연동시킬 수 있습니다.

### Rust — enum으로는 안 되는 것

Rust의 enum은 강력하지만 GADT를 지원하지 않습니다:

```rust
enum Expr {
    IntLit(i32),
    BoolLit(bool),
    Add(Box<Expr>, Box<Expr>),  // Expr일 뿐, "int Expr"이 아님
}
```

Rust에서 `Expr`에 타입 매개변수를 넣을 수는 있지만 (`Expr<T>`), 각 variant가 `T`를 다르게 고정하는 것은 불가능합니다. `IntLit`이 `Expr<i32>`를, `BoolLit`이 `Expr<bool>`을 만들어내게 하려면, 서로 다른 타입 (`Expr<i32>`와 `Expr<bool>`)이 되어 하나의 enum에 담을 수 없습니다.

Rust에서 이 문제를 해결하려면 trait object나 enum + 런타임 태그 패턴을 사용해야 합니다. 두 방법 모두 컴파일 시점 타입 안전성을 포기하거나, 상당한 보일러플레이트를 감수해야 합니다.

### 언어별 비교 요약

| 언어 | GADT 지원 | 구문 | 타입 주석 | 다형적 반환 |
|------|----------|------|----------|-----------|
| **Haskell** | 네이티브 (`GADTs` 확장) | `data Expr a where IntLit :: Int -> Expr Int` | 대부분 불필요 | `eval :: Expr a -> a` 가능 |
| **OCaml** | 네이티브 (4.0+) | `type _ expr = IntLit : int -> int expr` | `type a.` 필요 | 가능 |
| **LangThree** | 네이티브 | `IntLit : int -> int Expr` | 선택사항 (생략 시 다형적) | `eval : 'a Expr -> 'a` 가능 |
| **Scala** | sealed trait으로 유사 | `case class IntLit(n: Int) extends Expr[Int]` | 불필요 | 가능 |
| **TypeScript** | 판별 유니온 (제한적) | `{ tag: "int"; value: number }` | 불필요 | 유니온 반환만 |
| **Rust** | 미지원 | — | — | — |

LangThree의 GADT는 OCaml의 설계를 가장 가깝게 따르며, Haskell보다 단순하지만 핵심 기능(타입 정제, 불가능한 분기 제거, 컴파일 시점 타입 안전성)은 동일하게 제공합니다.

## ADT vs GADT: 언제 무엇을 쓸까

| 상황 | 선택 | 이유 |
|------|------|------|
| 단순 열거형 (Color, Direction) | ADT | 타입 매개변수가 불필요 |
| 재귀 데이터 (Tree, List) | ADT | 모든 노드가 같은 타입 |
| Option, Result | ADT | 감싸는 값의 타입이 하나 |
| 타입별로 다른 동작이 필요한 표현식 언어 | GADT | 생성자마다 반환 타입이 다름 |
| 잘못된 조합을 컴파일 시점에 차단하고 싶을 때 | GADT | 타입 수준의 제약 |
| 평가 결과 타입이 입력에 따라 달라질 때 | GADT | 타입 정제로 해결 |

**경험 법칙:** "이 잘못된 값은 타입 시스템이 막아줬으면 좋겠다"는 생각이 들 때가 GADT를 고려할 시점입니다. 그렇지 않다면 일반 ADT로 충분합니다. GADT의 추가적인 복잡성(타입 주석 필요, 구문이 더 복잡)은 타입 안전성의 이득이 있을 때만 감수할 가치가 있습니다.

## GADT 구문 요약

| 기능 | 구문 |
|------|------|
| 타입 선언 | `type Expr 'a = ...` |
| 생성자 | `IntLit : int -> int Expr` |
| 반환 타입 | `int Expr` (매개변수가 이름 앞에 위치) |
| match 주석 | `(match e with \| ... : int)` — 선택사항, 반환 타입 고정 시 사용 |
| 주석 없는 match | 컴파일러가 자동으로 다형적 타입 변수 생성 |
| 하나의 생성자라도 GADT이면 | 모든 생성자가 GADT로 취급됨 |

| 일반 ADT | GADT |
|----------|------|
| `type T 'a = A of int` | `type T 'a = A : int -> int T` |
| 모든 생성자가 `T<'a>` 생성 | 각 생성자가 `T<concrete>` 생성 |
| match에서 값만 분해 | match에서 값 분해 + 타입 정제 |
| 타입 주석 불필요 | `(match ... : Type)` 선택사항 |
| 모든 생성자 분기 필요 | 불가능한 생성자 자동 제외 |
