# 2장: 함수 (Functions)

## 익명 함수 (Anonymous Functions)

람다 구문은 `fun`을 사용합니다:

```
funlang> (fun x -> x + 1) 10
11
```

타입 어노테이션을 포함하는 경우:

```
funlang> (fun (x: int) -> x + 1) 10
11
```

튜플 파라미터를 직접 구조 분해할 수 있습니다:

```
funlang> (fun (x, y) -> x + y) (1, 2)
3

funlang> (fun (a, b, c) -> a + b + c) (1, 2, 3)
6
```

## Let 바인딩 (REPL / 표현식 모드)

REPL에서는 `let ... in`으로 값을 바인딩합니다:

```
funlang> let x = 5 in x + 1
6
```

바인딩을 연쇄적으로 사용할 수 있습니다:

```
funlang> let x = 5 in let y = x + 1 in y * 2
12
```

## Let 바인딩 (파일 모드)

파일 모드에서 `let` 바인딩은 최상위 선언(top-level declarations)이며 `in`이 필요 없습니다.
마지막 바인딩의 값이 출력됩니다:

```
$ cat add.l3
let a = 10
let b = 20
let result = a + b

$ langthree add.l3
30
```

## 다중 매개변수 함수 (Multi-Parameter Functions)

다중 매개변수 함수는 중첩된 람다로 변환됩니다 (커링, currying).
파일 모드(모듈 레벨)에서 다음과 같이 작동합니다:

```
$ cat multi.l3
let add x y = x + y
let result = add 3 4

$ langthree multi.l3
7
```

위 코드는 다음과 동일합니다:

```
$ cat multi2.l3
let add = fun x -> fun y -> x + y
let result = add 3 4

$ langthree multi2.l3
7
```

## 재귀 함수 (Recursive Functions)

재귀에는 `let rec`를 사용합니다. 표현식 레벨(`in`과 함께)에서 사용할 수 있으며,
v1.4부터는 모듈 레벨에서도 사용 가능합니다 (아래 "모듈 레벨 let rec" 참조).

```
funlang> let rec fact n = if n <= 1 then 1 else n * fact (n - 1) in fact 5
120
```

**제한 사항:** `let rec`는 단일 매개변수만 지원합니다. 다중 매개변수
재귀 함수의 경우, 단일 튜플을 받거나 본문 내부에서 중첩 람다를 사용하세요:

```
funlang> let rec len xs = match xs with | [] -> 0 | _ :: rest -> 1 + len rest in len [1, 2, 3]
3
```

파일 모드에서는 최상위 `let` 내부에 `let rec ... in`을 포함시킵니다:

```
$ cat factorial.l3
let result =
    let rec fact n = if n <= 1 then 1 else n * fact (n - 1)
    in fact 10

$ langthree factorial.l3
3628800
```

## 모듈 레벨 let rec

v1.4부터 `let rec`을 모듈 레벨(파일 최상위)에서도 사용할 수 있습니다.
이전에는 `let rec ... in ...` 형태로만 가능했지만, 이제 `in` 없이 직접 선언할 수 있습니다:

```
$ cat fact_module.l3
let rec fact n = if n <= 1 then 1 else n * fact (n - 1)
let result = fact 10

$ langthree fact_module.l3
3628800
```

이 형태는 파일 모드에서만 동작합니다. REPL에서는 여전히 `let rec ... in ...`을 사용하세요.

## 상호 재귀 (Mutual Recursion)

`and` 키워드로 서로를 호출하는 함수들을 동시에 선언할 수 있습니다:

```
$ cat even_odd.l3
let rec even n = if n = 0 then true else odd (n - 1)
and odd n = if n = 0 then false else even (n - 1)

let result = (even 10, odd 7)

$ langthree even_odd.l3
(true, true)
```

`even`과 `odd`는 서로를 호출합니다. `and`로 연결된 함수들은 동시에 환경에 등록되어
서로의 존재를 알 수 있습니다.

상호 재귀는 모듈 레벨에서만 동작합니다. 각 함수는 단일 파라미터를 받으며,
다중 파라미터는 클로저로 처리합니다:

```
$ cat mutrec_multi.l3
let rec isEven n = if n = 0 then true else isOdd (n - 1)
and isOdd n = if n = 0 then false else isEven (n - 1)

let r1 = isEven 100
let r2 = isOdd 99
let result = (r1, r2)

$ langthree mutrec_multi.l3
(true, true)
```

## 꼬리 호출 최적화 (Tail Call Optimization)

LangThree는 꼬리 위치(tail position)의 함수 호출을 자동으로 최적화합니다.
이를 통해 깊은 재귀도 스택 오버플로우 없이 실행됩니다.

**꼬리 호출이란?** 함수의 마지막 동작이 다른 함수를 호출하는 것입니다:

```
$ cat tco_loop.l3
let rec loop n = if n = 0 then 0 else loop (n - 1)
let result = loop 1000000

$ langthree tco_loop.l3
0
```

100만 번의 재귀가 스택 오버플로우 없이 동작합니다.

**누적 변수 패턴:** 꼬리 재귀로 바꾸려면 결과를 누적 변수에 전달하세요:

```
-- 꼬리 재귀가 아닌 버전 (n * fact(n-1)에서 곱셈이 남음):
funlang> let rec fact n = if n <= 1 then 1 else n * fact (n - 1) in fact 10
3628800

-- 꼬리 재귀 버전 (acc에 결과 누적):
$ cat tco_fact.l3
let rec factTail n = fun acc -> if n <= 1 then acc else factTail (n - 1) (acc * n)
let result = factTail 10 1

$ langthree tco_fact.l3
3628800
```

**꼬리 위치 규칙:**
- `if` 양쪽 브랜치: 꼬리 위치 ✓
- `match` 절 본문: 꼬리 위치 ✓
- `let ... in body`: body가 꼬리 위치 ✓
- `try ... with`: try 본문은 꼬리 위치 ✗ (예외 핸들러 때문)
- 산술 연산의 피연산자: 꼬리 위치 ✗ (연산이 남아있음)

## 고차 함수 (Higher-Order Functions)

함수는 일급 값(first-class values)입니다. 함수를 인자로 전달할 수 있습니다:

```
$ cat hof.l3
let apply f x = f x
let result = apply (fun x -> x + 1) 10

$ langthree hof.l3
11
```

함수에서 함수를 반환할 수도 있습니다:

```
$ cat hof2.l3
let make_adder n = fun x -> x + n
let add10 = make_adder 10
let result = add10 5

$ langthree hof2.l3
15
```

## 클로저 (Closures)

함수는 자신을 둘러싼 스코프(scope)의 변수를 캡처합니다:

```
$ cat closure.l3
let x = 10
let add_x y = x + y
let result = add_x 5

$ langthree closure.l3
15
```

## 커링과 부분 적용 (Currying and Partial Application)

다중 매개변수 함수는 자동으로 커링(currying)됩니다:

```
$ cat curry.l3
let add x y = x + y
let add5 = add 5
let result = add5 3

$ langthree curry.l3
8
```

## 파이프와 합성 연산자 (Pipe and Composition Operators)

파이프 연산자(pipe operator) `|>`는 값을 마지막 인자로 전달합니다:

```
funlang> 5 |> (fun x -> x + 1)
6
```

합성 연산자(composition operators) `>>`(왼쪽에서 오른쪽)와 `<<`(오른쪽에서 왼쪽):

```
funlang> let f = (fun x -> x + 1) >> (fun x -> x * 2) in f 3
8
```

여기서 `f 3`은 `(3 + 1) * 2 = 8`을 계산합니다.
