---
phase: 51-for-in-collection-loops
plan: 01
subsystem: interpreter
tags: [fsharp, ast, parser, bidir, eval, for-in, collection, loop, list, array]

# Dependency graph
requires:
  - phase: 46-loop-constructs
    provides: ForExpr (for-to/downto) as direct template for AST/Parser/Bidir/Eval pattern
  - phase: 50-newline-implicit-sequencing
    provides: SeqExpr nonterminal + SEMICOLON injection enabling multi-line loop bodies without explicit semicolons
provides:
  - ForInExpr AST DU case with (var, collection, body, span)
  - Parser grammar for FOR IDENT IN Expr DO SeqExpr (inline + indented body)
  - Bidir type synthesis: unifies collection with TList/TArray, binds loop var as immutable Scheme
  - Eval runtime: iterates ListValue and ArrayValue elements, returns unit
  - Passthrough arms in Infer, Format, TypeCheck (collectMatches, collectTryWiths, collectModuleRefs, rewriteModuleAccess)
  - 4 flt integration tests: FORIN-01 through FORIN-04
affects:
  - 52-option-result-stdlib (uses for-in in .fun examples)
  - 53-string-interpolation (no direct dependency)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ForExpr is the canonical template for loop AST/Parser/Bidir/Eval patterns"
    - "Loop variables bound as Scheme([], elemTy) without adding to mutableVars — immutability enforced by existing Assign check"
    - "Array literal [|...|] not supported; arrays created via Array.ofList"
    - "try/catch unification for TList-first/TArray-fallback collection type inference"

key-files:
  created:
    - tests/flt/expr/loop/loop-for-in-list.flt
    - tests/flt/expr/loop/loop-for-in-array.flt
    - tests/flt/expr/loop/loop-for-in-empty.flt
    - tests/flt/expr/loop/loop-for-in-immutable-error.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Bidir.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Infer.fs
    - src/LangThree/Format.fs
    - src/LangThree/TypeCheck.fs

key-decisions:
  - "Use freshVar() not freshTVar() — the codebase uses freshVar for fresh type variables in Bidir"
  - "flt test FORIN-02 uses Array.ofList not [|...|] literal — array literal syntax not supported in the language"
  - "try/catch unification pattern: TList first, TArray fallback — matches codebase exception-driven unification style"

patterns-established:
  - "ForInExpr follows exact ForExpr template across all 7 files"

# Metrics
duration: 20min
completed: 2026-03-28
---

# Phase 51 Plan 01: For-In Collection Loops Summary

**`for x in collection do body` syntax for iterating lists and arrays, with immutable loop variable enforced via E0320**

## Performance

- **Duration:** 20 min
- **Started:** 2026-03-28T01:53:34Z
- **Completed:** 2026-03-28T02:13:42Z
- **Tasks:** 3
- **Files modified:** 7 source files + 4 flt tests

## Accomplishments
- New `ForInExpr` AST node and parser grammar (no LALR conflict with existing `FOR IDENT EQUALS`)
- Bidir type synthesis unifies collection with `TList(elemTv)` first, falls back to `TArray(elemTv)`, binds loop var as immutable `Scheme([], elemTy)`
- Eval iterates `ListValue` and `ArrayValue` elements; empty collection returns `TupleValue []` with zero iterations
- All four TypeCheck.fs helpers updated with explicit recursive ForInExpr arms
- 582/582 flt tests pass (578 existing + 4 new)

## Task Commits

Each task was committed atomically:

1. **Task 1: ForInExpr AST node and parser grammar rules** - `8677bae` (feat)
2. **Task 2: Bidir, Eval, and passthrough files** - `a2bb2e6` (feat)
3. **Task 3: Four flt integration tests** - `f3dbdca` (test)

## Files Created/Modified
- `src/LangThree/Ast.fs` - Added ForInExpr DU case and span extractor arm
- `src/LangThree/Parser.fsy` - Added FOR IDENT IN grammar rules (inline + indented)
- `src/LangThree/Bidir.fs` - ForInExpr synth with TList/TArray unification, immutable var binding
- `src/LangThree/Eval.fs` - ForInExpr eval iterating ListValue/ArrayValue elements
- `src/LangThree/Infer.fs` - ForInExpr passthrough
- `src/LangThree/Format.fs` - ForInExpr AST formatter passthrough
- `src/LangThree/TypeCheck.fs` - ForInExpr arms in all 4 recursive helpers
- `tests/flt/expr/loop/loop-for-in-list.flt` - FORIN-01: list iteration
- `tests/flt/expr/loop/loop-for-in-array.flt` - FORIN-02: array iteration
- `tests/flt/expr/loop/loop-for-in-immutable-error.flt` - FORIN-03: E0320 on loop var assignment
- `tests/flt/expr/loop/loop-for-in-empty.flt` - FORIN-04: empty collection, zero iterations

## Decisions Made
- Used `freshVar()` not `freshTVar()` — the codebase naming convention for fresh type variables in Bidir.fs
- flt test FORIN-02 uses `Array.ofList [10; 20; 30]` instead of `[|10; 20; 30|]` — array literal syntax `[|...|]` is not supported in the language
- try/catch unification pattern for TList-first/TArray-fallback — consistent with exception-driven unification style in Bidir.fs

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Wrong function name: freshTVar vs freshVar**
- **Found during:** Task 2 (Bidir.fs implementation)
- **Issue:** Plan code used `freshTVar()` but the function is named `freshVar()` in Bidir.fs
- **Fix:** Replaced all three occurrences of `freshTVar()` with `freshVar()`
- **Files modified:** src/LangThree/Bidir.fs
- **Verification:** Build succeeded with 0 errors
- **Committed in:** a2bb2e6 (Task 2 commit)

**2. [Rule 1 - Bug] Array literal syntax [|...|] not supported**
- **Found during:** Task 3 (flt test creation)
- **Issue:** Plan specified `[|10; 20; 30|]` in FORIN-02 test but the language doesn't support array literal syntax; test produced parse error
- **Fix:** Changed test to use `Array.ofList [10; 20; 30]` — consistent with all other array flt tests
- **Files modified:** tests/flt/expr/loop/loop-for-in-array.flt
- **Verification:** Test passes: 60 / ()
- **Committed in:** f3dbdca (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 - Bug)
**Impact on plan:** Both auto-fixes necessary for correctness. No scope creep.

## Issues Encountered
- None beyond the two auto-fixed deviations above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 51 complete: `for x in collection do body` fully working for lists and arrays
- 582/582 flt tests passing
- Ready for Phase 52 (Option/Result stdlib .fun functions — zero interpreter changes needed)

---
*Phase: 51-for-in-collection-loops*
*Completed: 2026-03-28*
