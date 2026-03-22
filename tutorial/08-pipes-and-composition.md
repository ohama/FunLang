# 8장: 파이프와 합성 (Pipes and Composition)

함수형 프로그래밍에서 가장 우아한 아이디어 중 하나는 "함수를 조합하여 더 큰 함수를 만든다"는 것입니다. 그런데 그 조합 방식이 자연스럽지 않으면 코드가 오히려 복잡해집니다. `f(g(h(x)))`처럼 안쪽부터 읽어야 하는 중첩 호출은 사람의 직관과 반대 방향이거든요. 파이프와 합성 연산자는 이 문제를 정면으로 해결합니다.

## 파이프 연산자

파이프 연산자 `|>`는 값을 함수의 인자로 전달합니다. 설명은 간단하지만, 이 연산자가 코드를 읽는 방향을 바꿔놓습니다.

```
funlang> 5 |> (fun x -> x + 1)
6
```

왼쪽의 값이 오른쪽 함수의 입력이 됩니다. 마치 공장 생산 라인처럼 데이터가 왼쪽에서 오른쪽으로 흘러가죠. F#에서 가져온 이 연산자는 Elixir, Elm 등 여러 함수형 언어에서도 핵심 기능으로 자리잡고 있습니다.

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

만약 파이프 없이 썼다면 `inc (double 5)`가 됩니다. 두 함수뿐이라 아직은 비슷해 보이지만, 변환 단계가 5개, 10개로 늘어나면 차이가 확연해집니다. 중첩 호출은 오른쪽에서 왼쪽으로 읽어야 하지만, 파이프 체인은 우리가 글을 읽는 방향, 즉 왼쪽에서 오른쪽으로 읽을 수 있습니다.

## 내장 함수와 파이프

파이프는 사용자 정의 함수뿐만 아니라 내장 함수와도 함께 동작합니다:

```
funlang> "hello" |> string_length
5
```

커링된 내장 함수는 파이프와 자연스럽게 결합됩니다. LangThree의 내장 함수들은 모두 커링을 지원하므로, 인자를 부분적으로 적용한 뒤 파이프로 연결할 수 있습니다:

```
funlang> "world" |> string_concat "hello "
"hello world"
```

`string_concat "hello "`는 하나의 인자만 받은 부분 적용 함수입니다. 이 함수가 파이프를 통해 `"world"`를 두 번째 인자로 받아 최종 결과를 만들어냅니다. 이 패턴이 익숙해지면, 데이터 변환 파이프라인을 마치 레고 블록 쌓듯 구성할 수 있게 됩니다.

## 람다와 파이프

파이프 안에서 즉석으로 람다를 정의할 수도 있습니다:

```
funlang> 10 |> (fun x -> x * x)
100
```

람다 주위의 괄호는 필수입니다. 이는 파서가 `|>`의 오른쪽을 하나의 표현식으로 인식해야 하기 때문입니다. 괄호를 빠뜨리면 파서가 혼란스러워집니다. 간단한 규칙이니 기억해두세요: 파이프 뒤에 람다가 오면 반드시 괄호로 감싸야 합니다.

## 순방향 합성

`>>` 연산자는 두 함수를 왼쪽에서 오른쪽 순서로 합성합니다. `f >> g`는 `f`를 먼저 적용한 다음 `g`를 적용하는 새로운 함수를 만듭니다. 파이프가 "지금 이 값을 변환하는" 것이라면, 합성은 "나중에 사용할 변환기를 만드는" 것입니다.

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

수학에서의 함수 합성 `g ∘ f`와 비교해보세요. 수학적 표기법은 오른쪽에서 왼쪽으로 읽지만, `>>` 연산자는 왼쪽에서 오른쪽으로 읽을 수 있어 더 직관적입니다. `double >> inc`는 "먼저 두 배로 만들고, 그 다음 1을 더한다"고 소리 내어 읽을 수 있습니다.

## 역방향 합성

`<<` 연산자는 오른쪽에서 왼쪽으로 합성합니다. `g << f`는 "`f`를 먼저 적용한 다음 `g`를 적용한다"는 의미로, `f >> g`와 동일합니다:

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

`<<` 연산자는 Haskell의 `(.)` 합성 연산자와 방향이 같습니다. Haskell에 익숙하다면 `<<`가 더 자연스럽게 느껴질 수 있고, F#이나 OCaml에서 왔다면 `>>`가 더 편할 것입니다. 어떤 스타일을 선택하든 일관성을 유지하는 것이 중요합니다. 팀이나 코드베이스 안에서 하나의 방향으로 통일하면 코드를 읽을 때 방향 전환으로 인한 혼란이 없어집니다.

## 합성 체인

합성은 두 함수에만 국한되지 않습니다. 여러 단계를 체이닝하면 복잡한 변환을 명확하게 표현할 수 있습니다:

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

이렇게 합성된 함수 `f`는 이름 있는 변환 파이프라인입니다. `f`를 정의한 순간부터 임의의 값에 반복 적용할 수 있고, 테스트도 독립적으로 가능합니다. 변환 로직이 한 곳에 모여있으니 나중에 수정할 때도 한 곳만 바꾸면 됩니다.

## 파이프 vs 합성

이 두 연산자를 언제 써야 할지 헷갈린다면, 간단한 기준이 있습니다.

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

값이 있고 지금 바로 변환하고 싶을 때는 파이프를 사용하세요. 재사용 가능한 변환을 정의하고 싶을 때는 합성을 사용하세요.

실제로 생각해보면, 파이프는 "이 특정 데이터를 처리하는 과정"을 표현할 때 쓰고, 합성은 "이 처리 방식 자체를 캡처"할 때 씁니다. 위 예제에서 `transform`은 여러 번 호출할 수 있는 재사용 가능한 함수가 됩니다. 만약 파이프만 썼다면 같은 처리를 두 번 쓰기 위해 코드를 중복해야 했을 것입니다.

## 실용 예제: 데이터 파이프라인

실제 코드에서는 파이프와 합성을 같이 활용하는 경우가 많습니다. 파이프와 문자열 연산의 조합:

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

`format_num`은 한 번 정의하면 어떤 숫자에든 쓸 수 있는 포매터입니다. 이런 작은 변환 함수들을 합성으로 쌓아가다 보면, 복잡한 데이터 처리 로직도 단순한 블록들의 조합으로 표현할 수 있게 됩니다.

## Prelude 연산자와 파이프라인

Prelude가 제공하는 연산자를 파이프라인과 결합하면 더 간결한 코드를 작성할 수 있습니다. Prelude의 전체 함수/연산자 목록은 [9장: Prelude 표준 라이브러리](09-prelude.md)에서 다룹니다. 여기서는 파이프라인과의 조합에 집중합니다.

**`++` (리스트 연결):**

```
$ cat pipeline_ops.l3
let result = [1..3] ++ [10..13] ++ [20..22]

$ langthree pipeline_ops.l3
[1; 2; 3; 10; 11; 12; 13; 20; 21; 22]
```

**`^^` (문자열 연결):**

`string_concat` 대신 `^^` 연산자를 사용하면 더 읽기 쉽습니다. Python의 `+` 나 JavaScript의 `+`처럼 직관적인데, LangThree에서 `+`는 정수 덧셈에 예약되어 있으므로 문자열에는 별도의 연산자를 씁니다:

```
$ cat string_ops.l3
let greet name = "Hello, " ^^ name ^^ "!"
let result = greet "Alice"

$ langthree string_ops.l3
"Hello, Alice!"
```

**`<|>` (Option 대안):**

여러 시도 중 첫 번째로 성공한 결과를 택하는 패턴입니다. 파싱이나 fallback 로직을 표현할 때 특히 유용합니다:

```
$ cat option_ops.l3
let tryParse s = match s with | "42" -> Some 42 | _ -> None
let result = tryParse "abc" <|> tryParse "42" <|> Some 0

$ langthree option_ops.l3
Some 42
```

**혼합 파이프라인:**

여러 연산자를 파이프 `|>`와 함께 사용할 수 있습니다. 파이프라인의 각 단계가 명확히 구분되어, 코드를 읽는 사람이 데이터 흐름을 쉽게 추적할 수 있습니다:

```
$ cat mixed_pipeline.l3
// 리스트를 "[1, 2, 3]" 형태의 문자열로 변환
let formatList xs = "[" ^^ fold (fun acc -> fun x -> if acc = "" then to_string x else acc ^^ ", " ^^ to_string x) "" xs ^^ "]"

let result = [1..5] |> filter (fun x -> x > 2) |> formatList

$ langthree mixed_pipeline.l3
"[3, 4, 5]"
```

## 우선순위

파이프와 합성 연산자의 우선순위는 처음에 직관에 어긋날 수 있습니다. 그러나 설계 의도를 이해하면 이해가 쉽습니다. 파이프는 "마지막에 적용"되어야 하므로 가장 낮은 우선순위를 가집니다. 덕분에 `x + 1 |> f` 같은 식에서 `x + 1`의 결과가 완전히 계산된 뒤 `f`로 넘어갑니다. 추가 괄호가 필요 없습니다.

합성 연산자 `>>`, `<<`는 파이프보다는 높지만 산술보다는 낮습니다. 함수들을 먼저 합성한 뒤, 그 합성된 함수에 파이프로 값을 보내는 자연스러운 흐름을 반영합니다.

| 연산자 | 우선순위 |
|----------|-----------|
| `\|>` | 가장 낮음 |
| `>>`, `<<` | 낮음 |
| `\|\|` | ... |
| `&&` | ... |
| `+`, `-` | ... |
| `*`, `/` | 가장 높음 |

우선순위 때문에 예상치 못한 동작이 생긴다면, 괄호를 추가해 의도를 명확히 하는 것이 최선입니다. "괄호를 아낀다"는 미학보다 "코드를 오해 없이 읽는다"는 실용성이 중요합니다.
