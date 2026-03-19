# 12장: Prelude and Standard Library

LangThree는 시작 시 Prelude라는 표준 라이브러리를 로드합니다. Prelude 파일은
명시적인 import 없이 모든 사용자 코드에서 사용 가능한 타입 선언과 생성자를 제공합니다.

## Prelude의 동작 방식

Prelude는 LangThree 바이너리와 같은 위치의 `Prelude/` 디렉토리에 있는 `.fun` 파일로
구성됩니다. 시작 시 이 파일들은 알파벳순으로 정렬된 후, 각각 모듈로 파싱되고 타입 검사를
거쳐 평가됩니다. 이 파일들이 정의하는 타입과 생성자는 이후 모든 코드에서 사용 가능합니다.

현재 Prelude에는 하나의 파일이 포함되어 있습니다:

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

## 타입 전용 내장 시그니처

Prelude와는 별도로, LangThree에는 **타입 전용** 내장 이름 세트가 있습니다.
이들은 타입 검사를 위한 타입 시그니처를 가지지만 **런타임 구현이 없습니다**.
사용자 코드에서 표준 함수 타입을 참조할 수 있도록 존재합니다:

```
$ langthree --emit-type --expr 'map'
('a -> 'b) -> 'a list -> 'b list

$ langthree --emit-type --expr 'filter'
('a -> bool) -> 'a list -> 'a list

$ langthree --emit-type --expr 'id'
'a -> 'a
```

그러나 런타임에 이들을 호출하면 오류가 발생합니다:

```
funlang> id 42
Error: Undefined variable: id
```

타입 전용 내장 함수의 전체 목록:

| 이름 | 타입 시그니처 |
|------|---------------|
| `map` | `('a -> 'b) -> 'a list -> 'b list` |
| `filter` | `('a -> bool) -> 'a list -> 'a list` |
| `fold` | `('a -> 'b -> 'a) -> 'a -> 'b list -> 'a` |
| `length` | `'a list -> int` |
| `reverse` | `'a list -> 'a list` |
| `append` | `'a list -> 'a list -> 'a list` |
| `hd` | `'a list -> 'a` |
| `tl` | `'a list -> 'a list` |
| `id` | `'a -> 'a` |
| `const` | `'a -> 'b -> 'a` |
| `compose` | `('a -> 'b) -> ('c -> 'a) -> 'c -> 'b` |

이 연산들을 사용하려면 `let rec ... in`으로 직접 정의하세요:

```
$ cat my_map.l3
let result =
    let rec myMap f = fun xs -> match xs with | [] -> [] | x :: rest -> f x :: myMap f rest
    in myMap (fun x -> x * 2) [1, 2, 3]

$ langthree my_map.l3
[2, 4, 6]
```

`let rec`은 단일 매개변수만 지원하므로, 두 번째 매개변수에는 중첩된 `fun`을 사용합니다
(자세한 내용은 [Chapter 2](02-functions.md)를 참조하세요).

## 런타임 내장 함수

별도의 내장 함수 세트는 런타임 구현을 **가지고 있습니다**. 이들은 Prelude 파일이 아닌
내장 환경에서 제공됩니다:

| 함수 | 타입 | 설명 |
|----------|------|-------------|
| `string_length` | `string -> int` | 문자열의 길이 |
| `string_concat` | `string -> string -> string` | 두 문자열을 연결 |
| `string_sub` | `string -> int -> int -> string` | 부분 문자열 (시작, 길이) |
| `string_contains` | `string -> string -> bool` | 부분 문자열 포함 여부 |
| `to_string` | `'a -> string` | int/bool/string을 문자열로 변환 |
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
```

자세한 내용은 [Chapter 11: Strings and Output](11-strings-and-output.md)을 참조하세요.

## Prelude vs 내장 함수 요약

| 분류 | 출처 | 런타임 사용 가능? | 예제 |
|----------|--------|----------------------|---------|
| Prelude 타입 | `Prelude/*.fun` 파일 | 예 (타입 + 생성자) | `Option`, `None`, `Some` |
| 타입 전용 내장 함수 | 타입 환경 | 아니오 (타입 검사만) | `map`, `id`, `compose` |
| 런타임 내장 함수 | 내장 환경 | 예 (호출 가능) | `print`, `string_length` |

## 참고 사항

- **Prelude 파일**은 `Prelude/` 디렉토리에서 알파벳순으로 로드되는 `.fun` 파일입니다
- **Prelude 생성자** (`None`, `Some`)는 `open` 없이 사용 가능합니다
- **타입 전용 내장 함수** (`map`, `id` 등)는 타입 검사는 통과하지만 런타임에 실패합니다
- **런타임 내장 함수** (`print`, `string_length` 등)는 어디서든 동작합니다
- **패턴 매칭**은 파일 모드에서 Prelude 타입에 대해 동작합니다
