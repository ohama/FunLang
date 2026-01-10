# End Session

End the current work session, save context, and prepare for context reset.

**Important**: After `/endsession`, start a **new conversation** to reset context, then use `/startsession` to restore.

## Steps

1. Summarize work completed during this session:
   - Tasks completed
   - Current phase progress
   - Tests written/passed
2. Collect debugging observations:
   - Any issues encountered
   - Debugging commands used
   - Error patterns discovered
3. **Auto-detect issues from session context** (DO NOT ask user):
   - Check if any build failures occurred → should have been recorded as issues
   - Check if any test failures occurred → should have been recorded as issues
   - Check conversation context for unresolved problems
   - Verify issues were properly recorded during the session
4. Update `.claude/session/state.json` with:
   - Current phase progress
   - Work in progress
   - Completed tasks
   - Important notes and context
   - Debugging notes
   - **Issues (unresolved/resolved)**
   - Session end timestamp
5. **Update `.claude/HISTORY.md`** (context handoff for next session)
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

## Context Handoff to HISTORY.md (필수)

세션 종료 시 **다음 세션을 위한 컨텍스트**를 `.claude/HISTORY.md`에 저장합니다.

### 목적

- 세션 간 컨텍스트 연속성 유지
- AI 컨텍스트 리셋 후에도 프로젝트 히스토리 복원
- 중요한 결정/발견/패턴을 영구 기록

### HISTORY.md 구조

```markdown
# FunLang Development History

## Current Status

- **Phase**: Phase 4 Complete
- **Tests**: 206 passed
- **Last Session**: 2026-01-10

## Recent Sessions

### 2026-01-10 (Session: abc123)

**주요 변경 사항:**
- Phase 4: Pattern Matching 완료 (206 tests)
- Issue 관리 시스템 구현

**시도한 실험:**
- Issue 저장: 단일 파일 → 개별 파일 구조로 변경
- Pattern guard: 에러 대신 다음 케이스로 이동

**배운 점:**
- FsLexYacc: --unicode 플래그 필수
- Multiline: nl_opt rule 사용

**Key Decisions:**
- Pattern guard 실패 시 다음 케이스로 이동 (에러 아님)
- 이슈는 개별 파일로 저장 (docs/issues/)

**Unresolved Issues:**
- (없음)

---

### 2026-01-09 (Session: xyz789)

**주요 변경 사항:**
- ...

---

## Accumulated Knowledge

### Build/Parser Tips
- FsLexYacc --module 플래그로 모듈명 지정
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파

### Architecture Decisions
- Post-lexer indentation processing (Python 스타일)
- EBlock이 마지막 표현식 값 반환
```

### 업데이트 규칙

| 섹션 | 업데이트 방식 |
|------|---------------|
| Current Status | 매 세션 **덮어쓰기** |
| Recent Sessions | 새 세션을 **맨 위에 추가** (최근 5개 유지, 초과분 삭제) |
| Accumulated Knowledge | 새 발견 시 **추가**, Phase 완료 시 **통합/정리** |

### 크기 관리 규칙

HISTORY.md가 너무 커지지 않도록 관리:

| 항목 | 제한 | 초과 시 처리 |
|------|------|--------------|
| Recent Sessions | 최대 5개 | 오래된 세션 삭제 |
| Accumulated Knowledge | Phase당 10개 항목 | 통합/요약 |
| 전체 파일 | ~500줄 | 월별 아카이브 |

**월별 아카이브:**
- 파일이 너무 커지면 `docs/history/YYYY-MM.md`로 이동
- HISTORY.md에는 최근 내용만 유지
- 아카이브 예: `docs/history/2026-01.md`

**Accumulated Knowledge 정리 시점:**
1. Phase 완료 시
2. 같은 주제 항목이 5개 이상일 때
3. 중복/구식 정보 발견 시

**정리 방법:**
```
Before:
- FsLexYacc --module 플래그 필요
- FsLexYacc --unicode 플래그 필요
- Parser.fs가 Lexer.fs보다 먼저 컴파일

After (통합):
- FsLexYacc: --module, --unicode 필수; Parser.fs → Lexer.fs 순서
```

### 저장할 컨텍스트

세션에서 다음 정보를 추출하여 기록:

1. **주요 변경 사항**: 이번 세션에서 완료한 작업, 구현한 기능
2. **시도한 실험**: 시도한 접근법, 실패한 방법, 대안 탐색
3. **배운 점**: 세션에서 얻은 인사이트, 발견한 패턴
4. **Key Decisions**: 중요한 설계/구현 결정
5. **Unresolved Issues**: 미해결 이슈 (있다면)

### 구현 방법

```
1. 세션 컨텍스트에서 중요 정보 추출
   ↓
2. .claude/HISTORY.md 읽기 (없으면 생성)
   ↓
3. Current Status 섹션 업데이트
   ↓
4. Recent Sessions에 새 엔트리 추가 (맨 위)
   ↓
5. 오래된 세션 엔트리 정리 (5개 초과 시 삭제)
   ↓
6. Accumulated Knowledge에 새 발견 추가
   ↓
7. 파일 저장
```

### 예시: 세션 엔트리 추가

```markdown
### 2026-01-10 (Session: a1b2c3d4)

**주요 변경 사항:**
- Issue 관리 시스템 구현 (/issue command)
- startsession/endsession에 이슈 표시
- Context handoff 시스템 (HISTORY.md)

**시도한 실험:**
- Issue 저장: 단일 파일 → 개별 파일 구조로 변경
- Session commands: 사용자 질문 → 자동 판단으로 변경

**배운 점:**
- 사용자 질문 최소화가 UX 개선에 효과적
- 개별 파일 저장이 추적/이동에 용이

**Key Decisions:**
- endsession에서 사용자 질문 없이 자동 판단
- 이슈는 docs/issues/에 개별 파일로 저장

**Unresolved Issues:**
- (없음)
```

## Issue Tracking

세션 종료 시 이슈 상태를 **컨텍스트에서 자동 판단**합니다. (사용자에게 질문하지 않음)

**관련 명령어:** `/issue` - 이슈 조회/추가/해결

### 자동 판단 기준

세션 컨텍스트를 분석하여 다음을 확인:

| 확인 사항 | 판단 기준 |
|-----------|-----------|
| 빌드 실패 | 세션 중 `dotnet build` 실패 여부 |
| 테스트 실패 | 세션 중 `dotnet test` 실패 여부 |
| 미해결 문제 | 대화 중 해결되지 않은 에러/버그 언급 |
| 해결된 이슈 | 기존 unresolved 이슈가 해결됨 |

### 자동 처리 로직

```
1. 세션 컨텍스트 분석
   ↓
2. 빌드/테스트 실패가 있었는가?
   - 있었다면: 이슈로 기록되었는지 확인
   - 기록 안됨: state.json에 이슈 추가
   ↓
3. 기존 unresolved 이슈 중 해결된 것?
   - 있다면: resolved로 이동
   ↓
4. Session Summary에 이슈 현황 표시
```

### 이슈 미기록 시 자동 생성

세션 중 실패가 있었지만 이슈가 기록되지 않은 경우, endsession 시점에 자동 생성:

```json
{
  "id": "issue-XXX",
  "description": "[Auto] 빌드/테스트 실패 발생",
  "summary": "세션 중 발생한 미기록 이슈",
  "priority": "medium",
  "context": "endsession auto-detected"
}
```

### 이슈 기록 위치

1. **`.claude/session/state.json`**: 현재 이슈 상태 (실시간)
2. **`docs/issues/unresolved/`**: 미해결 이슈 파일 (이슈당 1파일)
3. **`docs/issues/resolved/`**: 해결된 이슈 파일 (이슈당 1파일)

### ⚠️ 필수 이슈 기록

세션 중 빌드/테스트 실패가 있었다면 반드시 이슈가 기록되어야 합니다:

| 상황 | 필수 행동 |
|------|-----------|
| 빌드 실패 발생 | `/issue add "빌드 에러: ..."` |
| 테스트 실패 발생 | `/issue add "테스트 실패: ..."` |
| 이슈 해결됨 | `/issue resolve <id>` |

**체크**: endsession 전에 모든 실패가 이슈로 기록되었는지 확인!

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

### docs/issues/ 파일 관리

각 이슈는 개별 파일로 저장:

```
docs/issues/
├── unresolved/
│   └── issue-XXX.md   (미해결 이슈)
└── resolved/
    └── issue-XXX.md   (해결된 이슈)
```

**이슈 생성 시**: `docs/issues/unresolved/issue-XXX.md` 파일 생성

```markdown
# issue-005: Parser fails on nested match expressions

- **Status**: unresolved
- **Priority**: high
- **Context**: src/FunLang/Parser.fsy
- **Created**: 2026-01-10 19:00
- **Session**: a1b2c3d4

## Description

{이슈 설명}
```

**이슈 해결 시**:
1. `docs/issues/unresolved/issue-XXX.md` 파일을 `docs/issues/resolved/`로 이동
2. Resolution 정보 추가:

```markdown
# issue-001: Parser conflict with NEWLINE token

- **Status**: resolved
- **Priority**: high
- **Context**: src/FunLang/Parser.fsy
- **Created**: 2026-01-10 18:30
- **Resolved**: 2026-01-10 19:05

## Description

{이슈 설명}

## Resolution

Added nl_opt rule for optional NEWLINE.
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

자동 확인 (사용자 질문 없음):

```
[Auto] All tests pass? (dotnet test)
[Auto] Build succeeds? (dotnet build)
[Auto] Changes committed? (git status)
[Auto] Issues auto-detected from context
[ ] Debugging notes saved?
[ ] Next steps documented?
```

**자동 이슈 감지:**
- 세션 컨텍스트에서 빌드/테스트 실패 여부 확인
- 미기록 이슈 발견 시 자동 생성
- 해결된 이슈 자동 감지 및 상태 업데이트

## Key Files Reference

```
.claude/PLAN.md              - Check phase progress
.claude/HISTORY.md           - Session history & accumulated knowledge
.claude/DEBUGGING.md         - Reference debugging guide
.claude/session/state.json   - Current session state
CLAUDE.md                    - Development guidelines
docs/issues/unresolved/      - Unresolved issue files
docs/issues/resolved/        - Resolved issue files
docs/prompt/                 - Session prompt logs
```

## Context Reset Workflow

```
/endsession
    ↓
[컨텍스트 저장됨]
- HISTORY.md 업데이트
- state.json 저장
- prompt log 저장
    ↓
[새 대화 시작] ← 여기서 컨텍스트 리셋됨
    ↓
/startsession
    ↓
[컨텍스트 복원됨]
- HISTORY.md에서 읽기
- PLAN.md에서 읽기
- state.json에서 읽기
```

**왜 새 대화가 필요한가?**
- AI 컨텍스트는 대화 단위로 관리됨
- 같은 대화 내에서는 이전 내용이 계속 유지됨
- 새 대화 = 깨끗한 컨텍스트 + HISTORY.md에서 복원

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
9. **HISTORY.md updated** (context handoff)
10. **Prompt log file path** (docs/prompt/YYYY-MM-DD_HH-MM.md)
11. **Context reset instruction**: "새 대화를 시작한 후 `/startsession` 실행"
