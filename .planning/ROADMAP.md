# Milestone v2.2: Module Access Fix & Test Coverage

**Status:** In progress
**Phases:** 36-37
**Total Plans:** TBD

## Overview

Fix two classes of qualified module access failures (E0313) in imported file modules and Prelude, fix a parser bug with inline `failwith` inside `try-with`, then add flt regression tests covering the fixed features plus previously untested functionality (failwith basics, LetPatDecl).

## Phases

- [x] **Phase 36: Bug Fixes** - Fix E0313 for imported + Prelude module qualified access and fix failwith inline parse error
- [ ] **Phase 37: Test Coverage** - Add flt tests for fixed features and fill missing coverage gaps

## Phase Details

### Phase 36: Bug Fixes

**Goal**: `Module.func` qualified access works from imported files and Prelude, and `try failwith "x" with e -> y` parses without error
**Depends on**: Phase 35 (v2.1 complete)
**Requirements**: MOD-01, MOD-02, PAR-01
**Success Criteria** (what must be TRUE):
  1. After `open "file.fun"`, calling `Module.func` resolves correctly at runtime (no E0313)
  2. `List.length`, `List.map`, `List.head` from Prelude resolve correctly at runtime (no E0313)
  3. `try failwith "boom" with e -> "caught"` parses and evaluates without error
  4. All 214 F# unit tests and 468+ flt tests continue to pass after fixes
**Plans**: 2 plans

Plans:
- [ ] 36-01-PLAN.md — Thread module maps through TypeCheck/Prelude/Program and wrap Prelude files in modules (MOD-01, MOD-02)
- [ ] 36-02-PLAN.md — Add TryWithClauses grammar nonterminal for inline try-with (PAR-01)

### Phase 37: Test Coverage

**Goal**: Every fixed feature has flt regression tests, and Phase 26 (failwith) and Phase 28 (LetPatDecl) have their first flt coverage
**Depends on**: Phase 36
**Requirements**: TST-14, TST-15, TST-16, TST-17
**Success Criteria** (what must be TRUE):
  1. flt tests for failwith cover basic raise and try-with catch (at least 2 tests)
  2. flt tests for `let (a, b) = (1, 2)` module-level destructuring pass
  3. flt test for `open "file.fun"` followed by `Module.func` call passes
  4. flt tests for `List.length` and `List.map` Prelude qualified access pass
**Plans**: TBD

Plans:
- [ ] 37-01: TBD

---

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 36. Bug Fixes | v2.2 | 2/2 | Complete | 2026-03-25 |
| 37. Test Coverage | v2.2 | 0/TBD | Not started | - |

---

_For current project status, see .planning/STATE.md_
