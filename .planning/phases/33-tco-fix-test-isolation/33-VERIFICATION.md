---
phase: 33-tco-fix-test-isolation
verified: 2026-03-25T02:30:00Z
status: passed
score: 5/5 must-haves verified
---

# Phase 33: TCO Fix + Test Isolation Verification Report

**Phase Goal:** The interpreter correctly tail-call-optimizes recursive functions and the test suite runs deterministically
**Verified:** 2026-03-25T02:30:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth                                                                                                      | Status     | Evidence                                                                                    |
| --- | ---------------------------------------------------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------------------------- |
| 1   | `let rec loop n = if n = 0 then 0 else loop (n-1)` with 1,000,000 iterations completes without overflow   | VERIFIED   | Smoke test ran `loop 1000000` via CLI, returned `0` with no exception                      |
| 2   | `let rec ... and ...` mutually recursive functions tail-call-optimize correctly (LetRecDecl path)          | VERIFIED   | `isEven 1000000` (mutual recursion, 1M alternating calls) returned `true` with no overflow |
| 3   | Local `let rec` inside expressions tail-call-optimizes correctly (LetRec path)                            | VERIFIED   | Nested `let rec loop` with `loop 1000000` inside expression returned `0` with no overflow  |
| 4   | All 214 F# unit tests pass on every run, including under parallel execution                                | VERIFIED   | `dotnet test` ran twice consecutively: both runs show `Passed: 214, Failed: 0`             |
| 5   | `typeCheckModule` calls do not interfere through shared global state in MatchCompile                       | VERIFIED   | No module-level mutable state in MatchCompile.fs; local counter in `compileMatch` only     |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact                               | Expected                                              | Status     | Details                                                                                   |
| -------------------------------------- | ----------------------------------------------------- | ---------- | ----------------------------------------------------------------------------------------- |
| `src/LangThree/Eval.fs` line 777       | `eval recEnv moduleEnv callEnv true funcBody`         | VERIFIED   | Line 777 reads exactly `eval recEnv moduleEnv callEnv true funcBody` — LetRec TCO enabled |
| `src/LangThree/Eval.fs` line 1039      | `eval recEnv modEnv callEnv true body`                | VERIFIED   | Line 1039 reads exactly `eval recEnv modEnv callEnv true body` — LetRecDecl TCO enabled  |
| `src/LangThree/MatchCompile.fs`        | Local counter in `compileMatch`, no module-level state | VERIFIED  | `let mutable nextVar = 0` at line 233, inside `compileMatch` scope only                  |
| `src/LangThree/MatchCompile.fs compile` | `compile (freshTestVar: unit -> TestVar) ...`         | VERIFIED   | Line 126: `let rec compile (freshTestVar: unit -> TestVar) (clauses: MatchRow list)`      |

### Key Link Verification

| From                             | To                      | Via                                          | Status   | Details                                                                                             |
| -------------------------------- | ----------------------- | -------------------------------------------- | -------- | --------------------------------------------------------------------------------------------------- |
| Eval.fs LetRec BuiltinValue      | App trampoline loop     | `tailPos=true` causes eval to return TailCall | WIRED    | Line 757-765: App case checks tailPos, returns TailCall or runs while-loop trampoline               |
| Eval.fs LetRecDecl BuiltinValue  | App trampoline loop     | same tailPos=true change in evalModuleDecls   | WIRED    | Line 1039 uses `true`; applyFunc at call sites runs trampoline                                      |
| MatchCompile.compileMatch        | MatchCompile.compile    | `freshTestVar` passed as parameter            | WIRED    | Line 247: `compile freshTestVar rows`; all recursive calls thread `freshTestVar` through            |

### Requirements Coverage

| Requirement | Status      | Notes                                                                           |
| ----------- | ----------- | ------------------------------------------------------------------------------- |
| FIX-01      | SATISFIED   | `loop 1000000` completes; Eval.fs line 777 uses `tailPos=true`                  |
| FIX-02      | SATISFIED   | `isEven 1000000` (mutual) completes; Eval.fs line 1039 uses `tailPos=true`      |
| FIX-03      | SATISFIED   | Local `let rec` with 1M iterations completes; same LetRec path as FIX-01       |
| ISO-01      | SATISFIED   | 214/214 tests pass on two consecutive runs                                      |
| ISO-02      | SATISFIED   | No module-level `nextTestVar`/`resetTestVarCounter` in MatchCompile.fs source   |

### Anti-Patterns Found

None. No TODO/FIXME/placeholder patterns found in modified files. No stub implementations detected.

### Human Verification Required

None required — all success criteria are programmatically verifiable and have been confirmed:
- TCO correctness verified by running 1M-iteration programs through the actual interpreter
- Test determinism verified by two consecutive full test runs
- Source structure verified by direct inspection of the modified files

### Summary

Phase 33 fully achieves its goal. Both sub-plans executed cleanly:

**Plan 01 (TCO fix):** Two one-character changes in `src/LangThree/Eval.fs` — `tailPos=false` changed to `true` at lines 777 (LetRec) and 1039 (LetRecDecl). This restored TCO for all three `let rec` forms by allowing the existing App trampoline to catch TailCall values produced by recursive bodies.

**Plan 02 (Test isolation):** Three module-level declarations (`nextTestVar`, `freshTestVar`, `resetTestVarCounter`) removed from `src/LangThree/MatchCompile.fs`. The counter is now a local `let mutable nextVar = 0` inside `compileMatch`, with `freshTestVar` threaded as an explicit parameter to the recursive `compile` function. This eliminates the race condition that caused TestVar ID collisions under parallel test execution.

All five success criteria are confirmed against the actual codebase.

---

_Verified: 2026-03-25T02:30:00Z_
_Verifier: Claude (gsd-verifier)_
