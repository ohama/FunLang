# Requirements: LangThree v1.3

**Defined:** 2026-03-18
**Core Value:** 함수형 프로그래밍 경험자를 위한 포괄적 LangThree 튜토리얼

## v1.3 Requirements

### Tutorial Chapters (TUT)

- [ ] **TUT-01**: Getting Started — 실행 방법, 기본 값 (int, bool, string), 산술/비교/논리 연산자
- [ ] **TUT-02**: Functions — lambda, let binding, let rec, 다중 인자, 고차 함수, 클로저
- [ ] **TUT-03**: Lists & Tuples — 리스트 생성, cons, 튜플, 패턴 매칭 기초
- [ ] **TUT-04**: Pattern Matching — 상수/와일드카드/중첩 패턴, when guard, decision tree
- [ ] **TUT-05**: Algebraic Data Types — ADT 선언, 파라메트릭, 재귀, 상호 재귀 타입
- [ ] **TUT-06**: Records — 생성, 필드 접근, copy-update, mutable fields, 패턴 매칭
- [ ] **TUT-07**: GADTs — GADT 선언, 타입 정제, 어노테이션, exhaustiveness
- [ ] **TUT-08**: Modules & Namespaces — module, namespace, open, 중첩, qualified access
- [ ] **TUT-09**: Exceptions — exception 선언, raise, try-with, when guard, 중첩
- [ ] **TUT-10**: Pipes & Composition — |>, >>, <<, 파이프라인 프로그래밍
- [ ] **TUT-11**: Strings & Output — string 내장 함수, print, println, printf
- [ ] **TUT-12**: Prelude & Standard Library — Option 타입, Prelude 시스템
- [ ] **TUT-13**: CLI Reference — --expr, 파일 모드, --emit-ast, --emit-type, REPL

### Quality (QUAL)

- [ ] **QUAL-01**: 모든 코드 예제가 실제 LangThree에서 실행 가능
- [ ] **QUAL-02**: 각 챕터에 최소 5개 이상의 실행 가능한 예제 포함

## Out of Scope (v1.3)

| Feature | Reason |
|---------|--------|
| mdBook/HTML 빌드 | 마크다운 원본만, 빌드 시스템은 별도 마일스톤 |
| 한국어 번역 | 영어로 작성 (코드 중심이라 언어 장벽 낮음) |
| API/Internals 문서 | 사용자 튜토리얼만, 구현 문서는 별도 |

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| TUT-01 | 13 | Complete |
| TUT-02 | 13 | Complete |
| TUT-03 | 13 | Complete |
| TUT-04 | 13 | Complete |
| TUT-05 | 13 | Complete |
| TUT-06 | 13 | Complete |
| TUT-07 | 13 | Complete |
| TUT-08 | 14 | Complete |
| TUT-09 | 14 | Complete |
| TUT-10 | 14 | Complete |
| TUT-11 | 14 | Complete |
| TUT-12 | 14 | Complete |
| TUT-13 | 14 | Complete |
| QUAL-01 | 13 | Complete |
| QUAL-02 | 13 | Complete |

**Coverage:**
- v1.3 requirements: 15 total
- Mapped to phases: 15
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-18*
