---
phase: 02-algebraic-data-types
plan: 04
subsystem: type-system
tags: [exhaustiveness, redundancy, pattern-matching, maranget, tdd]

dependency-graph:
  requires: []
  provides: [exhaustiveness-checking, redundancy-checking, pattern-matrix-algorithm]
  affects: [02-05, phase-03, phase-04]

tech-stack:
  added: []
  patterns: [maranget-usefulness, pattern-matrix-specialization, witness-construction]

key-files:
  created:
    - src/LangThree/Exhaustive.fs
    - tests/LangThree.Tests/ExhaustiveTests.fs
  modified:
    - src/LangThree/LangThree.fsproj
    - tests/LangThree.Tests/LangThree.Tests.fsproj

decisions:
  - id: "02-04-01"
    decision: "Self-contained CasePat type instead of depending on AST Pattern"
    rationale: "Decouples exhaustiveness analysis from AST evolution; parallel execution with 02-05 requires no Ast.fs modifications"
  - id: "02-04-02"
    decision: "Constructor set passed explicitly rather than global registry"
    rationale: "Functional style, testable without type system integration; getConstructors deferred to TData integration"

metrics:
  duration: "3 min"
  completed: "2026-03-09"
---

# Phase 2 Plan 4: Pattern Exhaustiveness Checking Summary

Self-contained Maranget usefulness algorithm with CasePat abstract pattern type, 30 TDD tests across usefulness/exhaustiveness/redundancy/formatting/specialization

## TDD Execution

### RED Phase (d2561dc)
- Created Exhaustive.fs stub with type definitions and failwith implementations
- Created ExhaustiveTests.fs with 30 test cases across 5 test groups:
  - Usefulness (7 tests): core algorithm, wildcards, nested constructors
  - Exhaustiveness (9 tests): Option, Tree, Bool types, wildcard coverage, nested patterns
  - Redundancy (5 tests): duplicate detection, wildcard-after-coverage, post-wildcard
  - PatternFormat (5 tests): nullary, unary, nested, wildcard, multi-arg formatting
  - SpecializeMatrix (4 tests): filtering, wildcard expansion, empty matrix, multi-arg
- All 30 tests failed with "Not implemented"

### GREEN Phase (a78d5be)
- Implemented complete Maranget usefulness algorithm:
  - `specializeRow`/`specializeMatrix`: row-level constructor matching and argument expansion
  - `defaultMatrix`: wildcard-first-column extraction
  - `headConstructors`: first-column constructor enumeration
  - `useful`: recursive usefulness with complete/incomplete signature branching
  - `buildMissingWitness`: concrete missing pattern construction for error messages
  - `checkExhaustive`: wildcard usefulness test with witness generation
  - `checkRedundant`: per-pattern usefulness against predecessors
  - `formatPattern`: human-readable display with parenthesization rules
- All 30 tests passed

### REFACTOR Phase (8619192)
- Extracted `isCompleteSignature` helper for readability
- Extracted `lookupConstructor` helper for centralized constructor resolution
- Reduced nesting in `useful` function
- All 30 tests still passed

## Architecture

The module defines its own `CasePat` type independent of `Ast.Pattern`:
- `ConstructorPat(name, args)` for ADT constructors
- `WildcardPat` for catch-all patterns
- `OrPat` placeholder for future or-pattern support

Constructor sets (`ConstructorSet = ConstructorInfo list`) are passed explicitly, enabling pure functional testing without type system integration.

## Integration Points

- `getConstructors` stub exists for future TData type integration (02-02)
- Bidir.fs Match case can call `checkExhaustive`/`checkRedundant` after pattern type inference
- Diagnostic.fs can be extended with exhaustiveness/redundancy warning kinds

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] AST types not yet available**
- **Found during:** Initial context analysis
- **Issue:** Plan assumed ConstructorPat/TData/ConstructorEnv from 02-01/02-02/02-03 would exist, but those plans haven't been executed yet
- **Fix:** Designed self-contained CasePat type with explicit constructor sets; deferred integration
- **Files created:** src/LangThree/Exhaustive.fs
- **Impact:** Module is fully testable and functional; integration deferred to when TData types are added

## Next Phase Readiness

- Exhaustive.fs ready for integration when ADT types (02-01/02-02) are implemented
- Conversion functions from `Ast.Pattern` to `CasePat` needed at integration time
- Diagnostic warnings for non-exhaustive/redundant patterns need TypeErrorKind extension
