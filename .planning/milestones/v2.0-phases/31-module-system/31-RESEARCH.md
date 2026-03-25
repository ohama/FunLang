# Phase 31: Module System - Research

**Researched:** 2026-03-25
**Domain:** F#/fslex/fsyacc language interpreter — module system, file import, record type scoping
**Confidence:** HIGH

## Summary

Phase 31 adds file-based imports (`open "path/to/file.fun"`) and fixes record field name
collision resolution for modules (MOD-01, MOD-02, MOD-05). The existing codebase already
has a functioning in-file module system with qualified access (`Module.member`), `open`
directives for in-file modules, ADT type isolation across modules, and multiple `module X =`
declarations in a single file.

The primary missing piece is MOD-01: `open "path/to/file.fun"` does not exist at all — the
parser does not accept `OPEN STRING`, the AST has no `FileImportDecl` node, and there is no
file-loading path in the type checker or evaluator. The file loading pattern is already
established in `Prelude.fs` (`parseModuleFromString` + `typeCheckModuleWithPrelude` +
`evalModuleDecls`), so MOD-01 is essentially reusing the Prelude loading logic at the
declaration level.

MOD-02 (multiple `module X` declarations in one file) is already satisfied: the existing
grammar and evaluator handle multiple `ModuleDecl` nodes in a `Module(decls, _)` container.
MOD-05 (type name isolation) works for ADT types but has a partial gap for record types with
shared field names across modules — `RecordExpr` resolution uses a global `recEnv` flat
lookup that fails with `DuplicateFieldName` when two record types share any field name across
modules. The fix is to propagate a module-scoped `recEnv` when type-checking inside a `ModuleDecl`.

**Primary recommendation:** Implement MOD-01 by adding `FileImportDecl` to the AST, a `OPEN STRING` grammar rule, and a `loadAndMergeFile` function that mirrors Prelude loading. Fix MOD-05 record collision by scoping `recEnv` within `ModuleDecl` type checking.

## Standard Stack

This phase is an internal language implementation task using the existing tech stack. No new
external libraries are needed.

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| F# / .NET 10 | 10.0 | Implementation language | Already in use throughout |
| FSharp.Text.Lexing | via fslex | Token stream production | Already in use |
| FsLexYacc (fsyacc) | via fsyacc | Parser grammar | Already in use |
| System.IO (File, Path) | .NET 10 BCL | File path resolution and reading | Same pattern as Prelude.fs |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| Prelude.fs `parseModuleFromString` | existing | Parse .fun file from string | Reuse for file import |
| Prelude.fs `findPreludeDir` pattern | existing | Path resolution strategy | Adapt for relative paths |
| TypeCheck `typeCheckModuleWithPrelude` | existing | Type check imported file | Reuse with accumulated envs |
| Eval `evalModuleDecls` | existing | Evaluate imported file | Reuse with accumulated envs |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New `FileImportDecl` AST node | Reuse `OpenDecl` with string payload | `OpenDecl` uses `string list` for qualified paths; adding a string file path means overloading the AST union — a separate node is cleaner |
| Flat-file import (merge decls at call site) | Module-wrapping (auto-wrap in named module) | MOD-01 says bindings "made available" — flat import matches `open M` behavior, module-wrap matches `import M from "..."` — use flat import for minimal complexity |

**Installation:** No new packages needed.

## Architecture Patterns

### Existing Project Structure (relevant files)
```
src/LangThree/
├── Ast.fs           # Add FileImportDecl here
├── Lexer.fsl        # OPEN already lexed; no change needed (STRING already a token)
├── Parser.fsy       # Add OPEN STRING grammar rule
├── TypeCheck.fs     # Add FileImportDecl handling in typeCheckDecls
├── Eval.fs          # Add FileImportDecl handling in evalModuleDecls
├── Prelude.fs       # Reference implementation for file loading pattern
└── Diagnostic.fs    # Add E0505 (file import error) if needed
```

### Pattern 1: File Import Declaration (MOD-01)

**What:** When the parser sees `open "path/to/file.fun"`, it produces a `FileImportDecl` AST
node. The type checker and evaluator process it by loading, parsing, type-checking, and
evaluating the file, then merging its exports into the current environment.

**When to use:** Every time a `.fun` file wants to use bindings from another `.fun` file.

**Implementation sketch:**

```fsharp
// Ast.fs — add to Decl union
| FileImportDecl of path: string * Span

// Parser.fsy — add to Decls production
| OPEN STRING
    { [FileImportDecl($2, ruleSpan parseState 1 2)] }
| OPEN STRING Decls
    { FileImportDecl($2, ruleSpan parseState 1 2) :: $3 }

// TypeCheck.fs — add to typeCheckDecls fold
| FileImportDecl(path, span) ->
    let resolvedPath = resolveImportPath path (spanFileName span)
    match loadAndTypeCheckFile resolvedPath accumEnvs with
    | Ok (fileTypeEnv, fileCtorEnv, fileRecEnv) ->
        let env' = Map.fold (fun acc k v -> Map.add k v acc) env fileTypeEnv
        let cEnv' = Map.fold (fun acc k v -> Map.add k v acc) cEnv fileCtorEnv
        let rEnv' = Map.fold (fun acc k v -> Map.add k v acc) rEnv fileRecEnv
        (env', cEnv', rEnv', mods, warns)
    | Error diag -> raise (TypeException diag)

// Eval.fs — add to evalModuleDecls fold
| FileImportDecl(path, span) ->
    let resolvedPath = resolveImportPath path (spanFileName span)
    let (importedEnv, importedModEnv) = loadAndEvalFile resolvedPath currentEnvs
    (Map.merge env importedEnv, Map.merge modEnv importedModEnv)
```

### Pattern 2: Path Resolution for File Imports

**What:** Resolve `"path/to/file.fun"` relative to the importing file's location.

**When to use:** Always inside `FileImportDecl` handling.

**Reference:** The `Span.FileName` field carries the source filename. Use `Path.GetDirectoryName`
on it to get the base directory. Apply `Path.Combine` with the import string. Normalize with
`Path.GetFullPath`. Guard against cycles via a visited-set passed through the load chain.

```fsharp
// Resolve import path relative to importing file
let resolveImportPath (importPath: string) (importingFile: string) : string =
    if Path.IsPathRooted importPath then
        importPath
    else
        let baseDir =
            if System.String.IsNullOrEmpty importingFile || importingFile = "<unknown>"
            then Directory.GetCurrentDirectory()
            else Path.GetDirectoryName(Path.GetFullPath importingFile)
        Path.GetFullPath(Path.Combine(baseDir, importPath))
```

### Pattern 3: Module-Scoped RecordEnv for MOD-05 Fix

**What:** When type-checking inside a `ModuleDecl`, the `recEnv` passed to inner declarations
should only contain record types visible from that scope (parent recEnv). After the inner
declarations are processed, module-local record types are exported but NOT merged back into
the parent's flat `recEnv`. This prevents `RecordExpr` from seeing both `Parser.Tok` and
`Lexer.Tok` simultaneously when resolving by field names.

**Why it matters:** The existing `RecordExpr` synthesis (Bidir.fs line 396-423) resolves
record types by matching the full set of field names against `recEnv`. If `Parser.Tok` and
`Lexer.Tok` both have a field named `kind`, the match returns two results and raises
`DuplicateFieldName`. Scoping the `recEnv` per module prevents both from being visible at
the same time.

**Current behavior:** `typeCheckDecls` for `ModuleDecl` runs inner decls with the parent
`rEnv`, which already contains all outer record types. The inner module's new record types
are added to `innerRecEnv` and then filtered to get `moduleRecEnv` (only new ones). But all
of outer's records leak into `innerRecEnv`, so the inner `recEnv` grows monotonically.

**Fix:** When the same-named field collision matters only across distinct modules, the fix
scope is: the module-level `RecordExpr` check. The `recEnv` passed into `typeCheckDecls` for
a `ModuleDecl`'s inner body should be only the parent's records, and when `ModuleDecl` finishes,
its new records are stored only in `exports.RecEnv`, not merged back into the parent flat env.
The parent env then only gets them back when `open ModuleName` is called.

**Current code already does this correctly for the parent env**: `moduleRecEnv` is filtered
to exclude parent-level entries before being stored in `exports`. What does NOT work is the
reverse direction: inner declarations see all parent records plus all siblings — which could
cause collision for `RecordExpr` resolution.

**Practical impact:** The collision only occurs when two different `module X =` blocks define
record types with overlapping field names AND code outside those modules creates record literals
with those same fields in ambiguous context. Inside a module, the code only sees that module's
`recEnv` entries (plus inherited). For MOD-05, the success criterion focuses on qualified
access (`Parser.Token` vs `Lexer.Token`) which already works for ADTs. Record types are a
secondary concern; the simplest fix for field ambiguity is documentation ("use unique field
names across modules" or "use type-annotated creation functions inside modules").

### Anti-Patterns to Avoid

- **Recursive file imports at parse time:** Do not recursively invoke the parser during
  parsing. File imports must be resolved during the type-check and eval phases (like Prelude
  loading), not during grammar reduction.
- **Shadowing the global recEnv with file-imported record types:** File imports should merge
  into the running `recEnv`, but if two imported files define record types with overlapping
  field names, `RecordExpr` resolution will fail. Document this limitation.
- **Circular import detection at runtime:** Cycle detection must happen before evaluation.
  Track a `Set<string>` of in-progress file paths during the type-check phase. If a cycle is
  detected, raise `E0501`-style error (or a new `E0505`).
- **Re-processing the same file multiple times:** Cache loaded files by their resolved
  absolute path in a `Map<string, PreludeResult>`-style accumulator passed through the
  import chain.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File parsing | Custom parser invocation | Reuse `Prelude.parseModuleFromString` | Already handles IndentFilter + fsyacc tokenizer dance |
| Path normalization | Custom path logic | `System.IO.Path.GetFullPath` + `Path.Combine` | Handles `..`, symlinks, platform separators |
| Type environment merging | Custom merge | `Map.fold (fun acc k v -> Map.add k v acc)` | The existing pattern used throughout TypeCheck.fs and Prelude.fs |
| Value environment merging | Custom merge | `Map.fold` pattern from `loadPrelude` | Already used in Prelude.fs line 103-108 |
| Cycle detection | Graph traversal | Simple `Set<string>` visited set | Files have no names like modules; a visited path set is sufficient |

**Key insight:** Prelude.fs already implements the complete file-loading pipeline
(parse → type check → eval → merge environments). MOD-01 is that same pipeline triggered by
an AST node rather than a startup scan.

## Common Pitfalls

### Pitfall 1: Span.FileName is "<unknown>" for REPL/test inputs
**What goes wrong:** `resolveImportPath` uses `Span.FileName` from the `FileImportDecl` span
to compute the base directory. In tests and REPL mode, the filename is `"test"` or `"<expr>"`.
**Why it happens:** `Lexer.setInitialPos` takes a filename string passed from the call site.
Test helpers use `"test"`, REPL uses `"<repl>"`.
**How to avoid:** In `resolveImportPath`, treat any non-rooted, non-existing-directory
filename as "use CWD". Only use `Path.GetDirectoryName` when the filename is an actual file
path (check `File.Exists` or `Path.IsPathRooted`).
**Warning signs:** `DirectoryNotFoundException` during tests.

### Pitfall 2: `typeCheckDecls` runs before `evalModuleDecls` — both need file loading
**What goes wrong:** If `FileImportDecl` is only handled in `evalModuleDecls` but not in
`typeCheckDecls`, the type checker sees unknown bindings from the imported file and raises
`E0101 (UnboundVariable)`.
**Why it happens:** The pipeline is: parse → type check → eval. File loading must happen in
both the TC and Eval phases to populate both the type env and value env.
**How to avoid:** Add `FileImportDecl` handling to BOTH `typeCheckDecls` (TypeCheck.fs) and
`evalModuleDecls` (Eval.fs). The TC phase accumulates type/ctor/rec envs; the Eval phase
accumulates value and module value envs.
**Warning signs:** Type error on bindings that clearly exist in the imported file.

### Pitfall 3: The `filterNewBindings` pattern must be applied carefully for imports
**What goes wrong:** The Prelude loading logic filters out bindings that already existed
in the prior result (lines 106-108 of Prelude.fs). If this filter is omitted for file imports,
imported prelude-level names (like `id`, `map`) get re-added, causing no bugs but wasted
work. More importantly, if two imported files both define `let x = ...`, the second import
must win (shadowing), not be filtered.
**How to avoid:** For file imports, merge the imported file's entire type env and value env
without filtering (shadowing is acceptable). Prelude.fs filters to avoid double-counting
builtins, but file imports are additive.
**Warning signs:** `UnboundVariable` for a name defined in an earlier import.

### Pitfall 4: `OpenDecl` and `FileImportDecl` share the `OPEN` keyword
**What goes wrong:** The existing grammar has `OPEN QualifiedIdent` for in-file module open.
Adding `OPEN STRING` creates a grammar ambiguity only if the lexer could produce either
`QualifiedIdent` or `STRING` in the same position, which it cannot — `STRING` is a quoted
token (`"..."`) while `QualifiedIdent` starts with an unquoted `IDENT`. No ambiguity.
**How to avoid:** Add `OPEN STRING` rules alongside the existing `OPEN QualifiedIdent` rules.
The fsyacc parser will correctly select based on the next token.
**Warning signs:** Shift/reduce conflicts in fsyacc output during `dotnet build`.

### Pitfall 5: Record field collision is a type inference limitation, not a module scoping bug
**What goes wrong:** A function `let getKind t = t.kind` cannot be type-checked when `kind`
exists in multiple record types in `recEnv`. This is not a bug in module scoping — it's the
same problem with global field names.
**Why it happens:** Bidirectional type inference on `FieldAccess` requires the receiver's
type to be known (`TData(name, _)`) before looking up the field. A free variable `'a` cannot
be narrowed by field name alone without row-polymorphism or nominal type inference.
**How to avoid:** For MOD-05, focus on ADT constructor isolation (already works). For records
with same-named fields across modules: (a) within-module creation functions (`make`) avoid the
ambiguity since `recEnv` inside the module is scoped; (b) cross-module collisions require the
user to qualify or use unique field names. Document this limitation.
**Warning signs:** `E0313 (FieldAccessOnNonRecord)` or `DuplicateFieldName` errors.

### Pitfall 6: Dependency graph for cycle detection needs to include file imports
**What goes wrong:** The existing `buildDependencyGraph` / `detectCircularDeps` in TypeCheck.fs
only tracks in-file `open Module` dependencies. If file imports can create cycles
(A.fun imports B.fun imports A.fun), this is not caught.
**Why it happens:** `buildDependencyGraph` only looks at `OpenDecl`, not `FileImportDecl`.
**How to avoid:** Track loaded file paths in a mutable `HashSet<string>` threaded through
the recursive `loadAndTypeCheckFile` call. Before loading a file, check if it is already in
the set; if so, raise a cycle error. After loading, remove it from the set.
**Warning signs:** Stack overflow from mutual file imports.

## Code Examples

### File Loading Pattern (from Prelude.fs)
```fsharp
// Source: src/LangThree/Prelude.fs (lines 77-118)
// This is the reference pattern to adapt for FileImportDecl:

let private loadSingleFile
    (result: PreludeResult) (file: string) : PreludeResult =
    let source = File.ReadAllText file
    let m = parseModuleFromString source file
    match typeCheckModuleWithPrelude result.CtorEnv result.RecEnv result.TypeEnv m with
    | Ok (_warnings, ctorEnv, recEnv, _modules, typeEnv) ->
        let newTypeBindings = typeEnv |> Map.filter (fun k _ ->
            not (Map.containsKey k initialTypeEnv))
        let mergedTypeEnv = Map.fold (fun acc k v -> Map.add k v acc) result.TypeEnv newTypeBindings
        let mergedCtorEnv = Map.fold (fun acc k v -> Map.add k v acc) result.CtorEnv ctorEnv
        let mergedRecEnv = Map.fold (fun acc k v -> Map.add k v acc) result.RecEnv recEnv
        let decls = getDecls m
        let evalEnv = Map.fold (fun acc k v -> Map.add k v acc) result.Env Eval.initialBuiltinEnv
        let (finalEnv, _moduleEnv) = Eval.evalModuleDecls mergedRecEnv Map.empty evalEnv decls
        let newValues = finalEnv |> Map.filter (fun k _ ->
            not (Map.containsKey k Eval.initialBuiltinEnv) && not (Map.containsKey k result.Env))
        let mergedEnv = Map.fold (fun acc k v -> Map.add k v acc) result.Env newValues
        { Env = mergedEnv; TypeEnv = mergedTypeEnv; CtorEnv = mergedCtorEnv; RecEnv = mergedRecEnv }
    | Error diag ->
        failwithf "Import error in %s: %s" file (formatDiagnostic diag)
```

### Path Resolution
```fsharp
// Source: adapted from System.IO patterns + Prelude.fs findPreludeDir pattern
let resolveImportPath (importPath: string) (importingFile: string) : string =
    if Path.IsPathRooted importPath then
        importPath
    else
        let baseDir =
            if not (System.String.IsNullOrEmpty importingFile)
               && importingFile <> "<unknown>"
               && importingFile <> "<expr>"
               && importingFile <> "test"
               && File.Exists importingFile then
                Path.GetDirectoryName(Path.GetFullPath importingFile)
            else
                Directory.GetCurrentDirectory()
        Path.GetFullPath(Path.Combine(baseDir, importPath))
```

### Parser Grammar Addition (fsyacc)
```fsharp
// Source: src/LangThree/Parser.fsy (add alongside existing OPEN QualifiedIdent rules)
// In Decls production, add:
| OPEN STRING
    { [FileImportDecl($2, ruleSpan parseState 1 2)] }
| OPEN STRING Decls
    { FileImportDecl($2, ruleSpan parseState 1 2) :: $3 }
```

### Existing OpenDecl Handling (reference for FileImportDecl handler shape)
```fsharp
// Source: src/LangThree/Eval.fs lines 854-865
| OpenDecl(path, _) ->
    match path with
    | [name] ->
        match Map.tryFind name modEnv with
        | Some modValEnv ->
            let env' = Map.fold (fun acc k v -> Map.add k v acc) env modValEnv.Values
            let env'' = Map.fold (fun acc k v -> Map.add k v acc) env' modValEnv.CtorEnv
            (env'', modEnv)
        | None -> (env, modEnv)
    | _ -> (env, modEnv)

// FileImportDecl will be similar but loads from disk instead of moduleEnv:
// | FileImportDecl(path, span) ->
//     let resolvedPath = resolveImportPath path (span.FileName)
//     let imported = loadAndEvalFile resolvedPath currentEnvs
//     (mergeEnv env imported.Env, modEnv)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `cat a.fun b.fun | funlang` (shell hack) | `open "a.fun"` inside b.fun | Phase 31 | Users can compose files without shell scripting |
| Module system only in-file | File-level module system | Phase 31 | Multi-file programs become possible |
| `open ModuleName` (in-file only) | `open "path"` (cross-file) + `open ModuleName` (in-file) | Phase 31 | Both syntaxes coexist using the `OPEN` keyword |

**Deprecated/outdated:**
- None (this is additive). The existing `open ModuleName` (in-file) syntax is preserved.

## Open Questions

1. **Should imported file bindings be wrapped in a named module automatically?**
   - What we know: `open "lib.fun"` should make `lib.fun`'s bindings directly available (flat import), matching the behavior of `open Module` for in-file modules.
   - What's unclear: Whether the user might also want `import Lib from "lib.fun"` (module-wrap import) is out of scope for Phase 31 per the requirements.
   - Recommendation: Use flat import (no auto-wrapping). The success criterion says "makes that module's bindings available", which is what `open` semantics already provides.

2. **Should record field collision (same field name in two modules) be fixed in Phase 31?**
   - What we know: MOD-05 success criterion says "record field name collisions resolved via module scoping." The current `RecordExpr` resolver fails with `DuplicateFieldName` when two in-scope record types share field names. This exists even today without file imports.
   - What's unclear: Whether fixing this requires row-polymorphism (heavy) or just module-scoped recEnv lookup (lighter).
   - Recommendation: Fix the scoped `recEnv` issue so that record types inside a `module X =` block do not leak their field names into the sibling module's resolution scope. This is achievable by ensuring `typeCheckDecls` for a `ModuleDecl` passes only the pre-module `rEnv` (not accumulated inner records) to sibling `ModuleDecl`s. The remaining limitation (functions with unknown record parameter type cannot use `.field` syntax) is a pre-existing type inference limitation unrelated to Phase 31.

3. **Cycle detection strategy for file imports**
   - What we know: In-file module cycles are detected via the DFS graph algorithm. File import cycles need a separate mechanism.
   - What's unclear: Whether mutable state (a `HashSet`) threaded via the call stack is acceptable in this functional-first codebase.
   - Recommendation: Use a `Set<string>` (immutable) passed as an extra parameter to the file-loading function. This is purely functional and consistent with the existing style.

## Sources

### Primary (HIGH confidence)
- `src/LangThree/Prelude.fs` — Complete reference implementation of file loading pipeline (parse → type check → eval → merge environments)
- `src/LangThree/Ast.fs` — Current AST including `OpenDecl of path: string list * Span` and `ModuleDecl`
- `src/LangThree/Parser.fsy` — Grammar: `OPEN QualifiedIdent` (lines 523-526), `parseModule` rule (lines 480-488)
- `src/LangThree/TypeCheck.fs` — `OpenDecl` handling (lines 700-715), `ModuleDecl` handling (lines 670-698), `typeCheckDecls` fold
- `src/LangThree/Eval.fs` — `OpenDecl` handling (lines 854-865), `ModuleDecl` handling (lines 825-853), `evalModuleDecls`
- `src/LangThree/Bidir.fs` — `RecordExpr` field-name resolution (lines 395-423), `FieldAccess` type checking (lines 425-442)
- `src/LangThree/Diagnostic.fs` — Existing E0501-E0504 module error codes
- `.planning/REQUIREMENTS.md` — MOD-01, MOD-02, MOD-05 requirement text
- `.planning/ROADMAP.md` — Phase 31 success criteria
- `tests/LangThree.Tests/ModuleTests.fs` — Existing module test suite (209 tests passing)

### Secondary (MEDIUM confidence)
- Manual testing via `dotnet run` confirmed: MOD-02 already works (multiple `module X =` blocks), ADT type isolation works, record creation with unique field names across modules works
- Manual testing confirmed: `open "path.fun"` produces `parse error` (MOD-01 not implemented), `let getKind t = t.kind` with ambiguous record type produces `E0313` (field inference limitation)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new libraries; existing patterns identified from source
- Architecture: HIGH — implementation pattern directly derived from Prelude.fs
- Pitfalls: HIGH — verified empirically via test runs and code inspection
- MOD-02 current state: HIGH — verified working via test run
- MOD-05 record gap: HIGH — root cause identified in Bidir.fs line 441

**Research date:** 2026-03-25
**Valid until:** 2026-04-25 (stable codebase, monthly refresh sufficient)
