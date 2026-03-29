# Phase 54: Property & Method Dispatch - Research

**Researched:** 2026-03-29
**Domain:** F# interpreter — runtime dispatch for value-type properties and methods
**Confidence:** HIGH

## Summary

Phase 54 extends the existing `FieldAccess` AST node to handle property access and method dispatch on primitive value types (strings and arrays). Currently `FieldAccess(expr, fieldName, span)` handles only record field access and module-qualified access; any other value type raises `FieldAccessOnNonRecord` in `Bidir.fs` and `"Field access on non-record value"` in `Eval.fs`.

The implementation requires touching exactly two files: `Eval.fs` (runtime dispatch) and `Bidir.fs` (static type checking). No AST changes, no parser changes, and no new Value DU cases are needed. The parser already handles `obj.Method(args)` correctly — it parses as `App(FieldAccess(obj, "Method", span), args, span)`, so method dispatch falls out naturally once `FieldAccess` returns a `BuiltinValue` function.

Phase 54 establishes the dispatch infrastructure that Phases 55–57 will extend. The scope for this phase is minimal: `.Length` on strings and arrays (PROP-01), and the general dispatch mechanism that allows `BuiltinValue` functions to be returned from `FieldAccess` (PROP-04). Specific methods like `.Append`, `.Contains`, `.TryGetValue` belong to later phases.

**Primary recommendation:** Add value-type dispatch to the `FieldAccess` arm in `Eval.fs` before the record fallback; mirror with `TString`/`TArray` cases in `Bidir.fs`'s `FieldAccess` arm.

## Standard Stack

No new libraries required. This is a pure interpreter extension.

### Core (Already Present)
| Component | Location | Purpose | Role in Phase 54 |
|-----------|----------|---------|-----------------|
| `FieldAccess` AST | `Ast.fs:97` | Field/property access node | Reuse as-is |
| `eval` function | `Eval.fs:730` | Main evaluator dispatch | Extend `FieldAccess` arm |
| `synth` function | `Bidir.fs:42` | Type synthesizer | Extend `FieldAccess` arm |
| `BuiltinValue` | `Ast.fs:204` | Native function carrier | Return from `FieldAccess` for methods |
| `callValue` | `Eval.fs:161` | Applies a Value to an arg | Already handles `BuiltinValue` |

## Architecture Patterns

### How Method Calls Already Parse

`"hello".Length` parses as:
```
FieldAccess(String("hello", span), "Length", span)
```

`arr.Length` parses as:
```
FieldAccess(Var("arr", span), "Length", span)
```

`obj.Method(arg)` parses as:
```
App(FieldAccess(Var("obj", span), "Method", span), Var("arg", span), span)
```

This is confirmed in `Parser.fsy:357`:
```
| Atom DOT IDENT   { FieldAccess($1, $3, ruleSpan parseState 1 3) }
```

And `Parser.fsy:300`:
```
| AppExpr Atom { App($1, $2, ruleSpan parseState 1 2) }
```

Method dispatch therefore requires no new AST nodes or parser rules. When `FieldAccess` evaluates to a `BuiltinValue`, `App` will apply it automatically via `applyFunc`.

### Pattern: Value-Type Dispatch in Eval.fs

The `FieldAccess` arm in `Eval.fs` (lines 1032–1096) currently follows this decision tree:
1. Is the expression a module reference? → module qualified access
2. Is the expression itself a `FieldAccess`? → chained access (handles `A.B.c`)
3. Fallback: evaluate inner expression, expect `RecordValue`

The Phase 54 extension adds value-type dispatch **inside** the final `| _ ->` branch, before the `RecordValue` match. The correct location is after evaluating the inner expression:

```fsharp
// Phase 3 (Records) + Phase 5 (Modules) + Phase 54 (Properties): Field access / qualified access
| FieldAccess (expr, fieldName, _) ->
    // ... existing module and chained-access handling ...
    | _ ->
        // Evaluate the object
        match eval recEnv moduleEnv env false expr with
        // Phase 54: Value-type property/method dispatch
        | StringValue s ->
            match fieldName with
            | "Length" -> IntValue s.Length
            | _ -> failwithf "String has no property or method '%s'" fieldName
        | ArrayValue arr ->
            match fieldName with
            | "Length" -> IntValue arr.Length
            | _ -> failwithf "Array has no property or method '%s'" fieldName
        // Existing record fallback
        | RecordValue (_, fields) ->
            match Map.tryFind fieldName fields with
            | Some valueRef -> !valueRef
            | None -> failwithf "Field not found: %s" fieldName
        | v -> failwithf "Field access on non-record value: %s" (formatValue v)
```

For methods that take arguments, `FieldAccess` returns a `BuiltinValue`:

```fsharp
| StringValue s ->
    match fieldName with
    | "Length" -> IntValue s.Length
    | "Contains" ->
        BuiltinValue (fun arg ->
            match arg with
            | StringValue needle -> BoolValue (s.Contains(needle))
            | _ -> failwith "String.Contains: expected string argument")
    | _ -> failwithf "String has no property or method '%s'" fieldName
```

### Pattern: Type Checking in Bidir.fs

The `FieldAccess` arm in `Bidir.fs` (lines 514–531) currently only handles `TData(typeName, typeArgs)` (record types), raising `FieldAccessOnNonRecord` for everything else.

Extend with `TString` and `TArray` cases before the fallthrough:

```fsharp
| FieldAccess (accessExpr, fieldName, span) ->
    let s1, exprTy = synth ctorEnv recEnv ctx env accessExpr
    let resolvedTy = apply s1 exprTy
    match resolvedTy with
    // Phase 54: String property/method types
    | TString ->
        match fieldName with
        | "Length" -> (s1, TInt)
        | _ ->
            raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
    // Phase 54: Array property/method types
    | TArray elemTy ->
        match fieldName with
        | "Length" -> (s1, TInt)
        | _ ->
            raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
    // Existing record handling
    | TData (typeName, typeArgs) ->
        // ... unchanged ...
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; ... })
```

For methods returning functions (future phases), return the arrow type:
```fsharp
| "Contains" -> (s1, TArrow(TString, TBool))
```

### Recommended Project Structure (unchanged)

No structural changes needed. All changes are in existing files:
```
src/LangThree/
├── Eval.fs      -- Extend FieldAccess arm with value-type dispatch
├── Bidir.fs     -- Extend FieldAccess arm with TString/TArray type rules
└── tests/flt/
    └── file/property/   -- New test directory for property/method tests
```

### Anti-Patterns to Avoid

- **New AST node for property access:** Do not add a `PropertyAccess` node. The existing `FieldAccess` node is identical at the AST level — the distinction is purely in what the evaluator does with the result value type.
- **Modifying the parser:** No parser changes are needed. `obj.Method(args)` already parses correctly as `App(FieldAccess(...))`.
- **Changing the record fallback order:** Value-type dispatch must come BEFORE the record fallback, not inside it. The evaluator evaluates `expr` once, then dispatches on the result value type.
- **Assuming type checker is optional:** `Bidir.fs` runs before `Eval.fs` in the file pipeline (`Program.fs:200`). If `Bidir.fs` raises `FieldAccessOnNonRecord` for `"hello".Length`, execution never reaches `Eval.fs`. Both must be updated.
- **Type variable for `.Length` return type:** `.Length` always returns `int` — use `TInt` directly, not a fresh `TVar`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| String length | Custom loop | `s.Length` (.NET property) | Already available on F# string |
| Array length | Custom loop | `arr.Length` (.NET property) | Already available on F# array |
| Method arity checking | Runtime arity counter | Nested `BuiltinValue` chain | Same currying pattern used by all existing builtins |

**Key insight:** The existing `BuiltinValue` currying pattern (used by `string_concat`, `array_fold`, etc.) is the correct way to represent curried methods. Multi-argument methods like `Contains(needle)` become `BuiltinValue (fun needle -> ...)`.

## Common Pitfalls

### Pitfall 1: Type Checker Blocks Execution
**What goes wrong:** `"hello".Length` fails with a type error about `FieldAccessOnNonRecord string`, even though `Eval.fs` handles it.
**Why it happens:** `Program.fs:200` calls `TypeCheck.typeCheckModuleWithPrelude` before `Eval.evalModuleDecls`. The type checker runs first and rejects the expression.
**How to avoid:** Add `TString` and `TArray` cases to the `FieldAccess` arm in `Bidir.fs` at the same time as `Eval.fs` changes.
**Warning signs:** `flt` test fails with a diagnostic error message like `FieldAccessOnNonRecord` rather than a runtime `failwith`.

### Pitfall 2: Wrong Insertion Point in FieldAccess
**What goes wrong:** Value-type dispatch is added to the chained-access sub-branch (`| FieldAccess (innerExpr, innerField, _) ->`) instead of the top-level fallback.
**Why it happens:** The `FieldAccess` arm has nested match expressions. The chained branch handles `A.B.c`; the top-level `| _ ->` branch handles simple `obj.field`.
**How to avoid:** Add value-type dispatch in the `| _ ->` branch at line 1089, which handles the common case. The chained case also falls through to a record match at line 1082–1088 — that branch also needs updating for completeness (in case `inner.prop.Length` is written).
**Warning signs:** `"hello".Length` works but `getStr().Length` fails.

### Pitfall 3: Module Name Collision
**What goes wrong:** A user defines a module named `String` or `Array`. The `tryGetModuleName` check in `FieldAccess` may intercept `"hello".Length` if `Length` is in a `String` module.
**Why it happens:** `tryGetModuleName` checks the expression against `moduleEnv`. If the expression is `Var("s", _)` and `s` is a string variable, it won't be in `moduleEnv`, so this is safe. The collision only occurs if the object expression is a Constructor/Var that matches a module name.
**How to avoid:** No special handling needed — `tryGetModuleName` checks module membership explicitly, and string variables are not module names.
**Warning signs:** None expected; this is a false alarm.

### Pitfall 4: flt Test Command Path
**What goes wrong:** flt tests use the old binary path and fail to find the executable.
**Why it happens:** flt tests hardcode the binary path in the `// --- Command:` line.
**How to avoid:** Use the correct binary path `../fslit/dist/FsLit tests/flt/` per the CLAUDE.md build instructions. New test files should copy the path format from existing tests.
**Warning signs:** flt runner reports "command not found" rather than a test failure.

### Pitfall 5: Type Variable Inference for Method Return Types
**What goes wrong:** `arr.Length` type-checks as `'a` instead of `int`.
**Why it happens:** If a fresh `TVar` is returned from `Bidir.fs` instead of `TInt`, unification proceeds but the result type is under-constrained.
**How to avoid:** Return `(s1, TInt)` for `Length` directly — no unification needed.
**Warning signs:** `--emit-type` shows `arr.Length : 'a` instead of `arr.Length : int`.

## Code Examples

### Eval.fs Extension (FieldAccess arm, final `| _ ->` branch)
```fsharp
// In eval function, FieldAccess arm, after module/chained-access handling:
| _ ->
    match eval recEnv moduleEnv env false expr with
    // Phase 54: String properties and methods
    | StringValue s ->
        match fieldName with
        | "Length" -> IntValue s.Length
        | _ -> failwithf "String has no property '%s'" fieldName
    // Phase 54: Array properties and methods
    | ArrayValue arr ->
        match fieldName with
        | "Length" -> IntValue arr.Length
        | _ -> failwithf "Array has no property '%s'" fieldName
    // Existing record fallback
    | RecordValue (_, fields) ->
        match Map.tryFind fieldName fields with
        | Some valueRef -> !valueRef
        | None -> failwithf "Field not found: %s" fieldName
    | v -> failwithf "Field access on non-record value: %s" (formatValue v)
```

### Bidir.fs Extension (FieldAccess arm)
```fsharp
| FieldAccess (accessExpr, fieldName, span) ->
    let s1, exprTy = synth ctorEnv recEnv ctx env accessExpr
    let resolvedTy = apply s1 exprTy
    match resolvedTy with
    // Phase 54: String property types
    | TString ->
        match fieldName with
        | "Length" -> (s1, TInt)
        | _ ->
            raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy
                                   Span = span; Term = Some expr
                                   ContextStack = ctx; Trace = [] })
    // Phase 54: Array property types
    | TArray _ ->
        match fieldName with
        | "Length" -> (s1, TInt)
        | _ ->
            raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy
                                   Span = span; Term = Some expr
                                   ContextStack = ctx; Trace = [] })
    // Existing: record field access
    | TData (typeName, typeArgs) ->
        // ... unchanged existing code ...
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy
                               Span = span; Term = Some expr
                               ContextStack = ctx; Trace = [] })
```

### flt Test Format
```
// Test: .Length property on strings
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let s = "hello"
let n = s.Length
let _ = println (to_string n)
let m = "hello".Length
let _ = println (to_string m)
// --- Stdout:
5
5
```

```
// Test: .Length property on arrays
// --- Command: /Users/ohama/vibe-coding/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let arr = Array.create 3 0
let n = arr.Length
let _ = println (to_string n)
// --- Stdout:
3
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `string_length s` | `s.Length` | Phase 54 | More idiomatic syntax |
| `array_length arr` | `arr.Length` | Phase 54 | More idiomatic syntax |
| N/A | `obj.Method(args)` | Phase 54 | Enables collection methods in Phases 55–57 |

**Deprecated/outdated:**
- `string_length` and `array_length` builtins: NOT removed in Phase 54. They remain available as aliases. Removal (if ever) is a separate decision.

## Open Questions

1. **Should `.Length` also work on ListValue?**
   - What we know: `ListValue` is immutable and has `List.length` already in the Prelude
   - What's unclear: Whether the feature requests require `list.Length` or only string/array
   - Recommendation: Scope Phase 54 to strings and arrays only (per PROP-01 wording). ListValue dispatch can be added trivially if needed.

2. **What other methods does PROP-04 need to dispatch?**
   - What we know: PROP-04 says "dispatch mechanism for collection methods" — Phase 55 adds `.Append`, `.ToString`, `.EndsWith`, `.Trim`, `.StartsWith`
   - What's unclear: Whether Phase 54 should add a placeholder dispatch or just add `.Length` + the infrastructure
   - Recommendation: Phase 54 adds only `.Length` (the concrete requirement from PROP-01) and proves the dispatch mechanism works. Later phases add their methods by extending the same `match fieldName with` blocks.

3. **Type variable for unknown property access in Bidir.fs**
   - What we know: Currently `FieldAccessOnNonRecord` is raised for unrecognized properties on string/array. This gives a good error message.
   - What's unclear: Whether future phases need to handle dynamic method lookup (e.g., user-defined types with methods)
   - Recommendation: Keep the strict match — raise `FieldAccessOnNonRecord` for unknown field names on known types. This provides good error messages.

## Sources

### Primary (HIGH confidence)
- Direct codebase analysis: `Eval.fs` lines 1032–1096 (FieldAccess arm)
- Direct codebase analysis: `Bidir.fs` lines 514–531 (FieldAccess type checking)
- Direct codebase analysis: `Parser.fsy` lines 357, 300 (parse rules)
- Direct codebase analysis: `Program.fs` lines 200–221 (pipeline: typecheck then eval)
- Direct codebase analysis: `Ast.fs` lines 97, 194–208 (AST nodes and Value DU)

### Secondary (MEDIUM confidence)
- ROADMAP.md and REQUIREMENTS.md: Phase 54 scope and goals
- Existing phases (38, 39, 47) as reference implementations for adding new value-type operations

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — pure interpreter extension, no new dependencies
- Architecture: HIGH — parse behavior and eval dispatch are directly verified
- Pitfalls: HIGH — type checker ordering is directly observed in Program.fs

**Research date:** 2026-03-29
**Valid until:** Stable (internal codebase, no external dependencies)
