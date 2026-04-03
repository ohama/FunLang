# Roadmap: FunLang

## Milestones

- ✅ **v1.0–v10.1** - Phases 1-78 (shipped 2026-04-01)
- 🚧 **v11.0 Typed AST Export** - Phases 79-82 (in progress)

## Phases

<details>
<summary>✅ v1.0–v10.1 (Phases 1-78) - SHIPPED 2026-04-01</summary>

Phases 1-78 delivered the complete FunLang interpreter: indentation-based syntax, ADT/GADT/Records, module system, pattern matching, TCO, mutable data structures, mutable variables, imperative ergonomics, project build system, AST span fixes, and type classes with error reporting.

See milestone archive for details.

</details>

### 🚧 v11.0 Typed AST Export (In Progress)

**Milestone Goal:** Export HM type inference results — per-expression annotation map and top-level binding environment — so FunLangCompiler can replace ~250 lines of heuristic type-guessing code with accurate type information.

#### Phase 79: Type Annotation Infrastructure

**Goal**: Per-expression type annotation map exists and is populated during type checking
**Depends on**: Phase 78 (v10.1 complete)
**Requirements**: TA-01, TA-02
**Success Criteria** (what must be TRUE):
  1. `TypeAnnotationMap` module compiles with `Dictionary<Span, Type>` type and record/access helpers
  2. After `Bidir.synth` runs on any expression, its inferred type (with substitution applied) is recorded in the map
  3. All ~40 `Expr` node variants produce entries — no node is silently skipped
  4. Existing tests pass unchanged (annotation recording is purely additive)
**Plans:** 2 plans

Plans:
- [x] 79-01-PLAN.md — Define TypeAnnotationMap module, declare mutable ref, wire resets
- [x] 79-02-PLAN.md — Wire annotation recording into every Bidir.synth arm + tests

#### Phase 80: Type Environment Export

**Goal**: Top-level binding types (user-defined and builtin/prelude) are accessible as a named collection
**Depends on**: Phase 79
**Requirements**: TE-01, TE-02
**Success Criteria** (what must be TRUE):
  1. Top-level `let` bindings expose a name → `TypeScheme` map after type-checking a file
  2. Builtin and Prelude binding schemes are included in the same map (no gaps for standard library names)
  3. The exported map is queryable by binding name from outside the type-checker module
**Plans:** 1 plan

Plans:
- [x] 80-01-PLAN.md — Add BindingEnv alias + exportBindingEnv helper, TypeEnvTests for TE-01/TE-02

#### Phase 81: Export API

**Goal**: External callers can type-check a FunLang file and receive a single `TypedModule` record containing all type information
**Depends on**: Phase 80
**Requirements**: API-01, API-02
**Success Criteria** (what must be TRUE):
  1. `ExportApi.typeCheckFile` accepts a file path and returns a `TypedModule` record without error
  2. `TypedModule` contains the annotation map, binding environment, and builtin schemes in one value
  3. The API compiles and is accessible from outside `FunLang.fsproj` (e.g., a test or consumer project)
**Plans:** 1 plan

Plans:
- [ ] 81-01-PLAN.md — Implement ExportApi.fs with TypedModule record, typeCheckFile entry point, and Expecto tests

#### Phase 82: CLI Integration

**Goal**: Users can invoke `--emit-typed-ast` on any FunLang file and receive JSON type information on stdout
**Depends on**: Phase 81
**Requirements**: CLI-01
**Success Criteria** (what must be TRUE):
  1. `langthree --emit-typed-ast file.fun` exits 0 and prints valid JSON
  2. The JSON includes at least the per-expression span→type entries and top-level binding types
  3. `langthree --emit-typed-ast` on a file with a type error exits non-zero with an error message (not malformed JSON)
**Plans**: TBD

Plans:
- [ ] 82-01: Add --emit-typed-ast flag and JSON serialization (CLI-01)

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 79. Type Annotation Infrastructure | v11.0 | 2/2 | ✓ Complete | 2026-04-03 |
| 80. Type Environment Export | v11.0 | 1/1 | ✓ Complete | 2026-04-03 |
| 81. Export API | v11.0 | 0/1 | Not started | - |
| 82. CLI Integration | v11.0 | 0/1 | Not started | - |
