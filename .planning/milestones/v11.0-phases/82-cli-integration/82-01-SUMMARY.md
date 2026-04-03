---
phase: 82-cli-integration
plan: 01
subsystem: cli
tags: [fsharp, cli, json, typed-ast, export-api, flt]

# Dependency graph
requires:
  - phase: 81-export-api
    provides: ExportApi.typeCheckFile returning TypedModule with AnnotationMap, BindingEnv, BuiltinSchemes
provides:
  - --emit-typed-ast CLI flag that type-checks a .fun file and prints TypedModule as JSON to stdout
  - JSON format: {annotations: [{span, type}], bindings: {name: scheme}} filtered to user file only
  - flt integration tests for success and type-error paths
affects: [future IDE/tooling integrations that consume typed AST JSON]

# Tech tracking
tech-stack:
  added: [System.Text.Json.Nodes (JsonObject, JsonArray, JsonValue)]
  patterns: [serializeTypedModule helper filters spans by FileName=absUserFile and bindings by excluding BuiltinSchemes+prelude]

key-files:
  created:
    - tests/flt/emit/typed-ast/basic.flt
    - tests/flt/emit/typed-ast/type-error.flt
  modified:
    - src/FunLang/Cli.fs
    - src/FunLang/Program.fs

key-decisions:
  - "Load prelude TypeEnv inside serializeTypedModule to exclude prelude bindings from JSON output (plan only said exclude BuiltinSchemes, but that left all prelude bindings included)"
  - "Use StderrContains in type-error.flt because temp file path in stderr makes exact Stderr match impossible"

patterns-established:
  - "serializeTypedModule: filter annotations by FileName equality, filter bindings by !containsKey in both BuiltinSchemes and prelude.TypeEnv"

# Metrics
duration: 4min
completed: 2026-04-03
---

# Phase 82 Plan 01: CLI Integration Summary

**--emit-typed-ast flag added to fn CLI: type-checks a .fun file via ExportApi.typeCheckFile and prints JSON {annotations, bindings} filtered to user-defined content only**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-04-03T03:38:43Z
- **Completed:** 2026-04-03T03:42:47Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Added `Emit_Typed_Ast` case to `CliArgs` DU with usage description
- Added `serializeTypedModule` helper in Program.fs using `System.Text.Json.Nodes` to produce JSON with span-annotated expressions and user-only bindings
- Handler branch exits 0 with JSON on success, exits 1 with error on stderr for type errors or missing files
- Two flt integration tests pass (711/712 total, 1 pre-existing failure unrelated)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Emit_Typed_Ast flag and JSON handler to CLI** - `bbc945f` (feat)
2. **Task 2: Add flt integration tests** - `61c15bf` (feat)

**Plan metadata:** (pending)

## Files Created/Modified
- `src/FunLang/Cli.fs` - Added `Emit_Typed_Ast` DU case and usage string
- `src/FunLang/Program.fs` - Added `serializeTypedModule` helper and `--emit-typed-ast` handler branch
- `tests/flt/emit/typed-ast/basic.flt` - Verifies JSON output with annotations and bindings for `let x = 42`
- `tests/flt/emit/typed-ast/type-error.flt` - Verifies exit 1 and E0301 on stderr for type error

## Decisions Made
- **Prelude exclusion fix:** The plan said to exclude `Map.containsKey kv.Key tm.BuiltinSchemes` from bindings, but `BuiltinSchemes = initialTypeEnv` only covers language builtins (print, (+), etc.), not prelude functions (map, filter, fold, etc.). Added a second prelude load to get prelude TypeEnv for exclusion, so only user-defined bindings appear in JSON.
- **StderrContains for type-error test:** Stderr contains the absolute temp file path, making an exact `Stderr:` match impossible with `%input`. Used `StderrContains:` to match just the error code and message.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Extended bindings filter to also exclude prelude functions**
- **Found during:** Task 1 (testing with temp file)
- **Issue:** Plan specified `exclude Map.containsKey kv.Key tm.BuiltinSchemes` but this only excludes initial builtins, leaving all prelude functions (map, filter, fold, show, etc.) in the output
- **Fix:** Added `Prelude.loadPrelude None None` call inside `serializeTypedModule` and extended filter to exclude both BuiltinSchemes and prelude TypeEnv keys
- **Files modified:** src/FunLang/Program.fs
- **Verification:** `let x = 42` output shows only `{"x":"int"}` in bindings, not prelude functions
- **Committed in:** bbc945f (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (missing critical)
**Impact on plan:** Fix essential for correct output - without it the JSON would contain hundreds of prelude bindings alongside user bindings.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 82 plan 01 complete; milestone v11.0 Typed AST Export is fully implemented
- CLI integration is the last phase (82 of 82)
- All must_haves satisfied: JSON output, annotations with spans, bindings without prelude/builtins, non-zero exit on type error, integration tests passing

---
*Phase: 82-cli-integration*
*Completed: 2026-04-03*
