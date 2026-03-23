# Roadmap: LangThree v1.8

## Current Milestone: v1.8 Polymorphic GADT

### Phase 25: Polymorphic GADT Return Types (COMPLETE)

**Goal:** OCaml 스타일 다형적 GADT 반환 — synth에서 GADT match를 fresh type variable로 check 위임, 분기별 독립적 타입 정제
**Depends on:** v1.7 (complete)
**Requirements:** TYP-01~04, COV-01~04
**Status:** COMPLETE — `eval : 'a Expr -> 'a` 패턴 동작, 199 F# + 442 fslit tests passing

Plans:
- [x] 25-01: Bidir.fs synth-mode GADT match: E0401 제거, fresh type variable로 check 위임
- [x] 25-02: 다형적 GADT 반환 테스트 3개 신규 추가 (gadt-poly-*.flt)
- [x] 25-03: Tutorial Ch14 업데이트: 다형적 반환 타입 섹션 추가
- [x] 25-04: [GAP] Bidir.fs check-mode per-branch independent result type (isPolyExpected)
- [x] 25-05: [GAP] Tutorial accuracy verification

---
