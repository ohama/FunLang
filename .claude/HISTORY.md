# FunLang Development History

## Current Status

- **Phase**: Phase 7 In Progress
- **Tests**: 323 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 14:15 (Session: t0123456)

**주요 변경 사항:**
- (없음 - 짧은 세션)

**시도한 작업:**
- Changelog generator skill 추가 시도 (GitHub에서 SKILL.md 가져오기)
- 사용자에 의해 중단됨

**Unresolved Issues:**
- issue-004: 주석 (`--`) 렉서 미지원

---

### 2026-01-11 14:06 (Session: s9012345)

**주요 변경 사항:**
- SESSION_MANAGEMENT.md 생성: vibe-coding 디렉토리에 세션 관리 가이드 작성
- 세션 워크플로우 문서화: startsession/endsession/clear 순서
- 중간 저장 패턴 문서화: 같은 대화에서 여러 번 endsession 가능

**배운 점:**
- endsession은 저장만 수행 (컨텍스트 리셋 안함)
- clear 또는 새 대화로 컨텍스트 리셋
- 올바른 순서: 저장(endsession) → 리셋(clear) → 복원(startsession)

**Key Decisions:**
- 세션 관리 문서는 FunLang 외부(vibe-coding)에 배치
- 여러 프로젝트에서 공통 참조 가능

**Unresolved Issues:**
- issue-004: 주석 (`--`) 렉서 미지원

---

### 2026-01-11 13:54 (Session: r8901234)

**주요 변경 사항:**
- **Issue-005 해결**: Multiline let rec 체인 파싱 에러 수정
- Parser.fsy: `IN` 앞에 `nl_opt` 추가 (LET...IN, LET REC...IN)
- ParserTests.fs: 3개 회귀 테스트 추가
- 테스트: 320 → 323 (+3)

**배운 점:**
- Indentation processor가 `in` 앞에 NEWLINE 생성
- Parser grammar에서 `IN` 앞에 `nl_opt` 필요
- 토큰 스트림 디버깅으로 문제 원인 빠르게 파악 가능

**Resolved Issues:**
- issue-005: Multiline let rec 체인 파싱 에러 → `nl_opt` 추가로 해결

---

### 2026-01-11 08:15 (Session: q7890123)

**주요 변경 사항:**
- startsession.md 수정: 읽기 전용으로 변경 (파일 쓰기 제거)
- startsession.md에 unresolved issues 읽기/표시 기능 강화

---

### 2026-01-11 07:21 (Session: p6789012)

**주요 변경 사항:**
- Multiline if-then-else 파싱 버그 수정
- demos/013-tree-sort.fun 추가: Tree 'a 타입으로 BST 정렬 구현
- 2개 이슈 발견 및 기록 (issue-004, issue-005)

---

## Accumulated Knowledge

### Parser Multiline Handling
- `nl_opt` 규칙으로 optional NEWLINE 처리
- `IN` 앞뒤 모두 `nl_opt` 필요: `expr nl_opt IN nl_opt expr`
- `ELSE` 앞에도 `nl_opt`: `IF expr THEN nl_opt expr nl_opt ELSE nl_opt expr`
- Indentation processor가 같은 열의 토큰 앞에 NEWLINE 생성

### Session Command Management
- startsession: 읽기 전용 (HISTORY.md, PLAN.md, state.json, issues 파일)
- endsession: 쓰기 수행 (state.json, HISTORY.md, prompt log)
- unresolved issues: docs/issues/unresolved/*.md 파일 직접 읽어서 표시
- 중간 저장 가능: endsession 여러 번 호출해도 컨텍스트 유지
- 워크플로우: 저장(endsession) → 리셋(clear/새대화) → 복원(startsession)

### Type System Implementation
- Algorithm W: 표현식별 추론 → substitution 합성 → 최종 타입
- ctorEnv: ThreadLocal<TypeEnv>로 병렬 테스트 안전
- TypeDefEnvBuilder: 음수 ID (-1, -2, ...) 사용하여 타입 변수 충돌 방지

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함
- Ast.Token 중복 제거 → GeneratedParser.token 사용

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수
- "(fun x -> x) -1" → 뺄셈으로 해석됨

### Known Parser Limitations
- 주석 (`--`): 렉서에서 미지원, DemoTests에서 제거 후 파싱

### Phase Completion History
- Phase 0~6: COMPLETE
- Phase 7: Advanced Features - IN PROGRESS (323 tests)
