# 23장: 타입 클래스 (Type Classes)

지금까지 LangThree에서 타입을 정의하고, 패턴 매칭하고, 함수를 조합하는 방법을 배웠습니다. 하지만 한 가지 빠진 조각이 있습니다 — "이 타입에 대해 이런 동작을 할 수 있다"는 사실을 타입 시스템 수준에서 표현하는 방법입니다.

예를 들어 `to_string`은 모든 타입을 문자열로 바꿀 수 있는 내장 함수입니다. 하지만 "문자열로 변환 가능하다"는 속성을 사용자가 직접 정의하고 확장할 수 있으면 어떨까요? 타입 클래스는 바로 이 질문에 대한 답입니다.

Haskell의 타입 클래스에서 직접 영감을 받은 이 기능은, 다형성 함수가 특정 동작을 **요구**할 수 있게 합니다. Java의 인터페이스나 Rust의 trait과 비슷한 역할이지만, 타입 정의와 분리되어 있어 기존 타입에도 새 동작을 추가할 수 있습니다.

## 타입 클래스 선언

타입 클래스는 `typeclass` 키워드로 선언합니다. 타입 변수와 메서드 시그니처의 목록을 정의합니다:

```
$ cat show_class.l3
typeclass Show 'a =
    | show : 'a -> string

instance Show int =
    let show x = to_string x

let result = show 42

$ langthree show_class.l3
"42"
```

`typeclass Show 'a`는 "타입 `'a`에 대해 `show`라는 함수가 존재해야 한다"고 선언합니다. 메서드는 선행 파이프(`|`)와 타입 어노테이션으로 작성합니다 — ADT의 생성자 선언과 같은 구문입니다.

`instance Show int`는 "`int` 타입에 대해 `Show`의 구체적 구현을 제공한다"는 뜻입니다. 인스턴스 본문에서 `let show x = ...`로 실제 함수를 정의합니다.

## 여러 메서드를 가진 타입 클래스

타입 클래스는 메서드를 여러 개 가질 수 있습니다:

```
$ cat describable.l3
typeclass Describable 'a =
    | describe : 'a -> string
    | tag : 'a -> string

instance Describable int =
    let describe x = to_string x
    let tag x = "int"

let result = describe 42 + ":" + tag 42

$ langthree describable.l3
"42:int"
```

인스턴스는 타입 클래스가 선언한 모든 메서드를 구현해야 합니다.

## 내장 인스턴스: Show와 Eq

LangThree는 Prelude에서 두 가지 타입 클래스와 기본 타입에 대한 인스턴스를 제공합니다. 별도의 선언 없이 바로 사용할 수 있습니다.

### Show 클래스

`Show`는 값을 문자열로 변환하는 `show` 함수를 제공합니다. `int`, `bool`, `string`, `char` 네 가지 기본 타입에 대한 인스턴스가 내장되어 있습니다:

```
$ cat show_builtin.l3
let _ = println (show 42)
let _ = println (show true)
let _ = println (show 'x')
let _ = println (show "hello")

$ langthree show_builtin.l3
42
true
x
hello
```

`show`는 `to_string`과 비슷하지만, 타입 클래스 시스템을 통해 동작합니다. 즉 사용자가 직접 정의한 타입에도 `Show` 인스턴스를 추가할 수 있습니다.

### Eq 클래스

`Eq`는 두 값의 동등성을 비교하는 `eq` 함수를 제공합니다. 역시 `int`, `bool`, `string`, `char`에 대한 인스턴스가 내장되어 있습니다:

```
$ cat eq_builtin.l3
let _ = println (to_string (eq 1 1))
let _ = println (to_string (eq 1 2))
let _ = println (to_string (eq "hello" "hello"))
let _ = println (to_string (eq 'a' 'b'))

$ langthree eq_builtin.l3
true
false
true
false
```

## 제약 추론 (Constraint Inference)

타입 클래스의 진정한 힘은 **제약 추론**에 있습니다. 함수가 타입 클래스 메서드를 사용하면, 컴파일러가 자동으로 해당 제약을 추론합니다:

```
$ cat show_twice.l3
let show_twice x = show x + show x
let result = show_twice 42

$ langthree show_twice.l3
"4242"
```

`show_twice`는 `show`를 호출하므로, 컴파일러가 `Show 'a => 'a -> string`이라는 타입을 추론합니다. "타입 `'a`가 `Show`의 인스턴스일 때만 이 함수를 호출할 수 있다"는 의미입니다. `show_twice 42`를 호출하면 `'a`가 `int`로 결정되고, `Show int` 인스턴스가 자동으로 선택됩니다.

하나의 제약된 함수를 여러 타입에 사용할 수 있습니다:

```
$ cat show_poly.l3
let show_twice x = show x + show x
let _ = println (show_twice 42)
let _ = println (show_twice true)

$ langthree show_poly.l3
4242
truetrue
```

`show_twice 42`에서는 `Show int` 인스턴스가, `show_twice true`에서는 `Show bool` 인스턴스가 자동으로 선택됩니다. 함수를 한 번만 작성하고 다양한 타입에 대해 재사용할 수 있는 것이 핵심입니다.

## 명시적 제약 어노테이션

제약을 직접 명시할 수도 있습니다. `=>` 구문으로 제약과 타입을 구분합니다:

```
$ cat constrained_annot.l3
let f : Show 'a => 'a -> string = fun x -> show x

let result = f 42

$ langthree constrained_annot.l3
"42"
```

제약이 추론 가능한 경우에는 생략해도 되지만, 복잡한 함수에서 의도를 명확히 하고 싶을 때 유용합니다.

## 고차 함수와 타입 클래스

타입 클래스 메서드는 일반 함수이므로, 고차 함수의 인자로 전달할 수 있습니다:

```
$ cat show_map.l3
let map_show lst = List.map show lst
let result = map_show [1; 2; 3]

$ langthree show_map.l3
["1"; "2"; "3"]
```

`List.map show [1; 2; 3]`에서 `show`는 `int -> string` 함수처럼 동작합니다. Prelude의 `Show int` 인스턴스가 자동으로 선택됩니다. 타입 클래스 메서드가 일급 함수라는 사실이 파이프라인 스타일 프로그래밍과 자연스럽게 어울립니다.

## 에러 처리

타입 클래스 시스템은 잘못된 사용에 대해 명확한 에러 메시지를 제공합니다.

### 인스턴스가 없는 타입에 메서드 사용

```
$ cat no_instance.l3
let bad = show (fun x -> x)

$ langthree no_instance.l3
error[E0701]: No instance of Show for 'x -> 'x
```

함수 타입에 대한 `Show` 인스턴스가 없으므로 컴파일 에러가 발생합니다. "이 타입은 문자열로 변환할 방법이 정의되지 않았다"는 것을 타입 시스템이 잡아줍니다.

### 중복 인스턴스 선언

```
$ cat dup_instance.l3
typeclass Show 'a =
    | show : 'a -> string

instance Show int =
    let show x = to_string x

instance Show int =
    let show x = to_string x

$ langthree dup_instance.l3
error[E0702]: Duplicate instance declaration: Show int
```

같은 타입에 대해 인스턴스를 두 번 선언하면 에러가 발생합니다. 어떤 구현을 선택해야 할지 모호해지기 때문입니다.

### Eq 제약 위반

```
$ cat eq_error.l3
let result = eq (fun x -> x) (fun x -> x)

$ langthree eq_error.l3
error[E0701]: No instance of Eq for 'z -> 'z
```

함수 타입은 동등성 비교가 불가능합니다. 수학적으로 두 함수가 같은지 판정하는 것은 일반적으로 불가능한 문제이며, LangThree의 타입 시스템은 이를 컴파일 타임에 방지합니다.

## 사용자 정의 타입에 인스턴스 추가하기

타입 클래스의 큰 장점은 사용자가 정의한 ADT에도 인스턴스를 추가할 수 있다는 것입니다. Prelude의 `Show`와 `Eq`에 대해 사용자 타입의 인스턴스를 바로 선언할 수 있습니다:

```
$ cat custom_show.l3
type Color =
    | Red
    | Green
    | Blue

instance Show Color =
    let show c =
        match c with
        | Red -> "Red"
        | Green -> "Green"
        | Blue -> "Blue"

let result = show Green

$ langthree custom_show.l3
"Green"
```

타입 정의와 인스턴스 선언이 분리되어 있으므로, 이미 존재하는 타입에 새로운 동작을 추가할 수 있습니다. Java에서 기존 클래스에 인터페이스를 구현하려면 클래스 자체를 수정해야 하지만, 타입 클래스에서는 그럴 필요가 없습니다.

`Eq`도 마찬가지입니다:

```
$ cat custom_eq.l3
type Direction = | North | South | East | West

instance Eq Direction =
    let eq a = fun b ->
        match (a, b) with
        | (North, North) -> true
        | (South, South) -> true
        | (East, East) -> true
        | (West, West) -> true
        | _ -> false

let _ = println (to_string (eq North North))
let result = eq North South

$ langthree custom_eq.l3
true
false
```

## 모듈과 타입 클래스

타입 클래스는 모듈 시스템과 자연스럽게 결합됩니다. 타입과 인스턴스를 같은 모듈에 묶어서 캡슐화할 수 있습니다:

```
$ cat mod_typeclass.l3
module Shapes =
    type Shape = | Circle | Square | Triangle
    instance Show Shape =
        let show s =
            match s with
            | Circle -> "circle"
            | Square -> "square"
            | Triangle -> "triangle"

open Shapes
let _ = println (show Circle)
let _ = println (show Square)
let result = show Triangle

$ langthree mod_typeclass.l3
circle
square
"triangle"
```

모듈 안에서 선언된 인스턴스는 전역적으로 동작합니다 — `open Shapes` 이후에 `show Circle`이 바로 동작합니다. 인스턴스가 모듈 안에 있더라도 `open` 없이 인스턴스 자체는 유효합니다. `open`이 필요한 것은 생성자(`Circle`, `Square`)와 타입 이름을 스코프에 가져오기 위해서입니다.

타입 클래스 자체도 모듈 안에서 선언하고 `open`으로 가져올 수 있습니다:

```
$ cat mod_class.l3
module Render =
    typeclass Renderable 'a =
        | render : 'a -> string

open Render
instance Renderable int =
    let render x = "[" + to_string x + "]"

let result = render 42

$ langthree mod_class.l3
"[42]"
```

## Prelude의 타입 클래스

`Prelude/Typeclass.fun` 파일에는 다음이 정의되어 있습니다:

```
typeclass Show 'a =
    | show : 'a -> string

instance Show int =
    let show x = to_string x

instance Show bool =
    let show x = if x then "true" else "false"

instance Show string =
    let show x = x

instance Show char =
    let show x = to_string x

typeclass Eq 'a =
    | eq : 'a -> 'a -> bool

instance Eq int =
    let eq x = fun y -> x = y

instance Eq bool =
    let eq x = fun y -> x = y

instance Eq string =
    let eq x = fun y -> x = y

instance Eq char =
    let eq x = fun y -> x = y
```

이 파일은 Prelude의 다른 파일과 마찬가지로 자동으로 로드됩니다. 따라서 `show`와 `eq`는 별도의 선언 없이 모든 코드에서 사용할 수 있습니다.

## 요약

타입 클래스는 "이 타입에 이런 동작이 가능하다"를 타입 시스템으로 표현하는 방법입니다:

| 구성 요소 | 구문 | 역할 |
|----------|------|------|
| 타입 클래스 선언 | `typeclass Show 'a = \| show : 'a -> string` | 메서드 시그니처 정의 |
| 인스턴스 선언 | `instance Show int = let show x = ...` | 특정 타입에 대한 구현 |
| 제약 어노테이션 | `Show 'a => 'a -> string` | 함수가 요구하는 인스턴스 명시 |
| 제약 추론 | (자동) | 메서드 사용 시 제약 자동 추론 |

- `Show`와 `Eq` 타입 클래스가 Prelude에 내장되어 있으며, `int`, `bool`, `string`, `char`에 대한 인스턴스를 제공합니다
- 사용자 정의 ADT에 대해 `instance Show MyType = ...`으로 인스턴스를 추가할 수 있습니다
- 타입 클래스 메서드는 일급 함수로, 고차 함수와 자연스럽게 결합됩니다
- 모듈 안에서 선언된 인스턴스는 전역적으로 동작합니다
- 인스턴스가 없는 타입에 메서드를 사용하면 `E0701` 에러가 발생합니다

향후 버전에서는 제약된 인스턴스 (`Show 'a => Show (list 'a)`), 슈퍼클래스 제약, 자동 인스턴스 도출(`derive`) 등이 추가될 예정입니다.
