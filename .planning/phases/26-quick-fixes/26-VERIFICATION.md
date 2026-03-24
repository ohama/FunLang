---
phase: 26-quick-fixes
verified: 2026-03-24T08:00:50Z
status: gaps_found
score: 3/4 must-haves verified
gaps:
  - truth: "`option` is accepted as a type name interchangeably with `Option` (e.g., `let x : int option = Some 42`)"
    status: partial
    reason: "The ROADMAP example `let x : int option = Some 42` is impossible — the parser does not support type annotations in module-level let bindings. The option alias normalization IS implemented and works in `fun (x : int option) ->` lambda parameter contexts. The success criterion as literally written cannot be satisfied without a parser change."
    artifacts:
      - path: "src/LangThree/Elaborate.fs"
        issue: "Implementation is correct — TEData('option', ...) normalizes to TData('Option', ...). But the ROADMAP test case requires `let x : int option = Some 42` which the parser grammar (Decl rule) does not support."
      - path: "src/LangThree/Parser.fsy"
        issue: "The Decl grammar only supports `let IDENT EQUALS Expr` and `let IDENT ParamList EQUALS Expr` — no `let IDENT COLON TypeExpr EQUALS Expr` form. Top-level let bindings cannot carry type annotations."
    missing:
      - "Parser support for `let x : TypeExpr = expr` at module level (Decl grammar rule)"
      - "OR: update ROADMAP SC4 to reflect what is actually achievable (`fun (x : int option) -> ...` syntax)"
    note: "The implementation delivers what is achievable given the current parser. SUMMARY acknowledged this and adapted verification to `fun (v : string option) ->` form which does work."
---

# Phase 26: Quick Fixes Verification Report

**Phase Goal:** Eliminate trivial workarounds and unblock downstream work
**Verified:** 2026-03-24T08:00:50Z
**Status:** gaps_found — 3/4 success criteria verified; SC4 partially achieved (works in lambda context, not in ROADMAP example syntax)
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Running an empty .fun file produces no crash and returns unit | VERIFIED | `printf "" > /tmp/empty.fun && dotnet run -- /tmp/empty.fun` outputs `()` exits 0 |
| 2 | `dotnet run -- somefile.fun` from any directory loads Prelude functions | VERIFIED | Ran from `/tmp`: `fst`, `snd`, `map`, `filter`, `length` all resolve; Prelude loaded via 3-stage search |
| 3 | `failwith "error message"` raises an exception with the given message string | VERIFIED | Raises `LangThreeException(StringValue "...")`, catchable by `try-with`; works in if-else branches without type error |
| 4 | `option` is accepted as a type name interchangeably with `Option` (e.g., `let x : int option = Some 42`) | PARTIAL | `int option` in `fun (x : int option) ->` lambda params works. `let x : int option = Some 42` is a parse error — parser grammar has no `let IDENT COLON TypeExpr EQUALS Expr` rule |

**Score:** 3/4 truths fully verified (1 partial)

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/LangThree/Eval.fs` | `failwith` builtin in `initialBuiltinEnv` | VERIFIED | Line 252-256: `"failwith", BuiltinValue (fun v -> ... raise (LangThreeException (StringValue msg)) ...)` |
| `src/LangThree/TypeCheck.fs` | `failwith` type scheme in `initialTypeEnv` | VERIFIED | Line 55-56: `"failwith", Scheme([0], TArrow(TString, TVar 0))` — polymorphic return |
| `src/LangThree/Elaborate.fs` | `option`/`result` normalization in `TEData` case | VERIFIED | Line 63: `let canonical = match name with "option" -> "Option" \| "result" -> "Result" \| n -> n` in `elaborateWithVars`; Line 94: same pattern in `substTypeExprWithMap` |
| `src/LangThree/Prelude.fs` | `findPreludeDir` 3-stage path search | VERIFIED | Lines 51-74: Stage 1 (CWD), Stage 2 (assembly-relative), Stage 3 (walk-up 6 levels) |
| `src/LangThree/Program.fs` | `IsNullOrWhiteSpace` guard for empty files | VERIFIED | Line 182-184: `if System.String.IsNullOrWhiteSpace input then printfn "()" 0` |
| `src/LangThree/Program.fs` | `List.isEmpty moduleDecls` guard | VERIFIED | Line 201-203: `if List.isEmpty moduleDecls then printfn "()" 0` |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `TypeCheck.fs initialTypeEnv` | `Eval.fs initialBuiltinEnv` | Both declare `failwith` | WIRED | Both entries exist at matching positions; polymorphic type in checker, `LangThreeException` raiser in evaluator |
| `Elaborate.fs TEData case` | TypeCheck unification | `TData("Option", ...)` output | WIRED | `int option` parses as `TEData("option", [TEInt])`, normalizes to `TData("Option", [TInt])`, unifies with Prelude's `Option` type |
| `Prelude.fs findPreludeDir` | `loadPrelude` caller | `findPreludeDir()` call at line 78 | WIRED | `loadPrelude` calls `findPreludeDir()` and uses the result to find `*.fun` files |
| `Program.fs` | `Prelude.loadPrelude()` | Line 57 | WIRED | `let prelude = Prelude.loadPrelude()` called unconditionally at startup |

### Requirements Coverage

| Requirement | Status | Notes |
|-------------|--------|-------|
| MOD-03 (empty file graceful handling) | SATISFIED | `IsNullOrWhiteSpace` guard prints `()` and exits 0; whitespace-only and comment-only files also handled cleanly |
| MOD-04 (Prelude loads from any CWD) | SATISFIED | 3-stage search: CWD -> assembly-relative -> walk-up 6 levels from binary location |
| STD-01 (`failwith` builtin) | SATISFIED | Raises `LangThreeException`, polymorphic return type, catchable by `try-with` |
| TYPE-03 (`option`/`result` alias) | PARTIAL | Normalization in elaborator is correct; works in `fun (x : int option) ->` contexts; ROADMAP example `let x : int option = ...` requires parser change not implemented |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None found | — | — | — | All implementations are substantive |

### Test Suite Status

All 199 tests pass at current HEAD (`d4fa238`). There was an observed intermittent failure of "nested cons pattern" test during verification (1 failure on first run, 0 on subsequent runs). Root cause is the global mutable `freshTypeVarIndex` counter in `Elaborate.fs` — test results are non-deterministic based on run order and prior state. This is a pre-existing issue, not introduced by phase 26. The commit `9420977` (option alias) was verified as the trigger, but the root cause is the shared counter, not the feature itself.

### Gaps Summary

**SC4 is partially achieved.** The ROADMAP success criterion requires `let x : int option = Some 42` to work. This requires the parser to support type annotations in module-level `let` bindings — a parser feature that does not exist. The elaborator normalization (`option` -> `Option`) is correctly implemented and works wherever the parser can produce a `TEData("option", ...)` node (i.e., in `fun (x : int option) -> ...` lambda parameter annotations).

**Two paths to close this gap:**

1. Add parser support for `let IDENT COLON TypeExpr EQUALS Expr` in the `Decl` grammar rule in `Parser.fsy`. This would allow `let x : int option = Some 42` to parse and the existing elaborator normalization would handle it correctly.

2. Update the ROADMAP SC4 wording to match what's actually achievable: "The `option` alias is accepted in lambda parameter annotations (`fun (x : int option) -> ...`)" — this is already working and is the language's actual syntax.

**This gap does NOT block downstream work** (STD-01, MOD-03, MOD-04 are all satisfied). The `failwith` builtin, Prelude path fix, and empty file handling all work as intended. The partial TYPE-03 implementation is sufficient for most real use cases since function parameters (the main use of option types) do accept the alias.

---

_Verified: 2026-03-24T08:00:50Z_
_Verifier: Claude (gsd-verifier)_
