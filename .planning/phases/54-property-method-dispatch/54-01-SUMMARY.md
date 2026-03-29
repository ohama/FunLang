---
phase: 54
plan: 01
subsystem: interpreter
tags: [property-dispatch, field-access, string, array, builtins, type-checker]

dependency-graph:
  requires: []
  provides:
    - ".Length property on TString and TArray values"
    - ".Contains method on TString values"
    - "FieldAccess dispatch infrastructure for value types"
  affects:
    - "Phase 55: string methods (.Append, .Contains, etc.)"
    - "Phase 56: array methods"
    - "Phase 57: hashtable methods"

tech-stack:
  added: []
  patterns:
    - "Value-type dispatch before RecordValue in FieldAccess arm (Eval.fs)"
    - "TString/TArray cases before TData in FieldAccess arm (Bidir.fs)"
    - "BuiltinValue returned from FieldAccess for methods taking arguments"

key-files:
  created:
    - tests/flt/file/property/property-string-length.flt
    - tests/flt/file/property/property-array-length.flt
    - tests/flt/file/property/property-string-contains.flt
  modified:
    - src/LangThree/Bidir.fs
    - src/LangThree/Eval.fs

decisions:
  - "Contains returns BuiltinValue (curried function) so App(FieldAccess(...), arg) dispatch works automatically"
  - "Value-type dispatch placed BEFORE RecordValue match in both Eval.fs | _ -> branch"
  - "TString/TArray cases placed BEFORE TData case in Bidir.fs FieldAccess arm"
  - "Raise FieldAccessOnNonRecord for unknown fields on known types (good error messages)"

metrics:
  duration: "~6 minutes"
  completed: "2026-03-29"
---

# Phase 54 Plan 01: Property & Method Dispatch Summary

**One-liner:** FieldAccess dispatch extended for .Length on strings/arrays and .Contains on strings via BuiltinValue currying

## What Was Built

Extended the `FieldAccess` arm in `Bidir.fs` (type checker) and `Eval.fs` (evaluator) to handle property access and method dispatch on primitive value types.

**Bidir.fs changes (lines 514-531):**
- Added `TString` case returning `(s1, TInt)` for `Length` and `(s1, TArrow(TString, TBool))` for `Contains`
- Added `TArray _` case returning `(s1, TInt)` for `Length`
- Both cases raise `FieldAccessOnNonRecord` for unknown field names
- Inserted BEFORE the existing `TData` case

**Eval.fs changes (FieldAccess arm, `| _ ->` branch):**
- Added `StringValue s` match: `Length` returns `IntValue s.Length`; `Contains` returns `BuiltinValue` closure capturing `s`
- Added `ArrayValue arr` match: `Length` returns `IntValue arr.Length`
- Both cases raise `failwithf` for unknown properties
- Inserted BEFORE the existing `RecordValue` match

**flt tests created:**
- `property-string-length.flt`: .Length on string variable, string literal, and empty string
- `property-array-length.flt`: .Length on arrays of different sizes
- `property-string-contains.flt`: .Contains on string variables and literals

## Verification

- `dotnet build src/LangThree/LangThree.fsproj -c Release`: Build succeeded (0 warnings, 0 errors)
- `/Users/ohama/vibe-coding/fslit/dist/FsLit tests/flt/file/property/`: 3/3 passed
- `/Users/ohama/vibe-coding/fslit/dist/FsLit tests/flt/`: 594/594 passed (no regressions)

## Key Design Decisions

**Why BuiltinValue for Contains:** The parser already handles `obj.Method(arg)` as `App(FieldAccess(obj, "Method", span), arg, span)`. When `FieldAccess` returns a `BuiltinValue`, `App` automatically applies it via `applyFunc` — no new AST nodes or special-casing needed.

**Why TArrow(TString, TBool) in Bidir.fs:** `.Contains` takes a string argument and returns bool. The type checker must reflect this so the type of the `App` expression resolves correctly.

**Why value-type dispatch before RecordValue:** The evaluator evaluates `expr` once and dispatches on the result value type. String/array values must be caught before the `RecordValue` match or they fall through to the error case.

## Deviations from Plan

None - plan executed exactly as written.

## Commits

- `9cbb67a`: feat(54-01): add value-type property/method dispatch for strings and arrays

## Next Phase Readiness

Phase 55 (string methods) can extend the `TString` and `StringValue` match arms in the same files using the identical pattern established here. The `BuiltinValue` currying approach for `.Contains` is the template for all future methods.
