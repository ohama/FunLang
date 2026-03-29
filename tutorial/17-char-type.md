# 문자 타입 (Char Type)

앞 장에서 문자열을 다뤘으니, 이번에는 단일 문자를 살펴봅니다. LangThree는 문자열(`string`)과는 별도로 단일 문자를 표현하는 `char` 타입을 제공합니다. 작은따옴표로 문자 리터럴을 작성하며, 문자와 정수 사이의 변환 함수를 제공합니다.

## 문자 리터럴

작은따옴표로 단일 문자를 표현합니다:

```
funlang> 'a'
'a'

funlang> 'Z'
'Z'

funlang> '0'
'0'
```

이스케이프 시퀀스도 지원합니다:

```
funlang> '\n'
'\n'

funlang> '\t'
'\t'
```

## 문자 변환

`char_to_int`와 `int_to_char`로 문자와 ASCII 코드 사이를 변환합니다:

```
$ cat char_conv.l3
let code = char_to_int 'A'
let back = int_to_char 65
let result = code

$ langthree char_conv.l3
65
```

`int_to_char`는 0~127 범위의 ASCII 코드만 지원합니다. 범위를 벗어나면 오류가 발생합니다.

이 함수들은 문자 기반 알고리즘에 유용합니다. 예를 들어, 대문자를 소문자로 변환하려면:

```
$ cat to_lower.l3
let toLower c =
    let code = char_to_int c
    if code >= 65 then
        if code <= 90 then int_to_char (code + 32)
        else c
    else c
let result = toLower 'H'

$ langthree to_lower.l3
'h'
```

## 문자 비교

문자는 비교 연산자로 순서를 비교할 수 있습니다. ASCII 코드 값 기준으로 비교됩니다:

```
funlang> 'a' < 'z'
true

funlang> 'A' > 'Z'
false

funlang> 'a' = 'a'
true
```

문자와 문자열 사이의 비교도 가능합니다. 비교 연산자는 피연산자의 타입에 따라 자동으로 widening됩니다.

## 패턴 매칭에서의 문자

문자 리터럴은 패턴 매칭에서도 사용할 수 있습니다:

```
$ cat char_match.l3
let classify c =
    match c with
    | 'a' -> "lowercase a"
    | 'A' -> "uppercase A"
    | _ -> "other"
let result = classify 'A'

$ langthree char_match.l3
"uppercase A"
```

## Char 모듈 함수

Prelude의 `Char` 모듈은 문자 판별 및 변환 함수를 제공합니다. 위에서 `char_to_int`로 직접 구현한 `toLower` 같은 변환을 더 간단하게 할 수 있습니다:

```
$ cat char_module.l3
let _ = println (to_string (Char.IsDigit '3'))
let _ = println (to_string (Char.IsDigit 'a'))
let _ = println (to_string (Char.IsLetter 'z'))
let _ = println (to_string (Char.ToUpper 'a'))

$ langthree char_module.l3
true
false
true
'A'
()
```

대소문자 판별과 변환도 제공됩니다:

```
$ cat char_case.l3
let _ = println (to_string (Char.IsUpper 'A'))
let _ = println (to_string (Char.IsLower 'a'))
let _ = println (to_string (Char.ToLower 'Z'))

$ langthree char_case.l3
true
true
'z'
()
```

| 함수 | 설명 |
|------|------|
| `Char.IsDigit c` | 숫자 문자('0'~'9')인지 확인 |
| `Char.IsLetter c` | 알파벳 문자인지 확인 |
| `Char.IsUpper c` | 대문자인지 확인 |
| `Char.IsLower c` | 소문자인지 확인 |
| `Char.ToUpper c` | 대문자로 변환 |
| `Char.ToLower c` | 소문자로 변환 |

이 함수들을 사용하면 `char_to_int`/`int_to_char`로 ASCII 코드를 직접 계산할 필요 없이, 의도가 명확한 코드를 작성할 수 있습니다.

## 참고 사항

- **문자 리터럴:** `'a'`, `'\n'` (작은따옴표)
- **문자열 리터럴:** `"hello"` (큰따옴표) — 서로 다른 타입
- **`char_to_int`:** 문자 → ASCII 코드 (int)
- **`int_to_char`:** ASCII 코드 → 문자 (0~127만)
- **비교:** `<`, `>`, `<=`, `>=`, `=` 모두 지원

문자열과 문자를 다루는 방법을 알았으니, 다음 장에서는 파이프 연산자와 함수 합성으로 데이터 처리 파이프라인을 구성하는 방법을 알아봅니다.
