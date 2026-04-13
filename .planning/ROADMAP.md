# Roadmap: v15.0 Type Class Maturity

**Status:** Active
**Phases:** 96–103 (8 phases)
**Coverage:** 17/17 requirements mapped + 2 bug fix phases

## Overview

v15.0 matures FunLang's type class system by fixing correctness bugs in the existing infrastructure, adding superclass entailment, repairing runtime dispatch for constrained instances, introducing the Num type class, improving derive for parameterized ADTs, and polishing error messages. All 724 flt tests must pass at every phase boundary.

---

## Phases

### Phase 96: Correctness Foundations

**Goal:** The existing type class infrastructure is reliable — no silent coherence violations, no constraint leaks, no broken recursive dispatch.

**Dependencies:** None (must come first — bugs corrupt all subsequent work)

**Requirements:** TC-01, TC-02, TC-03, TC-04

**Success Criteria:**

1. Declaring `Show 'a => Show ('a list)` twice produces an E0702 duplicate instance error — the alpha-equivalence check normalizes TVar IDs before comparison.
2. After type-checking an instance method body, no constraints from that body appear on the next unrelated let-binding (constraint leak eliminated).
3. An instance method body that calls the same class's method recursively (e.g., `show` calling `show` on a list tail) type-checks and evaluates correctly without E0701.
4. E0701 "no instance" errors display normalized type variable names (e.g., `'a`) rather than internal TVar IDs (e.g., `'t42`).

---

### Phase 97: Superclass Entailment

**Goal:** Users can declare superclass constraints on type classes, and the type checker automatically satisfies superclass constraints via entailment.

**Dependencies:** Phase 96 (correctness foundations must be solid before extending resolveConstraint)

**Requirements:** SC-01, SC-02, SC-03

**Success Criteria:**

1. `ClassInfo` carries a `Superclasses: string list` field, populated when a `typeclass Eq 'a => Ord 'a` declaration is processed.
2. When `Ord int` is in scope, `resolveConstraint` for `Eq int` succeeds via superclass entailment without requiring an explicit `instance Eq int` declaration at the call site.
3. Declaring `instance Ord int` when `instance Eq int` does not exist produces a declaration-time error, not a silent acceptance followed by a confusing call-site error.
4. `Prelude/Typeclass.fun` contains `typeclass Eq 'a => Ord 'a` with `compare` as its method, and all existing 724 flt tests continue to pass.

---

### Phase 98: Constrained Instance Runtime Dispatch

**Goal:** Calling a class method on a value whose type matches a constrained instance (e.g., `show [1; 2; 3]`) invokes the correct constrained implementation at runtime.

**Dependencies:** Phase 97 (Prelude instances added here use superclass constraints that require Phase 97 infrastructure)

**Requirements:** CD-01, CD-02

**Success Criteria:**

1. `show [1; 2; 3]` evaluates to `"[1; 2; 3]"` — the `Show ('a list)` constrained instance is selected at runtime, not a monomorphic fallback.
2. Monomorphic instances (`show 42`, `show true`) continue to dispatch to their literal-named methods with no behavior change — name mangling applies only to instances with non-empty `InstanceVars`.
3. All 724 existing flt tests pass after mangling is introduced (backward-compatibility invariant).
4. A new flt test for `show` on nested lists (`show [[1;2];[3;4]]`) passes, demonstrating recursive constrained dispatch.

---

### Phase 99: Num Type Class

**Goal:** Users can define numeric type class instances and use generic arithmetic methods for user-defined numeric types; `Eq ('a list)` constrained instance works in the Prelude.

**Dependencies:** Phase 98 (constrained instances added here must dispatch correctly at runtime)

**Requirements:** NUM-01, NUM-02, NUM-03

**Success Criteria:**

1. `typeclass Num 'a` with methods `add`, `sub`, `mul` is declared in `Prelude/Typeclass.fun` and the class is accessible to user programs.
2. `instance Num int` is registered so user code can call `Num.add 3 4` (or the unqualified form) and receive `7`.
3. The built-in `+`, `-`, `*` operators continue to use their existing `Add`/`Subtract`/`Multiply` AST dispatch — no operator migration occurs in this phase.
4. `instance Eq 'a => Eq ('a list)` is registered and `(=) [1;2;3] [1;2;3]` evaluates to `true` using the constrained instance.

---

### Phase 100: Derive for Parameterized ADTs

**Goal:** `deriving Show` and `deriving Eq` work correctly on parameterized ADTs, generating constrained instances with the right type variables and constraints.

**Dependencies:** Phase 98 (generated constrained instances must dispatch correctly), Phase 97 (generated instances may carry superclass constraints)

**Requirements:** DRV-01, DRV-02

**Success Criteria:**

1. `type Tree 'a = Leaf | Node of 'a * Tree 'a * Tree 'a` followed by `deriving Show Tree` generates a `Show 'a => Show (Tree 'a)` instance with `InstanceVars = ["'a"]` and `InstanceConstraints = [Show 'a]`.
2. `show (Node (1, Leaf, Leaf))` evaluates to the expected string representation using the derived instance.
3. A recursive ADT (`Tree 'a` containing `Tree 'a` fields) derives correctly and the show method handles recursive values without a runtime crash.
4. `deriving Eq` on a type containing function-typed fields produces a clear compile-time error rather than a runtime crash.

---

### Phase 101: Error Message Polish

**Goal:** Type class error messages are actionable — they identify the missing subgoal, fire the right error code, and show constraint chain context.

**Dependencies:** Phase 96 (formatTypeNormalized already in place), Phase 99 (Num/Eq instances exist to exercise hints)

**Requirements:** ERR-01, ERR-02, ERR-03

**Success Criteria:**

1. When `show (myRecord)` fails because `Show MyRecord` is missing, E0701 displays "No instance of Show for MyRecord" (the actual missing element-type instance), not "No instance of Show for MyRecord list".
2. Passing a value with a mismatched method type in an `instance` declaration fires E0704 (not E0301).
3. A type error that originates from a constraint on a called function displays the constraint chain context — e.g., "required by call to `show` which requires `Show 'a`".

### Phase 102: Fix LambdaAnnot Span Collision (Issue #18)

**Goal:** Each nested LambdaAnnot node gets a unique span so annotationMap lookups return the correct arrow type for each parameter.

**Dependencies:** Phase 101

**Plans:** 2 plans

Plans:
- [x] 102-01-PLAN.md — Per-parameter span assignment in Parser.fsy (desugarAnnotParams, desugarMixedParams, all callsites)
- [x] 102-02-PLAN.md — Unit test (TA-08) and flt regression tests for span uniqueness

**Details:**
`desugarAnnotParams` generates nested LambdaAnnot nodes sharing the same span, causing annotationMap collision. Inner parameters receive the outermost arrow type instead of their own. Fix by assigning each LambdaAnnot a unique span based on the parameter's source position.

### Phase 103: Fix Bidir.fs annotationMap Population for LambdaAnnot Spans (Issue #19)

**Goal:** Type checker (Bidir.fs) records arrow type in annotationMap using each LambdaAnnot's own per-parameter span, so FunLangCompiler lookups return the correct type instead of None.

**Dependencies:** Phase 102

**Plans:** 2 plans

Plans:
- [x] 103-01-PLAN.md — Extend LetRec/LetRecDecl 5-tuple to 6-tuple, update Parser.fsy + all pattern matches, add recordTy calls
- [x] 103-02-PLAN.md — TA-09 unit test + flt regression test for let rec first-param annotationMap

**Details:**
Phase 102 fixed the parser to assign unique spans per LambdaAnnot, but Bidir.fs does not use those spans when populating annotationMap. All inner LambdaAnnot entries return None on lookup. Fix Bidir.fs to record arrow types keyed by each LambdaAnnot node's own span.

### Phase 104: Remove DuplicateRecordField(E0311) Check (Issue #21)

**Goal:** [To be planned]
**Depends on:** Phase 103
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd:plan-phase 104 to break down)

**Details:**
`TypeCheck.fs`의 `checkDuplicateRecordFields` (L590-604)가 서로 다른 record 타입에서 동일 필드명을 사용하는 경우를 E0311 에러로 처리. ML 계열(OCaml, F#)의 정상 패턴이며, 이 에러로 타입 체크가 중단되어 Issue #20의 FieldAccess TData 기록이 무효화됨. FunLangCompiler#24 (가비지 값) 해결의 블로커. E0311 제거 또는 경고로 강등하여 동일 필드명 record 타입들을 허용.

---

## Progress

| Phase | Goal | Requirements | Status |
|-------|------|--------------|--------|
| 96 — Correctness Foundations | Reliable type class infrastructure | TC-01, TC-02, TC-03, TC-04 | Pending |
| 97 — Superclass Entailment | Superclass chain resolution | SC-01, SC-02, SC-03 | Pending |
| 98 — Constrained Instance Runtime Dispatch | Constrained instances evaluate correctly | CD-01, CD-02 | Pending |
| 99 — Num Type Class | Num/Eq Prelude additions | NUM-01, NUM-02, NUM-03 | Pending |
| 100 — Derive for Parameterized ADTs | derive works on `Tree 'a` etc. | DRV-01, DRV-02 | Pending |
| 101 — Error Message Polish | Actionable type class errors | ERR-01, ERR-02, ERR-03 | Pending |
| 102 — Fix LambdaAnnot Span Collision | Unique spans for nested LambdaAnnot (Issue #18) | — | ✓ Complete |
| 103 — Fix Bidir.fs annotationMap for LambdaAnnot | annotationMap populated with per-param span (Issue #19) | — | ✓ Complete |
| 104 — Remove DuplicateRecordField(E0311) Check | Allow same field name across record types (Issue #21) | — | ✓ Complete |

**Coverage:** 17/17 requirements mapped. No orphans.

---

## Coverage Map

| Requirement | Phase |
|-------------|-------|
| TC-01 | 96 |
| TC-02 | 96 |
| TC-03 | 96 |
| TC-04 | 96 |
| SC-01 | 97 |
| SC-02 | 97 |
| SC-03 | 97 |
| CD-01 | 98 |
| CD-02 | 98 |
| NUM-01 | 99 |
| NUM-02 | 99 |
| NUM-03 | 99 |
| DRV-01 | 100 |
| DRV-02 | 100 |
| ERR-01 | 101 |
| ERR-02 | 101 |
| ERR-03 | 101 |

---

## Key Constraints

- Phase 96 must precede all others — existing bugs corrupt the instance environment and confound every subsequent phase.
- Phase 98 (name mangling) is highest-risk — monomorphic instances must preserve literal method names; only `InstanceVars != []` instances get mangled names.
- Phase 99 must NOT migrate `+`/`-`/`*`/`=` operator dispatch through the type class system — `Add`/`Subtract`/`Multiply`/`Equal` AST nodes remain hardcoded.
- 724 flt tests must pass at every phase boundary before the phase is considered complete.

---

*Roadmap created: 2026-04-09 — v15.0 Type Class Maturity*
*Phases continue from v14.0 Phase 95*
*Phase 102 added: 2026-04-10 — Fix LambdaAnnot span collision (Issue #18)*
*Phase 103 added: 2026-04-10 — Fix Bidir.fs annotationMap population for LambdaAnnot spans (Issue #19)*
*Phase 104 added: 2026-04-13 — Remove DuplicateRecordField(E0311) check to allow same field name across record types (Issue #21)*
