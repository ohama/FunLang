---
created: 2026-04-09
description: FunLang에 새 builtin 함수를 추가하는 3단계 패턴 (Eval + TypeCheck + Prelude)
---

# FunLang에 Builtin 함수 추가하기

새 builtin 함수를 추가할 때 3개 파일을 수정한다. 런타임 구현, 타입 시그니처, Prelude 래퍼.

## The Insight

FunLang의 builtin은 `BuiltinValue(fn: Value -> Value)` 래퍼로 구현된다. 다중 인자는 currying으로 처리: 첫 인자를 받아 다음 `BuiltinValue`를 반환하는 중첩 구조.

## Why This Matters

Eval.fs에만 추가하면 런타임은 동작하지만 타입 체커가 `Unbound variable` 에러를 발생시킨다. TypeCheck.fs에 타입 시그니처가 없으면 타입 추론이 실패하고, FunLangCompiler의 `annotationMap` 전체가 소멸한다.

## Recognition Pattern

- FunLangCompiler에서 새 builtin을 추가했을 때 인터프리터 호환성 필요
- 새 자료구조의 조작 함수가 필요할 때
- F# 런타임 기능을 FunLang에 노출할 때

## The Approach

### Step 1: Eval.fs — 런타임 구현

`initialBuiltinEnv`에 추가한다.

**단일 인자:**
```fsharp
"mytype_count", BuiltinValue (fun v ->
    match v with
    | MyTypeValue mt -> IntValue mt.Count
    | _ -> failwith "mytype_count: expected MyType")
```

**다중 인자 (currying):**
```fsharp
"mytype_add", BuiltinValue (fun mtVal ->
    BuiltinValue (fun v ->
        match mtVal with
        | MyTypeValue mt -> mt.Add(v); TupleValue []
        | _ -> failwith "mytype_add: expected MyType"))
```

### Step 2: TypeCheck.fs — 타입 시그니처

`initialTypeEnv`에 `Scheme`을 추가한다.

```fsharp
// 단일 인자: MyType -> int
"mytype_count", Scheme([0], [], TArrow(TMyType(TVar 0), TInt))

// 다중 인자: MyType -> 'a -> unit
"mytype_add", Scheme([0], [], TArrow(TMyType(TVar 0), TArrow(TVar 0, TTuple [])))
```

**Scheme 구조:** `Scheme(boundVars, constraints, type)`
- `boundVars`: 다형 타입 변수 인덱스 목록 (예: `[0]` = `'a`, `[0; 1]` = `'a, 'b`)
- `constraints`: 타입 클래스 제약 (보통 `[]`)
- `type`: 함수 타입 (TArrow 체인)

### Step 3: Prelude/*.fun — 래퍼 함수

```fun
module MyType =
    let count (mt : 'a mytype) : int = mytype_count mt
    let add (mt : 'a mytype) (v : 'a) : unit = mytype_add mt v
```

## Example

Issue #11에서 `hashset_keys`와 `mutablelist_tolist`를 추가한 사례:

```fsharp
// Eval.fs
"hashset_keys", BuiltinValue (fun hsVal ->
    match hsVal with
    | HashSetValue hs -> ListValue (hs |> Seq.toList)
    | _ -> failwith "hashset_keys: expected HashSet")

// TypeCheck.fs
"hashset_keys", Scheme([0], [], TArrow(THashSet(TVar 0), TList(TVar 0)))
```

```fun
// Prelude/HashSet.fun
let keys (hs : 'a hashset) : 'a list = hashset_keys hs
```

## 체크리스트

- [ ] Eval.fs: `initialBuiltinEnv`에 `BuiltinValue` 추가
- [ ] TypeCheck.fs: `initialTypeEnv`에 `Scheme` 추가
- [ ] Prelude/*.fun: 타입 어노테이션 포함한 래퍼 함수
- [ ] `dotnet build` 성공
- [ ] flt 테스트 추가 (새 함수 동작 확인)
- [ ] 전체 테스트 통과
