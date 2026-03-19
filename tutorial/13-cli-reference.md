# 13장: CLI Reference

이 장에서는 LangThree의 모든 커맨드라인 모드와 옵션을 다룹니다.

## 표현식 모드

`--expr`로 단일 표현식을 평가합니다:

```
$ langthree --expr '1 + 2'
3
```

표현식은 파싱, 타입 검사, 평가를 거칩니다. 결과는 표준 출력으로 출력됩니다.

다양한 결과 타입:

```
$ langthree --expr '42'
42

$ langthree --expr '"hello"'
"hello"

$ langthree --expr 'true'
true

$ langthree --expr '[1, 2, 3]'
[1, 2, 3]

$ langthree --expr '(1, "hello", true)'
(1, "hello", true)

$ langthree --expr '()'
()

$ langthree --expr 'fun x -> x + 1'
<function>
```

표현식 모드에서는 `let ... in` 구문으로 로컬 바인딩을 사용합니다:

```
$ langthree --expr 'let x = 5 in let y = 10 in x + y'
15
```

표현식 모드는 한 줄만 지원하며, 들여쓰기를 지원하지 않습니다.

## 파일 모드

프로그램 파일을 평가합니다:

```
$ cat hello.l3
let greeting = "hello"
let result = greeting + " world"

$ langthree hello.l3
"hello world"
```

파일 모드는 들여쓰기 기반 문법을 지원합니다. 마지막 `let` 바인딩의 값이
출력됩니다. 최종 결과 전에 부수 효과를 실행하려면 `let _ =`를 사용합니다:

```
$ cat greet.l3
let _ = println "starting..."
let name = "world"
let result = "hello " + name

$ langthree greet.l3
starting...
"hello world"
```

파일 모드는 LangThree의 모든 기능을 지원합니다: 타입 선언, 모듈,
예외, 들여쓰기 기반 패턴 매칭, 여러 줄 표현식.

`.l3` 확장자는 관례이며 강제되지 않습니다 -- 어떤 파일 이름이든 사용 가능합니다.

## AST 출력

`--emit-ast`로 파싱된 추상 구문 트리를 확인합니다:

```
$ langthree --emit-ast --expr '1 + 2'
Add (Number 1, Number 2)

$ langthree --emit-ast --expr 'fun x -> x + 1'
Lambda ("x", Add (Var "x", Number 1))

$ langthree --emit-ast --expr 'let x = 5 in x + 1'
Let ("x", Number 5, Add (Var "x", Number 1))
```

파일 모드에서는 각 선언이 표시됩니다:

```
$ cat ast_demo.l3
let x = 42
let add a b = a + b

$ langthree --emit-ast ast_demo.l3
LetDecl ("x", Number 42)
LetDecl ("add", Lambda ("a", Lambda ("b", Add (Var "a", Var "b"))))
```

파싱 문제를 디버깅하는 데 유용합니다 -- 코드가 의도한 대로 파싱되었는지 확인합니다.

## 타입 출력

`--emit-type`으로 추론된 타입을 확인합니다:

```
$ langthree --emit-type --expr '1 + 2'
int

$ langthree --emit-type --expr 'fun x -> x + 1'
int -> int

$ langthree --emit-type --expr '"hello"'
string
```

파일 모드에서는 사용자 정의 최상위 바인딩의 타입이 모두 표시됩니다
(알파벳순 정렬, 내장 및 Prelude 바인딩 제외):

```
$ cat types_demo.l3
let x = 42
let greet name = "hello " + name
let result = greet "world"

$ langthree --emit-type types_demo.l3
greet : string -> string
result : string
x : int
```

다형 타입은 타입 변수를 표시합니다:

```
$ langthree --emit-type --expr 'fun x -> x'
'a -> 'a

$ langthree --emit-type --expr 'fun f -> fun x -> f x'
('a -> 'b) -> 'a -> 'b
```

## 토큰 출력

`--emit-tokens`로 렉서 출력을 확인합니다:

```
$ langthree --emit-tokens --expr '1 + 2'
NUMBER(1) PLUS NUMBER(2) EOF
```

파일 모드에서는 IndentFilter가 들여쓰기를 토큰으로 처리합니다:

```
$ cat tokens_demo.l3
let result =
    if true then 1
    else 2

$ langthree --emit-tokens tokens_demo.l3
LET IDENT(result) EQUALS NEWLINE(4) IF TRUE THEN NUMBER(1) NEWLINE(4) ELSE NUMBER(2) NEWLINE(0) EOF
```

`NEWLINE(n)` 토큰은 각 줄 시작의 들여쓰기 수준을 나타냅니다.
들여쓰기 문제를 디버깅하는 데 유용합니다 -- 프로그램이 파일 모드에서 파싱에 실패할 때,
토큰 출력을 통해 IndentFilter가 공백을 어떻게 해석하는지 확인할 수 있습니다.

## REPL

인자 없이 `langthree`를 실행하여 대화형 세션을 시작합니다:

```
$ langthree
FunLang REPL
Type '#quit' or Ctrl+D to quit.

funlang> 1 + 2
3
funlang> let x = 5 in x * 2
10
funlang> #quit
```

REPL은 한 줄 표현식을 평가합니다 (`--expr` 모드와 동일). 바인딩에는
`let ... in` 구문을 사용합니다. 모듈 수준의 `let` (`in` 없이)은 지원되지 않습니다.

`#quit` 또는 Ctrl+D로 종료합니다.

## 진단 모드 요약

| 플래그 | 설명 | 표현식 | 파일 |
|------|-------------|-----------|------|
| *(없음)* | 평가 후 결과 출력 | N/A | 마지막 바인딩의 값 |
| `--expr` | 표현식 평가 | 표현식 결과 | N/A |
| `--emit-ast` | 파싱된 AST 표시 | 표현식의 AST | 모든 선언 |
| `--emit-type` | 추론된 타입 표시 | 표현식의 타입 | 모든 바인딩 타입 |
| `--emit-tokens` | 렉서 토큰 표시 | 원시 토큰 | IndentFilter 적용 토큰 |
| *(인자 없음)* | REPL 대화형 세션 | 줄 단위 평가 | N/A |

진단 플래그는 `--expr` 또는 파일 이름과 함께 사용합니다:

```
$ langthree --emit-type --expr '1 + 2'
int

$ langthree --emit-type types_demo.l3
greet : string -> string
result : string
x : int
```

## 오류 메시지

타입 오류에는 오류 코드, 소스 위치, 힌트가 포함됩니다:

```
$ langthree --expr '"hello" + 1'
error[E0301]: Type mismatch: expected string but got int
 --> <expr>:1:6-11
   = hint: Check that all branches of your expression return the same type
```

파싱 오류:

```
$ langthree --expr '1 +'
Error: parse error
```

미정의 변수 오류:

```
$ langthree --expr 'foo'
error[E0303]: Unbound variable: foo
 --> <expr>:1:0-3
   = hint: Make sure the variable is defined before use
```

## 경고

LangThree는 잠재적 문제에 대해 경고를 발생시킵니다. 경고는 실행을 막지 않으며,
프로그램은 그대로 실행됩니다.

### W0001: 불완전한 패턴 매칭

match 표현식이 모든 생성자를 다루지 않을 때 발생합니다:

```
$ cat incomplete.l3
type Color = Red | Green | Blue
let result =
    match Red with
    | Red -> 1
    | Green -> 2

$ langthree incomplete.l3
Warning: warning[W0001]: Incomplete pattern match. Missing cases: Blue
 --> :0:0-1:0
   = hint: Add the missing cases or a wildcard pattern '_' to cover all values
1
```

### W0002: 중복 패턴

패턴 절에 도달할 수 없을 때 발생합니다:

```
$ cat redundant.l3
type Color = Red | Green | Blue
let result =
    match Red with
    | Red -> 1
    | Green -> 2
    | Blue -> 3
    | Red -> 4

$ langthree redundant.l3
Warning: warning[W0002]: Redundant pattern in clause 4. This case will never be reached.
 --> :0:0-1:0
   = hint: Remove the unreachable pattern
1
```

### W0003: 불완전한 예외 핸들러

`try ... with` 블록이 가능한 모든 예외를 처리하지 않을 때 발생합니다:

```
$ cat handler.l3
exception MyError of string
let result =
    try
        raise (MyError "oops")
    with
    | MyError msg -> msg

$ langthree handler.l3
Warning: warning[W0003]: Non-exhaustive exception handler: not all exceptions are handled; consider adding a catch-all handler
 --> :0:0-1:0
   = hint: Add a catch-all handler or handle all possible exceptions
"oops"
```

이 경고를 없애려면 `| _ -> ...` 포괄 핸들러를 추가하세요.
