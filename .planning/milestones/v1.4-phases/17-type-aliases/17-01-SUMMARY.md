---
phase: 17-type-aliases
plan: "01"
title: "Type Alias Implementation"
subsystem: parser-typechecker
tags: [type-alias, parser, transparent-types]
dependency-graph:
  requires: []
  provides: [type-alias-syntax, transparent-alias-expansion]
  affects: []
tech-stack:
  added: []
  patterns: [transparent-type-alias, alias-grammar-disambiguation]
key-files:
  created:
    - tests/flt/file/alias-simple.flt
    - tests/flt/file/alias-tuple.flt
    - tests/flt/file/alias-function.flt
    - tests/flt/file/alias-list.flt
    - tests/flt/emit/type-decl/type-decl-alias.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Format.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs
decisions:
  - id: D17-01
    title: "Transparent aliases via AliasTypeExpr grammar"
    context: "Need LALR(1)-safe way to distinguish type aliases from ADT declarations"
    choice: "Separate AliasTypeExpr grammar that excludes bare IDENT (which starts ADT constructors)"
    alternatives: ["Lookahead-based disambiguation", "Unified TypeExpr with semantic check"]
  - id: D17-02
    title: "Aliases as no-op in type checker and evaluator"
    context: "Type aliases are transparent -- they expand to underlying types"
    choice: "TypeAliasDecl is a no-op; TEName elaborates to fresh TVar which unifies naturally"
    alternatives: ["Explicit alias environment with substitution pass"]
metrics:
  duration: "4 min"
  completed: "2026-03-19"
---

# Phase 17 Plan 01: Type Alias Implementation Summary

Transparent type alias syntax (`type Name = string`, `type IntPair = int * int`) with zero-overhead type checking.

## What Was Built

### Parser (Task 1)
- Added `TypeAliasDecl` variant to `Decl` DU in Ast.fs
- Created `AliasTypeExpr` grammar rules that match type expressions starting with lowercase keywords (int, bool, string, unit) or type variables -- cleanly avoiding LALR(1) conflicts with ADT constructor rules
- Added Format.fs support for TypeAliasDecl display

### Type Checking (Task 2)
- TypeAliasDecl is a transparent no-op in typeCheckDecls
- TypeAliasDecl is a no-op in evalModuleDecls (no runtime behavior)
- The elaborator's existing TEName -> fresh TVar behavior means alias names in annotations unify naturally with their underlying types

### Testing (Tasks 3-4)
- All 196 F# tests pass
- All 329 fslit tests pass (324 existing + 5 new)
- Manual verification: string, tuple, function, and list aliases all work
- `--emit-type` correctly shows underlying types (not alias names)

## Decisions Made

1. **AliasTypeExpr grammar disambiguation (D17-01):** Created separate AliasTypeExpr, AliasArrowType, AliasTupleType, AliasAtomicType nonterminals that mirror the regular TypeExpr grammar but exclude bare IDENT (uppercase identifiers). This cleanly avoids LALR(1) conflicts since ADT constructor rules start with uppercase IDENT while alias type expressions start with lowercase type keywords.

2. **No-op transparent expansion (D17-02):** Rather than building an explicit alias environment and substitution pass, leveraged the existing elaboration behavior where TEName produces a fresh TVar that unifies with the actual type at use sites. This zero-code approach means aliases are truly transparent with no runtime or type-checking overhead.

## Deviations from Plan

None -- plan executed exactly as written.

## Test Results

| Suite | Before | After | Delta |
|-------|--------|-------|-------|
| F# unit tests | 196 | 196 | +0 |
| fslit tests | 324 | 329 | +5 |
| **Total** | **520** | **525** | **+5** |

## Commits

| Hash | Message |
|------|---------|
| 390aaf2 | feat(17-01): add TypeAliasDecl to AST and parser |
| 960d069 | feat(17-01): implement type alias elaboration |
| 9933158 | chore(17-01): verify build and all 520 tests pass |
| faa853f | test(17-01): add fslit tests for type aliases |
