# Phase 60: Builtins & Prelude Modules - Research

**Researched:** 2026-03-29
**Domain:** LangThree interpreter — Eval.fs builtins, TypeCheck.fs type schemes, Prelude .fun module files
**Confidence:** HIGH

## Summary

Phase 60 adds 5 new builtin functions (BLT-01 through BLT-05) and extends 3 Prelude modules (MOD-01 through MOD-03) to give users full module-function API coverage for string, hashtable, and StringBuilder operations. This is the prerequisite step before dot-notation dispatch is removed in Phases 61-62.

The implementation pattern is extremely well-established in this codebase: add a builtin to `Eval.fs initialBuiltinEnv`, add its type scheme to `TypeCheck.fs initialTypeEnv`, then expose it via a one-liner wrapper in the appropriate `Prelude/*.fun` module. All 5 existing Prelude modules (Array, Char, HashSet, Hashtable, Queue, MutableList, StringBuilder, String) follow this exact pattern. No parser changes, no AST changes, no Bidir.fs changes are needed for this phase.

The only non-trivial aspect is the `StringBuilder.append -> StringBuilder.add` rename (MOD-03): the existing `stringbuilder_append` builtin stays unchanged (it's referenced by old tests), but the Prelude module wrapper renames the exposed function from `append` to `add`. The conflict reason is that `open List` is in effect across the Prelude evaluation environment, so `List.append` shadows any prelude-level `append` binding.

**Primary recommendation:** Add 5 builtins to Eval.fs + TypeCheck.fs using existing patterns, then update 3 Prelude .fun files. Two plans: Plan 01 handles builtins + TypeCheck, Plan 02 handles Prelude .fun files + flt tests.

## Standard Stack

This is an internal language implementation project. There is no external package dependency — all changes are in-project F# source files.

### Core Files
| File | Purpose | Role in This Phase |
|------|---------|-------------------|
| `src/LangThree/Eval.fs` | Runtime evaluator | Add 5 new builtin entries to `initialBuiltinEnv` |
| `src/LangThree/TypeCheck.fs` | Type checker | Add 5 new type schemes to `initialTypeEnv` |
| `Prelude/String.fun` | String module | Add endsWith, startsWith, trim, length, contains |
| `Prelude/Hashtable.fun` | Hashtable module | Add tryGetValue, count |
| `Prelude/StringBuilder.fun` | StringBuilder module | Rename append to add |

### No Changes Needed
| File | Reason |
|------|--------|
| `src/LangThree/Ast.fs` | No new value types |
| `src/LangThree/Bidir.fs` | No new type synthesis rules needed |
| `src/LangThree/Parser.fsy` | No new syntax |
| `src/LangThree/Lexer.fsl` | No new tokens |

## Architecture Patterns

### Recommended Project Structure

```
Eval.fs initialBuiltinEnv
  string_endswith      <- BLT-01
  string_startswith    <- BLT-02
  string_trim          <- BLT-03
  hashtable_trygetvalue  <- BLT-04
  hashtable_count        <- BLT-05

TypeCheck.fs initialTypeEnv
  string_endswith      <- BLT-01 type scheme
  string_startswith    <- BLT-02 type scheme
  string_trim          <- BLT-03 type scheme
  hashtable_trygetvalue  <- BLT-04 type scheme
  hashtable_count        <- BLT-05 type scheme

Prelude/String.fun     <- MOD-01
  (add endsWith, startsWith, trim, length, contains)

Prelude/Hashtable.fun  <- MOD-02
  (add tryGetValue, count)

Prelude/StringBuilder.fun  <- MOD-03
  (rename append -> add)
```

### Pattern 1: Adding a Builtin (Eval.fs)

Every builtin follows this structure in `initialBuiltinEnv`:

```fsharp
// string_endswith : string -> string -> bool
"string_endswith", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue s ->
        BuiltinValue (fun v2 ->
            match v2 with
            | StringValue suffix -> BoolValue (s.EndsWith(suffix))
            | _ -> failwith "string_endswith: second argument must be string")
    | _ -> failwith "string_endswith: first argument must be string")

// string_startswith : string -> string -> bool
"string_startswith", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue s ->
        BuiltinValue (fun v2 ->
            match v2 with
            | StringValue prefix -> BoolValue (s.StartsWith(prefix))
            | _ -> failwith "string_startswith: second argument must be string")
    | _ -> failwith "string_startswith: first argument must be string")

// string_trim : string -> string
"string_trim", BuiltinValue (fun v ->
    match v with
    | StringValue s -> StringValue (s.Trim())
    | _ -> failwith "string_trim: expected string argument")

// hashtable_trygetvalue : hashtable<'k, 'v> -> 'k -> (bool * 'v)
"hashtable_trygetvalue", BuiltinValue (fun htVal ->
    BuiltinValue (fun keyVal ->
        match htVal with
        | HashtableValue ht ->
            match ht.TryGetValue(keyVal) with
            | true, v  -> TupleValue [BoolValue true;  v]
            | false, _ -> TupleValue [BoolValue false; TupleValue []]
        | _ -> failwith "hashtable_trygetvalue: expected hashtable"))

// hashtable_count : hashtable<'k, 'v> -> int
"hashtable_count", BuiltinValue (fun htVal ->
    match htVal with
    | HashtableValue ht -> IntValue ht.Count
    | _ -> failwith "hashtable_count: expected hashtable")
```

These go in `initialBuiltinEnv` at the end of the Phase 39 Hashtable section, and after string_contains in the string section.

### Pattern 2: Adding Type Schemes (TypeCheck.fs)

Every builtin's type scheme mirrors its runtime signature in `initialTypeEnv`:

```fsharp
// BLT-01: string_endswith : string -> string -> bool
"string_endswith", Scheme([], TArrow(TString, TArrow(TString, TBool)))

// BLT-02: string_startswith : string -> string -> bool
"string_startswith", Scheme([], TArrow(TString, TArrow(TString, TBool)))

// BLT-03: string_trim : string -> string
"string_trim", Scheme([], TArrow(TString, TString))

// BLT-04: hashtable_trygetvalue : hashtable<'k, 'v> -> 'k -> (bool * 'v)
// The return type is a tuple (bool * 'v) — represented as TTuple [TBool; TVar 1]
"hashtable_trygetvalue", Scheme([0; 1], TArrow(THashtable (TVar 0, TVar 1), TArrow(TVar 0, TTuple [TBool; TVar 1])))

// BLT-05: hashtable_count : hashtable<'k, 'v> -> int
"hashtable_count", Scheme([0; 1], TArrow(THashtable (TVar 0, TVar 1), TInt))
```

### Pattern 3: Prelude .fun Module Wrapper

Each Prelude function is a one-liner wrapping the builtin:

```
// Prelude/String.fun — MOD-01
module String =
    let concat sep lst = string_concat_list sep lst
    let endsWith s suffix = string_endswith s suffix
    let startsWith s prefix = string_startswith s prefix
    let trim s = string_trim s
    let length s = string_length s
    let contains s needle = string_contains s needle
```

```
// Prelude/Hashtable.fun — MOD-02
module Hashtable =
    let create ()           = hashtable_create ()
    let get ht key          = hashtable_get ht key
    let set ht key value    = hashtable_set ht key value
    let containsKey ht key  = hashtable_containsKey ht key
    let keys ht             = hashtable_keys ht
    let remove ht key       = hashtable_remove ht key
    let tryGetValue ht key  = hashtable_trygetvalue ht key
    let count ht            = hashtable_count ht
```

```
// Prelude/StringBuilder.fun — MOD-03
module StringBuilder =
    let create () = stringbuilder_create ()
    let add sb s  = stringbuilder_append sb s
    let toString sb = stringbuilder_tostring sb
```

Note: `append` is removed and replaced by `add`. Old tests using `StringBuilder.append` will break — but per v7.1 requirements, this is intentional. The underlying `stringbuilder_append` builtin is NOT renamed (only the module wrapper changes).

### Pattern 4: flt Test File Format

All flt tests use `// --- Output:` (not `// --- Stdout:`). The FsLit runner only supports `// --- Output:`. Tests must include `()` on the last line when the final statement is `let _ = println ...`.

```flt
// Test: String module endsWith/startsWith/trim (BLT-01, BLT-02, BLT-03, MOD-01)
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let _ = println (to_string (String.endsWith "hello.txt" ".txt"))
let _ = println (to_string (String.startsWith "hello" "he"))
let _ = println (String.trim "  hi  ")
// --- Output:
true
true
hi
()
```

### Anti-Patterns to Avoid

- **Using `// --- Stdout:` instead of `// --- Output:`**: FsLit silently ignores Stdout sections, tests will always pass vacuously.
- **Blank lines inside .fun module bodies**: A blank line in a Prelude .fun file causes `NEWLINE(0) = DEDENT out of module` parse error. All Prelude .fun file bodies must have no blank lines.
- **Adding `open` statement at end of String.fun or Hashtable.fun**: Only List.fun and Core.fun have `open` at the end (to pollute the top-level namespace). String, Hashtable, StringBuilder modules should NOT have `open Module` at the bottom — they are used via qualified access only.
- **Reusing the name `append` in StringBuilder.fun**: `open List` is in effect, so `List.append` is in scope. A `StringBuilder.append` function inside `module StringBuilder` would shadow but could still cause confusion in tests. The name `add` is the correct replacement.
- **Forgetting type scheme in TypeCheck.fs**: If a builtin exists in Eval.fs but has no entry in `initialTypeEnv`, any .fun file that calls it will get a type error at Prelude load time, and any user code calling the function directly also gets a type error.
- **Multi-arg lambdas in .fun files**: `fun i x -> ...` fails to parse; use curried form `fun i -> fun x -> ...`.
- **Unit pattern in .fun file params**: `let f x () = ...` does not parse in .fun files. Use `let f x u = ...` with a named param.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| String.EndsWith runtime | Custom substring check | `s.EndsWith(suffix)` .NET method | Already used in FieldAccess dispatch (Eval.fs line 1407) |
| String.StartsWith runtime | Custom prefix check | `s.StartsWith(prefix)` .NET method | Already used in FieldAccess dispatch (Eval.fs line 1412) |
| String.Trim runtime | Custom whitespace strip | `s.Trim()` .NET method | Already used in FieldAccess dispatch (Eval.fs line 1418) |
| Hashtable.TryGetValue runtime | Two-step containsKey+get | `ht.TryGetValue(keyVal)` .NET method | Already used in FieldAccess dispatch (Eval.fs line 1484) |
| Hashtable.Count runtime | Manual key counting | `ht.Count` .NET property | Already used in FieldAccess dispatch (Eval.fs line 1487) |

**Key insight:** Every new builtin is a thin wrapper around .NET APIs already in use in the FieldAccess dispatch section (Eval.fs lines 1396-1489). The logic is already proven — it just needs to be exposed as a named builtin so users can call it without dot notation.

## Common Pitfalls

### Pitfall 1: Missing type scheme in TypeCheck.fs
**What goes wrong:** Prelude .fun file loads, calls the new builtin, gets `Unbound variable: string_endswith` type error at startup. All programs fail to run.
**Why it happens:** `initialBuiltinEnv` and `initialTypeEnv` must both be updated — they are separate Maps.
**How to avoid:** Always add both: the BuiltinValue in Eval.fs AND the Scheme in TypeCheck.fs in the same commit.
**Warning signs:** `Warning: Type error in String.fun: Unbound variable: string_endswith` in stderr at startup.

### Pitfall 2: StringBuilder.append naming conflict
**What goes wrong:** If `StringBuilder.fun` keeps `let append sb s = ...`, then `open List` (which runs after StringBuilder.fun loads) makes `List.append` shadow it at the module scope level. Code that calls `StringBuilder.append` works but is fragile.
**Why it happens:** The Prelude load order sorts by filename: Array.fun, Char.fun, Core.fun (opens Core), HashSet.fun, Hashtable.fun, List.fun (**opens List**), MutableList.fun, Queue.fun, Result.fun, StringBuilder.fun.
**How to avoid:** Rename to `add` in the module (MOD-03). The underlying `stringbuilder_append` builtin is not renamed.
**Warning signs:** Existing `StringBuilder.append` usages will break — this is expected and intentional per v7.1.

### Pitfall 3: flt test output format
**What goes wrong:** Writing `// --- Stdout:` instead of `// --- Output:` causes the test runner to silently ignore expected output. Tests always pass even when the implementation is wrong.
**Why it happens:** FsLit only recognizes `// --- Output:` for stdout matching.
**How to avoid:** Always use `// --- Output:` in new flt tests. Check existing passing tests for the pattern.
**Warning signs:** New tests pass immediately without any code changes.

### Pitfall 4: hashtable_trygetvalue return type mismatch
**What goes wrong:** Using `Scheme([0; 1], TArrow(..., TTuple [TBool; TVar 1]))` is correct. If the value type variable is not included in the return tuple type, the type checker infers an incorrect type and downstream code cannot pattern-match the tuple.
**Why it happens:** The TryGetValue result is `(bool * 'v)` where `'v` is the hashtable value type. Both type variables 0 and 1 must appear in the scheme's quantifier.
**How to avoid:** Use `Scheme([0; 1], TArrow(THashtable (TVar 0, TVar 1), TArrow(TVar 0, TTuple [TBool; TVar 1])))`.
**Warning signs:** Pattern matching `let (found, value) = Hashtable.tryGetValue ht key` fails to type-check.

### Pitfall 5: String.length and String.contains shadowing builtins
**What goes wrong:** `string_length` and `string_contains` already exist as builtins. Adding `String.length` and `String.contains` module wrappers is fine — but the Prelude must not accidentally re-export them in a way that clobbers the flat-namespace versions.
**Why it happens:** The String module is NOT opened (no `open String` at the end of String.fun), so `String.length` and `string_length` coexist independently.
**How to avoid:** Do not add `open String` to String.fun. Verify that `string_length "abc"` still works after the change.
**Warning signs:** Existing tests calling `string_length` directly start failing.

## Code Examples

### BLT-01 through BLT-03: String builtins in Eval.fs

Add after the `string_contains` entry (~line 235), before `to_string`:

```fsharp
// Phase 60: string_endswith : string -> string -> bool  (BLT-01)
"string_endswith", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue s ->
        BuiltinValue (fun v2 ->
            match v2 with
            | StringValue suffix -> BoolValue (s.EndsWith(suffix))
            | _ -> failwith "string_endswith: second argument must be string")
    | _ -> failwith "string_endswith: first argument must be string")

// Phase 60: string_startswith : string -> string -> bool  (BLT-02)
"string_startswith", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue s ->
        BuiltinValue (fun v2 ->
            match v2 with
            | StringValue prefix -> BoolValue (s.StartsWith(prefix))
            | _ -> failwith "string_startswith: second argument must be string")
    | _ -> failwith "string_startswith: first argument must be string")

// Phase 60: string_trim : string -> string  (BLT-03)
"string_trim", BuiltinValue (fun v ->
    match v with
    | StringValue s -> StringValue (s.Trim())
    | _ -> failwith "string_trim: expected string argument")
```

### BLT-04 and BLT-05: Hashtable builtins in Eval.fs

Add after the `hashtable_remove` entry (~line 640), before `// Phase 55: StringBuilder builtins`:

```fsharp
// Phase 60: hashtable_trygetvalue : hashtable<'k, 'v> -> 'k -> (bool * 'v)  (BLT-04)
"hashtable_trygetvalue", BuiltinValue (fun htVal ->
    BuiltinValue (fun keyVal ->
        match htVal with
        | HashtableValue ht ->
            match ht.TryGetValue(keyVal) with
            | true, v  -> TupleValue [BoolValue true;  v]
            | false, _ -> TupleValue [BoolValue false; TupleValue []]
        | _ -> failwith "hashtable_trygetvalue: expected hashtable"))

// Phase 60: hashtable_count : hashtable<'k, 'v> -> int  (BLT-05)
"hashtable_count", BuiltinValue (fun htVal ->
    match htVal with
    | HashtableValue ht -> IntValue ht.Count
    | _ -> failwith "hashtable_count: expected hashtable")
```

### Type schemes in TypeCheck.fs

Add after `string_contains` entry (~line 33), and after `hashtable_remove` entry (~line 161):

```fsharp
// Phase 60: String operation builtins (BLT-01, BLT-02, BLT-03)
"string_endswith",   Scheme([], TArrow(TString, TArrow(TString, TBool)))
"string_startswith", Scheme([], TArrow(TString, TArrow(TString, TBool)))
"string_trim",       Scheme([], TArrow(TString, TString))

// Phase 60: Hashtable operation builtins (BLT-04, BLT-05)
"hashtable_trygetvalue", Scheme([0; 1], TArrow(THashtable (TVar 0, TVar 1), TArrow(TVar 0, TTuple [TBool; TVar 1])))
"hashtable_count",       Scheme([0; 1], TArrow(THashtable (TVar 0, TVar 1), TInt))
```

### Updated Prelude/String.fun

```
module String =
    let concat sep lst = string_concat_list sep lst
    let endsWith s suffix = string_endswith s suffix
    let startsWith s prefix = string_startswith s prefix
    let trim s = string_trim s
    let length s = string_length s
    let contains s needle = string_contains s needle
```

### Updated Prelude/Hashtable.fun

```
module Hashtable =
    let create ()           = hashtable_create ()
    let get ht key          = hashtable_get ht key
    let set ht key value    = hashtable_set ht key value
    let containsKey ht key  = hashtable_containsKey ht key
    let keys ht             = hashtable_keys ht
    let remove ht key       = hashtable_remove ht key
    let tryGetValue ht key  = hashtable_trygetvalue ht key
    let count ht            = hashtable_count ht
```

### Updated Prelude/StringBuilder.fun

```
module StringBuilder =
    let create () = stringbuilder_create ()
    let add sb s  = stringbuilder_append sb s
    let toString sb = stringbuilder_tostring sb
```

### Sample flt test: String module

```
// Test: String module endsWith, startsWith, trim (BLT-01..03, MOD-01)
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let _ = println (to_string (String.endsWith "hello.txt" ".txt"))
let _ = println (to_string (String.startsWith "hello" "he"))
let _ = println (String.trim "  hi  ")
// --- Output:
true
true
hi
()
```

### Sample flt test: Hashtable module tryGetValue/count

```
// Test: Hashtable.tryGetValue and Hashtable.count (BLT-04, BLT-05, MOD-02)
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let ht = Hashtable.create ()
let _ = Hashtable.set ht "a" 1
let _ = Hashtable.set ht "b" 2
let r1 = Hashtable.tryGetValue ht "a"
let r2 = Hashtable.tryGetValue ht "z"
let _ = println (to_string r1)
let _ = println (to_string r2)
let _ = println (to_string (Hashtable.count ht))
// --- Output:
(true, 1)
(false, ())
2
()
```

### Sample flt test: StringBuilder.add rename

```
// Test: StringBuilder.add (renamed from append, MOD-03)
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let sb = StringBuilder.create ()
let _ = StringBuilder.add sb "hello"
let _ = StringBuilder.add sb " world"
let result = StringBuilder.toString sb
let _ = println result
// --- Output:
hello world
()
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `.EndsWith`, `.StartsWith`, `.Trim` via FieldAccess dispatch | `String.endsWith`, `String.startsWith`, `String.trim` module functions | Phase 60 | Dot notation can be removed later |
| `.TryGetValue`, `.Count` via HashtableValue FieldAccess dispatch | `Hashtable.tryGetValue`, `Hashtable.count` module functions | Phase 60 | Dot notation can be removed later |
| `StringBuilder.append` (conflicts with `List.append`) | `StringBuilder.add` | Phase 60 | Resolves scope pollution from `open List` |

**Deprecated/outdated after this phase:**
- `StringBuilder.append`: Replaced by `StringBuilder.add`. Old flt tests using `.Append(...)` dot notation will be converted in Phase 61.
- Direct builtin calls (`string_endswith`, `hashtable_trygetvalue` etc.) are valid but not the recommended API — use module-qualified versions.

## Open Questions

1. **Should stringbuilder_append be kept in TypeCheck.fs/Eval.fs?**
   - What we know: The old `stringbuilder_append` builtin is still referenced by the Prelude `StringBuilder.add` wrapper (which calls `stringbuilder_append` internally). The builtin must remain.
   - What's unclear: Whether any flt tests call `stringbuilder_append` directly (rather than through the module).
   - Recommendation: Keep `stringbuilder_append` as-is; only change the module wrapper name. Verify no flt tests call the builtin directly.

2. **String.length and String.contains — expose existing builtins via module?**
   - What we know: `string_length` and `string_contains` already exist as builtins. MOD-01 says to expose them as `String.length` and `String.contains`.
   - What's unclear: Whether there are tests that call `String.length` or `String.contains` with qualified syntax and expect them to fail (i.e., currently unbound).
   - Recommendation: Add them to String.fun as one-liners wrapping the existing builtins. This is pure additive — no breaking change.

3. **Prelude load order: does StringBuilder.fun load after List.fun?**
   - What we know: Prelude files are loaded in alphabetical order via `Array.sort`. List.fun sorts before StringBuilder.fun (L < S), so `open List` is in scope when StringBuilder.fun is type-checked.
   - What's unclear: Whether this ordering matters for the module definitions themselves (it does for conflict).
   - Recommendation: Confirmed that `open List` at the bottom of List.fun executes before StringBuilder.fun loads, so using `add` instead of `append` is essential.

## Sources

### Primary (HIGH confidence)
- `/Users/ohama/vibe-coding/LangThree/src/LangThree/Eval.fs` — Full `initialBuiltinEnv` map inspected; all 5 builtin implementation patterns verified against existing code at lines 189-780
- `/Users/ohama/vibe-coding/LangThree/src/LangThree/TypeCheck.fs` — Full `initialTypeEnv` map inspected; all type scheme patterns verified at lines 15-210
- `/Users/ohama/vibe-coding/LangThree/Prelude/String.fun`, `Hashtable.fun`, `StringBuilder.fun` — Current state of all 3 modules inspected
- `/Users/ohama/vibe-coding/LangThree/.planning/milestones/v7.1-REQUIREMENTS.md` — BLT-01..05, MOD-01..03 requirements read directly
- `/Users/ohama/vibe-coding/LangThree/.planning/STATE.md` — Prior decisions including `StringBuilder.append` conflict confirmed

### Secondary (MEDIUM confidence)
- Existing flt test files inspected: `stringbuilder-prelude.flt`, `hashtable-keys-tryget.flt`, `property-string-length.flt`, `property-string-contains.flt` — output format verified

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — inspected all relevant source files directly; patterns are unambiguous
- Architecture: HIGH — builtin + type scheme + .fun wrapper is the established pattern for 6 prior phases (Phase 39, 55, 56, 57)
- Pitfalls: HIGH — all pitfalls come from documented STATE.md decisions or direct code inspection
- flt test format: HIGH — confirmed `// --- Output:` requirement from STATE.md and working test files

**Research date:** 2026-03-29
**Valid until:** Stable (this is an internal project with no external dependency churn)
