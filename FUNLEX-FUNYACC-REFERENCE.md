# funlex / funyacc Reference Guide

이 문서는 LangThree 언어를 대상으로 `funlex`(lexer generator)와 `funyacc`(parser generator)를 만드는 프로젝트를 위한 종합 참조 가이드입니다.

## 프로젝트 목표

LangThree는 현재 fslex/fsyacc(FsLexYacc)를 사용하여 lexer/parser를 생성합니다. `funlex`과 `funyacc`는 이를 대체하는 F# 기반 lexer/parser generator를 만드는 것이 목표입니다.

- **funlex**: `.fsl` 형식의 lexer 명세 → F# 소스 코드 생성 (DFA 기반 tokenizer)
- **funyacc**: `.fsy` 형식의 parser 명세 → F# 소스 코드 생성 (LALR(1) parser)
- **첫 번째 테스트 케이스**: LangThree의 `Lexer.fsl`과 `Parser.fsy`

## 참조 문서 목록

다른 프로젝트의 CLAUDE.md에 아래 내용을 포함하세요:

```markdown
## Reference: LangThree Language

funlex/funyacc의 첫 번째 대상 언어. 아래 문서를 참조:

### 언어 명세
- `../LangThree/SPEC.md` — 전체 언어 명세 (토큰 47+, BNF 문법 ~60규칙, 연산자 우선순위 13단계, 들여쓰기 규칙, 타입 시스템)
- `../LangThree/ARCHITECTURE.md` — 인터프리터 파이프라인 설계 (Lexer → IndentFilter → Parser → TypeCheck → Eval)
- `../LangThree/TESTS.md` — 토큰화/파싱/들여쓰기 edge case 테스트 케이스

### Generator 입력 파일 (첫 번째 테스트 케이스)
- `../LangThree/src/LangThree/Lexer.fsl` — funlex이 처리해야 할 실제 lexer 명세 (173줄)
- `../LangThree/src/LangThree/Parser.fsy` — funyacc이 처리해야 할 실제 parser 명세 (~500줄)

### Generator 형식 참조
- `../LangThree/FUNLEX-FUNYACC-REFERENCE.md` — fslex/fsyacc 입력 형식 상세 + 생성 코드 형태 + 핵심 알고리즘

### 언어 예제
- `../LangThree/tutorial/` — 16 chapters, 250+ runnable examples
- `../LangThree/tests/flt/` — 442 fslit integration tests
```

---

## Part 1: fslex 입력 형식 (funlex이 구현해야 할 것)

### 1.1 파일 구조

```
{                           ← F# 헤더 코드 (그대로 출력에 포함)
open System
open Parser
// ... helper functions ...
}                           ← 헤더 끝

// Character class definitions
let digit = ['0'-'9']
let letter = ['a'-'z' 'A'-'Z']
let ident_start = letter | '_'
let ident_char = letter | digit | '_'
// ...

// Lexer rules (각 rule은 별도의 DFA)
rule tokenize = parse
    | pattern1    { action1 }
    | pattern2    { action2 }
    // ...

// Additional rules (서로 호출 가능)
and read_indent col = parse
    | pattern    { action }
    // ...

and read_string buf = parse
    | pattern    { action }
    // ...
```

### 1.2 패턴 문법

| 패턴 | 의미 | 예시 |
|------|------|------|
| `'c'` | 문자 리터럴 | `'+'`, `'\n'` |
| `"str"` | 문자열 리터럴 | `"let"`, `"->"` |
| `['a'-'z']` | 문자 범위 | `['0'-'9']`, `['a'-'z' 'A'-'Z']` |
| `[^ chars]` | 문자 부정 | `[^ '\n' '\r']` |
| `p1 \| p2` | 대안 | `'\n' \| '\r' '\n'` |
| `p*` | 0회 이상 반복 | `digit*` |
| `p+` | 1회 이상 반복 | `digit+` |
| `p?` | 0 또는 1회 | (미사용) |
| `p1 p2` | 연결 | `letter digit*` |
| `ident` | 정의된 이름 참조 | `ident_start ident_char*` |
| `eof` | 입력 끝 | `eof` |
| `""` | 빈 문자열 (epsilon) | `""` (read_indent에서 사용) |
| `_` | 임의의 한 문자 | `_` |

### 1.3 액션 코드

중괄호 `{ }` 안의 F# 코드. 다음 바인딩이 사용 가능:

- `lexbuf`: `LexBuffer<char>` — 현재 lexer 버퍼
- `lexeme lexbuf`: 매칭된 문자열
- `lexbuf.EndPos`: 현재 위치 (줄/열)
- `lexbuf.EndPos.NextLine`: 줄바꿈 위치 업데이트

액션은 토큰을 반환하거나, 다른 rule을 호출:
```
{ NUMBER (Int32.Parse(lexeme lexbuf)) }   // 토큰 반환
{ tokenize lexbuf }                        // 다른 rule 호출 (재귀)
{ read_string (StringBuilder()) lexbuf }   // 다른 rule에 인자 전달
```

### 1.4 규칙 간 호출

`rule` / `and` 로 정의된 규칙들은 서로 호출 가능:
- `tokenize` → `read_indent`, `read_string`, `block_comment` 호출
- `read_indent` → 완료 시 `NEWLINE col` 반환 (tokenize로 돌아가지 않음)
- `block_comment` → 중첩 depth 추적, 완료 시 `tokenize lexbuf` 호출

규칙은 추가 매개변수를 받을 수 있음:
```
and read_indent col = parse       // col: int 매개변수
and block_comment depth = parse   // depth: int 매개변수
and read_string buf = parse       // buf: StringBuilder 매개변수
```

### 1.5 매칭 규칙

- **Longest match**: 여러 패턴이 매칭되면 가장 긴 것 선택
- **First match**: 길이가 같으면 먼저 선언된 것 선택
- 키워드(`let`, `if` 등)는 식별자 패턴보다 먼저 선언해야 함

### 1.6 LangThree Lexer.fsl 특이사항

funlex이 올바르게 처리해야 하는 핵심 패턴:

1. **키워드 vs 식별자**: `"let"`이 `ident_start ident_char*`보다 먼저 — longest match로 `"letter"`는 IDENT, `"let"`은 LET
2. **멀티-문자 연산자 순서**: `"<="`, `">="` 등이 `'<'`, `'>'` 보다 먼저
3. **NEWLINE → read_indent 전환**: newline 후 별도 rule에서 공백 수를 셈
4. **빈 문자열 매칭**: `read_indent`에서 `""` 패턴으로 indent 계산 종료
5. **사용자 정의 연산자**: `op_char op_char+`가 모든 특정 연산자 규칙 뒤에 위치
6. **중첩 블록 주석**: depth counter로 `(* ... (* ... *) ... *)` 처리

---

## Part 2: fsyacc 입력 형식 (funyacc이 구현해야 할 것)

### 2.1 파일 구조

```
%{                          ← F# 헤더 코드
open Ast
// ... helper functions ...
%}                          ← 헤더 끝

// Token declarations
%token <int> NUMBER         ← payload 있는 토큰
%token <string> IDENT
%token PLUS MINUS           ← payload 없는 토큰

// Precedence declarations (아래로 갈수록 높은 우선순위)
%left PLUS MINUS
%left STAR SLASH
%right CONS
%nonassoc LT GT

// Start symbols
%start start
%type <Ast.Expr> start

%%                          ← 문법 규칙 시작

// Grammar rules
start:
    | Expr EOF   { $1 }

Expr:
    | Expr PLUS Term    { Add($1, $3, ruleSpan parseState 1 3) }
    | Term              { $1 }

Term:
    | NUMBER            { Number($1, symSpan parseState 1) }
    // ...
```

### 2.2 토큰 선언

```
%token <type> NAME    // payload가 있는 토큰 (semantic value)
%token NAME           // payload가 없는 토큰
```

예시:
```
%token <int> NUMBER          // int 값을 가진 NUMBER 토큰
%token <string> IDENT        // string 값을 가진 IDENT 토큰
%token <string> INFIXOP0     // string 값 (연산자 문자열)
%token PLUS MINUS STAR       // 값 없는 토큰들
%token <int> NEWLINE         // int 값 (column position)
```

### 2.3 우선순위 선언

아래로 갈수록 높은 우선순위:

```
%left TOKEN1 TOKEN2      // 좌결합
%right TOKEN3            // 우결합
%nonassoc TOKEN4 TOKEN5  // 비결합 (같은 레벨 연속 사용 불가)
```

LangThree의 13단계 우선순위:
```
%left PIPE_RIGHT           // Level 1 (lowest)
%left COMPOSE_RIGHT        // Level 2
%right COMPOSE_LEFT        // Level 3
%left OR                   // Level 4
%left AND                  // Level 5
%nonassoc EQUALS LT GT LE GE NE  // Level 6
%left INFIXOP0             // Level 7
%right INFIXOP1            // Level 8
%right CONS                // Level 9
%left INFIXOP2             // Level 10
%left INFIXOP3             // Level 11
%right INFIXOP4            // Level 12 (highest declared)
// Function application (juxtaposition) = Level 13 (implicit, highest)
```

### 2.4 문법 규칙

```
NonTerminal:
    | Symbol1 Symbol2 ... SymbolN    { semantic_action }
    | Alternative1                    { action1 }
    | Alternative2                    { action2 }
```

**Semantic action 변수:**
- `$1`, `$2`, ... `$N`: 각 심볼의 semantic value
- `parseState`: `IParseState` — 위치 정보 접근용

**예시:**
```
Expr:
    | Expr PLUS Term       { Add($1, $3, ruleSpan parseState 1 3) }
    // $1 = Expr의 값, $3 = Term의 값 (PLUS는 $2지만 값 없음)

    | LET IDENT EQUALS Expr IN Expr
                           { Let($2, $4, $6, ruleSpan parseState 1 6) }
    // $2 = IDENT string, $4 = 첫 Expr, $6 = 둘째 Expr
```

### 2.5 LALR(1) 충돌 해결

- **Shift/Reduce**: 기본적으로 shift 우선 (precedence 선언으로 제어 가능)
- **Reduce/Reduce**: 먼저 선언된 규칙 우선

LangThree는 332 shift/reduce conflicts — 모두 shift 우선으로 올바르게 해결.
주된 원인: `INDENT Expr DEDENT` 규칙의 41개 + dangling else 유사 패턴.

### 2.6 LangThree Parser.fsy 특이사항

funyacc이 올바르게 처리해야 하는 핵심 패턴:

1. **INDENT/DEDENT as block delimiters**: `INDENT Expr DEDENT`가 `{ }` 역할
2. **다중 매개변수 디슈거링**: `let f x y = body` → `LetDecl("f", Lambda("x", Lambda("y", body)))` — semantic action에서 처리
3. **Constructor ambiguity**: 대문자 IDENT가 생성자인지 변수인지 — semantic action에서 구분
4. **GADT 생성자 파싱**: `Name : ArgType -> ReturnType` — `splitGadt` helper로 분리
5. **Operator as function**: `(++)` → `Var("++")` — 괄호 안의 연산자를 함수로 사용
6. **Mutual recursion**: `let rec f = ... and g = ...` — continuation 패턴

---

## Part 3: 생성해야 할 코드 형태

### 3.1 funlex 출력 (Lexer 모듈)

funlex이 생성하는 F# 코드는 다음 형태:

```fsharp
module Lexer

open FSharp.Text.Lexing

// 헤더 코드 (입력의 { } 블록에서 복사)
open System
open Parser

let lexeme (lexbuf: LexBuffer<_>) = LexBuffer<_>.LexemeString lexbuf
// ... 기타 helper ...

// DFA transition tables (generated)
let private trans_tokenize: int[][] = [| ... |]
let private accept_tokenize: int[] = [| ... |]

// Generated tokenizer function
let rec tokenize (lexbuf: LexBuffer<char>) : Parser.token =
    // DFA-based matching using trans_tokenize/accept_tokenize
    // Returns token based on accept state
    ...

and read_indent (col: int) (lexbuf: LexBuffer<char>) : Parser.token =
    // DFA for indent counting
    ...

and read_string (buf: System.Text.StringBuilder) (lexbuf: LexBuffer<char>) : Parser.token =
    // DFA for string literal parsing
    ...

and block_comment (depth: int) (lexbuf: LexBuffer<char>) : Parser.token =
    // DFA for block comment parsing
    ...
```

핵심: 각 `rule`/`and` 블록이 하나의 DFA가 됨. 매개변수는 그대로 함수 인자로 전달.

### 3.2 funyacc 출력 (Parser 모듈)

funyacc이 생성하는 F# 코드는 다음 형태:

```fsharp
module Parser

// Token type (from %token declarations)
type token =
    | NUMBER of int
    | IDENT of string
    | STRING of string
    | PLUS | MINUS | STAR | SLASH
    | LPAREN | RPAREN
    | LET | IN | EQUALS
    // ... 모든 토큰 ...
    | EOF

// Parse tables (generated)
let private action_table: int[][] = [| ... |]
let private goto_table: int[][] = [| ... |]
let private reduction_table: (int * int * (obj[] -> obj))[] = [| ... |]

// Entry point functions
let start (tokenizer: LexBuffer<char> -> token) (lexbuf: LexBuffer<char>) : Ast.Expr =
    // LALR(1) table-driven parsing
    ...

let parseModule (tokenizer: LexBuffer<char> -> token) (lexbuf: LexBuffer<char>) : Ast.Module =
    // LALR(1) table-driven parsing for module mode
    ...
```

핵심: `%start` 선언마다 하나의 entry point 함수. `token` DU는 모든 `%token` 선언에서 생성.

---

## Part 4: 핵심 알고리즘

### 4.1 funlex — Regex → DFA

1. **Regex 파싱**: 입력 패턴을 정규표현식 AST로 파싱
2. **Thompson's Construction**: Regex → NFA (비결정적 유한 오토마타)
3. **Subset Construction**: NFA → DFA (결정적 유한 오토마타)
4. **DFA Minimization**: 상태 수 최소화 (Hopcroft's algorithm)
5. **코드 생성**: DFA transition table → F# 배열

**Longest match 구현**: DFA가 더 이상 전진 불가능한 지점까지 진행하면서, 마지막 accept 상태를 기록. 전진 불가 시 마지막 accept 상태의 action 실행.

**Multiple rules**: 각 `rule`/`and` 블록이 독립 DFA. 규칙 간 호출은 일반 F# 함수 호출.

### 4.2 funyacc — LALR(1)

1. **문법 읽기**: `.fsy` 파일에서 토큰, 우선순위, 문법 규칙 추출
2. **Augmented Grammar**: 시작 규칙 `S' → S $` 추가
3. **LR(0) Item Sets**: 클로저와 goto로 상태(item set) 구성
4. **LALR(1) Lookahead**: LR(0) 상태를 병합하되 lookahead 보존
5. **Action/Goto Table**: shift, reduce, accept, error 액션 생성
6. **Conflict Resolution**: 우선순위/결합방향으로 shift/reduce 해결
7. **코드 생성**: 테이블 → F# 배열, reduction → F# 함수

**우선순위 기반 충돌 해결:**
- Shift/reduce conflict: 토큰의 우선순위 vs 규칙의 우선순위 비교
- 규칙의 우선순위 = 규칙에서 마지막으로 나타나는 토큰의 우선순위
- 같은 우선순위: left → reduce, right → shift, nonassoc → error

---

## Part 5: 검증 전략

### 5.1 funlex 검증

1. LangThree의 `Lexer.fsl`을 funlex으로 처리
2. 생성된 코드로 LangThree 소스 파일을 토큰화
3. fslex 생성 코드의 출력과 비교 (토큰 시퀀스 일치)
4. `TESTS.md`의 "Tokenization Test Cases" 섹션 통과

### 5.2 funyacc 검증

1. LangThree의 `Parser.fsy`를 funyacc으로 처리
2. 생성된 코드로 토큰 스트림을 파싱
3. fsyacc 생성 코드의 AST 출력과 비교
4. `TESTS.md`의 "Parsing Test Cases" 섹션 통과
5. 332 shift/reduce conflicts 동일하게 해결

### 5.3 통합 검증

1. funlex + funyacc 생성 코드를 LangThree 인터프리터에 통합
2. 전체 테스트 스위트 실행 (199 F# + 442 fslit = 641 tests)
3. 모든 테스트 통과 = generator가 올바르게 동작

---

## Part 6: 실제 입력 파일 요약

### 6.1 Lexer.fsl (173줄)

- **헤더**: classifyOperator (INFIXOP 분류), setInitialPos
- **문자 클래스**: 7개 (digit, whitespace, newline, letter, ident_start, ident_char, type_var, op_char)
- **규칙 4개**: tokenize (메인), read_indent (들여쓰기), read_string (문자열), block_comment (주석)
- **토큰 패턴**: 키워드 22개, 연산자/구분자 30+개, 리터럴 4종류
- **특수 처리**: 탭 금지, NEWLINE(col), 중첩 블록 주석

### 6.2 Parser.fsy (~500줄)

- **헤더**: ruleSpan, symSpan, desugarAnnotParams helpers
- **토큰 선언**: 47+ 토큰 (5개 payload 있음)
- **우선순위**: 12 레벨 (PIPE_RIGHT ~ INFIXOP4)
- **시작 심볼**: 2개 (start → Expr, parseModule → Module)
- **문법 규칙**: ~60 nonterminals, ~200 productions
- **Shift/reduce conflicts**: 332개 (모두 shift 우선으로 해결)

---

## 부록: 파일 위치 요약

```
LangThree/
├── SPEC.md              ← 언어 명세 (토큰, 문법, 타입 시스템)
├── ARCHITECTURE.md      ← 파이프라인 설계
├── TESTS.md             ← 검증 테스트 케이스
├── FUNLEX-FUNYACC-REFERENCE.md  ← 이 문서 (generator 형식 + 알고리즘)
├── src/LangThree/
│   ├── Lexer.fsl        ← funlex 첫 번째 입력 (173줄)
│   ├── Parser.fsy       ← funyacc 첫 번째 입력 (~500줄)
│   ├── Ast.fs           ← AST 타입 정의 (생성 코드가 참조)
│   └── IndentFilter.fs  ← 토큰 필터 (lexer/parser 사이)
├── tutorial/            ← 언어 예제 (16 chapters)
└── tests/flt/           ← 442 integration tests
```
