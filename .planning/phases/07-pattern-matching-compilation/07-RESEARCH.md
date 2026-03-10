# Phase 7: Pattern Matching Compilation - Research

**Researched:** 2026-03-10
**Domain:** Pattern matching compilation to decision trees (compiler internals)
**Confidence:** HIGH

## Summary

This phase compiles LangThree's `match` expressions from naive sequential pattern testing (try each clause top-to-bottom) into binary decision trees that never perform redundant constructor tests. The reference algorithm is Jules Jacobs' "How to compile pattern matching" (2021), which produces binary match# nodes by splitting clauses into cases based on whether they match a tested constructor, don't match, or are agnostic (wildcard/variable).

The current implementation in `Eval.fs` uses `matchPattern` (recursive pattern-value matching returning bindings) and `evalMatchClauses` (sequential clause testing). This must be replaced with a compilation pass that transforms `Match(scrutinee, clauses, span)` AST nodes into a `DecisionTree` intermediate representation, which is then evaluated or interpreted. The existing `Exhaustive.fs` module (Maranget usefulness algorithm) handles exhaustiveness/redundancy checking separately and should be preserved -- decision tree generation is an optimization of evaluation, not a replacement for static checking.

The implementation is entirely within the F#/.NET codebase with no external library dependencies. The core work is: (1) define a `DecisionTree` discriminated union type, (2) implement the compilation algorithm with clause splitting and heuristic variable selection, (3) write a decision tree evaluator, and (4) verify semantic equivalence with the existing naive evaluator.

**Primary recommendation:** Add a new `MatchCompile.fs` module that compiles `Match` expressions to binary decision trees using Jules Jacobs' clause-splitting algorithm, then modify `Eval.fs` to evaluate the decision tree instead of sequential clause testing.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| F# / .NET | net10.0 | Implementation language | Already the project language |
| Expecto | (existing) | Test framework | Already used for all project tests |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FsCheck | (if needed) | Property-based testing | Verify semantic equivalence between naive and compiled matching |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Binary decision tree (Jacobs) | Backtracking automata (classic SPJ) | Jacobs is simpler, avoids code duplication issues |
| Binary decision tree | Decision DAG with hash consing | DAG is optimization; start with tree, can compress later |
| New IR pass | Inline in evaluator | Separate module is cleaner, testable independently |

## Architecture Patterns

### Recommended Project Structure
```
src/LangThree/
├── MatchCompile.fs   # NEW: Decision tree types + compilation algorithm
├── Eval.fs           # MODIFIED: evaluate decision trees instead of sequential clauses
├── Exhaustive.fs     # UNCHANGED: static exhaustiveness/redundancy checking
├── AST.fs            # UNCHANGED: Pattern, MatchClause types
└── Type.fs           # UNCHANGED: type definitions
```

### Pattern 1: Decision Tree Data Type

**What:** A discriminated union representing compiled match decisions
**When to use:** Output of match compilation, input to evaluation

```fsharp
/// Compiled decision tree for pattern matching
/// Represents binary decisions: test one constructor at a time
type DecisionTree =
    /// Leaf: match succeeded, evaluate body expression with bindings
    | Leaf of bindings: (string * AccessPath) list * bodyIndex: int
    /// Fail: no clause matched (should not occur after exhaustiveness check)
    | Fail
    /// Switch: test scrutinee at path for constructor, branch on result
    | Switch of
        testVar: AccessPath *
        constructor: string *
        arity: int *
        onMatch: DecisionTree *    // constructor matches: bind sub-values, continue
        onElse: DecisionTree        // constructor doesn't match: try next test

/// Path to access a sub-value within the scrutinee
/// e.g., Root is the scrutinee itself; Field(Root, 0) is first constructor arg
and AccessPath =
    | Root
    | Field of parent: AccessPath * index: int
```

### Pattern 2: Clause Representation for Compilation

**What:** Internal representation of match clauses during compilation
**When to use:** Input to the compilation algorithm

```fsharp
/// A clause under compilation: a row in the pattern matrix
type MatchRow = {
    /// Pattern constraints remaining to be tested (variable -> pattern)
    Patterns: Map<AccessPath, Pattern>
    /// When-guard expression (if any)
    Guard: Expr option
    /// Index of the original clause (for body lookup)
    ClauseIndex: int
}
```

### Pattern 3: Jules Jacobs Clause Splitting (Cases a/b/c)

**What:** When testing variable `v` against constructor `C(p1,...,pn)`, split remaining clauses into three cases
**When to use:** Core of the compilation algorithm

The algorithm works as follows:
1. Pick a test variable `v` and constructor `C` to test
2. For each remaining clause, classify it:
   - **Case (a):** Clause has pattern `C(q1,...,qn)` at position `v` -- goes into "match" branch with sub-patterns expanded
   - **Case (b):** Clause has a different constructor `D(...)` at position `v` -- goes into "else" branch only
   - **Case (c):** Clause has wildcard/variable at position `v` -- goes into BOTH branches (this is the source of potential clause duplication)
3. Recursively compile each branch

```fsharp
/// Split clauses based on testing variable `testVar` for constructor `ctor`
let splitClauses (testVar: AccessPath) (ctor: string) (arity: int)
                 (clauses: MatchRow list) : MatchRow list * MatchRow list =
    let matchBranch = ResizeArray()
    let elseBranch = ResizeArray()
    for clause in clauses do
        match Map.tryFind testVar clause.Patterns with
        | Some (ConstructorPat(name, argPat, _)) when name = ctor ->
            // Case (a): constructor matches, expand sub-patterns
            let expanded = expandConstructorPattern testVar arity argPat clause
            matchBranch.Add(expanded)
        | Some (ConstructorPat _) ->
            // Case (b): different constructor, only goes to else branch
            elseBranch.Add(clause)
        | Some (VarPat _ | WildcardPat _) | None ->
            // Case (c): wildcard/variable, goes to BOTH branches
            matchBranch.Add(clause)
            elseBranch.Add(clause)
        | Some _ ->
            // Other pattern types (const, cons, etc.) -- handle per type
            matchBranch.Add(clause)
            elseBranch.Add(clause)
    matchBranch |> Seq.toList, elseBranch |> Seq.toList
```

### Pattern 4: Heuristic Test Variable Selection

**What:** Select the (variable, constructor) pair to test that minimizes clause duplication
**When to use:** At each compilation step to choose what to test next

The heuristic from Jules Jacobs: select the test that is present in the maximum number of other clauses. This minimizes the number of Case (c) clauses (which get duplicated into both branches).

```fsharp
/// Select the best (variable, constructor) to test next
/// Heuristic: maximize number of clauses that have a constructor
/// at this variable position (minimizes wildcard duplication)
let selectTest (clauses: MatchRow list) : AccessPath * string * int =
    // Collect all (variable, constructor, arity) candidates
    let candidates =
        clauses
        |> List.collect (fun row ->
            row.Patterns
            |> Map.toList
            |> List.choose (fun (path, pat) ->
                match pat with
                | ConstructorPat(name, argPat, _) ->
                    let arity = if argPat.IsSome then 1 else 0
                    Some (path, name, arity)
                | _ -> None))
        |> List.distinct

    // Score each candidate: count clauses that have ANY constructor at that variable
    candidates
    |> List.maxBy (fun (path, _, _) ->
        clauses
        |> List.filter (fun row ->
            match Map.tryFind path row.Patterns with
            | Some (ConstructorPat _) -> true
            | _ -> false)
        |> List.length)
```

### Anti-Patterns to Avoid

- **Modifying the AST:** Do NOT add decision tree nodes to the Expr AST type. Keep the decision tree as a separate IR consumed only by the evaluator.
- **Replacing Exhaustive.fs:** The existing Maranget-based exhaustiveness checking should remain separate. Decision tree compilation is an evaluation optimization, not a replacement for static analysis.
- **Compiling at parse time:** Compilation should happen at evaluation time (or as a separate compilation step between type checking and evaluation), since it needs constructor arity information from the environment.
- **Ignoring when-guards:** Guards complicate decision trees. A clause with a guard should be treated as: if the pattern matches AND the guard is true, take this branch; otherwise fall through to the next applicable clause. Model this with a `GuardedLeaf` node.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Exhaustiveness checking | Custom check in decision tree | Existing `Exhaustive.fs` | Already implements Maranget algorithm correctly |
| Pattern-to-bindings extraction | New binding extraction | Existing `matchPattern` in Eval.fs | Reuse for leaf evaluation |
| Type-based constructor lookup | Manual constructor tracking | Existing `ConstructorEnv` from Type.fs | Already tracks all ADT constructors with arity |
| Decision DAG optimization | Hash consing from start | Plain decision tree first | Premature optimization; tree is correct and simpler |

**Key insight:** The decision tree compilation is purely an evaluation optimization. All static checking (types, exhaustiveness, redundancy) remains unchanged. The new module only needs to produce a tree that the evaluator walks at runtime.

## Common Pitfalls

### Pitfall 1: Clause Duplication Explosion
**What goes wrong:** Case (c) wildcards duplicate clauses into both branches, leading to exponential tree size for pathological patterns
**Why it happens:** Every wildcard at a test position copies the clause into both match and else branches
**How to avoid:** The heuristic (maximize constructor-tested clauses) directly minimizes duplication. For LangThree's use cases (ADTs, lists, tuples), this is sufficient. If tree size becomes a concern later, apply hash consing to compress to a DAG.
**Warning signs:** Decision trees with many thousands of nodes for simple patterns

### Pitfall 2: Forgetting Non-Constructor Patterns
**What goes wrong:** The algorithm handles `ConstructorPat` well but crashes or miscompiles `ConstPat`, `ConsPat`, `EmptyListPat`, `TuplePat`, `RecordPat`
**Why it happens:** Jules Jacobs' paper focuses on ADT constructors; the LangThree pattern language is richer
**How to avoid:** Map each pattern type to constructor-like representation before compilation:
- `ConstPat(IntConst n)` -> treat as constructor named `#int_n` with arity 0
- `ConsPat` -> constructor `::` with arity 2
- `EmptyListPat` -> constructor `[]` with arity 0
- `TuplePat(ps)` -> constructor `#tuple_N` with arity N
- `RecordPat(fields)` -> treat as constructor with arity = number of fields
- `VarPat`, `WildcardPat` -> wildcard (matches everything)
**Warning signs:** Match expressions with list/tuple patterns producing wrong results

### Pitfall 3: When-Guard Semantics
**What goes wrong:** Guards are compiled away during tree construction, losing the fallthrough semantics
**Why it happens:** A guarded clause `| C x when x > 0 -> ...` must fall through to subsequent clauses if the guard fails, but the decision tree may have already committed to the C branch
**How to avoid:** Model guarded leaves as `GuardedLeaf(bindings, guard, bodyIndex, fallthrough)` where `fallthrough` is the continuation decision tree for when the guard fails
**Warning signs:** Guarded patterns that should fall through instead raise "match failure"

### Pitfall 4: Breaking Existing Behavior
**What goes wrong:** The compiled decision tree produces different results from naive sequential matching
**Why it happens:** Subtle bugs in clause splitting, binding extraction, or guard handling
**How to avoid:** Implement comprehensive semantic equivalence testing: run every existing test through both the old `evalMatchClauses` and the new decision tree evaluator, compare results
**Warning signs:** Any existing test failure after the change

### Pitfall 5: AccessPath Confusion
**What goes wrong:** Sub-pattern bindings point to wrong values when evaluating a leaf
**Why it happens:** AccessPath indexing is off-by-one or doesn't account for nested destructuring
**How to avoid:** Test with nested patterns like `Node(Leaf, x, Node(y, z, _))` and verify all bindings are correct
**Warning signs:** Variables bound to wrong values in match bodies

## Code Examples

### Main Compilation Function

```fsharp
/// Compile a list of match clauses into a decision tree
let rec compile (clauses: MatchRow list) : DecisionTree =
    match clauses with
    | [] ->
        // No clauses left: match failure
        Fail
    | first :: _ when Map.isEmpty first.Patterns || allWildcards first ->
        // First clause has no remaining constraints: it matches
        match first.Guard with
        | None -> Leaf(extractBindings first, first.ClauseIndex)
        | Some guard ->
            // Guarded: try this leaf, fall through to rest on guard failure
            GuardedLeaf(extractBindings first, guard, first.ClauseIndex,
                        compile (List.tail clauses))
    | _ ->
        // Pick best test variable and constructor
        let testVar, ctor, arity = selectTest clauses
        // Split clauses into match/else branches
        let matchClauses, elseClauses = splitClauses testVar ctor arity clauses
        // Recursively compile branches
        Switch(testVar, ctor, arity, compile matchClauses, compile elseClauses)
```

### Decision Tree Evaluator

```fsharp
/// Evaluate a decision tree against a scrutinee value
let rec evalDecisionTree
    (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>)
    (env: Env) (scrutinee: Value) (bodies: Expr array)
    (tree: DecisionTree) : Value =
    match tree with
    | Leaf(bindings, bodyIdx) ->
        let bindEnv =
            bindings
            |> List.fold (fun e (name, path) ->
                Map.add name (accessValue scrutinee path) e) env
        eval recEnv moduleEnv bindEnv bodies.[bodyIdx]
    | Fail ->
        failwith "Match failure: no pattern matched"
    | Switch(testVar, ctor, arity, onMatch, onElse) ->
        let testValue = accessValue scrutinee testVar
        if matchesConstructor testValue ctor then
            evalDecisionTree recEnv moduleEnv env scrutinee bodies onMatch
        else
            evalDecisionTree recEnv moduleEnv env scrutinee bodies onElse
    | GuardedLeaf(bindings, guard, bodyIdx, fallthrough) ->
        let bindEnv =
            bindings
            |> List.fold (fun e (name, path) ->
                Map.add name (accessValue scrutinee path) e) env
        match eval recEnv moduleEnv bindEnv guard with
        | BoolValue true -> eval recEnv moduleEnv bindEnv bodies.[bodyIdx]
        | _ -> evalDecisionTree recEnv moduleEnv env scrutinee bodies fallthrough

/// Access a sub-value within the scrutinee using an access path
and accessValue (root: Value) (path: AccessPath) : Value =
    match path with
    | Root -> root
    | Field(parent, idx) ->
        match accessValue root parent with
        | DataValue(_, Some arg) when idx = 0 -> arg
        | TupleValue vals -> vals.[idx]
        | ListValue (h :: _) when idx = 0 -> h
        | ListValue (_ :: t) when idx = 1 -> ListValue t
        | _ -> failwithf "Invalid access path"

/// Check if a value matches a constructor name
and matchesConstructor (value: Value) (ctor: string) : bool =
    match value, ctor with
    | DataValue(name, _), c -> name = c
    | ListValue [], "[]" -> true
    | ListValue (_ :: _), "::" -> true
    | IntValue n, c when c.StartsWith("#int_") -> string n = c.Substring(5)
    | BoolValue b, c when c.StartsWith("#bool_") -> string b = c.Substring(6)
    | TupleValue _, c when c.StartsWith("#tuple_") -> true  // tuples always match their arity
    | _ -> false
```

### Normalizing Patterns for Compilation

```fsharp
/// Convert an AST Pattern to a set of (AccessPath, Pattern) constraints
let rec normalizePattern (path: AccessPath) (pat: Pattern) : Map<AccessPath, Pattern> =
    match pat with
    | VarPat(name, _) -> Map.ofList [(path, pat)]
    | WildcardPat _ -> Map.empty  // no constraint
    | ConstructorPat(name, None, _) -> Map.ofList [(path, pat)]
    | ConstructorPat(name, Some argPat, _) ->
        let self = Map.ofList [(path, pat)]
        let sub = normalizePattern (Field(path, 0)) argPat
        Map.fold (fun acc k v -> Map.add k v acc) self sub
    | TuplePat(pats, span) ->
        let self = Map.ofList [(path, pat)]
        pats
        |> List.mapi (fun i p -> normalizePattern (Field(path, i)) p)
        |> List.fold (fun acc m -> Map.fold (fun a k v -> Map.add k v a) acc m) self
    | ConsPat(headPat, tailPat, _) ->
        let self = Map.ofList [(path, pat)]
        let headMap = normalizePattern (Field(path, 0)) headPat
        let tailMap = normalizePattern (Field(path, 1)) tailPat
        [self; headMap; tailMap]
        |> List.fold (fun acc m -> Map.fold (fun a k v -> Map.add k v a) acc m) Map.empty
    | EmptyListPat _ -> Map.ofList [(path, pat)]
    | ConstPat _ -> Map.ofList [(path, pat)]
    | RecordPat(fields, _) ->
        let self = Map.ofList [(path, pat)]
        fields
        |> List.mapi (fun i (_, p) -> normalizePattern (Field(path, i)) p)
        |> List.fold (fun acc m -> Map.fold (fun a k v -> Map.add k v a) acc m) self
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Backtracking automata (SPJ Ch.5) | Binary decision trees (Jacobs 2021, Maranget 2008) | ~2008-2021 | Simpler algorithm, no backtracking, same efficiency |
| Naive sequential testing | Decision tree compilation | Standard since ML compilers 1990s | Eliminates redundant tests |
| Decision trees (exponential size) | Decision DAGs via hash consing | Ongoing optimization | Reduces tree size without losing efficiency |

**Deprecated/outdated:**
- Naive sequential testing: Only acceptable for interpreters, not compiled pattern matching
- Full backtracking automata: More complex than needed; binary decision trees achieve same results more simply

## Open Questions

1. **Record pattern compilation**
   - What we know: Record patterns (`RecordPat`) match by field name, not by constructor
   - What's unclear: How to represent record field access in AccessPath (by name vs. by index)
   - Recommendation: Use field name strings in AccessPath for records, or normalize at compilation time by sorting fields alphabetically and using indices

2. **Interaction with GADT type refinement**
   - What we know: GADT matching in `Bidir.fs` does local type refinement per branch. Decision tree compilation reorders tests.
   - What's unclear: Whether reordering tests could affect GADT type refinement correctness
   - Recommendation: For Phase 7, compile decision trees for evaluation only. Type checking (including GADT refinement) continues to use the original clause order. This is safe because the decision tree is semantically equivalent.

3. **When to compile: eager vs. lazy**
   - What we know: Compilation needs constructor arity info (available after type checking)
   - What's unclear: Whether to compile once at load time or on-demand at each match evaluation
   - Recommendation: Compile eagerly when the Match expression is first encountered during evaluation. Cache the compiled tree alongside the expression if needed.

4. **Performance of the heuristic on LangThree-sized programs**
   - What we know: The heuristic works well for real-world ML programs
   - What's unclear: Whether the overhead of compilation exceeds the benefit for small programs
   - Recommendation: Implement it correctly first; optimization is not a concern for a language this size

## Sources

### Primary (HIGH confidence)
- Jules Jacobs, "How to compile pattern matching" (2021) - https://julesjacobs.com/notes/patternmatching/patternmatching.pdf - Binary match# algorithm with clause splitting
- Luc Maranget, "Compiling Pattern Matching to good Decision Trees" (2008) - http://moscova.inria.fr/~maranget/papers/ml05e-maranget.pdf - Foundational decision tree compilation
- Gleam compiler implementation (based on Jacobs' paper) - https://deepwiki.com/gleam-lang/gleam/2.4-pattern-matching-and-exhaustiveness - Decision/Switch/Fail/Leaf types, pivot heuristic
- Codebase analysis: `Eval.fs`, `Exhaustive.fs`, `AST.fs`, `Type.fs`, `Bidir.fs` - Current implementation examined directly

### Secondary (MEDIUM confidence)
- compiler.club: "Compiling Pattern Matching" - https://compiler.club/compiling-pattern-matching/ - Maranget algorithm walkthrough
- Clojure core.match algorithm explanation - https://github.com/clojure/core.match/wiki/Understanding-the-algorithm - Column scoring heuristic

### Tertiary (LOW confidence)
- crumbles.blog: Decision tree pattern matching - https://crumbles.blog/posts/2025-11-28-extensible-match-decision-tree.html - Hash consing optimization (may be useful later)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No external dependencies needed, pure F# implementation
- Architecture: HIGH - Algorithm is well-documented across multiple sources, codebase is well-understood
- Pitfalls: HIGH - Common issues well-documented in literature and verified against codebase pattern types
- Code examples: MEDIUM - Pseudocode derived from algorithm description and adapted to codebase types; not copy-pasted from working implementation

**Research date:** 2026-03-10
**Valid until:** 2026-04-10 (stable domain, algorithm unchanged since 2021)
