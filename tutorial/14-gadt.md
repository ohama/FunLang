# 14장: GADT (일반화된 대수적 데이터 타입)

## GADT가 필요한 이유

5장에서 배운 일반 ADT는 매우 강력합니다. 하지만 ADT에는 한 가지 근본적인 한계가 있습니다. 하나의 타입 안에 있는 모든 생성자가 정확히 같은 타입의 값을 만들어냅니다.

예를 들어 작은 수식 언어를 만든다고 생각해봅시다. 정수 리터럴과 불리언 리터럴을 하나의 `Expr` 타입으로 표현하고 싶습니다. 일반 ADT로는 이렇게 쓸 수 있습니다:

```
type Expr = IntLit of int | BoolLit of bool
```

이제 `eval` 함수를 만들어봅시다. `IntLit 42`를 평가하면 `int`가 나와야 하고, `BoolLit true`를 평가하면 `bool`이 나와야 합니다. 그런데 함수의 반환 타입은 하나여야 합니다. 어떻게 해야 할까요?

흔한 해결책은 `eval`의 반환 타입을 유니온으로 만드는 것입니다. 예컨대 `Value = IntVal of int | BoolVal of bool`처럼요. 하지만 그러면 `eval (IntLit 42)`가 `IntVal 42`를 반환하고, 이걸 정수로 쓰려면 다시 패턴 매칭을 해야 합니다. 더 나쁜 것은, 타입 시스템이 `Add (IntLit 1, BoolLit true)` 같이 의미없는 표현을 막아주지 못한다는 점입니다. 정수와 불리언을 더하는 표현이 타입 오류 없이 만들어질 수 있고, 오류는 런타임에서야 발견됩니다.

GADT는 이 문제를 컴파일 시점에 완전히 해결합니다. 각 생성자가 자신만의 구체적인 반환 타입을 가질 수 있으므로, `IntLit`은 반드시 `int Expr`를 만들고 `BoolLit`은 반드시 `bool Expr`를 만들도록 선언할 수 있습니다. 그러면 `Add`의 인자 타입을 `int Expr * int Expr`로 제한할 수 있고, 컴파일러가 `Add (IntLit 1, BoolLit true)`를 타입 오류로 거부합니다.

## GADT란 무엇인가?

일반 ADT에서는 모든 생성자가 동일한 반환 타입을 가집니다. GADT는 각 생성자가 자신만의 반환 타입을 지정할 수 있게 하여, 패턴 매치 분기 내에서 타입 시스템이 타입을 정제(refine)할 수 있도록 합니다.

"타입 정제"라는 말이 낯설 수 있습니다. 일반 패턴 매칭에서 우리는 값의 구조를 분해합니다. GADT 패턴 매칭에서는 값의 구조를 분해하는 동시에, 타입 매개변수가 무엇인지에 대한 정보도 함께 얻습니다. `IntLit n` 분기에 들어간 순간 컴파일러는 지금 다루는 `Expr`의 타입 매개변수가 `int`임을 확실히 알고, 그 정보를 이용해 `n`이 `int`임을 보장합니다.

## GADT 생성자 구문

각 생성자는 `:`와 `->`를 사용하여 인자 타입과 반환 타입을 선언합니다:

```
$ cat expr.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let result = IntLit 42

$ langthree expr.l3
IntLit 42
```

일반 ADT에서 `IntLit of int`이라고 쓰는 것과 비교해 보세요. GADT 구문에서 `IntLit : int -> int Expr`의 의미는 다음과 같습니다:
- `int` 인자를 받음
- `int Expr` 타입의 값을 생성함 (단순한 `'a Expr`이 아님)

타입 매개변수는 타입 이름 뒤에 위치합니다: `type Expr 'a`. 반환 타입은 `int Expr` 형태입니다 (`Expr int`이나 `Expr<int>`가 아님).

이 구문이 처음에는 생소할 수 있습니다. `IntLit : int -> int Expr`를 "int를 받아서 int Expr을 돌려주는 함수의 타입"으로 읽으면 이해가 쉬워집니다. 생성자를 일종의 함수로 보는 관점은 Haskell에서도 자주 쓰이는 시각입니다.

## match에서의 타입 정제

GADT의 핵심 능력: 생성자를 매치하면 컴파일러가 정확한 타입 매개변수를 알 수 있습니다. 이를 위해 match 표현식에 타입 주석(annotation)이 필요합니다:

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

`: int` 주석은 match의 끝에, 괄호 안에 위치합니다. 이렇게 하면 match 표현식이 "검사 모드(check mode)"에 놓여서 컴파일러가 각 분기별로 타입을 정제할 수 있습니다:
- `IntLit n` 분기에서 `n`은 `int`임이 확정됨
- `BoolLit b` 분기에서 `b`는 `bool`임이 확정됨

왜 이 주석이 필요한지 한 번 더 생각해봅시다. 컴파일러는 `e`의 타입이 `'a Expr`이라는 것을 압니다. 하지만 `'a`가 무엇인지는 모릅니다. 타입 주석 `: int`를 통해 "이 match의 결과가 `int`가 되어야 한다"고 알려주면, 컴파일러는 각 분기를 분석할 때 GADT의 생성자 타입 정보를 활용할 수 있습니다.

이 주석은 OCaml의 로컬 타입 추상화(locally abstract types) 메커니즘과 유사한 역할을 합니다. Haskell에서는 이것이 언어 코어에 더 깊이 통합되어 있어서 명시적인 주석 없이도 동작하는 경우가 많지만, LangThree는 명시적인 주석을 요구해서 코드를 읽는 사람이 "여기서 GADT 타입 정제가 일어나고 있다"는 것을 바로 알 수 있게 합니다.

## 주석은 필수

타입 주석이 없으면 GADT 매칭은 E0401 오류로 실패합니다:

```
$ cat no_anno.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let eval e =
    match e with
    | IntLit n -> n
    | BoolLit b -> if b then 1 else 0
let result = eval (IntLit 42)

$ langthree no_anno.l3
error[E0401]: GADT match requires type annotation on scrutinee of type 'm
 --> :0:0-1:0
   = hint: Add a type annotation to the match scrutinee: match (expr : Type) with ...
```

해결 방법: match를 괄호로 감싸고 끝에 `: ResultType`을 추가하세요.

이 오류가 처음 나타날 때 당황하지 마세요. 메시지의 힌트가 정확하게 무엇을 해야 하는지 알려줍니다. GADT를 쓸 때는 항상 match를 괄호로 감싸고 반환 타입을 명시한다고 기억하면 됩니다.

## 재귀 GADT 평가

실제로 유용한 표현식 언어는 중첩된 표현식을 가집니다. `1 + (2 + 3)` 같은 것이요. 이를 GADT로 표현하려면 재귀 생성자가 필요합니다.

`Add`와 같은 재귀 생성자의 경우 `let rec ... in`을 사용합니다:

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

`let rec`은 표현식 수준에서만 동작하므로, 재귀 평가기(evaluator)는 `let ... in` 블록 안에 정의합니다.

`Add : int Expr * int Expr -> int Expr` 선언의 힘을 보세요. `Add`의 두 인자는 반드시 `int Expr`이어야 합니다. 즉 `Add (BoolLit true, IntLit 1)` 같은 표현은 타입 오류입니다. 컴파일러가 수식의 타입 안전성을 보장해줍니다. 파이썬 같은 동적 타입 언어에서는 이런 검사를 런타임에 직접 구현해야 했을 것입니다.

## GADT 완전성 검사

GADT의 또 다른 능력은 불가능한 케이스를 컴파일러가 자동으로 걸러낸다는 것입니다.

컴파일러는 타입 정보를 기반으로 불가능한 생성자를 올바르게 필터링합니다. `int Expr` 타입의 값이 주어지면 `IntLit`과 `Add`만 가능합니다 -- `BoolLit`은 `int Expr`를 생성할 수 없습니다:

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

`BoolLit` 누락에 대한 경고가 없습니다 -- 컴파일러가 `int Expr` 값에 대해 이것이 불가능함을 알고 있기 때문입니다.

이것이 일반 ADT와 근본적으로 다른 점입니다. 일반 ADT였다면 `BoolLit` 분기를 빠뜨리면 패턴 매칭 불완전 경고가 나타납니다. GADT에서는 타입 시스템이 "이 생성자는 이 컨텍스트에서 절대로 나타날 수 없다"는 것을 증명해주기 때문에, 그 분기 자체를 쓸 필요가 없습니다.

이 특성은 타입 안전한 인터프리터나 컴파일러를 작성할 때 특히 값집니다. 정수 표현식을 평가하는 함수에 불리언 표현식이 들어오는 케이스를 명시적으로 처리하거나 예외를 던지는 대신, 그런 상황이 타입 시스템 수준에서 불가능하게 만들 수 있습니다.

## 여러 타입 매개변수의 활용

GADT를 쓰면 여러 종류의 값을 하나의 타입으로 다루면서도 각 값의 구체적인 타입을 타입 시스템이 추적하게 할 수 있습니다.

GADT는 더 풍부한 타입 관계를 인코딩할 수 있습니다:

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

`Val 'a`는 정수, 불리언, 문자열을 모두 담을 수 있는 타입이지만, 각 값의 구체적인 타입 정보를 `'a`에 보존합니다. `VInt 99`는 `int Val` 타입이고, `VBool true`는 `bool Val` 타입입니다. `show_int` 함수는 `int Val`만 받으므로, `show_int (VBool true)` 같은 호출은 컴파일 시점에 타입 오류입니다.

이 패턴은 타입 안전한 데이터베이스 쿼리 빌더나, 각 열의 타입이 다른 테이블 표현에서 볼 수 있습니다. GADT가 없다면 모든 열을 동일한 타입으로 처리하거나, 런타임 타입 태그를 직접 관리해야 합니다.

## 실용 예제: 타입 안전한 평가기

모든 것을 결합하여 -- 정수 연산을 가진 작은 표현식 언어:

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

`Add(IntLit 10, Neg(IntLit 3))` 표현식은 `10 + (-3) = 7`로 평가됩니다. 각 생성자가 반환 타입을 제약하므로, 컴파일러는 모든 분기가 `int`를 생성함을 알 수 있습니다.

이 예제는 실제 언어 인터프리터나 컴파일러의 작동 방식과 동일한 구조를 가지고 있습니다. 타입 안전한 AST(Abstract Syntax Tree)를 GADT로 표현하고, 재귀 평가기로 값을 계산합니다. GHC(Glasgow Haskell Compiler)의 핵심 IR도 이와 매우 유사한 방식으로 설계되어 있습니다.

## GADT를 언제 쓸까

GADT는 강력하지만, 일반 ADT로 충분한 상황에서 굳이 쓸 필요는 없습니다. GADT를 써야 할 때는 크게 세 가지 신호가 있습니다.

첫째, 같은 데이터 타입의 다른 생성자들이 다른 타입의 "페이로드"를 가지고, 그 타입 정보를 evaluation이나 변환 시점에 활용하고 싶을 때입니다. 앞서 본 `Expr 'a` 예제가 바로 이 경우입니다.

둘째, "이 잘못된 표현은 타입 시스템이 막아줬으면 좋겠다"라는 생각이 들 때입니다. 런타임 오류를 컴파일 시점 타입 오류로 바꿀 수 있다면, 그것은 언제나 좋은 트레이드오프입니다.

셋째, 평가 후 반환 타입이 입력에 따라 달라지는 함수를 타입 안전하게 만들고 싶을 때입니다. "정수 표현식을 평가하면 `int`가 나오고, 불리언 표현식을 평가하면 `bool`이 나온다"는 것을 함수 타입에 직접 표현하고 싶다면 GADT가 필요합니다.

반대로, 단순히 여러 케이스를 하나의 타입으로 묶고 싶다면 일반 ADT로 충분합니다. GADT의 추가적인 복잡성(타입 주석 필요, 구문이 더 복잡)은 그 이득이 있을 때만 감수할 가치가 있습니다.

## GADT 구문 요약

| 기능 | 구문 |
|------|------|
| 타입 선언 | `type Expr 'a = ...` |
| 생성자 | `IntLit : int -> int Expr` |
| 반환 타입 | `int Expr` (매개변수가 이름 앞에 위치) |
| match 주석 | `(match e with \| ... : int)` |
| 하나의 생성자라도 GADT이면 | 모든 생성자가 GADT로 취급됨 |
