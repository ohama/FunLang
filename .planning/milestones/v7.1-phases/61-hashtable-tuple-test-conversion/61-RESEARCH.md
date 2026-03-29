# Phase 61: Hashtable Tuple & Test Conversion - Research

**Researched:** 2026-03-29
**Domain:** LangThree interpreter — AST, Parser, Bidir.fs type-checking, Eval.fs runtime, flt test file paths
**Confidence:** HIGH

## Summary

Phase 61 has two distinct requirements. STR-01 changes how hashtable for-in iteration works: instead of producing a `KeyValuePair` record that users access via `.Key` and `.Value`, the loop variable becomes a `(key, value)` tuple enabling direct pattern destructuring like `for (k, v) in ht do`. TST-01 fixes the 5 currently failing flt tests — all caused by wrong binary path (`/Users/ohama/vibe/LangThree/...` instead of `/Users/ohama/vibe-coding/LangThree/...`) and converts the 3 dot-notation hashtable tests to use module function equivalents.

Running the full test suite (`../fslit/dist/FsLit tests/flt/`) currently reports 632/637 passing (at the test suite level) or 409/414 for file tests. The 5 failures are: `hashtable-forin.flt`, `hashtable-dot-api.flt`, `hashtable-keys-tryget.flt`, `property-count-consistency.flt`, and `str-concat-module.flt`. All 5 use the stale binary path `/Users/ohama/vibe/LangThree/...`. There are 39 flt files total with this stale path, but only 5 are currently failing because the rest either pass (the binary happens to exist at the old path) or their test logic was updated. The 3 hashtable tests that use dot notation (`ht.TryGetValue`, `ht.Count`, `ht.Keys`, `kv.Key`, `kv.Value`) must be converted to module functions as part of TST-01.

The core implementation insight: `ForInExpr` currently has `var: string`. To support `for (k, v) in ht do`, the AST must change `var: string` to `var: Pattern`. This cascades through 8 files (Ast.fs, Parser.fsy, Bidir.fs, Eval.fs, TypeCheck.fs, Infer.fs, Format.fs) but each change is localized and follows existing pattern-matching infrastructure that already handles `TuplePat` in `LetPat`, `matchPattern`, and lambda desugaring.

**Primary recommendation:** Two plans — Plan 01: AST change + Bidir + Eval for tuple for-in (STR-01). Plan 02: Fix all 39 stale-path flt tests + convert 3 dot-notation hashtable tests to module functions (TST-01).

## Standard Stack

This is an internal language implementation project with no external dependencies. All changes are in-project F# source files.

### Core Files

| File | Purpose | Role in This Phase |
|------|---------|-------------------|
| `src/LangThree/Ast.fs` | AST definition | Change `ForInExpr.var: string` to `ForInExpr.var: Pattern` |
| `src/LangThree/Parser.fsy` | fsyacc grammar | Add `FOR TuplePattern IN` rules alongside existing `FOR IDENT IN` |
| `src/LangThree/Bidir.fs` | Bidirectional type-checker | Change `THashtable` case to emit `TTuple [keyTy; valTy]`; remove KeyValuePair field access; bind pattern in loopEnv |
| `src/LangThree/Eval.fs` | Runtime evaluator | Change HashtableValue case to produce `TupleValue [k; v]`; use `matchPattern` to bind loop variable |
| `src/LangThree/TypeCheck.fs` | Type check + module analysis | Update `rewriteModuleAccess` and collection functions that destructure ForInExpr |
| `src/LangThree/Infer.fs` | Type inference | Update `ForInExpr` match arm (trivial — ignores var) |
| `src/LangThree/Format.fs` | AST formatter | Update `ForInExpr` match arm for debug output |
| `tests/flt/file/hashtable/*.flt` | Integration tests | Fix path, convert dot-notation to module functions |

### No Changes Needed

| File | Reason |
|------|--------|
| `Prelude/Hashtable.fun` | Already has `tryGetValue` and `count` module functions |
| `src/LangThree/Lexer.fsl` | No new tokens — LPAREN and IDENT already tokenized |
| `src/LangThree/MatchCompile.fs` | ForInExpr not involved in match compilation |
| `src/LangThree/Exhaustive.fs` | ForInExpr not involved in exhaustiveness checking |

### Alternatives Considered

| Standard Approach | Alternative | Tradeoff |
|-------------------|-------------|----------|
| Change `ForInExpr.var` to `Pattern` | Desugar at parse time to `let (k,v) = __elem in ...` | Desugar adds synthetic LetPat inside body which complicates error spans. Pattern approach is cleaner and consistent with how `fun (x, y) ->` works |
| Update flt test Command lines | Keep old path; create symlink | Symlink is fragile; direct fix is unambiguous |

## Architecture Patterns

### Recommended Project Structure

```
Ast.fs
  ForInExpr of var: Pattern * ...   // was: var: string

Parser.fsy
  FOR TuplePattern IN Expr DO ...   // new rule
  FOR IDENT IN Expr DO ...          // existing rule → wrap IDENT in VarPat

Bidir.fs
  ForInExpr(pat, collExpr, body, span)
    THashtable → elemTy = TTuple [keyTy; valTy]   // was TData("KeyValuePair", ...)
    bind pattern vars into loopEnv via bindPatternInEnv

Eval.fs
  ForInExpr(pat, collExpr, body, _)
    HashtableValue ht → TupleValue [kv.Key; kv.Value]  // was RecordValue("KeyValuePair", ...)
    use matchPattern pat elemVal to build loopEnv

TypeCheck.fs
  rewriteModuleAccess: ForInExpr(var, ...) → pass var through unchanged (it's a Pattern)
  collectModuleRefs: ForInExpr(_, coll, body, _) → unchanged (var not inspected)
```

### Pattern 1: AST Change — ForInExpr var: string → var: Pattern

**What:** The `ForInExpr` union case in Ast.fs changes from `var: string` to `var: Pattern`. All match sites update their pattern to destructure a `Pattern` rather than a `string`.

**When to use:** Whenever an AST loop variable needs to support both single-ident and tuple destructuring.

**Example:**
```fsharp
// Ast.fs — Before (Phase 51)
| ForInExpr of var: string * collection: Expr * body: Expr * span: Span

// Ast.fs — After (Phase 61)
| ForInExpr of var: Pattern * collection: Expr * body: Expr * span: Span
```

### Pattern 2: Parser — IDENT wrapped in VarPat, TuplePattern used directly

**What:** The existing `FOR IDENT IN` rule wraps the ident string in `VarPat`. The new `FOR TuplePattern IN` rule passes the pattern directly.

**Example:**
```fsharp
// Parser.fsy — updated existing rules
| FOR IDENT IN Expr DO SeqExpr
    { ForInExpr(VarPat($2, symSpan parseState 2), $4, $6, ruleSpan parseState 1 6) }
| FOR IDENT IN Expr DO INDENT SeqExpr DEDENT
    { ForInExpr(VarPat($2, symSpan parseState 2), $4, $7, ruleSpan parseState 1 8) }

// Parser.fsy — new tuple destructuring rules
| FOR TuplePattern IN Expr DO SeqExpr
    { ForInExpr($2, $4, $6, ruleSpan parseState 1 6) }
| FOR TuplePattern IN Expr DO INDENT SeqExpr DEDENT
    { ForInExpr($2, $4, $7, ruleSpan parseState 1 8) }
```

`TuplePattern` is already defined in the grammar at line 395:
```
TuplePattern:
    | LPAREN PatternList RPAREN   { TuplePat($2, ruleSpan parseState 1 3) }
```

### Pattern 3: Bidir.fs — Bind Pattern in Loop Environment

**What:** Instead of `Map.add var (Scheme([], elemTy)) env2`, use `bindPatternInEnv` helper or inline logic that calls `unifyPatternWithType` (similar to how `LetPat` is type-checked).

**When to use:** Whenever the loop variable is a `Pattern` rather than a `string`.

**Example:**
```fsharp
// Bidir.fs — current (single var bind)
let loopEnv = Map.add var (Scheme([], elemTy)) env2
let s3, _bodyTy = synth ctorEnv recEnv ctx loopEnv body

// Bidir.fs — updated (pattern bind)
let s3, loopEnv = checkPattern ctorEnv recEnv ctx env2 pat elemTy
let s4, _bodyTy = synth ctorEnv recEnv ctx loopEnv body
(compose s4 (compose s3 s12), TTuple [])
```

Look at how `LetPat` is type-checked in Bidir.fs to find the existing `checkPattern`/`synthPat` helper.

### Pattern 4: Eval.fs — matchPattern Replaces Map.add

**What:** Instead of `let loopEnv = Map.add var elemVal env`, use the existing `matchPattern pat elemVal` function which handles `TuplePat` bindings.

**Example:**
```fsharp
// Eval.fs — current
| HashtableValue ht ->
    ht |> Seq.map (fun kv ->
        let fields = Map.ofList [("Key", ref kv.Key); ("Value", ref kv.Value)]
        RecordValue("KeyValuePair", fields)) |> Seq.toList
...
for elemVal in elements do
    let loopEnv = Map.add var elemVal env
    eval recEnv moduleEnv loopEnv false body |> ignore

// Eval.fs — updated
| HashtableValue ht ->
    ht |> Seq.map (fun kv -> TupleValue [kv.Key; kv.Value]) |> Seq.toList
...
for elemVal in elements do
    let loopEnv =
        match matchPattern pat elemVal with
        | Some bindings -> List.fold (fun e (n, v) -> Map.add n v e) env bindings
        | None -> failwith "for-in: pattern match failed"
    eval recEnv moduleEnv loopEnv false body |> ignore
```

### Pattern 5: TypeCheck.fs rewriteModuleAccess Update

**What:** The `rewriteModuleAccess` function has a `ForInExpr(var, coll, body, s)` arm. When `var` changes from `string` to `Pattern`, the rewrite must pass `var` through unchanged (patterns don't contain module references).

**Example:**
```fsharp
// TypeCheck.fs — current
| ForInExpr(var, coll, body, s) ->
    ForInExpr(var, rewriteModuleAccess modules coll, rewriteModuleAccess modules body, s)

// TypeCheck.fs — after (no change needed, Pattern is opaque to module rewriting)
| ForInExpr(var, coll, body, s) ->
    ForInExpr(var, rewriteModuleAccess modules coll, rewriteModuleAccess modules body, s)
// This is already correct — var (now a Pattern) passes through unchanged.
```

### Pattern 6: TST-01 — flt Path Fix

**What:** 39 flt files have `// --- Command: /Users/ohama/vibe/LangThree/...`. All need `vibe/LangThree` → `vibe-coding/LangThree`.

**Affected files (5 currently failing):**
- `tests/flt/file/hashtable/hashtable-forin.flt`
- `tests/flt/file/hashtable/hashtable-dot-api.flt`
- `tests/flt/file/hashtable/hashtable-keys-tryget.flt`
- `tests/flt/file/property/property-count-consistency.flt`
- `tests/flt/file/string/str-concat-module.flt`

**Remaining 34 files with stale path (currently passing because old binary still present):**
These must also be fixed for portability, even though they currently pass.

**Sed command for path fix:**
```bash
find tests/flt -name "*.flt" -exec \
  sed -i '' 's|/Users/ohama/vibe/LangThree|/Users/ohama/vibe-coding/LangThree|g' {} \;
```

### Pattern 7: Dot-Notation to Module Function Conversion

**What:** Three hashtable tests use dot-notation that will break once `KeyValuePair` is removed. These must be converted to use module function equivalents.

| Dot Notation | Module Function Equivalent |
|--------------|---------------------------|
| `ht.TryGetValue(key)` | `Hashtable.tryGetValue ht key` |
| `ht.Count` | `Hashtable.count ht` |
| `ht.Keys` | `Hashtable.keys ht` |
| `kv.Key` | First element of tuple `k` (from `for (k, v) in ht do`) |
| `kv.Value` | Second element of tuple `v` (from `for (k, v) in ht do`) |

**hashtable-forin.flt conversion:**
```
// Old (dot notation with KeyValuePair):
let _ = for kv in ht do
  let _ = println kv.Key
  println kv.Value

// New (tuple destructuring):
let _ = for (k, v) in ht do
  let _ = println k
  println v
```

**hashtable-dot-api.flt conversion:**
```
// Old:
let r1 = ht.TryGetValue("x")
let r2 = ht.TryGetValue("missing")
let _ = println (to_string r1)
let _ = println (to_string r2)
let _ = println (to_string ht.Count)
let ks = ht.Keys
let _ = println (to_string (List.length ks))

// New:
let r1 = Hashtable.tryGetValue ht "x"
let r2 = Hashtable.tryGetValue ht "missing"
let _ = println (to_string r1)
let _ = println (to_string r2)
let _ = println (to_string (Hashtable.count ht))
let ks = Hashtable.keys ht
let _ = println (to_string (List.length ks))
```

**hashtable-keys-tryget.flt conversion:**
```
// Old:
let keys = ht.Keys
let _ = println (to_string (List.sort keys))
let r1 = ht.TryGetValue("a")
let r2 = ht.TryGetValue("z")
let _ = println (to_string r1)
let _ = println (to_string r2)
let _ = println (to_string ht.Count)

// New:
let keys = Hashtable.keys ht
let _ = println (to_string (List.sort keys))
let r1 = Hashtable.tryGetValue ht "a"
let r2 = Hashtable.tryGetValue ht "z"
let _ = println (to_string r1)
let _ = println (to_string r2)
let _ = println (to_string (Hashtable.count ht))
```

### Anti-Patterns to Avoid

- **Forgetting to remove KeyValuePair field access from Bidir.fs:** After STR-01, the `TData("KeyValuePair", [keyTy; valTy])` arm in the field access case (lines 637-642) should be removed, otherwise it becomes dead code and may mislead future maintainers. However, removing it before all dot-notation tests are converted will break tests.
- **Fixing stale paths one-by-one:** Use sed to batch-fix all 39 files at once. Doing them individually is error-prone and wasteful.
- **Assuming TST-01 is trivial:** The 34 files that currently pass with the stale path pass only because the old binary happens to still exist at `/Users/ohama/vibe/LangThree/...`. These should all be fixed for long-term correctness.
- **Parser rule ordering:** In fsyacc, `FOR IDENT IN` and `FOR TuplePattern IN` may cause shift/reduce conflicts if ordering is wrong. `TuplePattern` starts with `LPAREN` which is unambiguous vs `IDENT`, so no conflict exists — but add `TuplePattern` rules AFTER `IDENT` rules to be safe.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Binding tuple pattern in loop env | Custom field-extraction logic | `matchPattern pat elemVal` (already in Eval.fs) | matchPattern handles VarPat, TuplePat, WildcardPat recursively; re-implementing it is error-prone |
| Type-checking loop pattern | Manual env extension per variable | Existing `synth`/`checkPattern` pattern from `LetPat` handling in Bidir.fs | Same infrastructure already handles `fun (x, y) -> ...` lambda tuple args |
| Finding all stale path flt files | Manual grep | `find tests/flt -name "*.flt"` + sed | 39 files; batch operation is reliable |

**Key insight:** The pattern-matching infrastructure (`matchPattern` in Eval.fs, `LetPat` type-checking in Bidir.fs) already handles tuple patterns completely. The ForInExpr change simply wires into that existing infrastructure.

## Common Pitfalls

### Pitfall 1: Bidir.fs Pattern Binding Lookup

**What goes wrong:** Developers look for a standalone `checkPattern` helper in Bidir.fs and can't find it, then write ad-hoc code.
**Why it happens:** The pattern type-checking logic in Bidir.fs is inlined into the `LetPat` and `LetPatDecl` branches rather than extracted as a reusable function.
**How to avoid:** Look at the `LetPat` arm in Bidir.fs. It calls `synthPat` or inline logic to get `(bindings: (string * Scheme) list)`. Extract that same logic for `ForInExpr`. Alternatively, write a small inline helper: given `pat` and `elemTy`, produce `Map<string, Scheme>` using unification.
**Warning signs:** If you find yourself duplicating type-unification logic for tuples.

### Pitfall 2: KeyValuePair Field Access Removal Timing

**What goes wrong:** Removing the `TData("KeyValuePair", ...)` field-access arm from Bidir.fs (lines 637-642) before converting all dot-notation tests causes those tests to fail with a type error.
**Why it happens:** The three `hashtable-{forin,dot-api,keys-tryget}.flt` tests currently use `kv.Key`, `ht.TryGetValue`, etc., which depend on this arm.
**How to avoid:** In Plan 01 (implementing STR-01), do NOT remove the KeyValuePair arm from Bidir.fs. Only remove it in Plan 02 after all test files have been converted. Or, keep KeyValuePair arm permanently as legacy dot-access still works.
**Warning signs:** Type error `Cannot access field on non-record type 'KeyValuePair'` in tests.

### Pitfall 3: Only Fixing Failing Tests, Not All 39 Stale-Path Files

**What goes wrong:** Developer fixes only the 5 currently-failing tests, leaving 34 with stale paths that will break if the old binary path disappears.
**Why it happens:** The 34 other files happen to pass because the binary at the old path still exists.
**How to avoid:** Run `grep -r "vibe/LangThree" tests/flt --include="*.flt" -l | wc -l` to verify 39 files; fix all of them.
**Warning signs:** CI failure on a different machine where old binary path doesn't exist.

### Pitfall 4: Parser Shift/Reduce Conflict With TuplePattern

**What goes wrong:** Adding `FOR TuplePattern IN` before `FOR IDENT IN` causes fsyacc to emit shift/reduce conflict warnings or errors.
**Why it happens:** In some grammar setups, lookahead can be ambiguous.
**How to avoid:** Add the tuple rules AFTER the existing IDENT rules. `LPAREN` (start of TuplePattern) and `IDENT` are distinct tokens, so there is no ambiguity at the grammar level.
**Warning signs:** `fsyacc` warning during build: "shift/reduce conflict in state..."

### Pitfall 5: Infer.fs Pattern Binding for ForInExpr

**What goes wrong:** Infer.fs `ForInExpr` arm ignores var (uses `_`), so it compiles fine with the type change but may not need updating at all.
**Why it happens:** Infer.fs is a simpler type inference pass that doesn't need to bind the loop variable.
**How to avoid:** Just change `ForInExpr (_, coll, body, _)` — the `_` for var already works whether var is `string` or `Pattern`.
**Warning signs:** None — this is a no-op change.

## Code Examples

### Current State: Hashtable For-In (to be changed)

```fsharp
// Eval.fs — current (produces RecordValue KeyValuePair)
| HashtableValue ht ->
    ht |> Seq.map (fun kv ->
        let fields = Map.ofList [("Key", ref kv.Key); ("Value", ref kv.Value)]
        RecordValue("KeyValuePair", fields)) |> Seq.toList
// ...
for elemVal in elements do
    let loopEnv = Map.add var elemVal env
    eval recEnv moduleEnv loopEnv false body |> ignore

// Bidir.fs — current (types element as TData("KeyValuePair", ...))
| THashtable (keyTy, valTy) ->
    (s1, TData("KeyValuePair", [keyTy; valTy]))
```

### Target State: Hashtable For-In (after STR-01)

```fsharp
// Eval.fs — after (produces TupleValue)
| HashtableValue ht ->
    ht |> Seq.map (fun kv -> TupleValue [kv.Key; kv.Value]) |> Seq.toList
// ...
for elemVal in elements do
    let loopEnv =
        match matchPattern var elemVal with
        | Some bindings -> List.fold (fun e (n, v) -> Map.add n v e) env bindings
        | None -> failwith "for-in: pattern match failed"
    eval recEnv moduleEnv loopEnv false body |> ignore

// Bidir.fs — after (types element as TTuple)
| THashtable (keyTy, valTy) ->
    (s1, TTuple [keyTy; valTy])
```

### New flt Syntax (after STR-01)

```
// Before (KeyValuePair dot notation):
let _ = for kv in ht do
  let _ = println kv.Key
  println kv.Value

// After (tuple destructuring):
let _ = for (k, v) in ht do
  let _ = println k
  println v

// Both single-var (non-hashtable) and tuple still work:
let _ = for x in [1; 2; 3] do println (to_string x)
let _ = for (a, b) in [(1, "x"); (2, "y")] do
  println (to_string a)
```

### Bidir.fs Pattern Binding (find in LetPat arm)

```fsharp
// Bidir.fs — how LetPat binds a pattern (reference for ForInExpr)
// Look for the LetPat arm which calls something like:
| LetPat (pat, bindingExpr, bodyExpr, span) ->
    let s1, valTy = synth ctorEnv recEnv ctx env bindingExpr
    let s2, patEnv = synthPat ctorEnv recEnv ctx (apply s1 env) pat (apply s1 valTy)
    // patEnv contains the new bindings from the pattern
    let s3, bodyTy = synth ctorEnv recEnv ctx patEnv bodyExpr
    (compose s3 (compose s2 s1), bodyTy)
```

To find the exact helper name, read the `LetPat` branch in Bidir.fs.

### flt Path Fix Command

```bash
# Fix all 39 stale-path flt files at once
find /Users/ohama/vibe-coding/LangThree/tests/flt -name "*.flt" -exec \
  sed -i '' 's|/Users/ohama/vibe/LangThree|/Users/ohama/vibe-coding/LangThree|g' {} \;

# Verify no stale paths remain
grep -r "vibe/LangThree" /Users/ohama/vibe-coding/LangThree/tests/flt --include="*.flt" | wc -l
# Expected: 0
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `for kv in ht do kv.Key` | `for (k, v) in ht do k` | Phase 61 (this phase) | Eliminates KeyValuePair synthetic record; tuple is idiomatic |
| `ht.TryGetValue(key)` | `Hashtable.tryGetValue ht key` | Phase 60 (builtin added), Phase 61 (test converted) | Consistent with module function pattern |
| `ht.Count` | `Hashtable.count ht` | Phase 61 (test converted) | Consistent with module function pattern |
| `ht.Keys` | `Hashtable.keys ht` | Phase 61 (test converted) | Consistent with module function pattern |
| `ForInExpr of var: string` | `ForInExpr of var: Pattern` | Phase 61 (this phase) | Enables pattern-based destructuring in for-in |

**Deprecated/outdated after this phase:**
- `TData("KeyValuePair", ...)` in Bidir.fs field access: no longer used for hashtable for-in (may be removed or kept as legacy)
- `RecordValue("KeyValuePair", fields)` in Eval.fs: removed, replaced by `TupleValue`

## Open Questions

1. **Should dot-notation hashtable access (`ht.TryGetValue`, `ht.Count`, `ht.Keys`) be removed from Bidir.fs?**
   - What we know: Phase 61 success criteria says "KeyValuePair-based code no longer exists" — implying full removal.
   - What's unclear: Whether `THashtable` field access in Bidir.fs (lines 628-635) should also be removed.
   - Recommendation: Keep `THashtable` field access in Bidir.fs for now (only remove the `TData("KeyValuePair", ...)` arm). The scope in the success criteria specifically targets KeyValuePair, not all dot-notation.

2. **What happens to `for x in ht do` (non-tuple single var over hashtable)?**
   - What we know: After STR-01, the element type changes to `TTuple [keyTy; valTy]`. A single VarPat var `x` would bind to the whole tuple.
   - What's unclear: Whether `x` bound to `TupleValue [k; v]` is useful or should be disallowed.
   - Recommendation: Allow it — `let _ = for kv in ht do ...` still works; `kv` will be a tuple, and users can access it with `let (k, v) = kv`. This is backward-compatible with the VarPat case.

3. **Does Bidir.fs have a reusable `synthPat`/`checkPattern` function?**
   - What we know: `LetPat` is type-checked in Bidir.fs and binds pattern variables.
   - What's unclear: Whether there is a standalone helper or if it's inlined.
   - Recommendation: Read the `LetPat` arm in Bidir.fs before writing Plan 01. If no helper exists, inline the same logic for `ForInExpr`.

## Sources

### Primary (HIGH confidence)
- Direct code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Ast.fs` — ForInExpr definition at line 119
- Direct code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Parser.fsy` — ForInExpr rules at lines 253-256; TuplePattern at line 396
- Direct code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Eval.fs` — ForInExpr eval at lines 1044-1062; matchPattern at lines 904-956
- Direct code inspection of `/Users/ohama/vibe-coding/LangThree/src/LangThree/Bidir.fs` — ForInExpr type-check at lines 231-267; KeyValuePair field access at lines 636-642
- Direct test run: `../fslit/dist/FsLit tests/flt/file/` — 409/414 passing; 5 failures all identified as stale binary path
- Manual invocation of failing test content: confirms tests work correctly with current binary

### Secondary (MEDIUM confidence)
- Grep of all 39 stale-path flt files — confirmed by `grep -r "vibe/LangThree" tests/flt --include="*.flt" -l | wc -l` returning 39
- Phase 60 RESEARCH.md — confirms module functions `tryGetValue`, `count`, `keys` are already available

## Metadata

**Confidence breakdown:**
- AST change (ForInExpr var: Pattern): HIGH — exact change is clear, all match sites identified
- Parser change (TuplePattern rule): HIGH — TuplePattern already exists in grammar; new rule is 2 lines
- Bidir.fs change: MEDIUM-HIGH — need to check exact shape of LetPat pattern binding before writing code
- Eval.fs change: HIGH — matchPattern already handles TuplePat; change is mechanical
- TST-01 path fix: HIGH — confirmed 39 files, sed command is reliable
- TST-01 dot-notation conversion: HIGH — module functions verified working; exact conversion logic is clear

**Research date:** 2026-03-29
**Valid until:** 2026-04-28 (stable codebase; no external dependencies)
