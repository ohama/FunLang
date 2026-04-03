# Feature Landscape: Typed AST Export

**Domain:** Compiler-facing typed AST — structured type info export from FunLang to FunLangCompiler
**Researched:** 2026-04-02
**Milestone focus:** Replace 6 tracking sets + 8 heuristic functions (~250 lines) in FunLangCompiler with first-class type info derived from HM inference results

---

## Context: What the Heuristics Are Replacing

FunLangCompiler currently has no access to FunLang's inferred types. Instead, Elaboration.fs carries forward-propagating sets that grow as the compiler encounters let-bindings, then queries them at call sites:

| Set / Function | Tracks | Used For |
|----------------|--------|----------|
| `ArrayVars: Set<string>` | Variable names bound to array-creating expressions | ForInExpr dispatch: `lang_for_in_array` vs `lang_for_in_list` |
| `CollectionVars: Map<string, CollectionKind>` | Variable names → HashSet/Queue/MutableList/Hashtable | ForInExpr dispatch: `lang_for_in_hashset/queue/mlist/hashtable` |
| `BoolVars: Set<string>` | Variable names bound to bool-producing expressions | `to_string` dispatch: `lang_to_string_bool` vs `lang_to_string_int` |
| `StringVars: Set<string>` | Variable names bound to string-valued expressions | IndexGet dispatch: `lang_string_char_at` vs `lang_index_get` |
| `StringFields: Set<string>` | Record field names with `TEString` type annotation | IndexGet dispatch for field accesses |
| `MutableVars: Set<string>` | Variable names introduced by `LetMut`/`Assign` | Closure capture coercion: `Ptr` vs `I64` |
| `isPtrParamBody` (250-line function) | Whether a lambda param needs `Ptr` (list/record/string/ADT) vs `I64` (int/bool) | Lambda param type at codegen |
| `isArrayExpr` | Structural pattern-match on AST to detect array origins | ForInExpr, for-in dispatch |
| `isStringExpr` | Structural pattern-match on AST to detect string origins | IndexGet dispatch |
| `isBoolExpr` | Structural pattern-match on AST to detect bool origins | `to_string` dispatch |
| `detectCollectionKind` | Structural pattern-match on AST to detect collection origins | ForInExpr dispatch |
| `bodyReturnsBool` | Traverses function body to detect bool-returning functions | ClosureInfo.InnerReturnIsBool |

The key insight: all of these are re-deriving information that HM inference already computed. The typed AST export needs to make that information available at each expression node, so the compiler can query it directly instead of re-inferring it from AST structure.

---

## Table Stakes

**These are required for FunLangCompiler to use the typed AST at all.** Without them, the compiler still needs the full set of heuristics.

### TS-1: Per-expression type annotation (replaces all heuristics)

Every expression node in the exported AST carries a resolved `Type` from HM inference.

| Compiler heuristic replaced | How the type annotation replaces it |
|-----------------------------|-------------------------------------|
| `ArrayVars` | `Var` node's type is `TArray _` |
| `CollectionVars` | `Var` node's type is `THashtable`, `TData("HashSet",_)`, `TData("Queue",_)`, `TData("MutableList",_)` |
| `BoolVars` + `isBoolExpr` | Expression node's type is `TBool` |
| `StringVars` + `isStringExpr` | Expression node's type is `TString` |
| `StringFields` | `FieldAccess` node's type is `TString` — no separate field set needed |
| `isPtrParamBody` | Lambda param's type from function type: `TArrow(paramType, _)` — if `paramType` is `TList _`, `TData _`, `TString`, `TArray _`, `THashtable _` → Ptr; if `TInt`, `TBool`, `TChar` → I64 |
| `isArrayExpr`, `isBoolExpr`, `isStringExpr`, `detectCollectionKind` | Replaced entirely by the type field on the expression node |
| `bodyReturnsBool` | Return type of lambda: `TArrow(_, TBool)` |

**Implementation notes:**
- The type must be fully resolved (substitutions applied). Returning a `TVar` for an uninferrable expression is acceptable but degrades to heuristic fallback.
- A `TError` type indicates inference failure — the compiler can fall back to existing heuristics for that node.
- The type field must be on every expression node, not just terminal nodes. The compiler needs the type of subexpressions (e.g., `collExpr` inside `ForInExpr`, `argExpr` inside `App`).

**Complexity:** High — requires threading the final substitution back through every expression node after inference completes, or building an expression-keyed type map during inference.

---

### TS-2: Mutable variable flag on let-bindings (replaces `MutableVars`)

The typed AST must clearly mark which variable bindings are mutable (`LetMut`) vs immutable (`Let`/`LetRec`).

**Compiler heuristic replaced:**
- `MutableVars: Set<string>` — compiler currently checks `Set.contains name env.MutableVars` at every `Var` reference to decide whether to emit a `LlvmLoadOp` from a `RefValue` Ptr cell.
- Closure capture coercion: `let capType = if Set.contains capName env.MutableVars then Ptr else I64`

**What the typed AST needs to provide:**
- A `isMutable: bool` flag on the binding node, OR
- The `LetMut` / `Let` distinction preserved in the typed AST (currently exists in `Ast.Expr` as separate constructors — this information must not be erased in conversion to typed AST)

**Note:** This is actually already present in `Ast.Expr` as separate `LetMut` vs `Let` constructors. The typed AST export must not collapse them into a single binding form. The compiler's `MutableVars` set exists because the set needs to propagate to child scopes. With per-node type info, the binding site is unambiguous, and the compiler can derive mutability from node kind rather than maintaining a set.

**Complexity:** Low — information already exists in AST.

---

### TS-3: Hashtable key type accessible at IndexGet / IndexSet sites (replaces key-type inference)

For `IndexGet(ht, key)` and `IndexSet(ht, key, val)`, the compiler must dispatch to `lang_index_get_str` vs `lang_index_get` based on whether the key is a string.

**Current behavior:** The compiler checks `idxVal.Type` after elaborating the index expression. Since type info is on the expression, this is partially working — but only because `String _` literals elaborate to `Ptr` and `Number _` literals elaborate to `I64`. Variable keys require `StringVars` to be queried.

**What the typed AST provides:** With per-expression types (TS-1), `idxExpr`'s type will be `TString` or `TInt` — no `StringVars` lookup needed.

**Complexity:** Zero additional work beyond TS-1.

---

### TS-4: `ForInExpr` collection type accessible at iteration site (replaces `CollectionVars` + `isArrayExpr`)

The `forInFn` dispatch in `elaborateExpr` selects `lang_for_in_array/list/hashset/queue/mlist/hashtable` based on the collection's type.

**Current behavior:** `detectCollectionKind env.CollectionVars collExpr` and `isArrayExpr env.ArrayVars collExpr` traverse the expression and check the variable sets.

**What the typed AST provides:** With TS-1, `collExpr`'s type will be one of:
- `TArray _` → `lang_for_in_array`
- `TList _` → `lang_for_in_list`
- `TData("HashSet", _)` → `lang_for_in_hashset`
- `TData("Queue", _)` → `lang_for_in_queue`
- `TData("MutableList", _)` → `lang_for_in_mlist`
- `THashtable _ _` → `lang_for_in_hashtable`

**Complexity:** Zero additional work beyond TS-1.

---

### TS-5: Lambda parameter type at definition site (replaces `isPtrParamBody`)

`isPtrParamBody` is the single most complex heuristic — a 250-line recursive traversal of the lambda body to determine if the parameter will be passed as `Ptr` or `I64`. This exists entirely because the compiler does not know the parameter's inferred type.

**What the typed AST provides:** With TS-1 applied to the `Lambda` node, the function's type is `TArrow(paramType, returnType)`. The compiler can read `paramType` directly:
- `TInt`, `TBool`, `TChar` → `I64`
- `TString`, `TList _`, `TArray _`, `THashtable _ _`, `TData _ _`, `TTuple _` → `Ptr`

**Note:** This also applies to `LetRec` bindings where the function's inferred type determines the parameter's IR type. The `isPtrParamBody` heuristic was needed because the compiler synthesized the Lambda parameter type from body analysis — with the typed AST, that analysis is done once by HM inference.

**Complexity:** Zero additional work beyond TS-1, but requires verifying that inference produces ground types (not `TVar`) for lambda parameters in all practical cases.

---

### TS-6: `to_string` dispatch type at call site (replaces `BoolVars` + `isBoolExpr`)

`to_string` dispatches to `lang_to_string_bool` vs `lang_to_string_int` based on whether the argument is bool. The compiler currently checks `argVal.Type = I1 || isBoolExpr env.BoolVars env.KnownFuncs argExpr`.

**What the typed AST provides:** With TS-1, `argExpr`'s type is `TBool` or `TInt`. Dispatch is a direct type check.

**Complexity:** Zero additional work beyond TS-1.

---

### TS-7: Export format — typed AST as a parallel structure

The typed AST must be a defined type in FunLang that can be serialized to a format FunLangCompiler can consume without depending on FunLang's internal `Type` module.

**Options:**

| Option | Description | Tradeoff |
|--------|-------------|----------|
| **A: Annotated Ast (parallel tree)** | New `TExpr` type that mirrors `Ast.Expr` but carries a `Type` at each node | Clean separation; compiler gets full AST + types; large type definition |
| **B: Span-keyed type map** | Map from `Span` (source location) to `Type`; original `Ast.Expr` unchanged | Minimal FunLang changes; compiler must correlate by span; fragile if spans are non-unique |
| **C: Inline annotation via `Annot` reuse** | Reuse `Ast.Annot` to wrap every expression with its inferred type | Abuses existing AST; pollutes pattern matches |
| **D: Separate serialized type file** | FunLang emits a JSON/binary file of `(span → typeString)` mappings | Language-agnostic; requires parsing overhead in compiler |

**Recommendation: Option A (annotated parallel tree).**

A `TExpr` type that mirrors `Ast.Expr` with a `ty: Type` field at each node is the cleanest interface. The compiler can pattern-match on structure and read `.ty` without span correlation. This is how GHC, OCaml, and most typed compilers represent post-inference ASTs.

**Implementation sketch:**
```fsharp
type TExpr =
    | TNumber of int * ty: Type * span: Span
    | TVar of string * ty: Type * span: Span
    | TApp of TExpr * TExpr * ty: Type * span: Span
    | TLambda of param: string * paramTy: Type * body: TExpr * ty: Type * span: Span
    // ... all Ast.Expr variants ...
```

The `ty` field carries the type of the entire expression. `TLambda` additionally carries `paramTy` to directly expose the parameter's inferred type (critical for TS-5).

**Complexity:** High — requires defining the full parallel type and a conversion pass after inference.

---

## Differentiators

**Useful but not strictly required to replace all heuristics.** The compiler can still function at lower quality without these.

### D-1: Typed pattern bindings

In `match` clauses and `LetPat`, the bound variables carry their inferred types. This would replace heuristic tracking of what type a `VarPat`-bound variable has (currently tracked via `BoolVars`, `StringVars`, etc. as variables pass through pattern destructuring).

**Value:** Replaces the fragile propagation of `BoolVars`/`StringVars` when pattern-match arms introduce new bindings. E.g., `match x with (a, b) -> ...` — the types of `a` and `b` are currently not available to the compiler without re-running inference.

**Complexity:** Medium — requires threading types into `MatchClause` and `Pattern` nodes in the typed AST.

---

### D-2: Resolved record field types in the typed AST

`FieldAccess(expr, fieldName)` nodes carry the type of the accessed field as an explicit annotation, not just the type of the resulting expression.

**Value:** Currently `StringFields` exists specifically because the compiler needs to know if a field access produces a string (for IndexGet dispatch). With per-expression types (TS-1), `FieldAccess` node's `ty` already provides this. This differentiator is largely superseded by TS-1 — listing it separately for clarity.

**Complexity:** Zero, subsumed by TS-1.

---

### D-3: Type info on declaration nodes (module-level bindings)

`LetDecl` and `LetRecDecl` nodes carry the inferred type scheme of the declared function/value. The compiler currently calls `prePassDecls` to collect type info from AST annotations (TypeExpr) — not from inferred types. With typed declarations, the compiler gets the actual inferred type, not just the user's annotation.

**Value:** Enables the compiler to know the full return type of module-level functions — currently `FuncSignature.ReturnIsBool` and `FuncSignature.InnerReturnIsBool` are computed heuristically from the body. With declaration types, these are read directly.

**Complexity:** Low — declaration types are available after the top-level type-check pass.

---

### D-4: Stable, version-tagged export format

The typed AST export includes a version tag so FunLangCompiler can detect incompatible FunLang versions at startup rather than producing silent miscompilations.

**Value:** Operational reliability as both projects evolve.

**Complexity:** Trivial — add a `version: string` field to the export root.

---

## Anti-Features

**Deliberately do not build these in this milestone.**

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Exporting `Scheme` (polymorphic types) at call sites** | FunLangCompiler needs monomorphic types at each use site, not polymorphic schemes. Exporting `Scheme` would require the compiler to instantiate them — re-implementing a piece of type inference in the compiler. | Export fully-applied, instantiated types at each expression node (post-substitution). |
| **Exporting type variable IDs** | Raw `TVar 1042` in the export is meaningless to the compiler; it cannot act on an unconstrained type variable. | Substitute all remaining `TVar` occurrences with their best-known concrete type, or leave as opaque `Unknown` so the compiler can fall back gracefully. |
| **Exporting constraint/typeclass info** | The compiler does not need constraint info; it needs resolved monomorphic types. Constraints are resolved by the instance selection in Bidir.fs before the typed AST is built. | Strip all constraint metadata from the export — the compiler sees only `Type` values, not `Scheme` values. |
| **Replacing MlirType in the compiler** | The compiler's `MlirType` (I64, Ptr, I1) is a different representation level from FunLang's `Type`. Do not try to unify them. | The compiler maps `Type → MlirType` at elaboration time using a straightforward function: `TInt/TBool/TChar → I64`, `TString/TList/TData/TArray/THashtable/TTuple → Ptr`. |
| **Lazy/on-demand type lookup** | Providing a query API `getTypeOf: Span -> Type option` instead of embedding types in nodes would require the compiler to maintain spans across transformations and make the interface fragile. | Embed types directly in AST nodes (Option A from TS-7). |
| **Type inference in the compiler** | Any heuristic that re-derives type information from AST structure is technical debt that the typed AST export should eliminate, not supplement. | After typed AST export is available, delete the heuristics entirely rather than keeping them as fallback. |
| **Exporting intermediate inference states** | The compiler needs the final fully-resolved types, not types mid-inference. Exporting before substitution is fully applied produces TVar noise. | Only export after applying the final `Subst` to every node type. |

---

## Feature Dependencies

```
TS-7: Typed AST format definition (TExpr type)
  └─► TS-1: Per-expression type annotation (conversion pass: Ast.Expr → TExpr with types from Bidir.fs)
        ├─► TS-2: Mutable variable flag (preserved from LetMut vs Let constructors)
        ├─► TS-3: Hashtable key type (falls out of TS-1 for index expressions)
        ├─► TS-4: ForInExpr collection type (falls out of TS-1 for collection expressions)
        ├─► TS-5: Lambda parameter type (falls out of TS-1 for function types)
        └─► TS-6: to_string dispatch type (falls out of TS-1 for argument expressions)

D-1: Typed pattern bindings
  └─► TS-1 (requires types to be computed for pattern-bound variables too)

D-3: Declaration types
  └─► TS-1 (declaration types available after top-level typecheck)
```

**Critical path:** TS-7 then TS-1. Everything else (TS-2 through TS-6, all differentiators) follows from TS-1 being available. The hard work is TS-7 (defining the format) and TS-1 (threading types through every expression node).

**Key dependency:** TS-1 requires that Bidir.fs can produce a complete final substitution after type-checking — either by exposing the accumulated substitution, or by running a "type-annotate" pass that walks the AST querying the type environment. The current Bidir.fs `synth` function is bidirectional and produces a `(Type, MlirOp list)` pair — it does not currently output an annotated AST. This is the primary implementation challenge.

---

## MVP Recommendation

### MVP Scope

**Minimum to replace all 6 tracking sets and 8 heuristics:**

1. **Define `TExpr`** — parallel typed AST type in a new file (e.g., `TypedAst.fs`), mirroring all `Ast.Expr` variants plus `ty: Type` and for lambdas `paramTy: Type`.

2. **Conversion pass** — `annotateExpr : TypeEnv -> Subst -> Ast.Expr -> TExpr` that walks the expression tree, looks up each sub-expression's type in the inference results, and builds the `TExpr` tree. Run this pass after `typeCheckModuleWithPrelude` produces the final substitution.

3. **Expose from FunLang** — make `TypedAst.TExpr` and `Type.Type` available as a public API, ideally through a new entry point in `TypeCheck.fs` that returns `(TExpr list, TypeEnv)` instead of just a type environment.

4. **Update FunLangCompiler** — add a reference to FunLang's assembly, receive `TExpr list` from the typed check entry point, and replace all `ElabEnv` set lookups with direct `.ty` queries on `TExpr` nodes.

5. **Delete the heuristics** — remove `ArrayVars`, `CollectionVars`, `BoolVars`, `StringVars`, `StringFields`, `MutableVars`, `isPtrParamBody`, `isArrayExpr`, `isStringExpr`, `isBoolExpr`, `detectCollectionKind`, `bodyReturnsBool` from Elaboration.fs.

### Post-MVP (Defer)

| Feature | Reason to Defer |
|---------|-----------------|
| D-1: Typed pattern bindings | Heuristics for pattern-bound variables are less common; tackle after core case works |
| D-3: Declaration types | `FuncSignature.ReturnIsBool` heuristic is less impactful than the core set; defer |
| D-4: Version tag | Nice-to-have; add in a follow-up |

---

## Confidence Assessment

| Area | Confidence | Basis |
|------|------------|-------|
| Heuristic inventory | HIGH | Read all 6 sets + 8 functions in Elaboration.fs directly |
| Type system completeness | HIGH | `Type.fs` has `TArray`, `THashtable`, `TData`, `TList`, `TBool`, `TString` — all types the heuristics detect are expressible |
| Conversion pass feasibility | MEDIUM | Bidir.fs produces types per expression but does not currently build an annotated tree; will require new scaffolding |
| Lambda param type coverage | MEDIUM | `isPtrParamBody` handles edge cases (closures, captured vars, tuple params) that depend on inference being ground; risk of residual TVar for some params |
| FunLangCompiler integration | HIGH | Elaboration.fs is well-structured; replacing set lookups with `.ty` field accesses is mechanical once types are available |

---

**Document Status:** Research complete for Typed AST export milestone
**Next Step:** Use this feature catalog to define the typed AST format and conversion pass requirements
