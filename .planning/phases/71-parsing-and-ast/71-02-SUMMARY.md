---
phase: 71-parsing-and-ast
plan: "02"
subsystem: testing
tags: [flt, integration-test, emit-ast, typeclass, instance, indentfilter, fsyacc]

# Dependency graph
requires:
  - phase: 71-parsing-and-ast (plan 01)
    provides: TYPECLASS/INSTANCE/FATARROW tokens, TypeClassDecl/InstanceDecl AST nodes, --emit-ast rendering
provides:
  - 5 flt integration tests validating typeclass/instance/constraint parsing end-to-end
  - IndentFilter fix: TYPECLASS/INSTANCE bodies use InModule context (no spurious IN or SEMICOLON)
  - Grammar fix: TypeClassMethod requires leading PIPE (unambiguous multi-method parsing)
affects:
  - 72-type-inference (confirmed syntax for typeclass/instance declarations)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Typeclass method signatures use leading PIPE (| methodName : type) — same disambiguation as ADT constructors"
    - "TYPECLASS/INSTANCE in IndentFilter treated like MODULE — bodies get InModule context, not InExprBlock"

key-files:
  created:
    - tests/flt/file/typeclass/typeclass-parse-basic.flt
    - tests/flt/file/typeclass/typeclass-parse-instance.flt
    - tests/flt/file/typeclass/typeclass-parse-multi-method.flt
    - tests/flt/file/typeclass/typeclass-parse-constrained-annot.flt
    - tests/flt/file/typeclass/typeclass-parse-combined.flt
  modified:
    - src/LangThree/IndentFilter.fs
    - src/LangThree/Parser.fsy

key-decisions:
  - "TypeClassMethod requires leading PIPE (| show : 'a -> string) — IDENT after TypeExpr is ambiguous (TEData type application conflict) but PIPE cannot appear in TypeExpr"
  - "TYPECLASS/INSTANCE tokens set JustSawModule=true in IndentFilter — causes body to push InModule context (not InExprBlock), preventing spurious IN injection for instance method LET and SEMICOLON injection between methods"
  - "One flt file per test case (not multiple tests in one file) — flt format is single-test-per-file"

patterns-established:
  - "New keyword that introduces a declaration body: add to IndentFilter JustSawModule handling if body contains declaration-level LET (not expression-level)"
  - "Grammar disambiguation for method-like items: use leading PIPE as unambiguous separator"

# Metrics
duration: 20min
completed: 2026-03-31
---

# Phase 71 Plan 02: Typeclass Integration Tests Summary

**5 flt integration tests confirming typeclass/instance/constraint syntax parses to correct AST via --emit-ast, plus two parser bugs fixed: IndentFilter InModule context for typeclass/instance bodies and leading-PIPE grammar for unambiguous multi-method parsing**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-03-31T00:20:00Z
- **Completed:** 2026-03-31T00:40:00Z
- **Tasks:** 1 (plus 2 auto-fixed deviations committed together)
- **Files modified:** 7

## Accomplishments
- Created 5 flt integration tests covering typeclass declaration, instance declaration, multi-method typeclass, constrained type annotation, and combined typeclass+instance in one file
- Fixed IndentFilter bug: TYPECLASS/INSTANCE bodies now use InModule context (not InExprBlock), preventing spurious IN token injection for instance method LET declarations
- Fixed grammar bug: TypeClassMethod now requires leading PIPE (| methodName : type) making multi-method typeclass bodies unambiguous for the LALR parser
- All 664 flt tests pass (659 existing + 5 new), all 224 unit tests pass

## Task Commits

1. **Task 1: Create flt integration tests (with auto-fixed parser bugs)** - `8583fb9` (feat)

**Plan metadata:** (docs commit below)

## Files Created/Modified
- `tests/flt/file/typeclass/typeclass-parse-basic.flt` - typeclass Show with single method
- `tests/flt/file/typeclass/typeclass-parse-instance.flt` - instance Show int with one method
- `tests/flt/file/typeclass/typeclass-parse-multi-method.flt` - typeclass Eq with two methods
- `tests/flt/file/typeclass/typeclass-parse-constrained-annot.flt` - constrained type annotation in let
- `tests/flt/file/typeclass/typeclass-parse-combined.flt` - typeclass + instance together
- `src/LangThree/IndentFilter.fs` - TYPECLASS/INSTANCE set JustSawModule=true in both filter variants
- `src/LangThree/Parser.fsy` - TypeClassMethod grammar changed to PIPE IDENT COLON TypeExpr

## Decisions Made
- **Leading PIPE for method signatures**: TypeClassMethod grammar changed from `IDENT COLON TypeExpr` to `PIPE IDENT COLON TypeExpr`. Rationale: `IDENT` can start a type expression (TEName/TEData rules in AtomicType), making multi-method bodies ambiguous. LALR defaults to shift → second method name consumed as type application. PIPE is unambiguous as it cannot appear inside a TypeExpr.
- **TYPECLASS/INSTANCE as InModule context**: IndentFilter was treating typeclass/instance bodies as `InExprBlock` (because last token before NEWLINE→INDENT was EQUALS). This caused (1) SEMICOLON injection between typeclass methods; (2) LET inside instance body treated as block-let with offside rule, injecting spurious IN tokens. Fix: add TYPECLASS/INSTANCE to the JustSawModule=true set, same as MODULE.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] IndentFilter emits spurious IN tokens for instance method LET declarations**
- **Found during:** Task 1 (creating instance test case)
- **Issue:** `instance Show int = \n    let show x = ...` caused parse error. Root cause: EQUALS before NEWLINE→INDENT triggers InExprBlock context. LET inside InExprBlock with depth>1 → blockLet=true → InLetDecl pushed. When DEDENT occurs, InLetDecl fires the offside rule → IN emitted. Parser sees `INSTANCE ... INDENT LET show x = expr DEDENT IN` — spurious IN causes parse failure at top level.
- **Fix:** Add TYPECLASS and INSTANCE token handling in IndentFilter (both `filter` and `filterPositioned` functions) to set `JustSawModule = true`, causing the body to push InModule context (where LET is NOT treated as a block-let and no IN is injected).
- **Files modified:** src/LangThree/IndentFilter.fs
- **Verification:** `instance Show int = let show x = to_string x` now parses to correct AST
- **Committed in:** 8583fb9

**2. [Rule 1 - Bug] LALR shift-reduce: IDENT after TypeExpr consumed as type application in multi-method typeclass**
- **Found during:** Task 1 (creating multi-method typeclass test case)
- **Issue:** `typeclass Eq 'a = | eq : 'a -> 'a -> bool | neq : ...` failed. With InExprBlock (before fix #1), SEMICOLON was injected between methods but grammar had no SEMICOLON separator. After fix #1 (InModule context), same-level NEWLINE produces nothing → tokens are concatenated → LALR parser shifts IDENT(neq) as type application (`AtomicType IDENT → TEData`) rather than reducing to start a new TypeClassMethod. fsyacc has 163 shift/reduce conflicts resolved silently by shift preference.
- **Fix:** Change TypeClassMethod grammar from `IDENT COLON TypeExpr` to `PIPE IDENT COLON TypeExpr`. PIPE cannot appear in a TypeExpr (it's a pattern/ADT separator), so the parser unambiguously starts a new method on PIPE. Same pattern used by ADT constructors.
- **Files modified:** src/LangThree/Parser.fsy
- **Verification:** `typeclass Eq 'a = | eq : 'a -> 'a -> bool | neq : 'a -> 'a -> bool` parses to TypeClassDecl with both methods
- **Committed in:** 8583fb9

---

**Total deviations:** 2 auto-fixed (both Rule 1 - bugs in Plan 01 implementation)
**Impact on plan:** Both bugs were in the Plan 01 parser implementation, surfaced by writing integration tests. No scope creep. Fixes are minimal (4 lines in IndentFilter, 1 grammar rule change).

## Issues Encountered
- flt format is one-test-per-file; initial attempt to put 5 tests in one file failed (runner treats the entire file as a single test case). Split into 5 separate files.

## Next Phase Readiness
- Phase 71 complete: all typeclass/instance/constraint syntax parses correctly, integration tests confirm it
- Phase 72 (type inference) can begin: TypeClassDecl and InstanceDecl AST nodes are stable, --emit-ast renders them correctly
- Note for Phase 72: typeclass method syntax is `| methodName : type` (leading pipe) — update any documentation that showed the old `methodName : type` format

---
*Phase: 71-parsing-and-ast*
*Completed: 2026-03-31*
