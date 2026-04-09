# Domain Pitfalls: Type Class Maturity in FunLang (v15.0)

**Domain:** Adding constrained instances, superclasses, Num/Eq operator migration, and derive
improvements to an existing dictionary-passing type class system  
**Researched:** 2026-04-08  
**Scope:** Mistakes when extending FunLang's v10.0–v12.0 type class system (TypeCheck.fs,
Bidir.fs, Eval.fs) while preserving 724 flt tests and 244 unit tests

Each pitfall carries a **Phase** tag:
- **P1**: Constrained instances (`Show 'a => Show ('a list)`)
- **P2**: Superclass constraints (`typeclass Eq 'a => Ord 'a`)
- **P3**: Num/Eq operator migration (`+`, `-`, `*`, `=` to type class dispatch)
- **P4**: `derive` improvements (recursive types, nested ADTs)
- **P5**: Error message improvements

---

## PART A: Critical Pitfalls (Cause Rewrites or Silent Semantic Breaks)

### Pitfall TC-1: Instance Resolution Loop Returns `false` Instead of a Useful Error

**What goes wrong:** `resolveConstraint` in `Bidir.fs` (line 92) guards against infinite
recursion with `if depth > 20 then false`. When a constrained instance causes a cycle —
for example, `Show 'a => Show ('a list)` used where `'a` is itself a list — the recursion
hits depth 20 and silently returns `false`. The caller (line 115) then raises E0701 "No
instance of Show for `'a list`", pointing the user at the wrong place. The error message
shows `Available instances: Show 'a list` (which exists!) but gives no hint that the problem
is a missing base instance for the element type.

**Why it happens:** The depth guard is a correctness guard, not an error-quality guard.
`false` at depth 20 is indistinguishable from "instance does not exist." The error reporter
shows available instances by class name only (`Map.tryFind c.ClassName instEnv`), not by
the concrete failing subgoal.

**Consequences:** Users see `error[E0701]: No instance of Show for int list` when the real
problem is `Show int` missing from the instance environment. They add `Show ('a list)` again
(getting E0702 duplicate) or are confused because the instance clearly exists.

**Detection:** Write a test where the element-type instance is missing:
```
instance Show 'a => Show ('a list) = let show xs = to_string xs
let _ = show [true]   -- OK (Show bool exists)
let _ = show [{ x = 1 }]  -- Fails: no Show for user record type
```
The error should say "No instance of Show for MyRecord, needed for Show (MyRecord list)" —
not "No instance of Show for MyRecord list."

**Prevention:**
- When `resolveConstraint` returns `false` at depth > 0, capture the failing *subgoal*
  (`c` at the point of failure) and pass it up to the error reporter.
- Alternatively, on E0701, check whether any instance exists for `c.ClassName` that
  structurally matches `c.TypeArg` (i.e., would match if its constraints were satisfied).
  If so, emit a "missing element type instance" hint.
- Keep the depth guard at 20 but add a `depth = 20` branch that emits a distinct error:
  "instance resolution exceeded depth limit — possible circular instance chain."

**Phase:** P1. Must address before adding constrained instances.  
**Severity:** Blocks progress — misdiagnosed errors will waste time on every new instance.

---

### Pitfall TC-2: Duplicate Instance Detection Breaks for Constrained Instances with the Same Head

**What goes wrong:** The duplicate instance check at TypeCheck.fs line 1122 is:
```fsharp
if existingInstances |> List.exists (fun ii -> ii.InstanceType = instType) then
    raise DuplicateInstance
```
This compares `instType` by structural equality. For constrained instances, `instType` for
`Show ('a list)` is `TList (TVar N)` where `N` is a freshly allocated type variable. Two
declarations of `Show 'a => Show ('a list)` generate *different* `TVar` ids (because
`elaborateWithVars` uses `freshVar()`), so `ii.InstanceType = instType` returns `false` and
the duplicate is silently accepted.

**Why it happens:** Fresh variable allocation is per-instance, not normalized. `TVar 1042`
and `TVar 1089` are structurally different even if they represent the same "element type
variable" in two otherwise identical instance declarations.

**Consequences:** Two identical `Show 'a => Show ('a list)` declarations both register.
During resolution, `List.exists` in `resolveConstraint` finds the first match — the second
instance is dead code and silently ignored. If the two instances have different method bodies,
the second body is never used (coherence violation).

**Detection:** Add a test:
```
instance Show 'a => Show ('a list) = let show xs = to_string xs
instance Show 'a => Show ('a list) = let show xs = "WRONG"
```
Should produce E0702. Currently it likely does not.

**Prevention:**
- Normalize type variables before duplicate comparison: replace all `TVar N` in `instType`
  with canonical indices (e.g., 0, 1, 2 in order of appearance). Two instances with the
  same structure but different fresh var ids become equal under normalization.
- Alternatively, use `freeVars`-based alpha-equivalence: two instance types are duplicates
  if there exists a bijection on their free variables that makes them structurally equal.
- The existing `Type.formatTypeNormalized` function already does this normalization for
  display — adapt it for equality comparison.

**Phase:** P1. Must fix before adding any constrained instances to Prelude.  
**Severity:** Blocks progress — silent duplicate acceptance leads to incoherent resolution.

---

### Pitfall TC-3: Num/Eq Migration Breaks 724 Tests if `+`, `-`, `*`, `=` Dispatch Changes

**What goes wrong:** `Bidir.fs` handles `Add`, `Subtract`, `Multiply`, `Divide`, `Modulo`,
`Equal`, `NotEqual` as dedicated AST cases with hard-coded `TInt`/`TBool`/`TString` dispatch
(lines 534–579). These cases bypass the type class system entirely. If `+`, `-`, `*` are
migrated to a `Num` type class and `=` to an `Eq` type class, every expression using these
operators now requires constraint resolution during type inference. Any program that uses
`+` without an explicit type annotation generates a `Num 'a` constraint that must resolve
at generalization time.

The 724 flt tests contain hundreds of `+`, `-`, `*`, `=` uses on `int` values. After
migration, each use generates a `Num int` subgoal. If the `Num int` instance is missing
from the instance environment (not loaded from Prelude, or loaded after the expression is
type-checked), every test fails with E0701.

**Why it happens:** The current implementation is load-order-safe (hard-coded operators never
need instance lookup). The type class version depends on `currentInstEnv` being populated
before `+` is type-checked. `currentInstEnv` is a module-level mutable (Bidir.fs line 19)
set by TypeCheck.fs after each `InstanceDecl` or `TypeClassDecl`. If Prelude loads after
user code starts type checking (which does not happen today, but could happen if initialization
order changes), all 724 tests fail.

**Why it happens additionally:** The `Add` case in Bidir.fs currently supports string
concatenation by checking `TInt | TString` at line 544. A `Num` type class `+` does not
naturally cover string concatenation. Migrating `+` to `Num` while preserving `string`
concatenation requires either (a) a separate `Concat` class, (b) a `Semigroup`/`Appendable`
superclass, or (c) special-casing string in the `Num int` and `Num string` instances — all
of which add complexity.

**Consequences:** Complete regression of all arithmetic and comparison tests if migration is
not staged. Alternatively, if migration is staged (keep `Add` as builtin AST node, add `Num`
class separately), there is a permanent inconsistency: `show (1 + 2)` works (builtin) but
`let add x y = x + y` with type annotation `Num 'a => 'a -> 'a -> 'a` fails because `+` is
not dispatched through the type class system.

**Detection:** After adding `Num` class, write:
```
let f (x : Num 'a => 'a) (y : 'a) : 'a = x + y
```
If `+` is still builtin-dispatched, this fails to use the `Num` constraint — `f` will only
work for `int`, defeating the purpose.

**Prevention:**
- Do NOT attempt full migration of `+`, `-`, `*`, `=` to type class dispatch in a single
  phase. Stage it:
  1. Phase 3a: Add `Num` class to Prelude with built-in instances for `int` only. Keep
     `Add/Subtract/Multiply` as builtin AST nodes but make them emit `Num 'a` constraints.
  2. Phase 3b: After all 724 tests pass with constraint emission, switch dispatch to use
     the instance environment.
  3. Phase 3c: Add `Num string` instance for `+` (string concat), verify `Add` still works.
- Run `scripts/fslit tests/flt/` after EACH sub-step. Never batch multiple dispatch changes.
- Add `Num int` and `Eq int`/`Eq bool`/`Eq string` instances to Prelude (or to the builtin
  initialization in TypeCheck.fs line 1280–1299) BEFORE changing any dispatch logic in
  Bidir.fs. The instance env must be pre-populated.

**Phase:** P3. The migration is the highest-regression-risk phase in the milestone.  
**Severity:** Blocks progress — a wrong migration order silently breaks 724 tests at once.

---

### Pitfall TC-4: Superclass Entailment Not Checked When Declaring Instances

**What goes wrong:** When a user declares `instance Ord int = ...` and `Ord` has superclass
`Eq`, the current implementation (TypeCheck.fs line 1063–1095) adds superclass constraints
to each method's Scheme so that calling `compare x y` requires `Eq 'a` at the call site.
However, there is no check that the instance declaration for `Ord int` is accompanied by an
instance for `Eq int`. The instance is accepted even if `Eq int` is absent.

At call time, `compare 3 5` synthesizes `Ord int` (satisfied by the instance) and emits
subgoal `Eq int` (from the superclass constraint on `compare`'s Scheme). `Eq int` is then
resolved against `currentInstEnv`. If `Eq int` is absent, E0701 fires at the call site, not
at the `instance Ord int` declaration — the error points to the wrong location.

**Why it happens:** `InstanceDecl` type-checking (TypeCheck.fs line 1097) validates that
method types match the class declaration, but does not call `resolveConstraint` on the
superclass constraints for the instance's concrete type. The check is deferred to call time.

**Consequences:** Users declaring `instance Ord int` without `instance Eq int` get a
confusing E0701 at `compare 3 5` rather than at `instance Ord int`. With multiple levels of
superclasses, the error origin is several frames removed.

**Detection:**
```
typeclass Eq 'a => Ord 'a =
    | compare : 'a -> 'a -> int
instance Ord int =   -- no Eq int instance declared
    let compare x = fun y -> if x < y then -1 else if x > y then 1 else 0
let _ = compare 3 5  -- E0701 fires here, should fire at "instance Ord int"
```

**Prevention:**
- In `InstanceDecl` processing, after registering the instance, immediately resolve the
  superclass constraints for the concrete instance type. For `instance Ord int`, resolve
  `Eq int` against `currentInstEnv` at registration time.
- If resolution fails, raise a new error kind: "Instance `Ord int` requires superclass
  instance `Eq int` which is not declared."
- This check is simple: for each superclass `sc` in `classInfo`, call
  `resolveConstraint { ClassName = sc; TypeArg = instType; SourceSpan = span } 0`.

**Phase:** P2. Implement alongside superclass constraint parsing.  
**Severity:** Quality issue, not a progress blocker — programs still fail, just at the wrong
location. Becomes a blocker when superclass chains are deep.

---

### Pitfall TC-5: `derive` Generates Non-Polymorphic Show for Types with Type Parameters

**What goes wrong:** The `derive Show` implementation in TypeCheck.fs (line 1185–1207)
hardcodes `InstanceVars = []` and `InstanceConstraints = []` for the generated instance:
```fsharp
let newInst = { ClassName = "Show"; InstanceType = instType; InstanceVars = []; InstanceConstraints = [] }
```
For a type like `type Tree 'a = Leaf | Node of 'a * Tree 'a`, `derive Show Tree` should
generate `Show 'a => Show (Tree 'a)`. Instead it generates `Show Tree<?>` with no type
parameters — the generated `show` function calls `show __v` on the node's data field without
a `Show 'a` constraint, which means `show` is resolved against whatever is in scope at derive
time, not the element type.

**Why it happens:** The derive code derives from `typeCtors` which gives `ConstructorInfo`
with `ArgType: Type option`. For `Node of 'a * Tree 'a`, `ArgType` is `TTuple [TVar N, TData("Tree", [TVar N])]`
where `TVar N` is the type parameter. The derive code calls `show __v` on this without adding
`Show (TVar N)` as a constraint or `TVar N` to `InstanceVars`.

**Consequences:** `derive Show Tree` on a parameterized type either:
(a) compiles but calls `to_string` on the element (falling back to the built-in conversion),
    silently giving wrong output, or
(b) fails with E0701 at derive time if no catch-all Show instance exists.

**Detection:**
```
type Tree 'a = Leaf | Node of 'a * Tree 'a
derive Show Tree
let t = Node (42, Node (1, Leaf))
let _ = println (show t)  -- Should print "Node 42 (Node 1 Leaf)" not "Node <opaque>"
```

**Prevention:**
- Before implementing `derive` for parameterized types, audit the `typeCtors` extraction
  (TypeCheck.fs line 1175) to collect type parameters from constructor arg types.
- The generated instance must set `InstanceVars = [N]` and
  `InstanceConstraints = [{ ClassName = "Show"; TypeArg = TVar N }]` for each type
  parameter `N` that appears in an arg type.
- The generated `show` body for `Node of 'a * Tree 'a` must call `show __v` where `show`
  is the class method dispatched through the `Show 'a` constraint — not a bare lookup.
- Defer `derive` for parameterized types to a separate sub-phase after the simple
  (non-parameterized) derive works end-to-end.

**Phase:** P4. Parameterized derive is a separate sub-problem from simple derive.  
**Severity:** Blocks progress on the derive phase — silent wrong output is worse than an
error.

---

## PART B: Moderate Pitfalls (Cause Delays and Technical Debt)

### Pitfall TC-6: Superclass Diamond Problem — Same Method Name in Two Paths

**What goes wrong:** If `Ord` inherits from `Eq` and `Hash` also inherits from `Eq`, and
a future class inherits from both `Ord` and `Hash`, methods declared in `Eq` (e.g., `eq`)
appear in both superclass constraint chains. When calling `eq` on a value of that type,
`resolveConstraint` finds `Eq 'a` through both the `Ord 'a` path and the `Hash 'a` path.
Since `resolveConstraint` returns `true` on the first match (`List.exists`), this is
practically safe for constraint *satisfaction*. However, if the user declares two different
`Eq int` instances (one for the `Ord` path and one for the `Hash` path), the `DuplicateInstance`
check (E0702) would fire, preventing valid use.

**Why it happens:** FunLang's `InstanceEnv` stores instances as `Map<className, InstanceInfo list>`
and E0702 checks for structural equality of `instType`. Two `Eq int` instances from different
derivation paths would be structurally equal and trigger a false duplicate error.

**Consequences:** Diamond inheritance is blocked by E0702 even when only one `Eq int`
instance exists. Alternatively, if the first instance silently wins, the diamond is handled
correctly but coherence is not enforced.

**Prevention:**
- For v15.0, limit superclass declarations to single-inheritance chains (one superclass per
  class). Document this restriction explicitly in the grammar comment.
- The parser already accepts `ConstraintList` for superclass constraints (Parser.fsy line 724),
  so the grammar allows multiple superclasses. Add a check in TypeCheck.fs `TypeClassDecl`
  processing that errors if `superclasses.Length > 1`:
  ```
  "Multiple superclass constraints not supported in v15.0"
  ```
- Defer multi-superclass / diamond handling to a later milestone.

**Phase:** P2. Must document the restriction before releasing superclass support.  
**Severity:** Moderate — only affects advanced usage, but silent wrong behavior is possible.

---

### Pitfall TC-7: Mutable `currentInstEnv` and `currentClassEnv` Cause Resolution Order Bugs

**What goes wrong:** `Bidir.currentInstEnv` and `Bidir.currentClassEnv` are module-level
mutable refs (lines 17–19 of Bidir.fs). They are set by TypeCheck.fs after each
`TypeClassDecl` or `InstanceDecl` is processed (lines 1090, 1169, 1204, 1227). During
type-checking of a `let` binding that FOLLOWS an instance declaration, the mutable is
already set. During type-checking of the method bodies INSIDE the instance declaration
(line 1152), the mutable still holds the *pre-instance* environment.

For constrained instances like `Show 'a => Show ('a list)`, the method body may call
`show` recursively (on the element type). At the time the method body is type-checked, the
`Show ('a list)` instance has not yet been added to `currentInstEnv` (it is added at line
1167, after method type-checking). A direct recursive use of `show` inside the instance body
therefore generates E0701.

**Why it happens:** The add-then-check order in TypeCheck.fs is:
1. Type-check method bodies (line 1143–1154) — `currentInstEnv` does NOT contain the new instance
2. Add instance to `iEnv'` (line 1166–1167) — `currentInstEnv` NOW contains it

For recursive instances (the instance referring to itself), step 1 cannot see step 2.

**Consequences:** `instance Show 'a => Show ('a list) = let show xs = List.map show xs`
(direct recursive element-level show) would fail during method body type-checking with
E0701 for the recursive call to `show`.

**Detection:**
```
instance Show 'a => Show ('a list) =
    let show xs = match xs with
        | [] -> "[]"
        | h :: t -> show h ++ " :: " ++ show t
```
The `show t` call (recursive on tail, same class) should work but may not.

**Prevention:**
- Before type-checking method bodies, add a *provisional* instance entry (with placeholder
  method types) to `currentInstEnv`, then type-check the bodies, then replace with the
  real entry.
- Alternatively: don't type-check instance method bodies against the class at all at
  declaration time — infer their types separately and verify at call site (less safe but
  avoids the ordering problem).
- The simplest fix: add the instance to `currentInstEnv` BEFORE type-checking method bodies
  (swap the order in TypeCheck.fs lines 1143–1167). This is safe for non-self-referential
  instances and fixes the recursive case.

**Phase:** P1. Test recursive constrained instance method bodies explicitly.  
**Severity:** Moderate — only affects recursive instance methods; caught immediately by tests.

---

### Pitfall TC-8: `E0701` Error Shows Internal TVar Index for Indirect Polymorphic Constraints

**What goes wrong:** (This is the known bug described in project context.)  
When a constraint is generated indirectly — e.g., calling a function `f : Show 'a => 'a ->
string` with an argument whose type is still a TVar — the span on the emitted constraint
(via `instantiateAt` in Bidir.fs line 49) comes from the *call site's* span. But `c.TypeArg`
at E0701 time is `TVar 1042` — an internal integer — and `formatType (TVar 1042)` produces
`'z` (or some letter based on `1042 % 26`), not the user-facing type name.

After Num/Eq migration, this problem multiplies: every `+`, `-`, `*`, `=` call on a
polymorphic argument generates a `Num 'a` or `Eq 'a` constraint. If the constraint fires
for an indirect reason, the error shows `'z` rather than the actual type.

**Why it happens:** `formatType` uses `97 + n % 26` (line 106 of Type.fs) for arbitrary
TVar indices. Variable indices like 1042 wrap around in the alphabet unpredictably. The
user sees `'z` when they expect `'a` or the actual type name.

**Prevention:**
- In E0701 error formatting (Diagnostic.fs line 452), apply `formatTypeNormalized` rather
  than `formatType` when the type arg is a TVar or contains TVars:
  ```fsharp
  sprintf "No instance of %s for %s" className (formatTypeNormalized ty)
  ```
  `formatTypeNormalized` (Type.fs line 128) remaps vars in order of appearance to `'a`, `'b`, ...
- Fix this BEFORE adding Num/Eq migration — otherwise the new constraints will produce
  confusing error messages immediately.

**Phase:** P5 (error messages), but should be fixed in P1 or P3 to prevent cascade.  
**Severity:** Quality issue, but becomes a progress blocker after Num/Eq migration multiplies
the occurrence rate.

---

### Pitfall TC-9: `derive Eq` on Types with Function-Typed Fields Produces Runtime Crash

**What goes wrong:** The `derive Eq` implementation (TypeCheck.fs lines 1208–1229) generates
an equality function that recursively calls `eq` on constructor arguments. If a constructor
has a function-typed field (`type Callback = CB of (int -> int)`), the generated code tries
`eq __a __b` where `__a : int -> int`. Function equality is not defined — FunLang's `Equal`
AST node currently compares `Value` using F#'s structural equality, which would raise an
exception for `ClosureValue` comparison.

**Why it happens:** The derive code does not inspect `ArgType` to validate that the field
type is equatable. It blindly generates `eq __a __b` for all fields.

**Consequences:** The type-checker accepts `derive Eq CB` (no type error at derive time).
At runtime, `eq (CB (fun x -> x)) (CB (fun x -> x))` raises an F# exception (not a
FunLang error), producing a crash rather than a graceful error.

**Prevention:**
- At derive time, check `ArgType` for function types (`TArrow _`). If found, either:
  (a) Emit a type error: "Cannot derive Eq for type with function-typed fields"
  (b) Generate `false` for that constructor pair (structural equality is undefined)
- For v15.0, option (a) is safer and simpler. Option (b) is semantically inconsistent.
- Add a check in `DerivingDecl` processing: traverse all constructor `ArgType`s and
  reject `TArrow` types with a descriptive error.

**Phase:** P4.  
**Severity:** Moderate — only triggered by unusual types. Crash (not graceful error) is the
main concern.

---

### Pitfall TC-10: Method Type-Checking in `InstanceDecl` Ignores Substitution Side Effects

**What goes wrong:** In TypeCheck.fs line 1152–1154:
```fsharp
let s, actualTy = Bidir.synth cEnv rEnv [] env rewrittenBody
let s2 = Unify.unifyWithContext [] [] span (apply s actualTy) (apply s expectedTy)
ignore s2
```
The result substitution `s2` is discarded. This is fine for simple instances where the method
body is monomorphic. For constrained instances, the method body may emit pending constraints
(via `instantiateAt`) that need to be drained via `generalize` before the method type is
finalized. Discarding `s2` means `pendingConstraints` may contain unresolved constraints from
the method body that are still in scope after the instance declaration is processed, leaking
into subsequent let bindings.

**Why it happens:** The type-checker was written for simple monomorphic method bodies. The
pattern `let s, ty = synth ...; ignore (unify ...)` is consistent with TypeCheck.fs's other
constraint-draining sites, but those sites call `generalize` or `applySubstToConstraints`
afterward. The instance method checking does not.

**Consequences:** Pending constraints from instance method bodies accumulate in
`pendingConstraints` and attach to the next `generalize` call, creating spurious constraints
on unrelated let bindings. This appears as `Num 'a => ...` in the inferred type of a binding
that has nothing to do with Num.

**Prevention:**
- After type-checking each method body in `InstanceDecl`, drain pending constraints:
  ```fsharp
  Bidir.applySubstToConstraints s
  let _ = Bidir.pendingConstraints  // drain
  Bidir.pendingConstraints <- []
  ```
  Or wrap each method body check in a `generalize` boundary.
- Write a test with a constrained instance followed by an unrelated `let`:
  ```
  instance Show 'a => Show ('a list) = let show xs = to_string xs
  let result = 42 + 1   // Should have type int, not Num 'a => int
  ```
  If `result` has a constraint, the leak is confirmed.

**Phase:** P1.  
**Severity:** Moderate — spurious constraints cause confusing E0701 in unrelated code.

---

## PART C: Minor Pitfalls (Annoying but Fixable)

### Pitfall TC-11: `E0704` (Method Type Mismatch) Never Fires — E0301 Fires Instead

**What goes wrong:** (This is the known bug documented in ERRORS.md as "Bug 10 — deferred".)
The method body type check at TypeCheck.fs line 1153 unifies `actualTy` with `expectedTy`
using `Unify.unifyWithContext`. If the types don't match, `Unify.unifyWithContext` raises
`TypeException { Kind = UnifyMismatch(...) }` (E0301), not `TypeException { Kind = MethodTypeMismatch(...) }`
(E0704). E0704 is never raised.

After Num/Eq migration, more method bodies will be type-checked (every builtin operator
instance), increasing the surface area where E0704 should fire. Users implementing a `Num`
instance with wrong method types will see E0301 ("type mismatch") instead of E0704 ("method
type mismatch in instance Num for int"). The fix is to catch `UnifyMismatch` in the
`InstanceDecl` processing block and re-raise as `MethodTypeMismatch`.

**Prevention:**
- Wrap the `unifyWithContext` call at line 1153 in a try/with:
  ```fsharp
  try
      let s2 = Unify.unifyWithContext [] [] span (apply s actualTy) (apply s expectedTy)
      ignore s2
  with
  | TypeException { Kind = UnifyMismatch _ } ->
      raise (TypeException {
          Kind = MethodTypeMismatch(className, methodName, apply s actualTy, apply s expectedTy)
          Span = span; Term = None; ContextStack = []; Trace = []; Scope = []})
  ```
- Fix this before adding Num/Eq instances — the instance bodies are type-checked at
  declaration time and any mismatch should produce E0704.

**Phase:** P5 (error messages), but easy to fix in P3 alongside instance declarations.  
**Severity:** Minor quality issue. Functional behavior is correct; error code/message is wrong.

---

### Pitfall TC-12: `uniqueDeferred` Deduplication Uses Structural TVar Equality

**What goes wrong:** Bidir.fs line 128:
```fsharp
let uniqueDeferred = deferred |> List.distinctBy (fun c -> (c.ClassName, c.TypeArg))
```
Two constraints `Show (TVar 1042)` and `Show (TVar 1089)` are considered distinct even if
they refer to the same logical type variable after substitution. In function types with
multiple constrained parameters, this causes duplicate constraints in the scheme:
```
Show 'a, Show 'a => 'a -> 'a -> string
```
instead of:
```
Show 'a => 'a -> 'a -> string
```

This is currently a minor cosmetic issue (extra constraints do not break resolution). After
Num/Eq migration, the number of emitted constraints grows significantly and duplicates
accumulate more.

**Prevention:**
- Apply the current substitution to constraints before deduplication:
  ```fsharp
  let uniqueDeferred =
      deferred
      |> List.map (fun c -> { c with TypeArg = apply currentSubst c.TypeArg })
      |> List.distinctBy (fun c -> (c.ClassName, c.TypeArg))
  ```
  This requires passing the final substitution into `generalize`, which already has access
  to `env` via `applyEnv`.

**Phase:** P3. Becomes more visible after Num/Eq migration.  
**Severity:** Minor — cosmetic issue in inferred types, no runtime impact.

---

### Pitfall TC-13: `derive Show` Generates `"__v"` Variable Names That Collide with User Code

**What goes wrong:** The derive code (TypeCheck.fs line 1195) uses `__v`, `__x`, `__a`,
`__b` as internal variable names in the generated AST. If a user's ADT constructor field
type is itself an ADT with a constructor named `__v`, or if the user's scope contains a
binding named `__v`, the generated match pattern accidentally captures the wrong variable.

**Why it happens:** Generated variable names are not gensym'd — they are hardcoded strings.
FunLang does not have a hygienic macro system.

**Consequences:** Silent wrong output from `show` on types whose constructors happen to bind
`__v` in an outer scope. Unlikely in practice but possible.

**Prevention:**
- Use names unlikely to collide: `__derive_v__`, `__derive_x__`, `__derive_a__`, `__derive_b__`.
- Or use `Infer.freshVar()` to generate a numeric index and append it:
  `sprintf "__v_%d__" freshIdx`.
- For recursive types, the generated `show __v` call needs `show` to refer to the class
  method, not any user-defined `show` in scope. Ensure the generated body uses a fully
  qualified reference if modules are in play.

**Phase:** P4.  
**Severity:** Minor — edge case. Use ugly-enough names to avoid collision.

---

## PART D: Phase-Specific Warning Summary

| Phase | Topic | Most Likely Pitfall | Mitigation |
|-------|-------|--------------------|-|
| P1 | Constrained instance resolution | E0701 misdiagnosed when element instance missing (TC-1) | Capture failing subgoal in error |
| P1 | Duplicate instance detection | False negative for alpha-equivalent constrained instances (TC-2) | Normalize TVars before duplicate check |
| P1 | Recursive instance method bodies | Pending constraints leak out of instance scope (TC-10) | Drain `pendingConstraints` after each method body |
| P1 | Recursive self-referential methods | Method body cannot see its own instance (TC-7) | Add provisional instance before checking bodies |
| P2 | Superclass constraint on instance | Superclass entailment not checked at declaration time (TC-4) | Resolve superclass constraints at InstanceDecl registration |
| P2 | Multi-superclass diamond | Diamond blocked by E0702 or silently incoherent (TC-6) | Restrict to single-inheritance chains in v15.0 |
| P3 | Num/Eq operator migration | 724-test regression if dispatch order wrong (TC-3) | Stage migration; populate instances before changing dispatch |
| P3 | Constraint count explosion | Duplicate deferred constraints accumulate (TC-12) | Apply substitution before `distinctBy` |
| P3 | Error messages for operator misuse | E0701 shows `'z` (raw TVar index) for indirect constraints (TC-8) | Use `formatTypeNormalized` in E0701 formatter |
| P4 | derive on parameterized types | Generates non-polymorphic instance without type param constraints (TC-5) | Collect type params from ArgType; set InstanceVars/InstanceConstraints |
| P4 | derive on function-typed fields | Runtime crash on function equality (TC-9) | Reject `TArrow` fields at derive time |
| P4 | Generated variable name collision | `__v` clashes with user scope (TC-13) | Use gensym'd or ugly-enough names |
| P5 | E0704 never fires | Method type mismatch reported as E0301 (TC-11) | Catch UnifyMismatch in InstanceDecl and re-raise as MethodTypeMismatch |
| P5 | E0701 internal TVar display | Raw TVar index shown in error (TC-8) | `formatTypeNormalized` in Diagnostic.fs |

---

## PART E: FunLang-Specific Architecture Risks

### Risk TC-AX-1: `currentInstEnv` as Global Mutable is Correct for Sequential Processing but Fragile

FunLang's type checking is sequential (declarations processed top-to-bottom). The mutable
`Bidir.currentInstEnv` works correctly because each `InstanceDecl` updates it before any
subsequent declarations are type-checked. This is safe for single-file programs and for
Prelude-then-user-code loading.

The risk is in module loading: `TypeCheck.fs` line 261 updates `Bidir.currentInstEnv` when
a module is opened. If two modules each define instances, opening them out of order (or
opening a module that was imported in a different order) can change which instance "wins."
The existing flt tests do not stress this case but v15.0's Prelude changes (adding Num/Eq
instances) will be visible to all modules.

**Recommendation:** Do not change the mutable ref pattern for v15.0. Document that instance
resolution is global and order-dependent. Add a comment in Bidir.fs warning future engineers.

---

### Risk TC-AX-2: Depth Guard of 20 in `resolveConstraint` Is Insufficient for Transitively Nested Constrained Instances

A chain of constrained instances like:
```
Show 'a => Show ('a list)
Show 'a => Show ('a option)
```
resolves `Show (int list option)` in 3 steps (depth 3). But deeply nested structures like
`Show ((int list) option list)` require 5 steps. With `Show ((((int list) option) list) option)`
this grows linearly. At depth > 20, resolution silently returns `false`.

For v15.0's `derive Show` on recursive types (e.g., `Tree 'a`), the generated constrained
instance `Show 'a => Show (Tree 'a)` resolves in 2 steps for a leaf, but O(depth_of_tree)
steps for a deep tree — except that resolution happens at *type level* (one call per type
constructor), not at *value level* (one call per tree node). The maximum structural depth of
a type expression is bounded by the user's type annotations, not the runtime data.

In practice, depth 20 is safe for manually written programs. The risk emerges if:
- Users nest more than 10 layers of parameterized types in annotations
- A future superclass chain adds 5+ levels of constraint entailment per call

**Recommendation:** Raise the depth guard from 20 to 50 in v15.0. Add a specific error for
depth exhaustion (TC-1 prevention). The cost of 50 recursive calls is negligible.

---

## Sources

- FunLang source: `/src/FunLang/Bidir.fs` lines 92–128 (resolveConstraint, generalize,
  depth guard, uniqueDeferred deduplication)
- FunLang source: `/src/FunLang/TypeCheck.fs` lines 1063–1231 (TypeClassDecl, InstanceDecl,
  DerivingDecl processing; superclass constraint building; instVars collection)
- FunLang source: `/src/FunLang/Type.fs` lines 83–95 (InstanceInfo structure, InstanceVars,
  InstanceConstraints fields), lines 106, 128 (formatType vs. formatTypeNormalized)
- FunLang source: `/src/FunLang/Diagnostic.fs` lines 452–481 (E0701–E0704 error formatting)
- FunLang source: `/src/FunLang/Bidir.fs` lines 534–579 (Add/Subtract/Multiply/Equal builtin
  dispatch — must change for Num/Eq migration)
- FunLang tests: `tests/flt/file/typeclass/` — 29 type class tests, including
  `typeclass-constrained-instance-show-list.flt`, `typeclass-superclass.flt`,
  `typeclass-deriving-show.flt`, `typeclass-deriving-eq.flt`
- FunLang ERRORS.md lines 414–491 — E0701–E0706 documentation, known Bug 10 (E0704 deferred)
- FunLang PROJECT.md — v15.0 milestone definition, constraint on 724 flt / 244 unit tests
- GHC Commentary: "Simplifier — Class and Instance Declarations" — superclass entailment
  checking at instance declaration time (reference pattern for TC-4 prevention)
- THIH (Typing Haskell in Haskell, Jones 1999) — instance resolution algorithm, depth-first
  constraint resolution with occurs-check (reference for TC-1 and TC-AX-2)
- "System FC: Explicit Substitutions" — coherence in type class systems (reference for TC-2
  alpha-equivalence duplicate detection)
