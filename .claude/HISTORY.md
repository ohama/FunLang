# FunLang Development History

## Current Status

- **Phase**: Phase 8.5 Complete (Phase 8 진행중)
- **Tests**: 441 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 21:23 (Session: c1234567)

**주요 변경 사항:**
- Phase 8.5: Error Explanations 구현 완료
- ErrorExplanations.fs 생성 (22개 에러 코드 설명)
- ErrorFormatter.fs에 inline `= info:` 라인 통합
- CLI --explain 옵션 추가
- 18개 새 테스트 추가 (422 → 441)
- 기존 에러 테스트 기대값 업데이트 (info 라인 추가)

**시도한 작업:**
- TDD 방식으로 ErrorExplanations 구현 (RED → GREEN)
- 설계 문서대로 구현 완료

**배운 점:**
- TDD: 먼저 테스트 작성 후 구현하면 명확한 목표 설정
- 기존 테스트 변경 시 영향 범위 파악 필요

**Key Decisions:**
- 모든 알려진 에러 코드에 info 라인 표시
- Unknown code는 info 없이 출력

**Unresolved Issues:**
- (없음)

---

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

**Key Decisions:**
- 문법 명세를 두 파일로 분리: grammar.md (혼합), funlang.ebnf (순수 EBNF)

---

### 2026-01-11 19:47 (Session: z7890123)

**주요 변경 사항:**
- Phase 8.4: Smart Suggestions 완료
- Suggestions.fs: Levenshtein distance 알고리즘 구현
- 28개 새 테스트 추가 (394 → 422)

**Key Decisions:**
- Unbound variable 에러만 suggestions 지원 (Phase 8.4)
- 거리 2 이하만 제안, 최대 3개 제안

---

### 2026-01-11 16:32 (Session: y6789012)

**주요 변경 사항:**
- File-based error testing 추가 (9개 에러 테스트)
- Diagnostic.fs 버그 수정: lexerMsg의 null 문자 문제 해결
- 테스트: 385 → 394 (+9)

---

## Accumulated Knowledge

### Phase 8: Better Error Messages
- Diagnostic.fs: Severity, SourceSpan, LabeledSpan, Suggestion 타입
- ErrorFormatter.fs: Rust-style 에러 출력 (header, location, source context, footer)
- ErrorExplanations.fs: 22개 에러 코드 설명, getBrief/get/allCodes API
- Suggestions.fs: Levenshtein distance, findSimilar (distance <= 2, max 3)
- 에러는 stderr로 출력 (eprintfn 사용)
- `module Diag = FunLang.Diagnostic` 패턴으로 이름 충돌 회피
- `= info:` 라인으로 inline 에러 설명 표시

### Error Output Format (with info)
```
error[E202]: Unbound variable 'prnt'
  --> :1:1
  |
1 | prnt "hello"
  | ^^^^
   = help: did you mean `print`?
   = info: variables must be defined with 'let' before use
```

### CLI --explain Option
- `funlang --explain E202` - 단일 에러 설명
- `funlang --explain E001,E202` - 여러 에러 설명
- `funlang --explain all` - 모든 에러 코드 목록

### File-Based Testing
- 포맷: `// --COMMAND`, `// --INPUT`, `// --EXPECTED`
- `%s` 플레이스홀더가 입력 파일 경로로 치환됨
- 에러 테스트: exit code 무시, 출력만 비교
- 디렉토리: lex-tests, parse-tests, eval-tests, indent-tests, error-tests

### Documentation
- docs/grammar.md: 문법 개요, AST 타입, 예제 포함
- docs/funlang.ebnf: 표준 EBNF 문법 명세 (순수 문법만)
- docs/plans/2026-01-11-error-explanations-design.md: Phase 8.5 설계

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
- Phase 8.5: Error Explanations - COMPLETE
