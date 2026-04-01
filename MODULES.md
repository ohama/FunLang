# FunLang Module System & Multi-File Import

FunLang의 모듈 시스템과 파일 임포트 메커니즘 레퍼런스.

---

## 1. Module Declaration

### Basic Module

```fsharp
module Math =
    let double x = x * 2
    let square x = x * x

let result = Math.double 5    // 10
```

모듈 본문은 들여쓰기로 구분된다. `module Name = INDENT ... DEDENT` 형식.

### Multiple Modules in One File

```fsharp
module A =
    let x = 10

module B =
    let y = 20

let result = A.x + B.y    // 30
```

한 파일에 여러 모듈을 선언할 수 있다. 위에서 아래로 순서대로 처리되며, 뒤의 모듈은 앞의 모듈을 참조할 수 있다.

### Nested Modules

```fsharp
module Outer =
    module Inner =
        let value = 42

let result = Outer.Inner.value    // 42
```

모듈 안에 모듈을 중첩할 수 있다. 접근 시 점(`.`)으로 경로를 지정한다.

---

## 2. Qualified Access

모듈 멤버에 `Module.member` 형식으로 접근한다.

```fsharp
module Config =
    let maxRetries = 3

module StringUtils =
    let greet name = string_concat ["Hello, "; name; "!"]

let _ = println (to_string Config.maxRetries)       // 3
let _ = println (StringUtils.greet "World")          // Hello, World!
```

### What Can Be Accessed

| 대상 | 구문 | 예시 |
|------|------|------|
| 값/함수 | `M.name` | `Math.square 5` |
| ADT 생성자 | `M.Ctor` | `Color.Red` |
| 레코드 생성자 | `open M` 후 `{ field = v }` | 레코드는 `open` 필요 |
| 중첩 모듈 | `M.N.name` | `Outer.Inner.value` |

### Type Checking Mechanism

내부적으로 `Math.square 5`는 타입 체크 전에 `square 5`로 재작성(rewrite)된다. 이 과정에서:

1. `Math`가 모듈인지 확인
2. `square`가 값인지 생성자인지 판별
3. 해당 모듈의 타입 환경을 현재 스코프에 병합
4. 재작성된 식을 타입 체크

이 rewrite 덕분에 qualified access와 unqualified access가 동일한 타입 체크 경로를 공유한다.

---

## 3. Open Directive

### Module Open

`open`으로 모듈의 바인딩을 현재 스코프에 가져온다.

```fsharp
module M =
    let x = 42
    let f y = y + 1

open M

let result = x + f 10    // 53 (unqualified access)
let same = M.x + M.f 10  // 53 (qualified access도 여전히 동작)
```

`open` 후에도 qualified access는 유지된다.

### What `open` Brings Into Scope

| 대상 | open 후 사용 가능 | 예시 |
|------|-------------------|------|
| 값/함수 | O | `let r = square 5` |
| ADT 생성자 | O | `match x with Red -> ...` |
| 레코드 타입 | O | `{ x = 1; y = 2 }` |
| 예외 타입 | O | `raise (BadInput "err")` |
| 타입 클래스 | O (항상 전역) | `show 42` |
| 중첩 모듈 | X (qualified만) | `M.Inner.x` |

### Open Order Matters

```fsharp
module A =
    let x = 1

module B =
    let x = 2

open A
open B
let result = x    // 2 (B의 x가 A의 x를 shadow)
```

나중에 `open`한 모듈이 이전 바인딩을 shadow한다.

---

## 4. Module Contents

### ADT in Modules

```fsharp
module Color =
    type T =
        | Red
        | Green
        | Blue
    let toInt c =
        match c with
        | Red -> 1
        | Green -> 2
        | Blue -> 3

open Color
let result = toInt Green    // 2
```

### Records in Modules

```fsharp
module Geo =
    type Point = { x: int; y: int }
    let origin = { x = 0; y = 0 }

open Geo
let p = { x = 3; y = 4 }
let result = p.x + p.y    // 7
```

### GADT in Modules

```fsharp
module Typed =
    type Expr 'a =
        | IntLit : int -> int Expr
        | BoolLit : bool -> bool Expr

open Typed
let v = IntLit 42
```

### Exceptions in Modules

```fsharp
module Err =
    exception BadInput of string

open Err
let result =
    try raise (BadInput "oops")
    with BadInput msg -> msg    // "oops"
```

### Mutable State in Modules

```fsharp
module Counter =
    let mut value = 0
    let inc x = value <- value + 1
    let get x = value

let _ = Counter.inc 0
let _ = Counter.inc 0
let _ = println (to_string (Counter.get 0))    // 2
```

모듈 내부의 mutable 변수는 캡슐화되어 외부에서 직접 접근할 수 없고, 모듈 함수를 통해서만 조작할 수 있다.

### Type Classes in Modules

```fsharp
module MyShow =
    typeclass Show 'a =
        | show : 'a -> string

    instance Show int =
        let show x = to_string x

open MyShow
let _ = println (show 42)    // "42"
```

타입 클래스와 인스턴스는 모듈 내에 선언되더라도 `open` 시 전역 스코프로 승격된다. `ClassEnv`와 `InstanceEnv`가 자동으로 외부 스코프에 전파된다.

---

## 5. File Import

### Basic Syntax

```fsharp
// lib.fun
let add x y = x + y
let mul x y = x * y
```

```fsharp
// main.fun
open "lib.fun"
let result = add 3 (mul 2 5)    // 13
```

`open "path.fun"` 구문으로 외부 파일을 임포트한다.

### Path Resolution

| 경로 형태 | 해석 | 예시 |
|-----------|------|------|
| 상대 경로 | **임포트하는 파일** 기준 | `open "lib.fun"` |
| 절대 경로 | 그대로 사용 | `open "/usr/lib/utils.fun"` |
| 중첩 상대 | 임포트 체인 각각 기준 | `open "subdir/inner.fun"` |

**중요:** 상대 경로는 CWD가 아닌 **임포트하는 파일의 디렉토리** 기준이다.

```
project/
├── main.fun          # open "lib/utils.fun"
└── lib/
    ├── utils.fun     # open "helper.fun"  ← lib/ 기준
    └── helper.fun
```

### Imported Module Access

임포트한 파일에 모듈이 있으면 qualified access 가능:

```fsharp
// math.fun
module Math =
    let square x = x * x
```

```fsharp
// main.fun
open "math.fun"
let result = Math.square 7    // 49
```

### Import Cache

동일 파일을 여러 곳에서 임포트해도 한 번만 타입 체크/평가한다:

```
      main.fun
      /      \
  a.fun    b.fun
      \      /
      shared.fun    ← 한 번만 로드 (diamond dependency safe)
```

캐시는 `tcCache` (타입 체크)와 `evalCache` (평가)로 분리 관리된다. 각 파일은 **자신의 export만** 캐시에 저장하므로 caller의 환경이 오염되지 않는다.

### Cycle Detection

순환 임포트는 자동 감지된다:

```fsharp
// a.fun
open "b.fun"
let x = 1

// b.fun
open "a.fun"    // ← Error: Circular module dependency
let y = 2
```

내부적으로 `fileLoadingStack` (HashSet)에 현재 로딩 중인 파일을 추적하여, 이미 로딩 중인 파일을 다시 임포트하면 **E0501** 에러를 발생시킨다.

---

## 6. Prelude System

### Automatic Loading

FunLang 실행 시 `Prelude/` 디렉토리의 모든 `*.fun` 파일이 자동 로드된다.

**현재 Prelude 모듈 (13개):**

| 모듈 | 파일 | 주요 기능 |
|------|------|-----------|
| Core | `Core.fun` | `id`, `not`, `min`, `max`, `abs`, `fst`, `snd`, `ignore` |
| List | `List.fun` | `map`, `filter`, `fold`, `length`, `sort`, `tryFind`, ... |
| Option | `Option.fun` | `None`, `Some`, `optionIter`, `optionFilter`, ... |
| Result | `Result.fun` | `Ok`, `Error`, `resultIter`, `resultToOption`, ... |
| Array | `Array.fun` | `Array.create`, `Array.map`, `Array.fold`, ... |
| Hashtable | `Hashtable.fun` | `Hashtable.create`, `Hashtable.set`, ... |
| String | `String.fun` | `String.length`, `String.endsWith`, `String.trim`, ... |
| Char | `Char.fun` | `Char.IsDigit`, `Char.ToUpper`, `Char.IsLetter`, ... |
| HashSet | `HashSet.fun` | `HashSet.create`, `HashSet.add`, ... |
| Queue | `Queue.fun` | `Queue.create`, `Queue.enqueue`, ... |
| MutableList | `MutableList.fun` | `MutableList.create`, `MutableList.add`, ... |
| StringBuilder | `StringBuilder.fun` | `StringBuilder.create`, `StringBuilder.add`, ... |
| Typeclass | `Typeclass.fun` | `Show`/`Eq` 내장 인스턴스 (int, bool, string, char) |

### Prelude Path Priority

1. `--prelude` CLI 플래그
2. `LANGTHREE_PRELUDE` 환경 변수
3. `funproj.toml`의 `[project].prelude` 설정
4. Auto-discovery: CWD → assembly 디렉토리 → 상위 6레벨까지 탐색

### Load Order

Prelude 파일은 **생성자 의존성 기반 위상 정렬**로 로드된다:

1. 각 파일에서 선언된 생성자(`[A-Z]`로 시작하는 식별자) 추출
2. 각 파일에서 참조하는 생성자 식별
3. 의존성 그래프 구성: 파일 → 참조하는 생성자를 선언한 파일들
4. 위상 정렬 (알파벳 순서로 동률 해결)

예: `List.fun`이 `Some`/`None`을 사용하면, `Option.fun`이 먼저 로드된다.

### Qualified vs Unqualified Access

Prelude 모듈은 자동으로 `open`되므로 두 가지 방식 모두 사용 가능:

```fsharp
// Unqualified (Prelude가 open되어 있으므로)
let xs = map (fun x -> x + 1) [1; 2; 3]

// Qualified (모듈 명시)
let ys = List.map (fun x -> x + 1) [1; 2; 3]
```

---

## 7. Module Export Rules

### Export Filtering

모듈은 **자신이 새로 추가한 바인딩만** export한다:

```fsharp
module M =
    let x = 42        // M.x로 접근 가능
    let f y = y + x   // M.f로 접근 가능
    // println, to_string 등 외부 바인딩은 export되지 않음
```

내부 구현: `ModuleExports`에는 외부 스코프에 이미 존재하는 바인딩을 제외한 **새로운 바인딩만** 포함된다. 이를 통해 모듈 re-export를 방지한다.

### ModuleExports 구조

```fsharp
type ModuleExports = {
    TypeEnv: TypeEnv                         // 타입 바인딩
    CtorEnv: ConstructorEnv                  // ADT/레코드/예외 생성자
    RecEnv: RecordEnv                        // 레코드 타입 정의
    ClassEnv: ClassEnv                       // 타입 클래스 정의
    InstanceEnv: InstanceEnv                 // 타입 클래스 인스턴스
    SubModules: Map<string, ModuleExports>   // 중첩 모듈
}
```

### Type Class Global Propagation

타입 클래스와 인스턴스는 모듈 경계를 넘어 전역으로 전파된다:

```fsharp
module M =
    instance Show MyType =
        let show x = "MyType"

// open M 없이도 show (value : MyType) 호출 가능
// 인스턴스는 모듈 처리 시 자동으로 외부 ClassEnv/InstanceEnv에 추가됨
```

---

## 8. Module Errors

| 코드 | 에러 | 발생 조건 | 예시 |
|------|------|-----------|------|
| **E0501** | Circular Module Dependency | 모듈 간 순환 `open` | `A opens B, B opens A` |
| **E0502** | Unresolved Module | 존재하지 않는 모듈 참조 | `open NotExist` / `X.y` |
| **E0503** | Duplicate Module Name | 같은 이름의 모듈 중복 선언 | `module M = ...` 두 번 |
| **E0504** | Forward Module Reference | 정의 전 모듈 참조 | 순서 위반 |

### E0502 with "Did you mean?"

```
error[E0502]: Unresolved module: Mth
   = hint: Did you mean 'Math'?
```

편집 거리(Levenshtein) 기반으로 유사한 모듈 이름을 제안한다.

---

## 9. IndentFilter and Module Bodies

모듈 본문은 `InModule` 컨텍스트에서 처리된다:

```
module Math =         ← MODULE IDENT EQUALS → InModule 컨텍스트 push
    let square x =    ← INDENT, InLetDecl 컨텍스트 push
        x * x         ← 본문
                      ← DEDENT (let body 종료)
    let double x =    ← 같은 들여쓰기 레벨의 다음 선언
        x + x
                      ← DEDENT (모듈 본문 종료), InModule pop
let result = ...      ← 모듈 바깥 코드
```

`InModule` 컨텍스트에서는:
- implicit `in` 삽입이 **비활성화**됨 (모듈은 선언 시퀀스, 식이 아님)
- SEMICOLON 주입은 `InExprBlock`에서만 작동하므로 모듈 본문에 영향 없음
- 빈 줄은 무시됨 (모듈 본문 중간에 빈 줄 허용)

---

## 10. Runtime Module Evaluation

### ModuleValueEnv

런타임에서 모듈은 `ModuleValueEnv` 구조체로 저장된다:

```fsharp
type ModuleValueEnv = {
    Values: Map<string, Value>               // 값 바인딩
    CtorEnv: Map<string, Value>              // 생성자
    RecEnv: Map<string, obj>                 // 레코드 정보
    SubModules: Map<string, ModuleValueEnv>  // 중첩 모듈
}
```

### Qualified Access Resolution

`Math.square 5` 런타임 평가:

1. `Math`를 `moduleEnv`에서 조회 → `ModuleValueEnv` 획득
2. `square`를 `modValEnv.Values`에서 조회 → `ClosureValue` 획득
3. 클로저에 `5` 적용 → 결과 반환

중첩 접근 `A.B.c`:

1. `A`를 `moduleEnv`에서 조회
2. `B`를 `A.SubModules`에서 조회
3. `c`를 `B.Values`에서 조회

---

## 11. Complete Example

```fsharp
// geometry.fun
module Geometry =
    type Shape =
        | Circle of int
        | Rect of int * int

    let area s =
        match s with
        | Circle r -> r * r * 3
        | Rect (w, h) -> w * h

    let describe s =
        match s with
        | Circle r -> string_concat ["Circle("; to_string r; ")"]
        | Rect (w, h) -> string_concat ["Rect("; to_string w; ","; to_string h; ")"]
```

```fsharp
// main.fun
open "geometry.fun"
open Geometry

let shapes = [Circle 5; Rect (3, 4)]

let _ = for s in shapes do
    let a = area s
    let d = describe s
    printfn "%s -> area=%d" d a
```

출력:

```
Circle(5) -> area=75
Rect(3,4) -> area=12
```

---

## Test Coverage

| 카테고리 | 파일 수 | 위치 |
|----------|---------|------|
| Core module tests | 14 | `tests/flt/file/module/` |
| File import tests | 4 | `tests/flt/file/import/` |
| Advanced module tests | 6+ | `tests/flt/file/{offside,mutable,char}/` |
| F# unit tests | 25+ | `tests/FunLang.Tests/ModuleTests.fs` |
| Tutorial | 1 chapter | `tutorial/10-modules.md` |

---

*Source: `src/FunLang/TypeCheck.fs`, `src/FunLang/Eval.fs`, `src/FunLang/Prelude.fs`, `src/FunLang/Ast.fs`*
*Last updated: 2026-04-01*
