---
phase: 02-algebraic-data-types
plan: 05
subsystem: evaluator
tags: [ADT, constructor, pattern-matching, evaluation, DataValue]

# Dependency graph
requires:
  - phase: 00-bootstrap
    provides: Eval.fs with matchPattern and eval functions
  - phase: 01-indentation-based-syntax
    provides: Module parsing with indentation support
provides:
  - Constructor expression in Expr AST
  - DataValue variant in Value for runtime ADT representation
  - ConstructorPat in Pattern for pattern matching
  - Evaluator support for ADT construction and pattern matching
  - 8 integration tests covering ADT evaluation
affects: [02-01 through 02-04 (AST/type infrastructure), 03-records, 04-gadts]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Uppercase-first IDENT disambiguation for constructors vs variables in parser
    - Constructor application special case in AppExpr
    - DataValue carries optional argument for nullary vs unary constructors

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Parser.fs
    - src/LangThree/Lexer.fs
    - src/LangThree/Format.fs
    - src/LangThree/Infer.fs
    - tests/LangThree.Tests/IntegrationTests.fs

key-decisions:
  - "Uppercase IDENT parsed as Constructor, lowercase as Var - simple lexer-level disambiguation"
  - "Constructor takes optional single argument (nullary: None, unary: Some x) - tuple arg for multi-field"
  - "Tests skip type checking since ADT type infrastructure not yet complete (parallel plan dependency)"
  - "Added stub ConstructorPat/Constructor handling in Infer.fs to avoid compilation errors"

patterns-established:
  - "Constructor application in AppExpr: Constructor(name, None) + Atom -> Constructor(name, Some atom)"
  - "ConstructorPat in Pattern grammar: IDENT Pattern for constructor with argument"
  - "DataValue matching in evalMatchClauses via extended matchPattern"

# Metrics
duration: 8min
completed: 2026-03-09
---

# Phase 02 Plan 05: Runtime Evaluation of ADT Values Summary

**Constructor expr/DataValue evaluation with pattern matching via uppercase IDENT disambiguation**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-09T01:06:40Z
- **Completed:** 2026-03-09T01:14:40Z
- **Tasks:** 3
- **Files modified:** 8

## Accomplishments
- Constructor expression added to Expr AST with optional argument
- DataValue variant added to Value for runtime ADT representation
- ConstructorPat added to Pattern for ADT pattern matching
- Parser distinguishes uppercase IDENT as constructors, lowercase as variables
- Evaluator constructs DataValue and matches ConstructorPat correctly
- Recursive ADT values (trees) evaluate correctly with let rec
- Nested constructors (Some (Some 1)) work
- 8 new integration tests all pass, 42 total tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Constructor expr, ConstructorPat, DataValue to AST and parser** - `d998633` (feat)
   - Constructor variant in Expr, ConstructorPat in Pattern, DataValue in Value
   - Parser uses System.Char.IsUpper for IDENT disambiguation
   - AppExpr handles constructor application as special case
   - Pattern grammar supports IDENT Pattern for constructor with arg
   - Updated spanOf, patternSpanOf, Format.fs formatAst/formatPattern
   - Added stub handling in Infer.fs for compilation compatibility

2. **Task 2: Extend evaluator for ADT construction and pattern matching** - `c6996d7` (feat)
   - Constructor expr evaluates to DataValue with optional argument
   - matchPattern handles ConstructorPat vs DataValue matching
   - formatValue displays DataValue (nullary: "None", with arg: "Some 42")

3. **Task 3: Add comprehensive ADT evaluation integration tests** - `da0492f` (test)
   - 8 tests: nullary constructor, constructor with arg, pattern matching (nullary/data)
   - Recursive tree construction and evaluation with let rec
   - Nested constructors (Some (Some 1))
   - formatValue unit tests for DataValue display
   - All 42 tests pass (8 new + 34 existing)

## Files Created/Modified
- `src/LangThree/Ast.fs` - Constructor in Expr, ConstructorPat in Pattern, DataValue in Value
- `src/LangThree/Eval.fs` - Constructor evaluation, ConstructorPat matching, DataValue formatting
- `src/LangThree/Parser.fsy` - Uppercase IDENT disambiguation, constructor application in AppExpr
- `src/LangThree/Parser.fs` - Regenerated from Parser.fsy
- `src/LangThree/Lexer.fs` - Regenerated
- `src/LangThree/Format.fs` - formatAst/formatPattern for Constructor/ConstructorPat
- `src/LangThree/Infer.fs` - Stub inferPattern/inferWithContext for ConstructorPat/Constructor
- `tests/LangThree.Tests/IntegrationTests.fs` - 8 ADT evaluation integration tests

## Decisions Made
- **Uppercase IDENT disambiguation:** Simple approach where any IDENT starting with uppercase is parsed as a Constructor. This mirrors F#/OCaml conventions and avoids needing a separate token type or constructor environment at parse time.
- **Constructor takes optional single argument:** Nullary (None, Leaf) has `arg: None`, unary (Some 42) has `arg: Some expr`. Multi-field constructors use tuple argument: Node (Leaf, 10, Leaf) passes a tuple.
- **Tests skip type checking:** ADT type infrastructure (ConstructorEnv, TData, elaborateTypeDecl) is being built in parallel plans 02-01 through 02-03. Tests validate parse + eval only.
- **Stub handling in Infer.fs:** Added minimal ConstructorPat/Constructor cases returning fresh type variables to avoid compilation errors. Full type inference for ADTs will come from other plans.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing] Added ConstructorPat/Constructor stubs in Infer.fs**
- **Found during:** Task 1
- **Issue:** Adding Constructor to Expr and ConstructorPat to Pattern caused incomplete pattern match warnings in Infer.fs (which was not listed in the plan's files)
- **Fix:** Added minimal stub handling returning fresh type variables
- **Files modified:** src/LangThree/Infer.fs

**2. [Rule 2 - Adaptation] Tests skip type checking**
- **Found during:** Task 3
- **Issue:** Plan's test code references TypeCheck.typeCheckModule which doesn't exist; type checker doesn't support ADTs yet
- **Fix:** Tests use direct parse + eval pipeline (parseAndEvalModule helper) without type checking
- **Files modified:** tests/LangThree.Tests/IntegrationTests.fs

**3. [Rule 1 - Bug] Test input formatting for indentation-sensitive parsing**
- **Found during:** Task 3
- **Issue:** Multi-line test inputs with nested match expressions triggered IndentationError due to pipe alignment requirements
- **Fix:** Used single-line match expressions in tests or let rec..in expressions to avoid indentation complexity
- **Files modified:** tests/LangThree.Tests/IntegrationTests.fs

## Issues Encountered

**Bidir.fs warning:** Adding Constructor to Expr causes an incomplete pattern match warning in Bidir.fs. This file cannot be modified per parallel execution constraints (02-04 modifies it). The warning is benign -- Constructor expressions will cause a runtime match failure if type-checked before Bidir.fs is updated with Constructor handling.

**Module-level let rec:** The parser's Decl grammar does not support `let rec` at module level (only as an expression with `in`). This required restructuring recursive tree tests to use `let rec ... in` expression form within a `let result = ...` declaration.

## Next Phase Readiness

ADT runtime evaluation is complete. The following remain for full Phase 2 completion:
- Plans 02-01 through 02-03: AST/parser for type declarations, type checker with ConstructorEnv/TData
- Plan 02-04: Exhaustiveness checking for pattern matching
- Bidir.fs needs Constructor handling added (will be done in 02-03 or 02-04)
- Module-level `let rec` support (future enhancement)

Current test suite: 42 passing tests (8 new ADT evaluation tests).

---
*Phase: 02-algebraic-data-types*
*Completed: 2026-03-09*
