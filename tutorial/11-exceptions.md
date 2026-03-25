# 11장: 예외 (Exceptions)

오류 처리는 모든 언어에서 쉽지 않은 문제입니다. 함수형 언어들은 보통 두 가지 접근을 씁니다. 하나는 `Option`이나 `Result` 같은 타입으로 오류를 값으로 표현하는 방법이고, 다른 하나는 예외(exception)를 발생시켜 호출 스택을 타고 올라가는 방법입니다. LangThree는 둘 다 지원하며, 이 장에서는 예외 메커니즘을 다룹니다.

예외는 "예상치 못한 상황"을 처리할 때 강력합니다. 깊이 중첩된 함수 안에서 발생한 오류를 모든 계층에서 하나씩 전달하지 않고, 한 번에 적절한 핸들러까지 올려보낼 수 있습니다. 다만, 예외를 남용하면 제어 흐름을 추적하기 어려워지므로, 정말 예외적인 상황에만 쓰는 것이 좋습니다. 예외와 `Option`/`Result` 중 언제 어떤 것을 써야 하는지는 바로 다음 [12장: 에러 처리 전략](12-error-handling.md)에서 비교합니다.

## 예외 선언

`exception`으로 예외 타입을 선언합니다. LangThree의 예외는 ADT의 생성자와 비슷하게 생겼습니다. 이름을 선언하고, `raise`로 발생시키고, `try-with`로 잡습니다:

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

OCaml이나 F#의 예외 구문과 거의 동일합니다. `try-with` 블록 안에서 예외가 발생하면 `with` 아래의 패턴들과 순서대로 매칭합니다. 매칭된 핸들러의 결과가 전체 `try-with` 식의 결과가 됩니다.

한 가지 중요한 점: LangThree에서 `try-with`는 식(expression)입니다. 값을 반환하므로 `let result =` 등에 바인딩할 수 있습니다. Java처럼 "제어 흐름을 위한 문장"이 아니라 F#처럼 "값을 생산하는 식"입니다.

## 데이터를 가진 예외

예외가 단순히 "무언가 잘못됐다"는 신호를 보내는 것을 넘어, 왜 잘못됐는지에 대한 정보를 담을 수 있습니다. `of`를 사용하면 예외에 데이터를 실을 수 있습니다:

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

핸들러에서 `InvalidArg msg`처럼 패턴 매칭으로 데이터를 꺼낼 수 있습니다. 오류 메시지, 오류 코드, 또는 어떤 타입이든 담을 수 있어서 오류의 맥락을 풍부하게 전달할 수 있습니다.

참고: `raise`는 원자(atom)를 받으므로, 생성자 적용에는 괄호가 필요합니다: `raise (InvalidArg "bad input")`, `raise InvalidArg "bad input"`이 아닙니다. 이 점을 빠뜨리면 파서가 `raise`에 `InvalidArg`만 넘기고 `"bad input"`을 별개의 식으로 해석합니다. 컴파일 오류가 발생하니 금방 알아챌 수 있지만, 알아두면 헷갈리지 않습니다.

## 여러 핸들러

실제 코드에서는 여러 종류의 예외가 발생할 수 있습니다. 동일한 `try-with`에서 여러 예외 타입을 처리할 수 있고, 각 예외에 맞는 다른 응답을 제공할 수 있습니다:

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

핸들러는 위에서 아래로 순서대로 시도됩니다. `match` 표현식과 동일한 규칙입니다. 첫 번째로 매칭되는 핸들러가 실행되므로, 더 구체적인 핸들러를 앞에, `| _ ->` 같은 포괄적인 핸들러를 뒤에 배치해야 합니다. 순서를 반대로 하면 구체적인 핸들러가 영원히 도달하지 못할 수 있습니다.

## when 가드

때로는 같은 예외 타입이지만 데이터에 따라 다르게 처리해야 할 때가 있습니다. `when` 가드를 사용하면 패턴 매칭 후 추가 조건을 검사할 수 있습니다:

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

가드는 패턴이 매치된 후에 평가됩니다. 가드가 실패하면 다음 핸들러로 매칭이 계속됩니다.

42는 100보다 크지 않으므로 첫 번째 핸들러의 `when x > 100` 가드가 실패하고, 두 번째 핸들러 `Error x`로 넘어갑니다. 이 패턴은 "같은 예외지만 심각도에 따라 다르게 처리"하는 경우에 특히 유용합니다. HTTP 상태 코드처럼 숫자로 오류 코드를 넘길 때 범위별로 처리하는 코드를 `when` 가드로 깔끔하게 표현할 수 있습니다.

## 중첩된 try-with

예외는 발생한 위치에서 가장 가까운 핸들러로 이동합니다. 중첩된 `try-with`가 있으면 안쪽부터 먼저 시도합니다. 처리되지 않은 예외는 외부 핸들러로 전파됩니다:

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

내부 핸들러가 매치되지 않으면 예외가 외부로 전파됩니다. 내부의 `raise`를 `raise Outer`로 변경하면 내부 핸들러의 catch-all이 대신 매치됩니다.

이 예제에서 `Inner` 예외가 발생했을 때, 안쪽 `try-with`의 핸들러를 먼저 봅니다. `Outer`는 매칭 실패, `_ -> "inner caught"`는 모든 예외를 잡으므로 여기서 처리됩니다. 만약 안쪽에 `| _ -> ...`가 없었다면 `Inner`는 바깥쪽 `try-with`까지 전파되어 `| Inner -> "outer caught"`에 잡혔을 것입니다.

이런 전파 메커니즘 덕분에, 저수준 함수는 예외를 발생시키고 고수준 코드에서 한 번에 처리하는 구조를 만들 수 있습니다. 모든 중간 계층에서 오류를 전달하는 boilerplate가 필요 없습니다.

## 예외 재발생

어떤 핸들러도 매치되지 않으면 예외는 자동으로 재발생(re-raise)됩니다. 이것은 예외의 자동 전파 메커니즘입니다:

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

`First`가 발생했을 때 안쪽 `try-with`를 보면, `Second`는 매칭 실패하지만 `| _ ->` 가 모든 것을 잡으므로 `"inner fallback"`이 반환됩니다. 만약 안쪽 `| _ ->`가 없었다면 `First`는 바깥쪽으로 전파되어 `"outer caught first"`가 되었을 것입니다.

중요한 점은, 핸들러가 하나도 매칭되지 않을 때 예외가 자동으로 재발생된다는 것입니다. 명시적으로 `re-raise`를 호출할 필요가 없습니다. 이 동작을 잘 이해하면 "이 레이어에서 처리할 예외"와 "상위 레이어로 올려보낼 예외"를 선택적으로 처리하는 구조를 설계할 수 있습니다.

## 비완전 핸들러 경고 (W0003)

예외는 개방 타입(open type)입니다. 새로운 예외를 어디서든 선언할 수 있기 때문에, 컴파일러는 현재 핸들러가 모든 가능한 예외를 커버하는지 정적으로 알 수 없습니다. 따라서 catch-all이 없는 핸들러에 대해 경고합니다:

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

이 경고는 무시해도 코드가 동작하지만, 프로덕션 코드에서는 없애는 게 좋습니다. 미처 생각하지 못한 예외가 발생했을 때 프로그램이 조용히 죽는 것보다, catch-all에서 명시적으로 처리하는 것이 훨씬 안전합니다.

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

ADT의 패턴 매칭이 "닫힌 타입"에 대해 완전성을 보장할 수 있는 것과 달리, 예외는 "열린 타입"이라 항상 미지의 예외가 올 수 있습니다. `| _ -> 0`처럼 기본값을 제공하거나, `| _ -> raise e`처럼 잡은 예외를 다시 던지는 방법도 있습니다. 어떻게 처리할지는 상황에 따라 다르지만, 경고 자체를 무시하는 것은 피하세요.

## 실용 예제: 안전한 나눗셈

예외를 실제 코드에 적용하는 전형적인 사례입니다. 0으로 나누는 것은 수학적으로 정의되지 않으므로 예외로 처리합니다:

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

`safe_div` 함수 자체는 예외 처리를 하지 않고 발생시키기만 합니다. 어떻게 처리할지는 호출하는 쪽의 맥락에 따라 다를 수 있기 때문입니다. 어떤 곳에서는 -1을 반환하고, 다른 곳에서는 0을 반환하거나, 또 다른 곳에서는 오류 메시지를 출력할 수 있습니다. 이렇게 발생과 처리를 분리하면 `safe_div`는 재사용 가능한 순수한 함수가 됩니다.

비교해보면, `Option`을 쓰는 방식 (`if b = 0 then None else Some (a / b)`)은 오류를 값으로 표현해 타입에 드러냅니다. 호출하는 쪽이 `None`을 처리해야 한다는 것이 타입 시스템에 강제됩니다. 어느 방식을 택하느냐는 함수가 실패할 가능성이 얼마나 일상적인가에 달려있습니다. 빈번히 실패할 수 있는 연산이라면 `Option`이나 `Result`가 더 적합하고, 정말 예외적인 상황이라면 예외가 더 자연스럽습니다.

## failwith 내장 함수

`failwith`는 문자열 메시지와 함께 예외를 발생시키는 내장 함수입니다. 간단한 오류 처리에 유용합니다:

```
$ cat failwith_demo.l3
let safeDivide a b =
    if b = 0 then failwith "division by zero"
    else a / b
let result =
    try
        safeDivide 10 0
    with
    | e -> 0

$ langthree failwith_demo.l3
0
```

`failwith`는 커스텀 예외를 선언하지 않고도 빠르게 오류를 발생시킬 때 편리합니다. F#의 `failwith`와 동일합니다.

## 인라인 try-with

간단한 경우에는 `try-with`를 한 줄로 작성할 수 있습니다. 파이프 `|` 없이 바로 패턴과 핸들러를 쓸 수 있습니다:

```
$ cat inline_try.l3
let result = try failwith "boom" with e -> "caught"

$ langthree inline_try.l3
"caught"
```

여러 핸들러가 필요하면 파이프를 사용하는 일반 형태를 쓰세요. 인라인 형태는 단일 catch-all 핸들러에 적합합니다.

## 구문 참고 사항

- **`raise`는 원자를 받음:** 데이터를 가진 생성자에는 괄호를 사용: `raise (Error msg)`
- **`try` 들여쓰기:** `with` 핸들러의 파이프는 `match` 파이프와 같은 방식으로 정렬
- **인라인 try-with:** `try expr with ident -> expr` 형태로 한 줄 작성 가능
- **`failwith`:** `failwith "msg"`로 예외를 빠르게 발생
- **개방 타입:** 예외 타입은 완전한 매칭이 불가능 (따라서 W0003 경고 발생)
- **catch-all:** 모든 예외를 포괄하려면 마지막 핸들러로 `| _ -> ...`를 추가
