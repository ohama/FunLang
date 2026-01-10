# End Session

End the current work session and save state for later resumption.

## Steps

1. Summarize work completed during this session
2. Ask user for any notes or context to save for next session
3. Update `.claude/session/state.json` with:
   - Current work in progress
   - Important notes and context
   - Completed tasks
   - Session end timestamp
4. Display summary of saved state
5. Confirm session saved successfully

## Session State Schema

```json
{
  "sessionId": "uuid",
  "startedAt": "ISO timestamp",
  "endedAt": "ISO timestamp",
  "lastUpdatedAt": "ISO timestamp",
  "currentGoal": "User's main objective",
  "workInProgress": ["list of ongoing tasks"],
  "notes": ["important context and notes"],
  "completedTasks": ["list of completed items"]
}
```

## Output

Confirm that session state has been saved and can be restored with `/startsession`.
