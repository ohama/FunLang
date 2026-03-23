# Roadmap: LangThree v1.8

## Current Milestone: v1.8 Polymorphic GADT

### Phase 25: Polymorphic GADT Return Types

**Goal:** OCaml 스타일 다형적 GADT 반환 — synth에서 GADT match를 fresh type variable로 check 위임, 타입 주석에 타입 변수 허용
**Depends on:** v1.7 (complete)
**Requirements:** TYP-01~04, COV-01~04

Plans:
- [ ] TBD (run /gsd:plan-phase 25 to break down)

**Success Criteria:**
1. `(match e with | IntLit n -> n | BoolLit b -> b : 'a)` — 타입 변수 주석으로 분기별 다른 타입 반환
2. synth 모드에서 GADT match가 E0401 대신 자동으로 check 위임
3. `eval (IntLit 42)` → `42` (int), `eval (BoolLit true)` → `true` (bool) — 다형적 반환
4. `Add (IntLit 10, IntLit 20)` 재귀 평가 동작
5. 기존 `(match ... : int)` 구체적 주석 호환
6. 기존 GADT 테스트 전부 통과

**Key Technical Approach:**
- Bidir.fs synth의 GADT match 분기 (line 273-284): E0401 에러 대신 fresh type variable 생성 후 check로 위임
- Bidir.fs check의 GADT match (line 540-621): `expected`가 타입 변수일 때 localS가 분기별로 정제 — 기존 로직 그대로 동작
- Parser.fsy: 타입 주석 위치에 TEVar 허용
- 테스트 + 튜토리얼 업데이트

---
