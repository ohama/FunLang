---
phase: 41-tests-and-documentation
plan: 02
subsystem: documentation
tags: [tutorial, korean, array, hashtable, mutable, mdbook]

# Dependency graph
requires:
  - phase: 40-array-higher-order
    provides: Array.iter/map/fold/init builtins + Prelude/Array.fun wrappers
  - phase: 39-hashtable-type
    provides: Hashtable.create/get/set/containsKey/keys/remove builtins
provides:
  - tutorial/19-mutable-data.md (Korean tutorial chapter for Array and Hashtable)
  - tutorial/SUMMARY.md link entry for 19-mutable-data.md
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created:
    - tutorial/19-mutable-data.md
  modified:
    - tutorial/SUMMARY.md

key-decisions:
  - "Chapter does not include a combined Array+Hashtable practical example due to a limitation: qualified module names (e.g., Hashtable.set) inside inline lambdas passed to Array.iter cause a parse/eval error; workaround is to bind the module function to a local variable first"
  - "Array equality note: = always returns false for arrays (reference equality, never equal by value); noted in 주의사항"
  - "fold callback documented with explicit curried form: fun acc -> fun x -> ... (fun acc x -> is a parse error)"

patterns-established: []

# Metrics
duration: 5min
completed: 2026-03-25
---

# Phase 41 Plan 02: Mutable Data Structures Tutorial Summary

**Korean tutorial chapter covering Array (create/get/set/length/ofList/toList/iter/map/fold/init) and Hashtable (create/set/get/containsKey/keys/remove) with 9 verified runnable examples; added to SUMMARY.md under 실용 프로그래밍**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-03-25T06:44:09Z
- **Completed:** 2026-03-25T06:48:40Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Wrote tutorial/19-mutable-data.md in Korean, following the style of 18-file-io.md (plain title, `$ cat`/`$ langthree` format, prose explanations)
- Verified all 9 code examples against the Release binary before finalizing
- Documented key LangThree-specific gotchas: curried fold callbacks, `let _ =` for unit, `[|...|]` print format, array reference equality, no `open Array/Hashtable`
- Discovered and documented the inline lambda + qualified module name limitation
- Updated SUMMARY.md to link 19-mutable-data.md under 실용 프로그래밍, after 파일 I/O

## Task Commits

Each task was committed atomically:

1. **Task 1: Write tutorial/19-mutable-data.md** - `469d12b` (docs)
2. **Task 2: Add chapter to tutorial/SUMMARY.md** - `e1bc149` (docs)

## Files Created/Modified
- `tutorial/19-mutable-data.md` - Korean tutorial chapter for mutable data structures (Array + Hashtable), 233 lines, 9 runnable examples
- `tutorial/SUMMARY.md` - Added `- [가변 데이터 구조](19-mutable-data.md)` under 실용 프로그래밍 section

## Decisions Made
- **Inline lambda + module qualification limitation:** When an inline lambda is passed to `Array.iter` and its body calls `Hashtable.set` (or similar qualified module function), the interpreter raises "Field access on non-record value: Hashtable". This is a known evaluator limitation. The chapter documents this as a 주의사항 and the workaround (bind module function to local variable first). No fix applied — out of scope.
- **No combined Array+Hashtable example:** Given the limitation above, a combined practical example (e.g., frequency counter using Array.iter + Hashtable) would require workarounds that obscure the teaching point. Omitted in favor of clean individual examples.
- **Array equality note documented:** `a = a` returns `false` for arrays (pure reference equality, arrays never equal by `=`). Documented in 주의사항.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Discovered that the Release binary was stale (predate Phase 40 builtins); rebuilt before running any examples. `dotnet build` succeeded with 0 warnings, 0 errors.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- v3.0 milestone documentation is complete (TST-23 done)
- Phase 41 Plan 02 is the final plan in the 41-phase series
- Tutorial book is complete and up to date
- v3.0 milestone (Mutable Data Structures) is fully delivered

---
*Phase: 41-tests-and-documentation*
*Completed: 2026-03-25*
