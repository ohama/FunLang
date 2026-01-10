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
3. **Ask about issues** (see Issue Tracking section below):
   - Ask if there are any unresolved issues to record
   - Ask if any previously unresolved issues were resolved
   - Record issue status changes
4. Ask user for any notes or context to save
5. Update `.claude/session/state.json` with:
   - Current phase progress
   - Work in progress
   - Completed tasks
   - Important notes and context
   - Debugging notes
   - **Issues (unresolved/resolved)**
   - Session end timestamp
6. **Save session prompts to file** (see below)
7. Generate session summary report
8. Confirm session saved successfully

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

### 문법 교정 규칙

각 프롬프트당 **하나의 버전만 기록:**

| 상황 | 기록 내용 |
|------|-----------|
| 문법 오류 없음 | 원본 프롬프트 그대로 기록 |
| 문법 오류 있음 | 교정된 프롬프트만 기록 (원본 제외) |

### 저장 형식

```markdown
# Session Prompts: YYYY-MM-DD HH:MM

Period: {이전 endsession/startsession 시간} ~ {현재 endsession 시간}

---

## Prompts

1. {프롬프트 (원본 또는 교정본)}

2. {프롬프트 (원본 또는 교정본)}

3. ...

---

## Summary

- Total prompts: {개수}
- Main topics: {주요 주제들}
```

### 구현 방법

1. `.claude/session/state.json`에서 `lastPromptLoggedAt` 필드 확인
2. 해당 시점 이후의 모든 사용자 메시지 수집
3. 파일 저장 후 `lastPromptLoggedAt`을 현재 시간으로 업데이트

**파일 경로:** `docs/prompt/` 디렉토리에 저장 (없으면 생성)

## Issue Tracking

세션 종료 시 이슈 상태를 확인하고 기록합니다.

**관련 명령어:** `/issue` - 이슈 조회/추가/해결

### 질문 순서

1. **Unresolved Issues 확인:**
   ```
   이번 세션에서 해결되지 않은 이슈가 있나요?
   (버그, 막힌 부분, 나중에 확인할 사항 등)
   ```
   - 있으면 `/issue add` 로 추가하거나 직접 state.json에 기록

2. **Resolved Issues 확인:**
   ```
   이전에 기록된 unresolved issue 중 해결된 것이 있나요?
   ```
   - 있으면 `/issue resolve <id>` 로 해결 처리

### 이슈 기록 위치

1. **`.claude/session/state.json`**: 현재 이슈 상태 (실시간)
2. **`docs/issues.md`**: 이슈 히스토리 (영구 기록, git 추적)

### 이슈 기록 형식 (state.json)

```json
{
  "issues": {
    "unresolved": [
      {
        "id": "issue-001",
        "createdAt": "ISO timestamp",
        "description": "이슈 설명",
        "context": "관련 파일/함수",
        "priority": "high|medium|low",
        "sessionCreated": "session-id"
      }
    ],
    "resolved": [
      {
        "id": "issue-001",
        "createdAt": "ISO timestamp",
        "resolvedAt": "ISO timestamp",
        "description": "이슈 설명",
        "resolution": "해결 방법",
        "sessionCreated": "session-id",
        "sessionResolved": "session-id"
      }
    ],
    "nextId": 2
  }
}
```

### Session Issues Display (endsession 시)

이번 세션에서 생성/해결된 이슈를 표시:

```
=== Session Issues ===

Created this session (2):
  [high] issue-005: Parser fails on nested match (unresolved)
  [low] issue-006: Add documentation for patterns (unresolved)

Resolved this session (1):
  issue-001: Parser conflict with NEWLINE token
  → Added nl_opt rule for optional NEWLINE

======================
```

### docs/issues.md 업데이트

이슈 생성/해결 시 `docs/issues.md` 파일도 함께 업데이트:

```markdown
## Unresolved

### issue-005: Parser fails on nested match expressions
- **Priority**: high
- **Context**: src/FunLang/Parser.fsy
- **Created**: 2026-01-10 19:00
- **Session**: a1b2c3d4

---

## Resolved

### issue-001: Parser conflict with NEWLINE token
- **Priority**: high
- **Context**: src/FunLang/Parser.fsy
- **Created**: 2026-01-10 18:30
- **Resolved**: 2026-01-10 19:05
- **Resolution**: Added nl_opt rule for optional NEWLINE
```

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
  "nextPhase": {
    "phase": "Phase X: Name",
    "items": ["item 1", "item 2"]
  },
  "planFile": ".claude/PLAN.md",
  "workInProgress": ["list of ongoing tasks"],
  "notes": ["important context and notes"],
  "completedTasks": ["list of completed items"],
  "devGuidelines": {
    "tdd": true,
    "propertyBasedTesting": true,
    "testFramework": "Expecto + FsCheck"
  },
  "debuggingNotes": [
    "debugging observations from this session"
  ],
  "issues": {
    "unresolved": [
      {
        "id": "issue-001",
        "createdAt": "ISO timestamp",
        "description": "description",
        "context": "file/function",
        "priority": "high|medium|low"
      }
    ],
    "resolved": [
      {
        "id": "issue-001",
        "createdAt": "ISO timestamp",
        "resolvedAt": "ISO timestamp",
        "description": "description",
        "resolution": "how it was fixed"
      }
    ]
  },
  "testStatus": {
    "lastRun": "ISO timestamp",
    "passed": 0,
    "failed": 0,
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

Issues:
  Unresolved: {count}
  - [priority] {description} ({id})
  Resolved this session: {count}
  - {description} → {resolution}

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
7. **Issues status** (unresolved count, resolved this session)
8. Notes for next session
9. **Prompt log file path** (docs/prompt/YYYY-MM-DD_HH-MM.md)
10. Restoration command: `/startsession`
