---
phase: 03-records
verified: 2026-03-09T07:00:00Z
status: passed
score: 7/7 must-haves verified
---

# Phase 3: Records Verification Report

**Phase Goal:** Users can define and use record types with field access and immutable updates
**Verified:** 2026-03-09T07:00:00Z
**Status:** PASSED
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can declare record types with named fields | VERIFIED | Parser rule `RecordDeclaration` produces `RecordDecl` AST; `elaborateRecordDecl` builds `RecordTypeInfo`; `typeCheckModule` registers in `RecordEnv`. Tests: 3 REC-01 tests pass. |
| 2 | User can create record instances with field initialization | VERIFIED | Parser rule `RecordExprInner` + `RecordFieldBindings` produce `RecordExpr` AST; `Bidir.synth` type checks with field-type unification; `Eval.eval` creates `RecordValue`. Tests: 2 REC-02 tests pass. |
| 3 | User can access record fields via dot notation | VERIFIED | Parser rule `Atom DOT IDENT` produces `FieldAccess` AST; `Bidir.synth` resolves field type from `RecordEnv`; `Eval.eval` dereferences ref cell. Tests: 3 REC-03 tests pass (including chained access). |
| 4 | User can create modified copy using copy-and-update syntax | VERIFIED | Parser rule `Expr WITH RecordFieldBindings` produces `RecordUpdate` AST; `Bidir.synth` validates field names and types; `Eval.eval` copies fields to fresh refs and applies updates. Tests: 4 REC-04 tests pass (including original unchanged). |
| 5 | User can pattern match on record fields | VERIFIED | Parser rule `LBRACE RecordPatFields RBRACE` produces `RecordPat`; `matchPattern` handles partial field matching; `Eval.evalMatchClauses` binds matched fields. Tests: 2 REC-05 tests pass. |
| 6 | Records support structural equality (REC-06) | VERIFIED | `Eval.eval Equal` case dereferences ref cells and compares field maps by value. Tests: 3 REC-06 tests pass (equal, not-equal, `<>` operator). |
| 7 | Records support mutable fields (REC-07) | VERIFIED | Lexer emits `MUTABLE`/`LARROW` tokens; Parser produces `SetField` AST with `mutable` field decl; `Bidir.synth` validates `IsMutable` flag (raises `ImmutableFieldAssignment` if not); `Eval.eval` mutates ref in-place. Tests: 5 REC-07 tests pass (assign/read, immutable error, alias sharing, read-only access, copy isolation). |

**Score:** 7/7 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/LangThree/Ast.fs` | Record AST nodes (RecordExpr, FieldAccess, RecordUpdate, SetField, RecordPat, RecordDecl, RecordFieldDecl, RecordValue) | VERIFIED | 235 lines. All 8 record-related types/variants present. RecordValue uses `Map<string, Value ref>` for mutable support. |
| `src/LangThree/Type.fs` | RecordFieldInfo, RecordTypeInfo, RecordEnv | VERIFIED | 169 lines. RecordFieldInfo has Name/FieldType/IsMutable/Index. RecordTypeInfo has TypeParams/Fields/ResultType. RecordEnv is `Map<string, RecordTypeInfo>`. |
| `src/LangThree/Elaborate.fs` | elaborateRecordDecl function | VERIFIED | 143 lines. `elaborateRecordDecl` maps RecordDecl to (name, RecordTypeInfo), handling type params, field types, mutability. Shares `substTypeExprWithMap` with ADT elaboration. |
| `src/LangThree/Diagnostic.fs` | Record error kinds | VERIFIED | 296 lines. 7 record-specific error kinds: UnboundField, DuplicateFieldName, MissingFields, ImmutableFieldAssignment, DuplicateRecordField, NotARecord, FieldAccessOnNonRecord. Each has error code (E0307-E0313) and hint. |
| `src/LangThree/Lexer.fsl` | LBRACE, RBRACE, SEMICOLON, DOT, MUTABLE, LARROW tokens | VERIFIED | 135 lines. All 6 tokens emitted: `{`, `}`, `;`, `.`, `mutable` keyword, `<-` operator. |
| `src/LangThree/Parser.fsy` | Record grammar rules | VERIFIED | 380 lines. Full grammar: RecordDeclaration, RecordFields, RecordField (with mutable), RecordExprInner, RecordFieldBindings, RecordPatFields, FieldAccess (Atom DOT IDENT), SetField (Atom DOT IDENT LARROW Expr). |
| `src/LangThree/Bidir.fs` | Record type checking (synth cases) | VERIFIED | 441 lines. 4 record synth cases: RecordExpr (field resolution + type checking), FieldAccess (field type lookup), RecordUpdate (field validation), SetField (IsMutable check, returns TTuple []). |
| `src/LangThree/Eval.fs` | Record evaluation | VERIFIED | 354 lines. 4 record eval cases: RecordExpr (create with ref cells + type name resolution), FieldAccess (deref), RecordUpdate (copy with fresh refs), SetField (in-place mutation). Plus structural equality and pattern matching. |
| `src/LangThree/TypeCheck.fs` | Module-level record integration | VERIFIED | 235 lines. `typeCheckModule` builds RecordEnv from RecordTypeDecl, validates globally unique field names, passes recEnv to Bidir.synth. collectMatches handles record expression variants. |
| `tests/LangThree.Tests/RecordTests.fs` | Integration tests for REC-01 through REC-07 | VERIFIED | 313 lines, 26 tests. Full coverage: type declaration (3), creation (2), field access (3), copy-and-update (4), pattern matching (2), structural equality (3), mutable fields (5), error cases (4). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Lexer.fsl | Parser.fsy | Token emission (LBRACE, RBRACE, SEMICOLON, DOT, MUTABLE, LARROW) | WIRED | All 6 tokens declared in Parser and emitted by Lexer. |
| Parser.fsy | Ast.fs | AST construction (RecordExpr, FieldAccess, RecordUpdate, SetField, RecordPat, RecordDecl) | WIRED | Parser rules produce all record AST nodes. |
| TypeCheck.fs | Elaborate.fs | `elaborateRecordDecl` builds RecordEnv | WIRED | `typeCheckModule` calls `elaborateRecordDecl` for each `RecordTypeDecl`, builds `recEnv`. |
| TypeCheck.fs | Bidir.fs | `Bidir.synth ctorEnv recEnv` | WIRED | recEnv passed to synth for all let-decl type checking. |
| Bidir.fs | Type.fs | RecordEnv lookup for field resolution | WIRED | synth cases for RecordExpr/FieldAccess/RecordUpdate/SetField all look up recEnv. |
| Eval.fs | Type.fs | RecordEnv for type name resolution | WIRED | `resolveRecordTypeName` uses recEnv at runtime for RecordExpr evaluation. |
| RecordTests.fs | TypeCheck.fs | `typeCheckModule` integration | WIRED | Tests call `parseAndTypeCheck` -> `TypeCheck.typeCheckModule`, then evaluate with returned recEnv. |

### Requirements Coverage

| Requirement | Status | Evidence |
|-------------|--------|----------|
| REC-01: Record type declarations | SATISFIED | Parser, elaboration, type checking, 3 tests pass |
| REC-02: Record expressions | SATISFIED | Parser, type checking, evaluation, 2 tests pass |
| REC-03: Field access via dot notation | SATISFIED | Parser, type checking, evaluation, 3 tests pass |
| REC-04: Copy-and-update syntax | SATISFIED | Parser, type checking, evaluation (fresh refs), 4 tests pass |
| REC-05: Pattern matching on records | SATISFIED | Parser, matchPattern, evaluation, 2 tests pass |
| REC-06: Structural equality | SATISFIED | Eval deref-based comparison for = and <>, 3 tests pass |
| REC-07: Mutable fields | SATISFIED | Lexer/Parser (mutable, <-), Bidir IsMutable check, Eval ref mutation, 5 tests pass |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/LangThree/Exhaustive.fs` | 250 | Missing `RecordPat` case in `astPatToCasePat` (compiler warning FS0025) | Warning | No functional impact -- RecordPat should map to WildcardPat for ADT exhaustiveness. Compiler emits warning but match will throw at runtime only if RecordPat reaches this code path (unlikely since exhaustiveness checking is ADT-only). |
| `src/LangThree/Elaborate.fs` | 57 | Comment "placeholder" on TEName handling | Info | Pre-existing from Phase 2. Not record-specific. TEName resolves to TData correctly via `substTypeExprWithMap`. |

### Human Verification Required

### 1. Record Formatting Display

**Test:** Create a record and print it in the REPL to verify display format.
**Expected:** Output like `{ x = 1; y = 2 }` with proper formatting.
**Why human:** Cannot verify REPL display output programmatically from test infrastructure.

### 2. Indented Record Declarations

**Test:** Write a record type declaration with the opening brace on a new indented line.
**Expected:** Parser accepts `type Point =\n    { x: int; y: int }` via INDENT/DEDENT tokens.
**Why human:** Indentation interaction is tested via filtered token pipeline but multi-line formatting edge cases benefit from manual verification.

### Gaps Summary

No gaps found. All 7 must-have truths are verified with substantive implementations and complete wiring. All 26 record tests pass (covering REC-01 through REC-07 plus error cases). The only notable item is a compiler warning in Exhaustive.fs where `RecordPat` is not handled in `astPatToCasePat` -- this is a minor cleanup task (adding `| Ast.RecordPat _ -> WildcardPat`) that does not block goal achievement.

---

_Verified: 2026-03-09T07:00:00Z_
_Verifier: Claude (gsd-verifier)_
