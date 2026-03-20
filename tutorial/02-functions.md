# 2장: 함수 (Functions)

함수형 언어에서 함수는 단순한 코드 묶음이 아닙니다. 함수는 값입니다 -- 변수에 담을 수 있고, 다른 함수에 인자로 넘길 수 있으며, 함수에서 함수를 반환할 수도 있습니다. 이 장에서는 LangThree가 함수를 어떻게 다루는지 살펴보면서, 함수형 프로그래밍의 핵심 아이디어들을 하나씩 짚어봅니다.

## 익명 함수 (Anonymous Functions)

모든 함수의 기반이 되는 개념부터 시작합니다. 이름 없는 함수, 즉 람다(lambda)입니다.

람다 구문은 `fun`을 사용합니다:

```
funlang> (fun x -> x + 1) 10
11
```

`fun x -> x + 1`은 "x를 받아서 x + 1을 반환하는 함수"입니다. Python의 `lambda x: x + 1`, JavaScript의 `x => x + 1`과 같은 개념입니다. 이 함수에 `10`을 바로 적용한 결과가 `11`입니다.

타입 어노테이션을 포함하는 경우:

```
funlang> (fun (x: int) -> x + 1) 10
11
```

LangThree는 타입을 자동으로 추론하기 때문에 보통은 타입 어노테이션을 쓸 필요가 없습니다. 하지만 타입 오류를 디버깅할 때나, 코드를 읽는 사람에게 의도를 명확히 전달하고 싶을 때 유용합니다. 타입 어노테이션은 컴파일러에게도, 코드를 읽는 사람에게도 일종의 문서 역할을 합니다.

튜플 파라미터를 직접 구조 분해할 수 있습니다:

```
funlang> (fun (x, y) -> x + y) (1, 2)
3

funlang> (fun (a, b, c) -> a + b + c) (1, 2, 3)
6
```

함수 정의 자체에서 튜플을 분해하는 이 문법은 코드를 훨씬 간결하게 만들어줍니다. `fun pair -> fst pair + snd pair`처럼 쓰는 대신 패턴 매칭을 인자 단계에서 바로 수행할 수 있습니다. F#과 Haskell에서도 이런 스타일을 많이 씁니다.

## Let 바인딩 (REPL / 표현식 모드)

매번 함수를 쓸 때마다 이름 없는 람다를 쓸 수는 없습니다. `let`으로 값과 함수에 이름을 붙입니다.

REPL에서는 `let ... in`으로 값을 바인딩합니다:

```
funlang> let x = 5 in x + 1
6
```

`let x = 5 in x + 1`을 읽는 방법: "x를 5로 정의하고, 그 문맥에서 x + 1을 계산하라." `in` 뒤의 표현식이 전체의 결과값이 됩니다. 수학의 "... where x = 5"와 같은 개념입니다.

바인딩을 연쇄적으로 사용할 수 있습니다:

```
funlang> let x = 5 in let y = x + 1 in y * 2
12
```

이처럼 `let ... in let ... in ...` 형태로 바인딩을 쌓아갈 수 있습니다. 중간 계산 결과에 이름을 붙여가며 복잡한 표현식을 단계적으로 구성할 수 있습니다. 익숙해지면 매우 자연스러운 스타일입니다.

## Let 바인딩 (파일 모드)

파일에서 코드를 작성할 때는 REPL과 약간 다른 문법을 씁니다. `in` 키워드 없이 최상위에서 바인딩을 나열합니다.

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

파일 모드의 `let`은 Python의 모듈 레벨 변수 선언과 비슷합니다. 위에서 아래로 순서대로 평가되며, 각 바인딩은 그 이후의 바인딩에서 사용할 수 있습니다. `result`가 마지막 바인딩이므로 그 값인 `30`이 출력됩니다.

## 다중 매개변수 함수 (Multi-Parameter Functions)

여러 인자를 받는 함수를 어떻게 정의할까요? 여기서 함수형 언어의 핵심 개념인 커링(currying)이 등장합니다.

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

두 코드가 완전히 동일하다는 점이 커링의 핵심입니다. `add x y = x + y`는 문법적 편의를 위한 축약 표현일 뿐, 실제로는 `fun x -> fun y -> x + y`입니다. `add 3 4`를 평가하면 먼저 `add 3`이 `fun y -> 3 + y`라는 새로운 함수를 반환하고, 거기에 `4`를 적용해 `7`을 얻습니다.

이 동작이 단순한 구현 세부사항처럼 보일 수 있지만, 이것이 바로 부분 적용(partial application)을 가능하게 하는 기반입니다. 뒤에 나오는 커링 섹션에서 이것이 얼마나 유용한지 볼 수 있습니다.

## 재귀 함수 (Recursive Functions)

함수형 언어에서는 반복을 표현하는 주된 방법이 재귀입니다. `for` 루프나 `while` 루프 대신, 함수가 자기 자신을 호출합니다.

재귀에는 `let rec`를 사용합니다. 표현식 레벨(`in`과 함께)과 모듈 레벨(파일 최상위) 모두에서 사용할 수 있습니다.

```
funlang> let rec fact n = if n <= 1 then 1 else n * fact (n - 1) in fact 5
120
```

왜 `let rec`가 필요한 걸까요? 일반 `let`에서는 정의하는 시점에 자기 자신의 이름이 아직 스코프에 없습니다. `let rec`는 "이 함수의 이름을 함수 본문 안에서도 참조할 수 있다"고 컴파일러에게 알려주는 키워드입니다. OCaml과 F#도 같은 방식을 씁니다.

**제한 사항:** `let rec`는 단일 매개변수만 지원합니다. 다중 매개변수
재귀 함수의 경우, 단일 튜플을 받거나 본문 내부에서 중첩 람다를 사용하세요:

```
funlang> let rec len xs = match xs with | [] -> 0 | _ :: rest -> 1 + len rest in len [1; 2; 3]
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

이 패턴은 자주 쓰이므로 익숙해지면 좋습니다. 재귀 함수를 외부에 노출하지 않고 지역 구현 세부사항으로 감추고 싶을 때 유용합니다.

## 모듈 레벨 let rec

`let rec`을 모듈 레벨(파일 최상위)에서 `in` 없이 직접 선언할 수 있습니다:

```
$ cat fact_module.l3
let rec fact n = if n <= 1 then 1 else n * fact (n - 1)
let result = fact 10

$ langthree fact_module.l3
3628800
```

이 형태는 파일 모드에서만 동작합니다. REPL에서는 여전히 `let rec ... in ...`을 사용하세요.

모듈 레벨 `let rec`는 재귀 함수를 여러 곳에서 사용해야 할 때 훨씬 편리합니다. 중첩 `let rec ... in` 패턴은 함수를 한 번만 쓸 때 적합하고, 모듈 레벨 선언은 여러 바인딩에서 공유해야 할 때 적합합니다.

## 상호 재귀 (Mutual Recursion)

때로는 두 함수가 서로를 호출해야 할 경우가 있습니다. 하나를 먼저 정의하면 다른 하나가 아직 존재하지 않는 문제가 생깁니다. 이를 해결하는 것이 `and` 키워드입니다.

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

짝수/홀수 판별은 교과서적인 예제지만, 실제로 상호 재귀는 상태 머신(state machine)을 구현하거나 문법 파서를 작성할 때 매우 유용합니다. 예를 들어 "문자열 내부를 파싱하는 상태"와 "문자열 외부를 파싱하는 상태"가 서로를 전환하는 패턴이 전형적인 상호 재귀입니다.

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

재귀를 쓰면 자연스럽게 드는 걱정이 있습니다: "깊이 재귀하면 스택이 넘치지 않을까?" LangThree는 꼬리 호출 최적화(TCO)로 이 문제를 해결합니다.

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

`loop (n - 1)`이 함수의 마지막 동작입니다. 이 경우 컴파일러는 재귀 호출을 실제로 새 스택 프레임을 만드는 대신, 현재 프레임을 재사용하는 루프로 변환합니다. 결과적으로 `while n != 0: n -= 1`과 동일한 기계어 코드가 됩니다. 메모리 사용량이 일정하게 유지됩니다.

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

`n * fact (n - 1)`에서는 `fact (n - 1)`이 반환된 후 곱셈을 해야 하므로 꼬리 위치가 아닙니다. 스택에 "나중에 곱해야 할 n"들이 쌓입니다. 반면 `factTail (n - 1) (acc * n)`은 마지막 동작이 순수한 함수 호출이므로 꼬리 위치입니다. 누적값 `acc`를 파라미터로 전달함으로써 스택 대신 파라미터에 상태를 저장합니다.

**꼬리 위치 규칙:**
- `if` 양쪽 브랜치: 꼬리 위치 ✓
- `match` 절 본문: 꼬리 위치 ✓
- `let ... in body`: body가 꼬리 위치 ✓
- `try ... with`: try 본문은 꼬리 위치 ✗ (예외 핸들러 때문)
- 산술 연산의 피연산자: 꼬리 위치 ✗ (연산이 남아있음)

이 규칙을 외울 필요는 없습니다. 핵심만 기억하세요: "재귀 호출 결과를 그대로 반환하면 꼬리 위치, 그 결과로 무언가를 더 해야 하면 꼬리 위치가 아니다."

## 고차 함수 (Higher-Order Functions)

함수가 값이라면, 함수를 인자로 받거나 반환하는 함수도 당연히 가능합니다. 이것이 고차 함수(higher-order functions)입니다. 처음에는 추상적으로 들리지만, 실제로는 매우 실용적인 패턴입니다.

함수는 일급 값(first-class values)입니다. 함수를 인자로 전달할 수 있습니다:

```
$ cat hof.l3
let apply f x = f x
let result = apply (fun x -> x + 1) 10

$ langthree hof.l3
11
```

`apply`는 함수 `f`와 값 `x`를 받아 `f x`를 계산합니다. 단순해 보이지만, 이 패턴을 확장하면 `map`, `filter`, `fold` 같은 강력한 추상화가 나옵니다. 어떤 연산을 수행할지 외부에서 주입받는 것이 고차 함수의 핵심입니다.

함수에서 함수를 반환할 수도 있습니다:

```
$ cat hof2.l3
let make_adder n = fun x -> x + n
let add10 = make_adder 10
let result = add10 5

$ langthree hof2.l3
15
```

`make_adder`는 함수를 만드는 함수, 즉 팩토리(factory)입니다. `make_adder 10`은 "10을 더하는 함수"를 반환합니다. `add10`은 그 결과인 함수를 담고 있고, `add10 5`는 `15`를 줍니다. 이런 패턴은 설정값을 캡처한 함수를 만들어야 할 때 매우 유용합니다.

## 클로저 (Closures)

`make_adder`가 동작하는 이유는 클로저(closure) 덕분입니다. 함수는 자신이 정의된 스코프의 변수를 "기억"합니다.

함수는 자신을 둘러싼 스코프(scope)의 변수를 캡처합니다:

```
$ cat closure.l3
let x = 10
let add_x y = x + y
let result = add_x 5

$ langthree closure.l3
15
```

`add_x`는 정의될 때 `x = 10`이라는 환경을 캡처합니다. 나중에 `add_x 5`를 호출할 때 `x`가 여전히 `10`이라는 것을 알고 있습니다. 이것이 클로저입니다 -- 함수와 그 함수가 캡처한 환경의 조합입니다.

클로저는 함수형 프로그래밍의 가장 강력한 도구 중 하나입니다. 상태를 객체 대신 함수와 클로저로 표현할 수 있습니다. `make_adder`가 좋은 예입니다 -- 각 호출마다 다른 `n`을 캡처한 별개의 클로저를 만들어냅니다.

## 커링과 부분 적용 (Currying and Partial Application)

앞서 다중 매개변수 함수가 중첩된 람다라는 것을 배웠습니다. 이것이 실용적으로 어떻게 쓰이는지 살펴봅니다.

다중 매개변수 함수는 자동으로 커링(currying)됩니다:

```
$ cat curry.l3
let add x y = x + y
let add5 = add 5
let result = add5 3

$ langthree curry.l3
8
```

`add 5`는 `add`에 첫 번째 인자만 적용한 결과입니다. `y`는 아직 제공하지 않았으므로 `fun y -> 5 + y`라는 함수가 됩니다. 이것이 부분 적용(partial application)입니다.

이것이 실제로 왜 유용할까요? `map`, `filter` 같은 함수와 조합할 때 빛을 발합니다. 예를 들어 리스트의 모든 원소에 5를 더하고 싶다면, `map (add 5) [1; 2; 3]`처럼 쓸 수 있습니다. `fun x -> add 5 x`처럼 람다를 명시적으로 쓸 필요가 없습니다. 커링된 함수는 항상 부분 적용이 가능한 "반쯤 완성된 함수"를 자연스럽게 만들어냅니다.

## 파이프와 합성 연산자 (Pipe and Composition Operators)

여러 변환을 연달아 적용할 때, 중첩 함수 호출은 안에서 밖으로 읽어야 해서 불편합니다. 파이프 연산자와 합성 연산자가 이 문제를 해결합니다.

파이프 연산자(pipe operator) `|>`는 값을 마지막 인자로 전달합니다:

```
funlang> 5 |> (fun x -> x + 1)
6
```

`5 |> (fun x -> x + 1)`은 `(fun x -> x + 1) 5`와 동일합니다. 차이는 읽는 방향입니다. "5에서 시작해서 1을 더한다"고 왼쪽에서 오른쪽으로 읽을 수 있습니다. F#과 Elixir에서 온 개발자라면 이 연산자가 무척 익숙할 것입니다.

합성 연산자(composition operators) `>>`(왼쪽에서 오른쪽)와 `<<`(오른쪽에서 왼쪽):

```
funlang> let f = (fun x -> x + 1) >> (fun x -> x * 2) in f 3
8
```

여기서 `f 3`은 `(3 + 1) * 2 = 8`을 계산합니다.

`>>` 는 두 함수를 하나로 합칩니다. `f >> g`는 "먼저 f를 적용하고 그 결과에 g를 적용하는 새 함수"입니다. `g(f(x))`를 `(f >> g)(x)`로 쓸 수 있습니다. 수학의 함수 합성 `g ∘ f`와 같지만 적용 순서가 왼쪽에서 오른쪽이라 읽기 더 자연스럽습니다. 여러 변환 단계를 파이프라인으로 조합할 때, `|>`는 데이터에 초점을, `>>`는 함수 조합 자체에 초점을 맞춥니다.
