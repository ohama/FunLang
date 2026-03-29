---
phase: 62-remove-dot-dispatch
plan: 01
subsystem: testing
tags: [flt, string, array, stringbuilder, dot-notation, migration]

# Dependency graph
requires:
  - phase: 60-builtins-prelude-modules
    provides: String/Array/StringBuilder module functions (String.length, String.contains, etc.)
provides:
  - 10 flt test files migrated from dot-notation to module function API
  - All String property tests use String.length/contains/endsWith/startsWith/trim
  - All Array property tests use Array.length
  - All StringBuilder tests use StringBuilder.create/add/toString
affects: [future-dot-notation-removal, phase-62-plans]

# Tech tracking
tech-stack:
  added: []
  patterns: [module-function-over-dot-notation, StringBuilder.add-not-append]

key-files:
  created: []
  modified:
    - tests/flt/file/property/property-string-length.flt
    - tests/flt/file/property/property-string-contains.flt
    - tests/flt/file/property/property-array-length.flt
    - tests/flt/file/string/str-methods-trim.flt
    - tests/flt/file/string/str-methods-endswith-startswith.flt
    - tests/flt/file/string/stringbuilder-basic.flt
    - tests/flt/file/string/stringbuilder-chaining.flt
    - tests/flt/file/string/stringbuilder-prelude.flt
    - tests/flt/file/string/stringbuilder-append-char.flt
    - tests/flt/file/string/stringbuilder-with-methods.flt

key-decisions:
  - "StringBuilder chaining (r1.Append, r2.Append) converted to sequential StringBuilder.add sb calls on same instance — mutating API makes return value unnecessary"
  - "String dot methods on literals (\"hello.txt\".EndsWith) wrapped as String.endsWith \"hello.txt\""

patterns-established:
  - "Module function pattern: String.length s (not s.Length)"
  - "Module function pattern: Array.length arr (not arr.Length)"
  - "Module function pattern: StringBuilder.add sb x (not sb.Append x)"

# Metrics
duration: 2min
completed: 2026-03-29
---

# Phase 62 Plan 01: String/Array/StringBuilder flt Test Migration Summary

**10 flt test files rewritten from dot-notation to module function API (String, Array, StringBuilder) — all passing**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-29T10:54:40Z
- **Completed:** 2026-03-29T10:56:31Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments

- Migrated 5 String/Array property tests: s.Length -> String.length s, s.Contains -> String.contains, arr.Length -> Array.length arr, s.Trim() -> String.trim, s.EndsWith/StartsWith -> String.endsWith/startsWith
- Migrated 5 StringBuilder tests: StringBuilder() -> StringBuilder.create(), sb.Append -> StringBuilder.add, sb.ToString -> StringBuilder.toString
- Eliminated all dot-notation from the property/ and relevant string/ flt test subdirectories

## Task Commits

Each task was committed atomically:

1. **Task 1: Migrate String and Array property tests** - `28e5422` (feat)
2. **Task 2: Migrate StringBuilder tests** - `2358788` (feat)

**Plan metadata:** _(docs commit follows)_

## Files Created/Modified

- `tests/flt/file/property/property-string-length.flt` - s.Length -> String.length s
- `tests/flt/file/property/property-string-contains.flt` - s.Contains -> String.contains s arg
- `tests/flt/file/property/property-array-length.flt` - arr.Length -> Array.length arr
- `tests/flt/file/string/str-methods-trim.flt` - s.Trim() -> String.trim s
- `tests/flt/file/string/str-methods-endswith-startswith.flt` - dot EndsWith/StartsWith -> String.endsWith/startsWith
- `tests/flt/file/string/stringbuilder-basic.flt` - StringBuilder()/Append/ToString -> create/add/toString
- `tests/flt/file/string/stringbuilder-chaining.flt` - chained .Append -> sequential StringBuilder.add calls
- `tests/flt/file/string/stringbuilder-prelude.flt` - remaining .Append calls -> StringBuilder.add
- `tests/flt/file/string/stringbuilder-append-char.flt` - char .Append -> StringBuilder.add with char
- `tests/flt/file/string/stringbuilder-with-methods.flt` - combined string+builder dot-notation -> all module fns

## Decisions Made

- StringBuilder chaining originally used the return value of each `.Append` call (r1, r2, r3). Since `StringBuilder.add` mutates the same object and returns it, sequential calls on the same `sb` with ignored return (`let _ = StringBuilder.add sb x`) produce identical results.
- String method calls on literals (e.g., `"hello.txt".EndsWith(".txt")`) were rewritten as `String.endsWith "hello.txt" ".txt"` — module-function-first argument order.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All 10 target flt tests pass with module function API
- No dot-notation remains in the migrated files
- Ready for Phase 62 Plan 02 (remaining dot-notation migration in other test categories)

---
*Phase: 62-remove-dot-dispatch*
*Completed: 2026-03-29*
