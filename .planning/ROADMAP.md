# Roadmap: LangThree v5.0 Imperative Ergonomics

## Milestones

- v1.0-v3.0: Phases 1-41 (shipped 2026-03-25)
- v4.0 Mutable Variables: Phases 42-44 (shipped 2026-03-27)
- v5.0 Imperative Ergonomics: Phases 45-49 (in progress)

## Overview

v5.0 adds four imperative syntax constructs that complement v4.0's mutable variables: expression sequencing (`;`), while and for loops, array/hashtable indexing syntax (`arr.[i]`), and if-then without else. Sequencing comes first because loops depend on it in their bodies. Indexing syntax is independent and follows. If-then is the simplest addition and rounds out the feature set before tests and documentation close the milestone.

## Phases

<details>
<summary>v1.0-v4.0 (Phases 1-44) -- SHIPPED 2026-03-27</summary>

44 phases, 103 plans completed. See git history for details.

</details>

### v5.0 Imperative Ergonomics (In Progress)

**Milestone Goal:** Array indexing syntax, expression sequencing, loops, and if-then without else — the imperative ergonomics layer on top of mutable variables

- [ ] **Phase 45: Expression Sequencing** — `e1; e2` desugars to `let _ = e1 in e2`, enabling multi-step imperative blocks
- [ ] **Phase 46: Loop Constructs** — `while cond do body` and `for i = start to end do body` with immutable loop variable
- [ ] **Phase 47: Array and Hashtable Indexing Syntax** — `arr.[i]` / `arr.[i] <- v` / `ht.[key]` / `ht.[key] <- v` with chained indexing
- [ ] **Phase 48: If-Then Without Else** — `if cond then expr` accepted when expr is unit, type error otherwise
- [ ] **Phase 49: Tests and Documentation** — flt test suites for all v5.0 features and tutorial chapter update

## Phase Details

### Phase 45: Expression Sequencing
**Goal**: Users can write `e1; e2` to evaluate expressions in sequence, enabling multi-step imperative code without verbose `let _ = ... in` boilerplate
**Depends on**: Phase 44 (v4.0 complete)
**Requirements**: SEQ-01, SEQ-02, SEQ-03
**Success Criteria** (what must be TRUE):
  1. User can write `print "hello"; print "world"` and both effects execute in order
  2. User can chain three or more expressions with `;` and get the value of the last expression
  3. Sequencing works inside indentation-based blocks (offside rule handles `;` at same column)
**Plans**: TBD

Plans:
- [ ] 45-01: Lexer/parser: `;` as sequencing operator (distinct from list separator), AST node, desugar to `let _ = e1 in e2`
- [ ] 45-02: Eval, type checker, and offside rule integration for expression sequencing

### Phase 46: Loop Constructs
**Goal**: Users can write `while` and `for` loops for imperative iteration, with the loop variable immutable inside the body
**Depends on**: Phase 45 (sequencing available for loop bodies)
**Requirements**: LOOP-01, LOOP-02, LOOP-03, LOOP-04
**Success Criteria** (what must be TRUE):
  1. User can write `while !running do body` and the body repeats until the condition is false, returning unit
  2. User can write `for i = 0 to 9 do body` and `i` takes values 0 through 9 in sequence
  3. User can write `for i = 9 downto 0 do body` and `i` takes values 9 down to 0
  4. Attempting `i <- 42` inside a for loop body produces a type error (loop variable is immutable)
**Plans**: TBD

Plans:
- [ ] 46-01: AST nodes, parser grammar for while and for-to/downto, eval semantics, unit return
- [ ] 46-02: Type checker for loop constructs, loop variable immutability enforcement

### Phase 47: Array and Hashtable Indexing Syntax
**Goal**: Users can read and write array/hashtable elements with `.[i]` syntax instead of calling `array_get`/`array_set` functions
**Depends on**: Phase 44 (v4.0 array/hashtable infrastructure)
**Requirements**: IDX-01, IDX-02, IDX-03, IDX-04, IDX-05
**Success Criteria** (what must be TRUE):
  1. User can write `arr.[i]` to read an array element (equivalent to `array_get arr i`)
  2. User can write `arr.[i] <- v` to write an array element (equivalent to `array_set arr i v`)
  3. User can write `ht.[key]` and `ht.[key] <- v` to read/write hashtable entries
  4. User can write `matrix.[r].[c]` to index a nested array (chained indexing)
**Plans**: TBD

Plans:
- [ ] 47-01: Lexer/parser: `.[` token, AST nodes for index-read and index-write expressions
- [ ] 47-02: Eval and type checker for indexing syntax, chained indexing support

### Phase 48: If-Then Without Else
**Goal**: Users can write `if cond then expr` when the then-branch returns unit, with a clear type error when it does not
**Depends on**: Phase 45 (sequencing — useful in unit-returning then-branches)
**Requirements**: IFTHEN-01, IFTHEN-02
**Success Criteria** (what must be TRUE):
  1. User can write `if x > 0 then print "positive"` and it compiles and runs correctly, returning unit
  2. Writing `if x > 0 then 42` (non-unit then-branch) produces a type error at compile time
**Plans**: TBD

Plans:
- [ ] 48-01: Parser accepts optional else branch, AST update, eval returns unit for absent else, type checker enforces unit constraint

### Phase 49: Tests and Documentation
**Goal**: Comprehensive flt test coverage for all v5.0 features and an updated tutorial chapter that users can follow to learn the new syntax
**Depends on**: Phases 45, 46, 47, 48 (all features complete)
**Requirements**: TST-28, TST-29, TST-30, TST-31, TST-32
**Success Criteria** (what must be TRUE):
  1. flt tests for `arr.[i]` / `arr.[i] <- v` / `ht.[key]` / `ht.[key] <- v` and chained indexing all pass
  2. flt tests for `e1; e2` sequencing (two-expression, multi-expression, and block-level) all pass
  3. flt tests for `while` loop and `for` to/downto loops including immutable loop variable error all pass
  4. flt tests for `if cond then expr` (unit case and non-unit type error case) all pass
  5. Tutorial chapter covers all four v5.0 feature areas with working examples
**Plans**: TBD

Plans:
- [ ] 49-01: flt tests for indexing syntax (TST-28)
- [ ] 49-02: flt tests for sequencing (TST-29) and if-then without else (TST-31)
- [ ] 49-03: flt tests for loop constructs (TST-30) and tutorial chapter (TST-32)

## Progress

**Execution Order:** 45 -> 46 -> 47 -> 48 -> 49

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 42. Core Mutable Variables | v4.0 | 2/2 | Complete | 2026-03-27 |
| 43. Edge Cases and Error Handling | v4.0 | 1/1 | Complete | 2026-03-27 |
| 44. Tests and Documentation | v4.0 | 2/2 | Complete | 2026-03-27 |
| 45. Expression Sequencing | v5.0 | 0/TBD | Not started | - |
| 46. Loop Constructs | v5.0 | 0/TBD | Not started | - |
| 47. Array and Hashtable Indexing Syntax | v5.0 | 0/TBD | Not started | - |
| 48. If-Then Without Else | v5.0 | 0/TBD | Not started | - |
| 49. Tests and Documentation | v5.0 | 0/TBD | Not started | - |

---
*Roadmap created: 2026-03-28*
