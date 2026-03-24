---
phase: 31-module-system
plan: 02
subsystem: testing
tags: [module-system, file-import, integration-tests, record-scoping, MOD-05]

# Dependency graph
requires:
  - phase: 31-module-system
    provides: plan 01 - FileImportDecl pipeline, mutable delegate pattern, cycle detection

provides:
  - fileImportTests test suite (5 tests) covering all Phase 31 success criteria
  - evalFileModule helper for evaluating .fun files in test context
  - MOD-05 verification: sibling module record types with shared field names are correctly scoped
  - SC-1 through SC-4 all verified by passing integration tests

affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - testSequenced wrapper for tests using shared mutable state (currentTypeCheckingFile, currentEvalFile)
    - evalFileModule helper: Prelude.emptyPrelude access triggers module init for delegate registration
    - save/restore pattern for currentTypeCheckingFile/currentEvalFile in evalFileModule

key-files:
  created: []
  modified:
    - tests/LangThree.Tests/ModuleTests.fs

key-decisions:
  - "MOD-05 recEnv scoping already correct in TypeCheck.fs: validateUniqueRecordFields only checks direct RecordTypeDecl in flat decl list (not inside ModuleDecl), and parent rEnv is returned unchanged from ModuleDecl arm — no fix needed"
  - "testSequenced wrapper required: file import tests use shared mutables (currentTypeCheckingFile, currentEvalFile); without sequencing, parallel tests cause path resolution races"
  - "Prelude module init trigger: Prelude.emptyPrelude access in evalFileModule ensures the do block runs, registering fileImportTypeChecker/fileImportEvaluator delegates before use"
  - "SC-4 test design: use 'open M1' to bring M1.Tok into recEnv; record literal {kind=id; value=42} resolves via exact field-set matching (not M2.Item which has {kind, count})"

patterns-established:
  - "Pattern: evalFileModule triggers Prelude init via Prelude.emptyPrelude access before calling TypeCheck.typeCheckModule"
  - "Pattern: file-I/O tests that use shared global mutables should be wrapped in testSequenced"

# Metrics
duration: 17min
completed: 2026-03-25
---

# Phase 31 Plan 02: Module System Integration Tests Summary

**5 integration tests proving SC-1 through SC-4 (file import, nested modules, same-named types, record scoping); MOD-05 verified correct with no source fix needed**

## Performance

- **Duration:** 17 min
- **Started:** 2026-03-24T22:05:23Z
- **Completed:** 2026-03-24T22:22:49Z
- **Tasks:** 2 of 2 (Task 1 = investigation, no code change; Task 2 = tests)
- **Files modified:** 1

## Accomplishments

- Added `evalFileModule` helper that reads a `.fun` file by path, triggers Prelude init, sets path mutables, and evaluates the last let binding
- Added `fileImportTests` test list (5 tests) wrapped in `testSequenced` to cover all Phase 31 success criteria
- Verified MOD-05: `validateUniqueRecordFields` only scans direct `RecordTypeDecl` nodes in the flat decl list — records inside `ModuleDecl` children are invisible to this check, so sibling modules can share field names safely
- All 214 tests pass (up from 209); pre-existing flaky test ("deep nested constructor match", composeCounter race) confirmed pre-existing

## Task Commits

1. **Task 1 + Task 2: MOD-05 investigation + fileImportTests suite** - `db3c3a4` (test)

## Files Created/Modified

- `tests/LangThree.Tests/ModuleTests.fs` - Added `evalFileModule` helper + `fileImportTests` test list (5 tests, testSequenced)

## Decisions Made

1. **MOD-05 no fix needed**: The `ModuleDecl` arm in `typeCheckDecls` returns the parent `rEnv` unchanged to the caller (line 731). Each sibling module's inner `typeCheckDecls` call starts with the unmodified parent `rEnv`. The `validateUniqueRecordFields` function only looks at top-level `RecordTypeDecl` nodes in the flat decl list passed to it — records inside `ModuleDecl` children are not scanned. So two sibling modules can each define a record type with a `kind` field without error.

2. **testSequenced wrapper**: The `evalFileModule` helper mutates `TypeCheck.currentTypeCheckingFile` and `Eval.currentEvalFile` (global mutable state). Without `testSequenced`, parallel test runs cause path resolution races where one test's file import resolves to another test's temp directory. `testSequenced` serializes the 5 file import tests while the rest of the suite continues running in parallel.

3. **Prelude init via `Prelude.emptyPrelude`**: The `fileImportTypeChecker` delegate in TypeCheck.fs is set by Prelude.fs's `do` block. This block only runs when the Prelude module is first accessed. Since existing tests only use TypeCheck/Eval/Parser/Lexer directly, the Prelude module may not be initialized when file import tests run. Accessing `Prelude.emptyPrelude` at the start of `evalFileModule` triggers the F# module initialization chain.

4. **SC-4 test design with `open M1`**: Module-internal records are not in the parent's `recEnv` automatically — they live in `exports.RecEnv`. To create a record literal that resolves to `M1.Tok`, we use `open M1` which merges `M1.RecEnv` (containing `Tok` with fields `{kind, value}`) into the current `recEnv`. The record literal `{ kind = "id"; value = 42 }` then resolves uniquely to `Tok` (exact field-set match: `{kind, value}`) rather than M2.Item (`{kind, count}`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] testSequenced wrapper required for race condition safety**

- **Found during:** Task 2 (running dotnet test)
- **Issue:** File import tests use shared global mutables (`currentTypeCheckingFile`, `currentEvalFile`). Parallel test execution caused path resolution races: test A would set `currentTypeCheckingFile` to its temp path, then test B would overwrite it before test A's `loadAndTypeCheckFileImpl` ran, causing `File.Exists` to fail on the wrong path.
- **Fix:** Wrapped `fileImportTests` in `testSequenced(...)` so the 5 file import tests run sequentially.
- **Files modified:** tests/LangThree.Tests/ModuleTests.fs
- **Verification:** 10 consecutive runs all pass
- **Committed in:** `db3c3a4`

**2. [Rule 1 - Bug] evalFileModule needed explicit Prelude init trigger**

- **Found during:** Task 2 (first test run: "FileImport type checker not initialized")
- **Issue:** The `fileImportTypeChecker` delegate in TypeCheck.fs defaults to an error stub. It's replaced by Prelude.fs's `do` block — but only when the Prelude F# module is first accessed. Test helpers only use TypeCheck/Eval/Parser, never touching Prelude, so the delegate remained as the error stub.
- **Fix:** Added `let _initPrelude = Prelude.emptyPrelude` at the start of `evalFileModule` to trigger Prelude module initialization.
- **Files modified:** tests/LangThree.Tests/ModuleTests.fs
- **Committed in:** `db3c3a4`

**3. [Rule 1 - Bug] 'end' keyword not supported; 'module X = ...' uses indentation**

- **Found during:** Task 2 (parse error on SC-2, SC-3, SC-4 tests)
- **Issue:** Plan template used `module A = ... end` syntax. LangThree uses indentation-based module blocks, no `end` keyword.
- **Fix:** Rewrote test strings to use `module A =\n    let x = 10\n\n` style (trailing blank line ends block).
- **Files modified:** tests/LangThree.Tests/ModuleTests.fs
- **Committed in:** `db3c3a4`

**4. [Rule 1 - Bug] `M1.Tok { ... }` is not valid record creation syntax**

- **Found during:** Task 2 (analyzing SC-4 test design)
- **Issue:** Plan specified `let t = M1.Tok { kind = "id"; value = 42 }` but LangThree creates records via `{ field = value }` literals — no type-name prefix. Parser would treat `M1.Tok` as a constructor call applied to the record literal.
- **Fix:** Used `open M1` to bring M1.Tok into scope, then `let t = { kind = "id"; value = 42 }` resolves to M1.Tok via exact field-set matching.
- **Files modified:** tests/LangThree.Tests/ModuleTests.fs
- **Committed in:** `db3c3a4`

---

**Total deviations:** 4 auto-fixed (4 bugs found during test execution)
**Impact on plan:** All fixes are test-layer issues. TypeCheck.fs required NO changes — MOD-05 scoping was already correct. No scope creep.

## Issues Encountered

- Pre-existing flaky test ("deep nested constructor match" in MatchCompileTests.fs due to `composeCounter` mutable shared across parallel tests) confirmed pre-existing from 31-01.

## Next Phase Readiness

- All Phase 31 success criteria (SC-1 through SC-4) are verified by passing integration tests
- File import pipeline (31-01) proven correct via real-file tests
- Module system complete: nested modules, open, qualified access, file imports, cycle detection
- Phase 31 can be marked complete

---
*Phase: 31-module-system*
*Completed: 2026-03-25*
