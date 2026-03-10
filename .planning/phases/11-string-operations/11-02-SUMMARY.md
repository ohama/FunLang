---
phase: 11-string-operations
plan: "02"
subsystem: interpreter
tags: [fsharp, builtins, string-operations, fslit, testing, value-du]

# Dependency graph
requires:
  - phase: 11-string-operations
    plan: "01"
    provides: BuiltinValue infrastructure, initialBuiltinEnv, 6 string type schemes
provides:
  - All 6 string functions wired into Program.fs and Repl.fs startup environments
  - 7 fslit integration tests (6 expr + 1 emit/type-decl)
  - Value DU with [<CustomEquality; CustomComparison>] for test suite compatibility
affects:
  - All future built-in functions follow same merge pattern in Program.fs/Repl.fs
  - Future tests can use Expect.equal on Value without FS0001 errors

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Map.fold merge pattern — combine preludeEnv and initialBuiltinEnv at startup"
    - "and [<CustomEquality; CustomComparison>] Value — attribute on and-clause of mutually recursive DU"
    - "fslit %input format — file-mode tests for --emit-type with multi-binding input files"

key-files:
  created:
    - tests/flt/expr/str-length.flt
    - tests/flt/expr/str-concat.flt
    - tests/flt/expr/str-sub.flt
    - tests/flt/expr/str-contains.flt
    - tests/flt/expr/str-to-string.flt
    - tests/flt/expr/str-to-int.flt
    - tests/flt/emit/type-decl/type-decl-str-builtins.flt
  modified:
    - src/LangThree/Program.fs
    - src/LangThree/Repl.fs
    - src/LangThree/Ast.fs

key-decisions:
  - "Map.fold merge order: preludeEnv is the accumulator base, initialBuiltinEnv values are inserted — string functions override any hypothetical Prelude name conflicts"
  - "and [<CustomEquality; CustomComparison>] Value — attribute goes inline on the and-clause, not on a preceding line (F# syntax requirement)"
  - "BuiltinValue equality: two BuiltinValue instances are never equal (functions have no structural equality) — this is semantically correct"

patterns-established:
  - "Startup merge: Map.fold (fun acc k v -> Map.add k v acc) preludeEnv Eval.initialBuiltinEnv for combining environments"
  - "fslit single-quote quoting: --expr 'string_length \"hello\"' uses single quotes in Command line"

# Metrics
duration: 5min
completed: 2026-03-10
---

# Phase 11 Plan 02: String Operations Wiring + fslit Tests Summary

**initialBuiltinEnv wired into Program.fs and Repl.fs; 7 fslit integration tests covering all 6 string functions; Value DU CustomEquality fix for test suite; 196 F# + 193 fslit tests passing**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-10T07:38:37Z
- **Completed:** 2026-03-10T07:43:37Z
- **Tasks:** 2
- **Files modified:** 3, files created: 7

## Accomplishments
- Wired `Eval.initialBuiltinEnv` into `Program.fs` startup env (merged with `preludeEnv`)
- Same merge in `Repl.fs startRepl` function
- All 6 string functions verified working via `--expr`: `string_length`, `string_concat`, `string_sub`, `string_contains`, `to_string`, `string_to_int`
- Created 6 expr fslit tests (one per function) proving correct return values
- Created 1 emit/type-decl fslit test (`type-decl-str-builtins.flt`) proving all 6 functions have correct types
- Auto-fixed `Value` DU equality bug: added `[<CustomEquality; CustomComparison>]` to enable F# test equality

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire initialBuiltinEnv into Program.fs and Repl.fs** - `fc6412a` (feat)
2. **Task 2: Add 7 fslit tests and fix Value CustomEquality for test suite** - `9e22390` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/LangThree/Program.fs` - Replaced `Prelude.loadPrelude()` with `preludeEnv + Map.fold merge with initialBuiltinEnv`
- `src/LangThree/Repl.fs` - Same merge in `startRepl`
- `src/LangThree/Ast.fs` - Added `[<CustomEquality; CustomComparison>]` to `Value` DU with `Equals`, `GetHashCode`, `IEquatable`, `IComparable`, `valueEqual`, `valueCompare` members
- `tests/flt/expr/str-length.flt` - `string_length "hello"` → `5`
- `tests/flt/expr/str-concat.flt` - `string_concat "foo" "bar"` → `"foobar"`
- `tests/flt/expr/str-sub.flt` - `string_sub "hello" 1 3` → `"ell"`
- `tests/flt/expr/str-contains.flt` - `string_contains "hello" "ell"` → `true`
- `tests/flt/expr/str-to-string.flt` - `to_string 42` → `"42"`
- `tests/flt/expr/str-to-int.flt` - `string_to_int "42"` → `42`
- `tests/flt/emit/type-decl/type-decl-str-builtins.flt` - 6 bindings using all string functions, verifies types (a:int, b:string, c:string, d:bool, e:string, f:int)

## Decisions Made
- Used `Map.fold (fun acc k v -> Map.add k v acc) preludeEnv Eval.initialBuiltinEnv` pattern. This places string functions as the "later" values so they override any Prelude name collision. Consistent and readable.
- `[<CustomEquality; CustomComparison>]` attribute placed inline on the `and` clause (`and [<CustomEquality; CustomComparison>] Value =`). F# requires the attribute on the same line as `and` — preceding-line placement causes FS0010 parse error.
- `BuiltinValue` equality returns `false` for any two `BuiltinValue` instances. Semantically correct: two function values are not structurally equal even if they implement the same operation.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed Value DU missing CustomEquality causing FS0001 in test project**

- **Found during:** Task 2 (running `dotnet test`)
- **Issue:** `BuiltinValue of fn: (Value -> Value)` was added in plan 11-01. F# cannot auto-derive equality for DUs containing function types. The F# test project uses `Expect.equal result (Ast.IntValue 42)` which invokes generic `=` on `Value`, producing FS0001 errors for 20+ test cases in `MatchCompileTests.fs` and `ExceptionTests.fs`. This was an oversight in 11-01 — `valuesEqual` was added to `Eval.fs` but the test project's usage of `Expect.equal` was not updated.
- **Fix:** Added `[<CustomEquality; CustomComparison>]` to the `Value` DU in `Ast.fs`. Implemented `override Equals`, `override GetHashCode`, `interface IEquatable<Value>`, `interface IComparable`, and static helpers `valueEqual`/`valueCompare`. All existing structural comparisons are preserved; `BuiltinValue` instances always compare as unequal.
- **Files modified:** `src/LangThree/Ast.fs`
- **Commit:** `9e22390` (bundled with Task 2)

---

**Total deviations:** 1 auto-fixed (test-suite-blocking build error from 11-01 oversight)
**Impact on plan:** Fix was necessary to run `dotnet test`. No scope creep. 196 F# tests now pass with the new equality implementation.

## Issues Encountered
- `[<CustomEquality; CustomComparison>]` placed before `and Value =` (on its own line) causes FS0010 parse error. F# syntax requires the attribute inline: `and [<CustomEquality; CustomComparison>] Value =`.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All 6 string operations fully working end-to-end: type-checked, evaluated, integration-tested
- 196 F# + 193 fslit tests passing (7 new fslit tests added this plan)
- `Value` DU now has proper `CustomEquality` — future test code can safely use `Expect.equal` on `Value` values
- Ready for Phase 12 or any additional practical language features

---
*Phase: 11-string-operations*
*Completed: 2026-03-10*
