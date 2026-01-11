# FunLang Development History

## Current Status

- **Phase**: Phase 7 In Progress
- **Tests**: 340 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 15:18 (Session: u2345678)

**주요 변경 사항:**
- File-based testing framework 구현 (FileBasedTests.fs)
- demos/ → tests/file-tests/ 마이그레이션 (새 포맷으로 변환)
- 테스트 서브디렉토리 구조화: lex-tests (7), parse-tests (10), eval-tests (13)
- Default log level을 Fatal로 변경 (로그 출력 기본 비활성화)
- `--show-tokens`/`--show-ast` 조기 종료 기능 구현
- 테스트: 323 → 340 (+17 file-based tests)

**시도한 작업:**
- File-based test format: `// --COMMAND`, `// --INPUT`, `// --EXPECTED`
- Shell 명령어 실행 및 결과 비교
- trimEmptyLines로 앞뒤 공백 라인 무시

**배운 점:**
- File-based test에서 상대 경로 사용하여 중복 테스트명 방지
- `--show-tokens`는 토큰 출력 후 즉시 종료 (파싱/평가 없음)
- `--show-ast`는 AST 출력 후 즉시 종료 (평가 없음)

**Key Decisions:**
- DemoTests.fs 삭제, FileBasedTests.fs로 대체
- 테스트 카테고리별 서브디렉토리 분리

**Unresolved Issues:**
- issue-004: 주석 (`--`) 렉서 미지원

---

### 2026-01-11 14:15 (Session: t0123456)

**주요 변경 사항:**
- (없음 - 짧은 세션)

**시도한 작업:**
- Changelog generator skill 추가 시도 (GitHub에서 SKILL.md 가져오기)
- 사용자에 의해 중단됨

---

### 2026-01-11 14:06 (Session: s9012345)

**주요 변경 사항:**
- SESSION_MANAGEMENT.md 생성: vibe-coding 디렉토리에 세션 관리 가이드 작성

**배운 점:**
- endsession은 저장만 수행 (컨텍스트 리셋 안함)
- clear 또는 새 대화로 컨텍스트 리셋

---

### 2026-01-11 13:54 (Session: r8901234)

**주요 변경 사항:**
- **Issue-005 해결**: Multiline let rec 체인 파싱 에러 수정
- Parser.fsy: `IN` 앞에 `nl_opt` 추가
- 테스트: 320 → 323 (+3)

**Resolved Issues:**
- issue-005: Multiline let rec 체인 파싱 에러 → `nl_opt` 추가로 해결

---

### 2026-01-11 08:15 (Session: q7890123)

**주요 변경 사항:**
- startsession.md 수정: 읽기 전용으로 변경

---

## Accumulated Knowledge

### File-Based Testing
- 포맷: `// --COMMAND`, `// --INPUT`, `// --EXPECTED`
- `%s` 플레이스홀더가 입력 파일 경로로 치환됨
- trimEmptyLines로 앞뒤 공백 라인 무시
- 상대 경로로 테스트명 생성 (중복 방지)

### Parser Multiline Handling
- `nl_opt` 규칙으로 optional NEWLINE 처리
- `IN` 앞뒤 모두 `nl_opt` 필요: `expr nl_opt IN nl_opt expr`
- Indentation processor가 같은 열의 토큰 앞에 NEWLINE 생성

### Session Command Management
- startsession: 읽기 전용 (HISTORY.md, PLAN.md, state.json)
- endsession: 쓰기 수행 (state.json, HISTORY.md, prompt log)
- 워크플로우: 저장(endsession) → 리셋(clear/새대화) → 복원(startsession)

### Type System Implementation
- Algorithm W: 표현식별 추론 → substitution 합성 → 최종 타입
- ctorEnv: ThreadLocal<TypeEnv>로 병렬 테스트 안전
- TypeDefEnvBuilder: 음수 ID (-1, -2, ...) 사용하여 타입 변수 충돌 방지

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수

### Known Parser Limitations
- 주석 (`--`): 렉서에서 미지원

### Phase Completion History
- Phase 0~6: COMPLETE
- Phase 7: Advanced Features - IN PROGRESS (340 tests)
