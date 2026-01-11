# FunLang Development History

## Current Status

- **Phase**: Phase 7 In Progress (Phase 8 설계 완료)
- **Tests**: 340 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 17:30 (Session: w4567890)

**주요 변경 사항:**
- Better Error Messages 설계 문서에 Rust 패턴 추가
- Rust Diagnostic System 분석 (rustc-dev-guide, annotate-snippets, Ariadne, Miette)
- Phase 8 구현 계획 확장 (8.5 Error Explanations, 8.6 REPL & Color 추가)

**시도한 작업:**
- Rust 진단 시스템 웹 리서치
- rustc-dev-guide, Ariadne, Miette 문서 분석
- 설계 문서 1000줄 규모로 확장

**배운 점:**
- Rust Diagnostic 원칙: 메시지 독립성, Primary Span 자족성
- Suggestion Applicability: MachineApplicable만 자동 적용 가능
- Multi-span diagnostics: 여러 위치 라벨링
- Error Explanations: `rustc --explain E0308` 패턴

**Key Decisions:**
- Diagnostic Builder API 패턴 적용 (fluent interface)
- Primary/Secondary span 구분
- SuggestionApplicability 레벨 (MachineApplicable, HasPlaceholders, MaybeIncorrect, Unspecified)
- Phase 8 구현 순서: 8.1 → 8.2 → 8.4 → 8.5 → 8.6 → 8.3

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 16:45 (Session: v3456789)

**주요 변경 사항:**
- Better Error Messages 설계 문서 작성 완료
- 현재 에러 시스템 상세 분석 (Errors.fs, Types.fs, Program.fs, Interpreter.fs)
- Phase 8 구현 계획 수립

**배운 점:**
- Parser가 `Result<Expr, string>` 반환 → position 정보 손실
- AST에 Position 없음 → 타입/런타임 에러 위치 표시 불가
- `formatErrorWithSource` 존재하지만 호출되지 않음
- FunLangError와 TypeError가 분리되어 있음

**Resolved Issues:**
- issue-004: Comments not supported → Won't Fix (v0.1 설계 결정)

---

### 2026-01-11 15:18 (Session: u2345678)

**주요 변경 사항:**
- File-based testing framework 구현 (FileBasedTests.fs)
- demos/ → tests/file-tests/ 마이그레이션 (새 포맷으로 변환)
- 테스트 서브디렉토리 구조화: lex-tests (7), parse-tests (10), eval-tests (13)
- Default log level을 Fatal로 변경 (로그 출력 기본 비활성화)
- `--show-tokens`/`--show-ast` 조기 종료 기능 구현
- 테스트: 323 → 340 (+17 file-based tests)

---

### 2026-01-11 13:54 (Session: r8901234)

**주요 변경 사항:**
- **Issue-005 해결**: Multiline let rec 체인 파싱 에러 수정
- Parser.fsy: `IN` 앞에 `nl_opt` 추가
- 테스트: 320 → 323 (+3)

---

### 2026-01-11 08:15 (Session: q7890123)

**주요 변경 사항:**
- startsession.md 수정: 읽기 전용으로 변경

---

## Accumulated Knowledge

### Better Error Messages Design (Phase 8)
- 설계 문서: `docs/design/better-error-messages.md` (~1000줄)
- Rust Diagnostic 원칙: 메시지 독립성, Primary Span 자족성, 평이한 언어
- Primary vs Secondary Spans: Primary = 핵심 위치, Secondary = 관련 정보
- Suggestion Applicability: MachineApplicable, HasPlaceholders, MaybeIncorrect, Unspecified
- 구현 순서: 8.1 (Diagnostic) → 8.2 (Formatter) → 8.4 (Suggestions) → 8.5 (Explain) → 8.6 (REPL) → 8.3 (AST)
- 에러 코드 체계: E001-E099 (Lexer), E100-E199 (Parser), E200-E299 (Type), E300-E399 (Runtime)
- `funlang --explain E201` 명령으로 상세 설명 제공

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

### Known Limitations
- 주석 (`--`): 렉서에서 미지원 (Won't Fix)

### Phase Completion History
- Phase 0~6: COMPLETE
- Phase 7: Advanced Features - IN PROGRESS (340 tests)
- Phase 8: Better Error Messages - DESIGN COMPLETE
