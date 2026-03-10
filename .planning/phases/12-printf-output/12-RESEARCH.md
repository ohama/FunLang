# Phase 12: Printf Output - Research

**Researched:** 2026-03-10
**Domain:** F# interpreter built-in I/O functions — BuiltinValue pattern, side-effectful functions, printf format string parsing
**Confidence:** HIGH

## Summary

Phase 12 adds `print`, `println`, and `printf` as built-in functions to LangThree. The codebase already has the exact pattern needed: Phase 11 introduced `BuiltinValue of fn:(Value->Value)` in the Value DU and `initialBuiltinEnv : Env` in Eval.fs. All three print functions slot into these existing extension points with zero new infrastructure.

`print` and `println` are trivial: they take one string argument, write to stdout, and return `TupleValue []` (unit). `printf` is the key challenge: it is variadic in concept but the language only supports curried function application. The approach is to implement `printf` as a curried function that accumulates argument values one at a time, consuming format specifiers (`%d`, `%s`, `%b`) left-to-right. When all specifiers are consumed, the formatted string is flushed to stdout.

The type system challenge is that `printf "x=%d, s=%s" 42 "hi"` has type `unit`, but the intermediate applications `printf "x=%d, s=%s"` and `printf "x=%d, s=%s" 42` have types `int -> unit` and `string -> unit` respectively. This is exactly the curried BuiltinValue nesting pattern already used by `string_sub` (3 levels deep), applied dynamically based on the format string.

**Primary recommendation:** Implement `print` and `println` as straightforward single-arg `BuiltinValue`s returning `TupleValue []`. Implement `printf` as a BuiltinValue that parses the format string on first application and returns nested `BuiltinValue`s for each remaining specifier — flushing when all specifiers are satisfied.

## Standard Stack

### Core

| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `BuiltinValue of fn:(Value->Value)` | Phase 11 established | Carrier for native F# functions in the Value DU | Already in Ast.fs; all builtins use this |
| `initialBuiltinEnv : Env` in Eval.fs | Phase 11 established | Map of name -> BuiltinValue at startup | Pattern established in Phase 11 |
| `initialTypeEnv : TypeEnv` in TypeCheck.fs | Phase 11 established | Type schemes for built-in names | Parallel to initialBuiltinEnv |
| `TupleValue []` / `TTuple []` | Phase 10 established | Unit value/type representation | No TUnit/UnitValue needed |
| `printf` via F# `printf`/`printfn`/`stdout.Write` | .NET 10 | Actual I/O | Native .NET, no libraries |

### Supporting

| Component | Purpose | When to Use |
|-----------|---------|-------------|
| `System.String.Format` or manual substitution | Printf format parsing | For `%d`, `%s`, `%b` replacement in `printf` impl |
| `stdout.Write` / `stdout.WriteLine` | Immediate output | `print`/`println` — avoid buffering issues |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Curried BuiltinValue nesting for printf | New `PrintfValue` DU case | Curried nesting reuses existing infrastructure; new DU case needs changes in formatValue, valuesEqual, eval |
| Runtime format parsing in printf | Compile-time printf like F# typed printf | Runtime parsing is simpler; no new AST/type machinery required |
| `string_format` helper function | Inline format in printf | Separate helper is cleaner but adds scope; inline is sufficient |

**Installation:** No new packages — pure F# / .NET 10.

## Architecture Patterns

### Recommended Project Structure

No new files needed. Changes go in:

```
src/LangThree/
├── Eval.fs          # Add print/println/printf to initialBuiltinEnv
├── TypeCheck.fs     # Add type schemes to initialTypeEnv
└── tests/flt/
    ├── expr/        # print/println --expr tests
    └── file/        # printf multi-arg file tests
```

### Pattern 1: Single-Arg BuiltinValue (print, println)

**What:** A BuiltinValue that takes one Value, performs I/O, returns TupleValue [].
**When to use:** For `print` and `println` — no currying needed, one argument.

```fsharp
// In Eval.fs initialBuiltinEnv
"print", BuiltinValue (fun v ->
    match v with
    | StringValue s ->
        stdout.Write(s)
        TupleValue []
    | _ -> failwith "print: expected string argument")

"println", BuiltinValue (fun v ->
    match v with
    | StringValue s ->
        stdout.WriteLine(s)
        TupleValue []
    | _ -> failwith "println: expected string argument")
```

### Pattern 2: Curried BuiltinValue for Printf (dynamic arity from format string)

**What:** `printf` takes a format string first, parses it to count specifiers, then returns nested BuiltinValues for each specifier, flushing when done.
**When to use:** `printf` — arity depends on the runtime format string.

```fsharp
// In Eval.fs initialBuiltinEnv
"printf", BuiltinValue (fun fmtVal ->
    match fmtVal with
    | StringValue fmt ->
        // Parse format string: extract specifiers (%d, %s, %b) in order
        let specifiers = parsePrintfSpecifiers fmt
        if List.isEmpty specifiers then
            // No specifiers: flush format string directly, return unit
            stdout.Write(fmt)
            TupleValue []
        else
            // Build curried chain: one BuiltinValue per specifier
            applyPrintfArgs fmt specifiers []
    | _ -> failwith "printf: first argument must be a format string")
```

The helper `applyPrintfArgs` accumulates collected values and returns a BuiltinValue for the next argument, or flushes when all specifiers are consumed:

```fsharp
let rec applyPrintfArgs (fmt: string) (remaining: string list) (collected: Value list) : Value =
    match remaining with
    | [] ->
        // All specifiers consumed — substitute and flush
        let result = substitutePrintfArgs fmt (List.rev collected)
        stdout.Write(result)
        TupleValue []
    | _ :: rest ->
        BuiltinValue (fun argVal ->
            applyPrintfArgs fmt rest (argVal :: collected))
```

The `substitutePrintfArgs` function replaces `%d`/`%s`/`%b` left-to-right with formatted values:

```fsharp
let substitutePrintfArgs (fmt: string) (args: Value list) : string =
    // Walk fmt, replacing %d/%s/%b with corresponding arg formatted values
    // %d -> IntValue n -> string n
    // %s -> StringValue s -> s  (no quotes, unlike formatValue)
    // %b -> BoolValue b -> "true"/"false"
```

### Pattern 3: Type Schemes for Print Functions

**What:** Add monomorphic type schemes for `print`/`println` and a polymorphic-looking scheme for `printf`.
**When to use:** Must add to `initialTypeEnv` in TypeCheck.fs for type checking.

```fsharp
// In TypeCheck.fs initialTypeEnv
// print: string -> unit
"print", Scheme([], TArrow(TString, TTuple []))

// println: string -> unit
"println", Scheme([], TArrow(TString, TTuple []))

// printf: string -> ...  (variadic at runtime, typed as string -> 'a for type checker)
// The type checker cannot express true variadic types; use permissive polymorphic scheme
// like to_string: the first arg is string, further args/result unconstrained
"printf", Scheme([0], TArrow(TString, TVar 0))
```

**Critical insight on printf type:** The type checker cannot statically know how many arguments `printf "x=%d, s=%s"` takes because the format string is a runtime value. Using `Scheme([0], TArrow(TString, TVar 0))` is permissive: it says "printf takes a string and returns anything." This matches the pattern of `to_string` being permissively polymorphic. The runtime enforces correct arity. This is intentional and documented — not a bug.

### Anti-Patterns to Avoid

- **Returning StringValue from print/println:** They must return `TupleValue []` (unit), not a string. Success criterion 4 requires unit return type.
- **Using `formatValue` for printf %s substitution:** `formatValue` adds quotes around strings (`"hello"`). Printf `%s` should output the raw string `hello` (no quotes). Use a separate value-to-string helper for printf.
- **Buffered I/O without flush:** Use `stdout.Write`/`stdout.WriteLine` and call `stdout.Flush()` after writing in `print`/`printf` to ensure mid-execution output appears immediately (success criterion 5).
- **Trying to make printf statically typed:** The format string is a runtime value; accept the permissive polymorphic scheme. Don't attempt to add a new TypeExpr or AST node for printf.
- **Reusing `formatValue` for %d:** `formatValue` on `IntValue 42` returns `"42"` which is fine, but be consistent: write a dedicated `printfFormatArg` helper that handles the three specifier types cleanly.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Printf specifier parsing | Full regex-based parser | Simple linear scan with `%` detection | Only 3 specifiers needed; regex adds dependency |
| Printf output buffering | Custom StreamWriter | `stdout.Write` + `stdout.Flush()` | .NET Console output is line-buffered; explicit flush handles mid-execution output |
| New Value DU case for printf | `PrintfValue of ...` | Nested `BuiltinValue` chain | BuiltinValue already works for `string_sub` (3-level nesting); adding a DU case requires changes in 5+ pattern match sites |

**Key insight:** The `string_sub` implementation in Eval.fs (3 levels of BuiltinValue nesting, determined statically) is the exact same structural pattern as `printf` — just with dynamic nesting depth determined by format string parsing at runtime.

## Common Pitfalls

### Pitfall 1: Printf %s outputs quoted strings

**What goes wrong:** Using `formatValue` for `%s` substitution produces `"hello"` instead of `hello`.
**Why it happens:** `formatValue` is designed for REPL display (adds quotes, shows structure). Printf `%s` should output the raw string content.
**How to avoid:** Write a separate `printfFormatValue (spec: string) (v: Value) : string` helper that handles `%s` -> raw string, `%d` -> int string, `%b` -> bool string.
**Warning signs:** Test `printf "%s" "hi"` produces `"hi"` (with quotes) instead of `hi`.

### Pitfall 2: Printf arity mismatch not caught

**What goes wrong:** `printf "%d %s" 42` (missing one arg) returns a BuiltinValue, not unit. If this value is then used as the final expression, `formatValue` prints `<builtin>` instead of crashing.
**Why it happens:** Dynamic arity means partial application is indistinguishable from a bug.
**How to avoid:** This is acceptable behavior — partial application of printf is a valid function value. Document it, don't try to detect it. The test cases should all provide correct arity.
**Warning signs:** Tests producing `<builtin>` as output instead of expected formatted string.

### Pitfall 3: Mid-execution output buffering

**What goes wrong:** In file mode, `print "hello"` output doesn't appear before the final value is printed.
**Why it happens:** .NET Console is line-buffered; `stdout.Write` without flush may not flush immediately.
**How to avoid:** Call `stdout.Flush()` after each `print`/`printf` call (or use `Console.Write` which forces flush on interactive streams). Alternatively, set `Console.OutputEncoding` and use `Console.Write` directly.
**Warning signs:** File-mode test output appears in wrong order (final value before mid-execution prints).

### Pitfall 4: Printf format string with no specifiers

**What goes wrong:** `printf "hello"` (no `%` specifiers) behaves incorrectly if the no-specifier path is not handled.
**Why it happens:** The specifier list is empty; the curried chain would have zero levels and immediately return `TupleValue []`.
**How to avoid:** Explicitly handle the empty-specifier case in the BuiltinValue: write the format string directly and return `TupleValue []`.
**Warning signs:** `printf "hello"` does nothing or crashes.

### Pitfall 5: initialTypeEnv printf scheme causes type errors in valid programs

**What goes wrong:** `printf "%d" 42` fails type checking because the inferred type of the printf application conflicts with the scheme.
**Why it happens:** Using an overly restrictive type scheme for printf.
**How to avoid:** Use `Scheme([0], TArrow(TString, TVar 0))` — says "printf takes string, returns anything." This unifies with any call pattern. The `TVar 0` in the return position absorbs whatever the callers infer.
**Warning signs:** Type errors like "expected unit but got int" on valid printf calls.

## Code Examples

Verified patterns from codebase inspection:

### Existing BuiltinValue multi-arg pattern (string_sub, 3-level nesting)

```fsharp
// From Eval.fs initialBuiltinEnv — the exact pattern for printf dynamic nesting
"string_sub", BuiltinValue (fun v1 ->
    match v1 with
    | StringValue s ->
        BuiltinValue (fun v2 ->
            match v2 with
            | IntValue start ->
                BuiltinValue (fun v3 ->
                    match v3 with
                    | IntValue len -> StringValue (s.[start .. start + len - 1])
                    | _ -> failwith "string_sub: third argument must be int")
            | _ -> failwith "string_sub: second argument must be int")
    | _ -> failwith "string_sub: first argument must be string")
```

### How App handles BuiltinValue in eval

```fsharp
// From Eval.fs eval, App case — BuiltinValue is called directly, no closure env needed
| App (funcExpr, argExpr, _) ->
    let funcVal = eval recEnv moduleEnv env funcExpr
    match funcVal with
    | FunctionValue (param, body, closureEnv) -> ...
    | BuiltinValue fn ->
        let argValue = eval recEnv moduleEnv env argExpr
        fn argValue   // <-- BuiltinValue is just called with the evaluated arg
    | _ -> failwith "Type error: attempted to call non-function"
```

### Startup merge pattern (already established)

```fsharp
// From Program.fs and Repl.fs — no changes needed; print/println/printf
// added to initialBuiltinEnv will automatically be merged at startup
let initialEnv = Map.fold (fun acc k v -> Map.add k v acc) preludeEnv Eval.initialBuiltinEnv
```

### Unit type scheme pattern

```fsharp
// From TypeCheck.fs initialTypeEnv — existing pattern for unit-returning builtins
// (no current unit-returning builtins, but TTuple [] is established as unit)
"print",   Scheme([], TArrow(TString, TTuple []))
"println", Scheme([], TArrow(TString, TTuple []))
```

### Printf specifier parsing (to implement)

```fsharp
// Simple linear scan — no regex needed
let parsePrintfSpecifiers (fmt: string) : string list =
    let mutable i = 0
    let specs = System.Collections.Generic.List<string>()
    while i < fmt.Length do
        if fmt.[i] = '%' && i + 1 < fmt.Length then
            let spec = fmt.[i + 1]
            match spec with
            | 'd' | 's' | 'b' ->
                specs.Add(string spec)
                i <- i + 2
            | '%' ->
                i <- i + 2  // escaped %%, not a specifier
            | _ ->
                i <- i + 1
        else
            i <- i + 1
    specs |> Seq.toList
```

### File-mode output: print mid-execution

```fsharp
// In Program.fs file mode, after evalModuleDecls, the last let binding value is printed.
// print/println calls happen DURING evalModuleDecls (as side effects).
// stdout.Flush() in print/println ensures they appear before the final value.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No print functions | BuiltinValue-based print/println/printf | Phase 12 | File-mode programs can produce output mid-execution |
| Only final value printed | print/println for mid-execution output | Phase 12 | Programs can be practical I/O scripts |

**Deprecated/outdated:** None — this is new functionality.

## Open Questions

1. **Printf type scheme and type inference interaction**
   - What we know: `Scheme([0], TArrow(TString, TVar 0))` works for `to_string` (permissive poly)
   - What's unclear: Whether `printf "%d" 42` type-checks correctly — TVar 0 must unify with `int -> unit`, then `unit` for the full application.
   - Recommendation: Test `printf "%d" 42` type inference early in Plan 01. If inference fails, use `Scheme([0; 1], TArrow(TString, TArrow(TVar 0, TVar 1)))` for 2-specifier case, or accept that printf must be used with `let _ =` for sequencing (which is fine per UNIT-03).

2. **Printf with 0 specifiers vs print**
   - What we know: `printf "hello"` should output "hello" and return unit (same as `print "hello"`)
   - What's unclear: Whether users expect `printf "hello"` to work identically to `print "hello"` — it should
   - Recommendation: Handle zero-specifier case explicitly: write the format string, flush, return `TupleValue []`

3. **printf %% escape**
   - What we know: Real printf supports `%%` to output a literal `%`
   - What's unclear: Whether this is in scope (not explicitly mentioned in PRINT-03 which only mentions `%d`, `%s`, `%b`)
   - Recommendation: Implement `%%` -> `%` in the substitution step as a freebie — trivial to add, avoids surprises

4. **File-mode: last binding is printed after all side-effectful bindings run**
   - What we know: Program.fs prints the last `LetDecl` body value after `evalModuleDecls` completes
   - What's unclear: Whether `let _ = print "hello"` followed by `let result = 42` causes "hello" to appear before `42` or after
   - Recommendation: Confirm that `evalModuleDecls` processes `LetDecl`s in order (it uses `List.fold` — yes, in order). `print` side effect happens during fold, before the final value print. Flush ensures ordering.

## Sources

### Primary (HIGH confidence)

- Eval.fs (codebase) — BuiltinValue pattern, App dispatch, initialBuiltinEnv, string_sub 3-level nesting
- TypeCheck.fs (codebase) — initialTypeEnv, to_string permissive polymorphic scheme pattern
- Ast.fs (codebase) — Value DU with BuiltinValue, TupleValue [] as unit
- Program.fs (codebase) — startup merge pattern, file-mode execution order
- STATE.md decisions — Phase 11 decisions, TTuple []/TupleValue [] as unit, BuiltinValue infrastructure

### Secondary (MEDIUM confidence)

- tests/flt/expr/str-concat.flt — confirms flt test format for builtin function tests
- tests/flt/expr/unit-literal.flt — confirms `()` output format for unit
- tests/flt/file/let-sequence.flt — confirms file-mode output behavior (last binding printed)

### Tertiary (LOW confidence)

- None needed — all patterns fully resolved from codebase inspection.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — BuiltinValue infrastructure fully established in Phase 11; no new libraries
- Architecture: HIGH — print/println are trivially simple; printf curried-nesting pattern mirrors string_sub exactly
- Pitfalls: HIGH — derived from close reading of Eval.fs App dispatch, formatValue, and Program.fs output pipeline

**Research date:** 2026-03-10
**Valid until:** Stable — changes only if BuiltinValue infrastructure changes (unlikely before Phase 12)
