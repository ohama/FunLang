# FunLang Development History

## Current Status

- **Phase**: Phase 8.1-8.2 Complete (Phase 8 진행중)
- **Tests**: 385 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 16:21 (Session: x5678901)

**주요 변경 사항:**
- Phase 8.2: ErrorFormatter 구현 완료 (Rust-style error output)
- 26개 ErrorFormatter 테스트 추가 (TDD)
- Program.fs에 ErrorFormatter 통합
- 에러 출력을 stderr로 변경 (eprintfn)
- 테스트: 340 → 385 (+45)

**시도한 작업:**
- ErrorFormatter 모듈 구현 (formatHeader, formatLocation, formatSourceContext, formatFooter)
- Multi-line span 지원 및 line elision 구현
- `open FunLang.Diagnostic` 시 이름 충돌 발생 → module alias로 해결

**배운 점:**
- `open FunLang.Diagnostic` 시 Severity.Error가 Result.Error를 가림
- 해결책: `module Diag = FunLang.Diagnostic` 사용
- 에러는 stderr (eprintfn), 정상 출력은 stdout (printfn)

**Key Decisions:**
- 에러 출력을 stderr로 변경 (CLI 표준 관례 준수)
- Config 타입으로 formatter 설정 캡슐화

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 17:30 (Session: w4567890)

**주요 변경 사항:**
- Better Error Messages 설계 문서에 Rust 패턴 추가
- Rust Diagnostic System 분석 (rustc-dev-guide, annotate-snippets, Ariadne, Miette)
- Phase 8 구현 계획 확장 (8.5 Error Explanations, 8.6 REPL & Color 추가)

**배운 점:**
- Rust Diagnostic 원칙: 메시지 독립성, Primary Span 자족성
- Suggestion Applicability: MachineApplicable만 자동 적용 가능

**Key Decisions:**
- Phase 8 구현 순서: 8.1 → 8.2 → 8.4 → 8.5 → 8.6 → 8.3

---

### 2026-01-11 16:45 (Session: v3456789)

**주요 변경 사항:**
- Better Error Messages 설계 문서 작성 완료
- 현재 에러 시스템 상세 분석

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

### Error Output Format
```
error[E201]: Type mismatch
  --> file.fun:3:15
  |
3 | let x = "hello" + 1
  | ^^^^^^^ expected `int`, found `string`
  |
  = note: `+` requires int operands
  = help: use `++` for string concatenation
```

### File-Based Testing
- 포맷: `// --COMMAND`, `// --INPUT`, `// --EXPECTED`
- `%s` 플레이스홀더가 입력 파일 경로로 치환됨

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
