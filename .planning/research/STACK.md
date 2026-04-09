# Technology Stack: Type Class Maturity

**Project:** FunLang — ML-style functional language interpreter
**Researched:** 2026-04-08
**Milestone:** Advanced type class features — constrained instances, superclass constraints, Num/Eq migration, improved error messages
**Confidence:** HIGH — derived from direct codebase inspection + cross-language pattern survey

---

## Existing Stack (No NuGet Changes Needed)

| Technology | Version | Role |
|------------|---------|------|
| F# | .NET 10 | Implementation language |
| FsLexYacc (fslex + fsyacc) | 11.3.0 | LALR(1) parser generation |
| Argu | 6.2.5 | CLI argument parsing |
| Tomlyn | 2.3.0 | funproj.toml parsing |

No new NuGet packages are required. All changes are pure F# within existing files.

---

## Current State Assessment

### What Already Works (Do Not Re-Implement)

The existing type class system is more capable than it might appear:

**Constrained instances are already parsed and stored.** `InstanceDecl` in `Ast.fs`
carries `constraintExprs: (string * TypeExpr) list`. TypeCheck.fs elaborates these into
`InstanceInfo.InstanceConstraints: Constraint list`. The `resolveConstraint` function in
`Bidir.fs` (lines 92–112) already recurses into subgoals:

```fsharp
ii.InstanceConstraints |> List.forall (fun ic ->
    let resolvedArg = apply s (apply instSubst ic.TypeArg)
    let subgoal = { ic with TypeArg = resolvedArg }
    ...
    else resolveConstraint subgoal (depth + 1))
```

This means `Show 'a => Show ('a list)` already resolves through the constraint chain at
the type level. There are passing flt tests for this: `typeclass-constrained-instance-show-list.flt`
and `typeclass-constrained-instance-option.flt`.

**Superclass constraints are already parsed and stored.** `TypeClassDecl` in `Ast.fs`
carries `superclasses: string list`. TypeCheck.fs (line 1075–1083) embeds superclass
constraints into every method scheme at class declaration time. There is a passing flt test:
`typeclass-superclass.flt` (Ord extends Eq).

**`Eq` and `Show` are already defined in the Prelude** (`Prelude/Typeclass.fun`). The
instances for int, bool, string, char exist. The `typeclass-builtin-eq.flt` and
`typeclass-builtin-show.flt` tests pass.

### What Is Missing or Incomplete

1. **Constrained instances: runtime dispatch is broken.** The type checker accepts
   `instance Show 'a => Show ('a list)`, but `Elaborate.elaborateTypeclasses` (the eval
   pipeline step) promotes each instance method to a flat let-binding with the literal
   method name. For `Show ('a list)`, `show` becomes a top-level `let show xs = ...`.
   This name-collides with the `Show int` instance's `show`. The last instance wins at
   runtime, so constrained dispatch is effectively non-functional for evaluation. The
   resolution mechanism in `Bidir.generalize` is type-level-only.

2. **Superclass entailment at constraint resolution time is incomplete.** When resolving
   a constraint `Eq 'a`, the resolver checks the `InstanceEnv["Eq"]` list but does NOT
   check whether a superclass chain can satisfy it. Example: if `Ord int` is declared and
   `Ord` has superclass `Eq`, the resolver does not derive `Eq int` from `Ord int`. Each
   class's instances are resolved independently without walking the superclass graph.

3. **`+`, `-`, `*` are hardcoded in Bidir.fs and Eval.fs.** They are not dispatched
   through a `Num` type class. `=` is similarly hardcoded (structural equality in Eval.fs
   via `valuesEqual`). These cannot be overridden by user instances.

4. **Error messages for `NoInstance` are sparse.** The `NoInstance` error (E0701) prints
   available instances but does not explain the constraint chain that required the instance,
   or suggest which instance the user might want to add.

---

## Feature 1: Constrained Instances — Runtime Fix

### The Problem in Detail

The current elaboration pipeline:

```
InstanceDecl("Show", TList(TVar a), [("show", body)], [Show 'a], span)
  -> elaborateTypeclasses ->
LetDecl("show", body, span)   // name collision with Show int's "show"
```

Every instance of the same typeclass has the same method names. The current flat
promotion strategy works for monomorphic instances (only one `show : int -> string`
ever loaded) but breaks for constrained polymorphic ones where the same name must
dispatch differently based on the argument type.

### The GHC Approach (Dictionary Passing)

GHC compiles type classes to explicit dictionary records. Each `instance` declaration
becomes a dictionary value. At each call site where a method is used, the type checker
inserts an explicit dictionary argument. The method call becomes a record field projection.

This is the correct long-term architecture but requires deep changes to the evaluator:
all method calls must carry a dictionary argument, all instance declarations must build
dictionary values, and the elaboration pass must insert dictionary expressions at every
constrained call site.

### The FunLang Pragmatic Approach (Name Mangling)

Given FunLang's dictionary-passing elaboration is already committed to name-based dispatch
(methods become top-level lets), the correct extension is **instance-specific name mangling**.

Instead of `LetDecl("show", body)`, generate `LetDecl("show__Show_list", body)`. At each
call site where `show` is called on a list, the elaborator rewrites `show x` to
`show__Show_list x` when it can determine the concrete type.

**Problem:** This requires knowing the concrete type at elaboration time, which is not
available until after type checking. The elaboration pass runs before the evaluator but
after type checking.

**Solution:** Use the TypeAnnotationMap. After type checking, every expression span has a
recorded type. The elaboration pass can look up the type of each `show` call site in the
annotation map and select the correct mangled name.

**Implementation path:**
- After `Bidir.synth` populates `annotationMap`, run a second elaboration pass
- This second pass is type-aware: it looks up `annotationMap` for each method call site
- For each call to a typeclass method, look up the concrete type of the argument
- Select the correct instance's mangled method name

**Mangling scheme:** `<methodName>__<ClassName>_<TypeName>`
- `show__Show_int`, `show__Show_bool`, `show__Show_list`, `show__Show_option`
- `eq__Eq_int`, `eq__Eq_string`

This is consistent with GHC's dictionary-passing at the naming level, without building
full dictionary records.

**Scope for this milestone:** Implement mangled names for the concrete cases needed:
`Show ('a list)`, `Show ('a Option)`, `Eq ('a list)` (if added). The general mechanism
for arbitrary user-defined constrained instances requires the type-aware elaboration pass.

**Alternative (simpler but limited):** For the specific case of `Show`, special-case the
dispatch in Eval.fs: check the runtime value type and dispatch to the right `show`
implementation. This avoids the annotation map approach but only works for built-in types.

### Impact on Existing Tests

The 645 non-typeclass flt tests (724 total minus ~79 typeclass-related) are not affected
because they do not use typeclass dispatch. The 79 typeclass tests are at risk only if
the mangling scheme is not backward-compatible. The key invariant to preserve: for
monomorphic instances (no type variables in `InstanceType`), the mangling can be applied
transparently since there is no collision.

---

## Feature 2: Superclass Entailment

### The Problem in Detail

When resolving `Eq 'a` for some concrete type `int`, the resolver looks in
`InstanceEnv["Eq"]` for an instance whose `InstanceType` unifies with `int`. This works
for explicit `instance Eq int` declarations. But if a user only declares `instance Ord int`
(and `Ord` has superclass `Eq`), `Eq int` is not in `InstanceEnv["Eq"]` and the
constraint fails.

### The Haskell/GHC Approach

GHC's entailment algorithm (Wadler-Blott 1989, extended in GHC's `TcDeriv`) works as:

```
entails(ClassEnv, Constraint C T):
  1. Check if (C, T) directly in InstanceEnv.
  2. For each instance (C T' where [C1 T1', ..., Cn Tn']):
       if T unifies with T':
         check all subgoals Ci Ti' are entailed (recursively)
  3. For each class C' in ClassEnv where C is a superclass of C':
       check if (C', T) is entailed
```

Step 3 is the superclass entailment rule: if `Eq` is a superclass of `Ord`, and `Ord int`
is entailed, then `Eq int` is entailed.

### FunLang Implementation

The current `resolveConstraint` in `Bidir.fs` implements steps 1 and 2 but not step 3.

**Change needed in `Bidir.fs`:**

```fsharp
let rec resolveConstraint (c: Constraint) (depth: int) =
    if depth > 20 then false
    else
        // Step 1+2: Direct instance lookup (existing code)
        let directResolution = currentInstEnv |> Map.tryFind c.ClassName |> ...
        if directResolution then true
        else
            // Step 3: Superclass entailment — try to satisfy via subclasses
            // For each class C' where c.ClassName is in C'.Superclasses:
            //   check if (C', c.TypeArg) is entailed
            let classEnv = currentClassEnv
            classEnv |> Map.exists (fun className classInfo ->
                // classInfo does not currently store superclasses — THIS IS THE GAP
                ...)
```

**The gap:** `ClassInfo` in `Type.fs` is:
```fsharp
type ClassInfo = {
    Name: string
    TypeVar: int
    Methods: (string * Scheme) list
}
```

It does not store the superclass list. The superclass list is currently only used at
TypeClassDecl processing time (to embed superclass constraints into method schemes) and
then discarded.

**Fix:** Add `Superclasses: string list` to `ClassInfo`:

```fsharp
type ClassInfo = {
    Name: string
    TypeVar: int
    Methods: (string * Scheme) list
    Superclasses: string list   // NEW
}
```

Then in TypeCheck.fs where `ClassInfo` is constructed (line ~1087):
```fsharp
let classInfo = { Name = className; TypeVar = classVarId; Methods = methodSchemes; Superclasses = superclasses }
```

Then in `resolveConstraint`, after the direct lookup fails, iterate over `ClassEnv` to
find any class `C'` that lists `c.ClassName` in its `Superclasses`, and check if
`(C', c.TypeArg)` is entailed. This is a simple reverse lookup.

**Files changed:** `Type.fs` (ClassInfo), `TypeCheck.fs` (ClassInfo construction),
`Bidir.fs` (resolveConstraint), `ExportApi.fs` (if ClassInfo is exported).

---

## Feature 3: Num Type Class Migration

### The Problem

`+`, `-`, `*`, `/`, `%` are hardcoded in:
- `Bidir.fs`: `Add`, `Subtract`, `Multiply`, `Divide`, `Modulo` branches in `synth`
- `Eval.fs`: same 5 branches in `eval`
- `Ast.fs`: 5 dedicated DU cases

Migrating to a `Num` typeclass means these operators dispatch through instance resolution
like `show` and `eq` do.

### Cross-Language Survey

**Haskell:** `Num` has `(+)`, `(-)`, `(*)`, `negate`, `abs`, `signum`, `fromInteger`.
  Instances for `Int`, `Integer`, `Double`, `Float`. `(/)` is in `Fractional`, a subclass
  of `Num`. Arithmetic expressions compile to `Num.+`, `Num.*` etc. via dictionary passing.

**OCaml:** No numeric typeclass. `+` and `+.` are separate for int and float respectively.
  Polymorphic arithmetic requires functors or modular implicits (experimental).

**F# (source language for FunLang syntax):** No numeric typeclass either. `+` is
  operator-overloading via `inline` and static constraints (`^a`). Not typeclass-based.

**Rust:** No typeclass-style Num. `std::ops::Add` is a trait implemented per type.
  `+` desugars to `Add::add(a, b)`. The trait bound system is similar to typeclasses.

**The GHC lesson:** Migrating `+` to `Num` in Haskell works because GHC generates code
that passes dictionaries explicitly. For an interpreter like FunLang, the equivalent is
type-aware dispatch at the call site in Eval.fs.

### Gradual vs Big-Bang Migration

**Big-bang (all at once):**
- Remove `Add`/`Subtract`/`Multiply`/`Divide`/`Modulo` from `Ast.fs`
- Add `Num` typeclass to `Prelude/Typeclass.fun`
- Parser desugars `a + b` to `App(App(Var "add", a), b)` during parsing
- Bidir.fs treats `+` as any other infix operator; constraint `Num 'a` is emitted
- Eval.fs evaluates `add a b` by looking up the `add` binding

**Problems with big-bang:**
- 168 flt tests exercise arithmetic. They would break simultaneously.
- `Add` is used in `DerivingDecl` Show elaboration: `Ast.Add(Ast.String(...), ...)` is
  constructed directly in TypeCheck.fs (line 1196). This would break the deriving Show
  mechanism.
- `string concatenation via +` is also an `Add` node in the AST (Bidir handles `Add`
  for both `TInt` and `TString`). A `Num` typeclass for `+` cannot cover string concat.

**Gradual (recommended):**
- Keep `Add`/`Subtract`/`Multiply` AST nodes and their Bidir/Eval handling
- Add a `Num` typeclass to the Prelude that defines `add`, `sub`, `mul`, `div_`
  as distinct names (not operator symbols)
- Wire the `+` operator to emit a `Num 'a` constraint during type checking (by changing
  the `Add` branch in Bidir to add a pending constraint) while keeping the Eval branch
  for backward compatibility
- Users can use `add x y` for generic numeric operations; `x + y` remains int-only

**Verdict: Do not remove `Add`/`Subtract` AST nodes in this milestone.** The test
regression risk is too high (168 tests) and the string-concat ambiguity is unresolved.
Instead, add the `Num` typeclass as a Prelude declaration with `add`/`sub`/`mul` function
names, and track the constraint-emitting wire-up as a separate task.

The `+` operator can be annotated as requiring `Num` in the type checker without changing
Eval semantics: the type checker emits `Num 'a` when it sees `Add(e1, e2)` and the
operands are polymorphic. For concrete int or string, it still resolves to `TInt`/`TString`
via the existing logic.

---

## Feature 4: Eq Type Class Migration

### Current State

`Equal`/`NotEqual` in `Bidir.fs` (lines 574–579):
```fsharp
| Equal (e1, e2, span) | NotEqual (e1, e2, span) ->
    let s1, t1 = synth ...
    let s2, t2 = synth ...
    let s3 = unifyWithContext ctx [] span (apply s2 t1) t2
    recordTy span TBool
    (compose s3 (compose s2 s1), TBool)
```

This does NOT emit a `Eq` constraint. Structural equality works on any type including
functions (where it would be nonsensical). The Prelude already has `typeclass Eq 'a` and
instances for int, bool, string, char — but `=` does not dispatch through `Eq.eq`.

### The Needed Change

To make `=` require `Eq 'a`:

1. In `Bidir.fs`, the `Equal`/`NotEqual` branch should emit `pendingConstraints` with
   `{ ClassName = "Eq"; TypeArg = t1; SourceSpan = span }` after unifying the two operands.

2. This will break existing tests that use `=` on function types or ADTs without an `Eq`
   instance. Currently `let result = eq (fun x -> x) (fun x -> x)` correctly fails with
   E0701 (tested in `typeclass-builtin-eq-error.flt`), but `(fun x -> x) = (fun x -> x)`
   does NOT currently fail — it returns false via structural equality.

3. Migrating `=` to require `Eq` means adding `Eq` constraints in Bidir and adding `Eq`
   instances for all ADTs and list types that currently work with `=` in existing tests.
   Checking how many tests use `=` on non-primitive types:

The 79 typeclass-related tests are fine. The risk is in the 645 other tests where `=` is
used structurally (e.g., `match (a, b) with | (Some x, Some y) -> x = y`). If `x` and `y`
are `int`, the `Eq int` instance exists, so this is fine. If they are polymorphic or ADT
types, a constraint would be required.

**Gradual approach for Eq migration:**
- Add the constraint emission to `Equal`/`NotEqual` in Bidir
- But make constraint resolution for `Eq` fall back to structural equality when no instance
  is found: instead of raising NoInstance, emit a warning and allow the comparison
- OR: Add `deriving Eq` to all ADT types in the Prelude that need equality

**Recommended scope for this milestone:** Do not migrate `=` to strict `Eq` constraint.
Instead, add an `Eq ('a list)` constrained instance to the Prelude, and ensure that the
existing `Eq` typeclass (with its `eq` function) is more prominently documented.

---

## Feature 5: Improved Error Messages

### Current NoInstance Error (E0701)

The current message:
```
error[E0701]: No instance of Show for int list
   = hint: Add an instance declaration for this type (Available instances: Show int, Show bool, Show string, Show char)
```

This does not:
- Show which expression triggered the constraint
- Explain the constraint chain (why `Show ('a list)` was needed)
- Show superclass requirements

### Improvements Needed

**In Diagnostic.fs, `NoInstance` case:**

The `err.Scope` field already carries available instance names. Improvements:

1. Format the available instances in a table, not a comma-separated list.
2. Add a "constraint chain" note: if the constraint was triggered through a function call,
   show the call site span and the function's declared constraint.
3. For parameterized types (e.g., `No instance of Eq for 'a list`), suggest that the user
   needs `instance Eq 'a => Eq ('a list)`.

**In Bidir.fs, `resolveConstraint` failure reporting:**

Currently the error is raised with:
```fsharp
raise (TypeException {
    Kind = NoInstance(c.ClassName, c.TypeArg)
    Span = c.SourceSpan
    ...
    Scope = availableInstances
})
```

The `availableInstances` format is `"Show int"`, `"Show bool"` etc. Adding the constraint
chain requires threading the "why was this constraint generated" information. The
`SourceSpan` already tracks the call site. A `Constraint.TraceReason: string option` field
could carry "required by function `printAll` which has constraint `Show 'a`".

**Scope for this milestone:** Implement the suggestion for constrained instances. When
`NoInstance(className, TList t)` or `NoInstance(className, TData(n, args))` is raised,
check if a constrained instance for the type constructor exists (ignoring the type
argument), and if so, suggest that the user's type argument may be missing an instance.

Example: `No instance of Show for Tree` could hint "Did you mean `deriving Show` for Tree,
or add `instance Show Tree`?"

---

## Architecture Summary: Files Changed Per Feature

| File | Feature 1 | Feature 2 | Feature 3 | Feature 4 | Feature 5 |
|------|-----------|-----------|-----------|-----------|-----------|
| `Type.fs` | — | Add `Superclasses` to `ClassInfo` | — | — | Optional `TraceReason` on `Constraint` |
| `TypeCheck.fs` | — | Populate `Superclasses` in ClassInfo | — | — | — |
| `Bidir.fs` | — | Extend `resolveConstraint` with superclass step | Optional: emit `Num 'a` on `Add` | Optional: emit `Eq 'a` on `Equal` | — |
| `Elaborate.fs` | Instance name mangling (major change) | — | — | — | — |
| `Eval.fs` | Dispatch to mangled names | — | No change (keep hardcoded) | No change | — |
| `Diagnostic.fs` | — | — | — | — | Better NoInstance formatting |
| `Prelude/Typeclass.fun` | Add `show__Show_list`, `eq__Show_list` (if mangling) | — | Add `typeclass Num 'a` | Add `Eq ('a list)` instance | — |
| `Ast.fs` | No change | No change | No change | No change | No change |
| `Parser.fsy` | No change | No change | No change | No change | No change |
| `ExportApi.fs` | No change | Add Superclasses to ClassInfo export | No change | No change | No change |

---

## What NOT to Change

**`Ast.fs` — do not add or remove any DU cases.** All 5 arithmetic operator nodes
(`Add`, `Subtract`, `Multiply`, `Divide`, `Modulo`) and both equality nodes (`Equal`,
`NotEqual`) stay. Their removal is out of scope and would break 168+ tests.

**`Parser.fsy` / `Lexer.fsl` — no changes.** All new features are pure runtime/type
system changes. The grammar already supports the constrained instance syntax and superclass
syntax (`typeclass Eq 'a => Ord 'a`). The parser already produces the correct AST nodes.

**`Eval.fs` arithmetic/equality branches — keep as-is for now.** The hardcoded dispatch
for `+`, `-`, `*`, `=` stays. Adding `Num` means adding an `add` function to the Prelude,
not rewiring the built-in `+` operator.

**`Bidir.generalize` — do not change the drain-and-resolve algorithm.** The constraint
resolution logic is correct. The only change is adding the superclass entailment step to
`resolveConstraint` inside that function.

**`Elaborate.elaborateTypeclasses` structure — the outer list-collect pattern stays.**
The change is to the inner name generation: instead of always using the literal method
name, generate a mangled name when the instance is constrained or when multiple instances
of the same class exist for the same method name.

---

## Risk Assessment

| Change | Risk to Existing 724 Tests | Mitigation |
|--------|---------------------------|------------|
| Add `Superclasses` to `ClassInfo` | LOW — additive field, all construction sites just add `Superclasses = []` for existing classes | Verify ExportApi.fs construction |
| Extend `resolveConstraint` for superclass | LOW — only affects constraint resolution for classes with declared superclasses; no existing class uses this path | Only `Ord extends Eq` test exercises it |
| Instance name mangling in Elaborate | HIGH — changes how all instance methods are named in the eval environment | Must preserve backward compat: monomorphic instances keep their literal names |
| `Num` typeclass addition to Prelude | NONE — additive only, no existing code uses `add`/`sub` names | Can conflict if prelude already defines `add` elsewhere; check first |
| `Eq ('a list)` instance in Prelude | MEDIUM — introduces a new constraint that may fail for previously-passing list equality tests | Test with `eq [1;2] [1;2]` before and after |
| Better NoInstance error messages | NONE — display change only, no type system behavior change | — |

The **instance name mangling** change carries the most risk. The recommended approach is:

1. Only mangle instances where `InstanceVars` is non-empty (i.e., constrained/polymorphic instances).
2. Monomorphic instances (`Show int`, `Show bool`) keep their literal method names.
3. Polymorphic instances (`Show ('a list)`) get mangled names.
4. At constrained call sites, the type-aware elaboration pass selects the mangled name.

This preserves backward compatibility for all 724 existing tests, which use only
monomorphic instances.

---

## Cross-Language Survey: Constrained Instance Resolution

### Haskell (GHC)

GHC resolves constrained instances through the `TcInteract` constraint solver (historically,
the Wadler-Blott algorithm). The key invariant: each instance head must be unifiable with
the constraint goal, and all instance sub-constraints must be recursively satisfiable.
GHC enforces the Paterson conditions to guarantee termination (no infinite instance chains).

FunLang already has the 20-depth guard (`if depth > 20 then false`), which is the practical
equivalent of Paterson termination.

### OCaml Modules / Modular Implicits

OCaml does not have typeclasses; it uses explicit module functors for parameterized
implementations. Modular implicits (experimental) would add implicit argument passing
similar to typeclass dictionaries. Not applicable here.

### Rust Traits

Rust trait bounds are structurally similar to Haskell typeclasses. Constrained
implementations (`impl<T: Fmt> Display for Vec<T>`) resolve exactly as GHC does: the
trait bound `Fmt` on `T` becomes a subgoal that must be satisfied by the concrete type.
The Rust compiler (via chalk) uses the same algorithm FunLang approximates in `resolveConstraint`.

### Scala 3 Given/Using

Scala 3's given instances with using clauses are typeclass-equivalent. Constrained
instances work identically to Haskell. The instance selection uses a deterministic
"most specific" rule when multiple instances could match, which FunLang avoids by
requiring non-overlapping instances (the `DuplicateInstance` error).

---

## Sources

- FunLang codebase: `Type.fs`, `Bidir.fs`, `TypeCheck.fs`, `Elaborate.fs`, `Eval.fs`,
  `Diagnostic.fs`, `Prelude/Typeclass.fun` (inspected directly, 2026-04-08)
- FunLang flt tests: `tests/flt/file/typeclass/` (29 tests, inspected 2026-04-08)
- Wadler, P. and Blott, S. (1989): "How to make ad-hoc polymorphism less ad hoc"
  (the foundational typeclass paper — constraint resolution algorithm)
- GHC Developers Guide: Constraint Solving (https://ghc.gitlab.haskell.org/ghc/doc/users_guide/)
- Rust Reference: Trait implementations (https://doc.rust-lang.org/reference/items/implementations.html)
- "Implementing Type Classes" by John Peterson & Mark Jones (1993) — dictionary passing
