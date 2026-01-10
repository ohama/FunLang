# Issue Management

Manage project issues (bugs, blockers, todos for later).

## Usage

```
/issue                    # Show all unresolved issues
/issue all                # Show all issues (resolved + unresolved)
/issue add <description>  # Add new unresolved issue
/issue resolve <id>       # Mark issue as resolved
/issue show <id>          # Show issue details
```

## Commands

### `/issue` or `/issue list`

Show all unresolved issues:

```
=== Unresolved Issues (3) ===

1. [high] issue-001: Parser conflict with NEWLINE token
   Context: src/FunLang/Parser.fsy
   Created: 2026-01-10 18:30

2. [medium] issue-002: Performance issue in large lists
   Context: src/FunLang/Interpreter.fs
   Created: 2026-01-10 17:00

3. [low] issue-003: Add better error messages
   Context: src/FunLang/Errors.fs
   Created: 2026-01-09 15:00

=============================
```

### `/issue all`

Show all issues including resolved:

```
=== All Issues ===

Unresolved (3):
  [high] issue-001: Parser conflict with NEWLINE token
  [medium] issue-002: Performance issue in large lists
  [low] issue-003: Add better error messages

Resolved (2):
  issue-000: Lexer not handling unicode (resolved 2026-01-10)
  issue-004: Type inference bug (resolved 2026-01-09)

==================
```

### `/issue add <description>`

Add a new unresolved issue. Will prompt for:
- Description (required)
- Priority (high/medium/low)
- Context (optional: file/function)

**Process:**
1. Generate new issue ID (issue-XXX, incrementing)
2. Add to `.claude/session/state.json` under `issues.unresolved`
3. Append to `docs/issues.md`
4. Confirm creation

**Example interaction:**
```
> /issue add Parser fails on nested match expressions

Creating new issue...

Priority? [high/medium/low]: medium
Context (file/function, optional): src/FunLang/Parser.fsy

Issue created:
  ID: issue-005
  Priority: medium
  Description: Parser fails on nested match expressions
  Context: src/FunLang/Parser.fsy
  Created: 2026-01-10 19:00

Saved to docs/issues.md
```

### `/issue resolve <id>`

Mark an issue as resolved. Will prompt for resolution description.

**Process:**
1. Find issue in `issues.unresolved`
2. Move to `issues.resolved` with resolution info
3. Update `docs/issues.md`
4. Confirm resolution

**Example interaction:**
```
> /issue resolve issue-001

Resolving issue-001: Parser conflict with NEWLINE token

How was it resolved?: Added nl_opt rule for optional NEWLINE

Issue resolved:
  ID: issue-001
  Resolution: Added nl_opt rule for optional NEWLINE
  Resolved: 2026-01-10 19:05

Updated docs/issues.md
```

### `/issue show <id>`

Show full details of an issue:

```
=== Issue: issue-001 ===

ID: issue-001
Status: resolved
Priority: high
Description: Parser conflict with NEWLINE token
Context: src/FunLang/Parser.fsy

Created: 2026-01-10 18:30
Resolved: 2026-01-10 19:05
Resolution: Added nl_opt rule for optional NEWLINE

========================
```

## Data Storage

### Session State (`.claude/session/state.json`)

```json
{
  "issues": {
    "unresolved": [
      {
        "id": "issue-001",
        "createdAt": "2026-01-10T18:30:00.000Z",
        "description": "Parser conflict with NEWLINE token",
        "context": "src/FunLang/Parser.fsy",
        "priority": "high",
        "sessionCreated": "session-id"
      }
    ],
    "resolved": [
      {
        "id": "issue-000",
        "createdAt": "2026-01-09T10:00:00.000Z",
        "resolvedAt": "2026-01-10T15:00:00.000Z",
        "description": "Lexer not handling unicode",
        "context": "src/FunLang/Lexer.fsl",
        "priority": "high",
        "resolution": "Added --unicode flag to FsLex",
        "sessionCreated": "session-id",
        "sessionResolved": "session-id"
      }
    ],
    "nextId": 6
  }
}
```

### Issue History (`docs/issues.md`)

Persistent record of all issues:

```markdown
# Issue History

## Unresolved

### issue-005: Parser fails on nested match expressions
- **Priority**: medium
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

---
```

## Integration with Session Commands

### startsession

Shows unresolved issues on session start:

```
=== Unresolved Issues (2) ===
1. [high] issue-001: Parser conflict with NEWLINE token
2. [medium] issue-002: Performance issue in large lists
=============================
```

### endsession

Shows issues created/resolved during this session:

```
=== Session Issues ===

Created this session:
- [medium] issue-005: Parser fails on nested match expressions (unresolved)

Resolved this session:
- issue-001: Parser conflict with NEWLINE token

======================
```

## Implementation Notes

1. **Issue IDs**: Auto-increment, format `issue-XXX` (3 digits, zero-padded)
2. **Priority levels**: high, medium, low
3. **Session tracking**: Track which session created/resolved each issue
4. **Persistence**: Both state.json and docs/issues.md are updated
5. **docs/issues.md**: Human-readable, git-tracked history
