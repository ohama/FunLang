# Phase 11: String Operations - Research

**Researched:** 2026-03-10
**Domain:** F# interpreter — built-in function registration, string operations, eval/type-env wiring
**Confidence:** HIGH

## Summary

Phase 11 adds six string built-in functions (`string_length`, `string_concat`, `string_sub`, `string_contains`, `to_string`, `string_to_int`) to the LangThree interpreter. The codebase already has `TString` / `StringValue` / `String` AST node and basic `+` concatenation in `Eval.fs`. The type system has `initialTypeEnv` in `TypeCheck.fs` where prelude function type schemes live.

The central architectural challenge is that ALL prelude functions currently in `initialTypeEnv` (map, filter, fold, etc.) exist only as type schemes — they are NOT available in the eval environment. `Prelude.loadPrelude()` returns `emptyEnv` because there is no `Prelude.fun` file. String functions need to be called at runtime, so they must be present in the eval environment as values. This requires adding a new `BuiltinValue` variant to the `Value` DU and wiring it into the `App` eval case, OR encoding each built-in as a chain of nested `FunctionValue` closures with F# lambda bodies via a new mechanism.

The cleanest, minimal-impact approach: add `BuiltinValue of (Value -> Value)` to `Ast.Value`, handle it in `Eval.eval App` case, create an `initialBuiltinEnv : Env` in `Eval.fs` (or a new `Builtins.fs`), and merge it into `initialEnv` in `Program.fs` and `Repl.fs`. Curried builtins (like `string_sub` which takes three arguments) use nested `BuiltinValue` wrappers.

**Primary recommendation:** Add `BuiltinValue of (Value -> Value)` to the `Value` DU. Implement all six string functions as F# lambdas registered in a new `initialBuiltinEnv`. Add type schemes to `initialTypeEnv`. Wire both together in `Program.fs` and `Repl.fs`.

## Standard Stack

Phase 11 operates entirely within the existing project stack. No new external libraries are needed.

### Core (existing)

| Tool | Version | Purpose | Notes |
|------|---------|---------|-------|
| F# / .NET 10 | 10 | Implementation language | All source in `src/LangThree/` |
| FsLexYacc | embedded | Lexer/Parser | No changes needed for Phase 11 |
| Expecto | embedded | Unit test framework | Tests in `tests/LangThree.Tests/` |
| .flt test framework | embedded | Integration tests | Tests in `tests/flt/` |

### Files to Modify

| File | Change | Reason |
|------|--------|--------|
| `Ast.fs` | Add `BuiltinValue of (Value -> Value)` to `Value` DU | Enable F#-native built-ins |
| `Eval.fs` | Handle `BuiltinValue` in `App` case; add `initialBuiltinEnv` | Runtime built-in dispatch |
| `TypeCheck.fs` | Add six string function type schemes to `initialTypeEnv` | Type checking of string ops |
| `Program.fs` | Merge `initialBuiltinEnv` into `initialEnv` | Both `--expr` and file modes |
| `Repl.fs` | Merge `initialBuiltinEnv` into `initialEnv` | REPL mode |
| `Format.fs` | Handle `BuiltinValue` in `formatValue` | Prevent match-incomplete warning |

### No New Libraries Required

All F# string operations (`String.length`, `String.concat`, `System.String.Substring`, `System.String.Contains`, `System.Int32.TryParse`, `string`, `sprintf`) are built into .NET.

## Architecture Patterns

### Pattern 1: `BuiltinValue` — Native F# Function Carrier

**What:** Add a new `Value` case that carries an F# function `Value -> Value`. The `App` eval case dispatches to it. Curried multi-argument builtins return intermediate `BuiltinValue` carrying a partially-applied closure.

**When to use:** All string built-ins in Phase 11. Future built-ins (numeric, I/O) would follow the same pattern.

**Example:**
```fsharp
// Ast.fs — add to Value DU:
| BuiltinValue of fn: (Value -> Value)

// Eval.fs — App case extension:
| App (funcExpr, argExpr, _) ->
    let funcVal = eval recEnv moduleEnv env funcExpr
    match funcVal with
    | FunctionValue (param, body, closureEnv) ->
        // ... existing code ...
    | BuiltinValue fn ->
        let argValue = eval recEnv moduleEnv env argExpr
        fn argValue
    | _ -> failwith "Type error: attempted to call non-function"
```

### Pattern 2: `initialBuiltinEnv` — Built-in Eval Registration

**What:** A `Map<string, Value>` (type alias `Env`) defined in `Eval.fs` containing all built-in function values. Merged into the prelude env at startup.

**When to use:** Any built-in that needs to be callable from user code.

**Example:**
```fsharp
// Eval.fs — new section after emptyEnv:
let initialBuiltinEnv : Env =
    Map.ofList [
        "string_length", BuiltinValue (fun v ->
            match v with
            | StringValue s -> IntValue (String.length s)
            | _ -> failwith "string_length: expected string")

        "string_concat", BuiltinValue (fun v1 ->
            BuiltinValue (fun v2 ->
                match v1, v2 with
                | StringValue s1, StringValue s2 -> StringValue (s1 + s2)
                | _ -> failwith "string_concat: expected two strings"))

        "string_sub", BuiltinValue (fun v1 ->
            BuiltinValue (fun v2 ->
                BuiltinValue (fun v3 ->
                    match v1, v2, v3 with
                    | StringValue s, IntValue start, IntValue len ->
                        StringValue (s.[start .. start + len - 1])
                    | _ -> failwith "string_sub: expected string int int")))

        "string_contains", BuiltinValue (fun v1 ->
            BuiltinValue (fun v2 ->
                match v1, v2 with
                | StringValue haystack, StringValue needle ->
                    BoolValue (haystack.Contains(needle))
                | _ -> failwith "string_contains: expected two strings"))

        "to_string", BuiltinValue (fun v ->
            match v with
            | IntValue n -> StringValue (string n)
            | BoolValue b -> StringValue (if b then "true" else "false")
            | StringValue s -> StringValue s
            | _ -> failwith "to_string: unsupported type")

        "string_to_int", BuiltinValue (fun v ->
            match v with
            | StringValue s ->
                match System.Int32.TryParse(s) with
                | true, n -> IntValue n
                | false, _ -> failwith (sprintf "string_to_int: cannot parse '%s' as int" s)
            | _ -> failwith "string_to_int: expected string")
    ]
```

### Pattern 3: Type Scheme Registration in `initialTypeEnv`

**What:** Six new entries in `TypeCheck.initialTypeEnv` mapping function names to their polymorphic type schemes.

**When to use:** For type checking to accept string function calls.

**Example:**
```fsharp
// TypeCheck.fs — add to initialTypeEnv Map.ofList:

// string_length : string -> int
"string_length", Scheme([], TArrow(TString, TInt))

// string_concat : string -> string -> string
"string_concat", Scheme([], TArrow(TString, TArrow(TString, TString)))

// string_sub : string -> int -> int -> string
"string_sub", Scheme([], TArrow(TString, TArrow(TInt, TArrow(TInt, TString))))

// string_contains : string -> string -> bool
"string_contains", Scheme([], TArrow(TString, TArrow(TString, TBool)))

// to_string : 'a -> string  (simplified: accept int or bool, both monomorphic)
// IMPORTANT: Cannot be truly polymorphic (HM can't express ad-hoc polymorphism).
// Use overloaded approach: register as string -> string, int -> string, bool -> string
// OR: register as a monomorphic scheme with a type variable that unifies to int or bool.
// Best: Scheme([0], TArrow(TVar 0, TString)) — polymorphic, works for int/bool/string.
"to_string", Scheme([0], TArrow(TVar 0, TString))

// string_to_int : string -> int
"string_to_int", Scheme([], TArrow(TString, TInt))
```

### Pattern 4: `formatValue BuiltinValue` — Display Representation

**What:** `formatValue` in `Eval.fs` needs a case for `BuiltinValue` to avoid incomplete match warnings.

**Example:**
```fsharp
// Eval.fs formatValue:
| BuiltinValue _ -> "<builtin>"
```

### Pattern 5: Merging `initialBuiltinEnv` in Program.fs and Repl.fs

**What:** The `initialEnv` used for evaluation must include built-ins. Currently `Program.fs` calls `Prelude.loadPrelude()` which returns `emptyEnv`. Merge `initialBuiltinEnv` into the result.

**Example:**
```fsharp
// Program.fs:
let preludeEnv = Prelude.loadPrelude()
let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) preludeEnv Eval.initialBuiltinEnv

// Repl.fs:
let preludeEnv = Prelude.loadPrelude()
let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) preludeEnv Eval.initialBuiltinEnv
```

### Pattern 6: `string_sub` Indexing Convention

**What:** `string_sub "hello" 1 3` must return `"ell"`. Success criteria specifies: start index 1, length 3. This is **start + length** semantics, NOT start + end index.

**F# slice syntax:** `s.[start .. start + len - 1]` implements this correctly.

**CRITICAL:** Do NOT use `s.[start .. end_exclusive - 1]` (end-exclusive) or `s.[start .. end_inclusive]` (start/end) — use start + length. Verify: `"hello".[1..3]` = `"ell"` (indices 1, 2, 3 inclusive = length 3). YES, F# `.[1..3]` on `"hello"` gives `"ell"`. This is start=1, end_inclusive=3, length=(3-1+1)=3. So `s.[start .. start + len - 1]` is correct.

### Pattern 7: `to_string` Polymorphism Trade-off

**What:** `to_string` is ad-hoc polymorphic (works on int, bool). HM type inference cannot express true ad-hoc polymorphism. The type scheme `Scheme([0], TArrow(TVar 0, TString))` works for type checking (the type variable unifies with whatever is passed), but allows incorrect usage like `to_string [1, 2, 3]` to type-check.

**Recommendation:** Use `Scheme([0], TArrow(TVar 0, TString))`. At runtime, the `BuiltinValue` implementation rejects non-int/non-bool values with a clear error. The type system is permissive but the runtime enforces correctness. This is consistent with how untyped operations work in other dynamically-checked systems.

### Anti-Patterns to Avoid

- **Writing string builtins as Expr trees:** Implementing `string_length` as `Lambda("s", App(Var("String.length"), Var("s")))` is wrong — the interpreter has no `String.length` variable. Write native F# lambdas via `BuiltinValue`.
- **Adding a `BuiltinExpr` AST node:** Unnecessary complexity. `BuiltinValue` in the Value DU is sufficient. AST is for user-written code, not native operations.
- **Registering builtins only in `initialTypeEnv` without eval registration:** Type checking passes but runtime fails with "Undefined variable: string_length". Both registrations are required.
- **Using `FunctionValue` with synthetic AST bodies for builtins:** Would require encoding F# operations as LangThree expressions. `BuiltinValue` is the right abstraction.
- **Making `string_sub` use 0-based exclusive end:** Success criterion says `string_sub "hello" 1 3` returns `"ell"` — this is start=1, length=3. NOT start=1, end=3 (which would give `"el"`).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| String length | Manual loop/recursion | `String.length s` or `s.Length` | Built into .NET |
| Substring | Manual char slicing | `s.[start .. start + len - 1]` | F# slice syntax, handles edge cases |
| String contains | Manual search | `s.Contains(needle)` | .NET `System.String.Contains` |
| Int to string | Manual digit extraction | `string n` or `sprintf "%d" n` | Built into F# |
| String to int | Manual digit parsing | `System.Int32.TryParse` | Handles negatives, leading zeros, whitespace edge cases |
| Bool to string | Conditional expression | `if b then "true" else "false"` | Matches expected semantics exactly |

**Key insight:** All string operations map directly to .NET/F# built-ins. The work is purely registration/wiring, not implementation logic.

## Common Pitfalls

### Pitfall 1: `BuiltinValue` Not Handled in All Match Expressions

**What goes wrong:** After adding `BuiltinValue` to the `Value` DU, F# pattern matching is no longer exhaustive in `formatValue`, `matchPattern`, `Eval.Equal`, `Eval.NotEqual`, and any other place that matches on `Value`. The compiler warns (`FS0025`) but does NOT error by default.

**Why it happens:** F# DUs require exhaustive match; adding a new case invalidates existing matches.

**How to avoid:** Search all match-on-Value sites after adding `BuiltinValue`:
- `Eval.fs: formatValue` → add `| BuiltinValue _ -> "<builtin>"`
- `Eval.fs: Equal` → add `| _ -> failwith "Type error: = not supported for builtin values"`
- `Eval.fs: NotEqual` → same
- Any other pattern match on `Value` in the codebase (check `Format.fs`, `Repl.fs`)

**Warning signs:** Compiler warning `FS0025: Incomplete pattern matches on this expression`.

### Pitfall 2: Curried Builtin Evaluation — `BuiltinValue` in `App`

**What goes wrong:** Multi-argument builtins like `string_sub s 1 3` evaluate as nested `App` calls. If `App` only handles `FunctionValue` but not `BuiltinValue`, the second/third argument applications fail with "Type error: attempted to call non-function".

**Why it happens:** `string_sub "hello"` returns `BuiltinValue (fun v2 -> BuiltinValue (...))`. The outer `App` dispatches correctly, but the inner `App` also receives a `BuiltinValue` result and must dispatch again.

**How to avoid:** Add the `BuiltinValue` case to `App` BEFORE the `| _ -> failwith` case:
```fsharp
| BuiltinValue fn ->
    let argValue = eval recEnv moduleEnv env argExpr
    fn argValue
```
This handles all levels of curried application automatically.

### Pitfall 3: `to_string` Type Variable Creates Overly Permissive Type

**What goes wrong:** `Scheme([0], TArrow(TVar 0, TString))` allows `to_string [1, 2, 3]` to type-check (type variable unifies with `int list`). At runtime, the F# implementation raises an exception.

**Why it happens:** HM type inference cannot express "int or bool only" without ad-hoc polymorphism or type classes.

**How to avoid:** This is accepted behavior. The success criteria only tests `to_string 42` and `to_string true`. The runtime error for unsupported types is acceptable. Document in code comments that `to_string` is intentionally permissive in type system.

### Pitfall 4: `string_sub` Out-of-Bounds Access

**What goes wrong:** `string_sub "hello" 0 10` raises an F# `System.ArgumentOutOfRangeException` at runtime (not a clean language error).

**Why it happens:** F# `s.[start .. start + len - 1]` raises an exception when indices exceed string length.

**How to avoid:** Add bounds checking in the `BuiltinValue` implementation:
```fsharp
"string_sub", BuiltinValue (fun v1 ->
    BuiltinValue (fun v2 ->
        BuiltinValue (fun v3 ->
            match v1, v2, v3 with
            | StringValue s, IntValue start, IntValue len ->
                if start < 0 || len < 0 || start + len > String.length s then
                    failwithf "string_sub: index out of range (start=%d, len=%d, string_length=%d)"
                              start len (String.length s)
                else
                    StringValue (s.[start .. start + len - 1])
            | _ -> failwith "string_sub: expected string int int")))
```

### Pitfall 5: `string_to_int` Edge Cases

**What goes wrong:** `string_to_int ""` or `string_to_int "abc"` raises `System.FormatException` if using `int s` directly.

**Why it happens:** Direct F# `int "abc"` throws; `System.Int32.TryParse` returns false.

**How to avoid:** Always use `System.Int32.TryParse` pattern with clean error message:
```fsharp
| StringValue s ->
    match System.Int32.TryParse(s) with
    | true, n -> IntValue n
    | false, _ -> failwithf "string_to_int: cannot parse '%s' as integer" s
```

### Pitfall 6: `--emit-type` Only Shows User-Defined Bindings, Not Builtins

**What goes wrong:** `Program.fs` `--emit-type --file` filters to show only names NOT in `initialTypeEnv`. String functions ARE in `initialTypeEnv`, so they won't appear in `--emit-type` output. This is correct behavior (same as `map`, `filter`, etc.). However, a test like `let x = string_length "hello"` in a file should show `x : int` — this works because `x` is user-defined.

**Why it happens:** Line 115 in `Program.fs`: `|> Map.filter (fun k _ -> not (Map.containsKey k TypeCheck.initialTypeEnv))`.

**How to avoid:** No fix needed. The filtering is intentional and correct. Just ensure flt tests for `--emit-type` use user-defined binding names, not the built-in names directly.

### Pitfall 7: `string_concat` Redundancy with `+` Operator

**What goes wrong:** `string_concat` duplicates functionality of `+`. The type inference for `+` is implemented in `Bidir.fs`/`Infer.fs` which handles `Add` node (not `App`). `string_concat` as a function is distinct from `+` as an operator.

**Why it happens:** STR-02 requires both. They are complementary (operator vs. first-class function form).

**How to avoid:** Implement both. `string_concat` as `BuiltinValue` and `+` as existing `Add` node handling. No conflict.

## Code Examples

### Example 1: Adding `BuiltinValue` to `Ast.Value`

```fsharp
// Ast.fs — in Value DU (add after RecordValue):
| BuiltinValue of fn: (Value -> Value)
```

Note: `Value -> Value` must use the recursive `Value` type, which is fine since `Value` is already a recursive DU via `FunctionValue of param: string * body: Expr * closure: Env`.

### Example 2: Dispatching `BuiltinValue` in `Eval.App`

```fsharp
// Eval.fs — App case (add before | _ -> failwith):
| App (funcExpr, argExpr, _) ->
    let funcVal = eval recEnv moduleEnv env funcExpr
    match funcVal with
    | FunctionValue (param, body, closureEnv) ->
        // ... existing code unchanged ...
    | BuiltinValue fn ->
        let argValue = eval recEnv moduleEnv env argExpr
        fn argValue
    | _ -> failwith "Type error: attempted to call non-function"
```

### Example 3: `string_sub` with Bounds Check

```fsharp
"string_sub", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue s ->
        BuiltinValue (fun v2 ->
            match v2 with
            | IntValue start ->
                BuiltinValue (fun v3 ->
                    match v3 with
                    | IntValue len ->
                        if start < 0 || len < 0 || start + len > s.Length then
                            failwithf "string_sub: out of range (start=%d, len=%d, length=%d)"
                                      start len s.Length
                        StringValue (s.[start .. start + len - 1])
                    | _ -> failwith "string_sub: third argument must be int")
            | _ -> failwith "string_sub: second argument must be int")
    | _ -> failwith "string_sub: first argument must be string")
```

### Example 4: Type Schemes for String Built-ins

```fsharp
// TypeCheck.fs initialTypeEnv additions:
"string_length",   Scheme([], TArrow(TString, TInt))
"string_concat",   Scheme([], TArrow(TString, TArrow(TString, TString)))
"string_sub",      Scheme([], TArrow(TString, TArrow(TInt, TArrow(TInt, TString))))
"string_contains", Scheme([], TArrow(TString, TArrow(TString, TBool)))
"to_string",       Scheme([0], TArrow(TVar 0, TString))
"string_to_int",   Scheme([], TArrow(TString, TInt))
```

### Example 5: Merging Built-ins in `Program.fs`

```fsharp
// Program.fs — replace:
let initialEnv = Prelude.loadPrelude()
// with:
let preludeEnv = Prelude.loadPrelude()
let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) preludeEnv Eval.initialBuiltinEnv
```

### Example 6: Expected `.flt` Test for `string_length`

```
// Test: string_length returns length of string
// --- Command: /path/to/LangThree --expr "string_length \"hello\""
// --- Output:
5
```

### Example 7: Expected `.flt` Test for `string_sub`

```
// Test: string_sub extracts substring
// --- Command: /path/to/LangThree --expr "string_sub \"hello\" 1 3"
// --- Output:
"ell"
```

### Example 8: Expected `.flt` Test for `to_string`

```
// Test: to_string converts int to string
// --- Command: /path/to/LangThree --expr "to_string 42"
// --- Output:
"42"
```

### Example 9: `--emit-type` Test for String Functions

```
// Test: --emit-type shows string function applied correctly
// --- Command: /path/to/LangThree --emit-type --file %input
// --- Input:
let result = string_length "hello"
// --- Output:
result : int
```

## State of the Art

| Old Approach | Current Approach (Phase 11) | Impact |
|---|---|---|
| No string functions | Six built-in string functions | Practical string processing |
| All builtins type-only (no eval) | `BuiltinValue` carrier for F# lambdas | Runtime callable built-ins |
| `initialEnv = emptyEnv` | `initialEnv` includes `initialBuiltinEnv` | All builtins available by default |
| `formatValue` handles 8 Value cases | Handles 9 cases (`BuiltinValue` added) | No match warnings |

**Current codebase state (verified):**

- `StringValue` is in `Ast.Value` (line ~174 in Ast.fs)
- `TString` is in `Type.Type`
- `String(s, _)` AST node evaluates to `StringValue s` in `Eval.fs` line 139
- `Add` in `Eval.fs` handles `StringValue + StringValue` (line 198-199)
- `initialTypeEnv` has 11 entries currently (map, filter, fold, length, reverse, append, id, const, compose, hd, tl) — all type-only
- `Prelude.loadPrelude()` returns `emptyEnv` (no `Prelude.fun` file exists)
- `Value` DU has 8 cases: `IntValue`, `BoolValue`, `FunctionValue`, `StringValue`, `TupleValue`, `ListValue`, `DataValue`, `RecordValue`
- `BuiltinValue` does NOT exist yet

## Open Questions

1. **`to_string` for additional types**
   - What we know: Success criteria only tests `to_string 42` and `to_string true`
   - What's unclear: Should `to_string "hello"` (string passthrough) work? Should `to_string (1, 2)` fail cleanly?
   - Recommendation: Support `IntValue`, `BoolValue`, and `StringValue` in the runtime implementation. All others raise runtime error. Type scheme remains polymorphic `'a -> string`.

2. **`string_concat` vs `+` operator — type of `string_concat`**
   - What we know: `string_concat` is `string -> string -> string`. The `+` operator is handled via `Add` AST node with special-cased type in `Bidir.fs`.
   - What's unclear: Does `string_concat` need to be pipe-friendly (it already is as a curried function)?
   - Recommendation: No special handling needed. `"hello" |> string_concat "world"` works naturally with the curried `BuiltinValue` implementation.

3. **`string_to_int` failure mode**
   - What we know: Success criteria only tests `string_to_int "42"` returning 42
   - What's unclear: Should `string_to_int "abc"` raise a language-level exception (catchable with `try-with`) or an F# exception (uncatchable)?
   - Recommendation: Raise an F# exception (`failwithf`) — consistent with how other runtime errors (division by zero, match failure) work. The language doesn't have a `Result` type built-in. An uncaught exception prints an error message and exits with code 1.

## Sources

### Primary (HIGH confidence)
- Direct source code inspection: `Ast.fs`, `Type.fs`, `Eval.fs`, `TypeCheck.fs`, `Program.fs`, `Repl.fs`, `Prelude.fs`
- All findings verified by reading actual implementation files and running the interpreter
- Confirmed `BuiltinValue` does not exist; confirmed `Prelude.loadPrelude()` returns `emptyEnv`
- Verified `string_sub "hello" 1 3` semantics: F# `"hello".[1..3]` = `"ell"` (start=1, length=3 → end_inclusive=3)

### Secondary (MEDIUM confidence)
- F# / .NET documentation for `String.length`, `System.Int32.TryParse`, `String.Contains`
- Standard functional language interpreter patterns for native/builtin function carriers

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all tools confirmed by direct source inspection
- Architecture: HIGH — `BuiltinValue` pattern is verified against codebase; `App` case structure is known
- Pitfalls: HIGH — specific file/line references for known issues; F# semantics verified

**Research date:** 2026-03-10
**Valid until:** 2026-04-10 (stable domain, no external dependencies)
