# Technology Stack: LangThree v10.0 Type Classes

**Project:** LangThree — ML-style functional language interpreter
**Researched:** 2026-03-31
**Milestone:** v10.0 — Haskell-style type classes with dictionary-passing implementation
**Confidence:** HIGH — implementation strategy derived directly from codebase inspection

---

## Existing Stack (No NuGet Changes Needed)

| Technology | Version | Role |
|------------|---------|------|
| F# | .NET 10 | Implementation language |
| fslex (FsLexYacc) | 12.x | Lexer — Lexer.fsl |
| fsyacc (FsLexYacc) | 12.x | LALR(1) parser — Parser.fsy |
| IndentFilter.fs | custom | Token-stream layout filter |
| Prelude/*.fun | .fun files | Auto-loaded standard library |

No new NuGet packages. Type classes are a language feature implemented entirely within the existing F# source.

---

## Implementation Strategy: Dictionary Passing

**Recommendation: Dictionary passing (over monomorphization or vtable dispatch).**

### Why Dictionary Passing

Three strategies exist for implementing type classes at runtime:

| Strategy | How It Works | Verdict |
|----------|-------------|---------|
| **Dictionary passing** | Each typeclass instance is a record of functions. At call sites with a typeclass constraint, the dictionary is passed as an extra argument. | **Recommended.** Standard approach for interpreted languages, natural fit for LangThree's tree-walking evaluator. |
| **Monomorphization** | Generate a specialized copy of each function for each type instantiation, eliminating dictionaries at code generation time. | Inappropriate for an interpreter. Requires full type information at code generation time. LangThree's evaluator is tree-walking, not a compiler. |
| **Vtable / inline dispatch** | Embed method pointers in the value representation (like C++ vtables). | Would require adding method tables to every `Value` variant — invasive refactor of `CustomEquality`/`CustomComparison` machinery that is already complex in Ast.fs. |

Dictionary passing is the correct choice because:
1. LangThree is a tree-walking interpreter. Dictionaries are just values — `RecordValue` or `BuiltinValue` tuples — that slot cleanly into the existing `Value` type.
2. The type checker already threads `ConstructorEnv` and `RecordEnv` through `synth`/`check`. A new `ClassEnv` + `InstanceEnv` follows the identical pattern.
3. The evaluator already evaluates to `FunctionValue`/`BuiltinValue`. Dictionary-passing turns `show x` into `(dict.show) x` — a field lookup followed by a function application, both already supported.
4. Haskell itself uses dictionary passing as its canonical implementation of type classes.

---

## Component Changes

### 1. Type.fs — Add Typeclass Constraint Representation

**Current state:** `Type` DU has no notion of constraints. `Scheme` is `Scheme of vars: int list * ty: Type`.

**Change required:** Extend `Scheme` to carry class constraints, and add a `Constraint` type.

```fsharp
/// A typeclass constraint: "Show 'a", "Eq 'a", "Ord 'a"
type Constraint =
    | ClassConstraint of className: string * typeVar: int  // e.g., Show (TVar 5)

/// Extended type scheme with constraints: forall 'a. Show 'a => 'a -> string
type Scheme = Scheme of vars: int list * constraints: Constraint list * ty: Type
```

**Why this shape:** Constraints are tied to the quantified variables in a scheme, exactly as in Haskell. The `typeVar: int` field refers to a `TVar` index from the `vars` list, which is the same integer representation already used throughout Type.fs, Unify.fs, Infer.fs, and Bidir.fs. No new type-variable representation is needed.

**Impact on existing code:** Every pattern match on `Scheme(vars, ty)` becomes `Scheme(vars, _, ty)` or `Scheme(vars, constraints, ty)`. This is a mechanical change — F# exhaustive matching ensures the compiler flags every site. Estimated ~15 sites across Type.fs, Infer.fs, Bidir.fs, TypeCheck.fs.

**Alternative considered:** Add a separate `ConstrainedScheme` DU case and keep `Scheme` unchanged. Rejected because it doubles every pattern-match site without simplifying anything.

### 2. Type.fs — Add Class and Instance Environments

**New types to add:**

```fsharp
/// A typeclass declaration: "typeclass Show 'a = { show : 'a -> string }"
type ClassInfo = {
    TypeParam: int                         // The TVar index for the class parameter ('a)
    Methods: Map<string, Type>             // method name -> method type (using TypeParam)
}

/// A typeclass instance: "instance Show int = { show = fun x -> to_string x }"
type InstanceInfo = {
    ClassName: string
    ConcreteType: Type                     // The type this instance is for (e.g., TInt, TData("Option", [TVar 0]))
    ConstraintParams: int list             // If constrained instance: extra type vars (e.g., 'a in Show (Option 'a))
    ConstraintRequirements: Constraint list // What constraints the instance itself requires (e.g., Show 'a)
    MethodImpls: Map<string, Expr>         // method name -> implementation expression (from Ast)
}

/// Typeclass environment: class name -> ClassInfo
type ClassEnv = Map<string, ClassInfo>

/// Instance environment: (class name * canonical type key) -> InstanceInfo
/// The canonical type key is a normalized string like "int", "bool", "Option"
type InstanceEnv = Map<string * string, InstanceInfo>
```

**The canonical type key** is needed because `InstanceEnv` maps pairs to instances. The key `("Show", "int")` resolves the `Show int` instance. For parameterized instances like `Show (Option 'a)`, the key is `("Show", "Option")` and the `ConstraintRequirements` carries `[ClassConstraint("Show", freshVar)]`.

### 3. Ast.fs — Add Two New Declaration Forms

**Add two variants to `Decl`:**

```fsharp
// In type Decl:
| TypeClassDecl of
    className: string *
    typeParam: string *                    // e.g., "'a" in "typeclass Show 'a"
    methods: (string * TypeExpr) list *    // method name + type signature
    Span

| InstanceDecl of
    className: string *
    instanceType: TypeExpr *               // the type being instantiated (e.g., TEInt, TEData("Option", [...]))
    constraints: (string * string) list *  // constraint list: [("Show", "'a")] for "where Show 'a"
    methods: (string * Expr) list *        // method implementations
    Span
```

**No new `Expr` variants** for the method call sites. Method calls look like ordinary function application: `show x`. Dictionary passing is invisible in the source syntax — the type checker rewrites calls to inject dictionary arguments.

**The `spanOf` function in Ast.fs** does not need changes — it only handles `Expr`, not `Decl`.

**`declSpanOf`** needs two new cases for the new `Decl` variants. This is a three-line addition.

### 4. Lexer.fsl and Parser.fsy — Two New Keyword/Construct Pairs

**New keywords:**

```
typeclass   (TYPECLASS token)
instance    (INSTANCE token)
where       (WHERE token — may already exist; check for GADT usage)
```

`where` may already appear in the lexer for GADT syntax. Check Lexer.fsl before adding.

**Grammar rules (Parser.fsy):**

Two new top-level `Decl` productions:

```
// TCLASS-01: typeclass declaration
TypeClassDecl:
    | TYPECLASS IDENT TYVAR EQUALS INDENT MethodSigList DEDENT
        { TypeClassDecl($2, $3, $6, ...) }

// TCLASS-02: instance declaration (unconstrained)
InstanceDecl:
    | INSTANCE IDENT TypeExpr EQUALS INDENT MethodImplList DEDENT
        { InstanceDecl($2, $3, [], $6, ...) }

// TCLASS-03: instance declaration with constraints
InstanceDecl:
    | INSTANCE IDENT TypeExpr WHERE ConstraintList EQUALS INDENT MethodImplList DEDENT
        { InstanceDecl($2, $3, $5, $8, ...) }
```

`MethodSigList` and `MethodImplList` are new nonterminals following the same pattern as record field lists. The LALR(1) parser handles these without conflict because `TYPECLASS` and `INSTANCE` are distinct shift states from all existing declaration forms (`LET`, `TYPE`, `EXCEPTION`, `OPEN`).

**No changes to `IndentFilter.fs`**: `EQUALS` + INDENT already triggers `InExprBlock` for method bodies, which is exactly right.

### 5. Elaborate.fs — Elaborate TypeClassDecl and InstanceDecl into Environments

**Add two new elaboration functions:**

```fsharp
/// Elaborate typeclass declaration into ClassInfo
val elaborateTypeClassDecl :
    className: string -> typeParam: string -> methods: (string * TypeExpr) list
    -> ClassInfo

/// Elaborate instance declaration into InstanceInfo
val elaborateInstanceDecl :
    ClassEnv -> className: string -> instanceType: TypeExpr ->
    constraints: (string * string) list -> methods: (string * Expr) list
    -> string * InstanceInfo     // returns (canonical key, InstanceInfo)
```

These follow the exact pattern of `elaborateTypeDecl` and `elaborateRecordDecl` already in Elaborate.fs.

### 6. Bidir.fs — Constraint Propagation and Dictionary Injection

This is the most complex change. Bidir.fs must:

**a) Track constraints during inference.**

When `synth` encounters a `Var` lookup whose `Scheme` has constraints, instantiate those constraints along with the type variables. Collect the constraints for the current scope.

The existing `synth` signature:
```fsharp
synth : ConstructorEnv -> RecordEnv -> InferContext list -> TypeEnv -> Expr -> Subst * Type
```

Becomes (extended):
```fsharp
synth : ConstructorEnv -> RecordEnv -> ClassEnv -> InstanceEnv ->
        InferContext list -> TypeEnv -> Expr -> Subst * Type * Constraint list
```

The returned `Constraint list` is the set of constraints that must be satisfied at the call site. The caller either resolves them (if the concrete type is known) or propagates them outward to the enclosing let-generalization.

**Alternative:** Use a mutable `constraints` accumulator (like `mutableVars` in Bidir.fs). This avoids changing the return type but requires careful scoping. The `mutableVars` pattern already exists in the codebase (`let mutable mutableVars : Set<string> = Set.empty`). For type classes, a mutable approach is viable but risks interference in recursive/mutual contexts. Recommend the explicit return-value approach for correctness, accepting the signature extension.

**b) Resolve instances at let-generalization boundaries.**

At `Let(name, value, body, span)`, after inferring the value type, resolve any constraints where the type is fully known (ground type), and propagate unresolved constraints into the generalized scheme.

**c) Rewrite call sites via dictionary insertion.**

When a method `show` is called on a value whose type is known to be `int`, rewrite:
```
show x
```
to:
```
(dictShow_int.show) x
```

where `dictShow_int` is the dictionary record for `Show int`, which Eval.fs has already evaluated and stored.

This rewriting happens at the `Var` case in `synth` when the resolved variable is a typeclass method. The rewrite produces an `App(FieldAccess(Var dictName, methodName), arg)` expression (or equivalent) that the evaluator handles without new machinery.

**The key insight:** dictionary rewriting transforms typeclass method calls into ordinary function applications before the evaluator sees them. The evaluator needs zero changes for the basic case.

### 7. TypeCheck.fs — Thread ClassEnv and InstanceEnv Through Declaration Processing

**TypeCheck.fs currently accumulates:**
- `TypeEnv` (variable types)
- `ConstructorEnv` (ADT constructors)
- `RecordEnv` (record types)

**Add:**
- `ClassEnv` (typeclass declarations)
- `InstanceEnv` (typeclass instances)

These are accumulated during the declaration processing loop in `typecheckDecls` (the function that processes `Decl list`). When a `TypeClassDecl` is encountered, elaborate it into `ClassEnv`. When an `InstanceDecl` is encountered, elaborate it into `InstanceEnv` and also emit a `let` binding for the dictionary value into both `TypeEnv` and the runtime environment.

**Dictionary name convention:** `__dict_ClassName_TypeKey` (e.g., `__dict_Show_int`). Double underscore marks compiler-generated names. This name is injected into `TypeEnv` at instance registration time.

### 8. Eval.fs — Evaluate Instance Method Bodies into Dictionary Values

When `TypeCheck.fs` processes an `InstanceDecl`, it must also emit a runtime dictionary value. The dictionary is a `RecordValue` (or a `TupleValue` of functions, indexed by method name).

**Recommended shape: RecordValue.**

```fsharp
// Instance "Show int" with method show = fun x -> to_string x
// Evaluates to:
RecordValue("__dict_Show_int", Map [
    "show", ref (FunctionValue("x", App(Var("to_string", _), Var("x", _)), env))
])
```

Eval.fs needs no new match arms. `RecordValue` field lookup (`FieldAccess` or dot-access) is already evaluated. The dictionary values are just ordinary `RecordValue`s stored in the environment under generated names.

**One new concern for Eval.fs:** constrained instances like `Show (Option 'a) where Show 'a` require that the dictionary for `Show (Option 'a)` can look up the dictionary for `Show 'a` at runtime. This means the dictionary constructor must accept the inner dictionary as a parameter:

```fsharp
// __dict_Show_Option = fun (dictShowA : Show 'a dict) ->
//     { show = fun x -> match x with Some v -> "Some(" ++ dictShowA.show v ++ ")" | None -> "None" }
```

This is a `FunctionValue` that returns a `RecordValue`. Already evaluable. No new evaluator machinery.

### 9. Unify.fs — Constraint Unification (Minimal Change)

Unification itself does not need to know about constraints. Constraints are handled at the `generalize` / `instantiate` level in Infer.fs. Unify.fs unifies types, not constraints.

**No change needed to Unify.fs.**

### 10. Infer.fs — Extend generalize and instantiate

**`instantiate`** must instantiate constraint type variables alongside the scheme type variables. A scheme `forall 'a. Show 'a => 'a -> string` instantiates to `Show 'x => 'x -> string` with a fresh `'x`.

**`generalize`** must collect constraints for free variables and include them in the resulting scheme. A function inferred to have type `'a -> string` with constraint `Show 'a` in scope generalizes to `forall 'a. Show 'a => 'a -> string`.

Both changes are localized to these two functions in Infer.fs.

---

## Summary: What Changes Per File

| File | Change | Scope |
|------|--------|-------|
| `Type.fs` | Extend `Scheme` with `Constraint list`; add `Constraint`, `ClassInfo`, `InstanceInfo`, `ClassEnv`, `InstanceEnv` types | ~40 lines |
| `Ast.fs` | Add `TypeClassDecl` and `InstanceDecl` to `Decl` DU; extend `declSpanOf` | ~12 lines |
| `Lexer.fsl` | Add `typeclass`, `instance`, `where` keywords (if `where` not already present) | ~3-5 lines |
| `Parser.fsy` | Add `TypeClassDecl`, `InstanceDecl` grammar rules; `MethodSigList`, `MethodImplList` nonterminals | ~30-40 lines |
| `Elaborate.fs` | Add `elaborateTypeClassDecl`, `elaborateInstanceDecl` | ~50 lines |
| `Infer.fs` | Extend `instantiate` and `generalize` to handle `Constraint list` in `Scheme` | ~20 lines |
| `Unify.fs` | **No change** | — |
| `Bidir.fs` | Thread `ClassEnv`/`InstanceEnv` through `synth`/`check`; constraint resolution; dictionary injection at `Var` sites | ~100-150 lines |
| `TypeCheck.fs` | Thread `ClassEnv`/`InstanceEnv` through declaration processing; emit dictionary bindings for `InstanceDecl` | ~50 lines |
| `Eval.fs` | **No new match arms** for basic case; constrained instances evaluated as curried `FunctionValue` returning `RecordValue` | ~0-20 lines |
| `Format.fs` | Add formatting for `TypeClassDecl` and `InstanceDecl` (if --emit-ast used) | ~10 lines |
| `Exhaustive.fs` | No change — type class methods are not match scrutinees | — |
| `Prelude/*.fun` | Add `Show`, `Eq`, `Ord` instances for builtin types; refactor `to_string` overloading | ~50-80 lines |

**Total estimated change: ~400-450 lines across 10 files. No new NuGet packages.**

---

## Constraint on Existing Scheme Usages

Changing `Scheme` from `Scheme of vars: int list * ty: Type` to `Scheme of vars: int list * constraints: Constraint list * ty: Type` affects every pattern-match on `Scheme`. Current match sites (confirmed by source inspection):

- `Type.fs`: `applyScheme`, `freeVarsScheme`, `formatSchemeNormalized` — 3 sites
- `Infer.fs`: `instantiate`, `generalize`, `inferPattern` — 3 sites
- `Bidir.fs`: `synth` Let/LetRec/LetMut cases, `check` — ~8 sites
- `TypeCheck.fs`: `initialTypeEnv` declarations (all use `Scheme([], ty)`) — ~60 sites (mechanical `Scheme([], [], ty)` update)

The `TypeCheck.fs` initial environment declarations are the largest mechanical change. All existing entries have `Scheme([], ty)` (no type variables, no constraints) and become `Scheme([], [], ty)`. This is a sed-level change with zero semantic content.

---

## Alternatives Considered

| Decision | Alternative | Why This Way |
|----------|-------------|--------------|
| Dictionary passing | Monomorphization | Interpreter is tree-walking; no code generation phase to specialize into |
| Dictionary passing | Vtable in Value DU | Would require adding method pointers to every Value variant; breaks CustomEquality |
| `RecordValue` for dictionaries | New `DictValue` variant | RecordValue already supports field lookup; zero new Eval.fs machinery |
| Extend `Scheme` signature | New `ConstrainedScheme` DU case | Would double every pattern match; single clean shape is better |
| `__dict_ClassName_TypeKey` naming | Fresh integer suffixes | Readable in --emit-ast output; deterministic (no counter) |
| Constrained instances as curried functions | Monomorphic dictionary records | Runtime lookup of inner dicts requires the function-returning-record shape; this is how Haskell GHC does it too |
| Constraint tracking via returned list from synth | Mutable accumulator (like mutableVars) | Recursive/mutual let contexts require correct scoping; explicit return is safer |

---

## Risk Assessment

| Area | Risk | Mitigation |
|------|------|------------|
| `Scheme` shape change | ~70 mechanical sites in TypeCheck.fs initialTypeEnv | All are `Scheme([], ty)` → `Scheme([], [], ty)`; zero semantic change; F# exhaustive matching catches missed sites |
| Bidir.fs signature extension | Many call sites for `synth`/`check` | Add ClassEnv/InstanceEnv as parameters at the end; existing callers pass empty maps; progressive roll-in |
| Constraint resolution in Bidir | Incomplete constraint solving could cause silent type errors | Resolve eagerly at let-boundaries; emit error if constraint unsatisfiable rather than silently ignoring |
| `where` keyword conflict | `where` might conflict with indentation-sensitive contexts | Check whether `where` is already used (GADT) before adding; consider `with` (already used for try-with) — may need a fresh token |
| Constrained instance dictionary lookup | At runtime, must find inner dict `Show 'a` before constructing `Show (Option 'a)` | Inner dicts are looked up by name `__dict_Show_X` in the eval env; same lookup mechanism as ordinary variables |
| Prelude integration | Existing `to_string` is a builtin `Scheme([0], TArrow(TVar 0, TString))`; wrapping into a typeclass changes its call convention | Staged migration: keep `to_string` as builtin; add `Show` typeclass that calls `to_string` for builtins; full migration is a follow-on milestone |

---

## Sources

- Codebase inspection: `Type.fs`, `Ast.fs`, `Infer.fs`, `Bidir.fs`, `TypeCheck.fs`, `Eval.fs`, `Unify.fs`, `Elaborate.fs` (all read directly, 2026-03-31)
- Hindley-Milner with type classes: Jones (1994) "Qualified Types: Theory and Practice" — the standard reference for dictionary-passing HM with constraints
- GHC Core desugaring: type class instances desugar to records of functions; method calls desugar to record projections — same shape as this design
