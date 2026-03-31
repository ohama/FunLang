# Architecture Patterns: Type Classes in LangThree

**Domain:** Adding type classes (ad-hoc polymorphism) to an existing ML-style interpreter
**Researched:** 2026-03-31
**Confidence:** HIGH

---

## Overview

This document covers the integration architecture for adding Haskell-style type classes to the
existing LangThree interpreter. The chosen implementation strategy is **dictionary passing**:
type class constraints are elaborated into explicit dictionary arguments during type checking,
and dictionary values are constructed and passed at call sites during evaluation.

The existing pipeline is unchanged structurally:

```
Source text
    ↓
[Lexer.fsl]  →  raw tokens
    ↓
[IndentFilter.fs]  →  filtered token stream
    ↓
[Parser.fsy]  →  Ast.Module
    ↓
[Elaborate.fs]  →  desugared Ast.Module
    ↓
[Bidir.fs / TypeCheck.fs]  →  type-checked (with constraint solving)
    ↓
[Eval.fs]  →  Value (with dictionary values in env)
```

The type class feature touches: `Ast.fs`, `Type.fs`, `TypeCheck.fs`, `Bidir.fs`, `Eval.fs`,
`Elaborate.fs` (optionally), `Parser.fsy`, and `Lexer.fsl`.

---

## Component 1: New AST Nodes (Ast.fs)

### New Decl variants

```fsharp
// Type class declaration: type class Show 'a where show : 'a -> string
| ClassDecl of
    name: string *
    typeParams: string list *
    methods: (string * TypeExpr) list *
    Span

// Instance declaration: instance Show int where show x = ...
| InstanceDecl of
    className: string *
    typeArgs: TypeExpr list *
    methods: (string * Expr) list *
    Span
```

`ClassDecl` defines the class name, its type parameters, and the type signatures of its methods.
`InstanceDecl` provides concrete method implementations for a particular type.

### New TypeExpr variant

```fsharp
// Constrained type: (Show 'a) => 'a -> string
// Multiple constraints: (Show 'a, Eq 'a) => 'a -> string
| TEConstrained of constraints: (string * TypeExpr list) list * inner: TypeExpr
```

This allows user-facing type annotations to specify class constraints. During elaboration this is
converted into the internal `Scheme`-with-constraints representation.

### New Expr variant (for elaborated code)

```fsharp
// Explicit dictionary application (produced by elaboration, not parsed)
// DictApp(expr, dictExpr) applies a dictionary argument to a constrained function
| DictApp of func: Expr * dict: Expr * span: Span

// Dictionary construction (produced by elaboration, not parsed)
// DictValue(className, typeName, methods) constructs a dictionary record
| DictLit of className: string * typeName: string * methods: (string * Expr) list * span: Span
```

These nodes are synthetic — the parser never produces them. They are introduced by `Elaborate.fs`
during constraint elaboration, after type checking resolves which instances to use.

**Practical alternative (simpler):** Do not introduce `DictApp`/`DictLit` AST nodes. Instead,
represent dictionaries as `RecordValue` in the evaluator and pass them as ordinary `Var` references
injected into the environment by the type checker. This avoids adding new AST nodes at the cost of
less explicit elaboration. Recommended for a first implementation.

---

## Component 2: Type System Extensions (Type.fs)

### New types

```fsharp
// Constraint: class name + type arguments
// Example: Show int, Eq 'a, Ord 'a
type Constraint = {
    ClassName: string
    TypeArgs: Type list
}

// Extended Scheme with constraints
// Old: Scheme of vars: int list * ty: Type
// New: Scheme of vars: int list * constraints: Constraint list * ty: Type
type Scheme = Scheme of vars: int list * constraints: Constraint list * ty: Type
```

**Impact of changing Scheme:** The `Scheme` DU is used throughout `Type.fs`, `Infer.fs`,
`Bidir.fs`, and `TypeCheck.fs`. Every `Scheme(vars, ty)` construction and pattern match must
gain the constraints list. For backwards compatibility during incremental implementation, start
by using `Scheme(vars, [], ty)` (empty constraints) for all existing code, then add constraint
threads progressively.

### Class and instance environments

```fsharp
// Method signature within a class
// Example: show : forall 'a. Show 'a => 'a -> string
type ClassMethodInfo = {
    MethodName: string
    MethodType: Type    // type with class's type param as TVar
}

// Type class declaration metadata
type ClassInfo = {
    TypeParams: int list        // fresh TVar ids for the class's type parameters
    Methods: ClassMethodInfo list
    SuperClasses: string list   // superclass constraints (empty for MVP)
}

// Instance metadata: which type does this instance cover?
type InstanceInfo = {
    ClassName: string
    InstanceTypes: Type list    // e.g., [TInt] for "instance Show int"
    DictName: string            // variable name of the dictionary in evaluation env
    MethodImpls: Map<string, Expr>  // method name -> implementation expr
}

// Class environment: class name -> class info
type ClassEnv = Map<string, ClassInfo>

// Instance environment: class name -> list of instances
type InstanceEnv = Map<string, InstanceInfo list>
```

These two environments are threaded through `TypeCheck.typeCheckModuleWithPrelude` alongside the
existing `TypeEnv`, `ConstructorEnv`, and `RecordEnv`.

---

## Component 3: Constraint Solving (Bidir.fs + new ConstraintSolver.fs)

### Where constraint solving fits

Constraint solving happens in two places:

1. **At `let` generalization boundaries** (`generalize` in `Infer.fs`): constraints on the type
   are retained in the scheme if the type variable they mention is generalized. This is the
   standard "ambiguity check" — a constraint like `Show 'a` stays in the scheme only if `'a` is
   a bound variable.

2. **At instantiation sites** (`instantiate` in `Infer.fs`): when a constrained scheme is
   instantiated, fresh type variables replace the bound vars, and the constraints become
   "active" — they must be resolved against the instance environment to find the dictionary.

### Constraint resolution algorithm

**Linear search through instances** is the correct starting implementation (this is what GHC
does internally for the common case):

```fsharp
/// Resolve a constraint to an instance.
/// Returns Some(InstanceInfo) if found, None if no instance matches.
let resolveConstraint (instEnv: InstanceEnv) (c: Constraint) (subst: Subst) : InstanceInfo option =
    match Map.tryFind c.ClassName instEnv with
    | None -> None
    | Some instances ->
        instances |> List.tryFind (fun inst ->
            // Try to unify c.TypeArgs with inst.InstanceTypes under current subst
            try
                let _ = List.map2 (fun a b -> unify (apply subst a) (apply subst b)) c.TypeArgs inst.InstanceTypes
                true
            with _ -> false)
```

This runs at every call site where a constrained function is applied. For an interpreter (not a
compiler), linear search is acceptable — there are never thousands of instances.

### Where in Bidir.fs

Add constraint resolution in the `Var` case of `synth`, immediately after `instantiate`:

```fsharp
| Var (name, span) ->
    match Map.tryFind name env with
    | Some (Scheme(vars, constraints, ty)) ->
        let freshTy = instantiate (Scheme(vars, [], ty))   // existing logic
        // NEW: for each constraint, resolve instance and record which dict to pass
        let resolvedDicts =
            constraints |> List.map (fun c ->
                let c' = { c with TypeArgs = List.map (apply currentSubst) c.TypeArgs }
                match resolveConstraint instEnv c' currentSubst with
                | Some inst -> inst.DictName
                | None ->
                    raise (TypeException { Kind = UnresolvedConstraint c'; ... }))
        // resolvedDicts is a list of env variable names to pass as implicit args
        (empty, freshTy, resolvedDicts)   // return with dict info
    | None -> raise (UnboundVar ...)
```

**Representation of resolved constraint evidence:** The simplest model is to return a list of
dictionary variable names alongside the type. The call site then wraps the original `Expr` with
`App(expr, Var dictName)` applications, one per resolved constraint. This is done during
elaboration (see Component 6).

---

## Component 4: Evaluation Changes (Eval.fs)

### Dictionary values

Dictionaries are first-class values in the evaluator. Use `RecordValue` (already exists in
`Ast.fs`):

```fsharp
// A dictionary for "Show int" looks like this at runtime:
// RecordValue("Show_int", Map.ofList [
//   ("show", BuiltinValue (fun (IntValue n) -> StringValue (string n)))
// ])
```

This means no new `Value` DU variant is needed. Dictionaries are ordinary records, and dictionary
method dispatch is ordinary field access.

### Dictionary environment initialization

When `TypeCheck.fs` processes an `InstanceDecl`:

1. Evaluate each method implementation expression in the current evaluation environment.
2. Package them into a `RecordValue` with a generated name (e.g., `"__dict_Show_int"`).
3. Bind that record value in the evaluation environment under the dictionary variable name.

The dictionary variable name (`inst.DictName`) is the same name that constraint resolution will
insert at call sites. Example: `__dict_Show_int` is bound in `Env` once the `instance Show int`
declaration is processed.

### evalExpr changes

No new `eval` cases are needed if the elaboration strategy (Component 6) produces ordinary
`App` and `Var` nodes. The evaluator sees:

```
// Original source: show 42
// After elaboration: show __dict_Show_int 42
// Which in AST terms: App(App(Var "show", Var "__dict_Show_int"), Number 42)
```

The `show` function itself was elaborated to take the dictionary as its first argument, and the
dictionary is in the environment. Evaluation proceeds normally.

**If elaboration is deferred** (phased implementation), add a special `DictMethod` expression
variant to carry method lookup at eval time. This trades simplicity for phased delivery but
creates divergence between the "real" representation and the eval representation.

**Recommendation:** Do full elaboration. It is cleaner and the evaluator stays untouched.

---

## Component 5: TypeCheck.fs Changes

### New environments threaded through typeCheckModule

The `typeCheckModuleWithPrelude` function currently passes:
- `TypeEnv` (variable types)
- `ConstructorEnv` (ADT constructors)
- `RecordEnv` (record field types)
- `Map<string, ModuleExports>` (modules)

Add:
- `ClassEnv` (class declarations)
- `InstanceEnv` (instance declarations)

These new environments are returned in `ModuleExports` so nested modules and opens work correctly.

### Processing ClassDecl

When a `ClassDecl` is encountered:

1. Create fresh `TVar` ids for the class's type parameters.
2. Elaborate each method's `TypeExpr` into an internal `Type` referencing those `TVar`s.
3. Add the class to `ClassEnv`.
4. Add each method to `TypeEnv` as a constrained scheme:
   `show : Scheme([a], [Constraint("Show", [TVar a])], TArrow(TVar a, TString))`

This means method names are in scope as ordinary polymorphic functions with class constraints.
No special treatment needed in `Bidir.fs` beyond the constraint resolution in the `Var` case.

### Processing InstanceDecl

When an `InstanceDecl` is encountered:

1. Look up the class in `ClassEnv`.
2. Unify the instance's type arguments with the class's type parameters to build a substitution.
3. Check each provided method has the correct type (type-check the method body against the
   substituted method type from the class).
4. Generate a dictionary variable name: `"__dict_" + className + "_" + typeArgStr`.
5. Build an `InstanceInfo` record and add it to `InstanceEnv`.
6. At evaluation time, construct the `RecordValue` dictionary and add it to `Env`.

### ModuleExports extension

```fsharp
type ModuleExports = {
    TypeEnv: TypeEnv
    CtorEnv: ConstructorEnv
    RecEnv: RecordEnv
    SubModules: Map<string, ModuleExports>
    // NEW:
    ClassEnv: ClassEnv
    InstEnv: InstanceEnv
}
```

---

## Component 6: Elaboration (Elaborate.fs or inline in TypeCheck.fs)

### What elaboration does

After type checking resolves which instance dictionary to use for each constrained call, the
program must be rewritten so that:

1. Every definition with a constraint receives implicit dictionary parameters.
2. Every call site passes the appropriate dictionary.

This is the **dictionary-passing elaboration** transform.

### Where to do it

**Option A: Inline in TypeCheck.fs** — resolve constraints during `synth`, immediately wrap
expressions with `App(expr, Var dictName)`. The resulting AST is already in "dictionary-passed"
form before the evaluator sees it.

**Option B: Separate Elaborate.fs pass** — type check first (record constraint evidence as
metadata), then walk the typed AST and insert dictionary applications. More modular but requires
storing evidence alongside AST nodes.

**Recommendation for MVP:** Option A (inline). It avoids adding new AST node types and reuses
the existing `App`/`Var` nodes. The cost is that `synth` in `Bidir.fs` returns slightly more
information, but the structure stays familiar.

### Elaboration of constrained functions

When `TypeCheck.fs` processes a `LetDecl` for a constrained function:

```
// Source: let show_pair (x, y) = "(" ++ show x ++ ", " ++ show y ++ ")"
// Type: (Show 'a) => ('a * 'a) -> string
```

The elaborator rewrites it to:

```
// Elaborated: let show_pair __dict_Show = fun (x, y) -> "(" ++ (__dict_Show.show x) ++ ...
// Type: Show_dict -> ('a * 'a) -> string
```

The dictionary becomes an explicit lambda parameter. All uses of `show` inside the body are
replaced with field accesses on `__dict_Show`.

### Elaboration of call sites

When `synth` resolves a constrained `Var` to specific instances, it inserts explicit `App` nodes:

```
// Source: show 42
// Elaborated: App(Var "show", Var "__dict_Show_int")  (before the user argument)
//   → App(App(Var "show", Var "__dict_Show_int"), Number 42)
```

The dictionary insertion happens in the `Var` case of `synth` — the dictionary arguments are
prepended before the function is applied to its user-visible arguments.

---

## Component 7: Parser and Lexer Changes

### New keywords

- `typeclass` (or `class` — conflicts with potential future OOP syntax; `typeclass` is safer)
- `instance`
- `where` (already lexed as `WHERE` in many LALR grammars; check existing token set)

Check whether `where` is already in the lexer for other purposes. If it is used as a contextual
keyword (e.g., for record `with` syntax), adding `WHERE` as a distinct token requires auditing
all existing grammar rules.

**Recommendation:** Use `where` for both class/instance bodies and any future where-clauses.
This matches Haskell's syntax and is immediately recognizable to ML/Haskell users.

### New grammar rules

```
// Class declaration
ClassDecl:
  | TYPECLASS IDENT TypeParams WHERE INDENT MethodSigs DEDENT
      { ClassDecl($2, $3, $6, ...) }

MethodSigs:
  | MethodSig                    { [$1] }
  | MethodSig MethodSigs         { $1 :: $2 }

MethodSig:
  | IDENT COLON TypeExpr         { ($1, $3) }  // show : 'a -> string

// Instance declaration
InstanceDecl:
  | INSTANCE ClassName TypeArgs WHERE INDENT MethodImpls DEDENT
      { InstanceDecl($2, $3, $6, ...) }

MethodImpls:
  | MethodImpl                   { [$1] }
  | MethodImpl MethodImpls       { $1 :: $2 }

MethodImpl:
  | IDENT EQUALS Expr            { ($1, $3) }  // show x = ...
```

### Constraint syntax in type annotations

Add `TEConstrained` parsing for user-visible annotations:

```
// (Show 'a) => 'a -> string
TypeExpr:
  | LPAREN ConstraintList RPAREN FATARROW TypeExpr  { TEConstrained($2, $5) }

ConstraintList:
  | Constraint                               { [$1] }
  | ConstraintList COMMA Constraint          { $1 @ [$3] }

Constraint:
  | IDENT TypeExprList           { ($1, $2) }  // Show 'a
```

`FATARROW` is a new token `=>`. Add it to the lexer.

---

## Build Order: What Depends on What

The features form a strict dependency chain. Each phase is independently testable.

### Phase 1: Core type class infrastructure (Type.fs, TypeCheck.fs)

**What:** Extend `Scheme` with constraints, add `ClassEnv`/`InstanceEnv`, add `ClassInfo`/
`InstanceInfo` types, add `resolveConstraint`.

**Does not require:** Parser changes, new AST nodes.

**Test:** F# unit tests in `LangThree.Tests` validating that `Scheme` with constraints can be
created, that `generalize` preserves constraints, and that `resolveConstraint` finds the right
instance by linear search.

**Files changed:** `Type.fs`, `TypeCheck.fs` (initialTypeEnv function signature update)

### Phase 2: ClassDecl/InstanceDecl parsing (Lexer.fsl, Parser.fsy, Ast.fs)

**What:** Add `typeclass`, `instance`, `where`, `=>` tokens. Add `ClassDecl`/`InstanceDecl` to
`Decl` DU. Add `TEConstrained` to `TypeExpr` DU. Add grammar rules.

**Does not require:** Constraint solving, elaboration, evaluation changes.

**Test:** Parse a `typeclass Show 'a where show : 'a -> string` declaration and verify the AST
matches expected structure. Parse `instance Show int where show x = to_string x` and verify.

**Files changed:** `Lexer.fsl`, `Parser.fsy`, `Ast.fs`

### Phase 3: TypeCheck wiring (TypeCheck.fs, Bidir.fs)

**What:** Thread `ClassEnv`/`InstanceEnv` through `typeCheckModuleWithPrelude`. Process
`ClassDecl` (add methods to TypeEnv as constrained schemes, add class to ClassEnv). Process
`InstanceDecl` (type check method bodies, add to InstanceEnv). Add constraint resolution in
`Bidir.synth`'s `Var` case.

**Does not require:** Evaluation changes (dictionaries not yet in Env).

**Test:** Type-check a program that defines a class and instance. Verify the resolved instance
is the correct one. Verify that a type error is raised for missing instances.

**Files changed:** `TypeCheck.fs`, `Bidir.fs`

### Phase 4: Evaluation — dictionary construction and method dispatch (Eval.fs, TypeCheck.fs)

**What:** When processing `InstanceDecl`, evaluate method bodies and construct `RecordValue`
dictionaries. Bind them in the evaluation environment under the generated `DictName`. Wire up
elaboration in `synth` to prepend `App(expr, Var dictName)` nodes.

**Does not require:** Any further parser changes.

**Test:** End-to-end `.flt` test: define `typeclass Show 'a`, define `instance Show int`,
call `show 42`, verify output is `"42"`. Then define `instance Show bool` and verify `show true`
outputs `"true"`. Then verify that a function constrained on `Show 'a` passes the right dict.

**Files changed:** `TypeCheck.fs` (instance processing), `Bidir.fs` (elaboration insertion),
`Eval.fs` (none, if elaboration rewrites to existing App/Var)

### Phase 5: Constrained polymorphic functions (end-to-end)

**What:** Handle the case where a user-defined function has a class constraint. The function
receives a dictionary parameter, uses it to call methods, and the call site resolves which
dictionary to pass.

**Example:**
```
let showPair (x, y) : string = show x ++ ", " ++ show y
// Type: (Show 'a) => 'a * 'a -> string
```

**Test:** Call `showPair (1, 2)` and `showPair (true, false)` and verify both work with the
appropriate dictionaries.

**Files changed:** `Bidir.fs` (elaboration of constrained let-bound functions), `TypeCheck.fs`

---

## Integration Points with Existing Architecture

### Scheme change: most invasive change

Changing `Scheme of vars * ty` to `Scheme of vars * constraints * ty` affects every pattern
match on `Scheme` in:

- `Type.fs`: `applyScheme`, `freeVarsScheme`, `formatSchemeNormalized`
- `Infer.fs`: `instantiate`, `generalize`, `inferPattern`
- `Bidir.fs`: every `Scheme(vars, ty)` construction and destruction (approx 20+ sites)
- `TypeCheck.fs`: `initialTypeEnv` (all schemes become `Scheme(vars, [], ty)`)

**Mitigation strategy:** Add a helper `mkScheme vars ty = Scheme(vars, [], ty)` and a helper
`schemeType (Scheme(_, _, ty)) = ty`. Use these in all existing code. Then add constraint-aware
variants only in the new code paths. This minimizes churn on existing pattern matches.

### ConstructorEnv, RecordEnv patterns are a model

The `ConstructorEnv` and `RecordEnv` additions in earlier milestones established the pattern for
extending `TypeCheck.typeCheckModuleWithPrelude` with new environment types. Follow the same
pattern for `ClassEnv`/`InstanceEnv`:

- Add to `ModuleExports`
- Thread through all `typeCheckDecl` recursive calls
- Merge on `OpenDecl` (same as existing `openModuleExports`)

### Bidir.synth's Var case is the correct injection point

The `Var` case in `synth` is where polymorphic instantiation happens. This is also where
constraint resolution must happen — when we instantiate a constrained scheme, we immediately
resolve the fresh-variable constraints against the instance environment. This keeps constraint
resolution close to instantiation and avoids a separate "constraint propagation" pass.

### Unify.fs does not change

Unification is purely structural. Constraints are not part of the `Type` DU — they are only in
`Scheme`. The unifier never sees constraints. This is a deliberate design: constraints live in
schemes, not in types, matching the standard Hindley-Milner-with-classes design.

### Eval.fs — minimal change required

If elaboration is done inline in `synth`/`TypeCheck.fs`, the evaluator receives a program that
already has explicit dictionary arguments. The evaluator sees only `App`, `Var`, `RecordValue`,
and `FieldAccess` — all existing node types. The only change to `Eval.fs` may be adding
`RecordValue` construction for the dictionary when processing `InstanceDecl`.

---

## Component Interaction Map

```
Lexer.fsl
  ├─ new tokens: TYPECLASS, INSTANCE, WHERE (if not existing), FATARROW (=>)
  └─ existing tokens unchanged

Parser.fsy
  ├─ new Decl rules: ClassDecl, InstanceDecl
  ├─ new TypeExpr rule: TEConstrained
  └─ existing rules unchanged

Ast.fs
  ├─ new Decl variants: ClassDecl, InstanceDecl
  ├─ new TypeExpr variant: TEConstrained
  └─ existing variants unchanged

Type.fs
  ├─ MODIFIED: Scheme gains constraints field
  ├─ new types: Constraint, ClassInfo, InstanceInfo, ClassEnv, InstanceEnv
  └─ helper fns: mkScheme, schemeType (for backwards compat)

Infer.fs
  ├─ MODIFIED: instantiate must drop constraints when instantiating (return active constraints)
  ├─ MODIFIED: generalize must retain constraints on generalized vars
  └─ freshVar, inferPattern unchanged

Unify.fs
  └─ UNCHANGED — constraints not in Type DU

Bidir.fs
  ├─ MODIFIED: synth Var case — resolve constraints, prepend dict args
  ├─ MODIFIED: synth Let case — add implicit dict params for constrained let-bindings
  └─ all other synth cases unchanged

TypeCheck.fs
  ├─ MODIFIED: typeCheckModuleWithPrelude gains ClassEnv, InstanceEnv params
  ├─ new: process ClassDecl (build ClassInfo, add methods to TypeEnv)
  ├─ new: process InstanceDecl (type-check bodies, build InstanceInfo, construct dict value)
  └─ initialTypeEnv: existing entries gain empty constraints list

Eval.fs
  ├─ POSSIBLY: add dict construction when processing InstanceDecl (in TypeCheck.fs eval path)
  └─ UNCHANGED if elaboration rewrites to App(Var dictName) before eval
```

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Storing constraints in the Type DU

Adding a `TConstraint` or `TForall` variant to `Type.fs` forces the unifier to handle
constraints structurally. This is the GADT/System F approach, not the Hindley-Milner approach.
In HM-with-classes, constraints live in `Scheme` only, not in types. The unifier stays simple.

**Detection:** If `Unify.fs` gains a new pattern match for class constraints, this anti-pattern
has occurred.

### Anti-Pattern 2: Resolving constraints at generalization time

Constraints should be resolved at **instantiation** (use sites), not at generalization (binding
sites). Resolving at generalization forces you to know the concrete types at the `let` boundary,
which breaks parametric polymorphism. A function `show_pair : (Show 'a) => 'a * 'a -> string`
must be generalized with the constraint retained, not resolved.

**Detection:** If `generalize` in `Infer.fs` calls `resolveConstraint`, this anti-pattern has
occurred.

### Anti-Pattern 3: Implementing method dispatch as a special-case eval path

It is tempting to add a `MethodCall(className, methodName, arg)` AST node and handle it
specially in `Eval.fs`. This creates a parallel evaluation path and diverges from the
dictionary-passing model. All method calls should desugar to ordinary function applications
before the evaluator sees them. The evaluator should be type-class-unaware.

**Detection:** If `Eval.fs` gains a `ClassEnv` or `InstanceEnv` parameter, this anti-pattern
has occurred.

### Anti-Pattern 4: Naming dictionaries by type string at call sites

Generating dictionary variable names using the string form of the type (e.g., `"__dict_Show_int"`)
works for monomorphic instances. For parametric instances like `instance Show 'a => Show ('a list)`,
the dictionary is not a fixed value but a function from `Show 'a` dictionary to `Show ('a list)`
dictionary. The naming scheme must handle this: parametric instances produce dictionary
**functions**, not dictionary records.

**For MVP:** Defer parametric instances. Only handle ground instances (no type variable in the
instance head). Document this limitation clearly in the milestone scope.

### Anti-Pattern 5: Putting type class processing in Elaborate.fs instead of TypeCheck.fs

The existing `Elaborate.fs` handles syntactic desugaring (infix operators, do-notation, etc.)
before type information is known. Type class elaboration (dictionary insertion) requires type
information — specifically, which instance was resolved. It must happen during or after type
checking, not before. The correct location is inline in `Bidir.fs` `synth`, driven by
`TypeCheck.fs` instance resolution.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Scheme extension with constraints | HIGH | Standard HM-with-classes; well-trodden path |
| Dictionary as RecordValue | HIGH | Reuses existing Value DU; no new eval node needed |
| ClassEnv/InstanceEnv threading | HIGH | Follows exact pattern of ConstructorEnv/RecordEnv |
| Constraint resolution via linear search | HIGH | Correct for interpreter scale; GHC uses same approach |
| Elaboration inline in synth | MEDIUM | Requires synth to return extra data; some refactoring needed |
| Parametric instances (Show 'a => Show 'a list) | LOW | Deferred to post-MVP; dictionary-function approach needed |
| Superclass constraints | LOW | Deferred; requires superclass dictionary embedding in subclass dict |
| WHERE token conflict with existing grammar | MEDIUM | Must audit IndentFilter and Parser for existing WHERE uses |

---

## Files Changed by Component

### Phase 1 — Core infrastructure
- `src/LangThree/Type.fs` — Scheme, Constraint, ClassInfo, InstanceInfo, ClassEnv, InstanceEnv

### Phase 2 — Parsing
- `src/LangThree/Lexer.fsl` — TYPECLASS, INSTANCE, FATARROW tokens
- `src/LangThree/Parser.fsy` — ClassDecl, InstanceDecl, TEConstrained rules
- `src/LangThree/Ast.fs` — ClassDecl, InstanceDecl Decl variants; TEConstrained TypeExpr variant

### Phase 3 — Type checking wiring
- `src/LangThree/TypeCheck.fs` — thread ClassEnv/InstanceEnv, process ClassDecl/InstanceDecl
- `src/LangThree/Bidir.fs` — Var case: constraint resolution + dict arg insertion
- `src/LangThree/Infer.fs` — instantiate and generalize: constraint threading

### Phase 4 — Evaluation
- `src/LangThree/TypeCheck.fs` — construct RecordValue dicts for instance declarations
- `src/LangThree/Eval.fs` — minimal or no changes (elaboration already rewrites AST)

---

## Sources

- [Implementing, and Understanding Type Classes — okmij.org](https://okmij.org/ftp/Computation/typeclass.html)
- [Hindley-Milner type system — Wikipedia](https://en.wikipedia.org/wiki/Hindley%E2%80%93Milner_type_system)
- [Hindley-Milner inference with constraints — Kwang's Haskell Blog](https://kseo.github.io/posts/2017-01-02-hindley-milner-inference-with-constraints.html)
- [Type Classes — Tufts CS150PLD Notes](https://www.cs.tufts.edu/comp/150PLD/Notes/TypeClasses.pdf)
- [GHC Instance declarations and resolution](https://downloads.haskell.org/ghc/latest/docs/users_guide/exts/instances.html)
