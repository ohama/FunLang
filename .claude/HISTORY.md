# FunLang Development History

## Current Status

- **Phase**: Phase 8.3 Complete (Phase 8 진행중)
- **Tests**: 455 passed
- **Last Session**: 2026-01-11

## Recent Sessions

### 2026-01-11 23:15 (Session: f4567890)

**주요 변경 사항:**
- Let-In Expression 설계 문서 작성 (`docs/design/let-in-expression.md`)

**문서 내용:**
- One-line vs Multi-line let 문법 비교
- Parser 구분 방식 (lookahead: `in` vs `NEWLINE/DEDENT`)
- AST 변환 과정 (BlockItem → nested ELet)
- 스코프 규칙 및 다른 언어와 비교
- Best practices

**Key Decisions:**
- 문서화 작업 (구현 변경 없음)

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 21:32 (Session: e3456789)

**주요 변경 사항:**
- Phase 8.3: AST Position Tracking 구현 완료
- Located<'T> wrapper type 추가 (Ast.fs)
- LExpr = Located<Expr>, LPattern = Located<Pattern> 타입 별칭
- Parser.fsy: locate helper로 모든 노드에 위치 정보 부착
- Display module: 깔끔한 AST 출력을 위한 Located wrapper 제거
- 테스트: 441 → 455 (+14 LocatedTests)

**시도한 작업:**
- TDD로 먼저 LocatedTests.fs 작성
- Expr/Pattern 내부 참조를 LExpr/LPattern으로 변경
- 모든 모듈 업데이트 (TypeInfer, Interpreter, ConstructorResolver, ParserWrapper)
- 테스트 파일 업데이트 (ParserTests, TypeTests, UserDefinedTypeTests, SuggestionTests)

**배운 점:**
- F# module 내에서 같은 이름 타입 정의 시 shadowing 발생
- 해결: `type OrigExpr = Expr` alias 먼저 정의 후 새 타입 정의
- Display.ofExpr로 Located wrapper 제거하여 깔끔한 AST 출력

**Key Decisions:**
- Single Position (span이 아닌 시작 위치만)
- 합성 노드는 Located.noLoc 사용 (noPos = { Line = 0; Column = 0; File = None })
- Display module로 --show-ast 출력 호환성 유지

**Unresolved Issues:**
- (없음)

---

### 2026-01-11 22:30 (Session: d2345678)

**주요 변경 사항:**
- Phase 8.3: AST Position Tracking 설계 완료
- Brainstorming을 통해 설계 결정 도출

**설계 결정:**
- Wrapper type 방식: `Located<'T> = { Node: 'T; Pos: Position }`
- Single position (span이 아닌 시작 위치만)
- Expr + Pattern 모두 위치 추적 (LExpr, LPattern)

---

### 2026-01-11 21:23 (Session: c1234567)

**주요 변경 사항:**
- Phase 8.5: Error Explanations 구현 완료
- ErrorExplanations.fs 생성 (22개 에러 코드 설명)
- CLI --explain 옵션 추가
- 18개 새 테스트 추가 (422 → 441)

---

### 2026-01-11 19:47 (Session: z7890123)

**주요 변경 사항:**
- Phase 8.4: Smart Suggestions 완료
- Suggestions.fs: Levenshtein distance 알고리즘 구현
- 28개 새 테스트 추가 (394 → 422)

---

### 2026-01-11 16:21 (Session: x5678901)

**주요 변경 사항:**
- Phase 8.2: ErrorFormatter 구현 완료 (Rust-style error output)
- Program.fs에 ErrorFormatter 통합
- 에러 출력을 stderr로 변경 (eprintfn)

---

## Accumulated Knowledge

### Phase 8.3: AST Position Tracking (완료)
- `Located<'T> = { Node: 'T; Pos: Position }` wrapper type
- `LExpr = Located<Expr>`, `LPattern = Located<Pattern>`
- Parser에서 `parseState.InputStartPosition(1)` 사용
- 합성 노드 (blockToExpr 등)는 `Located.noLoc` 사용
- 패턴 매칭: `match lexpr.Node with ...`, 위치: `lexpr.Pos`
- Display module: `OrigExpr`/`OrigPattern` alias로 타입 shadowing 해결
- `Display.ofExpr`로 --show-ast 출력에서 Located wrapper 제거

### Phase 8: Better Error Messages
- Diagnostic.fs: Severity, SourceSpan, LabeledSpan, Suggestion 타입
- ErrorFormatter.fs: Rust-style 에러 출력 (header, location, source context, footer)
- ErrorExplanations.fs: 22개 에러 코드 설명, getBrief/get/allCodes API
- Suggestions.fs: Levenshtein distance, findSimilar (distance <= 2, max 3)
- 에러는 stderr로 출력 (eprintfn 사용)
- `module Diag = FunLang.Diagnostic` 패턴으로 이름 충돌 회피

### Error Output Format
```
error[E202]: Unbound variable 'prnt'
  --> :1:1
  |
1 | prnt "hello"
  | ^^^^
   = help: did you mean `print`?
   = info: variables must be defined with 'let' before use
```

### File-Based Testing
- 포맷: `// --COMMAND`, `// --INPUT`, `// --EXPECTED`
- `%s` 플레이스홀더가 입력 파일 경로로 치환됨

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수
- Severity.Error와 Result.Error 이름 충돌 주의
- F# module 내 타입 shadowing: OrigType alias 사용

### Phase Completion History
- Phase 0~6: COMPLETE
- Phase 7: Advanced Features - IN PROGRESS
- Phase 8.1: Diagnostic Type & Builder - COMPLETE
- Phase 8.2: Error Formatter - COMPLETE
- Phase 8.3: AST Position Tracking - COMPLETE
- Phase 8.4: Smart Suggestions - COMPLETE
- Phase 8.5: Error Explanations - COMPLETE
