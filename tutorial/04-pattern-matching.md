# 4장: 패턴 매칭 (Pattern Matching)

패턴 매칭(pattern matching)은 LangThree의 핵심 제어 흐름 메커니즘입니다.
구조 분해(destructuring), 조건부 디스패치(conditional dispatch), 변수 바인딩(variable binding)을
하나의 구문으로 결합합니다.
컴파일러는 완전성(exhaustiveness)을 검사하고 패턴을 효율적인 결정 트리(decision tree)로 컴파일합니다.

## 기본 Match 구문

Match 표현식은 `match` 키워드에 맞춰 정렬된 `|` 파이프를 사용합니다:

```
funlang> match 2 with | 0 -> "zero" | 1 -> "one" | _ -> "other"
"other"
```

파일 모드에서 여러 줄의 match는 들여쓰기를 사용합니다:

```
$ cat classify.l3
let classify x =
    match x with
    | 0 -> "zero"
    | 1 -> "one"
    | _ -> "other"
let result = classify 1

$ langthree classify.l3
"one"
```

파이프는 `match` 키워드의 열(column)에 맞춰야 하며, 그보다 들여쓰기하면 안 됩니다.

## 패턴 종류

### 상수 패턴 (Constant Patterns)

정수 및 불리언 리터럴:

```
funlang> match true with | true -> "yes" | false -> "no"
"yes"

funlang> match 3 with | 1 -> "one" | 2 -> "two" | 3 -> "three" | _ -> "other"
"three"
```

음수 정수도 패턴으로 사용할 수 있습니다:

```
funlang> match (0 - 1) with | -1 -> "neg one" | 0 -> "zero" | _ -> "other"
"neg one"
```

디스패치 테이블을 위한 다중 상수 사용:

```
$ cat daytype.l3
let dayType d =
    match d with
    | 1 -> "Monday"
    | 2 -> "Tuesday"
    | 3 -> "Wednesday"
    | 4 -> "Thursday"
    | 5 -> "Friday"
    | 6 -> "Saturday"
    | 7 -> "Sunday"
    | _ -> "invalid"
let result = dayType 3

$ langthree daytype.l3
"Wednesday"
```

### 문자열 패턴 (String Patterns)

v1.4부터 문자열 리터럴도 패턴으로 사용할 수 있습니다:

```
$ cat string_match.l3
let greet name =
    match name with
    | "Alice" -> "Hello, Alice!"
    | "Bob" -> "Hi, Bob!"
    | _ -> "Who are you, " + name + "?"
let result = greet "Alice"

$ langthree string_match.l3
"Hello, Alice!"
```

커맨드 디스패치에 유용합니다:

```
$ cat cmd_dispatch.l3
let classify cmd =
    match cmd with
    | "quit" | "exit" | "q" -> "exit command"
    | "help" | "?" -> "help command"
    | _ -> "unknown: " + cmd
let result = classify "quit"

$ langthree cmd_dispatch.l3
"exit command"
```

위 예제는 or-패턴과 문자열 패턴을 함께 사용합니다. or-패턴은 아래에서 설명합니다.

### 변수 및 와일드카드 패턴 (Variable and Wildcard Patterns)

변수 패턴은 매칭된 값을 이름에 바인딩합니다. `_`는 값을 버리는 와일드카드(wildcard)입니다:

```
funlang> match 42 with | x -> x + 1
43

funlang> match 42 with | _ -> 0
0
```

변수 패턴은 항상 매칭됩니다 -- 모든 값을 잡는 기본 케이스(catch-all) 역할을 합니다:

```
$ cat sign.l3
let sign x =
    match x with
    | 0 -> 0
    | n when n > 0 -> 1
    | _ -> 0 - 1
let result = (sign 5, sign 0, sign (0 - 3))

$ langthree sign.l3
(1, 0, -1)
```

**섀도잉(Shadowing):** 패턴 내의 변수는 같은 이름의 외부 바인딩을 가립니다:

```
funlang> let x = 10 in match 5 with | x -> x
5
```

내부 `x`는 10이 아닌 5에 바인딩됩니다.

### 튜플 패턴 (Tuple Patterns)

튜플을 제자리에서 분해합니다:

```
funlang> match (1, 2) with | (a, b) -> a + b
3
```

중첩 튜플 패턴:

```
funlang> match ((1, 2), (3, 4)) with | ((a, b), (c, d)) -> a + b + c + d
10
```

튜플을 상수 및 와일드카드와 결합:

```
$ cat classify_pair.l3
let classify pair =
    match pair with
    | (true, 0) -> "zero-true"
    | (true, x) -> "positive-true: " + to_string x
    | (false, _) -> "false"
let result = classify (true, 42)

$ langthree classify_pair.l3
"positive-true: 42"
```

### 리스트 패턴 (List Patterns)

빈 리스트, cons, 또는 특정 길이에 대해 매칭합니다:

```
funlang> match [1, 2, 3] with | [] -> "empty" | x :: _ -> to_string x
"1"

funlang> match [1, 2, 3] with | a :: b :: _ -> a + b | _ -> 0
3
```

정확한 길이 매칭:

```
$ cat list_describe.l3
let describe xs =
    match xs with
    | [] -> "empty"
    | x :: [] -> "singleton: " + to_string x
    | x :: y :: [] -> "pair: " + to_string x + "," + to_string y
    | x :: y :: z :: _ -> "three+: " + to_string x + "," + to_string y + "," + to_string z

let r1 = describe []
let r2 = describe [42]
let r3 = describe [1, 2]
let r4 = describe [10, 20, 30, 40]
let result = r1 + " | " + r2 + " | " + r3 + " | " + r4

$ langthree list_describe.l3
"empty | singleton: 42 | pair: 1,2 | three+: 10,20,30"
```

### 생성자 패턴 (Constructor Patterns)

대수적 데이터 타입(algebraic data type)의 생성자를 매칭합니다:

```
$ cat shape.l3
type Shape = Circle of int | Rect of int * int

let area s =
    match s with
    | Circle r -> r * r * 3
    | Rect (w, h) -> w * h
let result = area (Circle 5)

$ langthree shape.l3
75
```

데이터가 없는 생성자 (nullary):

```
$ cat card.l3
type Card = Ace | King | Queen | Jack | Num of int

let value c =
    match c with
    | Ace -> 11
    | King -> 10
    | Queen -> 10
    | Jack -> 10
    | Num n -> n

let result = value Ace + value King + value (Num 5)

$ langthree card.l3
26
```

생성자 내부에서 와일드카드 사용:

```
$ cat is_leaf.l3
type Tree = Leaf | Node of Tree * int * Tree

let isLeaf t =
    match t with
    | Leaf -> true
    | Node (_, _, _) -> false

let result = (isLeaf Leaf, isLeaf (Node (Leaf, 1, Leaf)))

$ langthree is_leaf.l3
(true, false)
```

### 중첩 패턴 (Nested Patterns)

패턴은 자유롭게 합성할 수 있습니다 -- 생성자 안의 생성자, 튜플 안의 리스트 등:

**Option 안의 Option:**

```
$ cat deep_option.l3
let deepGet opt =
    match opt with
    | Some (Some (Some x)) -> to_string x
    | Some (Some None) -> "inner none"
    | Some None -> "mid none"
    | None -> "outer none"

let r1 = deepGet (Some (Some (Some 42)))
let r2 = deepGet (Some (Some None))
let r3 = deepGet (Some None)
let r4 = deepGet None
let result = r1 + " | " + r2 + " | " + r3 + " | " + r4

$ langthree deep_option.l3
"42 | inner none | mid none | outer none"
```

**튜플의 리스트:**

```
funlang> let rec sumFirst xs = match xs with | [] -> 0 | (a, _) :: rest -> a + sumFirst rest in sumFirst [(1, "a"), (2, "b"), (3, "c")]
6
```

**리스트 안의 튜플을 포함하는 생성자:**

```
$ cat nested_complex.l3
type Opt 'a = None | Some of 'a
let result =
    match Some [1, 2, 3] with
    | Some (x :: _) -> x
    | Some [] -> 0
    | None -> 0

$ langthree nested_complex.l3
1
```

### 레코드 패턴 (Record Patterns)

match에서 레코드 필드를 구조 분해합니다:

```
$ cat record_match.l3
type Point = { x: int; y: int }
let p = { x = 1; y = 2 }
let result =
    match p with
    | { x = a; y = b } -> a + b

$ langthree record_match.l3
3
```

부분 레코드 패턴 -- 일부 필드만 매칭:

```
$ cat record_partial.l3
type Person = { name: string; age: int; active: bool }

let greet p =
    match p with
    | { name = n; age = a } -> n + " is " + to_string a

let result = greet { name = "Alice"; age = 30; active = true }

$ langthree record_partial.l3
"Alice is 30"
```

## Or-패턴 (Or-Patterns)

여러 패턴이 같은 본문을 공유할 때 `|`로 결합합니다:

```
funlang> match 2 with | 1 | 2 | 3 -> "small" | _ -> "big"
"small"
```

각 대안은 같은 결과 표현식으로 이어집니다. 파일 모드에서:

```
$ cat or_pattern.l3
let classify n =
    match n with
    | 0 -> "zero"
    | 1 | 2 | 3 -> "small"
    | 4 | 5 | 6 -> "medium"
    | _ -> "large"
let result = classify 5

$ langthree or_pattern.l3
"medium"
```

### 생성자와 Or-패턴

ADT 생성자에도 사용할 수 있습니다:

```
$ cat or_ctor.l3
type Direction = North | South | East | West

let isVertical d =
    match d with
    | North | South -> true
    | East | West -> false
let result = (isVertical North, isVertical East)

$ langthree or_ctor.l3
(true, false)
```

### 문자열 Or-패턴

문자열 패턴과 조합하면 강력한 디스패치가 가능합니다:

```
$ cat or_string.l3
let respond input =
    match input with
    | "yes" | "y" | "ok" -> true
    | "no" | "n" -> false
    | _ -> false
let result = (respond "yes", respond "n", respond "maybe")

$ langthree or_string.l3
(true, false, false)
```

### 소진 검사와 Or-패턴

Or-패턴은 소진 검사(exhaustiveness)에 올바르게 통합됩니다.
각 대안이 별도의 패턴으로 취급되어, or-패턴으로 모든 경우를 커버하면 경고가 나오지 않습니다:

```
$ cat or_exhaust.l3
type Color = Red | Green | Blue

let name c =
    match c with
    | Red -> "red"
    | Green | Blue -> "cool"
let result = name Red

$ langthree or_exhaust.l3
"red"
```

위 예제에서 `Green | Blue`가 나머지 모든 경우를 커버하므로 소진 경고가 없습니다.

### 제한 사항

- Or-패턴은 **최상위 레벨에서만** 지원됩니다. 중첩된 or-패턴 (`Some (1 | 2)`)은 아직 지원되지 않습니다.
- Or-패턴 내에서 변수 바인딩은 허용되지 않습니다. 상수와 생성자 패턴만 사용하세요.

## When 가드 (When Guards)

`when`을 사용하여 패턴에 불리언 조건을 추가합니다. 가드는
패턴이 매칭된 후에 평가됩니다. 가드가 실패하면 다음 절(clause)로
매칭이 계속됩니다.

```
$ cat guard.l3
let classify n =
    match n with
    | x when x > 0 -> "positive"
    | 0 -> "zero"
    | _ -> "negative"
let result = classify 5

$ langthree guard.l3
"positive"
```

### 범위 분류를 위한 가드 (Guards for Range Classification)

여러 가드를 사용하여 범위 기반 디스패치를 만들 수 있습니다:

```
$ cat grade.l3
let grade score =
    match score with
    | s when s >= 90 -> "A"
    | s when s >= 80 -> "B"
    | s when s >= 70 -> "C"
    | s when s >= 60 -> "D"
    | _ -> "F"
let result = grade 85

$ langthree grade.l3
"B"
```

### 생성자와 가드 결합

구조적 매칭과 값 조건을 결합합니다:

```
$ cat shape_guard.l3
type Shape = Circle of int | Rect of int * int

let isLarge s =
    match s with
    | Circle r when r > 10 -> true
    | Rect (w, h) when w * h > 100 -> true
    | _ -> false

let r1 = isLarge (Circle 15)
let r2 = isLarge (Circle 5)
let r3 = isLarge (Rect (20, 10))
let result = (r1, r2, r3)

$ langthree shape_guard.l3
(true, false, true)
```

### 가드 폴스루 (Guard Fallthrough)

가드가 실패하면 기본 케이스로 건너뛰는 것이 **아니라** 다음 절로
매칭이 계속됩니다. 이를 통해 계층적 조건을 구성할 수 있습니다:

```
$ cat fallthrough.l3
let classify x =
    match x with
    | n when n > 100 -> "large"
    | n when n > 10 -> "medium"
    | n when n > 0 -> "small"
    | 0 -> "zero"
    | _ -> "negative"
let result = classify 50

$ langthree fallthrough.l3
"medium"
```

## 계산된 값에 대한 Match

변수뿐만 아니라 표현식의 결과에 대해서도 매칭할 수 있습니다:

```
$ cat match_expr.l3
let abs x = if x < 0 then 0 - x else x
let classify x =
    match abs x with
    | 0 -> "zero"
    | n when n < 10 -> "small"
    | n when n < 100 -> "medium"
    | _ -> "large"
let result = classify (0 - 42)

$ langthree match_expr.l3
"medium"
```

## 완전성 검사 (Exhaustiveness Checking)

컴파일러는 누락된 케이스에 대해 경고합니다 (W0001):

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

### 중복 경고 (Redundancy Warnings)

컴파일러는 도달 불가능한 패턴에 대해서도 경고합니다 (W0002):

```
$ cat redundant.l3
let result =
    match 1 with
    | _ -> "catch all"
    | 1 -> "one"

$ langthree redundant.l3
Warning: warning[W0002]: Redundant pattern match clause. This pattern is never reached
 --> :0:0-1:0
   = hint: Remove this clause or reorder the patterns
"catch all"
```

와일드카드 `_`가 모든 값을 잡으므로, `| 1` 절은 도달 불가능합니다.

### ADT 튜플의 완전성 검사

패턴 완전성 검사는 중첩된 구조에서도 작동합니다:

```
$ cat color_mix.l3
type Color = Red | Green | Blue

let mix a b =
    match (a, b) with
    | (Red, Blue) -> "purple"
    | (Blue, Red) -> "purple"
    | (Red, Green) -> "yellow"
    | (Green, Red) -> "yellow"
    | (Blue, Green) -> "cyan"
    | (Green, Blue) -> "cyan"
    | _ -> "same"

let result = mix Red Blue

$ langthree color_mix.l3
"purple"
```

## Let 패턴 구조 분해 (Let-Pattern Destructuring)

전체 `match` 없이 구조 분해할 수 있습니다:

```
funlang> let (x, y) = (1, 2) in x + y
3

funlang> let (a, b, c) = (1, 2, 3) in a + b + c
6
```

## 결정 트리 컴파일 (Decision Tree Compilation)

LangThree는 Jules Jacobs 알고리즘을 사용하여 패턴 매칭을
이진 결정 트리(binary decision tree)로 컴파일합니다. 이는 다음을 의미합니다:

- **중복 테스트 없음:** 각 생성자는 실행 경로당 최대 한 번만 테스트됩니다
- **효율적인 디스패치:** match당 O(depth)이며, O(clauses)가 아닙니다
- **절 공유:** 공통 하위 패턴이 결정 노드를 공유합니다

정확성을 위해 이를 신경 쓸 필요는 없지만, 많은 절이 있는 복잡한
매칭도 효율적으로 처리된다는 것을 의미합니다.

## 실전 예제

### 재귀적 리스트 처리

리스트 재귀의 표준 패턴: 빈 리스트와 cons를 매칭하고, 꼬리(tail)에 대해 재귀합니다.

**리스트 합계:**

```
funlang> let rec sum xs = match xs with | [] -> 0 | x :: rest -> x + sum rest in sum [1, 2, 3, 4, 5]
15
```

**요소 개수 세기:**

```
funlang> let rec length xs = match xs with | [] -> 0 | _ :: rest -> 1 + length rest in length [10, 20, 30]
3
```

**조건 함수로 필터링 (클로저 캡처):**

```
funlang> let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t in filter (fun x -> x > 3) [1, 2, 3, 4, 5, 6]
[4, 5, 6]
```

**조건이 참인 동안 가져오기(take while):**

```
$ cat take_while.l3
let result =
    let rec takeWhile pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: takeWhile pred t else []
    in takeWhile (fun x -> x < 5) [1, 2, 3, 4, 5, 6, 7]

$ langthree take_while.l3
[1, 2, 3, 4]
```

### ADT 표현식 평가기 (ADT Expression Evaluator)

패턴 매칭은 재귀적 ADT 순회에서 특히 빛을 발합니다:

```
$ cat expr_eval.l3
type Expr = Num of int | Add of Expr * Expr | Mul of Expr * Expr

let result =
    let rec eval e = match e with | Num n -> n | Add (a, b) -> eval a + eval b | Mul (a, b) -> eval a * eval b
    in eval (Add (Mul (Num 3, Num 4), Num 5))

$ langthree expr_eval.l3
17
```

`eval (Add (Mul (Num 3, Num 4), Num 5))`는 `(3 * 4) + 5 = 17`을 계산합니다.

### 연관 리스트에서 조회 (Lookup in Association List)

튜플 리스트에 대한 패턴 매칭으로 키-값 조회를 수행합니다:

```
$ cat lookup.l3
let result =
    let rec lookup key = fun xs -> match xs with | [] -> None | (k, v) :: rest -> if k = key then Some v else lookup key rest
    in
    let env = [(1, "one"), (2, "two"), (3, "three")]
    in
    let r1 = lookup 2 env
    in
    let r2 = lookup 9 env
    in (r1, r2)

$ langthree lookup.l3
(Some "two", None)
```

### 패턴 매칭을 이용한 트리 순회

모든 트리 연산은 자연스러운 패턴 매칭으로 표현됩니다:

```
$ cat tree_ops.l3
type Tree = Leaf | Node of Tree * int * Tree

let result =
    let rec depth t = match t with | Leaf -> 0 | Node (l, _, r) -> let dl = depth l in let dr = depth r in if dl > dr then dl + 1 else dr + 1
    in
    let rec size t = match t with | Leaf -> 0 | Node (l, _, r) -> 1 + size l + size r
    in
    let rec sumTree t = match t with | Leaf -> 0 | Node (l, v, r) -> sumTree l + v + sumTree r
    in
    let t = Node (Node (Leaf, 1, Leaf), 2, Node (Leaf, 3, Node (Leaf, 4, Leaf)))
    in (depth t, size t, sumTree t)

$ langthree tree_ops.l3
(3, 4, 10)
```

### 패턴 매칭을 이용한 삽입 정렬 (Insertion Sort)

두 개의 재귀 함수를 연결합니다:

```
$ cat isort.l3
let sorted =
    let rec insert x = fun xs -> match xs with | [] -> x :: [] | h :: t -> if x <= h then x :: h :: t else h :: insert x t
    in
    let rec sort xs = match xs with | [] -> [] | h :: t -> insert h (sort t)
    in sort [5, 3, 8, 1, 9, 2, 7, 4, 6]

let result = sorted

$ langthree isort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

## 요약

| 패턴 | 구문 | 예제 |
|------|------|------|
| 상수 | `0`, `true` | `\| 0 -> "zero"` |
| 문자열 | `"hello"` | `\| "hello" -> "hi"` |
| 변수 | `x` | `\| x -> x + 1` |
| 와일드카드 | `_` | `\| _ -> "default"` |
| 튜플 | `(a, b)` | `\| (x, y) -> x + y` |
| 빈 리스트 | `[]` | `\| [] -> "empty"` |
| 리스트 cons | `h :: t` | `\| x :: rest -> x` |
| 생성자 | `Some x` | `\| Some v -> v` |
| 레코드 | `{ x = a }` | `\| { x = a } -> a` |
| 중첩 | `Some (x :: _)` | `\| Some (h :: _) -> h` |
| Or-패턴 | `1 \| 2 \| 3` | `\| 1 \| 2 \| 3 -> "small"` |
| 가드 포함 | `x when cond` | `\| n when n > 0 -> "pos"` |
