# Project Research Summary

**Project:** LangThree - ML-style functional language with F# features
**Domain:** Programming language implementation (interpreter with type inference)
**Researched:** 2026-02-25
**Confidence:** HIGH

## Executive Summary

LangThree extends FunLang v6.0 (an existing Hindley-Milner type inferencer with bidirectional type checking) by adding six major features: indentation-based syntax, algebraic data types (ADT), generalized algebraic data types (GADT), records, F#-style modules, and exceptions. Research indicates this is a well-trodden path in programming language implementation, with established patterns from Python (indentation), OCaml/Haskell (GADTs), and F# (modules).

The recommended approach follows a staged implementation: start with indentation syntax (affects lexer only), then build type system features (ADT, GADT, Records) leveraging existing Hindley-Milner infrastructure, followed by organizational features (Modules) and runtime support (Exceptions). The critical architectural insight is that FunLang's existing bidirectional type checker provides the foundation needed for GADTs, making this extension incremental rather than revolutionary. Indentation parsing via lexer token injection (INDENT/DEDENT tokens) keeps the parser context-free and avoids grammar ambiguity.

Key risks center on three areas: (1) indentation lexer state management across multiple DEDENT tokens, (2) GADT type inference requiring mandatory type annotations to avoid undecidability, and (3) circular module dependencies breaking builds. Mitigation strategies are well-documented: implement Python's indentation stack algorithm, enforce explicit GADT annotations with clear error messages, and adopt F#'s strict file-ordering discipline for modules.

## Key Findings

### Recommended Stack

The stack inherits FunLang v6.0's foundation: F# on .NET 10, with fslex/fsyacc (FsLexYacc 12.x+) for lexer and parser generation. This choice is prescriptive—FunLang already provides Hindley-Milner type inference and bidirectional type checking, which are exactly what's needed for ADT/GADT support.

**Core technologies:**
- **F# / .NET 10**: Implementation language — required by FunLang base, excellent for compiler work with algebraic types and pattern matching
- **fslex (FsLexYacc)**: Lexer generation — OCamllex-compatible, supports mutable state for indentation stack
- **fsyacc (FsLexYacc)**: LALR parser generation — OCamlyacc-compatible, proven for ML-family languages
- **FunLang v6.0**: Type inference foundation — provides Hindley-Milner + bidirectional checking, eliminating need to build type system from scratch

**Key insight from STACK.md:** Indentation is purely a lexer concern (emit INDENT/DEDENT tokens), keeping parser grammar context-free. GADTs leverage existing bidirectional type checking rather than extending Hindley-Milner, which would break principal types property.

### Expected Features

Research reveals a clear three-tier feature classification based on ML-family language expectations.

**Must have (table stakes):**
- **Indentation-based syntax**: Offside rule for blocks, let-bindings, match expressions — F# syntax principle
- **ADT with pattern matching**: Sum types with constructors, exhaustiveness checking, type parameters — core ML feature
- **Records with field access**: Named fields, dot notation, copy-and-update syntax, structural equality — standard data structure
- **Module system (F# style)**: Top-level modules, namespace declarations, `open` keyword, qualified names — code organization
- **Basic GADT support**: Explicit constructor return types, type refinement in pattern matching — enables typed DSLs
- **Exception handling**: Exception declarations, `raise`, `try...with` expressions — error handling

**Should have (competitive):**
- **GADT exhaustiveness checking**: Refutation clauses for impossible patterns — advanced but expected in modern languages
- **Module recursive support**: `module rec` for mutual recursion — convenience feature, not critical for v1
- **try...finally**: Resource cleanup guarantees — RAII replacement for safety

**Defer (v2+):**
- **Row polymorphism for records**: Extensible record types `{x: int | r}` — complex type system feature
- **Record field punning**: `{name}` shorthand for `{name = name}` — syntax sugar
- **Anonymous records**: F# `{| x = 1 |}` syntax — lightweight but not essential
- **Module signatures**: `.fsi` interface files for separate compilation — scalability feature

**Anti-features (explicitly excluded):**
- **Mixed tabs/spaces**: Indentation source of bugs — enforce spaces-only
- **OCaml functors**: Parameterized modules too complex — F#-style simple modules only
- **Checked exceptions**: Java-style throws clauses break ML semantics — dynamic exceptions only
- **Automatic GADT inference**: Undecidable in general — require explicit annotations

### Architecture Approach

LangThree maintains FunLang's pipeline architecture: Lexer → Parser → Type Checker → Evaluator. Each new feature extends specific pipeline stages without breaking the overall flow. The key architectural principle is respecting component boundaries—indentation is lexer-only, type system features affect Type Checker/AST, modules affect all components via namespace resolution, exceptions affect runtime only.

**Major components:**
1. **Lexer (Lexer.fsl)** — Tokenization + indentation stack management (emit INDENT/DEDENT tokens)
2. **Parser (Parser.fsy, Ast.fs)** — Syntax analysis, AST construction (new nodes: TypeDef, RecordExpr, ModuleDef, TryWith)
3. **Type Checker (Type.fs, Infer.fs, Bidir.fs)** — Hindley-Milner inference for ADT/Records, bidirectional checking for GADTs
4. **Module System (new: Modules.fs)** — Two-phase compilation: collect signatures, resolve qualified names
5. **Evaluator (Eval.fs)** — Runtime execution, pattern matching (new: record ops, exception unwinding)
6. **Exception Support (new: Exceptions.fs)** — Stack unwinding, handler matching (leverages F# exception mechanism)

**Component independence:** Indentation (lexer) is independent of type system features. ADT, GADT, Records can be developed in parallel (different AST nodes, different type rules). Modules require stable type system but are independent of runtime. Exceptions are orthogonal to type features (mostly runtime concern).

### Critical Pitfalls

Top 5 pitfalls from domain research, with prevention strategies:

1. **Indentation lexer state corruption** — Multiple DEDENT tokens on a single line (e.g., indent 8→0 requires two DEDENTs) require token buffering. **Prevention:** Implement explicit indentation stack with token queue, use Python's algorithm (compare current indent to stack top, emit multiple DEDENTs as needed). Test edge case: three-level dedent in one line.

2. **Mixed tabs and spaces** — Code appears correct visually but parser rejects with confusing errors. Tabs render differently (2/4/8 spaces) across editors. **Prevention:** Follow F# approach—reject tabs entirely with clear error "tabs not allowed, use spaces". Strict spaces-only mode prevents invisible bugs.

3. **GADT type inference undecidability** — Type inference for GADTs is undecidable without annotations. Hindley-Milner assumes principal types, but GADTs break this property. **Prevention:** Require explicit type signatures on GADT pattern matches. Document clearly: "GADTs require type annotations—this is fundamental, not a bug." Use bidirectional type checking where annotations guide inference.

4. **GADT rigid type variables escaping scope** — Existential types from GADT pattern matching leak into broader scope, causing type unsoundness. **Prevention:** Implement rigid/wobbly type system—mark GADT-opened existentials as rigid (cannot unify with anything), check no rigid variables escaped when leaving branch scope.

5. **Circular module dependencies** — Two modules depend on each other, breaking build even when no actual value-level cycle exists. ML-family languages require declaration-before-use ordering. **Prevention:** Enforce strict file ordering (F# style)—no circularity allowed. Use `and` keyword for mutual recursion in same file. Provide clear error messages with cycle path: "A.fs → B.fs → A.fs".

## Implications for Roadmap

Based on research, suggested phase structure follows dependency order and risk profile:

### Phase 1: Indentation-Based Syntax
**Rationale:** Indentation affects all subsequent parsing—must be stable before implementing other features. Lexer is foundation that all downstream components depend on.
**Delivers:** Parser accepts indentation-based syntax for all language constructs (let-bindings, match expressions, modules)
**Addresses:** Offside rule implementation, spaces-only enforcement, INDENT/DEDENT token generation
**Avoids:** Pitfall #1 (indentation state corruption) via token queue, Pitfall #2 (mixed tabs/spaces) via strict rejection
**Complexity:** Medium-High (lexer state management is tricky)
**Research flag:** LOW—Python's algorithm is well-documented and proven

### Phase 2: Algebraic Data Types (ADT)
**Rationale:** Simplest type system extension, provides foundation for GADTs. Extends existing pattern matching rather than rebuilding.
**Delivers:** Sum types with constructors, pattern matching, exhaustiveness checking, type parameters
**Uses:** Hindley-Milner inference (Infer.fs), fsyacc grammar extensions for type declarations
**Implements:** Type.fs (TData type), Ast.fs (TypeDef node), Eval.fs (constructor pattern matching)
**Addresses:** Core data modeling capability expected in ML-family languages
**Complexity:** Medium (standard HM extension)
**Research flag:** LOW—well-established patterns from OCaml/F#

### Phase 3: Records
**Rationale:** Independent of ADT/GADT, can be developed in parallel with Phase 4. Simpler than modules (no cross-file dependencies).
**Delivers:** Record types, field access, copy-and-update syntax, structural equality, pattern matching
**Uses:** Structural typing (nominal requires more infrastructure), Type.fs (TRecord), Unify.fs (field unification)
**Implements:** RecordExpr AST nodes, runtime representation as Map<string, Value>
**Addresses:** Named product types, immutable data structures
**Avoids:** Pitfall #6 (field name collisions) via module namespacing or type-directed disambiguation
**Complexity:** Medium (type inference + structural typing)
**Research flag:** LOW—standard record semantics, defer row polymorphism

### Phase 4: Generalized Algebraic Data Types (GADT)
**Rationale:** Builds on ADT infrastructure. Requires bidirectional checking (already in FunLang). Can be parallel with Phase 3.
**Delivers:** Explicit constructor return types, type refinement in pattern matching, indexed type families
**Uses:** Bidirectional type checking (Bidir.fs), equational constraints in unification
**Implements:** GADTConstructor AST extensions, TGADTInstance types, rigid/wobbly type variables
**Addresses:** Type-safe DSLs, typed expression evaluators
**Avoids:** Pitfall #3 (undecidable inference) via mandatory annotations, Pitfall #4 (rigid variable escape) via scope checking
**Complexity:** High (type system extension with refinement)
**Research flag:** MEDIUM—bidirectional typing integration needs careful design

### Phase 5: Module System
**Rationale:** Requires complete type system (ADT/Records/GADT must exist to put into modules). Affects all components but builds on type system work.
**Delivers:** Top-level modules, namespace declarations, nested modules, `open` keyword, qualified name resolution
**Uses:** Two-phase compilation (collect signatures, then resolve names), topological sort for dependencies
**Implements:** New Modules.fs, Env.fs extensions, all files support qualified names
**Addresses:** Code organization, namespace management
**Avoids:** Pitfall #7 (circular dependencies) via strict file ordering and cycle detection
**Complexity:** Medium (coordination across components, dependency ordering)
**Research flag:** MEDIUM—two-phase compilation needs careful design

### Phase 6: Exceptions
**Rationale:** Most isolated feature. Primarily runtime concern, leverages F# exception mechanism. Can be developed anytime after ADT (exception constructors are ADT-like).
**Delivers:** Exception declarations, `raise` function, `try...with` expressions, pattern matching on exception types
**Uses:** Extensible variant types (exn), stack unwinding via F# exceptions
**Implements:** New Exceptions.fs, Eval.fs handler stack, TryWith/Raise AST nodes
**Addresses:** Error handling, control flow
**Avoids:** Resource leak pitfalls via eventual try...finally support
**Complexity:** Low-Medium (leverages existing infrastructure)
**Research flag:** LOW—F# exception semantics are well-defined

### Phase Ordering Rationale

**Sequential dependencies:**
- Indentation → everything (lexer is foundation)
- ADT → GADT (GADT extends ADT syntax and type checking)
- Type system (ADT/GADT/Records) → Modules (modules contain typed declarations)

**Parallel opportunities:**
- After Indentation: ADT foundation work can start
- After ADT: GADT and Records can proceed in parallel (different type system concerns)
- After Modules: Exceptions can be developed independently

**Risk-based ordering:**
- Highest risk (Indentation lexer state, GADT type inference) tackled early when flexibility is high
- Lower risk (Records, Exceptions) can be deferred or developed in parallel
- Modules in middle—coordination overhead but standard patterns exist

**Total estimate:** 7-12 weeks sequential, 6-9 weeks with parallel work (Phase 3 || Phase 4)

### Research Flags

**Phases needing deeper research during planning:**
- **Phase 4 (GADT):** Bidirectional type checking integration with FunLang's existing Bidir.fs—may need prototype to validate approach
- **Phase 5 (Modules):** Two-phase compilation and topological sorting—need to design module signature extraction algorithm

**Phases with standard patterns (skip research-phase):**
- **Phase 1 (Indentation):** Python's algorithm is authoritative, well-documented
- **Phase 2 (ADT):** Standard Hindley-Milner extension, OCaml/F# patterns apply directly
- **Phase 3 (Records):** Structural typing is straightforward, ML semantics are clear
- **Phase 6 (Exceptions):** F# exception mechanism can be leveraged directly

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | **HIGH** | FunLang v6.0 base is prescriptive, fslex/fsyacc are standard F# tools with official docs |
| Features | **HIGH** | Feature expectations based on official F#/OCaml docs and academic papers on GADTs |
| Architecture | **HIGH** | Pipeline architecture is proven for interpreters, component boundaries are clear |
| Pitfalls | **MEDIUM-HIGH** | Critical pitfalls well-documented (Python indentation, GADT papers), some edge cases may emerge |

**Overall confidence:** **HIGH**

### Gaps to Address

Areas where research was inconclusive or needs validation during implementation:

- **GADT exhaustiveness checking:** Complex algorithm, may need to defer refutation clauses to post-v1. Literature exists but implementation details are sparse. **Handling:** Start with basic exhaustiveness warnings, accept some false positives for GADTs initially.

- **Record field disambiguation strategy:** Choice between module namespacing, type-directed resolution, or unique names constraint. All are valid; needs design decision. **Handling:** Start with module namespacing (simplest), can add type-directed resolution later if needed.

- **Exception marshalling safety:** Not a priority for v1 (no serialization), but worth documenting limitation. **Handling:** Add warning in docs that `exn` type cannot be marshaled across processes.

- **Module mutual recursion:** F#'s `module rec` allows cycles, but adds complexity. **Handling:** Skip for v1, enforce acyclic dependencies. Can add `module rec` in later version if demand exists.

- **Value restriction with GADTs:** GADT let-bindings may become monomorphic due to value restriction. **Handling:** Accept limitation initially, document eta-expansion workaround. Consider relaxed value restriction in future.

## Feature Summary Table

Comprehensive view of all features with complexity and dependencies for roadmap planning:

| Feature | Phase | Complexity | Dependencies | Critical Pitfall | Research Needed |
|---------|-------|------------|--------------|------------------|-----------------|
| **Indentation syntax** | 1 | Medium-High | None | Token buffering, tabs/spaces | LOW (Python algorithm) |
| **ADT (basic)** | 2 | Medium | Indentation | None major | LOW (standard HM) |
| **Records** | 3 | Medium | Indentation, Type system | Field name collisions | LOW (ML semantics) |
| **GADT** | 4 | High | ADT, Bidir.fs | Type inference, rigid vars | MEDIUM (integration) |
| **Modules** | 5 | Medium | Type system complete | Circular dependencies | MEDIUM (2-phase compile) |
| **Exceptions** | 6 | Low-Medium | ADT (constructors) | Resource cleanup | LOW (F# semantics) |

## Sources

### Primary (HIGH confidence)
- [Python 3.14 Lexical Analysis](https://docs.python.org/3/reference/lexical_analysis.html) — Official indentation algorithm specification
- [FsLexYacc GitHub](https://github.com/fsprojects/FsLexYacc) — fslex/fsyacc capabilities and examples
- [F# Language Reference - Modules](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules) — Module system semantics
- [F# Language Reference - Records](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records) — Record type specifications
- [Simple unification-based type inference for GADTs](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/gadt-pldi.pdf) — Simon Peyton Jones et al., foundational GADT type checking

### Secondary (MEDIUM confidence)
- [Bidirectional Typing](https://arxiv.org/pdf/1908.05839) — Jana Dunfield comprehensive survey on bidirectional type checking
- [Principled Parsing for Indentation-Sensitive Languages](https://michaeldadams.org/papers/layout_parsing/LayoutParsing.pdf) — Formal offside rule specification
- [Real World OCaml - Records](https://dev.realworldocaml.org/records.html) — OCaml record semantics and patterns
- [Real World OCaml - GADTs](https://dev.realworldocaml.org/gadts.html) — Practical GADT examples and type checking
- [F# for fun and profit - Removing cyclic dependencies](https://fsharpforfunandprofit.com/posts/removing-cyclic-dependencies/) — Module dependency best practices

### Tertiary (LOW confidence - validation needed)
- [Marcel Goh - Scanning Spaces](https://marcelgoh.ca/2019/04/14/scanning-spaces.html) — Practical lexer implementation blog post
- [Thunderseethe - HM vs Bidirectional](https://thunderseethe.dev/posts/how-to-choose-between-hm-and-bidir/) — Type system design tradeoffs
- Community discussions on GADTs, row polymorphism, module systems (various forum posts cited in research files)

---
*Research completed: 2026-02-25*
*Ready for roadmap: yes*
