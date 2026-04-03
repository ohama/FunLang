# Phase 82: CLI Integration - Research

**Researched:** 2026-04-03
**Domain:** F# CLI flag addition with JSON serialization via System.Text.Json
**Confidence:** HIGH

## Summary

Phase 82 adds `--emit-typed-ast` to the existing Argu-based CLI and serializes the `TypedModule` (from Phase 81) to JSON on stdout. The phase is purely integration work: the type-checking pipeline already exists (`ExportApi.typeCheckFile`), the data structures are defined (`TypedModule`, `Map<Ast.Span, Type.Type>`, `Map<string, Scheme>`), and the CLI argument infrastructure is in place (`Cli.fs` + `Program.fs`).

The serialization layer requires manual JSON construction because F# discriminated unions (`Type.Type`, `Scheme`) do not have built-in `System.Text.Json` serialization support. The recommended approach is to write a function `serializeTypedModule : TypedModule -> string` in `Program.fs` (or a new thin `JsonExport.fs`) that converts the two required outputs — per-expression `span -> type` entries and top-level binding types — into a JSON object using `System.Text.Json.Nodes` (the mutable `JsonObject`/`JsonArray` API) or manual string building. `System.Text.Json` is built into .NET 10 with no extra NuGet packages.

Error handling follows the exact pattern of the existing `--check` and `--emit-type` branches in `Program.fs`: call `ExportApi.typeCheckFile` inside a `try…with` block, catch the `failwith` exception, print the message to `stderr`, and return exit code 1. On success, print the JSON to `stdout` and return exit code 0. The "no malformed JSON on type error" requirement is satisfied automatically because the JSON is only emitted on the success path.

**Primary recommendation:** Add `Emit_Typed_Ast` to `Cli.fs`, add a handler branch in `Program.fs` (after the existing `Emit_Type` file branch), call `ExportApi.typeCheckFile`, serialize `TypedModule` to JSON using `System.Text.Json.Nodes`, print to stdout, and add flt integration tests.

## Standard Stack

This phase is entirely internal to the FunLang compiler. No new NuGet packages are required.

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `ExportApi.typeCheckFile` | Phase 81 | Single entry point returning `TypedModule` | Already implemented and tested; designed for this exact use case |
| `System.Text.Json` | .NET 10 built-in | JSON serialization | Built into BCL; no extra package; `JsonObject`/`JsonArray` node API works naturally for manually mapping DUs |
| `Argu` | 6.2.5 | CLI argument parsing | Already used; adding one DU case to `CliArgs` follows the exact existing pattern |
| `Cli.fs` | existing | CLI union type definitions | All flag definitions live here; `Emit_Typed_Ast` goes here |
| `Program.fs` | existing | Main entry point with flag dispatch | New branch follows the `Emit_Type + File` pattern exactly |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `Type.formatTypeNormalized` | existing | Render `Type.Type` as human-readable string | Use as the `"type"` value in JSON annotations (keeps JSON output stable and readable) |
| `Type.formatSchemeNormalized` | existing | Render `Scheme` as human-readable string | Use as the `"type"` value in top-level bindings JSON |
| `Ast.formatSpan` | existing | Render `Ast.Span` as `"file:line:col-col"` string | Use as the key or sub-fields in the annotation entries |
| `flt` integration tests | existing | Command-line test harness | Add `tests/flt/emit/typed-ast/` tests following existing flt conventions |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `System.Text.Json.Nodes` (JsonObject/JsonArray) | `Newtonsoft.Json` | Newtonsoft requires an extra NuGet package; System.Text.Json is already in the BCL and sufficient for this use case |
| `System.Text.Json.Nodes` (JsonObject/JsonArray) | `System.Text.Json.JsonSerializer` with custom converters | Custom converters for F# DUs are complex; manual node building is straightforward and produces exactly the JSON shape required |
| String formatting the JSON by hand | `System.Text.Json` | Hand-rolled JSON is error-prone (escaping); use the library |
| New `JsonExport.fs` module | Add serialization inline in `Program.fs` | Either works; inline in `Program.fs` is simpler for a single flag; a separate module is better if more export formats are anticipated |

**Installation:** No packages to install.

## Architecture Patterns

### File Changes Required

```
src/FunLang/
├── Cli.fs        — ADD Emit_Typed_Ast case to CliArgs DU
└── Program.fs    — ADD handler branch for Emit_Typed_Ast + File
```

No new files are strictly required. The serialization function can live in `Program.fs` as a private `let` binding before the `main` function.

### Pattern 1: Adding a Flag to Argu CliArgs

**What:** Add a new DU case with `[<CliPrefix(CliPrefix.DoubleDash)>]` already set at the type level. The case name uses underscores which Argu converts to hyphens in the flag name automatically.

**When to use:** Every new CLI flag follows this pattern.

**Example:**
```fsharp
// Source: Cli.fs — existing CliArgs DU pattern
[<CliPrefix(CliPrefix.DoubleDash)>]
type CliArgs =
    | Emit_Tokens
    | Emit_Ast
    | Emit_Type
    | Emit_Typed_Ast          // <-- Argu maps this to --emit-typed-ast
    | Check
    // ...
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Emit_Typed_Ast -> "emit typed AST as JSON to stdout"
            // ...
```

### Pattern 2: Handler Branch in Program.fs

**What:** Add an `elif results.Contains Emit_Typed_Ast && results.Contains File then` branch in `Program.fs`, following the exact structure of the `Emit_Type + File` branch (lines 329–357 in current Program.fs).

**When to use:** All file-based flag handlers follow this pattern.

**Example:**
```fsharp
// Source: Program.fs — Emit_Type with File branch (lines 329–357) as template
elif results.Contains Emit_Typed_Ast && results.Contains File then
    let filename = results.GetResult File
    if File.Exists filename then
        try
            let typed = ExportApi.typeCheckFile filename
            let json = serializeTypedModule typed
            printfn "%s" json
            0
        with ex ->
            eprintfn "Error: %s" ex.Message
            1
    else
        eprintfn "File not found: %s" filename
        1
```

### Pattern 3: JSON Serialization with System.Text.Json.Nodes

**What:** Build `JsonObject` manually by mapping F# data structures to JSON nodes. Uses `System.Text.Json.Nodes` namespace.

**When to use:** When serializing F# DUs or records that don't serialize automatically.

**Example:**
```fsharp
open System.Text.Json.Nodes

let private serializeType (ty: Type.Type) : string =
    Type.formatTypeNormalized ty

let private serializeSpan (span: Ast.Span) : JsonObject =
    let obj = JsonObject()
    obj["file"] <- JsonValue.Create(span.FileName)
    obj["startLine"] <- JsonValue.Create(span.StartLine)
    obj["startCol"] <- JsonValue.Create(span.StartColumn)
    obj["endLine"] <- JsonValue.Create(span.EndLine)
    obj["endCol"] <- JsonValue.Create(span.EndColumn)
    obj

let private serializeTypedModule (tm: ExportApi.TypedModule) : string =
    let root = JsonObject()

    // per-expression annotations: array of {span, type} objects
    let annotations = JsonArray()
    for kv in tm.AnnotationMap do
        let entry = JsonObject()
        entry["span"] <- serializeSpan kv.Key
        entry["type"] <- JsonValue.Create(serializeType kv.Value)
        annotations.Add(entry)
    root["annotations"] <- annotations

    // top-level binding types: object name -> type string
    let bindings = JsonObject()
    for kv in tm.BindingEnv do
        bindings[kv.Key] <- JsonValue.Create(Type.formatSchemeNormalized kv.Value)
    root["bindings"] <- bindings

    root.ToJsonString()
```

### Pattern 4: flt Integration Test for Emit Flags

**What:** `.flt` files under `tests/flt/emit/` specify a command and expected output. The test harness compares stdout.

**When to use:** All CLI emit flags have corresponding flt tests.

**Example (from existing `tests/flt/emit/ast-expr/ast-expr-tuple.flt`):**
```
// Test: --emit-ast for Tuple expression
// --- Command: src/FunLang/bin/Release/net10.0/fn --emit-ast --expr '(1, true, "hi")'
// --- Output:
Tuple [Number 1, Bool true, String "hi"]
```

For `--emit-typed-ast`, create `tests/flt/emit/typed-ast/` with tests verifying:
1. A simple file exits 0 and produces valid JSON (check `"annotations"` and `"bindings"` keys).
2. A file with a type error exits non-zero with a message on stderr and no output on stdout.

### Anti-Patterns to Avoid

- **Printing JSON to stderr:** The flag must print JSON to stdout and errors to stderr, matching the requirement "prints valid JSON" on stdout.
- **Emitting JSON even on type error:** The success path (`ExportApi.typeCheckFile` returns) is where JSON is emitted. The `failwith` from `ExportApi.typeCheckFile` goes to the `with ex` handler, which prints `ex.Message` to stderr. Never emit JSON in the error path.
- **Using `JsonSerializer.Serialize` directly on F# DUs:** F# discriminated unions are not automatically serializable with `System.Text.Json`; use manual `JsonObject` construction.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization | Manual string concatenation | `System.Text.Json.Nodes.JsonObject` | Handles escaping, Unicode, nesting; built-in to .NET 10 |
| Type-to-string rendering | Custom `formatType` | `Type.formatTypeNormalized` | Already exists; produces normalized, stable output |
| CLI flag parsing | Manual `argv` inspection | Argu | Already used; adding a DU case is trivial |
| Type-checking a file | Inline type-check pipeline | `ExportApi.typeCheckFile` | Phase 81 provides this exact entry point |

**Key insight:** Every component needed already exists. Phase 82 is integration work: wire existing pieces together in `Program.fs`.

## Common Pitfalls

### Pitfall 1: JSON on Error Path

**What goes wrong:** If the `try…with` block catches a type error from `ExportApi.typeCheckFile` and the code still tries to serialize, it may emit partial or malformed JSON, violating requirement 3 ("exits non-zero with an error message, not malformed JSON").

**Why it happens:** The developer forgets that `ExportApi.typeCheckFile` raises on type errors.

**How to avoid:** Never call `serializeTypedModule` in the `with ex` branch. The error handler should only do `eprintfn "Error: %s" ex.Message; 1`.

**Warning signs:** Test for type-error file exits 0, or stdout contains JSON even when there is a type error.

### Pitfall 2: Branch Ordering in Program.fs

**What goes wrong:** The `Emit_Typed_Ast` branch is placed after the catch-all `File` branch, so it is never reached.

**Why it happens:** The `elif` chain in `Program.fs` has a final `elif results.Contains File then` that catches any file invocation. New flag+file branches must come before this catch-all.

**How to avoid:** Place the new branch before the generic `elif results.Contains File then` block (currently around line 423 in Program.fs). Follow the same ordering as `Emit_Type + File` (line 329).

**Warning signs:** Running `fn --emit-typed-ast file.fun` evaluates and runs the file instead of emitting JSON.

### Pitfall 3: Argu Union Case Name Mismatch

**What goes wrong:** Naming the union case `EmitTypedAst` (camelCase) produces `--emit-typed-ast` in some Argu versions but `--emittypedast` in others (behavior depends on version).

**Why it happens:** Argu converts underscores to hyphens. The existing pattern in `Cli.fs` consistently uses underscores (`Emit_Tokens`, `Emit_Filtered_Tokens`, `Emit_Ast`, `Emit_Type`).

**How to avoid:** Name the new case `Emit_Typed_Ast` (with underscores), consistent with all existing cases.

**Warning signs:** `--help` shows `--emittypedast` instead of `--emit-typed-ast`.

### Pitfall 4: AnnotationMap Contains Prelude/Builtin Spans

**What goes wrong:** The `AnnotationMap` contains annotations for prelude expressions (spans from `<unknown>` or prelude files), producing a very large JSON output dominated by internal entries.

**Why it happens:** `Bidir.annotationMap` is populated for every expression type-checked in the call to `typeCheckModuleWithPrelude`, including expressions in imported prelude modules.

**How to avoid:** Filter `AnnotationMap` entries to only those whose `span.FileName` matches the user file before serializing. This is analogous to how `Program.fs`'s `--emit-type` branch filters `typeEnv` to exclude `initialTypeEnv` and `prelude.TypeEnv` entries.

**Warning signs:** JSON output is very large; span file names include paths to `Prelude/*.fun` files.

## Code Examples

### Adding Emit_Typed_Ast to Cli.fs

```fsharp
// Source: Cli.fs existing pattern
[<CliPrefix(CliPrefix.DoubleDash)>]
type CliArgs =
    | [<AltCommandLine("-e")>] Expr of expression: string
    | Emit_Tokens
    | Emit_Filtered_Tokens
    | Emit_Ast
    | Emit_Type
    | Emit_Typed_Ast          // NEW: maps to --emit-typed-ast
    | Check
    | Deps
    | Prelude of path: string
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Test of ParseResults<TestArgs>
    | [<MainCommand; Last>] File of filename: string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Emit_Typed_Ast -> "emit typed AST as JSON (span->type map + top-level bindings)"
            // ... existing cases unchanged
```

### serializeTypedModule Function

```fsharp
// Source: System.Text.Json.Nodes — .NET 10 built-in, no NuGet required
open System.Text.Json.Nodes

let private serializeTypedModule (userFile: string) (tm: ExportApi.TypedModule) : string =
    let absUserFile = System.IO.Path.GetFullPath(userFile)
    let root = JsonObject()

    // annotations: per-expression span -> type, filtered to user file only
    let annotations = JsonArray()
    for kv in tm.AnnotationMap do
        if kv.Key.FileName = absUserFile then
            let entry = JsonObject()
            let span = JsonObject()
            span["startLine"] <- JsonValue.Create(kv.Key.StartLine)
            span["startCol"] <- JsonValue.Create(kv.Key.StartColumn)
            span["endLine"] <- JsonValue.Create(kv.Key.EndLine)
            span["endCol"] <- JsonValue.Create(kv.Key.EndColumn)
            entry["span"] <- span
            entry["type"] <- JsonValue.Create(Type.formatTypeNormalized kv.Value)
            annotations.Add(entry)
    root["annotations"] <- annotations

    // bindings: top-level user bindings only (exclude builtins and prelude)
    let bindings = JsonObject()
    for kv in tm.BindingEnv do
        if not (Map.containsKey kv.Key tm.BuiltinSchemes) then
            bindings[kv.Key] <- JsonValue.Create(Type.formatSchemeNormalized kv.Value)
    root["bindings"] <- bindings

    root.ToJsonString()
```

### flt Integration Test for Success Case

```
// Test: --emit-typed-ast emits JSON with annotations and bindings keys
// --- Command: src/FunLang/bin/Release/net10.0/fn --emit-typed-ast %input
// --- Input:
let x = 42
// --- Output contains: "annotations"
// --- Output contains: "bindings"
```

Note: flt tests use exact output matching by default. For JSON, either use a fixture with known stable output, or verify the JSON structure with a separate unit test in `ExportApiTests.fs` / a new `CliIntegrationTests.fs`.

### Handler Branch in Program.fs (complete)

```fsharp
// Place before the generic "elif results.Contains File then" catch-all
// Source: Program.fs Emit_Type+File branch (lines 329-357) as template
elif results.Contains Emit_Typed_Ast && results.Contains File then
    let filename = results.GetResult File
    if File.Exists filename then
        try
            let typed = ExportApi.typeCheckFile filename
            let json = serializeTypedModule filename typed
            printfn "%s" json
            0
        with ex ->
            eprintfn "Error: %s" ex.Message
            1
    else
        eprintfn "File not found: %s" filename
        1
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Newtonsoft.Json` for .NET JSON | `System.Text.Json` (BCL) | .NET Core 3.0 (2019) | No extra package needed for basic JSON building |
| Manual argv parsing | Argu DU-based parsing | FunLang genesis | New flags are one DU case + one `elif` branch |

**Deprecated/outdated:**
- Newtonsoft.Json: Requires extra NuGet; `System.Text.Json.Nodes` covers this use case.

## Open Questions

1. **JSON schema for flt exact-match tests**
   - What we know: flt tests require exact stdout match; JSON output includes map iteration order.
   - What's unclear: `Map<Ast.Span, Type.Type>` iteration order in F# is lexicographic by span fields; this should be stable, but needs verification with a simple test file.
   - Recommendation: Write one flt test with a known minimal file (`let x = 42`) and capture its exact JSON output as the expected value. Alternatively, use a unit test in `ExportApiTests.fs` that checks for key presence rather than exact string match.

2. **Filtering AnnotationMap to user file spans**
   - What we know: `Bidir.annotationMap` is reset on each `typeCheckModuleWithPrelude` call; it will contain spans for the user file's expressions but also possibly for imported modules resolved during type checking.
   - What's unclear: Whether prelude loading in `ExportApi.typeCheckFile` (which calls `loadPrelude None None` internally) populates `annotationMap` with prelude spans before the user file check runs.
   - Recommendation: Filter annotations to `span.FileName = absUserFile` as shown in the code example above. This ensures only user-file spans appear in the output. Add a test with a file that imports a prelude function to verify prelude spans are excluded.

## Sources

### Primary (HIGH confidence)
- Direct source code inspection: `Cli.fs`, `Program.fs`, `ExportApi.fs`, `Type.fs`, `Ast.fs`, `FunLang.fsproj` — all read directly from the repository
- `ExportApiTests.fs` — existing test patterns for `typeCheckFile`
- `tests/flt/emit/ast-expr/ast-expr-tuple.flt` — flt test format confirmed

### Secondary (MEDIUM confidence)
- .NET 10 BCL: `System.Text.Json.Nodes` is part of the standard library; no external documentation fetched but usage is well-known

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all components are existing code in the repo; no new dependencies
- Architecture: HIGH — the branch structure mirrors three existing branches in Program.fs verbatim; Argu DU case pattern is identical to five existing cases
- Pitfalls: HIGH — branch ordering and error path pitfalls verified by direct reading of Program.fs; Argu underscore convention verified from Cli.fs
- JSON serialization: MEDIUM — `System.Text.Json.Nodes` pattern is well-established .NET 10 BCL; no docs fetched but pattern is unambiguous

**Research date:** 2026-04-03
**Valid until:** 2026-05-03 (stable domain — no fast-moving dependencies)
