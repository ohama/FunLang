# Issue Management Guide

이 문서는 FunLang 프로젝트의 이슈 관리 시스템에 대한 중앙 문서입니다.

## 핵심 원칙

```
빌드/테스트 실패 → 즉시 이슈 기록 → 해결 시 이슈 해결
```

**⚠️ 중요**: 이슈를 기록하지 않으면 같은 문제를 반복할 수 있습니다!

---

## 명령어

| 명령어 | 설명 |
|--------|------|
| `/issue` | 미해결 이슈 목록 보기 |
| `/issue add <desc>` | 새 이슈 추가 |
| `/issue resolve <id>` | 이슈 해결 처리 |
| `/issue show <id>` | 이슈 상세 보기 |
| `/issue unresolved` | 미해결 이슈 전체 (요약) |
| `/issue resolved` | 해결된 이슈 전체 (요약) |
| `/issue all` | 전체 이슈 보기 |

---

## 저장 위치

```
docs/issues/
├── unresolved/          # 미해결 이슈 (이슈당 1파일)
│   └── issue-XXX.md
└── resolved/            # 해결된 이슈 (이슈당 1파일)
    └── issue-XXX.md

.claude/session/state.json  # 세션 상태 (이슈 메타데이터)
```

---

## 이슈 파일 형식

### 미해결 이슈 (`docs/issues/unresolved/issue-XXX.md`)

```markdown
# Issue #XXX: [제목]

**Date**: YYYY-MM-DD
**Status**: Unresolved
**Priority**: high|medium|low
**Component**: [관련 파일/모듈]

## Problem

[문제 설명]

## Error Message (있는 경우)

```
[에러 메시지]
```

## Notes

- [관련 정보]
```

### 해결된 이슈 (`docs/issues/resolved/issue-XXX.md`)

```markdown
# Issue #XXX: [제목]

**Date**: YYYY-MM-DD
**Status**: Resolved
**Priority**: high|medium|low
**Component**: [관련 파일/모듈]

## Problem

[문제 설명]

## Root Cause

[원인 분석]

## Solution

[해결 방법]

## Files Changed

- [변경된 파일]: [변경 내용]

## Verification

[검증 방법/결과]
```

---

## 세션 상태 스키마 (state.json)

```json
{
  "issues": {
    "unresolved": [
      {
        "id": "issue-XXX",
        "createdAt": "ISO timestamp",
        "description": "이슈 설명",
        "context": "관련 파일/함수",
        "priority": "high|medium|low",
        "sessionCreated": "session-id"
      }
    ],
    "resolved": [
      {
        "id": "issue-XXX",
        "createdAt": "ISO timestamp",
        "resolvedAt": "ISO timestamp",
        "description": "이슈 설명",
        "resolution": "해결 방법",
        "sessionCreated": "session-id",
        "sessionResolved": "session-id"
      }
    ],
    "nextId": 1
  }
}
```

---

## 워크플로우

### 1. 이슈 발생 시

```
빌드 실패 → /issue add "빌드 에러: [에러 내용]"
테스트 실패 → /issue add "테스트 실패: [테스트명]"
```

**자동 생성되는 항목:**
- `docs/issues/unresolved/issue-XXX.md` 파일
- `state.json`의 `issues.unresolved` 배열에 추가

### 2. 이슈 해결 시

```
/issue resolve <id>
```

**자동 처리:**
- `docs/issues/unresolved/issue-XXX.md` → `docs/issues/resolved/issue-XXX.md` 이동
- Resolution 정보 추가
- `state.json` 업데이트

### 3. 세션 연동

| 시점 | 동작 |
|------|------|
| `/startsession` | 미해결 이슈 목록 표시 |
| `/endsession` | 세션 중 생성/해결된 이슈 표시, 미기록 이슈 자동 감지 |

---

## 자동 이슈 감지 (endsession)

`/endsession` 실행 시 세션 컨텍스트를 분석하여:

1. **빌드/테스트 실패** 여부 확인
2. **미기록 이슈** 발견 시 자동 생성
3. **해결된 이슈** 자동 감지 및 상태 업데이트

---

## 이슈 ID 규칙

- 형식: `issue-XXX` (3자리 숫자, 예: issue-001)
- 자동 증가: `state.json`의 `issues.nextId` 사용
- 고유성: 프로젝트 전체에서 유일

---

## 우선순위 기준

| 우선순위 | 기준 |
|----------|------|
| **high** | 빌드 불가, 핵심 기능 불가 |
| **medium** | 일부 기능 문제, 성능 이슈 |
| **low** | 개선 사항, 문서화, 리팩토링 |

---

## 관련 문서

- `/issue` 명령어: `.claude/commands/issue.md`
- 세션 관리: `.claude/commands/startsession.md`, `.claude/commands/endsession.md`
- 프로젝트 가이드: `CLAUDE.md`
