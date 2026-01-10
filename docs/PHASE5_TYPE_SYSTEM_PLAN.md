# Phase 5: Type System - 상세 구현 계획

## 개요

FunLang에 Hindley-Milner 타입 추론을 구현합니다.

**목표:**
- 타입 어노테이션 없이 자동 타입 추론
- Let-polymorphism 지원
- 명확한 타입 에러 메시지
- 기존 206개 테스트 유지

## 구현 단계

### Step 1: Core Type Definitions (Types.fs)

**파일:** `src/FunLang/Types.fs`

```fsharp
module FunLang.Types

/// 타입 변수 ID
type TypeVar = int

/// Monotype (단형 타입)
type Type =
    | TInt
    | TBool
    | TString
    | TUnit
    | TVar of TypeVar              // 타입 변수 α, β, ...
    | TFun of Type * Type          // τ₁ → τ₂
    | TList of Type                // list τ
    | TTuple of Type list          // (τ₁, τ₂, ...)

/// Type Scheme (다형 타입)
type TypeScheme =
    | Forall of TypeVar list * Type   // ∀α₁...αₙ. τ

/// Type Environment
type TypeEnv = Map<string, TypeScheme>

/// Substitution (치환)
type Substitution = Map<TypeVar, Type>

/// 타입 에러
type TypeError = {
    Kind: TypeErrorKind
    Message: string
    Position: Position option
    Hint: string option
}

and TypeErrorKind =
    | UnboundVariable of string
    | TypeMismatch of expected: Type * actual: Type
    | OccursCheck of TypeVar * Type
    | ArityMismatch of expected: int * actual: int
    | NotAFunction of Type
    | PatternTypeMismatch of expected: Type * actual: Type
```

**Helper Functions:**

```fsharp
module TypeHelpers =
    /// Fresh type variable 생성
    let mutable private counter = 0
    let freshTypeVar () =
        counter <- counter + 1
        TVar counter

    /// Substitution 적용
    let rec apply (s: Substitution) (t: Type) : Type =
        match t with
        | TInt | TBool | TString | TUnit -> t
        | TVar v -> Map.tryFind v s |> Option.defaultValue t
        | TFun (t1, t2) -> TFun (apply s t1, apply s t2)
        | TList t1 -> TList (apply s t1)
        | TTuple ts -> TTuple (List.map (apply s) ts)

    /// Substitution 합성 (s1 ∘ s2)
    let compose (s1: Substitution) (s2: Substitution) : Substitution =
        let s2' = Map.map (fun _ t -> apply s1 t) s2
        Map.fold (fun acc k v -> Map.add k v acc) s2' s1

    /// 자유 타입 변수
    let rec freeTypeVars (t: Type) : Set<TypeVar> =
        match t with
        | TInt | TBool | TString | TUnit -> Set.empty
        | TVar v -> Set.singleton v
        | TFun (t1, t2) -> Set.union (freeTypeVars t1) (freeTypeVars t2)
        | TList t1 -> freeTypeVars t1
        | TTuple ts -> ts |> List.map freeTypeVars |> Set.unionMany

    /// Type Scheme의 자유 변수
    let freeTypeVarsScheme (Forall (vars, t)) : Set<TypeVar> =
        Set.difference (freeTypeVars t) (Set.ofList vars)

    /// Environment의 자유 변수
    let freeTypeVarsEnv (env: TypeEnv) : Set<TypeVar> =
        env |> Map.toSeq |> Seq.map (snd >> freeTypeVarsScheme) |> Set.unionMany

    /// Generalization
    let generalize (env: TypeEnv) (t: Type) : TypeScheme =
        let envFV = freeTypeVarsEnv env
        let tFV = freeTypeVars t
        let vars = Set.difference tFV envFV |> Set.toList
        Forall (vars, t)

    /// Instantiation
    let instantiate (Forall (vars, t)) : Type =
        let subst = vars |> List.map (fun v -> (v, freshTypeVar ())) |> Map.ofList
        apply subst t
```

**TDD 테스트:**

```fsharp
// tests/FunLang.Tests/TypeTests.fs

testList "Type Helpers" [
    testProperty "apply empty substitution is identity" <| fun (t: Type) ->
        apply Map.empty t = t

    testProperty "compose with empty is identity" <| fun (s: Substitution) ->
        compose s Map.empty = s && compose Map.empty s = s

    testProperty "freeTypeVars of ground type is empty" <| fun () ->
        freeTypeVars TInt = Set.empty &&
        freeTypeVars TBool = Set.empty

    test "instantiate creates fresh variables" {
        let scheme = Forall ([1], TFun (TVar 1, TVar 1))
        let t1 = instantiate scheme
        let t2 = instantiate scheme
        Expect.notEqual t1 t2 "should create different instances"
    }
]
```

---

### Step 2: Unification (Unification.fs)

**파일:** `src/FunLang/Unification.fs`

```fsharp
module FunLang.Unification

open FunLang.Types

/// Occurs check: α가 τ 안에 나타나는지 검사
let occursIn (v: TypeVar) (t: Type) : bool =
    Set.contains v (TypeHelpers.freeTypeVars t)

/// 통합 알고리즘
let rec unify (t1: Type) (t2: Type) : Result<Substitution, TypeError> =
    match t1, t2 with
    // 같은 타입
    | TInt, TInt -> Ok Map.empty
    | TBool, TBool -> Ok Map.empty
    | TString, TString -> Ok Map.empty
    | TUnit, TUnit -> Ok Map.empty

    // 타입 변수
    | TVar v, t | t, TVar v ->
        if TVar v = t then Ok Map.empty
        elif occursIn v t then
            Error { Kind = OccursCheck (v, t)
                    Message = sprintf "Infinite type: %A occurs in %A" v t
                    Position = None; Hint = None }
        else Ok (Map.singleton v t)

    // 함수 타입
    | TFun (a1, r1), TFun (a2, r2) ->
        result {
            let! s1 = unify a1 a2
            let! s2 = unify (TypeHelpers.apply s1 r1) (TypeHelpers.apply s1 r2)
            return TypeHelpers.compose s2 s1
        }

    // 리스트 타입
    | TList t1, TList t2 -> unify t1 t2

    // 튜플 타입
    | TTuple ts1, TTuple ts2 when List.length ts1 = List.length ts2 ->
        List.zip ts1 ts2
        |> List.fold (fun acc (a, b) ->
            result {
                let! s = acc
                let! s' = unify (TypeHelpers.apply s a) (TypeHelpers.apply s b)
                return TypeHelpers.compose s' s
            }) (Ok Map.empty)

    // 타입 불일치
    | _ ->
        Error { Kind = TypeMismatch (t1, t2)
                Message = sprintf "Type mismatch: expected %A, got %A" t1 t2
                Position = None; Hint = None }
```

**TDD 테스트:**

```fsharp
testList "Unification" [
    test "unify same types" {
        Expect.isOk (unify TInt TInt) ""
        Expect.isOk (unify TBool TBool) ""
    }

    test "unify type variable with type" {
        let result = unify (TVar 1) TInt
        Expect.isOk result ""
        let s = Result.get result
        Expect.equal (Map.find 1 s) TInt ""
    }

    test "unify function types" {
        let t1 = TFun (TVar 1, TVar 2)
        let t2 = TFun (TInt, TBool)
        let result = unify t1 t2
        Expect.isOk result ""
    }

    test "occurs check fails" {
        let result = unify (TVar 1) (TList (TVar 1))
        Expect.isError result "should fail occurs check"
    }

    test "type mismatch fails" {
        let result = unify TInt TBool
        Expect.isError result "int != bool"
    }

    testProperty "unification is symmetric" <| fun (t1: Type) (t2: Type) ->
        match unify t1 t2, unify t2 t1 with
        | Ok _, Ok _ -> true
        | Error _, Error _ -> true
        | _ -> false
]
```

---

### Step 3: Type Inference Engine (TypeInfer.fs)

**파일:** `src/FunLang/TypeInfer.fs`

```fsharp
module FunLang.TypeInfer

open FunLang.Ast
open FunLang.Types
open FunLang.Unification

/// 타입 추론 결과
type InferResult = Result<Substitution * Type, TypeError>

/// 내장 연산자 타입
let builtinOpType (op: BinOp) : Type * Type * Type =
    match op with
    | Add | Sub | Mul | Div | Mod -> (TInt, TInt, TInt)
    | Lt | Gt | Le | Ge -> (TInt, TInt, TBool)
    | Eq | Ne -> let α = TypeHelpers.freshTypeVar() in (α, α, TBool)
    | And | Or -> (TBool, TBool, TBool)
    | Concat -> (TString, TString, TString)

/// Algorithm W
let rec infer (env: TypeEnv) (expr: Expr) : InferResult =
    match expr with
    // 리터럴
    | EInt _ -> Ok (Map.empty, TInt)
    | EBool _ -> Ok (Map.empty, TBool)
    | EString _ -> Ok (Map.empty, TString)
    | EUnit -> Ok (Map.empty, TUnit)

    // 변수
    | EVar name ->
        match Map.tryFind name env with
        | Some scheme -> Ok (Map.empty, TypeHelpers.instantiate scheme)
        | None ->
            Error { Kind = UnboundVariable name
                    Message = sprintf "Unbound variable: %s" name
                    Position = None; Hint = Some "Did you mean to define it?" }

    // Lambda
    | ELambda (param, body) ->
        let α = TypeHelpers.freshTypeVar ()
        let env' = Map.add param (Forall ([], α)) env
        result {
            let! (s, τ) = infer env' body
            return (s, TFun (TypeHelpers.apply s α, τ))
        }

    // Application
    | EApp (e1, e2) ->
        result {
            let! (s1, τ1) = infer env e1
            let! (s2, τ2) = infer (applyEnv s1 env) e2
            let α = TypeHelpers.freshTypeVar ()
            let! s3 = unify (TypeHelpers.apply s2 τ1) (TFun (τ2, α))
            return (TypeHelpers.compose s3 (TypeHelpers.compose s2 s1),
                    TypeHelpers.apply s3 α)
        }

    // Let
    | ELet (name, e1, e2) ->
        result {
            let! (s1, τ1) = infer env e1
            let env' = applyEnv s1 env
            let σ = TypeHelpers.generalize env' τ1
            let! (s2, τ2) = infer (Map.add name σ env') e2
            return (TypeHelpers.compose s2 s1, τ2)
        }

    // Let Rec
    | ELetRec (name, e1, e2) ->
        result {
            let α = TypeHelpers.freshTypeVar ()
            let env' = Map.add name (Forall ([], α)) env
            let! (s1, τ1) = infer env' e1
            let! s2 = unify (TypeHelpers.apply s1 α) τ1
            let s = TypeHelpers.compose s2 s1
            let env'' = applyEnv s env
            let σ = TypeHelpers.generalize env'' (TypeHelpers.apply s τ1)
            let! (s3, τ2) = infer (Map.add name σ env'') e2
            return (TypeHelpers.compose s3 s, τ2)
        }

    // If
    | EIf (cond, thenE, elseE) ->
        result {
            let! (s1, τ1) = infer env cond
            let! s2 = unify τ1 TBool
            let s = TypeHelpers.compose s2 s1
            let! (s3, τ2) = infer (applyEnv s env) thenE
            let! (s4, τ3) = infer (applyEnv (TypeHelpers.compose s3 s) env) elseE
            let! s5 = unify (TypeHelpers.apply s4 τ2) τ3
            return (TypeHelpers.compose s5 (TypeHelpers.compose s4 (TypeHelpers.compose s3 s)),
                    TypeHelpers.apply s5 τ3)
        }

    // Binary Operator
    | EBinOp (op, e1, e2) ->
        let (t1, t2, tr) = builtinOpType op
        result {
            let! (s1, τ1) = infer env e1
            let! s2 = unify τ1 t1
            let! (s3, τ2) = infer (applyEnv (TypeHelpers.compose s2 s1) env) e2
            let! s4 = unify τ2 (TypeHelpers.apply s3 t2)
            return (TypeHelpers.compose s4 (TypeHelpers.compose s3 (TypeHelpers.compose s2 s1)),
                    TypeHelpers.apply s4 tr)
        }

    // Tuple
    | ETuple es ->
        result {
            let! results = inferList env es
            let (s, ts) = results
            return (s, TTuple ts)
        }

    // List
    | EList [] ->
        Ok (Map.empty, TList (TypeHelpers.freshTypeVar ()))

    | EList (e::es) ->
        result {
            let! (s1, τ1) = infer env e
            let! (s2, ts) = inferList (applyEnv s1 env) es
            // 모든 원소 타입 통합
            let! s3 = unifyAll (TypeHelpers.apply s2 τ1 :: ts)
            let elemType = TypeHelpers.apply s3 (TypeHelpers.apply s2 τ1)
            return (TypeHelpers.compose s3 (TypeHelpers.compose s2 s1), TList elemType)
        }

    // Cons
    | ECons (head, tail) ->
        result {
            let! (s1, τ1) = infer env head
            let! (s2, τ2) = infer (applyEnv s1 env) tail
            let! s3 = unify τ2 (TList (TypeHelpers.apply s2 τ1))
            return (TypeHelpers.compose s3 (TypeHelpers.compose s2 s1),
                    TypeHelpers.apply s3 τ2)
        }

    // Match
    | EMatch (scrutinee, cases) ->
        inferMatch env scrutinee cases

    // Block
    | EBlock exprs ->
        inferBlock env exprs

    | _ -> Error { Kind = TypeMismatch (TUnit, TUnit)
                   Message = "Unsupported expression"
                   Position = None; Hint = None }

/// 여러 표현식 추론
and inferList (env: TypeEnv) (exprs: Expr list) : Result<Substitution * Type list, TypeError> =
    // ... implementation

/// 패턴 매칭 추론
and inferMatch (env: TypeEnv) (scrutinee: Expr) (cases: (Pattern * Expr option * Expr) list) : InferResult =
    // ... implementation

/// 패턴 타입 추론
and inferPattern (pattern: Pattern) : Result<TypeEnv * Type, TypeError> =
    // ... implementation

/// 환경에 치환 적용
and applyEnv (s: Substitution) (env: TypeEnv) : TypeEnv =
    Map.map (fun _ (Forall (vars, t)) -> Forall (vars, TypeHelpers.apply s t)) env
```

---

### Step 4: Type Annotations (Parser 변경)

**문법 추가:**

```
// 타입 어노테이션
type_expr:
    | INT_TYPE                     { TInt }
    | BOOL_TYPE                    { TBool }
    | STRING_TYPE                  { TString }
    | UNIT_TYPE                    { TUnit }
    | type_expr ARROW type_expr    { TFun ($1, $3) }
    | type_expr LIST_TYPE          { TList $1 }
    | LPAREN type_list RPAREN      { TTuple $2 }
    | LPAREN type_expr RPAREN      { $2 }
    ;

// 타입 어노테이션이 있는 let
let_expr:
    | LET IDENT COLON type_expr EQ expr IN expr
        { ELetAnnotated ($2, $4, $6, $8) }
    | LET IDENT EQ expr IN expr
        { ELet ($2, $4, $6) }
    ;

// 타입 어노테이션이 있는 lambda
lambda_expr:
    | FUN LPAREN IDENT COLON type_expr RPAREN ARROW expr
        { ELambdaAnnotated ($3, $5, $8) }
    | FUN IDENT ARROW expr
        { ELambda ($2, $4) }
    ;
```

**Lexer 토큰 추가:**

```fsl
| "int"     { INT_TYPE }
| "bool"    { BOOL_TYPE }
| "string"  { STRING_TYPE }
| "unit"    { UNIT_TYPE }
| "list"    { LIST_TYPE }
| "->"      { ARROW }
| ":"       { COLON }
```

---

### Step 5: Error Messages (TypeErrors.fs)

**파일:** `src/FunLang/TypeErrors.fs`

```fsharp
module FunLang.TypeErrors

open FunLang.Types

/// 타입을 읽기 좋은 문자열로 변환
let rec formatType (t: Type) : string =
    match t with
    | TInt -> "int"
    | TBool -> "bool"
    | TString -> "string"
    | TUnit -> "unit"
    | TVar v -> sprintf "'a%d" v
    | TFun (t1, t2) ->
        let left = match t1 with TFun _ -> sprintf "(%s)" (formatType t1) | _ -> formatType t1
        sprintf "%s -> %s" left (formatType t2)
    | TList t1 -> sprintf "%s list" (formatType t1)
    | TTuple ts -> ts |> List.map formatType |> String.concat " * " |> sprintf "(%s)"

/// 에러 메시지 포맷팅
let formatError (err: TypeError) : string =
    let main =
        match err.Kind with
        | UnboundVariable name ->
            sprintf "Error: Unbound variable '%s'" name
        | TypeMismatch (expected, actual) ->
            sprintf "Error: Type mismatch\n  Expected: %s\n  Actual: %s"
                (formatType expected) (formatType actual)
        | OccursCheck (v, t) ->
            sprintf "Error: Infinite type detected\n  Cannot construct type '%a%d = %s'"
                v (formatType t)
        | ArityMismatch (expected, actual) ->
            sprintf "Error: Wrong number of arguments\n  Expected: %d\n  Actual: %d"
                expected actual
        | NotAFunction t ->
            sprintf "Error: Not a function\n  Type: %s\n  Cannot apply arguments to non-function"
                (formatType t)
        | PatternTypeMismatch (expected, actual) ->
            sprintf "Error: Pattern type mismatch\n  Pattern expects: %s\n  Actual: %s"
                (formatType expected) (formatType actual)

    let position =
        match err.Position with
        | Some pos -> sprintf "\n  at line %d, column %d" pos.Line pos.Column
        | None -> ""

    let hint =
        match err.Hint with
        | Some h -> sprintf "\n  Hint: %s" h
        | None -> ""

    main + position + hint
```

---

### Step 6: Integration

**Program.fs 수정:**

```fsharp
let runWithTypeCheck input =
    result {
        let! tokens = Lexer.tokenize input
        let! ast = Parser.parse tokens
        let! (_, inferredType) = TypeInfer.infer Map.empty ast
        if options.ShowTypes then
            printfn "Type: %s" (TypeErrors.formatType inferredType)
        let! value = Interpreter.eval Map.empty ast
        return value
    }
```

**REPL :type 명령어:**

```fsharp
| ":type" ->
    match TypeInfer.infer env expr with
    | Ok (_, t) -> printfn "%s" (TypeErrors.formatType t)
    | Error e -> printfn "%s" (TypeErrors.formatError e)
```

---

## 파일 구조

```
src/FunLang/
├── Types.fs           # NEW: 타입 정의
├── Unification.fs     # NEW: 통합 알고리즘
├── TypeInfer.fs       # NEW: Algorithm W
├── TypeErrors.fs      # NEW: 에러 포맷팅
├── Ast.fs             # 수정: 타입 어노테이션 AST
├── Parser.fsy         # 수정: 타입 문법
├── Lexer.fsl          # 수정: 타입 토큰
├── Interpreter.fs     # 수정: 타입 체크 통합
├── Repl.fs            # 수정: :type 명령어
└── Program.fs         # 수정: --show-types

tests/FunLang.Tests/
├── TypeTests.fs       # NEW: 타입 추론 테스트
├── UnificationTests.fs # NEW: 통합 테스트
└── ...
```

**컴파일 순서 (.fsproj):**

```xml
<Compile Include="Types.fs" />
<Compile Include="Unification.fs" />
<Compile Include="TypeInfer.fs" />
<Compile Include="TypeErrors.fs" />
<!-- 기존 파일들 -->
```

---

## 테스트 전략

### Property-Based Tests

```fsharp
testProperty "well-typed expressions evaluate without runtime type errors" <|
    fun (expr: Expr) ->
        match TypeInfer.infer Map.empty expr with
        | Ok _ ->
            match Interpreter.eval Map.empty expr with
            | Ok _ -> true
            | Error e -> not (isTypeError e)
        | Error _ -> true  // 타입 에러면 평가 안함

testProperty "type inference is deterministic" <|
    fun (expr: Expr) ->
        let t1 = TypeInfer.infer Map.empty expr
        let t2 = TypeInfer.infer Map.empty expr
        t1 = t2

testProperty "substitution composition is associative" <|
    fun (s1: Substitution) (s2: Substitution) (s3: Substitution) ->
        compose (compose s1 s2) s3 = compose s1 (compose s2 s3)
```

### Unit Tests

```fsharp
testList "Type Inference" [
    test "infer integer literal" {
        let result = infer Map.empty (EInt 42)
        Expect.equal result (Ok (Map.empty, TInt)) ""
    }

    test "infer identity function" {
        let expr = ELambda ("x", EVar "x")
        let result = infer Map.empty expr
        match result with
        | Ok (_, TFun (TVar a, TVar b)) when a = b -> ()
        | _ -> failtest "expected α → α"
    }

    test "let-polymorphism" {
        // let id = fun x -> x in (id 1, id true)
        let expr =
            ELet ("id", ELambda ("x", EVar "x"),
                ETuple [EApp (EVar "id", EInt 1);
                        EApp (EVar "id", EBool true)])
        let result = infer Map.empty expr
        Expect.isOk result "should type check"
        match result with
        | Ok (_, TTuple [TInt; TBool]) -> ()
        | _ -> failtest "expected (int, bool)"
    }

    test "type error: int + bool" {
        let expr = EBinOp (Add, EInt 1, EBool true)
        let result = infer Map.empty expr
        Expect.isError result "should fail"
    }
]
```

---

## 구현 순서 (TDD)

1. **Types.fs** - 타입 정의 및 헬퍼 함수 (1일)
   - [ ] Type, TypeScheme 정의
   - [ ] Substitution 적용/합성
   - [ ] Free variables, generalize, instantiate

2. **UnificationTests.fs → Unification.fs** (1일)
   - [ ] 테스트 먼저 작성
   - [ ] occurs check
   - [ ] unify 구현

3. **TypeTests.fs → TypeInfer.fs** (2-3일)
   - [ ] 리터럴/변수 추론
   - [ ] Lambda/Application
   - [ ] Let/LetRec (polymorphism)
   - [ ] If/Binary operators
   - [ ] List/Tuple/Cons
   - [ ] Pattern matching

4. **TypeErrors.fs** (0.5일)
   - [ ] 에러 포맷팅
   - [ ] 위치 정보

5. **Parser/Lexer 수정** (1일)
   - [ ] 타입 어노테이션 문법
   - [ ] 토큰 추가

6. **Integration** (0.5일)
   - [ ] --show-types 옵션
   - [ ] :type REPL 명령어
   - [ ] 기존 테스트 통과 확인

---

## 검증 체크리스트

- [ ] 모든 기존 테스트 통과 (206개)
- [ ] Let-polymorphism 동작: `let id = fun x -> x in (id 1, id true)`
- [ ] 타입 에러 감지: `1 + true` 실패
- [ ] 재귀 함수 타입 추론: `let rec fact n = if n = 0 then 1 else n * fact (n-1)`
- [ ] 패턴 매칭 타입 추론
- [ ] 명확한 에러 메시지
- [ ] 타입 어노테이션 파싱
