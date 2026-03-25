# Phase 32: File I/O & System Builtins - Research

**Researched:** 2026-03-25
**Domain:** F# BuiltinValue registration pattern + .NET 10 System.IO / System.Environment APIs
**Confidence:** HIGH

## Summary

Phase 32 adds 14 new builtins (STD-02 through STD-15) covering file I/O, stdin, environment, paths, directory listing, and stderr output. This is purely additive work: no grammar changes, no parser changes, no new AST nodes. Every builtin follows the exact same two-step pattern already established by `char_to_int` / `failwith` in Phases 29 and 26: add a `Scheme(...)` entry to `initialTypeEnv` in `TypeCheck.fs`, and add a `BuiltinValue(...)` entry to `initialBuiltinEnv` in `Eval.fs`.

The main structural decisions are: (1) unit-argument builtins (`stdin_read_all`, `stdin_read_line`, `get_args`, `get_cwd`) must accept `TupleValue []` because `()` evaluates to that at runtime; (2) `get_args` requires a mutable `scriptArgs` field in `Eval.fs` populated from `Program.fs` after Argu parsing, mirroring the `currentEvalFile` pattern; (3) file errors (nonexistent path, permission denied) should raise `LangThreeException(StringValue msg)` so user code can catch them with `try-with`; (4) `read_lines` / `write_lines` use `ListValue` for `string list`.

All .NET 10 APIs needed (`System.IO.File`, `System.IO.Path`, `System.IO.Directory`, `System.Environment`, `Console.In`, `System.Console`) are already available — no new NuGet packages required.

**Primary recommendation:** Add all 14 builtins in two files only (TypeCheck.fs + Eval.fs), using the exact `BuiltinValue` / `Scheme` pattern from Phase 29, with `LangThreeException` for errors.

## Standard Stack

This phase has no external dependencies beyond what already exists.

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `System.IO.File` | .NET 10 | `ReadAllText`, `WriteAllText`, `AppendAllText`, `Exists`, `ReadAllLines`, `WriteAllLines` | BCL; already used in `Program.fs` and `Prelude.fs` |
| `System.IO.Path` | .NET 10 | `Combine`, `GetFullPath` | Already used in `TypeCheck.fs` and `Prelude.fs` |
| `System.IO.Directory` | .NET 10 | `GetCurrentDirectory`, `GetFiles` | Already used in `Prelude.fs` |
| `System.Environment` | .NET 10 | `GetEnvironmentVariable`, `GetCommandLineArgs` | BCL standard |
| `System.Console` | .NET 10 | `In.ReadToEnd`, `In.ReadLine`, `Error.Write`, `Error.WriteLine` | BCL standard |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `LangThreeException` | existing | Raise catchable errors from builtins | File not found, bad args; lets user `try-with` |
| `mutable scriptArgs` | new field in Eval.fs | Pass CLI args to `get_args` builtin | Populated from `Program.fs` after Argu parse |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `LangThreeException` for file errors | F# native exceptions (`exn`) | Native exceptions can't be caught by user `try-with`; LangThreeException is the only catchable type |
| `mutable scriptArgs` | Pass args through `Env` | Mutable mirrors existing `currentEvalFile` pattern; cleaner than threading extra arg through all call sites |

**Installation:** No new packages needed.

## Architecture Patterns

### No Changes to These Files
- `Ast.fs` — no new AST nodes
- `Parser.fsy` / `Lexer.fsl` — no grammar changes
- `Cli.fs` — no new CLI flags needed for basic builtins
- `Prelude.fs` — no changes needed

### Files Modified
```
src/LangThree/
├── TypeCheck.fs    # Add 14 Scheme entries to initialTypeEnv
├── Eval.fs         # Add 14 BuiltinValue entries to initialBuiltinEnv
│                   # Add mutable scriptArgs : string list
└── Program.fs      # Set Eval.scriptArgs after Argu parse (for get_args)
```

### Pattern 1: Simple String-Argument Builtins (read_file, file_exists, get_env, dir_files)

**What:** One string argument in, one value out. Minimal currying.
**When to use:** All single-arg builtins.

```fsharp
// TypeCheck.fs — initialTypeEnv
"read_file", Scheme([], TArrow(TString, TString))
"file_exists", Scheme([], TArrow(TString, TBool))
"get_env", Scheme([], TArrow(TString, TString))
"dir_files", Scheme([], TArrow(TString, TList TString))
```

```fsharp
// Eval.fs — initialBuiltinEnv
"read_file", BuiltinValue (fun v ->
    match v with
    | StringValue path ->
        if not (System.IO.File.Exists path) then
            raise (LangThreeException (StringValue (sprintf "read_file: file not found: %s" path)))
        StringValue (System.IO.File.ReadAllText path)
    | _ -> failwith "read_file: expected string argument")
```

### Pattern 2: Unit-Argument Builtins (stdin_read_all, stdin_read_line, get_args, get_cwd)

**What:** Called as `f ()` — `()` desugars to `TupleValue []` at runtime. The BuiltinValue receives `TupleValue []`.
**When to use:** Any builtin with signature `unit -> 'a`.

```fsharp
// TypeCheck.fs — initialTypeEnv
"stdin_read_all",  Scheme([], TArrow(TTuple [], TString))
"stdin_read_line", Scheme([], TArrow(TTuple [], TString))
"get_args",        Scheme([], TArrow(TTuple [], TList TString))
"get_cwd",         Scheme([], TArrow(TTuple [], TString))
```

```fsharp
// Eval.fs — initialBuiltinEnv
"stdin_read_all", BuiltinValue (fun v ->
    match v with
    | TupleValue [] -> StringValue (System.Console.In.ReadToEnd())
    | _ -> failwith "stdin_read_all: expected unit argument")

"get_args", BuiltinValue (fun v ->
    match v with
    | TupleValue [] -> ListValue (scriptArgs |> List.map StringValue)
    | _ -> failwith "get_args: expected unit argument")

"get_cwd", BuiltinValue (fun v ->
    match v with
    | TupleValue [] -> StringValue (System.IO.Directory.GetCurrentDirectory())
    | _ -> failwith "get_cwd: expected unit argument")
```

### Pattern 3: Curried Two-Argument Builtins (write_file, append_file, path_combine, eprint, eprintln)

**What:** Two string arguments, curried via nested `BuiltinValue`. Mirrors `string_concat` pattern.
**When to use:** All two-arg builtins.

```fsharp
// TypeCheck.fs — initialTypeEnv
"write_file",   Scheme([], TArrow(TString, TArrow(TString, TTuple [])))
"append_file",  Scheme([], TArrow(TString, TArrow(TString, TTuple [])))
"path_combine", Scheme([], TArrow(TString, TArrow(TString, TString)))
"eprint",       Scheme([], TArrow(TString, TTuple []))
"eprintln",     Scheme([], TArrow(TString, TTuple []))
```

```fsharp
// Eval.fs — initialBuiltinEnv
"write_file", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue path ->
        BuiltinValue (fun v2 ->
            match v2 with
            | StringValue content ->
                System.IO.File.WriteAllText(path, content)
                TupleValue []
            | _ -> failwith "write_file: second argument must be string")
    | _ -> failwith "write_file: first argument must be string")

"eprint", BuiltinValue (fun v ->
    match v with
    | StringValue s ->
        stderr.Write(s)
        stderr.Flush()
        TupleValue []
    | _ -> failwith "eprint: expected string argument")
```

### Pattern 4: read_lines and write_lines (string list I/O)

**What:** `read_lines` returns `ListValue` of `StringValue`; `write_lines` takes a `string list` as second arg.

```fsharp
// TypeCheck.fs
"read_lines",  Scheme([], TArrow(TString, TList TString))
"write_lines", Scheme([], TArrow(TString, TArrow(TList TString, TTuple [])))
```

```fsharp
// Eval.fs
"read_lines", BuiltinValue (fun v ->
    match v with
    | StringValue path ->
        if not (System.IO.File.Exists path) then
            raise (LangThreeException (StringValue (sprintf "read_lines: file not found: %s" path)))
        let lines = System.IO.File.ReadAllLines path
        ListValue (lines |> Array.toList |> List.map StringValue)
    | _ -> failwith "read_lines: expected string argument")

"write_lines", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue path ->
        BuiltinValue (fun v2 ->
            match v2 with
            | ListValue lines ->
                let strings = lines |> List.map (function
                    | StringValue s -> s
                    | _ -> failwith "write_lines: list must contain strings")
                System.IO.File.WriteAllLines(path, strings)
                TupleValue []
            | _ -> failwith "write_lines: second argument must be string list")
    | _ -> failwith "write_lines: first argument must be string")
```

### Pattern 5: get_args Mutable — Population from Program.fs

**What:** A `mutable scriptArgs : string list` in Eval.fs, populated from Program.fs after Argu parsing.
**When to use:** Mirrors `currentEvalFile` mutable pattern exactly.

```fsharp
// Eval.fs — add near currentEvalFile
/// Command-line arguments passed to the user script.
/// Set by Program.fs after Argu parsing. Used by get_args builtin.
let mutable scriptArgs : string list = []
```

```fsharp
// Program.fs — in the File evaluation branch, after Argu parse
// Extract remaining args after the filename for get_args
// With Argu, remaining positional args after the file can be captured via GetRemainingArguments
// or Environment.GetCommandLineArgs() minus the known flags.
// Simplest approach: store argv after stripping flags that Argu consumed.
// Recommended: use Environment.GetCommandLineArgs() and find args after the filename.
```

**Note on get_args implementation:** Since Argu consumes the known flags (`--file`, `-e`, etc.), the user script args are the "extra" arguments after the script filename. The cleanest approach is to capture the raw `argv` array in `Program.fs`, find the filename's position, and store everything after it as `Eval.scriptArgs`. This matches how Python/Ruby CLI wrappers work.

### Anti-Patterns to Avoid

- **Using F# native `failwith` for file errors:** This raises `System.Exception`, which user `try-with` cannot catch. Always use `raise (LangThreeException (StringValue msg))` for user-visible errors.
- **Using `TupleValue []` as success indicator for read operations:** Return the actual value (`StringValue`, `ListValue`), not unit.
- **Using `System.Console.ReadLine()` for stdin_read_line:** Use `System.Console.In.ReadLine()` to be consistent with `stdin_read_all` using `Console.In`. Handle `null` return (EOF) by returning empty string or raising an exception.
- **Forgetting to flush stderr:** Always call `stderr.Flush()` after `stderr.Write()` in `eprint`, mirroring how `print` flushes `stdout`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File reading | Custom stream reader | `System.IO.File.ReadAllText` | Handles encoding, buffering, disposal |
| Line splitting | `string.Split('\n')` | `System.IO.File.ReadAllLines` | Handles `\r\n`, trailing newlines, encoding |
| Path joining | String concatenation with `/` | `System.IO.Path.Combine` | Cross-platform; handles trailing separators |
| Directory listing | Custom recursive walker | `System.IO.Directory.GetFiles(path)` | Non-recursive by default; correct for the spec |
| Environment var | Process inspection | `System.Environment.GetEnvironmentVariable` | BCL standard; handles missing vars (returns null) |

**Key insight:** All .NET 10 BCL APIs are available and already used in the project. Never reinvent what `System.IO` already provides correctly.

## Common Pitfalls

### Pitfall 1: Unit-Arg Builtins Receive TupleValue [], Not Unit AST Node

**What goes wrong:** Implementer writes `match v with | _ -> ...` without matching `TupleValue []`, then the builtin silently accepts any argument.
**Why it happens:** The `unit` shorthand `()` desugars to `LambdaAnnot("__unit", TETuple [], ...)` in the parser, but at runtime the argument is evaluated as `TupleValue []` (an empty tuple).
**How to avoid:** Always match `TupleValue []` explicitly in unit-arg builtins:
```fsharp
| TupleValue [] -> (* do work *)
| _ -> failwith "builtin: expected unit argument"
```
**Warning signs:** Builtin appears to work but `get_args "oops"` doesn't raise a type error at runtime.

### Pitfall 2: File Errors Must Use LangThreeException

**What goes wrong:** Using `failwithf` or `failwith` for file-not-found errors produces F# exceptions that bypass user `try-with` handlers.
**Why it happens:** `try-with` in LangThree only catches `LangThreeException`. F# native exceptions propagate up and crash the runtime.
**How to avoid:** Use `raise (LangThreeException (StringValue msg))` for all user-visible errors from builtins.
**Warning signs:** `try-with (read_file "missing.txt") (fun e -> "default")` crashes instead of returning "default".

### Pitfall 3: get_env Returns Empty String vs Raises on Missing Var

**What goes wrong:** `System.Environment.GetEnvironmentVariable` returns `null` for missing vars. Wrapping with `StringValue null` causes a null-ref somewhere downstream.
**Why it happens:** F# lets null slip through unless checked.
**How to avoid:** Check for null explicitly:
```fsharp
| StringValue varName ->
    let v = System.Environment.GetEnvironmentVariable(varName)
    if v = null then
        raise (LangThreeException (StringValue (sprintf "get_env: variable '%s' not set" varName)))
    else StringValue v
```
**Warning signs:** `get_env "MISSING_VAR"` returns `""` or throws a NullReferenceException.

### Pitfall 4: dir_files Returns File Names Only vs Full Paths

**What goes wrong:** `Directory.GetFiles` returns full absolute paths, but users may expect just filenames.
**Why it happens:** The spec says `dir_files "path"` returns file list — ambiguous.
**How to avoid:** Return full paths (consistent with how `path_combine` and `file_exists` work with paths). This is the most useful behavior.

### Pitfall 5: stdin_read_line at EOF Returns null

**What goes wrong:** `Console.In.ReadLine()` returns `null` at EOF, causing `StringValue null` to be created.
**How to avoid:**
```fsharp
let line = System.Console.In.ReadLine()
if line = null then StringValue ""   // or raise LangThreeException
else StringValue line
```
Convention: return `""` at EOF (matches Unix `read` behavior).

### Pitfall 6: Scheme Type Variable Numbering Collisions

**What goes wrong:** Two entries in `initialTypeEnv` both use `TVar 0` in the same `Scheme([0], ...)`. This is fine — each `Scheme` is instantiated fresh. But forgetting `[0]` in the polymorphic vars list causes the type variable to be unconstrained.
**How to avoid:** `failwith : string -> 'a` is `Scheme([0], TArrow(TString, TVar 0))`. The `[0]` is the list of generalized type variables. Non-polymorphic builtins use `Scheme([], ...)`.

## Code Examples

### Complete TypeCheck.fs Block (all 14 entries)

```fsharp
// Source: LangThree codebase — TypeCheck.fs initialTypeEnv pattern
// Phase 32: File I/O & System Builtins

// STD-02: read_file : string -> string
"read_file", Scheme([], TArrow(TString, TString))

// STD-03: stdin_read_all : unit -> string
"stdin_read_all", Scheme([], TArrow(TTuple [], TString))

// STD-04: stdin_read_line : unit -> string
"stdin_read_line", Scheme([], TArrow(TTuple [], TString))

// STD-05: write_file : string -> string -> unit
"write_file", Scheme([], TArrow(TString, TArrow(TString, TTuple [])))

// STD-06: append_file : string -> string -> unit
"append_file", Scheme([], TArrow(TString, TArrow(TString, TTuple [])))

// STD-07: file_exists : string -> bool
"file_exists", Scheme([], TArrow(TString, TBool))

// STD-08: read_lines : string -> string list
"read_lines", Scheme([], TArrow(TString, TList TString))

// STD-09: write_lines : string -> string list -> unit
"write_lines", Scheme([], TArrow(TString, TArrow(TList TString, TTuple [])))

// STD-10: get_args : unit -> string list
"get_args", Scheme([], TArrow(TTuple [], TList TString))

// STD-11: get_env : string -> string
"get_env", Scheme([], TArrow(TString, TString))

// STD-12: get_cwd : unit -> string
"get_cwd", Scheme([], TArrow(TTuple [], TString))

// STD-13: path_combine : string -> string -> string
"path_combine", Scheme([], TArrow(TString, TArrow(TString, TString)))

// STD-14: dir_files : string -> string list
"dir_files", Scheme([], TArrow(TString, TList TString))

// STD-15: eprint : string -> unit
"eprint",   Scheme([], TArrow(TString, TTuple []))
// STD-15: eprintln : string -> unit
"eprintln", Scheme([], TArrow(TString, TTuple []))
```

### mutable scriptArgs in Eval.fs

```fsharp
// Source: LangThree codebase — mirrors currentEvalFile pattern
/// Command-line arguments passed to the user script.
/// Set by Program.fs after Argu parsing. Used by get_args builtin.
let mutable scriptArgs : string list = []
```

### Program.fs — populate scriptArgs

```fsharp
// Source: LangThree codebase — in the File evaluation branch
// After: let results = parser.Parse(argv, raiseOnUsage = false)
// The File argument occupies position [N] in argv. Everything after it are script args.
// Argu's GetRemainingArguments captures unrecognized positional args.
// If using --  separator: argv after "--" is script args.
// Simplest reliable method:
let rawArgv = System.Environment.GetCommandLineArgs()
// rawArgv[0] is the assembly path; find the script filename and take everything after
let filename = results.GetResult File
let idxInRaw = rawArgv |> Array.tryFindIndex (fun a -> a = filename)
Eval.scriptArgs <-
    match idxInRaw with
    | Some i -> rawArgv |> Array.skip (i + 1) |> Array.toList
    | None -> []
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Shell scripts for file ops | Built-in `read_file` / `write_file` | Phase 32 | Programs no longer need shell hacks |
| No stdin access | `stdin_read_all` / `stdin_read_line` | Phase 32 | Pipeline-friendly programs possible |
| Manual path concatenation | `path_combine` | Phase 32 | Cross-platform path handling |

**No deprecated approaches:** This is a greenfield addition; all patterns match the existing project style.

## Open Questions

1. **get_args: what counts as "user script arguments"?**
   - What we know: Argu parses known flags; `argv` contains the raw command line
   - What's unclear: Should args after `--` be special? Should `get_args` return `[]` in REPL mode?
   - Recommendation: Use `Environment.GetCommandLineArgs()`, strip the assembly path and known flags, return whatever follows the script filename. Return `[]` in REPL/expr mode (scriptArgs defaults to `[]`).

2. **get_env on missing variable: raise or return empty string?**
   - What we know: Python raises `KeyError`; Bash returns `""`; success criteria says "returns environment variable value" without specifying missing case
   - What's unclear: Whether user code should be able to distinguish "set to empty" from "not set"
   - Recommendation: Raise `LangThreeException` with a descriptive message, so user can do `try-with (get_env "X") (fun _ -> "default")`. This is more useful than silently returning `""`.

3. **dir_files: filenames only or full paths?**
   - What we know: `Directory.GetFiles` returns full paths; `Path.GetFileName` strips to name
   - What's unclear: Which is more useful for the common case
   - Recommendation: Return full paths (consistent with `read_file`, `file_exists` which take full paths).

4. **stdin_read_line at EOF: empty string or exception?**
   - What we know: `Console.In.ReadLine()` returns `null` at EOF
   - Recommendation: Return `""` at EOF (matches Unix `read` behavior, least surprising).

## Sources

### Primary (HIGH confidence)
- LangThree codebase — `Eval.fs` initialBuiltinEnv (direct code inspection)
- LangThree codebase — `TypeCheck.fs` initialTypeEnv (direct code inspection)
- LangThree codebase — `Ast.fs` Value DU, `Type.fs` Type DU (direct code inspection)
- LangThree codebase — `Program.fs` main entrypoint, argv handling (direct code inspection)

### Secondary (MEDIUM confidence)
- .NET 10 BCL: `System.IO.File`, `System.IO.Path`, `System.IO.Directory`, `System.Environment`, `System.Console` — all stable APIs in .NET 10, unchanged from .NET 6+

### Tertiary (LOW confidence)
- N/A — all claims verified directly from codebase

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all APIs already used in the codebase; no new dependencies
- Architecture: HIGH — identical to Phase 29 (char_to_int/int_to_char) pattern; confirmed by code inspection
- Pitfalls: HIGH — unit-param and LangThreeException patterns directly observed in existing code

**Research date:** 2026-03-25
**Valid until:** Stable indefinitely — this is codebase-internal research, no external dependencies
