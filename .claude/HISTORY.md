# FunLang Development History

## Current Status

- **Version**: v0.3.0
- **Phase**: Phase 9.3 Complete (Pattern Analysis Integration)
- **Tests**: 581 passed, 0 failed
- **Last Session**: 2026-01-13

## Recent Sessions

### 2026-01-13 14:52 (Session: p5678901)

**주요 변경 사항:**
- Phase 9.3 완료: TypeInfer 통합 & Program.fs 경고 출력
- v0.3.0 릴리스 (패턴 분석 경고 포함)
- PLAN.md에서 알고리즘 문서 분리:
  - `.claude/algorithms/HINDLEY_MILNER.md`
  - `.claude/algorithms/PATTERN_ANALYSIS.md`
- warning-tests/ 디렉토리 추가 (7개 테스트)
- README.md 업데이트:
  - Pattern Matching Analysis 섹션 추가
  - Development 섹션 추가 (version management)
- sorting-algorithms 테스트 재작성 (exhaustive patterns)

**시도한 실험:**
- 패턴 분석에서 TVar 타입 처리 문제 발견
- wildcard fallback으로 exhaustive 패턴 작성
- if-else vs pattern matching 변환

**배운 점:**
- 패턴 분석기가 TVar(미해결 타입 변수) 받으면 infinite domain으로 처리
- List 패턴: `| [] -> ... | _ -> ...` (wildcard fallback)
- Bool 패턴: `| true -> ... | false -> ...` (명시적)
- getConstructors가 TList, TBool 등 처리

**Key Decisions:**
- Phase 9.4 (Guard 지원 개선)는 선택적으로 보류
- sorting-algorithms 테스트를 pattern matching 스타일로 재작성

**Unresolved Issues:**
- (없음)

---

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
- Expecto 필터 옵션 문서화 (CLAUDE.md, HISTORY.md)
- Issue 008 등록: Sorting algorithms integrated test 보류

**배운 점:**
- Expecto `--filter`는 정확한 이름 필요, `--filter-test-list`는 substring 매칭
- 주석 끝 NEWLINE은 indentation 처리에서 선행 NEWLINE로 필터링됨

**Key Decisions:**
- `//` 스타일 한 줄 주석 채택 (F#/Scala 스타일)

---

### 2026-01-12 05:53 (Session: l1234567)

**주요 변경 사항:**
- Multiline type definition 문법 지원 추가 (Parser.fsy)
- 테스트 파일 015, 016을 multiline format으로 변환

**배운 점:**
- Lexer가 `=` 후 줄바꿈 시 NEWLINE 없이 바로 INDENT 생성
- `piped_constructor_list` rule로 leading PIPE 처리

---

### 2026-01-12 05:30 (Session: k0123456)

**주요 변경 사항:**
- Phase 7 상태 확인: 이미 완료되어 있음 확인
- Phase 7 file-based tests 4개 추가
- Phase 8.6 (REPL & Color Integration) 보류로 설정

---

## Accumulated Knowledge

### Pattern Analysis (Phase 9) - 2026-01-13
- Maranget's Usefulness algorithm 구현
- Non-exhaustive: 누락된 패턴 경고
- Redundant: 도달 불가 패턴 경고
- TVar 타입은 infinite domain으로 처리됨
- List exhaustive: `| [] -> ... | _ -> ...`
- Bool exhaustive: `| true -> ... | false -> ...`

### Comment Syntax (2026-01-12)
- 한 줄 주석: `// comment text`
- Lexer.fsl의 `lineComment` 룰로 처리

### Phase 7: Advanced Features (2026-01-12)
- Constructor Application Patterns: `Some x`
- Nested patterns: `Some (Some x)`
- Recursive Types: `type List 'a = Nil | Cons of 'a * List 'a`
- 타입 변수 문법: `'a` (single quote 필수)

### Expecto 테스트 필터 옵션
- `--filter-test-list`: substring 매칭 (권장)
- `--filter-test-case`: 테스트 케이스 이름 매칭
- `--filter`: 정확한 이름 필요 (비권장)

### Build/Parser Tips
- FsLexYacc --module, --unicode 필수
- Parser.fs가 Lexer.fs보다 먼저 컴파일되어야 함

### Common Pitfalls
- FsCheck에서 음수 테스트 시 NonNegativeInt 사용
- Exception 금지: Result/Option으로 에러 전파 필수

### Phase Completion History
- Phase 0~7: COMPLETE
- Phase 8.1~8.5: COMPLETE
- Phase 8.6: ON HOLD (REPL & Color Integration)
- Phase 9.0~9.3: COMPLETE (Pattern Analysis)
- Phase 9.4: PLANNED (Guard 지원 개선)
