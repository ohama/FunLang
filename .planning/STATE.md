# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-25)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** Phase 4 - GADT (Generalized Algebraic Data Types)

## Current Position

Phase: 4 of 6 (Generalized Algebraic Data Types)
Plan: 2 of 5 in current phase
Status: In progress
Last activity: 2026-03-09 -- Completed 04-02-PLAN.md (GADT elaboration)

Progress: [██████████░░░░░░░░░░] 53% (3/6 phases + 2/5 plans in phase 4)

## Performance Metrics

**Velocity:**
- Total plans completed: 15
- Average duration: 4.8 min
- Total execution time: 1.22 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01 | 4 | 37 min | 9.3 min |
| 02 | 2 | 8 min | 4.0 min |
| 03 | 7 | 21 min | 3.0 min |
| 04 | 2 | 7 min | 3.5 min |

**Recent Trend:**
- Last 5 plans: 03-06 (2min), 03-07 (4min), 04-01 (3min), 04-02 (4min)
- Trend: Consistent ~3-4min average for type system elaboration plans

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- F# 스타일 선택 (over OCaml) — 들여쓰기 기반이 현대적, 단순함
- GADT 포함 — bidirectional checking 활용, 표현력 있는 타입 시스템
- Functor 제외 — 복잡도 대비 실용성 낮음

**From 01-01 (Match Expression Indentation):**
- Enter match context before processing newline to enable pipe alignment validation
- Pop match contexts automatically when dedenting below their base level
- Pipes in match expressions align with 'match' keyword column, not indented from it

**From 01-02 (Multi-line Function Application):**
- canBeFunction (IDENT | RPAREN) identifies function positions, isAtom identifies argument positions
- Prevent re-entering InFunctionApp context when already in one to avoid nested INDENT tokens
- Consume newlines within function app context (emit no tokens) to allow multi-line arguments without extra INDENT/DEDENT

**From 01-04 (Module-Level Declarations):**
- Module and Decl types separate from Expr for clear file structure
- Function declarations desugar to nested lambdas (let f x y = e → let f = fun x -> fun y -> e)
- IndentFilter removes same-level NEWLINEs - rely on token boundaries in grammar

**From 01-03 (Improved Error Messages):**
- formatExpectedIndents shows all valid indent levels from stack plus "or a new indent level" hint
- validateIndentWidth enforces multiples only when StrictWidth=true (strict mode for style guides, lenient for development)
- EOF handling must emit all DEDENTs returned by processNewline in a single call (not one per loop iteration)
- Error message format: context-specific description with line number, actual column, and expected values

**From 02-04 (Exhaustiveness Checking):**
- Self-contained CasePat type decoupled from AST Pattern for independent testing
- Constructor sets passed explicitly (functional style) rather than global registry
- Maranget usefulness algorithm with complete/incomplete signature branching

**From 02-06 (Exhaustiveness Wiring):**
- Infer scrutinee type from constructor patterns rather than re-synthesizing (avoids scope issues)
- W-prefix for warning codes to distinguish from E-prefix error codes
- typeCheckModule returns Result<Diagnostic list * RecordEnv, Diagnostic> (warnings + RecordEnv on success)

**From 03-01 (Record Foundation):**
- substTypeExprWithMap extracted as shared module-level helper for both ADT and record elaboration
- RecordEnv parallel to ConstructorEnv for record type metadata
- Record error codes use E03xx range (E0307-E0313)

**From 03-02 (Record Parser Grammar):**
- RecordExprInner with function return resolves LBRACE IDENT LALR(1) ambiguity
- Left-recursive Atom DOT IDENT enables chained field access (a.b.c)
- IndentFilter unchanged -- no bracket tracking needed for braces

**From 03-03 (Record Type Checking):**
- recEnv added as separate parameter to synth/check/inferBinaryOp (parallel to ctorEnv)
- Record type resolved from field set (globally unique field names)
- typeCheckModule returns RecordEnv alongside warnings for evaluator use

**From 03-04 (Record Evaluation):**
- recEnv added as first param to eval/evalMatchClauses, threaded through all recursive calls
- resolveRecordTypeName: field-set lookup against RecordEnv for type name resolution
- Program.fs --file migrated to parseModule+typeCheckModule pipeline
- --expr and REPL pass Map.empty for recEnv (no module-level record type declarations)

**From 03-05 (Record Integration Tests):**
- Equality operator = ambiguous with let binding = in contexts like `let result = a = b`; use if-then-else wrapper
- All 21 record tests passed on first run -- no bugs found in plans 01-04 implementation

**From 03-06 (Mutable Field Syntax):**
- Token declarations must be in Parser.fsy before lexer can reference them (lexer imports from Parser module)
- SetField at Expr level for low-precedence assignment semantics

**From 03-07 (Mutable Field Semantics):**
- TTuple [] as unit type representation (no dedicated TUnit in Type system)
- RecordValue uses Map<string, Value ref> for mutable field support
- Module-level let for sequencing mutations in tests (no in keyword at module level)

**From 04-01 (GADT Foundation):**
- GadtConstructorDecl AST node with argTypes list and explicit returnType
- TEData TypeExpr variant for parameterized named types (int expr) separate from TEName
- ConstructorInfo extended with IsGadt bool and ExistentialVars int list
- GADT constructor parsed via splitGadt: single grammar rule to avoid LALR conflicts
- Existential vars = arg type vars minus result type vars
- GADT error codes use E04xx range (E0401-E0403)

**From 04-02 (GADT Elaboration):**
- Constructor-local type vars get fresh indices via freshTypeVarIndex, extending paramMap per-constructor
- IsGadt sweep: if any constructor uses GADT syntax, ALL constructors marked IsGadt=true
- inferTypeFromPatterns builds generic type from TData name for GADT constructors (exhaustiveness)

### Pending Todos

None yet.

### Blockers/Concerns

**Phase 1 dependencies:**
- Indentation lexer state management (Python algorithm well-documented, low risk)
- Spaces-only enforcement critical for correctness

**From 01-04:**
- **Nested indentation-based let:** Current implementation requires explicit `in` keywords for nested let bindings inside indented blocks. Full indentation-based `let` sequences not yet supported. Workaround: use explicit `in` keywords

**Phase 4 (GADT) known challenges:**
- Type inference undecidability requires mandatory annotations
- Rigid type variable scope checking needed

**Phase 5 (Modules) known challenges:**
- Circular dependency detection required
- Two-phase compilation design needed

## Session Continuity

Last session: 2026-03-09 (Phase 4 in progress)
Stopped at: Completed 04-02-PLAN.md (GADT elaboration)
Resume file: None

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-09 06:57 UTC*
