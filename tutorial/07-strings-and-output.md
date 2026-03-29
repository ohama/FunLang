# 7장: 문자열과 출력 (Strings and Output)

문자열 처리는 어떤 언어에서든 실질적인 프로그래밍의 기반입니다. LangThree는 간결하지만 충분히 강력한 문자열 함수들을 제공하고, `printf` 계열 함수로 형식화된 출력도 지원합니다. 이 장에서는 문자열을 만들고 조작하는 방법부터, 화면에 출력하는 다양한 방법까지 살펴봅니다.

## 문자열 리터럴

문자열 리터럴은 큰따옴표와 표준 이스케이프 시퀀스를 사용합니다:

```
funlang> "hello world"
"hello world"

funlang> "tab\there"
"tab	here"
```

`\t`, `\n`, `\\` 같은 표준 이스케이프 시퀀스를 모두 사용할 수 있습니다. REPL에서 문자열을 입력하면 따옴표가 포함된 결과가 나타나는데, 이는 "이것이 문자열 값임"을 명시하는 것입니다. 파일 출력(`println` 등)에서는 따옴표 없이 내용만 출력됩니다.

## 문자열 연결

`+` 연산자로 문자열을 연결합니다:

```
funlang> "hello" + " " + "world"
"hello world"
```

LangThree에서 `+`는 정수 덧셈과 문자열 연결 모두에 사용됩니다. 타입 추론 덕분에 컴파일러가 문맥에서 어떤 `+`인지 판단합니다. 단, 정수와 문자열을 섞어 쓰면 타입 오류가 발생합니다 — `"age: " + 30`은 안 되고, `"age: " + to_string 30`이 필요합니다.

## 내장 문자열 함수

LangThree는 자주 쓰이는 문자열 연산을 내장 함수로 제공합니다. 각 함수의 동작 방식과 언제 사용하면 좋은지 함께 살펴봅니다.

### string_length

문자 수를 반환합니다:

```
funlang> string_length "hello"
5

funlang> string_length ""
0
```

빈 문자열의 길이는 0입니다. 인덱스 범위 검사나 반복 횟수 계산에 자주 쓰입니다.

### string_concat

커링된 연결 함수 -- 두 개의 문자열을 받습니다:

```
funlang> string_concat "hello" " world"
"hello world"
```

`+` 연산자와 결과는 같지만, `string_concat`이 커링 함수라는 점이 다릅니다. 커링되어 있으므로 부분 적용이 가능합니다:

```
$ cat prefix.l3
let add_prefix = string_concat "prefix:"
let result = add_prefix "value"

$ langthree prefix.l3
"prefix:value"
```

`string_concat "prefix:"`는 "앞에 'prefix:'를 붙이는 함수"를 반환합니다. 이렇게 만든 `add_prefix`를 파이프라인에서 재사용할 수 있습니다. 예를 들어 리스트의 모든 항목에 접두사를 붙이거나, 고차 함수에 인자로 전달할 때 편리합니다.

### string_sub

시작 인덱스와 길이로 부분 문자열을 추출합니다:

```
funlang> string_sub "hello" 1 3
"ell"
```

`string_sub s start len`은 인덱스 `start`부터 `len`개의 문자를 반환합니다.
인덱스는 0부터 시작합니다.

Python의 슬라이싱 `s[1:4]`와 비슷하지만, 끝 인덱스가 아니라 길이를 지정한다는 점에 주의하세요. Python에서 `s[1:4]`는 인덱스 1부터 3까지 3개의 문자를 가져오지만, LangThree에서는 `string_sub s 1 3`으로 동일한 결과를 얻습니다. `start + len`이 문자열 길이를 초과하지 않도록 주의하세요.

### string_contains

문자열이 부분 문자열을 포함하는지 확인합니다:

```
funlang> string_contains "hello world" "world"
true

funlang> string_contains "hello" "xyz"
false
```

검색 결과를 `bool`로 반환하므로 `if` 표현식이나 조건 분기에 바로 사용할 수 있습니다. 파일 경로 검증, 키워드 필터링 같은 간단한 텍스트 검색에 유용합니다.

### to_string

모든 타입의 값을 문자열 표현으로 변환합니다:

```
funlang> to_string 42
"42"

funlang> to_string true
"true"

funlang> to_string "already a string"
"already a string"
```

ADT, 리스트, 튜플, 레코드 등 모든 복합 타입도 지원합니다:

```
funlang> to_string (Some 42)
"Some 42"

funlang> to_string [1; 2; 3]
"[1; 2; 3]"

funlang> to_string (1, true)
"(1, true)"
```

`to_string`은 LangThree에서 가장 유연한 함수 중 하나입니다. 디버깅할 때 복잡한 값을 출력해보고 싶을 때, 또는 로그 메시지를 만들 때 항상 `to_string`으로 시작하면 됩니다.

**참고:** 문자열은 그대로 반환됩니다 (따옴표 없음, F# `string` 함수와 동일).
복합 타입 내부의 문자열에는 따옴표가 포함됩니다: `to_string (Some "hi")` → `Some "hi"`.

이 동작이 처음에는 약간 헷갈릴 수 있습니다. `to_string "hello"`는 `"hello"`(따옴표 없이 hello)를 반환하지만, `to_string (Some "hello")`는 `"Some \"hello\""` 형태로 내부 문자열에 따옴표가 붙습니다. 컨테이너 안의 문자열이라는 맥락에서 따옴표가 필요하기 때문입니다.

### string_to_int

문자열을 정수로 파싱합니다:

```
funlang> string_to_int "123"
123
```

외부에서 입력받은 숫자 문자열을 계산에 사용하기 전에 정수로 변환할 때 필요합니다. 파싱이 실패하는 경우(숫자가 아닌 문자열)에 대한 처리는 현재 별도로 필요합니다.

## 출력 함수

LangThree는 네 가지 출력 함수를 제공합니다. 각각 줄바꿈과 형식화 여부가 다릅니다. 어떤 상황에 어떤 함수를 쓰면 좋은지 함께 알아봅니다.

### print

줄바꿈 없이 문자열을 출력하고, unit을 반환합니다:

```
funlang> print "hello"
hello()
```

대화형 세션에서는 부수 효과 텍스트가 `()` 결과 바로 앞에 나타납니다. 여러 값을 같은 줄에 이어 출력하거나, 진행 상황을 한 줄에 표시할 때 사용합니다.

### println

줄바꿈을 포함하여 문자열을 출력하고, unit을 반환합니다:

```
funlang> println "hello"
hello
()
```

가장 자주 쓰이는 출력 함수입니다. 한 줄씩 메시지를 출력할 때 기본 선택입니다. `print`와의 차이는 출력 후 자동으로 줄바꿈(`\n`)을 추가한다는 것입니다.

### printf

지정자를 사용한 형식화 출력:

| 지정자 | 타입 | 예제 |
|-----------|------|---------|
| `%d` | int | `printf "%d" 42` |
| `%s` | string | `printf "%s" "hi"` |
| `%b` | bool | `printf "%b" true` |
| `%%` | 리터럴 `%` | `printf "100%%"` |

`printf`는 커링되어 있으며, 각 지정자가 하나의 인자를 소비합니다:

```
$ cat printf_demo.l3
let _ = printf "%s is %d years old\n" "Alice" 30
let result = 0

$ langthree printf_demo.l3
Alice is 30 years old
0
```

C의 `printf`나 Python의 `%` 포맷팅에 익숙하다면 자연스럽게 느껴질 것입니다. 중요한 차이는 LangThree의 `printf`가 커링되어 있다는 점입니다 — `printf "%s is %d" "Alice"`는 아직 인자를 하나 더 받아야 하는 함수를 반환합니다. 이 덕분에 부분 적용이 가능합니다. 예를 들어 `let log_int = printf "value: %d\n"`으로 정수를 로깅하는 함수를 만들 수 있습니다.

복수 지정자:

```
$ cat printf_multi.l3
let _ = printf "name=%s, active=%b\n" "Bob" true

$ langthree printf_multi.l3
name=Bob, active=true
0
```

형식 문자열의 지정자 순서와 인자의 순서가 일치해야 합니다. 지정자의 타입과 실제 인자의 타입이 다르면 타입 오류가 발생합니다 — 이것이 단순 문자열 연결보다 `printf`가 안전한 이유입니다.

### printfn

`printf`와 동일하지만 자동으로 줄바꿈을 추가합니다:

```
$ cat printfn_demo.l3
let _ = printfn "name=%s, age=%d" "Alice" 30
let result = 0

$ langthree printfn_demo.l3
name=Alice, age=30
0
```

`printf "...\n"`을 쓸 필요 없이 `printfn "..."`을 사용하세요. 줄바꿈을 형식 문자열 끝에 매번 붙이는 것을 잊기 쉽습니다. 한 줄씩 형식화된 출력을 할 때는 `printfn`이 실수를 줄여줍니다.

### sprintf

형식화된 문자열을 **반환**합니다 (출력하지 않음):

```
funlang> sprintf "%d + %d = %d" 1 2 3
"1 + 2 = 3"

funlang> sprintf "name=%s" "Alice"
"name=Alice"
```

`sprintf`는 출력하지 않고 형식화된 문자열 값을 만들어 돌려줍니다. 바로 출력하는 대신 나중에 사용하거나 다른 함수에 전달할 형식화된 문자열이 필요할 때 유용합니다. Python의 `str.format()`나 f-string과 역할이 비슷합니다.

`+`와 `to_string` 대신 `sprintf`를 사용하면 더 간결합니다:

```
$ cat sprintf_vs.l3
// 이전: 수동 연결
let old = "result=" ^^ to_string 42

// 이후: sprintf
let new_ = sprintf "result=%d" 42

let result = (old, new_)

$ langthree sprintf_vs.l3
("result=42", "result=42")
```

두 방법의 결과는 같지만, `sprintf`가 형식을 한눈에 파악하기 좋습니다. 특히 여러 값을 조합할 때 차이가 뚜렷합니다 — `"name=" + to_string name + ", age=" + to_string age`보다 `sprintf "name=%s, age=%d" name age`가 훨씬 읽기 좋습니다.

## 문자열 슬라이싱 (String Slicing)

`string_sub`보다 간결한 슬라이싱 구문을 제공합니다. `s.[start..stop]`은 인덱스 `start`부터 `stop`까지(양쪽 포함)의 부분 문자열을 반환합니다:

```
$ cat str_slice.l3
let s = "hello"
let _ = println (s.[1..3])
let _ = println (s.[0..0])
let t = "abcdef"
let _ = println (t.[0..2])
let _ = println (t.[3..5])

$ langthree str_slice.l3
ell
h
abc
def
()
```

`s.[start..]` 형태로 끝 인덱스를 생략하면, `start`부터 문자열 끝까지를 반환합니다:

```
$ cat str_slice_open.l3
let s = "hello"
let _ = println (s.[2..])
let _ = println ("hello world".[6..])

$ langthree str_slice_open.l3
llo
world
()
```

`string_sub`가 시작+길이를 사용하는 반면, 슬라이싱은 시작+끝 인덱스를 사용합니다. Python의 `s[1:4]`와 비슷하지만 끝 인덱스가 포함(inclusive)된다는 차이가 있습니다.

## String 모듈 함수

Prelude의 `String` 모듈은 문자열 검사 및 변환 함수를 제공합니다:

```
$ cat str_module.l3
let _ = println (to_string (String.endsWith "hello.txt" ".txt"))
let _ = println (to_string (String.endsWith "hello.txt" ".csv"))
let _ = println (to_string (String.startsWith "hello" "he"))
let _ = println (to_string (String.startsWith "hello" "wo"))
let _ = println (String.trim "  spaces  ")

$ langthree str_module.l3
true
false
true
false
spaces
()
```

| 함수 | 설명 |
|------|------|
| `String.endsWith s suffix` | 문자열이 suffix로 끝나는지 확인 |
| `String.startsWith s prefix` | 문자열이 prefix로 시작하는지 확인 |
| `String.trim s` | 양쪽 공백 제거 |
| `String.length s` | 문자열 길이 (`string_length`와 동일) |
| `String.contains s needle` | 부분 문자열 포함 여부 (`string_contains`와 동일) |
| `String.concat sep lst` | 구분자로 문자열 리스트를 연결 |

## StringBuilder

문자열을 여러 번 `+`나 `^^`로 연결하면 매번 새 문자열이 생성되어 비효율적입니다. `StringBuilder`는 문자열 조각들을 모아두었다가 한 번에 합치는 가변 버퍼입니다:

```
$ cat sb_basic.l3
let sb = StringBuilder.create ()
let _ = StringBuilder.add sb "hello"
let _ = StringBuilder.add sb " "
let _ = StringBuilder.add sb "world"
let _ = println (StringBuilder.toString sb)

$ langthree sb_basic.l3
hello world
()
```

`StringBuilder.add`는 문자열과 문자(`char`) 모두 받을 수 있습니다:

```
$ cat sb_char.l3
let sb = StringBuilder.create ()
let _ = StringBuilder.add sb "hi"
let _ = StringBuilder.add sb ' '
let _ = StringBuilder.add sb '!'
let _ = println (StringBuilder.toString sb)

$ langthree sb_char.l3
hi !
()
```

| 함수 | 설명 |
|------|------|
| `StringBuilder.create ()` | 빈 StringBuilder 생성 |
| `StringBuilder.add sb s` | 문자열 또는 문자를 추가 |
| `StringBuilder.toString sb` | 축적된 내용을 문자열로 반환 |

루프 안에서 문자열을 반복적으로 조립할 때, `+`보다 `StringBuilder`가 훨씬 효율적입니다.

## 부수 효과 순서 지정

함수형 언어에서 출력은 "부수 효과(side effect)"입니다 — 값을 계산하는 것이 아니라 세계에 변화를 가져다줍니다. LangThree에서 unit을 반환하는 연산을 순서대로 실행하려면 `let _ =`를 사용합니다:

```
$ cat sequence.l3
let _ = println "first"
let _ = println "second"
let _ = println "third"
let result = "done"

$ langthree sequence.l3
first
second
third
"done"
```

각 `let _ =`는 unit 결과를 바인딩하고 버림으로써, 부수 효과가 순서대로
실행되도록 보장합니다.

왜 `let _ =`가 필요한가요? LangThree는 기본적으로 표현식 기반 언어입니다. `println "first"`는 unit 값 `()`을 반환하는 표현식입니다. 이 결과를 이름 없이 버리면서 다음 표현식으로 넘어가는 방법이 `let _ =`입니다. `_`는 "이 이름에는 관심이 없다"는 관례적인 표기입니다. 시퀀싱이 자연스러운 명령형 언어와 달리, 순수 함수형 언어에서는 부수 효과의 순서를 명시적으로 표현해야 합니다.

## 문자열 함수와 파이프

문자열 함수는 파이프 연산자와 자연스럽게 결합됩니다:

```
funlang> "hello" |> string_length
5
```

문자열 변환으로 파이프라인을 구성합니다:

```
$ cat string_pipe.l3
let result = 42 |> to_string |> string_concat "answer: "

$ langthree string_pipe.l3
"answer: 42"
```

`42 |> to_string`은 `"42"`를 만들고, `|> string_concat "answer: "`는 앞서 부분 적용된 `string_concat "answer: "`에 `"42"`를 전달하여 `"answer: 42"`를 만듭니다. 파이프와 커링이 함께 쓰이는 전형적인 패턴입니다. 변환 단계가 많을수록 파이프라인 방식이 중첩 함수 호출보다 훨씬 읽기 좋습니다.

## 실용 예제: 형식화된 보고서

지금까지 배운 것을 조합해 구조화된 출력을 만드는 예입니다:

```
$ cat report.l3
let format_line label value =
    label + ": " + to_string value
let _ = println (format_line "width" 800)
let _ = println (format_line "height" 600)
let result = "report complete"

$ langthree report.l3
width: 800
height: 600
"report complete"
```

`format_line`은 레이블과 임의의 값을 받아 형식화된 문자열을 만드는 함수입니다. `to_string`이 모든 타입을 받기 때문에, `format_line`은 정수뿐 아니라 문자열, 튜플, 레코드 등 어떤 타입의 값도 출력할 수 있습니다. 이처럼 작은 헬퍼 함수를 만들어 반복을 줄이는 방식이 함수형 프로그래밍의 기본 사고방식입니다.

## 참고 사항

이 장에서 다룬 함수들의 핵심 특징을 정리합니다:

- **`string_sub`는 시작+길이를 사용합니다** (시작+끝이 아님): `string_sub "hello" 1 3` = `"ell"`
- **`string_concat`는 커링되어 있습니다:** `string_concat "prefix"`는 함수를 반환합니다
- **`to_string`**은 모든 타입을 받습니다 — 문자열은 그대로, 복합 타입은 구조적 표현
- **`printf`**는 커링되어 있습니다: 각 `%` 지정자가 하나의 추가 인자를 소비합니다
- **순서 지정:** 모듈 수준에서 부수 효과를 체이닝하려면 `let _ =`를 사용하세요

문자열이 여러 문자의 시퀀스라면, 개별 문자를 다루는 방법도 필요합니다. 다음 장에서는 `char` 타입과 문자 변환 함수를 알아봅니다.
