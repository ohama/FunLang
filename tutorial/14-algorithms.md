# 14장: 알고리즘과 자료구조

LangThree v1.4에서 알고리즘을 구현하는 방법을 살펴봅니다. 이번 버전은
재귀 함수 작성 방식에 근본적인 변화를 가져왔습니다.

## v1.4의 변화: 모듈 레벨 `let rec`

v1.3까지 LangThree의 모든 재귀 함수는 표현식 내부에 갇혀 있었습니다.
정렬 알고리즘 하나를 작성하려면 `let sorted = let rec insert ... in let rec sort ... in sort [...]`
같은 거대한 단일 표현식이 필요했습니다. 헬퍼 함수가 늘어날수록 들여쓰기와
`in` 체인이 깊어져 가독성이 떨어졌습니다.

v1.4에서는 **모듈 레벨 `let rec`** 이 도입되었습니다. 이제 각 재귀 함수를
독립된 최상위 선언으로 작성할 수 있습니다:

```
(* v1.3 스타일 -- 모든 것이 하나의 표현식 안에 *)
let sorted =
    let rec insert x = fun xs -> ...
    in
    let rec sort xs = ...
    in sort [5, 3, 1]

(* v1.4 스타일 -- 각 함수가 독립된 선언 *)
let rec insert x = fun xs -> ...
let rec sort xs = ...
let result = sort [5, 3, 1]
```

이 변화가 가져오는 이점은 명확합니다:

- **가독성**: 각 함수가 독립된 선언이므로 한눈에 파악할 수 있습니다.
- **재사용성**: 모듈 레벨 함수는 파일 내 어디서든 호출할 수 있습니다.
- **유지보수**: 함수를 추가하거나 수정할 때 `in` 체인을 조정할 필요가 없습니다.

v1.4에서 추가된 다른 기능들도 알고리즘 작성을 크게 개선합니다:

- **리스트 범위**: `[1..100]`으로 연속 정수 리스트를 간편하게 생성
- **상호 재귀**: `let rec f = ... and g = ...`로 서로를 호출하는 함수 정의
- **Or 패턴**: 패턴 매칭에서 여러 패턴을 하나로 묶기

모듈 레벨 `let rec`의 제약 사항을 기억하세요:

- 매개변수는 **하나만** 직접 받습니다: `let rec f x = body`
- 두 번째 매개변수부터는 클로저로 전달합니다: `let rec f x = fun y -> body`
- `match` 표현식은 한 줄에 작성해야 합니다

이제 이 새로운 기능들을 활용한 알고리즘을 하나씩 살펴보겠습니다.

## 표준 리스트 함수

> **참고:** `map`, `filter`, `fold`, `length`, `reverse`, `append` 등은 이제 Prelude에서
> 제공됩니다. 아래 예제에서는 구현 방법을 보여주기 위해 직접 정의하지만,
> 실제 프로그램에서는 Prelude 함수를 바로 사용할 수 있습니다.

함수형 프로그래밍에서 `map`, `filter`, `fold`는 가장 기본적인 도구입니다.
v1.4에서는 이들을 모듈 레벨에 선언하여 프로그램 전체에서 재사용할 수 있습니다.

### Map

리스트의 각 원소에 함수를 적용하여 새 리스트를 만듭니다:

```
$ cat map.l3
let rec map f = fun xs -> match xs with | [] -> [] | h :: t -> f h :: map f t

let result = map (fun x -> x * x) [1, 2, 3, 4, 5]

$ langthree map.l3
[1, 4, 9, 16, 25]
```

`map`은 두 개의 매개변수를 받습니다. 첫 번째 `f`가 `let rec` 매개변수이고,
두 번째 `xs`는 `fun xs -> ...`를 통해 전달됩니다. 빈 리스트가 기저 사례이며,
비어 있지 않으면 head에 `f`를 적용하고 tail에 대해 재귀합니다.

### Filter

조건을 만족하는 원소만 남깁니다:

```
$ cat filter.l3
let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t

let result = filter (fun x -> x % 2 = 0) [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

$ langthree filter.l3
[2, 4, 6, 8, 10]
```

`x % 2 = 0`은 `%` (모듈로) 연산자로 짝수를 판별합니다. v1.5에서 `%` 연산자가
도입되어, 이전의 `x - (x / 2) * 2` 패턴 대신 간결하게 나머지를 구할 수 있습니다.

### Fold (왼쪽 폴드)

왼쪽 폴드는 리스트를 하나의 값으로 축약합니다. 이항 함수 `f`, 초기 누적자 `acc`,
리스트 `xs` 세 개의 매개변수를 받습니다:

```
$ cat fold.l3
let rec fold f = fun acc -> fun xs -> match xs with | [] -> acc | h :: t -> fold f (f acc h) t

let result = fold (fun acc -> fun x -> acc + x * x) 0 [1, 2, 3, 4, 5]

$ langthree fold.l3
55
```

세 개의 매개변수가 중첩 클로저로 전달됩니다: `f`가 `let rec` 매개변수,
`acc`가 첫 번째 `fun`, `xs`가 두 번째 `fun`입니다.
계산 과정은 0 + 1 + 4 + 9 + 16 + 25 = 55입니다.

### Prelude를 활용한 간결한 코드

Prelude 함수를 사용하면 알고리즘을 더 간결하게 작성할 수 있습니다:

```
$ cat sieve_prelude.l3
let rec sieve xs = match xs with | [] -> [] | p :: rest -> p :: sieve (filter (fun n -> n % p <> 0) rest)

let result = sieve [2..50]

$ langthree sieve_prelude.l3
[2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47]
```

`filter`는 Prelude에서 제공되므로 재정의할 필요 없이, `sieve` 함수만 작성하면
에라토스테네스의 체를 구현할 수 있습니다.

## 수론

### 팩토리얼

가장 기본적인 재귀 알고리즘입니다. 모듈 레벨 `let rec`으로 깔끔하게 표현됩니다:

```
$ cat factorial.l3
let rec fact n = if n <= 1 then 1 else n * fact (n - 1)

let result = fact 10

$ langthree factorial.l3
3628800
```

`fact 10`은 10 * 9 * 8 * ... * 1 = 3,628,800을 계산합니다. v1.3에서는
`let result = let rec fact n = ... in fact 10`이라는 한 덩어리로 작성해야 했지만,
이제 함수 정의와 호출이 분리되어 훨씬 읽기 좋습니다.

### 피보나치 수열

단순 재귀 피보나치를 리스트 범위와 `map`을 결합하여 수열 전체를 출력합니다:

```
$ cat fibonacci.l3
let rec fib n = if n <= 1 then n else fib (n - 1) + fib (n - 2)
let rec map f = fun xs -> match xs with | [] -> [] | h :: t -> f h :: map f t

let result = map fib [0..15]

$ langthree fibonacci.l3
[0, 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610]
```

`[0..15]`는 v1.4의 리스트 범위 기능으로, 0부터 15까지 16개의 정수 리스트를
자동으로 생성합니다. 이전에는 이런 리스트를 수동으로 나열해야 했습니다.
`map fib [0..15]`는 각 인덱스에 대한 피보나치 값을 계산합니다.

### GCD와 LCM

유클리드 알고리즘으로 최대공약수를 구하고, 이를 이용해 최소공배수를 유도합니다:

```
$ cat gcd_lcm.l3
let rec gcd a = fun b -> if b = 0 then a else gcd b (a % b)

let lcm a = fun b -> a / gcd a b * b

let result = (gcd 48 36, lcm 12 18)

$ langthree gcd_lcm.l3
(12, 36)
```

`gcd 48 36`의 축약: gcd 48 36 -> gcd 36 12 -> gcd 12 0 -> 12.
`lcm 12 18`은 12 / gcd(12,18) * 18 = 12 / 6 * 18 = 36입니다.

`a % b`는 나머지(모듈로) 연산자입니다. v1.5에서 `%` 연산자가 도입되어
이전의 `a - (a / b) * b` 패턴 대신 간결하게 나머지를 구할 수 있습니다.
`lcm`은 재귀가 아니므로 `let rec` 없이 일반 `let`으로 정의합니다.

### 서로소 (Coprimes)

GCD와 리스트 범위를 결합하면 주어진 수와 서로소인 수를 구할 수 있습니다:

```
$ cat coprimes.l3
let rec gcd a = fun b -> if b = 0 then a else gcd b (a % b)
let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t

let coprimes n = filter (fun k -> gcd n k = 1) [1..n]
let result = coprimes 12

$ langthree coprimes.l3
[1, 5, 7, 11]
```

`[1..12]` 중에서 12와 GCD가 1인 수만 남깁니다. 이것이 오일러 토션트 함수
phi(12) = 4의 구체적인 원소들입니다. 리스트 범위 덕분에 `[1..n]`으로
간결하게 후보를 생성할 수 있습니다.

### 소수 판별 (isPrime)

주어진 범위에서 소수를 걸러냅니다:

```
$ cat is_prime.l3
let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t
let rec checkPrime n = fun d -> if d * d > n then true else if n % d = 0 then false else checkPrime n (d + 1)
let isPrime n = if n < 2 then false else checkPrime n 2

let result = filter (fun n -> isPrime n) [2..50]

$ langthree is_prime.l3
[2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47]
```

`checkPrime n d`는 2부터 sqrt(n)까지 나눠보며 소수를 판별합니다.
`isPrime`은 비재귀 래퍼로, 2 미만인 경우를 먼저 걸러냅니다.
`[2..50]`에서 소수만 필터링하여 50 이하의 모든 소수를 구합니다.

### 거듭제곱

반복 곱셈으로 `base^exp`를 계산합니다:

```
$ cat power.l3
let rec power base = fun exp -> if exp = 0 then 1 else base * power base (exp - 1)

let result = power 2 10

$ langthree power.l3
1024
```

`power 2 10`은 2^10 = 1024를 계산합니다.

## 정렬 알고리즘

정렬은 알고리즘을 비교하기 좋은 주제입니다. 모듈 레벨 `let rec` 덕분에
각 헬퍼 함수가 독립 선언이 되어, v1.3의 깊은 `let rec ... in` 체인보다
구조가 훨씬 명확해졌습니다.

### 삽입 정렬

각 원소를 정렬된 리스트의 올바른 위치에 삽입하여 정렬합니다:

```
$ cat insertion_sort.l3
let rec insert x = fun xs -> match xs with | [] -> x :: [] | h :: t -> if x <= h then x :: h :: t else h :: insert x t

let rec sort xs = match xs with | [] -> [] | h :: t -> insert h (sort t)

let result = sort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree insertion_sort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

`insert`와 `sort`가 각각 독립된 최상위 함수입니다. `insert`는 정렬된 리스트에서
올바른 위치를 찾을 때까지 순회하고, `sort`는 tail을 재귀적으로 정렬한 후
head를 삽입합니다. 최악의 경우 O(n^2)이지만 구현이 단순하고 안정적(stable)입니다.

### 퀵정렬

피벗을 기준으로 분할하고, 각 부분을 재귀 정렬한 후 합칩니다:

```
$ cat quicksort.l3
let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t

let rec append xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: append t ys

let rec qsort xs = match xs with | [] -> [] | pivot :: rest -> let lo = filter (fun x -> x < pivot) rest in let hi = filter (fun x -> x >= pivot) rest in append (qsort lo) (pivot :: qsort hi)

let result = qsort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree quicksort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

`filter`, `append`, `qsort` 세 함수가 각각 독립 선언입니다. 피벗은 리스트의
head를 사용합니다. `filter`로 피벗보다 작은 원소(`lo`)와 크거나 같은
원소(`hi`)를 분리한 후, 정렬된 결과를 `append (qsort lo) (pivot :: qsort hi)`로
조합합니다.

Prelude `++` 연산자를 사용하면 `append`를 재정의할 필요가 없어 더 간결합니다:

```
$ cat qsort_prelude.l3
let rec qsort xs = match xs with | [] -> [] | p :: rest -> qsort (filter (fun x -> x < p) rest) ++ [p] ++ qsort (filter (fun x -> x >= p) rest)

let result = qsort [5, 3, 8, 1, 9, 2, 7]

$ langthree qsort_prelude.l3
[1, 2, 3, 5, 7, 8, 9]
```

### 병합 정렬

리스트를 반으로 나누고, 각 반쪽을 정렬한 후 병합합니다. 여러 헬퍼 함수가
필요한 알고리즘에서 모듈 레벨 선언의 장점이 특히 드러납니다:

```
$ cat merge_sort.l3
let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t
let rec take n = fun xs -> if n = 0 then [] else match xs with | [] -> [] | h :: t -> h :: take (n - 1) t
let rec drop n = fun xs -> if n = 0 then xs else match xs with | [] -> [] | _ :: t -> drop (n - 1) t
let rec merge xs = fun ys -> match xs with | [] -> ys | x :: xt -> match ys with | [] -> xs | y :: yt -> if x <= y then x :: merge xt (y :: yt) else y :: merge (x :: xt) yt
let rec msort xs = let len = length xs in if len <= 1 then xs else let mid = len / 2 in merge (msort (take mid xs)) (msort (drop mid xs))

let result = msort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree merge_sort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

다섯 개의 함수가 각각 한 줄의 최상위 선언입니다. v1.3에서는 이 모든 함수가
`let sorted = let rec length ... in let rec take ... in let rec drop ... in let rec merge ... in let rec msort ... in msort [...]`
라는 하나의 거대한 표현식이었습니다. 이제 각 함수를 독립적으로 읽고 이해할 수 있습니다.

`take`과 `drop`이 리스트를 중간점에서 분할하고, `merge`가 두 정렬된 리스트의
head를 비교하며 교차 배치합니다. O(n log n)이 보장됩니다.

## 트리 자료구조

### 이진 탐색 트리와 트리 정렬

대수적 데이터 타입으로 이진 트리를 정의하고, 삽입/구축/순회를 구현하여
트리 기반 정렬을 만듭니다:

```
$ cat tree_sort.l3
type Tree = Leaf | Node of Tree * int * Tree

let rec treeInsert x = fun t -> match t with | Leaf -> Node (Leaf, x, Leaf) | Node (l, v, r) -> if x <= v then Node (treeInsert x l, v, r) else Node (l, v, treeInsert x r)
let rec buildTree xs = match xs with | [] -> Leaf | h :: t -> treeInsert h (buildTree t)
let rec append xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: append t ys
let rec inorder t = match t with | Leaf -> [] | Node (l, v, r) -> append (inorder l) (v :: inorder r)

let result = inorder (buildTree [5, 3, 8, 1, 9, 2, 7])

$ langthree tree_sort.l3
[1, 2, 3, 5, 7, 8, 9]
```

`Tree` 타입은 두 생성자를 가진 대수적 데이터 타입입니다: `Leaf`(빈 노드)와
`Node`(왼쪽 하위 트리, 값, 오른쪽 하위 트리). `treeInsert`는 노드 값과 비교하여
왼쪽 또는 오른쪽으로 재귀하며 BST 성질을 유지합니다. `buildTree`로 리스트를
트리로 변환하고, `inorder` 중위 순회로 정렬된 리스트를 추출합니다.

### 페아노 자연수

자연수를 대수적 타입으로 표현하고, 구조적 재귀로 덧셈과 곱셈을 정의합니다.
수학의 기초를 코드로 직접 표현하는 예제입니다:

```
$ cat peano.l3
type Nat = Zero | Succ of Nat

let rec toInt n = match n with | Zero -> 0 | Succ p -> 1 + toInt p
let rec add a = fun b -> match a with | Zero -> b | Succ p -> Succ (add p b)
let rec mul a = fun b -> match a with | Zero -> Zero | Succ p -> add b (mul p b)

let three = Succ (Succ (Succ Zero))
let four = Succ (Succ (Succ (Succ Zero)))
let result = toInt (mul three four)

$ langthree peano.l3
12
```

`add`는 `a`에서 `Succ`를 하나씩 벗기고 결과를 감쌉니다:
add (Succ (Succ Zero)) b = Succ (Succ b). `mul`은 반복 덧셈을 사용합니다:
mul (Succ (Succ Zero)) b = add b (add b Zero).

3 * 4 = 12를 `toInt`로 검증합니다. 모듈 레벨 선언 덕분에 `toInt`, `add`, `mul`이
각각 독립된 함수로 깔끔하게 정의됩니다.

## 새로운 알고리즘

v1.4의 새 기능들 -- 리스트 범위, 상호 재귀, or 패턴 -- 을 활용하는
알고리즘입니다. 이전 버전에서는 구현하기 어렵거나 불가능했던 것들입니다.

### 에라토스테네스의 체

고대 그리스의 소수 알고리즘입니다. 리스트 범위 `[2..50]`으로 후보를 생성하고,
가장 작은 수의 배수를 반복적으로 제거합니다:

```
$ cat sieve.l3
let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t
let rec sieve xs = match xs with | [] -> [] | p :: rest -> p :: sieve (filter (fun n -> n % p <> 0) rest)

let result = sieve [2..50]

$ langthree sieve.l3
[2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47]
```

`[2..50]`이 2부터 50까지의 리스트를 생성합니다. `sieve`는 리스트의 첫 원소 `p`를
소수로 확정하고, 나머지에서 `p`의 배수를 `filter`로 제거한 후 재귀합니다.
`n % p <> 0`은 `%` 연산자로 `n`이 `p`의 배수가 아닌지 검사합니다.

이 알고리즘은 리스트 범위 없이는 후보 리스트를 수동으로 나열해야 했을 것입니다.
`[2..50]` 한 표현으로 깔끔하게 해결됩니다.

### Collatz 수열

콜라츠 추측은 어떤 양의 정수에서 시작하든 "짝수면 반으로, 홀수면 3n+1"을
반복하면 결국 1에 도달한다는 것입니다. 수열을 추적합니다:

```
$ cat collatz.l3
let rec collatz n = fun acc -> if n = 1 then n :: acc else if n % 2 = 0 then collatz (n / 2) (n :: acc) else collatz (3 * n + 1) (n :: acc)
let rec rev acc = fun xs -> match xs with | [] -> acc | h :: t -> rev (h :: acc) t

let result = rev [] (collatz 27 [])

$ langthree collatz.l3
[27, 82, 41, 124, 62, 31, 94, 47, 142, 71, 214, 107, 322, 161, 484, 242, 121, 364, 182, 91, 274, 137, 412, 206, 103, 310, 155, 466, 233, 700, 350, 175, 526, 263, 790, 395, 1186, 593, 1780, 890, 445, 1336, 668, 334, 167, 502, 251, 754, 377, 1132, 566, 283, 850, 425, 1276, 638, 319, 958, 479, 1438, 719, 2158, 1079, 3238, 1619, 4858, 2429, 7288, 3644, 1822, 911, 2734, 1367, 4102, 2051, 6154, 3077, 9232, 4616, 2308, 1154, 577, 1732, 866, 433, 1300, 650, 325, 976, 488, 244, 122, 61, 184, 92, 46, 23, 70, 35, 106, 53, 160, 80, 40, 20, 10, 5, 16, 8, 4, 2, 1]
```

`collatz`는 꼬리 재귀 함수로, 누적자 `acc`에 각 단계의 값을 기록합니다.
결과가 역순으로 쌓이므로 `rev`로 뒤집습니다. 27에서 시작하면 111단계를 거쳐
1에 도달합니다. `n % 2 = 0`으로 짝수/홀수를 판별합니다.

### FizzBuzz

프로그래밍 면접의 고전 문제입니다. 리스트 범위와 `map`을 결합합니다:

```
$ cat fizzbuzz.l3
let rec map f = fun xs -> match xs with | [] -> [] | h :: t -> f h :: map f t

let fizzbuzz n =
    let r3 = n % 3
    in let r5 = n % 5
    in match (r3, r5) with
    | (0, 0) -> "FizzBuzz"
    | (0, _) -> "Fizz"
    | (_, 0) -> "Buzz"
    | _ -> to_string n

let result = map fizzbuzz [1..20]

$ langthree fizzbuzz.l3
["1", "2", "Fizz", "4", "Buzz", "Fizz", "7", "8", "Fizz", "Buzz", "11", "Fizz", "13", "14", "FizzBuzz", "16", "17", "Fizz", "19", "Buzz"]
```

`fizzbuzz`는 `%` 연산자로 3과 5의 나머지를 구하고 튜플로 만들어 패턴 매칭합니다.
`(0, 0)`이면 둘 다 나누어지므로 "FizzBuzz", `(0, _)`이면 3만 나누어지므로 "Fizz",
`(_, 0)`이면 5만 나누어지므로 "Buzz", 나머지는 숫자 자체를 문자열로 변환합니다.
`[1..20]`으로 1부터 20까지 범위를 생성하고 `map fizzbuzz`로 변환합니다.

### 상태 머신 (상호 재귀)

v1.4의 `let rec ... and ...` 구문은 서로를 호출하는 함수를 정의할 수 있게 합니다.
상태 머신은 상호 재귀의 대표적인 활용 사례입니다:

```
$ cat state_machine.l3
let rec stateA xs = match xs with | [] -> "ended in A" | 0 :: rest -> stateB rest | _ :: rest -> stateA rest
and stateB xs = match xs with | [] -> "ended in B" | 1 :: rest -> stateA rest | _ :: rest -> stateB rest

let r1 = stateA [1, 0, 1, 0]
let r2 = stateA [1, 0, 0]
let result = (r1, r2)

$ langthree state_machine.l3
(ended in B, ended in B)
```

두 상태 A, B 사이를 전이하는 간단한 오토마톤입니다:

- **상태 A**: 입력이 0이면 상태 B로 전이, 그 외에는 A에 머무름
- **상태 B**: 입력이 1이면 상태 A로 전이, 그 외에는 B에 머무름

`let rec stateA ... and stateB ...`로 두 함수가 서로를 호출할 수 있습니다.
이전 버전에서는 상호 재귀를 직접 표현할 수 없었기 때문에, 이런 패턴을
구현하려면 우회적인 방법이 필요했습니다.

첫 번째 입력 `[1, 0, 1, 0]`은 A -> A -> B -> A -> B 경로를 따라 상태 B에서
끝납니다. 두 번째 `[1, 0, 0]`은 A -> A -> B -> B로 역시 상태 B에서 끝납니다.

## 요약

| 패턴 | v1.4 문법 |
|---|---|
| 모듈 레벨 재귀 | `let rec f x = body` |
| 다중 매개변수 재귀 | `let rec f x = fun y -> body` |
| 리스트 범위 | `[1..100]` |
| 상호 재귀 | `let rec f x = ... and g y = ...` |
| ADT + 재귀 | `type T = ... let rec f t = match t with ...` |

v1.4에서 달라진 핵심 사항:

- **모듈 레벨 `let rec`**: 재귀 함수를 최상위 선언으로 작성합니다.
  각 함수가 독립적이어서 읽기 쉽고 재사용하기 좋습니다.
  `let rec ... in let rec ... in` 체인이 사라졌습니다.
- **리스트 범위**: `[1..n]`으로 연속 정수 리스트를 생성합니다.
  에라토스테네스의 체, 소수 필터링, FizzBuzz 등에서 수동 리스트 나열을 대체합니다.
- **상호 재귀**: `let rec f = ... and g = ...`로 서로를 호출하는 함수를 정의합니다.
  상태 머신, 홀수/짝수 판별기 등 새로운 알고리즘 패턴이 가능해졌습니다.
- **대수적 데이터 타입**은 재귀 함수와 자연스럽게 결합되어 트리, 수식 언어,
  사용자 정의 수 타입에 활용됩니다.
