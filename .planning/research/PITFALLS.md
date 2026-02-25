# Domain Pitfalls: ML-Style Language Implementation

**Domain:** Programming language implementation (ML-family with F# features)
**Researched:** 2026-02-25
**Context:** LangThree - Hindley-Milner type inference with fslex/fsyacc

## Critical Pitfalls

Mistakes that cause rewrites or major issues.

### Pitfall 1: Indentation Lexer State Management Failures

**What goes wrong:** Multiple DEDENT tokens needed on a single line aren't emitted correctly, or lexer state becomes corrupted across token boundaries.

**Why it happens:** fslex lexers are typically stateless per-token, but [indentation parsing requires maintaining a stack of indentation levels](https://en.wikipedia.org/wiki/Off-side_rule) across multiple tokens. The lexer must hold contextual state (current indent level) to detect changes, making the lexical grammar not context-free.

**Consequences:**
- Parser receives incorrect token stream
- Syntactically valid code rejected
- Ambiguous parse trees for valid constructs
- Backtracking issues in error recovery

**Prevention:**
1. Maintain an explicit indentation stack in fslex header/trailer code
2. Implement token buffering to emit multiple DEDENTs from single lexer call
3. Use sentinel value (0) at bottom of indentation stack
4. Algorithm: compare line's indentation to stack top, push and emit INDENT if larger, pop and emit DEDENT for each popped level if smaller
5. Test edge case: jumping from indent level 8 to 0 should emit TWO DEDENTs (8→4, 4→0)

**Detection:**
- Parsing fails on nested blocks that dedent multiple levels
- Error messages report unexpected EOF at valid closing constructs
- Test: Parse code that goes from deep nesting (3+ levels) directly to baseline

**Phase mapping:** Phase 1 (Indentation syntax) - This is the core challenge

**References:**
- [Principled Parsing for Indentation-Sensitive Languages](https://michaeldadams.org/papers/layout_parsing/LayoutParsing.pdf)
- [Python indentation lexing algorithm](https://docs.python.org/3/reference/lexical_analysis.html)
- [FPish discussion: Python-like indentation with fslex](https://staging.fpish.net/topic/None/58109)

---

### Pitfall 2: Mixed Tabs and Spaces in Indentation

**What goes wrong:** Code appears correctly indented visually but parser rejects it with confusing errors about indentation mismatch.

**Why it happens:** Tabs render differently in different editors (2, 4, or 8 spaces). [Python disallows mixing tabs and spaces](https://learnpython.com/blog/indentation-python/), but without explicit detection, the lexer treats "\t" differently from equivalent spaces.

**Consequences:**
- User frustration ("it looks right!")
- Copy-paste from web sources breaks
- IDE migration introduces subtle bugs
- Different team members see different indentation

**Prevention:**
1. **Strict approach (recommended for F#-style):** Follow F#'s lead - [tabs are not allowed in F# code unless #indent off option is used](https://forums.fsharp.org/t/whats-tabs-are-not-allowed-in-f-code-unless-the-indent-off-option-is-used-error/1253). Reject tabs entirely with clear error message.
2. **Permissive approach (Python-style):** Convert tabs to spaces with consistent rule (e.g., tab = 8 spaces) BUT detect mixing within same block and error
3. Test with `python -tt` style strict mode during development

**Detection:**
- Run test suite with files containing mixed tabs/spaces
- Error message should say "inconsistent use of tabs and spaces" not generic "indentation error"
- Test: identical-looking code from two editors should parse identically or error clearly

**Phase mapping:** Phase 1 (Indentation syntax) - Detect during initial lexer implementation

**References:**
- [Python TabError: Inconsistent Use of Tabs and Spaces](https://www.geeksforgeeks.org/python-taberror-inconsistent-use-of-tabs-and-spaces-in-indentation/)
- [F# formatting guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)

---

### Pitfall 3: GADT Type Inference Requires Annotations

**What goes wrong:** GADT pattern matching fails to type-check despite being correct, or type inference becomes undecidable and hangs/rejects valid programs.

**Why it happens:** [Type inference for GADTs is undecidable](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/gadt-pldi.pdf) without programmer-supplied type annotations. Hindley-Milner assumes principal types, but [GADTs do not admit principal types in general](https://blog.polybdenum.com/2024/03/03/what-are-gadts-and-why-do-they-make-type-inference-sad.html).

**Consequences:**
- Valid GADT code rejected with cryptic type errors
- Inference algorithm doesn't terminate (infinite loop in unification)
- Developers add excessive type annotations everywhere (not just GADTs)
- Polymorphic recursion breaks silently

**Prevention:**
1. **Design decision:** Require explicit type signatures on GADT pattern matching branches OR on functions that use GADT patterns
2. Document clearly: "GADTs require type annotations - this is fundamental, not a bug"
3. Use [bidirectional type checking](https://arxiv.org/pdf/1908.05839) where annotations guide inference
4. Test: GADT examples from papers should work WITH annotations, error clearly WITHOUT them
5. For polymorphic recursion with GADTs, use explicit `forall` quantification

**Detection:**
- Type inference doesn't terminate on GADT examples
- Error messages mention "cannot construct infinite type" or "occurs check"
- Test: Simple GADT (like typed expression evaluator) should require annotation and error clearly if omitted

**Phase mapping:** Phase 2 (ADT/GADT) - Fundamental design constraint

**References:**
- [Simple unification-based type inference for GADTs](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/gadt-pldi.pdf) (Peyton Jones et al.)
- [What are GADTs and why do they make type inference sad?](https://blog.polybdenum.com/2024/03/03/what-are-gadts-and-why-do-they-make-type-inference-sad.html)

---

### Pitfall 4: GADT Rigid Type Variables Escaping Scope

**What goes wrong:** Type variables from GADT pattern matching leak into broader scope, causing type unsoundness or incorrect unification.

**Why it happens:** When pattern matching on GADT constructors opens existential types, [a rigid/skolem type variable is allocated to denote the unknown but fixed private type](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/gadt-pldi.pdf). These [must not escape the scope of the branch via unification variables](https://free.cofree.io/2020/09/01/type-errors/).

**Consequences:**
- Type system unsoundness (can write `1 : String`)
- Segfaults at runtime from type confusion
- Unification succeeds when it should fail

**Prevention:**
1. Implement **rigid/wobbly type system** in type checker
2. Mark GADT-opened existentials as **rigid** (cannot unify with anything)
3. Mark normal type variables as **wobbly** (can unify freely)
4. Rule: Rigid types can only unify with identical rigid types in same scope
5. When leaving GADT pattern branch scope, check no rigid variables escaped via unification
6. Bidirectional typing helps: scrutinee, case result, and free variables in alternatives must all be rigid

**Detection:**
- Write test that tries to return GADT existential from pattern branch
- Should error with "rigid type variable would escape its scope"
- Test: `case expr of MkExists x -> x` should be rejected

**Phase mapping:** Phase 2 (ADT/GADT) - Core type checking invariant

**References:**
- [Simple Unification-based Type Inference for GADTs](https://www.cs.tufts.edu/~nr/cs257/archive/simon-peyton-jones/gadt-icfp.pdf)
- [Un-obscuring GHC type error messages](https://free.cofree.io/2020/09/01/type-errors/)
- [GHC GADT documentation](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/gadt.html)

---

### Pitfall 5: GADT Exhaustiveness Checking with Abstract Types

**What goes wrong:** Pattern match exhaustiveness checker reports false positives (claims non-exhaustive match when all cases are covered) or false negatives (claims exhaustive when cases are missing).

**Why it happens:** [Type checker cannot know that an abstract type differs from concrete types in all contexts](https://github.com/ocaml/ocaml/issues/7028). GADT constraints can make certain patterns impossible, but exhaustiveness checker doesn't track these type-level constraints.

**Consequences:**
- Developer forced to add impossible `| _ -> failwith "impossible"` cases
- Breaks abstraction by requiring type exposure
- Obscures real incomplete matches with noise
- Runtime errors from supposedly impossible cases

**Prevention:**
1. Implement **refutation clauses**: allow `| ImpossiblePattern -> .` syntax to explicitly mark impossible cases
2. Make exhaustiveness checker GADT-aware: track type refinements from earlier patterns
3. Compute pattern intersections and verify whether resulting patterns can match concrete values
4. Document that some pattern matches will require refutation clauses
5. If impossible to implement: error on the side of allowing non-exhaustive warnings to be suppressed for GADT matches

**Detection:**
- Write GADT example where type constraints make a branch impossible
- Exhaustiveness checker should either (a) understand it's exhaustive, or (b) allow explicit refutation
- Test classic example: `type 'a t = Int : int t | Bool : bool t`

**Phase mapping:** Phase 2 (ADT/GADT) - Later refinement after basic GADT support

**References:**
- [GADT pattern exhaustiveness checking and abstract types](https://github.com/ocaml/ocaml/issues/7028)
- [GADTs and exhaustiveness: looking for the impossible](https://www.math.nagoya-u.ac.jp/~garrigue/papers/gadtspm.pdf)
- [GADT exhaustiveness discussion](https://discuss.ocaml.org/t/gadt-exhaustiveness/1501)

---

### Pitfall 6: Record Field Name Collisions

**What goes wrong:** When multiple record types have fields with the same name, either (a) shadowing makes older type inaccessible, or (b) ambiguous errors appear where field access is unclear.

**Why it happens:** [OCaml won't let you define records with duplicate field names](https://reasonml.chat/t/solved-declaring-variant-type-of-records-with-duplicate-field-names/2234) in the same scope. The most recent definition shadows earlier ones. Without qualified access, `person.name` is ambiguous if both `Person` and `Company` have `name` field.

**Consequences:**
- Cannot use two record types with same field name in same module
- Type errors appear far from actual definition site
- Refactoring adds field to second type, breaks first type's usage elsewhere
- Field access requires verbose module qualification

**Prevention:**
1. **Module namespacing (recommended):** Require record types in different modules if they share field names - access as `Person.name`, `Company.name`
2. **Type-directed disambiguation:** Use [type propagation to disambiguate field names](http://gallium.inria.fr/blog/resolving-field-names/) - if `r : Person` known, then `r.name` resolves to `Person.name`
3. **Unique field names in scope (simplest):** Documentation guideline - don't reuse field names
4. **Inline records:** Use GADT-style inline records `Person of {name: string}` vs `Company of {name: string}` - different constructor namespaces

**Detection:**
- Test: Define two record types with same field name in same module
- Should either: (a) error clearly "field name already used", or (b) work via type-directed resolution
- Test field access: should resolve unambiguously or error with "ambiguous field name"

**Phase mapping:** Phase 3 (Records) - Design decision needed early

**References:**
- [Records - Real World OCaml](https://dev.realworldocaml.org/records.html)
- [Declaring Variant type of records with duplicate field names](https://reasonml.chat/t/solved-declaring-variant-type-of-records-with-duplicate-field-names/2234)
- [Using type-propagation to disambiguate label names](http://gallium.inria.fr/blog/resolving-field-names/)

---

### Pitfall 7: Circular Module Dependencies

**What goes wrong:** Two modules depend on each other, causing build system to report circular dependency error even when no actual circular reference exists at value level.

**Why it happens:** [ML-family languages require declaration-before-use ordering](https://fsharpforfunandprofit.com/posts/removing-cyclic-dependencies/). F# and OCaml enforce file ordering, where files only know about files compiled before them. [ocamldep can generate false circular dependencies](https://github.com/ocaml/ocaml/issues/4618) because knowing what contains a module without typing it is unfeasible.

**Consequences:**
- Cannot compile mutually recursive modules
- Forced to merge unrelated modules to break cycle
- Large monolithic modules instead of clean separation
- Build errors that are "very hard to find out what is the reason"

**Prevention:**
1. **F# approach (strictest):** Enforce strict file ordering - no circularity allowed, period. [This encourages better design](https://fsharpforfunandprofit.com/posts/removing-cyclic-dependencies/).
2. **Workarounds for mutual recursion:**
   - Use `and` keyword for mutually recursive types/functions in SAME file
   - Move shared types to a third module that both depend on
   - Use references to hold one function as indirection (functional but ugly)
3. **Better error messages:** When cycle detected, report "Circular dependency: A.fs → B.fs → A.fs" not just "circular build error"
4. **Build system:** Compute transitive closure of dependencies for better diagnostics

**Detection:**
- Test: Two files that reference each other's types
- Should error clearly with cycle path: "A depends on B, B depends on A"
- Test: False positive - A imports B module name but doesn't use it

**Phase mapping:** Phase 4 (Modules) - Fundamental architecture decision

**References:**
- [Refactoring to remove cyclic dependencies](https://fsharpforfunandprofit.com/posts/removing-cyclic-dependencies/)
- [ocamldep circular dependency bug](https://github.com/ocaml/ocaml/issues/4618)
- [OCaml Cyclical Build Dependencies](https://wiki.xenproject.org/wiki/OCaml_Cyclical_Build_Dependencies)
- [F# declaration order matters](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.1/FS-1009-mutually-referential-types-and-modules-single-scope.md)

---

### Pitfall 8: Exception Type Polymorphism and Marshalling Unsafety

**What goes wrong:** Exceptions are marshalled/unmarshalled across process boundaries, losing type witnesses and creating false equality, leading to type unsoundness.

**Why it happens:** The [exn type is an extensible variant](https://ocaml.org/manual/5.4/extensiblevariants.html) - new constructors allocated at runtime. [Extension constructors are generated at runtime by physical identity](https://github.com/let-def/distwit), but [physical equality doesn't span across processes and is lost when marshalling](https://github.com/let-def/distwit). Unmarshalled exception has new constructor that never matches original.

**Consequences:**
- Type safety violated if unmarshalled constructor matches wrong witness
- Exception handlers catch wrong exception types
- Security vulnerability if attacker crafts malicious exception
- Silent failures where `try...with` doesn't catch expected exception

**Prevention:**
1. **No marshalling of exceptions (safest):** Document that `exn` type cannot be serialized
2. **Trusted processes only:** If marshalling needed, restrict to same-binary trusted processes
3. **Registration system:** Use explicit `register` function to bind exception constructor names to constructors, match by name not physical identity after unmarshal
4. **Pattern match discipline:** Always have default case `| _ -> ...` for extensible variants (including exn)
5. For LangThree: Likely not an issue initially (no marshalling), but document the limitation

**Detection:**
- Test: Marshal exception to bytes, unmarshal, try to catch - should fail to match unless registered
- Security test: Craft exception with same name as built-in exception but different arity
- Should either: (a) reject at unmarshal time, or (b) not match in pattern

**Phase mapping:** Phase 5 (Exceptions) - Document limitation, not a blocker for basic exceptions

**References:**
- [OCaml extensible variants](https://ocaml.org/manual/5.4/extensiblevariants.html)
- [distwit: Distribute instances of extensible variant types](https://github.com/let-def/distwit)
- [OCaml error handling](https://ocaml.org/docs/error-handling)

---

## Moderate Pitfalls

Mistakes that cause delays or technical debt.

### Pitfall 9: Offside Rule Parsing in LALR(1) Parser

**What goes wrong:** fsyacc (LALR parser generator) produces shift/reduce conflicts when trying to handle indentation-sensitive grammar directly in parser.

**Why it happens:** [Context-free grammars cannot express indentation rules](https://michaeldadams.org/papers/layout_parsing/). LALR parsers expect fixed lookahead, but indentation creates context-dependent parsing decisions. [Shift/reduce conflicts occur when parser faces choice](http://www.cs.columbia.edu/~aho/cs4115/Lectures/15-02-23.html) between shifting next token or reducing current production.

**Consequences:**
- Parser has ambiguous grammar
- fsyacc chooses default shift action, leading to wrong parse trees
- Grammar tweaking becomes time-consuming trial-and-error

**Prevention:**
1. **Handle indentation in LEXER, not parser (recommended):** Emit INDENT/DEDENT tokens from lexer, parser sees context-free token stream
2. Lexer maintains indentation stack, parser just treats INDENT/DEDENT like `{` and `}`
3. Parser grammar becomes simple and conflict-free
4. If shift/reduce conflict appears, use `%left`, `%right`, `%nonassoc` precedence declarations
5. Test grammar: run fsyacc with verbose flag to see conflict reports

**Detection:**
- fsyacc prints "shift/reduce conflict" warnings during compilation
- Parser accepts input it should reject, or rejects valid input
- Ambiguous parse trees for same input

**Phase mapping:** Phase 1 (Indentation syntax) - Architecture decision

**References:**
- [Principled Parsing for Indentation-Sensitive Languages](https://michaeldadams.org/papers/layout_parsing/LayoutParsing.pdf)
- [Handling conflicts in Yacc parsers](https://www2.cs.arizona.edu/~debray/Teaching/CSc453/DOCS/conflicts.pdf)

---

### Pitfall 10: Value Restriction Breaks GADT Polymorphism

**What goes wrong:** Let-bound GADTs lose polymorphism and become monomorphic, causing type errors on reuse.

**Why it happens:** [ML's value restriction says let-bound expressions must be nonexpansive to generalize](https://www.smlnj.org/doc/Conversion/types.html) (to preserve soundness with refs/exceptions). GADT pattern matching is often considered expansive. [While generalization is straightforward in Hindley-Milner, it becomes complicated with GADTs](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/tldi10-vytiniotis.pdf).

**Consequences:**
- `let x = match gadt with ...` gives monomorphic type
- Reusing `x` at different types fails
- Developer forced to eta-expand: `let x = fun () -> match gadt with ...` (ugly)

**Prevention:**
1. **Relaxed value restriction:** Allow generalization of GADT matches that are syntactically values
2. **Explicit polymorphism:** Require type signatures on polymorphic GADT bindings
3. **Document workaround:** Eta-expansion makes it nonexpansive: `let x = fun () -> ...`
4. For initial implementation: Accept the limitation, document clearly

**Detection:**
- Test: `let id = match (Int : int t) with Int -> fun x -> x` should be polymorphic
- Test reuse: `id 1`, `id "hello"` should both work
- If not: ensure eta-expanded version works

**Phase mapping:** Phase 2 (ADT/GADT) - Later refinement, not initial blocker

**References:**
- [Let Generalization, Polymorphic Recursion, and Variable Minimization](https://dl.acm.org/doi/pdf/10.1145/3776644)
- [SML '97 Value Restriction](https://www.smlnj.org/doc/Conversion/types.html)

---

### Pitfall 11: Module Name Resolution Interacts Badly with Type Inference

**What goes wrong:** Opening a module or changing file order causes type inference to fail or infer different types for the same code.

**Why it happens:** [F# is order-sensitive - modules only know about modules above them](https://gist.github.com/swlaschin/31d5a0a2c4478e82e3ed60d653c0206b). Type inference uses name resolution to find types, and [the order of open statements matters](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/namespaces). Opening module B before A vs A before B can shadow names differently.

**Consequences:**
- Same code infers different types depending on file order
- Changing module import order breaks code
- Cryptic type errors when shadowing occurs
- Refactoring risk: moving code between files changes types

**Prevention:**
1. **Qualified names by default:** Encourage `Module.function` instead of `open Module`
2. **Explicit type annotations:** Reduce reliance on inference for public API
3. **Open statements at top:** Establish consistent ordering convention
4. **Shadowing warnings:** Warn when opening module shadows existing names
5. For LangThree: Document that file order matters, provide clear error when module not found

**Detection:**
- Test: Two files with same function name, import both in different orders
- Type inference should either: (a) error "ambiguous", or (b) use last opened module consistently
- Change file compile order and verify behavior stays consistent

**Phase mapping:** Phase 4 (Modules) - Design interaction between modules and type inference

**References:**
- [F# declaration order](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.1/FS-1009-mutually-referential-types-and-modules-single-scope.md)
- [Effective F# tips](https://gist.github.com/swlaschin/31d5a0a2c4478e82e3ed60d653c0206b)
- [F# understanding type inference](https://fsharpforfunandprofit.com/posts/type-inference/)

---

### Pitfall 12: Row Polymorphism Loses Information vs Subtyping

**What goes wrong:** Type inference for records infers types that are more restrictive than necessary, or loses precision about which fields exist.

**Why it happens:** [Row polymorphism isn't subtyping](https://brianmckenna.org/blog/row_polymorphism_isnt_subtyping), and [subtyping loses information via subsumption rule](https://dev.to/maxheiber/subtyping-loses-information-row-polymorphism-does-not-2mb9). [OCaml lacks flow-sensitive typing for polymorphic variants](https://ahnfelt.medium.com/row-polymorphism-crash-course-587f1e7b7c47), making inferred types too restrictive.

**Consequences:**
- Function `f {x: int}` can't accept `{x: int; y: string}` even though it should
- Type inference fails where structural typing should work
- Developers forced to use subtyping (if available) or give up on polymorphism

**Prevention:**
1. **Design decision:** Choose row polymorphism OR structural subtyping, not both
2. **If row polymorphism:** Implement proper row variables: `{x: int | r}` means "x field plus other fields r"
3. **If subtyping:** Accept that type inference is harder, may need more annotations
4. **For LangThree initial version:** Simple nominal records (no row polymorphism) to avoid complexity
5. Defer row polymorphism to later milestone

**Detection:**
- Test: Function `let f r = r.x` applied to `{x: 1; y: 2}`
- Should either: (a) work with inferred type `{x: int | r}`, or (b) error clearly "requires structural typing"

**Phase mapping:** Phase 3 (Records) - Design decision, likely defer row polymorphism

**References:**
- [Row polymorphism isn't subtyping](https://brianmckenna.org/blog/row_polymorphism_isnt_subtyping)
- [Subtyping loses information](https://dev.to/maxheiber/subtyping-loses-information-row-polymorphism-does-not-2mb9)
- [Row polymorphism crash course](https://ahnfelt.medium.com/row-polymorphism-crash-course-587f1e7b7c47)

---

### Pitfall 13: Record Field Punning Parsing Ambiguity

**What goes wrong:** Syntax like `{name}` is ambiguous between field punning (`{name = name}`) and other constructs, causing parser errors.

**Why it happens:** [OCaml supports field punning](https://dev.realworldocaml.org/records.html) where `{service_name; port}` means `{service_name = service_name; port = port}`. This requires parser to track whether identifier is a field name or not. [Extended punning for destructuring raised ambiguity concerns](https://github.com/ocaml/ocaml/pull/3).

**Consequences:**
- Parser conflicts when pattern could be punned field or other construct
- User confusion: when does `{x}` mean `{x = x}` vs something else?
- Syntax changes break existing code

**Prevention:**
1. **Explicit syntax (recommended for initial version):** Require `{name = name}`, no punning
2. **If adding punning:** Only in record literals and patterns, not expressions
3. **Grammar analysis:** Check fsyacc reports no shift/reduce conflicts
4. **Clear scoping rule:** Punning only valid when field names are in scope (inside record type context)
5. Test parser on ambiguous-looking constructs

**Detection:**
- Test: `{x}` should either error "punning not supported" or work with clear semantics
- Test: `let {x} = r` vs `let {x = y} = r` should both work or error consistently

**Phase mapping:** Phase 3 (Records) - Syntax design decision

**References:**
- [OCaml record punning](https://dev.realworldocaml.org/records.html)
- [Extended punning proposal](https://github.com/ocaml/ocaml/pull/3)
- [Destruct punned field syntax issue](https://github.com/ocaml/merlin/pull/1734)

---

### Pitfall 14: Exception Handling Without Resource Cleanup (No RAII)

**What goes wrong:** Exceptions bypass resource cleanup code, leaking files, memory, connections.

**Why it happens:** ML-family languages lack RAII (Resource Acquisition Is Initialization) like C++/Rust. [C++ guarantees destructors are called during stack unwinding](https://www.hellocpp.dev/concepts/raii-principle), but ML requires explicit `try...finally` or equivalent. [Forgetting finally blocks is easy](https://www.incredibuild.com/glossary/raii-resource-acquisition-is-initialization) - "you can't forget to call a destructor, but you can forget to write a finally block."

**Consequences:**
- File handles leak
- Database connections not closed
- Locks not released (deadlock)
- Memory not freed (if manual memory management)
- Non-deterministic failures under exception conditions

**Prevention:**
1. **try...finally construct (essential):** Implement `try expr with exn -> handler finally cleanup` syntax
2. **use binding (F#-style):** `use file = open "x"` automatically disposes at scope exit
3. **Documentation:** Emphasize that resources MUST be in try...finally
4. **Bracket pattern:** Provide `bracket acquire release use` function in standard library
5. For initial implementation: Just try...with is OK, add finally in refinement

**Detection:**
- Test: Open file, raise exception in middle, check file is closed
- Test: Without finally, file handle leaks (check OS file descriptor count)
- With finally, file handle cleaned up

**Phase mapping:** Phase 5 (Exceptions) - Add finally soon after basic exceptions

**References:**
- [RAII principle](https://www.hellocpp.dev/concepts/raii-principle)
- [RAII guarantees cleanup](https://blog.truegeometry.com/api/exploreHTML/4c2fc5d71bf5b1869008fa3315aa1ab6.exploreHTML)

---

## Minor Pitfalls

Mistakes that cause annoyance but are fixable.

### Pitfall 15: Indentation Errors Have Poor Error Messages

**What goes wrong:** Parser reports "syntax error" or "unexpected EOF" instead of "indentation mismatch at line 42."

**Why it happens:** INDENT/DEDENT tokens are generated in lexer, but parser only sees generic tokens. When parser fails, error message doesn't know the failure was indentation-related.

**Prevention:**
1. **Error token metadata:** Attach source location and indentation context to each token
2. **Lexer error reporting:** When indentation doesn't match any stack level, report "dedent doesn't match any outer indentation level"
3. **Parser error recovery:** When parse fails on DEDENT, report "possible indentation error"
4. Python-style: Show expected indentation level vs actual

**Detection:**
- Test: Deliberately mis-indent code
- Error should mention indentation, not just "syntax error"

**Phase mapping:** Phase 1 (Indentation) - Error message polish pass

---

### Pitfall 16: Bidirectional Typing Direction Mistakes

**What goes wrong:** Type checker checks in wrong direction (inference when should check, checking when should infer), causing confusing errors.

**Why it happens:** [Bidirectional typing](https://arxiv.org/pdf/1908.05839) has two modes: checking (given expected type, verify expression matches) and inference (compute type of expression). GADTs and higher-rank types require careful choice of direction. [Earlier work had invalid decidability and completeness arguments](https://www.cl.cam.ac.uk/~nk480/bidir.pdf).

**Prevention:**
1. **Mode discipline:** Annotations switch to checking mode, no annotation uses inference mode
2. **GADT patterns:** Always check mode (expected type known from signature)
3. **Application:** Infer function type, check argument against parameter type
4. Test suite with examples from bidirectional typing papers

**Detection:**
- Type error messages say "expected X, got Y" when should say "cannot infer type"
- GADT examples fail to type check even with annotations

**Phase mapping:** Phase 2 (ADT/GADT) - Core algorithm correctness

**References:**
- [Bidirectional Typing](https://arxiv.org/pdf/1908.05839) (Dunfield & Krishnaswami)
- [Complete and Easy Bidirectional Typechecking](https://www.cl.cam.ac.uk/~nk480/bidir.pdf)

---

### Pitfall 17: Pattern Matching Order Affects GADT Type Refinement

**What goes wrong:** Swapping order of pattern match branches causes type checking to fail.

**Why it happens:** [Type refinement from earlier patterns enables later patterns to typecheck](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/gadt.html). GHC typechecks patterns left-to-right. If later pattern relies on constraint from earlier pattern, reordering breaks it.

**Prevention:**
1. **Document ordering sensitivity:** Type refinement is top-to-bottom, left-to-right
2. **Deterministic algorithm:** Don't let pattern order affect semantics, only typing
3. **Warning:** Warn if pattern order seems to matter (heuristic: if swapping changes typing result)

**Detection:**
- Test: GADT match where branch order matters for typing
- Should either: (a) work in any order, or (b) error clearly "earlier patterns must establish constraints"

**Phase mapping:** Phase 2 (ADT/GADT) - Refinement after basic GADT support

**References:**
- [GHC GADT documentation](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/gadt.html)

---

### Pitfall 18: Extensible Exception Type Causes Catch-All Issues

**What goes wrong:** Pattern match `try expr with E x -> ...` fails to catch some exceptions, or catches too many.

**Why it happens:** [exn type is extensible](https://ocaml.org/manual/5.4/extensiblevariants.html) - new constructors added at runtime. [Pattern matching on extensible variant requires default case](https://ocaml.org/docs/error-handling) to handle unknown constructors. [Catch-all cases are error-prone with polymorphic variants](https://dev.realworldocaml.org/variants.html).

**Prevention:**
1. **Require catch-all:** Force `with ... | _ -> ...` for exception handlers
2. **Warning:** Warn if exception match doesn't have default case
3. **Specific handlers first:** Put specific exception patterns before generic ones
4. Documentation: "Always include default exception handler"

**Detection:**
- Test: Define new exception, raise it, catch with old pattern match
- Without default case, exception should propagate
- With default case, should be caught

**Phase mapping:** Phase 5 (Exceptions) - Pattern match completeness checking

**References:**
- [OCaml extensible variants](https://ocaml.org/manual/5.4/extensiblevariants.html)
- [OCaml error handling](https://ocaml.org/docs/error-handling)

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Phase 1: Indentation | INDENT/DEDENT token buffering fails | Implement token queue in lexer header, test multi-level dedent |
| Phase 1: Indentation | Mixed tabs/spaces silently accepted | Reject tabs entirely OR convert consistently with mixing detection |
| Phase 1: Indentation | LALR(1) can't handle offside rule | Put indentation logic in LEXER, emit tokens, parser stays context-free |
| Phase 2: ADT/GADT | Type inference hangs on GADT | Require type annotations on GADT pattern matches, document clearly |
| Phase 2: ADT/GADT | Rigid type variables escape | Implement rigid/wobbly type system, check scope on branch exit |
| Phase 2: ADT/GADT | Exhaustiveness with GADTs wrong | Allow refutation clauses OR document limitation early |
| Phase 2: ADT/GADT | Value restriction too strict | Accept limitation initially, consider relaxation later |
| Phase 3: Records | Field name collision | Choose: module namespacing OR unique names OR type-directed resolution |
| Phase 3: Records | Row polymorphism complexity | Defer row polymorphism - use simple nominal records initially |
| Phase 3: Records | Field punning ambiguity | Skip punning for v1, add later if grammar stays unambiguous |
| Phase 4: Modules | Circular dependencies | Enforce strict file ordering (F# style), clear error messages with cycle path |
| Phase 4: Modules | Name resolution order | Document order sensitivity, encourage qualified names |
| Phase 5: Exceptions | No resource cleanup | Add try...finally construct, document bracket pattern |
| Phase 5: Exceptions | Extensible exn type matching | Require default case in exception handlers, warn if missing |
| Phase 5: Exceptions | Marshalling unsafety | Document that exn cannot be serialized (not a priority for v1) |

---

## Implementation Priority Recommendations

### Must Address in Initial Implementation

1. **Indentation token buffering** (Pitfall 1) - Core functionality
2. **Tabs vs spaces policy** (Pitfall 2) - User experience
3. **GADT type annotation requirement** (Pitfall 3) - Type system soundness
4. **Rigid type variable scoping** (Pitfall 4) - Type safety
5. **Record field disambiguation** (Pitfall 6) - Design decision
6. **Circular module prevention** (Pitfall 7) - Build system

### Can Defer to Refinement

1. **GADT exhaustiveness checking** (Pitfall 5) - Complex, can add refutation clauses later
2. **Value restriction relaxation** (Pitfall 10) - Workarounds exist
3. **Row polymorphism** (Pitfall 12) - Use nominal records initially
4. **Field punning** (Pitfall 13) - Nice-to-have syntax sugar
5. **Exception marshalling** (Pitfall 8) - No serialization in v1

### Error Message Polish

1. **Indentation error messages** (Pitfall 15)
2. **Better circular dependency diagnostics** (Pitfall 7)
3. **GADT type error clarity** (Pitfalls 3, 4)

---

## Testing Checklist

For each major feature, test these specific edge cases:

**Indentation:**
- [ ] Multi-level dedent (8→4→0 emits 2 DEDENTs)
- [ ] Mixed tabs/spaces rejected or normalized
- [ ] Dedent to non-matching level errors clearly
- [ ] Empty lines ignored
- [ ] Comments don't affect indentation

**GADTs:**
- [ ] Without type annotation: clear error "requires annotation"
- [ ] With annotation: type checks correctly
- [ ] Rigid type variable can't escape branch scope
- [ ] Polymorphic recursion works with explicit forall
- [ ] Exhaustiveness checking with impossible cases

**Records:**
- [ ] Two types with same field name: either error or disambiguate
- [ ] Field access with known type resolves correctly
- [ ] Field access with unknown type errors clearly

**Modules:**
- [ ] Circular dependency A→B→A detected with path shown
- [ ] File order matters: later file can't reference earlier file's internals
- [ ] Module not found error is clear

**Exceptions:**
- [ ] try...with catches correct exception types
- [ ] Unknown exception propagates through handler
- [ ] try...finally cleanup runs even when exception raised
- [ ] Nested try...with works correctly

---

## Sources

### Indentation Parsing
- [Principled Parsing for Indentation-Sensitive Languages](https://michaeldadams.org/papers/layout_parsing/LayoutParsing.pdf) (Adams 2013)
- [Python Lexical Analysis - Indentation](https://docs.python.org/3/reference/lexical_analysis.html)
- [Off-side rule - Wikipedia](https://en.wikipedia.org/wiki/Off-side_rule)
- [Python-like indentation with fslex and fsyacc](https://staging.fpish.net/topic/None/58109)
- [F# Lexing and Parsing - Wikibooks](https://en.wikibooks.org/wiki/F_Sharp_Programming/Lexing_and_Parsing)
- [F# formatting guidelines - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)

### GADT Type Inference
- [Simple unification-based type inference for GADTs](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/gadt-pldi.pdf) (Peyton Jones et al. 2006)
- [What are GADTs and why do they make type inference sad?](https://blog.polybdenum.com/2024/03/03/what-are-gadts-and-why-do-they-make-type-inference-sad.html)
- [Un-obscuring GHC type error messages](https://free.cofree.io/2020/09/01/type-errors/)
- [GHC GADT documentation](https://ghc.gitlab.haskell.org/ghc/doc/users_guide/exts/gadt.html)
- [GADT exhaustiveness checking](https://github.com/ocaml/ocaml/issues/7028)
- [GADTs and exhaustiveness: looking for the impossible](https://www.math.nagoya-u.ac.jp/~garrigue/papers/gadtspm.pdf) (Garrigue & Le Normand)

### Bidirectional Type Checking
- [Bidirectional Typing](https://arxiv.org/pdf/1908.05839) (Dunfield & Krishnaswami 2019)
- [Complete and Easy Bidirectional Typechecking](https://www.cl.cam.ac.uk/~nk480/bidir.pdf) (Dunfield & Krishnaswami)
- [Sound and Complete Bidirectional Typechecking for GADTs](https://www.cl.cam.ac.uk/~nk480/gadt.pdf)

### Records and Row Polymorphism
- [Records - Real World OCaml](https://dev.realworldocaml.org/records.html)
- [Row polymorphism isn't subtyping](https://brianmckenna.org/blog/row_polymorphism_isnt_subtyping)
- [Row polymorphism crash course](https://ahnfelt.medium.com/row-polymorphism-crash-course-587f1e7b7c47)
- [Subtyping loses information](https://dev.to/maxheiber/subtyping-loses-information-row-polymorphism-does-not-2mb9)
- [Using type-propagation to disambiguate label names](http://gallium.inria.fr/blog/resolving-field-names/)

### Module Systems
- [Refactoring to remove cyclic dependencies](https://fsharpforfunandprofit.com/posts/removing-cyclic-dependencies/) (F# for fun and profit)
- [ocamldep circular dependency bug](https://github.com/ocaml/ocaml/issues/4618)
- [OCaml Cyclical Build Dependencies](https://wiki.xenproject.org/wiki/OCaml_Cyclical_Build_Dependencies)
- [F# mutually referential types and modules](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.1/FS-1009-mutually-referential-types-and-modules-single-scope.md)
- [Effective F# tips](https://gist.github.com/swlaschin/31d5a0a2c4478e82e3ed60d653c0206b)

### Exception Handling
- [OCaml extensible variants](https://ocaml.org/manual/5.4/extensiblevariants.html)
- [OCaml error handling](https://ocaml.org/docs/error-handling)
- [distwit: Distribute instances of extensible variant types](https://github.com/let-def/distwit)
- [RAII principle](https://www.hellocpp.dev/concepts/raii-principle)

### Parsers and Yacc
- [Handling conflicts in Yacc parsers](https://www2.cs.arizona.edu/~debray/Teaching/CSc453/DOCS/conflicts.pdf) (Debray)
- [Implementing a Parser with Yacc](http://www.cs.columbia.edu/~aho/cs4115/Lectures/15-02-23.html) (Columbia CS)

### Type System Theory
- [Let Generalization, Polymorphic Recursion](https://dl.acm.org/doi/pdf/10.1145/3776644) (2026)
- [SML '97 Value Restriction](https://www.smlnj.org/doc/Conversion/types.html)
- [Understanding type inference - F# for fun and profit](https://fsharpforfunandprofit.com/posts/type-inference/)
