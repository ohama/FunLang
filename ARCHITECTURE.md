# LangThree Architecture Guide

LangThree 인터프리터의 파이프라인 설계와 각 컴포넌트의 역할.

## 1. Pipeline Overview

```
Source Code
    │
    ▼
┌──────────┐     NEWLINE(col) tokens
│ Lexer.fsl │ ──────────────────────────►
└──────────┘
    │
    ▼
┌──────────────┐  INDENT / DEDENT / IN tokens
│ IndentFilter.fs │ ────────────────────►
└──────────────┘
    │
    ▼
┌──────────────┐     AST (Expr / Decl / Module)
│ Parser.fsy    │ ──────────────────────►
└──────────────┘
    │
    ▼
┌──────────────┐     Elaborated types
│ Elaborate.fs  │ ──────────────────────►  TypeExpr → Type
└──────────────┘
    │
    ▼
┌──────────────┐     Type-checked AST + substitutions
│ Bidir.fs      │ ──────────────────────►  synth/check
│ (TypeCheck.fs)│
└──────────────┘
    │
    ▼
┌──────────────┐     Values
│ Eval.fs       │ ──────────────────────►  runtime evaluation
└──────────────┘
```

각 단계는 이전 단계의 출력만 소비. 역방향 의존성 없음.

## 2. Component Details

### 2.1 Lexer (Lexer.fsl)

**역할:** 소스 코드 → 토큰 스트림

**핵심 설계:**
- fslex (FsLexYacc) 기반
- 들여쓰기를 `NEWLINE(col)` 토큰으로 전달 — INDENT/DEDENT 생성은 하지 않음
- 키워드는 식별자보다 먼저 매칭 (longest match rule)
- 사용자 정의 연산자(INFIXOP)는 선두 문자로 우선순위 분류
- `mut`과 `mutable` 모두 `MUTABLE` 토큰으로 매핑 (v4.0)
- `.[` → `DOTLBRACKET` 단일 토큰 (v5.0, `.IDENT` 필드접근과 LALR 충돌 회피)
- `while`, `for`, `to`, `downto`, `do` 키워드 추가 (v5.0)

**중요 패턴:**
- `NEWLINE(col)`: 줄바꿈 후 다음 줄의 첫 비공백 문자 column
- 블록 주석 `(* ... *)`: 중첩 가능 (depth counter)
- 문자열 리터럴: 이스케이프 시퀀스 처리 (`\n`, `\t`, `\\`, `\"`)

### 2.2 IndentFilter (IndentFilter.fs)

**역할:** `NEWLINE(col)` → `INDENT` / `DEDENT` / implicit `IN`

**핵심 설계:**
- Lexer와 Parser 사이에 삽입되는 토큰 필터
- Python의 indent stack 알고리즘 기반
- Context stack으로 match/try/let/module 문맥 추적

**Context Stack 타입:**
```fsharp
type SyntaxContext =
    | TopLevel
    | InMatch of baseColumn: int
    | InTry of baseColumn: int
    | InFunctionApp of baseColumn: int
    | InLetDecl of blockLet: bool * offsideCol: int
    | InExprBlock of baseColumn: int
    | InModule
```

**핵심 알고리즘:**

1. **INDENT/DEDENT 생성**: indent stack과 column 비교
2. **Offside rule**: `InLetDecl(blockLet=true)` 컨텍스트에서 offside column 도달 시 `IN` 삽입
3. **Pipe alignment**: `InMatch`/`InTry` 컨텍스트에서 `|` 파이프 column 검증
4. **Function application**: 다음 줄이 현재보다 들여쓰기되고 atom이면 `InFunctionApp` 진입
5. **Context push/pop**: INDENT 시 `InExprBlock`/`InModule` push, DEDENT 시 pop

**이 패턴이 중요한 이유:**
파서 문법을 context-free로 유지하면서 들여쓰기 기반 문법을 구현. 파서는 `INDENT`/`DEDENT`를 `{}`처럼 취급.

### 2.3 Parser (Parser.fsy)

**역할:** 토큰 스트림 → AST

**핵심 설계:**
- fsyacc (LALR(1)) 기반
- 연산자 우선순위: `%left`, `%right`, `%nonassoc` 선언
- 함수 적용: 좌결합 juxtaposition (가장 높은 우선순위)
- 사용자 정의 연산자: `App(App(Var(op), lhs), rhs)` 로 디슈거

**문법 계층:**
```
Expr → Term → Factor → AppExpr → Atom
```

- `SeqExpr`: Expr + `e1; e2` 시퀀싱 (문장 위치에서만 허용, v5.0)
- `Expr`: let, if, fun, match, try, while, for, 이항 연산자
- `Term`: `*`, `/`, `%`
- `Factor`: 단항 `-`, `raise`
- `AppExpr`: 함수 적용 (좌결합)
- `Atom`: 리터럴, 괄호, 리스트, 레코드

**다중 매개변수 함수 디슈거링:**
```
let f x y z = body
→ LetDecl("f", Lambda("x", Lambda("y", Lambda("z", body))))
```

**LALR(1) 충돌 처리:**
- 332 shift/reduce conflicts — 모두 shift 우선으로 올바르게 해결
- `INDENT Expr DEDENT` 규칙으로 인한 41개 추가 충돌 포함
- `TryWithClauses`: `try`/`with` 블록에서 파이프 없이 인라인으로 예외 핸들러를 작성하는 문법 지원

### 2.4 Elaborate (Elaborate.fs)

**역할:** `TypeExpr` (파서 AST) → `Type` (내부 타입)

- `TEInt` → `TInt`
- `TEVar("'a")` → `TVar(freshIndex)`
- `TEArrow(a, b)` → `TArrow(elaborate a, elaborate b)`
- `TEData("Expr", [TEInt])` → `TData("Expr", [TInt])`
- GADT 생성자: 인자 타입과 반환 타입 분리

### 2.5 Type Checker (Bidir.fs + TypeCheck.fs)

**역할:** AST + 타입 환경 → 타입 검증된 AST

**모듈 맵 전파 (v2.2):**
- `typeCheckModuleWithPrelude`는 `initialModules` 파라미터를 받아 파일 임포트 시 이미 로드된 모듈 맵을 전달
- 파일 임포트(`fileImportTypeChecker` 델리게이트)가 반환한 모듈 맵이 타입 체크 환경에 병합
- `Program.fs`가 prelude 모듈 맵을 타입 체크와 평가 단계로 스레딩
- `resolveImportPath`는 상대 경로를 임포트하는 파일의 디렉토리 기준으로 해석 (Rust 모듈 해석과 동일 원칙)

**Bidirectional Type Checking:**

```
synth(env, expr) → (substitution, type)     // 타입 추론
check(env, expr, expected) → substitution    // 타입 검증
```

- **synth**: 리터럴, 변수, 함수 적용에서 타입 추론
- **check**: 람다, if-then-else, match 분기에서 기대 타입과 비교
- **subsumption**: check에서 직접 처리 못하면 synth 후 unify

**Let-Polymorphism:**
```
let id x = x in id 42, id true
// id : forall 'a. 'a -> 'a (generalized)
// id 42 : int, id true : bool (instantiated)
```

**GADT Type Refinement:**

synth 모드에서 GADT match를 만나면:
1. Fresh type variable `freshTy` 생성
2. `check(env, matchExpr, freshTy)` 호출
3. check 모드 GADT 핸들러에서 분기별 독립적 정제

check 모드에서:
- `expected`가 TVar (polymorphic): 각 분기가 독립적으로 정제, cross-branch substitution 격리 (`isPolyExpected`)
- `expected`가 concrete: 기존 동작 (모든 분기가 같은 타입)

### 2.6 Evaluator (Eval.fs)

**역할:** 타입 검증된 AST → 런타임 값

**핵심 설계:**
- 환경 기반 평가 (Env = Map<string, Value>)
- Trampoline TCO (TailCall DU + while loop)
- BuiltinValue for native F# functions
- MatchCompile.fs: 패턴 → 결정 트리 컴파일 (Jules Jacobs 알고리즘)

**Value 타입:**
```fsharp
type Value =
    | IntValue of int
    | BoolValue of bool
    | StringValue of string
    | CharValue of char
    | FunctionValue of param * body * closure_env
    | BuiltinValue of (Value -> Value)
    | TupleValue of Value list
    | ListValue of Value list
    | ArrayValue of Value array
    | HashtableValue of Dictionary<Value, Value>
    | StringBuilderValue of StringBuilder  // Mutable string builder (v7.0)
    | HashSetValue of HashSet<Value>       // Mutable unique-element set (v7.0)
    | QueueValue of Queue<Value>           // Mutable FIFO queue (v7.0)
    | MutableListValue of List<Value>      // Mutable resizable list (v7.0)
    | DataValue of constructorName * Value option
    | RecordValue of typeName * Map<string, Value ref>
    | TailCall of func * arg  // TCO trampoline
    | RefValue of Value ref   // Mutable variable (v4.0)
```

**Prelude 로딩:**
1. `Prelude/` 디렉토리의 `.fun` 파일을 의존성 분석 후 토폴로지 정렬 순서로 로드
2. 각 파일을 `module <Stem> = ...` 블록으로 래핑 후 `open <Stem>` 삽입 → 비정규화 접근 가능
3. 각 파일을 파싱 → 타입 체크 → 평가
4. `PreludeResult`는 `Modules`(타입 모듈 맵)와 `ModuleValueEnv`(값 모듈 맵) 필드를 포함
5. `Program.fs`가 prelude 모듈 맵을 타입 체크 및 평가 단계로 스레딩하여 정규화 접근(`List.map` 등)을 보장

## 3. Data Flow Example

`let x = [1; 2; 3]` 의 처리 과정:

```
1. Lexer:     LET IDENT("x") EQUALS LBRACKET NUMBER(1) SEMICOLON
              NUMBER(2) SEMICOLON NUMBER(3) RBRACKET

2. IndentFilter: (통과 — 들여쓰기 변화 없음)

3. Parser:    LetDecl("x", List([Number(1), Number(2), Number(3)]))

4. Elaborate: (타입 표현식 없음 — 건너뜀)

5. TypeCheck: synth → TList(TInt), substitution = {}

6. Eval:      ListValue([IntValue(1), IntValue(2), IntValue(3)])
```

## 4. File Structure

```
src/LangThree/
├── Lexer.fsl          # Token definitions (fslex)
├── Parser.fsy         # Grammar rules (fsyacc)
├── Ast.fs             # AST node types
├── IndentFilter.fs    # NEWLINE → INDENT/DEDENT/IN
├── Type.fs            # Type definitions, unification
├── Infer.fs           # Fresh variables, instantiation, generalization
├── Elaborate.fs       # TypeExpr → Type
├── Bidir.fs           # Bidirectional type checking (synth/check)
├── TypeCheck.fs       # Top-level type checking orchestration
├── Unify.fs           # Type unification algorithm
├── Exhaustive.fs      # Pattern exhaustiveness checking
├── MatchCompile.fs    # Pattern → decision tree compilation
├── Eval.fs            # Runtime evaluation + built-in functions
├── Prelude.fs         # Prelude loading, file import delegates
├── Format.fs          # AST/type pretty-printing
├── Diagnostic.fs      # Error/warning message formatting
├── Program.fs         # CLI entry point (--expr, file, --emit-ast, etc.)
├── Repl.fs            # Interactive REPL
└── LangThree.fsproj   # Project file
```

## 5. Key Design Patterns

### 5.1 Core Types

**기본 타입:**
- `int`, `bool`, `string`, `char`, `unit`
- `TList of Type` — 동종 리스트
- `TArray of Type` — 뮤터블 배열
- `THashtable of Type * Type` — 해시 테이블 (키 타입, 값 타입)
- 네이티브 컬렉션 (v7.0): StringBuilder, HashSet, Queue, MutableList (타입 시스템에서는 opaque)
- `TTuple of Type list` — 튜플
- `TArrow of Type * Type` — 함수 타입
- `TData of name * Type list` — 사용자 정의 ADT / GADT
- `TRecord of name * fields` — 레코드
- `TVar of int` — 타입 변수 (추론용)
- `TForall of int * Type` — 전칭 다형 타입

### 5.2 Token Filter Pattern

Lexer → **Filter** → Parser 구조로 들여쓰기를 처리. 파서 문법을 context-free로 유지.
이 패턴은 Python, F#, Haskell 모두 사용.

### 5.3 Context Stack Pattern

IndentFilter의 `SyntaxContext list`로 중첩된 문법 구조 추적.
Counter 대신 stack을 쓰면 중첩과 문맥 구분이 자연스럽게 해결.

### 5.4 Bidirectional Type Checking

`synth`와 `check` 두 모드로 타입 추론과 검증을 분리.
GADT 타입 정제는 check 모드에서만 동작 — synth는 fresh var로 위임.

### 5.5 Trampoline TCO

`TailCall(func, arg)` DU + while loop으로 꼬리 호출 최적화.
스택 오버플로 없이 무한 재귀 가능.

### 5.6 Decision Tree Pattern Compilation

Jules Jacobs 알고리즘으로 패턴 → 이진 결정 트리 컴파일.
순차 비교 대신 최소한의 비교로 올바른 분기에 도달.

### 5.7 Module Map Threading

`PreludeResult`는 `Modules`(타입 모듈 맵)와 `ModuleValueEnv`(값 모듈 맵)를 함께 전달.
`fileImportTypeChecker` 델리게이트는 임포트된 파일의 모듈 맵을 반환하고, 호출자가 이를 현재 환경에 병합.
`Program.fs`가 prelude 모듈 맵을 타입 체크 → 평가 단계 전체에 스레딩하여 Prelude와 임포트된 파일 모두에서 정규화 접근이 동작하도록 보장.

### 5.8 callValueRef Forward Reference

`Array.map`, `Array.fold` 등 HOF 빌트인은 사용자 함수를 호출해야 하지만 `eval`을 직접 참조할 수 없음.
`callValueRef: (Value -> Value -> Value) ref` 뮤터블 ref 패턴으로 해결:
빌트인 정의 시점에 ref를 플레이스홀더로 생성하고, `eval` 함수가 정의된 후 실제 구현으로 채움.
순환 의존성 없이 HOF 빌트인과 평가기를 연결하는 표준 F# 패턴.
