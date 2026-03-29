---
phase: 61-hashtable-tuple-test-conversion
plan: 01
subsystem: language-core
tags: [ast, parser, bidir, eval, typecheck, for-in, hashtable, pattern, tuple]

# Dependency graph
requires:
  - phase: 51-for-in-loops
    provides: ForInExpr AST node and for-in loop evaluation
  - phase: 39-hashtable
    provides: HashtableValue and THashtable type
provides:
  - ForInExpr var field changed from string to Pattern
  - Parser supports both `for x in coll` and `for (k, v) in coll` syntax
  - Hashtable iteration produces TupleValue [k; v] instead of RecordValue KeyValuePair
  - Loop env binding uses matchPattern for all pattern types
affects:
  - 61-02: test conversion plan that rewrites kv.Key/kv.Value tests to use tuple destructuring

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ForInExpr loop variable is now a Pattern (enabling destructuring in for-in)"
    - "Hashtable iteration yields TupleValue — same as let (k,v) = ... pattern"
    - "inferPattern used in Bidir.fs to unify pattern type against element type"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Bidir.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Format.fs

key-decisions:
  - "ForInExpr var: string -> var: Pattern (enables tuple destructuring syntax)"
  - "THashtable iteration emits TTuple [keyTy; valTy] in Bidir (not TData KeyValuePair)"
  - "HashtableValue iteration emits TupleValue [k; v] in Eval (not RecordValue KeyValuePair)"
  - "KeyValuePair field access arm in Bidir.fs retained for Plan 02 test conversion"
  - "hashtable-forin.flt test now failing (planned) — will be rewritten in Plan 02"

patterns-established:
  - "Pattern: use inferPattern + unify to bind loop patterns (same approach as LetPat)"
  - "Pattern: matchPattern for loop env binding (already used for match expressions)"

# Metrics
duration: 4min
completed: 2026-03-29
---

# Phase 61 Plan 01: ForInExpr Pattern Destructuring Summary

**ForInExpr var changed from string to Pattern, enabling `for (k, v) in ht do ...` syntax; hashtable iteration now produces TupleValue instead of KeyValuePair records**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-03-29T09:25:38Z
- **Completed:** 2026-03-29T09:28:58Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Changed `ForInExpr of var: string` to `ForInExpr of var: Pattern` in Ast.fs
- Parser now supports both `for x in coll do ...` (IDENT wrapped in VarPat) and `for (k, v) in coll do ...` (TuplePattern) syntax
- Bidir.fs type-checker uses `inferPattern`/`unify` to bind loop pattern against element type; THashtable yields `TTuple [keyTy; valTy]`
- Eval.fs runtime uses `matchPattern` for loop env binding; HashtableValue yields `TupleValue [k; v]`
- Format.fs uses `formatPattern` for the loop variable display

## Task Commits

Each task was committed atomically:

1. **Task 1: AST + Parser changes for ForInExpr Pattern support** - `4647574` (feat)
2. **Task 2: Update Bidir, Eval, TypeCheck, Format for Pattern-based ForInExpr** - `637e139` (feat)

**Plan metadata:** (in final commit)

## Files Created/Modified
- `src/LangThree/Ast.fs` - ForInExpr var: Pattern (was string)
- `src/LangThree/Parser.fsy` - FOR IDENT IN wraps in VarPat; new FOR TuplePattern IN rules
- `src/LangThree/Bidir.fs` - inferPattern binding, TTuple for THashtable
- `src/LangThree/Eval.fs` - matchPattern binding, TupleValue for HashtableValue
- `src/LangThree/Format.fs` - formatPattern for loop var

## Decisions Made
- Used `inferPattern` + `unifyWithContext` in Bidir.fs to bind the loop pattern — same approach as LetPat arm, ensures correct type unification for both VarPat and TuplePat
- Kept KeyValuePair field access arm in Bidir.fs (~lines 636-642) intact per plan — will be removed in Plan 02 after test conversion
- TypeCheck.fs needed no changes: `collectModuleRefs` uses `_` for var, `rewriteModuleAccess` binds var positionally and passes through unchanged
- Infer.fs needed no changes: ForInExpr arm uses `_` for var

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- `hashtable-forin.flt` test now fails (expected — it uses old `kv.Key`/`kv.Value` syntax that will be converted in Plan 02)
- `hashtable-keys-tryget.flt` was already failing before these changes (pre-existing untracked test file referencing non-existent builtins)
- Both failures are pre-planned or pre-existing; all other test suites pass

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ForInExpr now accepts Pattern — prerequisite for `for (k, v) in ht do ...` syntax complete
- Plan 02 can now convert existing hashtable tests from `kv.Key`/`kv.Value` to tuple destructuring
- After Plan 02 conversion, the KeyValuePair field access arm in Bidir.fs can be removed

---
*Phase: 61-hashtable-tuple-test-conversion*
*Completed: 2026-03-29*
