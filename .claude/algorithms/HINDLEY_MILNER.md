# Hindley-Milner Type Inference (Algorithm W)

> 이 문서는 Phase 5: Type System의 상세 알고리즘 설명입니다.
> 전체 구현 계획은 [PLAN.md](../PLAN.md)를 참조하세요.

## 개요

정적 타입 검사 + Hindley-Milner 타입 추론 (Algorithm W)

## Types.fs

```fsharp
module FunLang.Types

/// 타입 변수 ID
type TypeVar = int

/// 타입 정의
type Type =
    | TInt                          // 정수
    | TBool                         // 불리언
    | TString                       // 문자열
    | TUnit                         // 유닛 ()
    | TVar of TypeVar               // 타입 변수 (추론용)
    | TFun of Type * Type           // 함수 타입 T1 -> T2
    | TTuple of Type list           // 튜플 (T1, T2, ...)
    | TList of Type                 // 리스트 [T]
    | TConstructor of string * Type list  // 사용자 정의 타입

/// 타입 스킴 (다형성 지원)
/// ∀α₁,α₂,...,αₙ. τ
type TypeScheme = {
    Quantified: Set<TypeVar>   // 일반화된 타입 변수들
    Type: Type                 // 실제 타입
}

/// 타입 환경: 변수명 → 타입 스킴
type TypeEnv = Map<string, TypeScheme>

/// 치환: 타입 변수 → 타입
type Substitution = Map<TypeVar, Type>
```

## TypeInference.fs

```fsharp
module FunLang.TypeInference

open FunLang.Types
open FunLang.Ast

//============================================================
// Algorithm W: Hindley-Milner 타입 추론
//============================================================
//
// 핵심 아이디어:
// 1. 각 표현식에 대해 새로운 타입 변수 할당
// 2. 표현식 구조에 따라 타입 제약 조건 생성
// 3. 유니피케이션으로 제약 조건 해결
// 4. let 바인딩에서 일반화(generalization)로 다형성 지원
//

/// 새로운 타입 변수 생성
let mutable private nextVar = 0
let freshTypeVar () : Type =
    nextVar <- nextVar + 1
    TVar nextVar

let resetTypeVars () = nextVar <- 0

//------------------------------------------------------------
// 1. 치환 (Substitution)
//------------------------------------------------------------

/// 타입에 치환 적용
let rec applySubst (subst: Substitution) (t: Type) : Type =
    match t with
    | TVar v ->
        match Map.tryFind v subst with
        | Some t' -> applySubst subst t'  // 반복 적용 (transitive)
        | None -> t
    | TFun (t1, t2) -> TFun (applySubst subst t1, applySubst subst t2)
    | TTuple ts -> TTuple (List.map (applySubst subst) ts)
    | TList t -> TList (applySubst subst t)
    | TConstructor (name, ts) -> TConstructor (name, List.map (applySubst subst) ts)
    | _ -> t

/// 타입 스킴에 치환 적용
let applySubstScheme (subst: Substitution) (scheme: TypeScheme) : TypeScheme =
    // 일반화된 변수는 치환하지 않음
    let subst' = Map.filter (fun k _ -> not (Set.contains k scheme.Quantified)) subst
    { scheme with Type = applySubst subst' scheme.Type }

/// 타입 환경에 치환 적용
let applySubstEnv (subst: Substitution) (env: TypeEnv) : TypeEnv =
    Map.map (fun _ scheme -> applySubstScheme subst scheme) env

/// 두 치환 합성: (s2 ∘ s1)(t) = s2(s1(t))
let composeSubst (s1: Substitution) (s2: Substitution) : Substitution =
    let s1' = Map.map (fun _ t -> applySubst s2 t) s1
    Map.fold (fun acc k v -> Map.add k v acc) s1' s2

//------------------------------------------------------------
// 2. 자유 타입 변수 (Free Type Variables)
//------------------------------------------------------------

/// 타입의 자유 타입 변수
let rec freeTypeVars (t: Type) : Set<TypeVar> =
    match t with
    | TVar v -> Set.singleton v
    | TFun (t1, t2) -> Set.union (freeTypeVars t1) (freeTypeVars t2)
    | TTuple ts -> ts |> List.map freeTypeVars |> Set.unionMany
    | TList t -> freeTypeVars t
    | TConstructor (_, ts) -> ts |> List.map freeTypeVars |> Set.unionMany
    | _ -> Set.empty

/// 타입 스킴의 자유 타입 변수
let freeTypeVarsScheme (scheme: TypeScheme) : Set<TypeVar> =
    Set.difference (freeTypeVars scheme.Type) scheme.Quantified

/// 타입 환경의 자유 타입 변수
let freeTypeVarsEnv (env: TypeEnv) : Set<TypeVar> =
    env |> Map.toSeq |> Seq.map (snd >> freeTypeVarsScheme) |> Set.unionMany

//------------------------------------------------------------
// 3. 유니피케이션 (Unification)
//------------------------------------------------------------

/// Occurs Check: 타입 변수가 타입 내에 나타나는지 검사 (무한 타입 방지)
let rec occursCheck (v: TypeVar) (t: Type) : bool =
    match t with
    | TVar v' -> v = v'
    | TFun (t1, t2) -> occursCheck v t1 || occursCheck v t2
    | TTuple ts -> List.exists (occursCheck v) ts
    | TList t -> occursCheck v t
    | TConstructor (_, ts) -> List.exists (occursCheck v) ts
    | _ -> false

/// 두 타입의 유니피케이션: 둘을 같게 만드는 치환 찾기
let rec unify (t1: Type) (t2: Type) : Result<Substitution, string> =
    Logging.trace TypeCheck (sprintf "Unifying: %A ~ %A" t1 t2)
    match t1, t2 with
    // 같은 기본 타입
    | TInt, TInt -> Ok Map.empty
    | TBool, TBool -> Ok Map.empty
    | TString, TString -> Ok Map.empty
    | TUnit, TUnit -> Ok Map.empty

    // 타입 변수
    | TVar v, t | t, TVar v ->
        if t = TVar v then
            Ok Map.empty
        elif occursCheck v t then
            Error (sprintf "Infinite type: %A occurs in %A" v t)
        else
            Ok (Map.ofList [v, t])

    // 함수 타입
    | TFun (a1, r1), TFun (a2, r2) ->
        unify a1 a2
        |> Result.bind (fun s1 ->
            unify (applySubst s1 r1) (applySubst s1 r2)
            |> Result.map (fun s2 -> composeSubst s1 s2))

    // 튜플
    | TTuple ts1, TTuple ts2 when List.length ts1 = List.length ts2 ->
        List.zip ts1 ts2
        |> List.fold (fun acc (t1, t2) ->
            acc |> Result.bind (fun s ->
                unify (applySubst s t1) (applySubst s t2)
                |> Result.map (composeSubst s)))
           (Ok Map.empty)

    // 리스트
    | TList t1, TList t2 -> unify t1 t2

    // 사용자 정의 타입
    | TConstructor (n1, ts1), TConstructor (n2, ts2) when n1 = n2 && List.length ts1 = List.length ts2 ->
        List.zip ts1 ts2
        |> List.fold (fun acc (t1, t2) ->
            acc |> Result.bind (fun s ->
                unify (applySubst s t1) (applySubst s t2)
                |> Result.map (composeSubst s)))
           (Ok Map.empty)

    // 실패
    | _ -> Error (sprintf "Cannot unify %A with %A" t1 t2)

//------------------------------------------------------------
// 4. 일반화와 인스턴스화 (Generalization & Instantiation)
//------------------------------------------------------------

/// 일반화: 환경에 없는 자유 변수들을 양화
/// let x = e 에서 e의 타입을 일반화
let generalize (env: TypeEnv) (t: Type) : TypeScheme =
    let envFreeVars = freeTypeVarsEnv env
    let typeFreeVars = freeTypeVars t
    let quantified = Set.difference typeFreeVars envFreeVars
    Logging.trace TypeCheck (sprintf "Generalizing: %A, quantified: %A" t quantified)
    { Quantified = quantified; Type = t }

/// 인스턴스화: 양화된 변수들을 새로운 타입 변수로 교체
let instantiate (scheme: TypeScheme) : Type =
    let mapping =
        scheme.Quantified
        |> Set.toList
        |> List.map (fun v -> v, freshTypeVar())
        |> Map.ofList
    let rec substitute t =
        match t with
        | TVar v ->
            match Map.tryFind v mapping with
            | Some t' -> t'
            | None -> t
        | TFun (t1, t2) -> TFun (substitute t1, substitute t2)
        | TTuple ts -> TTuple (List.map substitute ts)
        | TList t -> TList (substitute t)
        | TConstructor (n, ts) -> TConstructor (n, List.map substitute ts)
        | _ -> t
    let result = substitute scheme.Type
    Logging.trace TypeCheck (sprintf "Instantiated: %A -> %A" scheme.Type result)
    result

//------------------------------------------------------------
// 5. Algorithm W - 핵심 추론 알고리즘
//------------------------------------------------------------

/// W(Γ, e) = (S, τ) where S는 치환, τ는 추론된 타입
let rec infer (env: TypeEnv) (expr: Expr) : Result<Substitution * Type, string> =
    Logging.trace TypeCheck (sprintf "Inferring: %A" expr)
    match expr with

    // 리터럴: 타입이 바로 결정됨
    | ELiteral lit ->
        let t = match lit with
                | LInt _ -> TInt
                | LBool _ -> TBool
                | LString _ -> TString
                | LUnit -> TUnit
        Ok (Map.empty, t)

    // 변수: 환경에서 찾아서 인스턴스화
    | EVariable name ->
        match Map.tryFind name env with
        | Some scheme -> Ok (Map.empty, instantiate scheme)
        | None -> Error (sprintf "Unbound variable: %s" name)

    // 람다: fun x -> e
    | ELambda (param, body) ->
        let paramType = freshTypeVar()
        let env' = Map.add param { Quantified = Set.empty; Type = paramType } env
        infer env' body
        |> Result.map (fun (s, bodyType) ->
            let resultType = TFun (applySubst s paramType, bodyType)
            (s, resultType))

    // 함수 적용: e1 e2
    | EApply (func, arg) ->
        let resultType = freshTypeVar()
        infer env func
        |> Result.bind (fun (s1, funcType) ->
            infer (applySubstEnv s1 env) arg
            |> Result.bind (fun (s2, argType) ->
                let funcType' = applySubst s2 funcType
                unify funcType' (TFun (argType, resultType))
                |> Result.map (fun s3 ->
                    let finalSubst = composeSubst (composeSubst s1 s2) s3
                    (finalSubst, applySubst s3 resultType))))

    // let 바인딩 (다형성 지원): let x = e1 in e2
    | ELet (name, value, body) ->
        infer env value
        |> Result.bind (fun (s1, valueType) ->
            let env' = applySubstEnv s1 env
            let scheme = generalize env' valueType  // 일반화!
            let env'' = Map.add name scheme env'
            infer env'' body
            |> Result.map (fun (s2, bodyType) ->
                (composeSubst s1 s2, bodyType)))

    // 재귀 let: let rec f = e1 in e2
    | ELetRec (name, value, body) ->
        let funcType = freshTypeVar()
        let env' = Map.add name { Quantified = Set.empty; Type = funcType } env
        infer env' value
        |> Result.bind (fun (s1, valueType) ->
            unify (applySubst s1 funcType) valueType
            |> Result.bind (fun s2 ->
                let s = composeSubst s1 s2
                let env'' = applySubstEnv s env
                let scheme = generalize env'' (applySubst s valueType)
                let env''' = Map.add name scheme env''
                infer env''' body
                |> Result.map (fun (s3, bodyType) ->
                    (composeSubst s s3, bodyType))))

    // 조건문: if e1 then e2 else e3
    | EIf (cond, thenBr, elseBr) ->
        infer env cond
        |> Result.bind (fun (s1, condType) ->
            unify condType TBool
            |> Result.bind (fun s2 ->
                let s = composeSubst s1 s2
                let env' = applySubstEnv s env
                infer env' thenBr
                |> Result.bind (fun (s3, thenType) ->
                    let env'' = applySubstEnv s3 env'
                    infer env'' elseBr
                    |> Result.bind (fun (s4, elseType) ->
                        unify (applySubst s4 thenType) elseType
                        |> Result.map (fun s5 ->
                            let finalSubst = composeSubst (composeSubst (composeSubst s s3) s4) s5
                            (finalSubst, applySubst s5 elseType))))))

    // 이항 연산
    | EBinaryOp (op, left, right) ->
        inferBinaryOp env op left right

    // ... 다른 케이스들 ...

and inferBinaryOp env op left right =
    let (expectedLeft, expectedRight, resultType) =
        match op with
        | Add | Sub | Mul | Div | Mod -> (TInt, TInt, TInt)
        | Eq | Neq -> (freshTypeVar(), freshTypeVar(), TBool)  // 다형성
        | Lt | Gt | Lte | Gte -> (TInt, TInt, TBool)
        | And | Or -> (TBool, TBool, TBool)

    infer env left
    |> Result.bind (fun (s1, leftType) ->
        infer (applySubstEnv s1 env) right
        |> Result.bind (fun (s2, rightType) ->
            unify (applySubst s2 leftType) expectedLeft
            |> Result.bind (fun s3 ->
                unify (applySubst s3 rightType) expectedRight
                |> Result.map (fun s4 ->
                    (composeSubst (composeSubst (composeSubst s1 s2) s3) s4,
                     applySubst s4 resultType)))))

//------------------------------------------------------------
// 6. 공개 API
//------------------------------------------------------------

/// 표현식의 타입 추론 (결과만 반환)
let inferType (expr: Expr) : Result<Type, string> =
    resetTypeVars()
    infer Map.empty expr
    |> Result.map (fun (subst, t) -> applySubst subst t)

/// 타입을 문자열로 포맷
let rec formatType (t: Type) : string =
    match t with
    | TInt -> "int"
    | TBool -> "bool"
    | TString -> "string"
    | TUnit -> "unit"
    | TVar v -> sprintf "'t%d" v
    | TFun (TFun _ as arg, ret) -> sprintf "(%s) -> %s" (formatType arg) (formatType ret)
    | TFun (arg, ret) -> sprintf "%s -> %s" (formatType arg) (formatType ret)
    | TTuple ts -> ts |> List.map formatType |> String.concat " * " |> sprintf "(%s)"
    | TList t -> sprintf "%s list" (formatType t)
    | TConstructor (name, []) -> name
    | TConstructor (name, ts) -> sprintf "%s<%s>" name (ts |> List.map formatType |> String.concat ", ")
```

## Algorithm W 동작 예시

```
표현식: let id = fun x -> x in id 42

1. 'id'의 타입 추론:
   - fun x -> x 에서 x에 새 타입 변수 α 할당
   - 본문 x의 타입은 α
   - 따라서 fun x -> x 의 타입은 α → α

2. 'id'의 일반화:
   - 환경에 α가 없으므로 일반화 가능
   - id : ∀α. α → α

3. 'id 42' 추론:
   - id를 인스턴스화: β → β (새 변수)
   - 42의 타입: int
   - 유니피케이션: β → β ~ int → γ
   - 결과: β = int, γ = int

4. 최종 타입: int
```

## FsCheck 테스트

```fsharp
[<Property>]
let ``identity function is polymorphic`` (x: int) =
    let code = "let id = fun x -> x in id"
    let t = inferType (parse (code + sprintf " %d" x))
    t = Ok TInt

[<Property>]
let ``arithmetic returns int`` (a: int) (b: int) =
    inferType (parse $"{a} + {b}") = Ok TInt

[<Property>]
let ``comparison returns bool`` (a: int) (b: int) =
    inferType (parse $"{a} < {b}") = Ok TBool

[<Property>]
let ``let polymorphism works`` () =
    // let f = fun x -> x in (f 1, f true) 는 (int, bool)
    let code = "let f = fun x -> x in (f 1, f true)"
    let t = inferType (parse code)
    t = Ok (TTuple [TInt; TBool])

[<Property>]
let ``recursive functions typecheck`` (n: PositiveInt) =
    let code = "let rec fact = fun n -> if n = 0 then 1 else n * fact (n - 1) in fact"
    let t = inferType (parse code)
    t = Ok (TFun (TInt, TInt))
```

## 타입 에러 메시지

```fsharp
type TypeError =
    | UnificationError of Type * Type * Position
    | OccursCheckError of TypeVar * Type * Position
    | UnboundVariable of string * Position
    | NotAFunction of Type * Position

// 예시 출력:
// Error at line 3, column 10: Type mismatch
//   Expected: int
//   Actual: bool
//   In expression: if x then 1 else 2
```

## 로깅 포인트

- 새 타입 변수 생성 (`freshTypeVar`)
- 유니피케이션 시도 및 결과
- 일반화/인스턴스화 과정
- 최종 추론된 타입
