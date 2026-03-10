---
phase: 12-printf-output
plan: 01
subsystem: eval-builtins
tags: [print, println, printf, output, builtin, BuiltinValue, TypeCheck]

dependency-graph:
  requires:
    - 11-02  # BuiltinValue infrastructure, initialBuiltinEnv merge pattern
    - 10-01  # TupleValue [] as unit, TTuple [] as unit type
  provides:
    - print/println/printf as BuiltinValue entries in Eval.initialBuiltinEnv
    - print/println/printf type schemes in TypeCheck.initialTypeEnv
    - parsePrintfSpecifiers / printfFormatArg / substitutePrintfArgs / applyPrintfArgs helpers
  affects:
    - any future phase using stdout output
    - integration tests in phase 12 (plans 02+)

tech-stack:
  added: []
  patterns:
    - curried BuiltinValue chain for variadic printf
    - let rec applyPrintfArgs for recursive arity accumulation
    - stdout.Write / stdout.Flush for immediate output ordering

key-files:
  created: []
  modified:
    - src/LangThree/Eval.fs
    - src/LangThree/TypeCheck.fs

decisions:
  - id: printf-type-permissive
    summary: "printf typed as string -> 'a (permissive polymorphic) in TypeCheck"
    rationale: "Cannot statically know arity from format string at type-check time; mirrors to_string pattern from Phase 11; runtime enforces specifier types"
  - id: stdout-not-printfn
    summary: "Use stdout.Write/stdout.Flush rather than F# printfn"
    rationale: "F# printfn goes through printf formatting machinery and interferes with tests; stdout.Write gives raw output control"
  - id: substitutePrintfArgs-separate
    summary: "substitutePrintfArgs extracted as separate helper (not inlined in applyPrintfArgs)"
    rationale: "Cleaner separation: argument collection (applyPrintfArgs) vs string construction (substitutePrintfArgs)"

metrics:
  tasks-completed: 2
  tasks-total: 2
  duration: "2 min"
  completed: "2026-03-10"
---

# Phase 12 Plan 01: printf Output Summary

**One-liner:** print/println/printf as BuiltinValue entries with curried applyPrintfArgs chain for variadic format output.

## What Was Built

Added three output functions to `Eval.initialBuiltinEnv` and three type schemes to `TypeCheck.initialTypeEnv`:

- **print**: `string -> unit` — writes to stdout without newline, flushes immediately
- **println**: `string -> unit` — writes to stdout with newline, flushes immediately
- **printf**: `string -> 'a` — parses %d/%s/%b specifiers, returns curried BuiltinValue chain per specifier; substitutes and flushes when all specifiers satisfied

Four module-level helpers added before `initialBuiltinEnv` in Eval.fs:

1. `parsePrintfSpecifiers` — scans format string, returns specifier char list; skips `%%`
2. `printfFormatArg` — converts Value to string for a given specifier; never quotes strings (unlike `formatValue`)
3. `substitutePrintfArgs` — rebuilds format string with `%%` → `%` and specifier substitution
4. `applyPrintfArgs` (`let rec`) — accumulates args via curried BuiltinValue chain; fires substitution + flush when remaining specifiers exhausted

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add print and println | 052b3fa | Eval.fs, TypeCheck.fs |
| 2 | Add printf with helpers | 8ed0e2a | Eval.fs, TypeCheck.fs |

## Verification Results

All spot-checks pass:

| Command | Expected | Actual |
|---------|----------|--------|
| `print "hi"` | `hi()` | `hi()` |
| `println "hi"` | `hi\n()` | `hi\n()` |
| `printf "x=%d" 42` | `x=42()` | `x=42()` |
| `printf "%s=%b" "ok" true` | `ok=true()` | `ok=true()` |
| `printf "done"` | `done()` | `done()` |
| `printf "100%%"` | `100%()` | `100%()` |

Build: `0 Error(s). 0 Warning(s).`

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| printf type scheme | `Scheme([0], TArrow(TString, TVar 0))` | Cannot statically know arity; mirrors `to_string` pattern |
| Output mechanism | `stdout.Write` + `stdout.Flush()` | Avoids F# printfn interference; ensures ordering |
| Helper structure | 4 separate module-level `let` bindings before `initialBuiltinEnv` | Must be in scope before the `Map.ofList`; clean separation of parse/format/substitute/accumulate |

## Deviations from Plan

None — plan executed exactly as written.

## Next Phase Readiness

Phase 12 plan 02 (fslit tests for print/println/printf) can proceed immediately. All three functions are live in the evaluation environment and the type checker.
