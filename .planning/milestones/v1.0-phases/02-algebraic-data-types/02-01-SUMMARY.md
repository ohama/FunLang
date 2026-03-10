---
phase: 02-algebraic-data-types
plan: 01
subsystem: parser
tags: [adt, discriminated-union, parser, lexer, fslexyacc]

# Dependency graph
requires:
  - phase: 01-indentation-based-syntax
    provides: "IndentFilter, module-level declarations, INDENT/DEDENT tokens"
provides:
  - "TypeDecl and ConstructorDecl AST nodes"
  - "TYPE, OF, AND_KW lexer tokens"
  - "Type declaration grammar rules with indentation support"
  - "TEName variant for named type references"
affects: [02-02, 02-03, 02-04, 02-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TypeDecl/ConstructorDecl in and-chain with Expr/TypeExpr"
    - "TypeDeclContinuation for mutually recursive type declarations"

key-files:
  created: []
  modified:
    - "src/LangThree/Ast.fs"
    - "src/LangThree/Lexer.fsl"
    - "src/LangThree/Parser.fsy"
    - "src/LangThree/Format.fs"
    - "src/LangThree/Elaborate.fs"
    - "tests/LangThree.Tests/IntegrationTests.fs"

key-decisions:
  - "AND_KW token name (not AND) to avoid conflict with existing && operator token"
  - "TypeDeclContinuation uses IDENT directly (not TYPE keyword) matching F# syntax: and U = ..."
  - "ConstructorDecl type name (not Constructor) to avoid naming conflicts"
  - "TEName variant added to TypeExpr for named type references (Tree, Option, etc.)"

patterns-established:
  - "Type declarations support both inline and indented constructor lists"
  - "Mutually recursive types: type T = ... and U = ... (and-continuation without type keyword)"
  - "extractTypeDecl test helper for clean pattern matching on Decl.TypeDecl"

# Metrics
duration: 9min
completed: 2026-03-09
---

# Phase 2 Plan 1: Type Declaration Parsing Summary

**F# discriminated union syntax parsing with TypeDecl/ConstructorDecl AST nodes, TYPE/OF/AND_KW lexer tokens, and grammar rules supporting inline, indented, and mutually recursive type declarations**

## Performance

- **Duration:** 9 min
- **Started:** 2026-03-09T00:43:06Z
- **Completed:** 2026-03-09T00:51:42Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- TypeDecl and ConstructorDecl types integrated into AST and-chain
- Parser accepts `type T = C1 | C2 of ty` with type parameters, recursion, and mutual recursion
- 6 integration tests covering simple, data, parametric, recursive, mutual recursive, and leading-pipe variants
- All 40 tests pass (34 existing + 6 new)

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend AST with TypeDecl and Constructor nodes** - `1f36c60` (feat)
2. **Task 2: Add TYPE, OF, AND keywords to lexer and extend parser grammar** - `269c9a2` (feat)
3. **Task 3: Add integration tests for type declaration parsing** - `1ed3e0d` (test)

## Files Created/Modified
- `src/LangThree/Ast.fs` - TypeDecl, ConstructorDecl types; TEName variant; Decl.TypeDecl case
- `src/LangThree/Lexer.fsl` - TYPE, OF, AND_KW keyword tokens
- `src/LangThree/Parser.fsy` - TypeDeclaration, Constructors, TypeDeclContinuation grammar rules
- `src/LangThree/Format.fs` - Format support for new tokens and TEName
- `src/LangThree/Elaborate.fs` - TEName elaboration placeholder (fresh TVar)
- `tests/LangThree.Tests/IntegrationTests.fs` - 6 type declaration parsing tests

## Decisions Made
- **AND_KW token name:** Used AND_KW instead of AND to avoid conflict with existing `&&` operator token (AND)
- **TypeDeclContinuation without TYPE:** F# mutual recursion uses `and U = ...` not `and type U = ...`, so continuation rule uses IDENT directly
- **TEName for named types:** Added new TypeExpr variant for named type references (like Tree in recursive ADTs), distinct from TEVar (which is for 'a type variables)
- **ConstructorDecl naming:** Used ConstructorDecl to avoid naming collisions between type name and DU case name

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added TEName variant to TypeExpr**
- **Found during:** Task 2 (grammar rules)
- **Issue:** Recursive type declarations (type Tree = Node of Tree * int * Tree) reference named types, but TypeExpr had no IDENT variant
- **Fix:** Added TEName variant to Ast.fs TypeExpr, IDENT rule to AtomicType in Parser.fsy, handling in Format.fs and Elaborate.fs
- **Files modified:** Ast.fs, Parser.fsy, Format.fs, Elaborate.fs
- **Verification:** testParseRecursiveADT passes
- **Committed in:** 269c9a2 (Task 2 commit)

**2. [Rule 3 - Blocking] Fixed TypeDeclContinuation grammar for and-keyword**
- **Found during:** Task 3 (tests)
- **Issue:** TypeDeclContinuation used `AND_KW TypeDeclaration` but F# uses `and U = ...` (no `type` keyword). Token stream showed `AND_KW IDENT EQUALS` not `AND_KW TYPE IDENT EQUALS`
- **Fix:** Changed TypeDeclContinuation to inline `IDENT TypeParams EQUALS Constructors TypeDeclContinuation`
- **Files modified:** Parser.fsy
- **Verification:** testParseMutuallyRecursiveADT passes
- **Committed in:** 1ed3e0d (Task 3 commit)

**3. [Rule 3 - Blocking] Added INDENT/DEDENT handling for indented constructors**
- **Found during:** Task 3 (tests)
- **Issue:** Leading-pipe style (`type T =\n    | A\n    | B`) produced INDENT/DEDENT tokens not handled by grammar
- **Fix:** Added two new TypeDeclaration alternatives with INDENT/DEDENT wrapping constructors
- **Files modified:** Parser.fsy
- **Verification:** testParseADTWithLeadingPipe passes
- **Committed in:** 1ed3e0d (Task 3 commit)

---

**Total deviations:** 3 auto-fixed (1 missing critical, 2 blocking)
**Impact on plan:** All auto-fixes necessary for correctness. No scope creep.

## Issues Encountered
None beyond the deviations documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- TypeDecl AST nodes ready for type system extension (02-02: constructor environment)
- Parser handles all F# discriminated union syntax variants
- TEName variant needs proper type resolution in future type checking phase

---
*Phase: 02-algebraic-data-types*
*Completed: 2026-03-09*
