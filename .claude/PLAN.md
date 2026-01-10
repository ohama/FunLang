# FunLang Interpreter Implementation Plan

## Overview

**Goal:** Multi-paradigm 함수형 언어 인터프리터 (F#/Scala 스타일)
**Type System:** 정적 타입 + Hindley-Milner 타입 추론
**Parser:** FsLexYacc (이미 프로젝트에 설정됨)

---

## Phase 1: Core Expressions (기초)

### 목표
숫자, 산술 연산, let 바인딩 파싱 및 평가

### 파일별 작업

**Ast.fs**
```fsharp
type Literal = LInt of int | LFloat of float | LBool of bool | LString of string | LUnit
type BinaryOp = Add | Sub | Mul | Div | Mod | Eq | Neq | Lt | Gt | Lte | Gte | And | Or
type UnaryOp = Neg | Not
type Expr =
    | ELiteral of Literal
    | EVariable of string
    | EBinaryOp of BinaryOp * Expr * Expr
    | EUnaryOp of UnaryOp * Expr
    | ELet of string * Expr * Expr
```

**Lexer.fsl** - 토큰: `INT`, `FLOAT`, `IDENT`, `LET`, `IN`, `+`, `-`, `*`, `/`, `(`, `)`, `=`

**Parser.fsy** - 연산자 우선순위와 함께 표현식 문법 정의

**Interpreter.fs** - `Value` 타입과 `eval` 함수 구현

**Program.fs** - 파싱 → 평가 파이프라인 연결

---

## Phase 2: Functions & Control Flow

### 목표
일급 함수(람다), 함수 호출, 재귀, if/else

### AST 추가
```fsharp
| ELambda of string * Expr
| EApply of Expr * Expr
| EIf of Expr * Expr * Expr
| ELetRec of string * Expr * Expr
```

### 새 토큰
`FUN`, `ARROW (->)`, `IF`, `THEN`, `ELSE`, `TRUE`, `FALSE`, `REC`

### Interpreter
- `VClosure` (클로저)와 `VRecClosure` (재귀 클로저) 추가
- 함수 적용시 환경 캡처 및 확장

---

## Phase 3: Data Structures

### 목표
튜플과 리스트

### AST 추가
```fsharp
| ETuple of Expr list
| EList of Expr list
| ECons of Expr * Expr
```

### 새 토큰
`[`, `]`, `,`, `::`, `;`

### Value 추가
`VTuple of Value list`, `VList of Value list`

---

## Phase 4: Pattern Matching

### 목표
match 표현식과 패턴

### AST 추가
```fsharp
type Pattern =
    | PWildcard | PVariable of string | PLiteral of Literal
    | PTuple of Pattern list | PList of Pattern list
    | PCons of Pattern * Pattern | PConstructor of string * Pattern option

type Expr = ... | EMatch of Expr * (Pattern * Expr option * Expr) list
```

### 새 토큰
`MATCH`, `WITH`, `|`, `_`, `WHEN`

### Interpreter
- `matchPattern: Pattern -> Value -> Map<string,Value> option`
- 패턴 매칭 실패시 다음 케이스로 이동

---

## Phase 5: Type System (Hindley-Milner)

### 목표
정적 타입 검사 + 타입 추론

### 새 파일: Types.fs
```fsharp
type Type = TInt | TFloat | TBool | TString | TUnit
          | TVar of int | TFun of Type * Type
          | TTuple of Type list | TList of Type
type TypeScheme = { Quantified: Set<int>; Type: Type }
```

### 새 파일: TypeInference.fs
- `unify`: 두 타입 통합
- `infer`: Algorithm W 구현
- `generalize` / `instantiate`: let-다형성

### .fsproj 수정
Types.fs, TypeInference.fs를 컴파일 순서에 추가

---

## Phase 6: User-Defined Types

### 목표
discriminated union 정의

### 문법 예시
```
type Option 'a = None | Some of 'a
type List 'a = Nil | Cons of 'a * List 'a
```

### AST 추가
```fsharp
type TypeDef = UnionDef of string * string list * (string * Type option) list
type Program = { TypeDefs: TypeDef list; MainExpr: Expr }
| EConstructor of string * Expr option
```

### 새 토큰
`TYPE`, `OF`, `'` (타입 파라미터용)

---

## Critical Files

| 파일 | 역할 |
|------|------|
| `src/FunLang/Ast.fs` | AST 타입 정의 |
| `src/FunLang/Parser.fsy` | 문법 규칙 |
| `src/FunLang/Lexer.fsl` | 토큰화 규칙 |
| `src/FunLang/Interpreter.fs` | 평가기 |
| `src/FunLang/Program.fs` | 진입점 |
| `src/FunLang/Types.fs` | (Phase 5) 타입 정의 |
| `src/FunLang/TypeInference.fs` | (Phase 5) 타입 추론 |
| `src/FunLang/FunLang.fsproj` | 프로젝트 설정 |

---

## Implementation Order

```
Phase 1 ─────> Phase 2 ────┐
                           ├──> Phase 4 ──> Phase 5 ──> Phase 6
Phase 1 ─────> Phase 3 ────┘
```

Phase 2와 3은 Phase 1 이후 병렬 진행 가능

---

## Verification

각 Phase 완료 후:
1. `dotnet build` - 컴파일 성공 확인
2. `dotnet run` - REPL 또는 테스트 코드 실행
3. 예시 프로그램 테스트:
   - Phase 1: `let x = 1 + 2 in x * 3` → `9`
   - Phase 2: `let rec fact = fun n -> if n = 0 then 1 else n * fact (n - 1) in fact 5` → `120`
   - Phase 3: `[1; 2; 3]`, `(1, "hello", true)`
   - Phase 4: `match [1;2] with | [] -> 0 | x::_ -> x` → `1`
   - Phase 5: 타입 추론 결과 출력
   - Phase 6: `type Option 'a = None | Some of 'a` 정의 및 사용

---

## Notes

- FsLexYacc는 LALR(1) 파서 - shift/reduce 충돌 주의
- 연산자 우선순위는 `%left`, `%right`, `%nonassoc`로 선언
- 컴파일 순서: Ast.fs → Parser.fsy → Lexer.fsl → Interpreter.fs → Program.fs
- 에러 메시지에 위치 정보 포함 권장
