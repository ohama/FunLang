---
phase: 23-offside-rule-refactoring
plan: "01"
subsystem: lexer
tags: [indent-filter, offside-rule, implicit-in, context-stack, fsyacc]

requires:
  - phase: 01
    provides: IndentFilter foundation (INDENT/DEDENT, match/try pipe alignment)
  - phase: 05
    provides: Module syntax (MODULE IDENT EQUALS INDENT Decls DEDENT)
provides:
  - F#-style offside rule for implicit IN insertion via CtxtLetDecl context
  - InExprBlock/InModule context tracking for expression vs declaration blocks
  - INDENT Expr DEDENT grammar rule for indented expression blocks
  - LET IDENT EQUALS INDENT Expr DEDENT IN Expr grammar rule
affects: []

tech-stack:
  added: []
  patterns:
    - "Offside rule: InLetDecl(blockLet, offsideCol) pushed on LET in expression context, popped when col <= offsideCol"
    - "InExprBlock pushed on INDENT after EQUALS/ARROW, InModule pushed on INDENT after MODULE"
    - "Explicit IN takes priority over implicit IN (nextIsExplicitIn guard)"

key-files:
  created:
    - tests/flt/file/implicit-in-nested.flt
    - tests/flt/file/implicit-in-letrec-multiline.flt
    - tests/flt/file/implicit-in-module-safe.flt
    - tests/flt/file/implicit-in-mixed.flt
    - tests/flt/file/implicit-in-match-body.flt
  modified:
    - src/LangThree/IndentFilter.fs
    - src/LangThree/Parser.fsy
    - tests/LangThree.Tests/IndentFilterTests.fs

key-decisions:
  - "InLetDecl with offside column replaces LetSeqDepth counter for let sequence tracking"
  - "InExprBlock context pushed on INDENT after EQUALS/ARROW to distinguish expression blocks from module/top-level"
  - "InModule context pushed on INDENT after MODULE to prevent implicit IN in module bodies"
  - "INDENT Expr DEDENT added as grammar rule for indented expression blocks (match clause bodies, etc.)"
  - "Stale InMatch/InTry from single-line matches popped by explicit IN handler (col match check)"
  - "Explicit IN suppresses implicit IN insertion (both same-level and DEDENT paths)"

patterns-established:
  - "Offside rule: push context on LET, pop and emit IN when next token reaches offside column"
  - "Expression context detection: isExprContext checks context stack head for InExprBlock/InLetDecl/InMatch/InTry"

duration: 26min
completed: 2026-03-20
---

# Phase 23 Plan 01: F#-Style Offside Rule Summary

**Replaced LetSeqDepth counter with F#-style InLetDecl offside rule for implicit IN, plus InExprBlock/InModule context tracking and INDENT Expr DEDENT grammar support**

## Performance

- **Duration:** 26 min
- **Started:** 2026-03-20T05:15:08Z
- **Completed:** 2026-03-20T05:42:00Z
- **Tasks:** 6 (combined tasks 1-5 into one atomic refactoring commit + task 6 tests)
- **Files modified:** 3 modified, 5 created

## Accomplishments
- Replaced fragile LetSeqDepth counter with clean offside-based InLetDecl context stack
- All existing 418 fslit + 196 F# tests pass with zero regressions
- Added 5 new comprehensive implicit-in tests (423 fslit total)
- Enabled indented match clause bodies via INDENT Expr DEDENT grammar rule (was pre-existing limitation)

## Task Commits

1. **Tasks 1-5: Offside rule refactoring** - `9982e4e` (feat)
   - SyntaxContext: +InLetDecl, +InExprBlock, +InModule
   - FilterState: -LetSeqDepth, -InModuleEquals, +JustSawModule
   - Offside-based IN insertion in NEWLINE processing
   - Grammar: +INDENT Expr DEDENT, +LET...INDENT Expr DEDENT IN Expr
2. **Task 6: Comprehensive implicit-in tests** - `b9d1e4c` (test)
   - 5 new .flt tests covering nested, letrec, module, mixed, match-body

## Files Created/Modified
- `src/LangThree/IndentFilter.fs` - Core offside rule refactoring (InLetDecl, InExprBlock, InModule contexts)
- `src/LangThree/Parser.fsy` - Grammar rules for INDENT Expr DEDENT and let...INDENT...DEDENT IN continuation
- `tests/LangThree.Tests/IndentFilterTests.fs` - Updated FilterState records, updated expected tokens for implicit IN
- `tests/flt/file/implicit-in-nested.flt` - Nested let blocks test
- `tests/flt/file/implicit-in-letrec-multiline.flt` - Quicksort with let rec + multiline fun ->
- `tests/flt/file/implicit-in-module-safe.flt` - Module lets not affected
- `tests/flt/file/implicit-in-mixed.flt` - Mixed explicit/implicit in
- `tests/flt/file/implicit-in-match-body.flt` - Let inside match clause body

## Decisions Made
- **InLetDecl with offside column:** Tracks the LET keyword's indent column; when next token's column <= offsideCol, the let scope ends and IN is inserted. This matches F# compiler LexFilter.fs semantics.
- **InExprBlock context:** Pushed on INDENT after EQUALS/ARROW/IN to distinguish expression blocks (implicit IN allowed) from module/top-level (no implicit IN).
- **InModule context:** Pushed on INDENT after MODULE to prevent implicit IN in module bodies.
- **INDENT Expr DEDENT grammar rule:** Added to handle indented expression blocks (match clause bodies with multi-line code). This was a pre-existing limitation that prevented multi-line match clause bodies.
- **Stale InMatch/InTry handling:** Single-line matches set JustSawMatch which gets consumed on the wrong NEWLINE, pushing InMatch between InLetDecl and explicit IN. Fixed by making explicit IN handler skip past InMatch/InTry at same indent level.
- **Explicit IN suppression:** Both same-level and DEDENT offside checks skip when nextToken is explicit IN, preventing double-IN emission.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] INDENT Expr DEDENT grammar rule needed**
- **Found during:** Task 3 (offside-based IN insertion)
- **Issue:** Grammar had no rule for `LET IDENT EQUALS INDENT Expr DEDENT IN Expr` or general `INDENT Expr DEDENT`. Nested let blocks and match clause bodies with indentation failed to parse.
- **Fix:** Added `INDENT Expr DEDENT -> $2` to Expr rule, `LET IDENT EQUALS INDENT Expr DEDENT IN Expr` and `LET REC IDENT IDENT EQUALS INDENT Expr DEDENT IN Expr` rules.
- **Files modified:** src/LangThree/Parser.fsy
- **Verification:** Nested let, match body, and all existing tests pass
- **Committed in:** 9982e4e

**2. [Rule 1 - Bug] Stale InMatch from single-line matches**
- **Found during:** Task 5 (full test suite)
- **Issue:** JustSawMatch flag from single-line `match x with | ...` was consumed on the next NEWLINE (end of line), pushing InMatch between InLetDecl and explicit IN. Explicit IN couldn't find InLetDecl to pop, causing spurious IN tokens on subsequent DEDENTs.
- **Fix:** Made explicit IN handler search past InMatch/InTry at same indent level when looking for InLetDecl.
- **Files modified:** src/LangThree/IndentFilter.fs
- **Verification:** algo-insertion-sort.flt and 3 other previously-failing tests now pass
- **Committed in:** 9982e4e

**3. [Rule 1 - Bug] Double IN from explicit IN + implicit offside IN**
- **Found during:** Task 5 (full test suite)
- **Issue:** When explicit IN follows a let binding, the offside rule also tried to insert implicit IN at the same position, producing double IN tokens.
- **Fix:** Added nextIsExplicitIn guard to both same-level and DEDENT offside check paths.
- **Files modified:** src/LangThree/IndentFilter.fs
- **Verification:** algo-factorial.flt and 3 other previously-failing tests now pass
- **Committed in:** 9982e4e

---

**Total deviations:** 3 auto-fixed (1 missing critical, 2 bugs)
**Impact on plan:** All auto-fixes necessary for correctness. Grammar rules were an expected gap in the plan. Stale InMatch and double-IN bugs were discovered through the existing test suite.

## Issues Encountered
- Plan tasks 1-5 were deeply intertwined (SyntaxContext changes, filter logic, INDENT/DEDENT handling, offside checks all depend on each other). Combined into a single atomic commit rather than 5 separate commits.
- 41 new shift/reduce conflicts from INDENT Expr DEDENT rule (332 total, up from 291). All resolved by default shift preference which is correct for extending expressions inside INDENT/DEDENT blocks.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Offside rule refactoring complete
- v1.6 milestone objectives met
- All 196 F# + 423 fslit = 619 tests passing

---
*Phase: 23-offside-rule-refactoring*
*Completed: 2026-03-20*
