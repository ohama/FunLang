# 가변 데이터 구조 (Mutable Data Structures)

파일 I/O와 시스템 함수를 통해 외부 세계와 상호작용하는 방법을 배웠습니다. 이번 장에서는 LangThree가 제공하는 두 가지 가변 데이터 구조인 **배열(Array)**과 **해시테이블(Hashtable)**을 살펴봅니다.

LangThree는 기본적으로 불변(immutable) 함수형 언어입니다. 리스트, 튜플, 레코드는 한 번 생성되면 값이 바뀌지 않습니다. 하지만 특정 상황에서는 가변 상태가 훨씬 자연스럽고 효율적입니다. 배열은 인덱스로 O(1) 접근이 필요할 때, 해시테이블은 동적인 키-값 저장소가 필요할 때 유용합니다. 이 두 타입은 명시적으로 변이(mutation)를 수행한다는 점에서 다른 LangThree 값들과 구별됩니다.

## 배열 (Array)

배열은 고정 크기의 가변 시퀀스입니다. 한 번 생성하면 길이는 바뀌지 않지만, 각 원소는 제자리에서(in-place) 바꿀 수 있습니다. 인덱스 기반 접근이 O(1)이므로 순차 접근이 잦거나 특정 위치를 반복적으로 수정해야 할 때 리스트보다 유리합니다.

### 생성과 기본 연산

`Array.create n v`는 길이 `n`의 배열을 만들고 모든 원소를 `v`로 초기화합니다. `Array.set arr i v`로 인덱스 `i`의 원소를 `v`로 바꾸고, `Array.get arr i`로 읽습니다. `Array.length arr`는 배열의 길이를 반환합니다:

```
$ cat arr_basic.l3
let arr = Array.create 5 0
let _ = Array.set arr 0 10
let _ = Array.set arr 1 20
let _ = Array.set arr 2 30
let v = Array.get arr 2
let n = Array.length arr
let result = (arr, v, n)

$ langthree arr_basic.l3
([|10; 20; 30; 0; 0|], 30, 5)
```

배열은 `[|1; 2; 3|]` 형식으로 출력됩니다. `Array.set`은 unit을 반환하므로 `let _ =`로 받습니다. 변이는 제자리에서 일어나므로 `arr`을 다시 바인딩할 필요가 없습니다.

### 리스트 변환

`Array.ofList`로 리스트를 배열로, `Array.toList`로 배열을 리스트로 변환할 수 있습니다. 두 함수를 조합하면 리스트로 데이터를 준비하고, 배열로 변환하여 인덱스 기반 수정을 한 뒤, 다시 리스트로 꺼낼 수 있습니다:

```
$ cat arr_conv.l3
let lst = [1; 2; 3; 4; 5]
let arr = Array.ofList lst
let _ = Array.set arr 2 99
let back = Array.toList arr
let result = back

$ langthree arr_conv.l3
[1; 2; 99; 4; 5]
```

`Array.set arr 2 99`가 세 번째 원소를 99로 바꿨고, `Array.toList`로 리스트로 꺼내면 그 변화가 반영되어 있습니다.

### 고차 함수

배열도 리스트처럼 고차 함수를 지원합니다. `Array.iter`, `Array.map`, `Array.fold`, `Array.init`이 있습니다.

**Array.iter** — 각 원소에 부수 효과(side effect) 함수를 적용합니다. 반환값은 unit입니다:

```
$ cat arr_iter.l3
let arr = Array.ofList [10; 20; 30]
let _ = Array.iter (fun x -> println (to_string x)) arr
let result = "완료"

$ langthree arr_iter.l3
10
20
30
"완료"
```

**Array.map** — 각 원소에 함수를 적용하여 새 배열을 반환합니다. 원본 배열은 변경되지 않습니다:

```
$ cat arr_map.l3
let arr = Array.ofList [1; 2; 3; 4; 5]
let squared = Array.map (fun x -> x * x) arr
let result = squared

$ langthree arr_map.l3
[|1; 4; 9; 16; 25|]
```

**Array.fold** — 배열을 하나의 값으로 축약합니다. 콜백은 반드시 커링된 형태 `fun acc -> fun x -> ...`로 작성합니다:

```
$ cat arr_fold.l3
let arr = Array.ofList [1; 2; 3; 4; 5]
let total = Array.fold (fun acc -> fun x -> acc + x) 0 arr
let result = total

$ langthree arr_fold.l3
15
```

`(fun acc -> fun x -> acc + x)`는 LangThree에서 두 인자를 받는 콜백을 작성하는 방식입니다. `fun acc x -> ...`는 파싱 에러이므로 반드시 커링 형태를 사용해야 합니다.

**Array.init** — 인덱스 `i`에 함수 `f i`를 적용한 값으로 배열을 초기화합니다:

```
$ cat arr_init.l3
let arr = Array.init 6 (fun i -> i * i)
let result = arr

$ langthree arr_init.l3
[|0; 1; 4; 9; 16; 25|]
```

`Array.init 6 f`는 길이 6인 배열을 만들고 각 인덱스 `i`에 `f i`를 채웁니다. `Array.create 6 v` 뒤에 반복적으로 `Array.set`을 호출하는 것과 같지만 훨씬 간결합니다.

### 주의사항

**범위 초과(Out-of-bounds):** `Array.get`이나 `Array.set`에서 인덱스가 범위를 벗어나면 예외가 발생합니다. `try-with`로 처리할 수 있습니다:

```
$ cat arr_oob.l3
let arr = Array.create 3 0
let result =
    try
        Array.get arr 10
    with
    | e -> -1

$ langthree arr_oob.l3
-1
```

**참조 동등성:** 배열은 참조 동등성을 사용합니다. 내용이 같은 두 배열이라도 `=` 연산자는 항상 `false`를 반환합니다. 동등성을 비교하려면 `Array.toList`로 변환한 뒤 리스트로 비교하거나, 직접 원소를 순회하는 방법을 사용하세요.

**모듈 한정 이름:** `open Array`를 사용하지 않습니다. 항상 `Array.create`, `Array.get` 등 모듈 한정 이름으로 호출합니다.

**람다 안에서의 모듈 접근:** 람다를 `Array.iter` 등의 고차 함수에 인라인으로 전달할 때, 람다 본체 안에서 `Hashtable.set` 같은 다른 모듈의 함수를 바로 호출하면 에러가 날 수 있습니다. 이 경우 모듈 함수를 미리 로컬 변수에 바인딩한 뒤 사용하세요.

## 해시테이블 (Hashtable)

해시테이블은 동적인 키-값 저장소입니다. 크기가 고정되지 않고, 어떤 LangThree 값이든 키나 값으로 쓸 수 있습니다. 빠른 키 조회(O(1) 평균), 동적 추가/삭제가 필요할 때 유용합니다.

### 생성과 기본 연산

`Hashtable.create ()`로 빈 해시테이블을 만들고, `Hashtable.set`, `Hashtable.get`, `Hashtable.containsKey`로 조작합니다:

```
$ cat ht_basic.l3
let ht = Hashtable.create ()
let _ = Hashtable.set ht "name" "Alice"
let _ = Hashtable.set ht "score" 42
let v = Hashtable.get ht "name"
let has = Hashtable.containsKey ht "score"
let result = (v, has)

$ langthree ht_basic.l3
("Alice", true)
```

`Hashtable.set ht key value`는 unit을 반환하는 변이 연산입니다. `Hashtable.get ht key`는 값을 반환하며, 키가 없으면 예외가 발생합니다. `Hashtable.containsKey ht key`는 키 존재 여부를 `bool`로 반환합니다.

**덮어쓰기:** 이미 있는 키에 `Hashtable.set`을 호출하면 값이 덮어씌워집니다:

```
$ cat ht_overwrite.l3
let ht = Hashtable.create ()
let _ = Hashtable.set ht "score" 10
let _ = Hashtable.set ht "score" 99
let result = Hashtable.get ht "score"

$ langthree ht_overwrite.l3
99
```

### 키 목록과 삭제

`Hashtable.keys ht`는 현재 테이블의 모든 키를 리스트로 반환합니다. `Hashtable.remove ht key`는 해당 키-값 쌍을 제거합니다:

```
$ cat ht_keys.l3
let ht = Hashtable.create ()
let _ = Hashtable.set ht "a" 1
let _ = Hashtable.set ht "b" 2
let _ = Hashtable.set ht "c" 3
let count_before = length (Hashtable.keys ht)
let _ = Hashtable.remove ht "b"
let count_after = length (Hashtable.keys ht)
let result = (count_before, count_after)

$ langthree ht_keys.l3
(3, 2)
```

`Hashtable.keys`가 반환하는 리스트에는 삽입 순서가 보장되지 않습니다. 특정 순서로 키를 처리해야 한다면 `Hashtable.keys`로 얻은 리스트를 `sort`로 정렬한 뒤 사용하세요.

### 주의사항

**키 순서 비결정적:** `Hashtable.keys`의 반환 순서는 실행마다 달라질 수 있습니다. 키 목록 자체를 결과로 출력하는 예제는 작성하지 않는 것이 좋습니다.

**없는 키 접근:** `Hashtable.get`으로 없는 키에 접근하면 예외가 발생합니다. 키 존재 여부를 먼저 `Hashtable.containsKey`로 확인하거나, `try-with`로 처리하세요.

**모듈 한정 이름:** `open Hashtable`을 사용하지 않습니다. 항상 `Hashtable.create`, `Hashtable.set` 등 모듈 한정 이름으로 호출합니다.

## 해시테이블 순회 (for-in)

해시테이블의 모든 키-값 쌍을 순회하려면 `for (k, v) in ht do` 구문을 사용합니다. 튜플 패턴으로 키와 값을 동시에 바인딩할 수 있습니다:

```
$ cat ht_forin.l3
let ht = Hashtable.create ()
let _ = Hashtable.set ht "name" "Alice"
let _ = for (k, v) in ht do
  let _ = println k
  println v

$ langthree ht_forin.l3
name
Alice
()
```

`Hashtable.keys`로 키 리스트를 얻어 순회하는 것보다 간결합니다. 순회 순서는 비결정적입니다.

## HashSet

HashSet은 중복 없는 값의 집합입니다. `HashSet.add`는 이미 있는 값을 추가하면 `false`를 반환하고, 새로운 값이면 `true`를 반환합니다:

```
$ cat hashset_basic.l3
let hs = HashSet.create ()
let _ = println (to_string (HashSet.add hs 1))
let _ = println (to_string (HashSet.add hs 2))
let _ = println (to_string (HashSet.add hs 1))
let _ = println (to_string (HashSet.contains hs 1))
let _ = println (to_string (HashSet.contains hs 9))
let _ = println (to_string (HashSet.count hs))

$ langthree hashset_basic.l3
true
true
false
true
false
2
()
```

`for x in hs do` 구문으로 순회할 수 있습니다:

```
$ cat hashset_forin.l3
let hs = HashSet.create ()
let _ = HashSet.add hs 42
let _ = for x in hs do println (to_string x)

$ langthree hashset_forin.l3
42
()
```

| 함수 | 설명 |
|------|------|
| `HashSet.create ()` | 빈 HashSet 생성 |
| `HashSet.add hs v` | 값 추가 (새로우면 `true`, 중복이면 `false`) |
| `HashSet.contains hs v` | 값 존재 여부 |
| `HashSet.count hs` | 원소 개수 |

중복 검사, 멤버십 테스트, 집합 연산이 필요할 때 유용합니다.

## Queue

Queue는 FIFO(선입선출) 자료구조입니다. `enqueue`로 넣고 `dequeue`로 꺼냅니다:

```
$ cat queue_basic.l3
let q = Queue.create ()
let _ = Queue.enqueue q 10
let _ = Queue.enqueue q 20
let _ = Queue.enqueue q 30
let _ = println (to_string (Queue.count q))
let v1 = Queue.dequeue q ()
let _ = println (to_string v1)
let v2 = Queue.dequeue q ()
let _ = println (to_string v2)
let _ = println (to_string (Queue.count q))

$ langthree queue_basic.l3
3
10
20
1
()
```

`Queue.dequeue q ()`는 가장 먼저 넣은 값을 꺼내 반환합니다. 빈 큐에서 `dequeue`하면 예외가 발생합니다.

`for x in q do` 구문으로 순회할 수 있습니다 (큐의 내용은 유지됩니다):

```
$ cat queue_forin.l3
let q = Queue.create ()
let _ = Queue.enqueue q 1
let _ = Queue.enqueue q 2
let _ = Queue.enqueue q 3
let _ = for x in q do println (to_string x)

$ langthree queue_forin.l3
1
2
3
()
```

| 함수 | 설명 |
|------|------|
| `Queue.create ()` | 빈 Queue 생성 |
| `Queue.enqueue q v` | 값을 큐의 뒤에 추가 |
| `Queue.dequeue q ()` | 앞에서 값을 꺼내 반환 (비어 있으면 예외) |
| `Queue.count q` | 큐의 원소 개수 |

BFS(너비 우선 탐색) 등 FIFO가 필요한 알고리즘에 적합합니다.

## MutableList

MutableList는 동적으로 크기가 변하는 가변 리스트입니다. 배열과 달리 크기가 고정되지 않고, 불변 리스트와 달리 제자리 수정이 가능합니다:

```
$ cat ml_basic.l3
let ml = MutableList.create ()
let _ = MutableList.add ml 10
let _ = MutableList.add ml 20
let _ = MutableList.add ml 30
let _ = println (to_string (MutableList.count ml))
let _ = println (to_string ml.[0])
let _ = println (to_string ml.[1])
let _ = println (to_string ml.[2])

$ langthree ml_basic.l3
3
10
20
30
()
```

`.[i]`로 읽고, `.[i] <- v`로 인덱스 위치의 값을 수정할 수 있습니다:

```
$ cat ml_index.l3
let ml = MutableList.create ()
let _ = MutableList.add ml 100
let _ = MutableList.add ml 200
let _ = println (to_string ml.[0])
let _ = ml.[0] <- 999
let _ = println (to_string ml.[0])

$ langthree ml_index.l3
100
999
()
```

`for x in ml do` 구문으로 순회할 수 있습니다:

```
$ cat ml_forin.l3
let ml = MutableList.create ()
let _ = MutableList.add ml 5
let _ = MutableList.add ml 10
let _ = MutableList.add ml 15
let _ = for x in ml do println (to_string x)

$ langthree ml_forin.l3
5
10
15
()
```

| 함수 | 설명 |
|------|------|
| `MutableList.create ()` | 빈 MutableList 생성 |
| `MutableList.add ml v` | 뒤에 값 추가 (크기 자동 증가) |
| `MutableList.count ml` | 원소 개수 |
| `ml.[i]` | 인덱스 읽기 |
| `ml.[i] <- v` | 인덱스 쓰기 |

배열보다 유연한 가변 컬렉션이 필요할 때 사용합니다. C#의 `List<T>`나 Python의 `list`에 해당합니다.

## 언제 사용할까?

대부분의 LangThree 코드는 불변 리스트와 재귀 함수만으로 충분합니다. 가변 데이터 구조는 특정 상황에서 진가를 발휘합니다:

| 상황 | 권장 |
|------|------|
| 순차 처리, 변환, 필터링 | 리스트 + `map`/`filter`/`fold` |
| 인덱스로 O(1) 접근, 고정 크기 수정 | Array |
| 동적 크기, 인덱스 접근 + 추가 | MutableList |
| 동적 키-값 저장, 빈도 계산, 캐시 | Hashtable |
| 중복 없는 값 집합, 멤버십 테스트 | HashSet |
| FIFO 순서 처리 (BFS 등) | Queue |

예를 들어 정렬이나 수열 생성은 리스트로 충분하지만, 행렬 연산처럼 특정 위치를 반복적으로 읽고 쓰는 경우는 배열이 적합합니다. 단어 빈도를 집계하거나 결과를 메모이제이션(memoize)할 때는 해시테이블이 자연스럽습니다.

## 함수 요약

### Array

| 함수 | 설명 |
|------|------|
| `Array.create n v` | 길이 `n`, 초기값 `v`인 배열 생성 |
| `Array.get arr i` | 인덱스 `i`의 원소 반환 (범위 초과 시 예외) |
| `Array.set arr i v` | 인덱스 `i`의 원소를 `v`로 변경 (unit 반환) |
| `Array.length arr` | 배열 길이 반환 |
| `Array.ofList lst` | 리스트를 배열로 변환 |
| `Array.toList arr` | 배열을 리스트로 변환 |
| `Array.iter f arr` | 각 원소에 `f`를 적용 (unit 반환) |
| `Array.map f arr` | 각 원소에 `f`를 적용한 새 배열 반환 |
| `Array.fold f init arr` | 배열을 하나의 값으로 축약 (커링 콜백 필요) |
| `Array.init n f` | `f i`로 인덱스 `i`를 초기화한 길이 `n` 배열 생성 |
| `Array.sort arr` | 배열을 제자리 정렬 |
| `Array.ofSeq coll` | 임의의 컬렉션을 배열로 변환 |

### Hashtable

| 함수 | 설명 |
|------|------|
| `Hashtable.create ()` | 빈 해시테이블 생성 |
| `Hashtable.set ht k v` | 키 `k`에 값 `v` 저장 (이미 있으면 덮어쓰기) |
| `Hashtable.get ht k` | 키 `k`의 값 반환 (없으면 예외) |
| `Hashtable.containsKey ht k` | 키 `k` 존재 여부 반환 |
| `Hashtable.keys ht` | 모든 키의 리스트 반환 (순서 비보장) |
| `Hashtable.remove ht k` | 키 `k`와 해당 값 제거 |

### HashSet

| 함수 | 설명 |
|------|------|
| `HashSet.create ()` | 빈 HashSet 생성 |
| `HashSet.add hs v` | 값 추가 (새로우면 `true`, 중복이면 `false`) |
| `HashSet.contains hs v` | 값 존재 여부 |
| `HashSet.count hs` | 원소 개수 |

### Queue

| 함수 | 설명 |
|------|------|
| `Queue.create ()` | 빈 Queue 생성 |
| `Queue.enqueue q v` | 값을 큐의 뒤에 추가 |
| `Queue.dequeue q ()` | 앞에서 값을 꺼내 반환 (비어 있으면 예외) |
| `Queue.count q` | 큐의 원소 개수 |

### MutableList

| 함수 | 설명 |
|------|------|
| `MutableList.create ()` | 빈 MutableList 생성 |
| `MutableList.add ml v` | 뒤에 값 추가 |
| `MutableList.count ml` | 원소 개수 |
| `ml.[i]` | 인덱스 읽기 |
| `ml.[i] <- v` | 인덱스 쓰기 |

### StringBuilder

| 함수 | 설명 |
|------|------|
| `StringBuilder.create ()` | 빈 StringBuilder 생성 |
| `StringBuilder.add sb s` | 문자열 또는 문자를 추가 |
| `StringBuilder.toString sb` | 축적된 내용을 문자열로 반환 |
