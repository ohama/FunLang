# 16장: 사용자 정의 연산자 (User-Defined Operators)

LangThree는 기호 문자로 구성된 사용자 정의 중위 연산자를 지원합니다.
이를 통해 DSL(도메인 특화 언어)을 만들거나, 반복적인 함수 호출을
간결한 표현으로 대체할 수 있습니다.

## 연산자 정의하기

`let (op) a b = body` 구문으로 사용자 정의 중위 연산자를 선언합니다:

```
$ cat op_basic.l3
let (++) xs ys = append xs ys
let result = [1, 2] ++ [3, 4]

$ langthree op_basic.l3
[1, 2, 3, 4]
```

연산자 이름은 기호 문자(`! $ % & * + - . / < = > ? @ ^ | ~`)의 조합입니다.
최소 2문자 이상이어야 합니다 (단일 문자 `+`, `*` 등은 내장 연산자).

`let rec`으로 재귀 연산자도 정의할 수 있습니다.

## 우선순위 규칙

연산자의 우선순위는 **첫 번째 문자**로 결정됩니다 (F#/OCaml 규칙):

| 레벨 | 첫 문자 | 결합성 | 예제 |
|------|---------|--------|------|
| INFIXOP0 (낮음) | `= < > \| & $ !` | 좌결합 | `<\|>`, `===`, `!=` |
| INFIXOP1 | `@ ^` | 우결합 | `^^`, `@>` |
| INFIXOP2 | `+ -` | 좌결합 | `++`, `+.` |
| INFIXOP3 | `* / %` | 좌결합 | `*/`, `%%` |
| INFIXOP4 (높음) | `**` | 우결합 | `**`, `***` |

같은 레벨의 연산자는 같은 우선순위를 가집니다.
예: `++` (INFIXOP2)는 `+`와 같은 우선순위, `<|>` (INFIXOP0)는 비교 연산자와 같은 우선순위.

## 연산자를 함수로 사용하기

괄호로 감싸면 연산자를 일반 함수처럼 사용할 수 있습니다:

```
$ cat op_as_func.l3
let (++) xs ys = append xs ys
let result = fold (++) [] [[1, 2], [3], [4, 5]]

$ langthree op_as_func.l3
[1, 2, 3, 4, 5]
```

`(++)`는 `fun xs -> fun ys -> append xs ys`와 동일합니다.

## Prelude 연산자

Prelude는 세 가지 연산자를 기본 제공합니다:

| 연산자 | 타입 | 설명 | 예제 |
|--------|------|------|------|
| `++` | `'a list -> 'a list -> 'a list` | 리스트 연결 | `[1,2] ++ [3,4]` → `[1,2,3,4]` |
| `<\|>` | `Option<'a> -> Option<'a> -> Option<'a>` | Option 대안 | `None <\|> Some 42` → `Some 42` |
| `^^` | `string -> string -> string` | 문자열 연결 | `"a" ^^ "b"` → `"ab"` |

## 실용 예제

### 리스트 정렬 (quicksort with ++)

```
$ cat qsort_op.l3
let rec qsort xs = match xs with | [] -> [] | p :: rest -> qsort (filter (fun x -> x < p) rest) ++ [p] ++ qsort (filter (fun x -> x >= p) rest)

let result = qsort [5, 3, 8, 1, 9, 2, 7]

$ langthree qsort_op.l3
[1, 2, 3, 5, 7, 8, 9]
```

### 문자열 포매팅

```
$ cat format_op.l3
let formatList xs = "[" ^^ fold (fun acc -> fun x -> if acc = "" then to_string x else acc ^^ ", " ^^ to_string x) "" xs ^^ "]"

let result = [1..5] |> filter (fun x -> x > 2) |> formatList

$ langthree format_op.l3
"[3, 4, 5]"
```

### Option fallback 체인

```
$ cat fallback_op.l3
let tryParse s = match s with | "42" -> Some 42 | "0" -> Some 0 | _ -> None

let result = tryParse "abc" <|> tryParse "xyz" <|> tryParse "42" <|> Some 0

$ langthree fallback_op.l3
Some 42
```

### 나만의 연산자 정의

```
$ cat custom_op.l3
let (=?) a b = if a = b then "equal" else "not equal"

let r1 = 1 =? 1
let r2 = 1 =? 2
let result = r1 ^^ ", " ^^ r2

$ langthree custom_op.l3
"equal, not equal"
```

## 주의 사항

- `(*`는 블록 코멘트 시작이므로, `*`로 시작하는 연산자를 함수로 사용할 때는 공백이 필요합니다: `( ** )` (not `(**)`).
- 단일 문자 연산자 (`+`, `*`, `=` 등)는 재정의할 수 없습니다. 이들은 내장 연산자입니다.
- 연산자 이름은 2문자 이상이어야 합니다.
