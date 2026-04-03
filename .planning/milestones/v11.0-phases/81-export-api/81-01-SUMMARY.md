---
phase: 81
plan: 01
subsystem: export-api
tags: [export-api, type-annotation, bidir, prelude, expecto]

dependency-graph:
  requires:
    - 79-01: TypeAnnotationMap infrastructure (ConcurrentDictionary<Span,Type> in Bidir.annotationMap)
    - 80-01: exportBindingEnv and BindingEnv type alias in TypeCheck.fs
  provides:
    - ExportApi.typeCheckFile: public entry point accepting a file path, returns TypedModule
    - TypedModule record: AnnotationMap + BindingEnv + BuiltinSchemes
  affects:
    - 82-01: typed AST consumer can call ExportApi.typeCheckFile directly

tech-stack:
  added: []
  patterns:
    - snapshot-after-typecheck: annotationMap converted to immutable Map<Span,Type> immediately after typeCheckModuleWithPrelude succeeds
    - position-tracked-parse: lexAndFilter with PositionedToken + filterPositioned for accurate span data

key-files:
  created:
    - src/FunLang/ExportApi.fs
    - tests/FunLang.Tests/ExportApiTests.fs
  modified:
    - src/FunLang/FunLang.fsproj
    - tests/FunLang.Tests/FunLang.Tests.fsproj

decisions:
  - id: d1
    summary: ExportApi uses private parseModuleFromString with PositionedToken (position-tracked) rather than the simpler Prelude.parseModuleFromString, to preserve accurate span data for the annotation map.

metrics:
  duration: 2m
  completed: 2026-04-03
---

# Phase 81 Plan 01: Export API Summary

**One-liner:** ExportApi.typeCheckFile loads prelude, type-checks a .fun file, and returns a TypedModule with immutable AnnotationMap snapshot, merged BindingEnv, and BuiltinSchemes.

## What Was Built

Added `src/FunLang/ExportApi.fs` exposing:

- `TypedModule` record with `AnnotationMap: Map<Ast.Span, Type.Type>`, `BindingEnv: TypeCheck.BindingEnv`, and `BuiltinSchemes: Type.TypeEnv`
- `typeCheckFile (filePath: string) : TypedModule` — the sole public API entry point

The function resolves the absolute path, reads the file, loads the prelude (with `None` for both path arguments, using the standard search strategy), sets `TypeCheck.currentTypeCheckingFile`, parses via the position-tracked `parseModuleFromString`, calls `typeCheckModuleWithPrelude`, then immediately snapshots `Bidir.annotationMap` into an immutable `Map` before returning.

Five Expecto tests were added in `tests/FunLang.Tests/ExportApiTests.fs`, all sequenced to avoid annotation map races. All 244 tests pass.

## Decisions Made

1. **Position-tracked parse path chosen over Prelude.parseModuleFromString** — `Prelude.parseModuleFromString` uses simple token lists without position restoration, which would produce incorrect spans and an empty/wrong annotation map. The ExportApi version mirrors `Program.parseModuleFromString` exactly.

## Deviations from Plan

None — plan executed exactly as written.

## Next Phase Readiness

Phase 82 (typed AST consumer / downstream tooling) can call `ExportApi.typeCheckFile` directly. The `TypedModule` record surface is stable.
