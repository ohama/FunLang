---
phase: 25-polymorphic-gadt-return-types
verified: 2026-03-23T01:00:00Z
status: passed
score: 8/8 must-haves verified
re_verification:
  previous_status: gaps_found
  previous_score: 5/8
  gaps_closed:
    - "COV-01: eval : 'a Expr -> 'a — IntLit→int, BoolLit→bool from same function (isPolyExpected fix in Bidir.fs)"
    - "TYP-03: per-branch independent type refinement — cross-type branches now work without annotation"
    - "COV-04: tutorial 14-gadt.md stale disclaimer removed; poly-eval example is accurate"
  gaps_remaining: []
  regressions: []
---

# Phase 25: Polymorphic GADT Return Types Verification Report

**Phase Goal:** OCaml 스타일 다형적 GADT 반환 — synth에서 GADT match를 fresh type variable로 check 위임, 타입 주석에 타입 변수 허용
**Verified:** 2026-03-23T01:00:00Z
**Status:** passed
**Re-verification:** Yes — after gap closure plans 25-04 (isPolyExpected fix) and 25-05 (tutorial accuracy)

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | TYP-01: GADT match에서 타입 변수 주석 허용 `(match e with ... : 'a)` | VERIFIED | `let f e = (match e with \| IntLit n -> n \| BoolLit b -> if b then 1 else 0 : 'a)` — `f (IntLit 42)` outputs 42 |
| 2 | TYP-02: synth 모드에서 GADT match를 fresh type variable로 check 위임 (E0401 없음) | VERIFIED | `let eval e = match e with \| IntLit n -> n` — `eval (IntLit 42)` outputs 42, no E0401 |
| 3 | TYP-03: 분기별 독립적 타입 정제 — IntLit→int, BoolLit→bool from same function | VERIFIED | `let eval e = match e with \| IntLit n -> n \| BoolLit b -> b` — `printf "%d\n" (eval (IntLit 99))` outputs 99; `eval (BoolLit false)` outputs false |
| 4 | TYP-04: 기존 구체적 주석 `(match ... : int)` 호환성 유지 | VERIFIED | `eval (Add (IntLit 10, IntLit 20))` with `: int` annotation outputs 30 |
| 5 | COV-01: `eval : 'a Expr -> 'a` — 정수 입력 시 int, 불리언 입력 시 bool 반환 | VERIFIED | Critical test: `eval (IntLit 42)` + `eval (BoolLit true)` — outputs 42 and true (printf shows 42, last binding shows true) |
| 6 | COV-02: 재귀 GADT 평가기 — `Add (IntLit 10, IntLit 20)` → 30 | VERIFIED | Recursive eval with `: int` annotation outputs 30 |
| 7 | COV-03: 기존 GADT 테스트 전부 통과 (하위 호환) | VERIFIED | 20/20 GADT F# unit tests pass (including new GADT-05 group); 442/442 fslit tests pass; 199/199 total F# unit tests pass |
| 8 | COV-04: 튜토리얼 Ch14 정확성 | VERIFIED | Stale disclaimer removed from line 342; `다형적 반환 타입` section (lines 208-228) accurately shows poly-eval.l3 compiling and outputting 42/true; docs/14-gadt.html rebuilt |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/LangThree/Bidir.fs` | isPolyExpected flag + per-branch independent result type | VERIFIED | `isPolyExpected` added before folder; polymorphic mode: bodyS not composed into cross-branch accumulator; concrete mode unchanged |
| `tests/LangThree.Tests/GadtTests.fs` | GADT-05 tests for cross-type polymorphic return | VERIFIED | 3-test GADT-05 group: type-checks without E0301, IntLit→42, BoolLit→true |
| `tests/flt/file/adt/gadt-poly-eval.flt` | two-branch cross-type eval flt test | VERIFIED | Uses `eval (IntLit 42)` + `eval (BoolLit true)` via printf; expected output 42/true; passes in 442/442 |
| `tests/flt/file/adt/gadt-poly-return.flt` | 'a annotation flt test | VERIFIED | `let f e = (match e with ... : 'a)` with function parameter form; outputs 42 |
| `tutorial/14-gadt.md` | Ch14 with accurate polymorphic return section | VERIFIED | Stale "지원하지 않습니다" disclaimer removed from Haskell comparison (line 342); replaced with accurate description |
| `docs/14-gadt.html` | Rebuilt HTML | VERIFIED | Contains `다형적 반환을 지원합니다` in Haskell comparison section |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `synth Match` | `check` | `isGadtMatch` guard + `freshVar()` | WIRED | `Bidir.fs` — guard fires, creates freshTy, calls check, returns `(s, apply s freshTy)` |
| `check Match` | per-branch independent refinement | `isPolyExpected` flag + `folder` | WIRED | When `apply s1 expected` is unbound TVar: bodyS is branch-local, not composed into cross-branch `s` |
| `check Match` | concrete mode (backward compat) | `isPolyExpected = false` path | WIRED | Concrete annotation path unchanged — `Add (IntLit 10, IntLit 20)` with `: int` still returns 30 |
| Tutorial poly-eval.l3 example | actual runtime behavior | compile + run | WIRED | `eval e = match e with \| IntLit n -> n \| BoolLit b -> b` compiles and outputs 42/true as documented |

### Requirements Coverage

| Requirement | Status | Blocking Issue |
|-------------|--------|----------------|
| synth-mode GADT match no longer raises E0401 | SATISFIED | Verified with TYP-02 |
| GADT match with 'a annotation accepted | SATISFIED | Verified with TYP-01 (function parameter form) |
| per-branch independent type refinement (IntLit→int, BoolLit→bool same function) | SATISFIED | isPolyExpected fix in Bidir.fs; verified with TYP-03 and COV-01 |
| recursive GADT evaluator with concrete annotation | SATISFIED | Verified with COV-02 |
| tutorial Ch14 accurately documents capabilities | SATISFIED | Stale disclaimer removed; poly-eval example verified accurate |

### Anti-Patterns Found

None. Previous anti-patterns (stale disclaimer in tutorial, inaccurate poly-eval.l3 example) have been resolved:

- Tutorial line 342 stale "지원하지 않습니다" disclaimer replaced with accurate description
- `다형적 반환 타입` section poly-eval.l3 example compiles and runs correctly

### Human Verification Required

None. All critical behaviors verified programmatically.

## Re-verification Summary

**Previous status:** gaps_found (5/8, 2026-03-23)

**Gaps closed by plan 25-04 (isPolyExpected fix):**

COV-01 and TYP-03 were both rooted in the same implementation gap: the GADT check-mode folder composed each branch's result-type substitution into a shared accumulator, causing the first branch (`IntLit n -> n : int`) to fix the expected type to `int` before the second branch (`BoolLit b -> b : bool`) could contribute its independent constraint.

Plan 25-04 introduced `isPolyExpected` — a flag set to `true` when `apply s1 expected` is still an unbound type variable after scrutinee synthesis. In polymorphic mode, each branch applies `combinedLocalS` (constructor unification) to the original expected TVar independently; `bodyS` is not composed into the cross-branch accumulator. This allows each branch to refine a fresh copy of the original TVar independently.

**Gaps closed by plan 25-05 (tutorial accuracy):**

COV-04 required removing the single stale disclaimer in the Haskell comparison section (line 342) that said LangThree could not achieve `eval : 'a Expr -> 'a`. The `다형적 반환 타입` section (lines 208-228) and "주석 없음" section (lines 151-182) were already accurate from plan 25-03. The Haskell comparison paragraph was corrected and the mdBook HTML rebuilt.

**No regressions detected:** 199/199 F# unit tests and 442/442 fslit tests pass.

---

*Verified: 2026-03-23T01:00:00Z*
*Verifier: Claude (gsd-verifier)*
