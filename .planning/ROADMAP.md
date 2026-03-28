# Roadmap: LangThree v6.0 Practical Programming

## Milestones

- ✅ **v1.0 Core Language** — Phases 1-7 (shipped 2026-03-10)
- ✅ **v1.1 File-Based Testing** — Phases 8 (shipped 2026-03-10)
- ✅ **v1.2 Practical Features** — Phases 9-12 (shipped 2026-03-18)
- ✅ **v1.3 Tutorial Documentation** — Phases 13-14 (shipped 2026-03-19)
- ✅ **v1.4 Language Completion** — Phases 15-18 (shipped 2026-03-20)
- ✅ **v1.5 User-Defined Operators** — Phases 19-22 (shipped 2026-03-20)
- ✅ **v1.6/v1.7 Offside Rule & List Syntax** — Phases 23-24 (shipped 2026-03-22)
- ✅ **v1.8 Polymorphic GADT** — Phase 25 (shipped 2026-03-23)
- ✅ **v2.0 Practical Language Completion** — Phases 26-32 (shipped 2026-03-25)
- ✅ **v2.1 Bug Fixes & Test Hardening** — Phases 33-35 (shipped 2026-03-25)
- ✅ **v2.2 Module Access Fix** — Phases 36-37 (shipped 2026-03-25)
- ✅ **v3.0 Mutable Data Structures** — Phases 38-41 (shipped 2026-03-25)
- ✅ **v4.0 Mutable Variables** — Phases 42-44 (shipped 2026-03-28)
- ✅ **v5.0 Imperative Ergonomics** — Phases 45-49 (shipped 2026-03-28)
- 🚧 **v6.0 Practical Programming** — Phases 50-53 (in progress)

## Phases

<details>
<summary>✅ v1.0 through v5.0 (Phases 1-49) - SHIPPED 2026-03-28</summary>

See MILESTONES.md for full history of completed phases.

</details>

### 🚧 v6.0 Practical Programming (In Progress)

**Milestone Goal:** 뉴라인 암묵적 시퀀싱, 컬렉션 for-in 루프, Option/Result 유틸리티로 실용적 프로그래밍 완성

- [ ] **Phase 50: Newline Implicit Sequencing** — IndentFilter auto-inserts SEMICOLON at same-indent newlines inside expression blocks
- [ ] **Phase 51: For-In Collection Loops** — `for x in collection do body` iterates lists and arrays
- [ ] **Phase 52: Option/Result Prelude Utilities** — map, bind, defaultValue, iter, filter and Result bridging functions in Prelude
- [ ] **Phase 53: Tests and Documentation** — flt integration tests + tutorial chapter 22 for all v6.0 features

---

#### Phase 50: Newline Implicit Sequencing

**Goal:** Users can write multi-line expression blocks without explicit semicolons — newlines at the same indent level automatically sequence expressions
**Depends on:** Phase 49 (v5.0 complete)
**Requirements:** NLSEQ-01, NLSEQ-02, NLSEQ-03, NLSEQ-04, NLSEQ-05
**Success Criteria** (what must be TRUE):
  1. A function body with multiple statements on separate lines at the same indent executes all statements in order without explicit `;`
  2. Multi-line function application (`f x` followed by an indented argument) is NOT split into two separate expressions
  3. Structural keywords (`else`, `with`, `|`, `then`) appearing after an expression are not preceded by a spurious SEMICOLON
  4. Explicit `;` sequencing written by users continues to parse and execute correctly
  5. Module-level `let` declarations are not sequenced — top-level bindings remain independent
**Plans:** TBD

Plans:
- [ ] 50-01: IndentFilter SEMICOLON injection for same-level newlines in InExprBlock contexts

---

#### Phase 51: For-In Collection Loops

**Goal:** Users can iterate directly over lists and arrays using `for x in collection do body` without calling List.iter
**Depends on:** Phase 50
**Requirements:** FORIN-01, FORIN-02, FORIN-03, FORIN-04
**Success Criteria** (what must be TRUE):
  1. `for x in [1; 2; 3] do body` executes body once per element with x bound to each element in order
  2. `for x in arr do body` iterates over all elements of a mutable array
  3. Assigning to the loop variable `x <- newValue` inside the body produces E0320 (immutable assignment error)
  4. `for x in [] do body` and `for x in empty_array do body` execute body zero times and return unit
**Plans:** TBD

Plans:
- [ ] 51-01: ForInExpr AST node, parser rules, type checker, evaluator, and passthrough files

---

#### Phase 52: Option/Result Prelude Utilities

**Goal:** Users have a complete set of Option and Result utility functions in Prelude covering map, bind, defaultValue, iter, filter, and Result-to-Option bridging
**Depends on:** Phase 50
**Requirements:** OPTRES-01, OPTRES-02, OPTRES-03, OPTRES-04
**Success Criteria** (what must be TRUE):
  1. `optionMap`, `optionBind`, `optionDefaultValue` transform and chain Option values without manual pattern matching
  2. `optionIter`, `optionFilter`, `optionIsSome`, `optionIsNone` cover side-effect and predicate use cases
  3. `resultMap`, `resultBind`, `resultMapError` transform Result values in all three slots
  4. `resultDefaultValue` and `resultToOption` convert Results to simpler types for downstream use
**Plans:** TBD

Plans:
- [ ] 52-01: Prelude/Option.fun and Prelude/Result.fun utility functions

---

#### Phase 53: Tests and Documentation

**Goal:** Every v6.0 feature has flt integration test coverage and tutorial chapter 22 explains all three features with working examples
**Depends on:** Phases 50, 51, 52
**Requirements:** TST-33, TST-34, TST-35, TST-36
**Success Criteria** (what must be TRUE):
  1. flt tests for newline sequencing cover both positive cases (multi-line bodies work) and regression cases (multi-line application, structural terminators, module-level not affected)
  2. flt tests for for-in loops cover list iteration, array iteration, immutable loop variable, and empty collection
  3. flt tests for Option/Result utilities cover all eight new functions with both Some/None and Ok/Error inputs
  4. Tutorial chapter 22 (22-practical-programming.md) is present with runnable examples for all three v6.0 features
**Plans:** TBD

Plans:
- [ ] 53-01: flt tests for newline sequencing, for-in loops, and Option/Result utilities
- [ ] 53-02: Tutorial chapter 22 — Practical Programming

---

## Progress

**Execution Order:** 50 → 51 → 52 → 53

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1-49. Prior milestones | v1.0-v5.0 | 108/108 | Complete | 2026-03-28 |
| 50. Newline Implicit Sequencing | v6.0 | 0/TBD | Not started | - |
| 51. For-In Collection Loops | v6.0 | 0/TBD | Not started | - |
| 52. Option/Result Prelude Utilities | v6.0 | 0/TBD | Not started | - |
| 53. Tests and Documentation | v6.0 | 0/TBD | Not started | - |
