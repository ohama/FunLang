---
phase: 71-parsing-and-ast
plan: "01"
subsystem: parser
tags: [fsyacc, fslexer, ast, typeclass, instance, lalr1, fatarrow]

# Dependency graph
requires:
  - phase: 70-core-type-infrastructure
    provides: Scheme(vars, constraints, ty), ClassEnv/InstanceEnv types threaded through pipeline
provides:
  - TypeClassDecl and InstanceDecl Decl DU variants in Ast.fs
  - TEConstrained TypeExpr DU variant in Ast.fs
  - TYPECLASS, INSTANCE, FATARROW lexer tokens
  - Parser grammar rules for typeclass/instance declarations and constrained type annotations
  - failwith stubs in TypeCheck.fs and Eval.fs for new Decl variants
  - TEConstrained stubs in Elaborate.fs
  - --emit-ast rendering for all new nodes in Format.fs
affects:
  - 71-02 (second plan in this phase if any)
  - 72-type-inference (phase that implements typeclass type checking)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Constrained types use ConstraintList FATARROW ArrowType grammar pattern"
    - "Typeclass/instance bodies use INDENT/DEDENT block structure like let/module"
    - "New Decl variants get failwith stubs immediately; Phase 72 replaces them"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Elaborate.fs
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Format.fs

key-decisions:
  - "FATARROW (=>) placed after LARROW (<-) in Lexer.fsl so it's matched before the op_char catch-all — no conflict with >= operator which is handled by GE token"
  - "ConstraintList uses IDENT TYPE_VAR grammar (not parenthesized) — LALR(1) disambiguation works via FATARROW lookahead after TYPE_VAR"
  - "Instance method bodies desugar to Lambda chains using List.foldBack, same pattern as other function declarations"
  - "TEConstrained in collectTypeExprVars collects vars from both the constraint list and the body type (needed for Phase 72 type variable tracking)"

patterns-established:
  - "New parser token always paired with Format.fs formatToken arm to keep exhaustive match"
  - "New Decl variants get stubs in TypeCheck.fs + Eval.fs immediately (same commit as parsing)"

# Metrics
duration: 15min
completed: 2026-03-31
---

# Phase 71 Plan 01: Typeclass/Instance Parsing and AST Summary

**TYPECLASS, INSTANCE, FATARROW tokens and grammar rules parse `typeclass Show 'a = ...` and `instance Show int = ...` into TypeClassDecl/InstanceDecl AST nodes; TEConstrained supports `Show 'a => 'a -> string` type annotations**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-31T00:00:00Z
- **Completed:** 2026-03-31T00:15:00Z
- **Tasks:** 2 (committed together — Task 1 alone doesn't compile due to F# exhaustive matching)
- **Files modified:** 7

## Accomplishments
- Added three new TypeExpr/Decl variants to Ast.fs: TEConstrained, TypeClassDecl, InstanceDecl
- Extended Lexer.fsl with TYPECLASS, INSTANCE, FATARROW keyword/operator tokens
- Extended Parser.fsy with ConstraintList, Constraint, TypeClassMethodList, TypeClassMethod, InstanceMethodList, InstanceMethod nonterminals and all typeclass/instance Decls rules
- Added TEConstrained stubs to Elaborate.fs (elaborateWithVars, substTypeExprWithMap, collectTypeExprVars)
- Added explicit failwith stubs for TypeClassDecl and InstanceDecl in TypeCheck.fs and Eval.fs
- Added --emit-ast rendering for all new nodes in Format.fs (including FATARROW/TYPECLASS/INSTANCE in formatToken)

## Task Commits

1. **Tasks 1+2: AST nodes, Lexer/Parser, downstream stubs, and --emit-ast** - `bae7352` (feat)

**Plan metadata:** (docs commit below)

## Files Created/Modified
- `src/LangThree/Ast.fs` - TEConstrained variant in TypeExpr; TypeClassDecl, InstanceDecl variants in Decl; declSpanOf updated
- `src/LangThree/Lexer.fsl` - TYPECLASS, INSTANCE keywords; FATARROW operator (placed after LARROW, before op_char catch-all)
- `src/LangThree/Parser.fsy` - TYPECLASS/INSTANCE/FATARROW token declarations; TypeExpr extended with ConstraintList; new nonterminals; typeclass/instance Decls rules
- `src/LangThree/Elaborate.fs` - TEConstrained stubs in all three TypeExpr-matching functions
- `src/LangThree/TypeCheck.fs` - failwith stubs for TypeClassDecl and InstanceDecl (before NamespaceDecl arm)
- `src/LangThree/Eval.fs` - failwith stubs for TypeClassDecl and InstanceDecl (after TypeAliasDecl arm)
- `src/LangThree/Format.fs` - TEConstrained in formatTypeExpr; TypeClassDecl/InstanceDecl in formatDecl; three new tokens in formatToken

## Decisions Made
- FATARROW placed after LARROW in lexer (not in keyword section) since `=>` is a two-char operator, not a keyword. This puts it before the `op_char op_char+` catch-all which would otherwise grab it.
- ConstraintList grammar uses `IDENT TYPE_VAR` (no parentheses). The LALR(1) parser can disambiguate via FATARROW lookahead — no conflicts observed.
- Tasks 1 and 2 committed together: Task 1 adds Decl variants that make TypeCheck/Eval/Format pattern-matches non-exhaustive; committing alone would produce warnings. Combined commit keeps every commit green.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added TYPECLASS/INSTANCE/FATARROW to Format.fs formatToken**
- **Found during:** Task 2 build (1 warning: incomplete match on token type in Format.fs line 8)
- **Issue:** Plan specified rendering for AST nodes but didn't mention the `formatToken` function which also matches on all Parser.token variants
- **Fix:** Added three token cases to `formatToken`
- **Files modified:** src/LangThree/Format.fs
- **Verification:** Build shows 0 warnings after fix
- **Committed in:** bae7352

---

**Total deviations:** 1 auto-fixed (missing critical — new tokens must be in formatToken)
**Impact on plan:** Necessary for correctness (zero warnings requirement). No scope creep.

## Issues Encountered
None — all grammar rules parsed cleanly without LALR conflicts on the new ConstraintList rule.

## Next Phase Readiness
- Phase 71 Plan 01 complete: all syntactic forms parse to AST nodes
- Phase 71 Plan 02 (if exists) or Phase 72 can begin implementing typeclass type checking
- TypeCheck.fs and Eval.fs stubs will raise `failwithf` if typeclass syntax is used — clear signal for Phase 72 team
- FATARROW token correctly separated from GE token (>= is GE, => is FATARROW)

---
*Phase: 71-parsing-and-ast*
*Completed: 2026-03-31*
