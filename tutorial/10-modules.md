# 10장: 모듈과 네임스페이스 (Modules and Namespaces)

## 모듈 선언

`module M =` 뒤에 들여쓰기된 본문으로 모듈을 정의합니다:

```
$ cat config.l3
module Config =
    let width = 800
    let height = 600
    let title = "My App"
let result = Config.title + " (" + to_string Config.width + "x" + to_string Config.height + ")"

$ langthree config.l3
"My App (800x600)"
```

들여쓰기가 모듈 본문의 범위를 결정합니다 -- `end` 키워드가 필요하지 않습니다.

## 한정된 접근

점 표기법(dot notation)으로 모듈 멤버에 접근합니다:

```
$ cat qualified.l3
module Math =
    let double x = x * 2
    let triple x = x * 3
let result = Math.double 5 + Math.triple 3

$ langthree qualified.l3
19
```

## open 지시문

`open`으로 모듈의 모든 멤버를 현재 스코프에 가져옵니다:

```
$ cat open_mod.l3
module M =
    let x = 10
    let y = 20
open M
let result = x + y

$ langthree open_mod.l3
30
```

`open M` 이후에는 `M.` 접두사 없이 `x`와 `y`를 직접 사용할 수 있습니다.

## 중첩 모듈

모듈은 중첩될 수 있습니다. 각 수준은 더 깊은 들여쓰기를 사용합니다:

```
$ cat nested.l3
module Outer =
    module Inner =
        let value = 42
let result = Outer.Inner.value

$ langthree nested.l3
42
```

연쇄된 한정 접근은 어떤 깊이에서든 동작합니다.

## 여러 모듈

하나의 파일에 여러 모듈 선언을 포함할 수 있습니다. 모듈은
위에서 아래로 해석됩니다 -- 나중의 모듈이 이전 모듈을 참조할 수 있습니다:

```
$ cat multi_mod.l3
module A =
    let x = 10
module B =
    let y = 20
let result = A.x + B.y

$ langthree multi_mod.l3
30
```

## 타입 선언을 포함하는 모듈

모듈은 ADT 정의를 포함할 수 있습니다. 생성자를 스코프에 가져오려면
`open`을 사용하거나, 인자 없는(nullary) 생성자에 대해 한정된 접근을 사용합니다:

```
$ cat mod_type.l3
module Colors =
    type Color = Red | Green | Blue
open Colors
let result =
    match Green with
    | Red -> "red"
    | Green -> "green"
    | Blue -> "blue"

$ langthree mod_type.l3
"green"
```

한정된 생성자 접근은 인자 없는 생성자와 데이터를 가진 생성자 모두에서 동작합니다:

```
$ cat mod_ctor.l3
module M =
    type Opt = MNone | MSome of int
let result = M.MSome 42

$ langthree mod_ctor.l3
MSome 42
```

## 패턴 매칭을 사용하는 모듈 함수

모듈 함수와 ADT를 결합할 수 있습니다:

```
$ cat mod_fn.l3
module M =
    type Opt = MNone | MSome of int
    let unwrap x =
        match x with
        | MSome v -> v
        | MNone -> 0
let result = M.unwrap (M.MSome 42)

$ langthree mod_fn.l3
42
```

## 네임스페이스 선언

`namespace` 선언은 파일의 전체 내용을 감쌉니다:

```
$ cat ns.l3
namespace App
let x = 42
let result = x + 1

$ langthree ns.l3
43
```

`module`과 달리 namespace는 중첩된 스코프를 생성하지 않습니다 -- 선언은
최상위 수준에 위치합니다.

## 실용 예제: 계층화된 설정

관련된 값을 정리하는 여러 모듈:

```
$ cat layers.l3
module DB =
    let host = "localhost"
    let port = 5432
module App =
    let name = "MyService"
    let version = 1
let result = App.name + " v" + to_string App.version + " -> " + DB.host + ":" + to_string DB.port

$ langthree layers.l3
"MyService v1 -> localhost:5432"
```

## 참고 사항

- **들여쓰기 기반:** 모듈 본문은 들여쓰기로 구분되며, `end`나 `}`가 아님
- **위에서 아래 순서:** 모듈은 참조되기 전에 정의되어야 함 (순환 의존성 불가)
- **`module M =`** 은 중첩 모듈에 `=`을 사용; 최상위 `namespace`는 `=`이 없음
- **한정된 접근**은 값, 함수, 생성자에 대해 동작함
