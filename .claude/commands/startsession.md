# Start Session

Start a new work session and restore context from previous sessions.

## Steps

1. **Read context files** (컨텍스트 복원):
   - `.claude/HISTORY.md` - 이전 세션 히스토리 및 축적된 지식
   - `.claude/PLAN.md` - 구현 계획 및 현재 phase
   - `.claude/session/state.json` - 마지막 세션 상태 (있다면)
   - `docs/issues/unresolved/*.md` - 미해결 이슈 파일들 (있다면 모두 읽기)
   - `docs/FILE_BASED_TESTING.md` - 파일 기반 테스트 가이드 (있다면)

2. **Restore accumulated knowledge** from HISTORY.md:
   - Recent session summaries (최근 세션 요약)
   - Key decisions made (중요한 결정사항)
   - Discovered patterns & tips (발견한 패턴/팁)
   - Common pitfalls to avoid (피해야 할 실수)

3. **Determine current phase** from PLAN.md:
   - Current phase and progress
   - Next phase items
   - Remaining work

4. **Restore session state** (read-only, DO NOT write):
   - If state.json exists: read previous state
   - If not: determine state from HISTORY.md and PLAN.md
   - Note: Session state is saved only by `/endsession`

5. **Display session context**:
   - Previous session summary (from HISTORY.md)
   - Current phase from PLAN.md
   - TDD/FsCheck requirements reminder
   - Available debugging options

6. **Display next steps prominently** (from `nextPhase`):
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
[Issue Tracking - 필수]
빌드 실패 → /issue add "빌드 에러: ..."
테스트 실패 → /issue add "테스트 실패: ..."
해결 시 → /issue resolve <id>

[TDD Required]
1. RED   : Write failing test first
2. GREEN : Minimum code to pass
3. REFACTOR : Clean up (tests must pass)

[FsCheck Required]
- Property-based testing, not simple examples
- Test algebraic properties, roundtrips, invariants
```

⚠️ **이슈 기록 의무**: 빌드/테스트 실패 시 반드시 이슈를 기록해야 합니다!

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
.claude/HISTORY.md           - Session history & accumulated knowledge (READ FIRST)
.claude/PLAN.md              - Implementation plan & current phase
.claude/session/state.json   - Last session state
.claude/DEBUGGING.md         - Debugging guide
CLAUDE.md                    - Development guidelines
README.md                    - Project overview
docs/issues/unresolved/      - Unresolved issue files
docs/issues/resolved/        - Resolved issue files
docs/FILE_BASED_TESTING.md   - File-based testing guide (if exists)
```

## Context Restoration from HISTORY.md

세션 시작 시 `.claude/HISTORY.md`에서 다음 정보를 읽어 컨텍스트를 복원합니다.

### 읽어야 할 섹션

| 섹션 | 용도 |
|------|------|
| Current Status | 현재 phase, 테스트 상태 파악 |
| Recent Sessions | 최근 작업 내용 확인 |
| Accumulated Knowledge | 축적된 팁/패턴/결정 복원 |

### 컨텍스트 복원 순서

```
1. .claude/HISTORY.md 읽기
   ↓
2. Current Status에서 현재 상태 파악
   ↓
3. Recent Sessions에서 최근 작업 확인
   ↓
4. Accumulated Knowledge 내재화
   - Build/Parser Tips
   - Common Pitfalls
   - Architecture Decisions
   ↓
5. .claude/PLAN.md 읽기
   ↓
6. 현재 phase와 다음 할 일 파악
   ↓
7. state.json 읽기 (있다면)
   ↓
8. docs/issues/unresolved/*.md 읽기 (이슈 파일들)
   ↓
9. docs/FILE_BASED_TESTING.md 읽기 (있다면)
   ↓
10. 세션 정보 표시 (파일 쓰기 없음)
```

### Previous Session Display

HISTORY.md에서 가장 최근 세션 정보를 표시:

```
=== Previous Session ===

Date: 2026-01-10
Session: a1b2c3d4

Completed:
- Issue 관리 시스템 구현
- /issue command 추가

Key Decisions:
- endsession에서 사용자 질문 없이 자동 판단

Unresolved Issues:
- (없음)

========================
```

### HISTORY.md가 없는 경우

첫 세션이거나 HISTORY.md가 없으면:
1. PLAN.md에서 Phase 0 시작
2. 기본 목표: "Build the FunLang interpreter"
3. HISTORY.md는 `/endsession`에서 생성됨 (startsession에서는 생성하지 않음)

## Output

Report the session status with:
1. **Context restored from** (HISTORY.md, PLAN.md)
2. **Previous session summary** (from HISTORY.md)
3. Session ID and timestamps
4. Current phase and goal
5. Accumulated knowledge reminder (key tips/patterns)
6. Work in progress
7. TDD reminder
8. Debugging options summary
9. **Next Steps section** (important!)
10. **Unresolved Issues section** (if any exist)
11. Ready to work message

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

**IMPORTANT:** If there are unresolved issues, ALWAYS display them prominently in the session output.

**How to get unresolved issues:**
1. Check `docs/issues/unresolved/` directory for `.md` files
2. Read each issue file to get description, workaround, impact
3. Also check `state.json` issues.unresolved array for quick reference

**Display format:**

```
=== Unresolved Issues ({count}) ===

1. [priority] issue-XXX: {short description}
   Impact: {low|medium|high}
   Workaround: {brief workaround if available}
   File: docs/issues/unresolved/XXX-description.md

2. [priority] issue-YYY: {short description}
   Impact: {low|medium|high}
   Workaround: {brief workaround if available}
   File: docs/issues/unresolved/YYY-description.md

Use `/issue` to manage issues
Use `/issue resolve <id>` to mark as resolved
==============================
```

**Rules:**
- If unresolved issues exist: MUST display this section
- If no unresolved issues: skip this section entirely
- Read actual issue files to get accurate descriptions and workarounds

### Issue Management

> **상세 가이드:** `.claude/ISSUES.md` 참조

**필수 이슈 기록:**
- 빌드 실패 → `/issue add "빌드 에러: ..."`
- 테스트 실패 → `/issue add "테스트 실패: ..."`
- 해결 완료 → `/issue resolve <id>`
