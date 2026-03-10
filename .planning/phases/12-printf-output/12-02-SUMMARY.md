---
phase: 12-printf-output
plan: 02
subsystem: testing
tags: [print, println, printf, fslit, integration-tests, file-mode, bug-fix]

dependency-graph:
  requires:
    - 12-01  # print/println/printf BuiltinValue implementations in Eval.fs + TypeCheck.fs
  provides:
    - 8 fslit integration tests covering print/println/printf end-to-end
    - bug fix: file-mode last-value printing no longer re-evaluates last LetDecl body
  affects:
    - any future file-mode tests with side-effectful final declarations

tech-stack:
  added: []
  patterns:
    - fslit %s-in-command avoidance: use %input file-mode for tests whose --expr arg contains %s specifier
    - file-mode result lookup: finalEnv[lastName] instead of re-eval(lastBody) for side-effect safety

key-files:
  created:
    - tests/flt/expr/print-basic.flt
    - tests/flt/expr/print-println.flt
    - tests/flt/expr/printf-int.flt
    - tests/flt/expr/printf-str.flt
    - tests/flt/expr/printf-bool.flt
    - tests/flt/expr/printf-multi.flt
    - tests/flt/expr/printf-no-spec.flt
    - tests/flt/file/print-sequence.flt
  modified:
    - src/LangThree/Program.fs

key-decisions:
  - "fslit substitutes %s in Command lines (like %input); tests whose --expr contains %s must use %input file-mode instead"
  - "Program.fs file-mode result: look up lastName in finalEnv rather than re-evaluating lastBody to avoid double side effects"

patterns-established:
  - "fslit Command line avoidance: any test needing %s specifier in --expr arg must use %input file-mode"

metrics:
  tasks-completed: 2
  tasks-total: 2
  duration: 11min
  completed: "2026-03-10"
---

# Phase 12 Plan 02: printf Integration Tests Summary

**8 fslit tests proving print/println/printf end-to-end, plus file-mode double-evaluation bug fix in Program.fs.**

## Performance

- **Duration:** 11 min
- **Started:** 2026-03-10T08:50:28Z
- **Completed:** 2026-03-10T09:01:36Z
- **Tasks:** 2
- **Files modified:** 9 (8 new .flt files + Program.fs fix)

## Accomplishments

- 7 `--expr` mode fslit tests covering `print`, `println`, and all `printf` specifier variants
- 1 file-mode sequencing test proving `let _ = print "a"` + `let _ = print "b"` outputs `ab` in order
- Fixed file-mode double-evaluation bug: Program.fs was calling `eval recEnv moduleEnv finalEnv lastBody` to get the result value, which re-fired all side effects for the last declaration; now looks up `finalEnv[lastName]` directly

## Task Commits

1. **Task 1: Write 7 --expr fslit tests for print, println, printf** - `ff87136` (feat + fix)
2. **Task 2: Write file-mode print sequencing test** - `daed6fe` (feat)

## Files Created/Modified

- `tests/flt/expr/print-basic.flt` — `print "hello"` outputs `hello()` (side-effect + unit result, no newline)
- `tests/flt/expr/print-println.flt` — `println "hello"` outputs `hello` then `()` on separate lines
- `tests/flt/expr/printf-int.flt` — `printf "%d" 42` outputs `42()`
- `tests/flt/expr/printf-str.flt` — `printf "%s" "hi"` via `%input` file-mode (avoids fslit `%s` substitution)
- `tests/flt/expr/printf-bool.flt` — `printf "%b" true` outputs `true()`
- `tests/flt/expr/printf-multi.flt` — `printf "%d and %s" 42 "hi"` via `%input` file-mode
- `tests/flt/expr/printf-no-spec.flt` — `printf "done"` outputs `done()`
- `tests/flt/file/print-sequence.flt` — two `let _ = print` declarations followed by `let result = 42`; output is `ab42`
- `src/LangThree/Program.fs` — file-mode result printing: `eval ... lastBody` → `Map.tryFind lastName finalEnv`

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| `%s` test avoidance | Use `%input` file-mode for `printf-str.flt` and `printf-multi.flt` | fslit substitutes `%s` in Command lines with the test file path (same mechanism as `%input`); cannot use `--expr 'printf "%s" "hi"'` directly |
| Program.fs result lookup | `Map.tryFind lastName finalEnv` | Re-evaluating `lastBody` fires side effects twice; env lookup is correct and safe |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed file-mode double side-effect evaluation in Program.fs**

- **Found during:** Task 1 (printf-str.flt and printf-multi.flt testing)
- **Issue:** `let _ = printf "%s" "hi"` in file mode output `hihi()` — `hi` appeared twice because Program.fs called `eval recEnv moduleEnv finalEnv lastBody` to get the display value, re-executing the print side effect
- **Fix:** Changed to `Map.tryFind lastName finalEnv` — the value was already computed by `evalModuleDecls`; look it up directly
- **Files modified:** `src/LangThree/Program.fs` (lines 202-206)
- **Verification:** `let _ = printf "%s" "hi"` in file mode now outputs `hi()` (one occurrence); `let-sequence.flt` still outputs `30`; all 196 F# unit tests pass; all 201 fslit tests pass
- **Committed in:** `ff87136` (part of Task 1 commit)

**2. [Rule 1 - Bug] fslit %s Command substitution — test design adjustment**

- **Found during:** Task 1 (printf-str.flt discovery)
- **Issue:** fslit treats `%s` in Command lines as a substitution token (replacing it with the test file path), parallel to `%input`; `'printf "%s" "hi"'` in a Command line caused fslit to pass the .flt file path instead of `"hi"` as the argument
- **Fix:** Rewrote `printf-str.flt` and `printf-multi.flt` to use `%input` file-mode instead of `--expr` mode
- **Files modified:** `tests/flt/expr/printf-str.flt`, `tests/flt/expr/printf-multi.flt`
- **Verification:** Both tests now pass with `fslit`
- **Committed in:** `ff87136` (part of Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 — bugs)
**Impact on plan:** Both fixes necessary for correctness. The Program.fs fix improves semantic correctness for all future file-mode programs with side effects. The fslit workaround is a test design pattern for `%s`-containing expressions.

## Issues Encountered

- fslit `%s` substitution is undocumented; discovered by running tests and observing that the .flt file path appeared as stdout

## Next Phase Readiness

Phase 12 is now complete. print/println/printf are fully implemented (plan 01) and fully tested (plan 02). All 201 fslit tests + 196 F# unit tests pass.

The Program.fs file-mode double-evaluation bug is fixed — future phases using side-effectful file-mode programs will behave correctly.

---
*Phase: 12-printf-output*
*Completed: 2026-03-10*
