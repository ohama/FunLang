# Project Research Summary

**Project:** FunLang — v15.0 Type Class Maturity
**Domain:** Type class system extension — constrained instances, superclass entailment, Num/Eq classes, derive improvements, error message quality
**Researched:** 2026-04-08
**Confidence:** HIGH — all findings derived from direct source inspection + cross-language survey

## Executive Summary

FunLang's type class system is more complete than it appears at first glance. The parsing, storage, and type-level resolution of constrained instances (`Show 'a => Show ('a list)`) and superclass constraints (`typeclass Eq 'a => Ord 'a`) already work. The `Eq` and `Show` classes with built-in instances exist in the Prelude. The critical gaps are all at the **runtime dispatch layer**: `Elaborate.fs` flattens all instance methods to top-level let-bindings with the literal method name, so multiple instances of the same class collide at the name level. Constrained dispatch is broken at evaluation time even though it passes type checking.

The recommended approach is to fix correctness bugs first (duplicate instance detection, constraint leaks from method bodies, recursive instance methods), then extend `resolveConstraint` in `Bidir.fs` with superclass entailment (step 3 of the Wadler-Blott algorithm), then fix runtime dispatch via instance name mangling in `Elaborate.fs`, and finally add `Num` and `Eq` classes to the Prelude as named-method classes without touching the hardcoded `Add`/`Equal` AST dispatch. Migrating `+`, `-`, `*`, `=` operators to type-class dispatch is explicitly deferred — it would risk 724 existing tests simultaneously.

The highest-risk change in the milestone is instance name mangling in `Elaborate.fs`, which must preserve backward compatibility for all 724 tests by only mangling constrained/polymorphic instances (non-empty `InstanceVars`), leaving monomorphic instances with their literal method names. The highest-risk anti-pattern to avoid is migrating the built-in `Add`/`Subtract`/`Multiply`/`Equal` AST dispatch to go through the type class system in this milestone.

## Key Findings

### Recommended Stack

No NuGet changes are required. The entire milestone is pure F# changes within the existing `FunLang.fsproj`. The relevant files are `Type.fs`, `TypeCheck.fs`, `Bidir.fs`, `Elaborate.fs`, `Eval.fs`, `Diagnostic.fs`, and `Prelude/Typeclass.fun`. The parser (`Parser.fsy`, `Lexer.fsl`) and `Ast.fs` require no changes — the grammar already supports all required syntax and `Ast.fs` already has all needed fields.

**Core technologies (unchanged):**
- F# / .NET 10 — implementation language, no version changes needed
- FsLexYacc 11.3.0 — parser generation, no grammar changes needed
- Existing `Bidir.fs` constraint machinery — `resolveConstraint`/`generalize`/`pendingConstraints` is the foundation for all new features

### Expected Features

**Must have (table stakes):**
- Constrained instance runtime dispatch fixed (name mangling in Elaborate.fs) — currently type-checks but evaluates wrong
- Superclass entailment: `Ord int` implies `Eq int` is satisfied at constraint resolution time
- `ClassInfo` gains `Superclasses: string list` — enables entailment lookup in `resolveConstraint`
- Superclass validation at `InstanceDecl` time — missing superclass instances caught at declaration, not call site
- `Num` type class in Prelude with named methods (`add`, `sub`, `mul`) for user-defined numeric types
- `Eq ('a list)` constrained instance in Prelude
- Better E0701 messages: normalized TVar display, element-type instance hints

**Should have (differentiators):**
- `derive Show` / `derive Eq` for parameterized ADTs (`type Tree 'a = ...`)
- Default method implementations in type class declarations
- Inline `deriving` syntax on `TypeDecl` (field already parsed, may already be wired)
- E0704 method type mismatch fires correctly (currently misfires as E0301)

**Defer out of scope:**
- Operator dispatch for `+`, `-`, `*` through Num — 724-test regression risk, string-concat ambiguity unresolved
- Operator dispatch for `=` through Eq — broad breaking change
- Ord type class operator wiring (`<`, `>`, `<=`, `>=`)
- Higher-kinded type classes (Functor, Foldable) — requires a kind system that does not exist
- Overlapping instances — breaks coherence guarantee
- Multi-superclass / diamond inheritance — restrict to single-inheritance chains in v15.0

### Architecture Approach

The architecture has three layers that must be extended in order. The **type-level layer** (`Bidir.fs resolveConstraint`) needs superclass entailment added as step 3 of Wadler-Blott — a pure additive extension. The **elaboration layer** (`Elaborate.fs`) needs instance name mangling: constrained instances generate mangled method names (`show__Show_list`), monomorphic instances keep literal names. The **runtime layer** (`Eval.fs`) dispatches to mangled names using the type annotation map to identify the concrete instance.

**Major components and their v15.0 role:**
1. `Type.fs` — add `Superclasses: string list` to `ClassInfo`; no changes to `Ast.fs` or `InstanceInfo`
2. `TypeCheck.fs` — populate `Superclasses` at class declaration; validate superclass instances at `InstanceDecl` registration
3. `Bidir.fs` — add superclass entailment step to `resolveConstraint`; fix `uniqueDeferred` deduplication to apply substitution before `distinctBy`
4. `Elaborate.fs` — generate mangled names for constrained instances; keep literal names for monomorphic ones
5. `Eval.fs` — dispatch to mangled names at constrained call sites
6. `Diagnostic.fs` — use `formatTypeNormalized` in E0701; add element-type instance hints
7. `Prelude/Typeclass.fun` — add `typeclass Num 'a`, `instance Num int`, `instance Eq 'a => Eq ('a list)`

**Build order (from Architecture research):**
1. Superclass validation at `InstanceDecl` time (TypeCheck.fs — minimal, safe)
2. Num/Ord in Prelude (additive, no dispatch changes)
3. Constrained instance method body checking fixes (Bidir.fs correctness)
4. Operator migration — explicitly deferred to later phase

### Critical Pitfalls

1. **TC-2: Silent duplicate acceptance for constrained instances** — The E0702 duplicate check uses structural `TVar` equality. Two declarations of `Show 'a => Show ('a list)` generate different fresh `TVar` IDs, so the duplicate passes silently, creating an incoherent instance environment. Fix: normalize type variables (alpha-equivalence) before the duplicate comparison, adapting the existing `formatTypeNormalized` function. Must fix before adding any constrained instances to Prelude.

2. **TC-3: 724-test regression from Num/Eq operator migration** — Changing `Add`/`Equal` dispatch in `Bidir.fs` to go through type class instances before Prelude instances are loaded causes every arithmetic and equality test to fail simultaneously. The `Num int` / `Eq int` instances must exist in the environment before any dispatch change. Do not attempt operator migration in v15.0 — the test regression surface is all 724 tests at once.

3. **TC-10: Pending constraint leak from instance method bodies** — After type-checking instance method bodies, `pendingConstraints` is not drained. Constraints from the method body attach to the next unrelated `generalize` call, producing spurious constraints on subsequent let-bindings (e.g., `let result = 42 + 1` may acquire `Num 'a` if it follows a Num instance). Fix: drain `pendingConstraints` after each method body check in TypeCheck.fs `InstanceDecl` processing.

4. **TC-7: Instance method bodies cannot see their own instance** — `currentInstEnv` is updated after method bodies are type-checked. A `show` body for `Show ('a list)` that calls `show` recursively on the tail fails with E0701 because the `Show ('a list)` instance is not yet in `currentInstEnv`. Fix: swap the add/check order — register a provisional instance before checking method bodies.

5. **TC-1: E0701 misdiagnosed when element-type instance is missing** — The depth guard returns `false` silently when a constrained instance exists but its element-type instance is missing. The error says "No instance of Show for `int list`" when the real problem is "No instance of Show for `MyRecord`". The failing subgoal must be captured and surfaced in the error.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Correctness Foundations
**Rationale:** Three bugs in the existing infrastructure must be fixed before any new features are added. These bugs corrupt the instance environment, leak constraints, and cause misdiagnosed errors that would confound all subsequent development.
**Delivers:** Reliable type class infrastructure with correct duplicate detection, no constraint leaks, working recursive instance methods, readable E0701 errors.
**Addresses:** TC-2 (duplicate detection), TC-10 (constraint leak), TC-7 (provisional instance for recursive methods), TC-8 (formatTypeNormalized in E0701), TC-AX-2 (raise depth guard to 50)
**Avoids:** Silent coherence violations that would corrupt all subsequent instance declarations

### Phase 2: Superclass Entailment
**Rationale:** Superclass entailment is the foundational dependency for Ord, Eq class interactions, and Prelude instances that need superclass satisfaction. Must be in place before any Prelude instances are added.
**Delivers:** `Ord int` implies `Eq int` is satisfied; superclass instances validated at declaration time; `ClassInfo` carries superclass list.
**Addresses:** Feature 2 (superclass auto-derivation), TC-4 (declaration-time superclass validation), TC-6 (single-inheritance restriction documentation)
**Avoids:** Confusing E0701 at call sites when the problem is a missing superclass instance at declaration

### Phase 3: Constrained Instance Runtime Dispatch
**Rationale:** The type-level foundation is now solid. The evaluation layer can be fixed. Name mangling is the highest-risk change but is isolated to Elaborate.fs and Eval.fs.
**Delivers:** `show [1; 2; 3]` calls the `Show ('a list)` implementation, not the last-registered `show`. Constrained polymorphic instances work end-to-end.
**Addresses:** Feature 1 (constrained instance runtime fix), the central gap in STACK.md
**Avoids:** Monomorphic instance regression — literal names preserved for `Show int`, `Show bool`, etc. (only instances with non-empty `InstanceVars` are mangled)

### Phase 4: Num Type Class (Named Methods Only)
**Rationale:** With instance infrastructure solid, adding `Num` as a named-method class to the Prelude is low-risk additive work. Gives users generic numeric programming without the operator migration risk.
**Delivers:** `typeclass Num 'a` with `add`, `sub`, `mul`, `div_`; `instance Num int`; `Eq ('a list)` constrained instance. Users can write `instance Num MyVec`.
**Addresses:** Feature 3 (Num class, named-method variant); Feature 4 partial (Eq list instance)
**Avoids:** Operator migration anti-pattern — `Add`/`Subtract`/`Multiply` remain as hardcoded AST nodes

### Phase 5: Derive for Parameterized ADTs
**Rationale:** Derive improvements require working constrained instance dispatch (Phase 3) and superclass entailment (Phase 2) before the generated constrained instances will evaluate correctly.
**Delivers:** `derive Show Tree` generates `Show 'a => Show (Tree 'a)` with correct recursive dispatch; `derive Eq` rejects function-typed fields gracefully.
**Addresses:** Feature 6 (parameterized derive), TC-5 (non-polymorphic instance for parameterized types), TC-9 (runtime crash on function equality), TC-13 (variable name collision)
**Avoids:** Silent wrong output from derive on parameterized types

### Phase 6: Error Message Polish
**Rationale:** Display-only changes with no type system behavior impact. Safe to do last; benefits from the infrastructure added in earlier phases.
**Delivers:** E0701 with element-type instance hints; E0704 fires correctly; constraint chain context.
**Addresses:** Feature 5 (better error messages), TC-11 (E0704 never fires), TC-1 (depth exhaustion error distinction)

### Phase Ordering Rationale

- Phase 1 must precede all others — bugs in the instance environment corrupt everything built on top
- Phase 2 (superclass) before Phase 3 (name mangling) because Prelude instances added in Phase 3 will have superclass constraints that require Phase 2 infrastructure
- Phase 3 before Phases 4 and 5 because both add constrained instances to the Prelude that must dispatch correctly at runtime
- Phase 5 (derive) after Phase 3 because derive generates constrained instances that must work at the runtime dispatch level
- Phase 6 last — error messages are independent, low-risk, and benefit from feature stability

### Research Flags

Phases likely needing design discussion before implementation:
- **Phase 3 (name mangling):** The exact mangling scheme must be finalized upfront (`show__Show_list` vs another convention). The mechanism for Eval.fs to select mangled names at call sites needs explicit design — the TypeAnnotationMap approach requires Elaborate.fs to be type-aware, which is a non-trivial architectural step.
- **Phase 5 (parameterized derive):** The pattern for collecting type parameters from nested `ArgType` values (e.g., `TData("Tree", [TVar N])` inside a constructor field) requires careful implementation to correctly identify which TVars need to appear in `InstanceVars` and `InstanceConstraints`.

Phases with standard patterns (lower planning overhead):
- **Phase 1:** Each fix is narrow and well-scoped; write a failing test, fix the bug, confirm the test passes
- **Phase 2:** Wadler-Blott step 3 is textbook; `ClassInfo` field addition is mechanical
- **Phase 4:** Pure Prelude addition; no existing code changes; verify no name collisions
- **Phase 6:** Display-only changes in Diagnostic.fs; wrap one unify call in TypeCheck.fs

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Direct source inspection; all file change sites identified with line numbers |
| Features | HIGH | Gap analysis from working flt tests + source code; constrained instance runtime gap confirmed by code path trace |
| Architecture | HIGH | Build order from direct codebase read; mangling approach is industry-standard (GHC-equivalent at naming level) |
| Pitfalls | HIGH | All pitfalls identified from Bidir.fs/TypeCheck.fs source with specific line references; reproduction cases given |

**Overall confidence:** HIGH

### Gaps to Address

- **Exact name mangling dispatch mechanism in Eval.fs:** STACK.md proposes annotation-map-based type-aware dispatch in Elaborate.fs, but the precise mechanism for rewriting call sites at elaboration time (before evaluation) needs a concrete design decision before Phase 3 implementation begins.
- **Inline deriving wiring status:** FEATURES.md notes `TypeDecl.deriving` is already parsed — whether it is wired to the same code generation as `DerivingDecl` is unverified. Low-effort investigation needed before Phase 5 planning.
- **ExportApi.fs ClassInfo export impact:** When `Superclasses` is added to `ClassInfo`, ExportApi.fs (from v11.0 milestone) may need a matching update if it exports ClassEnv. Verify during Phase 2.

## Sources

### Primary (HIGH confidence — direct source inspection, 2026-04-08)
- `src/FunLang/Bidir.fs` lines 17–19 (mutable currentInstEnv), 92–128 (resolveConstraint, generalize, uniqueDeferred deduplication)
- `src/FunLang/TypeCheck.fs` lines 1063–1231 (TypeClassDecl, InstanceDecl, DerivingDecl processing)
- `src/FunLang/Type.fs` — InstanceInfo, ClassInfo, Constraint structures
- `src/FunLang/Elaborate.fs` — elaborateTypeclasses flat name promotion
- `src/FunLang/Diagnostic.fs` lines 452–481 — E0701–E0704 formatting
- `src/FunLang/Prelude/Typeclass.fun` — existing Show/Eq class and instance declarations
- `tests/flt/file/typeclass/` — 29 type class flt tests (passing baseline confirmed)
- `FunLang ERRORS.md` — E0701–E0706 documentation, known Bug 10 (E0704 deferred)

### Secondary (MEDIUM confidence — established cross-language patterns)
- Wadler & Blott (1989): "How to make ad-hoc polymorphism less ad hoc" — steps 1-3 of constraint resolution algorithm
- GHC Users Guide: Instance declarations and resolution — superclass entailment reference pattern
- THIH (Jones 1999): Typing Haskell in Haskell — depth-first constraint resolution with occurs-check
- Rust Reference: Trait implementations — constrained impl resolution (confirms FunLang approach is sound)
- PureScript Type Classes — Eq/Ord operator dispatch via typeclass in ML-family language

### Tertiary (LOW confidence — informational only)
- OOPSLA 2023: "Getting into the Flow: Towards Better Type Error Messages" — constraint error explanation chains
- System FC paper — coherence and alpha-equivalence for duplicate instance detection

---
*Research completed: 2026-04-08*
*Ready for roadmap: yes*
