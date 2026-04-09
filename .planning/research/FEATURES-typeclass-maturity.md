# Feature Landscape: Type Class Maturity (v14.0 Milestone)

**Domain:** Advanced type class features for ML-style interpreter
**Researched:** 2026-04-08
**Milestone focus:** Constrained instances, superclass constraints, Num type class, Eq operator migration, derive improvements, better type error messages

---

## Existing Foundation (Already Built — Do Not Rebuild)

Before categorizing new features, the baseline is important:

| Already working | How it works in FunLang |
|-----------------|-------------------------|
| `typeclass Show 'a = ...` declarations | ClassInfo stored in ClassEnv, method Schemes built with class constraint |
| `instance Show int = ...` | InstanceInfo in InstanceEnv; method bodies type-checked against expected type |
| `instance Show 'a => Show ('a list)` | InstanceConstraints carried in InstanceInfo; Bidir.generalize resolves subgoals |
| `typeclass Eq 'a => Ord 'a` superclass syntax | Superclass constraints added to method Schemes at class declaration time |
| Built-in Show/Eq instances for int, bool, string, char | Registered in initialTypeEnv / initialInstEnv |
| `deriving Show` / `deriving Eq` for nullary ADTs | Code-generates match expressions in TypeCheck.fs |
| `TEConstrained` annotation parsing | Parsed, elaborated, emits constraints via pendingConstraints |
| Dictionary elaboration / constraint resolution in Bidir.generalize | Depth-limited recursive resolution against InstanceEnv |

**Key implication:** The type-level infrastructure (ClassEnv, InstanceEnv, pendingConstraints, resolveConstraint) is in place. The features below either extend it or expose it more visibly to users.

---

## Table Stakes

Features users expect from a language that advertises type class support. Missing any of these makes the system feel incomplete or unreliable.

### 1. Constrained Instance Constraint Propagation

**What it is:** When a function uses a method from a constrained instance (e.g., `show` inside `Show 'a => Show ('a list)`), the constraint `Show 'a` should propagate to the caller's type signature automatically.

**User-visible behavior:**
```
// User writes:
let showAll xs = show xs   // xs : 'a list

// System should infer:
// showAll : Show 'a => 'a list -> string
// NOT: showAll : 'a list -> string (which would fail at use site)
```

**What already works:** The `InstanceConstraints` field is carried in InstanceInfo and is checked during `resolveConstraint`. The propagation into caller type schemes via `pendingConstraints` is in place.

**What may be incomplete:** When `resolveConstraint` can match a constrained instance but the element constraint (`Show 'a`) is still polymorphic, it marks it as "assume satisfiable." This is correct at definition time, but the constraint must appear in the final scheme. Verify: does `let f xs = show xs` generalize to `Show 'a => 'a list -> string`?

**Complexity:** Low — mostly verification + test coverage. The mechanism exists.

**Dependencies:** Existing `InstanceConstraints`, `pendingConstraints`, `generalize`.

---

### 2. Superclass Constraint Auto-Derivation

**What it is:** If a type has an `Ord` instance and `Ord` requires `Eq`, the system should automatically provide an `Eq` constraint anywhere an `Eq` constraint is needed, given an `Ord` constraint.

**User-visible behavior:**
```
typeclass Eq 'a => Ord 'a =
    | compare : 'a -> 'a -> int

instance Ord int = ...   // must imply Eq int is satisfied

// Later: eq x y   where x : 'a with Ord 'a constraint
// Should work without explicit Eq 'a annotation
```

**Standard ML-family behavior:** In Haskell, superclass constraints are automatically derived — `class (Eq a) => Ord a` means any `Ord` constraint implies `Eq` is also available. Users never need to write `(Eq a, Ord a)` redundantly; `Ord a` alone suffices.

**What currently works:** Superclasses are parsed (`superclasses: string list` in TypeClassDecl, AST) and stored. Method schemes on the Ord class carry superclass constraints. But: when resolving an `Eq` constraint, does the resolver consider that an available `Ord` instance satisfies it?

**What is likely missing:** The `resolveConstraint` in Bidir only looks at direct InstanceInfo for the requested class. It does not walk the superclass chain (if `Ord int` exists and `Eq` is a superclass of `Ord`, resolving `Eq int` should succeed). This requires adding superclass-aware resolution.

**User-visible failure without this:** Writing `let min x y = if compare x y <= 0 then x else y` with only an `Ord` constraint would fail with `No instance of Eq` when `compare` result is used with `<=` (if `<=` goes through `Ord`).

**Complexity:** Medium. Requires superclass chain traversal during constraint resolution. The ClassEnv already has superclass information (via the `allConstraints` in method schemes), but `resolveConstraint` needs a separate lookup of the class hierarchy.

**Dependencies:** ClassEnv (already has superclass info), resolveConstraint in Bidir.fs.

---

### 3. Num Type Class (Arithmetic via Type Class)

**What it is:** A `Num` type class that allows user-defined numeric types to work with `+`, `-`, `*`.

**Standard behavior (Haskell Num):**
```haskell
class Num a where
    (+), (-), (*) :: a -> a -> a
    negate :: a -> a
    abs    :: a -> a
    fromInteger :: Integer -> a   -- literal coercion
```

**User-visible behavior in FunLang context:**
- Users can write `instance Num MyVec = let (+) a b = ...` and then `v1 + v2` works.
- Numeric literals like `1` become polymorphic (`Num 'a => 'a`) rather than always `int`.

**Critical decision point — literal polymorphism:** In Haskell, numeric literals dispatch through `fromInteger`, making `1 :: Double` valid. This is powerful but complex. For FunLang, the question is: should integer literals remain `int`, or become `Num 'a => 'a`?

**Recommendation for FunLang:** Keep numeric literals as concrete `int` initially. Expose `Num` as a class with named method functions (`plus`, `minus`, `times`) that user-defined types can implement. Migrating `+`/`-`/`*` operators themselves to dispatch through Num is a larger undertaking (see Anti-Features section).

**Complexity for named-method Num:** Low-Medium. Define the class in Prelude, add `int` instance, add built-in resolution.

**Complexity for operator-dispatch Num:** High. Requires AST `Add`/`Subtract`/`Multiply` nodes to emit `Num` constraints instead of hardcoding `TInt`. Every existing test that uses arithmetic is affected. Backward compatibility risk is high.

**Dependencies:** Existing typeclass infrastructure. Operator migration path (if pursued) depends on FixityEnv system.

---

### 4. Eq Migration (= Operator via Type Class)

**What it is:** The `=` equality operator currently type-checks structurally — it unifies the two operand types without requiring an Eq instance. Migrating it to dispatch through the Eq type class means `x = y` requires `Eq 'a` for the type of `x` and `y`.

**Standard behavior:** In Haskell, `(==)` is a method of `Eq`. Writing `x == y` for a type without an `Eq` instance is a type error. This is enforced at compile time, not runtime.

**Current FunLang behavior:**
```
| Equal (e1, e2, span) | NotEqual (e1, e2, span) ->
    // Unifies e1 and e2 types — no Eq constraint emitted
    (compose s3 (compose s2 s1), TBool)
```

**User-visible change after migration:**
- `x = y` on a function type would give `No instance of Eq for 'a -> 'b` rather than silently comparing by reference.
- `x = y` on a user-defined ADT would require `deriving Eq` or `instance Eq MyType`.
- `x = y` on `int`, `bool`, `string`, `char` would still work (built-in instances).

**Risk:** This is a breaking change for all 724 existing flt tests that use `=`. Tests that compare functions, or types without Eq instances, would break.

**Recommendation:** Implement the constraint emission but keep built-in instances broad enough to cover all currently-tested types. Alternatively: add `Eq` constraint emission as a type-check-only feature (warning, not error) first.

**Complexity:** Medium-High. Bidir.fs Equal/NotEqual cases need to emit a pending Eq constraint. Existing built-in Eq instances for int/bool/string/char/list must be pre-registered. All 724 tests must still pass.

**Dependencies:** Built-in Eq instance registration for all primitive types and list. Constrained instance for lists (`Eq 'a => Eq ('a list)`).

---

### 5. Better Type Error Messages for Constraint Failures

**What it is:** When a constraint cannot be satisfied (E0701 NoInstance), the error message should explain the chain of reasoning: what function required the constraint, what type was being used, and what instances are available.

**Current behavior:**
```
error[E0701]: No instance of Show for Foo
```

**What users expect (based on GHC and research):**
```
error[E0701]: No instance of Show for Foo
  --> file.fun:5:12
   |
 5 | println (show myFoo)
   |          ^^^^ requires Show Foo
   |
   = help: Add 'instance Show Foo = ...' or 'deriving Show Foo'
   = available: Show int, Show bool, Show string, Show char
```

**Key improvement dimensions:**
1. Source snippet (already exists via `renderSourceSnippet`)
2. "Required by" chain — show the call site that triggered the constraint
3. "Available instances" list — already partially in Scope field of NoInstance error
4. "Did you mean deriving?" suggestion for ADTs without instances

**Research finding (OOPSLA 2023):** Type errors should be explained as "faulty data flows" — showing the sequence of locations that led to the constraint being unsatisfiable. This is more actionable than showing only the failure site.

**Complexity:** Low-Medium for incremental improvements (better message formatting). High for full data-flow explanation chains.

**Dependencies:** Existing Diagnostic infrastructure, renderSourceSnippet, Scope field in TypeError.

---

### 6. Derive for Parameterized ADTs

**What it is:** `deriving Show` for types with type parameters: `type Tree 'a = | Leaf | Node of 'a * Tree 'a * Tree 'a`.

**Current limitation:** The `DerivingDecl` handler in TypeCheck.fs generates instances with `InstanceVars = []` and `InstanceConstraints = []` — it does not handle parameterized types. Only nullary ADTs work correctly today.

**User-visible behavior after fix:**
```
type Option 'a = | None | Some of 'a
deriving Show Option

show (Some 42)    // → "Some 42"
show None         // → "None"
show (Some "hi")  // → "Some hi"
```

The generated Show instance should be `instance Show 'a => Show ('a Option)`.

**Complexity:** Medium. DerivingDecl needs to:
1. Detect type parameters from the type's ConstructorInfo.
2. Generate constrained instance (`Show 'a => Show ('a TypeName)`) instead of monomorphic instance.
3. Ensure the show body uses `show` recursively for data-carrying constructors.

**Dependencies:** ConstructorInfo (TypeParams field), InstanceConstraints support (already exists).

---

## Differentiators

Features that distinguish FunLang from a minimal type class implementation. Valued but not blocking.

### 7. Default Method Implementations

**What it is:** A type class can provide a default implementation for a method, which instances can override or inherit.

**Haskell pattern:**
```haskell
class Eq a where
    (==) :: a -> a -> Bool
    (/=) :: a -> a -> Bool
    x /= y = not (x == y)   -- default: defined in terms of ==
```

**User benefit:** Users writing `instance Eq MyType` only need to implement `eq`; `neq` comes for free.

**Complexity:** Medium. The TypeClassDecl AST needs a `defaultBody` field. The InstanceDecl checker must inject defaults for missing methods. The type checker for defaults needs access to the class's own method types.

**Dependencies:** Parser extension for `typeclass ... = let method = ...` syntax in class body. TypeClassDecl AST change.

---

### 8. Multiple-Method Type Classes with Superclass Access

**What it is:** Within a class or instance method, being able to call superclass methods by name. For example, an `Ord` instance that calls `eq` (from `Eq`) in its implementation.

**Current behavior:** Multi-method typeclasses work (`typeclass-runtime-multi-method.flt` passes). But superclass methods are accessed via global name lookup, not guaranteed to resolve to the superclass's methods.

**User-visible behavior:**
```
typeclass Eq 'a => Ord 'a =
    | compare : 'a -> 'a -> int
    | le : 'a -> 'a -> bool

instance Ord int =
    let compare x = fun y -> if x < y then -1 else if x > y then 1 else 0
    let le x = fun y -> compare x y <= 0 || eq x y   // uses superclass eq
```

**Complexity:** Low, if superclass constraint resolution already works (item 2). The `eq` name resolves from the environment where it was registered as a method.

**Dependencies:** Superclass constraint auto-derivation (item 2).

---

### 9. Ord Type Class (Comparison via Type Class)

**What it is:** An `Ord` class with `compare`, `lt`, `le`, `gt`, `ge` methods, where the comparison operators `<`, `>`, `<=`, `>=` dispatch through it.

**Standard behavior:**
```
typeclass Eq 'a => Ord 'a =
    | compare : 'a -> 'a -> int   // -1, 0, 1
```

**Current FunLang:** Comparison operators hardcode `TInt | TString | TChar` — no Ord constraint is emitted. Migrating them to dispatch through Ord is structurally identical to Eq migration (item 4) but for a different class.

**Complexity:** Medium-High (same risks as Eq migration). Add after Eq migration is stable.

**Dependencies:** Eq migration (item 4), superclass auto-derivation (item 2).

---

### 10. `deriving` with Inline Syntax (`type Foo = ... deriving Show, Eq`)

**Current syntax:** `deriving Show Foo` is a separate declaration after the type declaration.

**Standard Haskell syntax:** `data Foo = ... deriving (Show, Eq)` — inline on the type declaration.

**FunLang parser note:** `TypeDecl` already has a `deriving: string list` field! This means inline deriving is already parsed. The question is whether TypeCheck.fs handles it.

**Complexity:** Low if TypeDecl's deriving list is already wired up to the same codegen as `DerivingDecl`. Check TypeCheck.fs handling of `TypeAdt`.

**Dependencies:** None beyond existing infrastructure.

---

### 11. Functor / Foldable Type Classes (Higher-Kinded)

**What it is:** `typeclass Functor 'f = | map : ('a -> 'b) -> 'f 'a -> 'f 'b` — where `'f` is a type constructor, not a concrete type.

**Why it matters:** Enables user-defined container types to participate in `List.map`-style operations.

**Complexity:** Very High. FunLang's type system uses `Type` without kind information. Higher-kinded types require a kind system (distinguishing `* -> *` from `*`). The constraint resolution and unification machinery would need extension.

**Recommendation:** Do not attempt in this milestone. Flag as future work.

**Dependencies:** Kind system (does not exist).

---

## Anti-Features

Things that appear attractive but should deliberately NOT be built in this milestone.

### A. Operator Dispatch via Num for Built-In Arithmetic

**Why it seems attractive:** Complete the Num type class by making `+`, `-`, `*` dispatch through it, as Haskell does.

**Why to avoid now:**
- The AST nodes `Add`, `Subtract`, `Multiply`, `Divide`, `Modulo` are hardcoded in Bidir.fs with `TInt` constraints. Changing them to emit `Num` constraints would affect every arithmetic expression in all 724 tests.
- Numeric literal polymorphism (`1 :: Double`) requires `fromInteger` — a significant type-directed coercion mechanism.
- The performance cost of constraint resolution for every arithmetic operation is not negligible in an interpreter.
- This is the same scope as the operator reform milestone (v12.0) — doing both simultaneously is high risk.

**What to do instead:** Define `Num` as a named-method class (`plus`, `minus`, `times`). Users can implement it for custom types without touching built-in operators.

---

### B. Overlapping Instances

**Why it seems attractive:** Allows `instance Show 'a` as a fallback and `instance Show int` as a specific override.

**Why to avoid:**
- Coherence (one unique instance per type) is a correctness guarantee. Overlapping breaks it.
- GHC requires explicit `{-# OVERLAPPING #-}` pragmas, acknowledging this is an advanced/dangerous feature.
- FunLang already has `DuplicateInstance` as an error (E0702). Relaxing this would require new disambiguation rules.

**What to do instead:** Rely on specificity: more specific instances (concrete type) are tried before parameterized ones. The current resolver already tries all instances and returns the first match — ordering by specificity is a natural extension if needed.

---

### C. Type Class Coherence Relaxation (Orphan Instances)

**Why it seems attractive:** Allow instances to be declared in any module, not just the module that defines the class or the type.

**Why to avoid:** Coherence violations cause subtle bugs where different code paths dispatch to different instances for the same type. GHC has orphan instance warnings for a reason. FunLang's module system is simpler — keep the constraint that instances live with the type or class declaration.

---

### D. Constraint Kinds (Rank-N Constraints)

**What it is:** `class (forall a. Show a => Show (f a)) => Functor f` — constraints as first-class values.

**Why to avoid:** Requires quantified constraints, which is a GHC extension even in Haskell 2010. Well outside the scope of this milestone.

---

## Feature Dependencies Graph

```
Constrained instance propagation (1)
    ↓ required by
Derive for parameterized ADTs (6)
    ↓ enabled by
Superclass auto-derivation (2)
    ↓ required by
Ord type class (9)
    ↑ required by
Eq migration (4)
    ↑ enables
Better error messages (5)   [independent, can be done anytime]
Num class named methods (3) [independent]
Default methods (7)         [independent]
deriving inline syntax (10) [likely already wired, low effort]
Higher-kinded (11)          [blocked on kind system — do not attempt]
```

---

## MVP Recommendation for This Milestone

**Phase 1 (Table stakes, low-medium risk):**
1. Verify and test constrained instance constraint propagation (item 1) — add flt tests for caller inference
2. Superclass constraint auto-derivation in resolveConstraint (item 2) — enables everything else
3. Derive for parameterized ADTs (item 6) — high user value, clear scope
4. Better E0701/E0705/E0706 error messages (item 5) — constant user friction

**Phase 2 (Higher risk, breaking changes):**
5. Eq migration via constraint emission (item 4) — requires broad built-in instance coverage
6. Num class with named methods (item 3, named-method variant only)

**Phase 3 (Differentiators, lower priority):**
7. Default method implementations (item 7)
8. Ord type class (item 9) — after Eq migration stable
9. Inline deriving syntax (item 10) — check if already wired

**Defer indefinitely:**
- Higher-kinded type classes (item 11)
- Operator dispatch via Num for built-ins (Anti-Feature A)
- Overlapping instances (Anti-Feature B)

---

## Feature Complexity Summary

| # | Feature | Complexity | Risk | Breaks Existing Tests? |
|---|---------|------------|------|------------------------|
| 1 | Constrained instance propagation | Low | Low | No |
| 2 | Superclass auto-derivation | Medium | Low | No |
| 3 | Num class (named methods) | Low-Med | Low | No |
| 4 | Eq migration (operator) | Med-High | High | Potentially many |
| 5 | Better error messages | Low-Med | Low | No |
| 6 | Derive for parameterized ADTs | Medium | Low | No |
| 7 | Default method implementations | Medium | Low | No |
| 8 | Multi-method superclass access | Low | Low | No |
| 9 | Ord type class | Med-High | High | Potentially many |
| 10 | Inline deriving syntax | Low | Low | No |
| 11 | Higher-kinded type classes | Very High | Very High | No |

---

## Sources

- [A Gentle Introduction to Haskell: Classes](https://www.haskell.org/tutorial/classes.html) — canonical Haskell superclass / constrained instance semantics
- [GHC Instance Declarations and Resolution](https://downloads.haskell.org/ghc/latest/docs/users_guide/exts/instances.html) — formal instance resolution rules
- [Learn You a Haskell: Types and Typeclasses](https://learnyouahaskell.com/types-and-typeclasses) — user-visible Num/Eq/Ord behavior
- [GHC/SuperClass — HaskellWiki](https://wiki.haskell.org/GHC/SuperClass) — superclass constraint derivation mechanics
- [Implementing, and Understanding Type Classes — okmij.org](https://okmij.org/ftp/Computation/typeclass.html) — dictionary passing implementation patterns
- [PureScript Type Classes](https://book.purescript.org/chapter6.html) — Eq/Ord operator dispatch via typeclass in ML-family language
- [Getting into the Flow: Towards Better Type Error Messages (OOPSLA 2023)](https://dl.acm.org/doi/10.1145/3622812) — research on constraint error message quality
- [Haskell/Classes and types — Wikibooks](https://en.wikibooks.org/wiki/Haskell/Classes_and_types) — class inheritance and shorter contexts via superclasses
- [A Gentle Introduction to Haskell: Numbers](https://www.haskell.org/tutorial/numbers.html) — Num class and fromInteger for literal polymorphism
