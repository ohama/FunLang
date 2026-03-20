# Roadmap: LangThree v1.5

## Current Milestone: v1.5 User-Defined Operators

### Phase 19: User-Defined Operators

**Goal:** 사용자 정의 중위 연산자 — 정의, 우선순위, 사용을 LALR(1) 파서와 호환되게 구현
**Depends on:** v1.4 (complete)
**Requirements:** LEX-01, LEX-02, DEF-01, DEF-02, DEF-03, USE-01, USE-02, USE-03, INTEROP-01
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 19 to break down)

**Success Criteria:**
1. `let (++) a b = append a b` — 사용자 연산자 정의 동작
2. `[1, 2] ++ [3, 4]` — 중위 표기로 사용
3. `(++)` — 연산자를 함수로 사용 가능
4. 우선순위: `a ++ b ** c`에서 `**`가 더 높은 우선순위
5. 기존 연산자 (`|>`, `>>`, `+`, `-` 등) 영향 없음
6. Prelude에서 연산자 정의 가능
7. 기존 테스트 전부 통과

---
