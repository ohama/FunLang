# 제9장: 예외 (Exceptions)

## 예외 선언

`exception`으로 예외 타입을 선언합니다:

```
$ cat exc_basic.l3
exception NotFound
let result = try
    raise NotFound
with
| NotFound -> 42
| _ -> 0

$ langthree exc_basic.l3
42
```

## 데이터를 가진 예외

생성자는 `of`를 사용하여 값을 포함할 수 있습니다:

```
$ cat exc_data.l3
exception InvalidArg of string
let result = try
    raise (InvalidArg "bad input")
with
| InvalidArg msg -> "error: " + msg
| _ -> "unknown"

$ langthree exc_data.l3
"error: bad input"
```

참고: `raise`는 원자(atom)를 받으므로, 생성자 적용에는 괄호가 필요합니다:
`raise (InvalidArg "bad input")`, `raise InvalidArg "bad input"`이 아닙니다.

## 여러 핸들러

동일한 `try-with`에서 여러 예외 타입을 매치할 수 있습니다:

```
$ cat exc_multi.l3
exception NotFound
exception Timeout of int
let result = try
    raise (Timeout 30)
with
| NotFound -> "not found"
| Timeout secs -> "timeout after " + to_string secs + "s"
| _ -> "unknown"

$ langthree exc_multi.l3
"timeout after 30s"
```

## when 가드

`when`으로 예외 핸들러에 조건을 추가할 수 있습니다:

```
$ cat exc_guard.l3
exception Error of int
let result = try
    raise (Error 42)
with
| Error x when x > 100 -> "big error"
| Error x -> "error: " + to_string x
| _ -> "unknown"

$ langthree exc_guard.l3
"error: 42"
```

가드는 패턴이 매치된 후에 평가됩니다. 가드가 실패하면
다음 핸들러로 매칭이 계속됩니다.

## 중첩된 try-with

처리되지 않은 예외는 외부 핸들러로 전파됩니다:

```
$ cat exc_nested.l3
exception Inner
exception Outer
let result = try
    try
        raise Inner
    with
    | Outer -> "wrong"
    | _ -> "inner caught"
with
| Inner -> "outer caught"
| _ -> "fallback"

$ langthree exc_nested.l3
"inner caught"
```

내부 핸들러가 매치되지 않으면 예외가 외부로 전파됩니다.
내부의 `raise`를 `raise Outer`로 변경하면 내부 핸들러의
catch-all이 대신 매치됩니다.

## 예외 재발생

어떤 핸들러도 매치되지 않으면 예외는 자동으로 재발생(re-raise)됩니다:

```
$ cat exc_reraise.l3
exception First
exception Second
let result = try
    try
        raise First
    with
    | Second -> "wrong"
    | _ -> "inner fallback"
with
| First -> "outer caught first"
| _ -> "outer fallback"

$ langthree exc_reraise.l3
"inner fallback"
```

## 비완전 핸들러 경고 (W0003)

예외는 개방 타입(open type)이므로 (새로운 예외를 어디서든 선언할 수 있음),
핸들러에 catch-all이 없으면 컴파일러가 경고합니다:

```
$ cat exc_warn.l3
exception NotFound
let result = try
    raise NotFound
with
| NotFound -> 42

$ langthree exc_warn.l3
Warning: warning[W0003]: Non-exhaustive exception handler: not all exceptions are handled; consider adding a catch-all handler
 --> :0:0-1:0
   = hint: Add a catch-all handler or handle all possible exceptions
42
```

경고를 없애려면 `| _ -> ...`를 추가하세요:

```
$ cat exc_nowarn.l3
exception NotFound
let result = try
    raise NotFound
with
| NotFound -> 42
| _ -> 0

$ langthree exc_nowarn.l3
42
```

## 실용 예제: 안전한 나눗셈

예외와 함수를 결합한 예제:

```
$ cat safe_div.l3
exception DivByZero
let safe_div a b =
    if b = 0 then raise DivByZero
    else a / b
let result = try
    safe_div 10 0
with
| DivByZero -> -1
| _ -> -2

$ langthree safe_div.l3
-1
```

## 구문 참고 사항

- **`raise`는 원자를 받음:** 데이터를 가진 생성자에는 괄호를 사용: `raise (Error msg)`
- **`try` 들여쓰기:** `with` 핸들러의 파이프는 `match` 파이프와 같은 방식으로 정렬
- **개방 타입:** 예외 타입은 완전한 매칭이 불가능 (따라서 W0003 경고 발생)
- **catch-all:** 모든 예외를 포괄하려면 마지막 핸들러로 `| _ -> ...`를 추가
