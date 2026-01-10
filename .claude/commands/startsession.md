# Start Session

Start a new work session and load session state with project context.

## Steps

1. Check if `.claude/session/state.json` exists
2. If exists, read and display the saved session state:
   - Current goal/phase
   - Work in progress
   - Notes and context
   - Last modified timestamp
   - Development guidelines reminder
3. If no saved state exists, create a new session:
   - Ask user for the current goal/task
   - Initialize empty session state
4. Save session start time to `.claude/session/state.json`
5. Display important project context:
   - Current phase from PLAN.md
   - TDD/FsCheck requirements reminder
   - Available debugging options
6. **Display next steps prominently** (from `nextPhase` in state.json):
   - Show the next phase name
   - List all items to be implemented
   - Highlight the first actionable item
7. **Display unresolved issues** (if any exist):
   - Show count and list of unresolved issues
   - Include priority and context for each
   - Remind user these need attention

## Session State Schema

```json
{
  "sessionId": "uuid",
  "startedAt": "ISO timestamp",
  "lastUpdatedAt": "ISO timestamp",
  "lastPromptLoggedAt": "ISO timestamp",
  "currentGoal": "User's main objective",
  "currentPhase": "Phase 0|1|1.2|1.5|2|3|4|5|6",
  "phaseProgress": {
    "phase": "current phase description",
    "completedItems": ["items done in this phase"],
    "remainingItems": ["items left to do"]
  },
  "nextPhase": {
    "phase": "Phase X: Name",
    "items": ["item 1", "item 2", "..."]
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
  "debuggingNotes": ["any debugging observations"],
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
    "resolved": []
  },
  "testStatus": {
    "lastRun": "ISO timestamp",
    "passed": 0,
    "failed": 0,
    "summary": "X tests passed"
  }
}
```

## Project Context Display

### Development Guidelines (Required)

```
[TDD Required]
1. RED   : Write failing test first
2. GREEN : Minimum code to pass
3. REFACTOR : Clean up (tests must pass)

[FsCheck Required]
- Property-based testing, not simple examples
- Test algebraic properties, roundtrips, invariants
```

### Available Debugging Options

```
CLI Options:
  -d, --debug          Full debug mode
  --show-tokens        Lexer output
  --show-ast           Parser output
  --show-types         Type inference output
  --show-indents       Indentation tokens
  --trace <phase>      Trace specific phase
  --log-level <level>  Set log level
  --log-file <path>    Log to file

REPL Commands:
  :env    Show environment
  :type   Show type
  :help   Show help

See .claude/DEBUGGING.md for full guide.
```

### Key Files

```
.claude/PLAN.md              - Implementation plan
.claude/DEBUGGING.md         - Debugging guide
CLAUDE.md                    - Development guidelines
README.md                    - Project overview
docs/issues/unresolved.md    - Unresolved issues
docs/issues/resolved.md      - Resolved issues history
```

## Output

Report the session status with:
1. Session ID and timestamps
2. Current phase and goal
3. Work in progress
4. TDD reminder
5. Debugging options summary
6. **Next Steps section** (important!)
7. **Unresolved Issues section** (if any exist)
8. Ready to work message

### Next Steps Display Format

```
=== Next Steps ===

Phase: {nextPhase.phase}

To Do:
  1. {nextPhase.items[0]} <- Start here
  2. {nextPhase.items[1]}
  3. {nextPhase.items[2]}
  ...

Suggested command: "Phase 5 시작하자" or specific item
==================
```

This section should be displayed prominently so the user knows exactly what to work on next.

### Unresolved Issues Display Format

If there are unresolved issues, display them prominently:

```
=== Unresolved Issues ({count}) ===

1. [high] issue-001: Parser conflict with NEWLINE token
   Context: src/FunLang/Parser.fsy
   Created: 2026-01-10

2. [medium] issue-002: Performance issue in large lists
   Context: src/FunLang/Interpreter.fs
   Created: 2026-01-09

Use `/issue` to manage issues
Use `/issue resolve <id>` to mark as resolved
==============================
```

If no unresolved issues exist, skip this section entirely.

### Issue Management Commands

```
/issue              # Show all unresolved issues
/issue all          # Show all issues (resolved + unresolved)
/issue add <desc>   # Add new issue
/issue resolve <id> # Mark issue as resolved
/issue show <id>    # Show issue details
```

See `docs/issues/` for issue history:
- `docs/issues/unresolved.md` - 미해결 이슈
- `docs/issues/resolved.md` - 해결된 이슈
