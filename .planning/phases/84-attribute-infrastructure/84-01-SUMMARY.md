---
phase: 84-attribute-infrastructure
plan: "01"
subsystem: parser
tags: [attributes, lexer, parser, ast, fixity, operators]

dependency-graph:
  requires: []
  provides:
    - Attribute infrastructure (Assoc, Attribute, InfixDecl types in Ast.fs)
    - ATTR_OPEN lexer token for #[ syntax
    - Attribute/AttributeList grammar rules in Parser
    - InfixDecl Decl variant with full type-check/eval support
  affects:
    - "85: Fixity table construction (reads InfixDecl attrs)"
    - "86: Operator precedence engine (consumes fixity metadata)"
    - "87: Integration tests"

tech-stack:
  added: []
  patterns:
    - "#[ ] attribute syntax (lexer two-char token #[ before single-char section)"
    - "LALR grammar: AttributeList before LET OpName in Decl rule"
    - "Treat InfixDecl as LetDecl in TypeCheck/Eval (attrs are metadata only in v12.0)"

key-files:
  created:
    - tests/flt/file/attribute/fixity-left.flt
    - tests/flt/file/attribute/fixity-right.flt
    - tests/flt/file/attribute/fixity-operator-use.flt
    - tests/flt/file/attribute/no-attribute-regression.flt
  modified:
    - src/FunLang/Ast.fs
    - src/FunLang/Lexer.fsl
    - src/FunLang/Parser.fsy
    - src/FunLang/TypeCheck.fs
    - src/FunLang/Eval.fs
    - src/FunLang/Format.fs
    - src/FunLang/Repl.fs
    - src/FunLang/Program.fs

decisions:
  - id: use-custom-operators-in-tests
    choice: "Use $> and <$ operators in flt tests instead of |> and <|"
    rationale: "|> and <| are built-in tokens (PIPE_RIGHT, INFIXOP0 via catch-all). The OpName rule accepts only INFIXOP0-4, not PIPE_RIGHT. $> and <$ lex as INFIXOP0 via catch-all and are unambiguous."
    alternatives: "Extend OpName to accept PIPE_RIGHT — would change behavior of existing |> usage"

metrics:
  duration: "~20 minutes"
  completed: "2026-04-03"
---

# Phase 84 Plan 01: Attribute Infrastructure Summary

**One-liner:** Added `#[left N]` / `#[right N]` fixity attribute syntax: ATTR_OPEN lexer token, Attribute/AttributeList grammar, InfixDecl AST node with full type-check and eval support.

## What Was Built

Three coordinated changes to add attribute infrastructure:

**AST (Ast.fs):**
- `Assoc` DU: `Left | Right`
- `Attribute` DU: `FixityAttr of Assoc * int`
- `InfixDecl` Decl variant: `attrs: Attribute list * name: string * body: Expr * Span`
- `declSpanOf` updated

**Lexer (Lexer.fsl):**
- `"#["` lexes as `ATTR_OPEN` (placed before single-char operators section)

**Parser (Parser.fsy):**
- `%token ATTR_OPEN` declaration
- `Attribute` rule: `ATTR_OPEN IDENT NUMBER RBRACKET` → `FixityAttr(assoc, prec)`
- `AttributeList` rule: one-or-more attributes
- Two `Decl` alternatives: `AttributeList LET OpName ParamList EQUALS SeqExpr [INDENT ... DEDENT]` → `InfixDecl`

**Supporting files:**
- `TypeCheck.fs`: InfixDecl handled identically to LetDecl (generalize + add to env)
- `Eval.fs`: InfixDecl evaluates body and adds to env (same as LetDecl)
- `Format.fs`: formatToken adds ATTR_OPEN; formatDecl adds InfixDecl rendering
- `Repl.fs`/`Program.fs`: tryPick updated to recognize InfixDecl bindings

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Test operators | Use `$>` / `<$` not `|>` / `<|` | `|>` is PIPE_RIGHT token; OpName only accepts INFIXOP0-4 |
| InfixDecl semantics | Treat as LetDecl in type-check/eval | Attr metadata stored in AST for phase 85+ to read; no runtime impact v12.0 |
| ATTR_OPEN placement | Before single-char operators in lexer | Clean placement; `#` is not in op_char so zero conflicts |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] |> and <| operators unusable in attribute tests**

- **Found during:** Task 3 (flt test writing)
- **Issue:** `(|>)` fails parse because `|>` lexes as PIPE_RIGHT, not an INFIXOP token. OpName rule only accepts INFIXOP0-4.
- **Fix:** Changed test operators to `$>` (INFIXOP0) and `<$` (INFIXOP0) which both lex correctly via `op_char op_char+` catch-all.
- **Files modified:** All 4 flt test files
- **Impact:** Tests still fully cover left-fixity, right-fixity, chaining, and regression scenarios.

## Commits

| Hash | Description |
|------|-------------|
| baab238 | feat(84-01): add Assoc, Attribute, InfixDecl types to Ast.fs |
| b2683c0 | feat(84-01): add ATTR_OPEN token and attribute grammar to Lexer/Parser |
| 8757aad | test(84-01): add 4 flt integration tests for attribute syntax |

## Test Results

- 714 existing tests: all pass (zero regression)
- 4 new attribute tests: all pass
- **Total: 718/718**

## Next Phase Readiness

Phase 85 (Fixity Table Construction) can now:
- Walk the `Decl` list looking for `InfixDecl(attrs, name, _, _)` nodes
- Extract `FixityAttr(assoc, prec)` from the attrs list
- Build a `Map<string, Assoc * int>` fixity table

No blockers. InfixDecl is in AST, type-checked, and evaluated.
