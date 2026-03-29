# Phase 58: Language Constructs - Research

**Researched:** 2026-03-29
**Domain:** F# interpreter — string slicing, list comprehensions, native collection for-in iteration, KeyValuePair .Key/.Value access
**Confidence:** HIGH

## Summary

Phase 58 adds three syntactic constructs that complete the language's ergonomics: string slicing (`s.[1..3]`, `s.[2..]`), list comprehensions (`[for x in coll -> expr]`, `[for i in 0..4 -> expr]`), and `for-in` iteration over native collections (HashSet, Queue, MutableList, Hashtable with `.Key`/`.Value` on the loop variable).

All three constructs require both parser changes (new grammar rules) and new AST nodes in `Ast.fs`. The type checker (`Bidir.fs`) and evaluator (`Eval.fs`) must handle the new nodes. No new `Value` DU cases are needed for slicing or comprehension, but the Hashtable `for-in` loop variable needs to yield a value that supports `.Key` and `.Value` field access — implemented by yielding a `RecordValue("KeyValuePair", ...)` with two ref-cell fields.

The key architectural risk is parser ambiguity: the new `DOTLBRACKET Expr DOTDOT Expr RBRACKET` rule for string slicing must not conflict with the existing `DOTLBRACKET Expr RBRACKET` rule. Since LALR parsers use longest-match, the new rules can co-exist by placing more-specific rules first. Similarly, `LBRACKET FOR ...` for comprehensions is unambiguous because no existing rule starts with `LBRACKET FOR`.

**Primary recommendation:** Add `StringSliceExpr` and `ListCompExpr` AST nodes. Extend `ForInExpr` eval/bidir to handle HashSet, Queue, MutableList, and HashtableValue. Represent hashtable KV pairs as `RecordValue("KeyValuePair", {Key->k; Value->v})` for natural `.Key`/`.Value` dispatch.

## Standard Stack

No new external libraries. All changes are within the interpreter source files.

### Core Files to Modify

| File | What Changes | Notes |
|------|--------------|-------|
| `Ast.fs` | Add `StringSliceExpr` and `ListCompExpr` nodes; update `spanOf` | Two new Expr variants |
| `Parser.fsy` | Add grammar rules for slice syntax and list comprehension | LALR rules, no new tokens needed |
| `Eval.fs` | Eval arms for `StringSliceExpr`, `ListCompExpr`; extend `ForInExpr` | Multi-site edits |
| `Bidir.fs` | Type arms for `StringSliceExpr`, `ListCompExpr`; extend `ForInExpr` | Multi-site edits |

### Tokens Already Available

| Token | Used For |
|-------|----------|
| `DOTLBRACKET` | Already exists: `.[` for array/hash indexing |
| `DOTDOT` | Already exists: `..` for list ranges |
| `LBRACKET` / `RBRACKET` | Already exists: `[` / `]` |
| `FOR` | Already exists: `for` keyword |
| `IN` | Already exists: `in` keyword |
| `ARROW` | Already exists: `->` |

No new tokens are needed for any of the three constructs.

## Architecture Patterns

### Recommended Project Structure

No structural changes. All edits are to existing source files:
```
src/LangThree/
├── Ast.fs          # Add StringSliceExpr, ListCompExpr
├── Parser.fsy      # Add grammar rules
├── Eval.fs         # Eval for new nodes + ForInExpr extension
└── Bidir.fs        # Types for new nodes + ForInExpr extension
```

### Pattern 1: StringSliceExpr — New AST Node

**What:** Represents `s.[start..stop]` (bounded slice) and `s.[start..]` (open-ended slice).

**Why a new node:** The index expression inside `DOTLBRACKET...RBRACKET` cannot currently be a range — `DOTDOT` only appears inside `LBRACKET...RBRACKET`. Reusing `IndexGet` with a `Range` inner node would require `Range` to evaluate to something other than a `ListValue` when used as a string index, which is a semantic conflict. A dedicated AST node is cleaner.

**Add to `Ast.fs` after `IndexGet`/`IndexSet`:**
```fsharp
// Phase 58 (String Slicing): s.[start..stop] inclusive, s.[start..] to end
| StringSliceExpr of str: Expr * start: Expr * stop: Expr option * span: Span
```

**Update `spanOf`:**
```fsharp
| StringSliceExpr(_, _, _, s) -> s
```

### Pattern 2: ListCompExpr — New AST Node

**What:** Represents `[for x in coll -> body]` and the range variant `[for i in start..stop -> body]`.

**Why a new node:** List comprehension is fundamentally different from a `ForInExpr` (which returns unit). A list comprehension collects body results into a list. There is no existing AST node that captures this.

**Add to `Ast.fs` after `StringSliceExpr`:**
```fsharp
// Phase 58 (List Comprehension): [for x in coll -> expr] or [for i in start..stop -> expr]
| ListCompExpr of var: string * collection: Expr * body: Expr * span: Span
```

For `[for i in 0..4 -> expr]`, the range `0..4` is desugared by the parser into a `Range(0, 4, None, span)` expression, which becomes the `collection` argument. This reuses `Range` evaluation (which already produces `ListValue [IntValue 0; 1; 2; 3; 4]`).

**Update `spanOf`:**
```fsharp
| ListCompExpr(_, _, _, s) -> s
```

### Pattern 3: Parser Rules for String Slicing

**Add two new rules in `Atom`** after the existing `Atom DOTLBRACKET Expr RBRACKET` rule:

```fsharp
// Phase 58 (String Slicing): s.[start..stop] (inclusive) and s.[start..] (to end)
| Atom DOTLBRACKET Expr DOTDOT Expr RBRACKET
    { StringSliceExpr($1, $3, Some $5, ruleSpan parseState 1 6) }
| Atom DOTLBRACKET Expr DOTDOT RBRACKET
    { StringSliceExpr($1, $3, None, ruleSpan parseState 1 5) }
```

**LALR conflict analysis:** The existing rule is `Atom DOTLBRACKET Expr RBRACKET`. The new rules start with the same prefix but have `DOTDOT` as the fourth token. The LALR(1) parser will shift `Expr` and then look at the lookahead: if `RBRACKET`, reduce as original `IndexGet`; if `DOTDOT`, shift and continue to the new rules. No conflict.

### Pattern 4: Parser Rules for List Comprehension

**Add two new rules in `Atom`** alongside existing list/range rules:

```fsharp
// Phase 58 (List Comprehension): [for x in coll -> body]
| LBRACKET FOR IDENT IN Expr ARROW Expr RBRACKET
    { ListCompExpr($3, $5, $7, ruleSpan parseState 1 8) }
// Phase 58 (List Comprehension with range): [for i in start..stop -> body]
| LBRACKET FOR IDENT IN Expr DOTDOT Expr ARROW Expr RBRACKET
    { ListCompExpr($3, Range($5, $7, None, ruleSpan parseState 5 7), $9, ruleSpan parseState 1 10) }
```

**Alternative:** Make `Range` a standalone `Expr` and only use the first rule. The collection `0..4` would then parse as `Range(0, 4, None)` via the `Expr` non-terminal. However, `DOTDOT` is not currently in the `Expr` grammar — adding it would require a new precedence level and risks conflicts with the existing `LBRACKET Expr DOTDOT Expr RBRACKET` rule. The explicit two-rule approach is safer and more explicit.

### Pattern 5: Eval for StringSliceExpr

**Add to `Eval.fs` after `IndexSet` arm:**
```fsharp
// Phase 58: String slicing s.[start..stop] and s.[start..]
| StringSliceExpr (strExpr, startExpr, stopOpt, _) ->
    let strVal   = eval recEnv moduleEnv env false strExpr
    let startVal = eval recEnv moduleEnv env false startExpr
    match strVal, startVal with
    | StringValue s, IntValue start ->
        let len = s.Length
        let stop =
            match stopOpt with
            | Some stopExpr ->
                match eval recEnv moduleEnv env false stopExpr with
                | IntValue i -> i
                | _ -> failwith "String slice: end index must be int"
            | None -> len - 1
        if start < 0 || start > len then
            raise (LangThreeException (StringValue (sprintf "String slice: start index %d out of bounds (length %d)" start len)))
        if stop < start - 1 || stop >= len then
            // allow stop = start - 1 for empty slice
            raise (LangThreeException (StringValue (sprintf "String slice: end index %d out of bounds (length %d)" stop len)))
        StringValue (s.[start .. stop])
    | _ -> failwith "String slice: expected string and int index"
```

**Edge cases:**
- `s.[0..0]` — single character, returns 1-char string
- `s.[0..]` — entire string
- `s.[len-1..]` — last character
- Empty string: `"hello".[5..]` — stop defaults to `len-1 = 4`, start = 5 > stop = 4, produces empty string — use `s.[5..4]` which F# returns `""`. Need to allow `stop = start - 1` for empty result.

**Note:** F# string slicing `s.[start..stop]` is inclusive and valid even when start > stop (returns ""). The implementation should use F# slice syntax directly: `s.[start .. stop]` handles edge cases correctly.

Simplified implementation:
```fsharp
| StringSliceExpr (strExpr, startExpr, stopOpt, _) ->
    let strVal   = eval recEnv moduleEnv env false strExpr
    let startVal = eval recEnv moduleEnv env false startExpr
    match strVal, startVal with
    | StringValue s, IntValue start ->
        let stop =
            match stopOpt with
            | Some stopExpr ->
                match eval recEnv moduleEnv env false stopExpr with
                | IntValue i -> i
                | _ -> failwith "String slice: end index must be int"
            | None -> s.Length - 1
        StringValue (s.[start .. stop])
    | _ -> failwith "String slice: expected string and int index"
```

### Pattern 6: Eval for ListCompExpr

**Add to `Eval.fs` after `StringSliceExpr` arm:**
```fsharp
// Phase 58: List comprehension [for x in coll -> body]
| ListCompExpr (var, collExpr, bodyExpr, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let elements =
        match collVal with
        | ListValue xs -> xs
        | ArrayValue arr -> arr |> Array.toList
        | HashSetValue hs -> hs |> Seq.toList
        | QueueValue q -> q |> Seq.toList
        | MutableListValue ml -> ml |> Seq.toList
        | _ -> failwith "List comprehension: collection must be a list, array, or native collection"
    let results =
        elements |> List.map (fun elemVal ->
            let loopEnv = Map.add var elemVal env
            eval recEnv moduleEnv loopEnv false bodyExpr)
    ListValue results
```

**Note:** `[for i in 0..4 -> i*i]` — the collection is `Range(0,4,None,_)` which evaluates to `ListValue [0;1;2;3;4]`, then the comprehension maps over it. This reuses all existing infrastructure.

### Pattern 7: Extend ForInExpr in Eval.fs

**Current state** (Eval.fs ~line 963):
```fsharp
| ForInExpr (var, collExpr, body, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let elements =
        match collVal with
        | ListValue xs -> xs
        | ArrayValue arr -> arr |> Array.toList
        | _ -> failwith "for-in: collection must be a list or array"
    for elemVal in elements do
        let loopEnv = Map.add var elemVal env
        eval recEnv moduleEnv loopEnv false body |> ignore
    TupleValue []
```

**New state** — extend to handle HashSet, Queue, MutableList, and HashtableValue:
```fsharp
| ForInExpr (var, collExpr, body, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let elements =
        match collVal with
        | ListValue xs -> xs
        | ArrayValue arr -> arr |> Array.toList
        | HashSetValue hs -> hs |> Seq.toList
        | QueueValue q -> q |> Seq.toList
        | MutableListValue ml -> ml |> Seq.toList
        | HashtableValue ht ->
            ht |> Seq.map (fun kv ->
                let fields = Map.ofList [("Key", ref kv.Key); ("Value", ref kv.Value)]
                RecordValue("KeyValuePair", fields)) |> Seq.toList
        | _ -> failwith "for-in: collection must be a list, array, or native collection"
    for elemVal in elements do
        let loopEnv = Map.add var elemVal env
        eval recEnv moduleEnv loopEnv false body |> ignore
    TupleValue []
```

**KeyValuePair representation:** `RecordValue("KeyValuePair", {Key -> ref kv.Key; Value -> ref kv.Value})`. The existing `FieldAccess` arm in `Eval.fs` handles `RecordValue` by looking up fields in the `Map<string, Value ref>` — so `kv.Key` and `kv.Value` work automatically with zero additional eval code. The `ref` cells are needed because `RecordValue` fields are `Value ref` (to support mutable fields), though KV fields will never be mutated.

### Pattern 8: Extend ForInExpr in Bidir.fs

**Current state** (Bidir.fs ~line 232) uses try/catch to unify with `TList` then `TArray`. Need to extend to handle `TData("HashSet", [])`, `TData("Queue", [])`, `TData("MutableList", [])`, and `THashtable(k,v)`.

**New state:**
```fsharp
| ForInExpr (var, collExpr, body, span) ->
    let s1, collTy = synth ctorEnv recEnv ctx env collExpr
    let resolvedCollTy = apply s1 collTy
    let s12, elemTy =
        match resolvedCollTy with
        | TList t ->
            (s1, t)
        | TArray t ->
            (s1, t)
        | TData("HashSet", []) ->
            let tv = freshVar()
            (s1, tv)
        | TData("Queue", []) ->
            let tv = freshVar()
            (s1, tv)
        | TData("MutableList", []) ->
            let tv = freshVar()
            (s1, tv)
        | THashtable (keyTy, valTy) ->
            // Loop variable gets type TData("KeyValuePair", []) but .Key and .Value
            // need to be accessible. Use freshVar for now; the type checker will
            // allow FieldAccess on RecordValue at runtime (dynamic dispatch).
            // Return a fresh unconstrained type to avoid false type errors.
            let tv = freshVar()
            (s1, tv)
        | _ ->
            // Try unify with TList as fallback (original behavior for polymorphic cases)
            let elemTv = freshVar()
            let s2 =
                try unifyWithContext ctx [] span (apply s1 collTy) (TList elemTv)
                with _ ->
                    let elemTv2 = freshVar()
                    unifyWithContext ctx [] span (apply s1 collTy) (TArray elemTv2)
            let s12 = compose s2 s1
            let elemTy =
                match apply s12 collTy with
                | TList t -> t
                | TArray t -> t
                | _ -> freshVar()
            (s12, elemTy)
    let env2 = applyEnv s12 env
    let loopEnv = Map.add var (Scheme([], elemTy)) env2
    let s3, _bodyTy = synth ctorEnv recEnv ctx loopEnv body
    (compose s3 s12, TTuple [])
```

**Important:** For `THashtable` case, the loop variable type is `freshVar()` (unconstrained). This allows `kv.Key` and `kv.Value` to be accessed without type errors at the type-checking level, since `FieldAccess` on a fresh type var falls through to `| _ ->` in Bidir which may raise. To handle this properly, there are two options:
1. Return a dedicated `TData("KeyValuePair", [keyTy; valTy])` and add a FieldAccess arm for it in Bidir
2. Return `freshVar()` and rely on the type checker not flagging the `.Key`/`.Value` accesses (since fresh type vars unify with anything)

**Recommended: Option 1** — add `TData("KeyValuePair", [keyTy; valTy])` and a FieldAccess arm:

```fsharp
| THashtable (keyTy, valTy) ->
    // Loop yields KeyValuePair with typed .Key and .Value
    (s1, TData("KeyValuePair", [keyTy; valTy]))
```

Then in FieldAccess arm (Bidir.fs), add after the `THashtable` arm:
```fsharp
// Phase 58: KeyValuePair field access types (for hashtable for-in iteration)
| TData("KeyValuePair", [keyTy; valTy]) ->
    match fieldName with
    | "Key"   -> (s1, keyTy)
    | "Value" -> (s1, valTy)
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
```

### Pattern 9: Bidir for StringSliceExpr

**Add to `Bidir.fs` after `IndexSet` arm:**
```fsharp
// Phase 58: String slice type checking
| StringSliceExpr (strExpr, startExpr, stopOpt, span) ->
    let s1, strTy  = synth ctorEnv recEnv ctx env strExpr
    let s2 = unifyWithContext ctx [] span (apply s1 strTy) TString
    let s12 = compose s2 s1
    let s3, startTy = synth ctorEnv recEnv ctx (applyEnv s12 env) startExpr
    let s4 = unifyWithContext ctx [] span (apply s3 startTy) TInt
    let s123 = compose s4 (compose s3 s12)
    let sFinal =
        match stopOpt with
        | None -> s123
        | Some stopExpr ->
            let s5, stopTy = synth ctorEnv recEnv ctx (applyEnv s123 env) stopExpr
            let s6 = unifyWithContext ctx [] span (apply s5 stopTy) TInt
            compose s6 (compose s5 s123)
    (sFinal, TString)
```

### Pattern 10: Bidir for ListCompExpr

**Add to `Bidir.fs` after `StringSliceExpr` arm:**
```fsharp
// Phase 58: List comprehension type checking
| ListCompExpr (var, collExpr, bodyExpr, span) ->
    let s1, collTy = synth ctorEnv recEnv ctx env collExpr
    let resolvedCollTy = apply s1 collTy
    let s12, elemTy =
        match resolvedCollTy with
        | TList t   -> (s1, t)
        | TArray t  -> (s1, t)
        | TData("HashSet", [])     -> (s1, freshVar())
        | TData("Queue", [])       -> (s1, freshVar())
        | TData("MutableList", []) -> (s1, freshVar())
        | _ ->
            let elemTv = freshVar()
            let s2 = unifyWithContext ctx [] span (apply s1 collTy) (TList elemTv)
            let s12 = compose s2 s1
            (s12, apply s12 elemTv)
    let loopEnv = Map.add var (Scheme([], elemTy)) (applyEnv s12 env)
    let s3, bodyTy = synth ctorEnv recEnv ctx loopEnv bodyExpr
    (compose s3 s12, TList (apply s3 bodyTy))
```

### Anti-Patterns to Avoid

- **Do NOT try to reuse `IndexGet` for string slicing.** `IndexGet` evaluates its index expression to a `Value`, and `Range(1,3,None)` evaluates to `ListValue [1;2;3]`. Detecting "this ListValue was from a range" inside IndexGet is fragile and semantically wrong.
- **Do NOT use `//---Stdout:` in test files.** Always `// --- Output:`.
- **Do NOT make `Range` a standalone Expr.** Adding `DOTDOT` to the `Expr` grammar risks shift-reduce conflicts with existing `LBRACKET Expr DOTDOT Expr RBRACKET` and is unnecessarily invasive.
- **Do NOT return `TupleValue [k; v]` for hashtable for-in elements.** Tuples don't support `.Key`/`.Value` field access. Use `RecordValue("KeyValuePair", ...)`.
- **Do NOT forget `ref` cells in RecordValue fields.** `RecordValue` uses `Map<string, Value ref>` not `Map<string, Value>`. Each field must be wrapped: `Map.ofList [("Key", ref kv.Key); ("Value", ref kv.Value)]`.
- **Do NOT add `TData("KeyValuePair", [])` (unparameterized).** The Bidir rule must use `TData("KeyValuePair", [keyTy; valTy])` (2 type args) so `.Key` and `.Value` return correctly typed values.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| String substring | Manual char-by-char copy | `s.[start .. stop]` (F# string slice) | F# natively handles inclusive range, empty result when start > stop |
| HashSet/Queue iteration | Convert to array first | `Seq.toList` directly | .NET collections implement `IEnumerable<T>` |
| KV pair representation | New `KVPairValue` DU case | `RecordValue("KeyValuePair", ...)` | FieldAccess on RecordValue already works; no new Ast.fs case |
| Range in comprehension | New `RangeExpr` standalone | Desugar `[for i in 0..4 -> ...]` to `ListCompExpr(i, Range(0,4,None), ...)` in parser action | Range already evaluates to ListValue; comprehension maps over it |

**Key insight:** The `RecordValue` approach for KeyValuePair reuses all existing FieldAccess infrastructure. The only cost is the `TData("KeyValuePair", [k;v])` arm in Bidir.fs's FieldAccess — a 5-line addition.

## Common Pitfalls

### Pitfall 1: LALR Conflict with Slice Rule and DOTDOT in Expr

**What goes wrong:** Adding `Atom DOTLBRACKET Expr DOTDOT Expr RBRACKET` causes a shift/reduce conflict if `DOTDOT` can appear inside the inner `Expr` production.

**Why it happens:** The parser sees `Atom DOTLBRACKET Expr` and encounters `DOTDOT`. If `DOTDOT` has a rule inside `Expr`, there's an ambiguity.

**How to avoid:** `DOTDOT` is NOT in `Expr` — it only appears in `Atom` rules (inside `LBRACKET...RBRACKET` and now `DOTLBRACKET...RBRACKET`). The inner `Expr` in `Atom DOTLBRACKET Expr DOTDOT Expr RBRACKET` uses arithmetic `Expr` which cannot contain `DOTDOT`. No conflict.

**Warning signs:** `fsyacc` reports shift-reduce or reduce-reduce conflicts for rules involving `DOTDOT`.

### Pitfall 2: String Slice Boundary for Open-Ended Slices

**What goes wrong:** `s.[5..]` on a 5-character string crashes with "index out of bounds" instead of returning `""`.

**Why it happens:** `stop = s.Length - 1 = 4`, and `s.[5..4]` in F# returns `""` cleanly (F# allows inverted ranges = empty slice).

**How to avoid:** Use `s.[start .. stop]` F# syntax directly — it handles the edge case. No manual bounds checking needed.

**Warning signs:** Tests with `s.[len..]` (slice from end) fail with runtime exceptions.

### Pitfall 3: ForInExpr on HashSet/Queue in Bidir Does Not Fall Through

**What goes wrong:** The current Bidir `ForInExpr` uses try/catch to unify with `TList` first, then `TArray`. Adding `TData("HashSet", [])` inside the try block causes it to fail and fall to the `TArray` try — which also fails, raising a type error.

**Why it happens:** The current code is structured as a try-catch chain, not as explicit pattern matching.

**How to avoid:** Refactor the `ForInExpr` bidir arm to use explicit `match resolvedCollTy with` pattern matching before the try/catch fallback (as shown in Pattern 8 above). Only the fallback for unknown type vars needs the try/catch.

**Warning signs:** Type error "expected list, got HashSet" when running the type checker on `for x in hs do ...`.

### Pitfall 4: List Comprehension Over Range Misses Type of Elements

**What goes wrong:** `[for i in 0..4 -> i * i]` fails type checking because the element type `i` is inferred as `freshVar()` instead of `TInt`.

**Why it happens:** The collection `Range(0,4,None)` evaluates to `ListValue [IntValue 0; ...]`, and the type of Range is `TList TInt`. In Bidir, `Range` evaluates to `TList TInt` — so the comprehension should correctly resolve `i` as `TInt`.

**How to avoid:** In `ListCompExpr` Bidir handling, after computing `collTy` from the collection, extract the element type from `TList TInt` → `TInt`. If the parser desugars `[for i in 0..4 -> ...]` to `ListCompExpr(i, Range(0,4,None), ...)`, then Bidir sees the collection as a `Range` expr which synthesizes to `TList TInt`, and the `| TList t -> (s1, t)` arm correctly returns `t = TInt`.

**Warning signs:** Type error `int * int = 'a` when using range variable `i` in arithmetic.

### Pitfall 5: RecordValue Fields Need ref Wrapping

**What goes wrong:** `RecordValue("KeyValuePair", Map.ofList [("Key", kv.Key); ("Value", kv.Value)])` causes a compile error because `RecordValue` requires `Map<string, Value ref>`.

**Why it happens:** `RecordValue` in `Ast.fs` is defined as `RecordValue of typeName: string * fields: Map<string, Value ref>` — fields are ref cells.

**How to avoid:** Always wrap values: `Map.ofList [("Key", ref kv.Key); ("Value", ref kv.Value)]`.

**Warning signs:** F# compilation error `type mismatch: expected Value ref`.

### Pitfall 6: List Comprehension with ARROW Token Precedence

**What goes wrong:** `[for x in [1;2;3] -> x * 2]` fails to parse because `ARROW` (`->`) appears inside a list context where it conflicts with match-expression arrows.

**Why it happens:** `ARROW` is used both in function/match syntax and in list comprehension body separator. Inside `Atom`, when the parser has seen `LBRACKET FOR IDENT IN Expr`, it must decide whether the next token (`ARROW`) reduces the comprehension rule or is part of something else.

**How to avoid:** The rule `LBRACKET FOR IDENT IN Expr ARROW Expr RBRACKET` is unambiguous given the `LBRACKET FOR IDENT IN` prefix. The parser knows it's in a comprehension context and `ARROW` is consumed by this rule. The comprehension rule is in `Atom`, not in `Expr` or `MatchClauses`, so no conflict with match arrows.

**Warning signs:** Parse error at `->` inside list comprehension.

## Code Examples

### StringSliceExpr AST Node

```fsharp
// Source: Phase 58 design — after IndexSet in Ast.fs
// Phase 58 (String Slicing): s.[start..stop] inclusive, s.[start..] to end
| StringSliceExpr of str: Expr * start: Expr * stop: Expr option * span: Span
```

### ListCompExpr AST Node

```fsharp
// Source: Phase 58 design — after StringSliceExpr in Ast.fs
// Phase 58 (List Comprehension): [for x in coll -> expr]
| ListCompExpr of var: string * collection: Expr * body: Expr * span: Span
```

### Parser: String Slice Rules (in Atom)

```fsharp
// Source: Phase 58 — add after existing `Atom DOTLBRACKET Expr RBRACKET` rule
| Atom DOTLBRACKET Expr DOTDOT Expr RBRACKET
    { StringSliceExpr($1, $3, Some $5, ruleSpan parseState 1 6) }
| Atom DOTLBRACKET Expr DOTDOT RBRACKET
    { StringSliceExpr($1, $3, None, ruleSpan parseState 1 5) }
```

### Parser: List Comprehension Rules (in Atom)

```fsharp
// Source: Phase 58 — add among list literal rules in Atom
| LBRACKET FOR IDENT IN Expr ARROW Expr RBRACKET
    { ListCompExpr($3, $5, $7, ruleSpan parseState 1 8) }
| LBRACKET FOR IDENT IN Expr DOTDOT Expr ARROW Expr RBRACKET
    { ListCompExpr($3, Range($5, $7, None, ruleSpan parseState 5 7), $9, ruleSpan parseState 1 10) }
```

### Eval: StringSliceExpr

```fsharp
// Source: Phase 58 — add after IndexSet arm in Eval.fs
| StringSliceExpr (strExpr, startExpr, stopOpt, _) ->
    let strVal   = eval recEnv moduleEnv env false strExpr
    let startVal = eval recEnv moduleEnv env false startExpr
    match strVal, startVal with
    | StringValue s, IntValue start ->
        let stop =
            match stopOpt with
            | Some stopExpr ->
                match eval recEnv moduleEnv env false stopExpr with
                | IntValue i -> i
                | _ -> failwith "String slice: end index must be int"
            | None -> s.Length - 1
        StringValue (s.[start .. stop])
    | _ -> failwith "String slice: expected string and int start index"
```

### Eval: ListCompExpr

```fsharp
// Source: Phase 58 — add after StringSliceExpr arm in Eval.fs
| ListCompExpr (var, collExpr, bodyExpr, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let elements =
        match collVal with
        | ListValue xs -> xs
        | ArrayValue arr -> arr |> Array.toList
        | HashSetValue hs -> hs |> Seq.toList
        | QueueValue q -> q |> Seq.toList
        | MutableListValue ml -> ml |> Seq.toList
        | _ -> failwith "List comprehension: collection must be a list, array, or native collection"
    ListValue (elements |> List.map (fun elemVal ->
        let loopEnv = Map.add var elemVal env
        eval recEnv moduleEnv loopEnv false bodyExpr))
```

### Eval: ForInExpr Extended (replaces existing arm at ~line 963)

```fsharp
| ForInExpr (var, collExpr, body, _) ->
    let collVal = eval recEnv moduleEnv env false collExpr
    let elements =
        match collVal with
        | ListValue xs -> xs
        | ArrayValue arr -> arr |> Array.toList
        | HashSetValue hs -> hs |> Seq.toList
        | QueueValue q -> q |> Seq.toList
        | MutableListValue ml -> ml |> Seq.toList
        | HashtableValue ht ->
            ht |> Seq.map (fun kv ->
                let fields = Map.ofList [("Key", ref kv.Key); ("Value", ref kv.Value)]
                RecordValue("KeyValuePair", fields)) |> Seq.toList
        | _ -> failwith "for-in: collection must be a list, array, or native collection"
    for elemVal in elements do
        let loopEnv = Map.add var elemVal env
        eval recEnv moduleEnv loopEnv false body |> ignore
    TupleValue []
```

### Bidir: TData("KeyValuePair") FieldAccess (in Bidir.fs FieldAccess arm)

```fsharp
// Source: Phase 58 — add after THashtable arm in Bidir.fs FieldAccess
// Phase 58: KeyValuePair field access (for hashtable for-in iteration)
| TData("KeyValuePair", [keyTy; valTy]) ->
    match fieldName with
    | "Key"   -> (s1, keyTy)
    | "Value" -> (s1, valTy)
    | _ ->
        raise (TypeException { Kind = FieldAccessOnNonRecord resolvedTy; Span = span; Term = Some expr; ContextStack = ctx; Trace = [] })
```

### Bidir: ForInExpr for THashtable

```fsharp
// Source: Phase 58 — in ForInExpr arm of Bidir.fs
| THashtable (keyTy, valTy) ->
    (s1, TData("KeyValuePair", [keyTy; valTy]))
```

### Sample flt Test: String Slicing

```
// Test: String slicing s.[start..stop] and s.[start..] (LANG-01)
// --- Command: /Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let s = "hello"
let _ = println s.[1..3]
let _ = println s.[2..]
let _ = println s.[0..0]
// --- Output:
ell
llo
h
()
```

### Sample flt Test: List Comprehension

```
// Test: List comprehension [for x in coll -> expr] (LANG-02)
// --- Command: /Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let result1 = [for x in [1;2;3] -> x * 2]
let result2 = [for i in 0..4 -> i * i]
let _ = println (to_string result1)
let _ = println (to_string result2)
// --- Output:
[2; 4; 6]
[0; 1; 4; 9; 16]
()
```

### Sample flt Test: Native Collection for-in

```
// Test: for-in over native collections (LANG-03, PROP-05)
// --- Command: /Users/ohama/vibe/LangThree/src/LangThree/bin/Release/net10.0/LangThree %input
// --- Input:
let hs = HashSet ()
let _ = hs.Add(10)
let _ = hs.Add(20)
let mut sum = 0
let _ =
    for x in hs do
        sum <- sum + x
let _ = println (to_string sum)
let ht = Hashtable.create ()
let _ = Hashtable.set ht "a" 1
let _ = Hashtable.set ht "b" 2
let _ =
    for kv in ht do
        println (kv.Key ^ "=" ^ to_string kv.Value)
// --- Output:
30
a=1
b=2
()
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `string_sub s start len` (length-based) | `s.[start..stop]` (inclusive) | Phase 58 | More ergonomic; F# idiom |
| Manual `List.map` over range | `[for i in 0..n -> expr]` | Phase 58 | Concise list generation |
| `for x in list do` only | `for x in hashSet/queue/mutableList/hashtable do` | Phase 58 | All native collections iterable |
| No `.Key`/`.Value` on loop var | `for kv in ht do kv.Key ... kv.Value` | Phase 58 | Natural hashtable iteration |
| `// --- Stdout:` in tests | `// --- Output:` in tests | Phase 55 | All new tests use `// --- Output:` |

**Deprecated/outdated:**
- `string_sub s start len` builtin: Still works (length-based), but `s.[start..stop]` is the new idiomatic form.

## Open Questions

1. **List comprehension over Hashtable**
   - What we know: `[for kv in ht -> kv.Key]` — HashTable for-in yields `RecordValue("KeyValuePair", ...)`. Should `ListCompExpr` also handle `HashtableValue`?
   - What's unclear: Whether this is in scope for Phase 58 (not mentioned in requirements)
   - Recommendation: Add `HashtableValue` to `ListCompExpr` eval for consistency. It falls naturally from extending the match arm, costs nothing, and avoids future confusion.

2. **String slicing on CharValue sequences**
   - What we know: `StringValue` is an F# `string`, char access via `s.[i]` returns a `char`
   - What's unclear: Does `s.[1..3]` return a `StringValue` or `ListValue [CharValue ...]`?
   - Recommendation: Return `StringValue` (a substring). F# string slicing returns a string, not a char list. This is consistent with `string_sub`.

3. **Empty slice bounds checking**
   - What we know: F# `"hello".[5..4]` returns `""` (empty string, no exception)
   - What's unclear: Should `s.[6..]` (out of bounds) raise or return `""`?
   - Recommendation: Let F# native string slicing handle it — `s.[6..(s.Length-1)]` in F# raises `IndexOutOfRangeException` when start > string length. Add a guard: if `start > s.Length`, raise `LangThreeException`. If `start = s.Length`, return `""`.

4. **ForInExpr Bidir — try/catch vs explicit match refactoring**
   - What we know: Current code uses try/catch to unify with TList then TArray
   - What's unclear: Whether the refactoring to explicit match will break any existing tests that rely on the try/catch behavior for polymorphic types
   - Recommendation: Keep the explicit match arms for known types, then fall through to the original try/catch for unknown/polymorphic type vars (`TVar _` cases). The fallback path is needed for when collection type is not yet resolved.

## Sources

### Primary (HIGH confidence)

Direct codebase inspection:
- `Ast.fs` lines 49-123 — all Expr variants; Value DU lines 194-213
- `Parser.fsy` lines 240-364 — ForIn rules (252-256), IndexGet (361-362), Range (348-351), Atom structure
- `Eval.fs` lines 963-992 — ForInExpr and IndexGet arms
- `Bidir.fs` lines 231-252 — ForInExpr arm; lines 549-634 — FieldAccess arm; lines 685-739 — IndexGet/IndexSet arms
- `Lexer.fsl` lines 153-156 — DOTLBRACKET, DOTDOT, DOT tokenization
- `Type.fs` — TData, THashtable, TList, TArray type constructors
- `tests/flt/expr/loop/loop-for-in-list.flt` — existing ForIn test format
- Phase 57 RESEARCH.md — confirmed RecordValue field ref pattern, TData("MutableList", []) in Bidir

### Secondary (HIGH confidence)

- F# language spec (knowledge cutoff): String slicing `s.[start..stop]` is inclusive; handles empty when start > stop within bounds
- .NET docs: `Dictionary<K,V>` implements `IEnumerable<KeyValuePair<K,V>>`; `HashSet<T>`, `Queue<T>`, `List<T>` all implement `IEnumerable<T>` and support `Seq.toList`

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new libraries; all affected files directly inspected
- Architecture: HIGH — parser rules analyzed for conflicts; eval patterns match existing Phase 55-57 approaches; RecordValue for KVPair verified against Ast.fs type definitions
- Pitfalls: HIGH — LALR conflict analysis done; try/catch vs explicit match refactoring risk identified; ref cell requirement confirmed from Ast.fs

**Research date:** 2026-03-29
**Valid until:** Stable until Phase 54-57 patterns change (estimated 60+ days)
