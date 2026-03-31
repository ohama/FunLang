# Domain Pitfalls: Type Classes for LangThree v10.0

**Domain:** Adding Haskell-style type classes to an existing ML interpreter with HM inference
**Researched:** 2026-03-31
**Scope:** Pitfalls specific to adding type classes to LangThree's existing HM + bidirectional checker + GADT + module system
**Context:** LangThree v9.1 baseline — Scheme(vars, ty) generalization, Bidir.fs synth/check, Infer.freshVar/generalize, Unify.unifyWithContext, Eval.fs tree-walker

---

## How to Read This File

Each pitfall has a **Phase** tag indicating which implementation phase is most at risk. Phases assumed are:

- **P1**: Type class declarations and instance declarations (parser + AST + typeclass env)
- **P2**: Constraint-augmented HM inference (Scheme → qualified types, constraint propagation)
- **P3**: Instance resolution (dictionary building, entailment checking)
- **P4**: Dictionary passing in evaluator (elaboration or runtime lookup)
- **P5**: Integration with existing builtins (to_string, comparison operators, arithmetic)
- **P6**: Constrained instances (e.g. `instance Show (Option 'a) where Show 'a`)

---

## PART A: Critical Pitfalls (Cause Rewrites)

### Pitfall TC-1: Constraint Variables Escape Let Generalization

**What goes wrong:** When generalizing a type at a `let` boundary, free type variables are quantified. If constraints are NOT tracked alongside quantified variables, the constraint `Show 'a` for a generalized `'a` disappears from the scheme. The resulting scheme `forall 'a. 'a -> string` looks unconditionally polymorphic but at call sites there is no constraint to discharge — either the wrong instance fires or a runtime crash occurs.

**Why it happens:** LangThree's `generalize` in `Infer.fs` currently produces `Scheme(vars, ty)` with no constraint component. Adding type variables to the quantified set without also capturing their constraints silently produces unsound schemes.

**Concrete example:**
```
let show_it x = show x   (* show : Show 'a => 'a -> string *)
```
Without constraint tracking, `show_it` generalizes to `Scheme([a], a -> string)` — no `Show a` constraint retained. Calling `show_it 42` works (Show int resolves), but `show_it (fun x -> x)` also type-checks with no error, then crashes at runtime when the Show dictionary lookup finds nothing.

**Prevention:**
- Extend `Scheme` to `Scheme of vars: int list * constraints: Constraint list * ty: Type` from the start of P2.
- Modify `generalize` to also collect constraints that mention any of the free variables being generalized.
- At call sites (`instantiate`), re-emit fresh constraint goals from the scheme constraints, substituting fresh vars for quantified vars.
- Do NOT generalize without simultaneously generalizing constraints.

**Warning signs:**
- `let f x = show x` type-checks but `f (fun x -> x)` produces no type error.
- Scheme pretty-printing shows no constraints for functions that use overloaded operations.

**Phase:** P2 (constraint-augmented generalization). Must be correct before P3 attempts any resolution.

---

### Pitfall TC-2: Instance Resolution Called at Inference Time Instead of Post-Unification

**What goes wrong:** Resolution is attempted eagerly during type inference — as soon as a constraint goal is generated — before unification has had a chance to determine what the type variable actually is. The resolver sees an unconstrained `TVar 1042` and either fails (no instance for `TVar`) or picks the wrong instance.

**Why it happens:** It is natural to resolve constraints immediately when they are emitted (similar to how unification is called immediately in Algorithm W). But constraints involving type variables must be deferred: `Show (TVar 1042)` cannot be resolved until 1042 is unified with a concrete type.

**Concrete example:**
```
let x = show 42    (* generates: Show ?a, ?a ~ int *)
```
If resolution fires before `?a ~ int` unification, the resolver sees `Show TVar 1042` and fails with "no instance for type variable". Correct behavior: defer, unify first, then resolve `Show int`.

**Prevention:**
- Maintain two constraint sets during inference: **wanted** (unresolved goals) and **given** (in-scope axioms from instance heads, local class constraints).
- Resolve wanted constraints only after unification is complete for a binding group — i.e., at the end of the let-binding, not inline.
- Alternatively, use a constraint-based reformulation (generate all constraints first, unify second, resolve third). This is cleaner than interleaved Algorithm W style.

**Warning signs:**
- Calls to `show 42` produce "no instance for TVar" errors even when int has a Show instance.
- Resolution works for top-level let bindings but fails for intermediate sub-expressions.

**Phase:** P2/P3 boundary. The discipline of when resolution fires must be decided in P2's design.

---

### Pitfall TC-3: Dictionary Passing via Environment (Not Elaboration) Causes Scope Leaks

**What goes wrong:** In a tree-walking interpreter, the simplest approach is to thread a "dictionary environment" alongside the value environment, adding instance dictionaries at declaration sites and looking them up at call sites. This breaks with higher-order functions because the dictionary is captured at the definition site, not resolved at the use site.

**Why it happens:** LangThree's `Eval.fs` passes `Env` (a `Map<string, Value>`) as a pure value. If dictionaries are stored in `Env` as `DictValue "Show"` etc., they are subject to normal closure capture. A function `fun f -> f 42` closed over a `Show int` dictionary will fail when called with an argument that requires `Show string` — the wrong dictionary is in the closure.

**Concrete example:**
```
let apply_show f x = f (show x)    (* show needs Show ?a dictionary *)
apply_show identity "hello"         (* should resolve Show string *)
apply_show identity 42              (* should resolve Show int *)
```
If `show`'s dictionary is captured at `apply_show`'s definition site, both calls use the same dictionary — wrong for whichever call doesn't match.

**Prevention:**
- The correct model: dictionaries are passed as **explicit lambda arguments** (elaboration). Type inference elaborates `show x` into `show_dict x` where `show_dict` is an explicit parameter.
- For a tree-walker, elaboration means: during type-checking, rewrite the AST to add dictionary parameters to functions that have class constraints, and add dictionary arguments at call sites.
- Alternatively: pass dictionaries through a separate "evidence environment" that is threaded correctly alongside the call stack — effectively the same as elaboration but done in the evaluator.
- Do NOT attempt to resolve dictionaries globally from a flat name table at runtime without elaboration — this is coherent only for a non-higher-order language.

**Warning signs:**
- Higher-order functions using overloaded operations produce wrong results or wrong-instance errors.
- `map show [1; 2; 3]` works, but `let f = map show in f [1; 2; 3]` fails or uses wrong instance.

**Phase:** P4 (evaluator integration). Must decide elaboration vs. runtime threading in P3 design.

---

### Pitfall TC-4: Overlapping `to_string`/Comparison Builtins Break Instance Uniqueness

**What goes wrong:** LangThree has existing built-in functions `to_string`, `=`, `<>`, `<`, `>`, `<=`, `>=` that work on multiple types via ad-hoc runtime dispatch in `Eval.fs`. When type classes are added, these become instances of `Show`, `Eq`, `Ord`. If both the old builtin dispatch AND the new type class dispatch exist simultaneously, there are two competing resolution paths for the same constraint — incoherence.

**Why it happens:** The natural migration path is to add type class instances for `Show int`, `Show string`, etc. while leaving the old builtins intact "for compatibility". The result is that `to_string 42` resolves via the builtin but `show 42` resolves via the type class, and they may diverge if the builtin is not an exact alias.

**Concrete example:**
```
to_string true   (* builtin: "true" *)
show true        (* typeclass Show bool: could be different format *)
```
If `show` for `bool` is user-extensible but `to_string` is hardcoded, a user overriding `show` for their ADT can never override `to_string` — inconsistency.

**Prevention:**
- Make a clear architectural decision at the start of P5: either (a) the builtins become the default instances and `to_string` becomes an alias for `show`, or (b) the builtins are removed and replaced entirely by type class instances.
- If (a): the builtin dispatch in `Eval.fs` must delegate through the type class mechanism, not bypass it.
- If (b): migration requires updating all existing tests that use `to_string`, `=`, etc.
- Never have both paths active simultaneously.

**Warning signs:**
- `to_string` and the method from `Show` produce different output for the same value.
- Tests that use `=` directly pass while tests that use `Eq.equal` fail, or vice versa.

**Phase:** P5 (builtin integration). The design decision must be made before P1 parser design commits syntax.

---

## PART B: Moderate Pitfalls (Cause Delays and Technical Debt)

### Pitfall TC-5: Ambiguous Type Errors with No Good Diagnostic

**What goes wrong:** After inference, a constraint remains with a type variable that is not determined by the function's inputs or outputs. For example:

```
let x = show (read "42")    (* Show ?a, Read ?a — both unsatisfied, ?a unknown *)
```

Without defaulting rules or explicit annotations, inference correctly reports this as ambiguous. But without a clear error message pointing to the ambiguous variable and the unsatisfied constraints, users get a cryptic unification error or a "no instance" error that does not explain what annotation is needed.

**Prevention:**
- After constraint solving, if any wanted constraint contains a type variable that is not reachable from the environment (no context to fix it), report `AmbiguousType` with the constraint name and the variable.
- Include the source span from where the constrained operation was called.
- Suggest "add a type annotation to disambiguate" in the error message.
- Add this to `Diagnostic.fs` as a new `ErrorKind`.

**Warning signs:**
- Users report confusing type errors on code that should obviously work once annotated.
- The error message mentions internal `TVar 1042` instead of a human-readable class name.

**Phase:** P2/P3. Must be addressed before user-facing testing begins.

---

### Pitfall TC-6: Superclass Constraints Cause Infinite Resolution Loops

**What goes wrong:** If `Ord 'a` has superclass `Eq 'a`, and the instance resolution for `Eq` tries to use `Ord` as evidence (common when `Eq` methods are defined in terms of `Ord`), you get an infinite loop: resolving `Eq int` looks for `Ord int` which requires `Eq int` to check the superclass...

**Why it happens:** The Paterson Conditions exist in GHC specifically to prevent this. Without a termination check on resolution depth or constraint set size, the resolver loops.

**Prevention:**
- For LangThree v10.0 (initial type classes), avoid superclass hierarchies entirely in the first phase. Implement `Show`, `Eq`, `Ord`, `Num` as independent classes with no declared superclass relationship.
- If superclasses are added later, implement a depth limit (e.g. 50 resolution steps) with a clear error message "constraint resolution exceeded depth limit — possible cycle".
- Track the set of constraints currently being resolved as a "stack" and detect when the same constraint re-appears in the stack (the Coin-cell check used in real implementations).

**Warning signs:**
- The type checker hangs on programs with multiple type class constraints.
- Stack overflow in the F# process when type-checking even simple programs.

**Phase:** P3 (instance resolution). Relevant if P6 (constrained instances) is in scope.

---

### Pitfall TC-7: Constraint Generalization Interacts Badly with Mutable Variables

**What goes wrong:** LangThree already has a deliberate decision: `let mut x = e` is monomorphic (no generalization). This was correct for preventing unsound polymorphism with mutable references. However, if a mutable variable holds a value of a constrained type, the constraint also must not be generalized. If the implementation forgets this and generalizes `let mut x = 0` to `Scheme([a; Show a], a)`, the mutable variable becomes unsoundly polymorphic.

**Why it happens:** The constraint-augmented generalization in P2 must query `mutableVars` (LangThree already has this `Set<string>` in `Bidir.fs`) and skip generalization for mutable variables — including skipping constraint generalization.

**Prevention:**
- Extend the existing `mutableVars` check in `generalize` to cover constraint generalization.
- Rule: if a binding is in `mutableVars`, produce `Scheme([], [], monotype)` with no quantified variables AND no generalized constraints.
- This is already correct for the type variable case; ensure the same holds when constraints are added.

**Warning signs:**
- A `let mut` binding accepts values of different types across different uses in the same scope.
- Runtime type errors in code that uses mutable variables with overloaded operations.

**Phase:** P2. A straightforward extension of the existing mutable variable check.

---

### Pitfall TC-8: LALR(1) Conflicts from Typeclass Syntax Choices

**What goes wrong:** LangThree uses fsyacc (LALR(1)). Type class syntax introduces tokens and productions that can conflict with existing grammar. Common conflict sites:

1. `typeclass Show 'a = ...` — if `typeclass` is a new keyword, it must be added to the lexer. But the parser currently has `let` declarations at top level. A `typeclass` at top level that uses `=` may conflict with `let ... = ...`.
2. `instance Show int = ...` — the word `instance` may tokenize as `IDENT` unless added as a keyword, causing ambiguity.
3. Constraint syntax `(Show 'a) =>` in function signatures — the `=>` token does not currently exist. If it is `= >` split across tokens, the lexer produces `ASSIGN GT` which is a sequence the parser cannot distinguish from `=` followed by `>`.
4. `where` clause — LangThree does not have `where`. Adding it as a keyword may conflict if any existing code uses `where` as an identifier.

**Prevention:**
- Reserve `typeclass`, `instance`, and `where` as keywords in the lexer (Lexer.fsl) from P1 start.
- Add `FATARROW` (or `DARROW`) as a distinct token for `=>`.
- Run `dotnet build` after every lexer/parser change and inspect the shift/reduce report for new conflicts.
- Keep type class declarations syntactically distinct from `let` declarations — using `typeclass Name 'a where` instead of `typeclass Name 'a =` avoids the `=`-conflict entirely.

**Warning signs:**
- fsyacc reports new shift/reduce or reduce/reduce conflicts after parser additions.
- The parser accepts type class declarations but silently parses them as something else (e.g., a `let` with a wrong name).

**Phase:** P1. Must be resolved before any other phase can proceed.

---

### Pitfall TC-9: Instance Resolution Not Threaded Through File Imports

**What goes wrong:** LangThree has a file import system (`open "path.fun"`) with an import cache. The instance environment must be accumulated across imported files. If instance declarations from imported files are not added to the instance environment before processing the importing file, instance resolution fails for any type defined in the imported file.

**Why it happens:** LangThree's existing import system passes `TypeEnv`, `ConstructorEnv`, `RecordEnv` from imported modules to the importer. A new `InstanceEnv` must be threaded through the same pipeline. If it is omitted, instance lookup is limited to the current file only.

**Concrete example:**
```
(* types.fun *)
type Color = Red | Green | Blue
instance Show Color = ...

(* main.fun *)
open "types.fun"
println (show Red)    (* fails: Show Color not in scope if InstanceEnv not propagated *)
```

**Prevention:**
- Add `InstanceEnv` as a return value from module type-checking, alongside existing `TypeEnv` etc.
- Import caching must include `InstanceEnv` in the cached result.
- Ensure `InstanceEnv` is passed through all call sites in `TypeCheck.fs` and `Cli.fs`.

**Warning signs:**
- Instances defined in imported files produce "no instance" errors in the importing file.
- Top-level instances work but instances in Prelude files do not.

**Phase:** P3/P6. Must be addressed before any multi-file programs with type classes work.

---

### Pitfall TC-10: Constrained Instance Context Reduction Incomplete

**What goes wrong:** `instance Show (Option 'a) where Show 'a` means: to resolve `Show (Option int)`, the resolver must also resolve `Show int` (a subgoal). If context reduction does not recursively resolve subgoals, the constrained instance is accepted at declaration time but fails at use time.

**Concrete example:**
```
instance Show (Option 'a) where Show 'a =
    fun x -> match x with
             | None -> "None"
             | Some v -> "Some(" ++ show v ++ ")"

show (Some 42)   (* must resolve: Show (Option int) -> Show int -> ok *)
show (Some (fun x -> x))  (* must fail: no Show for function types *)
```

If context reduction does not propagate `Show int` as a sub-goal, `show (Some 42)` may either crash or produce a type error that blames the wrong site.

**Prevention:**
- Implement context reduction as a recursive procedure that, when resolving `Show (Option int)`:
  1. Matches against `instance Show (Option 'a) where Show 'a`
  2. Emits the subgoal `Show int` (with `'a` = `int`)
  3. Recursively resolves `Show int`
  4. Builds the composite dictionary `{show = fun x -> ... show_dict_int ...}`
- Termination: rely on the depth limit from TC-6.

**Warning signs:**
- `show (Some 42)` works, but `show (Some (fun x -> x))` does not produce a type error (it should).
- Constrained instances resolve their head but pass dictionary holes to the body.

**Phase:** P6 (constrained instances). This is the hardest resolution case.

---

## PART C: Minor Pitfalls (Annoying but Fixable)

### Pitfall TC-11: Type Variable Index Collision in Instantiated Constraints

**What goes wrong:** `Infer.freshVar` starts at 1000 and increments. When a scheme with constraints is instantiated, fresh variables replace quantified variables in both the type AND the constraints. If the constraint instantiation uses a different fresh variable counter than the type instantiation (e.g., by calling freshVar separately), the constraint `Show ?1000` refers to a different variable than the type `?1001 -> string`, even though they should be the same `'a`.

**Prevention:**
- Instantiate the type and all constraints in a single pass using the same substitution mapping `{quantified_var -> fresh_var}`.
- Never call `freshVar()` separately for constraint instantiation vs. type instantiation.

**Warning signs:**
- Constraint errors reference type variables not present in the inferred type.
- `show` works when the constrained variable is the only type variable, but fails when there are multiple type variables.

**Phase:** P2 (constraint instantiation). Easy to get right if noticed early.

---

### Pitfall TC-12: `formatType` Does Not Display Constraints

**What goes wrong:** `Type.formatTypeNormalized` and `Type.formatSchemeNormalized` do not show constraints. Error messages that reference constrained types will omit the constraint, producing confusing output like `expected: 'a -> string, got: int -> string` when the real message should be `expected: Show 'a => 'a -> string, got: int -> string`.

**Prevention:**
- Extend `formatSchemeNormalized` to format constraints as `(Show 'a, Eq 'a) => ...` when constraints are present.
- Update all `Diagnostic.fs` error formatting that calls `formatType` or `formatScheme` to use the constraint-aware version.

**Warning signs:**
- Type error messages reference `'a -> string` without constraint context, confusing users.

**Phase:** P2. Fix immediately when Scheme is extended to carry constraints.

---

### Pitfall TC-13: Parser AST for Typeclass/Instance Declarations Not Unified with Module Decl

**What goes wrong:** LangThree's top-level is a list of `Decl` items. If `TypeclassDecl` and `InstanceDecl` are added as new union cases but are not handled in all visitors — `TypeCheck.fs`, `Bidir.fs`/top-level processing, `Eval.fs`, `Program.fs` pretty-printer, `--emit-ast` output — the F# compiler will silently ignore them via incomplete match warnings (or worse, match-all catch cases will swallow them).

**Prevention:**
- After adding `TypeclassDecl` and `InstanceDecl` to `Ast.fs`, immediately run `dotnet build` and treat ALL new incomplete-match warnings as blocking errors.
- Add a `failwith "TypeclassDecl not yet implemented"` stub in Eval.fs and TypeCheck.fs so that accidental paths through unimplemented code fail loudly at runtime rather than silently.
- The `--emit-ast` flag should print the new AST nodes verbatim — add cases to the Format.fs/AST printer immediately.

**Warning signs:**
- `typeclass` or `instance` declarations in source files are silently ignored.
- F# compiler emits "incomplete match" warnings that are suppressed by a catch-all.

**Phase:** P1. Structural issue that causes silent failures across all subsequent phases.

---

### Pitfall TC-14: Orphan Instance Confusion from Prelude Instances

**What goes wrong:** The Prelude loads `Show int`, `Eq int`, etc. as default instances. If user code in a `.fun` file also declares `instance Show int = ...`, there are two instances for the same type-class/type pair. This is incoherence. LangThree must enforce: one instance per (class, type) pair globally.

**Prevention:**
- Maintain the `InstanceEnv` as a `Map<string * string, InstanceInfo>` keyed by `(class_name, type_name)`.
- When adding a new instance, check for an existing entry and raise `E0XXX DuplicateInstance` if one exists.
- The Prelude's instances are loaded first (before user code); user code that re-declares a Prelude instance gets a clear error.

**Warning signs:**
- User accidentally re-declares a builtin instance and does not get an error — instead the last-declared instance silently wins.

**Phase:** P3. Needs to be part of instance registration from the beginning.

---

## PART D: Phase-Specific Warning Summary

| Phase | Topic | Most Likely Pitfall | Mitigation |
|-------|-------|--------------------|-|
| P1 | Parser/AST | LALR(1) conflicts from new keywords (TC-8) | Reserve keywords early; use `where` not `=` |
| P1 | AST | Silent swallow of new Decl variants (TC-13) | Add `failwith` stubs; treat warnings as errors |
| P2 | HM integration | Constraint not generalized with type vars (TC-1) | Extend Scheme to carry constraints from day one |
| P2 | HM integration | Type variable collision in instantiation (TC-11) | Single substitution pass for type + constraints |
| P2 | HM integration | Missing constraint formatting in errors (TC-12) | Extend formatSchemeNormalized immediately |
| P2 | HM integration | Mutable variable constraint leak (TC-7) | Extend mutableVars check to constraint generalization |
| P3 | Resolution | Eager resolution before unification (TC-2) | Deferred constraint solving; resolve after unification |
| P3 | Resolution | Superclass infinite loop (TC-6) | No superclass for v10.0; add depth limit if later |
| P3 | Resolution | Duplicate/orphan instances (TC-14) | InstanceEnv keyed by (class, type); error on duplicate |
| P3 | Resolution | Import system missing InstanceEnv (TC-9) | Thread InstanceEnv through import pipeline |
| P4 | Evaluator | Dictionary scope leak in higher-order functions (TC-3) | Elaboration or evidence-threaded call stack |
| P5 | Builtins | Dual dispatch incoherence for to_string/= (TC-4) | Decide migration strategy before P1 syntax |
| P6 | Constrained instances | Incomplete context reduction (TC-10) | Recursive subgoal resolution with depth limit |
| P2/P3 | Diagnostics | Ambiguous type variable errors (TC-5) | AmbiguousType diagnostic with constraint name and span |

---

## PART E: LangThree-Specific Integration Risks

These pitfalls are not general type-class pitfalls but arise specifically from LangThree's existing design decisions:

### Risk LT-1: `synth`/`check` Signature Explosion

`Bidir.synth` already takes `ctorEnv`, `recEnv`, `ctx`, `env`. Adding a `classEnv: ClassEnv` and `instanceEnv: InstanceEnv` parameter means every call site in Bidir.fs must be updated. LangThree has 67+ call sites for synth/check (per PROJECT.md "mutableVars avoids threading through 67+ synth/check call sites"). The same problem will recur for class/instance environments.

**Mitigation:** Use the same pattern as `mutableVars`: a module-level mutable ref for environments that do not change within a single inference pass. `let mutable classEnv: ClassEnv = Map.empty` in Bidir.fs, set at the top of each top-level binding check. Avoids threading through all call sites.

### Risk LT-2: GADT Branch Isolation vs. Constraint Propagation

LangThree's GADT support uses `isPolyExpected` per-branch isolation so each branch gets an independent expected type. If type class constraints are generated within a GADT branch, they must be solved with the GADT refinement in scope (the branch-local substitution). Constraints that escape a GADT branch may reference type variables that are only valid within that branch, causing resolution errors in the outer context.

**Mitigation:** Solve constraints locally within each GADT branch before merging branches. Do not defer constraints from GADT branches to the outer wanted set without first applying the branch's local substitution.

### Risk LT-3: `callValueRef` Pattern for Builtin Type Class Methods

LangThree uses a mutable `callValueRef` forward reference to let built-in functions invoke user closures (e.g., `Array.map`). If type class method dispatch at runtime needs to call a user-defined instance method, the same forward-reference pattern is needed — the evaluator must be wired before the instance dictionary is built. Failing to use this pattern causes `NullReferenceException` or stack overflows in F#.

**Mitigation:** When constructing the built-in instance dictionaries (e.g., a `DictValue` for `Show int`), the dictionary values must be `BuiltinValue` or `ClosureValue` that reference the evaluator via `callValueRef`, not direct F# functions that bypass the evaluator.

---

## Sources

- [GHC Instance Declarations and Resolution](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/instances.html) — authoritative on instance resolution algorithm and Paterson conditions
- [Implementing and Understanding Type Classes — okmij.org](https://okmij.org/ftp/Computation/typeclass.html) — dictionary passing mechanics, polymorphic recursion, constraint direction
- [Type Classes: Confluence, Coherence and Global Uniqueness — ezyang's blog](http://blog.ezyang.com/2014/07/type-classes-confluence-coherence-global-uniqueness/) — orphan instances and coherence
- [Type Classes in Haskell (Hall, Hammond, Peyton Jones)](https://dl.acm.org/doi/pdf/10.1145/227699.227700) — original qualified types + constraint generalization
- [Learn From Errors: Overlapping Instances — Serokell](https://serokell.io/blog/learn-from-errors-overlapping-instances) — practical overlapping instance errors
- [Coherence of Type Class Resolution (Bottu et al.)](https://xnning.github.io/papers/coherence-class.pdf) — formal treatment of superclass nondeterminism
- [Hindley-Milner with Constraints — Kwang's Haskell Blog](https://kseo.github.io/posts/2017-01-02-hindley-milner-inference-with-constraints.html) — constraint-based HM formulation
- [Monomorphism Restriction — HaskellWiki](https://wiki.haskell.org/Monomorphism_restriction) — constrained let generalization rules
