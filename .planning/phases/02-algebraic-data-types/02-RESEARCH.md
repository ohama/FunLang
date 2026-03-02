# Phase 2: Algebraic Data Types - Research

**Researched:** 2026-03-02
**Domain:** Type system extension (Algebraic Data Types with pattern matching)
**Confidence:** HIGH

## Summary

This research investigates how to implement Algebraic Data Types (ADTs, also called discriminated unions in F#) in a functional language compiler with Hindley-Milner type inference. The investigation covered AST extensions, type system modifications, pattern exhaustiveness checking algorithms, and recursive type handling.

The standard approach uses a three-layer implementation: (1) extend AST and parser for `type T = C1 | C2 of ty` syntax, (2) extend type system with named sum types and constructor environments, (3) implement exhaustiveness checking using the Maranget usefulness algorithm. The existing Hindley-Milner inference with occurs check already handles recursive types correctly.

F# syntax uses `type Option = None | Some of 'a` for simple ADTs, with `type ... and ...` for mutually recursive definitions. Type parameters use apostrophe notation ('a, 'b). Pattern matching exhaustiveness is checked using the usefulness algorithm: a pattern is useful if it can match values not covered by previous patterns.

**Primary recommendation:** Extend AST/Type modules incrementally (ADT declarations → constructor environment → exhaustiveness checking), reusing existing pattern matching infrastructure with Maranget usefulness algorithm for warnings.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| FsLexYacc | (current) | Parser generation for F# syntax | Already in use, handles indentation via IndentFilter |
| Hindley-Milner | (existing) | Type inference with let-polymorphism | Already implemented in Infer.fs/Type.fs |
| Occurs check | (existing) | Prevents infinite types in unification | Already in Unify.fs, handles recursive types |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| N/A | - | No external libraries needed | Core compiler extensions only |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Maranget algorithm | Backtracking exhaustiveness | Maranget is O(n*m), backtracking is exponential |
| Named types | Structural ADTs | F# uses nominal typing for discriminated unions |
| Decision trees | Naive pattern compilation | Decision trees optimize runtime, but not needed for Phase 2 |

**Installation:**
No new dependencies. Use existing F# compiler and FsLexYacc toolchain.

## Architecture Patterns

### Recommended Project Structure
```
src/LangThree/
├── Ast.fs           # Add TypeDecl, extend Decl for type declarations
├── Type.fs          # Add TData constructor, ConstructorEnv
├── Parser.fsy       # Add type declaration grammar rules
├── Lexer.fsl        # Add TYPE, OF, AND keywords (if not present)
├── Infer.fs         # Extend for constructor checking
├── Elaborate.fs     # Convert TypeExpr to Type for ADT parameters
├── Exhaustive.fs    # NEW: Exhaustiveness checking module
└── Eval.fs          # Add DataValue variant for runtime
```

### Pattern 1: AST Extension for Type Declarations

**What:** Add type declaration nodes to AST supporting F# discriminated union syntax.

**When to use:** First step before type system changes.

**Example:**
```fsharp
// F# syntax target:
// type Option = None | Some of 'a
// type Tree = Leaf | Node of Tree * int * Tree

// AST representation:
type TypeDecl =
    | TypeDecl of name: string * typeParams: string list * constructors: Constructor list * Span

and Constructor =
    | Constructor of name: string * dataType: TypeExpr option * Span

// Extend Decl:
type Decl =
    | LetDecl of name: string * body: Expr * Span
    | TypeDecl of TypeDecl  // NEW
```

### Pattern 2: Type System Extension

**What:** Add TData type constructor and maintain constructor environment.

**When to use:** After AST extension, before type checking.

**Example:**
```fsharp
// In Type.fs
type Type =
    | TInt | TBool | TString
    | TVar of int
    | TArrow of Type * Type
    | TTuple of Type list
    | TList of Type
    | TData of name: string * typeArgs: Type list  // NEW

// Constructor environment: constructor name -> (type params, arg type option, result type)
type ConstructorInfo = {
    TypeParams: int list        // Type variables
    ArgType: Type option        // None for constant constructors
    ResultType: Type            // Always TData
}

type ConstructorEnv = Map<string, ConstructorInfo>
```

### Pattern 3: Exhaustiveness Checking (Maranget Algorithm)

**What:** Usefulness-based algorithm to detect missing and redundant patterns.

**When to use:** After patterns are type-checked.

**Example:**
```fsharp
// Usefulness algorithm: Is pattern p useful given previous patterns?
let rec useful (constructors: string list) (prevPatterns: Pattern list) (p: Pattern) : bool =
    match prevPatterns with
    | [] -> true  // First pattern always useful
    | _ ->
        // Check if p can match values not matched by prevPatterns
        // Algorithm: try all constructors, check if p specializes differently
        // See Maranget 2007 "Warnings for pattern matching"

// Exhaustiveness: Check if wildcard is useful
let checkExhaustive (ty: Type) (patterns: Pattern list) : Pattern list option =
    if useful (getConstructors ty) patterns WildcardPat then
        Some (computeMissingPatterns ty patterns)  // Non-exhaustive
    else
        None  // Exhaustive

// Redundancy: Check each pattern against previous ones
let checkRedundant (patterns: Pattern list) : int list =
    patterns
    |> List.mapi (fun i p ->
        let prev = patterns |> List.take i
        if not (useful (getConstructorsFromPat p) prev p) then Some i else None)
    |> List.choose id
```

### Pattern 4: Recursive Types with Occurs Check

**What:** Use existing occurs check to prevent infinite types, allow recursive definitions.

**When to use:** During unification when type checking recursive ADTs.

**Example:**
```fsharp
// Recursive type example:
// type List = Nil | Cons of int * List
//
// When checking Cons(1, Nil):
// 1. Cons has type: int * List -> List
// 2. Unification: unify (int * List) (int * List) succeeds
// 3. Occurs check prevents: 'a = 'a -> int (infinite type)

// Existing Unify.fs already handles this:
let occurs (v: int) (t: Type): bool =
    Set.contains v (freeVars t)

// When unifying TVar n with t:
if occurs n t then
    raise (TypeException { Kind = OccursCheck (n, t); ... })
else
    singleton n t
```

### Pattern 5: Mutually Recursive Types

**What:** Handle `type T = ... and U = ...` syntax for interdependent types.

**When to use:** When types reference each other.

**Example:**
```fsharp
// F# syntax:
// type Tree = Empty | Node of int * Forest
// and Forest = Nil | Cons of Tree * Forest

// Parser grammar (Parser.fsy):
TypeDeclaration:
    | TYPE IDENT TypeParams EQUALS Constructors TypeDeclContinuation
        { ($2, $3, $5) :: $6 }

TypeDeclContinuation:
    | (* empty *)  { [] }
    | AND TypeDeclaration  { $2 }

// Type checking: Process all declarations in group simultaneously
// to handle forward references
let rec checkMutualTypes (decls: TypeDecl list) (env: TypeEnv) : TypeEnv =
    // 1. Add all type names with placeholder schemes
    let env' = decls |> List.fold (fun e (TypeDecl(name, _, _, _)) ->
        Map.add name (Scheme([], TData(name, []))) e) env

    // 2. Check each type body with all names in scope
    decls |> List.fold (fun e decl -> checkTypeDecl decl e) env'
```

### Anti-Patterns to Avoid

- **Structural equality for ADTs:** F# uses nominal typing - two types with same constructors are distinct unless they're the same named type.
- **Catch-all patterns in libraries:** Adding `| _ -> ...` prevents exhaustiveness warnings when new constructors are added.
- **Eager decision tree compilation:** Phase 2 only needs exhaustiveness warnings, not optimized runtime compilation.
- **Custom unification for recursive types:** Existing occurs check already prevents infinite types correctly.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Exhaustiveness checking | Ad-hoc constructor counting | Maranget usefulness algorithm | Handles nested patterns, OR patterns, wildcards correctly. Ad-hoc fails on `Some (_, _)` vs `Some (1, 2)` |
| Pattern compilation | Custom backtracking | Existing pattern matching in Infer.fs | Already handles ConsPat, TuplePat, ConstPat with type inference |
| Recursive type checking | Custom cycle detection | Existing occurs check in Unify.fs | Occurs check prevents `'a = 'a list` infinite types during unification |
| Missing pattern computation | Generate all combinations | Constructor splitting (Maranget) | Exponential blowup on large ADTs - Maranget groups efficiently |

**Key insight:** Pattern exhaustiveness is NP-complete for some pattern features, but Maranget's algorithm makes it practical by grouping constructors and using usefulness as the central concept rather than generating all possible values.

## Common Pitfalls

### Pitfall 1: Constructor Name Collision with Variables

**What goes wrong:** Treating constructors as variables in pattern matching.

**Why it happens:** In F#/OCaml, constructors must start with uppercase, variables with lowercase, but parser may not enforce this.

**How to avoid:** Maintain separate constructor environment. During pattern type checking, look up uppercase identifiers in ConstructorEnv first.

**Warning signs:** Type errors like "Option has type 'a -> Option but is used as a value" when constructor used in expression position without argument.

### Pitfall 2: Type Parameter Scope in Nested Patterns

**What goes wrong:** Type variable 'a in `Some 'a` unifies differently in different match branches.

**Why it happens:** Type variables are scoped per let-binding, not per pattern.

**How to avoid:** Instantiate constructor type schemes fresh for each pattern occurrence, just like function types.

**Warning signs:** Patterns like `match x with Some 1 -> ... | Some "hi" -> ...` incorrectly type-checking.

### Pitfall 3: Recursive Type Instantiation

**What goes wrong:** `type List = Nil | Cons of int * List` fails to unify during constructor application.

**Why it happens:** Forgetting to substitute type name with TData during elaboration.

**How to avoid:** When elaborating TypeExpr in constructor data types, replace type name references with TData constructor.

**Warning signs:** Error "type List is unbound" when it was just declared.

### Pitfall 4: Mutually Recursive Type Ordering

**What goes wrong:** `type Tree = Node of Forest and Forest = ...` fails with "Forest not found."

**Why it happens:** Processing type declarations sequentially instead of as a group.

**How to avoid:** Parse `type ... and ...` as a list of declarations, add all names to environment before checking bodies.

**Warning signs:** Forward reference errors in mutually recursive type groups.

### Pitfall 5: Exhaustiveness Warning on Infinite Types

**What goes wrong:** Exhaustiveness checker runs forever on `type Nat = Zero | Succ of Nat`.

**Why it happens:** Trying to enumerate all possible patterns instead of using constructor coverage.

**How to avoid:** Usefulness algorithm checks constructor coverage, not value enumeration. For `Nat`, patterns `Zero` and `Succ _` are exhaustive regardless of recursion.

**Warning signs:** Checker timeout or stack overflow on recursive types.

## Code Examples

Verified patterns from research:

### Type Declaration Syntax (F# Standard)

```fsharp
// Source: F# language specification, F# for Fun and Profit
// Simple ADT with type parameter
type Option<'a> = None | Some of 'a

// Multiple type parameters
type Result<'a, 'b> = Ok of 'a | Error of 'b

// Recursive type
type Tree<'a> = Empty | Node of Tree<'a> * 'a * Tree<'a>

// Mutually recursive types
type Expr =
    | Literal of int
    | Arith of ArithExpr
and ArithExpr = { left: Expr; op: string; right: Expr }
```

### Pattern Exhaustiveness (Maranget 2007)

```fsharp
// Source: "Warnings for pattern matching" - Luc Maranget, JFP 2007
// Usefulness algorithm pseudocode:

// U([], q) = true if q matches anything
// U(p::ps, q) = specialization-based recursion

// For constructor pattern:
// U((C p1..pn)::ps, C q1..qn) = U(p1::..::pn::specialize(C, ps), q1::..::qn)
// U((C' p1..pm)::ps, C q1..qn) = U(ps, C q1..qn) if C ≠ C'

// Exhaustiveness: U(patterns, _) = false means exhaustive

// Example: type Option = None | Some of 'a
// Patterns: [Some x]
// Check: U([Some x], _)
//   = try all constructors {None, Some}
//   = U([Some x], None) || U([Some x], Some _)
//   = U([], None)        (Some ≠ None, skip)
//   = true (None not covered)
// Result: NON-EXHAUSTIVE, missing None
```

### Constructor Environment Setup

```fsharp
// Type declaration:
// type Option = None | Some of 'a

// Constructor environment entries:
// "None" -> { TypeParams = [0]; ArgType = None; ResultType = TData("Option", [TVar 0]) }
// "Some" -> { TypeParams = [0]; ArgType = Some(TVar 0); ResultType = TData("Option", [TVar 0]) }

// When pattern matching:
match opt with
| None -> ...      // Instantiate: TData("Option", [TVar 1000]) (fresh)
| Some x -> ...    // Instantiate: TVar 1000 -> TData("Option", [TVar 1000])
                   // Bind x : TVar 1000
```

### Recursive Type Inference

```fsharp
// Source: OCaml Programming: Correct + Efficient + Beautiful
// Type declaration:
// type IntList = Nil | Cons of int * IntList

// During type checking of: Cons(1, Nil)
// 1. Look up "Cons" in ConstructorEnv:
//    Cons : int * IntList -> IntList
//    (desugar to) int * TData("IntList", []) -> TData("IntList", [])
//
// 2. Type check arguments:
//    1 : int
//    Nil : TData("IntList", [])
//
// 3. Unify:
//    (int, TData("IntList", [])) ~ (int * IntList) -> succeed
//    Result type: TData("IntList", [])
//
// No occurs check triggered because:
// - We're unifying concrete types TData("IntList", [])
// - Not unifying type variable with itself
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Backtracking exhaustiveness | Maranget usefulness algorithm | 2007 (JFP paper) | Polynomial time instead of exponential |
| Enumerate all patterns | Constructor coverage checking | 2007 | Handles infinite recursive types |
| Separate redundancy check | Unified usefulness algorithm | 2007 | Same algorithm detects both exhaustiveness and redundancy |
| Decision tree compilation in parser | Separate compilation phase | 2008 (Maranget ML'08) | Parser only checks exhaustiveness, runtime uses optimized trees |

**Deprecated/outdated:**
- **Manual pattern matrix construction:** Modern implementations use direct AST traversal with usefulness predicate
- **Separate algorithms for GADTs:** Generic algorithm (EPFL 2016) handles both ADTs and GADTs uniformly
- **ASCII type variables (a, b, c):** F# uses apostrophe notation ('a, 'b) consistently

## Open Questions

Things that couldn't be fully resolved:

1. **Should constructor arity be enforced at parse time or type checking time?**
   - What we know: F# enforces at type checking (parser accepts `Some 1 2`, checker rejects)
   - What's unclear: Performance tradeoff between early vs late checking
   - Recommendation: Follow F# - type checking phase for better error messages with type context

2. **Missing pattern representation for error messages**
   - What we know: Maranget algorithm computes usefulness but doesn't construct witness patterns
   - What's unclear: How to format "missing patterns: None | Some _" from usefulness matrix
   - Recommendation: Implement simple pattern reconstruction from constructor coverage gaps (Phase 2.1 if needed)

3. **Integration with existing Match expression inference**
   - What we know: Infer.fs has Match case that calls inferPattern, unifies scrutinee with patterns
   - What's unclear: Whether exhaustiveness checking runs before or after type inference
   - Recommendation: After type inference (need types to know which constructors are valid), add exhaustiveness pass to Bidir.synth

## Sources

### Primary (HIGH confidence)
- [OCaml Programming: Correct + Efficient + Beautiful - Algebraic Data Types](https://cs3110.github.io/textbook/chapters/data/algebraic_data_types.html) - ADT semantics, pattern matching, type parameters
- [Rust Compiler Dev Guide - Pattern Exhaustiveness](https://rustc-dev-guide.rust-lang.org/pat-exhaustive-checking.html) - Usefulness algorithm description, constructor splitting
- [Luc Maranget - Warnings for Pattern Matching (JFP 2007)](http://moscova.inria.fr/~maranget/papers/warn/index.html) - Original exhaustiveness algorithm
- [F# for Fun and Profit - Types Overview](https://fsharpforfunandprofit.com/posts/overview-of-types-in-fsharp/) - F# discriminated union syntax
- Existing codebase: Type.fs, Infer.fs, Unify.fs - Hindley-Milner implementation with occurs check

### Secondary (MEDIUM confidence)
- [Wikipedia - Mutual Recursion](https://en.wikipedia.org/wiki/Mutual_recursion) - Mutually recursive types with `type and` syntax, verified against F# docs
- [Hindley-Milner Type System (Wikipedia)](https://en.wikipedia.org/wiki/Hindley%E2%80%93Milner_type_system) - Generalization and let-polymorphism for type parameters
- [PutridParrot - Mutual Recursion in F#](https://putridparrot.com/blog/mutually-recursion-in-f/) - F# mutually recursive type examples

### Tertiary (LOW confidence)
- Generic exhaustiveness algorithm (EPFL 2016) - Paper exists but couldn't access full content, algorithm complexity unclear
- Decision tree compilation (Maranget ML'08) - Not needed for Phase 2, deferred research

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Existing F# toolchain (FsLexYacc, Hindley-Milner) already in use
- Architecture: HIGH - AST/Type/Infer extension pattern established in Phase 1, Maranget algorithm well-documented
- Pitfalls: HIGH - Common issues documented across OCaml/F# community, verified against language specs
- Code examples: MEDIUM - Syntax verified from F# docs, algorithm pseudocode from papers (not executable)

**Research date:** 2026-03-02
**Valid until:** 2026-04-01 (30 days - stable domain, type system theory changes slowly)
