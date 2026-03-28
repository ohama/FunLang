# Requirements: LangThree v5.0

**Defined:** 2026-03-28
**Core Value:** v4.0 가변 변수와 자연스럽게 어우러지는 명령형 구문 요소 추가

## v5.0 Requirements

### Array/Hashtable Indexing Syntax

- [ ] **IDX-01**: `arr.[i]` reads array element (desugars to `array_get arr i`)
- [ ] **IDX-02**: `arr.[i] <- v` writes array element (desugars to `array_set arr i v`)
- [ ] **IDX-03**: `ht.[key]` reads hashtable value (desugars to `hashtable_get ht key`)
- [ ] **IDX-04**: `ht.[key] <- v` writes hashtable value (desugars to `hashtable_set ht key v`)
- [ ] **IDX-05**: Chained indexing works: `matrix.[r].[c]`

### Expression Sequencing

- [ ] **SEQ-01**: `e1; e2` evaluates e1 (discards result), then evaluates and returns e2
- [ ] **SEQ-02**: Multi-statement sequencing `e1; e2; e3` works (left-associative)
- [ ] **SEQ-03**: Sequencing works with indentation-based blocks (offside rule)

### Loop Constructs

- [ ] **LOOP-01**: `while cond do body` — repeats body while cond is true, returns unit
- [ ] **LOOP-02**: `for i = start to end do body` — ascending loop, i bound in body
- [ ] **LOOP-03**: `for i = start downto end do body` — descending loop
- [ ] **LOOP-04**: Loop variable `i` is immutable within body (cannot reassign)

### If-Then Without Else

- [ ] **IFTHEN-01**: `if cond then expr` accepted when expr returns unit
- [ ] **IFTHEN-02**: `if cond then expr` produces type error when expr is non-unit

### Test Coverage

- [ ] **TST-28**: Array/hashtable indexing syntax flt tests
- [ ] **TST-29**: Expression sequencing flt tests
- [ ] **TST-30**: Loop construct flt tests (while, for-to, for-downto)
- [ ] **TST-31**: If-then without else flt tests
- [ ] **TST-32**: Tutorial chapter update for v5.0 features

## Out of Scope

| Feature | Reason |
|---------|--------|
| `arr[i]` C-style indexing | F# uses `arr.[i]` — consistent with record dot notation |
| `for x in collection do` | Collection iteration via for-in deferred — use `iter`/`map` HOFs |
| List indexing `list.[i]` | Lists are linked lists — O(n) access discouraged by design |
| `do` blocks / `begin`/`end` | Sequencing via `;` sufficient — no block delimiters needed |
| Mutable loop variable | `for` loop variable is always immutable — F# convention |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SEQ-01 | Phase 45 | Complete |
| SEQ-02 | Phase 45 | Complete |
| SEQ-03 | Phase 45 | Complete |
| LOOP-01 | Phase 46 | Complete |
| LOOP-02 | Phase 46 | Complete |
| LOOP-03 | Phase 46 | Complete |
| LOOP-04 | Phase 46 | Complete |
| IDX-01 | Phase 47 | Complete |
| IDX-02 | Phase 47 | Complete |
| IDX-03 | Phase 47 | Complete |
| IDX-04 | Phase 47 | Complete |
| IDX-05 | Phase 47 | Complete |
| IFTHEN-01 | Phase 48 | Pending |
| IFTHEN-02 | Phase 48 | Pending |
| TST-28 | Phase 49 | Pending |
| TST-29 | Phase 49 | Pending |
| TST-30 | Phase 49 | Pending |
| TST-31 | Phase 49 | Pending |
| TST-32 | Phase 49 | Pending |

**Coverage:**
- v5.0 requirements: 19 total
- Mapped to phases: 19
- Unmapped: 0 (complete)

---
*Requirements defined: 2026-03-28*
*Last updated: 2026-03-28 — traceability complete (v5.0 roadmap created)*
