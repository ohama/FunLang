# FunLang Development History

## Current Status

- **Phase**: Phase 8.1-8.2 Complete (Phase 8 진행중)
- **Tests**: 394 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 16:32 (Session: y6789012)

**주요 변경 사항:**
- File-based error testing 추가 (9개 에러 테스트)
- FileBasedTests.fs: non-zero exit code 지원 (에러 테스트용)
- Diagnostic.fs 버그 수정: lexerMsg의 null 문자 문제 해결
- tests/file-tests/error-tests/ 디렉토리 생성
- 테스트: 385 → 394 (+9)

**시도한 작업:**
- 에러 테스트 시 exit code 1로 인한 테스트 실패 문제 해결
- lexerMsg가 '\000' 문자를 사용하여 에러 메시지 손상되는 버그 발견 및 수정

**배운 점:**
- FileBasedTests.executeCommand가 exit code 확인하여 에러 테스트 불가 → exit code 무시하고 출력만 비교하도록 수정
- Diagnostic.fromFunLangError가 err.Message 대신 재구성한 메시지 사용 → null char 문제
- 수정: c = '\000'일 때 원본 err.Message 사용

**Key Decisions:**
- 에러 테스트도 동일한 file-based testing 프레임워크 사용
- exit code와 관계없이 stdout/stderr 출력으로 성공/실패 판단

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 16:21 (Session: x5678901)

**주요 변경 사항:**
- Phase 8.2: ErrorFormatter 구현 완료 (Rust-style error output)
- 26개 ErrorFormatter 테스트 추가 (TDD)
- Program.fs에 ErrorFormatter 통합
- 에러 출력을 stderr로 변경 (eprintfn)
- 테스트: 340 → 385 (+45)

**배운 점:**
- `open FunLang.Diagnostic` 시 Severity.Error가 Result.Error를 가림
- 해결책: `module Diag = FunLang.Diagnostic` 사용

**Key Decisions:**
- 에러 출력을 stderr로 변경 (CLI 표준 관례 준수)

---

### 2026-01-11 17:30 (Session: w4567890)

**주요 변경 사항:**
- Better Error Messages 설계 문서에 Rust 패턴 추가
- Phase 8 구현 계획 확장

---

### 2026-01-11 15:18 (Session: u2345678)

**주요 변경 사항:**
- File-based testing framework 구현 (FileBasedTests.fs)
- 테스트: 323 → 340 (+17 file-based tests)

---

### 2026-01-11 13:54 (Session: r8901234)

**주요 변경 사항:**
- Issue-005 해결: Multiline let rec 체인 파싱 에러 수정

---

## Accumulated Knowledge

### Phase 8: Better Error Messages
- Diagnostic.fs: Severity, SourceSpan, LabeledSpan, Suggestion 타입
- ErrorFormatter.fs: Rust-style 에러 출력 (header, location, source context, footer)
- 에러는 stderr로 출력 (eprintfn 사용)
- `module Diag = FunLang.Diagnostic` 패턴으로 이름 충돌 회피
- lexerMsg 사용 시 c='\000' → err.Message 사용하도록 처리

### File-Based Testing
- 포맷: `// --COMMAND`, `// --INPUT`, `// --EXPECTED`
- `%s` 플레이스홀더가 입력 파일 경로로 치환됨
- 에러 테스트: exit code 무시, 출력만 비교
- 디렉토리: lex-tests, parse-tests, eval-tests, indent-tests, error-tests

### Error Output Format
```
error[E001]: Unexpected character: @
  --> :1:1
  |
1 | @bad
  | ^
```

### Parser Multiline Handling
- `nl_opt` 규칙으로 optional NEWLINE 처리
- `IN` 앞뒤 모두 `nl_opt` 필요

### Type System Implementation
- Algorithm W: 표현식별 추론 → substitution 합성
- ctorEnv: ThreadLocal<TypeEnv>로 병렬 테스트 안전

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
- Phase 8.1: Diagnostic Type & Builder - COMPLETE
- Phase 8.2: Error Formatter - COMPLETE
