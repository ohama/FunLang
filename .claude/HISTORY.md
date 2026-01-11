# FunLang Development History

## Current Status

- **Phase**: Phase 8.5 Design Complete (Phase 8 진행중)
- **Tests**: 422 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 20:45 (Session: b0012345)

**주요 변경 사항:**
- Phase 8.5: Error Explanations 설계 완료
- 설계 문서 작성 및 커밋 (docs/plans/2026-01-11-error-explanations-design.md)

**설계 결정:**
- Inline one-liner + CLI --explain for full docs (Option C)
- 간결한 한 줄 설명이 에러 출력에 자동 포함 (= info: ...)
- `funlang --explain E202`로 상세 문서 확인 가능
- ErrorExplanations.fs에 F# 코드로 저장 (type-safe)

**배운 점:**
- Brainstorming skill로 설계 결정 체계화
- 한 번에 하나의 질문으로 명확한 결정 도출

**Key Decisions:**
- Error explanation storage: F# code (not markdown files)
- Inline format: `= info: {brief}` (one-liner)
- --explain output: title, explanation, bad/good examples, related errors

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 20:15 (Session: a8901234)

**주요 변경 사항:**
- Phase 8.4: Smart Suggestions 커밋 완료
- docs/grammar.md 재정리 (현재 구현 반영)
- docs/funlang.ebnf 생성 (표준 EBNF 문법 명세)

**시도한 작업:**
- 이전 세션에서 작성한 Phase 8.4 코드 커밋
- 문법 문서 현행화 (Pattern matching, User-defined types 등 반영)

**배운 점:**
- EBNF 문서를 별도 파일로 분리하면 가독성 향상
- grammar.md는 개요/예제 중심, funlang.ebnf는 정확한 문법 명세

**Key Decisions:**
- 문법 명세를 두 파일로 분리: grammar.md (혼합), funlang.ebnf (순수 EBNF)

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 19:47 (Session: z7890123)

**주요 변경 사항:**
- Phase 8.4: Smart Suggestions 완료
- Suggestions.fs: Levenshtein distance 알고리즘 구현
- TypeInfer.fs: unbound variable 에러에 suggestions 추가
- Diagnostic.fs: "did you mean?" 헬프 메시지 포맷팅
- 28개 새 테스트 추가 (394 → 422)

**배운 점:**
- Levenshtein distance threshold 2가 적절 (Rust/Elm 스타일)
- Max 3 suggestions으로 제한하여 clutter 방지

**Key Decisions:**
- Unbound variable 에러만 suggestions 지원 (Phase 8.4)
- 거리 2 이하만 제안, 최대 3개 제안

---

### 2026-01-11 16:32 (Session: y6789012)

**주요 변경 사항:**
- File-based error testing 추가 (9개 에러 테스트)
- Diagnostic.fs 버그 수정: lexerMsg의 null 문자 문제 해결
- 테스트: 385 → 394 (+9)

**배운 점:**
- 에러 테스트: exit code 무시하고 출력만 비교
- lexerMsg가 '\000' 문자 사용 시 err.Message 원본 사용

---

### 2026-01-11 16:21 (Session: x5678901)

**주요 변경 사항:**
- Phase 8.2: ErrorFormatter 구현 완료 (Rust-style error output)
- 26개 ErrorFormatter 테스트 추가 (TDD)
- 테스트: 340 → 385 (+45)

**배운 점:**
- `module Diag = FunLang.Diagnostic` 패턴으로 이름 충돌 회피

---

### 2026-01-11 15:18 (Session: u2345678)

**주요 변경 사항:**
- File-based testing framework 구현 (FileBasedTests.fs)
- 테스트: 323 → 340 (+17 file-based tests)

---

## Accumulated Knowledge

### Phase 8: Better Error Messages
- Diagnostic.fs: Severity, SourceSpan, LabeledSpan, Suggestion 타입
- ErrorFormatter.fs: Rust-style 에러 출력 (header, location, source context, footer)
- Suggestions.fs: Levenshtein distance, findSimilar (distance <= 2, max 3)
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
error[E202]: Unbound variable 'prnt'
  --> :1:1
  |
1 | prnt "hello"
  | ^^^^
   = help: did you mean `print`?
```

### Documentation
- docs/grammar.md: 문법 개요, AST 타입, 예제 포함
- docs/funlang.ebnf: 표준 EBNF 문법 명세 (순수 문법만)

### Parser Multiline Handling
- `nl_opt` 규칙으로 optional NEWLINE 처리
- `IN` 앞뒤 모두 `nl_opt` 필요

### Type System Implementation
- Algorithm W: 표현식별 추론 → substitution 합성
- ctorEnv: ThreadLocal<TypeEnv>로 병렬 테스트 안전

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함
- Parser module: FunLang.Parser (not ParserWrapper)

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수
- Severity.Error와 Result.Error 이름 충돌 주의

### Phase Completion History
- Phase 0~6: COMPLETE
- Phase 7: Advanced Features - IN PROGRESS
- Phase 8.1: Diagnostic Type & Builder - COMPLETE
- Phase 8.2: Error Formatter - COMPLETE
- Phase 8.4: Smart Suggestions - COMPLETE
