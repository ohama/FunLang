# Issue Management

Manage project issues (bugs, blockers, todos for later).

> **중앙 문서:** `.claude/ISSUES.md` - 이슈 관리 시스템의 전체 가이드

## Usage

```
/issue                    # Show all unresolved issues (default)
/issue unresolved         # Show all unresolved issues (summary)
/issue resolved           # Show all resolved issues (summary)
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

### `/issue unresolved`

Show all unresolved issues with summary:

```
=== Unresolved Issues (3) ===

1. [high] issue-001: Parser conflict with NEWLINE token
   Context: src/FunLang/Parser.fsy
   Created: 2026-01-10 18:30
   Summary: NEWLINE after IN keyword causes parser error

2. [medium] issue-002: Performance issue in large lists
   Context: src/FunLang/Interpreter.fs
   Created: 2026-01-10 17:00
   Summary: List operations slow with 1000+ elements

3. [low] issue-003: Add better error messages
   Context: src/FunLang/Errors.fs
   Created: 2026-01-09 15:00
   Summary: Error messages lack line/column info

Use `/issue show <id>` for full details
=============================
```

### `/issue resolved`

Show all resolved issues with summary:

```
=== Resolved Issues (2) ===

1. issue-000: Lexer not handling unicode
   Context: src/FunLang/Lexer.fsl
   Created: 2026-01-09 10:00
   Resolved: 2026-01-10 15:00
   Summary: Added --unicode flag to FsLex

2. issue-004: Type inference bug with tuples
   Context: src/FunLang/TypeInfer.fs
   Created: 2026-01-08 14:00
   Resolved: 2026-01-09 11:00
   Summary: Fixed unification for tuple types

Use `/issue show <id>` for full details
=============================
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
        "summary": "NEWLINE after IN causes parser error",
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
        "summary": "Unicode characters not recognized",
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

### Issue History (docs/issues/)

각 이슈는 개별 파일로 저장:

```
docs/issues/
├── unresolved/
│   ├── issue-001.md
│   ├── issue-002.md
│   └── ...
└── resolved/
    ├── issue-000.md
    └── ...
```

#### Unresolved Issue 파일 형식

`docs/issues/unresolved/issue-005.md`:

```markdown
# issue-005: Parser fails on nested match expressions

- **Status**: unresolved
- **Priority**: high
- **Context**: src/FunLang/Parser.fsy
- **Created**: 2026-01-10 19:00
- **Session**: a1b2c3d4

## Summary

Parser fails when handling nested match expressions.

## Description

Detailed description of the issue...

## Notes

- Related to pattern matching implementation
- Might need grammar changes
```

#### Resolved Issue 파일 형식

`docs/issues/resolved/issue-001.md`:

```markdown
# issue-001: Parser conflict with NEWLINE token

- **Status**: resolved
- **Priority**: high
- **Context**: src/FunLang/Parser.fsy
- **Created**: 2026-01-10 18:30
- **Resolved**: 2026-01-10 19:05
- **Session Created**: a1b2c3d4
- **Session Resolved**: b2c3d4e5

## Summary

NEWLINE after IN keyword causes parser error.

## Description

Parser conflict when NEWLINE appears after IN keyword.

## Resolution

Added nl_opt rule for optional NEWLINE after IN, THEN, ELSE, ARROW.
```

### 이슈 상태 변경 시 파일 이동

1. **이슈 생성 시**:
   - `docs/issues/unresolved/issue-XXX.md` 파일 생성

2. **이슈 해결 시**:
   - `docs/issues/unresolved/issue-XXX.md` → `docs/issues/resolved/issue-XXX.md` 이동
   - Resolution 정보 추가

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
