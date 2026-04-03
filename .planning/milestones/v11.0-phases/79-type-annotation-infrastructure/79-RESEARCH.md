# Phase 79: Type Annotation Infrastructure - Research

**Researched:** 2026-04-03
**Domain:** F# bidirectional type checker instrumentation — Dictionary<Span, Type> annotation map wired into Bidir.synth
**Confidence:** HIGH

## Summary

Phase 79 adds a per-expression type annotation map to FunLang's bidirectional type checker. The map stores the post-substitution inferred type for every Expr node visited by `Bidir.synth`. It follows the exact pattern already used in Bidir.fs for other mutable state: a module-level `mutable` ref declared at the top of Bidir.fs, reset at the entry point in TypeCheck.fs, and populated by side-effect during synthesis.

The primary technical challenge is completeness: all ~40 Expr variants in synth must record an annotation entry. Because `check` falls through to `synth` via subsumption for most cases, and because GADT branches use check mode, annotation recording must be placed at the correct return points in both modes. The secondary challenge is the pre/post-elaboration AST mismatch: `elaborateTypeclasses` rewrites the AST before Bidir runs, so spans from the original AST may differ from what synth actually sees.

The implementation is purely additive. No existing call sites, return types, or test behavior changes. TypeAnnotationMap is a new module (new file) inserted into the project between Ast.fs/Type.fs and Bidir.fs in the compilation order.

**Primary recommendation:** Declare `annotationMap : Dictionary<Ast.Span, Type.Type>` as a mutable ref in Bidir.fs (same pattern as `mutableVars`), define a thin TypeAnnotationMap helper module for external consumers, and record `spanOf expr |> annotationMap.[...] <- appliedTy` at every synth return point.

## Standard Stack

This phase is internal to the FunLang compiler. No new NuGet packages are required.

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `System.Collections.Generic.Dictionary<Span, Type>` | .NET 10 | O(1) span→type lookup | Already used in the codebase for mutable collections (HashtableValue uses Dictionary) |
| F# `mutable` module-level ref | F# 8 | Shared mutable state across synth calls | Existing pattern in Bidir.fs (`mutableVars`, `pendingConstraints`, `currentClassEnv`) |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `Ast.spanOf` | existing | Extract span from any Expr | Used as the dictionary key |
| `Type.apply` | existing | Apply substitution to type | Applied before recording so stored types are fully resolved |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Dictionary<Span, Type>` | `Map<Span, Type>` (F# immutable) | Dictionary is O(1) write, avoids allocation on every synth call. Map would require threading through all synth signatures — major refactor. |
| Module-level mutable | Passing map as parameter through synth | Parameter threading would require changing every synth/check call site — ~40 match arms, all helpers. Not worth it for an additive feature. |
| Recording in synth only | Recording in both synth and check | check mode's fallback calls synth anyway (subsumption). GADT branches use check without calling synth for the outer Match. Must record in check's GADT Match arm. |

## Architecture Patterns

### Recommended Project Structure

New file: `src/FunLang/TypeAnnotationMap.fs`

Inserted in fsproj between Elaborate.fs and Diagnostic.fs (position 2.6), or after Diagnostic.fs. Must come before Bidir.fs. Bidir.fs opens it and uses its helpers. TypeCheck.fs resets it at entry.

```
src/FunLang/
├── Ast.fs                    # Span type lives here
├── Type.fs                   # Type type lives here
├── Elaborate.fs
├── TypeAnnotationMap.fs      # NEW: module with reset/record/lookup helpers
├── Diagnostic.fs
├── Unify.fs
├── Infer.fs
├── Bidir.fs                  # mutable annotationMap ref here, or use TypeAnnotationMap directly
├── TypeCheck.fs              # resets map at typeCheckModuleWithPrelude entry
```

### Pattern 1: Mutable Module-Level State (existing pattern)

**What:** A `mutable` binding at module scope in Bidir.fs holds the annotation map. TypeCheck.fs resets it at each top-level entry point.
**When to use:** When state must accumulate across many recursive calls without threading through every function signature.

**Example from existing code:**
```fsharp
// Bidir.fs — existing patterns to follow exactly:
let mutable mutableVars : Set<string> = Set.empty
let mutable pendingConstraints : Constraint list = []
let mutable currentClassEnv : ClassEnv = Map.empty
let mutable currentInstEnv : InstanceEnv = Map.empty
let mutable accumulatedErrors : Diagnostic.TypeError list = []
```

**Proposed addition in Bidir.fs:**
```fsharp
// Bidir.fs — new annotation map (same pattern)
let mutable annotationMap : System.Collections.Generic.Dictionary<Ast.Span, Type> =
    System.Collections.Generic.Dictionary<Ast.Span, Type>()
```

**Reset in TypeCheck.fs** (at `typeCheckModuleWithPrelude` entry, alongside existing resets):
```fsharp
// TypeCheck.fs — existing resets at lines 1262-1266:
Bidir.mutableVars <- Set.empty
Bidir.currentClassEnv <- preludeClassEnv
Bidir.currentInstEnv <- preludeInstEnv
Bidir.pendingConstraints <- []
// NEW:
Bidir.annotationMap <- System.Collections.Generic.Dictionary<Ast.Span, Type>()
```

### Pattern 2: Recording at synth Return Points

**What:** At every `(s, ty)` return in `synth`, record `annotationMap.[spanOf expr] <- apply s ty`.
**When to use:** Every match arm in `synth` that returns `(Subst * Type)`.

**Example for a simple case:**
```fsharp
// Before (existing):
| Number (_, _) -> (empty, TInt)

// After (recording):
| Number (_, span) ->
    annotationMap.[span] <- TInt
    (empty, TInt)
```

**For recursive cases**, record the final resolved type after substitution:
```fsharp
// Before:
| App (func, arg, span) ->
    ...
    (compose s3 (compose s2 s1), apply s3 resultTy)

// After:
| App (func, arg, span) ->
    ...
    let finalTy = apply s3 resultTy
    annotationMap.[span] <- apply (compose s3 (compose s2 s1)) finalTy
    (compose s3 (compose s2 s1), finalTy)
```

### Pattern 3: TypeAnnotationMap Helper Module

**What:** A thin module wrapping the Dictionary for external consumers (LSP, IDE tooling).
**Location:** `TypeAnnotationMap.fs`

```fsharp
module TypeAnnotationMap

open Ast
open Type

/// Record a type for an expression span
let record (map: System.Collections.Generic.Dictionary<Span, Type>) (span: Span) (ty: Type) =
    map.[span] <- ty

/// Look up the type for a span (returns None if not found)
let tryFind (map: System.Collections.Generic.Dictionary<Span, Type>) (span: Span) : Type option =
    match map.TryGetValue(span) with
    | true, ty -> Some ty
    | _ -> None

/// Get all annotations as a sequence
let toSeq (map: System.Collections.Generic.Dictionary<Span, Type>) =
    map |> Seq.map (fun kv -> (kv.Key, kv.Value))
```

### Anti-Patterns to Avoid

- **Recording unsubstituted types:** Always `apply finalSubst ty` before recording. A `TVar n` in the map is useless for consumers.
- **Recording in `check` fallback only:** The subsumption fallback in `check` calls `synth`, so the synth arm already records. But the outer `Match` expr in GADT check mode needs its own recording since synth delegates to check.
- **Skipping `EmptyList`:** It has no inner expression but does have a span. Must record `TList (freshVar())` at its span. The fresh var will be resolved after unification if there's a context; if not, it will remain a TVar.
- **Span collision via unknownSpan:** Built-in/synthetic expressions use `unknownSpan`. The dictionary key collision is harmless — last write wins, and unknownSpan types are not meaningful for tooling.
- **Recording before substitution is composed:** In chains like `compose s3 (compose s2 s1)`, apply the full composed substitution, not just `s3`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Span equality/hashing for dictionary key | Custom IEqualityComparer | F# record structural equality | `Span` is a plain F# record — .NET Dictionary uses structural equality automatically via F# record's auto-generated `Equals`/`GetHashCode` |
| Substitution application | Custom traversal | `Type.apply s ty` | Already handles TVar chains, TArrow, TTuple, TData, etc. |
| Span extraction | Custom match | `Ast.spanOf expr` | Already handles all ~40 Expr variants, tested |

**Key insight:** The entire infrastructure already exists. This phase assembles existing parts (`Dictionary`, `spanOf`, `apply`, mutable pattern) into a thin wrapper.

## Common Pitfalls

### Pitfall 1: Missing Expr Variants in synth
**What goes wrong:** One or more synth match arms don't record an annotation. Success criterion 3 ("all ~40 Expr node variants") fails.
**Why it happens:** synth is a large `match expr with` block (~40 arms). Easy to miss one when adding recording logic, especially for less common nodes like `StringSliceExpr`, `ListCompExpr`, `Range`, or `ForInExpr`.
**How to avoid:** After wiring, add an assertion test that runs synth on an expression containing each variant and checks the map has an entry for every span. Alternatively, count distinct spans in map after running the full test suite and compare to expected node count.
**Warning signs:** Test passes but map entry is absent for a specific variant when manually checking.

### Pitfall 2: Recording Unsubstituted Type Variables
**What goes wrong:** Map entry for an expression is `TVar 42` instead of `TInt`. Downstream consumers get unresolved types.
**Why it happens:** Recording `ty` directly from `synth`'s return before composing and applying the full substitution chain.
**How to avoid:** Always record `apply finalSubst ty` where `finalSubst` is the fully composed substitution at the return point. For simple literals this is trivially `TInt`, but for recursive expressions the substitution must be applied.
**Warning signs:** Map has `TVar` entries for expressions that should have concrete types (e.g., `Number`'s parent `Add` expression shows `TVar`).

### Pitfall 3: GADT Match Not Recorded
**What goes wrong:** The outer `Match` node has no annotation entry when the match involves GADT constructors.
**Why it happens:** In synth, GADT matches delegate to `check` mode: `let s = check ... env expr freshTy` and return `(s, apply s freshTy)`. The synth arm for Match records correctly. But if recording is added only in the `else` (non-GADT) branch of the Match arm in synth, the GADT case is missed.
**How to avoid:** Record at the end of both branches of the GADT/non-GADT if-else in the Match synth arm, before the final return.
**Warning signs:** Expressions like `match expr with | IntVal n -> ...` (GADT match) have no annotation entry.

### Pitfall 4: Pre/Post-Elaboration AST Span Mismatch
**What goes wrong:** The original source AST has spans from parsing, but `elaborateTypeclasses` rewrites the AST before Bidir.synth runs. Synthetic nodes inserted during elaboration carry `unknownSpan`.
**Why it happens:** The context notes that "elaborateTypeclasses rewrites AST before Bidir runs." Elaborated typeclass method calls (desugared dictionaries) will have spans pointing to synthetic locations.
**How to avoid:** Accept that synthetic nodes with `unknownSpan` will overwrite each other in the dictionary — this is harmless. Real source spans from user code will be correctly recorded. Document that `unknownSpan` entries are not meaningful.
**Warning signs:** No warning — this is expected behavior. Just document it.

### Pitfall 5: File Ordering in fsproj
**What goes wrong:** Build fails with "TypeAnnotationMap is not defined in this context."
**Why it happens:** F# requires files to be listed in dependency order in the fsproj. TypeAnnotationMap.fs must come before Bidir.fs.
**How to avoid:** Insert TypeAnnotationMap.fs into fsproj between Elaborate.fs (position 2.5) and Diagnostic.fs (position 3), i.e., position 2.6. Bidir.fs is at position 5.6.
**Warning signs:** Compiler error at build time (not test time).

### Pitfall 6: Dictionary Not Reset Between Test Runs
**What goes wrong:** Test B sees annotation entries from Test A.
**Why it happens:** The mutable dictionary persists at module scope. `typeCheckModuleWithPrelude` resets it, but tests calling lower-level entry points (e.g., `typecheckExprWithPrelude`) may not reset it.
**How to avoid:** Ensure ALL TypeCheck entry points that call Bidir.synth reset `Bidir.annotationMap` at their start. Check `typecheck`, `typecheckWithDiagnostic`, `typecheckExprWithPrelude`, and `synthTop`/`synthTopWithCtors`.
**Warning signs:** Flaky tests that pass individually but fail when run together.

## Code Examples

### TypeAnnotationMap module

```fsharp
// src/FunLang/TypeAnnotationMap.fs
module TypeAnnotationMap

open Ast
open Type

/// Create a fresh annotation map
let create () : System.Collections.Generic.Dictionary<Span, Type> =
    System.Collections.Generic.Dictionary<Span, Type>()

/// Record a type annotation for the given span
let record (map: System.Collections.Generic.Dictionary<Span, Type>) (span: Span) (ty: Type) =
    if span <> Ast.unknownSpan then
        map.[span] <- ty

/// Look up the inferred type for a span
let tryFind (map: System.Collections.Generic.Dictionary<Span, Type>) (span: Span) : Type option =
    match map.TryGetValue(span) with
    | true, ty -> Some ty
    | _ -> None
```

### Bidir.fs mutable declaration

```fsharp
// Add after existing mutable declarations in Bidir.fs (around line 25):
/// Per-expression type annotation map populated during synth (Phase 79)
/// Keys are Span values from Ast.spanOf; values are fully substituted types.
/// Reset at typeCheckModuleWithPrelude entry (same pattern as mutableVars).
let mutable annotationMap : System.Collections.Generic.Dictionary<Ast.Span, Type> =
    TypeAnnotationMap.create()
```

### Recording in a synth arm (literal example)

```fsharp
// Number case — simplest: no substitution needed
| Number (_, span) ->
    annotationMap.[span] <- TInt
    (empty, TInt)
```

### Recording in a synth arm (recursive example)

```fsharp
// App case — must apply final substitution
| App (func, arg, span) ->
    let s1, funcTy = synth ctorEnv recEnv (InAppFun span :: ctx) env func
    let s2, argTy = synth ctorEnv recEnv (InAppArg span :: ctx) (applyEnv s1 env) arg
    let appliedFuncTy = apply s2 funcTy
    match appliedFuncTy with
    | TInt | TBool | ... ->
        raise (TypeException { ... })
    | _ ->
        let resultTy = freshVar()
        let s3 = unifyWithContext ctx [] span appliedFuncTy (TArrow (argTy, resultTy))
        let finalS = compose s3 (compose s2 s1)
        let finalTy = apply s3 resultTy
        annotationMap.[span] <- finalTy   // fully resolved
        (finalS, finalTy)
```

### TypeCheck.fs reset

```fsharp
// typeCheckModuleWithPrelude — add to existing reset block (around line 1262):
Bidir.mutableVars <- Set.empty
Bidir.currentClassEnv <- preludeClassEnv
Bidir.currentInstEnv <- preludeInstEnv
Bidir.pendingConstraints <- []
Bidir.annotationMap <- TypeAnnotationMap.create()   // NEW
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No annotation map | Dictionary<Span, Type> mutable | Phase 79 (now) | Enables IDE hover types, LSP, typed AST export |
| Pending constraint tracking in `pendingConstraints` | Same pattern reused for annotation map | Phase 72 established pattern | No new architecture needed |

**Deprecated/outdated:**
- None. This is purely additive.

## Open Questions

1. **Should TypeAnnotationMap.fs be its own file or inline in Bidir.fs?**
   - What we know: The existing pattern (`mutableVars`, `pendingConstraints`) inlines the mutable ref directly in Bidir.fs without a helper module.
   - What's unclear: Whether external consumers (future LSP, typed AST export) need a stable module interface.
   - Recommendation: Create a minimal `TypeAnnotationMap.fs` with `create`/`record`/`tryFind`. This is cleaner than inlining and gives future phases a stable import point. The mutable `annotationMap` ref still lives in Bidir.fs.

2. **How to verify all ~40 variants are covered?**
   - What we know: Ast.fs `spanOf` already covers all variants in its own match (lines 310-335). That match is the canonical list.
   - What's unclear: Whether there are any Expr variants that synth never reaches (e.g., variants only produced during elaboration).
   - Recommendation: Cross-reference synth's match arms against `Ast.spanOf`'s match arms. Any variant in `spanOf` not in `synth` is a gap. The known set: all 40 from `spanOf` should appear in `synth`. Add a compile-time warning by using `| _ ->` with a `failwith` guard during development.

3. **Should `check` record annotations independently?**
   - What we know: `check` has a fallback arm `| _ -> let s, actual = synth ... in ...` that calls synth. synth records the annotation. For GADT Match in check mode, the outer Match node returns from check without calling synth for the Match node itself.
   - What's unclear: Whether GADT Match's check arm needs to record the outer Match span separately.
   - Recommendation: Add explicit recording at the end of the GADT Match check arm. The inner branch bodies call synth/check which record their own spans. The outer Match node's span must be recorded separately in the check arm.

## Sources

### Primary (HIGH confidence)
- Direct source code inspection of `/Users/ohama/vibe-coding/FunLang/src/FunLang/Bidir.fs` (all 1119 lines)
- Direct source code inspection of `/Users/ohama/vibe-coding/FunLang/src/FunLang/TypeCheck.fs` (lines 1-1297)
- Direct source code inspection of `/Users/ohama/vibe-coding/FunLang/src/FunLang/Ast.fs` (Span definition, spanOf, all Expr variants)
- Direct source code inspection of `/Users/ohama/vibe-coding/FunLang/src/FunLang/Type.fs` (Type, Subst, apply)
- Direct source code inspection of `/Users/ohama/vibe-coding/FunLang/src/FunLang/FunLang.fsproj` (file ordering)

### Secondary (MEDIUM confidence)
- Phase context requirements (TA-01, TA-02) from the phase description
- Prior decisions from phase context: mutable ref pattern established in Phase 72, Span threaded through all synth

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all code is local, no external dependencies
- Architecture: HIGH — existing `mutableVars`/`pendingConstraints` pattern is directly reusable
- Pitfalls: HIGH — inspected all 40 Expr variants in synth, identified GADT check mode gap, span collision behavior, and fsproj ordering

**Research date:** 2026-04-03
**Valid until:** Stable — internal compiler module, no external dependencies. Valid as long as Bidir.fs architecture doesn't change.
