# 3장: 리스트와 튜플 (Lists and Tuples)

## 리스트 기초

리스트는 순서가 있는 동질적(homogeneous) 컬렉션입니다:

```
funlang> [1, 2, 3]
[1, 2, 3]

funlang> []
[]
```

cons 연산자 `::`는 요소를 앞에 추가합니다:

```
funlang> 1 :: [2, 3]
[1, 2, 3]

funlang> 1 :: 2 :: 3 :: []
[1, 2, 3]
```

## 리스트 범위 (List Ranges)

`[start..stop]` 구문으로 정수 리스트를 생성할 수 있습니다:

```
funlang> [1..5]
[1, 2, 3, 4, 5]

funlang> [1..10]
[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
```

스텝(증가값)을 지정할 수도 있습니다. `[start..step..stop]` 형태입니다:

```
funlang> [1..2..10]
[1, 3, 5, 7, 9]

funlang> [0..5..20]
[0, 5, 10, 15, 20]
```

`stop`이 `start`보다 작으면 빈 리스트를 반환합니다 (F# 동작과 동일):

```
funlang> [5..1]
[]
```

단일 원소 범위:

```
funlang> [3..3]
[3]
```

범위는 파이프 연산자와 함께 유용합니다:

```
$ cat range_sum.l3
let result =
    let rec fold f = fun acc -> fun xs -> match xs with | [] -> acc | h :: t -> fold f (f acc h) t
    in fold (fun acc -> fun x -> acc + x) 0 [1..100]

$ langthree range_sum.l3
5050
```

**참고:** 범위는 정수(`int`)만 지원합니다. 스텝이 0이면 런타임 에러가 발생합니다.

## 튜플 (Tuples)

튜플은 고정 크기의 이질적(heterogeneous) 컬렉션입니다:

```
funlang> (1, "hello")
(1, "hello")

funlang> (1, "hello", true)
(1, "hello", true)
```

패턴 바인딩으로 튜플을 분해할 수 있습니다:

```
funlang> let (x, y) = (1, 2) in x + y
3

funlang> let (a, b, c) = (1, 2, 3) in a + b + c
6
```

## 유닛 (Unit)

유닛 타입 `()`는 "값 없음"을 나타냅니다:

```
funlang> ()
()
```

## 리스트와 튜플 조합

튜플의 리스트:

```
funlang> [(1, "a"), (2, "b")]
[(1, "a"), (2, "b")]
```

## 재귀적 리스트 함수

`let rec`는 단일 매개변수만 지원하므로, 재귀적 리스트 함수는
리스트를 유일한 인자로 받습니다. 추가 상태를 전달하려면 클로저(closure)를 사용하세요.

**길이 구하기:**

```
funlang> let rec length xs = match xs with | [] -> 0 | _ :: rest -> 1 + length rest in length [1, 2, 3, 4]
4
```

**합계:**

```
funlang> let rec sum xs = match xs with | [] -> 0 | x :: rest -> x + sum rest in sum [1, 2, 3, 4, 5]
15
```

**Map** (각 요소에 함수 적용):

```
funlang> let rec go xs = match xs with | [] -> [] | x :: rest -> x * 10 :: go rest in go [1, 2, 3]
[10, 20, 30]
```

**Filter** (클로저에 조건 함수를 캡처):

```
funlang> let f = fun x -> x > 2 in let rec go xs = match xs with | [] -> [] | x :: rest -> if f x then x :: go rest else go rest in go [1, 2, 3, 4, 5]
[3, 4, 5]
```

## 리스트 패턴 매칭 (Pattern Matching on Lists)

4장 미리보기 -- 리스트 패턴을 사용한 `match`:

```
funlang> match [1, 2, 3] with | [] -> "empty" | x :: _ -> to_string x
"1"
```

중첩 구조 분해(nested destructuring):

```
funlang> match [1, 2, 3] with | a :: b :: _ -> a + b | _ -> 0
3
```
