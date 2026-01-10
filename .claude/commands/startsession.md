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

## Session State Schema

```json
{
  "sessionId": "uuid",
  "startedAt": "ISO timestamp",
  "lastUpdatedAt": "ISO timestamp",
  "currentGoal": "User's main objective",
  "currentPhase": "Phase 0|1|1.2|1.5|2|3|4|5|6",
  "planFile": ".claude/PLAN.md",
  "workInProgress": ["list of ongoing tasks"],
  "notes": ["important context and notes"],
  "completedTasks": ["list of completed items"],
  "devGuidelines": {
    "tdd": true,
    "propertyBasedTesting": true,
    "testFramework": "FsCheck + xUnit"
  },
  "debuggingNotes": ["any debugging observations"]
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
.claude/PLAN.md       - Implementation plan
.claude/DEBUGGING.md  - Debugging guide
CLAUDE.md             - Development guidelines
README.md             - Project overview
```

## Output

Report the session status with:
1. Session ID and timestamps
2. Current phase and goal
3. Work in progress
4. TDD reminder
5. Debugging options summary
6. Ready to work message
