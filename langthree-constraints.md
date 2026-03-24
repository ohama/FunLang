# LangThree Language Constraints

FunLexYacc 프로젝트 (Phase 1-7) 개발 과정에서 발견된 LangThree v1.8 언어의 제약사항을 정리한 문서.
각 항목에 대해 **현재 workaround**와 **개선 제안**을 포함.

발견 출처: `.planning/phases/` 디렉토리의 SUMMARY.md, RESEARCH.md 파일들

---

## 1. Module System

### 1.1 No Import Mechanism
- **증상**: `open Module` 이나 `import` 없음. 모듈을 사용하려면 파일을 물리적으로 연결(cat)해야 함.
- **Workaround**: `cat module1.fun module2.fun > combined.fun` 패턴으로 모듈 연결
- **발견**: Phase 1 (01-01), Phase 2 (02-03)
- **영향**: 모든 테스트 하네스, 빌드 스크립트가 sed + cat 파이프라인에 의존
- **개선 제안**: `open ModuleName` 또는 `#include "path.fun"` 지원

### 1.2 Multiple Module Declarations Cause Parse Error
- **증상**: 연결된 파일에 `module A` ... `module B` 두 개 이상의 module 선언이 있으면 파싱 에러
- **Workaround**: `sed '/^module ModuleName$/d'` 로 두 번째 이후 module 선언을 제거
- **발견**: Phase 3 (03-02)
- **영향**: 모든 빌드 스크립트에 sed 제거 단계 필요
- **개선 제안**: 연결된 파일에서 여러 module 선언을 허용하거나, 선언을 무시하는 모드 추가

### 1.3 Empty .fun Files Crash Interpreter
- **증상**: 바인딩이 없는 빈 .fun 파일을 실행하면 인터프리터가 크래시
- **Workaround**: `let placeholder = 0` 같은 더미 바인딩 추가
- **발견**: Phase 1 (01-01)
- **개선 제안**: 빈 모듈을 정상적으로 처리 (빈 프로그램 = unit)

### 1.4 Prelude Not Available Outside LangThree Directory
- **증상**: FunLexYacc 디렉토리에서 .fun 파일 실행 시 Prelude의 `my_append`, `my_reverse`, `Option` 등 사용 불가
- **Workaround**: 각 .fun 파일에 필요한 유틸리티 함수를 인라인으로 재정의
- **발견**: Phase 2 (02-01)
- **영향**: 모든 .fun 파일이 `Option`, `fst`, `snd`, `not` 등을 자체 정의해야 함
- **개선 제안**: Prelude를 인터프리터에 내장하거나 경로 기반 로딩 지원

---

## 2. Type System

### 2.1 Only 2-Tuples Supported
- **증상**: `(a, b, c)` 3-tuple 이상 사용 시 타입 에러. `fst`/`snd`는 2-tuple에만 동작.
- **Workaround**: 레코드 타입으로 대체하거나 중첩 2-tuple `(a, (b, c))` 사용
- **발견**: Phase 3 (03-03), Phase 5 (05-03), Phase 6 (06-01)
- **영향**: SubsetAcc, LrItem 등 복합 데이터를 모두 레코드로 정의해야 함
- **개선 제안**: N-tuple 지원 (`fst`, `snd` → `nth` 또는 패턴 매칭)

### 2.2 No Let-Tuple Destructuring
- **증상**: `let (a, b) = expr` 구문이 파싱 에러
- **Workaround**: `let r = expr` 후 `fst r`, `snd r` 사용
- **발견**: Phase 3 (03-02)
- **영향**: 함수에서 튜플을 반환하는 패턴이 항상 2단계로 분리
- **개선 제안**: `let (a, b) = expr` 패턴 매칭 지원

### 2.3 Option Type Must Be Uppercase
- **증상**: `Option 'a` (대문자)만 유효. `option` (소문자)은 별개의 타입. `None`/`Some`은 `Option`에만 연결됨.
- **Workaround**: 항상 `Option` (대문자 O) 사용. Prelude 외부에서는 직접 `type Option 'a = None | Some of 'a` 정의.
- **발견**: Phase 2 (02-01)
- **영향**: F#에서 `option` (소문자)을 사용하는 코드를 포팅할 때 혼동
- **개선 제안**: `option`을 `Option`의 별칭으로 추가하거나 대소문자 무관하게 처리

### 2.4 Record Field Name Collision Across Types
- **증상**: 서로 다른 레코드 타입이 같은 필드명(예: `id`, `prod`)을 가지면 충돌 에러. LangThree는 필드명으로 레코드 타입을 추론.
- **Workaround**: 타입별 접두사 규칙 — `fp_` (FlatProd), `gi_` (GrammarInfo), `sm_` (SymbolMap), `li_` (LrItem), `d_` (DfaState) 등
- **발견**: Phase 3 (03-03), Phase 5 (05-01)
- **영향**: 모든 레코드 타입에 접두사 필요, 코드 가독성 저하
- **개선 제안**: 필드명 해결에 타입 어노테이션 또는 모듈 스코프 사용

---

## 3. Syntax Restrictions

### 3.1 No `let rec` Inside Function Bodies
- **증상**: 함수 본문이나 match arm 안에서 `let rec` 사용 시 파싱 에러
- **Workaround**: 모든 재귀 헬퍼 함수를 최상위(top-level) `let rec`으로 추출
- **발견**: Phase 2 (02-01, 02-02)
- **영향**: 지역 재귀 함수를 사용할 수 없어 top-level이 복잡해짐
- **개선 제안**: `let rec ... in ...` 을 함수 본문 내에서 허용

### 3.2 No `()` Unit Literal
- **증상**: `fun () -> expr` 또는 `f ()` 같은 unit 인자 전달이 안됨
- **Workaround**: `_u` 더미 매개변수 사용. `let f _u = expr` 후 `f 0` 으로 호출.
- **발견**: Phase 3 (03-04)
- **개선 제안**: `()` unit 리터럴 및 `unit` 타입 지원

### 3.3 Multi-Line List Literals Cause Parse Error
- **증상**: 리스트 리터럴을 여러 줄에 걸쳐 작성하면 파싱 에러
  ```
  (* 에러 *)
  let xs = [1;
            2;
            3]
  ```
- **Workaround**: 모든 리스트 리터럴을 한 줄에 작성. `[1; 2; 3]`
- **발견**: Phase 3 (03-04), Phase 6 (06-01)
- **영향**: 큰 테이블/데이터를 리스트로 표현할 때 매우 긴 줄 발생
- **개선 제안**: 줄바꿈이 포함된 리스트 리터럴 허용

### 3.4 No Trailing Semicolons in List Literals
- **증상**: `[1; 2; ]` 처럼 마지막 요소 뒤에 세미콜론이 있으면 파싱 에러
- **Workaround**: 코드 생성 시 마지막 세미콜론 제거 로직 필요
- **발견**: Phase 3 (03-04)
- **개선 제안**: 선택적 trailing semicolon 허용 (OCaml, Rust 스타일)

### 3.5 No Single-Element List Pattern `[x]`
- **증상**: `match xs with | [x] -> ...` 패턴이 동작하지 않음
- **Workaround**: `| h :: rest -> match rest with | [] -> (* h is the single element *)` 중첩 매칭
- **발견**: Phase 3 (03-03)
- **개선 제안**: `[x]`, `[x; y]` 등 리스트 리터럴 패턴 지원

### 3.6 `let ... in` at Top-Level After Module Code Causes Parse Error
- **증상**: 연결된 모듈 코드 뒤에 `let x = ... in let y = ...` 형태의 top-level 표현식이 파싱 에러
- **Workaround**: 별도의 top-level `let` 바인딩 사용. `let x = ...` (in 없이)
- **발견**: Phase 3 (03-02)
- **개선 제안**: top-level 컨텍스트에서 `let ... in` 표현식 허용

### 3.7 Deeply Nested Function Bodies Cause Parse Errors
- **증상**: 함수 본문이 깊게 중첩되면 파싱 에러 발생
- **Workaround**: 중첩된 로직을 명명된 헬퍼 함수로 분리
- **발견**: Phase 5 (05-03)
- **개선 제안**: 파서의 중첩 깊이 제한 완화

---

## 4. Missing Builtins / Standard Library

### 4.1 No `failwith` Builtin
- **증상**: `failwith "message"` 사용 불가
- **Workaround**: `exception ParseError of string` 정의 후 `raise (ParseError "message")` 사용
- **발견**: Phase 3 (03-01)
- **개선 제안**: `failwith` 내장 함수 추가

### 4.2 No `List.hd` / `List.tl`
- **증상**: 리스트의 머리/꼬리를 가져오는 함수 없음
- **Workaround**: 패턴 매칭 `match xs with | h :: _ -> h`
- **발견**: Phase 3 (03-04)
- **개선 제안**: `List.hd`, `List.tl` (또는 `hd`, `tl`) 제공

### 4.3 No `char_to_int` / Character Type
- **증상**: 문자 타입 없음. `'a'` 같은 문자 리터럴을 정수로 변환하는 방법 없음.
- **Workaround**: 문자를 1-char 문자열로 처리하고 `string_sub s i 1` 사용. 문자 비교는 `string_sub s pos 1 = "{"` 패턴. 코드 포인트 필요 시 26개+ 등호 비교 체인.
- **발견**: Phase 3 (03-01, 03-04)
- **영향**: 렉서에서 문자 분류가 매우 비효율적 (26개 등호 비교로 대문자 판별)
- **개선 제안**: `char` 타입, `char_to_int`/`int_to_char`, 문자 비교 연산자 추가

### 4.4 No String Comparison Operators (`>=`, `<=`)
- **증상**: 문자열 순서 비교 연산자 없음 (`<`, `>`, `<=`, `>=`)
- **Workaround**: `is_upper_first`에 26개 등호 비교 (A=, B=, ... Z=) 사용
- **발견**: Phase 6 (06-03)
- **개선 제안**: 문자열 (또는 문자) 비교 연산자 추가

### 4.5 No File I/O — 상세

#### 현재 상태: 출력만 가능, 입력 불가

LangThree v1.8의 I/O 관련 builtin 함수는 **출력 전용**:

| 함수 | 타입 | 설명 |
|------|------|------|
| `print` | `string -> unit` | 줄바꿈 없이 stdout에 출력 (즉시 flush) |
| `println` | `string -> unit` | 줄바꿈 포함 stdout 출력 (즉시 flush) |
| `printf` | `string -> ...` | 포맷 문자열 출력 (%d, %s 등) |
| `printfn` | `string -> ...` | printf + 줄바꿈 |
| `to_string` | `'a -> string` | 임의 값을 문자열로 변환 |

**입력 함수는 하나도 없음:**
- `stdin_read_all` — 없음 (Unbound variable)
- `stdin_read_line` — 없음 (Unbound variable)
- `read_file` — 없음 (Unbound variable)
- `getchar` / `read_char` — 없음
- 커맨드라인 인자 접근 — 없음

#### 현재 Workaround: 쉘 스크립트 문자열 임베딩

파일 내용을 LangThree 프로그램에 전달하려면 쉘에서 escape 후 문자열 리터럴로 임베딩:

```bash
# Step 1: 파일 내용을 LangThree 문자열 리터럴로 escape
escaped=$(cat input.txt | sed 's/\\/\\\\/g; s/"/\\"/g; s/$/\\n/' | tr -d '\n')

# Step 2: .fun 파일에 문자열 변수로 삽입
echo "let file_content = \"${escaped}\"" >> program.fun

# Step 3: LangThree로 실행
LangThree program.fun
```

**이 패턴이 사용되는 곳 (FunLexYacc 프로젝트):**
- `tools/build-lexer.sh` — Lexer.funl 내용을 funlex 파이프라인에 임베딩
- `tools/build-parser.sh` — Parser.fsy 테이블 데이터를 파서 생성기에 임베딩
- `tests/harness/test-bootstrap.sh` — 442개 테스트 픽스처 각각을 문자열로 임베딩
- `tests/harness/test-funyparser.sh` — Parser.fsy 내용을 파서 테스트에 임베딩
- `tests/harness/test-parser-emit-e2e.sh` — 테스트 입력을 문자열로 임베딩

**문제점:**
1. 큰 파일 (Lexer.funl 172줄, Parser.fsy 642줄)을 단일 문자열 리터럴로 임베딩하면 매우 긴 줄 생성
2. 특수 문자 (백슬래시, 큰따옴표, 줄바꿈) escape 처리가 복잡하고 에러 유발
3. 바이너리 파일 처리 불가
4. 파일을 읽으려면 항상 외부 쉘 스크립트가 필요 — .fun 프로그램이 독립 실행 불가
5. 테스트 하네스가 느림 — 픽스처마다 쉘 escape + 파일 연결 오버헤드

#### 개선 제안: File I/O Builtins

**P0 — 필수 (즉시 추가 권장)**

| 함수 | 타입 | 설명 | 용도 |
|------|------|------|------|
| `read_file` | `string -> string` | 파일 경로를 받아 전체 내용을 문자열로 반환 | .funl/.funy 파일 읽기, 테스트 픽스처 로딩 |
| `stdin_read_all` | `unit -> string` | stdin 전체를 문자열로 읽기 (EOF까지) | 파이프 입력 처리 (`cat file \| LangThree prog.fun`) |
| `stdin_read_line` | `unit -> string` | stdin에서 한 줄 읽기 | 대화형 입력, 줄 단위 처리 |

**구현 예시 (Eval.fs에 추가):**
```fsharp
// read_file : string -> string
"read_file", BuiltinValue (fun v ->
    match v with
    | StringValue path ->
        try StringValue (System.IO.File.ReadAllText(path))
        with ex -> failwithf "read_file: cannot read '%s': %s" path ex.Message
    | _ -> failwith "read_file: expected string argument")

// stdin_read_all : unit -> string
"stdin_read_all", BuiltinValue (fun _ ->
    StringValue (stdin.ReadToEnd()))

// stdin_read_line : unit -> string
"stdin_read_line", BuiltinValue (fun _ ->
    let line = stdin.ReadLine()
    if isNull line then StringValue ""
    else StringValue line)
```

**P1 — 높은 우선순위**

| 함수 | 타입 | 설명 | 용도 |
|------|------|------|------|
| `write_file` | `string -> string -> unit` | `write_file path content` — 파일에 문자열 쓰기 | funlex/funyacc가 직접 .fun 파일 생성 |
| `append_file` | `string -> string -> unit` | `append_file path content` — 파일에 내용 추가 | 로그, 점진적 출력 |
| `file_exists` | `string -> bool` | 파일 존재 여부 확인 | 조건부 파일 처리 |

**구현 예시 (Eval.fs에 추가):**
```fsharp
// write_file : string -> string -> unit
"write_file", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue path ->
        BuiltinValue (fun v2 ->
            match v2 with
            | StringValue content ->
                System.IO.File.WriteAllText(path, content)
                TupleValue []
            | _ -> failwith "write_file: second argument must be string")
    | _ -> failwith "write_file: first argument must be string")

// append_file : string -> string -> unit
"append_file", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue path ->
        BuiltinValue (fun v2 ->
            match v2 with
            | StringValue content ->
                System.IO.File.AppendAllText(path, content)
                TupleValue []
            | _ -> failwith "append_file: second argument must be string")
    | _ -> failwith "append_file: first argument must be string")

// file_exists : string -> bool
"file_exists", BuiltinValue (fun v ->
    match v with
    | StringValue path -> BoolValue (System.IO.File.Exists(path))
    | _ -> failwith "file_exists: expected string argument")
```

**P2 — 있으면 좋음**

| 함수 | 타입 | 설명 | 용도 |
|------|------|------|------|
| `read_lines` | `string -> string list` | 파일을 줄 단위 리스트로 읽기 | 줄 단위 처리가 필요한 파서/변환기 |
| `write_lines` | `string -> string list -> unit` | 문자열 리스트를 줄바꿈 구분으로 파일에 쓰기 | 코드 생성 출력 |
| `get_args` | `unit -> string list` | 커맨드라인 인자 접근 | CLI 도구 작성 (funlex/funyacc를 독립 실행 가능하게) |
| `get_env` | `string -> string` | 환경 변수 읽기 | 설정, 경로 |
| `get_cwd` | `unit -> string` | 현재 작업 디렉토리 | 상대 경로 해석 |
| `path_combine` | `string -> string -> string` | 경로 결합 | OS 독립적 경로 처리 |
| `dir_files` | `string -> string list` | 디렉토리 내 파일 목록 | 테스트 러너, 배치 처리 |
| `eprint` | `string -> unit` | stderr에 출력 | 디버그/에러 메시지를 stdout과 분리 |
| `eprintln` | `string -> unit` | stderr에 줄바꿈 포함 출력 | 진행 상황 보고 |

**구현 예시:**
```fsharp
// get_args : unit -> string list
"get_args", BuiltinValue (fun _ ->
    let args = System.Environment.GetCommandLineArgs() |> Array.toList
    // Skip first element (executable path) and known flags
    let userArgs = args |> List.skipWhile (fun a -> a.StartsWith("-") || a.EndsWith(".fun"))
    ListValue (userArgs |> List.map StringValue))

// eprint : string -> unit
"eprint", BuiltinValue (fun v ->
    match v with
    | StringValue s ->
        stderr.Write(s)
        stderr.Flush()
        TupleValue []
    | _ -> failwith "eprint: expected string argument")

// eprintln : string -> unit
"eprintln", BuiltinValue (fun v ->
    match v with
    | StringValue s ->
        stderr.WriteLine(s)
        stderr.Flush()
        TupleValue []
    | _ -> failwith "eprintln: expected string argument")

// read_lines : string -> string list
"read_lines", BuiltinValue (fun v ->
    match v with
    | StringValue path ->
        try
            let lines = System.IO.File.ReadAllLines(path) |> Array.toList
            ListValue (lines |> List.map StringValue)
        with ex -> failwithf "read_lines: cannot read '%s': %s" path ex.Message
    | _ -> failwith "read_lines: expected string argument")

// dir_files : string -> string list
"dir_files", BuiltinValue (fun v ->
    match v with
    | StringValue path ->
        try
            let files = System.IO.Directory.GetFiles(path) |> Array.toList
            ListValue (files |> List.map StringValue)
        with ex -> failwithf "dir_files: cannot list '%s': %s" path ex.Message
    | _ -> failwith "dir_files: expected string argument")
```

#### 이 기능이 있으면 달라지는 것

**Before (현재 — 쉘 임베딩 패턴):**
```bash
# build-lexer.sh (외부 쉘 스크립트 필수)
escaped=$(cat src/Lexer/Lexer.funl | perl -pe 's/\\/\\\\/g; s/"/\\"/g; s/\n/\\n/g')
echo "let funl_input = \"${escaped}\"" >> gen.fun
echo "let _ = println (run_funlex funl_input)" >> gen.fun
LangThree gen.fun > generated/Lexer.fun
```

**After (File I/O 추가 후 — .fun 프로그램이 독립 실행):**
```
(* funlex_cli.fun — 쉘 스크립트 없이 직접 실행 가능 *)
let input_file = "src/Lexer/Lexer.funl"
let output_file = "generated/Lexer.fun"
let funl_content = read_file input_file
let result = run_funlex funl_content
let _ = write_file output_file result
let _ = eprintln (string_concat "Generated: " output_file)
```

**TypeCheck.fs에도 타입 추가 필요:**
```fsharp
// Phase 12 (Output functions) 에 추가:
"read_file", Scheme([], TArrow(TString, TString))
"write_file", Scheme([], TArrow(TString, TArrow(TString, TTuple [])))
"stdin_read_all", Scheme([], TArrow(TTuple [], TString))
"stdin_read_line", Scheme([], TArrow(TTuple [], TString))
"file_exists", Scheme([], TArrow(TString, TBool))
"eprint", Scheme([], TArrow(TString, TTuple []))
"eprintln", Scheme([], TArrow(TString, TTuple []))
```

### 4.6 No Mutable Data Structures
- **증상**: 배열, 해시맵 등 변경 가능한 자료구조 없음
- **Workaround**: 불변 리스트/맵으로 대체. 함수형 업데이트 패턴 사용.
- **발견**: Phase 5 (05-RESEARCH)
- **참고**: 함수형 언어의 설계 원칙에 맞지만, LALR 테이블 구축 같은 알고리즘에서 성능 제약
- **개선 제안**: 선택적 mutable array 또는 성능 최적화된 Map/Set 제공

### 4.7 No `seq` Expressions
- **증상**: F#의 `seq { yield ... }` 같은 시퀀스 표현식 없음
- **Workaround**: accumulator + reverse 패턴
- **발견**: Phase 2 (02-RESEARCH)
- **개선 제안**: 리스트 컴프리헨션 또는 `seq` 지원

---

## 5. Performance

### 5.1 Interpreter Too Slow for Large Computations
- **증상**: LangThree 인터프리터에서 전체 Parser.fsy (482 states) LALR 계산에 10분 이상 소요
- **Workaround**: 쉘 스크립트(`extract-tables.sh`)로 F# 바이너리에서 테이블을 사전 추출
- **발견**: Phase 5 (05-03)
- **영향**: funlex/funyacc가 큰 문법을 처리하려면 외부 도구에 의존
- **개선 제안**: 인터프리터 성능 최적화 (JIT, 바이트코드 컴파일 등)

---

## 6. Indentation / Formatting

### 6.1 `else` 뒤 표현식 시작 키워드에서 파싱 에러 — 상세

#### 증상

`else` 뒤에 `match`, `if`, `let`, `try`, `fun` 등 표현식 시작 키워드가 새 줄에 올 때 파싱 에러:

```
(* 모두 에러 *)
if cond then x
else match y with
    | a -> 1

if cond then x
else if y then 2 else 3

if cond then x
else let y = 2 in y + 1

if cond then x
else try someFunc 0 with | E -> 0
```

**Workaround**: `else`와 표현식을 같은 줄에 쓰거나, 들여쓰기 없이 분리:
```
if cond then x
else
    match y with
    | ...
```

**발견**: Phase 6 (06-02)

#### 근본 원인: IndentFilter에 ELSE 핸들러 부재

**Parser.fsy (문법) — 정상:**
```fsharp
(* Parser.fsy:120 — 문법은 else match를 허용 *)
| IF Expr THEN Expr ELSE Expr { If($2, $4, $6, ...) }
(* ELSE 뒤의 Expr에 MATCH Expr WITH MatchClauses 가 올 수 있음 *)
```

**문제: IndentFilter.fs가 ELSE 뒤에 잘못된 INDENT 토큰을 삽입**

토큰 스트림 변환 과정:
```
입력:  IF NUMBER THEN NUMBER ELSE NEWLINE(4) MATCH ...
                                      ↓
IndentFilter: IF NUMBER THEN NUMBER ELSE INDENT MATCH ...
                                         ^^^^^
                                         잘못 삽입됨!
```

Parser는 `ELSE Expr`을 기대하지만 `ELSE INDENT MATCH ...`를 받아서 에러.

#### 원인 분석 (IndentFilter.fs)

**1. FilterState에 `JustSawElse` 플래그 부재** (`IndentFilter.fs:24-36`)
```fsharp
type FilterState = {
    IndentStack: int list
    Context: SyntaxContext list
    JustSawMatch: bool      // ← 있음
    JustSawTry: bool        // ← 있음
    JustSawModule: bool     // ← 있음
    PrevToken: Parser.token option
    // JustSawElse: bool    // ← 없음!
}
```

**2. 토큰 처리 루프에 ELSE 케이스 없음** (`IndentFilter.fs:325-378`)
```fsharp
| Parser.MATCH ->
    state <- { state with JustSawMatch = true; ... }   // MATCH 특수 처리
| Parser.TRY ->
    state <- { state with JustSawTry = true; ... }     // TRY 특수 처리
| Parser.LET ->
    // LET 특수 처리 ...
| other ->
    state <- { state with PrevToken = Some other }     // ELSE는 여기로!
    yield other                                        // 아무 플래그도 안 세움
```

**3. NEWLINE 처리에서 ELSE 컨텍스트 무시** (`IndentFilter.fs:110-195`)
```fsharp
(* processNewlineWithContext *)
(* JustSawMatch → InMatch 컨텍스트 진입 (INDENT 억제) *)
(* JustSawTry → InTry 컨텍스트 진입 (INDENT 억제) *)
(* JustSawElse → 처리 없음! → 일반 INDENT 발생 *)
```

#### 수정 방법

**IndentFilter.fs 수정 필요 (Parser.fsy는 변경 불필요):**

**Step 1: FilterState에 플래그 추가** (`IndentFilter.fs:24-36`)
```fsharp
type FilterState = {
    // ... 기존 필드 ...
    JustSawElse: bool  // 추가
}
```

**Step 2: initialState에 초기값** (`IndentFilter.fs:36`)
```fsharp
let initialState = {
    // ... 기존 필드 ...
    JustSawElse = false
}
```

**Step 3: ELSE 토큰 핸들러 추가** (`IndentFilter.fs:325-354 사이`)
```fsharp
| Parser.ELSE ->
    state <- { state with JustSawElse = true; PrevToken = Some token }
    yield token
```

**Step 4: NEWLINE 처리에서 INDENT 억제** (`IndentFilter.fs:148-175`)
```fsharp
| _ ->
    if state.JustSawElse then
        // ELSE 뒤의 NEWLINE → INDENT 삽입하지 않음
        // 다음 토큰(match, if, let 등)이 표현식으로 파싱됨
        ({ stateWithTryContext with JustSawElse = false }, [])
    else
        // 일반 들여쓰기 처리
        let (newState, tokens) = processNewline config stateWithTryContext col
        (newState, tokens)
```

**Step 5: 다른 토큰에서 플래그 클리어**
```fsharp
(* MATCH, IF, LET, TRY, FUN 등 표현식 시작 토큰에서 *)
if state.JustSawElse then
    state <- { state with JustSawElse = false }
```

#### 동일 패턴의 영향 받는 구문들

| 패턴 | 현재 상태 | 수정 후 |
|------|----------|--------|
| `else match y with` | 에러 | 정상 |
| `else if y then` | 에러 | 정상 |
| `else let y = 2 in` | 에러 | 정상 |
| `else try f 0 with` | 에러 | 정상 |
| `else fun x -> x` | 에러 | 정상 |

#### 수정 파일

| 파일 | 변경 내용 |
|------|----------|
| `IndentFilter.fs:24-36` | `JustSawElse` 필드 추가 |
| `IndentFilter.fs:36` | initialState에 `JustSawElse = false` |
| `IndentFilter.fs:325-354` | ELSE 토큰 핸들러 추가 |
| `IndentFilter.fs:148-175` | NEWLINE 처리에서 INDENT 억제 |
| `IndentFilterTests.fs` | `else match`, `else if`, `else let`, `else try` 테스트 추가 |
| `Parser.fsy` | 변경 불필요 (문법은 이미 정확) |

---

## 7. Type Collision in Concatenated Files

### 7.1 Token/Expr Name Collision
- **증상**: Parser.fun의 `Expr` ADT가 `Let`, `Number`, `String`, `Match` 등 IndentFilter의 `Token` 타입과 동일한 생성자 이름을 재정의. 단일 파일 연결 시 이전 타입이 섀도잉됨.
- **Workaround**: 두 번에 나눠 실행 (tokenize 실행 → 쉘 변환 → parse 실행) 또는 타입 이름 접두사 (PToken)
- **발견**: Phase 7 (07-01)
- **영향**: 부트스트랩 파이프라인이 단일 실행 불가, 2단계 실행 필요
- **개선 제안**: 모듈 스코프로 타입 이름 격리 (e.g., `Parser.Token` vs `IndentFilter.Token`)

---

## Summary Table

| # | Category | Constraint | Severity | Workaround Cost |
|---|----------|-----------|----------|-----------------|
| 1.1 | Module | No import mechanism | HIGH | 모든 빌드에 cat/sed 파이프라인 |
| 1.2 | Module | Multiple module decls error | MEDIUM | sed 제거 |
| 1.3 | Module | Empty files crash | LOW | 더미 바인딩 |
| 1.4 | Module | Prelude unavailable outside dir | MEDIUM | 인라인 재정의 |
| 2.1 | Type | Only 2-tuples | HIGH | 레코드로 대체 |
| 2.2 | Type | No let-tuple destructuring | MEDIUM | fst/snd |
| 2.3 | Type | Option uppercase only | LOW | 규칙 숙지 |
| 2.4 | Type | Record field collision | HIGH | 접두사 규칙 |
| 3.1 | Syntax | No local let rec | MEDIUM | top-level 추출 |
| 3.2 | Syntax | No () unit | LOW | _u 더미 |
| 3.3 | Syntax | No multi-line lists | MEDIUM | 한 줄 리스트 |
| 3.4 | Syntax | No trailing semicolons | LOW | 생성기 로직 |
| 3.5 | Syntax | No [x] pattern | LOW | 중첩 매칭 |
| 3.6 | Syntax | No top-level let...in | LOW | 별도 let |
| 3.7 | Syntax | Deep nesting error | MEDIUM | 헬퍼 분리 |
| 4.1 | Stdlib | No failwith | LOW | exception + raise |
| 4.2 | Stdlib | No List.hd/tl | LOW | 패턴 매칭 |
| 4.3 | Stdlib | No char type | HIGH | 문자열 비교 체인 |
| 4.4 | Stdlib | No string comparison ops | MEDIUM | 등호 체인 |
| 4.5 | Stdlib | No file I/O (상세: read_file, stdin_read_all 등 전무) | HIGH | 쉘 임베딩 |
| 4.6 | Stdlib | No mutable structures | MEDIUM | 함수형 업데이트 |
| 4.7 | Stdlib | No seq expressions | LOW | accumulator |
| 5.1 | Perf | Slow interpreter | HIGH | 사전 계산 |
| 6.1 | Format | else + keyword error (IndentFilter ELSE 핸들러 부재) | MEDIUM | 줄바꿈 |
| 7.1 | Scope | Type name collision | HIGH | 2단계 실행 |

---

## Recommended Upgrade Priority

### P0 — Blocking (프로젝트 구조에 근본적 영향)
1. **Import mechanism** (1.1) — 모든 빌드/테스트의 cat/sed 해킹 제거
2. **Module scoping for types** (7.1, 2.4) — 타입 이름 충돌 해결
3. **File I/O** (4.5) — `read_file`, `stdin_read_all`, `write_file` 추가로 쉘 임베딩 해킹 제거

### P1 — High Impact (코드 품질 대폭 개선)
4. **N-tuples + destructuring** (2.1, 2.2) — 복합 데이터 처리 간소화
5. **Char type + char_to_int** (4.3, 4.4) — 렉서/문자 처리 효율화
6. **Multi-line list literals** (3.3) — 큰 데이터 리터럴 가독성
7. **Local let rec** (3.1) — 코드 구조화

### P2 — Nice to Have
8. **() unit literal** (3.2)
9. **failwith builtin** (4.1)
10. **[x] list pattern** (3.5)
11. **Trailing semicolons** (3.4)
12. **Interpreter performance** (5.1)

---
*Document created: 2026-03-24*
*Source: FunLexYacc .planning/phases/ SUMMARY.md and RESEARCH.md files (19 completed plans)*
