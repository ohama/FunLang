# 가변 변수 (Mutable Variables)

이전 장에서 배열(Array)과 해시테이블(Hashtable)이라는 가변 데이터 구조를 살펴봤습니다. 이들은 가변 *컨테이너*입니다 — 구조 자체가 제자리에서(in-place) 변합니다. 이번 장에서는 `let mut`으로 선언하는 **가변 변수(mutable variable)**를 소개합니다. 가변 변수는 변수 바인딩 자체를 바꿀 수 있게 해줍니다.

## 기본 사용법

`let mut`으로 가변 변수를 선언하고, `<-` 연산자로 새 값을 대입합니다:

```
$ cat mut_basic.l3
let mut x = 5
let _ = x <- 10
let result = x

$ langthree mut_basic.l3
10
```

`let mut x = 5`는 `x`를 가변으로 선언합니다. `x <- 10`은 `x`의 값을 10으로 변경합니다. 대입 연산(`<-`)은 unit을 반환하므로 `let _ =`로 받습니다:

```
$ cat mut_unit.l3
let mut x = 5
let r = x <- 10
let result = r

$ langthree mut_unit.l3
()
```

`x <- 10`의 반환값이 `()`(unit)임을 확인할 수 있습니다. 이는 대입이 부수 효과(side effect)임을 명시합니다.

## 모듈 수준 가변 변수

파일의 최상위(top-level)에서도 `let mut`을 사용할 수 있습니다. 모듈 수준 가변 변수는 파일 전체에서 접근하고 변경할 수 있습니다:

```
$ cat mut_toplevel.l3
let mut counter = 0
let _ = counter <- counter + 1
let _ = counter <- counter + 1
let _ = counter <- counter + 1
let result = counter

$ langthree mut_toplevel.l3
3
```

매번 `counter <- counter + 1`로 현재 값에 1을 더한 결과를 다시 대입합니다.

## 다양한 타입

가변 변수는 정수 외에도 다양한 타입에 사용할 수 있습니다.

**문자열:**

```
$ cat mut_string.l3
let mut greeting = "hello"
let _ = greeting <- "world"
let result = greeting

$ langthree mut_string.l3
"world"
```

**불리언(bool):**

```
$ cat mut_bool.l3
let mut flag = true
let _ = flag <- false
let result = flag

$ langthree mut_bool.l3
false
```

단, 한 번 선언된 가변 변수의 타입은 바꿀 수 없습니다. `let mut x = 5` 이후 `x <- "hello"`를 시도하면 타입 에러가 발생합니다 (에러 케이스 섹션 참조).

## 중첩 가변 변수

여러 개의 가변 변수를 동시에 사용할 수 있습니다:

```
$ cat mut_multi.l3
let mut x = 0
let mut y = 0
let _ = x <- 10
let _ = y <- 20
let result = (x, y)

$ langthree mut_multi.l3
(10, 20)
```

각 가변 변수는 독립적으로 관리됩니다. `x`를 바꿔도 `y`에는 영향이 없습니다.

## 함수와 가변 변수

함수 본체(body) 안에서 `let mut`을 사용하면, 해당 가변 변수는 함수의 지역 변수가 됩니다:

```
$ cat mut_func.l3
let counter () =
    let mut n = 0
    let _ = n <- n + 1
    let _ = n <- n + 1
    let _ = n <- n + 1
    n
let result = counter ()

$ langthree mut_func.l3
3
```

`counter`를 호출할 때마다 `n`은 0에서 시작하여 3번 증가한 뒤 최종값 3을 반환합니다.

## 클로저 캡처

함수(클로저)는 바깥 스코프의 가변 변수를 읽고 쓸 수 있습니다:

```
$ cat mut_closure.l3
let mut count = 0
let inc () = count <- count + 1
let _ = inc ()
let _ = inc ()
let _ = inc ()
let result = count

$ langthree mut_closure.l3
3
```

`inc` 함수는 바깥의 `count`를 캡처하여 호출될 때마다 값을 1씩 증가시킵니다. 가변 변수에 대한 클로저 캡처는 참조(reference)로 이루어지므로 함수 안에서의 변경이 바깥에도 반영됩니다.

인자를 받는 함수도 같은 방식으로 동작합니다:

```
$ cat mut_closure2.l3
let mut total = 0
let add n = total <- total + n
let _ = add 10
let _ = add 20
let _ = add 30
let result = total

$ langthree mut_closure2.l3
60
```

재귀 함수와 가변 변수를 결합할 수도 있습니다:

```
$ cat mut_recursive.l3
let mut total = 0
let rec sum_list lst =
    match lst with
    | [] -> ()
    | x :: rest ->
        let _ = total <- total + x
        sum_list rest
let _ = sum_list [1; 2; 3; 4; 5]
let result = total

$ langthree mut_recursive.l3
15
```

## 조건문과 패턴 매칭

`if-then-else`의 결과를 가변 변수에 대입할 수 있습니다:

```
$ cat mut_cond.l3
let mut x = 0
let _ = x <- if true then 42 else 0
let result = x

$ langthree mut_cond.l3
42
```

`match` 표현식도 동일하게 사용할 수 있습니다:

```
$ cat mut_match.l3
let mut label = "unknown"
let code = 1
let _ = label <-
    match code with
    | 0 -> "zero"
    | 1 -> "one"
    | _ -> "other"
let result = label

$ langthree mut_match.l3
"one"
```

`match`를 `<-` 오른쪽에 쓸 때는 다음 줄로 내려서 들여쓰기합니다.

## 에러 케이스

### E0320: 불변 변수에 대입

`let`(mut 없이)으로 선언한 변수에 `<-`를 쓰면 컴파일 에러가 발생합니다:

```
$ cat mut_err_immutable.l3
let x = 5
let _ = x <- 10

$ langthree mut_err_immutable.l3
error[E0320]: Cannot assign to immutable variable 'x'. Use 'let mut' to declare mutable variables.
```

힌트 메시지가 `let mut`을 사용하라고 안내합니다. 변수를 가변으로 만들려면 선언 시 `let mut`을 사용해야 합니다.

### E0301: 타입 불일치

가변 변수에 다른 타입의 값을 대입하면 타입 에러가 발생합니다:

```
$ cat mut_err_type.l3
let mut x = 5
let _ = x <- "hello"

$ langthree mut_err_type.l3
error[E0301]: Type mismatch: expected int but got string
```

`x`는 `int`로 선언되었으므로 `string`을 대입할 수 없습니다. 가변 변수의 타입은 선언 시점에 고정됩니다.

## 불변 vs 가변

LangThree는 기본적으로 불변을 선호합니다. 가변 변수는 필요할 때만 사용하세요.

| 항목 | 불변 (`let`) | 가변 (`let mut`) |
|------|-------------|-----------------|
| 선언 | `let x = 5` | `let mut x = 5` |
| 재대입 | 불가 (에러) | `x <- 10` |
| 타입 변경 | 해당 없음 | 불가 (같은 타입만) |
| 제네릭 | 가능 | 불가 (단형성) |
| 권장 상황 | 대부분의 코드 | 누적, 카운터, 상태 관리 |

**언제 가변 변수를 사용할까:**

- 루프에서 누적값을 쌓을 때
- 호출 횟수를 세는 카운터가 필요할 때
- 여러 단계에 걸쳐 상태를 점진적으로 변경할 때

**불변으로 충분한 경우:**

- 값을 한 번 계산하고 이름을 붙이는 경우
- 재귀와 `fold`로 누적이 가능한 경우
- 패턴 매칭으로 분기 처리하는 경우

대부분의 LangThree 코드는 `let`만으로 충분합니다. `let mut`은 가변 상태가 코드를 더 명확하고 간결하게 만드는 경우에 사용하세요.

## 구문 요약

| 구문 | 설명 |
|------|------|
| `let mut x = expr` | 가변 변수 선언 |
| `x <- expr` | 가변 변수에 새 값 대입 (unit 반환) |
| `let _ = x <- expr` | 대입의 unit 반환값을 버림 |
| `let mut x = expr in body` | 지역 가변 변수 (표현식 모드) |
