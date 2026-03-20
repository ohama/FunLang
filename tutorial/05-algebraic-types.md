# 5장: 대수적 데이터 타입 (Algebraic Data Types)

대수적 데이터 타입(ADT)은 함수형 프로그래밍의 핵심 도구입니다. "대수적"이라는 이름이 어렵게 들릴 수 있지만, 본질은 간단합니다 — 여러 가지 형태를 가질 수 있는 타입을 정의하는 방법입니다. Python의 클래스 계층 구조나 Java의 상속을 대체할 수 있는, 훨씬 간결하고 안전한 방식이라고 생각하면 됩니다.

ADT를 처음 접하는 분들은 "그냥 열거형(enum) 아닌가요?"라고 물을 수 있습니다. 맞습니다 — 단순한 경우에는 열거형입니다. 하지만 각 케이스가 서로 다른 종류의 데이터를 담을 수 있다는 점이 결정적으로 다릅니다. 이 차이가 ADT를 강력하게 만드는 이유입니다.

## 단순 열거형

가장 기본적인 형태부터 시작합니다. 이름 있는 생성자(constructor)를 가진 타입을 정의합니다:

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

`Red`, `Green`, `Blue`는 단순히 `Color` 타입의 값입니다. Python에서 `class Color(Enum)`으로 만드는 것과 비슷하지만, `match` 표현식이 함께 쓰일 때 진가가 드러납니다. 컴파일러가 모든 케이스를 다뤘는지 확인해 주기 때문입니다.

## 선행 파이프 구문

케이스가 많아지면 한 줄에 나열하는 것이 읽기 불편해집니다. 여러 줄의 타입 정의에서는 선행 파이프(leading pipe)를 사용할 수 있습니다:

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

선행 파이프는 단순히 스타일의 문제입니다 — 두 방식 모두 동일하게 동작합니다. 하지만 케이스가 4개 이상이라면 선행 파이프 방식이 훨씬 읽기 좋습니다. F#이나 OCaml 코드베이스에서도 이 관례를 자주 볼 수 있습니다.

## 데이터를 가진 생성자

ADT가 단순 열거형과 달라지는 지점입니다. 생성자는 `of`를 사용하여 값을 포함할 수 있습니다:

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

`Circle`은 반지름 하나를 담고, `Rect`는 너비와 높이 두 개를 담습니다. 서로 다른 케이스가 서로 다른 구조를 가질 수 있다는 것이 ADT의 힘입니다. Python에서 이를 표현하려면 별도의 클래스를 만들고 공통 기반 클래스를 상속받아야 하지만, LangThree에서는 한 줄로 끝납니다.

패턴 매칭에서 `Rect (w, h)`처럼 쓰면 생성자에 담긴 데이터가 자동으로 구조 분해(destructuring)됩니다. 별도의 getter나 필드 접근이 필요 없습니다.

## 매개변수화된 타입

같은 구조를 다양한 타입에 재사용하고 싶을 때 타입 매개변수를 사용합니다. 타입 매개변수(type parameter)는 타입 이름 뒤에 위치합니다:

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

`Option`은 함수형 프로그래밍에서 가장 중요한 타입 중 하나입니다. "값이 있을 수도 있고 없을 수도 있는" 상황을 null 없이 표현합니다. `'a`는 타입 변수 — 어떤 타입이든 담을 수 있다는 의미입니다. `Option 'a`라고 선언하면, 실제로 `Some 42`를 만들 때 컴파일러가 `'a`가 `int`임을 자동으로 추론합니다.

`--emit-type`으로 추론된 타입을 확인할 수 있습니다:

```
$ langthree --emit-type option.l3
result : int
x : Option<int>
```

`x`의 타입이 `Option<int>`로 정확하게 추론된 것을 볼 수 있습니다. 이 타입 추론 덕분에 타입을 명시하지 않아도 컴파일러가 타입 안전성을 보장해 줍니다.

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

`Either`는 두 가지 가능성을 표현합니다. Haskell에서는 오류 처리에 자주 쓰이는 패턴으로, `Left`는 보통 실패를, `Right`는 성공을 나타냅니다. 여기서는 `Left`에 `int`, `Right`에 `string`을 담을 수 있는 타입을 한 줄로 정의했습니다.

## 재귀 타입

ADT의 또 다른 강력한 특징은 자기 자신을 참조할 수 있다는 점입니다. 이를 통해 리스트, 트리, 그래프 같은 재귀적 자료 구조를 자연스럽게 표현할 수 있습니다:

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

`Cons (1, Cons (2, Cons (3, Nil)))`는 [1; 2; 3] 리스트를 직접 구현한 것입니다. LangThree의 내장 리스트도 사실 이런 식으로 동작합니다. 재귀 타입은 재귀 함수와 자연스럽게 짝을 이룹니다 — 타입의 구조가 함수의 구조를 그대로 반영합니다.

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

이진 트리를 두 줄로 정의하고, 깊이 함수까지 자연스럽게 작성했습니다. 객체지향 언어에서 이와 동등한 코드를 작성하려면 추상 기반 클래스와 두 개의 서브클래스가 필요합니다. ADT는 이런 상황에서 코드를 극적으로 줄여줍니다.

참고: `let rec`은 표현식 수준(`in`과 함께)에서만 동작하며, 모듈 수준에서는 사용할 수 없습니다.
파일 모드에서 재귀 함수를 사용하려면 최상위 `let` 안에 `let rec ... in`을 포함시키세요.

## 상호 재귀 타입

때로는 두 타입이 서로를 참조해야 할 때가 있습니다. `and`를 사용하여 서로를 참조하는 타입을 정의할 수 있습니다:

```
$ cat mutual.l3
type Tree = Leaf of int | Node of Forest
and Forest = Empty | Trees of Tree * Forest
let result = Node (Trees (Leaf 1, Trees (Leaf 2, Empty)))

$ langthree mutual.l3
Node (Trees ((Leaf 1, Trees ((Leaf 2, Empty)))))
```

`Tree`는 `Forest`를 참조하고, `Forest`는 `Tree`를 참조합니다. 두 타입을 별도로 선언하면 컴파일러가 먼저 선언된 타입을 아직 모르는 상태에서 후자를 정의해야 하는 문제가 생깁니다. `and` 키워드는 "이 두 타입을 동시에 정의한다"는 의미로, 이 문제를 깔끔하게 해결합니다. F#과 OCaml에서도 동일한 `and` 키워드를 사용합니다.

## 완전성 검사

ADT와 패턴 매칭의 조합이 특히 빛을 발하는 순간이 바로 여기입니다. 컴파일러는 match 패턴이 불완전할 때 경고합니다:

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

`Blue` 케이스를 빠뜨렸을 때 컴파일러가 정확히 어떤 케이스가 없는지 알려줍니다. 이 기능은 생각보다 훨씬 중요합니다. 나중에 타입에 케이스를 추가했을 때, 그 타입을 다루는 모든 `match` 표현식에서 경고가 발생합니다. 즉, 컴파일러가 "이 새로운 케이스를 처리하는 걸 잊지 마세요"라고 자동으로 알려주는 셈입니다. Python의 `if/elif/else` 체인에서는 절대 얻을 수 없는 안전성입니다.

프로그램은 여전히 실행되지만, 경고가 누락된 케이스를 알려줍니다.
와일드카드 `_`를 추가하거나 모든 생성자를 다루면 경고가 사라집니다.

## 실용 예제: 간단한 계산기

ADT의 실용적인 활용을 보여주는 고전적인 예제입니다. 산술 표현식을 데이터 구조로 표현하고, 그것을 평가하는 인터프리터를 만들 수 있습니다:

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

`Plus (Num 2, Mul (Num 3, Num 4))`는 `2 + (3 * 4)`를 트리로 표현한 것입니다. `eval` 함수는 이 트리를 순회하며 실제 값을 계산합니다. 실제 프로그래밍 언어 인터프리터도 이와 같은 방식으로 동작합니다 — AST(Abstract Syntax Tree)를 ADT로 정의하고, 각 케이스를 패턴 매칭으로 처리합니다. 이 예제에서 ADT가 "왜" 유용한지가 가장 잘 드러납니다.

## 타입 별칭 (Type Aliases)

지금까지 본 ADT는 새로운 타입을 만들었습니다. 하지만 때로는 기존 타입에 의미 있는 이름을 붙이고 싶을 때가 있습니다. `type Name = ExistingType`으로 기존 타입에 별칭을 부여할 수 있습니다:

```
$ cat alias_basic.l3
type Name = string
type Age = int

let greet name age = name + " is " + to_string age
let result = greet "Alice" 30

$ langthree alias_basic.l3
"Alice is 30"
```

`Name`과 `Age`는 코드를 읽는 사람에게 "이 문자열은 이름이고, 이 정수는 나이입니다"라는 의도를 전달합니다. 함수 시그니처에서 `string -> int -> string` 대신 `Name -> Age -> string`처럼 읽히면 훨씬 명확해집니다.

타입 별칭은 **투명(transparent)**합니다 — 별칭과 원본 타입은 완전히 동일합니다.
`Name`은 `string`과 같은 타입이므로, `string` 함수를 그대로 사용할 수 있습니다.

### 복합 타입 별칭

단순 타입뿐 아니라 튜플, 함수, 리스트 타입에도 별칭을 붙일 수 있습니다:

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

`Transform = int -> int`처럼 함수 타입에 별칭을 붙이면 특히 유용합니다. 고차 함수를 많이 사용하는 코드에서 `(int -> int) -> (int -> int)` 같은 타입보다 `Transform -> Transform`이 훨씬 읽기 좋습니다.

### 타입 별칭 vs ADT

두 기능을 혼동하지 않도록 주의하세요. 핵심 차이는 "새로운 타입을 만드는가"입니다:

- `type Name = string` — 별칭. `Name`은 `string`과 동일
- `type Color = Red | Green | Blue` — ADT. `Color`는 새로운 타입

타입 별칭은 문서화와 가독성을 위한 도구이고, ADT는 새로운 데이터 구조를 정의하는 도구입니다. 별칭은 기존 함수를 그대로 사용할 수 있지만, ADT는 패턴 매칭을 통해서만 값에 접근할 수 있습니다.

`--emit-type`에서 별칭은 원본 타입으로 표시됩니다:

```
$ cat alias_emit.l3
type Name = string
let x = "hello"

$ langthree --emit-type alias_emit.l3
x : string
```

`Name`이 아닌 `string`으로 표시됩니다. 컴파일러 입장에서 별칭은 완전히 투명하기 때문입니다. 이 점이 Haskell의 `newtype`과 다른 부분입니다 — `newtype`은 별도의 타입으로 취급되지만, LangThree의 타입 별칭은 단순히 다른 이름일 뿐입니다.
