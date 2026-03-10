---
phase: 06
plan: 01
subsystem: exceptions
tags: [ast, type, diagnostic, parser, lexer, match-clause, migration]
dependency-graph:
  requires: [05]
  provides: [exception-ast-nodes, texn-type, e06xx-diagnostics, match-clause-3-tuple, exception-parser-rules]
  affects: [06-02, 06-03]
tech-stack:
  added: []
  patterns: [match-clause-when-guard, exception-declaration]
key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Type.fs
    - src/LangThree/Diagnostic.fs
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Bidir.fs
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Infer.fs
    - src/LangThree/Format.fs
decisions: []
metrics:
  duration: 5min
  completed: 2026-03-09
---

# Phase 6 Plan 01: Exception Foundation and MatchClause Migration Summary

**One-liner:** Cross-cutting MatchClause 3-tuple migration with Raise/TryWith/ExceptionDecl AST nodes, TExn type, E06xx diagnostics, and full parser support for exceptions and when guards.

## What Was Done

### Task 1: AST, Type, and Diagnostic Extensions
- Changed `MatchClause` from `Pattern * Expr` to `Pattern * Expr option * Expr` (pattern, when-guard, body)
- Added `Raise`, `TryWith` Expr variants and `ExceptionDecl` Decl variant
- Added `TExn` type variant with full support in `formatType`, `formatTypeNormalized`, `apply`, `freeVars`
- Added E0601-E0604 error kinds and W0003 warning for exception diagnostics

### Task 2: Lexer and Parser
- Added `exception`, `raise`, `try`, `when` keyword tokens to Lexer
- Added `EXCEPTION`, `RAISE`, `TRY`, `WHEN` token declarations to Parser
- Updated `MatchClauses` grammar to produce 3-tuples with optional when guards
- Added exception declaration rules in Decls (singleton/continuation pattern)
- Added `raise Atom` in Factor and `try ... with ...` in Expr

### Task 3: MatchClause 3-tuple Migration
- Updated all 10+ destructuring sites across Bidir.fs, Eval.fs, TypeCheck.fs, Infer.fs, Format.fs
- Added Raise/TryWith stubs in Bidir.fs synth/check (failwith "TODO")
- Added Raise/TryWith stubs in Eval.fs eval (failwith "TODO")
- Added ExceptionDecl passthrough in TypeCheck.fs typeCheckDecls
- Added Raise/TryWith cases in collectMatches, collectModuleRefs, rewriteModuleAccess
- Added exception token formatting in Format.fs

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | 2a8c0f8 | AST, Type, Diagnostic extensions |
| 2 | 60028b1 | Lexer/Parser rules for exceptions and when guards |
| 3 | afbf6dc | MatchClause 3-tuple migration and exception stubs |

## Verification

- Project compiles with zero errors
- All 149 existing tests pass (zero regressions)
- MatchClause is `Pattern * Expr option * Expr` everywhere
- No remaining `Pattern * Expr` (2-tuple) MatchClause definitions

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

Plan 02 (type checking) and Plan 03 (evaluation) can now build on:
- `Raise` and `TryWith` AST nodes (currently `failwith "TODO"` stubs)
- `ExceptionDecl` declaration (currently passthrough in typeCheckDecls)
- `TExn` type for exception type checking
- E0601-E0604 diagnostics ready for use
- When guard slot in MatchClause ready for semantic evaluation
