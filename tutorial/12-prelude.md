# 12장: Prelude and Standard Library

LangThree는 시작 시 Prelude라는 표준 라이브러리를 로드합니다. Prelude 파일은
명시적인 import 없이 모든 사용자 코드에서 사용 가능한 타입, 생성자, 함수를 제공합니다.

## Prelude의 동작 방식

Prelude는 LangThree 바이너리와 같은 위치의 `Prelude/` 디렉토리에 있는 `.fun` 파일로
구성됩니다. 시작 시 이 파일들은 알파벳순으로 정렬된 후, 각각 모듈로 파싱되고 타입 검사를
거쳐 평가됩니다. 이 파일들이 정의하는 타입, 생성자, 함수는 이후 모든 코드에서 사용 가능합니다.

현재 Prelude에는 다음 파일들이 포함되어 있습니다:

- `Prelude/Core.fun` -- 핵심 고차 함수 (`id`, `const`, `compose`)
- `Prelude/List.fun` -- 리스트 처리 함수 (`map`, `filter`, `fold`, `length`, `reverse`, `append`, `hd`, `tl`)
- `Prelude/Option.fun` -- Option 타입 정의

**Prelude/Option.fun:**
```
type Option 'a = None | Some of 'a
```

이 파일은 `Option` 타입과 `None`, `Some` 생성자를 정의하며, `open` 지시어 없이
어디서든 사용 가능합니다.

## Option 타입 사용하기

### Option 값 생성

`Some`과 `None` 생성자는 REPL과 파일 모드 모두에서 동작합니다:

```
funlang> Some 42
Some 42

funlang> Some "hello"
Some "hello"

funlang> None
None
```

추론된 타입을 확인합니다:

```
$ cat check_option.l3
let x = Some 42

$ langthree --emit-type check_option.l3
x : Option<int>
```

### Option에 대한 패턴 매칭

Prelude 타입에 대한 패턴 매칭은 파일 모드에서 동작합니다:

```
$ cat option_match.l3
let x = Some 42
let result =
    match x with
    | Some v -> v
    | None -> 0

$ langthree option_match.l3
42
```

기본값으로 추출하기:

```
$ cat option_default.l3
let getOrDefault default opt =
    match opt with
    | Some x -> x
    | None -> default
let result = getOrDefault 0 None

$ langthree option_default.l3
0
```

### 일반적인 Option 패턴

**Option에 대한 맵핑** -- `Some` 내부의 값에 함수를 적용합니다:

```
$ cat option_map.l3
let optionMap f opt =
    match opt with
    | Some x -> Some (f x)
    | None -> None
let double x = x * 2
let result =
    match optionMap double (Some 5) with
    | Some v -> v
    | None -> 0

$ langthree option_map.l3
10
```

**바인딩 (flatMap)** -- 실패할 수 있는 연산을 체이닝합니다:

```
$ cat option_bind.l3
let optionBind f opt =
    match opt with
    | Some x -> f x
    | None -> None
let safeDivide x =
    if x = 0 then None else Some (100 / x)
let result =
    match optionBind safeDivide (Some 5) with
    | Some v -> v
    | None -> 0

$ langthree option_bind.l3
20
```

**파이프와 함께 Option 사용하기:**

```
$ cat option_pipe.l3
let optionMap f opt =
    match opt with
    | Some x -> Some (f x)
    | None -> None
let double x = x * 2
let result =
    match (Some 5 |> optionMap double) with
    | Some v -> v
    | None -> 0

$ langthree option_pipe.l3
10
```

## Prelude 확장하기

`Prelude/` 디렉토리에 새 `.fun` 파일을 생성하여 자신만의 타입을 Prelude에 추가할 수
있습니다. 파일은 알파벳순으로 정렬되므로, 파일 이름이 로드 순서에 영향을 줍니다.

예를 들어, `Prelude/Result.fun`을 생성하면:

```
type Result 'a 'b = Ok of 'a | Error of 'b
```

이 파일을 추가한 후, `Ok`과 `Error` 생성자가 모든 코드에서 사용 가능해집니다:

```
$ cat result_demo.l3
let safeDivide x y =
    if y = 0 then Error "division by zero"
    else Ok (x / y)
let result =
    match safeDivide 10 3 with
    | Ok v -> v
    | Error _ -> 0

$ langthree result_demo.l3
3
```

Prelude 타입은 REPL과 파일 모드 모두에서 동작합니다. Prelude 파일의 생성자는
`open` 없이 사용 가능합니다.

## Prelude 리스트 함수

`Prelude/List.fun`은 리스트를 다루는 8개의 표준 함수를 제공합니다. 이 함수들은
import 없이 REPL과 파일 모드 모두에서 바로 사용할 수 있는 실제 함수입니다.

### 리스트 변환: map, filter

`map`은 리스트의 각 요소에 함수를 적용합니다:

```
funlang> map (fun x -> x * 2) [1..5]
[2, 4, 6, 8, 10]
```

`filter`는 조건을 만족하는 요소만 남깁니다:

```
funlang> filter (fun x -> x > 3) [1..6]
[4, 5, 6]
```

### 리스트 축약: fold

`fold`는 리스트를 하나의 값으로 축약합니다:

```
funlang> fold (fun acc -> fun x -> acc + x) 0 [1..10]
55
```

### 리스트 정보: length, hd, tl

```
funlang> length [1, 2, 3]
3

funlang> hd [10, 20]
10

funlang> tl [10, 20]
[20]
```

### 리스트 조작: reverse, append

```
funlang> reverse [] [1, 2, 3]
[3, 2, 1]

funlang> append [1, 2] [3, 4]
[1, 2, 3, 4]
```

## Prelude 핵심 함수

`Prelude/Core.fun`은 범용 고차 함수 3개를 제공합니다. 마찬가지로 import 없이
어디서든 사용할 수 있습니다.

### id -- 항등 함수

입력값을 그대로 반환합니다:

```
funlang> id 42
42
```

### const -- 상수 함수

첫 번째 인자를 반환하고 두 번째 인자를 무시합니다:

```
funlang> const 42 "ignored"
42
```

### compose -- 함수 합성

두 함수를 합성합니다. `compose f g x`는 `f (g x)`와 같습니다:

```
funlang> compose inc double 5
11
```

## 파이프라인과 함께 사용하기

Prelude 함수들은 파이프 연산자 `|>`와 결합하면 강력한 데이터 처리 파이프라인을
구성할 수 있습니다:

```
$ cat pipeline.l3
let result =
    [1..10]
    |> filter (fun x -> x % 2 = 0)
    |> map (fun x -> x * x)

$ langthree pipeline.l3
[4, 16, 36, 64, 100]
```

## Prelude 연산자

Prelude는 자주 사용하는 패턴을 위한 연산자도 제공합니다.

### `++` — 리스트 연결

`append`의 중위 연산자 버전입니다:

```
funlang> [1, 2] ++ [3, 4, 5]
[1, 2, 3, 4, 5]
```

`(++)`를 함수로 사용할 수도 있습니다:

```
funlang> fold (++) [] [[1, 2], [3], [4, 5]]
[1, 2, 3, 4, 5]
```

`++`는 INFIXOP2 (+ 와 같은 우선순위, 좌결합)입니다.

### `<|>` — Option 대안

첫 번째 `Some` 값을 반환하거나, 모두 `None`이면 `None`을 반환합니다:

```
funlang> Some 1 <|> Some 2
Some 1

funlang> None <|> Some 42
Some 42

funlang> None <|> None
None
```

연쇄하여 fallback 패턴을 구현할 수 있습니다:

```
$ cat fallback.l3
let tryParse s = match s with | "42" -> Some 42 | "0" -> Some 0 | _ -> None
let result = tryParse "abc" <|> tryParse "xyz" <|> tryParse "42" <|> Some 0

$ langthree fallback.l3
Some 42
```

`<|>`는 INFIXOP0 (비교 연산자 수준, 좌결합)입니다.

### `^^` — 문자열 연결

`string_concat`의 중위 연산자 버전입니다:

```
funlang> "hello" ^^ " " ^^ "world"
"hello world"
```

`+` 연산자는 정수 덧셈이므로, 문자열 연결에는 `^^`를 사용합니다:

```
$ cat string_build.l3
let formatPair key = fun value -> key ^^ "=" ^^ value
let result = formatPair "name" "Alice"

$ langthree string_build.l3
"name=Alice"
```

`^^`는 INFIXOP1 (@ 와 같은 우선순위, 우결합)입니다.

## 런타임 내장 함수

Prelude 함수와는 별도로, LangThree에는 내장 환경(`initialBuiltinEnv`)에서
제공되는 런타임 내장 함수가 있습니다:

| 함수 | 타입 | 설명 |
|----------|------|-------------|
| `string_length` | `string -> int` | 문자열의 길이 |
| `string_concat` | `string -> string -> string` | 두 문자열을 연결 |
| `string_sub` | `string -> int -> int -> string` | 부분 문자열 (시작, 길이) |
| `string_contains` | `string -> string -> bool` | 부분 문자열 포함 여부 |
| `to_string` | `'a -> string` | 모든 타입을 문자열로 변환 (문자열은 그대로) |
| `string_to_int` | `string -> int` | 문자열을 정수로 파싱 |
| `print` | `string -> unit` | 줄바꿈 없이 출력 |
| `println` | `string -> unit` | 줄바꿈 포함 출력 |
| `printf` | `string -> ...` | 형식화 출력 |

이들은 REPL과 파일 모드 모두에서 동작합니다:

```
funlang> string_length "hello"
5

funlang> to_string 42
"42"

funlang> to_string (Some [1, 2, 3])
"Some [1, 2, 3]"
```

`to_string`은 모든 타입을 지원합니다 — ADT, 리스트, 튜플, 레코드 등.
문자열은 따옴표 없이 그대로 반환됩니다 (F# `string` 함수와 동일).

자세한 내용은 [Chapter 11: Strings and Output](11-strings-and-output.md)을 참조하세요.

## Prelude vs 내장 함수 요약

| 분류 | 출처 | 예제 |
|----------|--------|---------|
| Prelude 타입+함수 | `Prelude/*.fun` 파일 | `Option`, `map`, `filter`, `fold`, `id`, `compose` 등 |
| Prelude 연산자 | `Prelude/*.fun` 파일 | `++` (리스트 연결), `<\|>` (Option 대안), `^^` (문자열 연결) |
| 런타임 내장 함수 | `initialBuiltinEnv` | `string_length`, `print`, `println`, `printf` 등 |

두 분류 모두 런타임에 실제로 동작하며, REPL과 파일 모드에서 import 없이 사용 가능합니다.

## 참고 사항

- **Prelude 파일**은 `Prelude/` 디렉토리에서 알파벳순으로 로드되는 `.fun` 파일입니다
- **Prelude 생성자** (`None`, `Some`)와 **Prelude 함수** (`map`, `filter` 등)는 `open` 없이 사용 가능합니다
- **Prelude 함수**는 모두 실제 런타임 함수로, 호출하면 정상적으로 결과를 반환합니다
- **런타임 내장 함수** (`print`, `string_length` 등)는 어디서든 동작합니다
- **패턴 매칭**은 파일 모드에서 Prelude 타입에 대해 동작합니다
