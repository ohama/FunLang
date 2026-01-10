# Log Prompts

현재 시점까지의 프롬프트를 기록하는 명령.

## 개요

- `endsession`과 동일한 방식으로 프롬프트 기록
- 세션을 종료하지 않고 중간에 프롬프트 저장
- 이후 `endsession` 또는 다음 `log` 호출 시 이 시점 이후부터 기록

## 사용 시나리오

```
startsession
  ↓
프롬프트 1, 2, 3
  ↓
log          → 프롬프트 1, 2, 3 저장
  ↓
프롬프트 4, 5
  ↓
endsession   → 프롬프트 4, 5만 저장 (log 이후)
```

## 기록 범위

| 시나리오 | 기록 범위 |
|----------|-----------|
| `startsession → log` | startsession 이후 ~ log 까지 |
| `log → log` | 이전 log 이후 ~ 현재 log 까지 |
| `log → endsession` | log 이후 ~ endsession 까지 |
| `endsession → log` | 이전 endsession 이후 ~ log 까지 |

## 파일명 규칙

- **현재 log 시점의 시간 사용**
- 형식: `docs/prompt/YYYY-MM-DD_HH-MM.md`
- 예: `docs/prompt/2026-01-10_18-30.md`

## 문법 교정 (필수)

사용자 프롬프트에 문법 오류가 있을 수 있음. **저장 전 반드시 교정:**

- 오타 수정
- 문법 오류 교정 (한국어/영어)
- 의미는 유지하되 자연스러운 문장으로 변환
- 원본과 교정본 모두 기록

## 저장 형식

```markdown
# Session Prompts: YYYY-MM-DD HH:MM

Period: {이전 log/endsession/startsession 시간} ~ {현재 log 시간}
Type: Intermediate Log

---

## Prompts

### 1.
- **Original:** {원본 프롬프트}
- **Corrected:** {문법 교정된 프롬프트}

### 2.
- **Original:** {원본 프롬프트}
- **Corrected:** {문법 교정된 프롬프트}

...

---

## Summary

- Total prompts: {개수}
- Main topics: {주요 주제들}
- Corrections made: {교정 횟수}
```

## 구현 방법

1. `.claude/session/state.json`에서 `lastPromptLoggedAt` 필드 확인
2. 해당 시점 이후의 모든 사용자 메시지 수집
3. 문법 교정 적용
4. `docs/prompt/YYYY-MM-DD_HH-MM.md` 파일 저장
5. `lastPromptLoggedAt`을 현재 시간으로 업데이트
6. 세션은 계속 유지 (종료하지 않음)

## Session State 업데이트

```json
{
  "lastPromptLoggedAt": "현재 log 실행 시간 (ISO timestamp)"
}
```

## Output

1. 저장된 파일 경로 표시
2. 기록된 프롬프트 개수
3. 세션 계속 진행 안내

```
=== Prompts Logged ===

File: docs/prompt/2026-01-10_18-30.md
Prompts: 5
Period: 14:30 ~ 18:30

Session continues. Use /endsession to end.
========================
```
