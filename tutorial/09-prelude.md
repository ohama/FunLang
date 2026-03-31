# 9장: Prelude 표준 라이브러리 (Prelude and Standard Library)

어떤 언어를 쓰든, 매번 처음부터 리스트 처리 함수를 직접 만들거나 "없음"을 표현하는 타입을 직접 정의하는 건 번거로운 일입니다. LangThree는 이런 공통적인 필요를 Prelude라는 표준 라이브러리로 해결합니다.

LangThree는 시작 시 Prelude라는 표준 라이브러리를 로드합니다. Prelude 파일은 명시적인 import 없이 모든 사용자 코드에서 사용 가능한 타입, 생성자, 함수를 제공합니다. Python의 builtins이나 Haskell의 Prelude와 비슷한 개념이지만, LangThree에서는 Prelude 자체도 `.fun` 파일로 작성되어 있어 언어의 일반 코드와 다를 바가 없습니다. 원하면 직접 읽어보고 확장할 수도 있습니다.

## Prelude의 동작 방식

Prelude는 LangThree 바이너리와 같은 위치의 `Prelude/` 디렉토리에 있는 `.fun` 파일로 구성됩니다. 시작 시 이 파일들은 의존성 분석을 거쳐 올바른 순서로 로드된 후, 각각 모듈로 파싱되고 타입 검사를 거쳐 평가됩니다. 이 파일들이 정의하는 타입, 생성자, 함수는 이후 모든 코드에서 사용 가능합니다.

로드 순서는 자동으로 결정됩니다. 각 파일이 선언하는 타입 생성자와 다른 파일에서 참조하는 생성자를 분석하여 의존성 그래프를 구축하고, 토폴로지 정렬로 순서를 정합니다. 예를 들어 `List.fun`이 `Some`과 `None`을 사용하면, 이를 선언한 `Option.fun`이 자동으로 먼저 로드됩니다. 의존성이 없는 파일 간에는 알파벳순으로 정렬됩니다.

현재 Prelude에는 다음 파일들이 포함되어 있습니다:

- `Prelude/Option.fun` -- Option 타입과 함수 (`optionMap`, `optionBind`, `optionDefault`, `isSome`, `isNone` 등)
- `Prelude/Array.fun` -- 배열 모듈 (`Array.create`, `Array.get`, `Array.set`, `Array.sort`, `Array.ofSeq` 등)
- `Prelude/Char.fun` -- 문자 모듈 (`Char.IsDigit`, `Char.IsLetter`, `Char.ToUpper`, `Char.ToLower` 등)
- `Prelude/Core.fun` -- 핵심 고차 함수 (`id`, `const`, `compose`)
- `Prelude/HashSet.fun` -- HashSet 모듈 (`HashSet.create`, `HashSet.add`, `HashSet.contains`, `HashSet.count`)
- `Prelude/Hashtable.fun` -- Hashtable 모듈
- `Prelude/List.fun` -- 리스트 처리 함수 (`map`, `filter`, `fold`, `sort`, `tryFind`, `choose` 등)
- `Prelude/MutableList.fun` -- MutableList 모듈
- `Prelude/Queue.fun` -- Queue 모듈
- `Prelude/Result.fun` -- Result 타입과 함수 (`resultMap`, `resultBind`, `resultDefault` 등)
- `Prelude/String.fun` -- String 모듈 (`String.endsWith`, `String.startsWith`, `String.trim` 등)
- `Prelude/StringBuilder.fun` -- StringBuilder 모듈
- `Prelude/Typeclass.fun` -- 타입 클래스 (`Show`, `Eq`)와 기본 타입 인스턴스 (`int`, `bool`, `string`, `char`)

**Prelude/Option.fun:**
```
type Option 'a =
    | None
    | Some of 'a
```

이 파일은 `Option` 타입과 `None`, `Some` 생성자를 정의하며, `open` 지시어 없이 어디서든 사용 가능합니다. 단 한 줄이지만, 이것 하나로 "값이 있을 수도 없을 수도 있음"이라는 개념을 타입 시스템에서 안전하게 표현할 수 있게 됩니다.

## Option 타입 사용하기

함수형 프로그래밍에서 `Option` (또는 `Maybe`, `Optional`)은 아마 가장 중요한 타입일 것입니다. `null`을 사용하는 대신, 값의 부재를 타입 수준에서 명시함으로써 null 참조 오류를 컴파일 타임에 방지할 수 있습니다. 토니 호아르가 `null`을 "10억 달러짜리 실수"라고 부른 것을 기억하나요? `Option`은 그 실수를 타입 시스템으로 고치는 방법입니다.

### Option 값 생성

`Some`과 `None` 생성자는 REPL과 파일 모드 모두에서 동작합니다:

```
funlang> Some 42
Some 42

funlang> Some "hello"
Some "hello"

funlang> None
None
```

`Some`은 어떤 타입이든 감쌀 수 있습니다. `None`은 값이 없다는 의미입니다. 타입 파라미터 `'a`가 있어서 `Option<int>`, `Option<string>`, `Option<Option<int>>` 같은 타입이 모두 가능합니다.

추론된 타입을 확인합니다:

```
$ cat check_option.l3
let x = Some 42

$ langthree --emit-type check_option.l3
x : Option<int>
```

컴파일러가 `42`가 `int`임을 보고 `Option<int>`로 타입을 추론합니다. 별도로 타입을 적어줄 필요가 없습니다.

### Option에 대한 패턴 매칭

`Option`의 진짜 가치는 패턴 매칭과 함께 쓸 때 드러납니다. 값이 있는 경우와 없는 경우를 반드시 둘 다 처리해야 하므로, 실수로 None을 무시하는 일이 없습니다:

```
$ cat option_match.l3
let x = Some 42
let result =
    match x with
    | Some v -> v
    | None -> 0

$ langthree option_match.l3
42
```

Java나 Python에서 `null` 체크를 빠뜨리면 런타임에야 NPE나 AttributeError가 발생합니다. LangThree에서는 패턴 매칭이 불완전하면 컴파일러가 경고하므로, 실수가 훨씬 일찍 잡힙니다.

기본값으로 추출하기:

```
$ cat option_default.l3
let getOrDefault default opt =
    match opt with
    | Some x -> x
    | None -> default
let result = getOrDefault 0 None

$ langthree option_default.l3
0
```

`getOrDefault`는 매우 자주 쓰이는 패턴이므로 직접 정의해두면 편리합니다. Haskell의 `fromMaybe`, Rust의 `unwrap_or`와 같은 개념입니다.

### 일반적인 Option 패턴

Option 값을 다룰 때 반복되는 패턴 두 가지가 있습니다. 익혀두면 Option이 나오는 코드를 훨씬 자연스럽게 다룰 수 있습니다.

**Option에 대한 맵핑** -- `Some` 내부의 값에 함수를 적용합니다. `None`이면 그대로 `None`을 유지합니다:

```
$ cat option_map.l3
let optionMap f opt =
    match opt with
    | Some x -> Some (f x)
    | None -> None
let double x = x * 2
let result =
    match optionMap double (Some 5) with
    | Some v -> v
    | None -> 0

$ langthree option_map.l3
10
```

`None`인지 먼저 확인할 필요 없이, `optionMap`은 값이 있을 때만 `double`을 적용합니다. "값이 있으면 변환하고, 없으면 없는 채로 둔다" — 이것이 functor 패턴의 핵심입니다.

**바인딩 (flatMap)** -- 실패할 수 있는 연산을 체이닝합니다. 각 단계가 성공했을 때만 다음 단계로 넘어갑니다:

```
$ cat option_bind.l3
let optionBind f opt =
    match opt with
    | Some x -> f x
    | None -> None
let safeDivide x =
    if x = 0 then None else Some (100 / x)
let result =
    match optionBind safeDivide (Some 5) with
    | Some v -> v
    | None -> 0

$ langthree option_bind.l3
20
```

`optionBind`와 `optionMap`의 차이는 `f`의 반환 타입입니다. `map`에서 `f`는 일반 값을 반환하고, `bind`에서 `f`는 `Option`을 반환합니다. 이를 통해 여러 개의 실패 가능한 연산을 체이닝할 때 이중 중첩(`Option<Option<a>>`)을 피할 수 있습니다.

**파이프와 함께 Option 사용하기:**

파이프 연산자와 결합하면 Option 변환 체인을 더 읽기 쉽게 표현할 수 있습니다:

```
$ cat option_pipe.l3
let optionMap f opt =
    match opt with
    | Some x -> Some (f x)
    | None -> None
let double x = x * 2
let result =
    match (Some 5 |> optionMap double) with
    | Some v -> v
    | None -> 0

$ langthree option_pipe.l3
10
```

## Prelude 확장하기

Prelude가 `.fun` 파일로 구성되어 있다는 것은 단순히 구현 방식의 이야기가 아닙니다. 여러분이 직접 Prelude를 확장할 수 있다는 뜻입니다. 프로젝트에서 공통으로 쓰는 타입이나 함수를 Prelude에 추가하면, 모든 파일에서 import 없이 사용할 수 있습니다.

`Prelude/` 디렉토리에 새 `.fun` 파일을 생성하여 자신만의 타입을 Prelude에 추가할 수 있습니다. 로드 순서는 의존성 분석으로 자동 결정되므로, 파일 이름을 신경 쓸 필요가 없습니다.

예를 들어, `Prelude/Result.fun`을 생성하면:

```
type Result 'a 'b =
    | Ok of 'a
    | Error of 'b
```

이 파일을 추가한 후, `Ok`과 `Error` 생성자가 모든 코드에서 사용 가능해집니다:

```
$ cat result_demo.l3
let safeDivide x y =
    if y = 0 then Error "division by zero"
    else Ok (x / y)
let result =
    match safeDivide 10 3 with
    | Ok v -> v
    | Error _ -> 0

$ langthree result_demo.l3
3
```

`Result` 타입은 Rust나 F#에서 오류 처리의 주력 도구입니다. `Option`이 "값이 있거나 없거나"라면, `Result`는 "성공했거나, 구체적인 이유와 함께 실패했거나"입니다. 예외를 사용하는 대신 `Result`를 반환하면, 오류 처리가 타입 시스템에 드러나므로 호출하는 쪽에서 처리를 강제할 수 있습니다.

Prelude 타입은 REPL과 파일 모드 모두에서 동작합니다. Prelude 파일의 생성자는 `open` 없이 사용 가능합니다.

## Prelude 리스트 함수

`Prelude/List.fun`은 리스트를 다루는 8개의 표준 함수를 제공합니다. 이 함수들은 import 없이 REPL과 파일 모드 모두에서 바로 사용할 수 있는 실제 함수입니다.

다른 함수형 언어(Haskell, OCaml, F#)에서 이미 이름을 알고 있다면 바로 쓸 수 있고, 처음이라면 이 함수들을 익히는 것이 리스트 처리의 첫 걸음입니다.

### 한정된 접근 (Qualified Access)

Prelude 함수는 모듈 이름을 붙여서도 사용할 수 있습니다. `List.map`, `List.length`처럼 모듈 이름을 접두사로 붙이면 어디서 온 함수인지 명확해집니다:

```
$ cat qualified_prelude.l3
let n = List.length [1; 2; 3]
let doubled = List.map (fun x -> x * 2) [1; 2; 3]
let result = doubled

$ langthree qualified_prelude.l3
[2; 4; 6]
```

한정된 접근과 비한정된 접근을 섞어 쓸 수도 있습니다. `map`과 `List.map`은 같은 함수입니다:

```
funlang> length [1; 2; 3]
3

funlang> List.length [1; 2; 3]
3
```

마찬가지로 `Core.id`, `Core.compose`, `Option.None`, `Option.Some` 등 다른 Prelude 모듈도 한정된 접근이 가능합니다.

### 리스트 변환: map, filter

`map`은 리스트의 각 요소에 함수를 적용합니다. 리스트 처리의 가장 기본적인 패턴으로, 각 원소를 독립적으로 변환할 때 씁니다:

```
funlang> map (fun x -> x * 2) [1..5]
[2; 4; 6; 8; 10]
```

`filter`는 조건을 만족하는 요소만 남깁니다. `map`이 "모든 원소를 변환"한다면 `filter`는 "일부 원소만 선택"합니다:

```
funlang> filter (fun x -> x > 3) [1..6]
[4; 5; 6]
```

이 두 함수는 대부분의 리스트 처리 코드의 80%를 커버합니다. Python의 리스트 컴프리헨션이나 `map()`, `filter()` 내장 함수와 같은 역할을 합니다.

### 리스트 축약: fold

`fold`는 리스트를 하나의 값으로 축약합니다. 합계, 최댓값, 문자열 합치기 등 리스트를 단일 결과로 만드는 모든 연산이 `fold`로 표현됩니다:

```
funlang> fold (fun acc -> fun x -> acc + x) 0 [1..10]
55
```

`fold`는 처음엔 조금 낯설 수 있습니다. 핵심은 "누산기(accumulator)를 초기값에서 시작해 각 원소마다 업데이트해 나간다"는 것입니다. 여기서 `0`이 초기값이고, `fun acc -> fun x -> acc + x`가 매 원소마다 실행되는 업데이트 함수입니다. 1부터 10까지 더하면 55가 됩니다.

`fold`는 `map`과 `filter`를 포함한 거의 모든 리스트 연산을 직접 구현할 수 있을 만큼 강력합니다. 하지만 그렇다고 항상 `fold`를 쓸 필요는 없습니다. `map`이나 `filter`로 표현되는 코드가 훨씬 읽기 쉬울 때는 그것을 쓰세요.

### 리스트 정보: length, hd, tl

```
funlang> length [1; 2; 3]
3

funlang> hd [10; 20]
10

funlang> tl [10; 20]
[20]
```

`hd`(head)는 첫 번째 원소를, `tl`(tail)은 나머지 전부를 반환합니다. OCaml의 명명 관습에서 온 이름입니다. `hd`와 `tl`을 빈 리스트에 적용하면 런타임 오류가 발생하므로, 빈 리스트 가능성이 있다면 패턴 매칭으로 먼저 확인하는 것이 안전합니다.

### 리스트 조작: reverse, append

```
funlang> reverse [] [1; 2; 3]
[3; 2; 1]

funlang> append [1; 2] [3; 4]
[1; 2; 3; 4]
```

`reverse`의 첫 번째 인자가 빈 리스트인 점이 눈에 띕니다. 이것은 누산기 패턴의 흔적입니다. 내부적으로 빈 리스트를 누산기 초기값으로 사용하면서 반전을 수행하는 방식이 인터페이스에 드러난 것입니다. 대부분의 경우 `reverse [] myList` 형태로 호출하면 됩니다.

### 리스트 정렬: sort, sortBy

`List.sort`는 리스트를 오름차순으로 정렬합니다. `List.sortBy`는 키 함수를 적용한 결과로 정렬합니다:

```
$ cat list_sort.l3
let r1 = List.sort [3; 1; 2]
let r2 = List.sortBy (fun x -> 0 - x) [1; 2; 3]
let _ = println (to_string r1)
let _ = println (to_string r2)

$ langthree list_sort.l3
[1; 2; 3]
[3; 2; 1]
()
```

`List.sortBy (fun x -> 0 - x)`는 키를 부호 반전하여 내림차순 정렬을 구현합니다.

### 리스트 검색: exists, tryFind, choose, distinctBy

```
$ cat list_search.l3
let _ = println (to_string (List.exists (fun x -> x > 2) [1; 2; 3]))
let _ = println (to_string (List.tryFind (fun x -> x > 2) [1; 2; 3]))
let _ = println (to_string (List.tryFind (fun x -> x > 10) [1; 2; 3]))
let r = List.choose (fun x -> if x > 1 then Some (x * 10) else None) [1; 2; 3]
let _ = println (to_string r)
let d = List.distinctBy (fun x -> x % 2) [1; 2; 3; 4; 5]
let _ = println (to_string d)

$ langthree list_search.l3
true
Some 3
None
[20; 30]
[1; 2]
()
```

| 함수 | 설명 |
|------|------|
| `List.exists pred xs` | 조건을 만족하는 원소가 하나라도 있으면 `true` |
| `List.tryFind pred xs` | 조건을 만족하는 첫 번째 원소를 `Option`으로 반환 |
| `List.choose f xs` | `f`가 `Some`을 반환한 값만 모은 리스트 (filter + map) |
| `List.distinctBy f xs` | 키 함수 `f`의 결과가 중복되지 않는 첫 번째 원소만 남김 |

### 리스트 변환 확장: mapi, item, isEmpty

```
$ cat list_transform.l3
let r1 = List.mapi (fun i -> fun x -> i + x) [10; 20; 30]
let _ = println (to_string r1)
let _ = println (to_string (List.item 1 [10; 20; 30]))
let _ = println (to_string (List.isEmpty []))
let _ = println (to_string (List.isEmpty [1]))

$ langthree list_transform.l3
[10; 21; 32]
20
true
false
()
```

| 함수 | 설명 |
|------|------|
| `List.mapi f xs` | 인덱스와 원소를 받는 함수로 매핑 (`f i x`) |
| `List.item n xs` | n번째 원소 반환 (0-based) |
| `List.isEmpty xs` | 빈 리스트인지 확인 |
| `List.head xs` | 첫 번째 원소 (`hd`의 별칭) |
| `List.tail xs` | 나머지 원소 (`tl`의 별칭) |

### 컬렉션 변환: List.ofSeq

`List.ofSeq`는 배열, HashSet, Queue, MutableList 등 임의의 컬렉션을 불변 리스트로 변환합니다:

```
$ cat list_ofseq.l3
let hs = HashSet.create ()
let _ = HashSet.add hs 3
let _ = HashSet.add hs 1
let _ = HashSet.add hs 2
let sorted = List.sort (List.ofSeq hs)
let _ = println (to_string sorted)

$ langthree list_ofseq.l3
[1; 2; 3]
()
```

가변 컬렉션을 불변 리스트로 변환한 뒤 `map`, `filter`, `sort` 등 리스트 함수를 적용하는 패턴이 자주 쓰입니다. `Array.ofSeq`도 동일한 방식으로 배열로 변환합니다.

## Prelude 핵심 함수

`Prelude/Core.fun`은 범용 고차 함수 3개를 제공합니다. 마찬가지로 import 없이 어디서든 사용할 수 있습니다.

이 함수들은 수학의 기본 원소처럼, 단순하지만 다양한 맥락에서 유용합니다. 처음엔 "이걸 왜 쓰나?" 싶을 수 있는데, 파이프라인이나 고차 함수와 결합했을 때 진가가 드러납니다.

### id -- 항등 함수

입력값을 그대로 반환합니다:

```
funlang> id 42
42
```

"아무것도 안 하는 함수가 왜 필요한가?" — `id`는 함수를 인자로 받는 곳에 기본값으로 쓰기 좋습니다. 예를 들어 `map id [1; 2; 3]`은 리스트를 그대로 복사합니다. 또 `f >> id = f`, `id >> f = f`처럼 합성의 항등원이기도 합니다. Haskell이나 F#에서도 같은 이름으로 존재합니다.

### const -- 상수 함수

첫 번째 인자를 반환하고 두 번째 인자를 무시합니다:

```
funlang> const 42 "ignored"
42
```

`const`는 두 인자를 받지만 첫 번째만 돌려줍니다. 이것이 어디서 쓰이냐면, 예를 들어 `map (const 0) [1; 2; 3]`은 리스트의 모든 원소를 0으로 바꿉니다. 콜백이나 고차 함수가 함수를 기대하는데 "항상 이 값을 반환하는 함수"가 필요할 때 `const`가 딱 맞습니다.

### compose -- 함수 합성

두 함수를 합성합니다. `compose f g x`는 `f (g x)`와 같습니다:

```
funlang> compose inc double 5
11
```

`compose`는 `>>` 연산자의 함수 버전입니다. 연산자 형태(`>>`)가 더 읽기 편하지만, `compose`를 함수로 넘겨야 하는 경우에는 `compose`를 직접 씁니다. 예를 들어 함수들의 리스트를 `fold`로 합성하는 경우가 있습니다.

### 유틸리티 함수

```
funlang> not true
false

funlang> (min 3 5, max 3 5)
(3, 5)

funlang> abs (0 - 42)
42

funlang> (fst (1, 2), snd (1, 2))
(1, 2)

funlang> ignore 42
()
```

`fst`와 `snd`는 튜플의 첫 번째, 두 번째 원소를 꺼냅니다. `ignore`는 값을 받아서 `()`를 반환하는데, 부작용을 일으키는 함수의 반환값을 무시하고 싶을 때 유용합니다. F# 코드를 써봤다면 익숙한 이름들입니다.

## 파이프라인과 함께 사용하기

Prelude 함수들은 파이프 연산자 `|>`와 결합하면 강력한 데이터 처리 파이프라인을 구성할 수 있습니다. 이것이 LangThree에서 리스트 처리 코드를 가장 읽기 쉽게 쓰는 방법입니다:

```
$ cat pipeline.l3
let result =
    [1..10]
    |> filter (fun x -> x % 2 = 0)
    |> map (fun x -> x * x)

$ langthree pipeline.l3
[4; 16; 36; 64; 100]
```

"1부터 10까지의 수 중 짝수만 골라서, 각각 제곱한 결과" — 코드가 이 설명을 그대로 표현합니다. SQL의 `WHERE`와 `SELECT`처럼, 파이프라인의 각 단계가 한 가지 일만 담당합니다. 단계를 추가하거나 순서를 바꾸기도 쉽습니다.

## Prelude 연산자

Prelude는 자주 사용하는 패턴을 위한 연산자도 제공합니다. 연산자는 중위 표기(infix)를 쓸 수 있어서, 일부 표현은 함수 형태보다 연산자 형태가 훨씬 자연스럽습니다.

### `++` — 리스트 연결

`append`의 중위 연산자 버전입니다:

```
funlang> [1; 2] ++ [3; 4; 5]
[1; 2; 3; 4; 5]
```

`(++)`를 함수로 사용할 수도 있습니다. 괄호로 감싸면 일반 함수처럼 고차 함수에 넘길 수 있습니다:

```
funlang> fold (++) [] [[1; 2]; [3]; [4; 5]]
[1; 2; 3; 4; 5]
```

`fold`에 `(++)`를 넘겨서 리스트들을 하나로 합쳤습니다. Haskell의 `concat`과 같은 결과지만, 여기서는 `fold`와 `++`의 조합으로 직접 구현한 셈입니다.

`++`는 INFIXOP2 (+ 와 같은 우선순위, 좌결합)입니다.

### `<|>` — Option 대안

첫 번째 `Some` 값을 반환하거나, 모두 `None`이면 `None`을 반환합니다. 여러 소스에서 값을 찾을 때 fallback 체인을 표현하기에 딱 맞습니다:

```
funlang> Some 1 <|> Some 2
Some 1

funlang> None <|> Some 42
Some 42

funlang> None <|> None
None
```

연쇄하여 fallback 패턴을 구현할 수 있습니다. "여러 파싱 방법을 순서대로 시도해서 첫 번째로 성공한 결과를 택한다"는 파서 콤비네이터 스타일의 코드를 자연스럽게 표현합니다:

```
$ cat fallback.l3
let tryParse s = match s with | "42" -> Some 42 | "0" -> Some 0 | _ -> None
let result = tryParse "abc" <|> tryParse "xyz" <|> tryParse "42" <|> Some 0

$ langthree fallback.l3
Some 42
```

`<|>`는 INFIXOP0 (비교 연산자 수준, 좌결합)입니다.

### `^^` — 문자열 연결

`string_concat`의 중위 연산자 버전입니다:

```
funlang> "hello" ^^ " " ^^ "world"
"hello world"
```

`+` 연산자는 정수 덧셈이므로, 문자열 연결에는 `^^`를 사용합니다. 이 점이 Python이나 JavaScript와 다른 부분인데, LangThree는 타입에 따라 다른 동작을 하는 오버로딩을 지원하지 않아서 연산자를 구분합니다. 처음엔 어색하지만 코드를 읽을 때 "지금 더하는 게 숫자인지 문자열인지" 즉시 알 수 있다는 장점이 있습니다:

```
$ cat string_build.l3
let formatPair key = fun value -> key ^^ "=" ^^ value
let result = formatPair "name" "Alice"

$ langthree string_build.l3
"name=Alice"
```

`^^`는 INFIXOP1 (@ 와 같은 우선순위, 우결합)입니다.

## 런타임 내장 함수

Prelude 함수와는 별도로, LangThree에는 내장 환경(`initialBuiltinEnv`)에서 제공되는 런타임 내장 함수가 있습니다. 이것들은 `.fun` 파일이 아니라 인터프리터 자체에 내장되어 있으며, 특히 I/O나 타입 변환처럼 언어 런타임과 밀접한 기능들입니다:

| 함수 | 타입 | 설명 |
|----------|------|-------------|
| `char_to_int` | `char -> int` | 문자를 ASCII 코드로 변환 |
| `int_to_char` | `int -> char` | ASCII 코드를 문자로 변환 |
| `failwith` | `string -> 'a` | 메시지와 함께 예외 발생 |
| `string_length` | `string -> int` | 문자열의 길이 |
| `string_concat` | `string -> string -> string` | 두 문자열을 연결 |
| `string_sub` | `string -> int -> int -> string` | 부분 문자열 (시작, 길이) |
| `string_contains` | `string -> string -> bool` | 부분 문자열 포함 여부 |
| `to_string` | `'a -> string` | 모든 타입을 문자열로 변환 (문자열은 그대로) |
| `string_to_int` | `string -> int` | 문자열을 정수로 파싱 |
| `print` | `string -> unit` | 줄바꿈 없이 출력 |
| `println` | `string -> unit` | 줄바꿈 포함 출력 |
| `printf` | `string -> ...` | 형식화 출력 |
| `sprintf` | `string -> ...` | 형식화 문자열 반환 |
| `printfn` | `string -> ...` | 형식화 출력 + 줄바꿈 |

이들은 REPL과 파일 모드 모두에서 동작합니다:

```
funlang> string_length "hello"
5

funlang> to_string 42
"42"

funlang> to_string (Some [1; 2; 3])
"Some [1; 2; 3]"
```

`to_string`은 모든 타입을 지원합니다 — ADT, 리스트, 튜플, 레코드 등. 문자열은 따옴표 없이 그대로 반환됩니다 (F# `string` 함수와 동일). 디버깅할 때 복잡한 값을 출력해보고 싶다면 `to_string`을 `println`과 함께 쓰면 됩니다.

자세한 내용은 [7장: 문자열과 출력](07-strings-and-output.md)을 참조하세요.

## Prelude vs 내장 함수 요약

두 분류를 헷갈리지 않으려면 간단한 기준이 있습니다. Prelude는 LangThree 코드로 작성된 라이브러리이고, 내장 함수는 인터프리터 자체에 박혀있는 기능입니다. 사용자 입장에서는 둘 다 import 없이 쓸 수 있어 차이가 없지만, Prelude는 직접 수정하거나 확장할 수 있다는 점이 다릅니다.

| 분류 | 출처 | 예제 |
|----------|--------|---------|
| Prelude 타입+함수 | `Prelude/*.fun` 파일 | `Option`, `Result`, `map`, `filter`, `fold`, `sort`, `tryFind`, `choose`, `id`, `compose`, `not`, `min`, `max`, `abs`, `fst`, `snd`, `ignore` 등 |
| Prelude 타입 클래스 | `Prelude/Typeclass.fun` | `show`, `eq` — 타입 클래스와 기본 타입 인스턴스 ([23장](23-typeclasses.md) 참조) |
| Prelude 모듈 | `Prelude/*.fun` 파일 | `String.trim`, `Char.IsDigit`, `Array.sort`, `HashSet.create`, `Queue.create`, `MutableList.create`, `StringBuilder.create` 등 |
| Prelude 연산자 | `Prelude/*.fun` 파일 | `++` (리스트 연결), `<\|>` (Option 대안), `^^` (문자열 연결) |
| 런타임 내장 함수 | `initialBuiltinEnv` | `string_length`, `print`, `println`, `printf`, `sprintf`, `printfn` 등 |
| 산술 연산자 | 내장 | `+`, `-`, `*`, `/`, `%` (모듈로) |

두 분류 모두 런타임에 실제로 동작하며, REPL과 파일 모드에서 import 없이 사용 가능합니다.

## 참고 사항

- **Prelude 파일**은 `Prelude/` 디렉토리에서 의존성 순서로 자동 로드되는 `.fun` 파일입니다
- **Prelude 생성자** (`None`, `Some`)와 **Prelude 함수** (`map`, `filter` 등)는 `open` 없이 사용 가능합니다
- **Prelude 함수**는 모두 실제 런타임 함수로, 호출하면 정상적으로 결과를 반환합니다
- **런타임 내장 함수** (`print`, `string_length` 등)는 어디서든 동작합니다
- **패턴 매칭**은 파일 모드에서 Prelude 타입에 대해 동작합니다
