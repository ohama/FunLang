# Roadmap: LangThree v1.1

## Current Milestone: v1.1 Testing & CLI

### Phase 08: Full Coverage fslit Testing

**Goal:** --emit-ast, --emit-type, fslit을 이용한 LangThree 전체 문법 100% coverage 테스트
**Depends on:** v1.0 (complete)
**Plans:** 5 plans

Plans:
- [x] 08-01-PLAN.md — Expression AST tests (28 files, all passing)
- [x] 08-02-PLAN.md — Expression type tests (16 files, all passing)
- [x] 08-03-PLAN.md — Declaration AST tests (16 files, all passing)
- [x] 08-04-PLAN.md — Declaration type tests (12 files, all passing)
- [x] 08-05-PLAN.md — Pattern AST tests (12 files, all passing)

**Status:** COMPLETE — 168/168 fslit tests passing

**Details:**

모든 문법 구조에 대해 --emit-ast (AST 출력 검증)와 --emit-type (타입 추론 검증) fslit 테스트를 작성하여 파서와 타입 체커의 100% grammar coverage를 달성한다.

**Coverage targets:**
- Expressions: arithmetic, boolean, comparison, if, let, lambda, match, list, tuple, string, annotation
- Declarations: let, let rec, type (ADT, GADT, record), exception, module, namespace, open
- Patterns: variable, wildcard, constructor, tuple, list, cons, record, nested, when guard
- Types: int, bool, string, arrow, tuple, list, parametric, GADT return types

---
