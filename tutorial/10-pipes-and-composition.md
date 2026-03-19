# 10장: Pipes and Composition

## 파이프 연산자

파이프 연산자 `|>`는 값을 함수의 인자로 전달합니다:

```
funlang> 5 |> (fun x -> x + 1)
6
```

파이프 체인은 왼쪽에서 오른쪽으로 읽히며, 데이터 변환 파이프라인을 표현합니다:

```
$ cat pipe_chain.l3
let double x = x * 2
let inc x = x + 1
let result = 5 |> double |> inc

$ langthree pipe_chain.l3
11
```

여기서 `5 |> double |> inc`는 `inc (double 5)` = `inc 10` = `11`을 계산합니다.

## 내장 함수와 파이프

파이프는 사용자 정의 함수뿐만 아니라 내장 함수와도 함께 동작합니다:

```
funlang> "hello" |> string_length
5
```

커링된 내장 함수는 파이프와 자연스럽게 결합됩니다:

```
funlang> "world" |> string_concat "hello "
"hello world"
```

## 람다와 파이프

람다를 함수로 전달할 수 있습니다:

```
funlang> 10 |> (fun x -> x * x)
100
```

람다 주위의 괄호는 필수입니다.

## 순방향 합성

`>>` 연산자는 두 함수를 왼쪽에서 오른쪽 순서로 합성합니다.
`f >> g`는 `f`를 먼저 적용한 다음 `g`를 적용하는 함수를 만듭니다:

```
$ cat compose_fwd.l3
let double x = x * 2
let inc x = x + 1
let f = double >> inc
let result = f 5

$ langthree compose_fwd.l3
11
```

`f 5`는 `inc (double 5)` = `inc 10` = `11`을 계산합니다.

## 역방향 합성

`<<` 연산자는 오른쪽에서 왼쪽으로 합성합니다.
`g << f`는 "`f`를 먼저 적용한 다음 `g`를 적용한다"는 의미로, `f >> g`와 동일합니다:

```
$ cat compose_bwd.l3
let double x = x * 2
let inc x = x + 1
let g = inc << double
let result = g 5

$ langthree compose_bwd.l3
11
```

`double >> inc`와 `inc << double`은 동일한 함수를 생성합니다.

## 합성 체인

합성은 다단계 변환을 위해 체이닝할 수 있습니다:

```
$ cat compose_chain.l3
let add1 x = x + 1
let mul2 x = x * 2
let sub3 x = x - 3
let f = add1 >> mul2 >> sub3
let result = f 5

$ langthree compose_chain.l3
9
```

`f 5` = `sub3 (mul2 (add1 5))` = `sub3 (mul2 6)` = `sub3 12` = `9`.

## 파이프 vs 합성

**파이프**는 특정 값을 파이프라인을 통해 변환합니다:

```
$ cat pipe_example.l3
let double x = x * 2
let inc x = x + 1
let result = 5 |> double |> inc

$ langthree pipe_example.l3
11
```

**합성**은 나중에 사용할 새로운 함수를 만듭니다:

```
$ cat comp_example.l3
let double x = x * 2
let inc x = x + 1
let transform = double >> inc
let a = transform 5
let result = transform 10

$ langthree comp_example.l3
21
```

값이 있고 지금 바로 변환하고 싶을 때는 파이프를 사용하세요.
재사용 가능한 변환을 정의하고 싶을 때는 합성을 사용하세요.

## 실용 예제: 데이터 파이프라인

파이프와 문자열 연산의 조합:

```
$ cat pipeline.l3
let result = 42 |> to_string |> string_concat "answer: "

$ langthree pipeline.l3
"answer: 42"
```

재사용 가능한 포매터를 위한 합성:

```
$ cat formatter.l3
let format_num = to_string >> string_concat "value="
let result = format_num 99

$ langthree formatter.l3
"value=99"
```

## 우선순위

파이프는 모든 연산자 중 가장 낮은 우선순위를 가지므로, `x + 1 |> f`와 같은 식에서는
`x + 1`의 결과가 `f`로 전달됩니다. 합성 연산자는 파이프보다 높지만 산술 연산자보다
낮은 우선순위를 가집니다:

| 연산자 | 우선순위 |
|----------|-----------|
| `\|>` | 가장 낮음 |
| `>>`, `<<` | 낮음 |
| `\|\|` | ... |
| `&&` | ... |
| `+`, `-` | ... |
| `*`, `/` | 가장 높음 |
