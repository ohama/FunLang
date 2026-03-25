---
phase: 32-file-io-system-builtins
plan: 01
subsystem: stdlib
tags: [file-io, builtins, system, TypeCheck, Eval, BuiltinValue, Scheme]

# Dependency graph
requires:
  - phase: 29-char-comparison
    provides: char_to_int/int_to_char BuiltinValue pattern; LangThreeException error convention
  - phase: 26-prelude-failwith
    provides: Scheme registration pattern in initialTypeEnv; failwith polymorphic Scheme example
provides:
  - read_file : string -> string (raises LangThreeException on missing file)
  - stdin_read_all : unit -> string (reads all stdin)
  - stdin_read_line : unit -> string (reads one line, empty at EOF)
  - write_file : string -> string -> unit (creates/overwrites file)
  - append_file : string -> string -> unit (appends to file)
  - file_exists : string -> bool (bool check without exception)
  - read_lines : string -> string list (raises LangThreeException on missing file)
  - write_lines : string -> string list -> unit (writes string list to file)
affects:
  - 32-02 (remaining 6 builtins: get_args, get_env, get_cwd, path_combine, dir_files, eprint/eprintln)
  - Any future phase using file I/O in LangThree programs

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Unit-arg builtins (unit -> 'a) match TupleValue [] at runtime, not a unit AST value"
    - "File errors use raise (LangThreeException (StringValue msg)) for user try-with catchability"
    - "Two-arg builtins use nested BuiltinValue (curried), matching string_concat pattern"
    - "stdin_read_line handles null EOF from Console.In.ReadLine() by returning empty string"

key-files:
  created: []
  modified:
    - src/LangThree/TypeCheck.fs
    - src/LangThree/Eval.fs

key-decisions:
  - "read_file and read_lines use LangThreeException (not failwith) so user try-with can catch file errors"
  - "Unit-arg builtins explicitly match TupleValue [] — the runtime form of () — not wildcard"
  - "try-with in LangThree uses match-clause syntax: try expr with | e -> handler"

patterns-established:
  - "Pattern: Single-arg builtins — BuiltinValue (fun v -> match v with | StringValue s -> ... | _ -> failwith)"
  - "Pattern: Unit-arg builtins — match TupleValue [] explicitly, failwith on mismatch"
  - "Pattern: Two-arg builtins — outer BuiltinValue returns inner BuiltinValue (curried)"
  - "Pattern: User-visible errors — raise (LangThreeException (StringValue msg))"

# Metrics
duration: ~10min
completed: 2026-03-25
---

# Phase 32 Plan 01: File I/O Builtins Summary

**8 file I/O builtins (STD-02 through STD-09) registered in TypeCheck.fs and Eval.fs using BuiltinValue/Scheme pattern with LangThreeException for catchable file errors**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-25T00:00:00Z
- **Completed:** 2026-03-25T00:10:00Z
- **Tasks:** 3 (2 with commits, 1 pure verification)
- **Files modified:** 2

## Accomplishments

- Added 8 Scheme entries to `initialTypeEnv` in TypeCheck.fs (STD-02 through STD-09)
- Added 8 BuiltinValue entries to `initialBuiltinEnv` in Eval.fs using correct patterns for single-arg, unit-arg, and two-arg curried builtins
- All 8 builtins smoke-tested end-to-end; error paths confirmed catchable via try-with; full test suite (214 tests) passes

## Task Commits

Each task was committed atomically:

1. **Task 1: Add 8 file I/O Scheme entries to TypeCheck.fs** - `a3c1d18` (feat)
2. **Task 2: Add 8 file I/O BuiltinValue entries to Eval.fs** - `dcae575` (feat)
3. **Task 3: Smoke-test all 8 file I/O builtins** - (no new files; verification only)

## Files Created/Modified

- `src/LangThree/TypeCheck.fs` - 8 new Scheme entries in initialTypeEnv (lines after int_to_char)
- `src/LangThree/Eval.fs` - 8 new BuiltinValue entries in initialBuiltinEnv (lines after int_to_char)

## Decisions Made

- `read_file` and `read_lines` use `raise (LangThreeException ...)` on file-not-found so user code can `try ... with | e -> default` — this matches the convention from `failwith` in Phase 26
- `stdin_read_line` returns `StringValue ""` at EOF (null from `Console.In.ReadLine()`), matching Unix `read` behavior
- `try-with` syntax in LangThree is `try expr with | pattern -> expr` (match-clause form, not function form) — this affected smoke-test 5 (used wrong syntax initially, corrected before reporting)

## Deviations from Plan

None - plan executed exactly as written. The try-with syntax discovery during Task 3 smoke-testing was a test-harness adjustment (used wrong invocation form in plan's example), not a codebase change.

## Issues Encountered

- Task 3 smoke-test 5 (`try (read_file "missing") with (fun e -> "caught")`) failed with parse error because `with (fun e -> ...)` is not valid LangThree try-with syntax. Corrected to `try expr with | e -> handler` (match-clause form). No code changes needed.
- One flaky test failure ("nested cons pattern") appeared in a full test run due to parallel test execution race condition involving shared mutable state. Test passes in isolation and in subsequent full-suite runs. Pre-existing issue unrelated to this plan.

## Next Phase Readiness

- File I/O builtins complete; ready for plan 32-02 which adds the remaining 6 system builtins (get_args, get_env, get_cwd, path_combine, dir_files, eprint/eprintln)
- No blockers or concerns

---
*Phase: 32-file-io-system-builtins*
*Completed: 2026-03-25*
