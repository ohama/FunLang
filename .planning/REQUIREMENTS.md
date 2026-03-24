# Requirements: LangThree v2.0

**Defined:** 2026-03-24
**Core Value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Source:** FunLexYacc 프로젝트에서 발견된 제약사항 (langthree-constraints.md, updated 2026-03-24)

## v2.0 Requirements

### Module System

- [ ] **MOD-01**: `open "path.fun"` 또는 동등한 import로 외부 .fun 파일 로딩
- [ ] **MOD-02**: 연결된 파일에서 여러 module 선언 허용 (또는 무시 모드)
- [ ] **MOD-03**: 빈 .fun 파일 실행 시 크래시 없이 unit 반환
- [ ] **MOD-04**: Prelude를 LangThree 디렉토리 외부에서도 사용 가능 (경로 기반 로딩)
- [ ] **MOD-05**: 모듈 스코프로 타입 이름 격리 (`Parser.Token` vs `Lexer.Token`)

### Type System

- [x] **TYPE-01**: N-tuple 지원 (3-tuple 이상: `(a, b, c)`, `(a, b, c, d)`)
- [x] **TYPE-02**: Let-tuple destructuring (`let (a, b, c) = expr`)
- [ ] **TYPE-03**: `option`을 `Option`의 타입 별칭으로 추가
- [x] **TYPE-04**: Char 타입 + 문자 리터럴 (`'a'`, `'Z'`)
- [x] **TYPE-05**: `char_to_int` / `int_to_char` 변환 함수
- [x] **TYPE-06**: 문자열/문자 비교 연산자 (`<`, `>`, `<=`, `>=`)

### Syntax

- [x] **SYN-01**: 함수 본문 내 `let rec ... in` (local recursive functions)
- [x] **SYN-02**: 여러 줄에 걸친 리스트 리터럴 허용
- [x] **SYN-03**: 리스트 리터럴 끝 trailing semicolon 허용 (`[1; 2; ]`)
- [x] **SYN-04**: 리스트 리터럴 패턴 (`[x]`, `[x; y]`, `[x; y; z]`)
- [x] **SYN-05**: `else` 뒤 표현식 키워드 동작 (`else match`, `else if`, `else let`, `else try`, `else fun`)
- [x] **SYN-06**: 깊은 중첩 함수 본문 파싱 개선
- [x] **SYN-07**: `()` unit 리터럴을 함수 인자로 전달 가능 (`f ()`)
- [x] **SYN-08**: 모듈 코드 뒤 top-level `let ... in` 표현식 허용

### Standard Library

- [ ] **STD-01**: `failwith "message"` 내장 함수
- [ ] **STD-02**: File I/O — `read_file "path"` 함수
- [ ] **STD-03**: Stdin reading — `stdin_read_all ()` 함수
- [ ] **STD-04**: Stdin line reading — `stdin_read_line ()` 함수
- [ ] **STD-05**: File writing — `write_file "path" "content"` 함수
- [ ] **STD-06**: File appending — `append_file "path" "content"` 함수
- [ ] **STD-07**: File existence check — `file_exists "path"` 함수
- [ ] **STD-08**: File line reading — `read_lines "path"` → `string list`
- [ ] **STD-09**: File line writing — `write_lines "path" lines` 함수
- [ ] **STD-10**: Command-line args — `get_args ()` → `string list`
- [ ] **STD-11**: Environment variables — `get_env "VAR"` → `string`
- [ ] **STD-12**: Current directory — `get_cwd ()` → `string`
- [ ] **STD-13**: Path combining — `path_combine "dir" "file"` → `string`
- [ ] **STD-14**: Directory listing — `dir_files "path"` → `string list`
- [ ] **STD-15**: Stderr output — `eprint` / `eprintln` 함수

## Future Requirements

- Mutable data structures (array, hashtable) — 성능 최적화 필요 시
- Seq expressions (`seq { yield ... }`) — 지연 평가 패턴
- Interpreter performance optimization — JIT/bytecode 컴파일
- `List.hd` / `List.tl` 함수 — 패턴 매칭으로 대체 가능

## Out of Scope

| Feature | Reason |
|---------|--------|
| OCaml functors | F# 스타일 단순 모듈만 — 복잡도 대비 실용성 낮음 |
| Network I/O | 파일 I/O만 이번 마일스톤, 네트워크는 이후 |
| Compiler (native/bytecode) | 인터프리터 유지 |
| IDE / LSP | 언어 기능 완성 후 |
| Row polymorphism for records | 타입 시스템 복잡도 과도 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| MOD-01 | Phase 31 | Pending |
| MOD-02 | Phase 31 | Pending |
| MOD-03 | Phase 26 | Complete |
| MOD-04 | Phase 26 | Complete |
| MOD-05 | Phase 31 | Pending |
| TYPE-01 | Phase 28 | Complete |
| TYPE-02 | Phase 28 | Complete |
| TYPE-03 | Phase 26 | Complete |
| TYPE-04 | Phase 29 | Complete |
| TYPE-05 | Phase 29 | Complete |
| TYPE-06 | Phase 29 | Complete |
| SYN-01 | Phase 30 | Complete |
| SYN-02 | Phase 27 | Complete |
| SYN-03 | Phase 27 | Complete |
| SYN-04 | Phase 27 | Complete |
| SYN-05 | Phase 30 | Complete |
| SYN-06 | Phase 30 | Complete |
| SYN-07 | Phase 30 | Complete |
| SYN-08 | Phase 30 | Complete |
| STD-01 | Phase 26 | Complete |
| STD-02 | Phase 32 | Pending |
| STD-03 | Phase 32 | Pending |
| STD-04 | Phase 32 | Pending |
| STD-05 | Phase 32 | Pending |
| STD-06 | Phase 32 | Pending |
| STD-07 | Phase 32 | Pending |
| STD-08 | Phase 32 | Pending |
| STD-09 | Phase 32 | Pending |
| STD-10 | Phase 32 | Pending |
| STD-11 | Phase 32 | Pending |
| STD-12 | Phase 32 | Pending |
| STD-13 | Phase 32 | Pending |
| STD-14 | Phase 32 | Pending |
| STD-15 | Phase 32 | Pending |

**Coverage:**
- v2.0 requirements: 34 total
- Mapped to phases: 34/34
- Unmapped: 0

---
*Requirements defined: 2026-03-24*
*Last updated: 2026-03-24 (updated from new constraints doc — 12 new STD requirements, SYN-05 scope expanded)*
