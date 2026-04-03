# Phase 81: Export API - Research

**Researched:** 2026-04-03
**Domain:** F# compiler API design — assembling type-checker outputs into a single public record for external consumption
**Confidence:** HIGH

## Summary

Phase 81 creates `ExportApi.fs`, a new module in `FunLang.fsproj` that exposes a single entry point `typeCheckFile : string -> TypedModule`. The `TypedModule` record bundles the three pieces of type information produced during type checking: the per-expression annotation map (from `Bidir.annotationMap`, populated by Phase 79), the full binding environment (the `TypeEnv` returned by `typeCheckModuleWithPrelude`, established by Phase 80), and the builtin schemes (from `TypeCheck.initialTypeEnv`).

All the raw data already exists after `typeCheckModuleWithPrelude` runs. Phase 81 is integration work: wire `Prelude.loadPrelude`, parse the file, call `typeCheckModuleWithPrelude`, then snapshot `Bidir.annotationMap` before the next call can reset it. The snapshot must happen immediately after `typeCheckModuleWithPrelude` returns, because `annotationMap` is a mutable module-level ref that gets reset at every `typeCheckModuleWithPrelude` entry.

The `ExportApi.fs` module must be placed in `FunLang.fsproj` after `TypeCheck.fs` and `Prelude.fs` but before `Program.fs`. No new NuGet packages are needed. A new `ExportApiTests.fs` in the test project verifies API-01 and API-02, following the same Expecto pattern used by `TypeAnnotationTests.fs` and `TypeEnvTests.fs`.

**Primary recommendation:** Add `ExportApi.fs` after `Prelude.fs` in `FunLang.fsproj` (position 16.5, before `Repl.fs`/`Program.fs`), define `TypedModule` record with three fields, implement `typeCheckFile` by calling `loadPrelude` then `typeCheckModuleWithPrelude`, and snapshot `Bidir.annotationMap` immediately after type checking returns.

## Standard Stack

This phase is entirely internal to the FunLang compiler. No new NuGet packages required.

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `TypeCheck.typeCheckModuleWithPrelude` | existing | Full type-check pipeline returning 7-tuple | Already used by Program.fs for all file type-checking |
| `Prelude.loadPrelude` | existing | Load standard library environments | Already used by Program.fs before every file type-check |
| `Bidir.annotationMap` | existing (Phase 79) | ConcurrentDictionary<Span, Type> populated during synth | Reset by `typeCheckModuleWithPrelude` on entry; must snapshot after return |
| `TypeCheck.exportBindingEnv` | existing (Phase 80) | Identity wrapper documenting binding env extraction | Established by Phase 80 as the extraction point for Phase 81 |
| `TypeCheck.initialTypeEnv` | existing | `Map<string, Scheme>` of ~50 builtin function schemes | Already public, already the canonical builtin source |
| `TypeCheck.BindingEnv` | existing (Phase 80) | Type alias `= TypeEnv` | Documents intent; Phase 81 uses it for TypedModule field type |
| `TypeAnnotationMap` module | existing (Phase 79) | `create`/`record`/`tryFind`/`toSeq` helpers | Standard access pattern for the annotation map |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `Program.parseModuleFromString` | existing | Parse `string * filename -> Module` with IndentFilter | Reuse the same parsing pipeline as Program.fs (or inline it in ExportApi) |
| `TypeCheck.currentTypeCheckingFile` | existing | Mutable ref for relative import resolution | Must be set before `typeCheckModuleWithPrelude` for file imports to resolve |
| `System.IO.File.ReadAllText` | .NET stdlib | Read file contents | Same pattern as Program.fs Check/Emit_Type branches |
| `Expecto` | `10.*` | Test framework already in test project | Same framework as all existing tests |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Snapshot `Bidir.annotationMap` after return | Return the live `ConcurrentDictionary` reference | Returning the live ref is safe if caller uses it before the next type-check call, but is fragile. Snapshot is a `Map<Span, Type>` — immutable, safe to pass around. |
| Call `loadPrelude None None` every time | Cache PreludeResult as a module-level value | Caching is an optimization; for Phase 81, calling loadPrelude on each invocation is simpler and correct. |
| Include `AnnotationMap` as `ConcurrentDictionary` | Include as `Map<Span, Type>` (snapshot) | `Map` is immutable and safe for external consumers; `ConcurrentDictionary` reference may be mutated by next call. Use `Map`. |

**Installation:** No packages to install.

## Architecture Patterns

### Recommended File Placement in FunLang.fsproj

```
src/FunLang/
├── TypeAnnotationMap.fs   # Phase 79 (position 2.6)
├── TypeCheck.fs           # position 6 — BindingEnv + exportBindingEnv here
├── ...
├── Prelude.fs             # position 13 — loadPrelude here
├── ExportApi.fs           # NEW position 16.5 — must come after Prelude.fs
├── Repl.fs                # position 14
├── ProjectFile.fs         # position 15
├── Cli.fs                 # position 16
└── Program.fs             # position 17 — unchanged
```

ExportApi.fs must come after Prelude.fs (it calls `Prelude.loadPrelude`) and can come before or after Repl/ProjectFile/Cli since those are independent. Place it just before Program.fs for clarity.

### TypedModule Record Design

```fsharp
// Source: Requirements API-01, API-02 + Phase 79/80 infrastructure
type TypedModule = {
    /// Per-expression type annotations: Span -> Type.
    /// Contains one entry per expression node visited by Bidir.synth.
    /// Snapshot taken immediately after typeCheckModuleWithPrelude returns.
    AnnotationMap: Map<Ast.Span, Type.Type>

    /// Full binding environment: builtin + prelude + user top-level bindings.
    /// Keys are binding names; values are Scheme (forall vars. constraints => type).
    /// Identical to what typeCheckModuleWithPrelude returns as its 7th tuple element.
    BindingEnv: TypeCheck.BindingEnv

    /// Builtin-only schemes from TypeCheck.initialTypeEnv.
    /// Subset of BindingEnv; provided so consumers can distinguish builtins from user code.
    BuiltinSchemes: Type.TypeEnv
}
```

### Pattern 1: typeCheckFile Implementation

**What:** Reads a file, loads prelude, parses, type-checks, snapshots annotation map, assembles TypedModule.
**When to use:** This is the single public entry point for Phase 81.

```fsharp
// Source: Program.fs --check branch (lines 273-298) + Phase 79/80 patterns
let typeCheckFile (filePath: string) : TypedModule =
    let absPath = System.IO.Path.GetFullPath(filePath)
    let input = System.IO.File.ReadAllText(absPath)
    let prelude = Prelude.loadPrelude None None
    TypeCheck.currentTypeCheckingFile <- absPath
    let m = parseModuleFromString input absPath    // same as Program.fs
    match TypeCheck.typeCheckModuleWithPrelude
            prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv
            prelude.TypeEnv prelude.Modules m with
    | Error diags ->
        let msgs = diags |> List.map Diagnostic.formatDiagnostic |> String.concat "\n"
        failwith (sprintf "Type errors in %s:\n%s" absPath msgs)
    | Ok (_warnings, _ctorEnv, _recEnv, _classEnv, _instEnv, _modules, typeEnv) ->
        // CRITICAL: snapshot annotationMap immediately — it is reset on the NEXT call
        let annotationSnapshot =
            Bidir.annotationMap
            |> Seq.map (fun kv -> (kv.Key, kv.Value))
            |> Map.ofSeq
        {
            AnnotationMap = annotationSnapshot
            BindingEnv    = TypeCheck.exportBindingEnv typeEnv
            BuiltinSchemes = TypeCheck.initialTypeEnv
        }
```

### Pattern 2: parseModuleFromString in ExportApi

ExportApi.fs needs a local `parseModuleFromString`. The same function appears in `Program.fs` and `Prelude.fs`. Because F# compilation order requires ExportApi after Prelude, ExportApi can either:

1. **Duplicate the 20-line helper** (simplest — already duplicated in Program.fs and Prelude.fs)
2. **Call `Prelude.parseModuleFromString`** — but this function is `let private` in Prelude.fs

The correct approach is to duplicate the helper in ExportApi.fs, identical to the copy in Program.fs. This is the established codebase pattern.

### Pattern 3: Test Structure

**What:** Expecto tests that call `ExportApi.typeCheckFile` on a temp file and assert properties of the returned `TypedModule`.
**When to use:** New `ExportApiTests.fs` added to `FunLang.Tests.fsproj` before `Program.fs`.

```fsharp
// Source: TypeAnnotationTests.fs and TypeEnvTests.fs patterns
module FunLang.Tests.ExportApiTests

open Expecto
open System.IO

let withTempFile (content: string) (f: string -> 'a) : 'a =
    let path = Path.GetTempFileName() |> fun p -> Path.ChangeExtension(p, ".fun")
    File.WriteAllText(path, content)
    try f path finally File.Delete(path)

[<Tests>]
let exportApiTests = testSequenced <| testList "ExportApi" [

    test "API-01: typeCheckFile returns TypedModule without error" {
        withTempFile "let x = 42" (fun path ->
            let tm = ExportApi.typeCheckFile path
            ignore tm)  // no exception = pass
    }

    test "API-02: TypedModule.AnnotationMap is non-empty for non-trivial file" {
        withTempFile "let x = 1 + 2" (fun path ->
            let tm = ExportApi.typeCheckFile path
            Expect.isGreaterThan tm.AnnotationMap.Count 0 "AnnotationMap should have entries")
    }

    test "API-02: TypedModule.BindingEnv contains user binding" {
        withTempFile "let myVal = 99" (fun path ->
            let tm = ExportApi.typeCheckFile path
            Expect.isSome (Map.tryFind "myVal" tm.BindingEnv) "User binding should be in BindingEnv")
    }

    test "API-02: TypedModule.BuiltinSchemes contains print" {
        withTempFile "let x = 1" (fun path ->
            let tm = ExportApi.typeCheckFile path
            Expect.isSome (Map.tryFind "print" tm.BuiltinSchemes) "print should be in BuiltinSchemes")
    }
]
```

### Anti-Patterns to Avoid

- **Not snapshotting annotationMap:** `Bidir.annotationMap` is reset at each `typeCheckModuleWithPrelude` call. If the caller invokes `typeCheckFile` twice, the second call clears the map for the first call's TypedModule. Always snapshot to immutable `Map` immediately after the Ok return.
- **Returning `ConcurrentDictionary` reference directly:** Exposes mutable state. Return `Map<Span, Type>` snapshot.
- **Calling `typeCheckModuleWithPrelude` without setting `TypeCheck.currentTypeCheckingFile`:** Import resolution for `FileImportDecl` uses this mutable ref. Set it to the absolute file path before calling.
- **Using `typeCheckModule` (no-prelude variant):** For a real file API, prelude must be loaded. `typeCheckModule` passes empty prelude envs — prelude functions like `map`, `filter` would not type-check in user files.
- **Placing ExportApi.fs before Prelude.fs in fsproj:** F# requires files in dependency order. ExportApi calls `Prelude.loadPrelude` so it must come after Prelude.fs.
- **Duplicating PreludeResult fields into TypedModule:** TypedModule only needs annotation map + binding env + builtin schemes (per API-02). ClassEnv, InstanceEnv, CtorEnv are not part of the Phase 81 TypedModule contract.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File parsing | Custom lexer/parser pipeline | Duplicate existing `parseModuleFromString` from Program.fs | Already handles IndentFilter, position tracking, error formatting |
| Prelude loading | Parse Prelude/*.fun manually | `Prelude.loadPrelude None None` | Handles 3-stage dir discovery, topological sort, cycle detection |
| Annotation map snapshotting | Complex concurrent copy | `Seq.map (fun kv -> ...) |> Map.ofSeq` | One-liner; ConcurrentDictionary is iterable |
| TypeEnv merging | Custom fold | `Map.fold (fun acc k v -> Map.add k v acc) base overlay` | Standard pattern used 10+ times in TypeCheck.fs; don't reinvent |

**Key insight:** Every operation in Phase 81 is wiring existing infrastructure. The data is already produced — ExportApi assembles it.

## Common Pitfalls

### Pitfall 1: annotationMap read-after-reset
**What goes wrong:** `TypedModule.AnnotationMap` is empty (or stale) because the ConcurrentDictionary reference was captured after a subsequent `typeCheckModuleWithPrelude` call reset it.
**Why it happens:** `Bidir.annotationMap` is module-level mutable state reset at line 1269 of TypeCheck.fs on every `typeCheckModuleWithPrelude` entry. If a second call runs (e.g., in a test that calls `typeCheckFile` twice), the first call's data is gone.
**How to avoid:** Snapshot immediately after `typeCheckModuleWithPrelude` returns in the `Ok` branch, before any other operation. Use `|> Map.ofSeq` to make it immutable.
**Warning signs:** `AnnotationMap.Count = 0` in tests even for non-trivial files.

### Pitfall 2: Missing prelude — prelude functions not type-checkable
**What goes wrong:** User file that uses `map`, `filter`, or other prelude functions fails with "unbound variable" during `typeCheckFile`.
**Why it happens:** The test used `TypeCheck.typeCheckModule` (the no-prelude variant) or called `typeCheckModuleWithPrelude` with empty prelude args.
**How to avoid:** Always call `Prelude.loadPrelude None None` and pass its CtorEnv/RecEnv/ClassEnv/InstEnv/TypeEnv/Modules to `typeCheckModuleWithPrelude`. This is the same pattern as Program.fs `--check`.
**Warning signs:** Type errors in files that use standard prelude functions when calling `typeCheckFile`.

### Pitfall 3: Import resolution fails (currentTypeCheckingFile not set)
**What goes wrong:** Files with `import "./other.fun"` fail to resolve because the relative path base is wrong.
**Why it happens:** `TypeCheck.resolveImportPath` uses `TypeCheck.currentTypeCheckingFile` to resolve relative imports. If not set, it defaults to empty string and relative paths fail.
**How to avoid:** Set `TypeCheck.currentTypeCheckingFile <- absPath` before calling `typeCheckModuleWithPrelude`.
**Warning signs:** `UnresolvedModule` type errors for files that use file imports.

### Pitfall 4: fsproj ordering — ExportApi before Prelude
**What goes wrong:** Build fails with "Module 'Prelude' is not defined" or similar.
**Why it happens:** F# compiles files in order. ExportApi.fs depends on Prelude.fs; if ExportApi comes first in the ItemGroup, it can't see Prelude.
**How to avoid:** Insert `<Compile Include="ExportApi.fs" />` after the Prelude.fs entry in FunLang.fsproj.
**Warning signs:** Compile error on `Prelude.loadPrelude` reference in ExportApi.fs.

### Pitfall 5: TypedModule includes AnnotationMap entries from prelude loading
**What goes wrong:** AnnotationMap contains entries from prelude file type-checks, not just the user file.
**Why it happens:** `Prelude.loadPrelude` calls `typeCheckModuleWithPrelude` for each prelude file, which resets and repopulates `Bidir.annotationMap`. After all prelude files are loaded, the map reflects the last prelude file, not the user file.
**How to avoid:** Call `Prelude.loadPrelude` first (it will reset and repopulate the map for prelude files), then call `typeCheckModuleWithPrelude` for the user file (it resets the map again and populates it with user file annotations). Snapshot after the user-file type-check. The existing call order in Program.fs (prelude then user file) is correct.
**Warning signs:** AnnotationMap contains spans with filenames matching Prelude/*.fun instead of the user file.

## Code Examples

### ExportApi.fs — complete implementation sketch

```fsharp
// Source: Program.fs --check branch + Bidir.annotationMap snapshot pattern from TypeAnnotationTests.fs
module ExportApi

open System.IO
open FSharp.Text.Lexing
open Ast
open TypeCheck
open Diagnostic
open FunLang.IndentFilter

/// All type information produced by type-checking a FunLang file.
type TypedModule = {
    /// Per-expression type annotations: Span -> Type.
    /// Snapshot taken immediately after type checking (immutable Map).
    AnnotationMap: Map<Ast.Span, Type.Type>
    /// Full binding environment: builtins + prelude + user top-level bindings.
    BindingEnv: TypeCheck.BindingEnv
    /// Builtin-only schemes (subset of BindingEnv for consumer filtering).
    BuiltinSchemes: Type.TypeEnv
}

/// Parse a string as a FunLang module using IndentFilter.
/// Duplicated from Program.fs (F# compilation order requires it here).
let private parseModuleFromString (input: string) (filename: string) : Module =
    let filteredTokens =
        let lexbuf = LexBuffer<char>.FromString input
        Lexer.setInitialPos lexbuf filename
        let rec collect () =
            let startPos = lexbuf.StartPos
            let tok = Lexer.tokenize lexbuf
            let endPos = lexbuf.EndPos
            if tok = Parser.EOF then
                [{ Token = Parser.EOF; StartPos = startPos; EndPos = endPos }]
            else
                { Token = tok; StartPos = startPos; EndPos = endPos } :: collect ()
        filterPositioned defaultConfig (collect ())
    let lexbuf2 = LexBuffer<char>.FromString input
    Lexer.setInitialPos lexbuf2 filename
    let mutable index = 0
    let mutable lastToken : Parser.token option = None
    let tokenizer (lb: LexBuffer<_>) =
        if index < filteredTokens.Length then
            let pt = filteredTokens.[index]
            index <- index + 1
            lb.StartPos <- pt.StartPos
            lb.EndPos <- pt.EndPos
            lastToken <- Some pt.Token
            pt.Token
        else
            Parser.EOF
    Parser.parseModule tokenizer lexbuf2

/// Type-check a FunLang file and return all type information in a TypedModule record.
/// Raises on type errors (does not return Result — errors are exceptional for the export use case).
let typeCheckFile (filePath: string) : TypedModule =
    let absPath = Path.GetFullPath(filePath)
    let input = File.ReadAllText(absPath)
    let prelude = Prelude.loadPrelude None None
    TypeCheck.currentTypeCheckingFile <- absPath
    let m = parseModuleFromString input absPath
    match TypeCheck.typeCheckModuleWithPrelude
            prelude.CtorEnv prelude.RecEnv prelude.ClassEnv prelude.InstEnv
            prelude.TypeEnv prelude.Modules m with
    | Error diags ->
        let msgs = diags |> List.map formatDiagnostic |> String.concat "\n"
        failwith (sprintf "Type errors in %s:\n%s" absPath msgs)
    | Ok (_warnings, _ctorEnv, _recEnv, _classEnv, _instEnv, _modules, typeEnv) ->
        // Snapshot annotationMap immediately — it is reset on the next typeCheckModuleWithPrelude call
        let annotationSnapshot =
            Bidir.annotationMap
            |> Seq.map (fun kv -> (kv.Key, kv.Value))
            |> Map.ofSeq
        {
            AnnotationMap  = annotationSnapshot
            BindingEnv     = TypeCheck.exportBindingEnv typeEnv
            BuiltinSchemes = TypeCheck.initialTypeEnv
        }
```

### FunLang.fsproj addition (after Prelude.fs entry)

```xml
<!-- 13. Prelude loading (uses Eval) -->
<Compile Include="Prelude.fs" />

<!-- 13.5. Export API (uses TypeCheck + Prelude) -->
<Compile Include="ExportApi.fs" />

<!-- 14. REPL implementation (uses Eval) -->
<Compile Include="Repl.fs" />
```

### FunLang.Tests.fsproj addition (before Program.fs)

```xml
<Compile Include="TypeEnvTests.fs" />
<Compile Include="ExportApiTests.fs" />   <!-- NEW -->
<Compile Include="Program.fs" />
```

### ExportApiTests.fs — test pattern

```fsharp
// Source: TypeAnnotationTests.fs + TypeEnvTests.fs patterns
module FunLang.Tests.ExportApiTests

open Expecto
open System.IO

let withTempFile (content: string) (f: string -> 'a) : 'a =
    let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".fun")
    File.WriteAllText(path, content)
    try f path finally (try File.Delete(path) with _ -> ())

[<Tests>]
let exportApiTests = testSequenced <| testList "ExportApi" [

    test "API-01: typeCheckFile returns TypedModule for valid file" {
        withTempFile "let x = 42" (fun path ->
            let tm = ExportApi.typeCheckFile path
            ignore tm)
    }

    test "API-02: AnnotationMap non-empty for expression file" {
        withTempFile "let x = 1 + 2" (fun path ->
            let tm = ExportApi.typeCheckFile path
            Expect.isGreaterThan tm.AnnotationMap.Count 0 "AnnotationMap should have entries")
    }

    test "API-02: BindingEnv contains user top-level binding" {
        withTempFile "let answer = 42" (fun path ->
            let tm = ExportApi.typeCheckFile path
            Expect.isSome (Map.tryFind "answer" tm.BindingEnv) "answer should be in BindingEnv")
    }

    test "API-02: BuiltinSchemes contains print" {
        withTempFile "let x = 1" (fun path ->
            let tm = ExportApi.typeCheckFile path
            Expect.isSome (Map.tryFind "print" tm.BuiltinSchemes) "print should be in BuiltinSchemes")
    }

    test "API-02: BindingEnv includes builtins (print present)" {
        withTempFile "let x = 1" (fun path ->
            let tm = ExportApi.typeCheckFile path
            Expect.isSome (Map.tryFind "print" tm.BindingEnv) "print should be in BindingEnv")
    }
]
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No external API — type info accessible only inside type-checker | `ExportApi.typeCheckFile` returns `TypedModule` | Phase 81 (now) | External tools (FunLangCompiler) can consume type info |
| `Bidir.annotationMap` was private mutable state | Snapshot taken into immutable `Map<Span, Type>` after type-check | Phase 81 (now) | Safe to pass to external consumers without mutation risk |

**Deprecated/outdated:**
- None. This is a purely additive phase.

## Open Questions

1. **Should `typeCheckFile` return `Result<TypedModule, string>` or raise on error?**
   - What we know: Program.fs uses exception-based error handling (try/catch wrapping type-check calls). Tests use `failwith` in helpers.
   - What's unclear: Whether the API contract should be `Result`-based (more functional) or exception-based (matches rest of codebase).
   - Recommendation: Raise (`failwith`) for now. This matches Program.fs patterns and is simpler. The phase description says "returns a TypedModule record without error" — error path is out of scope for success criteria. Can be wrapped in Result by Phase 82 or consumer if needed.

2. **Should AnnotationMap include entries from imported files?**
   - What we know: `Bidir.annotationMap` is reset at each `typeCheckModuleWithPrelude` call. When `typeCheckModuleWithPrelude` processes a `FileImportDecl`, it calls into `Prelude.loadAndTypeCheckFileImpl` which calls `typeCheckModuleWithPrelude` again — resetting the map for the imported file. After all imports finish, the map reflects only the top-level user file's expressions.
   - What's unclear: Whether this is the desired behavior (user file only) or a gap (imported file expressions are not annotated).
   - Recommendation: Accept user-file-only behavior for Phase 81. This matches the success criteria which says "type-check a file and receive TypedModule." Imported file annotations are a future enhancement.

3. **Should `parseModuleFromString` be extracted to a shared module to avoid duplication?**
   - What we know: The function is duplicated in Program.fs (with position tracking) and Prelude.fs (without position tracking — simpler version). ExportApi.fs needs the position-tracking version (same as Program.fs) for correct span recording in the annotation map.
   - What's unclear: Whether Phase 81 should also clean up the duplication.
   - Recommendation: Duplicate for Phase 81 (follows established codebase pattern). Refactoring to a shared module is separate cleanup work not required by Phase 81 success criteria.

## Sources

### Primary (HIGH confidence)
- Direct inspection of `src/FunLang/Program.fs` — existing `--check` and `--emit-type` branches (lines 273-354): exact pattern for loading prelude, setting currentTypeCheckingFile, calling typeCheckModuleWithPrelude
- Direct inspection of `src/FunLang/TypeCheck.fs` — `typeCheckModuleWithPrelude` signature (lines 1254-1308), `BindingEnv` and `exportBindingEnv` (lines 1302-1308), `initialTypeEnv` (line 15), `annotationMap` reset (line 1269)
- Direct inspection of `src/FunLang/Bidir.fs` — `annotationMap` mutable declaration (lines 31-32), reset pattern established by `typeCheckModuleWithPrelude`
- Direct inspection of `src/FunLang/TypeAnnotationMap.fs` — `create`/`record`/`tryFind`/`toSeq` API
- Direct inspection of `src/FunLang/Prelude.fs` — `PreludeResult` record (lines 13-22), `loadPrelude` signature (line 266)
- Direct inspection of `src/FunLang/FunLang.fsproj` — compilation order (lines 56-130), insertion point for ExportApi.fs
- Direct inspection of `tests/FunLang.Tests/TypeAnnotationTests.fs` — `typeCheckAndSnapshot` pattern (lines 35-45): exact precedent for annotationMap snapshot
- Direct inspection of `tests/FunLang.Tests/TypeEnvTests.fs` — test helpers and Expecto patterns established by Phase 80
- Direct inspection of `tests/FunLang.Tests/FunLang.Tests.fsproj` — test project structure for adding ExportApiTests.fs
- Direct inspection of `.planning/REQUIREMENTS.md` — API-01, API-02 definitions
- Direct inspection of `.planning/ROADMAP.md` — Phase 81 success criteria

### Secondary (MEDIUM confidence)
- Phase 80 RESEARCH.md and SUMMARY.md — established BindingEnv/exportBindingEnv decisions
- Phase 79 RESEARCH.md — established annotationMap snapshot pattern

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all code is in-repo, no external dependencies, all types verified by direct inspection
- Architecture: HIGH — ExportApi is direct composition of existing infrastructure; all call signatures verified
- Pitfalls: HIGH — annotationMap reset timing is a real hazard verified in code; all other pitfalls derive from direct code reading

**Research date:** 2026-04-03
**Valid until:** Stable — changes only if `typeCheckModuleWithPrelude` signature changes, `Bidir.annotationMap` is refactored, or `Prelude.loadPrelude` API changes.
