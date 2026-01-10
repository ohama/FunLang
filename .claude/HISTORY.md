# FunLang Development History

## Current Status

- **Phase**: Phase 4 Complete + Session Management System
- **Tests**: 206 passed
- **Last Session**: 2026-01-10

## Recent Sessions

### 2026-01-10 19:47 (Session: b2c3d4e5)

**주요 변경 사항:**
- Context Handoff 시스템 구현 (HISTORY.md ↔ session commands)
- startsession: HISTORY.md에서 컨텍스트 복원
- endsession: HISTORY.md에 컨텍스트 저장, 리셋 워크플로우 문서화
- HISTORY.md 크기 관리 규칙 추가

**시도한 실험:**
- 사용자 질문 방식 → 자동 판단 방식으로 변경 (startsession, endsession 모두)
- HISTORY.md 구조: Current Status / Recent Sessions / Accumulated Knowledge

**배운 점:**
- AI 컨텍스트는 대화 단위로 관리됨 (새 대화 = 리셋)
- HISTORY.md를 통한 세션 간 핸드오프가 효과적
- 파일 크기 관리 필요: Recent Sessions 5개, 월별 아카이브

**Key Decisions:**
- 컨텍스트 리셋 워크플로우: /endsession → 새 대화 → /startsession
- HISTORY.md 최대 ~500줄, 초과시 docs/history/로 아카이브
- Accumulated Knowledge는 Phase 완료 시 통합/정리

**Unresolved Issues:**
- (없음)

---

### 2026-01-10 19:22 (Session: a1b2c3d4)

**주요 변경 사항:**
- Phase 4: Pattern Matching 완료 (206 tests)
- Issue 관리 시스템 구현 (/issue command, docs/issues/ 저장)
- Session commands 개선 (next steps, issue tracking)
- Context handoff 시스템 구현 (HISTORY.md ↔ startsession/endsession)

**시도한 실험:**
- Issue 저장: 단일 파일 → 두 파일 → 개별 파일 구조로 진화
- Session commands: 사용자 질문 방식 → 자동 판단 방식으로 변경

**배운 점:**
- endsession/startsession에서 사용자 질문 최소화가 UX 개선
- 컨텍스트 핸드오프를 위해 HISTORY.md 활용이 효과적
- 이슈 관리는 개별 파일이 추적/이동에 용이

**Key Decisions:**
- Pattern guard 실패 시 다음 케이스로 이동 (에러 아님)
- 이슈는 docs/issues/에 개별 파일로 저장
- endsession에서 사용자 질문 없이 자동 판단
- startsession에서도 자동 초기화 (사용자 입력 없음)

**Unresolved Issues:**
- (없음)

---

## Accumulated Knowledge

### Session & Context Management
- AI 컨텍스트는 대화 단위로 관리됨
- `/endsession` → 새 대화 시작 → `/startsession` 워크플로우
- HISTORY.md에 세션 간 컨텍스트 핸드오프 저장
- 세션 엔트리: 주요 변경, 시도한 실험, 배운 점, Key Decisions

### HISTORY.md 크기 관리
- Recent Sessions: 최근 5개만 유지 (오래된 것 삭제)
- Accumulated Knowledge: Phase 완료 시 통합/정리
- 아카이브: `docs/history/YYYY-MM.md`로 월별 백업

### Build/Parser Tips
- FsLexYacc --module 플래그로 모듈명 지정 필수
- FsLexYacc --unicode 플래그 필수 (char-based lexing)
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함
- Multiline: nl_opt rule for optional NEWLINE

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수
- Null 입력 처리 필수 (FsCheck가 null 생성 가능)
- "(fun x -> x) -1" → 뺄셈으로 해석됨

### Architecture Decisions
- Post-lexer indentation processing (Python 스타일)
- EBlock이 마지막 표현식 값 반환
- Hybrid syntax: 기존 `let x = 1 in x + 1`도 유지
- 괄호 내 들여쓰기 무시 (Python처럼)

### Phase Completion History
- Phase 0: Infrastructure Setup - COMPLETE
- Phase 1: Core Expressions - COMPLETE
- Phase 1.2: Indentation-Based Syntax - COMPLETE
- Phase 2 + 3: Functions & Data Structures - COMPLETE
- Phase 4: Pattern Matching - COMPLETE
- Phase 5: Type System - PENDING
