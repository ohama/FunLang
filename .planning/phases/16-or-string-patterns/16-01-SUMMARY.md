---
phase: 16-or-string-patterns
plan: "01"
subsystem: pattern-matching
tags: [or-pattern, string-pattern, LALR1, decision-tree, exhaustiveness]

# Dependency graph
requires:
  - phase: 07-match-compilation
    provides: decision tree compilation (MatchCompile.fs)
  - phase: 02-exhaustiveness
    provides: exhaustiveness checker with CasePat DU (Exhaustive.fs)
provides:
  - StringConst pattern matching (match "hello" with | "hello" -> ...)
  - Or-pattern matching (match x with | 1 | 2 | 3 -> ...)
  - Unparenthesized or-patterns in LALR(1) grammar (zero conflicts)
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Or-pattern expansion: OrPat expanded to multiple rows before decision tree compilation"
    - "#str_ prefix for string constant constructor names in MatchCompile"
    - "OrPattern nonterminal in Parser.fsy disambiguates PIPE in or-patterns vs match clauses"

key-files:
  created:
    - tests/flt/file/pat-string-basic.flt
    - tests/flt/file/pat-string-multi.flt
    - tests/flt/file/pat-or-int.flt
    - tests/flt/file/pat-or-bool.flt
    - tests/flt/file/pat-or-string.flt
    - tests/flt/emit/ast-pat/ast-pat-string.flt
    - tests/flt/emit/ast-pat/ast-pat-or.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Eval.fs
    - src/LangThree/Infer.fs
    - src/LangThree/MatchCompile.fs
    - src/LangThree/Exhaustive.fs
    - src/LangThree/Format.fs

key-decisions:
  - "Unparenthesized or-patterns via OrPattern nonterminal -- zero LALR(1) conflicts"
  - "Top-level or-pattern expansion only (no nested or-pattern expansion like Some (1 | 2))"
  - "StringConst uses #str_ prefix in MatchCompile (consistent with #int_ and #bool_)"
  - "specializeRow made recursive for or-pattern expansion in exhaustiveness"

patterns-established:
  - "Or-pattern expansion before decision tree: expandOrPatterns in compileMatch entry point"

# Metrics
duration: 7min
completed: 2026-03-19
---

# Phase 16 Plan 01: String Patterns + Or-Patterns Summary

**StringConst and OrPat added to Pattern DU with unparenthesized LALR(1) or-pattern grammar (zero conflicts), decision tree expansion, and exhaustiveness integration**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-19T03:10:15Z
- **Completed:** 2026-03-19T03:17:06Z
- **Tasks:** 4
- **Files modified:** 7 source + 7 test files

## Accomplishments
- String constant patterns parse and match via StringConst in Constant DU, propagated through all 7 modules
- Or-patterns parse unparenthesized (| 1 | 2 | 3 -> expr) with zero LALR(1) conflicts via OrPattern nonterminal
- Or-patterns integrated with decision tree (expansion to multiple rows) and exhaustiveness checker
- 7 new fslit tests covering string patterns, or-patterns, and --emit-ast output
- All 520 tests pass (196 F# + 324 fslit)

## Task Commits

1. **Task 1: Add StringConst to Constant DU and propagate** - `6c9e007` (feat)
2. **Task 2: Add OrPat to Pattern DU and implement** - `aaba7a1` (feat)
3. **Task 3: Integrate OrPat with decision tree and exhaustiveness** - `021d862` (feat)
4. **Task 4: Add fslit tests** - `defb2f9` (test)

## Files Created/Modified
- `src/LangThree/Ast.fs` - StringConst in Constant DU, OrPat in Pattern DU, patternSpanOf
- `src/LangThree/Parser.fsy` - STRING pattern rule, OrPattern nonterminal, MatchClauses updated
- `src/LangThree/Eval.fs` - matchPattern for StringConst and OrPat
- `src/LangThree/Infer.fs` - inferPattern for StringConst (TString) and OrPat (unify alternatives)
- `src/LangThree/MatchCompile.fs` - #str_ constructor, expandOrPatterns, OrPat defensive cases
- `src/LangThree/Exhaustive.fs` - specializeRow for OrPat, astPatToCasePat for Ast.OrPat
- `src/LangThree/Format.fs` - formatPattern for StringConst and OrPat

## Decisions Made
- **Unparenthesized or-patterns:** The OrPattern nonterminal (Pattern | Pattern PIPE OrPattern) disambiguates PIPE usage naturally in LALR(1) without any shift/reduce conflicts. No need for parenthesized fallback.
- **Top-level expansion only:** Or-patterns are expanded at the compileMatch entry point. Nested or-patterns (e.g., Some (1 | 2)) are not expanded -- they would need recursive pattern expansion which adds complexity for minimal benefit.
- **specializeRow made recursive:** The Exhaustive.fs specializeRow function needed `let rec` to handle OrPat alternatives that may themselves be or-patterns.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] specializeRow not recursive**
- **Found during:** Task 3
- **Issue:** specializeRow calls itself for OrPat alternatives but was not declared `let rec`
- **Fix:** Changed to `let rec specializeRow`
- **Files modified:** src/LangThree/Exhaustive.fs
- **Committed in:** 021d862

**2. [Rule 1 - Bug] fslit test indentation issues**
- **Found during:** Task 4
- **Issue:** Match pipes must align with match keyword in LangThree's indent-sensitive parser
- **Fix:** Restructured test files to use function body indentation pattern
- **Files modified:** tests/flt/file/pat-or-int.flt, pat-or-string.flt, pat-or-bool.flt, pat-string-basic.flt, pat-string-multi.flt
- **Committed in:** defb2f9

---

**Total deviations:** 2 auto-fixed (2 bugs)
**Impact on plan:** Both fixes necessary for correctness. No scope creep.

## Issues Encountered
None beyond the auto-fixed deviations above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Or-patterns and string patterns fully functional
- All 520 tests pass (196 F# + 324 fslit)
- Phase 16 complete (single plan)

---
*Phase: 16-or-string-patterns*
*Completed: 2026-03-19*
