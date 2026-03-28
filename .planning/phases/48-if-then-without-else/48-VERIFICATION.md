---
phase: 48-if-then-without-else
verified: 2026-03-28T00:00:00Z
status: passed
score: 4/4 must-haves verified
---

# Phase 48: If-Then Without Else Verification Report

**Phase Goal:** Users can write `if cond then expr` when the then-branch returns unit, with a clear type error when it does not
**Verified:** 2026-03-28
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can write `if cond then expr` when expr returns unit and it compiles and evaluates correctly | VERIFIED | if-then-unit.flt PASS: `if x > 0 then println "positive"` prints `positive` and returns `()` |
| 2 | User can use `;` sequencing inside an else-free then-branch | VERIFIED | if-then-seq.flt PASS: `if x > 0 then println "a"; println "b"` prints both lines |
| 3 | Writing `if x > 0 then 42` (non-unit then-branch) produces a compile-time type error | VERIFIED | if-then-nonunit-error.flt PASS: produces `error[E0301]: Type mismatch: expected int but got unit` (ExitCode 1) |
| 4 | Existing `if cond then e1 else e2` expressions are unaffected | VERIFIED | 573/573 flt tests pass; if-basic.flt, if-nested.flt, if-paren.flt all PASS |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/LangThree/Parser.fsy` | New `IF Expr THEN SeqExpr` rule desugaring to `If($2, $4, Tuple([], ...), ...)` | VERIFIED | Line 153: rule present immediately after IF-THEN-ELSE rule; 769 lines |
| `src/LangThree/Parser.fs` | Regenerated LALR(1) tables including the new rule | VERIFIED | Line 1080: `If(_2, _4, Tuple([], symSpan parseState 4), ruleSpan parseState 1 4)` |
| `tests/flt/expr/control/if-then-unit.flt` | IFTHEN-01: basic if-then with unit-returning branch | VERIFIED | 8 lines; tests `if x > 0 then println "positive"`; PASS |
| `tests/flt/expr/control/if-then-seq.flt` | IFTHEN-01+SEQ: if-then with semicolon-sequenced then-branch | VERIFIED | 9 lines; tests `if x > 0 then println "a"; println "b"`; PASS |
| `tests/flt/expr/control/if-then-nonunit-error.flt` | IFTHEN-02: non-unit then-branch produces E0301 type error | VERIFIED | 7 lines; tests `if true then 42`; ExitCode 1 with E0301; PASS |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Parser.fsy` grammar rule `IF Expr THEN SeqExpr` | `Ast.If` constructor | `If($2, $4, Tuple([], symSpan parseState 4), ruleSpan parseState 1 4)` | WIRED | Parser.fsy line 153 and Parser.fs line 1080 confirmed |
| `If(cond, intExpr, Tuple([], ...))` | Type unification failure | Bidir/Infer unifies then-type (int) with else-type (unit); E0301 raised | WIRED | if-then-nonunit-error.flt produces E0301 with ExitCode 1 |

### Requirements Coverage

| Requirement | Status | Notes |
|-------------|--------|-------|
| IFTHEN-01: `if cond then unit_expr` compiles and runs | SATISFIED | Truths 1 and 2 verified; if-then-unit.flt and if-then-seq.flt PASS |
| IFTHEN-02: `if cond then non_unit_expr` produces type error | SATISFIED | Truth 3 verified; if-then-nonunit-error.flt PASS with E0301 |

### Anti-Patterns Found

None. No TODO/FIXME/placeholder patterns in any modified files. No stub implementations detected.

### Notable Decision

The error message for IFTHEN-02 is `expected int but got unit` (not `expected unit but got int` as originally stated in the plan). This is correct behavior: the type unifier checks the synthetic unit else-branch against the already-inferred then-branch type (int), so the direction of the mismatch message reflects the unification order. The test was written to match actual compiler output and passes.

### Human Verification Required

None. All behavioral claims verified programmatically via flt integration tests and build output.

## Build Status

`dotnet build src/LangThree/LangThree.fsproj -c Release` — 0 errors, 0 warnings

## Test Results

- `tests/flt/expr/control/` — 6/6 PASS (3 existing + 3 new)
- `tests/flt/` (full suite) — 573/573 PASS

## Isolation Verification

Confirmed no changes to: `Ast.fs`, `Eval.fs`, `Bidir.fs`, `Infer.fs`, `TypeCheck.fs`, `Format.fs`. Phase 48 touched only `Parser.fsy` (modified) and 3 new flt test files — exactly as planned.

---

_Verified: 2026-03-28_
_Verifier: Claude (gsd-verifier)_
