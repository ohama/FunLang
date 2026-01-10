# End Session

End the current work session and save state for later resumption.

## Steps

1. Summarize work completed during this session:
   - Tasks completed
   - Current phase progress
   - Tests written/passed
2. Collect debugging observations:
   - Any issues encountered
   - Debugging commands used
   - Error patterns discovered
3. Ask user for any notes or context to save
4. Update `.claude/session/state.json` with:
   - Current phase progress
   - Work in progress
   - Completed tasks
   - Important notes and context
   - Debugging notes
   - Session end timestamp
5. **Save session prompts to file** (see below)
6. Generate session summary report
7. Confirm session saved successfully

## Session Prompt Logging (필수)

**endsession 실행 시점 기준으로 프롬프트 기록:**

### 기록 범위

| 시나리오 | 기록 범위 |
|----------|-----------|
| `startsession → endsession` | startsession 이후 ~ endsession 까지 |
| `endsession → endsession` | 이전 endsession 이후 ~ 현재 endsession 까지 |
| `startsession → endsession → endsession` | 두 번째 endsession은 첫 번째 endsession 이후부터 기록 |

### 파일명 규칙

- **현재 endsession 시점의 시간 사용**
- 형식: `docs/prompt/YYYY-MM-DD_HH-MM.md`
- 예: `docs/prompt/2026-01-10_18-30.md`

### 문법 교정 (필수)

사용자 프롬프트에 문법 오류가 있을 수 있음. **저장 전 반드시 교정:**

- 오타 수정
- 문법 오류 교정 (한국어/영어)
- 의미는 유지하되 자연스러운 문장으로 변환
- 원본과 교정본 모두 기록

### 저장 형식

```markdown
# Session Prompts: YYYY-MM-DD HH:MM

Period: {이전 endsession/startsession 시간} ~ {현재 endsession 시간}

---

## Prompts

### 1.
- **Original:** {원본 프롬프트}
- **Corrected:** {문법 교정된 프롬프트}

### 2.
- **Original:** {원본 프롬프트}
- **Corrected:** {문법 교정된 프롬프트}

...

---

## Summary

- Total prompts: {개수}
- Main topics: {주요 주제들}
- Corrections made: {교정 횟수}
```

### 구현 방법

1. `.claude/session/state.json`에서 `lastPromptLoggedAt` 필드 확인
2. 해당 시점 이후의 모든 사용자 메시지 수집
3. 파일 저장 후 `lastPromptLoggedAt`을 현재 시간으로 업데이트

**파일 경로:** `docs/prompt/` 디렉토리에 저장 (없으면 생성)

## Session State Schema

```json
{
  "sessionId": "uuid",
  "startedAt": "ISO timestamp",
  "endedAt": "ISO timestamp",
  "lastUpdatedAt": "ISO timestamp",
  "lastPromptLoggedAt": "ISO timestamp",
  "currentGoal": "User's main objective",
  "currentPhase": "Phase 0|1|1.2|1.5|2|3|4|5|6",
  "phaseProgress": {
    "phase": "current phase",
    "completedItems": ["items done in this phase"],
    "remainingItems": ["items left to do"]
  },
  "planFile": ".claude/PLAN.md",
  "workInProgress": ["list of ongoing tasks"],
  "notes": ["important context and notes"],
  "completedTasks": ["list of completed items"],
  "devGuidelines": {
    "tdd": true,
    "propertyBasedTesting": true,
    "testFramework": "FsCheck + xUnit"
  },
  "debuggingNotes": [
    "debugging observations from this session"
  ],
  "testStatus": {
    "lastRun": "ISO timestamp",
    "passed": true,
    "summary": "X tests passed"
  }
}
```

## Session Summary Report

Generate and display:

```
=== Session Summary ===

Session ID: {sessionId}
Duration: {startedAt} - {endedAt}

Current Phase: {currentPhase}
Phase Progress: {phaseProgress}

Completed Tasks:
- {task1}
- {task2}

Work In Progress:
- {wip1}
- {wip2}

Test Status: {passed/failed} ({summary})

Debugging Notes:
- {note1}
- {note2}

Notes for Next Session:
- {note1}
- {note2}

===========================
```

## Checklist Before Ending

Remind user to verify:

```
[ ] All tests pass? (dotnet test)
[ ] Build succeeds? (dotnet build)
[ ] Changes committed? (git status)
[ ] Debugging notes saved?
[ ] Next steps documented?
```

## Key Files Reference

```
.claude/PLAN.md       - Check phase progress
.claude/DEBUGGING.md  - Reference debugging guide
CLAUDE.md             - Development guidelines
```

## Output

Confirm that session state has been saved including:
1. Session summary with duration
2. Phase progress status
3. Completed tasks list
4. Work in progress
5. Test status
6. Debugging notes
7. Notes for next session
8. **Prompt log file path** (docs/prompt/YYYY-MM-DD_HH-MM.md)
9. Restoration command: `/startsession`
