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
5. Generate session summary report
6. Confirm session saved successfully

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
8. Restoration command: `/startsession`
