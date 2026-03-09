---
phase: 02-algebraic-data-types
plan: 03
subsystem: type-system
tags: [adt, pattern-matching, constructor-patterns, type-inference, bidirectional]

# Dependency graph
requires:
  - phase: 02-01
    provides: TypeDecl AST, parser for type declarations, ConstructorDecl
  - phase: 02-02
    provides: TData type, ConstructorInfo/ConstructorEnv, elaborateTypeDecl
provides:
  - ConstructorPat pattern variant in AST
  - Constructor pattern type inference via ConstructorEnv lookup
  - ConstructorEnv threaded through bidirectional type checker
  - Module-level type checking with ADT support (typeCheckModule)
  - Parenthesized pattern parsing for nested constructors
affects: [02-04-constructor-expressions, 02-05-exhaustiveness, 04-gadt]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ConstructorEnv threading through synth/check for ADT-aware type inference"
    - "Uppercase/lowercase pattern disambiguation in parser"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Infer.fs
    - src/LangThree/Bidir.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Diagnostic.fs
    - src/LangThree/Format.fs
    - tests/LangThree.Tests/IntegrationTests.fs

key-decisions:
  - "Uppercase first char = constructor, lowercase = variable in pattern parser"
  - "inferPattern takes ConstructorEnv param (breaking change from old signature)"
  - "synthTopWithCtors added as new entry point alongside backward-compatible synthTop"
  - "typeCheckModule builds ConstructorEnv before type checking let declarations"

patterns-established:
  - "ConstructorEnv threading: all synth/check/inferPattern calls pass ctorEnv"
  - "Fresh type var instantiation per constructor pattern occurrence"

# Metrics
duration: 5min
completed: 2026-03-09
---

# Phase 2 Plan 3: Constructor Pattern Type Checking Summary

**ConstructorPat AST node with ConstructorEnv-driven type inference, module-level ADT type checking, and nested constructor pattern support**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-09T00:58:52Z
- **Completed:** 2026-03-09T01:03:55Z
- **Tasks:** 3
- **Files modified:** 8

## Accomplishments
- ConstructorPat added to Pattern AST with parser support (uppercase = constructor, lowercase = variable)
- Constructor pattern type inference with fresh type variable instantiation and arity validation
- ConstructorEnv threaded through entire bidirectional type checker (synth, check, inferBinaryOp)
- Module-level typeCheckModule builds ConstructorEnv from type declarations before expression checking
- 4 new integration tests: simple ADT match, parametric ADT, nested constructors, arity mismatch

## Task Commits

Each task was committed atomically:

1. **Task 1: Add ConstructorPat to Pattern AST** - `34b7a2b` (feat)
2. **Task 2: Extend Infer.fs and Bidir.fs for constructor pattern type checking** - `7512c36` (feat)
3. **Task 3: Wire ConstructorEnv through module-level type checking and add tests** - `9ccf6df` (feat)

## Files Created/Modified
- `src/LangThree/Ast.fs` - Added ConstructorPat variant to Pattern type, updated patternSpanOf
- `src/LangThree/Parser.fsy` - Uppercase/lowercase pattern disambiguation, parenthesized patterns
- `src/LangThree/Infer.fs` - Extended inferPattern with ConstructorEnv param and ConstructorPat case
- `src/LangThree/Bidir.fs` - Threaded ConstructorEnv through synth/check, added synthTopWithCtors
- `src/LangThree/TypeCheck.fs` - Added typeCheckModule for ADT-aware module type checking
- `src/LangThree/Diagnostic.fs` - Added UnboundConstructor and ArityMismatch error kinds
- `src/LangThree/Format.fs` - Added ConstructorPat formatting
- `tests/LangThree.Tests/IntegrationTests.fs` - 4 new ADT pattern matching tests

## Decisions Made
- Uppercase first character distinguishes constructors from variables in patterns (F#/OCaml convention)
- inferPattern signature changed to accept ConstructorEnv (existing callers pass Map.empty)
- synthTopWithCtors added as separate entry point to avoid breaking existing typecheck/synthTop
- Parenthesized pattern rule added to parser to support nested constructor patterns like `Some (Some x)`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added parenthesized pattern rule to parser**
- **Found during:** Task 3 (integration tests)
- **Issue:** Nested constructor patterns like `Some (Some x)` failed to parse because parser had no rule for `(Pattern)` without commas
- **Fix:** Added `LPAREN Pattern RPAREN { $2 }` rule to Pattern production
- **Files modified:** src/LangThree/Parser.fsy
- **Verification:** testMatchWithNestedConstructors passes
- **Committed in:** 9ccf6df (Task 3 commit)

**2. [Rule 2 - Missing Critical] Added ConstructorPat to Format.fs**
- **Found during:** Task 1 (build warnings)
- **Issue:** Format.fs had incomplete pattern match warning for ConstructorPat
- **Fix:** Added ConstructorPat case to formatPattern function
- **Files modified:** src/LangThree/Format.fs
- **Verification:** Build warning eliminated
- **Committed in:** 34b7a2b (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 missing critical)
**Impact on plan:** Both necessary for correctness. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Constructor patterns type-check correctly with full ConstructorEnv support
- Ready for 02-04 (constructor expressions as values) which will allow creating ADT values
- Ready for 02-05 (exhaustiveness checking) which will verify all constructors are matched

---
*Phase: 02-algebraic-data-types*
*Completed: 2026-03-09*
