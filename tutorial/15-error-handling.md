# 15장: 에러 처리 전략 (Error Handling Strategies)

LangThree는 에러를 처리하는 세 가지 방법을 제공합니다:
**Exception**, **Option**, **Result**. 이 장에서는 세 가지를 비교하고,
언제 어떤 것을 사용해야 하는지 설명합니다.

## 세 가지 접근법 비교

같은 문제를 세 가지 방식으로 풀어보겠습니다: 리스트에서 조건을 만족하는
첫 번째 원소의 인덱스를 찾는 함수입니다.

### Exception 방식

```
$ cat find_exc.l3
exception NotFound of string

let rec findIdx pred = fun xs -> fun i -> match xs with | [] -> raise (NotFound "not found") | h :: t -> if pred h then i else findIdx pred t (i + 1)

let result = try
    findIdx (fun x -> x > 3) [1, 2, 3, 4, 5] 0
with
| NotFound _ -> 0 - 1

$ langthree find_exc.l3
3
```

### Option 방식

```
$ cat find_opt.l3
let rec findIdx pred = fun xs -> fun i -> match xs with | [] -> None | h :: t -> if pred h then Some i else findIdx pred t (i + 1)

let result = optionDefault (0 - 1) (findIdx (fun x -> x > 3) [1, 2, 3, 4, 5] 0)

$ langthree find_opt.l3
3
```

### Result 방식

```
$ cat find_res.l3
let rec findIdx pred = fun xs -> fun i -> match xs with | [] -> Error "not found" | h :: t -> if pred h then Ok i else findIdx pred t (i + 1)

let result = resultDefault (0 - 1) (findIdx (fun x -> x > 3) [1, 2, 3, 4, 5] 0)

$ langthree find_res.l3
3
```

세 가지 모두 같은 결과를 반환합니다. 그러나 **코드의 의미와 안전성**에서 큰 차이가 있습니다.

## 왜 Option/Result를 권장하는가

### 1. 타입 시스템이 실패를 표현한다

Exception은 타입 시그니처에 나타나지 않습니다:

```
// Exception: 타입만 보면 실패 가능성을 알 수 없음
// findIdx : ('a -> bool) -> 'a list -> int -> int
//                                            ^^^
//   실패 시 예외를 던지지만, 타입에는 int만 보임!

// Option: 타입이 실패 가능성을 명시
// findIdx : ('a -> bool) -> 'a list -> int -> Option<int>
//                                             ^^^^^^^^^^^
//   None이 올 수 있다는 것을 호출자가 알 수 있음

// Result: 타입이 실패 이유까지 명시
// findIdx : ('a -> bool) -> 'a list -> int -> Result<int, string>
//                                             ^^^^^^^^^^^^^^^^^^^^
//   에러 메시지가 포함될 수 있음을 호출자가 알 수 있음
```

Option이나 Result를 사용하면, **호출자가 반드시 실패 경우를 처리해야 합니다**.
패턴 매칭에서 `None`이나 `Error`를 처리하지 않으면 소진 경고가 나옵니다.

Exception은 호출자가 `try-with`를 잊으면 프로그램이 크래시합니다.

### 2. 합성(composition)이 자연스럽다

Exception은 합성이 어렵습니다. 여러 실패 가능한 연산을 순서대로 실행하려면
중첩된 `try-with`가 필요합니다:

```
$ cat chain_exc.l3
exception ParseError of string
exception DivError of string

let parseInt s = match s with | "42" -> 42 | "0" -> 0 | _ -> raise (ParseError ("invalid: " + s))
let safeDivide a = fun b -> if b = 0 then raise (DivError "div/0") else a / b

let compute input = try
    let n = parseInt input
    in try
        safeDivide 100 n
    with
    | DivError msg -> 0 - 1
with
| ParseError msg -> 0 - 2

let r1 = compute "42"
let r2 = compute "0"
let r3 = compute "abc"
let result = (r1, r2, r3)

$ langthree chain_exc.l3
(2, -1, -2)
```

Result를 사용하면 **파이프라인으로 합성**할 수 있습니다:

```
$ cat chain_res.l3
let parseInt s = match s with | "42" -> Ok 42 | "0" -> Ok 0 | _ -> Error ("invalid: " + s)
let safeDivide a = fun b -> if b = 0 then Error "div/0" else Ok (a / b)

let compute input = parseInt input |> resultBind (safeDivide 100) |> resultMap (fun x -> x + 1)

let r1 = compute "42"
let r2 = compute "0"
let r3 = compute "abc"
let result = (r1, r2, r3)

$ langthree chain_res.l3
(Ok 3, Error "div/0", Error "invalid: abc")
```

`resultBind`와 `resultMap`으로 에러가 자동 전파됩니다:
- `parseInt "abc"` → `Error "invalid: abc"` → `resultBind`가 `safeDivide`를 건너뛰고 에러 전파
- 에러가 발생한 정확한 지점의 메시지가 보존됩니다

### 3. 예외는 제어 흐름을 깨뜨린다

Exception은 **비지역적(non-local) 제어 흐름**입니다. `raise`는 현재 함수를
즉시 벗어나 가장 가까운 `try-with`까지 스택을 풀어올립니다. 이로 인해:

- 어디서 예외가 발생했는지 추적이 어렵습니다
- 중간 정리(cleanup) 코드가 실행되지 않을 수 있습니다
- 꼬리 호출 최적화(TCO)가 `try` 본문에서 동작하지 않습니다

Option/Result는 **일반적인 값**입니다. 함수가 `None`이나 `Error`를 반환하면,
호출자는 평범한 패턴 매칭으로 처리합니다. 제어 흐름이 예측 가능합니다.

### 4. TCO와 함께 사용할 수 있다

`try` 본문은 꼬리 위치가 아닙니다 (예외 핸들러가 스택 프레임을 필요로 하기 때문).
따라서 `try` 안에서 깊은 재귀를 하면 스택 오버플로우가 발생할 수 있습니다.

Option/Result를 사용하면 재귀 함수가 `try` 없이 동작하므로 TCO가 적용됩니다:

```
$ cat tco_result.l3
let rec searchList pred = fun xs -> fun i -> match xs with | [] -> Error "not found" | h :: t -> if pred h then Ok i else searchList pred t (i + 1)

let result = searchList (fun x -> x = 999999) [1..1000000] 0

$ langthree tco_result.l3
Ok 999998
```

100만 개의 리스트를 검색해도 스택 오버플로우 없이 동작합니다.

## Option vs Result: 언제 어떤 것을 쓰는가

### Option — 실패 이유가 중요하지 않을 때

"값이 있거나 없거나"만 구분하면 될 때 `Option`을 사용합니다:

```
$ cat option_use.l3
let safeHead xs = match xs with | [] -> None | h :: _ -> Some h
let safeDivide a = fun b -> if b = 0 then None else Some (a / b)

let result = Some [10, 20, 30] |> optionBind safeHead |> optionBind (safeDivide 100) |> optionDefault 0

$ langthree option_use.l3
10
```

적합한 경우:
- 리스트에서 원소 찾기 (`find`)
- 맵에서 키 검색
- 파싱이 성공하거나 실패하거나 (이유 불필요)
- null 대체 (nullable 값)

### Result — 실패 이유가 중요할 때

에러 메시지나 에러 코드를 보존해야 할 때 `Result`를 사용합니다:

```
$ cat result_use.l3
let validateAge age = if age < 0 then Error "age cannot be negative" else if age > 150 then Error "age too large" else Ok age
let validateName name = if string_length name = 0 then Error "name cannot be empty" else Ok name

let validate name = fun age -> validateName name |> resultBind (fun _ -> validateAge age |> resultMap (fun a -> name + " (" + to_string a + ")"))

let r1 = validate "Alice" 30
let r2 = validate "" 25
let r3 = validate "Bob" (0 - 5)
let result = (r1, r2, r3)

$ langthree result_use.l3
(Ok "Alice (30)", Error "name cannot be empty", Error "age cannot be negative")
```

적합한 경우:
- 사용자 입력 검증
- 파일/네트워크 작업 (에러 메시지 필요)
- 여러 단계의 처리 파이프라인
- API 응답 (에러 코드 + 메시지)

### Exception — 정말 예외적인 상황에만

Exception은 **프로그래밍 오류나 복구 불가능한 상황**에 사용합니다:

```
$ cat exception_use.l3
exception InternalError of string

let rec processAll xs = match xs with | [] -> 0 | h :: t -> if h < 0 then raise (InternalError "unexpected negative value") else h + processAll t

let result = processAll [1, 2, 3, 4, 5]

$ langthree exception_use.l3
15
```

적합한 경우:
- 프로그래밍 오류 (불변 조건 위반)
- 복구 불가능한 시스템 오류
- 방어적 프로그래밍 (도달할 수 없는 코드에 대한 안전장치)

## Prelude 함수 요약

```
-- Option 함수
optionMap    : ('a -> 'b) -> Option<'a> -> Option<'b>
optionBind   : ('a -> Option<'b>) -> Option<'a> -> Option<'b>
optionDefault: 'a -> Option<'a> -> 'a
isSome       : Option<'a> -> bool
isNone       : Option<'a> -> bool

-- Result 함수
resultMap     : ('a -> 'b) -> Result<'a, 'e> -> Result<'b, 'e>
resultBind    : ('a -> Result<'b, 'e>) -> Result<'a, 'e> -> Result<'b, 'e>
resultMapError: ('e -> 'f) -> Result<'a, 'e> -> Result<'a, 'f>
resultDefault : 'a -> Result<'a, 'e> -> 'a
isOk          : Result<'a, 'e> -> bool
isError       : Result<'a, 'e> -> bool
```

## 권장 가이드라인

| 상황 | 권장 방식 | 이유 |
|------|----------|------|
| 값이 있을 수도 없을 수도 있는 경우 | **Option** | 간단, 가벼움 |
| 실패 이유를 알아야 하는 경우 | **Result** | 에러 메시지 보존 |
| 여러 연산을 순서대로 합성 | **Result + `\|>`** | 파이프라인 합성 |
| 프로그래밍 오류/불변 조건 위반 | **Exception** | 즉시 중단, 디버깅 |
| 깊은 재귀 내부 에러 처리 | **Option/Result** | TCO 호환 |
| 최상위 레벨 에러 복구 | **Exception + try-with** | 프로그램 크래시 방지 |

**일반 원칙:**
- **기본값은 Option 또는 Result**를 사용하세요
- **Exception은 마지막 수단**으로만 사용하세요
- 타입이 실패 가능성을 말해주게 하세요 — 호출자가 놓치지 않도록
