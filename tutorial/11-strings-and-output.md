# 11장: Strings and Output

## 문자열 리터럴

문자열 리터럴은 큰따옴표와 표준 이스케이프 시퀀스를 사용합니다:

```
funlang> "hello world"
"hello world"

funlang> "tab\there"
"tab	here"
```

## 문자열 연결

`+` 연산자로 문자열을 연결합니다:

```
funlang> "hello" + " " + "world"
"hello world"
```

## 내장 문자열 함수

### string_length

문자 수를 반환합니다:

```
funlang> string_length "hello"
5

funlang> string_length ""
0
```

### string_concat

커링된 연결 함수 -- 두 개의 문자열을 받습니다:

```
funlang> string_concat "hello" " world"
"hello world"
```

커링되어 있으므로 부분 적용이 가능합니다:

```
$ cat prefix.l3
let add_prefix = string_concat "prefix:"
let result = add_prefix "value"

$ langthree prefix.l3
"prefix:value"
```

### string_sub

시작 인덱스와 길이로 부분 문자열을 추출합니다:

```
funlang> string_sub "hello" 1 3
"ell"
```

`string_sub s start len`은 인덱스 `start`부터 `len`개의 문자를 반환합니다.
인덱스는 0부터 시작합니다.

### string_contains

문자열이 부분 문자열을 포함하는지 확인합니다:

```
funlang> string_contains "hello world" "world"
true

funlang> string_contains "hello" "xyz"
false
```

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

funlang> to_string [1, 2, 3]
"[1, 2, 3]"

funlang> to_string (1, true)
"(1, true)"
```

**참고:** 문자열은 그대로 반환됩니다 (따옴표 없음, F# `string` 함수와 동일).
복합 타입 내부의 문자열에는 따옴표가 포함됩니다: `to_string (Some "hi")` → `Some "hi"`.

### string_to_int

문자열을 정수로 파싱합니다:

```
funlang> string_to_int "123"
123
```

## 출력 함수

### print

줄바꿈 없이 문자열을 출력하고, unit을 반환합니다:

```
funlang> print "hello"
hello()
```

대화형 세션에서는 부수 효과 텍스트가 `()` 결과 바로 앞에 나타납니다.

### println

줄바꿈을 포함하여 문자열을 출력하고, unit을 반환합니다:

```
funlang> println "hello"
hello
()
```

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

복수 지정자:

```
$ cat printf_multi.l3
let _ = printf "name=%s, active=%b\n" "Bob" true
let result = 0

$ langthree printf_multi.l3
name=Bob, active=true
0
```

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

`printf "...\n"`을 쓸 필요 없이 `printfn "..."`을 사용하세요.

### sprintf

형식화된 문자열을 **반환**합니다 (출력하지 않음):

```
funlang> sprintf "%d + %d = %d" 1 2 3
"1 + 2 = 3"

funlang> sprintf "name=%s" "Alice"
"name=Alice"
```

`^^`와 `to_string` 대신 `sprintf`를 사용하면 더 간결합니다:

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

## 부수 효과 순서 지정

unit을 반환하는 연산을 순서대로 실행하려면 `let _ =`를 사용합니다:

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

## 실용 예제: 형식화된 보고서

구조화된 출력을 위한 문자열 연산 조합:

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

## 참고 사항

- **`string_sub`는 시작+길이를 사용합니다** (시작+끝이 아님): `string_sub "hello" 1 3` = `"ell"`
- **`string_concat`는 커링되어 있습니다:** `string_concat "prefix"`는 함수를 반환합니다
- **`to_string`**은 모든 타입을 받습니다 — 문자열은 그대로, 복합 타입은 구조적 표현
- **`printf`**는 커링되어 있습니다: 각 `%` 지정자가 하나의 추가 인자를 소비합니다
- **순서 지정:** 모듈 수준에서 부수 효과를 체이닝하려면 `let _ =`를 사용하세요
