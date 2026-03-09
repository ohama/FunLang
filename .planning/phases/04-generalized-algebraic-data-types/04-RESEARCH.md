# Phase 4: Generalized Algebraic Data Types - Research

**Researched:** 2026-03-09
**Domain:** GADT type system implementation (type refinement, existential types, bidirectional checking)
**Confidence:** HIGH

## Summary

This phase extends LangThree's existing ADT infrastructure (Phase 2) with Generalized Algebraic Data Types (GADTs). GADTs generalize ordinary sum types by allowing each constructor to specify a distinct return type, enabling type refinement during pattern matching. The key implementation challenge is integrating GADT type refinement into the existing bidirectional type checker (Bidir.fs) while requiring mandatory type annotations on GADT match expressions (since full GADT inference is undecidable).

The standard approach, based on the Peyton Jones et al. "Simple unification-based type inference for GADTs" (ICFP 2006), distinguishes between "rigid" types (known from annotations) and "wobbly" types (inferred). Type refinement only applies to rigid types, making inference predictable. The existing codebase already has bidirectional checking with synth/check modes, ConstructorEnv with ArgType/ResultType, and unification -- all of which extend naturally to support GADTs.

The implementation touches 6 files (Ast.fs, Parser.fsy, Elaborate.fs, Bidir.fs, Unify.fs, Diagnostic.fs) plus Exhaustive.fs for GADT-aware exhaustiveness. The core changes are: (1) new constructor syntax with explicit return types, (2) local type equality constraints during pattern matching, (3) existential type variable scoping, and (4) mandatory annotation enforcement.

**Primary recommendation:** Extend ConstructorInfo with explicit return type constraints, add a local type refinement mechanism to Bidir.synth's Match case, and enforce mandatory type annotations on GADT match expressions.

## Standard Stack

This phase uses no new libraries. All implementation is within the existing F# / FsLexYacc / .NET stack.

### Core
| Component | Current | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| F# | .NET 10 | Implementation language | Already in use |
| FsLexYacc | In use | Parser/lexer generation | Already in use |
| Expecto | In use | Test framework | Already in use |

### No New Dependencies

GADT implementation is purely a type system extension. No external libraries needed. All changes are internal to the type checker, parser, and AST.

## Architecture Patterns

### Recommended Change Scope

```
src/LangThree/
  Ast.fs            # New GADT constructor declaration AST node
  Parser.fsy        # GADT constructor syntax: Name : ArgType -> ReturnType
  Lexer.fsl         # No changes needed (COLON already exists)
  Elaborate.fs      # Elaborate GADT constructors into ConstructorInfo with constraints
  Type.fs           # Extend ConstructorInfo with equality constraints
  Bidir.fs          # Type refinement in Match, annotation enforcement
  Unify.fs          # Local unification with constraint scoping
  Infer.fs          # Update inferPattern for GADT constructors
  Diagnostic.fs     # New error kinds for GADT-specific errors
  Exhaustive.fs     # GADT-aware impossible branch elimination
tests/
  GadtTests.fs      # Comprehensive GADT test suite
```

### Pattern 1: Rigid vs Wobbly Type Distinction

**What:** Types are classified as "rigid" (from annotations, fully known) or "wobbly" (inferred, uncertain). GADT type refinement only refines rigid types.
**When to use:** During GADT pattern matching, to decide whether to apply type equality constraints.
**How it maps to existing code:** The existing `check` mode in Bidir.fs provides rigid types (the expected type is known). The `synth` mode provides wobbly types. When a Match expression is in `check` mode, the expected type is rigid and refinement can occur. When in `synth` mode without annotation, emit an error.

**Implementation approach:**
```fsharp
// In Bidir.fs Match handling:
// When checking against a known type (rigid), apply GADT refinement
// When synthesizing without annotation, detect GADT and error

// Detect if a match involves GADT constructors
let isGadtMatch (ctorEnv: ConstructorEnv) (clauses: MatchClause list) =
    clauses |> List.exists (fun (pat, _) ->
        match pat with
        | ConstructorPat(name, _, _) ->
            match Map.tryFind name ctorEnv with
            | Some info -> info.IsGadt  // New field on ConstructorInfo
            | None -> false
        | _ -> false)
```

### Pattern 2: Local Type Equality Constraints in Pattern Branches

**What:** When matching a GADT constructor, the type checker learns local type equalities. These equalities are applied as a local substitution within the branch body only.
**When to use:** In each branch of a GADT pattern match, after determining the constructor.
**Why:** The key GADT feature: matching `Int n` in `type 'a expr = Int : int -> int expr` tells us `'a = int` in that branch.

**Implementation approach:**
```fsharp
// When matching constructor C : argTy -> T<concrete_types>
// against scrutinee of type T<type_vars>:
// 1. Unify concrete_types with type_vars to get local constraints
// 2. Apply these constraints to the branch body's type environment
// 3. Do NOT propagate these constraints outside the branch

// Example: scrutinee has type 'a expr, matching Int constructor
// Int : int -> int expr
// Unify 'a expr with int expr => local constraint: 'a = int
// In branch body, 'a is known to be int
```

### Pattern 3: Existential Type Variable Scoping

**What:** Constructor type variables that appear in arguments but NOT in the return type are existentially quantified. They must not escape the branch scope.
**When to use:** When elaborating GADT constructors and checking pattern match branches.

**Implementation approach:**
```fsharp
// In constructor: Pack : 'b * ('b -> string) -> packed_value
// 'b is existential (not in return type "packed_value")
// When matching Pack(x, f):
//   x : 'b_fresh (fresh type variable)
//   f : 'b_fresh -> string
// 'b_fresh must not appear in the branch result type

// Detection: compare TypeParams with free vars in ResultType
let existentialVars (info: ConstructorInfo) =
    let resultFreeVars = freeVars info.ResultType
    info.TypeParams |> List.filter (fun v -> not (Set.contains v resultFreeVars))
```

### Pattern 4: Mandatory Annotation Enforcement

**What:** GADT pattern matches require the programmer to provide a type annotation on the match expression or the enclosing function.
**When to use:** When a Match expression in synth mode encounters GADT constructors.
**Why:** Full GADT type inference is undecidable. Annotations make checking decidable and predictable.

**Implementation approach:**
```fsharp
// In Bidir.synth, Match case:
// If any constructor is a GADT constructor AND we're in synth mode (no expected type):
// raise error "GADTs require type annotations"

// Valid usage (annotation provides rigid type):
// (match e with Int n -> n + 1 | ... : int)
//   ^-- Annot wraps the match, providing expected type via check mode

// The annotation flows through Bidir.check -> Match case,
// where the expected type becomes the rigid return type
```

### Anti-Patterns to Avoid

- **Refining wobbly types:** Never apply GADT type equalities to inferred (wobbly) types. This leads to unsound type checking and order-dependent inference. Only refine when the type is known from an annotation.
- **Global constraint propagation:** GADT branch constraints are LOCAL. Never add them to the global substitution. They should be scoped to the branch body only.
- **Escaping existentials:** Existential type variables from GADT constructors must not appear in the result type of the match. Always check for escaping existentials after type-checking each branch.
- **Skipping occurs check for local constraints:** Even local GADT constraints need the occurs check to prevent infinite types.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| GADT type refinement | Custom constraint solver | Local substitution via existing `unify` | Unification already handles type equalities; just scope the substitution locally per branch |
| Annotation requirement detection | Ad-hoc GADT detection | Check ConstructorInfo.IsGadt flag | Centralize GADT detection in elaboration, not scattered through type checker |
| Existential variable tracking | Manual free variable tracking | Existing `freeVars` function in Type.fs | Already correctly computes free type variables |
| GADT exhaustiveness | Separate GADT exhaustiveness checker | Extend existing Maranget checker with impossible branch elimination | The existing checker handles constructors; just add branch pruning for impossible GADT constraints |

**Key insight:** The existing infrastructure (ConstructorEnv, unification, bidirectional checking, Maranget exhaustiveness) provides 80% of what GADTs need. The main new concept is LOCAL constraint scoping per branch -- everything else is composition of existing machinery.

## Common Pitfalls

### Pitfall 1: Not Distinguishing GADT vs Regular ADT Constructors
**What goes wrong:** If all constructors go through the same code path, regular ADT pattern matching might suddenly require annotations or behave differently.
**Why it happens:** GADT syntax is a superset of ADT syntax. A constructor like `Foo of int` in a `type t = ...` is both valid ADT and degenerate GADT syntax.
**How to avoid:** Add an `IsGadt` flag to ConstructorInfo during elaboration. A type is GADT if ANY constructor has a return type that instantiates type parameters differently from the type declaration head. If all constructors return `T<'a, 'b>` unchanged, it's a regular ADT.
**Warning signs:** Existing ADT tests start requiring annotations.

### Pitfall 2: Existential Type Variables Escaping Scope
**What goes wrong:** A match branch returns a value whose type mentions an existential type variable, leading to an unsound type.
**Why it happens:** Existential variables are fresh per branch but look like ordinary type variables.
**How to avoid:** After type-checking each branch body, verify that the result type doesn't contain any existential type variables from the constructor. Raise a clear error: "type variable would escape its scope".
**Warning signs:** Type variables appearing in inferred types that have no relation to the declared types.

### Pitfall 3: Order-Dependent Type Inference
**What goes wrong:** Type checking results change depending on which branches the checker processes first.
**Why it happens:** Without rigid/wobbly distinction, GADT refinements from one branch leak into the constraint set for other branches.
**How to avoid:** Process each branch independently with its own local constraint scope. Branch constraints should not affect the global substitution until after the branch body is fully checked.
**Warning signs:** Tests that pass when branches are reordered but fail in original order.

### Pitfall 4: Constructor Return Type Parsing Ambiguity
**What goes wrong:** Parser can't distinguish `Foo : int -> int expr` (GADT constructor) from other syntax.
**Why it happens:** The COLON token is already used for type annotations.
**How to avoid:** GADT constructor syntax should only appear within `type ... =` declarations. Inside a TypeDeclaration, after a constructor name (uppercase IDENT), a COLON signals GADT syntax: `Name : ArgType -> ReturnType`. The existing `Constructor` rule becomes: `IDENT COLON TypeExpr` for GADT constructors.
**Warning signs:** Parser shift-reduce conflicts.

### Pitfall 5: Breaking Existing ADT Semantics
**What goes wrong:** Changes to ConstructorInfo or pattern matching break the existing Phase 2 ADT functionality.
**Why it happens:** GADT changes touch the same data structures and code paths as regular ADTs.
**How to avoid:** Ensure backward compatibility by: (1) keeping existing ConstructorInfo fields unchanged, only adding new optional fields, (2) making IsGadt = false the default, (3) running all existing ADT/Record tests after each change.
**Warning signs:** Existing test failures in ExhaustiveTests.fs or IntegrationTests.fs.

### Pitfall 6: Incorrect GADT Exhaustiveness
**What goes wrong:** The exhaustiveness checker reports missing cases that are actually impossible due to GADT type constraints.
**Why it happens:** The Maranget algorithm doesn't know about type-level constraints -- it only sees constructor names.
**How to avoid:** After running standard Maranget check, filter out "missing" patterns whose type constraints are unsatisfiable. For example, if matching on `int expr`, the constructor `If : bool expr * ...` is impossible and shouldn't be reported as missing.
**Warning signs:** False exhaustiveness warnings on well-typed GADT matches.

## Code Examples

### GADT Declaration Syntax (Target)

```
type 'a expr =
    | Int : int -> int expr
    | Bool : bool -> bool expr
    | Add : int expr * int expr -> int expr
    | Eq : int expr * int expr -> bool expr
    | If : bool expr * 'a expr * 'a expr -> 'a expr
```

### GADT Pattern Match with Type Annotation (Target)

```
let eval (e : 'a expr) : 'a =
    match e with
    | Int n -> n                    // 'a refined to int, n : int
    | Bool b -> b                   // 'a refined to bool, b : bool
    | Add (x, y) -> eval x + eval y  // 'a refined to int
    | Eq (x, y) -> eval x = eval y   // 'a refined to bool
    | If (c, t, e) -> if eval c then eval t else eval e
```

### Existential Type Example (Target)

```
type showable =
    | Show : 'a * ('a -> string) -> showable

let show_it (s : showable) : string =
    match s with
    | Show (x, f) -> f x    // 'a is existential, can only use f on x
```

### AST Extension for GADT Constructor Declaration

```fsharp
// Extend ConstructorDecl to support explicit return types
// Option 1 (Recommended): Add returnType field
and ConstructorDecl =
    | ConstructorDecl of name: string * dataType: TypeExpr option * Span
    | GadtConstructorDecl of name: string * argTypes: TypeExpr list * returnType: TypeExpr * Span
```

### ConstructorInfo Extension

```fsharp
// Extend ConstructorInfo with GADT-specific fields
type ConstructorInfo = {
    TypeParams: int list
    ArgType: Type option
    ResultType: Type
    // New fields for GADT:
    IsGadt: bool                          // true if constructor has explicit return type
    LocalConstraints: (int * Type) list   // type equalities learned from return type
    ExistentialVars: int list             // type vars not in return type
}
```

### Local Type Refinement in Bidir.synth Match Case

```fsharp
// Pseudocode for GADT-aware match type checking
// In the Match case of Bidir.check (when expected type is known):

| Match (scrutinee, clauses, span) ->
    let s1, scrutTy = synth ctorEnv recEnv ctx env scrutinee

    let folder (s, idx) (pat, body) =
        match pat with
        | ConstructorPat(name, argPatOpt, _) ->
            match Map.tryFind name ctorEnv with
            | Some ctorInfo when ctorInfo.IsGadt ->
                // 1. Instantiate constructor with fresh vars
                let freshVars = ctorInfo.TypeParams |> List.map (fun _ -> freshVar())
                let subst = List.zip ctorInfo.TypeParams freshVars |> Map.ofList
                let ctorResultType = apply subst ctorInfo.ResultType

                // 2. Compute local constraints by unifying scrutinee type with constructor result type
                let localS = unifyWithContext ctx [] span (apply s scrutTy) ctorResultType

                // 3. Check for existential vars
                let existentials = ctorInfo.ExistentialVars |> List.map (fun v ->
                    match Map.tryFind v subst with
                    | Some (TVar n) -> n
                    | _ -> v)

                // 4. Apply local constraints to branch environment
                let branchEnv = applyEnv localS (applyEnv s env)
                // Add pattern variable bindings...

                // 5. Check branch body with refined expected type
                let branchExpected = apply localS (apply s expected)
                let s' = check ctorEnv recEnv ctx branchEnv body branchExpected

                // 6. Verify existentials don't escape
                let resultTy = apply s' branchExpected
                for ev in existentials do
                    if Set.contains ev (freeVars resultTy) then
                        raise (TypeException { Kind = ExistentialEscape ev; ... })

                (compose s' (compose localS s), idx + 1)

            | _ ->
                // Regular ADT constructor - existing code path
                // ...
        | _ ->
            // Non-constructor pattern - existing code path
            // ...

    let finalS, _ = List.fold folder (s1, 0) clauses
    finalS
```

### Parser Grammar for GADT Constructors

```yacc
Constructor:
    | IDENT                             { Ast.ConstructorDecl($1, None, symSpan parseState 1) }
    | IDENT OF TypeExpr                 { Ast.ConstructorDecl($1, Some $3, ruleSpan parseState 1 3) }
    // NEW: GADT constructor with explicit return type
    | IDENT COLON TypeExpr              { Ast.GadtConstructorDecl($1, [], $3, ruleSpan parseState 1 3) }
    | IDENT COLON TypeExpr ARROW TypeExpr
        { Ast.GadtConstructorDecl($1, [$3], $5, ruleSpan parseState 1 5) }
    // Multi-arg GADT: Name : T1 * T2 -> ReturnType
    | IDENT COLON TupleType ARROW TypeExpr
        { let args = match $3 with TETuple ts -> ts | t -> [t]
          Ast.GadtConstructorDecl($1, args, $5, ruleSpan parseState 1 5) }
```

### Diagnostic Extensions

```fsharp
// New error kinds in Diagnostic.fs
type TypeErrorKind =
    // ... existing ...
    | GadtAnnotationRequired of scrutineeType: string
    | ExistentialEscape of varName: string
    | GadtConstraintMismatch of expected: Type * actual: Type * constructor: string
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Wobbly/rigid types (Peyton Jones 2006) | OutsideIn(X) with local assumptions (Vytiniotis 2011) | 2011 | OutsideIn is more principled but more complex; wobbly/rigid is simpler for a teaching/small language |
| No GADT inference, annotations everywhere | Partial inference with principal types (Garrigue 2013) | 2013 | OCaml's approach allows more inference; GHC still requires more annotations |

**For LangThree:** Use the simpler wobbly/rigid approach (Peyton Jones 2006). The codebase already has bidirectional checking which naturally provides rigid types via `check` mode. This is sufficient and simpler than OutsideIn(X).

## Open Questions

1. **GADT constructor syntax: F# style vs OCaml style**
   - What we know: OCaml uses `Int : int -> int term`. F# doesn't have GADTs natively.
   - What's unclear: Whether to use OCaml's colon syntax or invent an F#-flavored syntax.
   - Recommendation: Use OCaml's colon syntax (`Int : int -> int expr`) since F# has no native GADT syntax to follow. The COLON token already exists in the lexer. The user decision mentions "F# style" but for GADTs specifically there is no F# style, so OCaml syntax (which is the closest ML family reference) is appropriate.

2. **Tuple arguments in GADT constructors**
   - What we know: OCaml GADT constructors can have multiple arguments: `Add : int expr * int expr -> int expr`.
   - What's unclear: Whether to support multiple arguments as a tuple or require single-argument constructors.
   - Recommendation: Support tuple arguments since the existing ADT uses `of TypeExpr` which can be a tuple type. GADT constructors should similarly support `Name : T1 * T2 -> RetType`.

3. **Interaction with polymorphic recursion**
   - What we know: Evaluating GADTs often requires polymorphic recursion (the recursive call is at a different type instantiation).
   - What's unclear: Whether the current LetRec implementation supports polymorphic recursion.
   - Recommendation: The current LetRec pre-binds with a fresh type variable and then generalizes. This should handle polymorphic recursion if the function has a type annotation. Verify with test case.

4. **GADT exhaustiveness: how deep to go**
   - What we know: Full GADT exhaustiveness with impossible branch elimination requires solving type constraint satisfiability.
   - What's unclear: How complex the constraint satisfiability check needs to be.
   - Recommendation: Start with a simple approach -- only eliminate branches where the constructor's return type head doesn't match the scrutinee type head (e.g., `bool expr` constructor is impossible when matching `int expr`). This covers the common case without a full constraint solver.

## Sources

### Primary (HIGH confidence)
- Peyton Jones et al., "Simple unification-based type inference for GADTs" (ICFP 2006) -- core algorithm for rigid/wobbly distinction
- [OCaml GADT tutorial](https://ocaml.org/manual/5.2/gadts-tutorial.html) -- syntax, semantics, locally abstract types
- [Real World OCaml - GADTs chapter](https://dev.realworldocaml.org/gadts.html) -- practical patterns, existential types, pitfalls
- Existing codebase analysis: Ast.fs, Type.fs, Bidir.fs, Unify.fs, Elaborate.fs, Infer.fs, Exhaustive.fs

### Secondary (MEDIUM confidence)
- [GHC User's Guide - GADTs](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/gadt.html) -- GHC's pattern matching requirements (rigid scrutinee, result type)
- [Bidirectional Typing survey](https://dl.acm.org/doi/fullHtml/10.1145/3450952) -- Dunfield & Krishnaswami, comprehensive reference
- Vytiniotis et al., "OutsideIn(X)" (JFP 2011) -- more principled but complex approach (not recommended for LangThree)

### Tertiary (LOW confidence)
- [Scala 3 GADTs internals](https://www.scala-lang.org/api/3.3.4/docs/docs/internals/gadts.html) -- implementation notes from Scala team
- [Garrigue & Remy, "Ambivalent types for principal type inference with GADTs"](http://gallium.inria.fr/~remy/gadts/Garrigue-Remy:gadts@aplas2013.pdf) -- OCaml's approach, advanced

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new dependencies, pure type system extension on well-understood codebase
- Architecture: HIGH - clear mapping from academic literature to existing code structure; Bidir.fs/Unify.fs extension points identified
- Pitfalls: HIGH - well-documented in academic literature and compiler implementations; escaping existentials, order-dependence, annotation requirements all well-understood
- Code examples: MEDIUM - pseudocode based on codebase analysis, not yet validated by compilation

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable domain, academic foundations unchanged)
