# 6장: 레코드 (Records)

앞 장에서 ADT를 배웠습니다. ADT가 "이 값은 A이거나, B이거나, C다"라는 선택(합 타입)을 표현한다면, 레코드는 "이 값은 A이고, B이고, C다"라는 묶음(곱 타입)을 표현합니다. 함께 속하는 데이터를 하나의 단위로 묶고, 각 부분에 이름을 붙이는 것이 레코드의 역할입니다.

Python의 `dataclass`, Rust의 `struct`, F#의 record와 본질적으로 같은 개념입니다. 다만 LangThree의 레코드는 기본적으로 불변(immutable)이라는 점이 다릅니다 — 명시적으로 `mutable`을 선언하지 않으면 값을 변경할 수 없습니다.

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

타입 선언에서는 `:`로 필드 이름과 타입을 구분하고, 생성 시에는 `=`로 필드 이름과 값을 연결합니다. 세미콜론 `;`은 필드 구분자입니다. 처음에 `:` vs `=`가 헷갈릴 수 있으니 주의하세요 — 선언은 콜론, 생성은 등호입니다.

**중요:** 필드 이름은 모든 레코드 타입에 걸쳐 전역적으로 고유해야 합니다.
두 레코드 타입이 같은 필드 이름을 공유할 수 없습니다. 이는 컴파일러가 필드 이름만으로 어떤 레코드 타입인지 결정하기 때문입니다. 예를 들어 `Point`와 `Vector` 두 타입이 모두 `x` 필드를 가질 수는 없습니다. 이 제약을 피하기 위해 `px`, `py`처럼 타입 접두사를 붙이는 관례를 사용하는 경우가 많습니다.

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

`alice.name`처럼 점 표기법을 사용하는 것은 대부분의 언어에서 익숙한 방식입니다. 레코드를 변수에 담아두고 필요한 필드를 꺼내 쓰는 것이 일반적인 패턴입니다.

## 연쇄 필드 접근

레코드 안에 레코드가 중첩되어 있을 때, 점 표기법을 이어 붙여 깊은 곳의 값에 접근할 수 있습니다:

```
$ cat nested.l3
type Inner = { val: int }
type Outer = { inner: Inner }
let o = { inner = { val = 42 } }
let result = o.inner.val

$ langthree nested.l3
42
```

`o.inner.val`처럼 연쇄 접근은 자연스럽게 읽힙니다. 복잡한 설정 값이나 계층적인 데이터 구조를 표현할 때 중첩 레코드가 유용합니다. 다만 중첩이 너무 깊어지면 업데이트가 번거로워질 수 있습니다 — 이 부분은 다음 섹션의 복사 후 갱신에서 더 자세히 다룹니다.

## 복사 후 갱신

함수형 프로그래밍에서는 기존 값을 변경하는 대신 수정된 새 값을 만드는 방식을 선호합니다. `{ record with field = value }` 구문으로 수정된 복사본을 생성합니다:

```
$ cat update.l3
type Point = { px: int; py: int }
let p = { px = 1; py = 2 }
let moved = { p with px = 10 }
let result = moved

$ langthree update.l3
{ px = 10; py = 2 }
```

`{ p with px = 10 }`은 "`p`의 모든 필드를 그대로 복사하되, `px`만 10으로 바꾼 새 레코드를 만들어라"는 의미입니다. `py = 2`는 자동으로 복사됩니다. 필드가 많은 레코드에서 하나만 바꾸고 싶을 때 특히 편리합니다.

여러 필드를 한 번에 갱신할 수 있습니다:

```
$ cat multi_update.l3
type Vec3 = { vx: int; vy: int; vz: int }
let v = { vx = 1; vy = 2; vz = 3 }
let result = { v with vx = 10; vy = 20 }

$ langthree multi_update.l3
{ vx = 10; vy = 20; vz = 3 }
```

`with` 뒤에 세미콜론으로 구분하여 여러 필드를 동시에 지정할 수 있습니다. `vz = 3`은 원본 `v`에서 그대로 가져옵니다.

원본 레코드는 변경되지 않습니다 -- 복사 후 갱신은 새로운 값을 생성합니다. 이 점이 중요합니다. `moved`를 만든 이후에도 원본 `p`는 `{ px = 1; py = 2 }`로 그대로 남아 있습니다. 이런 불변성 덕분에 값을 공유하거나 히스토리를 추적하는 코드에서 예상치 못한 변경으로 인한 버그가 발생하지 않습니다.

## 레코드 패턴 매칭

필드 접근 외에도, `match` 표현식에서 레코드를 구조 분해할 수 있습니다:

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

패턴에서 `{ px = a; py = b }`는 "레코드의 `px` 필드를 `a`에 바인딩하고, `py` 필드를 `b`에 바인딩하라"는 의미입니다. 이후 `a`와 `b`를 일반 변수처럼 사용할 수 있습니다. ADT와 레코드를 함께 사용할 때 이 패턴이 자연스럽게 쓰입니다 — 예를 들어 `Some { px = x; py = y }`처럼 중첩 구조를 한 번에 분해할 수 있습니다.

## 가변 필드

지금까지의 레코드는 모두 불변이었습니다. 하지만 상태를 추적해야 하는 경우도 있습니다. `mutable`로 필드를 선언하면 제자리 갱신(in-place update)이 가능합니다:

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

가변 필드는 강력하지만 신중하게 사용해야 합니다. 값이 언제 바뀔지 예측하기 어려워지면 버그를 찾기가 힘들어집니다. 일반적인 원칙은 진짜 상태(예: 캐시, 카운터, 외부 리소스)가 아니면 불변 레코드와 복사 후 갱신을 선호하는 것입니다.

## 매개변수화된 레코드

ADT처럼 레코드도 타입 매개변수를 가질 수 있습니다. 이를 통해 같은 구조를 여러 타입에 재사용할 수 있습니다:

```
$ cat pair.l3
type Pair 'a = { fst: 'a; snd: 'a }
let p = { fst = 1; snd = 2 }
let result = p.fst + p.snd

$ langthree pair.l3
3
```

`Pair 'a`는 같은 타입의 두 값을 묶는 레코드입니다. `{ fst = 1; snd = 2 }`를 만들면 컴파일러가 `'a`를 `int`로 추론합니다. 두 필드의 타입이 다른 쌍을 만들고 싶다면 `type Pair 'a 'b = { fst: 'a; snd: 'b }`처럼 타입 매개변수를 두 개로 늘리면 됩니다.

## 구조적 동치

레코드의 동등 비교는 내용 기반으로 이루어집니다. 같은 타입, 같은 필드 값이면 같은 레코드입니다:

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

`p1`과 `p2`는 별개의 값이지만 모든 필드가 같으므로 동등합니다. `p1`과 `p3`는 `py`가 다르므로 동등하지 않습니다. 이 구조적 동치(structural equality)는 참조 동등성(reference equality)을 사용하는 Java의 `==`와 다릅니다 — Java에서는 두 `new Point(1, 2)`가 서로 다르다고 판단합니다. LangThree에서는 내용이 같으면 같습니다.

## 실용 예제: 가변 상태

가변 필드의 실용적인 사용 예입니다. 입금과 잔액 확인이 가능한 은행 계좌:

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

초기 잔액 100에서 50을 더하고 30을 빼면 120이 됩니다. 이처럼 시간이 지남에 따라 상태가 변해야 하는 경우에 가변 필드가 적합합니다. 다만 실제 금융 시스템이라면 불변 레코드와 트랜잭션 히스토리를 함께 유지하는 방식이 더 안전하겠지만, 여기서는 가변 필드의 동작 방식을 보여주는 데 집중합니다.

## 제한 사항

LangThree 레코드에는 현재 두 가지 제약이 있습니다. 이를 미리 알아두면 당황하지 않을 수 있습니다:

- **필드 단축 표기 불가:** `{ px = px; py = py }`의 축약형으로 `{ px; py }`를 사용할 수 없습니다.
  JavaScript의 `{ px, py }` 단축 표기에 익숙하다면 아쉬울 수 있지만, 현재는 항상 명시적으로 `{ px = px; py = py }`라고 써야 합니다.
- **전역적으로 고유한 필드:** 두 레코드 타입이 같은 필드 이름을 공유할 수 없습니다.
  컴파일러가 필드 이름으로 레코드 타입을 결정하므로, 고유성이 필요합니다. 여러 레코드 타입을 정의할 때 필드 이름 앞에 타입 약어를 붙이는 관례(`px`, `py` 대신 단순히 `x`, `y`를 쓰면 충돌 가능)를 따르면 이 제약을 자연스럽게 회피할 수 있습니다.
