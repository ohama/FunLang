# Start Session

Start a new work session and load any saved session state.

## Steps

1. Check if `.claude/session/state.json` exists
2. If exists, read and display the saved session state:
   - Current task/goal
   - Work in progress
   - Notes and context
   - Last modified timestamp
3. If no saved state exists, create a new session:
   - Ask user for the current goal/task
   - Initialize empty session state
4. Save session start time to `.claude/session/state.json`
5. Display session summary and ready to work message

## Session State Schema

```json
{
  "sessionId": "uuid",
  "startedAt": "ISO timestamp",
  "lastUpdatedAt": "ISO timestamp",
  "currentGoal": "User's main objective",
  "workInProgress": ["list of ongoing tasks"],
  "notes": ["important context and notes"],
  "completedTasks": ["list of completed items"]
}
```

## Output

Report the session status and any loaded context to help resume work efficiently.
