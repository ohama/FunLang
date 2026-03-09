# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-25)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** Phase 2 - Algebraic Data Types

## Current Position

Phase: 2 of 6 (Algebraic Data Types)
Plan: 05 of 5 in current phase (02-05 complete, 02-01 through 02-04 pending)
Status: In progress
Last activity: 2026-03-09 - Completed 02-05-PLAN.md (runtime evaluation of ADT values)

Progress: [██░░░░░░░░] 21%

## Performance Metrics

**Velocity:**
- Total plans completed: 5
- Average duration: 9.1 min
- Total execution time: 0.76 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01 | 4 | 37 min | 9.3 min |
| 02 | 1 | 8 min | 8.0 min |

**Recent Trend:**
- Last 5 plans: 01-04 (12min), 01-02 (13min), 01-03 (3min), 02-05 (8min)
- Trend: Consistent velocity

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

**From 02-05 (ADT Runtime Evaluation):**
- Uppercase IDENT parsed as Constructor, lowercase as Var - simple lexer-level disambiguation
- Constructor takes optional single argument (nullary vs unary); multi-field uses tuple
- Tests skip type checking since ADT type infrastructure not yet complete (parallel plan dependency)
- Stub ConstructorPat/Constructor handling added to Infer.fs for compilation compatibility

### Pending Todos

None yet.

### Blockers/Concerns

**Phase 1 dependencies:**
- Indentation lexer state management (Python algorithm well-documented, low risk)
- Spaces-only enforcement critical for correctness

**From 01-04:**
- **Nested indentation-based let:** Current implementation requires explicit `in` keywords for nested let bindings inside indented blocks. Full indentation-based `let` sequences not yet supported. Workaround: use explicit `in` keywords

**From 02-05:**
- **Bidir.fs incomplete pattern warning:** Constructor variant in Expr causes FS0025 warning in Bidir.fs. Will be resolved when 02-03 adds Constructor handling to type checker.
- **Module-level let rec:** Parser Decl grammar does not support `let rec` at module level. Tests work around this using `let rec ... in` expression form.

**Phase 4 (GADT) known challenges:**
- Type inference undecidability requires mandatory annotations
- Rigid type variable scope checking needed

**Phase 5 (Modules) known challenges:**
- Circular dependency detection required
- Two-phase compilation design needed

## Session Continuity

Last session: 2026-03-09 01:14 UTC
Stopped at: Completed 02-05-PLAN.md
Resume file: None

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-09 01:14 UTC*
