# 1장: 시작하기 (Getting Started)

LangThree는 F# 스타일의 들여쓰기 구문을 사용하는 정적 타입(statically-typed) 함수형 언어로,
대수적 데이터 타입(algebraic data types, GADTs 포함), 결정 트리 컴파일 방식의 패턴 매칭(pattern matching),
그리고 모듈 시스템을 지원합니다.

## LangThree 실행하기

**REPL (대화형 모드)** 는 인자 없이 실행하면 대화형 세션을 시작합니다:

```
$ langthree
funlang>
```

프롬프트에서 직접 표현식을 입력하면 결과를 즉시 확인할 수 있습니다.

**표현식 모드(Expression mode)** 는 커맨드 라인에서 단일 표현식을 평가합니다:

```
$ langthree --expr '1 + 2'
3
```

**파일 모드(File mode)** 는 프로그램 파일을 평가합니다. 마지막 바인딩의 값이 출력됩니다:

```
$ cat hello.l3
let greeting = "hello"
let result = greeting + " world"

$ langthree hello.l3
"hello world"
```

**진단 모드(Diagnostic modes)** 는 평가 없이 컴파일 결과를 검사합니다:

```
$ langthree --emit-ast --expr '1 + 2'
Add (Number 1, Number 2)

$ langthree --emit-type --expr '1 + 2'
int
```

## 정수와 산술 연산

표준 산술 연산자를 지원하며 일반적인 우선순위를 따릅니다. 나눗셈은 정수 나눗셈(integer division)입니다.

```
funlang> 1 + 2 * 3
7

funlang> 10 - 3
7

funlang> 10 / 3
3

funlang> -5
-5

funlang> 10 % 3
1

funlang> 7 % 2
1
```

`%`는 나머지(모듈로) 연산자입니다.

## 불리언 (Booleans)

`true`와 `false`를 지원하며, 단락 평가(short-circuit) 방식의 `&&`와 `||`를 사용합니다.

```
funlang> true && false
false

funlang> true || false
true
```

단락 평가(short-circuit evaluation)란 불필요한 경우 오른쪽 피연산자를 평가하지 않는 것을 의미합니다:

```
funlang> false && (1/0 = 0)
false

funlang> true || (1/0 = 0)
true
```

`not` 함수로 불리언을 부정할 수 있습니다:

```
funlang> not true
false
```

## 문자열 (Strings)

문자열 리터럴은 큰따옴표를 사용하며 표준 이스케이프 시퀀스(`\n`, `\t`, `\\`, `\"`)를 지원합니다.
문자열 연결(concatenation)에는 `+` 연산자를 사용합니다.

```
funlang> "hello" + " world"
"hello world"

funlang> "line1\nline2"
"line1
line2"
```

내장 문자열 함수:

```
funlang> string_length "hello"
5

funlang> string_sub "hello" 1 3
"ell"

funlang> to_string 42
"42"
```

## 비교 연산자

등호는 `=`입니다 (`==`가 아닙니다). 부등호는 `<>`입니다.

```
funlang> 1 = 1
true

funlang> 1 <> 2
true

funlang> 3 < 5
true

funlang> 3 >= 3
true
```

## 조건문 (Conditionals)

```
funlang> if 1 < 2 then "yes" else "no"
"yes"
```

두 분기(branch)는 동일한 타입이어야 합니다. `else` 분기는 필수입니다.

## 주석 (Comments)

`//`로 줄 주석을, `(* ... *)`로 블록 주석을 작성합니다:

```
funlang> 1 + 2 // this is ignored
3

funlang> (* block comment *) 1 + 2
3
```

## 유닛 타입 (Unit Type)

유닛 타입 `()`는 "의미 있는 값이 없음"을 나타내며, 부수 효과(side effects)에 사용됩니다:

```
funlang> ()
()

funlang> println "hello"
hello
()
```

## 구문 참고 사항

- **들여쓰기 기반** -- 블록에 세미콜론이나 중괄호가 필요 없습니다 (F# 스타일)
- **파일 모드**: 최상위 레벨에서 `let` 바인딩을 사용하며 `in`이 필요 없습니다; 마지막 바인딩의 값이 출력됩니다
- **REPL / 표현식 모드**: 지역 바인딩에 `let x = ... in body`를 사용합니다
- `match` 파이프는 `match` 키워드의 열(column)에 맞춰 정렬해야 합니다
