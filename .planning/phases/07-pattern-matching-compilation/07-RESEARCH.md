# Phase 7: Pattern Matching Compilation - Research

**Researched:** 2026-03-10
**Domain:** Pattern matching compilation to decision trees (interpreter context)
**Confidence:** HIGH

## Summary

This phase transforms the existing naive sequential pattern matching evaluation in `Eval.fs` into an efficient decision tree representation that avoids redundant constructor tests. The reference algorithm is Jules Jacobs' "How to compile pattern matching" (2021), which produces binary decision trees (match#) through a clause-splitting approach with three cases (a/b/c) and a heuristic that selects test variables appearing in the maximum number of other clauses.

The current implementation in `evalMatchClauses` tries each clause sequentially top-to-bottom. When a clause fails, it retests constructors that may have already been tested by earlier clauses. The decision tree approach eliminates this by "remembering what we learnt" -- when we test a variable against a constructor and it fails, we carry that knowledge forward to the remaining clauses.

Since this is an interpreted language, "compilation" means transforming `Match` AST nodes into an intermediate `DecisionTree` representation that the evaluator executes. The existing exhaustiveness checking (Maranget usefulness algorithm in `Exhaustive.fs`) remains separate but can be cross-validated: if the decision tree generation produces a `Fail` node, the match is non-exhaustive; if a clause body never appears in the tree, that clause is redundant.

**Primary recommendation:** Add a new `MatchCompile.fs` module implementing the Jules Jacobs algorithm, then modify `Eval.fs` to compile match expressions to decision trees and evaluate those trees instead of sequential clause testing.

## Standard Stack

This phase requires no external libraries -- it is a pure algorithmic implementation in F#.

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| F# discriminated unions | net10.0 | DecisionTree IR representation | Native F# pattern matching over DU types is idiomatic and efficient |
| F# Map/Set | net10.0 | Clause test tracking, variable mappings | Immutable data structures fit the recursive algorithm naturally |
| Existing `Ast.fs` Pattern type | current | Input to the compiler | Already defines all pattern variants: ConstructorPat, ConsPat, EmptyListPat, TuplePat, ConstPat, RecordPat, VarPat, WildcardPat |
| Existing `Eval.fs` matchPattern | current | Reusable for runtime value inspection | Can be used for the `matchesConstructor` / `accessValue` helpers |
| Expecto | existing | Test framework | Already used for all project tests |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Binary decision tree (Jacobs) | N-way switch tree (Maranget) | Binary avoids clause duplication when wildcards are present (Jacobs Section 5) |
| Binary decision tree | Backtracking automata (Augustsson 1985) | Jacobs is simpler, no backtracking state needed |
| Separate DecisionTree IR | Decision DAG with hash consing | Start with tree; compress to DAG later if needed |
| New `MatchCompile.fs` module | Inline in Eval.fs | Separate module is cleaner, independently testable |

## Architecture Patterns

### Recommended Project Structure
```
src/LangThree/
  MatchCompile.fs   # NEW: Decision tree types + compilation algorithm
  Eval.fs           # MODIFIED: evaluate decision trees instead of sequential clauses
  Exhaustive.fs     # UNCHANGED: static exhaustiveness/redundancy checking
  Ast.fs            # UNCHANGED: Pattern, MatchClause types
  Type.fs           # UNCHANGED: type definitions
```

### Pattern 1: Clause Representation with Map-Based Tests (from Jacobs)

**What:** Represent each match clause as a `Map<TestVar, Pattern>` mapping test variables to their patterns, plus a body. This is the key representation from Jacobs' paper -- it generalizes the simple "one pattern per clause" into "multiple variable-pattern tests per clause" that naturally arises during compilation.

**When to use:** Always -- this is the internal format for the compilation algorithm.

The Jacobs reference implementation (Scala) uses exactly this:
```scala
// From pmatch.sc -- Jacobs' reference implementation
case class Clause(pats: Map[ID, Pat], body: Call)
```

F# equivalent:
```fsharp
/// Fresh variable identifier for sub-values during compilation
type TestVar = int

/// A clause under compilation
type MatchRow = {
    Patterns: Map<TestVar, Pattern>  // variable -> pattern constraint
    Body: Expr                       // right-hand side expression
    Guard: Expr option               // optional when guard
    Bindings: Map<string, TestVar>   // pattern variable name -> test variable
    OriginalIndex: int               // for redundancy detection
}
```

### Pattern 2: Binary Decision Tree (match#)

**What:** The output IR -- a binary tree where each node tests one variable against one constructor. From the paper: "match# a with | C(a1,...,an) => [A] | _ => [B]"

```fsharp
/// Decision tree: the compiled form of pattern matching
type DecisionTree =
    /// Leaf: match succeeded, apply bindings and evaluate body
    | Leaf of bindings: Map<string, TestVar> * guard: Expr option
            * body: Expr * fallback: DecisionTree option
    /// Switch: test one variable against one constructor, binary branch
    | Switch of testVar: TestVar * constructorName: string
              * argVars: TestVar list * ifMatch: DecisionTree * ifNoMatch: DecisionTree
    /// Fail: non-exhaustive match failure
    | Fail
```

### Pattern 3: The Compilation Algorithm (Jacobs Section 3)

**What:** The main recursive algorithm with 5 steps:

1. **Push variable bindings:** Convert all `a is y` (VarPat/WildcardPat) tests into bindings, so only constructor tests remain
2. **Select test:** Pick a `(testVar, constructor)` from the first clause using the heuristic
3. **Generate match#:** Create `Switch(testVar, ctor, argVars, [A], [B])`
4. **Split clauses into A and B** using cases a/b/c
5. **Recursively compile** A and B

**Base cases:**
- Empty clause list -> `Fail` (non-exhaustive)
- First clause has no remaining tests -> `Leaf` (match succeeded)

From Jacobs' reference implementation:
```scala
// From pmatch.sc
def genMatch(clauses: Seq[Clause]): CaseTree = {
  if(clauses.isEmpty) assert(false, "Non-exhaustive pattern")
  val clauses2 = clauses.map(substVarEqs)  // Step 1: push var bindings
  val Clause(pats, bod) = clauses2.head
  if(pats.isEmpty) return Run(bod)           // Base case: no tests left
  val branchVar = branchingHeuristic(pats, clauses2)  // Step 2
  val Constr(constrname, args) = pats(branchVar)
  // Step 4: split clauses
  var yes = Buffer[Clause]()
  var no = Buffer[Clause]()
  val vars = args.map(x => fresh())
  for(Clause(pats, bod) <- clauses2) {
    pats.get(branchVar) match {
      case Some(Constr(name2, args2)) =>
        if(constrname == name2)
          yes += Clause(pats - branchVar ++ vars.zip(args2), bod)  // Case (a)
        else
          no += Clause(pats, bod)                                   // Case (b)
      case None =>
        yes += Clause(pats, bod)                                    // Case (c)
        no += Clause(pats, bod)
    }
  }
  return Test(branchVar, constrname, vars, genMatch(yes), genMatch(no))
}
```

F# translation:
```fsharp
let rec compile (clauses: MatchRow list) : DecisionTree =
    match clauses with
    | [] -> Fail
    | _ ->
        // Step 1: Push variable bindings into body
        let clauses' = clauses |> List.map pushVarBindings
        let first = clauses'.Head

        // Base case: first clause has no remaining constructor tests
        if Map.isEmpty first.Patterns then
            let fallback =
                if clauses'.Length > 1 then Some(compile clauses'.Tail)
                else None
            Leaf(first.Bindings, first.Guard, first.Body, fallback)
        else
            // Step 2: Select test variable (heuristic)
            let testVar = selectTestVariable first clauses'
            let pat = Map.find testVar first.Patterns
            let ctorName, arity = extractConstructorInfo pat
            let freshVars = List.init arity (fun _ -> freshTestVar())

            // Steps 3-4: Split and generate Switch
            let yesClauses, noClauses =
                splitClauses testVar ctorName arity freshVars clauses'
            Switch(testVar, ctorName, freshVars,
                   compile yesClauses, compile noClauses)
```

### Pattern 4: Heuristic Test Selection (Jacobs Section 4)

**What:** Select the test variable appearing in the maximum number of other clauses. This directly minimizes case (c) duplication.

From Jacobs' reference:
```scala
def branchingHeuristic(pats: Map[String, Pat], clauses: Seq[Clause]): String =
  pats.keys.maxBy(v => clauses.count{ case Clause(ps, _) => ps.contains(v) })
```

F# translation:
```fsharp
let selectTestVariable (first: MatchRow) (allClauses: MatchRow list) : TestVar =
    first.Patterns
    |> Map.toSeq
    |> Seq.map fst
    |> Seq.maxBy (fun tv ->
        allClauses |> List.sumBy (fun c ->
            if Map.containsKey tv c.Patterns then 1 else 0))
```

### Pattern 5: Clause Splitting (Cases a/b/c from Jacobs Section 3)

**What:** When testing variable `v` against constructor `C`:
- **(a)** Clause has `v is C(P1,...,Pn)` -- expand sub-patterns into fresh vars, add to YES only
- **(b)** Clause has `v is D(...)` where D != C -- add to NO only
- **(c)** Clause has no test for `v` (wildcard/variable already pushed) -- add to BOTH

```fsharp
let splitClauses (testVar: TestVar) (ctorName: string) (arity: int)
                 (freshVars: TestVar list) (clauses: MatchRow list)
    : MatchRow list * MatchRow list =
    let yesClauses = ResizeArray()
    let noClauses = ResizeArray()
    for clause in clauses do
        match Map.tryFind testVar clause.Patterns with
        | Some pat ->
            let patCtor, patArgs = extractConstructorInfo pat
            if patCtor = ctorName then
                // Case (a): same constructor, expand sub-patterns
                let newTests = clause.Patterns |> Map.remove testVar
                let expanded =
                    List.zip freshVars (extractSubPatterns pat)
                    |> List.fold (fun acc (fv, subPat) ->
                        Map.add fv subPat acc) newTests
                yesClauses.Add({ clause with Patterns = expanded })
            else
                // Case (b): different constructor, NO branch only
                noClauses.Add(clause)
        | None ->
            // Case (c): no test for this variable, add to BOTH
            yesClauses.Add(clause)
            noClauses.Add(clause)
    (Seq.toList yesClauses, Seq.toList noClauses)
```

### Anti-Patterns to Avoid

- **N-way branching on constructors:** Simultaneously testing all constructors of a type causes clause duplication when wildcards are present. Binary (is C / is not C) avoids this (Jacobs Section 5 demonstrates the issue).
- **Modifying the AST Expr type:** Do NOT add decision tree nodes to the Expr DU. Keep the decision tree as a separate IR consumed only by the evaluator.
- **Replacing Exhaustive.fs:** The existing Maranget-based checking handles static warnings. Decision tree compilation is an evaluation optimization, not a replacement for static analysis.
- **Mixing compilation with evaluation:** Build the complete decision tree before evaluation begins. Do not interleave tree-building with value matching.
- **Forgetting non-constructor patterns:** All pattern types must be mapped to constructor-like tests. See Pitfall 2 below for the mapping.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Exhaustiveness checking | Custom check in decision tree | Existing `Exhaustive.fs` Maranget algorithm | Already correct, well-tested, handles all pattern types |
| Fresh variable generation | Ad-hoc counter | Module-level counter (like `freshTypeVarIndex` in Elaborate.fs) | Avoid name collisions |
| Constructor arity lookup | Manual tracking | Existing `ConstructorEnv` from Type.fs / `ConstructorInfo.ArgType` | Already knows all ADT constructors with types |
| Decision DAG optimization | Hash consing | Plain decision tree | Premature optimization; tree is correct and simpler |

**Key insight:** The decision tree compilation is purely an evaluation optimization. All static checking (types, exhaustiveness, redundancy) remains unchanged. The new module only needs to produce a tree that the evaluator walks at runtime.

## Common Pitfalls

### Pitfall 1: When-Guard Fallthrough Semantics
**What goes wrong:** A guarded clause `| C x when x > 0 -> e` matches the constructor but fails the guard. The decision tree has already committed to the C branch and cannot try subsequent clauses.
**Why it happens:** Guards are evaluated at runtime, not compile time. The tree must model guard failure as a fallthrough.
**How to avoid:** Model guarded leaves with an explicit `fallback` tree. When a Leaf has `guard = Some g` and the guard evaluates to false, follow the `fallback` decision tree (which represents the remaining clauses compiled without the guarded clause). Yorick Peterse's implementation confirms this approach: "When we are about to produce a Success node for a row, we check if it defines a guard. If so, all remaining rows are compiled into the guard's fallback tree."
**Warning signs:** Tests with `when` guards that work in naive evaluation but produce "match failure" with decision trees.

### Pitfall 2: Non-Constructor Pattern Types
**What goes wrong:** The algorithm handles `ConstructorPat` but crashes or miscompiles `ConstPat`, `ConsPat`, `EmptyListPat`, `TuplePat`, `RecordPat`.
**Why it happens:** Jacobs' paper focuses on ADT constructors. LangThree's pattern language is richer.
**How to avoid:** Map each pattern type to a constructor-like test before compilation:

| Pattern | Constructor Name | Arity | Sub-patterns |
|---------|-----------------|-------|--------------|
| `ConstructorPat("Some", Some p)` | `"Some"` | 1 | `[p]` |
| `ConstructorPat("None", None)` | `"None"` | 0 | `[]` |
| `ConsPat(h, t)` | `"::"` | 2 | `[h; t]` |
| `EmptyListPat` | `"[]"` | 0 | `[]` |
| `TuplePat([p1;p2;p3])` | `"#tuple3"` | 3 | `[p1;p2;p3]` |
| `ConstPat(IntConst 42)` | `"#int_42"` | 0 | `[]` |
| `ConstPat(BoolConst true)` | `"#bool_true"` | 0 | `[]` |
| `RecordPat(fields)` | `"#record"` | len(fields) | field patterns (sorted by name) |
| `VarPat`, `WildcardPat` | (not a constructor test) | N/A | pushed to bindings in step 1 |

**Warning signs:** Match expressions with list/tuple/literal patterns producing wrong results.

### Pitfall 3: Semantic Equivalence Violation
**What goes wrong:** The compiled decision tree produces different results from naive sequential matching.
**Why it happens:** Subtle bugs in clause splitting, binding extraction, or guard handling.
**How to avoid:** Comprehensive differential testing: run both naive `evalMatchClauses` and the new decision tree evaluator on the same inputs, assert identical results. Keep the old evaluator intact for comparison.
**Warning signs:** Any existing test failure after the change.

### Pitfall 4: Infinite Pattern Domains (Int, String Constants)
**What goes wrong:** Integer and string literal patterns cannot have a "complete signature." Missing default branches cause incorrect Fail results.
**Why it happens:** Unlike ADT constructors where all variants are known, `ConstPat(IntConst n)` can match any integer.
**How to avoid:** When the tested pattern is a literal constant, always ensure the "no-match" branch exists. Never treat literal patterns as exhaustive (the Maranget exhaustiveness checker already handles this correctly).
**Warning signs:** Missing default branches for integer/string pattern matches.

### Pitfall 5: Variable Binding Correctness
**What goes wrong:** Pattern variables bound in different branches of the decision tree end up with wrong values or are unbound.
**Why it happens:** The decision tree introduces fresh `TestVar` identifiers for destructured sub-values. These must be correctly mapped back to pattern variable names when evaluating leaf nodes.
**How to avoid:** Each `Leaf` node carries a `Bindings: Map<string, TestVar>` that maps pattern variable names to the TestVar holding their runtime value. The evaluator uses a `Map<TestVar, Value>` (the "variable environment") alongside the regular `Env`. At a Leaf, it resolves each binding name to its TestVar's current value.
**Warning signs:** "Undefined variable" errors in match bodies that work with naive evaluation.

## Code Examples

### Evaluating a Decision Tree at Runtime

```fsharp
/// Runtime variable environment mapping TestVar -> Value
type VarEnv = Map<TestVar, Value>

/// Evaluate a decision tree against runtime values
let rec evalDecisionTree (recEnv: RecordEnv) (moduleEnv: Map<string, ModuleValueEnv>)
                         (env: Env) (varEnv: VarEnv) (tree: DecisionTree) : Value =
    match tree with
    | Fail -> failwith "Match failure: no pattern matched"
    | Leaf(bindings, guard, body, fallback) ->
        let bodyEnv =
            bindings |> Map.fold (fun e name tv ->
                Map.add name (Map.find tv varEnv) e) env
        match guard with
        | None -> eval recEnv moduleEnv bodyEnv body
        | Some guardExpr ->
            match eval recEnv moduleEnv bodyEnv guardExpr with
            | BoolValue true -> eval recEnv moduleEnv bodyEnv body
            | _ ->
                match fallback with
                | Some fb -> evalDecisionTree recEnv moduleEnv env varEnv fb
                | None -> failwith "Match failure: no pattern matched"
    | Switch(testVar, ctorName, argVars, ifMatch, ifNoMatch) ->
        let value = Map.find testVar varEnv
        match tryDestructure ctorName value with
        | Some subValues ->
            let varEnv' =
                List.zip argVars subValues
                |> List.fold (fun acc (tv, v) -> Map.add tv v acc) varEnv
            evalDecisionTree recEnv moduleEnv env varEnv' ifMatch
        | None ->
            evalDecisionTree recEnv moduleEnv env varEnv ifNoMatch

/// Try to destructure a value as a given constructor, returning sub-values
and tryDestructure (ctorName: string) (value: Value) : Value list option =
    match value, ctorName with
    | DataValue(name, None), c when name = c -> Some []
    | DataValue(name, Some arg), c when name = c -> Some [arg]
    | ListValue [], "[]" -> Some []
    | ListValue (h :: t), "::" -> Some [h; ListValue t]
    | IntValue n, c when c = sprintf "#int_%d" n -> Some []
    | BoolValue b, c when c = sprintf "#bool_%b" b -> Some []
    | TupleValue vals, c when c.StartsWith("#tuple") -> Some vals
    | RecordValue(_, fields), "#record" ->
        Some (fields |> Map.toList |> List.sortBy fst |> List.map (fun (_, r) -> !r))
    | _ -> None
```

### Integration Point in Eval.fs

```fsharp
// In eval function, the Match case becomes:
| Match (scrutinee, clauses, _) ->
    let value = eval recEnv moduleEnv env scrutinee
    let rootVar = MatchCompile.freshTestVar()
    let rows = MatchCompile.matchClausesToRows rootVar clauses
    let tree = MatchCompile.compile rows
    let varEnv = Map.ofList [(rootVar, value)]
    MatchCompile.evalDecisionTree recEnv moduleEnv env varEnv tree
```

### Converting MatchClauses to MatchRows

```fsharp
let matchClausesToRows (scrutineeVar: TestVar) (clauses: MatchClause list) : MatchRow list =
    clauses |> List.mapi (fun i (pat, guard, body) ->
        { Patterns = Map.ofList [(scrutineeVar, pat)]
          Body = body
          Guard = guard
          Bindings = Map.empty
          OriginalIndex = i })
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Backtracking automata (Augustsson 1985) | Binary decision trees (Jacobs 2021) | 2021 | Simpler algorithm, no backtracking |
| N-way branching (all constructors at once) | Binary branching (one constructor yes/no) | Jacobs 2021 | Avoids clause duplication |
| Naive sequential testing | Decision tree compilation | Standard since 1990s | Eliminates redundant tests |
| Decision trees (potential size) | Decision DAGs via hash consing | Optimization | Reduces tree size (not needed initially) |

**Key finding from literature:** Different heuristics produce nearly identical code for real-world programs (Scott & Ramsey 2000, Maranget 2008). The "max shared tests" heuristic is simple and effective enough.

## Open Questions

1. **When to compile: eager vs. lazy**
   - What we know: Compilation needs constructor arity info (available from pattern structure itself, not from type env)
   - What's unclear: Whether to compile once at elaboration time or lazily at first evaluation
   - Recommendation: Compile lazily on first evaluation of each Match expression. The algorithm only needs pattern structure (not type information), so it can run in the evaluator. Cache the compiled tree if the same Match is evaluated multiple times.

2. **Record pattern field ordering**
   - What we know: Record patterns match by field name. Fields need a canonical ordering for AccessPath indexing.
   - What's unclear: Best canonical ordering for record fields in sub-patterns
   - Recommendation: Sort fields alphabetically by name. This ensures consistent TestVar assignment regardless of pattern field order.

3. **Interaction with try-with handlers**
   - What we know: `TryWith` uses the same `MatchClause` type and `evalMatchClauses`
   - What's unclear: Whether try-with handlers should also use decision trees
   - Recommendation: Yes, apply the same compilation to try-with handlers. Exception constructors are ADT-like (ConstructorPat matching against DataValue).

4. **GADT type refinement interaction**
   - What we know: GADT matching does local type refinement per branch in `Bidir.fs`. Decision tree compilation is evaluation-only.
   - What's unclear: Whether reordering tests could affect anything
   - Recommendation: Safe because decision tree compilation operates only at evaluation time, after type checking is complete. Type checking continues to use original clause order.

## Sources

### Primary (HIGH confidence)
- Jules Jacobs, "How to compile pattern matching" (2021) - https://julesjacobs.com/notes/patternmatching/patternmatching.pdf - Full paper read (5 pages), algorithm extracted with all cases
- Jules Jacobs, `pmatch.sc` reference implementation - https://julesjacobs.com/notes/patternmatching/pmatch.sc - Scala source code read, data structures and heuristic confirmed
- Codebase analysis: `Eval.fs` (matchPattern, evalMatchClauses), `Exhaustive.fs` (Maranget algorithm), `Ast.fs` (Pattern DU, MatchClause), `Type.fs` (ConstructorEnv) - Current implementation fully analyzed

### Secondary (MEDIUM confidence)
- Yorick Peterse, pattern-matching-in-rust - https://gitlab.com/yorickpeterse/pattern-matching-in-rust - Guard handling (fallback tree), literal patterns, exhaustiveness via Fail nodes, redundancy via body reachability
- Gleam compiler pattern matching - https://deepwiki.com/gleam-lang/gleam/2.4-pattern-matching-and-exhaustiveness - Decision/Switch/Fail/Leaf types, branching_factor heuristic
- Luc Maranget, "Compiling Pattern Matching to good Decision Trees" (ML'08) - http://moscova.inria.fr/~maranget/papers/ml05e-maranget.pdf - Foundational reference, confirms heuristic effectiveness

### Tertiary (LOW confidence)
- compiler.club: "Compiling Pattern Matching" - https://compiler.club/compiling-pattern-matching/ - Specialization/defaulting matrix approach
- crumbles.blog: Decision tree pattern matching - https://crumbles.blog/posts/2025-11-28-extensible-match-decision-tree.html - Alternative heuristic (fnr), hash consing

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No external dependencies; pure F# algorithmic code
- Architecture: HIGH - Algorithm fully specified in 5-page paper with reference Scala implementation; codebase is well-understood
- Pitfalls: HIGH - Guard handling and non-constructor patterns are known challenges, documented in Yorick Peterse's implementation and verified against LangThree's pattern types
- Code examples: HIGH - Derived from Jacobs' reference Scala implementation, translated to F# with LangThree-specific adaptations

**Research date:** 2026-03-10
**Valid until:** Indefinite (stable algorithms, no framework churn)
