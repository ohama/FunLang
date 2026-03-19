---
phase: 13-tutorial-core
plan: "01"
title: "Tutorial Chapters 1-4: Basics"
subsystem: documentation
tags: [tutorial, getting-started, functions, lists, pattern-matching]
dependency-graph:
  requires: [12-printf-output]
  provides: [tutorial-chapters-1-4]
  affects: [13-02]
tech-stack:
  added: []
  patterns: [example-driven-documentation, cli-verified-examples]
key-files:
  created:
    - tutorial/01-getting-started.md
    - tutorial/02-functions.md
    - tutorial/03-lists-and-tuples.md
    - tutorial/04-pattern-matching.md
  modified: []
decisions:
  - id: D1301-01
    title: "Single-line examples for let rec in file mode"
    rationale: "Multi-line if/else inside let rec body causes parse errors in file mode; keep recursive bodies on single line"
  - id: D1301-02
    title: "No file-mode map/filter/fold examples in Ch3"
    rationale: "Nested let rec inside multi-param let at module level produces parse errors; keep recursive list functions in --expr mode only"
metrics:
  duration: "6 min"
  completed: "2026-03-19"
---

# Phase 13 Plan 01: Tutorial Chapters 1-4 Summary

Tutorial basics documentation with 78 runnable CLI-verified examples across 4 chapters.

## Commits

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Chapter 1: Getting Started | 0d393f2 | tutorial/01-getting-started.md |
| 2 | Chapter 2: Functions | 99a5945 | tutorial/02-functions.md |
| 3 | Chapter 3: Lists and Tuples | b2768a9 | tutorial/03-lists-and-tuples.md |
| 4 | Chapter 4: Pattern Matching | c428211 | tutorial/04-pattern-matching.md |
| 5 | Verify all examples | (inline) | All chapters verified during writing |

## What Was Built

**Chapter 1: Getting Started** (28 examples)
- Running LangThree: --expr, file mode, --emit-ast, --emit-type, --repl
- Integers, arithmetic, booleans, short-circuit evaluation
- Strings, concatenation, built-in string functions
- Comparison operators, conditionals, comments, unit type

**Chapter 2: Functions** (16 examples)
- Anonymous functions, let bindings (expr and file mode)
- Multi-parameter functions, recursive functions (let rec)
- Higher-order functions, closures, currying
- Pipe and composition operators

**Chapter 3: Lists and Tuples** (16 examples)
- List literals, cons operator, tuples, tuple decomposition
- Recursive list functions: length, sum, map, filter
- Closure-based workarounds for single-param let rec limitation

**Chapter 4: Pattern Matching** (18 examples)
- All pattern types: constant, variable, wildcard, tuple, list, constructor, nested, record
- When guards, exhaustiveness checking, let-pattern destructuring

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2] Removed file-mode map example from Chapter 3**
- **Found during:** Task 3 verification
- **Issue:** Nested `let rec` inside multi-param `let` at module level produces parse error
- **Fix:** Removed the file-mode map example; kept --expr mode examples only
- **Files modified:** tutorial/03-lists-and-tuples.md

**2. [Rule 2] Single-line rec bodies in Chapter 2**
- **Found during:** Task 2 verification
- **Issue:** Multi-line if/else inside `let rec` body in file mode causes parse error
- **Fix:** Changed factorial example to single-line format
- **Files modified:** tutorial/02-functions.md

## Documented Limitations

- `not` function does not exist (use `if x then false else true`)
- `let rec` only works at expression level (with `in`), not at module level
- `let rec` supports only a single parameter
- String constant patterns are not supported
- No modulo operator

## Next Phase Readiness

Chapter 5-8 (plan 13-02) can proceed. No blockers.
