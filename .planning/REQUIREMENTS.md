# Requirements: LangThree v2.0

**Defined:** 2026-03-24
**Core Value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Source:** FunLexYacc 프로젝트에서 발견된 24개 제약사항 (langthree-constraints.md)

## v2.0 Requirements

### Module System

- [ ] **MOD-01**: `open "path.fun"` 또는 동등한 import로 외부 .fun 파일 로딩
- [ ] **MOD-02**: 연결된 파일에서 여러 module 선언 허용 (또는 무시 모드)
- [ ] **MOD-03**: 빈 .fun 파일 실행 시 크래시 없이 unit 반환
- [ ] **MOD-04**: Prelude를 LangThree 디렉토리 외부에서도 사용 가능 (경로 기반 로딩)
- [ ] **MOD-05**: 모듈 스코프로 타입 이름 격리 (`Parser.Token` vs `Lexer.Token`)

### Type System

- [ ] **TYPE-01**: N-tuple 지원 (3-tuple 이상: `(a, b, c)`, `(a, b, c, d)`)
- [ ] **TYPE-02**: Let-tuple destructuring (`let (a, b, c) = expr`)
- [ ] **TYPE-03**: `option`을 `Option`의 타입 별칭으로 추가
- [ ] **TYPE-04**: Char 타입 + 문자 리터럴 (`'a'`, `'Z'`)
- [ ] **TYPE-05**: `char_to_int` / `int_to_char` 변환 함수
- [ ] **TYPE-06**: 문자열/문자 비교 연산자 (`<`, `>`, `<=`, `>=`)

### Syntax

- [ ] **SYN-01**: 함수 본문 내 `let rec ... in` (local recursive functions)
- [ ] **SYN-02**: 여러 줄에 걸친 리스트 리터럴 허용
- [ ] **SYN-03**: 리스트 리터럴 끝 trailing semicolon 허용 (`[1; 2; ]`)
- [ ] **SYN-04**: 리스트 리터럴 패턴 (`[x]`, `[x; y]`, `[x; y; z]`)
- [ ] **SYN-05**: `else match` 같은 줄에서 동작
- [ ] **SYN-06**: 깊은 중첩 함수 본문 파싱 개선
- [ ] **SYN-07**: `()` unit 리터럴을 함수 인자로 전달 가능 (`f ()`)
- [ ] **SYN-08**: 모듈 코드 뒤 top-level `let ... in` 표현식 허용

### Standard Library

- [ ] **STD-01**: `failwith "message"` 내장 함수
- [ ] **STD-02**: File I/O — `read_file "path"` 함수
- [ ] **STD-03**: Stdin reading — `stdin_read_all ()` 또는 동등 함수

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
| MOD-01 | — | Pending |
| MOD-02 | — | Pending |
| MOD-03 | — | Pending |
| MOD-04 | — | Pending |
| MOD-05 | — | Pending |
| TYPE-01 | — | Pending |
| TYPE-02 | — | Pending |
| TYPE-03 | — | Pending |
| TYPE-04 | — | Pending |
| TYPE-05 | — | Pending |
| TYPE-06 | — | Pending |
| SYN-01 | — | Pending |
| SYN-02 | — | Pending |
| SYN-03 | — | Pending |
| SYN-04 | — | Pending |
| SYN-05 | — | Pending |
| SYN-06 | — | Pending |
| SYN-07 | — | Pending |
| SYN-08 | — | Pending |
| STD-01 | — | Pending |
| STD-02 | — | Pending |
| STD-03 | — | Pending |

**Coverage:**
- v2.0 requirements: 22 total
- Mapped to phases: 0
- Unmapped: 22 ⚠️

---
*Requirements defined: 2026-03-24*
*Last updated: 2026-03-24 after initial definition*
