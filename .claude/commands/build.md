# Build Project

Build the F# project and report any errors.

## Steps

1. Run `dotnet build` in the project root
2. If there are errors, analyze them and suggest fixes
3. **If build fails, create an issue** (MANDATORY):
   ```
   /issue add "빌드 에러: [에러 내용 요약]"
   ```
4. Report build status (success/failure)
5. If issue was created and then fixed, resolve it:
   ```
   /issue resolve <id>
   ```

## Issue Recording (필수)

빌드 실패 시 반드시 이슈를 기록해야 합니다:

| 상황 | 행동 |
|------|------|
| 빌드 실패 | `/issue add "빌드 에러: ..."` |
| 에러 해결 | `/issue resolve <id>` |
| 같은 에러 재발 | 기존 이슈 참조 후 해결 |

**이슈 기록 예시:**

```
빌드 에러 발생:
  error FS0001: Type mismatch in Parser.fs line 42

이슈 생성:
  /issue add "빌드 에러: Type mismatch in Parser.fs:42"
  Priority: high
  Context: src/FunLang/Parser.fs
```

## Output

```
=== Build Result ===

Status: SUCCESS / FAILURE
Errors: {count}
Warnings: {count}

[If failed]
Issues Created: issue-XXX
====================
```
