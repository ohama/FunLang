# issue-008: Inline match syntax parsing

- **Status**: resolved
- **Priority**: medium
- **Context**: Parser.fsy match_cases rules
- **Created**: 2026-01-12
- **Resolved**: 2026-01-13
- **Session**: p5678901

## Summary

인라인 match 문법 (`| [] -> match ys with | [] -> true | _ -> false`)이 후속 케이스를 잘못 파싱하는 문제

## Root Cause

기존 `match_cases` 규칙이 NEWLINE 여부와 관계없이 모든 케이스를 동일 match에 포함:
```fsy
match_cases:
    | match_case                            { [$1] }
    | match_case match_cases                { $1 :: $2 }
    | match_case NEWLINE match_cases        { $1 :: $3 }  // 문제!
```

인라인 match 바디 다음에 오는 외부 match의 케이스가 내부 match의 케이스로 파싱됨.

## Resolution

인라인 vs 블록 match cases를 분리:

```fsy
// Inline: NEWLINE 없이 연속된 케이스만
match_cases_inline:
    | match_case                            { [$1] }
    | match_case match_cases_inline         { $1 :: $2 }

// Block: NEWLINE 허용
match_cases_block:
    | match_case                            { [$1] }
    | match_case match_cases_block          { $1 :: $2 }
    | match_case NEWLINE match_cases_block  { $1 :: $3 }

match_cases_start:
    | match_cases_inline                    { $1 }     // 인라인
    | NEWLINE match_cases_block             { $2 }     // 블록
    | NEWLINE INDENT match_cases_block DEDENT { $3 }
    | INDENT match_cases_block DEDENT       { $2 }
```

## Test Case

```fsharp
let rec listEqual = fun xs -> fun ys ->
  match xs with
  | [] -> match ys with | [] -> true | _ -> false  // 인라인 match
  | xh :: xt ->                                     // 외부 match 케이스
    match ys with
    | [] -> false
    | yh :: yt -> if xh = yh then listEqual xt yt else false

listEqual [1; 2; 3] [1; 2; 3]  // => true
```

## Related

- issue-009: Match expression INDENT issue (resolved)
- issue-010: Position test regression (resolved)
