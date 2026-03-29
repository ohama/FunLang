---
phase: 58-language-constructs
plan: "01"
subsystem: language-syntax
tags: [ast, parser, string-slicing, list-comprehension, grammar]

dependency-graph:
  requires: []
  provides: [StringSliceExpr AST node, ListCompExpr AST node, string slice grammar rules, list comprehension grammar rules]
  affects: [58-02]

tech-stack:
  added: []
  patterns: [lalr-grammar-extension, ast-node-addition, spanOf-extension]

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy

decisions:
  - id: listcomp-range-desugaring
    choice: "Desugar [for i in start..stop -> body] directly in parser action to ListCompExpr(i, Range(start, stop, None), body)"
    reason: "Range already evaluates to ListValue in Eval.fs, so no extra machinery needed for range-based comprehensions"

metrics:
  duration: ~5 minutes
  completed: 2026-03-29
---

# Phase 58 Plan 01: String Slicing and List Comprehension AST/Parser Summary

**One-liner:** StringSliceExpr and ListCompExpr AST nodes with four new LALR grammar rules for `s.[start..stop]`, `s.[start..]`, `[for x in coll -> body]`, and `[for i in start..stop -> body]`.

## What Was Built

Added syntactic foundation for two new language features:

1. **Ast.fs** — Two new `Expr` DU cases:
   - `StringSliceExpr of str: Expr * start: Expr * stop: Expr option * span: Span` — represents `s.[start..stop]` (bounded) and `s.[start..]` (open-ended)
   - `ListCompExpr of var: string * collection: Expr * body: Expr * span: Span` — represents `[for x in coll -> body]`
   - Both cases handled in `spanOf` function.

2. **Parser.fsy** — Four new grammar rules added to the `Atom` production:
   - String slicing (bounded): `Atom DOTLBRACKET Expr DOTDOT Expr RBRACKET` → `StringSliceExpr($1, $3, Some $5, ...)`
   - String slicing (open-ended): `Atom DOTLBRACKET Expr DOTDOT RBRACKET` → `StringSliceExpr($1, $3, None, ...)`
   - List comprehension over collection: `LBRACKET FOR IDENT IN Expr ARROW Expr RBRACKET` → `ListCompExpr($3, $5, $7, ...)`
   - List comprehension over range: `LBRACKET FOR IDENT IN Expr DOTDOT Expr ARROW Expr RBRACKET` → `ListCompExpr($3, Range($5, $7, None, ...), $9, ...)`

   The LALR disambiguation is natural: after `Atom DOTLBRACKET Expr`, lookahead `RBRACKET` reduces as `IndexGet`, lookahead `DOTDOT` shifts into the slice rules.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add StringSliceExpr and ListCompExpr to Ast.fs | 65c155b | src/LangThree/Ast.fs |
| 2 | Add parser grammar rules for string slicing and list comprehension | ee4204c | src/LangThree/Parser.fsy |

## Verification

Build: `dotnet build src/LangThree/LangThree.fsproj -c Release`
- 0 errors
- 5 warnings (FS0025 incomplete pattern match in Eval.fs, Bidir.fs, TypeCheck.fs, Infer.fs, Format.fs — expected, will be resolved in Plan 02)
- No new LALR shift/reduce or reduce/reduce conflicts introduced

All LALR conflicts in build output are pre-existing (INDENT/DEDENT ambiguity and FUN ARROW INDENT SeqExpr DEDENT ambiguity that existed before Phase 58).

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Range desugaring location | Parser action | Desugaring `start..stop` to `Range(...)` in the parser action is simpler than a separate pass; Range already evaluates to a ListValue |
| StringSliceExpr `stop` field | `Expr option` | `None` for open-ended slice `s.[start..]`; `Some end` for bounded slice `s.[start..stop]` |

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

- Phase 58 Plan 02 (Eval.fs + Bidir.fs implementation) can proceed immediately
- Both AST nodes are fully defined; Plan 02 will add eval and type-check arms
- The 5 FS0025 warnings are the "TODO" markers that Plan 02 will resolve
