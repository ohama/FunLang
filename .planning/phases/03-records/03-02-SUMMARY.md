---
phase: 03-records
plan: 02
subsystem: parser
tags: [fsyacc, parser, grammar, records, LALR]

# Dependency graph
requires:
  - phase: 03-01
    provides: "AST nodes (RecordDecl, RecordExpr, FieldAccess, RecordUpdate, RecordPat), token declarations (LBRACE, RBRACE, SEMICOLON, DOT)"
provides:
  - "Parser grammar rules for all record syntax forms"
  - "Format.fs formatting for record tokens and AST nodes"
affects: [03-03, 03-04, 03-05, 03-06, 03-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "RecordExprInner with function return to resolve LBRACE IDENT ambiguity in LALR(1)"
    - "Left-recursive Atom DOT IDENT for chained field access"

key-files:
  created: []
  modified:
    - "src/LangThree/Parser.fsy"
    - "src/LangThree/Format.fs"

key-decisions:
  - "Used RecordExprInner approach (not two separate Atom productions) for LBRACE ambiguity resolution"
  - "IndentFilter unchanged -- no bracket tracking needed for braces"

patterns-established:
  - "RecordExprInner: function-return pattern for parser ambiguity resolution in fsyacc"

# Metrics
duration: 2min
completed: 2026-03-09
---

# Phase 3 Plan 2: Record Parser Grammar Summary

**Full record syntax grammar (declarations, expressions, field access, copy-and-update, patterns) using RecordExprInner for LALR(1) ambiguity resolution**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-09T05:49:26Z
- **Completed:** 2026-03-09T05:51:39Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Parser accepts record type declarations: `type Point = { x: int; y: int }`
- Parser accepts record expressions: `{ x = 1; y = 2 }`
- Parser accepts field access with chaining: `point.x`, `a.b.c`
- Parser accepts copy-and-update: `{ point with y = 3 }`
- Parser accepts record patterns: `{ x = px; y = py }`
- Format.fs handles all new tokens (LBRACE, RBRACE, SEMICOLON, DOT) and AST nodes
- Zero new parser conflicts introduced

## Task Commits

Each task was committed atomically:

1. **Task 1: Parser record grammar rules** - `5b20523` (feat)
2. **Task 2: Format.fs and IndentFilter updates** - `fc8c85f` (feat)

## Files Created/Modified
- `src/LangThree/Parser.fsy` - Added 45 lines: RecordDeclaration, RecordFields, RecordField, RecordExprInner, RecordFieldBindings, RecordPatFields rules, FieldAccess and record Atom productions, Decls extensions
- `src/LangThree/Format.fs` - Added 20 lines: formatToken cases for LBRACE/RBRACE/SEMICOLON/DOT, formatAst cases for RecordExpr/FieldAccess/RecordUpdate, formatPattern case for RecordPat

## Decisions Made
- **RecordExprInner approach chosen:** Used the function-return pattern (`fun span -> ...`) instead of two separate Atom productions. This cleanly resolves the LBRACE IDENT LALR(1) ambiguity because RecordFieldBindings requires `IDENT EQUALS` while Expr reduction sees `WITH` as next token.
- **IndentFilter unchanged:** No bracket tracking exists for LPAREN/RPAREN in IndentFilter, so no corresponding LBRACE/RBRACE handling was added. The parser's explicit INDENT/DEDENT alternatives in RecordDeclaration handle indented record declarations.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Full record syntax is now parseable, unblocking type checking (plan 03) and evaluation (plan 04)
- Remaining FS0025 warnings in Eval.fs, TypeCheck.fs, Infer.fs, Bidir.fs, Exhaustive.fs are expected and will be addressed in subsequent plans
- All 89 existing tests pass without regression

---
*Phase: 03-records*
*Completed: 2026-03-09*
