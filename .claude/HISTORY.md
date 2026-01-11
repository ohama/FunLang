# FunLang Development History

## Current Status

- **Phase**: Phase 7 Complete, Phase 8.6 On Hold
- **Tests**: 490 passed, 1 failed (sorting test - issue-009)
- **Last Session**: 2026-01-12

## Recent Sessions

### 2026-01-12 06:25 (Session: n3456789)

**주요 변경 사항:**
- Sorting test 파일에서 `in` 키워드 제거 (block-style로 변환)
- issue-009 발견 및 등록: match INDENT after else 문제

**시도한 실험:**
- Sorting test에서 `in` 제거하여 block-style 사용 시도
- `else match xs with` 다음 줄에서 INDENT 토큰 파싱 실패 확인

**배운 점:**
- `match_cases_start` 규칙이 INDENT를 처리하지 않음
- `else` 다음에 `match`가 오고 다음 줄에 cases가 있으면 INDENT 발생

**Key Decisions:**
- issue-009 등록: Parser.fsy에 INDENT 케이스 추가 필요

**Unresolved Issues:**
- issue-008: Sorting test (issue-009로 인해 블록됨)
- issue-009: Match INDENT after else - Parser.fsy 수정 필요

---

### 2026-01-12 06:10 (Session: m2345678)

**주요 변경 사항:**
- 주석 기능 구현 (`// ...` 한 줄 주석)
  - Lexer.fsl에 `lineComment` 룰 추가
  - 7개 주석 테스트 추가
- Expecto 필터 옵션 문서화 (CLAUDE.md, HISTORY.md)
- Issue 008 등록: Sorting algorithms integrated test 보류

**배운 점:**
- Expecto `--filter`는 정확한 이름 필요, `--filter-test-list`는 substring 매칭
- 주석 끝 NEWLINE은 indentation 처리에서 선행 NEWLINE로 필터링됨

**Key Decisions:**
- `//` 스타일 한 줄 주석 채택 (F#/Scala 스타일)
- issue-004 (Won't Fix) 결정 번복, 주석 기능 구현

**Unresolved Issues:**
- Issue 008: Sorting test 파싱 이슈 (주석과 무관, 중첩 let 구문 문제)

---

### 2026-01-12 05:53 (Session: l1234567)

**주요 변경 사항:**
- Multiline type definition 문법 지원 추가 (Parser.fsy)
  - 기존: `type Option 'a = None | Some of 'a`
  - 추가: `type Option 'a = | None | Some of 'a` (INDENT/DEDENT 사용)
- 테스트 파일 015, 016을 multiline format으로 변환

**배운 점:**
- Lexer가 `=` 후 줄바꿈 시 NEWLINE 없이 바로 INDENT 생성
- `piped_constructor_list` rule로 leading PIPE 처리

**Key Decisions:**
- Multiline format은 반드시 leading PIPE 필요 (`| None`)
- Inline format은 기존대로 leading PIPE 없이 사용

**Unresolved Issues:**
- (없음)

---

### 2026-01-12 05:30 (Session: k0123456)

**주요 변경 사항:**
- Phase 7 상태 확인: 이미 완료되어 있음 확인
  - Constructor Application Patterns (`Some x`, `Some (Some x)`) 동작
  - Recursive Types (`List 'a = Nil | Cons of 'a * List 'a`) 동작
- Phase 7 file-based tests 4개 추가:
  - `014-nested-constructor-pattern.test`
  - `015-constructor-pattern-literal.test`
  - `016-list-map.test`
  - `017-tree-type.test`
- Phase 8.6 (REPL & Color Integration) 보류로 설정

**배운 점:**
- Phase 7이 이미 구현되어 있었음 (테스트 479개 모두 통과)
- 타입 변수 문법: `'a` (single quote 필수)

**Key Decisions:**
- Phase 8.6 보류
- Phase 7 완료 확인 후 file-based tests 추가

**Unresolved Issues:**
- (없음)

---

### 2026-01-12 05:10 (Session: j8901234)

**주요 변경 사항:**
- Issue 006 해결: Parser error position이 이제 올바르게 표시됨
- Program.fs에서 `tokenize` + `parse` 대신 `tokenizeWithPositions` + `parseProgramWithPositions` 사용
- `--show-tokens` 출력에 위치 정보 `[line:col]` 추가

**근본 원인 분석:**
- `Program.fs`에서 `tokenize` (위치 정보 없음) + `parse` 사용
- `parse` 함수가 위치 정보 없는 토큰에 `(1,1)` 할당
- 해결: `tokenizeWithPositions` + `parseProgramWithPositions` 사용

**배운 점:**
- Systematic debugging 프로세스가 효과적 (데이터 흐름 추적)
- 디버그 출력으로 각 레이어의 데이터 확인 필수
- Raw lexer는 올바른 위치 반환, 문제는 호출 경로에 있었음

**Key Decisions:**
- 토큰 위치 정보를 출력에 포함 (`[1:5] IDENT "x"` 형식)
- 테스트 파일 업데이트는 별도 Issue 007로 분리

**Unresolved Issues:**
- Issue 007: 15개 테스트 파일 형식 업데이트 필요

---

### 2026-01-12 04:57 (Session: i7890123)

**주요 변경 사항:**
- Parser error tests 6개 추가 (101-106)
- Rich parse error 구현 (Parser.fsy에 RichParseError 예외)
- `tokenTagToName` 함수: 토큰 인덱스를 사람이 읽기 쉬운 이름으로 변환
- `parse_error_rich` 핸들러: 상세한 에러 컨텍스트 캡처
- `processIndentationWithPositions`: 위치 정보 보존하는 indentation 처리
- `tokenizeWithPositions`, `parseProgramWithPositions` 함수 추가

**배운 점:**
- Parser.fs는 Parser.fsy에서 FsYacc가 생성하는 파일 (직접 수정 불가)
- `parse_error_rich`를 Parser.fsy 헤더에 정의하면 생성된 Parser.fs에 포함됨
- 토큰 리스트 기반 파싱에서 위치 추적은 별도 구현 필요

**Key Decisions:**
- RichParseError 예외로 currentToken, expectedTokens, position 전달
- tokenTagToName에서 모든 토큰 태그를 사람이 읽기 쉬운 문자열로 변환

---

### 2026-01-12 09:30 (Session: h6789012)

**주요 변경 사항:**
- Lexer error position 버그 수정 (ParserWrapper.fs)
- Test file format 표준화 (`// --EXPECTED` 앞뒤로 빈 줄)
- 새 error tests 추가 (12개): 010-015 (lexer), 020-025 (runtime)

**배운 점:**
- F# try-with 스코핑: try 블록 내 변수는 with 핸들러에서 접근 불가
- FsLex LexBuffer: StartPos는 액션 실행 전에 업데이트됨

---

## Accumulated Knowledge

### Comment Syntax (2026-01-12)
- 한 줄 주석: `// comment text`
- 주석은 줄 끝까지 모든 문자 무시
- Lexer.fsl의 `lineComment` 룰로 처리
- 특수문자 포함 가능: `// @#$%^&*() OK`

### Phase 7: Advanced Features (2026-01-12)
- Constructor Application Patterns: `Some x` → `PConstructor("Some", Some(PVariable "x"))`
- Nested patterns 지원: `Some (Some x)`
- Recursive Types: `type List 'a = Nil | Cons of 'a * List 'a`
- 타입 변수 문법: `'a` (single quote 필수)
- Multiline type definition: `type T = | A | B` (INDENT 블록 내 leading PIPE)

### Lexer Error Position Fix (2026-01-12)
- `lexbuf`는 반드시 `try` 블록 **밖**에 정의해야 함
- F# 스코핑: try 블록 내 변수는 with 핸들러에서 접근 불가
- `LexBuffer.StartPos`는 액션 실행 전에 업데이트됨 → catch에서 사용 가능

### File-Based Testing Format
- 포맷: `// --COMMAND`, `// --INPUT`, `// --EXPECTED`
- **중요**: `// --EXPECTED` 앞뒤로 빈 줄 하나씩 필요
- `%s` 플레이스홀더가 입력 파일 경로로 치환됨

### Expecto 테스트 필터 옵션 (2026-01-12)
- `--filter`: 정확한 테스트 리스트 이름 필요 (계층 구조, "/" 구분)
- `--filter-test-list`: 테스트 리스트 이름 substring 매칭 (권장)
- `--filter-test-case`: 테스트 케이스 이름 substring 매칭
- **주의**: `--filter "FileBasedTests"` ❌ → `--filter-test-list "File-Based"` ✅
- 테스트 목록 확인: `--list-tests`

### Phase 8: Better Error Messages
- Diagnostic.fs: Severity, SourceSpan, LabeledSpan, Suggestion 타입
- ErrorFormatter.fs: Rust-style 에러 출력
- ErrorExplanations.fs: 22개 에러 코드 설명
- Suggestions.fs: Levenshtein distance, findSimilar
- Rich Parse Error: `RichParseError`, `tokenTagToName`, `parse_error_rich`

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수
- Severity.Error와 Result.Error 이름 충돌 주의

### Phase Completion History
- Phase 0~7: COMPLETE
- Phase 8.1~8.5: COMPLETE
- Phase 8.6: ON HOLD (REPL & Color Integration)
