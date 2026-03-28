---
phase: 47-array-hashtable-indexing
plan: 01
subsystem: language
tags: [lexer, parser, ast, type-checking, evaluation, flt-tests, dotlbracket, indexing]

# Dependency graph
requires:
  - phase: 46-loop-constructs
    provides: while/for loops establishing the full imperative ergonomics pipeline pattern
  - phase: 42-mutable-variables
    provides: mutable arrays/hashtables and assignment syntax (<-)

provides:
  - DOTLBRACKET token in Lexer/Parser/IndentFilter
  - IndexGet/IndexSet AST nodes with spans
  - IndexOnNonCollection error kind (E0471) in Diagnostic
  - Full bidirectional type checking for arr.[i] and arr.[i] <- v
  - Runtime evaluation with bounds checking for arrays, key-not-found for hashtables
  - 7 flt integration tests for indexing syntax
affects:
  - future phases using arr.[i] syntax in examples/tests
  - 48-string-indexing or similar if string indexing is added

# Tech tracking
tech-stack:
  added: []
  patterns:
    - DOTLBRACKET single token strategy (same as F# compiler) to avoid LALR conflicts
    - IndexGet in Atom for left-recursive chaining; IndexSet in Expr mirroring SetField
    - TArray/THashtable dispatch in Bidir.fs synth for polymorphic collection indexing

key-files:
  created:
    - tests/flt/expr/indexing/index-array-read.flt
    - tests/flt/expr/indexing/index-array-write.flt
    - tests/flt/expr/indexing/index-hashtable-read.flt
    - tests/flt/expr/indexing/index-hashtable-write.flt
    - tests/flt/expr/indexing/index-chained.flt
    - tests/flt/expr/indexing/index-out-of-bounds.flt
    - tests/flt/expr/indexing/index-type-error.flt
  modified:
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Ast.fs
    - src/LangThree/Diagnostic.fs
    - src/LangThree/IndentFilter.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/Infer.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Format.fs
    - src/LangThree/TypeCheck.fs

key-decisions:
  - "Used DOTLBRACKET single token (same as F# compiler) to avoid LALR shift/reduce conflict between Atom.DOT.IDENT (field access) and array indexing"
  - "Chose IndexGet/IndexSet AST nodes (Pattern 4 from research) over parser-level desugaring — enables proper type error messages via Bidir.fs dispatch on TArray/THashtable"
  - "IndexGet placed in Atom (left-recursive for chaining); IndexSet in Expr (mirrors SetField — set returns unit, must not appear as function argument)"
  - "array_create/hashtable_create are the actual builtin names (not array_new/hashtable_new as plan initially suggested)"

patterns-established:
  - "DOTLBRACKET token in IndentFilter bracket tracking alongside LBRACKET for multi-line index expressions"
  - "TArray/THashtable match dispatch in Bidir.fs for collection type-polymorphic operations"

# Metrics
duration: 13min
completed: 2026-03-28
---

# Phase 47 Plan 01: Array and Hashtable Indexing Syntax Summary

**`.[i]` indexing syntax for arrays and hashtables — read, write, and chained forms — with DOTLBRACKET token, IndexGet/IndexSet AST nodes, Bidir type checking, bounds-checked eval, and 7 passing flt tests**

## Performance

- **Duration:** 13 min
- **Started:** 2026-03-27T23:53:52Z
- **Completed:** 2026-03-28T00:07:00Z
- **Tasks:** 3
- **Files modified:** 10 source files + 7 new test files

## Accomplishments

- Added `.[` lexed as single `DOTLBRACKET` token (same strategy as F# compiler) to prevent LALR shift/reduce conflict
- `arr.[i]` / `ht.[key]` read and `arr.[i] <- v` / `ht.[key] <- v` write syntax fully working
- Chained indexing `matrix.[r].[c]` works left-associatively via left-recursive `Atom` grammar rule
- Type checker dispatches on TArray (index must be int) vs THashtable (index must match key type), with E0471 on non-collection
- Runtime evaluation with bounds checking (array index out of bounds) and key-not-found (hashtable)
- 570/570 flt tests pass with zero regressions; 224/224 unit tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Lexer + Parser + AST + Diagnostic + IndentFilter** - `1cdcffc` (feat)
2. **Task 2: Bidir, Infer, Eval, Format, TypeCheck** - `a77412e` (feat)
3. **Task 3: 7 flt integration tests** - `055cd47` (test)

## Files Created/Modified

- `src/LangThree/Lexer.fsl` - Added `".[" { DOTLBRACKET }` rule before `..` and `.` rules
- `src/LangThree/Parser.fsy` - `%token DOTLBRACKET`, `IndexGet` in `Atom`, `IndexSet` in `Expr`
- `src/LangThree/Ast.fs` - `IndexGet`/`IndexSet` Expr variants and `spanOf` arms
- `src/LangThree/Diagnostic.fs` - `IndexOnNonCollection` error kind E0471 with format
- `src/LangThree/IndentFilter.fs` - `DOTLBRACKET` added to bracket depth tracking
- `src/LangThree/Bidir.fs` - Type checking: TArray/THashtable dispatch for IndexGet and IndexSet
- `src/LangThree/Infer.fs` - Stub `| IndexGet _ | IndexSet _ -> (empty, freshVar())`
- `src/LangThree/Eval.fs` - Bounds-checked array indexing, key-not-found hashtable indexing
- `src/LangThree/Format.fs` - `formatAst` and `formatToken` arms for new nodes/token
- `src/LangThree/TypeCheck.fs` - `IndexGet`/`IndexSet` in collectMatches, collectTryWiths, collectModuleRefs, rewriteModuleAccess
- `tests/flt/expr/indexing/` - 7 new flt tests (IDX-01 through IDX-05 + error tests)

## Decisions Made

- **DOTLBRACKET token:** Same strategy as F# compiler — lex `.[` as a single token to prevent LALR conflict with `Atom DOT IDENT` (field access). Without this, `Atom DOT` creates an ambiguous lookahead between `LBRACKET` (index) and `IDENT` (field).
- **IndexGet/IndexSet AST nodes:** Chose Pattern 4 (AST nodes with Bidir type checking) over parser-level desugaring to builtin calls. This enables proper type error messages (E0471 with type info) rather than cryptic unification errors.
- **array_create / hashtable_create:** The actual builtin names are `array_create` and `hashtable_create` (not `array_new`/`hashtable_new` as mentioned in plan notes) — verified from Eval.fs source.

## Deviations from Plan

None - plan executed exactly as written. The plan notes correctly flagged `array_create` vs `array_new` discrepancy; actual builtins confirmed before writing tests.

## Issues Encountered

None — the build produced zero errors and zero warnings after Task 2 was complete. The DOTLBRACKET token was unavailable in IndentFilter.fs IDE diagnostics until after the first build (expected, since the parser must be regenerated), but the file compiled correctly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `.[i]` indexing syntax complete — users can read and write arrays and hashtables ergonomically
- Phase 47 complete; phases 45 (SeqExpr), 46 (loops), and 47 (indexing) form the full v5.0 imperative ergonomics layer
- Ready for phase 48 (remaining v5.0 phases per ROADMAP)

---
*Phase: 47-array-hashtable-indexing*
*Completed: 2026-03-28*
