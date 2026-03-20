---
phase: 19-user-defined-operators
plan: "01"
subsystem: parser
tags: [lexer, parser, operators, infixop, precedence]
dependency-graph:
  requires: []
  provides: ["user-defined-infix-operators", "operator-definition-syntax", "operator-as-function"]
  affects: []
tech-stack:
  added: []
  patterns: ["OCaml INFIXOP0-4 first-character precedence classification", "operator desugaring to App(App(Var(op),lhs),rhs))"]
key-files:
  created:
    - tests/flt/file/op-define-basic.flt
    - tests/flt/file/op-as-function.flt
    - tests/flt/file/op-precedence.flt
    - tests/flt/file/op-right-assoc.flt
    - tests/flt/file/op-existing-compat.flt
    - tests/flt/file/op-let-rec.flt
  modified:
    - src/LangThree/Lexer.fsl
    - src/LangThree/Parser.fsy
    - src/LangThree/Format.fs
decisions:
  - id: D19-01
    title: "OCaml INFIXOP0-4 approach for operator precedence"
    choice: "First character of operator determines precedence bucket"
    alternatives: ["Haskell infixl/infixr declarations", "Single precedence level for all custom ops", "Pratt parser"]
  - id: D19-02
    title: "Operators starting with * require spaces in parenthesized form"
    choice: "( *. ) and ( ** ) with spaces, because (* starts block comments"
    alternatives: ["Special lexer lookahead for (*op*)", "Different comment syntax"]
metrics:
  duration: 9min
  completed: 2026-03-20
---

# Phase 19 Plan 01: User-Defined Operators Summary

**One-liner:** OCaml-style INFIXOP0-4 lexer classification with grammar-level precedence, desugaring to function application

## What Was Done

### Task 1: Add INFIXOP0-4 tokens to lexer
- Added `classifyOperator` helper function that classifies multi-char operator sequences by first character into 5 precedence buckets (INFIXOP0=comparison, INFIXOP1=concat, INFIXOP2=additive, INFIXOP3=multiplicative, INFIXOP4=exponentiation)
- Added `op_char` character set: `! $ % & * + - . / < = > ? @ ^ | ~`
- Added catch-all lexer rule `op_char op_char+` AFTER all existing specific operator rules
- Added INFIXOP0-4 token declarations with string payload to Parser.fsy
- Added precedence declarations at correct levels with proper associativity (left for 0/2/3, right for 1/4)
- Added grammar rules in Expr (INFIXOP0, INFIXOP1), Expr near +/- (INFIXOP2), Term (INFIXOP3), Factor (INFIXOP4)
- All rules desugar to `App(App(Var(op), lhs), rhs)` -- no new AST nodes needed

### Task 2: Add operator definition syntax and operator-as-function
- Added `OpName` nonterminal: `LPAREN INFIXOPn RPAREN` for all 5 levels
- Added Decl rules: `let (op) params = body` for operator definitions
- Added LetRecDeclaration rules: `let rec (op) params = body` for recursive operators
- Added Atom rules: `(op)` as `Var(op)` for using operators as first-class function values

### Task 3: Update Format.fs
- Added INFIXOP0-4 pattern matches to `formatToken` for complete token formatting
- Eliminated FS0025 incomplete pattern match warning
- No changes needed in TypeCheck.fs, Bidir.fs, Infer.fs, IndentFilter.fs, or Eval.fs (operators desugar to plain function application)

### Task 4: Verification
- Manual testing of all operator scenarios: definition, usage, as-function, precedence, right-associativity, recursive operators, existing operator compatibility

### Task 5: fslit tests
- 6 new fslit tests covering all operator features

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| D19-01 | OCaml INFIXOP0-4 first-character classification | Battle-tested in OCaml/F# for 30+ years; integrates cleanly with LALR(1); no new AST nodes |
| D19-02 | Operators starting with * require spaces: `( ** )` not `(**)` | `(*` starts block comments; same behavior as OCaml and F# |

## Deviations from Plan

None -- plan executed exactly as written.

## Test Results

- **F# tests:** 196/196 passed (zero regression)
- **fslit tests:** 381/381 passed (375 existing + 6 new)
- **Total:** 577 tests passing

## Known Limitations

1. **Operators starting with `*` require spaces in parenthesized form:** `( ** )` not `(**)`, because `(*` starts a block comment. Same as OCaml/F#.
2. **Custom operators must be 2+ characters:** Single-char operators (`+`, `-`, `*`, etc.) are always lexed as built-in tokens. Users can define `++`, `<+>`, `*~` etc.
3. **No prefix operator support:** Only infix operators are supported. Prefix operators (like OCaml's `~-`) were out of scope.

## Next Phase Readiness

v1.5 milestone complete. User-defined operators are fully functional with correct precedence, associativity, definition syntax, and first-class function usage.
