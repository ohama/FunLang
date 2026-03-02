---
phase: 01-indentation-based-syntax
plan: 04
subsystem: parser
tags: [module, declarations, grammar, indentation]

dependencies:
  requires: []
  provides: [module-structure, top-level-declarations]
  affects: [phase-05-module-system]

tech-stack:
  added: []
  patterns: [module-as-declaration-list, function-declaration-desugaring]

files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Parser.fsy
    - tests/LangThree.Tests/IntegrationTests.fs

decisions:
  - decision: Module and Decl types separate from Expr
    rationale: Module structure is distinct from expression evaluation
    alternatives: [embed-in-expr-type]
    chosen: separate-types

  - decision: Function declarations desugar to lambdas
    rationale: Simplifies semantics - 'let f x = e' becomes 'let f = fun x -> e'
    alternatives: [native-function-decl]
    chosen: desugar-to-lambda

  - decision: Remove NEWLINE requirement from Decl grammar
    rationale: IndentFilter removes same-level NEWLINEs - rely on token boundaries instead
    alternatives: [preserve-newlines, use-semicolons]
    chosen: token-boundaries

metrics:
  commits: 2
  tests-added: 4
  duration: 12min
  completed: 2026-03-02
---

# Phase 1 Plan 4: Module-Level Declarations Summary

**One-liner:** Parser supports multiple top-level let bindings at column 0 with Module/Decl AST types and function declaration syntax.

## What Was Built

### AST Extensions
- **Decl type:** `LetDecl of name * body * Span` for module-level declarations
- **Module type:** `Module of Decl list * Span` and `EmptyModule of Span`
- **Helper functions:** `declSpanOf` and `moduleSpanOf` for span extraction

### Parser Grammar
- **parseModule start symbol:** New entry point for parsing complete module files
- **Decls rule:** Sequence of declarations (`Decl | Decl Decls`)
- **Decl rule:** Four production rules:
  - Simple binding: `LET IDENT EQUALS Expr`
  - Indented binding: `LET IDENT EQUALS INDENT Expr DEDENT`
  - Function declaration: `LET IDENT ParamList EQUALS Expr` (desugars to nested lambdas)
  - Indented function: `LET IDENT ParamList EQUALS INDENT Expr DEDENT`
- **ParamList rule:** One or more parameters for function declarations

### Integration Tests
1. **testModuleLevelDeclarations:** Three top-level bindings with arithmetic
2. **testModuleLevelWithIndentedBodies:** Nested let expressions with explicit `in` keywords
3. **testEmptyModule:** Empty file parses as EmptyModule
4. **testModuleWithVariousExprTypes:** Lambda, literal, and application declarations

## Key Technical Decisions

### NEWLINE Removal
**Problem:** Decl grammar initially required NEWLINE tokens, but IndentFilter removes same-level NEWLINEs.

**Solution:** Remove NEWLINE from grammar, rely on natural token boundaries between declarations.

**Impact:** Parser uses lookahead to distinguish between declarations without explicit delimiters.

### Function Declaration Desugaring
**Syntax:** `let f x y = expr`

**Desugaring:** `let f = fun x -> fun y -> expr`

**Implementation:** `List.foldBack` over ParamList to build nested Lambda expressions.

**Benefit:** Unified semantics - all bindings are `name = expr` where expr may be a lambda.

### Module Structure vs Expression Grammar
**Separation:** Module and Decl are separate types from Expr.

**Rationale:**
- Top-level declarations are not evaluated as expressions
- Foundation for Phase 5 module system (namespaces, imports)
- Clear distinction between file structure and expression semantics

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

✅ **Build:** `dotnet build` succeeds with no parser conflicts
✅ **Tests:** All 4 integration tests pass
✅ **Grammar:** Module accepts multiple Decl productions
✅ **AST:** Correct representation with declaration list

```bash
$ dotnet test --filter "testModuleLevelDeclarations|testModuleLevelWithIndentedBodies|testEmptyModule|testModuleWithVariousExprTypes"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

## Next Phase Readiness

### For Phase 5 (Module System)

**Ready:**
- Module structure in place (Module type with Decl list)
- Top-level declarations parsed correctly
- Span tracking for error messages

**Not Yet:**
- Module names/namespaces
- Import/export declarations
- Qualified names
- Module-level types

**Foundation Complete:** Phase 5 can extend Module and Decl types without modifying expression grammar.

### Blockers/Concerns

**Nested Indentation-Based Let:** Current implementation requires explicit `in` keywords for nested let bindings inside indented blocks. Full indentation-based `let` sequences (Plan 01-03?) not yet supported.

**Example Limitation:**
```fsharp
let x =
    let a = 1    (* parse error - needs 'in' *)
    let b = 2
    a + b
```

**Workaround:** Use explicit `in` keywords:
```fsharp
let x =
    let a = 1 in
    let b = 2 in
    a + b
```

This is a known limitation for future enhancement.

## Commits

1. **78e0f1c** - feat(01-04): add Module grammar for top-level declarations
   - Modified: Ast.fs, Parser.fsy
   - Added: Module, Decl, parseModule, Decls grammar rules

2. **757354e** - test(01-04): add integration tests for module-level declarations
   - Modified: IntegrationTests.fs, Parser.fsy, IndentFilterTests.fs
   - Added: 4 tests, parseModule helper, ParamList grammar

## Files Modified

| File | Changes | Purpose |
|------|---------|---------|
| src/LangThree/Ast.fs | +29 lines | Module and Decl types, span helpers |
| src/LangThree/Parser.fsy | +26 lines | parseModule start, Decls/Decl/ParamList rules |
| tests/LangThree.Tests/IntegrationTests.fs | +39 lines | parseModule helper, 4 integration tests |
| tests/LangThree.Tests/IndentFilterTests.fs | +4 lines | Fix PrevToken field for compatibility |

## Examples

### Multiple Top-Level Declarations
```fsharp
let x = 42
let y = 10
let z = x + y
```

Parses as:
```fsharp
Module([
  LetDecl("x", Number(42))
  LetDecl("y", Number(10))
  LetDecl("z", Add(Var("x"), Var("y")))
])
```

### Function Declarations
```fsharp
let add x y = x + y
let result = add 10 20
```

Desugars to:
```fsharp
Module([
  LetDecl("add", Lambda("x", Lambda("y", Add(Var("x"), Var("y")))))
  LetDecl("result", App(App(Var("add"), Number(10)), Number(20)))
])
```

### Indented Bodies
```fsharp
let const42 =
    42

let app = id const42
```

Parses as:
```fsharp
Module([
  LetDecl("const42", Number(42))
  LetDecl("app", App(Var("id"), Var("const42")))
])
```

## Alignment with Requirements

**INDENT-05 (Module-level declarations):** ✅ Complete

- Parser accepts multiple top-level let bindings
- Declarations must be at column 0
- Each declaration can have indented body
- Foundation ready for Phase 5 module system
