# Phase 55: StringBuilder & String Utilities - Research

**Researched:** 2026-03-29
**Domain:** F# interpreter — StringBuilder type, string/char methods, String/Char module functions, eprintfn
**Confidence:** HIGH

## Summary

Phase 55 adds five concrete capabilities: StringBuilder (COLL-01), string instance methods `.EndsWith`/`.Trim`/`.StartsWith` (STR-01), Char module functions (STR-02), `String.concat` module function (STR-03), and `eprintfn` builtin (STR-04).

The phase builds directly on Phase 54's FieldAccess dispatch infrastructure. String instance methods (STR-01) extend the `| StringValue s ->` match in `Eval.fs` and `| TString ->` match in `Bidir.fs` with three new field names. The `Char` and `String` module functions (STR-02, STR-03) are Prelude `.fun` files registered as modules in `moduleEnv` — the same pattern used by `Hashtable.fun` and `Array.fun`. `eprintfn` (STR-04) mirrors the existing `printfn` builtin, using `applyPrintfnArgs`-style with `stderr` instead of `stdout`.

StringBuilder (COLL-01) requires the most work: a new `StringBuilderValue` DU case in `Ast.fs`, special-cased constructor evaluation in `Eval.fs`, FieldAccess dispatch for `.Append`/`.ToString`, type-checker support in `Bidir.fs`, and a `StringBuilder` Prelude module. The key design insight is that `StringBuilder()` parses as `Constructor("StringBuilder", Some(Tuple([],_)), _)` due to the uppercase-first rule in the parser, so the `Constructor` arm in `Eval.fs` must be extended to intercept this specific name.

**Primary recommendation:** Implement in this order: (1) `eprintfn` — trivial, no dependencies; (2) STR-01 string methods — extend existing Phase 54 dispatch; (3) STR-02 Char module — new Prelude file; (4) STR-03 String module — new Prelude file; (5) COLL-01 StringBuilder — new Value case, Prelude module, full dispatch.

## Standard Stack

No new external libraries. All changes are within the interpreter.

### Core (Already Present)
| Component | Location | Purpose | Role in Phase 55 |
|-----------|----------|---------|-----------------|
| `FieldAccess` arm in `Eval.fs` | Lines 1032–1111 | Phase 54 dispatch table | Extend with `.EndsWith`/`.Trim`/`.StartsWith` for StringValue; add StringBuilderValue dispatch |
| `FieldAccess` arm in `Bidir.fs` | Lines 514–530 | Phase 54 type rules | Extend `TString` arm; add `TData("StringBuilder",[])` case |
| `Constructor` arm in `Eval.fs` | Line 973–976 | ADT constructor creation | Intercept "StringBuilder" to return `StringBuilderValue` |
| `Constructor` arm in `Bidir.fs` | Lines 64–105 | ADT constructor type check | Intercept "StringBuilder" to return `TData("StringBuilder",[])` |
| `applyPrintfnArgs` / `parsePrintfSpecifiers` | `Eval.fs` lines 92–102 | printfn implementation | Reuse for `eprintfn` (change stdout to stderr) |
| `initialBuiltinEnv` | `Eval.fs` line 166 | Flat builtin map | Add `eprintfn`, low-level `string_builder_*` helpers |
| `initialTypeEnv` | `TypeCheck.fs` line 15 | Builtin type map | Add `eprintfn` scheme |
| Prelude module pattern | `Prelude/Array.fun`, `Prelude/Hashtable.fun` | Module wrapper files | Model for new `Char.fun`, `String.fun`, `StringBuilder.fun` |

### New Value DU Case
| Case | Location | Wraps | Purpose |
|------|----------|-------|---------|
| `StringBuilderValue of System.Text.StringBuilder` | `Ast.fs` | .NET `System.Text.StringBuilder` | Mutable string builder, reference semantics |

### Installation

No packages. All changes are F# source modifications.

## Architecture Patterns

### Pattern 1: String Instance Methods (STR-01) — Minimal Extension

`"hello".EndsWith(".txt")` parses as:
```
App(FieldAccess(String("hello",_), "EndsWith", _), String(".txt",_), _)
```

The Phase 54 `| StringValue s ->` block in `Eval.fs` lines 1093–1101 already handles `.Contains`. Extend it:

```fsharp
| StringValue s ->
    match fieldName with
    | "Length" -> IntValue s.Length
    | "Contains" ->
        BuiltinValue (fun arg ->
            match arg with
            | StringValue needle -> BoolValue (s.Contains(needle))
            | _ -> failwith "String.Contains: expected string argument")
    // Phase 55: New methods
    | "EndsWith" ->
        BuiltinValue (fun arg ->
            match arg with
            | StringValue suffix -> BoolValue (s.EndsWith(suffix))
            | _ -> failwith "String.EndsWith: expected string argument")
    | "StartsWith" ->
        BuiltinValue (fun arg ->
            match arg with
            | StringValue prefix -> BoolValue (s.StartsWith(prefix))
            | _ -> failwith "String.StartsWith: expected string argument")
    | "Trim" -> StringValue (s.Trim())   // no args — returns value directly
    | _ -> failwithf "String has no property or method '%s'" fieldName
```

In `Bidir.fs`, extend `| TString ->`:
```fsharp
| TString ->
    match fieldName with
    | "Length" -> (s1, TInt)
    | "Contains" -> (s1, TArrow(TString, TBool))
    // Phase 55:
    | "EndsWith" -> (s1, TArrow(TString, TBool))
    | "StartsWith" -> (s1, TArrow(TString, TBool))
    | "Trim" -> (s1, TString)
    | _ -> raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; ... })
```

**Critical note for `.Trim()`:** `.Trim()` takes no argument but the parser sees `s.Trim()` as `App(FieldAccess(s,"Trim",_), Tuple([],_), _)`. This means `FieldAccess` on `.Trim` must return a `BuiltinValue` that accepts `TupleValue []`, NOT return a `StringValue` directly:

```fsharp
| "Trim" ->
    BuiltinValue (fun arg ->
        match arg with
        | TupleValue [] -> StringValue (s.Trim())
        | _ -> failwith "String.Trim: takes no arguments (call as .Trim())")
```

And in `Bidir.fs`, `Trim` has type `unit -> string`:
```fsharp
| "Trim" -> (s1, TArrow(TTuple [], TString))
```

### Pattern 2: Char Module (STR-02) — Prelude Module + Builtins

`Char.IsDigit('3')` parses as:
```
App(FieldAccess(Constructor("Char",None,_), "IsDigit", _), Char('3',_), _)
```

The `tryGetModuleName` check in `Eval.fs:FieldAccess` (line 1037) handles `Constructor(name, None, _) when Map.containsKey name moduleEnv`. So `Char` must be in `moduleEnv` at runtime.

**Two-layer approach:**
1. Register low-level builtins in `initialBuiltinEnv` and `initialTypeEnv`:
   ```fsharp
   "char_is_digit", BuiltinValue (fun v ->
       match v with
       | CharValue c -> BoolValue (System.Char.IsDigit(c))
       | _ -> failwith "Char.IsDigit: expected char argument")

   "char_to_upper", BuiltinValue (fun v ->
       match v with
       | CharValue c -> CharValue (System.Char.ToUpper(c))
       | _ -> failwith "Char.ToUpper: expected char argument")

   "char_is_letter", BuiltinValue (fun v -> ...)  // useful future-proofing
   ```

2. Wrap in Prelude `Char.fun`:
   ```fsharp
   module Char =
       let IsDigit c = char_is_digit c
       let ToUpper c = char_to_upper c
   ```

In `initialTypeEnv`:
```fsharp
"char_is_digit", Scheme([], TArrow(TChar, TBool))
"char_to_upper", Scheme([], TArrow(TChar, TChar))
```

### Pattern 3: String.concat Module (STR-03) — Prelude Module + Builtin

`String.concat ", " ["a"; "b"; "c"]` parses as:
```
App(App(FieldAccess(Constructor("String",None,_), "concat", _), String(", ",_)), List([...],_))
```

Register `string_concat_list` builtin in `initialBuiltinEnv`:
```fsharp
"string_concat_list", BuiltinValue (fun sepVal ->
    BuiltinValue (fun listVal ->
        match sepVal, listVal with
        | StringValue sep, ListValue strs ->
            let strings = strs |> List.map (function
                | StringValue s -> s
                | _ -> failwith "String.concat: list must contain strings")
            StringValue (System.String.Join(sep, strings))
        | _ -> failwith "String.concat: expected (string, string list)"))
```

In `initialTypeEnv`:
```fsharp
"string_concat_list", Scheme([], TArrow(TString, TArrow(TList TString, TString)))
```

Wrap in Prelude `String.fun`:
```fsharp
module String =
    let concat sep lst = string_concat_list sep lst
```

**Important naming:** The existing builtin `string_concat` (line 175) takes two strings. The new `string_concat_list` takes `string -> string list -> string`. Use a distinct name to avoid collision.

### Pattern 4: eprintfn (STR-04) — Trivial Builtin

`eprintfn "error: %s" msg` is identical to `printfn` but writes to `stderr`.

Add `applyEprintfnArgs` — copy of `applyPrintfnArgs` (line 92) but flush `stderr`:

```fsharp
let rec applyEprintfnArgs (fmt: string) (remaining: string list) (collected: Value list) : Value =
    match remaining with
    | [] ->
        let result = substitutePrintfArgs fmt (List.rev collected)
        stderr.Write(result)
        stderr.Write("\n")
        stderr.Flush()
        TupleValue []
    | _ :: rest ->
        BuiltinValue (fun argVal ->
            applyEprintfnArgs fmt rest (argVal :: collected))
```

Register in `initialBuiltinEnv`:
```fsharp
"eprintfn", BuiltinValue (fun fmtVal ->
    match fmtVal with
    | StringValue fmt ->
        let specifiers = parsePrintfSpecifiers fmt
        applyEprintfnArgs fmt specifiers []
    | _ -> failwith "eprintfn: first argument must be a format string")
```

In `initialTypeEnv`:
```fsharp
"eprintfn", Scheme([0], TArrow(TString, TVar 0))
```

### Pattern 5: StringBuilder (COLL-01) — New Value Case

#### 5a. Add StringBuilderValue to Ast.fs

Add to the Value DU after `HashtableValue`:
```fsharp
| StringBuilderValue of System.Text.StringBuilder  // Phase 55: Mutable string builder
```

Update `GetHashCode`, `valueEqual`, `valueCompare` to handle this new case (reference equality/identity).

Update `formatValue` in `Eval.fs`:
```fsharp
| StringBuilderValue sb -> sprintf "StringBuilder(\"%s\")" (sb.ToString())
```

#### 5b. Constructor interception in Eval.fs

`StringBuilder()` parses as `Constructor("StringBuilder", Some(Tuple([],_)), _)`.

In the `| Constructor (name, argOpt, _) ->` arm, add interception before `DataValue` creation:
```fsharp
| Constructor (name, argOpt, _) ->
    // Phase 55: StringBuilder() constructor interception
    match name, argOpt with
    | "StringBuilder", Some argExpr ->
        match eval recEnv moduleEnv env false argExpr with
        | TupleValue [] -> StringBuilderValue (System.Text.StringBuilder())
        | _ -> failwith "StringBuilder: expected ()"
    | _ ->
        let argValue = argOpt |> Option.map (eval recEnv moduleEnv env false)
        DataValue (name, argValue)
```

#### 5c. FieldAccess dispatch in Eval.fs

Add `StringBuilderValue` case to the value-type dispatch block:
```fsharp
| StringBuilderValue sb ->
    match fieldName with
    | "Append" ->
        BuiltinValue (fun arg ->
            match arg with
            | StringValue s ->
                sb.Append(s) |> ignore
                StringBuilderValue sb   // return self for chaining
            | CharValue c ->
                sb.Append(c) |> ignore
                StringBuilderValue sb
            | _ -> failwith "StringBuilder.Append: expected string or char argument")
    | "ToString" ->
        BuiltinValue (fun arg ->
            match arg with
            | TupleValue [] -> StringValue (sb.ToString())
            | _ -> failwith "StringBuilder.ToString: takes no arguments")
    | _ -> failwithf "StringBuilder has no property or method '%s'" fieldName
```

**Method chaining note:** `.Append` returns `StringBuilderValue sb` (the same object). This allows:
```
let result = sb.Append("a").Append("b").ToString()
```
Which evaluates step-by-step via `App(App(App(FieldAccess(sb,"Append",_), ...)))`.

#### 5d. Bidir.fs type-checker — Constructor interception

In `| Constructor (name, argOpt, span) ->`, before the `ctorEnv` lookup:
```fsharp
| Constructor (name, argOpt, span) ->
    // Phase 55: StringBuilder() type
    match name with
    | "StringBuilder" ->
        match argOpt with
        | Some argExpr ->
            let s, argTy = synth ctorEnv recEnv ctx env argExpr
            // Arg must be unit
            let s2 = unifyWithContext ctx [] span (apply s argTy) (TTuple [])
            (compose s2 s, TData("StringBuilder", []))
        | None ->
            (empty, TData("StringBuilder", []))
    | _ ->
        // ... existing ctorEnv lookup ...
```

#### 5e. Bidir.fs type-checker — FieldAccess for StringBuilder

Add `TData("StringBuilder", [])` case before `TData(typeName, typeArgs)` in the FieldAccess arm:
```fsharp
| TData("StringBuilder", []) ->
    match fieldName with
    | "Append" -> (s1, TArrow(TVar (freshVarInt()), TData("StringBuilder", [])))
        // Note: Append accepts string or char — use TVar for polymorphism, OR restrict to TString
        // Recommendation: use TString (simplest, matches the spec's .Append(str/char) — can be widened later)
    | "ToString" -> (s1, TArrow(TTuple [], TString))
    | _ -> raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; ... })
```

**Type of Append:** The spec says `.Append(str/char)`. Since the type system does not support union types (`string | char`), the simplest approach is to type `.Append` as `string -> StringBuilder` and rely on runtime checking for char. Alternative: use a fresh `TVar` so any argument is accepted (like `printf`). Recommend `TArrow(TString, TData("StringBuilder",[]))` initially — char support works at runtime but won't typecheck with char arg without widening.

Actually a better approach: use a fresh type variable, same as `printf`:
```fsharp
| "Append" ->
    let tv = freshVar()
    (s1, TArrow(tv, TData("StringBuilder", [])))
```

This accepts both string and char without requiring union types.

#### 5f. Prelude StringBuilder.fun

```fsharp
module StringBuilder =
    let create () = StringBuilder ()
    let append (sb : StringBuilder) (s : string) = sb.Append s
    let toString (sb : StringBuilder) = sb.ToString ()
```

This provides an alternative functional-style API (`StringBuilder.append sb "text"`) alongside the method-chaining style.

**Note:** `StringBuilder ()` in the Prelude file will itself go through the same `Constructor("StringBuilder",...)` interception in both eval and typechecker, so the Prelude file works naturally.

### Recommended Project Structure

```
src/LangThree/
├── Ast.fs        -- Add StringBuilderValue to Value DU
├── Eval.fs       -- Add StringBuilderValue dispatch, eprintfn, string method dispatch, string_concat_list, char builtins
├── Bidir.fs      -- Extend TString and add TData("StringBuilder",[]) type rules
├── TypeCheck.fs  -- Add eprintfn, string_concat_list, char_is_digit, char_to_upper to initialTypeEnv
Prelude/
├── Char.fun      -- module Char = let IsDigit c = char_is_digit c ...
├── String.fun    -- module String = let concat sep lst = string_concat_list sep lst ...
└── StringBuilder.fun  -- module StringBuilder = let create () = StringBuilder () ...
tests/flt/file/
├── string/       -- Add tests for EndsWith, StartsWith, Trim
├── char/         -- Add tests for Char.IsDigit, Char.ToUpper
└── stringbuilder/ -- New directory for StringBuilder tests
```

### Anti-Patterns to Avoid

- **Don't return StringValue from Trim directly:** `s.Trim()` calls `App(FieldAccess(s,"Trim",_), ())`. FieldAccess must return a `BuiltinValue` that accepts the `()` argument — not a `StringValue`.
- **Don't use `string_concat` name for the list concat builtin:** `string_concat` already exists as `string -> string -> string`. Use `string_concat_list`.
- **Don't make Append return unit:** Append must return `StringBuilderValue sb` to support method chaining.
- **Don't try to use TVar for Trim return type:** Trim always returns string. Use `TArrow(TTuple [], TString)`.
- **Don't add `open Char` or `open String` to the Prelude files:** These modules should only be accessible via qualified `Char.X` and `String.X` syntax, not polluting the flat namespace.
- **Don't skip formatValue update:** If `StringBuilderValue` is not handled in `formatValue`, printing a StringBuilder will crash or produce wrong output.
- **Don't skip CustomEquality members:** `StringBuilderValue` is added to the `Value` DU which uses `CustomEquality`/`CustomComparison`. The `GetHashCode`, `valueEqual`, and `valueCompare` methods must handle the new case. Use reference identity (like `ArrayValue` and `HashtableValue`).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| String builder accumulation | Manual list + join | `System.Text.StringBuilder` | Already in .NET BCL, correct semantics |
| `String.Join` | Manual recursion | `System.String.Join(sep, strings)` | Built-in, handles empty list |
| `String.Trim` | Manual char-scanning | `.NET string.Trim()` | Built-in, handles Unicode whitespace |
| `Char.IsDigit` | Manual range check | `System.Char.IsDigit` | Correct Unicode digit handling |
| `Char.ToUpper` | Manual ASCII offset | `System.Char.ToUpper` | Correct Unicode case mapping |
| Format string parsing | New parser | Reuse `parsePrintfSpecifiers` | Already validated, handles %% |

**Key insight:** All needed operations are in .NET BCL. The job is plumbing — connecting user-visible syntax to .NET calls through the Value DU and dispatch tables.

## Common Pitfalls

### Pitfall 1: Trim() Requires BuiltinValue, Not Direct StringValue
**What goes wrong:** `" hi ".Trim()` crashes at runtime with "Field access on non-record value" because Trim's `FieldAccess` returns a `StringValue` (the trimmed result) but then `App` tries to call it as a function with the `()` argument.
**Why it happens:** `s.Trim()` parses as `App(FieldAccess(s,"Trim",_), Tuple([],_), _)`. The `FieldAccess` result becomes the function position in `App`. If it's a `StringValue`, `applyFunc` raises "attempted to call non-function".
**How to avoid:** All zero-argument methods accessed as `obj.Method()` MUST return `BuiltinValue` from FieldAccess.
**Warning signs:** Test `" hi ".Trim()` fails with "attempted to call non-function" instead of returning `"hi"`.

### Pitfall 2: StringBuilder Constructor Not Intercepted in Bidir.fs
**What goes wrong:** `let sb = StringBuilder()` type-checks fine (the `| None ->` fallback gives it a fresh TVar) but then `sb.Append("x")` fails in Bidir.fs with `FieldAccessOnNonRecord (TVar 42)` because the FieldAccess arm doesn't know TVar 42 is a StringBuilder.
**Why it happens:** Without interception, `StringBuilder()` in Bidir.fs returns a fresh TVar. The FieldAccess arm matches `| _ -> raise FieldAccessOnNonRecord`.
**How to avoid:** Intercept `Constructor("StringBuilder", ...)` in Bidir.fs to return `TData("StringBuilder", [])`, then add the `TData("StringBuilder",[])` case in the FieldAccess arm.
**Warning signs:** Type error "FieldAccessOnNonRecord 'a" rather than a runtime error.

### Pitfall 3: Char Module Not in moduleEnv
**What goes wrong:** `Char.IsDigit('3')` raises "Module Char has no member or constructor IsDigit" or "Undefined variable Char" depending on how the lookup falls.
**Why it happens:** `tryGetModuleName` returns `None` for `Constructor("Char",None,_)` if "Char" is not in `moduleEnv`. The Prelude file must use `module Char = ...` syntax (not just flat bindings) so that `evalModuleDecls` registers it in `moduleEnv`.
**How to avoid:** Verify `Char.fun` uses `module Char = ...` syntax. The Prelude loader (Prelude.fs lines 175–183) merges `fileModuleEnv` into `result.ModuleValueEnv`.
**Warning signs:** "Undefined variable Char" at runtime or "unbound identifier" at typecheck time.

### Pitfall 4: string_concat Name Collision
**What goes wrong:** Defining `String.concat` via a builtin named `string_concat` silently replaces the existing two-string concatenation builtin, breaking `string_concat a b` usage everywhere.
**Why it happens:** `initialBuiltinEnv` is a `Map` — adding `"string_concat"` again overwrites the existing entry.
**How to avoid:** Use `"string_concat_list"` as the internal builtin name.
**Warning signs:** `string_concat "a" "b"` crashes with arity error.

### Pitfall 5: Missing StringBuilderValue in CustomEquality Members
**What goes wrong:** Compilation error "Incomplete pattern matches on this expression" in `GetHashCode` or `valueEqual`.
**Why it happens:** Adding a new DU case to a type with `CustomEquality` requires updating all pattern-matching methods on that type.
**How to avoid:** After adding `StringBuilderValue` to `Ast.fs`, search for all match expressions on `Value` (particularly `GetHashCode`, `valueEqual`, `valueCompare`) and add the new case.
**Warning signs:** F# compilation error, not runtime error.

### Pitfall 6: Prelude File Load Order
**What goes wrong:** `String.fun` references `string_concat_list` which is not yet defined when the Prelude is loaded.
**Why it happens:** `Prelude/*.fun` files are loaded via `Directory.GetFiles(...) |> Array.sort`, so alphabetical order. But `string_concat_list` is a builtin in `initialBuiltinEnv` which is always present — Prelude files can always reference builtins.
**How to avoid:** No issue. Builtins are always available. But if a Prelude file references another Prelude module, alphabetical ordering matters (e.g., `StringBuilder.fun` can reference `String.fun` only if `S-t-r-i-n-g` < `S-t-r-i-n-g-B` alphabetically — which it is).
**Warning signs:** "Undefined variable string_concat_list" only if the Prelude file tried to reference another module defined later alphabetically.

## Code Examples

### eprintfn Builtin (Eval.fs)
```fsharp
// Add after applyPrintfnArgs (line ~102):
let rec applyEprintfnArgs (fmt: string) (remaining: string list) (collected: Value list) : Value =
    match remaining with
    | [] ->
        let result = substitutePrintfArgs fmt (List.rev collected)
        stderr.Write(result)
        stderr.Write("\n")
        stderr.Flush()
        TupleValue []
    | _ :: rest ->
        BuiltinValue (fun argVal ->
            applyEprintfnArgs fmt rest (argVal :: collected))

// In initialBuiltinEnv:
"eprintfn", BuiltinValue (fun fmtVal ->
    match fmtVal with
    | StringValue fmt ->
        let specifiers = parsePrintfSpecifiers fmt
        applyEprintfnArgs fmt specifiers []
    | _ -> failwith "eprintfn: first argument must be a format string")
```

### StringBuilderValue DU case (Ast.fs)
```fsharp
// In the Value DU, after HashtableValue:
| StringBuilderValue of System.Text.StringBuilder  // Phase 55: Mutable string builder

// In GetHashCode:
| StringBuilderValue sb -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(sb)

// In valueEqual:
| StringBuilderValue sb1, StringBuilderValue sb2 -> System.Object.ReferenceEquals(sb1, sb2)

// In valueCompare:
| StringBuilderValue _, StringBuilderValue _ -> 0
```

### StringBuilder Constructor Interception (Eval.fs)
```fsharp
// Modify the | Constructor (name, argOpt, _) -> arm:
| Constructor (name, argOpt, _) ->
    match name, argOpt with
    | "StringBuilder", Some argExpr ->
        match eval recEnv moduleEnv env false argExpr with
        | TupleValue [] -> StringBuilderValue (System.Text.StringBuilder())
        | StringValue initial -> StringBuilderValue (System.Text.StringBuilder(initial))
        | _ -> failwith "StringBuilder: expected () or string argument"
    | "StringBuilder", None ->
        StringBuilderValue (System.Text.StringBuilder())
    | _ ->
        let argValue = argOpt |> Option.map (eval recEnv moduleEnv env false)
        DataValue (name, argValue)
```

### StringBuilder FieldAccess dispatch (Eval.fs, in the value-type dispatch block)
```fsharp
| StringBuilderValue sb ->
    match fieldName with
    | "Append" ->
        BuiltinValue (fun arg ->
            match arg with
            | StringValue s -> sb.Append(s) |> ignore; StringBuilderValue sb
            | CharValue c -> sb.Append(c) |> ignore; StringBuilderValue sb
            | _ -> failwith "StringBuilder.Append: expected string or char")
    | "ToString" ->
        BuiltinValue (fun arg ->
            match arg with
            | TupleValue [] -> StringValue (sb.ToString())
            | _ -> failwith "StringBuilder.ToString: takes no arguments")
    | _ -> failwithf "StringBuilder has no property or method '%s'" fieldName
```

### Char.fun (Prelude)
```fsharp
module Char =
    let IsDigit c = char_is_digit c
    let ToUpper c = char_to_upper c
    let IsLetter c = char_is_letter c
    let IsUpper c = char_is_upper c
    let IsLower c = char_is_lower c
    let ToLower c = char_to_lower c
```

### String.fun (Prelude)
```fsharp
module String =
    let concat sep lst = string_concat_list sep lst
```

### StringBuilder.fun (Prelude)
```fsharp
module StringBuilder =
    let create () = StringBuilder ()
    let append sb s = sb.Append s
    let toString sb = sb.ToString ()
```

### flt Test: StringBuilder
```
// Test: StringBuilder basic usage (COLL-01)
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let sb = StringBuilder ()
let _ = sb.Append("hello")
let _ = sb.Append(" ")
let _ = sb.Append("world")
let result = sb.ToString ()
let _ = println result
// --- Stdout:
hello world
```

### flt Test: String methods
```
// Test: EndsWith, StartsWith, Trim (STR-01)
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let _ = println (to_string ("hello.txt".EndsWith(".txt")))
let _ = println (to_string ("hello".StartsWith("he")))
let trimmed = " hi ".Trim ()
let _ = println trimmed
// --- Stdout:
true
true
hi
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual string accumulation via `^^` | `StringBuilder().Append(...).ToString()` | Phase 55 | Efficient mutable accumulation |
| No `eprintfn` (use `eprintln`) | `eprintfn "error: %s" msg` | Phase 55 | Printf-style stderr output |
| No `String.concat` module function | `String.concat sep list` | Phase 55 | F# idiomatic join |
| No Char module | `Char.IsDigit c`, `Char.ToUpper c` | Phase 55 | F# idiomatic char predicates |
| No string methods | `"hi".Trim()`, `"a".EndsWith("b")` | Phase 55 | Method-chaining string processing |

**Deprecated/outdated:**
- Phase 54 only added `.Length` and `.Contains` for strings. Phase 55 extends without removing anything.

## Open Questions

1. **Type of StringBuilder.Append in Bidir.fs**
   - What we know: `.Append` accepts both `string` and `char`. The type system has no union types.
   - What's unclear: Whether `TArrow(TString, TData("StringBuilder",[]))` or `TArrow(TVar fresh, TData("StringBuilder",[]))` is better.
   - Recommendation: Use a fresh `TVar` (polymorphic) so both string and char arguments typecheck. This is consistent with how `printf` handles variadic args. The runtime enforces the actual type restriction.

2. **Should `.Trim()` also handle `.TrimStart()` and `.TrimEnd()`?**
   - What we know: The spec only lists `.Trim()`.
   - Recommendation: Implement only `.Trim()` as scoped. `.TrimStart()`/`.TrimEnd()` can be added in a future phase.

3. **Should `StringBuilder()` also accept an initial string argument?**
   - What we know: The spec shows `StringBuilder()` (no arg). F# `System.Text.StringBuilder` also has `StringBuilder(string)`.
   - Recommendation: Support `StringBuilder()` (empty) and as a bonus handle `StringBuilder("initial")` since the interception code has the right structure. Does not add complexity.

4. **Should `String.fun` also include other methods?**
   - What we know: Only `String.concat` is required (STR-03).
   - Recommendation: Add only `concat` for now. Other string functions (if needed) belong to later phases or can be requested separately.

## Sources

### Primary (HIGH confidence)
- Direct codebase analysis: `Eval.fs` lines 1032–1111 (Phase 54 FieldAccess dispatch — exact extension points)
- Direct codebase analysis: `Eval.fs` lines 80–102, 249–271 (applyPrintfnArgs/applyPrintfArgs — model for eprintfn)
- Direct codebase analysis: `Eval.fs` lines 164–572 (initialBuiltinEnv — registration patterns)
- Direct codebase analysis: `Bidir.fs` lines 63–105 (Constructor arm — fallback to freshVar)
- Direct codebase analysis: `Bidir.fs` lines 514–530 (FieldAccess TString/TArray — extension model)
- Direct codebase analysis: `Ast.fs` lines 194–270 (Value DU with CustomEquality/CustomComparison)
- Direct codebase analysis: `TypeCheck.fs` lines 15–148 (initialTypeEnv registration patterns)
- Direct codebase analysis: `Parser.fsy` lines 300–311, 324–330 (Constructor parsing rule)
- Direct codebase analysis: `Eval.fs` line 1037 (tryGetModuleName pattern for uppercase identifiers)
- Direct codebase analysis: `Prelude/Array.fun`, `Prelude/Hashtable.fun` (module wrapper patterns)
- Direct codebase analysis: `.planning/STATE.md` (Phase 54 decisions and current state)
- Direct codebase analysis: `.planning/REQUIREMENTS.md` (COLL-01, STR-01 through STR-04 specifications)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — pure interpreter extension, no new external dependencies
- Architecture: HIGH — all dispatch points directly verified in source, module pattern verified
- Pitfalls: HIGH — parse behavior verified, CustomEquality confirmed mandatory, Trim() zero-arg issue verified

**Research date:** 2026-03-29
**Valid until:** Stable (internal codebase, no external dependencies)
