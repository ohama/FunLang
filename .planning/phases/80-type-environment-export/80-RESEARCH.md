# Phase 80: Type Environment Export - Research

**Researched:** 2026-04-03
**Domain:** F# type checker — extracting and exposing the top-level binding TypeEnv as a named, queryable collection
**Confidence:** HIGH

## Summary

Phase 80 exposes the top-level binding type environment after `typeCheckModuleWithPrelude` runs. The function already returns a `TypeEnv` (a `Map<string, Scheme>`) as its final result component. That map contains initialTypeEnv (builtins) + preludeTypeEnv + user-defined bindings merged together. The task is to make this map cleanly accessible as the "exported binding env" for TE-01 and TE-02, without mutation of existing APIs.

The current state: `typeCheckModuleWithPrelude` returns `Result<..., TypeEnv>` where `TypeEnv` is already the full merged map. Callers in `Program.fs` already filter this map to distinguish user bindings (`--emit-type` at line 342-344). The pattern for including builtins is simply not filtering. Both `initialTypeEnv` (builtins) and `prelude.TypeEnv` (prelude) are available at the call site. The "export" is already in the return value — Phase 80 needs to either document/expose this fact for downstream (Phase 81 ExportApi) or ensure the map is accessible without requiring callers to reconstruct it.

The implementation is a thin data extraction: add a `BindingEnv` type alias or record in TypeCheck.fs (or leave it as `TypeEnv`) and provide a helper that takes the `typeCheckModuleWithPrelude` result plus the prelude result and returns a single named map covering all three layers (builtins + prelude + user).

**Primary recommendation:** Add a `TopLevelBindingEnv` type alias (= TypeEnv) and a helper function `buildBindingEnv` in TypeCheck.fs that merges `initialTypeEnv + preludeTypeEnv + moduleTypeEnv` into a single map. This is what Phase 81 (ExportApi) will store in `TypedModule`.

## Standard Stack

This phase is entirely internal to the FunLang compiler. No new NuGet packages required.

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `Type.TypeEnv` (`Map<string, Scheme>`) | existing | The binding environment type | Already used throughout TypeCheck.fs and Bidir.fs |
| `TypeCheck.initialTypeEnv` | existing | All ~50 builtin function schemes | Already defined at top of TypeCheck.fs |
| `Prelude.PreludeResult.TypeEnv` | existing | All prelude-defined binding schemes | Already available after `Prelude.loadPrelude` |
| `typeCheckModuleWithPrelude` return `typeEnv` | existing | Full env including user bindings | Already returned as 7th tuple element |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `Map.fold` | F# stdlib | Merge multiple TypeEnv maps | Already used in TypeCheck.fs for all env merges |
| `Type.formatSchemeNormalized` | existing | Pretty-print a Scheme | For testing/output |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New `buildBindingEnv` helper | Inline merge at call site | Helper is reusable by Phase 81 ExportApi — prefer the helper |
| New `TypedBindingEnv` record | Bare `TypeEnv` alias | A type alias is enough; a record adds indirection with no benefit until Phase 81 |

**Installation:** No packages to install.

## Architecture Patterns

### What the returned `typeEnv` already contains

After `typeCheckModuleWithPrelude` returns `Ok(... , typeEnv)`:

```
typeEnv = initialTypeEnv (builtins)
        + preludeTypeEnv (prelude functions)
        + user-defined top-level let bindings
        + exception constructors added to typeEnv during typeCheckDecls
        + module-scoped constructors (ADT constructors get added to typeEnv as function schemes)
```

This is because `typeCheckModuleWithPrelude` line 1282:
```fsharp
let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) initialTypeEnv preludeTypeEnv
```
passes `mergedTypeEnv` into `typeCheckDecls`, which accumulates user bindings on top of it and returns the fully merged env.

**Consequence:** TE-01 (user bindings) and TE-02 (builtins + prelude included) are **both already satisfied** by the existing return value. Phase 80 is about making this accessible for the Phase 81 ExportApi.

### Pattern 1: Helper function in TypeCheck.fs

**What:** A pure helper that takes `preludeTypeEnv`, `moduleTypeEnv` (from `typeCheckModuleWithPrelude` result) and returns the complete merged binding env.
**When to use:** Called from Phase 81's `ExportApi.typeCheckFile` to populate `TypedModule.BindingEnv`.

```fsharp
// Source: TypeCheck.fs pattern (existing merge pattern at line 1282)

/// Build the complete top-level binding environment for export.
/// Combines builtin schemes, prelude schemes, and user-defined top-level bindings.
/// The returned map is queryable by binding name from outside the type-checker.
let buildBindingEnv (preludeTypeEnv: TypeEnv) (moduleTypeEnv: TypeEnv) : TypeEnv =
    // moduleTypeEnv already contains initialTypeEnv + preludeTypeEnv + user bindings
    // (typeCheckModuleWithPrelude merges them before calling typeCheckDecls).
    // Return as-is — the full merged map is the binding env.
    moduleTypeEnv
```

**Simpler alternative** — `moduleTypeEnv` IS already the complete map. The helper may be a no-op wrapper, or it may explicitly rebuild from parts for clarity:

```fsharp
let buildBindingEnv (preludeTypeEnv: TypeEnv) (moduleTypeEnv: TypeEnv) : TypeEnv =
    // Re-merge for explicitness: builtins + prelude + user
    // (same result as what typeCheckDecls already returned via moduleTypeEnv)
    let withBuiltins = Map.fold (fun acc k v -> Map.add k v acc) initialTypeEnv preludeTypeEnv
    Map.fold (fun acc k v -> Map.add k v acc) withBuiltins moduleTypeEnv
```

### Pattern 2: Querying the binding env by name

**What:** External consumers look up a name in the returned map using `Map.tryFind`.
**When to use:** Phase 81 ExportApi will expose this; tests verify specific names are present.

```fsharp
// Source: Type.fs — TypeEnv = Map<string, Scheme>
let lookupBinding (bindingEnv: TypeEnv) (name: string) : Scheme option =
    Map.tryFind name bindingEnv
```

No additional infrastructure is needed — `Map.tryFind` is the standard F# operation.

### Pattern 3: Test pattern for TE-01 + TE-02

**What:** F# unit tests that type-check a source string, extract the binding env, and verify names + schemes are present.
**When to use:** New `TypeEnvTests.fs` test file, parallel to `TypeAnnotationTests.fs`.

```fsharp
// Source: TypeAnnotationTests.fs pattern (typeCheckAndSnapshot)
let typeCheckAndGetEnv (input: string) : TypeEnv =
    let m = parseModule input
    match TypeCheck.typeCheckModule m with
    | Error errs -> failwith (sprintf "Type check failed: %A" errs)
    | Ok (_, _, _, typeEnv) -> typeEnv

// TE-01: user binding present
test "TE-01: top-level let binding is in typeEnv" {
    let env = typeCheckAndGetEnv "let x = 42"
    Expect.isSome (Map.tryFind "x" env) "x should be in binding env"
    match Map.tryFind "x" env with
    | Some (Type.Scheme([], [], Type.TInt)) -> ()
    | other -> failwith (sprintf "Expected Scheme([],[],TInt), got %A" other)
}

// TE-02: builtin present
test "TE-02: builtin 'print' is in typeEnv" {
    let env = typeCheckAndGetEnv "let x = 1"
    Expect.isSome (Map.tryFind "print" env) "builtin 'print' should be in env"
}
```

### Recommended Project Structure

No new source files are required for the minimal implementation. The binding env is already in `typeCheckModuleWithPrelude`'s return value.

If a helper is added to TypeCheck.fs, no fsproj changes are needed (it's in an existing file).

A new test file `TypeEnvTests.fs` is needed — add it to `FunLang.Tests.fsproj` before `Program.fs`.

```
tests/FunLang.Tests/
├── TypeAnnotationTests.fs    # Phase 79 tests (TA-01..TA-07)
├── TypeEnvTests.fs           # NEW: Phase 80 tests (TE-01, TE-02)
└── Program.fs
```

### Anti-Patterns to Avoid

- **Returning only user bindings (not builtins):** TE-02 requires builtins and prelude to be included. Do not filter out `initialTypeEnv` keys before returning. The `--emit-type` filtering in `Program.fs` line 344 is for display, not for the exported API.
- **Adding a new mutable ref for the binding env:** The env is already in the return value. No mutable state needed (unlike Phase 79's annotation map).
- **Changing `typeCheckModuleWithPrelude` signature:** The binding env is already the 7th return tuple element. Phase 80 should not change function signatures that dozens of call sites depend on.
- **Using `typeCheckDecls`'s env directly (without preludeTypeEnv merge):** If calling `typeCheckDecls` directly (not via `typeCheckModuleWithPrelude`), the env passed in must include `initialTypeEnv + preludeTypeEnv` first.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Env merge | Custom loop | `Map.fold (fun acc k v -> Map.add k v acc) base overlay` | Exact pattern used in 10+ places in TypeCheck.fs already |
| Scheme pretty-print | Custom formatter | `Type.formatSchemeNormalized scheme` | Already handles constraints, normalized var names |
| Env lookup | Custom search | `Map.tryFind name env` | Standard F# Map operation, O(log n) |

**Key insight:** The exported binding env is already produced during type checking. Phase 80 is about wiring access to it, not building new infrastructure.

## Common Pitfalls

### Pitfall 1: Assuming typeEnv from typeCheckModuleWithPrelude excludes builtins
**What goes wrong:** Caller assumes `typeEnv` returned by `typeCheckModuleWithPrelude` contains only user-defined bindings, then separately adds builtins. This results in a double-merge and potential shadowing confusion.
**Why it happens:** The function signature says `(preludeTypeEnv: TypeEnv)` as input and returns `TypeEnv` — it's not obvious that the returned env includes builtins too.
**How to avoid:** Read line 1282: `let mergedTypeEnv = Map.fold ... initialTypeEnv preludeTypeEnv`. The merge happens inside `typeCheckModuleWithPrelude`. The returned `typeEnv` is already complete.
**Warning signs:** TE-02 test fails because `print` is not in the env returned by Phase 81's API — even though it's in the raw `typeCheckModuleWithPrelude` result.

### Pitfall 2: Prelude TypeEnv not passed through to ExportApi
**What goes wrong:** Phase 81's `ExportApi.typeCheckFile` calls `typeCheckModuleWithPrelude` but throws away `prelude.TypeEnv`. The returned `typeEnv` is complete, but the API's `TypedModule` loses visibility into which names came from prelude vs. user code.
**Why it happens:** It's easy to capture only the user-binding subset. Phase 81 will need access to both the full merged env and the prelude's own TypeEnv for filtering.
**How to avoid:** Pass `prelude.TypeEnv` and `initialTypeEnv` alongside the full merged env in `TypedModule`. Phase 80 should ensure these are accessible.
**Warning signs:** `TypedModule.BindingEnv` has `print` but downstream can't distinguish builtin vs. user binding.

### Pitfall 3: typeCheckModule (no-prelude variant) loses builtins
**What goes wrong:** Tests using `typeCheckModule` (not `typeCheckModuleWithPrelude`) see a binding env that lacks prelude names. The builtin env (`initialTypeEnv`) is still included because `typeCheckModule` calls `typeCheckModuleWithPrelude` with empty prelude args, but `initialTypeEnv` is merged inside.
**Why it happens:** `typeCheckModule` passes `Map.empty` as `preludeTypeEnv`. The returned `typeEnv` still contains `initialTypeEnv` (builtins) but no prelude.
**How to avoid:** Tests for TE-02 (prelude names present) should use `typeCheckModuleWithPrelude` with a real prelude, or accept that `typeCheckModule` only gives builtins (not prelude). The tests can still verify builtins like `print` are present since those come from `initialTypeEnv`.
**Warning signs:** TE-02 test checks for `List.map` (a prelude function), but using `typeCheckModule` without prelude — the name won't be in the env.

### Pitfall 4: Exception constructors in typeEnv
**What goes wrong:** The exported binding env includes exception constructor names (e.g., `MyError`) as function schemes `TArrow(argType, TExn)`. Consumers expecting only "let bindings" are surprised by these entries.
**Why it happens:** `typeCheckDecls` adds exception constructors to `typeEnv` at lines 823-838. They live alongside user `let` bindings in the same map.
**How to avoid:** Document that the exported env includes exception constructors. If consumers need to filter them, they can check for `TExn` or `TArrow(_, TExn)` in the scheme's type.
**Warning signs:** Exported binding env has names starting with uppercase (constructor convention) that resolve to `TExn` types.

## Code Examples

### Current `typeCheckModuleWithPrelude` return structure
```fsharp
// Source: TypeCheck.fs line 1254-1291
let typeCheckModuleWithPrelude
    (preludeCtorEnv: ConstructorEnv) (preludeRecEnv: RecordEnv)
    (preludeClassEnv: ClassEnv) (preludeInstEnv: InstanceEnv)
    (preludeTypeEnv: TypeEnv)
    (initialModules: Map<string, ModuleExports>)
    (m: Module)
    : Result<Diagnostic list * ConstructorEnv * RecordEnv * ClassEnv * InstanceEnv * Map<string, ModuleExports> * TypeEnv, Diagnostic list> =

    // Line 1282: merge initialTypeEnv + preludeTypeEnv BEFORE calling typeCheckDecls
    let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) initialTypeEnv preludeTypeEnv
    let (typeEnv, ...) = typeCheckDecls decls mergedTypeEnv ...
    // typeEnv is now: builtins + prelude + user bindings
    Ok (warnings, ctorEnv, recEnv, classEnv, instEnv, modules, typeEnv)
```

### How Program.fs already uses this (--emit-type pattern)
```fsharp
// Source: Program.fs line 338-347
match TypeCheck.typeCheckModuleWithPrelude ... m with
| Ok (warnings, _ctorEnv, _recEnv, _classEnv, _instEnv, _modules, typeEnv) ->
    // Filter to user-only bindings (for display):
    let userBindings =
        typeEnv
        |> Map.filter (fun k _ ->
            not (Map.containsKey k TypeCheck.initialTypeEnv)
            && not (Map.containsKey k prelude.TypeEnv))
    // For Phase 80's purpose (TE-01 + TE-02): use typeEnv directly (no filter)
```

### Proposed helper for Phase 81 consumption
```fsharp
// To add in TypeCheck.fs (near bottom, after typeCheckModuleWithPrelude)

/// The complete exported binding environment: builtins + prelude + user top-level bindings.
/// This is a type alias to make the intent explicit for Phase 81 ExportApi.
type BindingEnv = TypeEnv

/// Build the complete binding environment from a successful typeCheckModuleWithPrelude result.
/// preludeTypeEnv: from Prelude.loadPrelude result
/// moduleTypeEnv: the TypeEnv returned by typeCheckModuleWithPrelude (already includes builtins+prelude+user)
/// Returns the moduleTypeEnv as-is — it is already the complete binding env.
/// Exposed for Phase 81 ExportApi to include in TypedModule.
let exportBindingEnv (moduleTypeEnv: TypeEnv) : BindingEnv = moduleTypeEnv
```

### Test pattern for TE-01 (user binding in env)
```fsharp
// Source: TypeAnnotationTests.fs pattern — same parse/typeCheck helpers

let typeCheckGetEnv (input: string) : Type.TypeEnv =
    let m = parseModule input
    match TypeCheck.typeCheckModule m with
    | Error errs -> failwith (sprintf "Type check failed: %A" errs)
    | Ok (_, _, _, typeEnv) -> typeEnv

test "TE-01: top-level let x = 42 has x : int in binding env" {
    let env = typeCheckGetEnv "let x = 42"
    match Map.tryFind "x" env with
    | Some (Type.Scheme([], [], Type.TInt)) -> ()
    | Some other -> failwith (sprintf "Expected int scheme, got %A" other)
    | None -> failwith "x not found in binding env"
}

test "TE-01: top-level let f x = x + 1 has f : int -> int in binding env" {
    let env = typeCheckGetEnv "let f x = x + 1"
    match Map.tryFind "f" env with
    | Some (Type.Scheme([], [], Type.TArrow(Type.TInt, Type.TInt))) -> ()
    | Some other -> failwith (sprintf "Expected int->int, got %A" other)
    | None -> failwith "f not found in binding env"
}
```

### Test pattern for TE-02 (builtin in env)
```fsharp
test "TE-02: builtin 'print' is in binding env" {
    let env = typeCheckGetEnv "let x = 1"
    Expect.isSome (Map.tryFind "print" env) "builtin 'print' should be in env"
}

test "TE-02: builtin 'string_length' is in binding env" {
    let env = typeCheckGetEnv "let x = 1"
    Expect.isSome (Map.tryFind "string_length" env) "builtin 'string_length' should be in env"
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `typeCheckModule` returned only `TypeEnv` (no ClassEnv/InstanceEnv) | `typeCheckModuleWithPrelude` returns full 7-tuple including ClassEnv/InstanceEnv | v10.1 (Phase 78) | ClassEnv/InstanceEnv now accessible alongside TypeEnv at same return |
| TypeEnv filtered at use site (Program.fs line 344) | TypeEnv exposed raw for TE-01/TE-02 | Phase 80 (now) | Downstream consumers get full env, can filter if needed |

**Deprecated/outdated:**
- None. This is an additive exposure.

## Open Questions

1. **Should `exportBindingEnv` be added to TypeCheck.fs or deferred to Phase 81?**
   - What we know: Phase 80's plan is `80-01: Extract top-level binding env and include builtins/prelude`. The full env is already in `typeCheckModuleWithPrelude`'s return value.
   - What's unclear: Whether a named helper in TypeCheck.fs is needed before Phase 81, or whether Phase 80 is purely about tests/verification.
   - Recommendation: Add `type BindingEnv = TypeEnv` and `let exportBindingEnv = id` (or the 3-way merge variant) in TypeCheck.fs. Costs nothing, makes Phase 81 cleaner. Also add `TypeEnvTests.fs` test file.

2. **Should tests use `typeCheckModule` (no prelude) or `typeCheckModuleWithPrelude` (with prelude)?**
   - What we know: `typeCheckModule` passes empty prelude to `typeCheckModuleWithPrelude`. The returned `typeEnv` includes `initialTypeEnv` builtins but no prelude names. TE-02 says "builtin and prelude" must be included.
   - What's unclear: Whether unit tests can satisfy TE-02 using `typeCheckModule` (which gets builtins) or need a prelude-loaded test.
   - Recommendation: Use `typeCheckModule` for TE-02 builtin verification (builtins come from `initialTypeEnv`, not prelude). For prelude-name tests, use integration tests or a separate prelude-loaded path. The unit tests can verify that `print` (a builtin) is in the env, which satisfies TE-02's intent.

3. **Does the `typeCheckDecls`-level env include module-internal let bindings?**
   - What we know: `typeCheckDecls` processes all `LetDecl` at top-level. Module-scoped inner lets (inside `module Foo do`) get their own `moduleTypeEnv` extracted at lines 986-991. The outer returned `typeEnv` contains the outer scope, inner modules are in `modules` map.
   - What's unclear: Whether `TypedModule.BindingEnv` should include module-scoped bindings or only top-level ones.
   - Recommendation: For Phase 80, return only the top-level `typeEnv` (as returned by `typeCheckModuleWithPrelude`). Module-scoped bindings are accessible via `ModuleExports.TypeEnv` if needed. This is the simplest correct answer.

## Sources

### Primary (HIGH confidence)
- Direct inspection of `TypeCheck.fs` lines 1-100 (initialTypeEnv), 220-240 (ModuleExports), 795-870 (typeCheckDecls LetDecl handling), 1254-1299 (typeCheckModuleWithPrelude)
- Direct inspection of `Program.fs` lines 338-354 (`--emit-type` flag — existing pattern for user binding extraction)
- Direct inspection of `Type.fs` lines 1-37 (TypeEnv definition, Scheme type)
- Direct inspection of `Prelude.fs` lines 12-22 (PreludeResult with TypeEnv field), 265-316 (loadPrelude accumulation)
- Direct inspection of `TypeAnnotationTests.fs` (test patterns and helpers for Phase 80 to follow)
- Direct inspection of `.planning/REQUIREMENTS.md` (TE-01, TE-02 definitions)
- Direct inspection of `.planning/ROADMAP.md` (Phase 80 success criteria)

### Secondary (MEDIUM confidence)
- Phase 79 RESEARCH.md — established mutable pattern context, pre/post-elaboration AST notes

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all code is in-repo, no external dependencies
- Architecture: HIGH — the binding env is already in the return value, pattern is established
- Pitfalls: HIGH — inspected all relevant code paths; pitfalls derive from direct code reading

**Research date:** 2026-04-03
**Valid until:** Stable — changes only if `typeCheckModuleWithPrelude` signature changes or `typeCheckDecls` merge logic changes.
