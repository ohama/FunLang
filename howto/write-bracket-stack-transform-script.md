---
created: 2026-03-22
description: 괄호 종류를 추적하는 스택으로 소스 코드의 구분자를 안전하게 일괄 변환하기
---

# Bracket-Stack으로 소스 코드 일괄 변환하기

리스트 `[1, 2, 3]`의 콤마만 세미콜론으로 바꾸고, 튜플 `(1, 2)` 안의 콤마는 유지해야 할 때 — depth counter가 아니라 bracket stack을 사용한다.

## The Insight

"괄호 depth를 세면 되겠지"라는 직관적 접근이 실패하는 케이스가 있다. `([1, 2, 3], 5)` 같은 표현에서 paren_depth=1, list_depth=1일 때 콤마를 만나면 — 이것은 리스트 내부 콤마인가, 튜플 구분 콤마인가? depth counter만으로는 "가장 가까운 괄호가 `[`인지 `(`인지" 알 수 없다.

핵심: **depth counter 대신 bracket stack**을 써서 "지금 내가 어떤 종류의 괄호 안에 있는가"를 추적한다.

## Why This Matters

paren_depth 접근법으로 처음 변환하면 ~95%는 맞지만, 리스트-of-튜플 `[(1, 2), (3, 4)]`이나 튜플-of-리스트 `([1, 2], [3, 4])` 같은 중첩 케이스에서 오변환이 발생한다. 100+ 파일을 수동으로 검수하면 이런 케이스를 놓치기 쉽다.

실제로 이 프로젝트에서 첫 번째 paren_depth 스크립트는 70개 파일을 변환했지만, 8개 파일에서 튜플 내부 콤마가 세미콜론으로 잘못 변환되었다.

## Recognition Pattern

- 소스 코드에서 특정 문맥의 구분자만 바꿔야 할 때
- `[]` 안의 `,` vs `()` 안의 `,`를 구분해야 할 때
- AST 출력 텍스트에서 `List [...]` vs `Tuple (...)` 형식을 구분해야 할 때
- 100개 이상의 파일을 일괄 변환할 때

## The Approach

### Step 1: Bracket Stack 구현

문자를 순회하면서 스택에 괄호 종류를 push/pop한다:

```python
def transform_line(line):
    result = []
    i = 0
    in_string = False
    bracket_stack = []  # '[' 또는 '(' 를 쌓는다

    while i < len(line):
        ch = line[i]

        # 문자열 리터럴 내부는 건드리지 않는다
        if in_string:
            result.append(ch)
            if ch == '\\' and i + 1 < len(line):
                result.append(line[i+1])
                i += 2
                continue
            elif ch == '"':
                in_string = False
            i += 1
            continue

        if ch == '"':
            in_string = True
            result.append(ch)
        elif ch == '[':
            bracket_stack.append('[')
            result.append(ch)
        elif ch == ']':
            if bracket_stack and bracket_stack[-1] == '[':
                bracket_stack.pop()
            result.append(ch)
        elif ch == '(':
            bracket_stack.append('(')
            result.append(ch)
        elif ch == ')':
            if bracket_stack and bracket_stack[-1] == '(':
                bracket_stack.pop()
            result.append(ch)
        elif ch == ',' and bracket_stack and bracket_stack[-1] == '[':
            # 스택 꼭대기가 '[' → 리스트 내부 콤마 → 세미콜론으로 변환
            result.append(';')
        else:
            result.append(ch)

        i += 1

    return ''.join(result)
```

### Step 2: 핵심 — 스택 꼭대기 검사

```python
elif ch == ',' and bracket_stack and bracket_stack[-1] == '[':
    result.append(';')  # 리스트 내부만 변환
```

`bracket_stack[-1] == '['`가 핵심이다. depth counter로는 불가능한 판단:

```
([1, 2, 3], 5)
 ^        ^  ^
 [push    ]pop (pop — 스택 꼭대기가 '('이므로 콤마 유지
```

### Step 3: 파일 일괄 처리

```python
import os

def process_file(path):
    with open(path, 'r') as f:
        lines = f.readlines()
    new_lines = [transform_line(line) for line in lines]
    if new_lines != lines:
        with open(path, 'w') as f:
            f.writelines(new_lines)
        print(f"Updated: {path}")

for root, dirs, files in os.walk('tests/flt'):
    for fname in files:
        if fname.endswith('.flt'):
            process_file(os.path.join(root, fname))
```

### Step 4: 변환 검증

변환 후 잔여 오변환을 검출한다:

```bash
# 리스트 리터럴에 콤마가 남아있는지 (튜플 내부 제외)
grep -rn '\[[0-9].*,.*[0-9]\]' tests/flt/ | grep -v "([^)]*,[^)]*)" | head -20

# 이중 공백 포맷 문제
grep -rn ';  ' tests/flt/ | head -10

# 전체 테스트 실행
./run_tests.sh
```

## Example

실제 변환 예시:

```
입력: [(1, "a"), (2, "b"), (3, "c")]
      ^                             — bracket_stack = ['[']
       ^                            — bracket_stack = ['[', '(']
              ^                     — 콤마, 스택 꼭대기 '(' → 유지
                ^                   — bracket_stack = ['[']
                 ^                  — 콤마, 스택 꼭대기 '[' → ';'로 변환
출력: [(1, "a"); (2, "b"); (3, "c")]
```

```
입력: match Some [1, 2, 3] with
출력: match Some [1; 2; 3] with
```

```
입력: "이 문자열 [1, 2, 3] 안은 변환 안 됨"
출력: "이 문자열 [1, 2, 3] 안은 변환 안 됨"  (문자열 리터럴 보호)
```

## 체크리스트

- [ ] bracket_stack (depth counter 아님) 사용
- [ ] 문자열 리터럴 내부 보호 (`in_string` 플래그)
- [ ] 이스케이프 시퀀스 처리 (`\"` 안에서 `"` 무시)
- [ ] 변환 후 grep으로 잔여 콤마 검출
- [ ] 전체 테스트 실행으로 오변환 확인

## 관련 문서

- `change-separator-token-in-lalr-parser.md` - 파서 문법에서 구분자 변경
