# 명령형 에르고노믹스 (Imperative Ergonomics)

LangThree는 명령형 스타일 코드를 더 자연스럽게 작성할 수 있는 네 가지 문법 기능을 제공합니다. 이 장에서는 표현식 시퀀싱, 루프, 인덱싱 문법, else 없는 if 표현식을 살펴봅니다.

## 표현식 시퀀싱 (Expression Sequencing)

함수형 언어에서 여러 부수 효과(side effect)를 순서대로 실행하려면 전통적으로 `let _ = e1 in e2` 패턴을 사용했습니다. LangThree에서는 `;` 연산자로 이를 간결하게 쓸 수 있습니다.

`e1; e2`는 `e1`을 평가한 뒤 그 결과를 버리고, `e2`를 평가하여 그 값을 반환합니다.

```
$ cat seq_basic.l3
let _ = println "hello"; println "world"

$ langthree seq_basic.l3
hello
world
()
```

여러 단계를 체이닝할 수도 있습니다. 아래는 가변 변수와 결합하여 카운터를 증가시키는 예입니다:

```
$ cat seq_chain.l3
let result = let mut x = 0 in x <- 1; x <- x + 1; x <- x + 1; x

$ langthree seq_chain.l3
3
```

함수 본체 안에서도 동일하게 사용할 수 있습니다:

```
$ cat seq_block.l3
let f () =
    println "a"; println "b"; println "c"
let result = f ()

$ langthree seq_block.l3
a
b
c
()
```

`;`은 오른쪽 결합(right-associative)이므로 `e1; e2; e3`는 `e1; (e2; e3)`로 파싱됩니다. 최종 반환값은 마지막 표현식의 값입니다.

## 루프 (Loops)

### while 루프

`while cond do body` 형태의 루프는 조건(`cond`)이 참인 동안 본체(`body`)를 반복 실행합니다. 루프 전체의 반환값은 unit입니다.

```
$ cat while_basic.l3
let mut i = 0
let _ = while i < 3 do i <- i + 1
let _ = println (to_string i)

$ langthree while_basic.l3
3
()
```

루프 본체에서 여러 문장을 실행하려면 `;`으로 연결합니다. 들여쓰기 블록 안에서 `;`을 사용하면 됩니다:

```
$ cat while_body.l3
let mut count = 0
let mut sum = 0
let _ =
    while count < 4 do
        sum <- sum + count; count <- count + 1
let _ = println (to_string sum)

$ langthree while_body.l3
6
()
```

루프가 끝날 때 `count`는 4, `sum`은 0+1+2+3 = 6이 됩니다.

### for 루프

`for i = start to end do body`는 `i`를 `start`부터 `end`까지 1씩 증가시키며 반복합니다. `downto`를 쓰면 1씩 감소합니다.

**오름차순 (to):**

```
$ cat for_asc.l3
let mut total = 0
let _ =
    for i = 0 to 3 do
        total <- total + i
let _ = println (to_string total)

$ langthree for_asc.l3
6
()
```

`i`가 0, 1, 2, 3 순서로 실행되며, `total`은 0+1+2+3 = 6이 됩니다.

**내림차순 (downto):**

```
$ cat for_desc.l3
let mut total = 0
let _ =
    for i = 3 downto 0 do
        total <- total + i
let _ = println (to_string total)

$ langthree for_desc.l3
6
()
```

`to`는 오름차순(start ≤ end), `downto`는 내림차순(start ≥ end)으로 반복합니다. start > end인 경우(`to`) 또는 start < end인 경우(`downto`) 루프 본체는 한 번도 실행되지 않고 unit을 반환합니다.

### 루프 변수의 불변성

for 루프 변수(`i`)는 불변입니다. 루프 본체 안에서 대입을 시도하면 E0320 에러가 발생합니다:

```
$ cat for_err.l3
let _ =
    for i = 0 to 9 do
        i <- 42

$ langthree for_err.l3
error[E0320]: Cannot assign to immutable variable 'i'. Use 'let mut' to declare mutable variables.
```

루프 카운터를 직접 수정할 필요가 있다면 별도의 `let mut` 변수를 사용하세요.

## 인덱싱 문법 (Indexing Syntax)

`arr.[i]` 형태의 인덱싱 문법으로 배열과 해시테이블에 더 직관적으로 접근할 수 있습니다.

### 배열 인덱싱

`arr.[i]`로 읽고, `arr.[i] <- v`로 씁니다. 기존의 `array_get`/`array_set` 함수보다 간결한 문법입니다.

**읽기 (IndexGet):**

```
$ cat arr_index_read.l3
let arr = array_create 3 0
let _ = array_set arr 0 10
let _ = array_set arr 1 20
let _ = array_set arr 2 30
let _ = println (to_string arr.[0])
let _ = println (to_string arr.[1])
let _ = println (to_string arr.[2])

$ langthree arr_index_read.l3
10
20
30
()
```

**쓰기 (IndexSet):**

```
$ cat arr_index_write.l3
let arr = array_create 3 0
let _ = arr.[0] <- 42
let _ = arr.[1] <- 99
let _ = println (to_string arr.[0])
let _ = println (to_string arr.[1])

$ langthree arr_index_write.l3
42
99
()
```

### 해시테이블 인덱싱

`ht.[key]`로 읽고, `ht.[key] <- v`로 씁니다.

**읽기:**

```
$ cat ht_index_read.l3
let ht = hashtable_create ()
let _ = hashtable_set ht "x" 100
let _ = hashtable_set ht "y" 200
let _ = println (to_string ht.["x"])
let _ = println (to_string ht.["y"])

$ langthree ht_index_read.l3
100
200
()
```

**쓰기:**

```
$ cat ht_index_write.l3
let ht = hashtable_create ()
let _ = ht.["name"] <- "Alice"
let _ = ht.["score"] <- 95
let _ = println ht.["name"]
let _ = println (to_string ht.["score"])

$ langthree ht_index_write.l3
Alice
95
()
```

### 체이닝

`.[`는 왼쪽 결합(left-associative)이므로 `matrix.[r].[c]`처럼 중첩 인덱싱이 가능합니다. 아래는 2D 배열(행렬) 예시입니다:

```
$ cat matrix.l3
let row0 = array_create 2 0
let row1 = array_create 2 0
let _ = row0.[0] <- 1
let _ = row0.[1] <- 2
let _ = row1.[0] <- 3
let _ = row1.[1] <- 4
let matrix = array_create 2 row0
let _ = matrix.[1] <- row1
let _ = println (to_string matrix.[0].[0])
let _ = println (to_string matrix.[1].[1])

$ langthree matrix.l3
1
4
()
```

`matrix.[0]`은 `row0` 배열을 반환하고, `matrix.[0].[0]`은 그 첫 번째 원소(1)를 반환합니다.

## else 없는 if 표현식

`if cond then expr` 형태로 `else` 없이 조건문을 쓸 수 있습니다. 이 경우 컴파일러가 암묵적으로 `else ()`를 추가합니다. 즉, `if cond then expr else ()`와 동일합니다.

부수 효과만 실행하고 결과를 버리는 경우에 유용합니다:

```
$ cat if_then.l3
let x = 5
let _ = if x > 0 then println "positive"

$ langthree if_then.l3
positive
()
```

가변 변수와 함께 사용하는 실용적인 예시입니다:

```
$ cat if_then_mut.l3
let mut x = 0
let _ = if true then x <- 42
let result = x

$ langthree if_then_mut.l3
42
```

암묵적 `else ()`가 붙으므로, then 브랜치가 unit이 아닌 값을 반환하면 타입 불일치 에러가 발생합니다:

```
$ cat if_then_err.l3
let _ = if true then 42

$ langthree if_then_err.l3
error[E0301]: Type mismatch: expected int but got unit
```

`if cond then expr`는 then 브랜치가 unit인 경우에만 사용하세요. 값을 반환하는 조건문이라면 반드시 `if cond then expr1 else expr2` 형태로 작성하세요.

## 루프와 시퀀싱 조합 — 실용 예제

네 가지 기능을 모두 조합한 실용적인 예시입니다. 배열에서 짝수의 개수를 세는 프로그램입니다:

```
$ cat imperative_example.l3
let arr = array_create 5 0
let _ = arr.[0] <- 2
let _ = arr.[1] <- 3
let _ = arr.[2] <- 4
let _ = arr.[3] <- 7
let _ = arr.[4] <- 8
let mut even_count = 0
let _ =
    for i = 0 to 4 do
        if arr.[i] % 2 = 0 then even_count <- even_count + 1
let result = even_count

$ langthree imperative_example.l3
3
```

- `arr.[i]`로 배열 원소를 읽고 (`인덱싱 문법`)
- `% 2 = 0`으로 짝수 여부를 확인하고 (`기본 연산`)
- `if ... then ...`으로 조건부 실행하고 (`else 없는 if`)
- `for i = 0 to 4 do`로 루프를 돌리며 (`for 루프`)
- 결과를 `even_count`에 누적합니다 (`가변 변수 + 시퀀싱`)

배열 원소 중 짝수(2, 4, 8)가 3개이므로 결과는 3입니다.

## 구문 요약

| 구문 | 설명 |
|------|------|
| `e1; e2` | 순서대로 평가, e2의 값 반환 |
| `e1; e2; e3` | 오른쪽 결합 체이닝 |
| `while cond do body` | 조건이 거짓이 될 때까지 반복 |
| `for i = s to e do body` | i를 s부터 e까지 1씩 증가 |
| `for i = s downto e do body` | i를 s부터 e까지 1씩 감소 |
| `arr.[i]` | 배열 원소 읽기 (IndexGet) |
| `arr.[i] <- v` | 배열 원소 쓰기 (IndexSet) |
| `ht.[key]` | 해시테이블 값 읽기 |
| `ht.[key] <- v` | 해시테이블 값 쓰기 |
| `if cond then expr` | else 없는 if (then 브랜치는 unit이어야 함) |
