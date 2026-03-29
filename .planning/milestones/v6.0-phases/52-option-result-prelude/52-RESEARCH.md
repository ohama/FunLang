# Phase 52: Option/Result Prelude Utilities - Research

**Researched:** 2026-03-29
**Domain:** LangThree Prelude .fun files — additive Option/Result combinators
**Confidence:** HIGH

## Summary

Phase 52 adds missing utility functions to `Prelude/Option.fun` and `Prelude/Result.fun`. Both files already exist with a partial set of combinators. This phase is purely additive: new functions are written in LangThree's own `.fun` syntax and require zero changes to any F# interpreter files (Ast.fs, Eval.fs, TypeCheck.fs, Parser.fsy, Bidir.fs, Infer.fs, IndentFilter.fs).

The language already has everything needed to implement all required functions: ADT pattern matching on `Some`/`None`/`Ok`/`Error`, lambdas, higher-order functions, the `()` unit literal, and the module/open system. All new functions follow the existing curried, pipeline-friendly style (`f -> container -> result`). No new syntax or runtime primitives are needed.

The ROADMAP success criteria uses `optionDefaultValue` and `resultDefaultValue` as the names for the default-extraction functions, while the current Prelude uses `optionDefault` and `resultDefault`. The phase must add `optionDefaultValue` and `resultDefaultValue` as aliases (keeping the existing names to avoid breaking 3+ existing tests that use `optionDefault`/`resultDefault`).

**Primary recommendation:** Edit only `Prelude/Option.fun` and `Prelude/Result.fun`. Add six new functions total: `optionIter`, `optionFilter`, `optionDefaultValue` (alias), `resultIter`, `resultToOption`, `resultDefaultValue` (alias). Write flt tests for each. Build passes with `dotnet build`. All existing tests remain green.

## Standard Stack

This phase has no external library dependencies. The "stack" is the existing LangThree toolchain.

### Core

| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| LangThree .fun syntax | current | Implement new Prelude functions | The Prelude is written in the language itself |
| dotnet build | current | Compile the F# interpreter | Standard build command per CLAUDE.md |
| FsLit flt runner | current | Integration test runner | Standard test runner per CLAUDE.md |

### Supporting

| Tool | Version | Purpose | When to Use |
|------|---------|---------|-------------|
| `dotnet test` | current | F# unit tests | Run after build to catch interpreter-level regressions |
| `../fslit/dist/FsLit` | current | flt integration tests | Primary test vehicle for new Prelude functions |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Adding to existing Option.fun | Creating a new OptionUtils.fun | Load order would be correct (O < R), but splitting a module is confusing — keep all Option functions together |
| Aliases in .fun | Renaming existing functions | Renaming would break existing flt tests (prelude-option-default.flt, prelude-result-default.flt use the old names) |

**Installation:**

No installation required. All changes are `.fun` file edits.

## Architecture Patterns

### Prelude File Load Order

```
Prelude/*.fun (loaded alphabetically by Prelude.fs):
├── Array.fun      # A — array builtins
├── Core.fun       # C — id, const, flip, compose, ignore, operators
├── Hashtable.fun  # H — hashtable builtins
├── List.fun       # L — list combinators
├── Option.fun     # O — Option type + combinators  ← EDIT THIS
└── Result.fun     # R — Result type + combinators  ← EDIT THIS
```

`Result.fun` loads after `Option.fun`, so `resultToOption` can use `Some`/`None` constructors (opened via `open Option` at the bottom of Option.fun).

### Pattern 1: Additive-Only Prelude Extension

**What:** Add new `let` bindings inside the existing module block, then confirm `open Module` at the bottom still applies.
**When to use:** Any time new combinators are needed without breaking existing users.
**Example:**

```fsharp
// Source: Prelude/Option.fun (existing pattern)
module Option =
    type Option 'a = None | Some of 'a
    // ... existing functions ...
    let optionIter f = fun opt -> match opt with | Some x -> f x | None -> ()
    let optionFilter pred = fun opt -> match opt with | Some x -> if pred x then Some x else None | None -> None
    let optionDefaultValue def = fun opt -> match opt with | Some x -> x | None -> def
    let (<|>) a b = match a with | Some x -> Some x | None -> b

open Option
```

### Pattern 2: Result-to-Option Bridge

**What:** `resultToOption` converts a `Result 'a 'b` to `Option 'a`, discarding the error.
**When to use:** When downstream code uses Option combinators but input came from a Result-producing function.

```fsharp
// Source: Prelude/Result.fun (new function)
// Note: Some/None are available because Option.fun loaded first and open Option applied
let resultToOption r = match r with | Ok x -> Some x | Error _ -> None
```

### Pattern 3: Alias for Renamed Functions

**What:** Add a `let newName = oldName` alias to preserve both names.
**When to use:** When the ROADMAP specifies a new name but existing tests use the old name.

```fsharp
// Preserve backward compatibility: optionDefault still works, optionDefaultValue is new canonical name
let optionDefaultValue def = fun opt -> match opt with | Some x -> x | None -> def
// OR: let optionDefaultValue = optionDefault  (identity alias)
```

### Recommended Function Signatures

All new functions follow curried order (function first, container last) for `|>` pipeline compatibility:

| Function | Signature (informal) | Behavior |
|----------|---------------------|----------|
| `optionIter` | `('a -> unit) -> Option 'a -> unit` | Call `f x` if `Some x`; do nothing if `None` |
| `optionFilter` | `('a -> bool) -> Option 'a -> Option 'a` | Return `Some x` if pred holds; `None` otherwise |
| `optionDefaultValue` | `'a -> Option 'a -> 'a` | Alias for `optionDefault` |
| `resultIter` | `('a -> unit) -> Result 'a 'b -> unit` | Call `f x` if `Ok x`; do nothing if `Error _` |
| `resultToOption` | `Result 'a 'b -> Option 'a` | `Ok x -> Some x`; `Error _ -> None` |
| `resultDefaultValue` | `'a -> Result 'a 'b -> 'a` | Alias for `resultDefault` |

### Anti-Patterns to Avoid

- **Renaming existing functions:** `optionDefault` → `optionDefaultValue` as a rename would break `prelude-option-default.flt` and `prelude-result-default.flt`. Add aliases instead.
- **Adding `resultIter` without a test:** The `iter` side-effect pattern needs a test that captures the printed output via `println`.
- **Using `open Result` before `open Option`:** Result.fun already does `open Result` at the end — don't add an extra `open Option` inside Result.fun; the constructors are already in scope from the file-level open.
- **Putting Option-dependent functions in Option.fun before the Option type declaration:** The `type Option 'a` must appear before any function that uses `Some`/`None`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Side effects on Option | Custom pattern match in user code | `optionIter` | The whole point of this phase is to put it in Prelude |
| Result-to-Option conversion | Manual `match r with Ok x -> Some x | Error _ -> None` | `resultToOption` | Prelude function removes boilerplate |
| Conditional Option | `if pred x then Some x else None` pattern | `optionFilter` | Standard combinator |

**Key insight:** All functions in this phase are one-liners in LangThree's match syntax. The work is not algorithmic complexity — it is writing the correct implementations, naming them consistently, and providing comprehensive flt test coverage.

## Common Pitfalls

### Pitfall 1: Forgetting `open Option` Provides `Some`/`None` in Result.fun

**What goes wrong:** `resultToOption` references `Some` and `None` in `Result.fun`. If `open Option` is removed or forgotten, type-checking fails because `Some`/`None` aren't in scope.
**Why it happens:** `open Option` at the bottom of `Option.fun` applies globally after that file loads. The Prelude loader accumulates environments, so `Result.fun` has `Some`/`None` in scope.
**How to avoid:** Do not add any `open Option` inside `Result.fun` — it's not needed. The constructors are already available.
**Warning signs:** Type error "Unknown constructor Some" when loading `Result.fun`.

### Pitfall 2: Breaking Existing Tests by Renaming Instead of Aliasing

**What goes wrong:** Replacing `optionDefault` with `optionDefaultValue` (instead of adding a new alias) breaks `prelude-option-default.flt` and `prelude-result-default.flt`.
**Why it happens:** Three existing flt tests use `optionDefault`, `resultDefault` by name.
**How to avoid:** Add `optionDefaultValue` and `resultDefaultValue` as additional `let` bindings, keeping the originals.
**Warning signs:** `prelude-option-default.flt` or `prelude-result-default.flt` fails with "Unbound variable optionDefault".

### Pitfall 3: Wrong Curried Argument Order

**What goes wrong:** `optionIter opt f` instead of `optionIter f opt` breaks `|>` pipeline idiom.
**Why it happens:** Confusion about which argument is the "function" vs. the "data".
**How to avoid:** Follow the established pattern in the codebase: all HOF combinators put the function argument first. Check `optionMap f`, `optionBind f`, `resultMap f` — all have `f` as first argument.
**Warning signs:** Test `Some 42 |> optionIter println` fails or requires awkward wrapping.

### Pitfall 4: `optionIter` Returning Wrong Type

**What goes wrong:** `optionIter` body returns `Some ()` instead of `()`, making the type `Option unit` instead of `unit`.
**Why it happens:** Writing `match opt with | Some x -> Some (f x) | None -> None` (copying from `optionMap`).
**How to avoid:** The None branch should return `()`, not `None`: `match opt with | Some x -> f x | None -> ()`.
**Warning signs:** Type error "expected unit, got Option unit".

### Pitfall 5: Qualified Access Tests for Option.map Style

**What goes wrong:** Writing tests that use `Option.map` / `Option.iter` qualified access when the ROADMAP only requires the `optionMap` / `optionIter` flat-name style.
**Why it happens:** The requirements say "Option.map, Option.bind, Option.defaultValue functions in Prelude" — this could be misread as requiring qualified access `Option.map`.
**How to avoid:** Read the success criteria: "optionMap, optionBind, optionDefaultValue" — these are flat names. The `Option.X` notation in the requirements refers to which *module* the functions belong to, not qualified-access syntax. Existing prelude tests use flat names (`optionMap`, `isSome`). Follow that pattern.
**Warning signs:** Confusion between `Option.map` (qualified access) vs. `optionMap` (flat name after `open Option`).

## Code Examples

Verified patterns from existing Prelude files:

### optionIter — side effect on Some

```fsharp
// Source: Prelude/Option.fun (new function, follows Array.iter pattern)
let optionIter f = fun opt -> match opt with | Some x -> f x | None -> ()
```

Test pattern (from `tests/flt/file/array/array-hof-iter.flt`):
```
let _ = optionIter (fun x -> println (to_string x)) (Some 42)
// Output: 42
```

### optionFilter — conditional Some/None

```fsharp
// Source: Prelude/Option.fun (new function)
let optionFilter pred = fun opt -> match opt with | Some x -> if pred x then Some x else None | None -> None
```

### optionDefaultValue — alias for optionDefault

```fsharp
// Source: Prelude/Option.fun (new alias, preserving backward compat)
let optionDefaultValue def = fun opt -> match opt with | Some x -> x | None -> def
```

### resultIter — side effect on Ok

```fsharp
// Source: Prelude/Result.fun (new function)
let resultIter f = fun r -> match r with | Ok x -> f x | Error _ -> ()
```

### resultToOption — bridge Result to Option

```fsharp
// Source: Prelude/Result.fun (new function, uses Some/None from Option.fun)
let resultToOption r = match r with | Ok x -> Some x | Error _ -> None
```

### resultDefaultValue — alias for resultDefault

```fsharp
// Source: Prelude/Result.fun (new alias, preserving backward compat)
let resultDefaultValue def = fun r -> match r with | Ok x -> x | Error _ -> def
```

### flt Test Structure (from existing prelude tests)

```
// Test: Prelude optionIter applies function to Some
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let _ = optionIter (fun x -> println (to_string x)) (Some 42)
let _ = optionIter (fun x -> println (to_string x)) None
// --- Output:
42
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual `match opt with Some x -> f x | None -> ()` in user code | `optionIter f opt` | Phase 52 | Removes boilerplate |
| Manual `match r with Ok x -> Some x | Error _ -> None` | `resultToOption r` | Phase 52 | Removes boilerplate |
| `optionDefault` name | `optionDefaultValue` as canonical per ROADMAP | Phase 52 | Both names exist after phase |
| `resultDefault` name | `resultDefaultValue` as canonical per ROADMAP | Phase 52 | Both names exist after phase |

**No deprecations:** Existing names (`optionDefault`, `resultDefault`, `isSome`, `isNone`, `isOk`, `isError`) are kept as-is. No existing tests should break.

## Open Questions

1. **Does OPTRES-02 require `optionIsSome`/`optionIsNone` as new names alongside existing `isSome`/`isNone`?**
   - What we know: The ROADMAP success criteria says "optionIsSome, optionIsNone" in point 2. The current Prelude has `isSome` and `isNone` (without the `option` prefix).
   - What's unclear: Are the ROADMAP names the required new names, or just descriptive names for the existing functions?
   - Recommendation: Add `optionIsSome` and `optionIsNone` as aliases for `isSome` and `isNone` to satisfy the requirement literally. Keep `isSome`/`isNone` to avoid breaking existing tests.

2. **Does OPTRES-03 require `resultIsOk`/`resultIsError` aliases?**
   - What we know: The ROADMAP does not mention these in the success criteria. Existing names are `isOk`/`isError`.
   - What's unclear: Symmetry with Option suggests they might be wanted.
   - Recommendation: Do not add unless the requirements explicitly state them. Phase 52 requirements only mention `Result.map`, `Result.bind`, `Result.mapError`, `Result.defaultValue`, `Result.toOption`.

3. **Should tests use qualified access (`Option.iter`) or flat names (`optionIter`)?**
   - What we know: Existing prelude tests all use flat names after `open Option`/`open Result`. Two tests (`prelude-list-map-qualified.flt`, `prelude-list-length-qualified.flt`) cover qualified access for List. No qualified access tests exist for Option/Result.
   - Recommendation: Write primary tests with flat names. Optionally add one qualified access test per module to confirm `Option.optionIter` / `Result.resultToOption` qualified syntax works, following the List pattern.

## Sources

### Primary (HIGH confidence)

- Prelude/Option.fun — current implementation, read directly
- Prelude/Result.fun — current implementation, read directly
- .planning/ROADMAP.md — success criteria for phase 52
- .planning/research/ARCHITECTURE.md — architecture analysis, feature 3
- .planning/research/FEATURES.md — feature priority table, implementation notes
- tests/flt/file/prelude/ — all existing prelude flt tests read directly
- src/LangThree/Eval.fs — confirmed `TupleValue []` = `()` (unit)
- tests/flt/file/array/array-hof-iter.flt — `iter` test pattern for side effects

### Secondary (MEDIUM confidence)

- .planning/research/SUMMARY.md — priority summary for missing functions

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — entire phase is internal .fun file edits, no external dependencies
- Architecture: HIGH — load order confirmed from Prelude.fs source, alphabetical sort verified
- Pitfalls: HIGH — naming conflicts identified by diffing ROADMAP names vs. current code; unit-return pitfall verified from Eval.fs; qualified access ambiguity identified from existing test patterns
- Open questions: MEDIUM — question 1 (isSome alias) is a naming edge case; questions 2-3 are low-stakes

**Research date:** 2026-03-29
**Valid until:** 2026-04-28 (stable internal codebase, no external deps)
