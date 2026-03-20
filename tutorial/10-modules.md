# 10장: 모듈과 네임스페이스 (Modules and Namespaces)

코드가 조금만 커져도 이름 충돌이 발생합니다. `parse`라는 함수가 파일마다 있다면 어느 파일의 `parse`인지 명확하지 않습니다. `width`가 설정 값인지 캔버스 크기인지 알 수 없을 때도 있습니다. 모듈은 이 문제를 해결하기 위한 도구입니다.

LangThree의 모듈 시스템은 F#과 OCaml의 영향을 받았습니다. 중괄호나 `end` 키워드 없이 들여쓰기만으로 범위를 정의하며, 관련 있는 값과 함수를 하나의 이름 아래 묶어 논리적인 단위를 만들 수 있습니다.

## 모듈 선언

`module M =` 뒤에 들여쓰기된 본문으로 모듈을 정의합니다. 들여쓰기가 모듈의 경계를 결정하므로, 모듈 바깥으로 나오려면 들여쓰기를 줄이면 됩니다:

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

이 예제의 장점이 바로 느껴집니다. `width`나 `height`만으로는 무엇의 크기인지 불분명하지만, `Config.width`는 설정 값임이 분명합니다. 점 표기법이 문서 역할을 겸합니다.

## 한정된 접근

점 표기법(dot notation)으로 모듈 멤버에 접근합니다. 모듈에 정의된 값과 함수 모두에 적용됩니다:

```
$ cat qualified.l3
module Math =
    let double x = x * 2
    let triple x = x * 3
let result = Math.double 5 + Math.triple 3

$ langthree qualified.l3
19
```

`Math.double`과 `Math.triple`은 단순히 `double`과 `triple`이 어디 있는지를 알려주는 것이 아닙니다. 이름이 충돌해도 `Math.double`과 `String.double`은 완전히 별개의 함수입니다. 대규모 코드베이스에서는 이 구분이 매우 중요합니다.

## open 지시문

한정된 접근이 명확하긴 하지만, 한 모듈의 기능을 집중적으로 쓸 때는 매번 `Math.`를 붙이는 게 번거로울 수 있습니다. `open`은 모듈의 모든 멤버를 현재 스코프로 가져옵니다:

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

`open`을 쓸 때는 주의가 필요합니다. 어떤 이름이 어느 모듈에서 왔는지 불분명해질 수 있기 때문입니다. Python에서 `from module import *`를 지양하는 것과 같은 이유입니다. 일반적으로 범위가 좁은 곳에서, 혹은 이름 충돌 위험이 없을 때 `open`을 쓰는 것이 좋습니다.

## 중첩 모듈

모듈 안에 모듈을 정의할 수 있습니다. 계층적인 구조가 필요할 때, 예를 들어 큰 서브시스템 안에 관련 있는 여러 하위 모듈이 있을 때 유용합니다. 각 수준은 더 깊은 들여쓰기를 사용합니다:

```
$ cat nested.l3
module Outer =
    module Inner =
        let value = 42
let result = Outer.Inner.value

$ langthree nested.l3
42
```

연쇄된 한정 접근은 어떤 깊이에서든 동작합니다. `Outer.Inner.value`처럼 경로를 따라 내려가는 방식은 파일 시스템의 디렉토리 구조와 비슷합니다. 실제로 대규모 F# 프로젝트에서는 `Domain.User.Repository.find` 같은 형태로 네임스페이스를 구성하는 경우도 있습니다.

너무 깊은 중첩은 경로가 길어져 오히려 읽기 불편해질 수 있으니, 2~3단계가 실용적인 한도입니다.

## 여러 모듈

하나의 파일에 여러 모듈 선언을 포함할 수 있습니다. 모듈은 위에서 아래로 해석됩니다 -- 나중의 모듈이 이전 모듈을 참조할 수 있습니다:

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

이 순서 규칙은 중요합니다. `B` 안에서 `A.x`를 참조하는 것은 가능하지만, `A` 안에서 `B.y`를 참조하는 것은 불가능합니다. 순환 의존성이 원천 차단됩니다. 처음엔 제약처럼 느껴지지만, 이 규칙 덕분에 코드의 의존성 그래프가 항상 단방향(topological order)을 유지합니다. 코드베이스가 커져도 의존성이 엉키지 않습니다.

## 타입 선언을 포함하는 모듈

모듈은 값과 함수뿐 아니라 ADT 정의도 포함할 수 있습니다. 관련 있는 타입과 그 타입을 다루는 함수를 한 모듈에 묶으면 자연스러운 캡슐화가 됩니다. 생성자를 스코프에 가져오려면 `open`을 사용하거나, 인자 없는(nullary) 생성자에 대해 한정된 접근을 사용합니다:

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

`M.MSome 42`처럼 모듈 이름을 붙여서 생성자를 사용할 수 있습니다. `open M` 없이도 생성자를 명확하게 참조할 수 있어서, 같은 이름의 생성자가 여러 모듈에 있어도 구분이 됩니다.

## 패턴 매칭을 사용하는 모듈 함수

모듈의 진짜 강점은 타입과 그 타입을 다루는 함수를 함께 묶을 때 나타납니다. OCaml이나 F#의 모듈 패턴과 동일합니다:

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

`M.unwrap`은 `M.Opt` 타입을 이해하는 함수입니다. 타입과 함수가 같은 모듈에 있으니, `unwrap`을 수정할 때 타입 정의와 함께 볼 수 있어 맥락을 잃지 않습니다. 이것이 객체지향의 클래스가 제공하는 캡슐화를 함수형 방식으로 달성하는 방법입니다.

## 네임스페이스 선언

`namespace` 선언은 파일의 전체 내용을 감쌉니다. 모듈과 달리, 네임스페이스는 중첩된 스코프를 만들지 않습니다. 파일 수준의 식별자 역할을 하며, 여러 파일에 걸쳐 일관된 이름 체계를 부여할 때 씁니다:

```
$ cat ns.l3
namespace App
let x = 42
let result = x + 1

$ langthree ns.l3
43
```

`module`과 달리 namespace는 중첩된 스코프를 생성하지 않습니다 -- 선언은 최상위 수준에 위치합니다.

`module`과 `namespace`의 차이를 한 문장으로 정리하면: `module`은 코드 안에서 이름 공간을 만들고, `namespace`는 파일 전체에 레이블을 붙입니다. 파일이 어느 논리적 영역에 속하는지 선언하는 용도로 `namespace`를 쓰고, 코드 내부에서 관련 기능을 묶을 때 `module`을 씁니다.

## 실용 예제: 계층화된 설정

모듈의 가장 일반적인 사용 사례 중 하나는 관련 설정 값들을 그룹화하는 것입니다. 모듈 없이 `db_host`, `db_port`, `app_name`, `app_version`처럼 긴 접두사를 붙여야 했을 값들을, 모듈을 쓰면 `DB.host`, `App.version`처럼 짧고 명확하게 표현할 수 있습니다:

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

코드를 읽는 사람이 `DB.host`는 데이터베이스 설정이고 `App.version`은 애플리케이션 버전임을 이름만 봐도 알 수 있습니다. 모듈 이름이 문서 역할을 합니다.

## 참고 사항

- **들여쓰기 기반:** 모듈 본문은 들여쓰기로 구분되며, `end`나 `}`가 아님
- **위에서 아래 순서:** 모듈은 참조되기 전에 정의되어야 함 (순환 의존성 불가)
- **`module M =`** 은 중첩 모듈에 `=`을 사용; 최상위 `namespace`는 `=`이 없음
- **한정된 접근**은 값, 함수, 생성자에 대해 동작함
