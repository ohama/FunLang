# 14장: Algorithms and Data Structures

이 장에서는 LangThree에서 전형적인 알고리즘과 자료구조를 구현하는 방법을 보여줍니다.
OCaml, Haskell 또는 유사한 언어로 함수형 프로그래밍 경험이 있다면 익숙한 패턴이겠지만,
LangThree만의 고유한 관용구를 이해할 필요가 있습니다.

## LangThree의 재귀 패턴

LangThree의 모든 재귀는 `in`과 함께 **표현식 수준**에서 `let rec`을 사용합니다.
모듈 수준의 `let rec`은 없습니다. 재귀 함수는 표현식 내부에서 정의되며 서로 체이닝됩니다:

```
let result =
    let rec f x = ...
    in f 10
```

재귀 함수가 **두 개 이상의 매개변수**를 필요로 할 때, 두 번째 매개변수는
클로저를 통해 전달됩니다:

```
let result =
    let rec f x = fun y -> ... f something_else another_thing ...
    in f 1 2
```

여러 재귀 헬퍼는 `let rec ... in let rec ... in`으로 체이닝합니다:

```
let answer =
    let rec helper1 x = ...
    in
    let rec helper2 y = ... helper1 ... helper2 ...
    in helper2 start
```

이 패턴은 이 장 전체에 걸쳐 등장합니다. 각 알고리즘은 독립적으로 구성되어 있습니다:
`let rec ... in`으로 헬퍼를 정의한 후, 마지막에 진입점을 호출합니다.

## 정수론

### 팩토리얼

전형적인 재귀 팩토리얼입니다. `let rec`은 표현식 내부에 있어야 합니다
-- 여기서는 `let result = ...` 바인딩 안에 위치합니다:

```
$ cat factorial.l3
let result =
    let rec fact n = if n <= 1 then 1 else n * fact (n - 1)
    in fact 10

$ langthree factorial.l3
3628800
```

`fact 10`은 10 * 9 * 8 * ... * 1 = 3628800을 계산합니다.

### 피보나치

단순 재귀 피보나치입니다. 이중 재귀로 인해 지수 시간이 걸리지만,
패턴을 명확하게 보여줍니다:

```
$ cat fibonacci.l3
let result =
    let rec fib n = if n <= 1 then n else fib (n - 1) + fib (n - 2)
    in fib 10

$ langthree fibonacci.l3
55
```

수열은 0, 1, 1, 2, 3, 5, 8, 13, 21, 34, **55**입니다.

### GCD와 LCM

유클리드 알고리즘으로 최대공약수를 구한 다음, 이를 이용해 최소공배수를 유도합니다.
`gcd`는 두 개의 매개변수가 필요하고 재귀적이므로, 두 번째 매개변수는
클로저(`fun b -> ...`)를 통해 전달됩니다:

```
$ cat gcd_lcm.l3
let result =
    let rec gcd a = fun b -> if b = 0 then a else gcd b (a - (a / b) * b)
    in
    let lcm = fun a -> fun b -> a / gcd a b * b
    in (gcd 48 36, lcm 12 18)

$ langthree gcd_lcm.l3
(12, 36)
```

`gcd 48 36`의 축약: gcd 48 36 -> gcd 36 12 -> gcd 12 0 -> 12.
`lcm 12 18`은 12 / gcd(12,18) * 18 = 12 / 6 * 18 = 36을 계산합니다.

`a - (a / b) * b`는 나머지(modulo) 연산입니다. LangThree의 `/`는 정수 나눗셈이기
때문입니다.

### 거듭제곱

반복 곱셈으로 `base^exp`를 계산합니다:

```
$ cat power.l3
let result =
    let rec power base = fun exp -> if exp = 0 then 1 else base * power base (exp - 1)
    in power 2 10

$ langthree power.l3
1024
```

`power 2 10`은 2^10 = 1024를 계산합니다. 두 매개변수 재귀 패턴은
`gcd`와 동일합니다: 첫 번째 매개변수 `base`가 `let rec` 매개변수이고,
두 번째 `exp`는 `fun exp -> ...`를 통해 전달됩니다.

## 리스트 유틸리티

### Reverse

꼬리 재귀 누적자를 사용하여 리스트를 뒤집습니다. 누적자 `acc`가
`let rec` 매개변수이고, 리스트 `xs`는 클로저를 통해 전달됩니다:

```
$ cat reverse.l3
let result =
    let rec rev acc = fun xs -> match xs with | [] -> acc | h :: t -> rev (h :: acc) t
    in rev [] [1, 2, 3, 4, 5]

$ langthree reverse.l3
[5, 4, 3, 2, 1]
```

이것은 꼬리 재귀입니다: 각 단계에서 head를 누적자에 cons하고 tail에 대해 재귀합니다.
스택이 증가하지 않습니다.

### Flatten

리스트의 리스트를 단일 리스트로 평탄화합니다. `app` (append) 헬퍼가 필요합니다:

```
$ cat flatten.l3
let result =
    let rec app xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: app t ys
    in
    let rec flatten xss = match xss with | [] -> [] | xs :: rest -> app xs (flatten rest)
    in flatten [[1, 2], [3, 4, 5], [], [6]]

$ langthree flatten.l3
[1, 2, 3, 4, 5, 6]
```

`flatten`은 각 하위 리스트를 처리하여 평탄화된 나머지에 덧붙입니다.

### Zip

두 리스트를 원소별로 쌍의 리스트로 결합합니다. 중첩된 match가 어느 한 리스트가
끝나는 경우를 처리합니다:

```
$ cat zip.l3
let result =
    let rec zip xs = fun ys -> match xs with | [] -> [] | x :: xt -> match ys with | [] -> [] | y :: yt -> (x, y) :: zip xt yt
    in zip [1, 2, 3] [10, 20, 30]

$ langthree zip.l3
[(1, 10), (2, 20), (3, 30)]
```

리스트의 길이가 다르면 `zip`은 짧은 쪽에서 멈춥니다.

### 리스트의 최댓값

최대 원소를 찾습니다. 기저 사례는 단일 원소 리스트 `x :: []`를 매칭합니다:

```
$ cat max_list.l3
let result =
    let rec maxList xs = match xs with | x :: [] -> x | x :: rest -> let m = maxList rest in if x > m then x else m
    in maxList [3, 7, 2, 9, 1, 8, 4]

$ langthree max_list.l3
9
```

### Fold (Left)

왼쪽 폴드는 리스트에 이항 함수를 적용하며 누적자를 전달합니다.
함수형 프로그래밍의 핵심 도구로, 다른 많은 연산을 폴드로 표현할 수 있습니다:

```
$ cat fold.l3
let result =
    let rec fold f = fun acc -> fun xs -> match xs with | [] -> acc | h :: t -> fold f (f acc h) t
    in fold (fun acc -> fun x -> acc + x * x) 0 [1, 2, 3, 4, 5]

$ langthree fold.l3
55
```

이 폴드는 0 + 1*1 + 2*2 + 3*3 + 4*4 + 5*5 = 1 + 4 + 9 + 16 + 25 = 55를 계산합니다.

`fold`는 중첩 클로저를 통해 세 개의 매개변수를 받습니다: 함수 `f`가
`let rec` 매개변수이고, `acc`는 첫 번째 `fun`을 통해, `xs`는 두 번째 `fun`을 통해
전달됩니다.

### Fold를 이용한 Map

`fold`가 있으면 그 위에 `map`을 구축할 수 있습니다:

```
$ cat map_via_fold.l3
let result =
    let rec fold f = fun acc -> fun xs -> match xs with | [] -> acc | h :: t -> fold f (f acc h) t
    in
    let rec rev acc = fun xs -> match xs with | [] -> acc | h :: t -> rev (h :: acc) t
    in
    let map f = fun xs -> rev [] (fold (fun acc -> fun x -> f x :: acc) [] xs)
    in map (fun x -> x * x) [1, 2, 3, 4, 5]

$ langthree map_via_fold.l3
[1, 4, 9, 16, 25]
```

폴드가 결과를 역순으로 생성하므로, 마지막에 뒤집습니다.

### Length

리스트의 원소 수를 셉니다:

```
funlang> let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t in length [10, 20, 30, 40]
4
```

## 정렬 알고리즘

정렬은 알고리즘적 접근 방식을 비교하기에 좋은 주제입니다. 아래 세 가지 정렬 모두
정렬되지 않은 리스트를 받아 새로운 정렬된 리스트를 반환합니다 -- 변이(mutation)가
관여하지 않습니다.

### 삽입 정렬

각 원소를 올바른 위치에 삽입하여 정렬된 리스트를 구축합니다.
두 개의 재귀 헬퍼가 있습니다: `insert`는 원소 하나를 배치하고, `sort`는 리스트를
처리합니다:

```
$ cat insertion_sort.l3
let sorted =
    let rec insert x = fun xs -> match xs with | [] -> x :: [] | h :: t -> if x <= h then x :: h :: t else h :: insert x t
    in
    let rec sort xs = match xs with | [] -> [] | h :: t -> insert h (sort t)
    in sort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree insertion_sort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

`insert`는 올바른 위치를 찾을 때까지 정렬된 리스트를 순회합니다. `sort`는 tail을
재귀적으로 정렬한 후 head를 삽입합니다. 최악의 경우 O(n^2)이지만, 단순하고 안정적입니다.

### 퀵정렬

피벗을 기준으로 분할하고, 각 반쪽을 재귀적으로 정렬한 후 합칩니다.
세 개의 헬퍼가 필요합니다 -- `filter`, `app` (append), 그리고 `qsort` 자체:

```
$ cat quicksort.l3
let sorted =
    let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t
    in
    let rec app xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: app t ys
    in
    let rec qsort xs = match xs with | [] -> [] | pivot :: rest -> let lo = filter (fun x -> x < pivot) rest in let hi = filter (fun x -> x >= pivot) rest in app (qsort lo) (pivot :: qsort hi)
    in qsort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree quicksort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

피벗은 단순히 리스트의 head입니다. `filter`가 피벗보다 작은 원소(`lo`)와
크거나 같은 원소(`hi`)를 선택합니다. 정렬된 결과는
`qsort lo ++ [pivot] ++ qsort hi`입니다.

### 병합 정렬

리스트를 반으로 나누고, 각 반쪽을 정렬한 후 병합합니다. 여러 헬퍼가 필요합니다
-- `length`, `take`, `drop`, `merge`, 그리고 `msort`:

```
$ cat merge_sort.l3
let sorted =
    let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t
    in
    let rec take n = fun xs -> if n = 0 then [] else match xs with | [] -> [] | h :: t -> h :: take (n - 1) t
    in
    let rec drop n = fun xs -> if n = 0 then xs else match xs with | [] -> [] | _ :: t -> drop (n - 1) t
    in
    let rec merge xs = fun ys -> match xs with | [] -> ys | x :: xt -> match ys with | [] -> xs | y :: yt -> if x <= y then x :: merge xt (y :: yt) else y :: merge (x :: xt) yt
    in
    let rec msort xs = let len = length xs in if len <= 1 then xs else let mid = len / 2 in merge (msort (take mid xs)) (msort (drop mid xs))
    in msort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree merge_sort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

이것은 하향식 병합 정렬입니다. `take`과 `drop`이 리스트를 중간점에서 분할합니다.
`merge`는 head를 비교하여 두 정렬된 리스트를 교차 배치합니다. O(n log n)이 보장됩니다.

`let rec ... in` 체인이 헬퍼 라이브러리를 구축하는 방식에 주목하세요. 이후 함수는
이전에 정의된 모든 함수를 호출할 수 있습니다. 이것이 복잡한 프로그램을 위한
LangThree의 관용적 패턴입니다.

## 트리 자료구조

### 이진 탐색 트리와 트리 정렬

이진 트리 타입을 정의한 후, 삽입, 구축, 중위 순회를 구현합니다.
결과는 트리 기반 정렬입니다:

```
$ cat tree_sort.l3
type Tree = Leaf | Node of Tree * int * Tree

let sorted =
    let rec treeInsert x = fun t -> match t with | Leaf -> Node (Leaf, x, Leaf) | Node (l, v, r) -> if x <= v then Node (treeInsert x l, v, r) else Node (l, v, treeInsert x r)
    in
    let rec buildTree xs = match xs with | [] -> Leaf | h :: t -> treeInsert h (buildTree t)
    in
    let rec app xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: app t ys
    in
    let rec inorder t = match t with | Leaf -> [] | Node (l, v, r) -> app (inorder l) (v :: inorder r)
    in inorder (buildTree [5, 3, 8, 1, 9, 2, 7])

$ langthree tree_sort.l3
[1, 2, 3, 5, 7, 8, 9]
```

`Tree` 타입은 두 개의 생성자를 가진 대수적 데이터 타입입니다: `Leaf` (비어 있음)와
왼쪽 하위 트리, 값, 오른쪽 하위 트리를 포함하는 `Node`.

`treeInsert`는 노드 값과 비교하여 왼쪽 또는 오른쪽으로 재귀하며 BST에 값을 배치합니다.
`buildTree`는 각 원소를 삽입하여 리스트를 트리로 접습니다.
`inorder` 순회는 정렬된 리스트를 생성합니다.

### 페아노 자연수와 Church 스타일 산술

자연수를 대수적 타입으로 표현하고, 구조적 재귀로 덧셈과 곱셈을 정의합니다:

```
$ cat peano.l3
type Nat = Zero | Succ of Nat

let result =
    let rec toInt n = match n with | Zero -> 0 | Succ p -> 1 + toInt p
    in
    let rec add a = fun b -> match a with | Zero -> b | Succ p -> Succ (add p b)
    in
    let rec mul a = fun b -> match a with | Zero -> Zero | Succ p -> add b (mul p b)
    in
    let three = Succ (Succ (Succ Zero))
    in
    let four = Succ (Succ (Succ (Succ Zero)))
    in toInt (mul three four)

$ langthree peano.l3
12
```

`add`는 `a`에서 `Succ`를 벗기고 결과를 감쌉니다: add (Succ (Succ Zero)) b
= Succ (Succ b). `mul`은 반복 덧셈을 사용합니다: mul (Succ (Succ Zero)) b =
add b (add b Zero).

3 곱하기 4는 12입니다 -- `toInt`로 `int`로 변환하여 검증합니다.

이 예제는 LangThree의 대수적 타입과 패턴 매칭이 사용자 정의 수 표현을 정의하고
계산하는 데 얼마나 자연스러운지 보여줍니다.

## 실용 예제

### 균형 괄호 검사기

괄호 시퀀스가 균형 잡혀 있는지 확인합니다. LangThree에는 char 타입이 없으므로,
`(`를 1로, `)`를 0으로 인코딩합니다:

```
$ cat balanced.l3
let result =
    let rec check depth = fun cs -> match cs with | [] -> depth = 0 | c :: rest -> if c = 1 then check (depth + 1) rest else if c = 0 then if depth > 0 then check (depth - 1) rest else false else check depth rest
    in (check 0 [1, 1, 0, 0], check 0 [1, 0, 0, 1])

$ langthree balanced.l3
(true, false)
```

첫 번째 시퀀스 `(())`는 균형이 맞으므로 `check`가 `true`를 반환합니다. 두 번째
시퀀스 `())(` 는 대응하는 여는 괄호 없이 닫는 괄호가 나타나므로, `depth`가
음수가 될 때 `check`가 즉시 `false`를 반환합니다.

이 알고리즘은 단일 패스입니다: `(`에서 depth를 증가시키고, `)`에서 감소시키며,
depth가 0 아래로 내려가면 실패하고, 끝에서 depth가 0일 때만 성공합니다.

### Option 체이닝과 안전한 나눗셈

Prelude의 `Option` 타입을 사용하여 실패할 수 있는 계산을 체이닝합니다:

```
$ cat safe_div.l3
let safediv a = fun b -> if b = 0 then None else Some (a / b)

let bind opt = fun f ->
    match opt with
    | None -> None
    | Some v -> f v

let result =
    let step1 = safediv 100 5 in
    let step2 = bind step1 (fun x -> safediv x 4) in
    let step3 = bind step2 (fun x -> safediv x 0) in
    (step1, step2, step3)

$ langthree safe_div.l3
(Some 20, Some 5, None)
```

`safediv 100 5`는 `Some 20`을 줍니다. 그것을 4로 나누면 `Some 5`가 됩니다.
0으로 나누면 `None`이 되며, `None`은 이후 `bind` 호출을 통해 전파됩니다.

### 수식 평가기

간단한 산술 수식 트리를 구축하고 평가합니다:

```
$ cat expr_eval.l3
type Expr =
    | Lit of int
    | Add of Expr * Expr
    | Mul of Expr * Expr

let result =
    let rec eval e = match e with | Lit n -> n | Add (a, b) -> eval a + eval b | Mul (a, b) -> eval a * eval b
    in eval (Add (Mul (Lit 3, Lit 4), Lit 5))

$ langthree expr_eval.l3
17
```

`Mul (Lit 3, Lit 4)`는 12로 평가되고, `Add (_, Lit 5)`는 17을 줍니다.
재귀 평가기는 트리 구조를 정확히 반영합니다 -- 생성자당 하나의 분기입니다.

### Filter와 Length를 이용한 카운팅

헬퍼를 조합하여 조건을 만족하는 원소의 수를 셉니다:

```
$ cat count_evens.l3
let result =
    let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t
    in
    let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t
    in
    let countWhere pred = fun xs -> length (filter pred xs)
    in countWhere (fun x -> x - (x / 2) * 2 = 0) [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

$ langthree count_evens.l3
5
```

1..10에는 5개의 짝수가 있습니다. `x - (x / 2) * 2 = 0` 조건은
정수 산술로 짝수 여부를 확인합니다 (`mod` 연산자가 없으므로).

## 요약

| 패턴 | 예제 |
|---|---|
| 단일 매개변수 재귀 | `let rec f x = ... f ... in f start` |
| 다중 매개변수 재귀 | `let rec f x = fun y -> ... f ... in f a b` |
| 체이닝된 헬퍼 | `let rec h1 ... in let rec h2 ... in h2 start` |
| 누적자 패턴 | `let rec go acc = fun xs -> ... go (acc') t in go init list` |
| ADT + 재귀 | `type T = ... let rec f t = match t with ...` |
| Option 체이닝 | `bind (safediv a b) (fun x -> safediv x c)` |

핵심 요점:

- **`let rec`은 표현식 수준에서만 사용 가능합니다.** 모든 재귀는
  `let name = let rec ... in ...` 바인딩 안에 위치합니다.
- **다중 매개변수 재귀 함수**는 두 번째 매개변수부터 `fun`을 사용합니다:
  `let rec f x = fun y -> ...`.
- **헬퍼 체이닝**은 `let rec ... in let rec ... in`으로 합니다. 각 헬퍼는
  이전에 정의된 모든 헬퍼를 호출할 수 있습니다.
- **대수적 데이터 타입**은 재귀 함수와 자연스럽게 결합되어 트리, 수식 언어,
  사용자 정의 수 타입에 활용됩니다.
- **Prelude의 `Option` 타입**은 예외 없이 안전한 계산을 위해 `None`과 `Some`을
  제공합니다.
