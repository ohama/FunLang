---
phase: 32-file-io-system-builtins
plan: 02
subsystem: stdlib
tags: [system-builtins, file-io, get-args, get-env, get-cwd, path-combine, dir-files, eprint, eprintln, TypeCheck, Eval, Program]

# Dependency graph
requires:
  - phase: 32-01
    provides: 8 file I/O builtins (STD-02 through STD-09); BuiltinValue/Scheme patterns established
  - phase: 29-char-comparison
    provides: LangThreeException error convention
provides:
  - get_args : unit -> string list (CLI args after script filename; [] in -e/REPL mode)
  - get_env : string -> string (raises LangThreeException if var not set)
  - get_cwd : unit -> string (current working directory)
  - path_combine : string -> string -> string (cross-platform path join)
  - dir_files : string -> string list (full file paths in directory)
  - eprint : string -> unit (write to stderr, no newline)
  - eprintln : string -> unit (write to stderr with newline)
  - scriptArgs mutable in Eval.fs (wired from Program.fs file-run branch)
affects:
  - v2.0 milestone complete (final plan of final phase)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "scriptArgs mutable declared before initialBuiltinEnv to avoid forward-reference issue"
    - "get_args reads mutable scriptArgs inside BuiltinValue closure (captured at call time)"
    - "path_combine is curried: outer BuiltinValue -> inner BuiltinValue"
    - "get_env null-checks return value and raises LangThreeException on missing var"
    - "eprint/eprintln flush stderr after write"

key-files:
  created: []
  modified:
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Program.fs

key-decisions:
  - "scriptArgs mutable placed before initialBuiltinEnv (forward-reference would fail if after)"
  - "get_env raises LangThreeException (not empty string) — consistent with read_file behavior for missing resources"
  - "Program.fs searches rawArgv for absFilename OR filename (handles both absolute and relative matches)"
  - "get_args returns [] in -e/REPL modes (scriptArgs defaults to []); no special casing needed"

patterns-established:
  - "Pattern: Mutable module-level state (scriptArgs) read inside BuiltinValue closures — closure captures binding, reads current value at call time"
  - "Pattern: Program.fs file-run wiring mirrors currentEvalFile pattern exactly"

# Metrics
duration: ~5min
completed: 2026-03-25
---

# Phase 32 Plan 02: System Builtins Summary

**6 system builtins (STD-10 through STD-15) registered in TypeCheck.fs and Eval.fs; scriptArgs wired from Program.fs; v2.0 milestone complete**

## Performance

- **Duration:** ~5 min
- **Completed:** 2026-03-25
- **Tasks:** 3 (2 with commits, 1 pure verification)
- **Files modified:** 3

## Accomplishments

- Added 6 Scheme entries to `initialTypeEnv` in TypeCheck.fs (STD-10 through STD-15)
- Added `mutable scriptArgs : string list = []` before `initialBuiltinEnv` in Eval.fs (forward-reference safe)
- Added 6 BuiltinValue entries to `initialBuiltinEnv` in Eval.fs using established patterns
- Wired `Eval.scriptArgs` population in Program.fs file-run branch (searches rawArgv for script filename)
- All 6 builtins smoke-tested end-to-end; full test suite (214 tests) passes with zero regressions

## Smoke Test Results

| Builtin | Input | Expected | Result |
|---------|-------|----------|--------|
| get_cwd () | unit | directory string | "/Users/ohama/vibe-coding/LangThree" |
| path_combine "/tmp" "test.txt" | two strings | "/tmp/test.txt" | "/tmp/test.txt" |
| dir_files "/tmp/lt32dir" | dir path | file list | ["/tmp/lt32dir/b.txt"; "/tmp/lt32dir/a.txt"] |
| get_env "HOME" | env var name | home path | "/Users/ohama" |
| get_env "NONEXISTENT_LT32_VAR" | missing var | LangThreeException caught | "caught" |
| eprint "to stderr" | string | stdout empty | stdout: '()' |
| eprintln "error line" | string | on stderr | "error line" found in stderr |
| get_args () | unit in -e mode | [] | [] |

## Task Commits

Each task was committed atomically:

1. **Task 1: Add 6 system builtins + scriptArgs mutable** - `6d88487` (feat)
2. **Task 2: Wire Eval.scriptArgs from Program.fs** - `93ee4fe` (feat)
3. **Task 3: Smoke-test all 6 system builtins** - (no new files; verification only)

## Files Created/Modified

- `src/LangThree/TypeCheck.fs` - 6 new Scheme entries in initialTypeEnv
- `src/LangThree/Eval.fs` - mutable scriptArgs + 6 new BuiltinValue entries in initialBuiltinEnv
- `src/LangThree/Program.fs` - scriptArgs population in file-run branch after Eval.currentEvalFile

## Decisions Made

- `scriptArgs` mutable declared before `initialBuiltinEnv` (not after) — F# forward-reference constraint: a `let` value's initialization body cannot reference a `let mutable` declared below it
- `get_env` raises `LangThreeException` on missing variable (not returning empty string) — consistent with `read_file`/`read_lines` convention for missing resources
- Program.fs searches `rawArgv` for both `absFilename` and `filename` — handles cases where the OS may present path differently in argv
- `get_args` returns `[]` in `-e` and REPL modes naturally (scriptArgs defaults to `[]`); no special casing required

## Deviations from Plan

None - plan executed exactly as written.

## Phase 32 Complete

All 14 builtins (STD-02 through STD-15) are registered and functional:
- **Plan 01 (STD-02 to STD-09):** read_file, stdin_read_all, stdin_read_line, write_file, append_file, file_exists, read_lines, write_lines
- **Plan 02 (STD-10 to STD-15):** get_args, get_env, get_cwd, path_combine, dir_files, eprint, eprintln

## v2.0 Milestone Complete

Phase 32 is the final phase of the v2.0 Practical Language Completion milestone. All phases complete:
- Phase 26: Prelude, failwith, option/result aliases
- Phase 27: Syntax improvements (bracket depth, trailing semicolons, list patterns)
- Phase 28: Pattern matching improvements
- Phase 29: Char type and comparison operators
- Phase 30: ELSE fix and expression let rec
- Phase 31: Module system (import/scoping)
- Phase 32: File I/O and system builtins

---
*Phase: 32-file-io-system-builtins*
*Completed: 2026-03-25*
