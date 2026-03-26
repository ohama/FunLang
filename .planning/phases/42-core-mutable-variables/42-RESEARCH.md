# Phase 42: Core Mutable Variables - Research

**Researched:** 2026-03-26
**Domain:** F# parser/evaluator extension for mutable variable bindings
**Confidence:** HIGH

## Summary

This phase adds `let mut x = expr` declarations and `x <- expr` reassignment to LangThree at both expression level and module level. The existing codebase already has all lexer tokens needed (MUTABLE, LARROW), mutable record field infrastructure (SetField AST node, `<-` parsing for `atom.field <- expr`), and ref-cell patterns in Eval (RecordValue uses `Value ref`). The work is purely additive: new AST nodes, new parser rules, new type checker logic, and new eval cases.

The main design decisions are:
1. **AST representation**: Add `LetMut` (expression-level), `LetMutDecl` (module-level), and `Assign` (reassignment) nodes rather than overloading existing Let/SetField
2. **Eval strategy**: Use `Value ref` in environment for mutable variables (the same pattern RecordValue already uses for mutable fields)
3. **Type checker strategy**: Track mutability via a separate `Set<string>` (mutable variables set) threaded alongside `TypeEnv`, rather than extending `Scheme`
4. **Parser disambiguation**: `IDENT LARROW Expr` is unambiguous because the existing `Atom DOT IDENT LARROW Expr` requires a dot -- bare `IDENT LARROW Expr` has no conflict

**Primary recommendation:** Add three new AST nodes (LetMut, LetMutDecl, Assign), use `Map<string, Value ref>` or a parallel mutable-ref map in Eval, and track mutability as a `Set<string>` in the type checker.

## Architecture Patterns

### Recommended AST Extensions

```fsharp
// In Ast.fs - Expr type
| LetMut of name: string * value: Expr * body: Expr * span: Span
    // let mut x = expr1 in expr2 (expression-level)
| Assign of name: string * value: Expr * span: Span
    // x <- expr (reassignment, returns unit)

// In Ast.fs - Decl type
| LetMutDecl of name: string * body: Expr * Span
    // let mut x = expr (module-level)
```

**Why separate nodes instead of reusing Let/LetDecl with a flag:**
- Pattern matching in Eval and TypeCheck is clearer with distinct cases
- No risk of breaking 41 phases of existing Let handling
- SetField is already its own node (not a flag on FieldAccess), establishing the pattern

### Recommended Eval Strategy

The current `Env = Map<string, Value>` uses immutable values. For mutable variables, we need ref cells. Two approaches:

**Option A: Parallel mutable ref map (RECOMMENDED)**
```fsharp
// Add a mutable ref environment alongside the immutable one
type MutEnv = Map<string, Value ref>

// In eval function signature, add mutEnv parameter
// LetMut: create ref cell, add to mutEnv
// Assign: look up in mutEnv, set ref
// Var: check mutEnv first, then env
```

**Option B: Change Env to use Value ref everywhere**
This would be too invasive -- it changes every existing eval case.

**Option A is better** because it's additive. The existing `env: Map<string, Value>` stays untouched. A new `mutEnv: Map<string, Value ref>` is threaded through eval. The `Var` case checks `mutEnv` first (dereferencing the ref), then falls back to `env`.

For module-level mutable variables, `evalModuleDecls` already maintains an accumulating `Env`. The mutable ref map can be threaded similarly. Since module-level variables persist across declarations, the ref cell approach works naturally.

### Recommended Type Checker Strategy

The type environment uses `Scheme` (polymorphic type scheme). Mutable variables should NOT be generalized (they should be monomorphic, as in OCaml/F#). Track mutability with a `Set<string>` threaded through the type checker:

```fsharp
// In Bidir.fs synth/check, add mutableVars: Set<string> parameter
// LetMut: add name to mutableVars set, DON'T generalize the type
// Assign: check name is in mutableVars, unify RHS with variable's type, return unit
// Var: no change needed (Scheme lookup works for both)
```

Key type-checking rules:
- `let mut x = expr` infers type of `expr`, stores as `Scheme([], ty)` (no generalization even if polymorphic)
- `x <- expr` checks that `x` is in the mutable set, unifies `expr`'s type with `x`'s type, returns `TTuple []` (unit)
- Attempting `x <- expr` on an immutable variable is a type error

### Recommended Parser Rules

```
// Expression level - in Expr production:
| LET MUTABLE IDENT EQUALS Expr IN Expr
    { LetMut($3, $5, $7, ruleSpan parseState 1 7) }
| LET MUTABLE IDENT EQUALS INDENT Expr DEDENT IN Expr
    { LetMut($3, $6, $9, ruleSpan parseState 1 9) }
// Standalone (no continuation, like existing Let standalone):
| LET MUTABLE IDENT EQUALS INDENT Expr DEDENT
    { LetMut($3, $6, Tuple([], symSpan parseState 7), ruleSpan parseState 1 7) }

// Reassignment - in Expr production:
| IDENT LARROW Expr
    { Assign($1, $3, ruleSpan parseState 1 3) }

// Module level - in Decl production:
| LET MUTABLE IDENT EQUALS Expr
    { LetMutDecl($3, $5, ruleSpan parseState 1 5) }
| LET MUTABLE IDENT EQUALS INDENT Expr DEDENT
    { LetMutDecl($3, $6, ruleSpan parseState 1 7) }
```

**Disambiguation of `IDENT LARROW Expr` vs `Atom DOT IDENT LARROW Expr`:**
The existing SetField rule is `Atom DOT IDENT LARROW Expr`. The new Assign rule is `IDENT LARROW Expr`. These are unambiguous because:
- `IDENT LARROW` has no DOT
- `Atom DOT IDENT LARROW` requires a DOT
- The parser sees IDENT, then either DOT (field access path) or LARROW (assignment)

However, there is a subtlety: `IDENT` in `Atom` production creates a `Var` node. If the parser reduces `IDENT` to `Atom` before seeing `LARROW`, the `Atom DOT IDENT LARROW` rule would match but `IDENT LARROW` would not (since IDENT was already reduced to Atom).

**Solution:** Place `IDENT LARROW Expr` in the `Expr` production (same level as SetField). The parser will see `IDENT` and can look ahead to `LARROW` before reducing to `Atom`. Since fsyacc is LALR(1), the lookahead of LARROW after IDENT will select the `IDENT LARROW Expr` rule. This works because:
- `IDENT LARROW` is a unique 2-token prefix (no other rule starts with it)
- The existing `Atom DOT IDENT LARROW` starts differently at the Expr level

### IndentFilter Handling

The IndentFilter pushes `InLetDecl` context when it sees `Parser.LET`. With `let mut`, the next token after LET is MUTABLE, then IDENT, then EQUALS. The IndentFilter does NOT need special handling for MUTABLE because:

1. It only tracks `LET` for the offside rule (pushing InLetDecl context)
2. The MUTABLE token between LET and IDENT doesn't affect indentation
3. The offside column is set from the indent stack head, not from the token position

The filter's `blockLet` logic checks `state.IndentStack.Length > 1 && isExprContext state.Context`. This works unchanged for `let mut` because LET still triggers the same context push.

### Pattern: Anti-Patterns to Avoid

- **Don't extend Scheme with mutability info:** Scheme is for polymorphism. Mutability is an environment-level property, not a type-level property. A mutable variable's type is the same as its immutable counterpart; only the ability to reassign differs.
- **Don't reuse SetField for variable assignment:** SetField semantically means "set a record field through a reference." Variable assignment is conceptually different (rebinding a name to a new value). Separate AST nodes keep semantics clear.
- **Don't allow `let mut` with pattern bindings:** `let mut (x, y) = ...` is not in scope for this phase. Only simple `let mut IDENT = expr`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Ref cells for mutable vars | Custom mutable cell type | F# `Value ref` / `ref` | Already used by RecordValue for mutable fields |
| Error codes for mutability errors | Ad-hoc error strings | Extend `Diagnostic.ErrorKind` | Consistent with existing error infrastructure |

## Common Pitfalls

### Pitfall 1: Forgetting to prevent generalization of mutable variables
**What goes wrong:** If `let mut x = []` generalizes x to `'a list`, then `x <- [1]` and later reading x as `string list` would be unsound.
**Why it happens:** The normal Let path generalizes. LetMut must skip generalization.
**How to avoid:** In the type checker, LetMut stores `Scheme([], ty)` (monomorphic) regardless.
**Warning signs:** Polymorphic mutable variable passes type check but causes runtime type confusion.

### Pitfall 2: Closures capturing mutable variables
**What goes wrong:** If a closure captures a mutable variable, it should see updates to that variable. With `Map<string, Value>`, closures capture the value at closure-creation time.
**Why it happens:** FunctionValue stores `closure: Env` which is an immutable map snapshot.
**How to avoid:** The mutEnv (with `Value ref`) must also be captured in closures. When a Var lookup finds a ref in mutEnv, it dereferences at access time, getting the current value.
**Warning signs:** `let mut x = 0; let f () = x; x <- 1; f ()` returns 0 instead of 1.

**CRITICAL:** This means `FunctionValue` needs access to the mutable environment. Options:
- A: Thread mutEnv through eval, and closures implicitly capture it because refs are shared. Since `Value ref` is a heap-allocated reference, multiple closures sharing the same `mutEnv` map entry share the same ref cell. The closure's `env: Env` doesn't need to change -- the `mutEnv` refs are captured by the eval recursion.
- B: Store the current `Value ref` directly in the immutable `env` map entry... but `Env = Map<string, Value>` not `Map<string, Value ref>`.

**Best approach:** When creating a `FunctionValue`, the mutable variables visible at that point should be readable. Since `eval` threads `mutEnv`, and closures re-enter eval with their captured `env`, we need the closure to also carry or have access to the mutable refs.

**Simplest correct approach:** Store mutable variables in `env` as a special `RefValue of Value ref` variant (add to Value DU), or change the eval signature so mutEnv is threaded. The RefValue approach is simpler:

```fsharp
// Add to Value type:
| RefValue of Value ref

// LetMut in eval: create ref, wrap in RefValue, add to env
// Var in eval: if value is RefValue(r), return !r
// Assign in eval: look up name, get RefValue(r), set r := newValue
// Closures work automatically: env contains RefValue pointing to shared ref cell
```

This is the cleanest approach because closures already capture `env`, and `RefValue` carries the shared ref cell through the closure boundary.

### Pitfall 3: Parser shift-reduce conflicts with IDENT LARROW
**What goes wrong:** fsyacc may have ambiguity if IDENT can be reduced to Atom before seeing LARROW.
**Why it happens:** LALR(1) parser reduces eagerly.
**How to avoid:** Place `IDENT LARROW Expr` rule in Expr production. Since IDENT is used in Atom (which flows up through AppExpr -> Factor -> Term -> Expr), and the LARROW token is not a valid continuation for any of those intermediate rules, the parser will hold the IDENT on the stack and see LARROW as lookahead, choosing shift (to match `IDENT LARROW Expr`) over reduce (to match `Atom` then something else).

Actually, this needs careful analysis. In `Atom`, `IDENT` is immediately reduced. Then in `AppExpr`, `Atom` is reduced. Then up through `Factor`, `Term`, and into `Expr`. At the `Expr` level, if we have `IDENT LARROW Expr` as a rule, the parser would need to see IDENT without reducing it to Atom first.

**Revised approach:** The rule should be at the Expr level as `IDENT LARROW Expr`. When the parser has `IDENT` on the stack with `LARROW` as lookahead, it must decide: reduce IDENT to Atom, or shift LARROW (if there's a rule that continues IDENT LARROW). Since `IDENT LARROW` only appears in the `IDENT LARROW Expr` rule, fsyacc should generate a shift action for LARROW after IDENT. This creates a shift-reduce conflict that fsyacc resolves in favor of shift (default). This is the correct behavior.

**Verify by building:** After adding the rule, check fsyacc output for unexpected conflicts.

### Pitfall 4: Module-level mutable variable scoping
**What goes wrong:** Module-level `let mut x = 0` followed by `x <- 1` then `let _ = println (to_string x)` -- the x must retain its mutated value across declarations.
**Why it happens:** `evalModuleDecls` folds over declarations with an accumulating env. If using RefValue approach, the RefValue in env persists across declarations and retains mutations.
**How to avoid:** Use RefValue approach; the ref cell in env naturally persists.

## Code Examples

### AST Nodes to Add

```fsharp
// Ast.fs - add to Expr type
| LetMut of name: string * value: Expr * body: Expr * span: Span
| Assign of name: string * value: Expr * span: Span

// Ast.fs - add to Decl type
| LetMutDecl of name: string * body: Expr * Span

// Ast.fs - add to Value type
| RefValue of Value ref
```

### Eval Cases

```fsharp
// LetMut: create mutable ref, add RefValue to env
| LetMut (name, valueExpr, body, _) ->
    let value = eval recEnv moduleEnv env false valueExpr
    let refCell = ref value
    let env' = Map.add name (RefValue refCell) env
    eval recEnv moduleEnv env' tailPos body

// Assign: find RefValue, update ref cell, return unit
| Assign (name, valueExpr, _) ->
    let newValue = eval recEnv moduleEnv env false valueExpr
    match Map.tryFind name env with
    | Some (RefValue r) ->
        r.Value <- newValue
        TupleValue []
    | Some _ -> failwithf "Cannot assign to immutable variable: %s" name
    | None -> failwithf "Undefined variable: %s" name

// Var: dereference RefValue transparently
| Var (name, _) ->
    match Map.tryFind name env with
    | Some (RefValue r) -> r.Value
    | Some value -> value
    | None -> failwithf "Undefined variable: %s" name
```

### Module-level Eval

```fsharp
// In evalModuleDecls fold:
| LetMutDecl(name, body, _) ->
    let value = eval recEnv modEnv env false body
    let refCell = ref value
    (Map.add name (RefValue refCell) env, modEnv)
```

### Type Checker (Bidir.fs synth)

```fsharp
// LetMut: monomorphic binding, track mutability
| LetMut (name, value, body, span) ->
    let s1, valueTy = synth ctorEnv recEnv (InLetRhs (name, span) :: ctx) env value
    let env' = applyEnv s1 env
    // NO generalization -- mutable variables must be monomorphic
    let scheme = Scheme([], apply s1 valueTy)
    let bodyEnv = Map.add name scheme env'
    let mutableVars' = Set.add name mutableVars
    let s2, bodyTy = synthWithMut ctorEnv recEnv ctx bodyEnv mutableVars' body
    (compose s2 s1, bodyTy)

// Assign: check mutability, unify types, return unit
| Assign (name, value, span) ->
    if not (Set.contains name mutableVars) then
        raise (TypeException { Kind = ImmutableVariableAssignment name; ... })
    match Map.tryFind name env with
    | Some scheme ->
        let ty = instantiate scheme
        let s1, valTy = synth ctorEnv recEnv ctx env value
        let s2 = unifyWithContext ctx [] span (apply s1 ty) valTy
        (compose s2 s1, TTuple [])  // returns unit
    | None ->
        raise (TypeException { Kind = UnboundVariable name; ... })
```

### Diagnostic Extension

```fsharp
// Add to ErrorKind in Diagnostic.fs:
| ImmutableVariableAssignment of varName: string

// Add formatting:
| ImmutableVariableAssignment varName ->
    Some "E0320",
    sprintf "Cannot assign to immutable variable '%s'. Use 'let mut' to declare mutable variables." varName,
    Some "Declare the variable with 'let mut' if you need to reassign it"
```

## Files to Modify

| File | Changes |
|------|---------|
| `Ast.fs` | Add LetMut, Assign to Expr; LetMutDecl to Decl; RefValue to Value; update spanOf, declSpanOf, valueEqual, valueCompare, GetHashCode |
| `Parser.fsy` | Add grammar rules for `let mut` (Expr + Decl) and `IDENT LARROW Expr` |
| `Lexer.fsl` | No changes needed (MUTABLE and LARROW already exist) |
| `IndentFilter.fs` | Likely no changes needed (LET triggers InLetDecl regardless of MUTABLE) |
| `Bidir.fs` | Add synth cases for LetMut and Assign; thread mutableVars set |
| `Infer.fs` | Add stub cases for LetMut, Assign (like existing record stubs) |
| `Eval.fs` | Add eval cases for LetMut, Assign; modify Var to dereference RefValue; add LetMutDecl to evalModuleDecls |
| `TypeCheck.fs` | Add LetMutDecl handling in checkModuleDecls; update collectMatches, collectModuleRefs, rewriteModuleAccess for new nodes |
| `Format.fs` | Add formatting cases for LetMut, Assign, LetMutDecl |
| `Diagnostic.fs` | Add ImmutableVariableAssignment error kind |

## Open Questions

1. **Threading mutableVars through Bidir.fs**
   - What we know: Bidir.synth/check currently take `ctorEnv recEnv ctx env expr`. Adding `mutableVars: Set<string>` changes the signature significantly.
   - What's unclear: How many call sites need updating? Is it better to put mutableVars inside the context list?
   - Recommendation: Audit all synth/check call sites. If too many, consider adding mutableVars to a combined state record or passing it through the context stack as a new context variant `InMutableScope of Set<string>`.

2. **RefValue in Value equality/comparison**
   - What we know: Value has CustomEquality/CustomComparison. RefValue needs cases.
   - Recommendation: RefValue equality should dereference and compare the contained values. RefValue comparison likewise. This keeps mutable variables transparent for equality checks.

3. **Parser conflict verification**
   - What we know: `IDENT LARROW Expr` at the Expr level should work with shift preference.
   - What's unclear: Whether fsyacc generates a clean parse table or reports conflicts.
   - Recommendation: Build after adding parser rules and check for reported conflicts before proceeding.

## Sources

### Primary (HIGH confidence)
- Direct codebase analysis of all relevant source files
- Existing patterns: SetField (Ast.fs:100), RecordValue with `Value ref` (Ast.fs:192), MUTABLE/LARROW tokens (Lexer.fsl:71,120, Parser.fsy:61)
- IndentFilter LET handling (IndentFilter.fs:365-374)
- Bidir.fs SetField type checking (Bidir.fs:469-491)
- Eval.fs SetField evaluation (Eval.fs:1102-1111)

### Secondary (MEDIUM confidence)
- Parser disambiguation analysis based on LALR(1) theory
- RefValue approach for closure capture (design reasoning, not verified by implementation)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - this is a brownfield F# project, no new dependencies needed
- Architecture: HIGH - clear patterns established by SetField/RecordValue mutable fields
- Pitfalls: HIGH - closure capture is the main risk, RefValue approach addresses it
- Parser rules: MEDIUM - LALR(1) conflict analysis needs build verification

**Research date:** 2026-03-26
**Valid until:** indefinite (internal codebase patterns, no external dependencies)
