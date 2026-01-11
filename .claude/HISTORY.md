# FunLang Development History

## Current Status

- **Phase**: Phase 8.3 Design Complete (Phase 8 진행중)
- **Tests**: 441 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 22:30 (Session: d2345678)

**주요 변경 사항:**
- Phase 8.3: AST Position Tracking 설계 완료
- Brainstorming을 통해 설계 결정 도출

**설계 결정:**
- Wrapper type 방식: `Located<'T> = { Node: 'T; Pos: Position }`
- Single position (span이 아닌 시작 위치만)
- Expr + Pattern 모두 위치 추적 (LExpr, LPattern)
- 합성 노드는 `noPos` 사용

**Phase 8.3 Design Summary:**
1. `Located<'T>` wrapper type with `create`, `noLoc`, `map` helpers
2. `LExpr = Located<Expr>`, `LPattern = Located<Pattern>` type aliases
3. Parser: `pos`/`locate` helpers using `parseState.InputStartPosition`
4. Interpreter/TypeInference: unwrap nodes, use `expr.Pos` for errors
5. BlockItem updated to use LExpr
6. Test helpers for creating unlocated expressions

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 21:23 (Session: c1234567)

**주요 변경 사항:**
- Phase 8.5: Error Explanations 구현 완료
- ErrorExplanations.fs 생성 (22개 에러 코드 설명)
- ErrorFormatter.fs에 inline `= info:` 라인 통합
- CLI --explain 옵션 추가
- 18개 새 테스트 추가 (422 → 441)

**Key Decisions:**
- 모든 알려진 에러 코드에 info 라인 표시
- Unknown code는 info 없이 출력

---

### 2026-01-11 20:45 (Session: b0012345)

**주요 변경 사항:**
- Phase 8.5: Error Explanations 설계 완료
- 설계 문서 작성 및 커밋

**Key Decisions:**
- Error explanation storage: F# code (not markdown files)
- Inline format: `= info: {brief}` (one-liner)

---

### 2026-01-11 20:15 (Session: a8901234)

**주요 변경 사항:**
- Phase 8.4: Smart Suggestions 커밋 완료
- docs/grammar.md 재정리, docs/funlang.ebnf 생성

---

### 2026-01-11 19:47 (Session: z7890123)

**주요 변경 사항:**
- Phase 8.4: Smart Suggestions 완료
- Suggestions.fs: Levenshtein distance 알고리즘 구현
- 28개 새 테스트 추가 (394 → 422)

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

### Phase 8.3: AST Position Tracking (설계 완료)
- `Located<'T> = { Node: 'T; Pos: Position }` wrapper type
- `LExpr = Located<Expr>`, `LPattern = Located<Pattern>`
- Parser에서 `parseState.InputStartPosition(1)` 사용
- 합성 노드 (blockToExpr 등)는 `Located.noLoc` 사용
- 패턴 매칭: `match expr.Node with ...`, 위치: `expr.Pos`

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
- Phase 8.3: AST Position Tracking - DESIGN COMPLETE
