# Phase 36: Bug Fixes - Research

**Researched:** 2026-03-25
**Domain:** F# interpreter — module qualified access propagation, parser grammar
**Confidence:** HIGH

## Summary

Phase 36 fixes three independent bugs: two in the module/type-check pipeline (MOD-01, MOD-02) and one in the parser grammar (PAR-01). All three bugs are well-understood from codebase inspection with no ambiguity about root causes or fixes.

**MOD-01** is a module propagation gap in `Prelude.fs/loadAndTypeCheckFileImpl`. When a `.fun` file is imported via `open "file.fun"`, the `FileImportDecl` handler calls `typeCheckModuleWithPrelude` and the returned `_mods` (the `Map<string, ModuleExports>`) is discarded. This means any `module Foo = ...` blocks defined inside the imported file are never added to the caller's `modules` map. Later, when `rewriteModuleAccess` or `collectModuleRefs` run, they look up modules by name in `mods` and find nothing, so `Foo.bar` expressions pass through unrewritten. `Bidir.synth` then sees a raw `FieldAccess` node and raises E0313 because `Foo` resolves to nothing in the type env, which gets typed as a non-record type.

**MOD-02** is a design-level gap: Prelude files (`Prelude/List.fun`, etc.) define functions at the top level with no `module List = ...` wrapper. The `loadPrelude` function discards `_modules` from `typeCheckModuleWithPrelude` and discards `_moduleEnv` from `evalModuleDecls`. `PreludeResult` carries no module map. When `List.length` is written in user code, there is no `List` entry in either the type-check `modules` map or the eval `moduleEnv`, so it fails at both type-check and eval. Two possible fixes exist: (a) wrap Prelude file contents in `module <Filename> = ...` inside the files themselves, or (b) build a virtual module from each prelude file's bindings using the stem of the filename. Option (a) is simpler and lower-risk.

**PAR-01** is a grammar ambiguity/restriction. The `TRY Expr WITH MatchClauses` rule requires `MatchClauses`, which always starts with a `PIPE` token (`| pattern -> expr`). `try failwith "boom" with e -> "caught"` has no `|`, so `MatchClauses` fails to reduce. The fix is to add a `TryWithClauses` nonterminal that also accepts the `IDENT ARROW Expr` form (bare single clause without a leading pipe), or to add an additional `Expr` rule `| TRY Expr WITH Pattern ARROW Expr` for the single-clause case.

**Primary recommendation:** Fix each bug in isolation; none interact. MOD-01 and MOD-02 both require threading the `modules`/`moduleEnv` maps through the import pipeline. PAR-01 is a self-contained grammar change.

## Standard Stack

No new libraries. All fixes are internal to existing F# source files.

### Files Under Change
| File | Purpose | Bug(s) Addressed |
|------|---------|-----------------|
| `src/LangThree/Prelude.fs` | File import TC + eval delegates; loadPrelude | MOD-01, MOD-02 |
| `src/LangThree/TypeCheck.fs` | `fileImportTypeChecker` delegate signature | MOD-01 |
| `src/LangThree/Eval.fs` | `fileImportEvaluator` delegate signature | MOD-01 |
| `src/LangThree/Program.fs` | Run pipeline wires TC and eval results | MOD-01 |
| `src/LangThree/Parser.fsy` | TRY-WITH grammar rule | PAR-01 |
| `Prelude/List.fun` (and others) | Prelude source files | MOD-02 |
| `tests/LangThree.Tests/ModuleTests.fs` | New regression tests | all |
| `tests/LangThree.Tests/ExceptionTests.fs` | New PAR-01 regression test | PAR-01 |

## Architecture Patterns

### MOD-01: Propagating imported module maps

The `fileImportTypeChecker` delegate currently has signature:
```fsharp
(string -> ConstructorEnv -> RecordEnv -> TypeEnv -> TypeEnv * ConstructorEnv * RecordEnv)
```
It returns a triple. It must be extended to also return the accumulated `Map<string, ModuleExports>`, making the signature:
```fsharp
(string -> ConstructorEnv -> RecordEnv -> TypeEnv -> Map<string, ModuleExports>
    -> TypeEnv * ConstructorEnv * RecordEnv * Map<string, ModuleExports>)
```

The call site in `TypeCheck.typeCheckDecls` at the `FileImportDecl` arm currently passes `(env', cEnv', rEnv', mods, warns)` — it must merge the returned `fileMods` into `mods` before returning.

Similarly, `fileImportEvaluator` currently returns `(Env * Map<string, ModuleValueEnv>)` which already includes module env — so the Eval side already passes `modEnv` through correctly via `fileImportEvaluator`. The bug on the eval side is that `loadAndEvalFileImpl` already returns `mergedModEnv`. So the eval pipeline is actually correct for runtime; the problem is the type-check phase only.

**Verify:** Check `TypeCheck.fileImportTypeChecker` call in `typeCheckDecls`:
```fsharp
| FileImportDecl(path, _span) ->
    let resolvedPath = resolveImportPath path currentTypeCheckingFile
    let (env', cEnv', rEnv') = fileImportTypeChecker resolvedPath cEnv rEnv env
    (env', cEnv', rEnv', mods, warns)   // <-- mods unchanged: bug here
```
The fix: `fileImportTypeChecker` must also accept `mods` and return `fileMods`, which gets merged into `mods`.

**Cascading changes from delegate signature change:**
1. `TypeCheck.fs`: update delegate type declaration
2. `Prelude.fs/loadAndTypeCheckFileImpl`: add `mods` param, pass it to `typeCheckModuleWithPrelude`, return `fileMods`
3. `TypeCheck.fs/typeCheckDecls` `FileImportDecl` arm: pass `mods`, unpack returned `fileMods`, merge
4. `TypeCheck.fs/typeCheckModuleWithPrelude` currently passes `Map.empty` as initial modules. For imported files, the caller's `mods` should be passed in so that the imported file also sees already-known modules.
5. `Program.fs` does not directly call `fileImportTypeChecker` — it calls `typeCheckModuleWithPrelude`. It does need to thread any resulting modules for the run pipeline, but `_modules` is already returned from `typeCheckModuleWithPrelude` — verify it is not needed at eval call sites.

### MOD-02: Prelude module wrapping

**Approach A (preferred):** Wrap prelude file contents with `module <Stem> = ...` inside the `.fun` files.

`Prelude/List.fun` becomes:
```
module List =
    let rec map f = fun xs -> ...
    let rec length xs = ...
    ...
```

This means:
- `typeCheckModuleWithPrelude` on the prelude file will produce a `Map<string, ModuleExports>` with `"List"` as a key
- `loadPrelude` must accumulate this modules map and store it in `PreludeResult`
- `PreludeResult` needs a new field `Modules: Map<string, ModuleExports>` and `ModuleValueEnv: Map<string, ModuleValueEnv>`
- `typeCheckModuleWithPrelude` call site in `loadPrelude` must collect `_modules` and merge it
- `evalModuleDecls` call site must collect `_moduleEnv` and merge it
- `Program.fs` run pipeline must pass `prelude.Modules` as initial modules to `typeCheckDecls` (currently passes `Map.empty`)
- `Program.fs` eval pipeline must pass `prelude.ModuleValueEnv` as initial `moduleEnv` to `evalModuleDecls` (currently passes `Map.empty`)

**Impact on unqualified names:** Wrapping in `module List = ...` means `length`, `map` etc. are no longer top-level bindings — they become `List.length` only. This **breaks unqualified access** unless the prelude also does `open List`. Each prelude file should add `open List` (or the equivalent module name) after the module block so unqualified names keep working.

Alternatively, keep top-level bindings and also add a module wrapper that aliases them. But the simplest approach is: wrap in module, add open at the bottom of each file.

**Approach B:** Build virtual modules from prelude file stems in `loadPrelude`. This avoids modifying `.fun` files but requires more F# code to extract bindings by stem name.

Approach A is recommended: it is transparent, uses the language's own module system, and the `.fun` file changes are minimal.

### PAR-01: try-with inline single clause

Current grammar:
```
| TRY Expr WITH MatchClauses
| TRY INDENT Expr DEDENT WITH MatchClauses
```

`MatchClauses` starts with `PIPE`. For inline `try failwith "x" with e -> y`:
- body is `failwith "x"` (an `Expr`)
- after `with`, parser sees `e` (an `IDENT`), not `PIPE`
- parse fails

**Fix option A — add bare single-clause variant:**
```fsharp
| TRY Expr WITH IDENT ARROW Expr
    { TryWith($2, [(PatVar($4, symSpan parseState 4), None, $6)], ruleSpan parseState 1 6) }
```
This adds a rule for the single-clause, no-pipe form. It only covers the bare ident pattern, not arbitrary patterns without a pipe.

**Fix option B — allow MatchClauses to optionally start without a pipe:**
Introduce a new `TryWithClauses` nonterminal:
```
TryWithClauses:
    | MatchClauses        { $1 }  // | p -> e form
    | Pattern ARROW Expr  { [($1, None, $3)] }  // bare p -> e (no leading pipe)
```
Then: `| TRY Expr WITH TryWithClauses { TryWith($2, $4, ruleSpan parseState 1 4) }`

This is broader: it allows any pattern (not just `IDENT`) without a leading pipe. Option B is more general and consistent with OCaml semantics where `try ... with e -> ...` is valid.

**Caution on grammar conflicts:** Adding `Pattern ARROW Expr` as an alternative in `TryWithClauses` may cause shift/reduce conflicts if `Pattern` can start with the same tokens that `MatchClauses` could use. In practice, `MatchClauses` requires `PIPE` first, so there is no conflict — the parser can distinguish by looking at whether the next token after `with` is `PIPE` or a pattern token.

**Recommended:** Option B (TryWithClauses nonterminal) for generality.

### Anti-Patterns to Avoid

- **Don't change `typeCheckModuleWithPrelude` to accept `mods` as input:** It always starts fresh from `Map.empty`. The fix is in the `FileImportDecl` arm of `typeCheckDecls`, which already has `mods` in scope and should merge the returned file modules into it.
- **Don't wrap all prelude functions in module + forget `open`:** This would break all existing tests that use `length`, `map` etc. unqualified.
- **Don't use a precedence directive to fix PAR-01:** This is a grammar issue, not an operator-precedence issue. Adding a new nonterminal rule is the correct approach.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Module map merging | Custom merge logic | `Map.fold` (already used throughout) | Consistent with existing pattern |
| Grammar conflict detection | Manual inspection | `fsyacc` conflict warnings | Build output shows S/R conflicts |

## Common Pitfalls

### Pitfall 1: Breaking unqualified Prelude access when wrapping in modules

**What goes wrong:** Wrapping `Prelude/List.fun` content in `module List = ...` makes `map`, `length` etc. accessible only as `List.map`, `List.length`. All existing tests that call `map` directly will break.
**Why it happens:** Module declarations create a new scope; names inside are not automatically exported to parent scope.
**How to avoid:** Add `open List` at the end of each prelude file after the module block, so all names are also available unqualified.
**Warning signs:** Large number of test failures after the MOD-02 fix for names that were previously unqualified.

### Pitfall 2: fileImportTypeChecker signature change breaks Prelude loading

**What goes wrong:** Changing the `fileImportTypeChecker` delegate signature requires updating every call site including `loadAndTypeCheckFileImpl` in `Prelude.fs`. If `Prelude.fs` is only partially updated, the mutable delegate initialization fails at compile time.
**How to avoid:** Update all of: delegate type declaration, `loadAndTypeCheckFileImpl` implementation, `FileImportDecl` arm call, and `loadAndTypeCheckFileImpl` initialization line in the `do` block at the bottom of `Prelude.fs`.

### Pitfall 3: Prelude modules map not threaded into Program.fs run pipeline

**What goes wrong:** After fixing MOD-02 so `PreludeResult` carries `Modules`, `Program.fs` still passes `Map.empty` as the initial `modules` arg to `typeCheckDecls`. `List.length` in a user file would still fail.
**How to avoid:** In `Program.fs`, pass `prelude.Modules` when calling `typeCheckModuleWithPrelude` — but `typeCheckModuleWithPrelude` currently doesn't accept an initial modules map. The fix requires either adding an initial-modules parameter to `typeCheckModuleWithPrelude`, or changing the call to `typeCheckDecls` directly. The cleanest approach: add an optional `initialModules` parameter to `typeCheckModuleWithPrelude`.

### Pitfall 4: PAR-01 grammar rule — MatchClauses also used by MATCH expression

**What goes wrong:** If `TryWithClauses` is introduced as a looser version of `MatchClauses`, it must not replace `MatchClauses` in the `MATCH Expr WITH MatchClauses` rule. The `match` expression still requires `PIPE`-led clauses.
**How to avoid:** Only use `TryWithClauses` in the `TRY` rules. Leave `MatchClauses` unchanged.

### Pitfall 5: Eval side moduleEnv for imported files

**What goes wrong:** The eval side `loadAndEvalFileImpl` already returns `mergedModEnv`. The `FileImportDecl` arm in `evalModuleDecls` already uses this return value correctly. So the eval side for MOD-01 may already work at runtime once the type-check side is fixed. This should be verified rather than assumed.
**How to verify:** After fixing the TC side, run `open "/tmp/mod.fun"` then `Math.square 5`. If it evaluates to 25 without a runtime error, the eval side was already correct.

## Code Examples

### Current (broken) FileImportDecl arm in TypeCheck.fs
```fsharp
// Line ~796-801 of TypeCheck.fs
| FileImportDecl(path, _span) ->
    let resolvedPath = resolveImportPath path currentTypeCheckingFile
    let (env', cEnv', rEnv') = fileImportTypeChecker resolvedPath cEnv rEnv env
    (env', cEnv', rEnv', mods, warns)   // mods not updated: MOD-01 bug
```

### Fixed FileImportDecl arm (sketch)
```fsharp
| FileImportDecl(path, _span) ->
    let resolvedPath = resolveImportPath path currentTypeCheckingFile
    let (env', cEnv', rEnv', fileMods) = fileImportTypeChecker resolvedPath cEnv rEnv env mods
    let mods' = Map.fold (fun acc k v -> Map.add k v acc) mods fileMods
    (env', cEnv', rEnv', mods', warns)
```

### Updated delegate signature
```fsharp
// TypeCheck.fs
let mutable fileImportTypeChecker :
    (string -> ConstructorEnv -> RecordEnv -> TypeEnv -> Map<string, ModuleExports>
        -> TypeEnv * ConstructorEnv * RecordEnv * Map<string, ModuleExports>) =
    fun resolvedPath _ _ _ _ ->
        failwithf "FileImport type checker not initialized. Cannot import '%s'." resolvedPath
```

### Updated loadAndTypeCheckFileImpl (sketch)
```fsharp
// Prelude.fs
let rec loadAndTypeCheckFileImpl
    (resolvedPath: string)
    (cEnv: ConstructorEnv)
    (rEnv: RecordEnv)
    (typeEnv: TypeEnv)
    (mods: Map<string, ModuleExports>)
    : TypeEnv * ConstructorEnv * RecordEnv * Map<string, ModuleExports> =
    // ...
    match typeCheckModuleWithPrelude cEnv rEnv typeEnv m with
    | Ok (_warnings, fileCEnv, fileREnv, fileMods, fileTypeEnv) ->
        let mergedCEnv = ...
        let mergedREnv = ...
        let mergedTypeEnv = ...
        let mergedMods = Map.fold (fun acc k v -> Map.add k v acc) mods fileMods
        (mergedTypeEnv, mergedCEnv, mergedREnv, mergedMods)
```

### PreludeResult with modules (sketch)
```fsharp
type PreludeResult = {
    Env: Env
    TypeEnv: TypeEnv
    CtorEnv: ConstructorEnv
    RecEnv: RecordEnv
    Modules: Map<string, ModuleExports>          // new
    ModuleValueEnv: Map<string, ModuleValueEnv>  // new
}
```

### Prelude file wrapping pattern (List.fun example)
```
module List =
    let rec map f = fun xs -> match xs with | [] -> [] | h :: t -> f h :: map f t
    let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length xs
    // ... rest of functions

open List   // re-export unqualified names
```

### PAR-01 fix in Parser.fsy (TryWithClauses nonterminal)
```fsharp
// Add TryWithClauses nonterminal
TryWithClauses:
    | MatchClauses                    { $1 }
    | Pattern ARROW Expr              { [($1, None, $3)] }

// Update TRY rules
| TRY Expr WITH TryWithClauses              { TryWith($2, $4, ruleSpan parseState 1 4) }
| TRY INDENT Expr DEDENT WITH TryWithClauses { TryWith($3, $6, ruleSpan parseState 1 6) }
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `_modules` discarded in import TC | propagate `fileMods` | This phase | Imported module qualified access works |
| Prelude bindings flat (no module) | Prelude files wrapped in module | This phase | Prelude qualified access (`List.length`) works |
| TRY only accepts `|`-led clauses | TRY also accepts bare `Pattern ARROW Expr` | This phase | Inline try-with works |

## Open Questions

1. **Does eval-side modEnv already propagate correctly for MOD-01?**
   - What we know: `loadAndEvalFileImpl` returns `mergedModEnv` which includes the file's `ModuleValueEnv`. `FileImportDecl` arm in `evalModuleDecls` returns this correctly.
   - What's unclear: Whether `evalModuleDecls` is called with the merged `modEnv` in all entry points, or whether `Program.fs` passes `Map.empty` as initial `moduleEnv`.
   - Recommendation: Inspect `Program.fs` line ~221: `Eval.evalModuleDecls mergedRecEnv Map.empty initialEnv moduleDecls` — `Map.empty` here means no prelude modules. After MOD-02 fix adds `prelude.ModuleValueEnv`, pass it here instead.

2. **Does `typeCheckModuleWithPrelude` need an `initialModules` parameter?**
   - What we know: It currently starts `typeCheckDecls` with `Map.empty` for modules.
   - What's unclear: Whether prelude modules need to be visible at the top-level `typeCheckDecls` call for user files.
   - Recommendation: Yes. Add `initialModules: Map<string, ModuleExports>` parameter to `typeCheckModuleWithPrelude` and thread it into `typeCheckDecls`. Call sites in `Program.fs` and `loadPrelude` pass `prelude.Modules` (or `Map.empty` for the prelude loading itself).

3. **Grammar conflict risk for PAR-01 TryWithClauses**
   - What we know: `Pattern` and `PIPE`-start of `MatchClauses` are disjoint by the leading token.
   - What's unclear: Whether fsyacc generates any shift/reduce warnings.
   - Recommendation: Build with `dotnet build` and inspect fsyacc output for new conflicts. If conflicts appear, fall back to Option A (only `IDENT ARROW Expr` bare form).

## Sources

### Primary (HIGH confidence)
- Direct source reading of `src/LangThree/TypeCheck.fs` — `typeCheckDecls`, `fileImportTypeChecker`, `mergeModuleExportsForTypeCheck`, `rewriteModuleAccess`, `typeCheckModuleWithPrelude`
- Direct source reading of `src/LangThree/Prelude.fs` — `loadAndTypeCheckFileImpl`, `loadAndEvalFileImpl`, `loadPrelude`
- Direct source reading of `src/LangThree/Eval.fs` — `fileImportEvaluator`, `evalModuleDecls`, `FileImportDecl` arm
- Direct source reading of `src/LangThree/Bidir.fs` — `FieldAccess` case, `FieldAccessOnNonRecord` raise
- Direct source reading of `src/LangThree/Parser.fsy` — `TRY Expr WITH MatchClauses`, `MatchClauses`, `TryWithClauses` gap
- Direct source reading of `src/LangThree/Program.fs` — run pipeline, `_modules` discard on line 204
- Direct source reading of `Prelude/List.fun` — flat top-level bindings, no module wrapper
- Direct source reading of `tests/LangThree.Tests/ModuleTests.fs` — test helper patterns

### Secondary (MEDIUM confidence)
- `Diagnostic.fs` — confirmed E0313 = `FieldAccessOnNonRecord`

## Metadata

**Confidence breakdown:**
- MOD-01 root cause: HIGH — code path is fully traced, `_mods` discard visible on lines 101, 204
- MOD-01 fix approach: HIGH — delegate signature extension is straightforward, no ambiguity
- MOD-02 root cause: HIGH — Prelude files have no module wrapper, `_modules` discarded in `loadPrelude`
- MOD-02 fix approach: MEDIUM — Approach A is clear but requires "open at bottom" trick to preserve unqualified access; risk of missing a prelude file or breaking existing unqualified tests
- PAR-01 root cause: HIGH — grammar rule requires PIPE, `e -> ...` has no PIPE
- PAR-01 fix approach: HIGH — new nonterminal is standard LALR technique; conflict risk is LOW given disjoint leading tokens
- PreludeResult threading into Program.fs: MEDIUM — requires identifying all call sites

**Research date:** 2026-03-25
**Valid until:** 2026-04-25 (stable codebase)
