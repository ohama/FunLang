# 제7장: GADT (일반화된 대수적 데이터 타입, Generalized Algebraic Data Types)

## GADT란 무엇인가?

일반 ADT에서는 모든 생성자가 동일한 반환 타입을 가집니다.
GADT는 각 생성자가 자신만의 반환 타입을 지정할 수 있게 하여,
패턴 매치 분기 내에서 타입 시스템이 타입을 정제(refine)할 수 있도록 합니다.

## GADT 생성자 구문

각 생성자는 `:`와 `->`를 사용하여 인자 타입과 반환 타입을 선언합니다:

```
$ cat expr.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let result = IntLit 42

$ langthree expr.l3
IntLit 42
```

일반 ADT에서 `IntLit of int`이라고 쓰는 것과 비교해 보세요.
GADT 구문에서 `IntLit : int -> int Expr`의 의미는 다음과 같습니다:
- `int` 인자를 받음
- `int Expr` 타입의 값을 생성함 (단순한 `'a Expr`이 아님)

타입 매개변수는 타입 이름 뒤에 위치합니다: `type Expr 'a`.
반환 타입은 `int Expr` 형태입니다 (`Expr int`이나 `Expr<int>`가 아님).

## match에서의 타입 정제

GADT의 핵심 능력: 생성자를 매치하면 컴파일러가 정확한 타입 매개변수를 알 수 있습니다.
이를 위해 match 표현식에 타입 주석(annotation)이 필요합니다:

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

`: int` 주석은 match의 끝에, 괄호 안에 위치합니다.
이렇게 하면 match 표현식이 "검사 모드(check mode)"에 놓여서 컴파일러가
각 분기별로 타입을 정제할 수 있습니다:
- `IntLit n` 분기에서 `n`은 `int`임이 확정됨
- `BoolLit b` 분기에서 `b`는 `bool`임이 확정됨

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

## 재귀 GADT 평가

`Add`와 같은 재귀 생성자의 경우 `let rec ... in`을 사용합니다:

```
$ cat calc.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr | Add : int Expr * int Expr -> int Expr
let result =
    let rec eval e = (match e with | IntLit n -> n | BoolLit b -> if b then 1 else 0 | Add (a, b) -> eval a + eval b : int) in
    eval (Add (IntLit 10, Add (IntLit 20, IntLit 12)))

$ langthree calc.l3
42
```

`let rec`은 표현식 수준에서만 동작하므로, 재귀 평가기(evaluator)는
`let ... in` 블록 안에 정의합니다.

## GADT 완전성 검사

컴파일러는 타입 정보를 기반으로 불가능한 생성자를 올바르게 필터링합니다.
`int Expr` 타입의 값이 주어지면 `IntLit`과 `Add`만 가능합니다
-- `BoolLit`은 `int Expr`를 생성할 수 없습니다:

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

`BoolLit` 누락에 대한 경고가 없습니다 -- 컴파일러가 `int Expr` 값에 대해
이것이 불가능함을 알고 있기 때문입니다.

## 여러 타입 매개변수의 활용

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

## 실용 예제: 타입 안전한 평가기

모든 것을 결합하여 -- 정수 연산을 가진 작은 표현식 언어:

```
$ cat typed_eval.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr | Add : int Expr * int Expr -> int Expr | Neg : int Expr -> int Expr
let result =
    let rec eval e = (match e with | IntLit n -> n | BoolLit b -> if b then 1 else 0 | Add (a, b) -> eval a + eval b | Neg x -> 0 - eval x : int) in
    eval (Add (IntLit 10, Neg (IntLit 3)))

$ langthree typed_eval.l3
7
```

`Add(IntLit 10, Neg(IntLit 3))` 표현식은 `10 + (-3) = 7`로 평가됩니다.
각 생성자가 반환 타입을 제약하므로, 컴파일러는 모든 분기가
`int`를 생성함을 알 수 있습니다.

## GADT 구문 요약

| 기능 | 구문 |
|------|------|
| 타입 선언 | `type Expr 'a = ...` |
| 생성자 | `IntLit : int -> int Expr` |
| 반환 타입 | `int Expr` (매개변수가 이름 앞에 위치) |
| match 주석 | `(match e with \| ... : int)` |
| 하나의 생성자라도 GADT이면 | 모든 생성자가 GADT로 취급됨 |
