---
created: 2026-04-09
description: FunLang 타입 시스템에 새 builtin 타입을 추가하는 5파일 패턴
---

# FunLang에 Builtin 타입 추가하기

새 컬렉션이나 자료구조를 FunLang에 추가할 때, 타입 시스템 전체에 전파해야 한다. 5개 파일을 순서대로 수정하면 된다.

## The Insight

FunLang의 타입은 `Type.fs`의 DU(discriminated union)로 표현된다. 새 타입을 추가하면 이 DU를 사용하는 모든 match 절에 새 케이스를 추가해야 한다. F# 컴파일러가 불완전 매치를 잡아주므로, 빌드하면 누락을 알 수 있다.

## Why This Matters

`TData("TypeName", [])` 같은 임시 표현을 쓰면 당장은 동작하지만, unify/format/freeVars 등에서 올바르게 처리되지 않아 타입 추론 오류가 발생한다.

## Recognition Pattern

- 새 mutable 컬렉션이나 자료구조를 언어에 추가할 때
- `TData("Name", [])` 같은 임시 표현이 코드에 있을 때
- 타입 어노테이션에서 새 타입을 사용하고 싶을 때

## The Approach

5개 파일을 순서대로 수정한다. 빌드 후 0 warnings를 확인한다.

### Step 1: Type.fs — DU 케이스 추가

```fsharp
type Type =
    // ... 기존 타입들
    | TMyType of Type    // 파라미터가 있으면 of Type, 없으면 단독
```

같은 파일에서 모든 match 절에 새 케이스를 추가한다:

| 함수 | 추가할 것 |
|------|-----------|
| `formatType` | `\| TMyType t -> sprintf "%s mytype" (formatType t)` |
| `collectVars` (2곳) | `\| TMyType t -> collectVars acc t` |
| `format` (2곳) | `\| TMyType t -> sprintf "%s mytype" (format t)` |
| `apply` | `\| TMyType t -> TMyType (apply s t)` |
| `freeVars` | `\| TMyType t -> freeVars t` |

### Step 2: Elaborate.fs — 타입 이름 매핑

`TEData` 처리에서 타입 이름을 내부 타입으로 변환한다:

```fsharp
// elaborateWithVars 함수 내 TEData 케이스
match canonical, types with
| "mytype", [t] -> (TMyType t, finalVars)
| _ -> (TData(canonical, types), finalVars)
```

`substTypeExprWithMap`에도 동일 처리. 파라미터 없는 타입은 `TEName`에서 처리:

```fsharp
| TEName name ->
    match name with
    | "mytype" -> (TMyType, vars)  // 파라미터 없는 경우
    | _ -> ...
```

### Step 3: Unify.fs — 통합 규칙

```fsharp
| TMyType t1, TMyType t2 ->
    unifyWithContext ctx trace span t1 t2
```

### Step 4: Bidir.fs — 생성자 추론 + 컬렉션 처리

Constructor synth에서 `TData` 대신 전용 타입을 사용한다:

```fsharp
| "MyType" ->
    let resultTy = TMyType(freshVar())
    // ...
```

`IndexGet`/`IndexSet`/`ForInExpr`에서 새 타입을 지원하는 경우 해당 케이스 추가.

### Step 5: TypeCheck.fs — Builtin 함수 타입 시그니처

```fsharp
"mytype_create", Scheme([0], [], TArrow(TTuple [], TMyType(TVar 0)))
"mytype_add",    Scheme([0], [], TArrow(TMyType(TVar 0), TArrow(TVar 0, TTuple [])))
```

## Example

v14.0에서 `THashSet`, `TQueue`, `TMutableList`, `TStringBuilder` 4개를 한 번에 추가한 사례:

```fsharp
// Type.fs
| THashSet of Type
| TQueue of Type
| TMutableList of Type
| TStringBuilder

// Elaborate.fs
| "hashset", [t] -> (THashSet t, finalVars)
| "queue", [t] -> (TQueue t, finalVars)

// Unify.fs
| THashSet t1, THashSet t2 -> unifyWithContext ctx trace span t1 t2
| TQueue t1, TQueue t2 -> unifyWithContext ctx trace span t1 t2
```

## 체크리스트

- [ ] Type.fs: DU 케이스 + formatType + collectVars(2) + format(2) + apply + freeVars
- [ ] Elaborate.fs: TEData 매핑 + substTypeExprWithMap 매핑 (+ TEName if 파라미터 없음)
- [ ] Unify.fs: 동일 타입 통합 규칙
- [ ] Bidir.fs: Constructor synth + IndexGet/ForInExpr (해당 시)
- [ ] TypeCheck.fs: builtin 함수 Scheme
- [ ] `dotnet build` 0 warnings 확인
- [ ] 전체 flt 테스트 통과
