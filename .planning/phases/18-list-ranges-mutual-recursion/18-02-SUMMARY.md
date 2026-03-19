---
phase: 18-list-ranges-mutual-recursion
plan: "02"
title: "Mutual Recursive Functions at Module Level"
subsystem: language-core
tags: [mutual-recursion, let-rec, parser, type-inference, evaluation]
dependency-graph:
  requires: []
  provides: [module-level-let-rec, mutual-recursion]
  affects: []
tech-stack:
  added: []
  patterns: [BuiltinValue-based-mutual-env, LetRecDeclaration-parser-rule, monomorphic-pre-binding]
key-files:
  created:
    - tests/flt/file/mutrec-even-odd.flt
    - tests/flt/file/mutrec-single.flt
    - tests/flt/file/mutrec-three.flt
    - tests/flt/emit/type-decl/type-decl-mutrec.flt
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Format.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs
decisions:
  - id: "18-02-01"
    description: "BuiltinValue with shared mutable ref for mutual recursion evaluation"
    rationale: "Immutable Map-based envs cannot create circular references needed for arbitrary-depth mutual recursion. BuiltinValue wrappers close over a shared ref cell that holds the complete mutual env."
  - id: "18-02-02"
    description: "LetRecDeclaration as separate parser non-terminal with LetRecContinuation"
    rationale: "Mirrors TypeDeclaration/TypeDeclContinuation pattern. Empty LetRecContinuation production avoids shift-reduce conflicts."
  - id: "18-02-03"
    description: "Multi-param functions desugared to nested lambdas in parser"
    rationale: "Consistent with LetDecl function declarations. Single param stored in LetRecDecl binding tuple."
metrics:
  duration: "18 min"
  completed: "2026-03-19"
---

# Phase 18 Plan 02: Mutual Recursive Functions at Module Level Summary

Module-level `let rec` with `and`-based mutual recursion using BuiltinValue closures over shared mutable env ref.

## What Was Built

Added `LetRecDecl` to the language, enabling both single self-recursive functions and mutual recursion at module level:

```
let rec fact n = if n <= 1 then 1 else n * fact (n - 1)

let rec even n = if n = 0 then true else odd (n - 1)
and odd n = if n = 0 then false else even (n - 1)
```

### Components

1. **AST** (`Ast.fs`): `LetRecDecl of bindings: (string * string * Expr * Span) list * Span` added to `Decl` DU.

2. **Parser** (`Parser.fsy`): `LetRecDeclaration` non-terminal with `LetRecContinuation` (mirrors TypeDeclContinuation). Multi-param functions desugared to nested lambdas. Uses existing `AND_KW` token.

3. **Type Checking** (`TypeCheck.fs`): Monomorphic pre-binding of all functions with fresh type variables, simultaneous body checking, post-group generalization. Standard ML/F# mutual recursion algorithm.

4. **Evaluation** (`Eval.fs`): BuiltinValue wrappers close over a shared `ref Env`. All mutual functions are registered in the shared env, then the ref is updated to the complete env. This creates true circular references for arbitrary recursion depth.

5. **Format** (`Format.fs`): `LetRecDecl` formatting support.

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| 18-02-01 | BuiltinValue + shared ref for mutual eval | Immutable maps cannot create circular refs; ref cell provides true mutual visibility |
| 18-02-02 | LetRecDeclaration parser non-terminal | Mirrors TypeDeclaration pattern; empty continuation avoids LALR conflicts |
| 18-02-03 | Multi-param desugar in parser | Consistent with LetDecl; single param in binding tuple |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] BuiltinValue approach replaces multi-pass immutable env**
- **Found during:** Task 3-4
- **Issue:** Initial approach using multi-pass immutable env updates only supported limited recursion depth (N passes = N levels). Three-function mutual recursion failed beyond a few calls.
- **Fix:** Replaced with BuiltinValue wrappers that close over a shared mutable `ref Env`. This gives true circular references.
- **Files modified:** src/LangThree/Eval.fs
- **Commit:** bb0998a

## Test Results

- **F# tests:** 196/196 passed
- **fslit tests:** 298/298 passed (294 existing + 4 new)
- **New tests:**
  - `mutrec-even-odd.flt`: Two-function mutual recursion (even/odd to depth 100)
  - `mutrec-single.flt`: Single let rec at module level (factorial)
  - `mutrec-three.flt`: Three-function mutual recursion (f->g->h->f chain)
  - `type-decl-mutrec.flt`: Type inference output for mutual recursive functions

## Commits

| Hash | Description |
|------|-------------|
| 5156fae | feat(18-02): add LetRecDecl to AST, parser, and Format |
| 8a7b07f | feat(18-02): implement mutual recursion type checking |
| 9c7506c | feat(18-02): implement mutual recursion evaluation |
| bb0998a | feat(18-02): fix mutual rec eval with BuiltinValue, add fslit tests |
