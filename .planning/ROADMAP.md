# Roadmap: LangThree v10.0 Type Classes

## Overview

v10.0 adds Haskell-style type classes to LangThree's existing Hindley-Milner type system, enabling ad-hoc polymorphism via the dictionary passing strategy. The five phases follow a strict dependency chain: type infrastructure must exist before parsing, parsing before type checker wiring, type checker before dictionary construction, and dictionary construction before built-in instances can replace hardcoded dispatch.

## Milestones

- (collapsed) v1.0–v9.1 — Phases 1–69 — shipped 2026-03-31
- 🚧 **v10.0 Type Classes** — Phases 70–74 (in progress)

## Phases

<details>
<summary>✅ v1.0–v9.1 (Phases 1–69) — SHIPPED 2026-03-31</summary>

69 phases across v1.0 through v9.1. See .planning/milestones/ for archived roadmaps.

</details>

### 🚧 v10.0 Type Classes (In Progress)

**Milestone Goal:** Haskell-style type classes with dictionary passing — `typeclass`/`instance` declarations, constraint inference, elaboration, and built-in `Show`/`Eq` instances for primitive types.

---

### Phase 70: Core Type Infrastructure
**Goal:** The type system's internal representation supports constraints — `Scheme` carries a constraint list, and the class/instance environment types exist.
**Depends on:** Phase 69 (v9.1 complete)
**Requirements:** TC-01, TC-02
**Success Criteria** (what must be TRUE):
  1. `Scheme(vars, constraints, ty)` compiles and all ~70 existing `Scheme([], ty)` sites are updated to `Scheme([], [], ty)` with zero F# compiler warnings.
  2. `ClassEnv` and `InstanceEnv` types are defined in `Type.fs` and threaded through `TypeCheck.typeCheckModuleWithPrelude` alongside existing environments.
  3. `mkScheme`/`schemeType` compatibility helpers exist so downstream consumers can be updated incrementally.
  4. `formatSchemeNormalized` displays constraints (e.g., `Show 'a =>`) when present; existing output is unchanged when constraints list is empty.
**Plans:** 2 plans

Plans:
- [ ] 70-01-PLAN.md — Extend Scheme to 3-field, add Constraint/ClassEnv/InstanceEnv types, update all ~92 match sites
- [ ] 70-02-PLAN.md — Thread ClassEnv/InstanceEnv through typeCheckModuleWithPrelude and all callers

---

### Phase 71: Parsing and AST
**Goal:** `typeclass` and `instance` declarations are valid syntax that produce well-formed AST nodes; constraint type annotations are parseable.
**Depends on:** Phase 70
**Requirements:** TC-03, TC-04, TC-05
**Success Criteria** (what must be TRUE):
  1. `typeclass Show 'a = show : 'a -> string` parses to a `TypeClassDecl` AST node with correct method name and signature.
  2. `instance Show int = let show x = to_string x` parses to an `InstanceDecl` AST node with correct class name, type, and method body.
  3. `Show 'a =>` constraint annotation in a type expression (`let f : Show 'a => 'a -> string = ...`) parses to a `TEConstrained` `TypeExpr` variant.
  4. `--emit-ast` renders `TypeClassDecl` and `InstanceDecl` nodes without crashing; `TypeCheck.fs` and `Eval.fs` have `failwith` stubs that surface unhandled cases instead of silently swallowing them.
**Plans:** 2 plans

Plans:
- [ ] 71-01-PLAN.md — AST nodes, lexer tokens, parser grammar, downstream stubs and --emit-ast rendering
- [ ] 71-02-PLAN.md — flt integration tests validating typeclass/instance/constraint parsing via --emit-ast

---

### Phase 72: Type Checker and Constraint Inference
**Goal:** Programs using type class constraints type-check correctly — constraints are inferred, propagated through `synth`/`check`, generalized at `let` boundaries, and resolved against known instances.
**Depends on:** Phase 71
**Requirements:** TC-06, TC-07, TC-08
**Success Criteria** (what must be TRUE):
  1. A function `let show_twice x = show x ++ show x` infers type `Show 'a => 'a -> string` without a type annotation.
  2. Calling `show_twice 42` resolves the `Show int` constraint and type-checks successfully; calling `show_twice (fun x -> x)` produces a clear "no instance of Show for ('a -> 'b)" error.
  3. A duplicate instance declaration (e.g., two `instance Show int` blocks) produces an error at the second declaration.
  4. `let`-generalization preserves constraints: a polymorphic constrained binding used at two different types produces two separate constraint resolution calls.
**Plans:** 3 plans

Plans:
- [ ] 72-01-PLAN.md — Diagnostic error kinds, TEConstrained elaboration, Eval no-ops, TypeClassDecl/InstanceDecl processing in typeCheckDecls
- [ ] 72-02-PLAN.md — pendingConstraints accumulator, constraint-aware instantiate/generalize, substitution-aware resolution
- [ ] 72-03-PLAN.md — flt integration tests for constraint inference, resolution, and error cases

---

### Phase 73: Dictionary Construction and Elaboration
**Goal:** Type-checked programs execute correctly — instance declarations produce runtime-callable method bindings via a post-type-check AST elaboration pass that desugars InstanceDecl into ordinary LetDecl bindings.
**Depends on:** Phase 72
**Requirements:** TC-09, TC-10
**Success Criteria** (what must be TRUE):
  1. Declaring `instance Show int` and then calling `show 42` evaluates to `"42"` at runtime with no changes to `Eval.fs`.
  2. A higher-order function `let map_show lst = List.map show lst` applied to `[1; 2; 3]` evaluates to `["1"; "2"; "3"]` — the dictionary is correctly threaded through the higher-order call.
  3. The evaluator sees only ordinary `App`/`Var` AST nodes — no `MethodCall` node or runtime class-dispatch machinery exists.
**Plans:** 2 plans

Plans:
- [ ] 73-01-PLAN.md — AST elaboration pass (elaborateTypeclasses in Elaborate.fs) + Program.fs pipeline wiring
- [ ] 73-02-PLAN.md — Runtime flt integration tests for typeclass method dispatch

---

### Phase 74: Built-in Instances and Tests
**Goal:** `Show` and `Eq` work out-of-the-box for all primitive LangThree types, and the integration test suite validates the end-to-end type class pipeline.
**Depends on:** Phase 73
**Requirements:** TC-11, TC-12, TC-13
**Success Criteria** (what must be TRUE):
  1. `show 42`, `show true`, `show 'x'`, `show "hello"`, `show [1; 2; 3]`, and `show (Some 5)` all evaluate to their string representations without any explicit `instance Show` declaration in user code.
  2. `1 = 1` evaluates to `true` and `1 = 2` evaluates to `false` via the `Eq` type class; attempting `(fun x -> x) = (fun x -> x)` produces a type error rather than a runtime crash.
  3. At least 5 new `.flt` integration tests cover: typeclass declaration, instance declaration, constrained function, built-in `Show` instances, and built-in `Eq` instances — all passing with `FsLit tests/flt/`.
**Plans:** 2 plans

Plans:
- [ ] 74-01-PLAN.md — Create Prelude/Typeclass.fun with Show+Eq classes and instances; fix Prelude.fs elaboration
- [ ] 74-02-PLAN.md — 5+ flt integration tests for built-in Show/Eq instances and constrained functions

---

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 70. Core Type Infrastructure | v10.0 | 2/2 | ✓ Complete | 2026-03-31 |
| 71. Parsing and AST | v10.0 | 2/2 | ✓ Complete | 2026-03-31 |
| 72. Type Checker and Constraint Inference | v10.0 | 3/3 | ✓ Complete | 2026-03-31 |
| 73. Dictionary Construction and Elaboration | v10.0 | 2/2 | ✓ Complete | 2026-03-31 |
| 74. Built-in Instances and Tests | v10.0 | 0/2 | Not started | - |
