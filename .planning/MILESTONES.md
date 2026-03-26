# Project Milestones: LangThree

## v3.0 Mutable Data Structures (Shipped: 2026-03-25)

**Delivered:** Array와 Hashtable 가변 자료구조 추가 — 생성/조회/변이/변환/고차함수 + 한국어 튜토리얼

**Phases completed:** 38-41 (6 plans total)

**Key accomplishments:**
- ArrayValue (mutable fixed-size array) — create/get/set/length/ofList/toList
- HashtableValue (mutable key-value store) — create/get/set/containsKey/keys/remove
- Array HOF builtins (iter/map/fold/init) with callValueRef forward reference pattern
- Korean tutorial chapter (19-mutable-data.md) + flt test suite (486→521 tests)

**Stats:**
- 38 files changed, +3,177 LOC
- ~11,850 lines of F# source
- 4 phases, 6 plans
- 1 day (2026-03-25)
- 224 F# unit tests + 521 flt tests, all passing

**Git range:** `feat(38-01)` → `docs(41): complete`

---

## v2.2 Module Access Fix & Test Coverage (Shipped: 2026-03-25)

**Delivered:** E0313 qualified module access 버그 수정 (임포트 + Prelude), inline try-with 파싱 수정, 누락 flt 테스트 보충

**Phases completed:** 36-37 (3 plans total)

**Key accomplishments:**
- 임포트된 파일 모듈 qualified access 수정 (TypeCheck module map 전파)
- Prelude qualified access 수정 (Prelude 파일 module 래핑 + PreludeResult 확장)
- inline `try failwith "boom" with e -> "caught"` 파싱 수정
- 6개 flt 테스트 추가 (468→474)

**Stats:**
- 224 F# unit tests + 474 flt tests, all passing
- 2 phases, 3 plans
- 1 day (2026-03-25)

**Git range:** `feat(36-01)` → `docs(37): complete`

---

## v2.1 Bug Fixes & Test Hardening (Shipped: 2026-03-25)

**Delivered:** v2.0 런타임 버그 수정 (TCO regression), 테스트 격리 개선, Phase 29-32 flt 테스트 보충, 컴파일 경고 제거

**Phases completed:** 33-35 (5 plans total)

**Key accomplishments:**
- TCO regression 수정: LetRec/LetRecDecl BuiltinValue wrapper의 tailPos=false→true (2줄 변경)
- MatchCompile 전역 카운터 제거 → 로컬 카운터로 테스트 격리 완성
- 21개 flt 테스트 추가 (447→468): char, parser, file import, file I/O
- FS0025 컴파일 경고 20개 전부 제거

**Stats:**
- 37 files changed, 1,744 lines added
- 11,569 lines of F# (from 11,574)
- 3 phases, 5 plans
- 1 day (2026-03-25)
- 214 F# unit tests + 468 flt tests, all passing, 0 warnings

**Git range:** `fix(33-01)` → `docs(35): complete`

---

## v2.0 Practical Language Completion (Shipped: 2026-03-25)

**Delivered:** FunLexYacc 프로젝트에서 발견된 34개 제약사항 전면 해결 — cat/sed 해킹, 접두사 규칙, 26개 등호 체인 등 모든 workaround 제거

**Phases completed:** 26-32 (14 plans total)

**Key accomplishments:**
- `open "path.fun"` 파일 기반 모듈 임포트 시스템 (cycle detection 포함)
- Char 타입 + 문자열/문자 비교 연산자 widening (`<`, `>`, `<=`, `>=`)
- N-tuple (3+ elements) + 모듈 레벨 let-destructuring
- 멀티라인 리스트, trailing semicolons, 리스트 리터럴 패턴 `[x; y; z]`
- 로컬 `let rec` (multi-param), unit 파라미터 shorthand `let f () = body`
- 14개 File I/O + System 빌트인 (read_file, write_file, stdin, get_args, get_env, eprint 등)

**Stats:**
- 60 files modified, 7,781 lines added
- 11,574 lines of F# (from 10,651)
- 7 phases, 14 plans
- 2 days (2026-03-24 → 2026-03-25)
- 214 tests passing

**Git range:** `feat(26-01)` → `docs(32): complete`

---

## v1.8 Polymorphic GADT (Shipped: 2026-03-23)

**Delivered:** OCaml 스타일 다형적 GADT 반환 타입 — `eval : 'a Expr -> 'a` 패턴으로 분기별 다른 타입 반환

**Phases completed:** 25 (5 plans: 3 initial + 2 gap closure)

**Key accomplishments:**
- synth 모드에서 GADT match를 fresh type variable로 check 위임 (E0401 제거)
- isPolyExpected 플래그로 분기별 독립적 결과 타입 (int~bool 충돌 해결)
- `eval (IntLit 42)` → `42` (int), `eval (BoolLit true)` → `true` (bool)
- Tutorial Ch14에 다형적 반환 타입 섹션 + 5개 언어 비교 추가

**Stats:**
- 23 source files, 10,651 lines of F#
- 641 tests (199 F# + 442 fslit), all passing
- 1 phase, 5 plans
- 1 day (2026-03-23)

**Git range:** `feat(25-01)` → `docs(25-05)`

---

## v1.7 F#-Style Offside Rule & List Syntax (Shipped: 2026-03-22)

**Delivered:** F# 스타일 offside rule로 implicit `in` 지원, 리스트 구분자를 세미콜론으로 변경 (`[1; 2; 3]`)

**Phases completed:** 23-24 (4 plans total)

**Key accomplishments:**
- CtxtLetDecl + offside column 기반 implicit `in` 삽입 (LetSeqDepth counter 교체)
- InExprBlock, InModule 컨텍스트로 expression/module 구분
- 리스트 구분자 `,` → `;` (F# 관례 호환): `[1; 2; 3]`
- SemiExprList 규칙 도입, 튜플은 COMMA 유지
- 439 fslit + 196 F# tests, 16 tutorial chapters 전체 업데이트

**Stats:**
- 23 source files, 10,624 lines of F#
- 635 tests (196 F# + 439 fslit), all passing
- 2 phases, 4 plans
- 2 days (2026-03-20 → 2026-03-22)

**Git range:** `feat(23-01)` → `docs(24): complete`

---

## v1.5 User-Defined Operators & Utilities (Shipped: 2026-03-20)

**Delivered:** 사용자 정의 연산자 (INFIXOP0-4), Prelude 유틸리티, sprintf/printfn/%, 음수 패턴, 튜플 람다

**Phases completed:** 19-22 (4 plans total)

**Key accomplishments:**
- 사용자 정의 중위 연산자: `let (++) a b = ...` + `[1,2] ++ [3,4]`
- Prelude 유틸리티: not, min, max, abs, fst, snd, ignore
- Prelude 연산자: `++`, `<|>`, `^^`
- `sprintf "%d" 42` → `"42"`, `printfn`, `%` 모듈로
- 음수 패턴 `| -1 ->`, 튜플 람다 `fun (x,y) ->`

**Stats:**
- 23 source files, 10,304 lines of F#
- 608 tests (196 F# + 412 fslit), all passing
- 4 phases, 4 plans
- 1 day (2026-03-20)

**Git range:** `feat(19-01)` → `feat(22-01)`

---

## v1.4 Language Completion (Shipped: 2026-03-20)

**Delivered:** TCO, or-패턴/문자열 패턴, 타입 별칭, 리스트 범위, 상호 재귀 함수로 언어 완성도 달성

**Phases completed:** 15-18 (6 plans total)

**Key accomplishments:**
- Trampoline TCO — `loop 1000000` stack overflow 없이 동작
- Or-patterns `| 1 | 2 | 3 ->` + 문자열 패턴 `| "hello" ->`
- 타입 별칭 `type Name = string`
- 리스트 범위 `[1..5]`, `[1..2..10]`
- 모듈 레벨 `let rec f = ... and g = ...` 상호 재귀

**Stats:**
- 23 source files, 9,805 lines of F#
- 533 tests (196 F# + 337 fslit), all passing
- 4 phases, 6 plans
- 1 day (2026-03-19 → 2026-03-20)

**Git range:** `feat(15-01)` → `docs(18)`

**What's next:** v1.5 (user-defined operators, type classes)

---

## v1.3 Tutorial Documentation (Shipped: 2026-03-19)

**Delivered:** 함수형 프로그래밍 경험자를 위한 포괄적 LangThree 튜토리얼 13개 챕터, 224개 CLI-verified 예제

**Phases completed:** 13-14 (4 plans total)

**Key accomplishments:**
- 13개 마크다운 튜토리얼 챕터 (기초부터 GADT, 모듈, 예외, 파이프, CLI까지)
- 224개 CLI에서 실제 실행 검증된 코드 예제
- 2,524 lines of tutorial documentation
- Prelude 시스템과 Option 타입 활용법 문서화

**Stats:**
- 13 tutorial files, 2,524 lines of markdown
- 224 CLI-verified examples
- 2 phases, 4 plans
- 1 day (2026-03-19)

**Git range:** `feat(13-01)` → `docs(14-02)`

**What's next:** v1.4 Language Completion (TCO, or-patterns, type aliases, list ranges, mutual recursion)

---

## v1.2 Practical Language Features (Shipped: 2026-03-18)

**Delivered:** 파이프/합성 연산자, unit 타입, 문자열 내장 함수, printf 포맷 출력, Prelude 디렉토리 기반 표준 라이브러리, Option 타입

**Phases completed:** 8-12 (12 plans total)

**Key accomplishments:**
- `|>` 파이프, `>>` `<<` 합성 연산자로 F# 스타일 파이프라인 프로그래밍
- `()` unit 타입과 `let _ =` 부수효과 시퀀싱
- BuiltinValue 인프라로 6개 문자열 내장 함수 (string_length, string_concat, string_sub, string_contains, to_string, string_to_int)
- print/println/printf 포맷 출력 (curried 인자 체인)
- Prelude/*.fun 디렉토리 로딩 + Option 타입 표준 라이브러리
- 168→260 fslit 테스트 확장 (전 기능 커버리지)

**Stats:**
- 23 source files, 9,112 lines of F#
- 456 tests (196 F# + 260 fslit), all passing
- 5 phases, 12 plans
- 8 days from start to ship (2026-03-10 → 2026-03-18)

**Git range:** `test(08-01)` → `fix: handle BuiltinValue in pipe operator`

**What's next:** v1.3 Language Completion (TCO, or-patterns, type aliases, list ranges, mutual recursion)

---

## v1.0 Core Language (Shipped: 2026-03-10)

**Delivered:** FunLang v6.0을 F# 스타일 문법과 현대적 타입 시스템(ADT, GADT, Records, Modules, Exceptions)을 갖춘 실용 함수형 언어로 변환

**Phases completed:** 1-7 (32 plans total)

**Key accomplishments:**
- F# 스타일 들여쓰기 기반 파싱 (offside rule, pipe alignment, multi-line function application)
- 대수적 데이터 타입 (ADT) with Maranget exhaustiveness/redundancy checking
- Generalized ADT (GADT) with bidirectional type refinement and existential types
- Records with mutable fields, copy-update syntax, dot notation access
- Module system with namespaces, qualified names, circular dependency detection
- Exception handling (try-with, when guards, custom exception types)
- Pattern matching compilation to binary decision trees (Jules Jacobs algorithm)

**Stats:**
- 23 source files, 8,362 lines of F#
- 196 tests (2,231 lines), all passing
- 7 phases, 32 plans
- 13 days from start to ship (2026-02-25 → 2026-03-10)

**Git range:** `feat(01-01)` → `test(07-03)`

**What's next:** Project complete (v1.0 milestone fulfills all planned requirements)

---
