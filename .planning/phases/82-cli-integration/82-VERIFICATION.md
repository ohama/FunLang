---
phase: 82-cli-integration
verified: 2026-04-03T03:45:28Z
status: passed
score: 5/5 must-haves verified
---

# Phase 82: CLI Integration Verification Report

**Phase Goal:** Users can invoke --emit-typed-ast on any FunLang file and receive JSON type information on stdout
**Verified:** 2026-04-03T03:45:28Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth                                                                                        | Status     | Evidence                                                                                                  |
| --- | -------------------------------------------------------------------------------------------- | ---------- | --------------------------------------------------------------------------------------------------------- |
| 1   | fn --emit-typed-ast file.fun exits 0 and prints valid JSON to stdout                        | VERIFIED   | CLI run on /tmp/verify-82.fun: exit 0, JSON stdout confirmed; stdout empty on error case                  |
| 2   | The JSON contains 'annotations' array with span+type entries for user file expressions       | VERIFIED   | JSON parsed: `annotations: 1`, entry `{'span': {'startLine':1,'startCol':6,...}, 'type': 'int'}`          |
| 3   | The JSON contains 'bindings' object with user-defined top-level binding types                | VERIFIED   | JSON parsed: `bindings: ['x']` for `let x = 42`; multi-binding test shows only `['a', 'b']`              |
| 4   | fn --emit-typed-ast on a file with a type error exits non-zero with error on stderr, no JSON | VERIFIED   | Exit code 1, stdout empty, stderr contains `error[E0301]: Type mismatch: expected int but got bool`       |
| 5   | Prelude/builtin spans and bindings are excluded from JSON output                             | VERIFIED   | `let a = 42; let b = "hello"` produces bindings `['a', 'b']` only — no prelude/builtin entries           |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact                                      | Expected                                       | Status   | Details                                                          |
| --------------------------------------------- | ---------------------------------------------- | -------- | ---------------------------------------------------------------- |
| `src/FunLang/Cli.fs`                          | Emit_Typed_Ast DU case + usage string          | VERIFIED | Lines 28, 44: `Emit_Typed_Ast` case and usage description        |
| `src/FunLang/Program.fs`                      | serializeTypedModule + --emit-typed-ast branch | VERIFIED | 532 lines; function at line 96, handler branch at line 358       |
| `tests/flt/emit/typed-ast/basic.flt`          | Success case integration test                  | VERIFIED | Exact JSON output match; `scripts/fslit` reports PASS            |
| `tests/flt/emit/typed-ast/type-error.flt`     | Type error case integration test               | VERIFIED | ExitCode: 1 + StderrContains check; `scripts/fslit` reports PASS |

### Key Link Verification

| From                      | To                         | Via                                  | Status   | Details                                                                          |
| ------------------------- | -------------------------- | ------------------------------------ | -------- | -------------------------------------------------------------------------------- |
| `src/FunLang/Program.fs`  | `ExportApi.typeCheckFile`  | call in Emit_Typed_Ast branch        | WIRED    | Line 362: `let typed = ExportApi.typeCheckFile filename`                         |
| `src/FunLang/Program.fs`  | `System.Text.Json.Nodes`   | open at top + JsonObject/JsonArray   | WIRED    | Line 3: `open System.Text.Json.Nodes`; lines 100-121 use JsonObject/JsonArray    |
| `src/FunLang/Program.fs`  | `Type.formatTypeNormalized`| type-to-string in annotations loop   | WIRED    | Line 112: `Type.formatTypeNormalized kv.Value`                                   |
| `src/FunLang/Program.fs`  | `Type.formatSchemeNormalized` | scheme-to-string in bindings loop | WIRED    | Line 119: `Type.formatSchemeNormalized kv.Value`                                 |

### Requirements Coverage

All success criteria from PLAN satisfied:

| Requirement                                                          | Status    |
| -------------------------------------------------------------------- | --------- |
| --emit-typed-ast flag fully functional end-to-end                    | SATISFIED |
| JSON includes per-expression span->type annotations (user file only) | SATISFIED |
| JSON includes top-level user binding types (no prelude/builtins)     | SATISFIED |
| Type errors produce clean stderr message + exit 1, no JSON on stdout | SATISFIED |
| flt integration tests cover success and error paths                  | SATISFIED |
| All existing tests continue to pass (711/712, 1 pre-existing failure)| SATISFIED |

### Anti-Patterns Found

None. No TODO/FIXME/placeholder patterns found in modified files. Handler is fully implemented with real ExportApi call, JSON serialization, and correct error handling.

### Human Verification Required

None. All goal behaviors were verified programmatically and via direct CLI execution.

## Gaps Summary

No gaps. All five observable truths verified, all artifacts present and substantive, all key links wired and confirmed by runtime execution.

## Runtime Evidence

**Success case (`let x = 42`):**
```
$ fn --emit-typed-ast /tmp/verify-82.fun
{"annotations":[{"span":{"startLine":1,"startCol":6,"endLine":1,"endCol":10},"type":"int"}],"bindings":{"x":"int"}}
EXIT_CODE: 0
```

**Error case (`let x = 1 + true`):**
```
$ fn --emit-typed-ast /tmp/verify-82-err.fun
# stdout: (empty)
# stderr: Error: error[E0301]: Type mismatch: expected int but got bool
#          --> /tmp/verify-82-err.fun:1:6-16 ...
EXIT_CODE: 1
```

**Integration tests:**
```
$ scripts/fslit tests/flt/emit/typed-ast/
PASS: tests/flt/emit/typed-ast/basic.flt
PASS: tests/flt/emit/typed-ast/type-error.flt
Results: 2/2 passed, 0 failed
```

---

_Verified: 2026-04-03T03:45:28Z_
_Verifier: Claude (gsd-verifier)_
