# 제5장: 대수적 데이터 타입 (Algebraic Data Types)

## 단순 열거형

이름 있는 생성자(constructor)를 가진 타입을 정의합니다:

```
$ cat color.l3
type Color = Red | Green | Blue
let result =
    match Green with
    | Red -> "red"
    | Green -> "green"
    | Blue -> "blue"

$ langthree color.l3
"green"
```

## 선행 파이프 구문

여러 줄의 타입 정의에서는 선행 파이프(leading pipe)를 사용할 수 있습니다:

```
$ cat direction.l3
type Direction =
    | North
    | South
    | East
    | West
let result =
    match North with
    | North -> "up"
    | South -> "down"
    | East -> "right"
    | West -> "left"

$ langthree direction.l3
"up"
```

## 데이터를 가진 생성자

생성자는 `of`를 사용하여 값을 포함할 수 있습니다:

```
$ cat shape.l3
type Shape = Circle of int | Rect of int * int
let area s =
    match s with
    | Circle r -> r * r * 3
    | Rect (w, h) -> w * h
let result = area (Rect (3, 4))

$ langthree shape.l3
12
```

튜플 인자를 가진 생성자는 패턴에서 구조 분해(destructuring)됩니다.

## 매개변수화된 타입

타입 매개변수(type parameter)는 타입 이름 뒤에 위치합니다:

```
$ cat option.l3
type Option 'a = None | Some of 'a
let x = Some 42
let result =
    match x with
    | Some v -> v
    | None -> 0

$ langthree option.l3
42
```

`--emit-type`으로 추론된 타입을 확인할 수 있습니다:

```
$ langthree --emit-type option.l3
result : int
x : Option<int>
```

여러 개의 타입 매개변수도 사용할 수 있습니다:

```
$ cat either.l3
type Either 'a 'b = Left of 'a | Right of 'b
let result =
    match Left 42 with
    | Left n -> n
    | Right s -> string_length s

$ langthree either.l3
42
```

## 재귀 타입

타입은 자기 자신을 참조할 수 있습니다:

```
$ cat intlist.l3
type IntList = Nil | Cons of int * IntList
let xs = Cons (1, Cons (2, Cons (3, Nil)))
let result =
    let rec sum xs = match xs with | Nil -> 0 | Cons (x, rest) -> x + sum rest in
    sum xs

$ langthree intlist.l3
6
```

깊이(depth) 함수를 가진 이진 트리:

```
$ cat tree.l3
type Tree = Leaf of int | Branch of Tree * Tree
let t = Branch (Leaf 1, Branch (Leaf 2, Leaf 3))
let result =
    // 트리의 깊이: 왼쪽/오른쪽 중 더 깊은 쪽 + 1
    let rec depth t = match t with | Leaf _ -> 1 | Branch (l, r) -> 1 + max (depth l) (depth r) in
    depth t

$ langthree tree.l3
3
```

참고: `let rec`은 표현식 수준(`in`과 함께)에서만 동작하며, 모듈 수준에서는 사용할 수 없습니다.
파일 모드에서 재귀 함수를 사용하려면 최상위 `let` 안에 `let rec ... in`을 포함시키세요.

## 상호 재귀 타입

`and`를 사용하여 서로를 참조하는 타입을 정의할 수 있습니다:

```
$ cat mutual.l3
type Tree = Leaf of int | Node of Forest
and Forest = Empty | Trees of Tree * Forest
let result = Node (Trees (Leaf 1, Trees (Leaf 2, Empty)))

$ langthree mutual.l3
Node (Trees ((Leaf 1, Trees ((Leaf 2, Empty)))))
```

## 완전성 검사

컴파일러는 match 패턴이 불완전할 때 경고합니다:

```
$ cat exhaustive.l3
type Color = Red | Green | Blue
let result =
    match Red with
    | Red -> 1
    | Green -> 2

$ langthree exhaustive.l3
Warning: warning[W0001]: Incomplete pattern match. Missing cases: Blue
 --> :0:0-1:0
   = hint: Add the missing cases or a wildcard pattern '_' to cover all values
1
```

프로그램은 여전히 실행되지만, 경고가 누락된 케이스를 알려줍니다.
와일드카드 `_`를 추가하거나 모든 생성자를 다루면 경고가 사라집니다.

## 실용 예제: 간단한 계산기

계산기를 위한 표현식 타입:

```
$ cat calc.l3
type Expr = Num of int | Plus of Expr * Expr | Mul of Expr * Expr
let e = Plus (Num 2, Mul (Num 3, Num 4))
let result =
    let rec eval e = match e with | Num n -> n | Plus (a, b) -> eval a + eval b | Mul (a, b) -> eval a * eval b in
    eval e

$ langthree calc.l3
14
```

## 타입 별칭 (Type Aliases)

`type Name = ExistingType`으로 기존 타입에 별칭을 부여할 수 있습니다:

```
$ cat alias_basic.l3
type Name = string
type Age = int

let greet name age = name + " is " + to_string age
let result = greet "Alice" 30

$ langthree alias_basic.l3
"Alice is 30"
```

타입 별칭은 **투명(transparent)**합니다 — 별칭과 원본 타입은 완전히 동일합니다.
`Name`은 `string`과 같은 타입이므로, `string` 함수를 그대로 사용할 수 있습니다.

### 복합 타입 별칭

튜플, 함수, 리스트 타입에도 별칭을 붙일 수 있습니다:

```
$ cat alias_complex.l3
type IntPair = int * int
type Transform = int -> int
type IntList = int list

let swap p =
    match p with
    | (a, b) -> (b, a)

let result = swap (1, 2)

$ langthree alias_complex.l3
(2, 1)
```

### 타입 별칭 vs ADT

타입 별칭은 새로운 타입을 만들지 않습니다. 단지 기존 타입의 다른 이름일 뿐입니다:

- `type Name = string` — 별칭. `Name`은 `string`과 동일
- `type Color = Red | Green | Blue` — ADT. `Color`는 새로운 타입

`--emit-type`에서 별칭은 원본 타입으로 표시됩니다:

```
$ cat alias_emit.l3
type Name = string
let x = "hello"

$ langthree --emit-type alias_emit.l3
x : string
```

`Name`이 아닌 `string`으로 표시됩니다.
