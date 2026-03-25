# Phase 26: Quick Fixes & Small Additions - Research

**Researched:** 2026-03-24
**Domain:** F# interpreter internals — parser, evaluator, type checker, Prelude loading
**Confidence:** HIGH

## Summary

Phase 26 addresses four independent bugs/missing features in the LangThree interpreter. Research was
conducted by reading the actual source files directly. All findings are verified from source.

Three of the four fixes (STD-01, MOD-04, TYPE-03) have clear root causes with identified fix
locations. For MOD-03, the current code paths traced through the source appear correct for empty
files (parser returns `EmptyModule`, TypeCheck handles it with an early-return branch, Program.fs
handles `EmptyModule _ -> []`). The crash may be scenario-specific (whitespace-only file, or a
state issue with `loadPrelude` when CWD is wrong). Empirical testing should confirm the exact
trigger before fixing.

**Primary recommendation:** Implement in order: STD-01 (failwith builtin, trivial) → TYPE-03
(option alias in Elaborate.fs, minimal) → MOD-04 (Prelude path, Prelude.fs only) → MOD-03 (needs
empirical crash reproduction first to find exact location).

---

## Fix 1: MOD-03 — Empty `.fun` file crash

### Code paths traced

**Parser** (`Parser.fsy` lines 437–445):
```
parseModule:
    | MODULE QualifiedIdent Decls EOF  { NamedModule(...) }
    | NAMESPACE QualifiedIdent Decls EOF  { NamespacedModule(...) }
    | Decls EOF                        { Module($1, ...) }
    | EOF                              { EmptyModule(...) }
```
Empty file → only `[EOF]` token → matches `| EOF` → returns `EmptyModule`. Safe.

**IndentFilter** for empty input: tokenizes `""` → `[EOF]` → filter yields `EOF` at line 318. Safe.

**TypeCheck.typeCheckModuleWithPrelude** (`TypeCheck.fs` lines 736–737):
```fsharp
match m with
| EmptyModule _ -> Ok ([], Map.empty, Map.empty, Map.empty, Map.empty)
| Module (decls, _) | ... ->
    ...
```
EmptyModule handled by explicit early return. Safe.

**Program.fs** lines 191–205 after typecheck succeeds:
```fsharp
let moduleDecls = match m with
                  | Module (decls, _) | ... -> decls
                  | EmptyModule _ -> []   // returns []
let mergedRecEnv = Map.fold ... prelude.RecEnv recEnv   // recEnv = Map.empty, safe
let finalEnv, moduleEnv = Eval.evalModuleDecls mergedRecEnv Map.empty initialEnv []  // fold over [], safe
match moduleDecls |> List.rev |> List.tryPick (...)  // tryPick on [], returns None, safe
| None -> ()  // returns 0
```
Entire empty-file path appears correct.

### Likely actual crash trigger

The constraints doc says "workaround: add `let placeholder = 0`". Two possible actual triggers:

1. **Whitespace-only file** (not truly empty): A file with only newlines/spaces might produce
   `NEWLINE` tokens before `EOF`. The IndentFilter may then emit stray `INDENT`/`DEDENT` tokens
   causing a parse error.

2. **Prelude load failure obscures the crash**: When `loadPrelude()` is called and CWD is wrong
   (Prelude not found), `emptyPrelude` is returned silently. With an empty .fun file AND missing
   Prelude, some code path might fail differently.

3. **The constraint is stale**: The fix may have been partially implemented already for truly empty
   files, but the constraint was written when whitespace-only or comment-only files were the issue.

### Fix approach

Add defensive handling in Program.fs for any file that produces no evaluable declarations:

```fsharp
// After extracting moduleDecls:
if moduleDecls |> List.isEmpty then
    printfn "()"  // Empty program = unit
    0
else
    // ... existing eval + print logic
```

This is safe to add regardless of where the crash occurs.

**Files to modify:**
- `src/LangThree/Program.fs` — add early return for empty `moduleDecls` before calling `evalModuleDecls`

**Risk**: Very low. Purely defensive. Does not change behavior for non-empty files.

---

## Fix 2: MOD-04 — Prelude not found outside LangThree directory

### Root cause (confirmed)

`Prelude.fs` line 52:
```fsharp
let loadPrelude () : PreludeResult =
    let preludeDir = "Prelude"
    if Directory.Exists preludeDir then
```

`"Prelude"` is a relative path — it resolves against CWD. When `dotnet run` is invoked from
`LangThree/` (the repo root), CWD is there and `Prelude/` exists. When invoked from
`LangThree/FunLexYacc/` or any other directory, `Prelude/` is not found and `emptyPrelude` is
returned silently. All Prelude functions (`Option`, `fst`, `snd`, `not`, `map`, `filter`, etc.)
become undefined.

### Fix

Change `Prelude.fs` to search multiple candidate paths:

```fsharp
let findPreludeDir () : string =
    // 1. CWD-relative (dev workflow: dotnet run from repo root)
    if Directory.Exists "Prelude" then "Prelude"
    else
        // 2. Assembly-relative (installed binary or dotnet publish)
        let assemblyLoc = System.Reflection.Assembly.GetEntryAssembly().Location
        if not (System.String.IsNullOrEmpty assemblyLoc) then
            let assemblyDir = Path.GetDirectoryName assemblyLoc
            let candidate = Path.Combine(assemblyDir, "Prelude")
            if Directory.Exists candidate then candidate
            else
                // 3. Walk up from assembly dir (for dotnet run, binary is in bin/Debug/net10.0/)
                let mutable dir = assemblyDir
                let mutable result = ""
                for _ in 1..6 do
                    if result = "" then
                        let c = Path.Combine(dir, "Prelude")
                        if Directory.Exists c then result <- c
                        let parent = Path.GetDirectoryName dir
                        if parent <> dir then dir <- parent
                result
        else ""

let loadPrelude () : PreludeResult =
    let preludeDir = findPreludeDir ()
    if preludeDir <> "" then
        ...
```

**Key insight**: The `System.IO` namespace is already `open`'d in Prelude.fs (line 3). Only
`System.Reflection` needs to be referenced, which is available in .NET base library.

**Walk-up logic for `dotnet run`**: Binary is at
`src/LangThree/bin/Debug/net10.0/LangThree`. Walking up:
- `net10.0/` → no Prelude
- `Debug/` → no Prelude
- `bin/` → no Prelude
- `LangThree/` (project dir) → no Prelude
- `src/` → no Prelude
- `LangThree/` (repo root) → **`Prelude/` found!**

6 levels up is sufficient.

**Files to modify:**
- `src/LangThree/Prelude.fs` — replace `let preludeDir = "Prelude"` with `findPreludeDir()` call

**Risk**: Low. The CWD fallback first preserves existing dev behavior. Walk-up logic handles the
`dotnet run` case from other directories.

---

## Fix 3: STD-01 — Add `failwith` builtin

### How builtins are implemented

Two registration points:

1. **`Eval.initialBuiltinEnv`** (`Eval.fs` line 145): map of `string -> Value`. Each entry is a
   `BuiltinValue (fun v -> ...)`. Multi-arg builtins use nested `BuiltinValue` wrappers.

2. **`TypeCheck.initialTypeEnv`** (`TypeCheck.fs` line 14): map of `string -> Scheme`. Provides
   static type for the builtin.

Exceptions use `exception LangThreeException of Value` declared at `Eval.fs` line 7. This is the
only exception type that `try-with` in the language catches (Eval matches it in the `TryWith` eval case).

### Implementation

**In `Eval.fs`** — add to `initialBuiltinEnv` map (after line ~250, before the closing `]`):
```fsharp
// failwith : string -> 'a  (raises exception with given message)
"failwith", BuiltinValue (fun v ->
    match v with
    | StringValue msg -> raise (LangThreeException (StringValue msg))
    | _ -> failwith "failwith: expected string argument")
```

**In `TypeCheck.fs`** — add to `initialTypeEnv` map (after line ~53, before closing `]`):
```fsharp
// failwith : string -> 'a  (polymorphic return — matches any expected type, like raise)
"failwith", Scheme([0], TArrow(TString, TVar 0))
```

The polymorphic return type `Scheme([0], TArrow(TString, TVar 0))` is critical. Using
`TArrow(TString, TTuple [])` (unit return) would cause type errors in branches expecting
non-unit types. Using `TVar 0` matches `raise`'s pattern — it unifies with any expected type.

**Files to modify:**
- `src/LangThree/Eval.fs` — add entry to `initialBuiltinEnv` list
- `src/LangThree/TypeCheck.fs` — add entry to `initialTypeEnv` list

**Risk**: Very low. Exact same pattern as `string_length`, `print`, etc. No parser changes.

---

## Fix 4: TYPE-03 — `option` as alias for `Option`

### Current state

`Prelude/Option.fun` line 1:
```
type Option 'a = None | Some of 'a
```

This defines `Option` (uppercase) in the type system as `TData("Option", [arg])`.

When user writes `let x : option int = Some 42`, the parser produces:
- Type annotation: `TEData("option", [TEInt])` (via `AliasAtomicType IDENT` rule in Parser.fsy line 588)

This elaborates to `TData("option", [TInt])` which fails to unify with `TData("Option", [TInt])`.

### Why grammar-level alias won't work

The `TypeAliasDeclaration` grammar rule (`Parser.fsy` lines 562–564) uses `AliasTypeExpr` for the
RHS, which is defined by `AliasAtomicType`. This specifically EXCLUDES bare uppercase `IDENT` to
avoid LALR(1) conflicts with ADT declarations. So `type option 'a = Option 'a` **cannot be parsed**
under the current grammar without grammar changes.

### Fix: normalize in Elaborate.fs

The correct fix is in `Elaborate.fs` where `TypeExpr` is converted to `Type`:

**`elaborateWithVars` function** (line 61) handles `TEData`:
```fsharp
| TEData (name, args) ->
    // CURRENT:
    let folder (acc, env) t = ...
    let (revTypes, finalVars) = List.fold folder ([], vars) args
    (TData(name, List.rev revTypes), finalVars)
```

Change to:
```fsharp
| TEData (name, args) ->
    // Normalize lowercase type name aliases to canonical uppercase names
    let canonical = match name with "option" -> "Option" | "result" -> "Result" | n -> n
    let folder (acc, env) t =
        let (ty, env') = elaborateWithVars env t
        (ty :: acc, env')
    let (revTypes, finalVars) = List.fold folder ([], vars) args
    (TData(canonical, List.rev revTypes), finalVars)
```

Also update `substTypeExprWithMap` (line 92) for consistency (used in ADT/record elaboration):
```fsharp
| Ast.TEData(name, args) ->
    let canonical = match name with "option" -> "Option" | "result" -> "Result" | n -> n
    TData(canonical, List.map (substTypeExprWithMap paramMap) args)
```

And `TEName` case (line 83) for bare `option` without type arguments:
```fsharp
| Ast.TEName n ->
    let canonical = match n with "option" -> "Option" | "result" -> "Result" | n -> n
    TData(canonical, [])
```

Also in `elaborateWithVars` for `TEName` (lines 55–59):
```fsharp
| TEName name ->
    // Named type (e.g., Tree, Option) - will be resolved in type checking
    // Normalize lowercase aliases
    let canonical = match name with "option" -> "Option" | "result" -> "Result" | n -> n
    // TEName without args becomes TData with no type args (same as TEData(name, []))
    let idx = freshTypeVarIndex()
    (TVar idx, vars)  // Keep as fresh var for now — TEName is used for bare type references
```

**Wait — important distinction**: `TEName` is currently treated as a fresh `TVar`, not as
`TData(n, [])`. This is intentional for bare named types in type annotations (e.g., `let x : Tree`
would match any `Tree` value via unification). Changing `TEName "option"` to `TData("Option", [])`
would create `Option<>` without type args which is wrong (Option takes one arg).

**Conclusion**: Only `TEData` normalization is needed for the `option int` use case. The `TEName`
case would cover `let x : option = ...` which is not a valid type anyway (Option requires a type
argument). So only fix `TEData` and `substTypeExprWithMap`.

**Files to modify:**
- `src/LangThree/Elaborate.fs` — normalize `name` in `TEData` case of `elaborateWithVars` (line 61)
  and in `substTypeExprWithMap` (line 92)

**Risk**: Low. Change is localized, only affects the `name` lookup, existing tests unaffected.

---

## Standard Stack

No new libraries needed. All changes use:
- `System.Reflection.Assembly.GetEntryAssembly().Location` — .NET base library, always available
- `System.IO.Path` — already imported in Prelude.fs
- `LangThreeException` — already declared in Eval.fs

## Architecture Patterns

### Adding a builtin function
1. `Eval.initialBuiltinEnv`: `"name", BuiltinValue (fun v -> ...)`
2. `TypeCheck.initialTypeEnv`: `"name", Scheme([typeVarIds], type)`
3. No parser/AST changes

### Adding a type alias (compiler-level)
1. Normalize in `Elaborate.elaborateWithVars` for `TEData` case
2. Normalize in `Elaborate.substTypeExprWithMap` for `TEData` case
3. No parser/Prelude file changes

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Binary path detection | Env-var scheme, config file | `Assembly.GetEntryAssembly().Location` | Standard .NET idiom |
| Type alias table | Parse-time resolution | Hardcode 1-line match in Elaborate.fs | Only one alias needed now |
| failwith semantics | Custom exception class | Reuse `LangThreeException` with `StringValue` | Consistent with `raise` in the language |

## Common Pitfalls

### Pitfall 1: failwith return type — use polymorphic scheme
**What goes wrong:** `Scheme([], TArrow(TString, TTuple []))` — if return type is `unit`, the
typechecker rejects `if cond then failwith "msg" else value` because branches have different types.
**Fix:** `Scheme([0], TArrow(TString, TVar 0))` — return type unifies with whatever is expected.

### Pitfall 2: failwith must raise LangThreeException, not System.Exception
**What goes wrong:** `raise (System.Exception(msg))` is not caught by `try-with` in the language.
The Eval `TryWith` handler only catches `LangThreeException`.
**Fix:** `raise (LangThreeException (StringValue msg))`

### Pitfall 3: option alias needs to be in TEData, not TEName
**What goes wrong:** Only normalizing `TEName` misses the `option int` case which parses as
`TEData("option", [TEInt])` via the `AliasAtomicType IDENT` parser rule.
**Fix:** Normalize in `TEData` case of `elaborateWithVars`.

### Pitfall 4: Prelude path walk-up needs parent != dir guard
**What goes wrong:** On some filesystems, `Path.GetDirectoryName` of a root returns itself,
creating an infinite loop.
**Fix:** `if parent <> dir then dir <- parent` guards the walk-up loop.

### Pitfall 5: MOD-03 — empirical testing needed
**What goes wrong:** Code review shows all paths handle `EmptyModule` correctly. Without
reproducing the crash, the wrong location might be patched.
**Fix:** Test with `echo "" > test.fun && dotnet run -- test.fun` before patching. Also test
with a file containing only comments or whitespace.

## Code Examples

### failwith builtin (Eval.fs, add to initialBuiltinEnv)
```fsharp
// failwith : string -> 'a
"failwith", BuiltinValue (fun v ->
    match v with
    | StringValue msg -> raise (LangThreeException (StringValue msg))
    | _ -> failwith "failwith: expected string argument")
```

### failwith type scheme (TypeCheck.fs, add to initialTypeEnv)
```fsharp
// failwith : string -> 'a  (polymorphic return to match any branch type)
"failwith", Scheme([0], TArrow(TString, TVar 0))
```

### option alias (Elaborate.fs, TEData case in elaborateWithVars)
```fsharp
| TEData (name, args) ->
    let canonical = match name with "option" -> "Option" | "result" -> "Result" | n -> n
    let folder (acc, env) t =
        let (ty, env') = elaborateWithVars env t
        (ty :: acc, env')
    let (revTypes, finalVars) = List.fold folder ([], vars) args
    (TData(canonical, List.rev revTypes), finalVars)
```

### Prelude path resolution (Prelude.fs)
```fsharp
let private findPreludeDir () : string =
    // 1. CWD-relative (dev workflow: dotnet run from repo root)
    if Directory.Exists "Prelude" then "Prelude"
    else
        let assemblyLoc = System.Reflection.Assembly.GetEntryAssembly().Location
        if not (System.String.IsNullOrEmpty assemblyLoc) then
            let assemblyDir = Path.GetDirectoryName assemblyLoc
            // 2. Assembly-relative
            let candidate = Path.Combine(assemblyDir, "Prelude")
            if Directory.Exists candidate then candidate
            else
                // 3. Walk up from assembly dir (handles dotnet run from other dirs)
                let mutable dir = assemblyDir
                let mutable result = ""
                for _ in 1..6 do
                    if result = "" then
                        let c = Path.Combine(dir, "Prelude")
                        if Directory.Exists c then result <- c
                        let parent = Path.GetDirectoryName dir
                        if parent <> dir then dir <- parent
                result
        else ""
```

### Empty file guard (Program.fs, inside the File branch)
```fsharp
let moduleDecls =
    match m with
    | Module (decls, _) | NamedModule(_, decls, _) | NamespacedModule(_, decls, _) -> decls
    | EmptyModule _ -> []
if List.isEmpty moduleDecls then
    // Empty program — return unit, no crash
    printfn "()"
    0
else
    let mergedRecEnv = ...
    // rest of eval
```

## Open Questions

1. **MOD-03 exact crash trigger**
   - What we know: All code paths for `EmptyModule` look correct in current source
   - What's unclear: Is the crash triggered by whitespace-only files? Comment-only files? Or is
     the constraint stale/already-fixed?
   - Recommendation: Run `echo "" > /tmp/test.fun && cd /tmp && dotnet run --project
     /path/to/LangThree -- test.fun` before implementing fix. If no crash, the fix may be to
     handle whitespace/comment-only files specifically.

2. **`result` alias for `Result`**
   - What we know: Same pattern applies to `result`/`Result` if that type exists in Prelude
   - What's unclear: Is `Result` defined? (Prelude/Result.fun exists)
   - Recommendation: Add `"result" -> "Result"` normalization alongside `"option" -> "Option"`.

## Sources

### Primary (HIGH confidence)
- Direct read: `src/LangThree/Prelude.fs` lines 1–93
- Direct read: `src/LangThree/Program.fs` lines 1–220
- Direct read: `src/LangThree/Eval.fs` lines 1–260, 766–810
- Direct read: `src/LangThree/TypeCheck.fs` lines 1–55, 515–760
- Direct read: `src/LangThree/Elaborate.fs` lines 1–103
- Direct read: `src/LangThree/Parser.fsy` lines 437–445, 558–590
- Direct read: `src/LangThree/Ast.fs` full file
- Direct read: `src/LangThree/IndentFilter.fs` lines 211–360
- Direct read: `Prelude/Option.fun` full file

## Metadata

**Confidence breakdown:**
- STD-01 (failwith): HIGH — exact pattern from existing builtins, two files, minimal change
- TYPE-03 (option alias): HIGH — TEData normalization in Elaborate.fs, one-line match
- MOD-04 (Prelude path): HIGH — root cause confirmed, fix approach standard .NET idiom
- MOD-03 (empty file): MEDIUM — code paths appear safe, crash trigger needs empirical verification

**Research date:** 2026-03-24
**Valid until:** 2026-04-24 (stable codebase, no active refactoring)
