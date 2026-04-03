---
phase: 80-type-environment-export
verified: 2026-04-03T03:15:37Z
status: passed
score: 3/3 must-haves verified
re_verification: false
---

# Phase 80: Type Environment Export Verification Report

**Phase Goal:** Top-level binding types (user-defined and builtin/prelude) are accessible as a named collection
**Verified:** 2026-04-03T03:15:37Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth                                                                                             | Status     | Evidence                                                                                                                      |
| --- | ------------------------------------------------------------------------------------------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------------- |
| 1   | Top-level let bindings have their name and TypeScheme in the exported env after type-checking     | VERIFIED  | Tests TE-01 (x:int, f:int->int, id polymorphic, foo:string) query `Map.tryFind` on the returned env and confirm correct schemes |
| 2   | Builtin functions (print, string_length, etc.) are present in the same env                        | VERIFIED  | Line 1282 TypeCheck.fs merges `initialTypeEnv` before `typeCheckDecls`; TE-02 tests confirm `print`, `string_length`, and all `initialTypeEnv` keys are present |
| 3   | The exported env is queryable by binding name using Map.tryFind from outside TypeCheck module      | VERIFIED  | TypeEnvTests.fs calls `Map.tryFind` on `TypeCheck.BindingEnv` results; `exportBindingEnv` is also called directly from tests |

**Score:** 3/3 truths verified

### Required Artifacts

| Artifact                                          | Expected                                         | Status    | Details                                                            |
| ------------------------------------------------- | ------------------------------------------------ | --------- | ------------------------------------------------------------------ |
| `src/FunLang/TypeCheck.fs`                        | BindingEnv type alias and exportBindingEnv helper | VERIFIED | `type BindingEnv = TypeEnv` at line 1302; `exportBindingEnv` at line 1308; both additive, no existing code changed |
| `tests/FunLang.Tests/TypeEnvTests.fs`             | TE-01 and TE-02 verification tests               | VERIFIED  | 124-line file with 9 tests covering user bindings, builtins, coexistence, identity, and queryability |
| `tests/FunLang.Tests/FunLang.Tests.fsproj`        | TypeEnvTests.fs included before Program.fs        | VERIFIED  | Line 16 confirms `<Compile Include="TypeEnvTests.fs" />` between TypeAnnotationTests.fs (line 15) and Program.fs (line 17) |

### Key Link Verification

| From                              | To                             | Via                               | Status   | Details                                                    |
| --------------------------------- | ------------------------------ | --------------------------------- | -------- | ---------------------------------------------------------- |
| `tests/.../TypeEnvTests.fs`       | `TypeCheck.typeCheckModule`    | direct call in test helper        | WIRED    | Lines 35 and 107 call `TypeCheck.typeCheckModule` directly |
| `tests/.../TypeEnvTests.fs`       | `TypeCheck.initialTypeEnv`     | builtin verification loop         | WIRED    | Line 92 iterates `TypeCheck.initialTypeEnv` to verify all builtins present |
| `tests/.../TypeEnvTests.fs`       | `TypeCheck.exportBindingEnv`   | identity test                     | WIRED    | Line 110 calls `TypeCheck.exportBindingEnv typeEnv` and asserts equality |

### Requirements Coverage

| Requirement                                           | Status    | Notes                                               |
| ----------------------------------------------------- | --------- | --------------------------------------------------- |
| TE-01: user let bindings in env with correct scheme   | SATISFIED | 4 test cases covering int, int->int, polymorphic, string |
| TE-02: builtins in returned env                       | SATISFIED | 3 test cases; exhaustive check iterates all initialTypeEnv keys |
| TE-03 (queryability from outside module)              | SATISFIED | Map.tryFind used throughout tests from external module |

### Anti-Patterns Found

None. No TODO/FIXME/placeholder patterns in TypeCheck.fs additions or TypeEnvTests.fs.

### Human Verification Required

None. All three truths are fully verifiable through code inspection and test execution.

## Test Execution Results

- `dotnet test ... --filter "TypeEnv"`: Passed 9/9 TypeEnv tests
- `dotnet test ...` (full suite): Passed 239/239 (no regressions)

## Gaps Summary

No gaps. All three must-have truths are verified at all three levels (existence, substantive, wired). The `BindingEnv` type alias and `exportBindingEnv` function exist in TypeCheck.fs and are substantive (documented identity wrapper with clear intent). Tests exercise the full goal: user bindings appear, builtins appear, and the map is queryable by name from outside the TypeCheck module.

---

_Verified: 2026-04-03T03:15:37Z_
_Verifier: Claude (gsd-verifier)_
