---
phase: 60-builtins-prelude-modules
plan: 02
subsystem: prelude
tags: [modules, string, hashtable, stringbuilder, flt, type-check, eval]

# Dependency graph
requires:
  - phase: 60-01
    provides: "5 new builtins in Eval.fs + TypeCheck.fs (string_endswith, string_startswith, string_trim, hashtable_trygetvalue, hashtable_count)"
provides:
  - "String.fun: endsWith, startsWith, trim, length, contains module functions"
  - "Hashtable.fun: tryGetValue, count module functions"
  - "StringBuilder.fun: add (renamed from append)"
  - "4 flt integration tests for all new module functions"
  - "Bug fix: module export builder now correctly exports shadow bindings"
affects: [phase 61, phase 62, any code using String.length, String.contains, Hashtable.tryGetValue]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Module export builder: include shadow bindings with different type (TypeCheck.fs)"
    - "Module value env: use reference equality to detect new closures (Eval.fs)"

key-files:
  created:
    - "tests/flt/file/string/string-module-endswith-startswith-trim.flt"
    - "tests/flt/file/string/string-module-length-contains.flt"
    - "tests/flt/file/hashtable/hashtable-module-trygetvalue-count.flt"
    - "tests/flt/file/string/stringbuilder-module-add.flt"
  modified:
    - "Prelude/String.fun"
    - "Prelude/Hashtable.fun"
    - "Prelude/StringBuilder.fun"
    - "src/LangThree/TypeCheck.fs"
    - "src/LangThree/Eval.fs"

key-decisions:
  - "StringBuilder.add replaces StringBuilder.append to avoid List.append scope conflict"
  - "TypeCheck.fs module export builder: include shadow binding when type differs from outer env"
  - "Eval.fs module value env: include binding when not reference-equal to parent binding"

patterns-established:
  - "Module functions can shadow globally open'd names if their type differs — fixed in TypeCheck + Eval"

# Metrics
duration: 11min
completed: 2026-03-29
---

# Phase 60 Plan 02: Prelude Module Wrappers Summary

**String/Hashtable/StringBuilder .fun modules updated with new builtin wrappers; module export bug fixed allowing String.length and String.contains to coexist with List.length**

## Performance

- **Duration:** 11 min
- **Started:** 2026-03-29T08:36:57Z
- **Completed:** 2026-03-29T08:47:57Z
- **Tasks:** 2
- **Files modified:** 7 (3 .fun, 2 .fs, 4 .flt created)

## Accomplishments
- String.fun expanded from 1 to 6 functions (added endsWith, startsWith, trim, length, contains)
- Hashtable.fun expanded from 6 to 8 functions (added tryGetValue, count)
- StringBuilder.fun renamed append to add (avoids List.append conflict)
- 4 flt integration tests created and passing
- Fixed TypeCheck.fs and Eval.fs module export builder to correctly export shadow bindings

## Task Commits

Each task was committed atomically:

1. **Task 1: Update 3 Prelude .fun module files** - `7b131b6` (feat)
2. **Deviation fix: module export builder** - `7ef6628` (fix)
3. **Task 2: Create flt integration tests** - `e2411c8` (test)

## Files Created/Modified
- `Prelude/String.fun` - Added endsWith, startsWith, trim, length, contains
- `Prelude/Hashtable.fun` - Added tryGetValue, count
- `Prelude/StringBuilder.fun` - Renamed append to add
- `src/LangThree/TypeCheck.fs` - Module export builder includes shadow bindings with different type
- `src/LangThree/Eval.fs` - Module value env uses reference equality to detect new closures
- `tests/flt/file/string/string-module-endswith-startswith-trim.flt` - Tests BLT-01/02/03, MOD-01
- `tests/flt/file/string/string-module-length-contains.flt` - Tests String.length, String.contains
- `tests/flt/file/hashtable/hashtable-module-trygetvalue-count.flt` - Tests BLT-04/05, MOD-02
- `tests/flt/file/string/stringbuilder-module-add.flt` - Tests MOD-03

## Decisions Made
- StringBuilder.add (not append) — avoids conflict with `open List` which brings `List.append` into scope
- TypeCheck.fs fix: `outerV <> v` comparison uses F# structural equality on Scheme DU (int list * Type) which works correctly
- Eval.fs fix: `obj.ReferenceEquals(v, parentV)` is `false` for new closures, correctly detects module-defined overrides
- Pre-existing 5 test failures (old binary path `/Users/ohama/vibe/LangThree/...`) confirmed not regressions from this plan

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Module export builder excluded shadow bindings**
- **Found during:** Task 2 (flt test for String.length)
- **Issue:** TypeCheck.fs `moduleTypeEnv` builder filtered out bindings already in outer env (`if Map.containsKey k env then acc`). This prevented `String.length` (string->int) from being exported because `length` (list->int) was already in scope from `open List`.
- **Fix:** Changed check to include binding if type differs from outer env (`outerV <> v`). Applied same fix to Eval.fs using reference equality.
- **Files modified:** src/LangThree/TypeCheck.fs, src/LangThree/Eval.fs
- **Verification:** `String.length "abc"` returns 3; `List.length [1;2;3]` still returns 3; full flt suite 632/637 (5 pre-existing failures unchanged)
- **Committed in:** 7ef6628 (separate fix commit between tasks)

---

**Total deviations:** 1 auto-fixed (Rule 1 bug)
**Impact on plan:** Bug fix was necessary for correctness of String.length and String.contains. No scope creep.

## Issues Encountered
- `String.length "abc"` returned type error "expected 'f list but got string" — traced to module export builder filtering. Fixed in TypeCheck.fs + Eval.fs.

## Next Phase Readiness
- Phase 60 complete: all 5 builtins implemented (60-01), all 3 Prelude modules updated (60-02)
- Module export shadow-binding fix benefits any future module function that shares a name with an open'd module
- Ready for Phase 61

---
*Phase: 60-builtins-prelude-modules*
*Completed: 2026-03-29*
