# FunLang Module System Design

## 목표

FunLang에 모듈 시스템을 도입하여 코드 재사용, 네임스페이스 분리, 캡슐화를 지원한다.

**현재 상태:**
- 단일 파일 기반 프로그램 (`Program = TypeDefs + MainExpr`)
- 모든 정의가 전역 스코프에 존재
- import/export 메커니즘 없음
- 정규화된 이름(qualified names) 미지원

**목표 상태:**
```funlang
module Math =
  export add, multiply, PI

  let PI = 3.14159
  let add x y = x + y
  let multiply x y = x * y
  let _helper x = x * 2  // private

open Math
let result = add 1 2      // 3
let pi = Math.PI          // 3.14159 (qualified access)
```

---

## 설계 접근 방식: Haskell-Inspired + F#-Style

### 선택 이유

| 언어 | 장점 | 단점 | FunLang 적합성 |
|------|------|------|---------------|
| **OCaml** | 강력한 펑터, 시그니처 | 복잡함 | △ 교육용에 과함 |
| **Haskell** | 명시적 export, 간단한 구조 | 중첩 모듈 없음 | ◎ 가장 적합 |
| **F#** | 파일 기반, .NET 통합 | 시그니처 없음 | ○ 참고 |
| **Lua** | 최소한, 동적 | 타입 안전성 낮음 | △ 너무 동적 |
| **ES6** | 현대적, 익숙함 | FP스럽지 않음 | △ 스타일 불일치 |

**결론:** Haskell 스타일 명시적 export + F# 스타일 로컬 모듈 조합

---

## Part 1: 문법 설계 (Syntax Design)

### 1.1 모듈 선언

```funlang
// 기본 모듈 선언
module ModuleName =
  export item1, item2, TypeName

  // 모듈 내용
  type TypeName = ...
  let item1 = ...
  let item2 = ...
  let privateItem = ...  // export에 없으면 private

// 중첩 모듈
module Outer =
  export Inner, value

  let value = 42

  module Inner =
    export innerValue
    let innerValue = 100
```

### 1.2 Export 선언

```funlang
// 값 export
export add, multiply, value

// 타입 export (생성자 포함)
export type Option           // 타입 이름만 (opaque)
export type Option(..)       // 타입 + 모든 생성자
export type Option(Some)     // 타입 + 일부 생성자

// 모든 것 export (권장하지 않음)
export *

// 여러 줄 export
export
  add
  multiply
  type Option(..)
```

### 1.3 Import/Open 선언

```funlang
// 전체 모듈 열기 (모든 export를 스코프에 추가)
open Math

// 선택적 import
import Math (add, multiply)

// 정규화된 import (별칭)
import qualified Math as M
let result = M.add 1 2

// 특정 항목 숨기기
open Math hiding (PI)

// 조합
import Math (add)           // add만 가져옴
open Math hiding (multiply) // multiply 제외 전부
```

### 1.4 정규화된 이름 (Qualified Names)

```funlang
// 점(.) 표기법
Math.add 1 2
Outer.Inner.value
Option.Some 42

// 중첩 접근
let x = MyModule.SubModule.function arg
```

### 1.5 들여쓰기 기반 문법

```funlang
// 모듈 본문은 들여쓰기로 구분
module Math =
  export add, multiply

  let add x y = x + y

  let multiply x y = x * y

  // 중첩 모듈도 들여쓰기
  module Constants =
    export PI, E

    let PI = 3.14159
    let E = 2.71828

// 모듈 끝은 dedent로 자동 감지
let mainExpr = Math.add 1 2
```

---

## Part 2: AST 설계 (Abstract Syntax Tree)

### 2.1 새로운 타입 정의

```fsharp
// src/FunLang/Ast.fs에 추가

/// 가시성 (Visibility)
type Visibility =
    | Public    // export됨
    | Private   // 모듈 내부에서만 접근 가능

/// 정규화된 경로 (Qualified Path)
type QualifiedPath = string list
// 예: ["Math"; "add"] = Math.add
//     ["Outer"; "Inner"; "value"] = Outer.Inner.value

/// Export 항목
type ExportItem =
    | ExportValue of string                          // export add
    | ExportType of string * ExportTypeMode          // export type Option(..)
    | ExportModule of string                         // export SubModule
    | ExportAll                                      // export *

and ExportTypeMode =
    | OpaqueType                    // 타입 이름만 (생성자 숨김)
    | AllConstructors               // 모든 생성자 포함
    | SomeConstructors of string list  // 일부 생성자만

/// Import 선언
type ImportDecl =
    | OpenModule of QualifiedPath                    // open Math
    | ImportItems of QualifiedPath * string list     // import Math (add, multiply)
    | ImportQualified of QualifiedPath * string      // import qualified Math as M
    | ImportHiding of QualifiedPath * string list    // open Math hiding (PI)

/// 모듈 항목 (Module Item)
type ModuleItem =
    | MIValue of name: string * value: LExpr * visibility: Visibility
    | MIRecValue of name: string * value: LExpr * visibility: Visibility
    | MIType of TypeDef * visibility: Visibility
    | MIModule of ModuleDecl

/// 모듈 선언 (Module Declaration)
and ModuleDecl = {
    Name: string
    Exports: ExportItem list option  // None = export nothing, Some [] with ExportAll = export all
    Imports: ImportDecl list
    Items: ModuleItem list
    Pos: Position
}

/// 프로그램 (확장된 Program)
type Program = {
    Modules: ModuleDecl list         // 최상위 모듈들
    Imports: ImportDecl list         // 프로그램 레벨 import
    TypeDefs: TypeDef list           // 레거시: 모듈 밖 타입 정의
    MainExpr: LExpr option           // 메인 표현식
}
```

### 2.2 표현식 확장

```fsharp
/// 표현식 (확장)
type Expr =
    // ... 기존 표현식들 ...
    | EVariable of string                 // 기존: 단순 이름
    | EQualifiedVar of QualifiedPath      // 신규: Math.add
    | EQualifiedCons of QualifiedPath     // 신규: Option.Some
```

### 2.3 패턴 확장

```fsharp
/// 패턴 (확장)
type Pattern =
    // ... 기존 패턴들 ...
    | PConstructor of string * LPattern option           // 기존
    | PQualifiedCons of QualifiedPath * LPattern option  // 신규: Option.Some x
```

---

## Part 3: Lexer 확장 (Lexer.fsl)

### 3.1 새로운 키워드

```fsl
// 키워드 추가
| "module"    { MODULE }
| "export"    { EXPORT }
| "import"    { IMPORT }
| "open"      { OPEN }
| "qualified" { QUALIFIED }
| "as"        { AS }
| "hiding"    { HIDING }
| "private"   { PRIVATE }
```

### 3.2 점(.) 연산자 처리

```fsl
// 현재: 점이 없음
// 추가 필요:

// 옵션 1: 단순 DOT 토큰
| "."         { DOT }

// 옵션 2: 문맥 기반 (대문자 시작이면 모듈 경로)
// Math.add → [UIDENT "Math"; DOT; LIDENT "add"]
// x.field  → [LIDENT "x"; DOT; LIDENT "field"]  // 추후 레코드용
```

### 3.3 토큰 정의 (Parser.fsy)

```fsy
%token MODULE EXPORT IMPORT OPEN QUALIFIED AS HIDING PRIVATE
%token DOT
%token <string> UIDENT  // 대문자 시작 식별자 (모듈/타입명)
%token <string> LIDENT  // 소문자 시작 식별자 (값/함수명)
```

---

## Part 4: Parser 확장 (Parser.fsy)

### 4.1 최상위 문법

```fsy
prog:
    | program EOF                          { $1 }

program:
    | module_decls top_level_body          { { Modules = $1; MainExpr = Some $2; ... } }
    | module_decls                         { { Modules = $1; MainExpr = None; ... } }
    | imports top_level_body               { { Imports = $1; MainExpr = Some $2; ... } }
    | top_level_body                       { { MainExpr = Some $1; ... } }  // 레거시
```

### 4.2 모듈 선언 문법

```fsy
module_decls:
    | module_decl                          { [$1] }
    | module_decl module_decls             { $1 :: $2 }

module_decl:
    | MODULE UIDENT EQ INDENT module_body DEDENT
                                           { { Name = $2; ... } }

module_body:
    | export_decl imports module_items     { ($1, $2, $3) }
    | export_decl module_items             { ($1, [], $2) }
    | imports module_items                 { (None, $1, $2) }
    | module_items                         { (None, [], $1) }

export_decl:
    | EXPORT export_list NEWLINE           { Some $2 }

export_list:
    | export_item                           { [$1] }
    | export_item COMMA export_list         { $1 :: $3 }

export_item:
    | LIDENT                                { ExportValue $1 }
    | UIDENT                                { ExportModule $1 }
    | TYPE UIDENT                           { ExportType($2, OpaqueType) }
    | TYPE UIDENT LPAREN DOTDOT RPAREN      { ExportType($2, AllConstructors) }
    | TYPE UIDENT LPAREN cons_list RPAREN   { ExportType($2, SomeConstructors $4) }
    | STAR                                  { ExportAll }
```

### 4.3 Import 문법

```fsy
imports:
    | import_decl                          { [$1] }
    | import_decl imports                  { $1 :: $2 }

import_decl:
    | OPEN qualified_path NEWLINE          { OpenModule $2 }
    | IMPORT qualified_path LPAREN import_items RPAREN NEWLINE
                                           { ImportItems($2, $4) }
    | IMPORT QUALIFIED qualified_path AS UIDENT NEWLINE
                                           { ImportQualified($3, $5) }
    | OPEN qualified_path HIDING LPAREN import_items RPAREN NEWLINE
                                           { ImportHiding($2, $5) }

qualified_path:
    | UIDENT                               { [$1] }
    | UIDENT DOT qualified_path            { $1 :: $3 }
```

### 4.4 정규화된 이름 문법

```fsy
// 표현식에서 정규화된 변수
simple_expr:
    | LIDENT                               { EVariable $1 }
    | qualified_path DOT LIDENT            { EQualifiedVar ($1 @ [$3]) }
    | qualified_path DOT UIDENT            { EQualifiedCons ($1 @ [$3]) }

// 패턴에서 정규화된 생성자
pattern:
    | UIDENT                               { PConstructor($1, None) }
    | UIDENT pattern                       { PConstructor($1, Some $2) }
    | qualified_path DOT UIDENT            { PQualifiedCons($1 @ [$3], None) }
    | qualified_path DOT UIDENT pattern    { PQualifiedCons($1 @ [$3], Some $4) }
```

---

## Part 5: 타입 시스템 확장 (TypeInfer.fs)

### 5.1 모듈 환경

```fsharp
/// 모듈 환경 (Module Environment)
type ModuleEnv = {
    Types: Map<string, TypeScheme>       // 타입 환경
    Values: Map<string, TypeScheme>      // 값 환경
    Constructors: Map<string, ConstructorInfo>  // 생성자
    SubModules: Map<string, ModuleEnv>   // 중첩 모듈
    Exports: Set<string>                 // export된 이름들
}

/// 전역 환경
type GlobalEnv = {
    Modules: Map<string, ModuleEnv>      // 최상위 모듈들
    CurrentScope: ModuleEnv              // 현재 스코프 (open된 것들 포함)
}
```

### 5.2 이름 해석 (Name Resolution)

```fsharp
/// 정규화된 이름 해석
let resolveQualifiedName (path: QualifiedPath) (env: GlobalEnv) : TypeScheme option =
    match path with
    | [name] ->
        // 단순 이름: 현재 스코프에서 검색
        Map.tryFind name env.CurrentScope.Values
    | moduleName :: rest ->
        // 정규화된 이름: 모듈 체인 따라가기
        match Map.tryFind moduleName env.Modules with
        | Some moduleEnv -> resolveInModule rest moduleEnv
        | None -> None

let rec resolveInModule (path: QualifiedPath) (moduleEnv: ModuleEnv) : TypeScheme option =
    match path with
    | [name] ->
        // export 확인
        if Set.contains name moduleEnv.Exports then
            Map.tryFind name moduleEnv.Values
        else
            None  // private
    | subModuleName :: rest ->
        match Map.tryFind subModuleName moduleEnv.SubModules with
        | Some subEnv -> resolveInModule rest subEnv
        | None -> None
    | [] -> None
```

### 5.3 Open 처리

```fsharp
/// open 문 처리: 모듈의 export를 현재 스코프에 병합
let processOpen (modulePath: QualifiedPath) (env: GlobalEnv) : GlobalEnv =
    match resolveModule modulePath env with
    | Some moduleEnv ->
        // export된 항목만 현재 스코프에 추가
        let exported =
            moduleEnv.Values
            |> Map.filter (fun name _ -> Set.contains name moduleEnv.Exports)
        let newScope = {
            env.CurrentScope with
                Values = Map.fold (fun acc k v -> Map.add k v acc) env.CurrentScope.Values exported
                // Types, Constructors도 동일하게 처리
        }
        { env with CurrentScope = newScope }
    | None ->
        // 에러: 모듈을 찾을 수 없음
        env
```

### 5.4 타입 추론 확장

```fsharp
/// 모듈 타입 추론
let inferModule (moduleDecl: ModuleDecl) (env: GlobalEnv) : Result<ModuleEnv, TypeError> =
    // 1. import 처리
    let envWithImports =
        moduleDecl.Imports
        |> List.fold processImport env

    // 2. 각 항목 타입 추론
    let rec inferItems items (acc: ModuleEnv) =
        match items with
        | [] -> Ok acc
        | MIValue(name, expr, vis) :: rest ->
            match infer envWithImports.CurrentScope.Values expr with
            | Ok scheme ->
                let acc' = { acc with Values = Map.add name scheme acc.Values }
                inferItems rest acc'
            | Error e -> Error e
        | MIType(typeDef, vis) :: rest ->
            // 타입 정의 처리
            inferItems rest acc
        | MIModule(subModule) :: rest ->
            // 중첩 모듈 재귀 처리
            match inferModule subModule env with
            | Ok subEnv ->
                let acc' = { acc with SubModules = Map.add subModule.Name subEnv acc.SubModules }
                inferItems rest acc'
            | Error e -> Error e

    // 3. export 목록 생성
    let exports = computeExports moduleDecl.Exports items

    inferItems moduleDecl.Items { emptyModuleEnv with Exports = exports }
```

---

## Part 6: 인터프리터 확장 (Interpreter.fs)

### 6.1 런타임 모듈 값

```fsharp
/// 값 (확장)
type Value =
    // ... 기존 값들 ...
    | VModule of ModuleValue      // 모듈 값

/// 모듈 런타임 값
and ModuleValue = {
    Values: Map<string, Value>
    SubModules: Map<string, ModuleValue>
    Exports: Set<string>
}

/// 런타임 환경 (확장)
type RuntimeEnv = {
    Modules: Map<string, ModuleValue>
    CurrentScope: Map<string, Value>
}
```

### 6.2 정규화된 이름 평가

```fsharp
/// 정규화된 변수 평가
let evalQualifiedVar (path: QualifiedPath) (env: RuntimeEnv) : EvalResult =
    match path with
    | [name] ->
        // 단순 이름
        match Map.tryFind name env.CurrentScope with
        | Some v -> Ok v
        | None -> Error (unboundVariable name)
    | moduleName :: rest ->
        // 모듈 경로 따라가기
        match Map.tryFind moduleName env.Modules with
        | Some moduleVal -> evalInModule rest moduleVal
        | None -> Error (unboundModule moduleName)

let rec evalInModule (path: QualifiedPath) (moduleVal: ModuleValue) : EvalResult =
    match path with
    | [name] ->
        if Set.contains name moduleVal.Exports then
            match Map.tryFind name moduleVal.Values with
            | Some v -> Ok v
            | None -> Error (unboundVariable name)
        else
            Error (privateAccess name)
    | subName :: rest ->
        match Map.tryFind subName moduleVal.SubModules with
        | Some sub -> evalInModule rest sub
        | None -> Error (unboundModule subName)
    | [] -> Error (invalidPath)
```

### 6.3 모듈 평가

```fsharp
/// 모듈 평가
let evalModule (moduleDecl: ModuleDecl) (env: RuntimeEnv) : Result<ModuleValue, RuntimeError> =
    // 1. import 처리로 환경 확장
    let envWithImports =
        moduleDecl.Imports
        |> List.fold evalImport env

    // 2. 각 항목 평가
    let rec evalItems items (acc: ModuleValue) =
        match items with
        | [] -> Ok acc
        | MIValue(name, expr, _) :: rest ->
            match eval envWithImports.CurrentScope expr with
            | Ok value ->
                let acc' = { acc with Values = Map.add name value acc.Values }
                evalItems rest acc'
            | Error e -> Error e
        // ... 나머지 처리 ...

    evalItems moduleDecl.Items emptyModuleValue
```

---

## Part 7: 패턴 분석 확장 (PatternAnalysis.fs)

### 7.1 모듈 인식 레지스트리

```fsharp
/// 타입 정의 레지스트리 (모듈 인식)
type ModuleAwareRegistry = {
    GlobalTypes: Map<string, TypeDefInfo>
    ModuleTypes: Map<QualifiedPath, TypeDefInfo>
}

/// 생성자 조회 (정규화된 이름 지원)
let lookupConstructor (path: QualifiedPath) (registry: ModuleAwareRegistry) : ConstructorInfo option =
    match path with
    | [name] ->
        // 전역에서 검색
        Map.tryFind name registry.GlobalTypes
        |> Option.bind (fun info ->
            info.Constructors |> List.tryFind (fun c -> c.Name = name))
    | _ ->
        // 모듈 경로로 검색
        let modulePath = List.take (List.length path - 1) path
        let consName = List.last path
        Map.tryFind modulePath registry.ModuleTypes
        |> Option.bind (fun info ->
            info.Constructors |> List.tryFind (fun c -> c.Name = consName))
```

---

## Part 8: 에러 메시지

### 8.1 새로운 에러 타입

```fsharp
type FunLangError =
    // ... 기존 에러들 ...

    // 모듈 관련 에러
    | UnboundModule of string * Position
    | PrivateAccess of string * string * Position    // item, module, position
    | AmbiguousName of string * QualifiedPath list * Position
    | CircularImport of QualifiedPath list
    | ExportNotFound of string * string * Position   // item, module
    | DuplicateModule of string * Position
    | InvalidQualifiedPath of QualifiedPath * Position
```

### 8.2 에러 메시지 포맷

```
error[E401]: Unbound module: Math
  --> example.fun:5:1
  |
5 | open Math
  | ^^^^^^^^^
  = help: Did you mean to import a module first?

error[E402]: Cannot access private member 'helper' in module 'Math'
  --> example.fun:10:5
   |
10 | let x = Math.helper 42
   |         ^^^^^^^^^^^
   = info: 'helper' is not exported from module 'Math'

error[E403]: Ambiguous name 'add'
  --> example.fun:15:9
   |
15 | let x = add 1 2
   |         ^^^
   = info: 'add' is defined in multiple modules: Math, Utils
   = help: Use qualified name: Math.add or Utils.add
```

---

## Part 9: 파일 시스템 통합 (Phase 2)

### 9.1 파일 기반 모듈 (향후)

```
project/
├── main.fun           → 암묵적 Main 모듈
├── math.fun           → 암묵적 Math 모듈
└── utils/
    ├── string.fun     → Utils.String 모듈
    └── list.fun       → Utils.List 모듈
```

### 9.2 모듈 로더

```fsharp
/// 모듈 로더 (Phase 2)
type ModuleLoader = {
    SearchPaths: string list
    LoadedModules: Map<QualifiedPath, ModuleDecl>
    LoadModule: QualifiedPath -> Result<ModuleDecl, LoadError>
}

let loadModule (path: QualifiedPath) (loader: ModuleLoader) : Result<ModuleDecl, LoadError> =
    // 1. 캐시 확인
    match Map.tryFind path loader.LoadedModules with
    | Some m -> Ok m
    | None ->
        // 2. 파일 시스템에서 검색
        let filePath = resolveModulePath path loader.SearchPaths
        match parseFile filePath with
        | Ok moduleDecl ->
            // 3. 캐시에 저장
            loader.LoadedModules <- Map.add path moduleDecl loader.LoadedModules
            Ok moduleDecl
        | Error e -> Error e
```

---

## Part 10: 구현 로드맵

### Phase 1: 최소 기능 (MVP)

| 단계 | 작업 | 파일 |
|------|------|------|
| 1.1 | AST 확장 (ModuleDecl, ImportDecl, QualifiedPath) | Ast.fs |
| 1.2 | Lexer 토큰 추가 (module, export, open, import, DOT) | Lexer.fsl |
| 1.3 | Parser 문법 추가 (module_decl, imports, qualified_path) | Parser.fsy |
| 1.4 | 단순 이름 해석 구현 | NameResolution.fs (신규) |
| 1.5 | 타입 추론 확장 (ModuleEnv, resolveQualified) | TypeInfer.fs |
| 1.6 | 인터프리터 확장 (VModule, evalQualified) | Interpreter.fs |
| 1.7 | 기본 테스트 | ModuleTests.fs |

### Phase 2: 완전 기능

| 단계 | 작업 |
|------|------|
| 2.1 | 중첩 모듈 지원 |
| 2.2 | 선택적 import (import Math (add)) |
| 2.3 | qualified import (import qualified Math as M) |
| 2.4 | hiding 지원 |
| 2.5 | 패턴 분석 모듈 인식 |
| 2.6 | 에러 메시지 개선 |

### Phase 3: 파일 시스템

| 단계 | 작업 |
|------|------|
| 3.1 | 파일 기반 모듈 로더 |
| 3.2 | 디렉토리 → 모듈 계층 |
| 3.3 | 순환 의존성 검사 |
| 3.4 | 증분 컴파일 |

---

## Part 11: 예제 프로그램

### 11.1 단일 모듈

```funlang
module Math =
  export add, multiply, square

  let add x y = x + y
  let multiply x y = x * y
  let square x = multiply x x
  let _helper x = x  // private

open Math

let result = add (square 3) (multiply 2 4)
// result = 9 + 8 = 17
```

### 11.2 다중 모듈

```funlang
module Option =
  export type Option(..), map, getOrElse

  type Option 'a = None | Some of 'a

  let map f opt =
    match opt with
    | None -> None
    | Some x -> Some (f x)

  let getOrElse default opt =
    match opt with
    | None -> default
    | Some x -> x

module List =
  export map, filter, fold

  let rec map f xs =
    match xs with
    | [] -> []
    | h :: t -> f h :: map f t

  let rec filter pred xs =
    match xs with
    | [] -> []
    | h :: t ->
        if pred h then h :: filter pred t
        else filter pred t

  let rec fold f acc xs =
    match xs with
    | [] -> acc
    | h :: t -> fold f (f acc h) t

open Option
open List

let numbers = [1; 2; 3; 4; 5]
let doubled = map (fun x -> x * 2) numbers
let sumOpt = Some (fold (fun a b -> a + b) 0 doubled)
let result = getOrElse 0 sumOpt
// result = 30
```

### 11.3 정규화된 접근

```funlang
module A =
  export value
  let value = 1

module B =
  export value
  let value = 2

// 명시적 정규화 필요 (이름 충돌)
let sum = A.value + B.value
// sum = 3

// 또는 선택적 import
import A (value)
let x = value + B.value
// x = 3
```

---

## Part 12: 검증 방법

### 12.1 단위 테스트

```fsharp
// tests/FunLang.Tests/ModuleTests.fs

let moduleTests = testList "Module System" [
    testList "Parsing" [
        test "parses simple module" {
            let input = "module M =\n  export x\n  let x = 1"
            let result = parseProgram input
            Expect.isOk result "should parse"
        }

        test "parses qualified name" {
            let input = "let x = Math.add 1 2"
            let result = parseExpr input
            Expect.isOk result "should parse qualified"
        }
    ]

    testList "Type Inference" [
        test "resolves qualified type" {
            let input = "module M =\n  export f\n  let f x = x\n\nM.f 42"
            let result = inferProgram input
            Expect.equal (Result.map fst result) (Ok TInt) "should infer int"
        }
    ]

    testList "Evaluation" [
        test "evaluates module value" {
            let input = "module M =\n  export x\n  let x = 42\n\nM.x"
            let result = evalProgram input
            Expect.equal result (Ok (VInt 42)) "should be 42"
        }
    ]
]
```

### 12.2 파일 기반 테스트

```
// tests/file-tests/module-tests/001-simple-module.test
// --COMMAND: dotnet run --project src/FunLang -- %s
// --INPUT
module Math =
  export add
  let add x y = x + y

open Math
add 1 2

// --EXPECTED
3
```

### 12.3 에러 테스트

```
// tests/file-tests/module-tests/100-private-access.test
// --COMMAND: dotnet run --project src/FunLang -- %s
// --INPUT
module M =
  export public_fn
  let public_fn x = x
  let private_fn x = x * 2

M.private_fn 5

// --EXPECTED-ERROR
error[E402]: Cannot access private member
```

---

## 설계 결정 요약

| 결정 사항 | 선택 | 이유 |
|----------|------|------|
| **모듈 문법** | `module Name = ... end` | F# 스타일, 들여쓰기 친화적 |
| **Export 방식** | 명시적 export 리스트 | 의도적 API 설계 유도 |
| **Import 방식** | `open`, `import`, `qualified` | Haskell 유연성 |
| **정규화 이름** | 점(.) 표기 | 익숙하고 간결 |
| **가시성** | export 여부로 결정 | 단순, 명확 |
| **중첩 모듈** | Phase 2에서 지원 | 점진적 복잡도 |
| **파일 기반** | Phase 3에서 지원 | 먼저 단일 파일에서 검증 |

---

## 주요 파일 변경 목록

| 파일 | 변경 유형 | 내용 |
|------|----------|------|
| `src/FunLang/Ast.fs` | 수정 | ModuleDecl, ImportDecl, QualifiedPath 추가 |
| `src/FunLang/Lexer.fsl` | 수정 | 키워드, DOT 토큰 추가 |
| `src/FunLang/Parser.fsy` | 수정 | 모듈/import 문법 추가 |
| `src/FunLang/NameResolution.fs` | **신규** | 이름 해석 모듈 |
| `src/FunLang/TypeInfer.fs` | 수정 | ModuleEnv, 정규화 이름 해석 |
| `src/FunLang/Interpreter.fs` | 수정 | VModule, 정규화 평가 |
| `src/FunLang/PatternAnalysis.fs` | 수정 | 모듈 인식 레지스트리 |
| `src/FunLang/Errors.fs` | 수정 | 모듈 관련 에러 타입 |
| `tests/FunLang.Tests/ModuleTests.fs` | **신규** | 모듈 테스트 |
