---
phase: 28-n-tuples
plan: 01
subsystem: compiler
tags: [fsharp, ast, parser, typechecker, eval, tuples, pattern-matching]

# Dependency graph
requires:
  - phase: 27-list-syntax-completion
    provides: pattern infrastructure and module-level declaration handling
provides:
  - LetPatDecl variant in Ast.Decl DU with declSpanOf support
  - Parser grammar rules for module-level tuple pattern binding
  - TypeCheck handler propagating tuple bindings to subsequent declarations
  - Eval handler binding all tuple elements into module environment
affects: [29-records-update, 30-expression-let-rec, future pattern-related phases]

# Tech tracking
tech-stack:
  added: []
  patterns: [LetPatDecl mirrors LetPat expression pattern but at declaration level, uses same matchPattern/inferPattern infrastructure]

key-files:
  created:
    - tests/phase28.fun
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Parser.fsy
    - src/LangThree/Format.fs

key-decisions:
  - "LetPatDecl placed as second variant in Decl DU (after LetDecl) for logical grouping"
  - "TypeCheck uses cEnv (ctor env) in inferPattern call, not Map.empty, matching Bidir.fs pattern"
  - "Format.fs required LetPatDecl match arm to fix FS0025 exhaustiveness warning (not in plan)"

patterns-established:
  - "Module-level pattern binding follows same type inference flow as expression-level LetPat"
  - "TuplePattern nonterminal already existed in parser - reused for Decl grammar rules"

# Metrics
duration: 11min
completed: 2026-03-24
---

# Phase 28 Plan 01: N-Tuples Summary

**Module-level tuple pattern binding via LetPatDecl - `let (a, b, c) = expr` now works at module level, closing the single declaration-level gap in N-tuple support**

## Performance

- **Duration:** ~11 min
- **Started:** 2026-03-24T09:24:51Z
- **Completed:** 2026-03-24T09:35:38Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Added `LetPatDecl` variant to `Ast.Decl` DU and `declSpanOf` match
- Wired `LetPatDecl` through TypeCheck (full generalization), Eval (matchPattern bindings), and Parser (`LET TuplePattern EQUALS` grammar)
- All 4 success criteria verified: tuple print, pattern binding, fun tuple param, fst regression
- Build clean: 0 errors, 0 warnings

## Task Commits

Each task was committed atomically:

1. **Task 1: Add LetPatDecl to Ast.Decl and wire through TypeCheck and Eval** - `4c4e06a` (feat)
2. **Task 2: Add parser grammar rules and write regression tests** - `4f707cf` (feat)

**Plan metadata:** (docs commit to follow)

## Files Created/Modified
- `src/LangThree/Ast.fs` - Added `LetPatDecl` variant to Decl DU and `declSpanOf`
- `src/LangThree/TypeCheck.fs` - Added `LetPatDecl` handler in typeCheckDecls fold
- `src/LangThree/Eval.fs` - Added `LetPatDecl` handler in evalModuleDecls fold
- `src/LangThree/Parser.fsy` - Added `LET TuplePattern EQUALS` Decl grammar rules
- `src/LangThree/Format.fs` - Fixed FS0025 exhaustiveness warning (auto-fix)
- `tests/phase28.fun` - Regression test exercising all 4 success criteria

## Decisions Made
- Used `cEnv` (constructor env) rather than `Map.empty` in `inferPattern` call in TypeCheck, matching the pattern used in `Bidir.fs` for expression-level `LetPat`
- Placed parser rules before `LET UNDERSCORE` rules (not after `LET IDENT`) to avoid any potential parser ambiguity issues

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed FS0025 exhaustiveness warning in Format.fs**
- **Found during:** Task 1 (build verification)
- **Issue:** Format.fs `formatDecl` match had no arm for `LetPatDecl`, causing FS0025 warning
- **Fix:** Added `Ast.LetPatDecl(pat, body, _)` match arm using existing `formatPattern` helper
- **Files modified:** `src/LangThree/Format.fs`
- **Verification:** Build exits 0 with 0 warnings
- **Committed in:** `4c4e06a` (part of Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 bug - exhaustiveness warning)
**Impact on plan:** Necessary correctness fix. No scope creep.

## Issues Encountered
- `string` is a reserved keyword token (`TYPE_STRING`) in the lexer, so `string x` causes parse errors in test files. Test file uses direct value output (final binding) instead of string conversion.

## Next Phase Readiness
- N-tuple module-level binding complete
- All tuple infrastructure (AST, inference, eval) confirmed working end-to-end
- Ready for Phase 29 (records update) or further v2.0 phases

---
*Phase: 28-n-tuples*
*Completed: 2026-03-24*
