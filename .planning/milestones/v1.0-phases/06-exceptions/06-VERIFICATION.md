---
phase: 06-exceptions
verified: 2026-03-09T12:00:00Z
status: passed
score: 5/5 must-haves verified
gaps: []
---

# Phase 6: Exceptions Verification Report

**Phase Goal:** Users can declare exceptions and handle errors with try-with expressions
**Verified:** 2026-03-09
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can declare custom exceptions with data | VERIFIED | Parser rules for `exception Name` and `exception Name of Type` in Parser.fsy:355-362; ExceptionDecl AST node; elaborateExceptionDecl in Elaborate.fs:204; 4 passing tests (EXC-01 group) |
| 2 | User can raise exceptions with `raise` | VERIFIED | Raise AST node; Bidir.fs synth checks arg unifies with TExn (line 313); Eval.fs throws LangThreeException (line 420-422); 5 passing tests (EXC-02 group) including type error for non-exn |
| 3 | User can catch exceptions with `try...with` | VERIFIED | TryWith AST node; Bidir.fs synth checks body/handler types unify (line 321); Eval.fs wraps body in F# try-with catching LangThreeException (line 423-433); InTry indent context in IndentFilter.fs; 4 passing tests (EXC-03 group) |
| 4 | User can pattern match on exception types in handlers | VERIFIED | TryWith handlers are MatchClause list; evalMatchClauses dispatches on pattern match (line 112-127); supports multiple handlers with first-match-wins, wildcard catch-all, nested try-with with re-raise; 5 passing tests (EXC-04 group) |
| 5 | User can use `when` guards in exception handlers | VERIFIED | MatchClause is 3-tuple (Pattern * Expr option * Expr); Parser.fsy:234-236 parses `| pat WHEN guard ARROW body`; evalMatchClauses evaluates guard in extended env and skips clause if not true (line 119-125); Bidir.fs checks guard type is TBool; 3 passing tests (EXC-05 group) |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/LangThree/Ast.fs` | Raise, TryWith, ExceptionDecl, MatchClause 3-tuple | VERIFIED | Lines 100-101 (Raise, TryWith), 229 (ExceptionDecl), MatchClause is Pattern * Expr option * Expr |
| `src/LangThree/Type.fs` | TExn type variant | VERIFIED | Line 14; integrated in formatType, apply, freeVars |
| `src/LangThree/Diagnostic.fs` | E0601-E0604, W0003 | VERIFIED | Lines 42-49 (error kinds), 282-302 (error codes) |
| `src/LangThree/Lexer.fsl` | exception/raise/try/when keywords | VERIFIED | Lines 67-70 |
| `src/LangThree/Parser.fsy` | Grammar rules for exceptions | VERIFIED | Token declarations (line 54), TryWith rules (118-119), Raise (137), when guard clauses (234-236), ExceptionDecl (355-362) |
| `src/LangThree/Elaborate.fs` | elaborateExceptionDecl | VERIFIED | Line 204, creates ConstructorInfo with ResultType=TExn |
| `src/LangThree/Bidir.fs` | Raise/TryWith synth, when guard checking | VERIFIED | Raise synth (311-317), TryWith synth (320-338), when guard TBool check in multiple contexts |
| `src/LangThree/TypeCheck.fs` | ExceptionDecl processing | VERIFIED | First pass elaboration (522-527), ctorEnv/recEnv threaded through fold state |
| `src/LangThree/Exhaustive.fs` | TExn open type handling | VERIFIED | No TExn match needed (open type returns empty constructor set via getConstructorsFromEnv) |
| `src/LangThree/Eval.fs` | LangThreeException, raise/try-with eval, when guards | VERIFIED | LangThreeException (7), Raise eval (420-422), TryWith eval with re-raise (423-433), when guard eval in evalMatchClauses (119-125), ExceptionDecl module eval (527+) |
| `src/LangThree/IndentFilter.fs` | InTry context | VERIFIED | InTry union case (18), JustSawTry state (27), context entry (104-105), pipe alignment (167), DEDENT popping with strict < (150) |
| `src/LangThree/Unify.fs` | TExn unification | VERIFIED | Line 20: `TExn, TExn -> empty` |
| `tests/LangThree.Tests/ExceptionTests.fs` | Integration tests | VERIFIED | 194 lines, 29 tests covering all 5 requirements plus when guards in match and edge cases |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Parser.fsy | Ast.fs | ExceptionDecl/Raise/TryWith constructors | WIRED | Parser rules produce AST nodes directly |
| TypeCheck.fs | Elaborate.fs | elaborateExceptionDecl call | WIRED | Line 523 calls Elaborate.elaborateExceptionDecl |
| Bidir.fs | Unify.fs | TExn unification in Raise/TryWith | WIRED | Raise synth unifies arg with TExn; TryWith unifies handler patterns with TExn |
| Eval.fs | .NET exceptions | LangThreeException type | WIRED | raise throws LangThreeException; TryWith catches it; re-raises when no handler matches |
| evalMatchClauses | when guards | Guard evaluation in extended env | WIRED | Pattern bindings extend env, guard evaluated, clause skipped if not BoolValue true |
| IndentFilter.fs | Parser tokens | InTry context for pipe alignment | WIRED | JustSawTry set on TRY token, InTry context entered, pipes aligned at base column |
| ExceptionTests.fs | Test project | fsproj Compile Include | WIRED | LangThree.Tests.fsproj line 13 includes ExceptionTests.fs |

### Requirements Coverage

| Requirement | Status | Blocking Issue |
|-------------|--------|----------------|
| EXC-01: Exception declarations | SATISFIED | None -- 4 tests verify nullary, with-data, tuple-data, multiple declarations |
| EXC-02: `raise` function | SATISFIED | None -- 5 tests verify raise nullary, with data, in branches, type error |
| EXC-03: `try...with` expressions | SATISFIED | None -- 4 tests verify catch, data extraction, passthrough, let-in body |
| EXC-04: Pattern matching on exception types | SATISFIED | None -- 5 tests verify multiple handlers, first-match, wildcard, nested inner/outer |
| EXC-05: `when` guards in exception handlers | SATISFIED | None -- 3 tests verify guard selection, fallthrough, wildcard fallback |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None | - | - | - | No TODO, FIXME, failwith TODO, or placeholder patterns found in any exception-related code |

Zero `failwith "TODO"` stubs remain. All stubs from Plan 01 were replaced by real implementations in Plans 02 and 03.

### Human Verification Required

### 1. Multi-line try-with indentation

**Test:** Write a multi-line try-with expression with multiple handlers and verify indentation parsing works correctly in the REPL or file-based execution.
**Expected:** Parser correctly handles INDENT/DEDENT transitions between try body and with handlers.
**Why human:** IndentFilter context management involves runtime token stream state that is difficult to fully verify via grep.

### 2. Error message quality

**Test:** Trigger each diagnostic (E0601-E0604, W0003) and verify the error messages are clear and helpful.
**Expected:** Error messages accurately describe the problem with appropriate context.
**Why human:** Message quality is subjective and requires reading the output.

### Gaps Summary

No gaps found. All 5 success criteria are verified with real implementations (no stubs), correct wiring between all components, and comprehensive test coverage (29 tests, all passing as part of the 178 total test suite). Four bugs were discovered and fixed during integration testing (TExn unification, InTry context popping, re-raise on no match, open directive ctorEnv/recEnv propagation), demonstrating that the test suite effectively validates the implementation.

---

_Verified: 2026-03-09_
_Verifier: Claude (gsd-verifier)_
