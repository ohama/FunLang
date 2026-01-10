# Run Tests

Run all tests in the solution and report results.

## Steps

1. Run `dotnet test` to execute all tests
2. Summarize test results (passed/failed/skipped)
3. **If any tests fail, create an issue for each failure** (MANDATORY):
   ```
   /issue add "테스트 실패: [테스트명]"
   ```
4. Analyze the failures and suggest fixes
5. If issue was created and then fixed, resolve it:
   ```
   /issue resolve <id>
   ```

## Issue Recording (필수)

테스트 실패 시 반드시 이슈를 기록해야 합니다:

| 상황 | 행동 |
|------|------|
| 테스트 실패 | `/issue add "테스트 실패: [테스트명]"` |
| 테스트 통과 | `/issue resolve <id>` (기존 이슈가 있다면) |
| 같은 테스트 재실패 | 기존 이슈 참조 후 해결 |

**이슈 기록 예시:**

```
테스트 실패:
  [FAIL] Parser.parse tuple pattern (Expected: Ok, Actual: Error)

이슈 생성:
  /issue add "테스트 실패: Parser.parse tuple pattern"
  Priority: high
  Context: tests/FunLang.Tests/ParserTests.fs
  Summary: Tuple pattern parsing returns Error instead of Ok
```

## Multiple Failures

여러 테스트가 실패한 경우:

1. **관련된 실패는 하나의 이슈로 그룹화**
   - 같은 원인의 여러 실패 → 하나의 이슈
   - 다른 원인의 실패 → 각각 별도 이슈

2. **우선순위 결정**
   - 전체 빌드 차단 → high
   - 특정 기능 영향 → medium
   - 마이너 이슈 → low

## Output

```
=== Test Result ===

Status: PASSED / FAILED
Total: {total}
Passed: {passed}
Failed: {failed}
Skipped: {skipped}

[If failed]
Failed Tests:
  1. {test_name}: {error_summary}
  2. {test_name}: {error_summary}

Issues Created: issue-XXX, issue-YYY
====================
```

## Quick Commands

```bash
# 전체 테스트
dotnet test

# 특정 테스트만
dotnet run --project tests/FunLang.Tests -- --filter "Parser"

# 상세 출력
dotnet run --project tests/FunLang.Tests -- --debug
```
