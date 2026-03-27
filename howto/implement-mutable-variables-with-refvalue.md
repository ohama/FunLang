---
created: 2026-03-27
description: 인터프리터에서 RefValue 패턴으로 가변 변수를 구현하고 클로저 캡처를 자연스럽게 지원하는 방법
---

# RefValue로 가변 변수와 클로저 캡처 구현

환경(env)에 ref cell을 값으로 저장하면, 클로저가 가변 상태를 공유하는 문제가 자연스럽게 풀린다.

## The Insight

불변 환경(`Map<string, Value>`)에서 가변 변수를 지원하려면, 변수의 값 자체가 아니라 **값을 가리키는 참조**를 환경에 넣어야 한다. `RefValue of Value ref`라는 래퍼를 Value DU에 추가하면, 기존 환경 구조를 전혀 건드리지 않으면서:

1. 변수 읽기: RefValue를 만나면 dereference
2. 변수 쓰기: RefValue의 ref cell을 업데이트
3. 클로저 캡처: 클로저가 env를 복사할 때 RefValue도 함께 복사되지만, ref cell은 힙에 있으므로 **같은 ref cell을 공유**

이 패턴의 핵심은 "환경은 불변이지만, 환경 안의 값이 mutable indirection을 가진다"는 점이다.

## Why This Matters

가변 변수 없이 구현하면:
- 클로저가 캡처 시점의 값만 보게 됨 (`let mut x = 0; let f () = x; x <- 1; f ()` → 0 반환)
- 환경 전체를 mutable로 바꾸면 기존 불변 변수 코드 전부에 영향

RefValue 없이 `Map<string, Value ref>`로 환경을 바꾸는 접근은 **모든 변수 조회 코드를 수정**해야 하므로 침습적이다.

## Recognition Pattern

다음 조건이 모두 충족될 때 이 패턴이 필요하다:
- 인터프리터의 환경이 불변 Map 기반
- 가변 변수(rebinding) 기능을 추가하려 함
- 클로저가 가변 변수를 캡처하여 읽기/쓰기해야 함

## The Approach

기존 Value DU에 `RefValue of Value ref`를 추가하고, 세 지점만 수정한다.

### Step 1: Value DU에 RefValue 추가

```fsharp
// Ast.fs
type Value =
    | IntValue of int
    | StringValue of string
    // ... 기존 variants
    | RefValue of Value ref  // 가변 변수용 ref cell
```

CustomEquality/CustomComparison이 있다면 RefValue 케이스도 추가한다. **투명 dereference** — 비교 시 ref 안의 값을 꺼내서 비교:

```fsharp
| RefValue r1, RefValue r2 -> valueEqual !r1 !r2
```

### Step 2: Eval에서 LetMut → RefValue 생성

```fsharp
| LetMut (name, valueExpr, body, _) ->
    let value = eval env valueExpr
    let refCell = ref value
    let env' = Map.add name (RefValue refCell) env
    eval env' body
```

핵심: `ref value`로 힙에 ref cell을 만들고, `RefValue`로 감싸서 env에 넣는다.

### Step 3: Var 조회에서 투명 dereference

```fsharp
| Var (name, _) ->
    match Map.tryFind name env with
    | Some (RefValue r) -> r.Value  // dereference
    | Some v -> v                    // 불변 변수는 그대로
    | None -> failwithf "Undefined: %s" name
```

**중요:** 읽기(Var)에서만 dereference한다. 쓰기(Assign)에서는 RefValue 자체를 찾아서 ref cell을 업데이트:

```fsharp
| Assign (name, valueExpr, _) ->
    let newValue = eval env valueExpr
    match Map.tryFind name env with
    | Some (RefValue r) ->
        r.Value <- newValue  // ref cell 업데이트
        TupleValue []        // unit 반환
    | Some _ -> failwith "Cannot assign to immutable variable"
    | None -> failwith "Undefined variable"
```

## Example

클로저가 가변 변수를 공유하는 시나리오:

```
let mut count = 0          -- env: { count = RefValue(ref 0) }
let inc () = count <- count + 1  -- 클로저 env에 RefValue(ref 0) 포함
let dec () = count <- count - 1  -- 같은 ref cell 공유
let _ = inc ()             -- ref cell: 0 → 1
let _ = inc ()             -- ref cell: 1 → 2
let _ = dec ()             -- ref cell: 2 → 1
-- count = 1
```

`inc`과 `dec` 클로저는 각각 env 복사본을 갖지만, `count`에 해당하는 `RefValue`가 **같은 ref cell**을 가리키므로 mutation이 공유된다.

이것이 불변 환경에 ref cell을 넣는 패턴의 핵심이다 — 환경의 "구조"는 불변이지만, 환경 안의 "값"이 mutable indirection을 제공한다.

## 체크리스트

- [ ] Value DU에 RefValue 추가 + equality/comparison/hashCode 케이스
- [ ] LetMut eval: `ref value` → `RefValue(refCell)` → env에 추가
- [ ] Assign eval: RefValue 찾아서 ref cell 업데이트, unit 반환
- [ ] Var eval: RefValue면 dereference, 아니면 그대로
- [ ] formatValue에서 RefValue 투명 처리 (dereference 후 출력)
- [ ] 타입 체커에서 가변 변수 generalization 금지 (monomorphic)

## 관련 문서

- `implement-offside-rule-with-context-stack.md` - 같은 프로젝트의 offside rule 구현
