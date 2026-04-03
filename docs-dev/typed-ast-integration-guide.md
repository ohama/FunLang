## v11.0 Typed AST Export — 구현 완료, FunLangCompiler 통합 가이드

v11.0에서 FunLang의 HM 타입 추론 결과를 export하는 기능이 구현되었습니다. 아래는 FunLangCompiler에서 이를 활용하기 위한 설계/구현 가이드입니다.

---

### 1. 개요: 무엇이 구현되었나

| 컴포넌트 | 파일 | 역할 |
|----------|------|------|
| **TypeAnnotationMap** | `TypeAnnotationMap.fs` | `ConcurrentDictionary<Span, Type>` — per-expression 타입 기록 |
| **Bidir.synth 연동** | `Bidir.fs` | 모든 Expr 노드의 추론된 타입을 map에 기록 (52 variants) |
| **ExportApi** | `ExportApi.fs` | `typeCheckFile` 진입점 → `TypedModule` record 반환 |
| **CLI flag** | `Program.fs` | `--emit-typed-ast` — JSON 형식으로 stdout 출력 |

핵심: **새로운 AST를 만들지 않았습니다.** 기존 AST는 그대로이고, 별도 `Dictionary<Span, Type>` 맵에 각 노드의 타입을 Span 키로 기록합니다.

---

### 2. API 사용법

#### 2.1 In-Process (프로젝트 참조)

FunLangCompiler에서 FunLang.fsproj를 프로젝트 참조로 추가하면:

```fsharp
open ExportApi

let typed = typeCheckFile "src/myfile.fun"

// Per-expression 타입 조회 (Span으로)
match Map.tryFind someExprSpan typed.AnnotationMap with
| Some ty -> // ty: Type.Type — TInt, TString, TArrow(...), TList(...) 등
| None -> // 해당 span에 타입 없음

// Top-level 바인딩 타입 조회 (이름으로)
match Map.tryFind "myFunc" typed.BindingEnv with
| Some (Scheme(vars, constraints, ty)) -> // 타입 스킴
| None -> // 바인딩 없음

// 빌트인인지 확인
let isBuiltin name = Map.containsKey name typed.BuiltinSchemes
```

#### 2.2 CLI (JSON 출력)

```bash
fn --emit-typed-ast file.fun
```

에러 시 exit code 1 + stderr, 성공 시 exit code 0 + stdout JSON.

---

### 3. TypedModule 구조

```fsharp
type TypedModule = {
    AnnotationMap:   Map<Span, Type>      // 모든 Expr 노드의 타입 (Span 키)
    BindingEnv:      Map<string, Scheme>   // builtins + prelude + user 바인딩
    BuiltinSchemes:  Map<string, Scheme>   // 빌트인만 (필터링용)
}
```

---

### 4. JSON 출력 형식

```json
{
  "annotations": [
    {
      "span": { "startLine": 1, "startCol": 6, "endLine": 1, "endCol": 10 },
      "type": "int"
    },
    {
      "span": { "startLine": 2, "startCol": 0, "endLine": 2, "endCol": 33 },
      "type": "string -> string"
    }
  ],
  "bindings": {
    "x": "int",
    "greet": "string -> string",
    "doubled": "int list"
  }
}
```

- **annotations**: user 파일의 모든 expression span → 추론된 타입
- **bindings**: user-defined top-level 바인딩만 (builtins/prelude 제외)
- 타입 변수는 정규화: `'a`, `'b`, `'c` 순서

---

### 5. FunLangCompiler Heuristic 대체 매핑

현재 컴파일러의 heuristic이 타입 정보로 어떻게 대체되는지:

| 현재 Heuristic | 코드 | Typed AST 대체 방법 |
|----------------|------|---------------------|
| `ArrayVars: Set<string>` | for-in dispatch | `AnnotationMap[collSpan]` → `TArray _` 이면 array |
| `StringVars: Set<string>` | IndexGet dispatch | `AnnotationMap[exprSpan]` → `TString` 이면 char-at |
| `BoolVars: Set<string>` | to_string dispatch | `AnnotationMap[argSpan]` → `TBool` 이면 bool variant |
| `CollectionVars: Map<string,Kind>` | for-in dispatch | `AnnotationMap[collSpan]` → `THashtable`/`TData("HashSet",_)` 등 |
| `MutableVars: Set<string>` | LOAD 삽입 | AST의 LetMut/Let 구분으로 충분 |
| `StringFields: Set<string>` | IndexGet dispatch | `AnnotationMap[fieldSpan]` → `TString` |
| `isPtrParamBody` (~85줄) | closure param Ptr/I64 | `AnnotationMap[lambdaSpan]` → `TArrow(TString, _)` 이면 Ptr |
| `isStringExpr` | string 판별 | `AnnotationMap[span]` → `TString` |
| `isBoolExpr` | bool 판별 | `AnnotationMap[span]` → `TBool` |
| `isArrayExpr` | array 판별 | `AnnotationMap[span]` → `TArray _` |

**핵심:** 모든 heuristic이 `AnnotationMap[span]`의 Type 패턴 매치로 대체됩니다.

---

### 6. 실제 예제: 컴파일러가 타입 정보를 사용하는 방법

#### Before (heuristic)
```fsharp
// Elaboration.fs — for-in dispatch
if isArrayExpr ctx env collection then
    emitForInArray ...
elif detectCollectionKind ctx collection = Some HashSetKind then
    emitForInHashSet ...
else
    emitForInList ...  // default, 틀릴 수 있음
```

#### After (typed AST)
```fsharp
// Elaboration.fs — for-in dispatch (type-directed)
match Map.tryFind (spanOf collection) typedModule.AnnotationMap with
| Some (TArray _) -> emitForInArray ...
| Some (TData("HashSet", _)) -> emitForInHashSet ...
| Some (TData("Queue", _)) -> emitForInQueue ...
| Some (TList _) | _ -> emitForInList ...
```

#### Before (isPtrParamBody ~85줄)
```fsharp
let isPtrParam = isPtrParamBody paramName body  // 85줄 heuristic
if isPtrParam then emitPtrParam else emitI64Param
```

#### After (1줄)
```fsharp
match Map.tryFind (spanOf paramExpr) typedModule.AnnotationMap with
| Some TString | Some (TList _) | Some (TData _) -> emitPtrParam
| _ -> emitI64Param
```

---

### 7. 통합 구현 단계 (FunLangCompiler)

#### Step 1: FunLang 프로젝트 참조 추가

```xml
<!-- FunLangCompiler.fsproj -->
<ItemGroup>
  <ProjectReference Include="../FunLang/src/FunLang/FunLang.fsproj" />
</ItemGroup>
```

#### Step 2: 파싱을 FunLang API로 대체

```fsharp
// Before: 자체 Parser/Lexer 사용
let ast = MyParser.parse source

// After: FunLang의 typeCheckFile 사용 (파싱 + 타입체크 동시)
let typedModule = ExportApi.typeCheckFile filePath
// AST는 기존대로 자체 파싱 유지하거나, FunLang의 Ast를 직접 사용
```

#### Step 3: ElabEnv에 타입 정보 추가

```fsharp
type ElabEnv = {
    // 기존 필드들...
    TypedModule: ExportApi.TypedModule  // 추가
}
```

#### Step 4: Heuristic 함수를 타입 조회로 교체

한 번에 하나씩, 각 heuristic을 `AnnotationMap` 조회로 교체:

```fsharp
// isStringExpr 교체
let isString span env =
    match Map.tryFind span env.TypedModule.AnnotationMap with
    | Some TString -> true
    | _ -> false
```

#### Step 5: 추적 집합 제거

모든 heuristic 교체 후:
- `ArrayVars`, `StringVars`, `BoolVars`, `CollectionVars`, `StringFields` 제거
- `isPtrParamBody`, `hasParamPtrUse`, `isArrayExpr` 등 ~250줄 삭제

---

### 8. Type 패턴 매치 치트시트

컴파일러에서 자주 사용할 패턴:

```fsharp
match ty with
| TInt -> "I64"
| TBool -> "I1"
| TChar -> "I8"
| TString -> "Ptr"           // string은 항상 Ptr
| TList _ -> "Ptr"           // list는 항상 Ptr
| TArray _ -> "Ptr"          // array는 항상 Ptr
| THashtable _ -> "Ptr"      // hashtable은 항상 Ptr
| TData("HashSet", _) -> "Ptr"
| TData("Queue", _) -> "Ptr"
| TData("MutableList", _) -> "Ptr"
| TData("StringBuilder", _) -> "Ptr"
| TArrow _ -> "Ptr"          // closure는 항상 Ptr
| TTuple [] -> "I64"         // unit
| TTuple _ -> "Ptr"          // tuple은 Ptr
| TData _ -> "Ptr"           // ADT는 Ptr
| TExn -> "Ptr"
| TVar _ -> "I64"            // 미해소 타입 변수 — fallback
| TError -> "I64"            // 에러 타입 — fallback
```

---

### 9. 주의사항

1. **Span 키 정합성**: FunLangCompiler가 자체 Parser를 사용하면 Span이 다를 수 있음. FunLang의 Parser를 그대로 사용하거나, 동일한 Span 생성을 보장해야 함.

2. **Substitution 적용 완료**: AnnotationMap의 모든 Type은 substitution이 적용된 상태. `TVar`가 남아있다면 진짜 다형적인 것 (추론이 더 구체화할 수 없음).

3. **elaboration 후 노드**: Type class elaboration이 생성하는 합성 노드는 `unknownSpan`이므로 AnnotationMap에 없음. 이들의 타입은 `BindingEnv`에서 이름으로 조회.

4. **전역 상태**: `Bidir.annotationMap`은 전역 mutable. `typeCheckFile` 호출 시 리셋됨. 반환된 `TypedModule.AnnotationMap`은 immutable snapshot이므로 안전.

---

### 10. 관련 테스트

```bash
# Unit tests
dotnet test tests/FunLang.Tests/ --filter "ExportApi|TypeAnnotation"

# flt integration tests
scripts/fslit tests/flt/file/emit/   # --emit-typed-ast 테스트

# 수동 확인
fn --emit-typed-ast yourfile.fun | python3 -m json.tool
```
