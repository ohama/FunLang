# issue-009: Match expression INDENT issue after else

- **Status**: unresolved
- **Priority**: medium
- **Context**: Parser.fsy, tests/file-tests/integrated-tests/001-sorting-algorithms.test
- **Created**: 2026-01-12
- **Session**: current

## Summary

`else match ... with` 구문에서 다음 줄의 match cases가 INDENT로 시작하면 파싱 실패

## Error Message

```
Parse error at line 72, column 8: unexpected 'INDENT', expected one of: newline, '|'
```

## Reproduction

```funlang
let rec take = fun n -> fun xs ->
  if n <= 0 then []
  else match xs with
       | [] -> []                    // <- INDENT 발생
       | h :: t -> h :: take (n - 1) t
```

## Root Cause

`match_cases_start` 규칙이 INDENT를 처리하지 않음:

```fsy
match_cases_start:
    | match_cases                           { $1 }
    | NEWLINE match_cases                   { $2 }
    // INDENT 케이스 없음!
```

## Proposed Fix

Parser.fsy에 INDENT 케이스 추가:

```fsy
match_cases_start:
    | match_cases                           { $1 }
    | NEWLINE match_cases                   { $2 }
    | NEWLINE INDENT match_cases DEDENT     { $3 }  // 추가
```

## Workaround

코드 스타일 변경으로 INDENT 회피:
1. `else` 다음에 블록 스타일 사용
2. 또는 match를 별도 let binding으로 분리

## Related

- issue-008: Sorting algorithms integrated test (이 문제로 인해 여전히 실패)
