---
phase: 08-full-coverage-fslit-testing
plan: 03
subsystem: testing
tags: [fslit, emit-ast, declarations, ADT, GADT, records, modules, exceptions]

# Dependency graph
requires:
  - phase: 01-04
    provides: Module-level declarations (LetDecl, TypeDecl)
  - phase: 03
    provides: Record types and operations
  - phase: 04
    provides: GADT declarations
  - phase: 05
    provides: Module system (ModuleDecl, NamespaceDecl, OpenDecl)
  - phase: 06
    provides: Exception system (ExceptionDecl, TryWith, Raise)
  - phase: 08-01
    provides: fslit test infrastructure and file-mode format
provides:
  - 16 --emit-ast file-mode tests for all declaration AST node types
  - Coverage of LetDecl, TypeDecl, RecordDecl, ExceptionDecl, ModuleDecl, OpenDecl, NamespaceDecl
  - Coverage of RecordExpr, FieldAccess, RecordUpdate, Match, TryWith in file mode
affects: [08-04, 08-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "file-mode %input tests for declaration-level AST verification"

key-files:
  created:
    - tests/flt/emit/ast-decl-let.flt
    - tests/flt/emit/ast-decl-func.flt
    - tests/flt/emit/ast-decl-adt-simple.flt
    - tests/flt/emit/ast-decl-adt-parametric.flt
    - tests/flt/emit/ast-decl-adt-mutual.flt
    - tests/flt/emit/ast-decl-gadt.flt
    - tests/flt/emit/ast-decl-record.flt
    - tests/flt/emit/ast-decl-record-mutable.flt
    - tests/flt/emit/ast-decl-record-ops.flt
    - tests/flt/emit/ast-decl-exception.flt
    - tests/flt/emit/ast-decl-exception-data.flt
    - tests/flt/emit/ast-decl-module.flt
    - tests/flt/emit/ast-decl-namespace.flt
    - tests/flt/emit/ast-decl-open.flt
    - tests/flt/emit/ast-decl-match.flt
    - tests/flt/emit/ast-decl-trywith.flt
  modified: []

key-decisions:
  - "Combined Task 1 (capture) and Task 2 (create files) into single commit since Task 1 produces no files"

patterns-established:
  - "ast-decl-*.flt naming convention for declaration-level AST tests"

# Metrics
duration: 2min
completed: 2026-03-10
---

# Phase 08 Plan 03: Declaration AST Emit Tests Summary

**16 file-mode --emit-ast tests covering all declaration node types: let, func, ADT (simple/parametric/mutual), GADT, record (basic/mutable/ops), exception (simple/data), module, namespace, open, match, try-with**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-10T05:54:11Z
- **Completed:** 2026-03-10T05:56:27Z
- **Tasks:** 2
- **Files created:** 16

## Accomplishments
- Every declaration AST node type has at least one --emit-ast file-mode test
- Record operations (create, field access, copy-update) tested in ast-decl-record-ops.flt
- Module system (module, namespace, open) each tested individually
- Exception system (simple declare, with-data declare, try-with handling) fully covered
- GADT declaration format with return type annotations verified
- All 16 tests pass via fslit; no regressions in existing test suite

## Task Commits

Each task was committed atomically:

1. **Tasks 1+2: Capture AST output and create .flt test files** - `1ca21d1` (test)

**Plan metadata:** [pending] (docs: complete plan)

## Files Created/Modified
- `tests/flt/emit/ast-decl-let.flt` - Simple let binding LetDecl AST
- `tests/flt/emit/ast-decl-func.flt` - Function declaration with lambda desugaring
- `tests/flt/emit/ast-decl-adt-simple.flt` - Simple ADT with constructor usage
- `tests/flt/emit/ast-decl-adt-parametric.flt` - Parametric ADT with type variable
- `tests/flt/emit/ast-decl-adt-mutual.flt` - Mutually recursive type declarations
- `tests/flt/emit/ast-decl-gadt.flt` - GADT with return type annotations
- `tests/flt/emit/ast-decl-record.flt` - Record type declaration and creation
- `tests/flt/emit/ast-decl-record-mutable.flt` - Record with mutable field
- `tests/flt/emit/ast-decl-record-ops.flt` - Record field access and copy-update
- `tests/flt/emit/ast-decl-exception.flt` - Simple exception declaration
- `tests/flt/emit/ast-decl-exception-data.flt` - Exception with data payload
- `tests/flt/emit/ast-decl-module.flt` - Module declaration with nested let
- `tests/flt/emit/ast-decl-namespace.flt` - Namespace declaration
- `tests/flt/emit/ast-decl-open.flt` - Module with open directive
- `tests/flt/emit/ast-decl-match.flt` - Match with constructor patterns (file mode)
- `tests/flt/emit/ast-decl-trywith.flt` - Try-with exception handling

## Decisions Made
- Combined exploration (Task 1) and file creation (Task 2) into single commit since Task 1 produces no artifacts
- Used pipe-based input for AST capture (`echo "source" | LangThree --emit-ast /dev/stdin`)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- 3 pre-existing failing tests found in emit/ directory (ast-expr-tuple.flt, ast-expr-literals-string.flt, ast-expr-match.flt) -- these are untracked files from a previous session, not related to this plan
- fslit only accepts a single file or directory argument (not multiple file arguments); ran tests individually and via directory

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All declaration AST node types now have emit tests
- Ready for 08-04 (type emission tests) and 08-05 (remaining coverage)
- Pre-existing broken test files in emit/ directory should be cleaned up

---
*Phase: 08-full-coverage-fslit-testing*
*Completed: 2026-03-10*
