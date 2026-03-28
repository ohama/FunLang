---
phase: 50-newline-implicit-sequencing
plan: 01
subsystem: lexer
tags: [IndentFilter, SEMICOLON injection, newline sequencing, implicit sequencing, SeqExpr]

# Dependency graph
requires:
  - phase: 45-expression-sequencing
    provides: SeqExpr nonterminal already handles SEMICOLON tokens for sequencing
  - phase: 46-for-loop
    provides: DO token trigger for InExprBlock push (while/for bodies)
provides:
  - SEMICOLON injection in IndentFilter.fs isAtSameLevel branch
  - isContinuationStart guard (PIPE_RIGHT, COMPOSE_RIGHT, COMPOSE_LEFT, AND, OR, CONS, INFIXOP*)
  - isStructuralTerminator guard (ELSE, WITH, THEN, PIPE, IN)
  - shouldInjectSemicolon logic gated on InExprBlock direct top context
  - 5 new nlseq flt tests covering NLSEQ-01 through NLSEQ-05
affects: [all future phases using expression blocks, 51-stdlib-additions, 52-option-result-helpers, 53-practical-examples]

# Tech tracking
tech-stack:
  added: []
  patterns: [Newline-as-SEMICOLON injection via IndentFilter context guard, InExprBlock discriminator for expression vs declaration scope]

key-files:
  created:
    - tests/flt/expr/seq/nlseq-basic.flt
    - tests/flt/expr/seq/nlseq-in-match.flt
    - tests/flt/expr/seq/nlseq-in-while.flt
    - tests/flt/expr/seq/nlseq-no-module.flt
    - tests/flt/expr/seq/nlseq-pipe-continuation.flt
  modified:
    - src/LangThree/IndentFilter.fs

key-decisions:
  - "SEMICOLON injection fires only when InExprBlock is the DIRECT top of context stack — InLetDecl on top means checkOffside fires first emitting IN instead"
  - "while loops at module top-level require let _ = wrapper — bare while is not a top-level declaration"
  - "nlseq-in-while.flt uses let _ = wrapper and expects trailing () in output (while returns unit)"
  - "Do NOT add PLUS/MINUS to isContinuationStart — unary -expr is valid as statement start"
  - "nextToken-based guard (not prevToken) — the next line's starting token determines continuation, not the previous line's ending token"

patterns-established:
  - "IndentFilter SEMICOLON injection: same pattern as INDENT/DEDENT/IN — filter-level token synthesis, no parser changes"
  - "InExprBlock _ :: _ direct-top guard: safely excludes let chains (InLetDecl on top), module level (InModule), top-level (TopLevel)"

# Metrics
duration: 7min
completed: 2026-03-28
---

# Phase 50 Plan 01: Newline Implicit Sequencing Summary

**SEMICOLON injection in IndentFilter.fs isAtSameLevel branch enables multi-line expression blocks without explicit semicolons, guarded by isContinuationStart and isStructuralTerminator against operator continuations and structural keywords**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-28T01:40:09Z
- **Completed:** 2026-03-28T01:47:46Z
- **Tasks:** 2
- **Files modified:** 6 (1 source + 5 tests)

## Accomplishments

- Added `isContinuationStart` and `isStructuralTerminator` helper functions to IndentFilter.fs
- Injected SEMICOLON in the `isAtSameLevel` else branch when `InExprBlock _ :: _` is direct top context
- All 5 new nlseq flt tests pass: basic multi-line bodies, match arm bodies, while bodies, module-level safety, pipe continuation suppression
- Full 578-test suite passes with zero regressions (573 original + 5 new)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add SEMICOLON injection to IndentFilter.fs** - `7f893db` (feat)
2. **Task 2: Write 5 nlseq flt tests + full regression** - `a3488b6` (test)

**Plan metadata:** (pending docs commit)

## Files Created/Modified

- `src/LangThree/IndentFilter.fs` - Added isContinuationStart, isStructuralTerminator helpers and shouldInjectSemicolon logic in isAtSameLevel branch
- `tests/flt/expr/seq/nlseq-basic.flt` - NLSEQ-01: multi-line function body without explicit semicolons
- `tests/flt/expr/seq/nlseq-in-match.flt` - NLSEQ-02: match arm body with multiple statements
- `tests/flt/expr/seq/nlseq-in-while.flt` - NLSEQ-03: while body with multiple statements (requires let _ = wrapper)
- `tests/flt/expr/seq/nlseq-no-module.flt` - NLSEQ-05: module-level lets independent, no spurious SEMICOLON
- `tests/flt/expr/seq/nlseq-pipe-continuation.flt` - NLSEQ-04: pipe continuation suppresses SEMICOLON

## Decisions Made

- `InExprBlock _ :: _` direct-top guard is the critical discriminator: InLetDecl on top means checkOffside fires first emitting IN, so SEMICOLON branch is never reached for let chains
- while loops are not valid top-level declarations — nlseq-in-while.flt uses `let _ =` wrapper; the while returns unit which appears in output as `()`
- nextToken-based guard (not prevToken): the first token of the next line determines whether it's a continuation or structural keyword
- Did NOT add PLUS/MINUS to isContinuationStart — unary `-1` is a valid statement start

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed nlseq-in-while.flt test — while requires let _ = wrapper at module level**

- **Found during:** Task 2 (creating flt tests)
- **Issue:** Plan's test wrote `while i < 2 do` at module top-level without `let _ = ...` wrapper; this produces a parse error because while is not a top-level declaration
- **Fix:** Wrapped the while expression in `let _ = ...` and added `()` to expected output (while returns unit)
- **Files modified:** tests/flt/expr/seq/nlseq-in-while.flt
- **Verification:** Test passes, output matches `tick\ntick\n()`
- **Committed in:** a3488b6 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug in test expected output)
**Impact on plan:** Necessary correction to test structure. No scope creep.

## Issues Encountered

None beyond the test structure fix noted above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 50 complete — users can now write multi-line expression blocks without explicit semicolons
- SeqExpr handles SEMICOLON from both explicit user-written semicolons and injected ones
- Phases 51-53 (stdlib additions, option/result helpers, practical examples) can all use newline sequencing in their example code
- No blockers

---
*Phase: 50-newline-implicit-sequencing*
*Completed: 2026-03-28*
