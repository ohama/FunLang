# Feature Landscape: v10.0 Type Classes

**Domain:** ML-style interpreter — type class / ad-hoc polymorphism system
**Researched:** 2026-03-31
**Milestone focus:** Haskell-style type classes to replace hardcoded `to_string`, polymorphic comparison, and enable user-defined ad-hoc polymorphism

---

## Context: What Already Exists

Before categorizing features, the baseline state of the relevant subsystems:

| Aspect | Current State | Problem |
|--------|--------------|---------|
| `to_string` | Hardcoded builtin — dispatch in F# on `Value` DU | Cannot be overridden per type; custom types get structural dump |
| `=` / `<>` equality | Hardcoded `valuesEqual` in Eval.fs — structural comparison on all `Value` types | No way to define custom equality for a type |
| `<` / `>` / `<=` / `>=` | Hardcoded to `TInt`, `TString`, `TChar` in Bidir.fs | Non-numeric user types cannot be ordered |
| Parametric polymorphism | Full HM inference (Infer.fs + Bidir.fs + Unify.fs) | Works great — type classes must coexist with it |
| Type system | `Type.fs` has `TInt`, `TBool`, `TString`, `TChar`, `TVar`, `TArrow`, `TTuple`, `TList`, `TArray`, `THashtable`, `TData`, `TExn` | No constraint representation yet |
| Modules | F#-style module system, `open "file.fun"` imports | Instances could live in modules — no global scope issue |

The motivation for type classes is concrete and bounded:
1. `to_string` should dispatch to a user-defined `Show` instance
2. Comparison operators should require an `Ord` (or `Eq`) constraint
3. Users writing custom types need a way to make them printable, comparable, hashable

---

## Design Space: Haskell vs Rust Traits vs F# SRTP

The three dominant models for ad-hoc polymorphism:

| Model | Instance Resolution | HM Compatibility | Interpreter Complexity |
|-------|---------------------|-----------------|------------------------|
| **Haskell type classes** | Implicit, dictionary-passing, globally coherent | Seamless — constraint variables in type schemes | High — global instance database, coherence, overlapping instances |
| **Rust traits** | Explicit (static dispatch) or dynamic (`dyn Trait`) | No HM — Rust uses bidirectional checking with explicit types | Medium — no implicit passing, but no HM integration |
| **F# SRTP** (Statically Resolved Type Parameters) | Compile-time specialization, monomorphization | Poor fit for dynamic interpreter | Very High — requires full type specialization |
| **Simplified dictionary passing** | Implicit, but no global coherence enforcement at first | HM-compatible with type constraint annotation | Medium — the right fit for an interpreter |

**Recommendation: Haskell-style dictionary passing, simplified.**

Dictionary passing means: each type class constraint `C 'a` becomes an implicit argument `dict_C` (a record of methods) at elaboration time. The interpreter passes dictionaries at call sites. This is the proven approach and is how GHC works internally.

The key simplification vs full Haskell: start without orphan instance detection, without overlapping instances, without multi-parameter type classes, and without superclass hierarchies. These can be added incrementally.

---

## Table Stakes

**Must have for type classes to be useful at all.** Without these, the feature is incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `typeclass` declaration syntax | Define what methods a type class requires | Medium | `typeclass Show 'a = { show : 'a -> string }` — introduces class name, type var, method signatures |
| `instance` declaration syntax | Bind a concrete type to a type class | Medium | `instance Show int = { let show x = to_string x }` |
| Constraint inference in type schemes | `show` function must carry `Show 'a =>` constraint | High | Requires extending `Scheme` to include constraints; touches Infer.fs, Bidir.fs, Unify.fs |
| Dictionary passing at call sites | Pass the right method dictionary when calling a constrained function | High | Elaboration step: replace `show x` with `dict.show x`; central to the approach |
| `Show` typeclass replacing `to_string` | Primary motivation — printable user types | Medium | `show : 'a -> string` where `Show 'a`; existing `to_string` becomes the default `Show int/bool/string/char` instance |
| `Eq` typeclass | Equality constraint — `=` and `<>` should require `Eq 'a` | Medium | Replaces hardcoded polymorphic equality; `eq : 'a -> 'a -> bool` |
| `Ord` typeclass with `Eq` superclass | Ordering constraint — `<`, `>`, `<=`, `>=` require `Ord 'a` | Medium | `Ord 'a` implies `Eq 'a`; comparison operators become typeclass-dispatched |
| Constrained instance declarations | `instance Show (Option 'a) where Show 'a` | High | Instance with constraints on type parameters — required for generic containers |
| Built-in instances for primitive types | `Show int`, `Show bool`, `Show string`, `Show char`, `Eq int`, `Ord int`, etc. | Low | These wire up existing builtins; defines the "default behavior" baseline |
| Type error when constraint not satisfied | `show x` without `Show` instance gives a compile error | Medium | Constraint resolution failure must produce clear diagnostic |

---

## Differentiators

**Features that set this implementation apart — useful but not strictly required for basic type class functionality.**

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `Num` typeclass | `+`, `-`, `*` become typeclass-dispatched — enables user-defined numeric types | High | Requires changing arithmetic operators to carry `Num 'a` constraint; risky — may break existing integer arithmetic |
| Default method implementations | `instance Show (Option 'a)` can omit methods that have sensible defaults | Medium | Class declaration can provide default bodies; instance overrides only what it needs |
| `derive Show` / `derive Eq` syntax | Automatic instance generation for ADTs | High | Eliminates boilerplate; structurally walks ADT and generates show/eq method — very ergonomic |
| `Hashable` typeclass | Makes user types usable as hashtable keys | Medium | Requires defining hash function per type; non-trivial to get right |
| `Functor` / `Foldable` typeclasses | Enables `map`, `foldl` on user-defined containers | Very High | Requires Higher-Kinded Types (`'f 'a`) — a different feature entirely; not in scope |
| Pretty-print vs debug distinction | Separate `Show` (human-readable) and `Debug` (structural) typeclasses | Low | Mirrors Rust `Display` vs `Debug`; nice-to-have |
| Named typeclass constraints in annotations | `let f (x : Show 'a => 'a) : string = show x` — explicit user-facing annotations | Medium | Useful for documentation and error messages |

---

## Anti-Features

**Things to deliberately NOT build in v10.0.** Common mistakes in first-generation type class systems.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Overlapping instances** | Destroys coherence — same type can resolve to different dictionaries in different contexts; produces subtle non-deterministic bugs | Require exactly one instance per (class, type) pair; error on duplicates |
| **Orphan instances** (enforcement) | Enforcing "instance must be defined in same module as type or class" is premature — adds complexity without immediate benefit | Allow orphan instances freely for now; add warning later |
| **Multi-parameter type classes** (`typeclass Conv 'a 'b`) | Requires functional dependencies or type families to be coherent; very complex | Single type parameter per class only |
| **Higher-Kinded Types in type classes** (`Functor 'f` where `'f : * -> *`) | Requires HKT extension to HM; `TVar` would need to track kinds | Defer to future milestone; `Functor`/`Monad` not in v10.0 |
| **`Num` typeclass replacing `+`, `-`, `*`** | Would require changing arithmetic expressions in Bidir.fs and Eval.fs; risks regression on all integer/float arithmetic; large blast radius | Keep `+`/`-`/`*` as-is for `int`; only add `Num` if there is a concrete user need |
| **Automatic `deriving`** in v10.0 | Significant complexity (structural walk of ADT, code generation); nice-to-have but not needed for core type classes | Manual instances first; derive can come in v10.1 or later |
| **`instance Show []` global list instance** | In Haskell, `[a]` has a single `Show` instance that recursively shows elements — this requires recursive constraint resolution working perfectly | Build the basic case first; container Show instances come after core machinery works |
| **Type defaulting (integer/fractional defaults)** | Haskell's infamous defaulting rules for ambiguous numeric types; complex and surprising behavior | Keep `TVar` ambiguity defaulting simple; do not try to mimic Haskell's `default (Integer, Double)` |
| **Incoherent instances** | GHC extension that allows multiple matching instances with arbitrary selection; completely undermines typeclass semantics | Never implement |

---

## Feature Dependencies

The dependency graph for v10.0 features:

```
1. Typeclass declaration parsing (AST + Parser)
   └─► 2. Instance declaration parsing (AST + Parser)
           └─► 3. TypeClass environment (Type.fs: ClassEnv, InstanceEnv)
                   └─► 4. Constraint representation in Type.fs (TConstraint / ClassConstraint)
                           └─► 5. Scheme extension: Scheme with constraints
                                   └─► 6. Constraint inference in Bidir.fs
                                           └─► 7. Dictionary building in elaboration
                                                   └─► 8. Dictionary passing in Eval.fs
                                                           └─► 9. Built-in instances (Show int, Eq int, etc.)
                                                                   └─► 10. Show replaces to_string
                                                                           └─► 11. Eq/Ord constrained operators

Superclass constraint (e.g., Ord requires Eq):
    4 → SupClass resolution → 7 (when building Ord dict, include Eq dict)
```

**Critical path:** Steps 1–8 are the core pipeline. Without all of them, nothing works. Steps 9–11 are the payoff that validates the machinery.

**Independent work:**
- `Show` instances for primitive types: can be done as soon as step 8 is working
- `Eq` / `Ord` operator refactoring: depends on `Eq`/`Ord` instances existing (step 9+)
- Constrained `Option 'a` instances: depends on constraint inference (step 6+)

---

## MVP Recommendation

### MVP Scope (v10.0)

The minimal set that delivers real value and validates the architecture:

**Phase A — Core machinery:**
1. `typeclass` / `instance` syntax in parser and AST
2. `ClassEnv` and `InstanceEnv` in the type checker
3. Constraint representation in `Type.fs` and `Scheme`
4. Constraint inference in `Bidir.fs` (pass constraint from usage to caller)
5. Dictionary building and passing in elaboration + `Eval.fs`

**Phase B — Payoff features:**
6. Built-in `Show` instances for all primitive types (`int`, `bool`, `string`, `char`, `int list`, `bool list`)
7. `to_string` becomes an alias or delegates to `Show.show`
8. Built-in `Eq` instances for primitive types; `=` / `<>` become `Eq`-constrained
9. Built-in `Ord` instances (superclass of `Eq`); `<` / `>` / `<=` / `>=` become `Ord`-constrained
10. Constrained instances: `instance Show (Option 'a) where Show 'a`

### Post-MVP (Defer to v10.1+)

| Feature | Reason to Defer |
|---------|-----------------|
| `derive Show` / `derive Eq` | Reduces boilerplate but not needed to validate core system |
| `Num` typeclass | High blast radius; no immediate user demand |
| `Hashable` typeclass | Non-trivial; depends on MVP working first |
| `Functor` / `Monad` | Requires HKTs — separate milestone |
| Default method implementations | Nice ergonomic improvement after core is working |
| Error messages with constraint context | Improve after MVP; "no instance for Show at ..." messages |

---

## Complexity Assessment

| Feature | Complexity | Primary Challenge | Files Affected |
|---------|------------|-------------------|----------------|
| Typeclass/instance syntax | Low | Parser rules; AST nodes for ClassDecl/InstanceDecl | Lexer.fsl, Parser.fsy, Ast.fs |
| ClassEnv / InstanceEnv | Low | Data structures only; Map lookups | Type.fs, TypeCheck.fs |
| Constraint in Type.fs / Scheme | Medium | Scheme extension touches all call sites of `generalize`/`instantiate` | Type.fs, Infer.fs, Bidir.fs |
| Constraint inference in Bidir.fs | High | Must propagate constraints through all synth/check cases; unification must handle constraint variables | Bidir.fs, Unify.fs |
| Dictionary building | High | Elaboration pass that resolves instances to concrete dictionaries at call sites | New Elaborate pass or Bidir.fs extension |
| Dictionary passing in Eval.fs | Medium | Dictionary is a `Value` (record or closure map); method dispatch is a field lookup | Eval.fs |
| Built-in instances | Low | Wire up existing builtins as instance bodies | New Prelude entries or Eval.fs init |
| Constrained container instances | Medium | `Show (Option 'a) where Show 'a` — recursive constraint passing | Bidir.fs + dictionary building |
| Replace `to_string` with `Show` | Low | After Show works, alias or redirect | Eval.fs (remove builtin), Prelude |
| `Eq`/`Ord` operator refactoring | Medium | Bidir.fs comparison operator cases must carry/check constraints | Bidir.fs, Eval.fs |

**Overall milestone complexity:** HIGH. The core machinery (constraint inference + dictionary passing) is the hard part and requires deep changes to Bidir.fs. The payoff features (Show, Eq, Ord instances) are straightforward once the plumbing exists.

---

## Interaction With Existing Features

| Existing Feature | Interaction | Risk |
|-----------------|-------------|------|
| HM type inference (Bidir.fs) | Constraints must be inferred and propagated alongside types — constraint variables live alongside type variables | HIGH — central integration point; every `synth`/`check` call may need to propagate constraints |
| `to_string` builtin | Will be replaced or delegated to `Show` instance | LOW — additive; can keep `to_string` as alias initially |
| `=` / `<>` operators | Move from untyped structural equality to `Eq`-constrained | MEDIUM — must not break existing code where `=` is used on `int` without explicit `Eq` annotation |
| `<` / `>` operators | Move from `TInt/TString/TChar` hardcode to `Ord`-constrained | MEDIUM — same; must preserve backward compat for `int` comparisons |
| Pattern matching | `match` doesn't inherently need type classes, but `=` comparisons in `when` guards may need `Eq` | LOW |
| Records | Record equality was structural; with `Eq` typeclass, record types would need an explicit `Eq` instance | MEDIUM — must decide: derive automatically or require explicit instance |
| ADTs | `DataValue` equality is structural in `valuesEqual`; after type classes, user ADTs need `Eq` instance | MEDIUM — migration strategy needed |
| Module system | Instances could be scoped to modules or global; for MVP, instances are global (like Haskell) | LOW — global instances avoid module complexity initially |
| `printf`/`sprintf` with `%a` or custom formatters | Currently `%s` requires string; after `Show`, `%s` might take any `Show 'a` | OUT OF SCOPE for v10.0 |

---

## Expected Syntax (LangThree Style)

Based on the existing F#-influenced syntax:

```fsharp
// Class declaration
typeclass Show 'a =
    show : 'a -> string

// Instance for built-in type
instance Show int =
    let show x = to_string x

// Instance for ADT (no constraints)
type Color = Red | Green | Blue

instance Show Color =
    let show c = match c with
        | Red -> "Red"
        | Green -> "Green"
        | Blue -> "Blue"

// Constrained instance (container type)
instance Show (Option 'a) where Show 'a =
    let show opt = match opt with
        | None -> "None"
        | Some x -> "Some(" ^^ show x ^^ ")"

// Using the typeclass (constraint inferred)
let printAll xs =
    List.iter (fun x -> println (show x)) xs

// Explicit constraint in annotation (optional)
let describe (x : Show 'a => 'a) : string =
    "Value: " ^^ show x

// Eq typeclass
typeclass Eq 'a =
    eq : 'a -> 'a -> bool
    neq : 'a -> 'a -> bool

// Ord with superclass
typeclass Ord 'a where Eq 'a =
    lt : 'a -> 'a -> bool
    gt : 'a -> 'a -> bool
    lte : 'a -> 'a -> bool
    gte : 'a -> 'a -> bool
```

**Notes on syntax:**
- `typeclass Name 'a = { method signatures }` — indentation-based, F# style
- `instance Name Type = { let method = ... }` — mirrors module declaration style
- `where ConstraintList` for superclass constraints — after the type
- Constraint annotation in `TypeExpr`: `Show 'a => 'a` — standard Haskell-inspired notation
- `=` / `<>` / `<` / `>` operators remain as operators; they dispatch via `Eq`/`Ord` dictionaries internally

---

## Sources

- [Implementing, and Understanding Type Classes — okmij.org](https://okmij.org/ftp/Computation/typeclass.html)
- [Making dictionary passing explicit in Haskell — Joachim Breitner](https://www.joachim-breitner.de/blog/398-Making_dictionary_passing_explicit_in_Haskell)
- [Introduction to Haskell Typeclasses — Serokell](https://serokell.io/blog/haskell-typeclasses)
- [Type class — Wikipedia](https://en.wikipedia.org/wiki/Type_class)
- [GHC Instance Declarations and Resolution](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/instances.html)
- [Orphan instance — HaskellWiki](https://wiki.haskell.org/Orphan_instance)
- [The trouble with typeclasses — Paul Chiusano](https://pchiusano.github.io/2018-02-13/typeclasses.html)
- [Coherence of type class resolution — ACM](https://dl.acm.org/doi/10.1145/3341695)
- [Hindley-Milner type system — Wikipedia](https://en.wikipedia.org/wiki/Hindley%E2%80%93Milner_type_system)

---

**Document Status:** Research complete for v10.0 milestone
**Confidence Level:** HIGH for feature categorization and MVP scope; MEDIUM for implementation complexity estimates (constraint inference integration depth is uncertain until Bidir.fs is studied in detail)
**Next Step:** Use this feature catalog to define requirements and roadmap phases for v10.0
