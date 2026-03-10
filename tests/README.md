# LangThree Tests

**Total: 364 tests** (196 F# unit tests + 168 fslit file-based tests)

## Test Structure

```
tests/
├── LangThree.Tests/          # F# unit tests (dotnet test)
│   ├── IndentFilterTests.fs   # Offside rule, INDENT/DEDENT token generation
│   ├── ExhaustiveTests.fs     # Pattern match exhaustiveness/redundancy checking
│   ├── IntegrationTests.fs    # End-to-end expression evaluation
│   ├── RecordTests.fs         # Record type checking and evaluation
│   ├── GadtTests.fs           # GADT type refinement and checking
│   ├── ModuleTests.fs         # Module system, qualified access, open
│   ├── ExceptionTests.fs      # Exception declaration, raise, try-with
│   └── MatchCompileTests.fs   # Decision tree compilation correctness
│
└── flt/                       # fslit file-based tests (.flt format)
    ├── expr/                  # Expression evaluation (--expr mode)
    ├── file/                  # File-mode evaluation (%input)
    ├── error/                 # Error detection and messages
    └── emit/                  # AST and type output verification
        ├── ast-expr/          #   --emit-ast for expressions
        ├── ast-decl/          #   --emit-ast for declarations
        ├── ast-pat/           #   --emit-ast for patterns
        ├── type-expr/         #   --emit-type for expressions
        └── type-decl/         #   --emit-type for declarations
```

## F# Unit Tests (196 tests)

Run: `dotnet test tests/LangThree.Tests/`

| File | Tests | What it covers |
|------|-------|----------------|
| IndentFilterTests.fs | 38 | NEWLINE→INDENT/DEDENT conversion, match/try pipe alignment, multi-line function app |
| ExhaustiveTests.fs | 30 | Maranget usefulness algorithm, constructor/wildcard/tuple/list/nested patterns |
| IntegrationTests.fs | 47 | Full pipeline: parse → typecheck → eval for expressions |
| RecordTests.fs | 21 | Record creation, field access, copy-update, mutable fields, pattern matching |
| GadtTests.fs | 17 | GADT type refinement, existential types, bidirectional checking |
| ModuleTests.fs | 17 | Module declarations, qualified access, open, nested modules, circular deps |
| ExceptionTests.fs | 9 | Exception declaration, raise, try-with, when guards, re-raise |
| MatchCompileTests.fs | 17 | Decision tree compilation, all pattern types, structural tree verification |

## fslit File-Based Tests (168 tests)

Run: `fslit tests/flt/`

### `flt/expr/` -- Expression Evaluation (42 tests)

Tests expression-mode evaluation (`--expr "..."`). Verifies runtime output.

| Category | Tests | Constructs |
|----------|-------|------------|
| Arithmetic | 4 | `+`, `-`, `*`, `/`, unary `-` |
| Boolean | 2 | `&&`, `\|\|` |
| Comparison | 6 | `=`, `<>`, `<`, `>`, `<=`, `>=` |
| If/else | 3 | basic, nested, parenthesized |
| Let | 3 | basic, nested, pattern destructuring |
| Let rec | 1 | factorial recursion |
| Lambda | 3 | basic, higher-order, annotated params |
| List | 4 | basic, cons, empty, single-element |
| Tuple | 1 | basic tuple creation |
| Match | 5 | int, list, tuple, bool, cons, wildcard |
| String | 1 | string literal |
| Annotation | 2 | basic, colon syntax |
| Prelude | 6 | id, map, filter, fold, length, reverse |

### `flt/file/` -- File-Mode Evaluation (34 tests)

Tests file-mode evaluation (`%input`). Verifies module-level declarations and output.

| Category | Tests | Constructs |
|----------|-------|------------|
| ADT | 6 | basic, match, parametric, recursive, leading pipe, mutual recursion |
| GADT | 2 | basic, eval (type-safe evaluator) |
| Record | 5 | basic, mutable, pattern, update, chained access, parametric |
| Module | 4 | basic, nested, open, qualified access |
| Exception | 4 | basic (uncaught), catch, data, reraise, indented try-with |
| Match | 5 | complex, nested, wildcard, nested constructor, parenthesized pattern |
| Let | 1 | sequence of let bindings |
| Function | 2 | multi-line, multi-param |
| Multiline | 1 | multi-line match |
| Namespace | 1 | namespace declaration |
| When guard | 1 | guard expressions |

### `flt/error/` -- Error Detection (4 tests)

Tests error messages and exit codes (`ExitCode: 1`, `Stderr:`).

| Test | What it checks |
|------|---------------|
| err-div-zero | Division by zero runtime error |
| err-type-mismatch | Type error: `1 + true` |
| err-unbound-var | Unbound variable error |
| err-type-emit | Type error via `--emit-type` |

### `flt/emit/ast-expr/` -- Expression AST (29 tests)

Tests `--emit-ast --expr "..."`. Verifies parser produces correct AST nodes.

| AST Node | Tests |
|----------|-------|
| Number, Bool, String | 3 (literals, literals-bool, literals-string) |
| Add, Subtract, Multiply, Divide, Negate | 5 (arithmetic + variants) |
| Equal, NotEqual, LessThan, GreaterThan, LessEqual, GreaterEqual | 6 (comparison + variants) |
| And, Or | 2 (boolean, boolean-or) |
| If | 1 |
| Let, LetRec, LetPat | 3 |
| Lambda, LambdaAnnot | 2 |
| App | 1 |
| Match | 1 |
| Tuple | 1 |
| EmptyList, Cons | 2 |
| Annot | 1 |

**Coverage: 100%** -- all expression AST node types covered.

### `flt/emit/ast-decl/` -- Declaration AST (17 tests)

Tests `--emit-ast %input`. Verifies parser produces correct declaration AST nodes.

| AST Node | Tests |
|----------|-------|
| LetDecl | 2 (let, func) |
| TypeDecl (ADT) | 3 (simple, parametric, mutual recursive) |
| TypeDecl (GADT) | 1 |
| RecordDecl | 3 (basic, mutable, operations) |
| ExceptionDecl | 2 (simple, with data) |
| ModuleDecl | 1 |
| NamespaceDecl | 1 |
| OpenDecl | 1 |
| Match (in decl context) | 1 |
| TryWith + Raise | 1 |

**Coverage: 100%** -- all declaration AST node types covered.

### `flt/emit/ast-pat/` -- Pattern AST (12 tests)

Tests `--emit-ast` for pattern nodes within match expressions.

| Pattern Node | Tests |
|-------------|-------|
| VarPat | 1 |
| WildcardPat | 1 |
| ConstPat (IntConst) | 1 |
| ConstPat (BoolConst) | 1 |
| ConstructorPat (no arg) | 1 |
| ConstructorPat (with arg) | 1 |
| TuplePat | 1 |
| EmptyListPat | 1 |
| ConsPat | 1 |
| RecordPat | 1 |
| Nested patterns | 1 |
| When guard | 1 |

**Coverage: 100%** -- all pattern AST node types covered.
String constant patterns are not supported by the parser.

### `flt/emit/type-expr/` -- Expression Types (17 tests)

Tests `--emit-type --expr "..."`. Verifies type inference for expressions.

| Type | Tests |
|------|-------|
| int | 4 (literals, arithmetic, let, app) |
| bool | 3 (literals, comparison, boolean) |
| string | 2 (literals, match) |
| int -> int | 1 (lambda) |
| int -> bool | 1 (lambda-annot) |
| int * bool * string | 1 (tuple) |
| int list | 2 (list, cons) |
| int -> int -> int | 1 (letrec) |
| int (annotation) | 1 (annot) |
| int (letpat) | 1 (letpat) |

**Coverage: 100%** -- all expression type inference paths covered.

### `flt/emit/type-decl/` -- Declaration Types (13 tests)

Tests `--emit-type %input`. Verifies type inference for module-level bindings.

| Type Category | Tests |
|--------------|-------|
| Simple types (int, bool) | 1 (let) |
| Arrow types (int -> int -> int) | 1 (func) |
| ADT types (Color) | 1 |
| Parametric ADT (Option\<int\>) | 1 |
| GADT types (Expr\<int\>) | 1 |
| Record types (Point) | 2 (basic, operations) |
| Exception types (exn) | 1 |
| Module types | 1 |
| Pattern match function types | 1 |
| Try-with function types | 1 |
| Polymorphic types ('a -> 'a) | 1 |

**Coverage: 100%** -- all declaration type inference paths covered.

## Running Tests

```bash
# All F# unit tests
dotnet test tests/LangThree.Tests/

# All fslit tests
fslit tests/flt/

# Specific category
fslit tests/flt/emit/ast-expr/
fslit tests/flt/expr/

# Single test
fslit tests/flt/expr/arith-basic.flt
```

## Coverage Summary

| Layer | Coverage | Tests |
|-------|----------|-------|
| Lexer + IndentFilter | F# unit tests | 38 |
| Parser (AST output) | `emit/ast-expr/` + `emit/ast-decl/` + `emit/ast-pat/` | 58 |
| Type Checker (type output) | `emit/type-expr/` + `emit/type-decl/` | 30 |
| Evaluator (runtime output) | `expr/` + `file/` | 76 |
| Error handling | `error/` | 4 |
| Exhaustiveness | F# unit tests | 30 |
| Pattern compilation | F# unit tests | 17 |

**Grammar coverage: 100%** -- every AST node type (expression, declaration, pattern) has dedicated `--emit-ast` tests verifying parser output, and `--emit-type` tests verifying type inference.
