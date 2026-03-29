# Phase 56: HashSet & Queue - Research

**Researched:** 2026-03-29
**Domain:** F# interpreter — HashSet and Queue native collection types, constructor interception, FieldAccess dispatch
**Confidence:** HIGH

## Summary

Phase 56 adds two mutable collection types to the interpreter: `HashSet` (unique elements, O(1) membership) and `Queue` (FIFO ordering). Both follow the exact same architectural pattern as Phase 55's `StringBuilderValue`: a new DU case in `Ast.fs`, constructor interception in `Eval.fs` and `Bidir.fs`, FieldAccess dispatch in both files, and a Prelude `.fun` module wrapper.

The .NET backing types are `System.Collections.Generic.HashSet<Value>` for HashSet and `System.Collections.Generic.Queue<Value>` for Queue. Both are generic collections that accept `Value` directly — since `Value` implements `IEquatable<Value>` and `IComparable` (via `CustomEquality`/`CustomComparison` already in place), the .NET `HashSet` equality semantics will work correctly against the language's value equality.

The design decision is that `HashSet()` and `Queue()` parse as `Constructor("HashSet", Some(Tuple([],_)), _)` and `Constructor("Queue", Some(Tuple([],_)), _)` due to the parser's uppercase-first constructor rule. Constructor interception in `Eval.fs` and `Bidir.fs` must handle both `Some argExpr` and `None` forms. All methods follow the `.Add(v)` / `.Contains(v)` / `.Count` / `.Enqueue(v)` / `.Dequeue()` naming from the spec.

**Primary recommendation:** Add `HashSetValue` and `QueueValue` DU cases to `Ast.fs`, then implement in order: (1) constructor interception in `Eval.fs`; (2) FieldAccess dispatch in `Eval.fs`; (3) constructor interception in `Bidir.fs`; (4) FieldAccess dispatch in `Bidir.fs`; (5) builtin registrations in `TypeCheck.fs` (if needed); (6) Prelude module files; (7) flt tests.

## Standard Stack

No new external libraries. All changes are within the interpreter source files.

### Core Files to Modify

| File | What Changes | Pattern from Phase 55 |
|------|--------------|-----------------------|
| `Ast.fs` | Add `HashSetValue` and `QueueValue` DU cases; update `GetHashCode`, `valueEqual`, `valueCompare`; update `formatValue` | `StringBuilderValue` case |
| `Eval.fs` | Constructor interception for "HashSet" and "Queue"; FieldAccess dispatch in `| StringBuilderValue sb ->` block style | Same pattern |
| `Bidir.fs` | Constructor interception returning `TData("HashSet",[])` / `TData("Queue",[])`; FieldAccess types for each | Same pattern |
| `TypeCheck.fs` | Low-level builtin registrations (only if adding raw builtins like `hashset_create`) | Same pattern as `stringbuilder_create` etc. |
| `Prelude/HashSet.fun` | Module wrapper: `create`, `add`, `contains`, `count` | Same pattern as `Prelude/StringBuilder.fun` |
| `Prelude/Queue.fun` | Module wrapper: `create`, `enqueue`, `dequeue`, `count` | Same pattern |

### .NET Backing Types

| LangThree Type | .NET Type | Why |
|----------------|-----------|-----|
| `HashSetValue` | `System.Collections.Generic.HashSet<Value>` | O(1) add/lookup; uses Value's IEquatable<Value> for equality |
| `QueueValue` | `System.Collections.Generic.Queue<Value>` | FIFO via Enqueue/Dequeue; throws InvalidOperationException on empty dequeue |

### Installation

No packages. Pure F# source modifications.

## Architecture Patterns

### Pattern 1: New Value DU Cases in Ast.fs

Follow `StringBuilderValue` exactly. Add after the existing `StringBuilderValue` line:

```fsharp
| HashSetValue of System.Collections.Generic.HashSet<Value>  // Phase 56: Mutable unique-element set
| QueueValue of System.Collections.Generic.Queue<Value>       // Phase 56: Mutable FIFO queue
```

**GetHashCode** (identity-based, not structural — same as HashtableValue and StringBuilderValue):
```fsharp
| HashSetValue hs -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(hs)
| QueueValue q    -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(q)
```

**valueEqual** (reference identity, same as ArrayValue):
```fsharp
| HashSetValue h1, HashSetValue h2 -> System.Object.ReferenceEquals(h1, h2)
| QueueValue q1,   QueueValue q2   -> System.Object.ReferenceEquals(q1, q2)
```

**valueCompare** (zero for incomparables, same as HashtableValue):
```fsharp
| HashSetValue _, _ | _, HashSetValue _ -> 0
| QueueValue _,   _ | _, QueueValue _   -> 0
```

**formatValue** (for REPL display):
```fsharp
| HashSetValue hs ->
    let elements = hs |> Seq.map formatValue |> String.concat "; "
    sprintf "HashSet{%s}" elements
| QueueValue q ->
    let elements = q |> Seq.map formatValue |> String.concat "; "
    sprintf "Queue[%s]" elements
```

### Pattern 2: Constructor Interception in Eval.fs

The `Constructor` arm at ~line 1055 already intercepts "StringBuilder". Extend it:

```fsharp
| Constructor (name, argOpt, _) ->
    match name, argOpt with
    | "StringBuilder", Some argExpr -> ...  // existing
    | "StringBuilder", None ->              // existing
        StringBuilderValue (System.Text.StringBuilder())
    // Phase 56: HashSet constructor interception
    | "HashSet", Some argExpr ->
        match eval recEnv moduleEnv env false argExpr with
        | TupleValue [] -> HashSetValue (System.Collections.Generic.HashSet<Value>())
        | _ -> failwith "HashSet: expected ()"
    | "HashSet", None ->
        HashSetValue (System.Collections.Generic.HashSet<Value>())
    // Phase 56: Queue constructor interception
    | "Queue", Some argExpr ->
        match eval recEnv moduleEnv env false argExpr with
        | TupleValue [] -> QueueValue (System.Collections.Generic.Queue<Value>())
        | _ -> failwith "Queue: expected ()"
    | "Queue", None ->
        QueueValue (System.Collections.Generic.Queue<Value>())
    | _ ->
        let argValue = argOpt |> Option.map (eval recEnv moduleEnv env false)
        DataValue (name, argValue)
```

### Pattern 3: FieldAccess Dispatch in Eval.fs

The FieldAccess arm at ~line 1214 dispatches on the evaluated value type. Add new match arms after `| StringBuilderValue sb ->`:

```fsharp
// Phase 56: HashSet method dispatch
| HashSetValue hs ->
    match fieldName with
    | "Add" ->
        BuiltinValue (fun arg ->
            let isNew = hs.Add(arg)
            BoolValue isNew)
    | "Contains" ->
        BuiltinValue (fun arg ->
            BoolValue (hs.Contains(arg)))
    | "Count" -> IntValue hs.Count
    | _ -> failwithf "HashSet has no property or method '%s'" fieldName
// Phase 56: Queue method dispatch
| QueueValue q ->
    match fieldName with
    | "Enqueue" ->
        BuiltinValue (fun arg ->
            q.Enqueue(arg)
            TupleValue [])
    | "Dequeue" ->
        BuiltinValue (fun arg ->
            match arg with
            | TupleValue [] ->
                if q.Count = 0 then
                    raise (LangThreeException (StringValue "Queue.Dequeue: queue is empty"))
                else
                    q.Dequeue()
            | _ -> failwith "Queue.Dequeue: takes no arguments (call as .Dequeue())")
    | "Count" -> IntValue q.Count
    | _ -> failwithf "Queue has no property or method '%s'" fieldName
```

**Critical note for zero-arg methods:** `.Dequeue()` parses as `App(FieldAccess(q,"Dequeue",_), Tuple([],_), _)`. The FieldAccess evaluation of "Dequeue" returns a `BuiltinValue` that accepts `TupleValue []`, then returns the dequeued value. This matches the `.Trim()` pattern from Phase 55.

**Critical note for `.Count`:** `.Count` is a property access without `()`, so it's a plain `FieldAccess` — NOT an `App`. Return `IntValue hs.Count` directly (no `BuiltinValue` wrapper).

**Critical note for empty dequeue:** Use `raise (LangThreeException (StringValue "..."))` to produce a catchable exception consistent with how `hashtable_get` raises on missing keys.

### Pattern 4: Constructor Interception in Bidir.fs

The `Constructor` arm in `Bidir.fs` at ~line 64 intercepts "StringBuilder". Extend it:

```fsharp
| Constructor (name, argOpt, span) ->
    match name with
    | "StringBuilder" -> ...  // existing
    | "HashSet" ->
        match argOpt with
        | Some argExpr ->
            let s, argTy = synth ctorEnv recEnv ctx env argExpr
            let s2 = unifyWithContext ctx [] span (apply s argTy) (TTuple [])
            (compose s2 s, TData("HashSet", []))
        | None ->
            (empty, TData("HashSet", []))
    | "Queue" ->
        match argOpt with
        | Some argExpr ->
            let s, argTy = synth ctorEnv recEnv ctx env argExpr
            let s2 = unifyWithContext ctx [] span (apply s argTy) (TTuple [])
            (compose s2 s, TData("Queue", []))
        | None ->
            (empty, TData("Queue", []))
    | _ ->
    // existing fallthrough...
```

### Pattern 5: FieldAccess Type Rules in Bidir.fs

The `FieldAccess` arm in `Bidir.fs` at ~line 525 resolves types for field/method access. Add after the `TData("StringBuilder", [])` case:

```fsharp
// Phase 56: HashSet field access types
| TData("HashSet", []) ->
    match fieldName with
    | "Add" ->
        let tv = freshVar()
        (s1, TArrow(tv, TBool))
    | "Contains" ->
        let tv = freshVar()
        (s1, TArrow(tv, TBool))
    | "Count" -> (s1, TInt)
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
// Phase 56: Queue field access types
| TData("Queue", []) ->
    match fieldName with
    | "Enqueue" ->
        let tv = freshVar()
        (s1, TArrow(tv, TTuple []))
    | "Dequeue" ->
        let tv = freshVar()
        (s1, TArrow(TTuple [], tv))
    | "Count" -> (s1, TInt)
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
```

**Note on type polymorphism:** HashSet and Queue are not parameterized in the type repr (`TData("HashSet", [])` not `TData("HashSet", [TVar 0])`). This matches `TData("StringBuilder", [])`. `freshVar()` is used for method arg/return types to allow usage with any element type — same approach as `Append` in StringBuilder.

### Pattern 6: Prelude Module Files

`Prelude/HashSet.fun`:
```
module HashSet =
    let create ()        = HashSet ()
    let add hs v         = hs.Add v
    let contains hs v    = hs.Contains v
    let count hs         = hs.Count
```

`Prelude/Queue.fun`:
```
module Queue =
    let create ()        = Queue ()
    let enqueue q v      = q.Enqueue v
    let dequeue q ()     = q.Dequeue ()
    let count q          = q.Count
```

**Important:** The Prelude wrappers call the constructors and methods directly using the dot-dispatch syntax. No raw builtin functions (like `hashset_create`, `queue_enqueue`) are needed — unlike Hashtable, which predated FieldAccess dispatch and uses raw builtins. Phase 56 uses the newer method dispatch pattern directly.

### Pattern 7: Test File Structure

Test files for flt use `// --- Output:` (not `// --- Stdout:`) per the prior decisions context. Place tests under `tests/flt/file/` in a new `collections/` subdirectory or reuse existing naming. The test binary path is `/Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree`.

Example structure for `hashset-basic.flt`:
```
// Test: HashSet basic usage (COLL-02)
// --- Command: /Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let hs = HashSet ()
let r1 = hs.Add(1)
let r2 = hs.Add(2)
let r3 = hs.Add(1)
let _ = println (to_string r1)
let _ = println (to_string r2)
let _ = println (to_string r3)
let _ = println (to_string (hs.Contains(1)))
let _ = println (to_string (hs.Contains(9)))
let _ = println (to_string hs.Count)
// --- Output:
true
true
false
true
false
2
()
```

Example structure for `queue-basic.flt`:
```
// Test: Queue basic usage (COLL-03)
// --- Command: /Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let q = Queue ()
let _ = q.Enqueue(10)
let _ = q.Enqueue(20)
let _ = q.Enqueue(30)
let _ = println (to_string q.Count)
let v1 = q.Dequeue ()
let _ = println (to_string v1)
let v2 = q.Dequeue ()
let _ = println (to_string v2)
let _ = println (to_string q.Count)
// --- Output:
3
10
20
1
()
```

### Anti-Patterns to Avoid

- **Do NOT add raw builtins like `hashset_create` / `queue_enqueue`** to `TypeCheck.fs` `initialTypeEnv` or `Eval.fs` `initialBuiltinEnv`. Phase 56 uses constructor interception + FieldAccess dispatch exclusively. The Hashtable pattern (raw builtins + module wrapper) was the old approach from before Phase 54's dispatch infrastructure existed.
- **Do NOT make HashSet/Queue parameterized types** (`TData("HashSet", [TVar 0])`). The StringBuilder pattern is simpler and sufficient — FieldAccess methods use fresh type variables locally.
- **Do NOT return `IntValue hs.Count` from a `BuiltinValue`** — `.Count` is a property with no `()`, so it must be returned directly from the `FieldAccess` arm, not wrapped in a `BuiltinValue`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Unique element tracking | Custom list-based dedup | `System.Collections.Generic.HashSet<Value>` | O(1) add/contains; Value has IEquatable already |
| FIFO queue | Custom list append/reverse | `System.Collections.Generic.Queue<Value>` | O(1) enqueue/dequeue |
| Empty dequeue error | Custom error code | `raise (LangThreeException ...)` | Consistent with hashtable_get missing key behavior |

**Key insight:** .NET's generic collections work directly with `Value` because `Value` implements `IEquatable<Value>` and `IComparable` through the existing `CustomEquality`/`CustomComparison` attributes and `valueEqual`/`valueCompare` statics. No adapter needed.

## Common Pitfalls

### Pitfall 1: .Count as Property vs .Dequeue() as Zero-Arg Method

**What goes wrong:** `.Count` is a property (no `()`), while `.Dequeue()` is a zero-arg method (has `()`). These behave differently at the AST level.

**Why it happens:** `hs.Count` parses as `FieldAccess(hs, "Count", _)` — just field access. But `q.Dequeue()` parses as `App(FieldAccess(q, "Dequeue", _), Tuple([],_), _)` — field access then application.

**How to avoid:**
- `.Count` in `Eval.fs` FieldAccess arm returns `IntValue hs.Count` directly.
- `.Dequeue` in `Eval.fs` FieldAccess arm returns `BuiltinValue(fun arg -> match arg with TupleValue [] -> q.Dequeue() | ...)`.
- In `Bidir.fs`: `.Count` has type `TInt`; `.Dequeue` has type `TArrow(TTuple [], tv)`.

**Warning signs:** If Count always returns a function value in the REPL, the BuiltinValue wrapper is wrong. If Dequeue fails with "field access on non-record", the FieldAccess arm is returning a value instead of a BuiltinValue.

### Pitfall 2: Ordering of Constructor Match Arms

**What goes wrong:** In `Eval.fs` Constructor arm, if "HashSet" and "Queue" cases are placed AFTER the `| _ ->` DataValue fallthrough, they will never fire.

**Why it happens:** F# match is first-match-wins. The fallthrough `| _ -> DataValue(name, argValue)` must remain last.

**How to avoid:** Place "HashSet" and "Queue" cases immediately after "StringBuilder" and before the `| _ ->` fallthrough, following the exact same structure.

**Warning signs:** `HashSet()` evaluates to `DataValue("HashSet", Some(TupleValue[]))` instead of `HashSetValue(...)`.

### Pitfall 3: HashSet Boolean Return from .Add()

**What goes wrong:** `.Add(v)` on .NET `HashSet<T>` returns `bool` — `true` if element was newly added, `false` if it already existed. If this return value is not captured, the behavior is correct but tests that check the bool will fail.

**Why it happens:** .NET `HashSet<T>.Add` has signature `bool Add(T item)` — the return must be wrapped in `BoolValue`.

**How to avoid:** `let isNew = hs.Add(arg) in BoolValue isNew` — always capture and return the bool.

### Pitfall 4: Empty Queue Dequeue Error Handling

**What goes wrong:** .NET `Queue<T>.Dequeue()` throws `System.InvalidOperationException` on empty queue. If not caught, this becomes an untyped .NET exception, not a LangThree catchable exception.

**Why it happens:** Raw .NET exceptions bypass the LangThree exception system.

**How to avoid:** Explicitly check `q.Count = 0` before dequeuing and raise `LangThreeException(StringValue "Queue.Dequeue: queue is empty")` for the empty case. The spec requires "empty dequeue error" to be tested, so this path must exist.

### Pitfall 5: Test File Output Marker

**What goes wrong:** Using `// --- Stdout:` instead of `// --- Output:` in flt test files.

**Why it happens:** Phase 54 tests use `// --- Stdout:` but Phase 55 StringBuilder tests use `// --- Output:`. The prior decisions state "FsLit uses // --- Output: not // --- Stdout:".

**How to avoid:** Always use `// --- Output:` in new test files.

## Code Examples

### HashSet Constructor Interception (Eval.fs)

```fsharp
// Source: Phase 55 StringBuilder pattern, Eval.fs ~line 1057
| "HashSet", Some argExpr ->
    match eval recEnv moduleEnv env false argExpr with
    | TupleValue [] -> HashSetValue (System.Collections.Generic.HashSet<Value>())
    | _ -> failwith "HashSet: expected ()"
| "HashSet", None ->
    HashSetValue (System.Collections.Generic.HashSet<Value>())
```

### HashSet FieldAccess Dispatch (Eval.fs)

```fsharp
// Source: Phase 55 StringBuilderValue dispatch, Eval.fs ~line 1215
| HashSetValue hs ->
    match fieldName with
    | "Add" ->
        BuiltinValue (fun arg ->
            BoolValue (hs.Add(arg)))
    | "Contains" ->
        BuiltinValue (fun arg ->
            BoolValue (hs.Contains(arg)))
    | "Count" -> IntValue hs.Count
    | _ -> failwithf "HashSet has no property or method '%s'" fieldName
```

### Queue Dequeue with Empty Check (Eval.fs)

```fsharp
// Source: Hashtable get missing key pattern, Eval.fs ~line 595
| QueueValue q ->
    match fieldName with
    | "Enqueue" ->
        BuiltinValue (fun arg ->
            q.Enqueue(arg)
            TupleValue [])
    | "Dequeue" ->
        BuiltinValue (fun arg ->
            match arg with
            | TupleValue [] ->
                if q.Count = 0 then
                    raise (LangThreeException (StringValue "Queue.Dequeue: queue is empty"))
                else
                    q.Dequeue()
            | _ -> failwith "Queue.Dequeue: takes no arguments")
    | "Count" -> IntValue q.Count
    | _ -> failwithf "Queue has no property or method '%s'" fieldName
```

### HashSet Type Rules (Bidir.fs)

```fsharp
// Source: Phase 55 TData("StringBuilder",[]) case, Bidir.fs ~line 546
| TData("HashSet", []) ->
    match fieldName with
    | "Add" ->
        let tv = freshVar()
        (s1, TArrow(tv, TBool))
    | "Contains" ->
        let tv = freshVar()
        (s1, TArrow(tv, TBool))
    | "Count" -> (s1, TInt)
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
```

### Prelude/HashSet.fun

```
// Source: Prelude/StringBuilder.fun pattern
module HashSet =
    let create ()     = HashSet ()
    let add hs v      = hs.Add v
    let contains hs v = hs.Contains v
    let count hs      = hs.Count
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Raw builtins (`hashset_create`, etc.) + module wrapper | Constructor interception + FieldAccess dispatch | Phase 54–55 | Phase 56 should use new approach, not old Hashtable-style raw builtins |
| `// --- Stdout:` in tests | `// --- Output:` in tests | Phase 55 | New tests must use `// --- Output:` |

## Open Questions

1. **Test directory location**
   - What we know: `tests/flt/file/` contains subdirectories by type (hashtable, string, property, etc.)
   - What's unclear: Should HashSet and Queue tests go in a new `collections/` directory or separate `hashset/` and `queue/` directories?
   - Recommendation: Create `tests/flt/file/hashset/` and `tests/flt/file/queue/` separate directories, mirroring `hashtable/`.

2. **String element support in HashSet**
   - What we know: HashSet uses `Value.GetHashCode()` which handles StringValue and IntValue correctly.
   - What's unclear: Whether the spec's "integers, strings" means tests must cover both in the same test file or separate files.
   - Recommendation: Cover both in separate test files for clarity.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection:
  - `Ast.fs` — Value DU definition, GetHashCode, valueEqual, valueCompare statics
  - `Eval.fs` — Constructor interception at ~line 1055; FieldAccess dispatch at ~line 1214; StringBuilderValue patterns
  - `Bidir.fs` — Constructor arm at line 64–74; FieldAccess type rules at line 545–554
  - `TypeCheck.fs` — initialTypeEnv at line 15–170 (Hashtable and StringBuilder entries)
  - `Prelude/StringBuilder.fun` — Module wrapper pattern
  - `tests/flt/file/string/stringbuilder-basic.flt` — Test file format reference
  - `tests/flt/file/string/stringbuilder-chaining.flt` — Method chaining test pattern

### Secondary (HIGH confidence)
- Phase 55 RESEARCH.md — Documented architectural decisions that Phase 56 directly follows
- .NET documentation (knowledge cutoff): `HashSet<T>.Add` returns bool; `Queue<T>.Dequeue` throws on empty

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — directly examined all affected files
- Architecture: HIGH — exact pattern from Phase 55 confirmed in source
- Pitfalls: HIGH — Count vs method, empty dequeue, bool return all verified from code

**Research date:** 2026-03-29
**Valid until:** Stable (changes only if Phase 54–55 patterns change)
