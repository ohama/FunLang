# LangThree Test Cases Reference

Lexer/parser generator 검증을 위한 테스트 케이스 모음.

## 1. Test Infrastructure

### 1.1 실행 방법

```bash
# F# unit tests (224 tests)
dotnet test tests/LangThree.Tests/LangThree.Tests.fsproj

# fslit integration tests (521 tests)
/path/to/FsLit tests/flt/

# Expression mode (단일 표현식)
langthree --expr '1 + 2 * 3'

# File mode (파일 실행)
langthree myfile.l3

# AST 출력
langthree --emit-ast --expr '1 + 2'
langthree --emit-ast myfile.l3

# 타입 출력
langthree --emit-type --expr 'fun x -> x + 1'
langthree --emit-type myfile.l3
```

### 1.2 fslit 파일 형식

```
--- Command:
langthree %input
--- Input:
let result = 1 + 2
--- Output:
3
```

`%input`은 임시 파일 경로로 치환됨. 출력은 정확히 일치해야 함.

### 1.3 테스트 디렉토리 구조

```
tests/flt/
├── expr/           # 표현식 모드 테스트 (83 tests, 16 subdirs)
│   ├── arithmetic/ # 산술 연산
│   ├── boolean/    # 불리언 연산
│   ├── comparison/ # 비교 연산
│   ├── compose/    # 합성 연산자
│   ├── control/    # if-then-else
│   ├── lambda/     # 람다 함수
│   ├── let/        # let 바인딩
│   ├── list/       # 리스트 리터럴
│   ├── match/      # 패턴 매칭
│   ├── pipe/       # 파이프 연산자
│   ├── prelude/    # Prelude 함수
│   ├── print/      # 출력 함수
│   ├── string/     # 문자열 연산
│   ├── tuple/      # 튜플
│   ├── type-annot/ # 타입 주석
│   └── unit/       # 유닛 타입
├── file/           # 파일 모드 테스트 (331 tests, 26 subdirs)
│   ├── adt/        # ADT + GADT (17 tests)
│   ├── algorithm/  # 알고리즘 (27 tests)
│   ├── array/      # mutable array (18 tests)
│   ├── char/       # char type (6 tests)
│   ├── exception/  # 예외 처리 (9 tests)
│   ├── fileio/     # file I/O (8 tests)
│   ├── function/   # 함수 (12 tests)
│   ├── hashtable/  # hashtable (10 tests)
│   ├── implicit-in/# implicit in (8 tests)
│   ├── import/     # file import (3 tests)
│   ├── match/      # 패턴 매칭 (38 tests)
│   ├── module/     # 모듈 (11 tests)
│   ├── offside/    # offside rule (34 tests)
│   ├── operator/   # 사용자 정의 연산자 (16 tests)
│   ├── option/     # Option 타입 (6 tests)
│   ├── pipe/       # 파이프 (7 tests)
│   ├── prelude/    # Prelude (39 tests)
│   ├── print/      # 출력 (9 tests)
│   ├── range/      # 리스트 범위 (4 tests)
│   ├── record/     # 레코드 (11 tests)
│   ├── string/     # 문자열 (5 tests)
│   ├── tco/        # 꼬리 호출 최적화 (4 tests)
│   └── unit/       # 유닛 (3 tests)
├── emit/           # AST/타입 출력 테스트 (100 tests)
│   ├── ast-decl/   # 선언 AST 출력 (17 tests)
│   ├── ast-expr/   # 표현식 AST 출력 (32 tests)
│   ├── ast-pat/    # 패턴 AST 출력 (14 tests)
│   ├── type-decl/  # 선언 타입 출력 (17 tests)
│   └── type-expr/  # 표현식 타입 출력 (20 tests)
└── error/          # 에러 케이스 (4 tests)
```

## 2. Tokenization Test Cases

### 2.1 키워드 vs 식별자

```
Input: let x = 1
Tokens: LET IDENT("x") EQUALS NUMBER(1) EOF

Input: letter = true
Tokens: IDENT("letter") EQUALS TRUE EOF

Input: if_then = false
Tokens: IDENT("if_then") EQUALS FALSE EOF
```

### 2.2 연산자

```
Input: 1 + 2 * 3
Tokens: NUMBER(1) PLUS NUMBER(2) STAR NUMBER(3) EOF

Input: x |> f >> g
Tokens: IDENT("x") PIPE_RIGHT IDENT("f") COMPOSE_RIGHT IDENT("g") EOF

Input: a :: b :: []
Tokens: IDENT("a") CONS IDENT("b") CONS LBRACKET RBRACKET EOF

Input: x <> y
Tokens: IDENT("x") NE IDENT("y") EOF
```

### 2.3 사용자 정의 연산자

```
Input: a ++ b
Tokens: IDENT("a") INFIXOP2("++") IDENT("b") EOF

Input: x <|> y
Tokens: IDENT("x") INFIXOP0("<|>") IDENT("y") EOF

Input: s ^^ t
Tokens: IDENT("s") INFIXOP1("^^") IDENT("t") EOF
```

### 2.4 문자열

```
Input: "hello\nworld"
Tokens: STRING("hello\nworld") EOF

Input: "say \"hi\""
Tokens: STRING("say \"hi\"") EOF
```

### 2.5 들여쓰기

```
Input:
  let x =
      1 + 2
  x

Tokens: LET IDENT("x") EQUALS NEWLINE(6) NUMBER(1) PLUS NUMBER(2) NEWLINE(2) IDENT("x") EOF

After IndentFilter:
  LET IDENT("x") EQUALS INDENT NUMBER(1) PLUS NUMBER(2) DEDENT IDENT("x") EOF
```

## 3. Parsing Test Cases

### 3.1 연산자 우선순위

```
Input: 1 + 2 * 3
AST: Add(Number(1), Multiply(Number(2), Number(3)))
Result: 7

Input: (1 + 2) * 3
AST: Multiply(Add(Number(1), Number(2)), Number(3))
Result: 9

Input: 1 :: 2 :: []
AST: Cons(Number(1), Cons(Number(2), EmptyList))
Result: [1; 2]

Input: x |> f |> g
AST: PipeRight(PipeRight(Var("x"), Var("f")), Var("g"))
Meaning: g(f(x))
```

### 3.2 함수 적용 (좌결합)

```
Input: f x y
AST: App(App(Var("f"), Var("x")), Var("y"))

Input: f (g x)
AST: App(Var("f"), App(Var("g"), Var("x")))
```

### 3.3 리스트 리터럴 (세미콜론 구분)

```
Input: [1; 2; 3]
AST: List([Number(1), Number(2), Number(3)])
Result: [1; 2; 3]

Input: [(1, "a"); (2, "b")]
AST: List([Tuple([Number(1), String("a")]), Tuple([Number(2), String("b")])])
Result: [(1, "a"); (2, "b")]
```

### 3.4 튜플 (콤마 구분)

```
Input: (1, 2, 3)
AST: Tuple([Number(1), Number(2), Number(3)])
Result: (1, 2, 3)

Input: (1, "hello", true)
AST: Tuple([Number(1), String("hello"), Bool(true)])
```

### 3.5 ADT / GADT 선언

```
Input:
type Color =
    | Red
    | Green
    | Blue
AST: TypeDecl("Color", [], [ConstructorDecl("Red", None), ...])

Input:
type Expr 'a =
    | IntLit : int -> int Expr
    | BoolLit : bool -> bool Expr
AST: TypeDecl("Expr", ["'a"], [GadtConstructorDecl("IntLit", [TInt], TData("Expr", [TInt])), ...])
```

### 3.6 패턴 매칭

```
Input:
match x with
| 0 -> "zero"
| n when n > 0 -> "positive"
| _ -> "negative"

AST: Match(Var("x"), [
  (ConstPat(IntConst(0)), None, String("zero")),
  (VarPat("n"), Some(GreaterThan(Var("n"), Number(0))), String("positive")),
  (WildcardPat, None, String("negative"))
])
```

## 4. Indentation Edge Cases

### 4.1 다단계 Dedent

```
Input:
let a =
    let b =
        let c = 1
        c + 1
    b + 1
a

Tokens after filter:
  LET IDENT("a") EQUALS INDENT
  LET IDENT("b") EQUALS INDENT
  LET IDENT("c") EQUALS NUMBER(1)
  IN                              // implicit, offside col=8
  IDENT("c") PLUS NUMBER(1)
  DEDENT IN                       // dedent to col=4, implicit IN
  IDENT("b") PLUS NUMBER(1)
  DEDENT                          // dedent to col=0
  IDENT("a") EOF
```

### 4.2 Match Pipe Alignment

```
// OK — pipes align with match keyword
let result =
    match x with
    | 0 -> "zero"
    | _ -> "other"

// ERROR — pipe not aligned
let result =
    match x with
        | 0 -> "zero"     // IndentationError
```

### 4.3 Module Context (No Implicit IN)

```
module M =
    let x = 1
    let y = 2       // NO implicit IN between module-level lets
    let z = x + y

let result = M.z   // result = 3
```

### 4.4 Implicit IN in Expression Context

```
let result =
    let x = 10      // implicit IN after this
    let y = 20      // implicit IN after this
    x + y           // implicit IN before x + y returns to outer let
// result = 30
```

### 4.5 Single-line Match/Try

```
// Single-line match (no InMatch context needed)
let r = match x with | 0 -> "zero" | _ -> "other"

// Multi-line match (InMatch context with pipe alignment)
let r =
    match x with
    | 0 -> "zero"
    | _ -> "other"
```

## 5. Type Checking Test Cases

### 5.1 기본 타입 추론

```
Expression: fun x -> x + 1
Type: int -> int

Expression: fun x -> x
Type: 'a -> 'a

Expression: fun f -> fun x -> f (f x)
Type: ('a -> 'a) -> 'a -> 'a
```

### 5.2 GADT 타입 정제

```
// Polymorphic return (no annotation)
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let eval e = match e with | IntLit n -> n | BoolLit b -> b
eval (IntLit 42)    → 42 : int
eval (BoolLit true) → true : bool

// Concrete annotation
let eval_int e = (match e with | IntLit n -> n : int)
eval_int (IntLit 7) → 7 : int

// Exhaustiveness: BoolLit branch not needed for int Expr
// No W0001 warning
```

### 5.3 에러 케이스

```
// Division by zero
1 / 0 → Error: Division by zero

// Type mismatch
1 + "hello" → error[E0301]: Type mismatch: expected int but got string

// Unbound variable
x + 1 → error[E0303]: Unbound variable: x
```

## 6. Runtime Test Cases

### 6.1 꼬리 호출 최적화

```
// This must NOT stack overflow
let rec loop n = if n = 0 then 0 else loop (n - 1)
loop 1000000 → 0
```

### 6.2 Prelude 함수

```
map (fun x -> x * 2) [1; 2; 3] → [2; 4; 6]
filter (fun x -> x > 3) [1; 2; 3; 4; 5] → [4; 5]
fold (fun acc -> fun x -> acc + x) 0 [1; 2; 3] → 6
[1; 2] ++ [3; 4] → [1; 2; 3; 4]
```

### 6.3 사용자 정의 연산자

```
let (^^) a b = string_concat a b
"hello" ^^ " " ^^ "world" → "hello world"

let (++) xs ys = append xs ys
[1; 2] ++ [3; 4] → [1; 2; 3; 4]
```

### 6.4 상호 재귀

```
let rec even n = if n = 0 then true else odd (n - 1)
and odd n = if n = 0 then false else even (n - 1)

even 10 → true
odd 7 → true
```

### 6.5 레코드

```
type Point = { px: int; py: int }
let p = { px = 3; py = 4 }
p.px + p.py → 7

let q = { p with py = 10 }
q.py → 10
```

### 6.6 예외 처리

```
exception NotFound
let result =
    try raise NotFound
    with | NotFound -> 42
result → 42
```

## 7. AST Emission Format

`--emit-ast` 출력 형식:

```
// Expression
1 + 2 * 3
→ Add (Number 1, Multiply (Number 2, Number 3))

// List
[1; 2; 3]
→ List [Number 1; Number 2; Number 3]

// Tuple
(1, "hello")
→ Tuple [Number 1; String "hello"]

// Match
match x with | 0 -> "zero" | _ -> "other"
→ Match (Var "x", [(ConstPat (IntConst 0), String "zero"); (WildcardPat, String "other")])

// Lambda
fun x -> x + 1
→ Lambda ("x", Add (Var "x", Number 1))
```

## 8. Type Emission Format

`--emit-type` 출력 형식:

```
// File mode: each binding's type
let x = 42
let f x = x + 1
→ f : int -> int
  x : int

// Expression mode
fun x -> x
→ 'a -> 'a

fun f -> fun x -> f x
→ ('a -> 'b) -> 'a -> 'b
```
