# FunLang Development History

## Current Status

- **Phase**: Phase 8.3 Complete (File-based error tests in progress)
- **Tests**: 473 passed
- **Last Session**: 2026-01-12

## Recent Sessions

### 2026-01-12 09:30 (Session: h6789012)

**주요 변경 사항:**
- Lexer error position 버그 수정 (ParserWrapper.fs)
  - `lexbuf`를 `try` 블록 밖으로 이동 (F# 스코핑 규칙)
  - `LexBuffer.StartPos`가 액션 실행 전에 업데이트되므로 catch에서 사용 가능
- Test file format 표준화 (`// --EXPECTED` 앞뒤로 빈 줄)
- startsession command 업데이트 (FILE_BASED_TESTING.md 읽기)
- docs/FILE_BASED_TESTING.md 문서화 개선 (템플릿, 체크리스트 추가)
- 새 error tests 추가 (12개): 010-015 (lexer), 020-025 (runtime)

**시도한 실험:**
- Lexer error position fix 두 가지 접근법 분석
  - Option 1: Lexer가 에러와 함께 위치 반환 (FsLex 제한으로 불가)
  - Option 2: catch 블록에서 lexbuf.StartPos 사용 (정확한 방법)

**배운 점:**
- F# try-with 스코핑: try 블록 내 변수는 with 핸들러에서 접근 불가
- FsLex LexBuffer: StartPos는 액션 실행 전에 업데이트됨
- 테스트 파일 형식: 가독성을 위해 `// --EXPECTED` 앞뒤로 빈 줄 권장

**Key Decisions:**
- lexbuf를 try 블록 밖에 정의하여 에러 위치 정보 정확하게 캡처
- Test file format 표준: `// --EXPECTED` 앞뒤로 빈 줄 하나씩

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 22:08 (Session: g5678901)

**주요 변경 사항:**
- 세션 시작 및 컨텍스트 복원
- Error message file-based test 강화 작업 계획

**작업 계획 (Todo 생성):**
- Lexer error tests (E001-E004) 추가 예정
- Parser error tests (E101-E106) 추가 예정
- Type error tests (E201-E208) 추가 예정
- Runtime error tests (E301-E304) 추가 예정

---

### 2026-01-11 23:15 (Session: f4567890)

**주요 변경 사항:**
- Let-In Expression 설계 문서 작성 (`docs/design/let-in-expression.md`)

---

### 2026-01-11 21:32 (Session: e3456789)

**주요 변경 사항:**
- Phase 8.3: AST Position Tracking 구현 완료
- Located<'T> wrapper type 추가 (Ast.fs)
- LExpr = Located<Expr>, LPattern = Located<Pattern> 타입 별칭
- Display module: 깔끔한 AST 출력을 위한 Located wrapper 제거
- 테스트: 441 → 455 (+14 LocatedTests)

---

### 2026-01-11 21:23 (Session: c1234567)

**주요 변경 사항:**
- Phase 8.5: Error Explanations 구현 완료
- ErrorExplanations.fs 생성 (22개 에러 코드 설명)
- CLI --explain 옵션 추가
- 18개 새 테스트 추가 (422 → 441)

---

## Accumulated Knowledge

### Lexer Error Position Fix (2026-01-12)
- `lexbuf`는 반드시 `try` 블록 **밖**에 정의해야 함
- F# 스코핑: try 블록 내 변수는 with 핸들러에서 접근 불가
- `LexBuffer.StartPos`는 액션 실행 전에 업데이트됨 → catch에서 사용 가능

### File-Based Testing Format
- 포맷: `// --COMMAND`, `// --INPUT`, `// --EXPECTED`
- **중요**: `// --EXPECTED` 앞뒤로 빈 줄 하나씩 필요
- `%s` 플레이스홀더가 입력 파일 경로로 치환됨

### Phase 8.3: AST Position Tracking
- `Located<'T> = { Node: 'T; Pos: Position }` wrapper type
- `LExpr = Located<Expr>`, `LPattern = Located<Pattern>`
- 합성 노드는 `Located.noLoc` 사용

### Phase 8: Better Error Messages
- Diagnostic.fs: Severity, SourceSpan, LabeledSpan, Suggestion 타입
- ErrorFormatter.fs: Rust-style 에러 출력
- ErrorExplanations.fs: 22개 에러 코드 설명
- Suggestions.fs: Levenshtein distance, findSimilar

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수
- Severity.Error와 Result.Error 이름 충돌 주의

### Phase Completion History
- Phase 0~6: COMPLETE
- Phase 7: Advanced Features - IN PROGRESS
- Phase 8.1~8.5: COMPLETE
