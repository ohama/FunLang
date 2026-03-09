---
phase: 03-records
plan: 01
subsystem: type-system
tags: [records, ast, type-env, lexer, parser, elaboration, diagnostics]

dependency_graph:
  requires: [02-06]
  provides: [record-ast-nodes, record-type-env, record-elaboration, record-diagnostics, record-tokens]
  affects: [03-02, 03-03, 03-04, 03-05, 03-06, 03-07]

tech_stack:
  added: []
  patterns:
    - "substTypeExprWithMap shared helper for type/record elaboration"
    - "RecordEnv parallel to ConstructorEnv for record type metadata"

key_files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Type.fs
    - src/LangThree/Elaborate.fs
    - src/LangThree/Diagnostic.fs
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy

decisions:
  - id: "03-01-01"
    decision: "Extract substTypeExprWithMap as module-level helper shared between ADT and record elaboration"
    rationale: "Avoids code duplication, same type expression substitution logic needed for both"

metrics:
  duration: "3 min"
  completed: "2026-03-09"
---

# Phase 3 Plan 01: Record Foundation Types and Tokens Summary

**One-liner:** Record AST nodes, type environment types, elaboration function, diagnostic error kinds, and lexer/parser tokens for { } ; .

## What Was Done

### Task 1: AST nodes, Value variant, and Type.fs record environment types
- Added `RecordFieldDecl` and `RecordDecl` types for record type declarations
- Extended `Expr` with `RecordExpr`, `FieldAccess`, `RecordUpdate` variants
- Extended `Pattern` with `RecordPat` variant
- Extended `Value` with `RecordValue` variant
- Extended `Decl` with `RecordTypeDecl` variant
- Updated `spanOf`, `patternSpanOf`, `declSpanOf` for all new variants
- Added `RecordFieldInfo`, `RecordTypeInfo`, `RecordEnv` to Type.fs
- **Commit:** 7a8369f

### Task 2: Elaborate, Diagnostic, Lexer, and Parser token declarations
- Extracted `substTypeExprWithMap` as shared module-level helper from `elaborateTypeDecl`
- Added `elaborateRecordDecl` function using shared helper
- Added 7 record error kinds to `TypeErrorKind` (E0307-E0313): UnboundField, DuplicateFieldName, MissingFields, ImmutableFieldAssignment, DuplicateRecordField, NotARecord, FieldAccessOnNonRecord
- Added lexer rules for `{`, `}`, `;`, `.` tokens
- Declared `LBRACE`, `RBRACE`, `SEMICOLON`, `DOT` parser tokens
- **Commit:** b9c1a0f

## Deviations from Plan

None - plan executed exactly as written.

## Decisions Made

1. **substTypeExprWithMap shared helper** - Extracted the local `substTypeExpr` function from `elaborateTypeDecl` into a module-level `substTypeExprWithMap` that takes a parameter map. Both `elaborateTypeDecl` and `elaborateRecordDecl` now use this shared helper, eliminating code duplication.

## Verification Results

- `dotnet build` succeeds with 0 errors, 10 warnings (all expected incomplete pattern match warnings)
- All grep checks pass for new types, functions, and tokens

## Next Phase Readiness

All foundation types and tokens are in place. Plan 02 (Parser grammar rules) can proceed to add record expression/pattern grammar rules using the LBRACE/RBRACE/SEMICOLON/DOT tokens declared here.
