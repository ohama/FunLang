# Roadmap: LangThree v1.3

## Current Milestone: v1.3 Tutorial Documentation

### Phase 13: Tutorial — Core Language (Chapters 1-7) (COMPLETE)

**Goal:** 기본 문법부터 GADT까지, LangThree 핵심 언어 기능을 가르치는 튜토리얼 챕터 작성
**Depends on:** v1.2 (complete)
**Requirements:** TUT-01, TUT-02, TUT-03, TUT-04, TUT-05, TUT-06, TUT-07, QUAL-01, QUAL-02
**Status:** COMPLETE — 7 chapters, 106 CLI-verified examples

Plans:
- [x] 13-01: Chapters 1-4 (Getting Started, Functions, Lists & Tuples, Pattern Matching) — 78 examples
- [x] 13-02: Chapters 5-7 (Algebraic Types, Records, GADTs) — 28 examples

**Success Criteria:**
1. `tutorial/01-getting-started.md` — 실행 방법, 기본 값, 연산자 예제 포함
2. `tutorial/02-functions.md` — lambda, let, let rec, 고차 함수 예제 포함
3. `tutorial/03-lists-and-tuples.md` — 리스트/튜플 생성, 패턴 매칭 예제 포함
4. `tutorial/04-pattern-matching.md` — 모든 패턴 종류, when guard, exhaustiveness 설명
5. `tutorial/05-algebraic-types.md` — ADT 선언/사용, 재귀 타입, Option 패턴
6. `tutorial/06-records.md` — 레코드 CRUD, mutable, 패턴 매칭
7. `tutorial/07-gadt.md` — GADT 선언, 타입 정제, 어노테이션 필수성 설명
8. 모든 코드 예제가 LangThree CLI에서 실행 가능 (QUAL-01)
9. 각 챕터에 최소 5개 실행 가능 예제 (QUAL-02)

---

### Phase 14: Tutorial — Practical Features & Reference (Chapters 8-13)

**Goal:** 모듈, 예외, 파이프, 문자열, Prelude, CLI 레퍼런스까지 튜토리얼 완성
**Depends on:** Phase 13
**Requirements:** TUT-08, TUT-09, TUT-10, TUT-11, TUT-12, TUT-13, QUAL-01, QUAL-02
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 14 to break down)

**Success Criteria:**
1. `tutorial/08-modules.md` — module, namespace, open, qualified access 예제
2. `tutorial/09-exceptions.md` — exception 선언, try-with, when guard 예제
3. `tutorial/10-pipes-and-composition.md` — |>, >>, << 파이프라인 예제
4. `tutorial/11-strings-and-output.md` — string 함수, printf 포맷 출력 예제
5. `tutorial/12-prelude.md` — Option 타입, Prelude 시스템 설명
6. `tutorial/13-cli-reference.md` — 모든 CLI 모드 사용법
7. 모든 코드 예제가 LangThree CLI에서 실행 가능 (QUAL-01)
8. 각 챕터에 최소 5개 실행 가능 예제 (QUAL-02)

---
