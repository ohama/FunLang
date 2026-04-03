# Domain Pitfalls: Typed AST Export for FunLang

**Domain:** Adding typed AST export to an existing ML-family interpreter
**Researched:** 2026-04-02
**Scope:** Pitfalls specific to adding typed AST export to FunLang's existing pipeline
**Context:** FunLang has HM inference (Infer.fs) + bidirectional checking (Bidir.fs) + type class
elaboration (Elaborate.elaborateTypeclasses). The current AST (Ast.Expr) carries NO type
annotations — it carries only source spans. TypeEnv (binding-name → Scheme) is the only
type information that survives type checking. The consumer (FunLangCompiler) needs concrete
types on every expression node for MLIR codegen.

---

## How to Read This File

Each pitfall has a **Phase** tag indicating which implementation phase is most at risk:

- **P1**: Design the typed AST data structure
- **P2**: Thread type collection through Bidir.synth / typeCheckDecls
- **P3**: Handle elaboration (InstanceDecl → LetDecl rewriting)
- **P4**: Serialize / export the typed AST
- **P5**: Consumer (FunLangCompiler) integration

---

## PART A: Critical Pitfalls (Cause Rewrites)

### Pitfall TA-1: Annotating Pre-Elaboration AST Instead of Post-Elaboration AST

**What goes wrong:** The pipeline has two distinct AST phases:
1. Pre-elaboration: `Decl list` from the parser, containing `InstanceDecl` nodes
2. Post-elaboration: `Decl list` after `Elaborate.elaborateTypeclasses`, where every `InstanceDecl` is replaced by ordinary `LetDecl` bindings

If type annotations are collected during type checking (which happens on the pre-elaboration AST) and stored per-node by AST identity (e.g., a `Map<Span, Type>`), the annotations reference spans from `InstanceDecl` method bodies — but those spans no longer exist as `InstanceDecl` in the post-elaboration tree. The consumer sees a post-elaboration `LetDecl` for `show_int` but cannot find a type annotation for it because the annotation was keyed to the `InstanceDecl` method body.

**Why it happens:** `typeCheckModuleWithPrelude` runs before `elaborateTypeclasses`. Any type map built during `typeCheckModuleWithPrelude` uses pre-elaboration structure. `Elaborate.elaborateTypeclasses` creates new `LetDecl` nodes at line 255:
```fsharp
methods |> List.map (fun (methodName, methodBody) ->
    LetDecl(methodName, methodBody, span))
```
These new nodes are not in any type annotation map because they are constructed after type checking.

**Consequences:** The consumer receives a fully annotated AST for user-defined bindings but untyped nodes for all type class method implementations. For MLIR codegen, these are exactly the nodes that carry polymorphic dispatch logic.

**Prevention:**
- Collect type annotations during type checking as `Map<string, Scheme>` (keyed by binding name), not as `Map<Span, Type>` (keyed by AST node).
- For instance methods, their types are already in `TypeEnv` after type checking: each method name maps to its scheme.
- Alternatively, collect annotations on the post-elaboration AST by doing a second lightweight pass (type annotation propagation) after elaboration — looking up binding names in `TypeEnv`.
- Never key type annotations to `Span` values — spans from `InstanceDecl` method bodies are reused in the `LetDecl` wrappers, but the mapping is fragile.

**Warning signs:**
- Type class method bodies have `TError` or `TVar ?_` types in the export.
- Every `show_*` / `eq_*` / `compare_*` binding in the export has an unknown type.

**Phase:** P1 (must be decided before any collection machinery is built).

---

### Pitfall TA-2: Exporting TVar Indices Instead of Resolved Concrete Types

**What goes wrong:** After `Bidir.synth`, the inferred type of a subexpression may still contain `TVar n` — either because the substitution has not been fully applied, or because the expression is legitimately polymorphic. The consumer (FunLangCompiler for MLIR) needs concrete types. If the export emits raw `TVar 1042` indices, the consumer cannot generate a concrete MLIR type.

**Why it happens:** `Bidir.synth` returns `(Subst * Type)` where the type may reference `TVar n` values that are in the returned `Subst` but have not yet been applied. The substitution must be explicitly applied to every collected type before export:
```fsharp
// WRONG: store type from synth directly
let ty = snd (synth ctorEnv recEnv ctx env expr)

// RIGHT: apply accumulated substitution before storing
let (s, ty) = synth ctorEnv recEnv ctx env expr
let resolvedTy = Type.apply s ty
```
Additionally, generalization at let-boundaries produces schemes with bound `TVar` indices (e.g., `Scheme([42], [], TArrow(TVar 42, TVar 42))`). If the export stores these raw, the consumer sees `TVar 42` rather than `'a`.

**Consequences:** MLIR codegen receives `TVar 1234` and cannot determine the MLIR type to emit. Either codegen fails or emits incorrect type casts.

**Prevention:**
- Apply the full accumulated substitution to every type before storing in the annotation map.
- In `typeCheckDecls`, the final `TypeEnv` has fully resolved schemes for top-level bindings — use those for export rather than mid-inference snapshots.
- For let-polymorphic bindings, the exported type should be the instantiated type at each use site, not the generalized scheme — unless the consumer explicitly needs the scheme.
- Add a `resolveType : Subst -> Type -> Type` post-processing step that replaces any remaining `TVar` with a fresh named variable for export.

**Warning signs:**
- Exported types contain `TVar` with large numeric indices (e.g., `TVar 1042`).
- Types that should be concrete (`int`, `bool`) are exported as type variables.

**Phase:** P2 (type collection). Apply substitution discipline from the start.

---

### Pitfall TA-3: Missing Types for Builtin and Prelude Bindings

**What goes wrong:** FunLang has two categories of bindings with implicit types:

1. **Hard-coded builtins** in `TypeCheck.initialTypeEnv` (e.g., `to_string`, `println`, `string_length`). Their types exist as F# `Scheme` values but have no corresponding `Expr` in any parsed AST — they are injected directly into `TypeEnv`.

2. **Prelude bindings** loaded from `Prelude/*.fun` files and evaluated before user code. Their types are in `prelude.TypeEnv` but the `Expr` nodes that define them are not part of the user module's `Decl list`.

If the typed AST export only annotates expressions from the user module, the consumer cannot type-check calls to `println`, `map`, `filter`, etc. — these appear in the user AST as `Var("println", span)` but their types are not in the per-module annotation.

**Why it happens:** `typeCheckModuleWithPrelude` merges `initialTypeEnv` and `prelude.TypeEnv` into a single environment before type-checking user code. The merged environment is used during inference but is not explicitly returned as "these are the external bindings available." The return value is only the user-module's `TypeEnv`.

**Consequences:** Consumer receives typed expressions for user code but untyped references to all standard library functions. For MLIR codegen, every call to a standard function produces a type error.

**Prevention:**
- Export must include a "preamble" type table covering all builtins and Prelude bindings, separate from per-expression annotations.
- `TypeCheck.initialTypeEnv` already exists as a `Map<string, Scheme>` — include it in the export as a "builtin type table."
- `prelude.TypeEnv` is returned from `Prelude.loadPrelude` — include it as a "stdlib type table."
- The consumer should look up `Var` node types in: (1) per-expression annotation map, (2) user module TypeEnv, (3) prelude TypeEnv, (4) builtin TypeEnv — in that order.
- Never assume a `Var` node's type is in the per-expression annotation map.

**Warning signs:**
- `println`, `map`, `filter`, `to_string` have missing or `TVar` types in the export.
- Only user-defined functions have resolved types; standard library functions are untyped.

**Phase:** P1 (export format design must include builtin/prelude tables) and P4 (serialization must emit them).

---

### Pitfall TA-4: Type Class Method Names Collide After Elaboration

**What goes wrong:** `Elaborate.elaborateTypeclasses` converts each `InstanceDecl` method into a top-level `LetDecl` with the method name as-is:
```fsharp
LetDecl(methodName, methodBody, span)
```
If two instances implement the same method (e.g., `Show int` and `Show string` both implement `show`), the elaborator emits two `LetDecl("show", ...)` bindings at the same scope level. In evaluation, the second binding shadows the first (last-wins). In a typed AST export, if the export stores binding names as keys, the second `show` overwrites the type of the first.

**Why it happens:** FunLang's type class dispatch uses last-definition-wins at evaluation time, which works because type checking has already resolved which `show` is called at each call site. But a typed AST export that is keyed by name rather than by span or node identity cannot distinguish `show : int -> string` from `show : string -> string` after elaboration.

**Consequences:** The exported `TypeEnv` for `show` contains only one type (the last one). The consumer incorrectly infers that `show` always has one concrete type, breaking calls to the other instance.

**Prevention:**
- Do NOT key the typed export by binding name alone for instance methods.
- Option A: Key call-site types by the call-site `Span` (each `App` or `Var` node's span is unique).
- Option B: During type checking (pre-elaboration), record per-call-site resolved instance method types in a `Map<Span, Type>` and export that alongside the `TypeEnv`.
- Option C: Rename instance methods during elaboration — `show_int`, `show_string` — and record the renaming map for the consumer.
- The consumer needs per-call-site types anyway (for MLIR); Option A or B is the correct direction.

**Warning signs:**
- Only one concrete type is exported for any overloaded method name.
- MLIR codegen for `show 42` and `show "hello"` generates the same type signature.

**Phase:** P1 (design — per-callsite vs. per-name keying) and P3 (elaboration must preserve or export the renaming).

---

## PART B: Moderate Pitfalls (Cause Delays and Technical Debt)

### Pitfall TA-5: Collecting Types Inside synth Without Threading the Annotation Map

**What goes wrong:** `Bidir.synth` is a recursive function with the signature:
```fsharp
let rec synth (ctorEnv: ConstructorEnv) (recEnv: RecordEnv) (ctx: InferContext list) (env: TypeEnv) (expr: Expr): Subst * Type
```
There are 67+ call sites. Adding a `typeAnnotationMap: Map<Span, Type> ref` parameter to collect per-subexpression types requires updating every call site — exactly the parameter-threading problem that `mutableVars` and `pendingConstraints` were designed to avoid (both use module-level `mutable`).

If the annotation map is NOT threaded, the only recourse is another module-level mutable. This is consistent with FunLang's existing pattern but has the same thread-safety caveat.

**Prevention:**
- Use the established FunLang pattern: module-level `mutable` in `Bidir.fs`.
- `let mutable typeAnnotations : Map<Span, Type> = Map.empty` — reset at each `typeCheckModuleWithPrelude` entry, populated by `synth` at every node.
- This is consistent with `mutableVars` and `pendingConstraints` — already accepted precedent in this codebase.
- Do NOT add `typeAnnotationMap` as a parameter to `synth`/`check` — the call-site explosion is not worth it.

**Warning signs:**
- `synth` has a new parameter not present in `check`; `check` lacks annotation collection.
- Some expression variants annotate correctly but others are missed because the `check` → `synth` delegation doesn't thread the map.

**Phase:** P2. Decide the collection strategy before writing any collection code.

---

### Pitfall TA-6: Let-Generalization Exports Schemes Where Consumer Needs Monotypes

**What goes wrong:** At a let-binding, `Bidir.generalize` produces a `Scheme` (e.g., `forall 'a. 'a -> 'a`). This scheme is stored in `TypeEnv`. But within the body of a function, each specific call site has a concrete monotype. If the export stores the generalized `Scheme` for every `Var` reference to a polymorphic binding, the consumer sees `forall 'a. 'a -> 'a` at every call site — it cannot determine the concrete instantiation for that particular call.

For MLIR codegen, the instantiation matters: `id 42` needs `int -> int`, not `forall 'a. 'a -> 'a`.

**Prevention:**
- The annotation for a `Var(name, span)` should be the INSTANTIATED type at that call site, not the Scheme.
- `Bidir.instantiateAt` already creates a fresh substitution and returns the instantiated monotype. The type returned by `synth (Var(name, span))` IS the instantiated monotype — collect that, not the scheme from TypeEnv.
- Schemes are only useful in the export as metadata for top-level binding declarations (for the consumer to understand what type parameters exist). Export them separately from per-expression types.
- Distinguish in the export format: `binding_schemes: Map<string, Scheme>` (for declarations) vs. `expr_types: Map<Span, Type>` (for expressions, always monotypes after instantiation).

**Warning signs:**
- Every `Var` reference to a polymorphic function has quantified type variables in the export.
- Consumer cannot distinguish `id 42 : int` from `id "hi" : string` — both show `'a`.

**Phase:** P1 (format design) and P2 (collection must use the instantiated type from `synth`, not `TypeEnv` lookup).

---

### Pitfall TA-7: Breaking Existing Interpreter Behavior by Modifying synth Return Type

**What goes wrong:** A tempting approach is to change `synth` to return `(Subst * Type * TypedExpr)` — a new typed expression tree alongside the existing results. This touches every `synth` and `check` call site (67+), every match branch that pattern-matches on `synth`'s return, and would require a parallel `TypedExpr` discriminated union mirroring `Ast.Expr`.

This is a major refactor. It risks introducing bugs in the inference algorithm itself because the compiler will correctly flag all unhandled cases but may miss subtle logic errors in how `TypedExpr` nodes are constructed.

**Prevention:**
- Do NOT change `synth`'s return type for the MVP of typed AST export.
- The mutable collection approach (TA-5 prevention) avoids changing any existing signatures.
- Build a separate `TypedExpr` representation only if needed by the consumer — and populate it in a separate pass over `Expr` using the collected `Map<Span, Type>`, not inline in `synth`.
- Gate the type annotation collection behind a flag so it has zero cost when not exporting: `if Bidir.collectingTypeAnnotations then Bidir.typeAnnotations <- Map.add span ty Bidir.typeAnnotations`.

**Warning signs:**
- `dotnet build` emits hundreds of new incomplete-match warnings after changing `synth` return type.
- The number of tests failing after the change is more than 5.

**Phase:** P2. The decision between "inline annotation during synth" vs. "post-pass annotation" is the highest-impact design choice.

---

### Pitfall TA-8: Type of `to_string` and Other Permissively Polymorphic Builtins

**What goes wrong:** Several builtins in `TypeCheck.initialTypeEnv` have intentionally broad types:
```fsharp
"to_string", Scheme([0], [], TArrow(TVar 0, TString))
"printf",    Scheme([0], [], TArrow(TString, TVar 0))
"failwith",  Scheme([0], [], TArrow(TString, TVar 0))
```
These schemes are correct for type checking but useless for MLIR codegen — `TVar 0` resolves to the type at the call site, but that resolution only happens through the inference substitution. If the export emits `'a -> string` for `to_string`, the consumer cannot generate MLIR code.

**Why it happens:** `to_string` is intentionally polymorphic because it handles `int`, `bool`, `string`, `char`, and ADT values via runtime dispatch in `Eval.fs`. It has no type class constraint; it is simply broad. The call-site instantiation IS available — `synth (App(Var("to_string"), arg))` will unify `TVar 0` with the arg type and the substitution will resolve it.

**Prevention:**
- For `to_string` and similar builtins, the per-call-site type IS resolvable from the collected `Map<Span, Type>` — the `App` node's argument type determines the substitution.
- Ensure the collection captures the type of the `App` node, not just the `Var` node for `to_string`.
- For the consumer, document that `to_string` and `printf`/`sprintf`/`printfn`/`failwith` are polymorphic and the concrete argument type must be looked up from the argument expression's span.
- Alternatively, for these specific builtins, emit them as having multiple concrete overloads in the export (one per observed call-site argument type).

**Warning signs:**
- `to_string 42` exports as `('a -> string) applied to int` rather than `(int -> string) applied to int`.
- Consumer cannot specialize `to_string` for specific types.

**Phase:** P3 (export format) and P5 (consumer integration documentation).

---

### Pitfall TA-9: Performance Impact of Annotating Every Subexpression

**What goes wrong:** FunLang's `Bidir.synth` is called on every subexpression during type checking — for a 500-line program, this may be tens of thousands of calls. If every call performs a `Map.add span ty` on a mutable annotation map, and the map grows to tens of thousands of entries, the constant allocation pressure and map rebalancing may noticeably slow type checking — especially because FunLang uses F#'s immutable `Map` (an AVL tree), not a mutable dictionary.

**Why it happens:** `Map<Span, Type>` in F# is an immutable balanced tree. Each `Map.add` produces a new map. With a mutable ref cell (`mutable typeAnnotations`), each mutation replaces the ref with a new map, causing repeated allocation.

**Prevention:**
- Use `System.Collections.Generic.Dictionary<Span, Type>` (mutable hashtable) instead of F#'s immutable `Map`.
- `Span` needs a structural equality comparison for hashing — since `Span` is a record, F# derives structural equality by default, so it can be used as a dictionary key without extra work.
- Gate annotation collection behind a flag (see TA-7 prevention) so normal interpretation incurs zero overhead.
- Only enable collection when `--emit-typed-ast` (or equivalent flag) is passed.

**Warning signs:**
- `dotnet test` takes noticeably longer after annotation collection is added.
- Memory usage during large file type checking increases substantially.

**Phase:** P2. The collection data structure choice must be made before implementing collection.

---

## PART C: Minor Pitfalls (Annoying but Fixable)

### Pitfall TA-10: Span Collisions for Synthetic AST Nodes

**What goes wrong:** Some `Expr` nodes are created synthetically with `Ast.unknownSpan` (e.g., nodes injected by match compilation in `MatchCompile.fs`, or the `LetDecl` wrappers created by `elaborateTypeclasses`). If the export is keyed by `Span`, multiple synthetic nodes have the same key (`unknownSpan = { FileName = "<unknown>"; ... }`), and map insertion silently overwrites earlier entries.

**Prevention:**
- When keying by span, skip nodes with `span = Ast.unknownSpan` or handle them separately.
- For synthetic nodes from `elaborateTypeclasses`, their types are determined by the instance's method name — look them up in `TypeEnv` rather than the per-span annotation map.
- Alternatively, assign unique synthetic spans to elaborated nodes: a counter-based `{ FileName = "<elaborated>"; StartLine = n; ... }`.

**Warning signs:**
- Multiple instance method bodies all map to the same key in the annotation map.
- Only the last instance processed has type information; earlier ones are overwritten.

**Phase:** P3 (elaboration). Address when `elaborateTypeclasses` is extended to produce annotated output.

---

### Pitfall TA-11: GADT Branch Type Annotations Capture Branch-Local Refinements

**What goes wrong:** FunLang's GADT checking in `Bidir.fs` uses per-branch substitutions that refine type variables local to that branch. For example, in `match (e : Expr int) with | Num n -> n`, the branch knows the result is `int` via the GADT refinement. If per-expression types are collected during GADT branch checking, the collected types may include branch-local `TVar` refinements that are not valid outside the branch.

**Why it happens:** GADT branches in `Bidir.fs` apply a branch-specific substitution before checking the branch body. Types collected inside the branch body are relative to that substitution. If the annotation map stores raw `Type` values without recording which substitution produced them, the consumer may misinterpret a branch-local `TVar 1099 = TInt` annotation as a global fact.

**Prevention:**
- Apply the full accumulated substitution (including branch-local GADT refinements) to collected types before storing them.
- This is consistent with TA-2 prevention — always apply the current substitution before storing a type annotation.
- For the common case, this is already correct if annotations are stored at the end of each branch check using the branch-final substitution.

**Warning signs:**
- GADT match branch expressions have wrong types in the export (e.g., `'a` instead of `int`).
- Types correct for simple matches but wrong for GADT matches.

**Phase:** P2 (collection). Must apply substitution discipline inside GADT branch checking.

---

### Pitfall TA-12: Consumer Misuse — Treating Scheme as Monotype

**What goes wrong:** The export contains two categories of types:
- `expr_types: Map<Span, Type>` — always monotypes (instantiated at call site)
- `binding_schemes: Map<string, Scheme>` — may be polymorphic (`Scheme([42], [], ...)`)

Consumers that treat `binding_schemes` entries as monotypes will see `TVar 42` and misinterpret it as an unresolved type variable rather than a quantified type parameter.

**Prevention:**
- Document the distinction clearly in the export format.
- Name the fields distinctly: do not use `type` for both categories.
- Provide a helper: `instantiateScheme : Scheme -> Type list -> Type` that maps type arguments to quantified variables — the consumer should call this when it needs a concrete instantiation.
- Include the fact that `TVar n` in a `Scheme` where `n` is in the `vars` list is a bound variable (parameter), not a free inference variable.

**Warning signs:**
- Consumer reports type errors on all polymorphic functions.
- Consumer treats `forall 'a. 'a -> 'a` as having type `TVar 42 -> TVar 42` with unknown `TVar 42`.

**Phase:** P5 (consumer integration). Primarily a documentation and API design issue.

---

### Pitfall TA-13: Over-Engineering the Export Format

**What goes wrong:** Attempting to export a complete typed IR with resolved dictionary passing, monomorphized instances, explicit type applications, and reconstructed spine forms before MLIR codegen actually requires them. This adds weeks of work and produces a complex format that changes as the consumer's needs are clarified.

**Concrete over-engineering traps:**
- Emitting explicit dictionary arguments in the export AST (like GHC's Core) before the consumer proves it needs them.
- Monomorphizing all polymorphic functions in the export (like MLton) before knowing the consumer's optimization strategy.
- Defining a complex JSON schema with 30+ node types before the consumer has written a single MLIR lowering pass.

**Prevention:**
- Start with the minimal format: `(post-elaboration Decl list) + (Map<Span, Type> for expressions) + (Map<string, Scheme> for top-level bindings) + (Map<string, Scheme> for builtins/prelude)`.
- Let the consumer drive format evolution: add features only when a specific MLIR lowering pass requires them.
- The first consumer milestone should use the export — if it can type all nodes it needs, the format is sufficient.

**Warning signs:**
- The export format design phase takes longer than the implementation phase.
- The export format has fields that no consumer code reads.

**Phase:** P1 (format design). Make the MVP format as simple as possible.

---

## PART D: Phase-Specific Warning Summary

| Phase | Topic | Most Likely Pitfall | Mitigation |
|-------|-------|--------------------|-|
| P1 | Format design | Pre-elaboration vs. post-elaboration mismatch (TA-1) | Key by binding name or call-site span, not InstanceDecl structure |
| P1 | Format design | Over-engineering the format (TA-13) | Start minimal: Decl list + two Maps |
| P1 | Format design | Per-name keying of instance methods (TA-4) | Use per-call-site span as primary key |
| P2 | Type collection | Raw TVar in collected types (TA-2) | Apply accumulated substitution before storing |
| P2 | Type collection | Threading annotation map through 67+ synth sites (TA-5) | Module-level mutable (established FunLang pattern) |
| P2 | Type collection | Storing Scheme where consumer needs monotype (TA-6) | Collect instantiated type from synth return, not TypeEnv lookup |
| P2 | Type collection | Breaking existing synth signature (TA-7) | Do NOT change synth return type; use mutable collection |
| P2 | Type collection | Performance of Map allocation per call (TA-9) | Use Dictionary<Span, Type>, gate behind flag |
| P2 | Type collection | GADT branch-local types escaping (TA-11) | Apply branch substitution before storing |
| P3 | Elaboration | Synthetic nodes have unknownSpan (TA-10) | Handle unknownSpan nodes via TypeEnv name lookup |
| P4 | Export | Permissively polymorphic builtins (TA-8) | Export call-site argument type alongside builtin type |
| P4 | Export | Missing builtin/prelude types (TA-3) | Include builtin and prelude type tables in export |
| P5 | Consumer | Treating Scheme as monotype (TA-12) | Document Scheme vs. Type distinction; provide instantiateScheme helper |

---

## PART E: FunLang-Specific Architecture Risks

These pitfalls arise specifically from FunLang's existing design, not from the general problem.

### Risk AX-1: `Bidir.mutableVars` Pattern Has Thread-Safety Caveat

The existing mutable state in `Bidir.fs` (`mutableVars`, `pendingConstraints`, `currentClassEnv`, `currentInstEnv`) is explicitly not thread-safe (see comment in `Bidir.fs` line 23: "Tests must run sequentially"). Adding `typeAnnotations` as another mutable follows the same pattern but reinforces the sequential-only constraint. If FunLang ever moves to parallel compilation, this becomes a blocker.

**Mitigation:** Document the thread-safety constraint explicitly in the new `typeAnnotations` declaration, consistent with the existing comment. The sequential-only constraint is acceptable for the current milestone scope.

---

### Risk AX-2: `elaborateTypeclasses` Is Not the Only AST-Rewriting Pass

`Elaborate.elaborateTypeclasses` is invoked in `Program.fs` at lines 211 and 465. But `MatchCompile.fs` may also rewrite match expressions. If typed AST export runs before match compilation, the consumer receives pre-compilation match trees (with `OrPat`, nested patterns, etc.) that differ from what the evaluator actually executes.

**Mitigation:** Clarify the exact pipeline stage at which the export runs. For MLIR codegen, post-match-compilation AST is likely needed (since MLIR needs explicit case analysis, not high-level pattern matching). Ensure the export pass runs at the same stage as evaluation.

---

### Risk AX-3: `TypeCheck.typeCheckDecls` Discards Per-Expression Types

`typeCheckDecls` returns `TypeEnv * ConstructorEnv * RecordEnv * ClassEnv * InstanceEnv * Map<string, ModuleExports> * Diagnostic list`. There is no per-expression type in the return. All per-expression type information computed during `Bidir.synth` is discarded unless explicitly collected via the mutable approach described in TA-5.

**Mitigation:** Treat `typeCheckDecls` as a black box and collect types inside `Bidir.synth` directly — do not try to extract them from `typeCheckDecls`'s return value. The `TypeEnv` in the return is only top-level binding names → schemes; it does not cover subexpression types.

---

## Sources

- FunLang source: `/src/FunLang/Bidir.fs` — `synth`, `generalize`, `instantiateAt`, mutable state patterns
- FunLang source: `/src/FunLang/Elaborate.fs` — `elaborateTypeclasses` implementation showing InstanceDecl → LetDecl rewriting
- FunLang source: `/src/FunLang/TypeCheck.fs` — `typeCheckModuleWithPrelude` return type (no per-expression types)
- FunLang source: `/src/FunLang/Ast.fs` — `Expr` carries only `Span`, no type field
- FunLang source: `/src/FunLang/Type.fs` — `Scheme`, `Constraint`, substitution machinery
- FunLang source: `/src/FunLang/Program.fs` — pipeline order: typecheck → elaborateTypeclasses → evalModuleDecls
- GHC source code notes on Core IR (System F) — motivation for post-elaboration typed representation
- "Typing Haskell in Haskell" (Jones 1999) — per-expression type annotation approach in HM systems
