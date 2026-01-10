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

**세션 중 모든 사용자 프롬프트를 기록:**

1. 현재 시간으로 파일명 생성: `docs/prompt/YYYY-MM-DD_HH-MM.md`
2. 세션 시작(startsession)부터 종료(endsession)까지의 모든 사용자 메시지 수집
3. 다음 형식으로 저장:

```markdown
# Session Prompts: YYYY-MM-DD HH:MM

Session ID: {sessionId}
Started: {startedAt}
Ended: {endedAt}

---

## Prompts

1. {첫 번째 사용자 프롬프트}

2. {두 번째 사용자 프롬프트}

3. ...

---

## Summary

- Total prompts: {개수}
- Main topics: {주요 주제들}
```

**파일 경로:** `docs/prompt/` 디렉토리에 저장 (없으면 생성)

## Session State Schema

```json
{
  "sessionId": "uuid",
  "startedAt": "ISO timestamp",
  "endedAt": "ISO timestamp",
  "lastUpdatedAt": "ISO timestamp",
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
