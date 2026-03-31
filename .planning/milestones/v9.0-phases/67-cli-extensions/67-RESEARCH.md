# Phase 67: CLI Extensions - Research

**Researched:** 2026-03-31
**Domain:** Argu CLI parsing, F# mutable delegates, file import dependency traversal
**Confidence:** HIGH

## Summary

Phase 67 adds five CLI features to LangThree: `--check` (type-check only), `--deps` (dependency tree), `--prelude <path>` (explicit Prelude path), `LANGTHREE_PRELUDE` env var, and file import caching. All features build on well-understood existing patterns in the codebase.

The current CLI (`Cli.fs`) uses Argu 6.2.5 with `CliPrefix.DoubleDash`. Adding new flags is straightforward -- just add DU cases. The existing `Program.fs` uses an `if/elif` chain for mode dispatch, which is the natural insertion point. The design survey (`survey/project-build-system-design.md` sections 4.1-4.4) already provides implementation sketches that align well with the codebase.

**Primary recommendation:** Implement in order: CLI-04 (env var) and CLI-03 (`--prelude`) first (they change `findPreludeDir` which everything depends on), then CLI-01 (`--check`), CLI-02 (`--deps`), and CLI-05 (caching) last since caching benefits from all other features being in place.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Argu | 6.2.5 | CLI argument parsing | Already in use, F# idiomatic DU-based parser |
| System.IO | .NET 10 | File/directory operations | Already in use for file loading |
| System.Collections.Generic.Dictionary | .NET 10 | File import cache | Mutable lookup, appropriate for single-threaded caching |

### Supporting
No additional libraries needed. All features use existing .NET BCL and Argu.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Dictionary cache | ConcurrentDictionary | Overkill -- LangThree is single-threaded |
| Argu | System.CommandLine | Would require massive rewrite for minimal gain |

## Architecture Patterns

### Pattern 1: Argu DU Case Addition
**What:** Add new cases to the `CliArgs` discriminated union for each flag
**When to use:** Every new CLI feature
**Example:**
```fsharp
[<CliPrefix(CliPrefix.DoubleDash)>]
type CliArgs =
    | [<AltCommandLine("-e")>] Expr of expression: string
    | Emit_Tokens
    | Emit_Ast
    | Emit_Type
    | Check                           // CLI-01: --check (flag, no argument)
    | Deps                            // CLI-02: --deps (flag, no argument)
    | Prelude of path: string         // CLI-03: --prelude <path> (takes argument)
    | [<MainCommand; Last>] File of filename: string
```

**Key Argu behaviors:**
- Parameterless DU cases become boolean flags (`--check`, `--deps`)
- DU cases with fields become valued flags (`--prelude /path/to/dir`)
- `Emit_Tokens` renders as `--emit-tokens` (underscores become hyphens)
- `Check` renders as `--check`, `Deps` renders as `--deps`, `Prelude` renders as `--prelude`
- `[<MainCommand; Last>]` on `File` means it captures trailing positional args
- `results.Contains Check` checks presence; `results.GetResult Prelude` gets the value
- Multiple flags combine naturally: `--check --prelude /path file.fun`

### Pattern 2: Prelude Path Resolution Priority Chain
**What:** Modify `findPreludeDir` to accept explicit path and env var with priority
**When to use:** CLI-03 and CLI-04
**Example:**
```fsharp
/// Resolve Prelude directory with priority: CLI flag > env var > auto-discovery
let resolvePreludeDir (explicitPath: string option) : string =
    match explicitPath with
    | Some path ->
        if Directory.Exists path then path
        else failwithf "Prelude directory not found: %s" path
    | None ->
        match System.Environment.GetEnvironmentVariable("LANGTHREE_PRELUDE") with
        | null | "" -> findPreludeDir()  // existing 3-stage auto-discovery
        | envPath ->
            if Directory.Exists envPath then envPath
            else failwithf "LANGTHREE_PRELUDE directory not found: %s" envPath
```

### Pattern 3: Check Mode (Type-Check Only, No Eval)
**What:** Run the existing type-check pipeline but skip `evalModuleDecls`
**When to use:** CLI-01
**Key insight:** The existing `--emit-type` with file mode (Program.fs lines 100-127) already does type-check-only. `--check` is essentially the same but with different output formatting (pass/fail message instead of printing types).

### Pattern 4: Dependency Tree Collection via AST Walk
**What:** Parse file, extract `FileImportDecl` nodes, recurse into imported files
**When to use:** CLI-02
**Key insight:** Only parsing is needed, not type-checking. `FileImportDecl(path, span)` nodes in the AST contain the import path. Use `TypeCheck.resolveImportPath` for relative path resolution.
```fsharp
let rec collectDeps (filePath: string) (visited: Set<string>) (depth: int) =
    let absPath = Path.GetFullPath(filePath)
    if Set.contains absPath visited then
        [(absPath, depth, true)]  // circular reference marker
    else
        let source = File.ReadAllText(absPath)
        let m = parseModuleFromString source absPath
        let imports = getDecls m |> List.choose (function
            | FileImportDecl(path, _) -> Some path
            | _ -> None)
        let childDeps =
            imports |> List.collect (fun importPath ->
                let resolved = TypeCheck.resolveImportPath importPath absPath
                collectDeps resolved (Set.add absPath visited) (depth + 1))
        (absPath, depth, false) :: childDeps
```

### Pattern 5: File Import Caching in Prelude.fs
**What:** Wrap `loadAndTypeCheckFileImpl` and `loadAndEvalFileImpl` with Dictionary-based memoization
**When to use:** CLI-05
**Cache key:** Absolute file path (already computed via `Path.GetFullPath`)
**What to cache:** Separate caches for TC results and eval results, since `--check` mode only needs TC cache

### Anti-Patterns to Avoid
- **Modifying `typeCheckModuleWithPrelude` signature:** Don't add cache parameters deep into the type checker. Keep caching at the `loadAndTypeCheckFileImpl` / `loadAndEvalFileImpl` boundary in `Prelude.fs`.
- **Subcommand-style CLI:** Argu supports subcommands, but LangThree's existing pattern is flat flags. Keep it flat.
- **Lazy prelude loading for `--deps`:** Even `--deps` should load prelude, because imported files may exist relative to Prelude-defined paths. However, `--deps` only needs parsing, not type-checking, so prelude loading could be skipped if we only want the import graph. Decide based on whether `--deps` should report type errors.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CLI parsing | Custom argv parsing | Argu (already used) | Handles help text, error messages, flag combinations |
| Path resolution for imports | Custom path logic | `TypeCheck.resolveImportPath` | Already handles absolute vs relative paths correctly |
| Prelude directory discovery | New discovery logic | Extend existing `findPreludeDir()` | Already handles 3 search stages |
| File cycle detection | New cycle detection | Existing `fileLoadingStack` HashSet | Already handles cycles with clear error messages |

## Common Pitfalls

### Pitfall 1: Prelude Loading Before CLI Flag Extraction
**What goes wrong:** `loadPrelude()` is called at line 57 of Program.fs BEFORE any flag checking. If `--prelude` changes the Prelude path, it must be extracted before `loadPrelude()` is called.
**Why it happens:** Current code does `let prelude = Prelude.loadPrelude()` unconditionally early.
**How to avoid:** Extract `--prelude` flag value and env var FIRST, pass to a modified `loadPrelude(explicitPath)`.
**Warning signs:** `--prelude` flag appears to have no effect.

### Pitfall 2: `--deps` Requires parseModuleFromString, Not parse
**What goes wrong:** Using `parse` (expression parser) instead of `parseModuleFromString` (module parser with IndentFilter) to extract `FileImportDecl` from files.
**Why it happens:** Two `parseModuleFromString` functions exist -- one in `Program.fs` and one in `Prelude.fs`. The file mode uses the module parser.
**How to avoid:** Use `parseModuleFromString` (either the one in Program.fs or Prelude.fs) for all file-based operations.

### Pitfall 3: Argu `MainCommand` Interaction with New Flags
**What goes wrong:** `[<MainCommand; Last>] File` captures everything after flags. Adding `--prelude <path>` works correctly because Argu knows `Prelude` takes an argument. But order matters: `file.fun --check` may not parse as expected because `[<Last>]` means File consumes everything after it.
**Why it happens:** Argu's `[<Last>]` attribute.
**How to avoid:** Document that flags must come before the filename, matching existing convention (e.g., `--emit-type file.fun`).

### Pitfall 4: Cache Invalidation Scope
**What goes wrong:** Caching TC results across files that have different accumulated environments.
**Why it happens:** `loadAndTypeCheckFileImpl` receives the caller's cumulative `(cEnv, rEnv, typeEnv, mods)`. The same file imported from different contexts may see different environments.
**How to avoid:** Cache by absolute path only, with the assumption that file imports are deterministic (same file always produces the same exports). This is valid because: (1) imports are processed top-to-bottom, (2) the first import of a file establishes its types, (3) subsequent imports of the same file would get the same result. The cache should store the file's OWN exports (not the merged caller env).

### Pitfall 5: `--check` Without `--prelude` in FunLexYacc Workflow
**What goes wrong:** Running `--check` on FunLexYacc files from the FunLexYacc directory fails because Prelude is not found.
**Why it happens:** `findPreludeDir()` looks for `Prelude/` in CWD first. FunLexYacc's CWD doesn't have Prelude.
**How to avoid:** This is exactly why CLI-03 and CLI-04 exist. Implement them first.

## Code Examples

### Adding Argu Flags (verified pattern from existing Cli.fs)
```fsharp
// Cli.fs -- Add new cases alongside existing ones
[<CliPrefix(CliPrefix.DoubleDash)>]
type CliArgs =
    | [<AltCommandLine("-e")>] Expr of expression: string
    | Emit_Tokens
    | Emit_Ast
    | Emit_Type
    | Check
    | Deps
    | Prelude of path: string
    | [<MainCommand; Last>] File of filename: string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Expr _ -> "evaluate expression"
            | Emit_Tokens -> "show lexer tokens"
            | Emit_Ast -> "show parsed AST"
            | Emit_Type -> "show inferred type"
            | Check -> "type-check without executing"
            | Deps -> "show file dependency tree"
            | Prelude _ -> "set Prelude directory path"
            | File _ -> "evaluate program from file"
```

### Extracting Prelude Path Before loadPrelude (new pattern)
```fsharp
// Program.fs -- Extract --prelude before loading
let preludePath =
    if results.Contains Prelude then Some (results.GetResult Prelude)
    else None
let prelude = Prelude.loadPrelude(preludePath)
```

### Check Mode (derived from existing --emit-type file mode, lines 100-127)
```fsharp
// --check with file
elif results.Contains Check && results.Contains File then
    let filename = results.GetResult File
    if File.Exists filename then
        try
            let input = File.ReadAllText filename
            TypeCheck.currentTypeCheckingFile <- System.IO.Path.GetFullPath filename
            let m = parseModuleFromString input filename
            match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.TypeEnv prelude.Modules m with
            | Ok (warnings, _, _, _, _) ->
                for w in warnings do
                    eprintfn "Warning: %s" (formatDiagnostic w)
                printfn "OK (%d warnings)" (List.length warnings)
                0
            | Error diag ->
                eprintfn "%s" (formatDiagnostic diag)
                1
        with ex ->
            eprintfn "Error: %s" ex.Message
            1
    else
        eprintfn "File not found: %s" filename
        1
```

### File Import Cache Structure
```fsharp
// Prelude.fs -- Add cache at module level
let private tcCache = System.Collections.Generic.Dictionary<string, TypeEnv * ConstructorEnv * RecordEnv * Map<string, ModuleExports>>()
let private evalCache = System.Collections.Generic.Dictionary<string, Env * Map<string, ModuleValueEnv>>()

let rec loadAndTypeCheckFileImpl resolvedPath cEnv rEnv typeEnv mods =
    match tcCache.TryGetValue(resolvedPath) with
    | true, cached -> cached
    | false, _ ->
        // ... existing logic ...
        let result = (mergedTypeEnv, mergedCEnv, mergedREnv, mergedMods)
        tcCache.[resolvedPath] <- result
        result
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single Prelude discovery path | 3-stage discovery (CWD/assembly/walk-up) | Already implemented | Works for `dotnet run` and published binary |
| No file import caching | Each import re-reads, re-parses, re-type-checks | Current state | Redundant work for diamond dependencies |

## Open Questions

1. **Should `--deps` load Prelude and type-check, or just parse?**
   - What we know: `FileImportDecl` is in the AST, so parsing alone suffices to collect the import graph
   - What's unclear: Should `--deps` report errors for missing/invalid files, or just show the tree?
   - Recommendation: Parse only (no type-check). Report file-not-found as warnings but still show the tree. This makes `--deps` fast and usable even with type errors.

2. **Should `--check` work with `--expr` too?**
   - What we know: `--emit-type --expr` already does type-check-only for expressions
   - What's unclear: Whether `--check --expr` should be supported or is redundant
   - Recommendation: Support `--check` only with `File`. For expressions, `--emit-type` already serves this purpose.

3. **Cache key: should it include file mtime?**
   - What we know: Within a single CLI invocation, files don't change
   - What's unclear: Whether future watch-mode or LSP would need mtime-based invalidation
   - Recommendation: For now, cache by absolute path only. Single invocation = no changes. Add mtime later if needed.

4. **What to cache exactly for TC vs Eval?**
   - What we know: TC returns `(TypeEnv, ConstructorEnv, RecordEnv, Map<string, ModuleExports>)`. Eval returns `(Env, Map<string, ModuleValueEnv>)`. These are the file's MERGED environments (caller + file's own).
   - What's unclear: Whether to cache merged or file-own-only results.
   - Recommendation: Cache the results as returned by the existing functions (merged). The first caller's context establishes the cache entry. Subsequent callers in the same diamond dependency pattern will have compatible contexts because imports are processed in order.

## Sources

### Primary (HIGH confidence)
- `src/LangThree/Cli.fs` -- Current Argu DU definition, 21 lines
- `src/LangThree/Program.fs` -- Full entry point, 243 lines, all mode dispatch
- `src/LangThree/Prelude.fs` -- Prelude loading, file import delegates, findPreludeDir, 262 lines
- `src/LangThree/TypeCheck.fs` -- resolveImportPath, fileImportTypeChecker delegate, FileImportDecl handling
- `src/LangThree/Eval.fs` -- fileImportEvaluator delegate, FileImportDecl eval
- `src/LangThree/Ast.fs` -- FileImportDecl AST node definition
- `survey/project-build-system-design.md` sections 4.1-4.4 -- Implementation sketches

### Secondary (MEDIUM confidence)
- Argu 6.2.5 NuGet package -- CliPrefix, MainCommand, Last attributes

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- Argu already in use, no new dependencies needed
- Architecture: HIGH -- Patterns directly derived from existing codebase (--emit-type mode is the template for --check)
- Pitfalls: HIGH -- Identified from actual code structure (Prelude loading order, parser choice, cache scope)

**Research date:** 2026-03-31
**Valid until:** 2026-05-01 (stable domain, no external dependencies changing)
