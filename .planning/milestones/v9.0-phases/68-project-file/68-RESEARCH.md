# Phase 68: Project File - Research

**Researched:** 2026-03-31
**Domain:** TOML parsing (Tomlyn), Argu subcommands, funproj.toml project file system
**Confidence:** HIGH

## Summary

Phase 68 adds `funproj.toml` project file support with `langthree build` and `langthree test` subcommands. The phase builds directly on Phase 67's completed CLI infrastructure (Argu, `--check` mode, `--prelude` flag, file caching). Three distinct technical problems need solving: (1) parsing TOML with the Tomlyn NuGet package, (2) restructuring the Argu CLI to support subcommands alongside the existing flat-flag interface, and (3) wiring `build`/`test` logic to the existing type-check and eval pipelines.

The current Argu setup uses a flat `CliArgs` DU with `CliPrefix.DoubleDash`. Subcommands in Argu require a new parent DU type using `CliPrefix.None` with nested `ParseResults<SubType>` fields. This is a significant restructuring of `Cli.fs` and the `Program.fs` dispatch chain. The critical design question is whether to merge the old flat flags into the new subcommand structure or keep them separate via a top-level discriminated union that routes either to legacy flat-flag mode or to subcommand mode.

Tomlyn 2.3.0 (released 2026-03-29) supports net10.0 directly, uses a `System.Text.Json`-style API (`TomlSerializer.Deserialize<T>()`), and maps `[[array_of_tables]]` to `List<T>` properties automatically. TOML key naming (snake_case like `[[executable]]`) vs F# property naming (PascalCase) requires either the `[TomlProperty("key")]` attribute or a `PropertyNamingPolicy` in `TomlSerializerOptions`. Since Tomlyn uses C#-style mutable classes, a thin F# wrapper module (`ProjectFile.fs`) is needed to bridge to idiomatic F# records.

**Primary recommendation:** Add Tomlyn 2.3.0 as a NuGet dependency. Create a new `ProjectFile.fs` with mutable POCO classes for TOML deserialization and an F# record `FunProjConfig` for the parsed result. Add a new top-level DU in `Cli.fs` to route between subcommand mode (`build`/`test`) and legacy flat-flag mode. Wire build targets to `--check` logic and test targets to the existing file eval pipeline.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Tomlyn | 2.3.0 | TOML 1.1 parsing and deserialization | Only mature TOML library for .NET; System.Text.Json-style API; net10.0 native target |
| Argu | 6.2.5 | CLI subcommand parsing | Already in use; supports nested `ParseResults<T>` for subcommands |
| System.IO | .NET 10 | File system operations for funproj.toml discovery | Already in use |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | .NET 10 | `[JsonPropertyName]` attributes reusable in Tomlyn | When TOML keys differ from F# property names |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Tomlyn | Tommy, CsToml | Tommy is older/less maintained; CsToml is newer but less tested |
| Tomlyn POCO deserialization | Tomlyn `TomlTable` untyped API | Untyped is more flexible but requires manual key lookups; POCO is safer |
| Argu subcommands | Manual argv parsing for build/test | Argu gives free help text, error messages, and type safety |

**Installation:**
```bash
dotnet add src/LangThree/LangThree.fsproj package Tomlyn --version 2.3.0
```

## Architecture Patterns

### Recommended Project Structure
```
src/LangThree/
├── Cli.fs              # Extended with Build/Test subcommand DU types
├── ProjectFile.fs      # NEW: TOML deserialization + FunProjConfig F# record
├── Program.fs          # Extended with build/test dispatch branches
└── ...existing files...
```

`ProjectFile.fs` must be added to `LangThree.fsproj` BEFORE `Cli.fs` (since `Cli.fs` doesn't depend on it, but `Program.fs` uses both — actually `Program.fs` uses it directly, so `ProjectFile.fs` can go between `Prelude.fs` and `Cli.fs` or between `Cli.fs` and `Program.fs`; it has no dependencies beyond `System.IO` and Tomlyn).

### Pattern 1: Tomlyn POCO Deserialization for funproj.toml

**What:** Define C#-style mutable classes for TOML deserialization, then convert to F# records
**When to use:** Every funproj.toml read

The TOML structure:
```toml
[project]
name = "funlexyacc"
prelude = "../LangThree/Prelude"

[[executable]]
name = "funlex"
main = "src/funlex/FunlexMain.fun"

[[test]]
name = "test-cset"
main = "tests/common/test_cset.fun"
```

The F# POCO classes and deserialization (Tomlyn requires mutable .NET classes with getters/setters; F# records with `[<CLIMutable>]` also work):

```fsharp
// Source: Tomlyn GitHub + NuGet 2.3.0 documentation
open Tomlyn

[<CLIMutable>]
type TomlProjectSection = {
    mutable name: string
    mutable prelude: string
}

[<CLIMutable>]
type TomlTarget = {
    mutable name: string
    mutable main: string
}

[<CLIMutable>]
type TomlFunProj = {
    mutable project: TomlProjectSection
    mutable executable: System.Collections.Generic.List<TomlTarget>
    mutable test: System.Collections.Generic.List<TomlTarget>
}
```

Deserialization:
```fsharp
let raw = TomlSerializer.Deserialize<TomlFunProj>(tomlText)
```

**Key insight:** Tomlyn maps TOML `[project]` to the `project` field, and `[[executable]]` to the `executable` field (as `List<TomlTarget>`). Field names in the POCO must match TOML keys exactly (lowercase). `[<CLIMutable>]` on F# records allows Tomlyn's reflection-based setter to work.

**Null safety:** `TomlSerializer.Deserialize<T>` returns a T instance. Missing optional sections (`prelude` not set, empty `[[test]]`) result in `null` strings and empty/null lists. Always null-check.

### Pattern 2: F# Record Wrapper

**What:** Convert raw TOML POCOs to idiomatic F# records immediately after parsing
**When to use:** After every `TomlSerializer.Deserialize` call

```fsharp
type TargetConfig = {
    Name: string
    Main: string
}

type FunProjConfig = {
    ProjectName: string
    PreludePath: string option   // None = not set in TOML
    Executables: TargetConfig list
    Tests: TargetConfig list
}

let parseFunProj (tomlText: string) (projDir: string) : FunProjConfig =
    let raw = TomlSerializer.Deserialize<TomlFunProj>(tomlText)
    {
        ProjectName = if raw.project <> Unchecked.defaultof<_> && raw.project.name <> null then raw.project.name else ""
        PreludePath = 
            if raw.project <> Unchecked.defaultof<_> && raw.project.prelude <> null && raw.project.prelude <> ""
            then Some (System.IO.Path.GetFullPath(System.IO.Path.Combine(projDir, raw.project.prelude)))
            else None
        Executables = 
            if raw.executable = null then []
            else raw.executable |> Seq.map (fun t -> { Name = t.name; Main = System.IO.Path.GetFullPath(System.IO.Path.Combine(projDir, t.main)) }) |> Seq.toList
        Tests = 
            if raw.test = null then []
            else raw.test |> Seq.map (fun t -> { Name = t.name; Main = System.IO.Path.GetFullPath(System.IO.Path.Combine(projDir, t.main)) }) |> Seq.toList
    }
```

**Important:** Resolve `main` paths relative to the `funproj.toml` directory (not CWD). Store absolute paths in `FunProjConfig` to avoid CWD-relative confusion during build/test execution.

### Pattern 3: Argu Subcommand Structure

**What:** Extend Argu DU types to support `build` and `test` as subcommands
**When to use:** The `build <name>?` and `test <name>?` commands

Argu subcommands require: a sub-DU per subcommand (with `IArgParserTemplate`), and a parent DU that uses `CliPrefix.None` + `ParseResults<SubType>` fields.

The challenge: the existing `CliArgs` is flat with `CliPrefix.DoubleDash`. Subcommands are `CliPrefix.None`. These cannot coexist in the same DU. The solution is a two-level structure:

```fsharp
// Source: Argu tutorial + existing Cli.fs pattern
// Sub-DU for build subcommand
[<CliPrefix(CliPrefix.None)>]
type BuildArgs =
    | [<MainCommand>] Target of name: string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "name of the executable target to build (optional)"

// Sub-DU for test subcommand
[<CliPrefix(CliPrefix.None)>]
type TestArgs =
    | [<MainCommand>] Target of name: string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "name of the test target to run (optional)"

// Top-level DU -- routes to subcommand or legacy flat-flag mode
[<CliPrefix(CliPrefix.DoubleDash)>]
type CliArgs =
    | [<AltCommandLine("-e")>] Expr of expression: string
    | Emit_Tokens
    | Emit_Ast
    | Emit_Type
    | Check
    | Deps
    | Prelude of path: string
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Test of ParseResults<TestArgs>
    | [<MainCommand; Last>] File of filename: string
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
            | Build _ -> "type-check all [[executable]] targets in funproj.toml"
            | Test _ -> "run all [[test]] targets in funproj.toml"
            | File _ -> "evaluate program from file"
```

**Key insight:** Mixing `CliPrefix.DoubleDash` on the parent and `CliPrefix.None` on individual subcommand cases is valid in Argu. The `[<CliPrefix(CliPrefix.None)>]` attribute on the `Build` and `Test` cases overrides the DU-level prefix for those specific cases. This is exactly how Argu's tutorial examples work (e.g., `GitArgs` with `Clean of ParseResults<CleanArgs>` where Clean has `CliPrefix.None`).

**Dispatch in Program.fs:**
```fsharp
elif results.Contains Build then
    let buildResults = results.GetResult Build
    let targetName = buildResults.TryGetResult BuildArgs.Target
    // ... load funproj.toml, filter targets, run --check on each
elif results.Contains Test then
    let testResults = results.GetResult Test
    let targetName = testResults.TryGetResult TestArgs.Target
    // ... load funproj.toml, filter targets, run eval on each
```

### Pattern 4: funproj.toml Discovery

**What:** Find `funproj.toml` in CWD when `langthree build` or `langthree test` is invoked
**When to use:** Every build/test command

```fsharp
let findFunProj () : string option =
    let candidate = System.IO.Path.GetFullPath("funproj.toml")
    if System.IO.File.Exists candidate then Some candidate
    else None
```

Discovery is CWD-only (no walk-up). The user must be in the project directory. This matches Cargo and npm behavior.

### Pattern 5: Prelude Priority for Build/Test

**What:** Implement PROJ-06 — `[project].prelude` has lowest priority
**When to use:** `build` and `test` subcommand dispatch

Priority chain (highest to lowest):
1. `--prelude` flag (already extracted from Argu results in Phase 67)
2. `LANGTHREE_PRELUDE` env var (already in `resolvePreludeDir`)
3. `[project].prelude` from `funproj.toml` (new in Phase 68)
4. auto-discovery (existing 3-stage `findPreludeDir`)

Implementation: Pass `FunProjConfig.PreludePath` as a fallback into `resolvePreludeDir`, OR call `resolvePreludeDir` first and only fall through to `funproj.toml` prelude when it returns "".

The cleanest approach is to extend `resolvePreludeDir` to accept an additional `projPrelude: string option` parameter:
```fsharp
let resolvePreludeDir (explicitPath: string option) (projPrelude: string option) : string =
    match explicitPath with
    | Some path -> if Directory.Exists path then path else failwithf "..."
    | None ->
        match System.Environment.GetEnvironmentVariable("LANGTHREE_PRELUDE") with
        | null | "" ->
            match projPrelude with
            | Some p when p <> "" && Directory.Exists p -> p
            | _ -> findPreludeDir()
        | envPath -> if Directory.Exists envPath then envPath else failwithf "..."
```

For non-project (flat-flag) invocations, pass `projPrelude = None`.

### Pattern 6: Build Target Execution (PROJ-02, PROJ-03)

**What:** Run `--check` logic on each target's `main` file
**When to use:** `langthree build` and `langthree build <name>`

Reuse the exact same pipeline as the `--check` branch in Program.fs (lines 128-153):
```
TypeCheck.currentTypeCheckingFile <- main
parseModuleFromString -> typeCheckModuleWithPrelude -> report OK/error
```

For multiple targets, run sequentially; collect failures. Return exit code 1 if ANY target fails.

```fsharp
let runBuildTarget (prelude: PreludeResult) (target: TargetConfig) : int =
    if System.IO.File.Exists target.Main then
        try
            let input = System.IO.File.ReadAllText target.Main
            TypeCheck.currentTypeCheckingFile <- target.Main
            let m = parseModuleFromString input target.Main
            match TypeCheck.typeCheckModuleWithPrelude prelude.CtorEnv prelude.RecEnv prelude.TypeEnv prelude.Modules m with
            | Ok (warnings, _, _, _, _) ->
                for w in warnings do eprintfn "Warning: %s" (formatDiagnostic w)
                eprintfn "OK: %s (%d warnings)" target.Name (List.length warnings)
                0
            | Error diag ->
                eprintfn "Error in %s: %s" target.Name (formatDiagnostic diag)
                1
        with ex ->
            eprintfn "Error in %s: %s" target.Name ex.Message
            1
    else
        eprintfn "Target file not found: %s" target.Main
        1
```

### Pattern 7: Test Target Execution (PROJ-04, PROJ-05)

**What:** Run eval pipeline on each test target's `main` file
**When to use:** `langthree test` and `langthree test <name>`

Reuse the existing file eval pipeline from Program.fs (lines 254-311). The test target's `main` file is treated as a regular `.fun` file — type-check then eval, print last binding value.

Key: `Eval.scriptArgs` should be empty for test targets (no argv passed through).

### Anti-Patterns to Avoid

- **Using `Tomlyn.TomlTable` untyped API:** Works but requires manual key lookup, null handling, and type casting. Use typed POCO deserialization instead.
- **Resolving `main` paths relative to CWD:** `funproj.toml` `main` paths must be resolved relative to the `funproj.toml` directory, not CWD. Resolve to absolute paths immediately on parse.
- **Walking up directories to find `funproj.toml`:** Only look in CWD. Walking up adds complexity and unexpected behavior.
- **Using F# records with immutable properties for Tomlyn POCO:** Tomlyn's reflection-based deserializer needs mutable setters. Use `[<CLIMutable>]` on F# records, or use mutable classes/structs. Without this, deserialization silently produces default values.
- **Merging `prelude` from all three sources before calling `loadPrelude`:** The priority chain must be explicit: CLI flag → env var → toml → auto-discovery. Don't concatenate all three into a list and pick first non-null.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| TOML parsing | Custom TOML parser | Tomlyn 2.3.0 | TOML has subtle edge cases (multi-line strings, datetime, unicode); Tomlyn is tested against TOML 1.1 compliance suite |
| Array of tables mapping | Manual `TomlTable` traversal | `[<CLIMutable>]` POCO + `TomlSerializer.Deserialize<T>` | Automatic type-safe mapping; one line vs 20 lines of manual code |
| CLI subcommand routing | Manual `argv.[0]` inspection | Argu `ParseResults<SubType>` | Free help text, error messages, and type-safe argument extraction |
| `main` path resolution | Custom path logic | `Path.GetFullPath(Path.Combine(projDir, t.main))` | Handles absolute paths, relative paths, and `../` correctly |

**Key insight:** The TOML POCO deserialization approach (Tomlyn + `[<CLIMutable>]` F# records) collapses the entire funproj.toml parsing problem into ~30 lines of idiomatic F#.

## Common Pitfalls

### Pitfall 1: `[<CLIMutable>]` Required for Tomlyn POCO Deserialization
**What goes wrong:** Tomlyn silently returns default values (null strings, empty lists) when deserializing into F# records without `[<CLIMutable>]`.
**Why it happens:** Tomlyn uses reflection to set property values via setters. F# record fields are immutable by default — no setter exists, so nothing is set.
**How to avoid:** Always annotate TOML POCO types with `[<CLIMutable>]`.
**Warning signs:** `raw.project` is null or `raw.project.name` is null after `Deserialize` even when the TOML has the field.

### Pitfall 2: Argu `CliPrefix.None` on Individual Cases, Not the DU
**What goes wrong:** Applying `[<CliPrefix(CliPrefix.None)>]` at the DU level (on `BuildArgs`) but trying to mix with `CliPrefix.DoubleDash` on the parent `CliArgs` DU.
**Why it happens:** Misreading the Argu documentation. The `CliPrefix.None` that matters for subcommand recognition is on the CASE in the parent DU (`| [<CliPrefix(CliPrefix.None)>] Build of ParseResults<BuildArgs>`), not on `BuildArgs` itself.
**How to avoid:** Put `[<CliPrefix(CliPrefix.None)>]` on the `Build` and `Test` cases inside `CliArgs`, not on `BuildArgs`/`TestArgs` type declaration.
**Warning signs:** `langthree build` is interpreted as `File "build"` instead of the `Build` subcommand.

### Pitfall 3: TOML `[[executable]]` Maps to Field Named `executable` (Not `executables`)
**What goes wrong:** Naming the POCO field `executables` (plural) when the TOML key is `[[executable]]` (singular).
**Why it happens:** Natural English pluralization instinct.
**How to avoid:** TOML key and F# field name must match exactly. `[[executable]]` → field `executable`. `[[test]]` → field `test`.
**Warning signs:** `raw.executable` is null after deserialization even when TOML has `[[executable]]` entries.

### Pitfall 4: Missing `open Tomlyn` in ProjectFile.fs
**What goes wrong:** `TomlSerializer` not found at compile time.
**Why it happens:** Tomlyn is a separate namespace from the F# standard library.
**How to avoid:** Add `open Tomlyn` at top of `ProjectFile.fs`. Add the package reference to `LangThree.fsproj` before compiling.

### Pitfall 5: `resolvePreludeDir` Signature Change Breaks Existing Call Sites
**What goes wrong:** Adding `projPrelude: string option` parameter to `resolvePreludeDir` breaks existing callers in `Program.fs`.
**Why it happens:** F# functions are not overloaded; changing the signature breaks all call sites.
**How to avoid:** Either (a) add `projPrelude = None` to existing call sites, or (b) add a new `resolvePreludeDirWithProj` function and keep the old one as a compatibility wrapper calling the new one with `None`. Option (b) avoids touching Program.fs's existing prelude extraction code.
**Warning signs:** Build error "resolvePreludeDir is not defined" or "wrong number of arguments" after the signature change.

### Pitfall 6: File Path for `TypeCheck.currentTypeCheckingFile` Must Be Absolute
**What goes wrong:** Type errors in imported files use wrong relative paths in error messages.
**Why it happens:** `currentTypeCheckingFile` is used for relative `open` path resolution. If it's a relative path, `resolveImportPath` computes wrong absolute paths.
**How to avoid:** Always set `TypeCheck.currentTypeCheckingFile <- System.IO.Path.GetFullPath(target.Main)` when type-checking build targets. The `FunProjConfig.Executables` list should already store absolute paths (Pattern 2 above).

### Pitfall 7: flt Tests for Subcommands Require funproj.toml in a Temp Directory
**What goes wrong:** flt tests can't assume a `funproj.toml` exists in the test runner's CWD.
**Why it happens:** `langthree build` looks for `funproj.toml` in CWD. flt tests run in some CWD that doesn't have `funproj.toml`.
**How to avoid:** Use `bash -c 'cd /tmp/... && langthree build'` pattern in flt test commands, similar to how `cli-deps.flt` creates temp files with `printf`. Create a temp directory with `funproj.toml` and target `.fun` files, then run `langthree build` from that directory.

## Code Examples

### Complete ProjectFile.fs Module

```fsharp
// Source: Tomlyn 2.3.0 API + F# CLIMutable pattern
module ProjectFile

open System.IO
open Tomlyn

// TOML POCO types (must be mutable for Tomlyn reflection-based deserialization)
[<CLIMutable>]
type TomlProjectSection = {
    mutable name: string
    mutable prelude: string
}

[<CLIMutable>]
type TomlTarget = {
    mutable name: string
    mutable main: string
}

[<CLIMutable>]
type TomlFunProj = {
    mutable project: TomlProjectSection
    mutable executable: System.Collections.Generic.List<TomlTarget>
    mutable test: System.Collections.Generic.List<TomlTarget>
}

// Idiomatic F# records for use in Program.fs
type TargetConfig = {
    Name: string
    Main: string   // absolute path
}

type FunProjConfig = {
    ProjectName: string
    PreludePath: string option   // None = not set in TOML; absolute path if set
    Executables: TargetConfig list
    Tests: TargetConfig list
}

let private makeTarget (projDir: string) (t: TomlTarget) : TargetConfig = {
    Name = if t.name <> null then t.name else ""
    Main = if t.main <> null then Path.GetFullPath(Path.Combine(projDir, t.main)) else ""
}

/// Parse a funproj.toml file. projDir is the directory containing funproj.toml (for relative path resolution).
let parseFunProj (tomlText: string) (projDir: string) : FunProjConfig =
    let raw = TomlSerializer.Deserialize<TomlFunProj>(tomlText)
    let projSection = raw.project   // may be Unchecked.defaultof if [project] absent
    {
        ProjectName =
            if box projSection <> null && projSection.name <> null then projSection.name else ""
        PreludePath =
            if box projSection <> null && projSection.prelude <> null && projSection.prelude <> ""
            then Some (Path.GetFullPath(Path.Combine(projDir, projSection.prelude)))
            else None
        Executables =
            if raw.executable = null then []
            else raw.executable |> Seq.map (makeTarget projDir) |> Seq.toList
        Tests =
            if raw.test = null then []
            else raw.test |> Seq.map (makeTarget projDir) |> Seq.toList
    }

/// Find funproj.toml in the current working directory.
let findFunProj () : string option =
    let candidate = Path.GetFullPath("funproj.toml")
    if File.Exists candidate then Some candidate else None

/// Load and parse funproj.toml. Returns Error message string on failure.
let loadFunProj (path: string) : Result<FunProjConfig, string> =
    try
        let text = File.ReadAllText path
        let projDir = Path.GetDirectoryName path
        Ok (parseFunProj text projDir)
    with ex ->
        Error (sprintf "Failed to parse %s: %s" path ex.Message)
```

### Argu Subcommand DU Types (Cli.fs additions)

```fsharp
// Source: Argu 6.2.5 tutorial + existing Cli.fs
// Add BEFORE CliArgs definition

[<CliPrefix(CliPrefix.None)>]
type BuildArgs =
    | [<MainCommand>] Target of name: string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "name of the executable target to build (omit for all)"

[<CliPrefix(CliPrefix.None)>]
type TestArgs =
    | [<MainCommand>] Target of name: string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "name of the test target to run (omit for all)"
```

### Program.fs Build Dispatch Branch

```fsharp
// Source: derived from existing --check branch (Program.fs lines 128-153)
elif results.Contains Build then
    let buildResults = results.GetResult Build
    let targetName = buildResults.TryGetResult BuildArgs.Target
    match ProjectFile.findFunProj() with
    | None ->
        eprintfn "Error: funproj.toml not found in current directory"
        1
    | Some projPath ->
        match ProjectFile.loadFunProj projPath with
        | Error msg -> eprintfn "%s" msg; 1
        | Ok config ->
            let targets =
                match targetName with
                | None -> config.Executables
                | Some name ->
                    match config.Executables |> List.tryFind (fun t -> t.Name = name) with
                    | Some t -> [t]
                    | None -> eprintfn "Error: no executable target named '%s'" name; []
            if targets.IsEmpty && targetName.IsSome then 1
            else
                // Resolve prelude with funproj.toml prelude as lowest priority
                let preludePath =
                    if results.Contains Prelude then Some (results.GetResult Prelude)
                    else None
                let prelude = Prelude.loadPrelude preludePath (config.PreludePath)
                let exitCodes = targets |> List.map (runBuildTarget prelude)
                if List.exists (fun c -> c <> 0) exitCodes then 1 else 0
```

### flt Test Pattern for Subcommands

```
// Test: langthree build type-checks all [[executable]] targets in funproj.toml
// --- Command: bash -c 'DIR=$(mktemp -d) && cat > $DIR/funproj.toml << EOF
// [project]
// name = "test"
// [[executable]]
// name = "main"
// main = "main.fun"
// EOF
// printf "let x : int = 42\n" > $DIR/main.fun && LT=.../LangThree && cd $DIR && $LT build 2>&1'
// --- Output:
// CONTAINS: OK: main
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No TOML support in .NET | Tomlyn 2.x with System.Text.Json-style API | 2023+ | Clean typed deserialization, net10.0 native |
| Argu flat flags only | Argu subcommands via `ParseResults<T>` | Argu 3.0+ | Git-style subcommands with independent arg schemas |
| Single file entry point | funproj.toml multi-target | Phase 68 | Systematic project management |

**Deprecated/outdated:**
- `Tomlyn.TomlTable` untyped API: Still works but verbose; prefer POCO deserialization for structured configs
- Tomlyn 0.x / 1.x API: Older API used `Toml.Parse()` directly; current 2.x uses `TomlSerializer.Deserialize<T>()`

## Open Questions

1. **Does Argu support optional MainCommand (target name present or absent)?**
   - What we know: `[<MainCommand>]` with `string` requires the argument when the subcommand is present. `string list` allows zero or more. Using `TryGetResult` handles the "not provided" case.
   - What's unclear: Whether `[<MainCommand>]` with `string option` works in Argu 6.2.5.
   - Recommendation: Use `string` (not `string option`) for `Target`, and use `TryGetResult BuildArgs.Target` in Program.fs dispatch. If `TryGetResult` returns `None`, run all targets.

2. **Should `langthree build` without `funproj.toml` be an error or fall back to something?**
   - What we know: The requirements say `langthree build` in a directory with `funproj.toml`. No fallback is specified.
   - Recommendation: Exit 1 with clear error message "funproj.toml not found in current directory". Don't fall back to anything.

3. **Should `[project].prelude` path be relative to `funproj.toml` directory or to CWD?**
   - What we know: Other paths in `funproj.toml` (`main`) are relative to the project file directory (Cargo convention).
   - Recommendation: Resolve `prelude` relative to `funproj.toml` directory. This is consistent with `main` path resolution and with Cargo conventions. Already handled in `parseFunProj` (Pattern 2).

4. **Should `loadPrelude` signature change to accept `projPrelude`?**
   - What we know: `loadPrelude` currently takes `(explicitPath: string option)` and calls `resolvePreludeDir`. Adding another parameter requires updating all call sites.
   - Recommendation: Add a new `resolvePreludeDirWithProj (explicitPath: string option) (projPrelude: string option)` function. The existing `resolvePreludeDir` (now a 1-arg function) becomes a wrapper: `resolvePreludeDir e = resolvePreludeDirWithProj e None`. Then `loadPrelude` can accept an optional second parameter or have a companion `loadPreludeWithProj` variant.

## Sources

### Primary (HIGH confidence)
- `src/LangThree/Cli.fs` - Current Argu DU definition (Phase 67 complete state)
- `src/LangThree/Prelude.fs` - `loadPrelude`, `resolvePreludeDir`, `tcCache`/`evalCache` (Phase 67 complete state)
- `src/LangThree/Program.fs` - Full dispatch chain with `--check` branch as template for build targets
- `survey/project-build-system-design.md` sections 3.1, 4.5, 6 - funproj.toml format design
- NuGet Gallery Tomlyn 2.3.0 - Version 2.3.0 confirmed current stable, net10.0 supported
- Argu tutorial (fsprojects.github.io) - Subcommand `ParseResults<T>` pattern with `CliPrefix.None`

### Secondary (MEDIUM confidence)
- Tomlyn GitHub README - `TomlSerializer.Deserialize<T>` API, property naming policy, `[TomlProperty]` attribute
- learnxbyexample.com/fsharp/command-line-subcommands - Complete F# Argu subcommand example

### Tertiary (LOW confidence)
- ssojet.com Tomlyn guide - `[<CLIMutable>]`/`[TomlProperty]` attribute usage (not official docs)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Tomlyn confirmed current; Argu already in use; POCO pattern well-documented
- Architecture: HIGH - Pattern derived directly from existing codebase (--check branch = build target runner); Argu subcommand structure from official tutorial
- Pitfalls: HIGH - `[<CLIMutable>]` requirement is a known F# + Tomlyn footgun; Argu prefix behavior verified from existing code patterns

**Research date:** 2026-03-31
**Valid until:** 2026-05-01 (Tomlyn 2.3.x stable; Argu 6.2.x stable; no fast-moving dependencies)
