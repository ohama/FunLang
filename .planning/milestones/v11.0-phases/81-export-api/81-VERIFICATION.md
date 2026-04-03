---
phase: 81-export-api
verified: 2026-04-03T03:30:06Z
status: passed
score: 3/3 must-haves verified
---

# Phase 81: Export API Verification Report

**Phase Goal:** External callers can type-check a FunLang file and receive a single TypedModule record containing all type information
**Verified:** 2026-04-03T03:30:06Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth                                                                                              | Status     | Evidence                                                                                    |
|----|----------------------------------------------------------------------------------------------------|------------|---------------------------------------------------------------------------------------------|
| 1  | ExportApi.typeCheckFile accepts a file path and returns a TypedModule record without error         | VERIFIED   | Function exists at line 76 of ExportApi.fs; API-01 test confirms no exception on valid file |
| 2  | TypedModule contains annotation map, binding environment, and builtin schemes in one value         | VERIFIED   | Record defined at line 13 with AnnotationMap, BindingEnv, BuiltinSchemes fields; API-02 tests verify all three fields are populated |
| 3  | The API compiles and is accessible from outside FunLang.fsproj (e.g., a test or consumer project) | VERIFIED   | ExportApiTests.fs in FunLang.Tests calls ExportApi.typeCheckFile directly; all 5 tests pass; full suite of 244 tests passes |

**Score:** 3/3 truths verified

### Required Artifacts

| Artifact                                       | Expected                                              | Status       | Details                                                    |
|------------------------------------------------|-------------------------------------------------------|--------------|------------------------------------------------------------|
| `src/FunLang/ExportApi.fs`                     | Public module with TypedModule record and typeCheckFile | VERIFIED   | 99 lines; full implementation with lex, filter, parse, type-check, snapshot |
| `tests/FunLang.Tests/ExportApiTests.fs`        | Tests exercising typeCheckFile from outside FunLang   | VERIFIED     | 64 lines; 5 sequenced Expecto tests covering all 3 TypedModule fields |
| `src/FunLang/FunLang.fsproj`                   | ExportApi.fs compiled into assembly                   | VERIFIED     | Line 120: `<Compile Include="ExportApi.fs" />`              |
| `tests/FunLang.Tests/FunLang.Tests.fsproj`     | ExportApiTests.fs compiled into test assembly         | VERIFIED     | Line 17: `<Compile Include="ExportApiTests.fs" />`          |

### Key Link Verification

| From                    | To                                 | Via                                             | Status   | Details                                                               |
|-------------------------|------------------------------------|-------------------------------------------------|----------|-----------------------------------------------------------------------|
| ExportApi.typeCheckFile | TypeCheck.typeCheckModuleWithPrelude | Direct call at line 82                         | WIRED    | Passes prelude envs, returns Ok/Error discriminated union              |
| ExportApi.typeCheckFile | Bidir.annotationMap                | Seq + Map.ofSeq snapshot at line 91             | WIRED    | Snapshot taken immediately after typeCheckModuleWithPrelude succeeds  |
| ExportApi.typeCheckFile | TypeCheck.exportBindingEnv         | Call at line 96 with typeEnv result             | WIRED    | Produces BindingEnv (= TypeEnv) containing builtins + prelude + user  |
| ExportApi.typeCheckFile | TypeCheck.initialTypeEnv           | Direct reference at line 97                     | WIRED    | Assigned to BuiltinSchemes field                                      |
| ExportApiTests.fs       | ExportApi module                   | Direct call `ExportApi.typeCheckFile path`      | WIRED    | Tests in FunLang.Tests project; assembly references FunLang project   |

### Requirements Coverage

| Requirement                                                | Status    | Notes                                  |
|------------------------------------------------------------|-----------|----------------------------------------|
| typeCheckFile accepts file path, returns TypedModule       | SATISFIED | Verified by test API-01                |
| TypedModule.AnnotationMap populated from Bidir.annotationMap | SATISFIED | Verified by test API-02 (non-empty)   |
| TypedModule.BindingEnv contains user + prelude + builtins  | SATISFIED | Verified by tests API-02 (user binding 'answer', builtin 'print') |
| TypedModule.BuiltinSchemes contains initial builtins       | SATISFIED | Verified by test API-02 ('print' present) |
| API accessible from outside FunLang.fsproj                 | SATISFIED | Verified by test compilation and 244/244 pass |

### Anti-Patterns Found

None. No TODO/FIXME/placeholder patterns, no stub returns, no empty handlers in ExportApi.fs or ExportApiTests.fs.

### Human Verification Required

None. All goal criteria are mechanically verifiable and confirmed by the test suite.

## Summary

Phase 81 goal is fully achieved. `ExportApi.typeCheckFile` is a substantive 99-line implementation (not a stub) that:

1. Resolves the file path, reads the file, loads the prelude
2. Lexes and filters with position tracking (preserving accurate spans)
3. Calls `typeCheckModuleWithPrelude` with all required environments
4. Immediately snapshots `Bidir.annotationMap` into an immutable `Map<Span, Type>`
5. Returns a populated `TypedModule` record

Five Expecto tests in the separate test project exercise all three TypedModule fields and confirm the API is accessible from outside `FunLang.fsproj`. All 244 tests in the suite pass with no regressions.

---

_Verified: 2026-04-03T03:30:06Z_
_Verifier: Claude (gsd-verifier)_
