# 6장: 레코드 (Records)

## 레코드 타입 선언

이름 있는 필드를 가진 레코드를 정의합니다:

```
$ cat point.l3
type Point = { px: int; py: int }
let p = { px = 3; py = 4 }
let result = p.px + p.py

$ langthree point.l3
7
```

**중요:** 필드 이름은 모든 레코드 타입에 걸쳐 전역적으로 고유해야 합니다.
두 레코드 타입이 같은 필드 이름을 공유할 수 없습니다.

## 필드 접근

점 표기법(dot notation)으로 필드에 접근합니다:

```
$ cat access.l3
type Person = { name: string; age: int }
let alice = { name = "Alice"; age = 30 }
let result = alice.name + " is " + to_string alice.age

$ langthree access.l3
"Alice is 30"
```

## 연쇄 필드 접근

중첩된 레코드에 대해 점 표기법을 연쇄할 수 있습니다:

```
$ cat nested.l3
type Inner = { val: int }
type Outer = { inner: Inner }
let o = { inner = { val = 42 } }
let result = o.inner.val

$ langthree nested.l3
42
```

## 복사 후 갱신

`{ record with field = value }` 구문으로 수정된 복사본을 생성합니다:

```
$ cat update.l3
type Point = { px: int; py: int }
let p = { px = 1; py = 2 }
let moved = { p with px = 10 }
let result = moved

$ langthree update.l3
{ px = 10; py = 2 }
```

여러 필드를 한 번에 갱신할 수 있습니다:

```
$ cat multi_update.l3
type Vec3 = { vx: int; vy: int; vz: int }
let v = { vx = 1; vy = 2; vz = 3 }
let result = { v with vx = 10; vy = 20 }

$ langthree multi_update.l3
{ vx = 10; vy = 20; vz = 3 }
```

원본 레코드는 변경되지 않습니다 -- 복사 후 갱신은 새로운 값을 생성합니다.

## 레코드 패턴 매칭

`match` 표현식에서 레코드를 구조 분해할 수 있습니다:

```
$ cat record_match.l3
type Point = { px: int; py: int }
let p = { px = 3; py = 4 }
let result =
    match p with
    | { px = a; py = b } -> a + b

$ langthree record_match.l3
7
```

## 가변 필드

`mutable`로 필드를 선언하면 제자리 갱신(in-place update)이 가능합니다:

```
$ cat counter.l3
type Counter = { mutable count: int }
let c = { count = 0 }
let _ = c.count <- c.count + 1
let _ = c.count <- c.count + 1
let _ = c.count <- c.count + 1
let result = c.count

$ langthree counter.l3
3
```

`<-` 연산자는 필드를 제자리에서 갱신하고 unit `()`을 반환합니다.
모듈 수준에서 변이(mutation)를 순차적으로 실행하려면 `let _ =`을 사용하세요.

## 매개변수화된 레코드

레코드는 타입 매개변수를 가질 수 있습니다 (타입 이름 뒤에 위치):

```
$ cat pair.l3
type Pair 'a = { fst: 'a; snd: 'a }
let p = { fst = 1; snd = 2 }
let result = p.fst + p.snd

$ langthree pair.l3
3
```

## 구조적 동치

레코드는 구조적 동치 비교를 지원합니다:

```
$ cat equality.l3
type Point = { px: int; py: int }
let p1 = { px = 1; py = 2 }
let p2 = { px = 1; py = 2 }
let p3 = { px = 1; py = 3 }
let r1 = if p1 = p2 then "equal" else "not equal"
let result = if p1 = p3 then "equal" else "not equal"

$ langthree equality.l3
"not equal"
```

## 실용 예제: 가변 상태

입금과 잔액 확인이 가능한 은행 계좌:

```
$ cat account.l3
type Account = { mutable balance: int }
let acct = { balance = 100 }
let _ = acct.balance <- acct.balance + 50
let _ = acct.balance <- acct.balance - 30
let result = acct.balance

$ langthree account.l3
120
```

## 제한 사항

- **필드 단축 표기 불가:** `{ px = px; py = py }`의 축약형으로 `{ px; py }`를 사용할 수 없습니다.
- **전역적으로 고유한 필드:** 두 레코드 타입이 같은 필드 이름을 공유할 수 없습니다.
  컴파일러가 필드 이름으로 레코드 타입을 결정하므로, 고유성이 필요합니다.
