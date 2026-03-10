---
phase: 10-unit-type
plan: "01"
subsystem: parser-typesystem
tags: [unit, parser, lexer, type-system, fsyacc, fslex, hindley-milner]

# Dependency graph
requires:
  - phase: 09-pipe-composition
    provides: pipe and composition operators, stable parser base
  - phase: 03-records
    provides: TupleValue [], TTuple [] already used as unit representation in SetField
provides:
  - "()" unit literal parses to Tuple([], span) / TupleValue []
  - "unit" type keyword token TYPE_UNIT → TETuple [] in elaboration
  - "fun () -> body" desugars to LambdaAnnot(\"__unit\", TETuple [], body)
  - "let _ = expr" at module level (Decl) and expression level (Expr in...in)
  - formatType TTuple [] → \"unit\" in all formatType / formatTypeNormalized paths
affects:
  - 11-print-function
  - any future phase using unit-returning functions

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Unit as zero-element tuple: TTuple [] / TupleValue [] (no new ADT case needed)"
    - "TYPE_UNIT lexer keyword consistent with TYPE_INT/TYPE_BOOL/TYPE_STRING/TYPE_LIST pattern"
    - "LambdaAnnot(\"__unit\", TETuple [], ...) for fun () -> body desugar (explicit type annotation ensures correct parameter type)"

key-files:
  created: []
  modified:
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Type.fs
    - src/LangThree/Format.fs

key-decisions:
  - "Reuse TTuple [] / TupleValue [] as unit representation — no new TUnit case needed anywhere"
  - "TYPE_UNIT lexer keyword approach (consistent with int/bool/string keywords, not special-casing in elaborator)"
  - "fun () -> body desugars to LambdaAnnot(\"__unit\", TETuple [], ...) for correct type inference without special-casing \"__unit\""
  - "Replaced Var(\"()\") sentinel in indented let continuation with Tuple([], span) to avoid undefined variable error now that () is a real expression"
  - "let _ = at Decl level produces LetDecl(\"_\", body, span) — binds \"_\" in env harmlessly, enables side-effect sequencing"

patterns-established:
  - "Unit literal: LPAREN RPAREN as first Atom rule (before LPAREN Expr RPAREN) — no LALR conflict"
  - "Unit type: TYPE_UNIT in AtomicType grammar — same as other primitive type keywords"
  - "Wildcard sequencing: LET UNDERSCORE EQUALS in both Decl and Expr grammar sections"

# Metrics
duration: 4min
completed: 2026-03-10
---

# Phase 10 Plan 01: Unit Type Summary

**Unit type fully surfaced: `()` literal, `unit` keyword, `fun () -> body`, `let _ =` sequencing — all wired through existing `TTuple []`/`TupleValue []` representation with zero new AST variants**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-10T06:51:11Z
- **Completed:** 2026-03-10T06:55:53Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- `()` parses as `Tuple([], span)`, evaluates to `TupleValue []`, displays as `()`
- `unit` type keyword lexes to `TYPE_UNIT`, elaborates to `TETuple []`, formats as `"unit"` via `--emit-type`
- `fun () -> body` produces type `unit -> bodyType` via `LambdaAnnot("__unit", TETuple [], body)` desugar
- `let _ = expr` works at module level (Decl) and expression level (`let _ = e1 in e2`)
- `formatType`/`formatTypeNormalized` both handle `TTuple [] -> "unit"` correctly
- All 196 existing F# tests pass (no regressions)

## Task Commits

Each task was committed atomically:

1. **Task 1: Lex and parse `()` literal, `unit` type, `fun () ->` parameter** - `1a2eff1` (feat)
2. **Task 2: Fix formatType, add `let _ =` grammar at Decl and Expr level** - `0f04e2a` (feat)

## Files Created/Modified
- `src/LangThree/Lexer.fsl` - Added `"unit" → TYPE_UNIT` keyword token rule
- `src/LangThree/Parser.fsy` - Added TYPE_UNIT declaration, AtomicType rule, LPAREN RPAREN Atom rule, fun () -> Expr rule, let _ = at Decl/Expr level; fixed Var("()") sentinel
- `src/LangThree/Type.fs` - Fixed `formatType`/`formatTypeNormalized` to render `TTuple [] → "unit"`
- `src/LangThree/Format.fs` - Added `Parser.TYPE_UNIT -> "TYPE_UNIT"` to formatToken

## Decisions Made
- **TTuple [] reuse:** The runtime (`TupleValue []`) and type (`TTuple []`) infrastructure for unit already existed. All changes were surface wiring only — no new AST/Value/Type variant needed.
- **TYPE_UNIT token:** Chose lexer keyword approach (Option A from research) for consistency with `TYPE_INT`, `TYPE_BOOL`, etc. The alternative of special-casing `TEName "unit"` in `elaborateWithVars` would have been inconsistent.
- **LambdaAnnot desugar for `fun () ->`:** Using `LambdaAnnot("__unit", TETuple [], body)` ensures the parameter type is constrained to `TTuple []` without any special-casing in Bidir.fs or Infer.fs.
- **Var("()") replacement:** The parser previously used `Var("()", ...)` as a sentinel for the indented-let continuation body. Now that `()` is a real expression, this would cause an "undefined variable" error. Fixed by replacing with `Tuple([], ...)`.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered
- **`let _ = x.v <- 99` with anonymous record:** The plan's verification step 4 used `{ mutable v = 1 }` without a type declaration, which is not valid LangThree syntax. Mutable records require explicit type declarations. The feature itself works correctly — tested with `type Counter = { mutable count: int }` which produced the expected result. This is a verification test wording issue only, not a code issue.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- Unit type foundation complete: `()`, `unit`, `fun () ->`, `let _ =` all operational
- Ready for: print/output functions that return unit, any side-effect-oriented features
- No blockers

---
*Phase: 10-unit-type*
*Completed: 2026-03-10*
