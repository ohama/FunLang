# Architecture: Typed AST Export

**Domain:** Adding Typed AST export to an existing ML-style interpreter
**Researched:** 2026-04-02
**Confidence:** HIGH (full source read)

---

## Overview

FunLangCompiler currently maintains its own copy of FunLang's Parser/Lexer and runs its own
elaboration pass with type-guessing heuristics. The goal is to replace that with a single
authoritative call to FunLang's type inference, receiving back a typed representation that
the compiler can walk to generate MLIR.

This document defines how the Typed AST export fits into FunLang's existing architecture,
what new components are needed, and the recommended build order.

---

## Existing Pipeline (Authoritative)

```
Source text
    |
[Lexer.fsl + IndentFilter.fs]  ->  filtered token stream
    |
[Parser.fsy]  ->  Ast.Module   (untyped; every node carries Span)
    |
[TypeCheck.typeCheckModuleWithPrelude]
    |  calls Bidir.synth/check internally
    |  returns: Ok(warnings, ctorEnv, recEnv, classEnv, instEnv, modules, typeEnv)
    |
[Elaborate.elaborateTypeclasses]  ->  desugared Decl list
    |                               (InstanceDecl -> LetDecl, TypeClassDecl removed)
    |
[Eval.evalModuleDecls]  ->  Value
```

Key facts that drive design decisions:

1. `Bidir.synth` returns `(Subst * Type)`. The `Subst` must be applied to get the final
   ground type for a node. No per-node type annotations are stored anywhere in the current
   pipeline; type information lives only in the `TypeEnv` at the top-level binding level
   after type checking finishes.

2. `typeCheckModuleWithPrelude` returns `typeEnv: TypeEnv` which maps **top-level binding
   names** to `Scheme`. It does NOT return types for sub-expressions. There is no existing
   data structure that associates types with sub-expression spans.

3. `Elaborate.elaborateTypeclasses` runs AFTER type checking. It rewrites `InstanceDecl`
   into `LetDecl` nodes and removes `TypeClassDecl` nodes. The AST seen by `Eval` is
   therefore not the same as the AST seen by `Bidir`. If the compiler needs the elaborated
   form, it must call `elaborateTypeclasses` on the post-typecheck decls.

4. The `Prelude` is loaded via `Prelude.loadPrelude`, which type-checks and evaluates all
   `Prelude/*.fun` files and caches results. The returned `PreludeResult` contains fully
   resolved `TypeEnv`, `CtorEnv`, `RecEnv`, `ClassEnv`, `InstEnv`. Prelude types are just
   entries in these maps — there is no separate "prelude typed AST" to handle.

---

## What FunLangCompiler Needs

The compiler needs, for each top-level let binding in the user's file:

1. The resolved `Type` of the binding (not a `Scheme` with type variables — a ground type
   after instantiation, or the scheme itself if the value is polymorphic).
2. For function bodies: the resolved `Type` at every sub-expression node, so the MLIR
   lowering knows what types to emit for locals, parameters, and return values.
3. Constructor and record type metadata (already in `CtorEnv`/`RecEnv` — these can be
   passed directly).
4. The elaborated `Decl list` (post-`elaborateTypeclasses`) to walk for code generation.

Items 3 and 4 are already available from the current pipeline. Item 1 is partially available
(top-level schemes in `TypeEnv`). Item 2 is not available at all — it requires a structural
change to the type checking pass.

---

## Design Decision: Separate TypedAst vs. Annotation Map

Two approaches exist for making per-node types available:

### Option A: Annotation Map (type table keyed by Span)

During `Bidir.synth`, record `(span, finalType)` pairs into a mutable side table. After type
checking, the compiler queries this table by span to get the type of any expression.

```fsharp
// New module: TypedAst.fs (or inline in Bidir.fs)
let typeTable = System.Collections.Generic.Dictionary<Ast.Span, Type>()
let recordType (span: Ast.Span) (ty: Type) = typeTable.[span] <- ty
```

Call `recordType span (apply s ty)` at each synth/check call site in Bidir.fs after the
substitution is known.

**Pros:**
- No new AST type needed
- Minimal change to Bidir.fs (additive)
- Compiler keeps walking the existing `Ast.Expr` / `Ast.Decl` types it already knows
- Spans are already on every node in `Ast.fs`, providing the key

**Cons:**
- Relies on Span uniqueness. The `unknownSpan` sentinel (`FileName = "<unknown>"`) is shared
  across synthetic nodes. Synthetic nodes (builtins, elaborated nodes) will collide.
- Mutable global state — not thread-safe (acceptable since the existing codebase uses the
  same pattern: `Bidir.mutableVars`, `Bidir.pendingConstraints`, etc.)
- After `elaborateTypeclasses`, new `LetDecl` nodes are created with spans copied from
  `InstanceDecl` — those nodes have types but were never in the type table

### Option B: Typed AST (new parallel DU)

Define a `TypedExpr` / `TypedDecl` type in a new `TypedAst.fs`, where every node carries
a `Type` field. `Bidir.synth` or a new wrapper produces `TypedExpr` in parallel with
returning `(Subst * Type)`.

```fsharp
// TypedAst.fs
type TypedExpr =
    | TNumber  of value: int   * ty: Type * span: Ast.Span
    | TVar     of name: string * ty: Type * span: Ast.Span
    | TApp     of func: TypedExpr * arg: TypedExpr * ty: Type * span: Ast.Span
    | TLambda  of param: string * body: TypedExpr * ty: Type * span: Ast.Span
    | TLet     of name: string * rhs: TypedExpr * body: TypedExpr * ty: Type * span: Ast.Span
    // ... one variant per Ast.Expr variant
```

**Pros:**
- Types are structurally attached; no lookup needed
- Compiler can pattern match on `TypedExpr` directly
- No span-uniqueness requirement

**Cons:**
- ~50+ new DU variants mirroring every `Ast.Expr` variant (Ast.fs has 40+ Expr variants)
- Bidir.synth must be rewritten to produce `TypedExpr` instead of/alongside the untyped AST
- Major refactor: every call site in Bidir.fs changes signature
- `Ast.Decl` mirroring (`TypedDecl`) doubles the size again

### Recommendation: Option A (Annotation Map) for Phase 1

The annotation map approach is strictly additive — Bidir.fs gains `recordType` calls but
its signature does not change. The compiler can start consuming it immediately. The typed
AST approach is the long-term ideal but requires significant refactoring that should be a
separate milestone after the basic export works.

For the collision issue with `unknownSpan`: synthetic nodes in the Prelude already have
`unknownSpan`. The compiler does not need types for Prelude internal nodes — it only needs
types for user-file nodes. Prelude nodes can be excluded from recording. A `span.FileName <> "<unknown>"` guard at each `recordType` call site handles this.

---

## New Components

### Component 1: TypeAnnotationMap.fs (NEW FILE)

Position in build order: after `Bidir.fs`, before `TypeCheck.fs`.

```fsharp
module TypeAnnotationMap

open Ast
open Type

/// Mutable table: span -> resolved Type, populated during Bidir.synth.
/// Keyed by (FileName, StartLine, StartColumn) tuple for uniqueness.
/// Cleared at each top-level typeCheckModuleWithPrelude entry.
let private table =
    System.Collections.Generic.Dictionary<Span, Type>()

let record (span: Span) (ty: Type) =
    if span.FileName <> "<unknown>" then
        table.[span] <- ty

let tryFind (span: Span) : Type option =
    match table.TryGetValue(span) with
    | true, ty -> Some ty
    | _ -> None

let clear () = table.Clear()

let snapshot () : Map<Span, Type> =
    table |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
```

`snapshot()` is the function the export API calls to hand a frozen copy to the compiler.

### Component 2: ExportApi.fs (NEW FILE)

Position: after `Prelude.fs`, before `Program.fs`.

This is the single entry point for FunLangCompiler. It wraps the existing pipeline and
bundles all output into a single `TypedModule` record.

```fsharp
module ExportApi

open Ast
open Type
open TypeCheck

/// Everything the compiler needs from one type-checked file
type TypedModule = {
    /// Elaborated declarations (post-elaborateTypeclasses, ready for code generation)
    Decls: Decl list
    /// Top-level binding types: name -> Scheme
    TopLevelTypes: TypeEnv
    /// Per-expression types: span -> Type (resolved, substitution applied)
    ExprTypes: Map<Ast.Span, Type>
    /// Constructor metadata
    CtorEnv: ConstructorEnv
    /// Record metadata
    RecEnv: RecordEnv
    /// Class metadata
    ClassEnv: ClassEnv
    /// Instance metadata
    InstEnv: InstanceEnv
    /// Type-check warnings
    Warnings: Diagnostic.Diagnostic list
}

/// Type-check a source file and return the typed module.
/// `prelude` is obtained from Prelude.loadPrelude; callers should cache it.
val typeCheckFile :
    prelude: Prelude.PreludeResult ->
    source: string ->
    filename: string ->
    Result<TypedModule, Diagnostic.Diagnostic list>
```

The implementation calls:
1. `Program.parseModuleFromString` (or inlined parseModule logic)
2. `TypeAnnotationMap.clear()`
3. `TypeCheck.typeCheckModuleWithPrelude` (populates TypeAnnotationMap as side effect)
4. `Elaborate.elaborateTypeclasses` on the decls
5. `TypeAnnotationMap.snapshot()`
6. Bundle everything into `TypedModule`

### Component 3: Bidir.fs modifications (EXISTING FILE, ADDITIVE)

Add `TypeAnnotationMap.record span (apply s ty)` calls at the end of each `synth` and
`check` case in Bidir.fs. The canonical location is at the point where the final
substitution is known and applied.

Pattern for each synth case:

```fsharp
| Number(n, span) ->
    let ty = TInt
    TypeAnnotationMap.record span ty     // ADD THIS
    (empty, ty)

| App(func, arg, span) ->
    // ... existing logic ...
    let finalTy = apply composedSubst resultTy
    TypeAnnotationMap.record span finalTy    // ADD THIS
    (composedSubst, finalTy)
```

The `check` function does not need to record — `check` always defers to `synth` via
subsumption for the type it checks against. Recording in `synth` is sufficient.

**Number of sites:** Approximately 40 expression variants in `Ast.Expr`. Each synth case
gets one `record` call. This is mechanical, low-risk work.

---

## Integration Points

### Where in the pipeline to extract type info

Extract **after Bidir.synth**, not after Infer. The type is final only after unification
has run and the substitution has been applied. Bidir is the authoritative type checker —
Infer.fs is now just a helpers module (see Infer.fs comment at top: "main entry points
are deprecated in favor of Bidir.synth/synthTop").

The substitution threading in Bidir is the critical detail: `synth` returns `(Subst * Type)`
where the `Subst` maps type variable IDs to concrete types. The `Type` return value is
already partially substituted. The final type is `apply s ty` where `s` is the accumulated
substitution at that call site. **Record `apply s ty`, not the raw `ty`.**

### How type class elaboration interacts

`Elaborate.elaborateTypeclasses` runs AFTER type checking on the original parsed `Decl list`.
It replaces `InstanceDecl(cls, ty, methods, span)` with individual `LetDecl(methodName, body, span)`
nodes — the span is copied from the `InstanceDecl`.

For the compiler:

- The elaborated `LetDecl` nodes for type class methods will have spans that correspond to
  the `InstanceDecl` span, not to the individual method body spans. The `TypeAnnotationMap`
  will have entries for the method body sub-expressions (recorded during synth when
  TypeCheck.fs type-checked the InstanceDecl methods), but the top-level `LetDecl` name
  itself may not have a table entry.
- **Mitigation:** For InstanceDecl-derived LetDecls, look up the method's `Scheme` from
  `typeEnv` by method name. This is always available.

### How Prelude is handled

The `Prelude.PreludeResult` provides fully-resolved `TypeEnv`, `CtorEnv`, `RecEnv`,
`ClassEnv`, `InstEnv` for all prelude definitions. The compiler does not need a typed AST
for prelude functions — it only needs their types, which are already in `PreludeResult.TypeEnv`.

For caching: `Prelude.loadPrelude` is already expensive (parses and type-checks all
`Prelude/*.fun` files). The result should be cached at the process level. `ExportApi.fs`
should accept a pre-loaded `PreludeResult` rather than calling `loadPrelude` internally.

The internal `tcCache` and `evalCache` in `Prelude.fs` are process-level dictionaries —
repeated `loadPrelude` calls with the same paths do use the file-level cache, but the
`loadPrelude` function itself still iterates all files to build `PreludeResult`. Cache the
`PreludeResult` at the call site.

### How FunLangCompiler consumes this

Two deployment models are feasible:

**Model A: In-process library reference**

FunLangCompiler adds a project reference to `FunLang.fsproj`. It calls `ExportApi.typeCheckFile`
directly. The `TypedModule` is an F# record in memory.

Advantage: No serialization overhead, full F# type safety.
Disadvantage: FunLangCompiler must be an F# project (or .NET project). Cross-language use
requires wrapping.

**Model B: CLI invocation with JSON/binary output**

Add a `--emit-typed-ast` flag to `Program.fs` that runs the full pipeline and writes the
`TypedModule` to stdout as JSON (or a binary format).

Advantage: FunLangCompiler can be in any language. Easy to debug (JSON is readable).
Disadvantage: Serialization overhead; JSON representation of `Type` DU requires schema design.

**Recommendation: Model A for the initial milestone.** FunLangCompiler is already a .NET
project. A project reference eliminates all serialization complexity. Model B can be added
later if cross-language consumption is needed.

---

## Data Flow Changes

### Current data flow (before this milestone)

```
parseModuleFromString -> Module
    |
typeCheckModuleWithPrelude -> (warnings, ctorEnv, recEnv, classEnv, instEnv, modules, typeEnv)
    |                          ^--- only top-level names have types here
    |
elaborateTypeclasses -> Decl list
    |
evalModuleDecls -> Value
```

### New data flow (after this milestone)

```
parseModuleFromString -> Module
    |
TypeAnnotationMap.clear()           <-- NEW
    |
typeCheckModuleWithPrelude -> (warnings, ctorEnv, recEnv, classEnv, instEnv, modules, typeEnv)
    |                          ^--- Bidir.synth now calls TypeAnnotationMap.record as side effect
    |
TypeAnnotationMap.snapshot() -> Map<Span, Type>   <-- NEW
    |
elaborateTypeclasses -> Decl list
    |
Bundle into TypedModule { Decls, TopLevelTypes, ExprTypes, CtorEnv, RecEnv, ... }   <-- NEW
    |
FunLangCompiler consumes TypedModule
    (walks Decl list, looks up ExprTypes[span] for each expression)
```

The `evalModuleDecls` call is NOT part of the export path — the compiler does not need
evaluated `Value`s. The export pipeline terminates after type checking and elaboration.

---

## Suggested Build Order

### Phase 1: TypeAnnotationMap module

Build `TypeAnnotationMap.fs` as a standalone module with no call sites yet. Add it to
`FunLang.fsproj` between `Bidir.fs` and `TypeCheck.fs`. Verify it compiles.

Files: `TypeAnnotationMap.fs`, `FunLang.fsproj`

Test: F# unit test that `record`, `tryFind`, `clear`, `snapshot` work correctly.

### Phase 2: Wire Bidir.fs to record types

Add `TypeAnnotationMap.record span (apply s ty)` at the end of each `synth` case in
`Bidir.fs`. Run existing test suite to verify no regressions.

Files: `Bidir.fs`

Test: After type-checking a simple module, `TypeAnnotationMap.snapshot()` should contain
entries for every expression span in the source. Verify with a new unit test that types for
specific spans are correct (e.g., `Number(42, span)` maps to `TInt`).

### Phase 3: ExportApi module

Build `ExportApi.fs` with `TypedModule` type and `typeCheckFile` function. Wire it to the
existing pipeline. Add it to `FunLang.fsproj` after `Prelude.fs`.

Files: `ExportApi.fs`, `FunLang.fsproj`

Test: Call `ExportApi.typeCheckFile` on a known source file, verify `TypedModule.ExprTypes`
is non-empty and contains correct types for a handful of known spans.

### Phase 4: CLI flag (optional, for debugging)

Add `--emit-typed-ast` to `Cli.fs` and `Program.fs`. Serialize `TypedModule` to JSON for
inspection. This is not required for FunLangCompiler Model A but is valuable for debugging
the export during compiler integration.

Files: `Cli.fs`, `Program.fs`

### Phase 5: FunLangCompiler integration

FunLangCompiler adds project reference, removes its own Parser/Lexer copy, removes its
`Elaboration.fs`, calls `ExportApi.typeCheckFile`, and uses real types from `ExprTypes` and
`TopLevelTypes` instead of heuristics.

This phase is in the FunLangCompiler repo, not FunLang.

---

## Component Interaction Map

```
TypeAnnotationMap.fs  (NEW)
  |- no dependencies on other FunLang modules
  |- populated by: Bidir.fs (side effect during synth)
  |- consumed by: ExportApi.fs (snapshot), TypeCheck.fs (clear at entry)

Bidir.fs  (MODIFIED, additive)
  |- adds: TypeAnnotationMap.record calls at each synth case
  |- signature unchanged: synth still returns (Subst * Type)
  |- existing tests unaffected

TypeCheck.fs  (MODIFIED, minimal)
  |- adds: TypeAnnotationMap.clear() at start of typeCheckModuleWithPrelude
  |- no other changes

ExportApi.fs  (NEW)
  |- depends on: TypeAnnotationMap, TypeCheck, Prelude, Elaborate, Ast, Type, Diagnostic
  |- exposes: TypedModule, typeCheckFile
  |- does NOT depend on: Eval (eval is not needed for compilation)

Program.fs  (MODIFIED, optional)
  |- adds: --emit-typed-ast flag wired to ExportApi
```

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Recording types before applying the substitution

In `Bidir.synth`, the raw `ty` return value often contains unresolved type variables
(`TVar n`). These are resolved by the accumulated `Subst`. Always record `apply s ty`,
never the raw `ty`. Failure to do this produces a table full of `TVar` entries that are
useless to the compiler.

**Detection:** If the table contains `TVar` entries for non-polymorphic expressions like
integer literals, this anti-pattern has occurred.

### Anti-Pattern 2: Trying to record types in the check function

`Bidir.check` validates an expression against a known expected type. It calls `synth`
internally for most cases (via subsumption). Recording in `check` would double-record many
nodes and the type would be the expected type, not the synthesized type. Record only in
`synth` cases. The synthesized type is always the authoritative one.

### Anti-Pattern 3: Serializing the full TypedModule for in-process use

If FunLangCompiler is in the same process (Model A), do not add a JSON serialization layer.
Passing the F# `TypedModule` record directly avoids the overhead and schema complexity.
Only serialize if cross-process consumption is genuinely required.

### Anti-Pattern 4: Recording types for elaborated (synthetic) nodes

`elaborateTypeclasses` creates new `LetDecl` nodes after the type-checking pass has
completed. These nodes have no entries in `TypeAnnotationMap` because they were never
visited by `Bidir.synth`. Do not attempt to add them retroactively. Instead, resolve
the types of these nodes from `TypeEnv` by name at the compiler side. For an `InstanceDecl`
method like `show` becoming `LetDecl("show", body, span)`, the compiler looks up `typeEnv["show"]`.

### Anti-Pattern 5: Putting ExportApi in Program.fs

`Program.fs` is the entry point and mixes CLI argument handling with pipeline orchestration.
`ExportApi.fs` must be a separate library module so FunLangCompiler can reference it without
pulling in the CLI argument parsing dependencies (Argu). Keep them separate.

---

## Scalability Considerations

| Concern | Current (interpreter) | After export |
|---------|----------------------|--------------|
| TypeAnnotationMap size | N/A | O(n) where n = expression count; typically small (<10K for most files) |
| Prelude loading overhead | ~50-100ms (one-time) | Same; caller must cache PreludeResult |
| Type checking cost | O(n * unification) | Unchanged; annotation recording is O(1) per node |
| FunLangCompiler coupling | None (separate repo) | Project reference; FunLangCompiler gains FunLang as dependency |

The annotation map holding `Map<Span, Type>` with O(n) entries for a typical source file
is not a memory concern at interpreter scale.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Annotation map approach | HIGH | Matches existing mutable-side-table patterns in Bidir.fs |
| Bidir.fs recording locations | HIGH | Each synth case has a clear final-type return point |
| Span uniqueness for real source | HIGH | All parsed nodes have unique (file, line, col) spans |
| Span uniqueness for synthetic nodes | MEDIUM | unknownSpan collision handled by filename guard |
| InstanceDecl method resolution via TypeEnv | HIGH | typeEnv contains all method names after type checking |
| Prelude caching requirement | HIGH | Already observed in Prelude.fs code |
| ExportApi as project reference | HIGH | Both repos are .NET; no serialization needed |
| CLI JSON flag (Phase 4) | MEDIUM | Requires designing Type DU JSON schema |

---

## Files Changed by Phase

### Phase 1 — TypeAnnotationMap
- `src/FunLang/TypeAnnotationMap.fs` (NEW)
- `src/FunLang/FunLang.fsproj` (add after Bidir.fs)

### Phase 2 — Bidir wiring
- `src/FunLang/Bidir.fs` (additive: ~40 record calls)
- `src/FunLang/TypeCheck.fs` (additive: clear() at entry point)

### Phase 3 — ExportApi
- `src/FunLang/ExportApi.fs` (NEW)
- `src/FunLang/FunLang.fsproj` (add after Prelude.fs)

### Phase 4 — CLI flag (optional)
- `src/FunLang/Cli.fs` (add --emit-typed-ast flag)
- `src/FunLang/Program.fs` (handle flag)

---

## Sources

- FunLang source: `Ast.fs`, `Type.fs`, `Bidir.fs`, `TypeCheck.fs`, `Elaborate.fs`,
  `Prelude.fs`, `Program.fs`, `FunLang.fsproj` (read 2026-04-02)
- Existing `ARCHITECTURE.md` (prior milestone: type classes) — patterns for environment
  threading and mutable side tables in Bidir.fs
