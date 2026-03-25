---
phase: 36-bug-fixes
verified: 2026-03-25T12:00:00Z
status: passed
score: 4/4 must-haves verified
---

# Phase 36: Bug Fixes Verification Report

**Phase Goal:** `Module.func` qualified access works from imported files and Prelude, and `try failwith "x" with e -> y` parses without error
**Verified:** 2026-03-25T12:00:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth                                                                          | Status     | Evidence                                                                                   |
| --- | ------------------------------------------------------------------------------ | ---------- | ------------------------------------------------------------------------------------------ |
| 1   | After `open "file.fun"`, `Module.func` resolves correctly (no E0313)           | VERIFIED   | Smoke test: `Math.square 5` returns `25`; MOD-01 regression tests pass in 224-test suite  |
| 2   | `List.length`, `List.map` from Prelude resolve correctly (no E0313)            | VERIFIED   | Smoke tests: `List.length [1;2;3]` → `3`; `List.map (fun x -> x+1) [1;2;3]` → `[2;3;4]` |
| 3   | `try failwith "boom" with e -> "caught"` parses and evaluates without error    | VERIFIED   | Smoke test: outputs `"caught"`; PAR-01 regression tests pass                              |
| 4   | All 214+ F# unit tests and 468+ flt tests continue to pass                     | VERIFIED   | `dotnet test`: 224 passed, 0 failed; `fslit tests/flt/`: 468/468 passed                   |

**Score:** 4/4 truths verified

**Note on `List.head`:** The ROADMAP success criterion lists `List.head` but the Prelude only defines `List.hd` (not `head`). `List.hd [10;20;30]` returns `10` correctly. This is a documentation discrepancy in the goal statement — the qualified access mechanism works correctly; the function simply has a different name (`hd`) in the Prelude.

### Required Artifacts

| Artifact                                   | Expected                                                                 | Status     | Details                                                                                   |
| ------------------------------------------ | ------------------------------------------------------------------------ | ---------- | ----------------------------------------------------------------------------------------- |
| `src/LangThree/TypeCheck.fs`               | 5-arg `fileImportTypeChecker`; `typeCheckModuleWithPrelude` w/ initialModules | VERIFIED | 875 lines; delegate at line 598 is 5-arg returning 4-tuple; `typeCheckModuleWithPrelude` at line 846 accepts `initialModules` param |
| `src/LangThree/Prelude.fs`                 | `PreludeResult` with `Modules`/`ModuleValueEnv`; `loadAndTypeCheckFileImpl` 4-tuple | VERIFIED | 193 lines; `PreludeResult` at line 13 has both fields; `loadAndTypeCheckFileImpl` at line 83 is 5-arg returning 4-tuple |
| `src/LangThree/Program.fs`                 | Passes `prelude.Modules` and `prelude.ModuleValueEnv` to pipeline        | VERIFIED   | 242 lines; lines 108, 200 pass `prelude.Modules`; line 221 passes `prelude.ModuleValueEnv` |
| `Prelude/List.fun`                         | Content wrapped in `module List = ...` with `open List` at bottom        | VERIFIED   | Starts with `module List =`; ends with `open List`                                        |
| `Prelude/Core.fun`                         | Wrapped in `module Core = ...` with `open Core`                          | VERIFIED   | Starts with `module Core =`                                                               |
| `Prelude/Option.fun`                       | Wrapped in `module Option = ...` with `open Option`                      | VERIFIED   | Starts with `module Option =`                                                             |
| `Prelude/Result.fun`                       | Wrapped in `module Result = ...` with `open Result`                      | VERIFIED   | Starts with `module Result =`                                                             |
| `src/LangThree/Parser.fsy`                 | Option A TRY rules for bare `IDENT ARROW Expr`; no new S/R conflicts     | VERIFIED   | 713 lines; lines 204–207 add two `TRY Expr WITH IDENT ARROW Expr` rules; build: 0 errors, 0 new conflicts |
| `tests/LangThree.Tests/ModuleTests.fs`     | `evalWithPrelude` helper + MOD-01/MOD-02 regression tests                 | VERIFIED   | 353 lines; `evalWithPrelude` at line 295; MOD-02 tests at line 316; MOD-01 tests at line 335 |
| `tests/LangThree.Tests/ExceptionTests.fs`  | PAR-01 regression tests for inline try-with                               | VERIFIED   | 220 lines; PAR-01 testList at line 178 with 4 tests                                       |

### Key Link Verification

| From                           | To                              | Via                                               | Status     | Details                                                          |
| ------------------------------ | ------------------------------- | ------------------------------------------------- | ---------- | ---------------------------------------------------------------- |
| `TypeCheck.fs / FileImportDecl` | `fileImportTypeChecker` delegate | 5-arg call passing `mods`, returns 4-tuple        | WIRED      | Line 801: `fileImportTypeChecker resolvedPath cEnv rEnv env mods` returns `(env', cEnv', rEnv', fileMods)` |
| `Prelude.fs / loadAndTypeCheckFileImpl` | `TypeCheck.fileImportTypeChecker` | Delegate assignment in `do` block             | WIRED      | Line 144: `TypeCheck.fileImportTypeChecker <- loadAndTypeCheckFileImpl` |
| `Prelude.fs / loadPrelude`     | `PreludeResult.Modules`          | Accumulates `mergedModules` from each file parse  | WIRED      | Line 173 accumulates into `mergedModules`; line 185 sets `Modules = mergedModules` |
| `Program.fs`                   | `typeCheckModuleWithPrelude`     | Passes `prelude.Modules` as `initialModules`      | WIRED      | Lines 108 and 200 both pass `prelude.Modules` as 4th argument   |
| `Program.fs`                   | `Eval.evalModuleDecls`           | Passes `prelude.ModuleValueEnv` as initial modEnv | WIRED      | Line 221: `Eval.evalModuleDecls mergedRecEnv prelude.ModuleValueEnv initialEnv moduleDecls` |
| `Parser.fsy / TRY rules`       | Inline try-with AST              | `TRY Expr WITH IDENT ARROW Expr` → `TryWith`      | WIRED      | Lines 204–207: two rules produce `TryWith(_, [(VarPat(...), None, ...)], ...)` |

### Requirements Coverage

| Requirement | Status      | Notes                                                                             |
| ----------- | ----------- | --------------------------------------------------------------------------------- |
| MOD-01      | SATISFIED   | Imported file qualified access: `Math.square 5` → `25` after `open "math.fun"`  |
| MOD-02      | SATISFIED   | Prelude qualified access: `List.length`, `List.map`, `List.hd` all work          |
| PAR-01      | SATISFIED   | `try failwith "boom" with e -> "caught"` parses and evaluates to `"caught"`      |

### Anti-Patterns Found

None found in the modified files. No TODO/FIXME/placeholder patterns. No stub implementations.

### Human Verification Required

None — all success criteria are verifiable programmatically and all checks passed.

## Test Results

| Test Suite    | Result         | Count            |
| ------------- | -------------- | ---------------- |
| F# unit tests | PASSED         | 224/224 (0 fail) |
| flt tests     | PASSED         | 468/468 (0 fail) |

## Gaps Summary

No gaps. All four observable truths verified, all artifacts are substantive and wired, both test suites pass, and smoke tests confirm correct runtime behavior.

---

_Verified: 2026-03-25T12:00:00Z_
_Verifier: Claude (gsd-verifier)_
