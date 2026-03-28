# 22장: 실용 프로그래밍 (Practical Programming)

v6.0에서 LangThree는 일상적인 프로그래밍을 더 편리하게 만드는 세 가지 기능을 추가했습니다. 뉴라인 암묵적 시퀀싱, 컬렉션 for-in 루프, 그리고 Option/Result 유틸리티 함수입니다. 이 장에서는 각 기능을 코드 예제와 함께 살펴봅니다.

## 뉴라인 암묵적 시퀀싱 (Newline Implicit Sequencing)

v5.0에서 `;` 연산자로 표현식을 순서대로 실행할 수 있게 되었습니다. v6.0에서는 들여쓰기 블록 안에서 줄 바꿈만으로도 동일한 효과를 낼 수 있습니다. 같은 들여쓰기 수준의 줄은 자동으로 `;`으로 연결됩니다.

### 함수 본체에서의 뉴라인 시퀀싱

함수 본체에서 여러 줄에 걸쳐 표현식을 나열하면, 각 줄이 순서대로 실행됩니다. 마지막 표현식의 값이 함수의 반환값이 됩니다.

```
$ cat greet.l3
let greet name =
    println ("Hello, " ^^ name)
    println "Welcome to LangThree"
let _ = greet "Alice"

$ langthree greet.l3
Hello, Alice
Welcome to LangThree
()
```

`greet` 함수는 `println ("Hello, " ^^ name)`과 `println "Welcome to LangThree"` 두 표현식을 순서대로 실행합니다. `println`의 반환값은 unit이므로, 마지막 `println`의 unit이 함수의 반환값이 됩니다.

### if/else 본체에서의 뉴라인 시퀀싱

if/else의 then 브랜치와 else 브랜치도 들여쓰기 블록으로 여러 줄을 쓸 수 있습니다.

```
$ cat check.l3
let check x =
    if x > 0 then
        println "positive"
        x * 2
    else
        println "non-positive"
        0
let result = check 5

$ langthree check.l3
positive
10
```

`check 5`를 실행하면 then 브랜치에서 `println "positive"`를 실행한 뒤 `5 * 2 = 10`을 반환합니다. 최상위 `let result = check 5`는 반환값 10을 출력합니다.

들여쓰기 수준에 주의하세요. then 브랜치의 표현식은 `then` 키워드보다 더 깊게 들여써야 하고, else 브랜치의 표현식은 `else` 키워드보다 더 깊게 들여써야 합니다.

## 컬렉션 for-in 루프 (For-In Collection Loops)

v6.0에서는 리스트와 배열의 원소를 순서대로 순회하는 `for x in collection do` 문법이 추가되었습니다. 인덱스가 필요 없을 때 `for i = 0 to n do arr.[i]`보다 훨씬 간결하게 컬렉션을 처리할 수 있습니다.

### 리스트 순회

```
$ cat list_iter.l3
let nums = [1; 2; 3]
let _ =
    for n in nums do
        println (to_string n)

$ langthree list_iter.l3
1
2
3
()
```

`for n in nums do` 문법으로 리스트 `nums`의 각 원소를 `n`에 바인딩하며 순회합니다. 루프 변수 `n`은 불변이며, 루프 전체의 반환값은 unit입니다. 따라서 `let _ =`로 감싸서 최상위에서 실행합니다.

### 배열 순회

```
$ cat arr_iter.l3
let arr = array_create 3 0
let _ = arr.[0] <- 10
let _ = arr.[1] <- 20
let _ = arr.[2] <- 30
let _ =
    for x in arr do
        println (to_string x)

$ langthree arr_iter.l3
10
20
30
()
```

배열도 동일한 문법으로 순회합니다. 원소는 인덱스 순서(0, 1, 2, ...)로 처리됩니다.

### 루프 변수의 불변성

for-in 루프 변수는 for 범위 루프와 마찬가지로 불변입니다. 루프 본체 안에서 대입을 시도하면 E0320 에러가 발생합니다. 루프 안에서 집계가 필요하다면 외부에 `let mut` 변수를 선언하세요.

## Option/Result 유틸리티 (Option/Result Utilities)

v6.0 Prelude에 Option 타입과 Result 타입을 다루는 유틸리티 함수가 추가되었습니다. 패턴 매칭 없이 간결하게 Option/Result 값을 변환하고 조합할 수 있습니다.

### optionMap과 optionBind

`optionMap f opt`는 `opt`가 `Some x`이면 `Some (f x)`를, `None`이면 `None`을 반환합니다. `optionBind f opt`는 `opt`가 `Some x`이면 `f x`(Option 반환)를, `None`이면 `None`을 반환합니다.

```
$ cat option_map_bind.l3
let doubled = optionMap (fun x -> x * 2) (Some 21)
let chained = optionBind (fun x -> if x > 10 then Some (x + 1) else None) doubled
let _ = println (to_string doubled)
let _ = println (to_string chained)

$ langthree option_map_bind.l3
Some 42
Some 43
()
```

`optionMap (fun x -> x * 2) (Some 21)`은 `Some 42`를 반환합니다. `optionBind`는 `Some 42`에서 42를 꺼내 `if 42 > 10 then Some 43`을 반환합니다.

### optionDefaultValue와 optionFilter

`optionDefaultValue default opt`는 `opt`가 `None`일 때 `default`를 반환합니다. `optionFilter pred opt`는 술어(predicate)를 만족하지 않으면 `None`으로 바꿉니다.

```
$ cat option_default_filter.l3
let safe = optionDefaultValue 0 (Some 42)
let fallback = optionDefaultValue 0 None
let filtered = optionFilter (fun x -> x > 5) (Some 10)
let rejected = optionFilter (fun x -> x > 5) (Some 3)
let _ = println (to_string safe)
let _ = println (to_string fallback)
let _ = println (to_string filtered)
let _ = println (to_string rejected)

$ langthree option_default_filter.l3
42
0
Some 10
None
()
```

`optionDefaultValue 0 (Some 42)`는 `Some`이므로 안의 값 42를 반환합니다. `optionDefaultValue 0 None`은 기본값 0을 반환합니다. `optionFilter (fun x -> x > 5) (Some 3)`은 술어를 만족하지 않으므로 `None`을 반환합니다.

### resultMap과 resultToOption

`resultMap f r`은 `Ok x`이면 `Ok (f x)`를, `Error e`이면 `Error e`를 반환합니다. `resultToOption r`은 `Ok x`이면 `Some x`를, `Error _`이면 `None`을 반환합니다.

```
$ cat result_map_opt.l3
let r = Ok 42
let mapped = resultMap (fun x -> x * 2) r
let asOption = resultToOption mapped
let errCase = resultToOption (Error "oops")
let _ = println (to_string mapped)
let _ = println (to_string asOption)
let _ = println (to_string errCase)

$ langthree result_map_opt.l3
Ok 84
Some 84
None
()
```

`resultMap`으로 Ok 값을 변환하고, `resultToOption`으로 Result를 Option으로 변환합니다. Error 케이스는 `None`으로 변환되어 에러 메시지가 사라집니다.

### Option/Result 유틸리티 함수 요약

| 함수 | 시그니처 | 설명 |
|------|----------|------|
| `optionMap` | `(a -> b) -> Option a -> Option b` | Some이면 함수 적용, None 전파 |
| `optionBind` | `(a -> Option b) -> Option a -> Option b` | Option 반환 함수로 체이닝 |
| `optionFilter` | `(a -> bool) -> Option a -> Option a` | 술어 불만족 시 None으로 변환 |
| `optionDefaultValue` | `a -> Option a -> a` | None일 때 기본값 반환 |
| `optionIsSome` | `Option a -> bool` | Some이면 true |
| `optionIsNone` | `Option a -> bool` | None이면 true |
| `optionIter` | `(a -> unit) -> Option a -> unit` | Some이면 부수 효과 실행 |
| `resultMap` | `(a -> b) -> Result a e -> Result b e` | Ok이면 함수 적용, Error 전파 |
| `resultToOption` | `Result a e -> Option a` | Ok → Some, Error → None |
| `resultDefaultValue` | `a -> Result a e -> a` | Error일 때 기본값 반환 |
| `resultIter` | `(a -> unit) -> Result a e -> unit` | Ok이면 부수 효과 실행 |

## 종합 예제 (Composition Example)

세 가지 기능을 조합한 예제입니다. Option 값의 리스트를 for-in으로 순회하면서 각 값에 함수를 적용합니다.

```
$ cat process.l3
let process items =
    let _ =
        for item in items do
            let result = optionMap (fun x -> x * 2) item
            println (to_string result)
    ()
let _ = process [Some 1; None; Some 3]

$ langthree process.l3
Some 2
None
Some 6
()
```

`process` 함수는 Option 값의 리스트를 받아 각 원소에 `optionMap (fun x -> x * 2)`를 적용합니다. `Some 1`은 `Some 2`가 되고, `None`은 `None`으로 유지되며, `Some 3`은 `Some 6`이 됩니다.

함수 본체에서 뉴라인 시퀀싱(`for` 루프와 `let result = ...` → `println ...`)과 for-in 루프, optionMap을 자연스럽게 조합합니다.

## 구문 및 함수 요약

| 기능 | 예시 | 설명 |
|------|------|------|
| 뉴라인 시퀀싱 | 들여쓰기 블록 내 줄 바꿈 | 암묵적 `;` 삽입 |
| `for x in list do` | `for n in nums do println (to_string n)` | 리스트 원소 순회 |
| `for x in arr do` | `for x in arr do println (to_string x)` | 배열 원소 순회 |
| `optionMap f opt` | `optionMap (fun x -> x * 2) (Some 21)` | Option 변환 |
| `optionBind f opt` | `optionBind f (Some x)` | Option 체이닝 |
| `optionDefaultValue d opt` | `optionDefaultValue 0 None` | 기본값 추출 |
| `optionFilter pred opt` | `optionFilter (fun x -> x > 0) opt` | 조건부 필터 |
| `resultMap f r` | `resultMap (fun x -> x * 2) (Ok 42)` | Result 변환 |
| `resultToOption r` | `resultToOption (Ok 42)` | Result → Option 변환 |
