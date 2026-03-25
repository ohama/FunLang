# LangThree Abstract Grammar

## 소개

이 문서는 LangThree 언어의 형식 추상 문법(formal abstract grammar)을 정의한다.
`Parser.fsy`의 구체적 문법(concrete grammar)과 `Ast.fs`의 AST 정의로부터 도출되었으며,
렉서 세부 사항(공백, 주석, 들여쓰기 필터)은 생략하고 언어의 구조적 본질만을 기술한다.

---

## 표기법 (Notation)

| 표기 | 의미 |
|------|------|
| `::=` | 정의 |
| `\|` | 선택 (alternatives) |
| `*` | 0회 이상 반복 |
| `+` | 1회 이상 반복 |
| `?` | 0 또는 1회 (optional) |
| `( ... )` | 그룹핑 |
| `'token'` | 리터럴 토큰 (따옴표 포함) |
| `IDENT` | 소문자 시작 식별자 |
| `UPPER` | 대문자 시작 식별자 (생성자) |
| `INT` | 정수 리터럴 |
| `BOOL` | `true` 또는 `false` |
| `STRING` | 문자열 리터럴 |
| `CHAR` | 문자 리터럴 |
| `TYPE_VAR` | 타입 변수 (`'a`, `'b`, ...) |
| `INFIXOP` | 사용자 정의 중위 연산자 |

---

## 1. 프로그램과 모듈 (Programs and Modules)

```
program     ::= 'module' qualified_ident decl* EOF
             |  'namespace' qualified_ident decl* EOF
             |  decl* EOF

qualified_ident ::= IDENT ('.' IDENT)*
```

최상위 프로그램은 빈 파일이거나(`EmptyModule`), 선언 목록이거나,
`module` / `namespace` 헤더를 가진 명명된 모듈이다.

---

## 2. 선언 (Declarations)

```
decl ::= 'let' IDENT '=' expr
       | 'let' IDENT param+ '=' expr
       | 'let' IDENT '(' ')' '=' expr
       | 'let' '(' op_name ')' param+ '=' expr
       | 'let' tuple_pattern '=' expr
       | 'let' '_' '=' expr
       | 'let' IDENT '=' expr 'in' expr

       | 'let' 'rec' IDENT param+ '=' expr
           ('and' IDENT param+ '=' expr)*
       | 'let' 'rec' '(' op_name ')' param+ '=' expr

       | 'type' IDENT type_var* '=' constructor ('|' constructor)*
       | 'type' IDENT type_var* '=' '{' field_decl (';' field_decl)* ';'? '}'
       | 'type' IDENT type_var* '=' type_alias_expr

       | 'exception' IDENT ('of' type_expr)?

       | 'module' IDENT '=' decl+

       | 'open' qualified_ident
       | 'open' STRING

       | 'namespace' qualified_ident

param    ::= IDENT
op_name  ::= INFIXOP0 | INFIXOP1 | INFIXOP2 | INFIXOP3 | INFIXOP4
type_var ::= TYPE_VAR
```

### 2.1 생성자 선언 (Constructor Declarations)

```
constructor ::= UPPER                              -- 인자 없는 ADT 생성자
              | UPPER 'of' type_expr               -- 인자 있는 ADT 생성자
              | UPPER ':' type_expr '->' type_expr  -- GADT 생성자
```

### 2.2 레코드 필드 선언 (Record Field Declarations)

```
field_decl ::= IDENT ':' type_expr
             | 'mutable' IDENT ':' type_expr
```

---

## 3. 표현식 (Expressions)

우선순위 낮은 순서부터 높은 순서로 기술한다.

```
expr ::= -- 바인딩 형식
         'let' IDENT '=' expr 'in' expr
       | 'let' IDENT param+ '=' expr 'in' expr
       | 'let' IDENT '(' ')' '=' expr 'in' expr
       | 'let' tuple_pattern '=' expr 'in' expr
       | 'let' '_' '=' expr 'in' expr
       | 'let' 'rec' IDENT IDENT '=' expr 'in' expr

         -- 패턴 매칭 / 예외 처리
       | 'match' expr 'with' match_clause+
       | 'try' expr 'with' match_clause+
       | 'try' expr 'with' IDENT '->' expr          -- 단일 핸들러 (인라인)

         -- 람다
       | 'fun' IDENT '->' expr
       | 'fun' '(' IDENT ':' type_expr ')' '->' expr
       | 'fun' '(' IDENT ':' type_expr ')'+  '->' expr
       | 'fun' '(' ')' '->' expr
       | 'fun' tuple_pattern '->' expr

         -- 제어 흐름
       | 'if' expr 'then' expr 'else' expr

         -- 파이프 및 합성 (좌에서 우 우선순위)
       | expr '|>' expr                             -- 파이프 오른쪽
       | expr '>>' expr                             -- 함수 합성 오른쪽
       | expr '<<' expr                             -- 함수 합성 왼쪽

         -- 논리 연산
       | expr '||' expr
       | expr '&&' expr

         -- 비교 연산 (비결합, non-associative)
       | expr '=' expr | expr '<>' expr
       | expr '<' expr | expr '>' expr
       | expr '<=' expr | expr '>=' expr

         -- 사용자 정의 연산자
       | expr INFIXOP0 expr                         -- 비교 수준
       | expr INFIXOP1 expr                         -- 연결 수준 (우결합)
       | expr '::' expr                             -- cons (우결합)
       | expr INFIXOP2 expr                         -- 덧셈 수준

         -- 산술 연산
       | expr '+' expr | expr '-' expr

         -- 곱셈 수준
       | expr '*' expr | expr '/' expr | expr '%' expr
       | expr INFIXOP3 expr

         -- 지수 수준 (우결합)
       | expr INFIXOP4 expr

         -- 단항 / raise
       | '-' expr
       | 'raise' atom

         -- 뮤터블 필드 대입
       | atom '.' IDENT '<-' expr

         -- 함수 적용 (좌결합, 최고 우선순위)
       | expr atom+

         -- 원자 표현식
       | atom
```

### 3.1 원자 표현식 (Atomic Expressions)

```
atom ::= '(' ')'                                   -- unit
       | INT                                        -- 정수 리터럴
       | BOOL                                       -- 불리언 리터럴
       | STRING                                     -- 문자열 리터럴
       | CHAR                                       -- 문자 리터럴
       | IDENT                                      -- 변수 (소문자 시작)
       | UPPER                                      -- 인자 없는 생성자 (대문자 시작)
       | '(' expr ')'                               -- 괄호 묶음
       | '(' expr ':' type_expr ')'                 -- 타입 어노테이션
       | '(' expr ',' expr (',' expr)* ')'          -- 튜플
       | '[' ']'                                    -- 빈 리스트
       | '[' expr (';' expr)* ';'? ']'             -- 리스트 리터럴
       | '[' expr '..' expr ']'                     -- 범위 [start..stop]
       | '[' expr '..' expr '..' expr ']'           -- 스텝 범위 [start..step..stop]
       | '(' INFIXOP ')'                            -- 연산자를 값으로 사용
       | atom '.' IDENT                             -- 필드 접근 (좌결합)
       | '{' field_binding (';' field_binding)* ';'? '}'          -- 레코드 생성
       | '{' expr 'with' field_binding (';' field_binding)* ';'? '}'  -- 레코드 갱신
```

### 3.2 매치 절 (Match Clauses)

```
match_clause ::= '|' or_pattern ('when' expr)? '->' expr

or_pattern   ::= pattern ('|' pattern)*
```

---

## 4. 패턴 (Patterns)

```
pattern ::= '_'                                     -- 와일드카드
          | IDENT                                   -- 변수 패턴 (소문자)
          | UPPER                                   -- 인자 없는 생성자 패턴 (대문자)
          | UPPER pattern                           -- 생성자 + 인자 패턴
          | INT                                     -- 정수 상수 패턴
          | '-' INT                                 -- 음수 정수 패턴
          | BOOL                                    -- 불리언 상수 패턴
          | STRING                                  -- 문자열 상수 패턴
          | CHAR                                    -- 문자 상수 패턴
          | '(' pattern ',' pattern (',' pattern)* ')' -- 튜플 패턴
          | pattern '::' pattern                    -- cons 패턴 (우결합)
          | '[' ']'                                 -- 빈 리스트 패턴
          | '[' pattern (';' pattern)* ';'? ']'    -- 리스트 리터럴 패턴 (cons 사슬로 변환)
          | '{' IDENT '=' pattern (';' IDENT '=' pattern)* ';'? '}'  -- 레코드 패턴
          | '(' pattern ')'                         -- 괄호 묶음

or_pattern    ::= pattern ('|' pattern)*            -- or-패턴 (한 절에 여러 대안)
tuple_pattern ::= '(' pattern ',' pattern (',' pattern)* ')'
```

`or_pattern`은 매치 절의 최상위에서만 허용된다.
각 패턴 대안은 동일한 바인딩 변수 집합을 가져야 한다.

---

## 5. 타입 표현식 (Type Expressions)

```
type_expr ::= tuple_type '->' type_expr             -- 함수 타입 (우결합)
            | tuple_type

tuple_type ::= atomic_type ('*' atomic_type)+       -- 튜플 타입 (2개 이상)
             | atomic_type

atomic_type ::= 'int'
              | 'bool'
              | 'string'
              | 'char'
              | 'unit'
              | TYPE_VAR                             -- 'a, 'b, ...
              | IDENT                               -- 명명된 타입 (Tree, Option, ...)
              | atomic_type 'list'                  -- 리스트 타입 (후위)
              | atomic_type IDENT                   -- 타입 적용 (e.g., int expr, 'a option)
              | '(' type_expr ')'
```

타입 별칭 선언(type alias)에서 RHS는 `type_alias_expr`로 제한된다.
이는 `IDENT`로 시작하는 bare named type을 제외하여 ADT 생성자와의 LALR(1) 충돌을 회피한다.

---

## 6. 내장 타입 (Built-in Types)

```
-- 기본 스칼라 타입
int                          -- 정수 (arbitrary precision 없음; .NET int32)
bool                         -- true | false
string                       -- UTF-16 문자열 (불변)
char                         -- 단일 문자 (ASCII 범위)
unit                         -- 단위 타입; 유일한 값은 ()

-- 복합 타입
'a list                      -- 불변 단일 연결 리스트
'a array                     -- 가변 고정 크기 배열
('k, 'v) hashtable           -- 가변 해시 테이블 (Dictionary 기반)

-- 함수 타입
'a -> 'b                     -- 단일 인자 함수 (커링으로 다인자 표현)

-- 튜플 타입
'a * 'b                      -- 2-튜플
'a * 'b * 'c                 -- 3-튜플
                             -- (n >= 2)

-- 예외 타입
exn                          -- 예외 (LangThreeException 래퍼)
```

### 6.1 Prelude 타입

Prelude가 자동으로 정의하는 주요 타입:

```
type 'a Option = None | Some of 'a
type ('a, 'b) Result = Ok of 'a | Error of 'b
```

---

## 7. 내장 함수 (Built-in Functions)

### 7.1 문자열 연산 (String Operations)

| 함수 | 타입 | 설명 |
|------|------|------|
| `string_length` | `string -> int` | 문자열 길이 |
| `string_concat` | `string -> string -> string` | 두 문자열 연결 |
| `string_sub` | `string -> int -> int -> string` | 부분 문자열 (시작 인덱스, 길이) |
| `string_contains` | `string -> string -> bool` | 부분 문자열 포함 여부 |
| `to_string` | `'a -> string` | 임의 값을 문자열로 변환 |
| `string_to_int` | `string -> int` | 문자열을 정수로 파싱 (실패 시 예외) |
| `sprintf` | `string -> ... -> string` | 형식 문자열로 문자열 생성 (`%d`, `%s`, `%b`, `%%`) |

### 7.2 문자 연산 (Char Operations)

| 함수 | 타입 | 설명 |
|------|------|------|
| `char_to_int` | `char -> int` | 문자를 ASCII 코드로 변환 |
| `int_to_char` | `int -> char` | ASCII 코드를 문자로 변환 (0–127 범위) |

### 7.3 출력 연산 (I/O Output)

| 함수 | 타입 | 설명 |
|------|------|------|
| `print` | `string -> unit` | 표준 출력에 문자열 출력 (줄바꿈 없음) |
| `println` | `string -> unit` | 표준 출력에 문자열 출력 (줄바꿈 포함) |
| `printf` | `string -> ... -> unit` | 형식 출력 (`%d`, `%s`, `%b`, `%%`) |
| `printfn` | `string -> ... -> unit` | 형식 출력 + 줄바꿈 |
| `eprint` | `string -> unit` | 표준 오류에 문자열 출력 (줄바꿈 없음) |
| `eprintln` | `string -> unit` | 표준 오류에 문자열 출력 (줄바꿈 포함) |
| `failwith` | `string -> 'a` | 메시지와 함께 예외 발생 |

### 7.4 파일 I/O (File I/O)

| 함수 | 타입 | 설명 |
|------|------|------|
| `read_file` | `string -> string` | 파일 전체 내용을 문자열로 읽기 |
| `write_file` | `string -> string -> unit` | 문자열을 파일에 쓰기 (덮어쓰기) |
| `append_file` | `string -> string -> unit` | 문자열을 파일에 추가 |
| `file_exists` | `string -> bool` | 파일 존재 여부 확인 |
| `read_lines` | `string -> string list` | 파일을 줄 목록으로 읽기 |
| `write_lines` | `string -> string list -> unit` | 줄 목록을 파일에 쓰기 |

### 7.5 표준 입력 (Standard Input)

| 함수 | 타입 | 설명 |
|------|------|------|
| `stdin_read_all` | `unit -> string` | 표준 입력 전체를 문자열로 읽기 |
| `stdin_read_line` | `unit -> string` | 표준 입력에서 한 줄 읽기 |

### 7.6 시스템 함수 (System Functions)

| 함수 | 타입 | 설명 |
|------|------|------|
| `get_args` | `unit -> string list` | 스크립트 실행 인자 목록 |
| `get_env` | `string -> string` | 환경 변수 값 읽기 (미설정 시 예외) |
| `get_cwd` | `unit -> string` | 현재 작업 디렉토리 경로 |
| `path_combine` | `string -> string -> string` | 경로 결합 |
| `dir_files` | `string -> string list` | 디렉토리 내 파일 목록 |

### 7.7 배열 연산 (Array Operations)

| 함수 | 타입 | 설명 |
|------|------|------|
| `array_create` | `int -> 'a -> 'a array` | 크기 n, 기본값으로 배열 생성 |
| `array_init` | `int -> (int -> 'a) -> 'a array` | 인덱스 함수로 배열 초기화 |
| `array_get` | `'a array -> int -> 'a` | 인덱스로 원소 접근 (범위 초과 시 예외) |
| `array_set` | `'a array -> int -> 'a -> unit` | 인덱스에 원소 설정 (가변) |
| `array_length` | `'a array -> int` | 배열 길이 |
| `array_of_list` | `'a list -> 'a array` | 리스트를 배열로 변환 |
| `array_to_list` | `'a array -> 'a list` | 배열을 리스트로 변환 |
| `array_iter` | `('a -> unit) -> 'a array -> unit` | 각 원소에 함수 적용 |
| `array_map` | `('a -> 'b) -> 'a array -> 'b array` | 각 원소를 변환한 새 배열 |
| `array_fold` | `('acc -> 'a -> 'acc) -> 'acc -> 'a array -> 'acc` | 배열 좌측 fold |

### 7.8 해시 테이블 연산 (Hashtable Operations)

| 함수 | 타입 | 설명 |
|------|------|------|
| `hashtable_create` | `unit -> ('k, 'v) hashtable` | 빈 해시 테이블 생성 |
| `hashtable_get` | `('k, 'v) hashtable -> 'k -> 'v` | 키로 값 조회 (없으면 예외) |
| `hashtable_set` | `('k, 'v) hashtable -> 'k -> 'v -> unit` | 키-값 설정 (가변) |
| `hashtable_containsKey` | `('k, 'v) hashtable -> 'k -> bool` | 키 존재 여부 확인 |
| `hashtable_keys` | `('k, 'v) hashtable -> 'k list` | 모든 키 목록 |
| `hashtable_remove` | `('k, 'v) hashtable -> 'k -> unit` | 키-값 삭제 |

---

## 8. 연산자 우선순위 (Operator Precedence)

낮은 우선순위에서 높은 우선순위 순서로 나열한다.

| 레벨 | 연산자 | 결합성 | 설명 |
|------|--------|--------|------|
| 1 | `\|>` | 좌결합 | 파이프 오른쪽 |
| 2 | `>>` | 좌결합 | 함수 합성 오른쪽 |
| 3 | `<<` | 우결합 | 함수 합성 왼쪽 |
| 4 | `\|\|` | 좌결합 | 논리 OR (단락 평가) |
| 5 | `&&` | 좌결합 | 논리 AND (단락 평가) |
| 6 | `=` `<>` `<` `>` `<=` `>=` | 비결합 | 비교 연산 |
| 7 | `INFIXOP0` | 좌결합 | 사용자 정의 — 비교 수준 (`=` `<` `>` `\|` `&` `$` `!` 시작) |
| 8 | `INFIXOP1` `::` | 우결합 | 사용자 정의 — 연결 수준 (`@` `^` 시작); cons |
| 9 | `+` `-` `INFIXOP2` | 좌결합 | 덧셈 수준; 사용자 정의 (`+` `-` 시작) |
| 10 | `*` `/` `%` `INFIXOP3` | 좌결합 | 곱셈 수준; 사용자 정의 (`*` `/` `%` 시작) |
| 11 | `INFIXOP4` | 우결합 | 사용자 정의 — 지수 수준 (`**` 시작) |
| 12 | 함수 적용 (juxtaposition) | 좌결합 | `f x` — 가장 높은 이항 우선순위 |
| 13 | 단항 `-`, `raise` | 전위 | 단항 부호 반전, 예외 발생 |

단일 문자 연산자(`+`, `-`, `*`, `/`, `%`, `<`, `>`, `=`)는 항상 내장 토큰으로 렉싱되며
사용자 정의 `INFIXOP`으로 처리되지 않는다.

---

## 9. 들여쓰기 규칙 요약 (Indentation Rules — Summary)

LangThree는 Python 방식의 들여쓰기 기반 블록 구조를 사용한다.
`IndentFilter`가 raw 토큰 스트림에서 `INDENT`/`DEDENT` 토큰을 생성하여 파서에 전달한다.

- 탭 문자 사용 금지 — 스페이스만 허용
- 표현식 컨텍스트의 `let` 바인딩은 오프사이드 규칙(offside rule)으로 암묵적 `in` 삽입
- `match` / `try` 뒤의 `|` 파이프는 해당 키워드의 열(column)에 정렬
- 모듈 컨텍스트의 `let`은 암묵적 `in` 삽입 없음

자세한 규칙은 `SPEC.md` §4 참조.
