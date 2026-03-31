# Project Research Summary

**Project:** LangThree v10.0 — Haskell-style Type Classes
**Domain:** ML-style interpreter — adding ad-hoc polymorphism to an existing HM type system
**Researched:** 2026-03-31
**Confidence:** HIGH

## Executive Summary

LangThree v10.0 adds Haskell-style type classes to an existing ML interpreter that already has full Hindley-Milner inference, bidirectional type checking, GADTs, a module system, and a tree-walking evaluator. The canonical implementation strategy is **dictionary passing**: each type class constraint `C 'a` is elaborated into an explicit dictionary argument (a record of method implementations) threaded through the program. This is the same approach GHC uses internally, and it is the correct fit for a tree-walking interpreter because dictionaries are just ordinary `RecordValue`s — no new evaluator machinery is required. The total implementation footprint is approximately 400–450 lines across 10 files with no new NuGet packages.

The recommended approach is to work in five sequential phases corresponding to the natural dependency chain: (1) extend the type infrastructure (`Scheme`, `ClassEnv`, `InstanceEnv`), (2) add parser/AST support, (3) wire constraint inference and resolution through the type checker, (4) construct dictionary values and elaborate call sites, and (5) replace hardcoded builtins with built-in `Show`/`Eq`/`Ord` instances. The payoff features are straightforward once the core machinery is in place. Features requiring Higher-Kinded Types (`Functor`, `Monad`), automatic `deriving`, multi-parameter type classes, and superclass hierarchies are explicitly out of scope for v10.0.

The primary risk is in the bidirectional type checker (`Bidir.fs`): constraint variables must be generalized alongside type variables, constraint resolution must be deferred until after unification completes, and elaboration (dictionary injection) must happen inline in `synth` rather than at runtime to correctly handle higher-order functions. A secondary risk is the mechanical breadth of the `Scheme` shape change — approximately 70 pattern-match sites in `TypeCheck.fs` require a trivial `Scheme([], ty)` to `Scheme([], [], ty)` update that is complete but tedious. The `mutableVars` pattern already present in `Bidir.fs` provides the correct model for threading `ClassEnv`/`InstanceEnv` without exploding call-site counts.

---

## Key Findings

### Recommended Stack

No new dependencies. The entire implementation lives within the existing F#/.NET 10 codebase using the existing FsLexYacc toolchain. Type classes are a language feature, not a library. Three new keywords (`typeclass`, `instance`, `where`) and one new token (`=>` as `FATARROW`) are added to the lexer. All runtime dictionary values reuse the existing `RecordValue` DU variant.

**Core technologies:**
- **F# / .NET 10** — implementation language; no version change needed
- **FsLexYacc 12.x** — LALR(1) lexer/parser; new keyword tokens and grammar rules added to existing files
- **Existing `RecordValue`** — reused as the runtime dictionary representation; zero new `Value` variants required
- **`Bidir.fs` synth/check** — primary integration point; constraint resolution and elaboration happen here

### Expected Features

**Must have (table stakes for v10.0):**
- `typeclass` and `instance` declaration syntax — defines the surface language
- Constraint representation in `Scheme` (`forall 'a. Show 'a => 'a -> string`) — foundational type system change
- Dictionary passing at call sites — elaboration rewrites `show x` to `show_dict.show x` before eval
- Constraint inference and propagation through `Bidir.fs` — constraints collected and generalized alongside types
- Built-in `Show`, `Eq`, `Ord` instances for all primitive types
- Constrained instance declarations (`instance Show (Option 'a) where Show 'a`) — required for generic containers
- Type error when a constraint is unsatisfied — clear diagnostic with constraint name and source span

**Should have (differentiators, deferrable within v10.0):**
- `to_string` becomes an alias for `Show.show` — completes the migration from hardcoded dispatch
- `=` / `<>` become `Eq`-constrained; `<` / `>` / `<=` / `>=` become `Ord`-constrained — replaces structural builtins
- Named constraint annotations in user-facing type signatures (`Show 'a => 'a`)
- Duplicate instance detection (error on re-declaration of the same class/type pair)

**Defer to v10.1+:**
- `derive Show` / `derive Eq` automatic instance generation — reduces boilerplate but non-trivial
- Default method implementations in class declarations
- `Num` typeclass replacing arithmetic operators — high blast radius, no immediate demand
- `Hashable` typeclass
- `Functor` / `Foldable` — requires Higher-Kinded Types, separate milestone

### Architecture Approach

The pipeline structure is unchanged. Type classes slot in as a new environment pair (`ClassEnv`, `InstanceEnv`) threaded through `TypeCheck.typeCheckModuleWithPrelude` alongside the existing `TypeEnv`, `ConstructorEnv`, and `RecordEnv`. Constraint solving is interleaved with HM inference: constraints are collected during `synth`, deferred past unification, resolved at `let` generalization boundaries, and elaborated inline by rewriting `Var` lookups for constrained methods into `App(Var dictName, ...)` nodes before the evaluator ever sees them. The evaluator remains type-class-unaware.

**Major components:**
1. **Type.fs** — Extended `Scheme of vars * constraints * ty`; new `Constraint`, `ClassInfo`, `InstanceInfo`, `ClassEnv`, `InstanceEnv` types
2. **Lexer.fsl / Parser.fsy / Ast.fs** — New keywords and grammar rules; `TypeClassDecl` and `InstanceDecl` AST nodes; `TEConstrained` type expression variant
3. **Infer.fs** — Extended `instantiate`/`generalize` to thread constraints alongside type variables in a single substitution pass
4. **Bidir.fs** — Constraint resolution at `Var` instantiation; inline elaboration injecting dictionary arguments; `ClassEnv`/`InstanceEnv` threaded as module-level mutable refs (same pattern as `mutableVars`)
5. **TypeCheck.fs** — Processes `TypeClassDecl` and `InstanceDecl`; builds and registers `RecordValue` dictionaries in the eval environment
6. **Eval.fs** — Minimal or no changes; receives fully elaborated AST with explicit dictionary applications as ordinary `App`/`Var` nodes

### Critical Pitfalls

1. **Constraint variables escaping let-generalization (TC-1)** — Extend `Scheme` to carry `Constraint list` from the very first commit; modify `generalize` to collect constraints on free variables simultaneously with the type variables. Never generalize a type without also generalizing its constraints. This is a correctness requirement, not an optimization.

2. **Eager constraint resolution before unification completes (TC-2)** — Maintain "wanted" (unresolved) and "given" (in-scope) constraint sets; resolve wanted constraints only after unification is finished for a binding group, not inline during `synth`. Attempting resolution on `TVar 1042` before it unifies with `int` produces false "no instance" errors that are difficult to debug.

3. **Dictionary scope leak in higher-order functions (TC-3)** — Use elaboration (rewrite the AST during type checking to add explicit dictionary parameters to constrained functions and explicit dictionary arguments at call sites). Do not thread a flat "dictionary environment" through the evaluator — closures would capture the wrong dictionary for higher-order uses.

4. **Dual dispatch incoherence for `to_string` and comparison operators (TC-4)** — Decide the migration strategy before writing any Phase 1 syntax: either builtins delegate through the type class mechanism or they are replaced entirely. Never leave both active simultaneously.

5. **`synth`/`check` signature explosion (LT-1)** — Thread `ClassEnv` and `InstanceEnv` as module-level mutable refs in `Bidir.fs` (the same pattern as `mutableVars`), not as additional function parameters. This avoids updating 67+ call sites.

---

## Implications for Roadmap

The feature dependency chain is strict: parsing and AST must precede type infrastructure wiring, which must precede constraint inference, which must precede dictionary construction and evaluation. The payoff features (built-in instances, operator refactoring) depend on all prior phases working. This dictates a 5-phase structure.

### Phase 1: Core Type Infrastructure
**Rationale:** Everything downstream depends on the `Scheme` shape and the new environment types. This phase has no parser dependencies and can be tested with F# unit tests before any `.fun` syntax changes.
**Delivers:** Extended `Scheme of vars * constraints * ty`; `Constraint`, `ClassInfo`, `InstanceInfo`, `ClassEnv`, `InstanceEnv` types in `Type.fs`; helper functions `mkScheme`/`schemeType` for backwards compatibility; mechanical update of all ~70 `Scheme([], ty)` to `Scheme([], [], ty)` sites in `TypeCheck.fs`; extended `formatSchemeNormalized` to display constraints.
**Addresses:** Table-stakes constraint representation.
**Avoids:** TC-1 (constraints must be in `Scheme` from the start), TC-11 (single substitution pass for type + constraints during instantiation), TC-12 (format constraints in error messages immediately).

### Phase 2: Parsing and AST
**Rationale:** New declaration forms must exist in the AST before the type checker can process them. LALR(1) conflict risk is highest here and must be resolved before other phases depend on the grammar.
**Delivers:** `typeclass`, `instance`, `where`, `FATARROW` (`=>`) tokens in `Lexer.fsl`; `TypeClassDecl` and `InstanceDecl` grammar rules in `Parser.fsy`; corresponding `Decl` variants and `TEConstrained` `TypeExpr` variant in `Ast.fs`; `Format.fs` and `--emit-ast` coverage; `failwith` stubs in `TypeCheck.fs` and `Eval.fs` to prevent silent swallowing.
**Avoids:** TC-8 (LALR(1) conflicts — use `where` not `=` in class/instance heads; add `FATARROW` as a distinct token; reserve keywords early), TC-13 (new `Decl` variants must be handled everywhere immediately; stubs prevent silent swallowing).

### Phase 3: Type Checker Wiring and Constraint Inference
**Rationale:** The core algorithmic work. Constraint propagation through `synth`/`check`, `generalize`, `instantiate`, and the class/instance environment pipeline. This phase makes programs type-check correctly; it does not yet produce evaluable output.
**Delivers:** `ClassEnv`/`InstanceEnv` as module-level mutable refs in `Bidir.fs`; `ClassDecl` processing (add methods to `TypeEnv` as constrained schemes); `InstanceDecl` processing (type-check method bodies, register in `InstanceEnv`); `resolveConstraint` linear search; constraint resolution in `Bidir.synth` `Var` case; constraint threading in `Infer.instantiate` and `Infer.generalize`; `InstanceEnv` propagated through the import/module pipeline; duplicate instance detection.
**Avoids:** TC-2 (defer resolution past unification), TC-6 (no superclass hierarchies in v10.0), TC-7 (extend `mutableVars` check to constraint generalization), TC-9 (thread `InstanceEnv` through import pipeline), TC-14 (error on duplicate instance registration), LT-1 (mutable refs pattern avoids call-site explosion).

### Phase 4: Dictionary Construction and Elaboration
**Rationale:** Bridges type checker resolution to the evaluator. Elaboration rewrites the AST in-place so the evaluator sees only ordinary `App`/`Var` nodes. This is the phase that makes programs execute.
**Delivers:** `InstanceDecl` processing emits a `RecordValue` dictionary bound in the eval environment under `__dict_ClassName_TypeKey`; inline elaboration in `Bidir.synth` wraps constrained `Var` lookups with `App(expr, Var dictName)` applications; constrained let-bound functions receive explicit dictionary lambda parameters; constrained parameterized instances (`Show (Option 'a)`) implemented as curried `FunctionValue` returning `RecordValue`.
**Avoids:** TC-3 (elaboration, not runtime env threading, to correctly handle higher-order functions), TC-10 (recursive context reduction for constrained instances with subgoals), Anti-Pattern 3 (no `MethodCall` AST node; evaluator stays type-class-unaware), LT-3 (`callValueRef` wiring for builtin method closures).

### Phase 5: Built-in Instances and Operator Migration
**Rationale:** The payoff phase. Once the machinery works end-to-end, wire up the concrete instances that replace hardcoded builtins. This phase is mostly Prelude `.fun` file additions plus targeted changes to `Eval.fs` and `Bidir.fs` to remove old dispatch paths.
**Delivers:** `Show`, `Eq`, `Ord` instances for all primitive types (`int`, `bool`, `string`, `char`); `Show (Option 'a)` constrained instance; `to_string` becomes an alias delegating to `Show.show`; `=` / `<>` become `Eq`-constrained; `<` / `>` / `<=` / `>=` become `Ord`-constrained; updated `flt` integration tests.
**Avoids:** TC-4 (single dispatch path — builtins either replaced or delegate through type class; dual paths are incoherent), TC-5 (clear ambiguous-type diagnostics with constraint name and source span).

### Phase Ordering Rationale

- Phase 1 is a prerequisite for everything: `Scheme` shape and environment types must exist before any other phase can compile.
- Phase 2 provides the AST nodes Phase 3 processes; parsing and type-checking are sequentially dependent.
- Phase 3 cannot resolve instances until Phase 1's environments exist and Phase 2's AST nodes can be constructed.
- Phase 4 cannot inject dictionaries until Phase 3 has resolved which instances exist.
- Phase 5 is the only phase that modifies Prelude `.fun` files and existing `flt` integration tests; it proceeds incrementally once Phase 4 proves the pipeline works end-to-end.
- The `Scheme` shape change (Phase 1) is the most invasive single change; doing it first ensures F# exhaustive pattern matching flags every incomplete update immediately as a compile error.

### Research Flags

Phases likely needing closer attention during planning:

- **Phase 3:** Constraint inference in `Bidir.fs` is the highest-uncertainty area. The exact design for "deferred wanted constraints" (collected during `synth`, resolved at let-boundaries) versus "given constraints" (in-scope class constraints) and how they interact with the existing `ctx: InferContext list` should be specified precisely before coding begins. LT-2 (GADT branch isolation vs. constraint propagation) is a related edge case to flag in the Phase 3 plan.
- **Phase 4:** Elaboration of constrained let-bound functions (adding implicit dict parameters) and constrained parameterized instances (dictionary-functions for `'a`-parameterized types) are the two sub-tasks most likely to require iteration.
- **Phase 2:** Audit `where` in `Lexer.fsl` before committing to the keyword — existing GADT syntax may already use it contextually. If it does, grammar rules must be adapted. Check before writing parser rules.

Phases with standard, well-documented patterns:

- **Phase 1:** Mechanical data structure extension. F# exhaustive matching is the complete verification strategy. No algorithmic uncertainty.
- **Phase 5:** Prelude instance wiring is additive and low-risk once Phase 4 is validated end-to-end.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Derived entirely from codebase inspection; no external dependency changes; implementation strategy matches GHC's own approach |
| Features | HIGH | Feature categorization is well-grounded; MVP scope is conservative and validated against the dependency chain; anti-features are explicitly argued |
| Architecture | HIGH | Standard HM-with-classes pattern (Jones 1994); component boundaries follow existing `ConstructorEnv`/`RecordEnv` precedent already in the codebase |
| Pitfalls | HIGH | Most pitfalls are LangThree-specific, derived from direct codebase inspection (67+ call sites, `mutableVars`, `callValueRef`); general pitfalls validated against GHC documentation |

**Overall confidence:** HIGH

### Gaps to Address

- **`where` keyword conflict:** `Lexer.fsl` must be audited before Phase 2 begins to confirm whether `WHERE` already exists. Low risk but must be checked first.
- **`synth` evidence representation:** Phase 3 requires `synth` to produce constraint resolution evidence alongside types. The exact F# representation (list of dict variable names, structured evidence type, or mutable ref accumulator) should be decided before Phase 3 coding starts. The mutable ref approach (like `mutableVars`) is recommended to avoid call-site explosion.
- **GADT branch constraint isolation (LT-2):** If constrained instances are used inside GADT match branches, the branch-local type refinement must be applied to constraints before they escape the branch. Flag this in the Phase 3 plan.
- **`callValueRef` wiring for builtin instance methods (LT-3):** When constructing built-in dictionaries (e.g., `Show int`), method closures must use `callValueRef` to invoke the evaluator. Account for this in Phase 4's dictionary construction design.

---

## Sources

### Primary (HIGH confidence)
- LangThree codebase (direct inspection, 2026-03-31) — `Type.fs`, `Ast.fs`, `Infer.fs`, `Bidir.fs`, `TypeCheck.fs`, `Eval.fs`, `Unify.fs`, `Elaborate.fs`
- Jones (1994), "Qualified Types: Theory and Practice" — standard reference for constraint-augmented HM
- GHC instance resolution documentation — authoritative on dictionary passing, Paterson conditions, linear-search resolution

### Secondary (MEDIUM confidence)
- [Implementing, and Understanding Type Classes — okmij.org](https://okmij.org/ftp/Computation/typeclass.html) — dictionary passing mechanics
- [Making dictionary passing explicit in Haskell — Joachim Breitner](https://www.joachim-breitner.de/blog/398-Making_dictionary_passing_explicit_in_Haskell) — GHC elaboration model
- [Hindley-Milner inference with constraints — Kwang's Haskell Blog](https://kseo.github.io/posts/2017-01-02-hindley-milner-inference-with-constraints.html) — constraint-based HM formulation
- [Type Classes — Tufts CS150PLD Notes](https://www.cs.tufts.edu/comp/150PLD/Notes/TypeClasses.pdf) — implementation walkthrough
- [Introduction to Haskell Typeclasses — Serokell](https://serokell.io/blog/haskell-typeclasses) — feature survey

### Tertiary (LOW confidence)
- [Coherence of type class resolution — Bottu et al.](https://xnning.github.io/papers/coherence-class.pdf) — formal coherence treatment (relevant if superclasses are added later)
- [Type Classes: Confluence, Coherence and Global Uniqueness — ezyang's blog](http://blog.ezyang.com/2014/07/type-classes-confluence-coherence-global-uniqueness/) — orphan instances and coherence

---
*Research completed: 2026-03-31*
*Ready for roadmap: yes*
