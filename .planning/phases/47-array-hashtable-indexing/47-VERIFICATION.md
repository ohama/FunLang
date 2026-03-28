---
phase: 47-array-hashtable-indexing
verified: 2026-03-28T00:12:57Z
status: passed
score: 6/6 must-haves verified
---

# Phase 47: Array and Hashtable Indexing Verification Report

**Phase Goal:** Users can read and write array/hashtable elements with `.[i]` syntax instead of calling `array_get`/`array_set` functions
**Verified:** 2026-03-28T00:12:57Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth                                                                           | Status     | Evidence                                                                 |
| --- | ------------------------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------ |
| 1   | User can write `arr.[i]` to read an array element and get the value back        | VERIFIED   | index-array-read.flt passes; Bidir/Eval IndexGet fully implemented       |
| 2   | User can write `arr.[i] <- v` to write an array element (returns unit)          | VERIFIED   | index-array-write.flt passes; IndexSet returns TTuple [] / TupleValue [] |
| 3   | User can write `ht.[key]` to read a hashtable value                             | VERIFIED   | index-hashtable-read.flt passes; Bidir THashtable branch in IndexGet     |
| 4   | User can write `ht.[key] <- v` to write a hashtable value (returns unit)        | VERIFIED   | index-hashtable-write.flt passes; Bidir THashtable branch in IndexSet    |
| 5   | User can write `matrix.[r].[c]` and chained indexing works left-associatively   | VERIFIED   | index-chained.flt passes; IndexGet in Atom production (left-recursive)   |
| 6   | Indexing a non-array/hashtable produces a type error (IndexOnNonCollection)     | VERIFIED   | index-type-error.flt passes; Bidir raises IndexOnNonCollection E0471     |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact                             | Expected                                              | Status     | Details                                                                  |
| ------------------------------------ | ----------------------------------------------------- | ---------- | ------------------------------------------------------------------------ |
| `src/LangThree/Lexer.fsl`            | DOTLBRACKET token rule before DOTDOT and DOT          | VERIFIED   | Line 153: `| ".["  { DOTLBRACKET }` before `..` (154) and `.` (156)     |
| `src/LangThree/Ast.fs`               | IndexGet and IndexSet Expr union cases with spans     | VERIFIED   | Lines 119-120: both cases; line 308: spanOf arms                        |
| `src/LangThree/Diagnostic.fs`        | IndexOnNonCollection error kind                       | VERIFIED   | Line 53: `| IndexOnNonCollection of ty: Type // E0471`; line 315: format |
| `src/LangThree/Bidir.fs`             | Type checking for IndexGet/IndexSet (TArray/THash dispatch) | VERIFIED | Lines 559-603: full TArray/THashtable/error dispatch for both nodes     |
| `src/LangThree/Eval.fs`              | Runtime evaluation of IndexGet (bounds check) and IndexSet | VERIFIED | Lines 779-807: bounds-checked array; key-not-found hashtable            |

### Key Link Verification

| From                   | To                    | Via                                              | Status   | Details                                               |
| ---------------------- | --------------------- | ------------------------------------------------ | -------- | ----------------------------------------------------- |
| `Lexer.fsl`            | `Parser.fsy`          | DOTLBRACKET token consumed by grammar rules      | WIRED    | Parser.fsy line 61: `%token DOTLBRACKET`; lines 253, 352: grammar rules |
| `Parser.fsy`           | `Ast.fs`              | IndexGet/IndexSet AST nodes produced by grammar  | WIRED    | Lines 254, 353: `{ IndexGet(...) }`, `{ IndexSet(...) }` |
| `IndentFilter.fs`      | `Parser.fsy`          | DOTLBRACKET increments BracketDepth              | WIRED    | Line 230: `Parser.LBRACKET \| ... \| Parser.DOTLBRACKET ->` depth +1    |

### Requirements Coverage

| Requirement | Status    | Notes                                                        |
| ----------- | --------- | ------------------------------------------------------------ |
| IDX-01      | SATISFIED | `arr.[i]` read — index-array-read.flt passes                 |
| IDX-02      | SATISFIED | `arr.[i] <- v` write — index-array-write.flt passes          |
| IDX-03      | SATISFIED | `ht.[key]` read — index-hashtable-read.flt passes            |
| IDX-04      | SATISFIED | `ht.[key] <- v` write — index-hashtable-write.flt passes     |
| IDX-05      | SATISFIED | `matrix.[r].[c]` chained — index-chained.flt passes          |

### Anti-Patterns Found

None. Build reports 0 warnings, 0 errors. No TODO/FIXME/placeholder patterns in modified files for IndexGet/IndexSet code paths. Infer.fs stub is intentional (HM path, primary checker is Bidir) and documented with a comment.

### Human Verification Required

None. All goal behaviors are verified programmatically via flt integration tests and build output.

### Test Results

| Suite                                     | Result        |
| ----------------------------------------- | ------------- |
| `dotnet build` (Release)                  | 0 errors, 0 warnings |
| `dotnet test` unit tests                  | 224/224 pass  |
| `FsLit tests/flt/expr/indexing/`          | 7/7 pass      |
| `FsLit tests/flt/` (full suite)           | 570/570 pass  |

### Summary

All 6 must-have truths verified. Phase 47 delivers complete `.[i]` indexing syntax:

- DOTLBRACKET lexed as single token (same strategy as F# compiler) prevents LALR conflict
- IndexGet in Atom grammar production enables left-recursive chained indexing (`matrix.[r].[c]`)
- IndexSet in Expr grammar mirrors SetField placement; returns unit type
- Bidir.fs dispatches on TArray (int index) vs THashtable (key-type index) with E0471 on non-collections
- Eval.fs provides bounds-checked array access and key-not-found hashtable access at runtime
- All 10 modified source files and 7 new flt test files are wired and substantive
- Zero regressions: full 570-test flt suite and 224-test unit suite pass

---

_Verified: 2026-03-28T00:12:57Z_
_Verifier: Claude (gsd-verifier)_
