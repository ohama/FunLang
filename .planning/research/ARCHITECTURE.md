# Architecture Patterns for LangThree

**Domain:** Programming Language Interpreter (ML-style functional language)
**Researched:** 2026-02-25
**Confidence:** HIGH

## Executive Summary

LangThree extends FunLang's existing interpreter pipeline (Lexer → Parser → Type Checker → Evaluator) with five new features. Each feature primarily impacts specific components while maintaining the pipeline architecture. The key architectural insight is that features can be implemented with minimal cross-component coupling by respecting the pipeline boundaries.

**Critical path:** Indentation-based syntax requires lexer changes first, as it affects the token stream that all downstream components depend on. Type system features (ADT/GADT, Records) can be developed in parallel after AST/type checker infrastructure is ready. Modules require coordination across all components but build on type system work. Exceptions require runtime support but are largely orthogonal to type system features.

## Recommended Architecture

### Overall Pipeline (Unchanged)

```
Source Text
    ↓
[Lexer.fsl] → Token Stream
    ↓
[Parser.fsy] → AST (Ast.fs)
    ↓
[Type Checker] → Typed AST
    ├─ Infer.fs (Hindley-Milner)
    └─ Bidir.fs (Bidirectional)
    ↓
[Evaluator] → Result
    └─ Eval.fs
```

This pipeline remains the foundation. Each new feature extends specific stages without breaking the overall flow.

### Component Boundaries

| Component | Responsibility | Input | Output | Files Affected |
|-----------|---------------|-------|--------|----------------|
| **Lexer** | Tokenization + layout rules | Source text | Token stream with INDENT/DEDENT | Lexer.fsl (new: indentation stack) |
| **Parser** | Syntax analysis | Token stream | Untyped AST | Parser.fsy, Ast.fs (new: ADT, Records, Modules, Exceptions) |
| **Type System** | Type inference/checking | Untyped AST | Typed AST or type errors | Type.fs (new types), Infer.fs (ADT/GADT), Bidir.fs (GADT refinement) |
| **Module System** | Name resolution | Typed AST | Symbol table + resolved AST | New: Modules.fs, Env.fs extensions |
| **Evaluator** | Execution | Typed/resolved AST | Runtime values or exceptions | Eval.fs (new: record ops, exceptions) |
| **Runtime Support** | Exception handling | N/A | Exception propagation | New: Exceptions.fs (exception values, unwinding) |

### Data Flow

#### 1. Indentation-Based Syntax Flow

```
Source text with indentation
    ↓
Lexer: Track indentation stack
    ├─ On newline: compare current vs. previous indent
    ├─ Deeper indent → emit INDENT token
    └─ Shallower indent → emit DEDENT token(s)
    ↓
Token stream: [..., NEWLINE, INDENT, ..., DEDENT, ...]
    ↓
Parser: INDENT/DEDENT tokens replace braces in grammar
    ↓
AST (unchanged structure)
```

**Key insight:** Indentation is resolved at lexer stage. Parser sees INDENT/DEDENT as ordinary tokens, making grammar changes minimal.

#### 2. ADT/GADT Flow

```
Source: type expr = Lit of int | Add of expr * expr
    ↓
Parser: Parse type definition → AST
    └─ Ast.fs: New node types (TypeDef, DataConstructor)
    ↓
Type Checker: Register constructors in environment
    ├─ Infer.fs: Extend unification for ADT types
    └─ Bidir.fs: Use GADT refinements (equational constraints)
    ↓
Typed AST: Constructors have known types
    ↓
Evaluator: Pattern matching with constructors
    └─ Eval.fs: Match on tagged values
```

**Key insight:** GADTs need bidirectional checking. Hindley-Milner alone cannot handle GADT refinements. Use Bidir.fs for expressions requiring type annotations.

#### 3. Records Flow

```
Source: { name = "Alice"; age = 30 }
    ↓
Parser: Parse record expression → AST
    └─ Ast.fs: New node RecordExpr(fields)
    ↓
Type Checker: Structural typing
    ├─ Type.fs: New TRecord(row) type
    ├─ Infer.fs: Unify record types by field names/types
    └─ Row polymorphism (optional, for extensibility)
    ↓
Typed AST: Record has known field types
    ↓
Evaluator: Runtime representation
    └─ Eval.fs: Records as maps (string → value)
```

**Key insight:** Records can use structural typing (no declaration needed) or nominal typing (requires explicit type). Structural is simpler for MVP. Runtime representation as dictionary/map is sufficient.

#### 4. Modules Flow

```
Source: module Math = ... | open Math
    ↓
Parser: Parse module definitions → AST
    └─ Ast.fs: New node ModuleDef(name, decls)
    ↓
Module System: Build environment
    ├─ Modules.fs: Track module namespaces
    ├─ Phase 1: Collect all module signatures
    ├─ Phase 2: Resolve names (qualified paths)
    └─ Dependency ordering (topological sort)
    ↓
Type Checker: Type check with qualified names
    └─ Env extended with module paths (Math.add)
    ↓
Evaluator: Evaluate in module scope
    └─ Eval.fs: Nested environments
```

**Key insight:** Modules require separate compilation support. Must process module signatures before bodies. Namespace resolution happens before type checking.

#### 5. Exceptions Flow

```
Source: try ... with | E -> ... | raise E
    ↓
Parser: Parse exception constructs → AST
    └─ Ast.fs: New nodes (TryWith, Raise, ExceptionDef)
    ↓
Type Checker: Track exception types
    ├─ Type.fs: Exn type (extensible sum)
    └─ Infer.fs: Raise has type 'a, try has handler type
    ↓
Evaluator: Runtime exception handling
    ├─ Eval.fs: Evaluate try/raise
    └─ Exceptions.fs: Stack unwinding (search for handlers)
```

**Key insight:** Exceptions are "zero-cost" in happy path (no overhead when not raised). Unwinding is reverse execution order. F# style exceptions are simpler than OCaml (no effects).

## Feature-Specific Architecture Patterns

### Pattern 1: Indentation-Based Syntax (Lexer Post-Processing)

**What:** Token injection technique. Lexer maintains indentation stack and injects INDENT/DEDENT tokens.

**When:** Processing newlines that start logical lines.

**Implementation approach:**

```fsharp
// Lexer.fsl
let indentStack = ref [0]  // Track indentation levels

let handleNewline column =
    let current = List.head !indentStack
    match compare column current with
    | x when x > 0 ->
        // Deeper: push and emit INDENT
        indentStack := column :: !indentStack
        [NEWLINE; INDENT]
    | 0 ->
        // Same: just newline
        [NEWLINE]
    | _ ->
        // Shallower: pop and emit DEDENTs
        let rec unwind acc stack =
            match stack with
            | [] -> failwith "Indentation error"
            | top :: rest when top > column ->
                unwind (DEDENT :: acc) rest
            | top :: rest when top = column ->
                (NEWLINE :: acc, top :: rest)
            | _ -> failwith "Indentation error"
        let (tokens, newStack) = unwind [] !indentStack
        indentStack := newStack
        tokens
```

**Parser changes:**

```fsharp
// Parser.fsy - Replace braces with INDENT/DEDENT
block:
    | INDENT declarations DEDENT { $2 }

// Was: LBRACE declarations RBRACE { $2 }
```

**Pitfalls:**

- Mixing tabs and spaces causes issues. Normalize to spaces.
- Comment-only lines should not affect indentation.
- EOF requires emitting remaining DEDENT tokens.

**Sources:**
- [Python Lexical Analysis](https://docs.python.org/3/reference/lexical_analysis.html) - Official Python indentation algorithm
- [Principled Parsing for Indentation-Sensitive Languages](https://michaeldadams.org/papers/layout_parsing/LayoutParsing.pdf) - Formal layout rule specification
- [antlr-denter](https://github.com/yshavit/antlr-denter) - Token injection implementation pattern

### Pattern 2: ADT/GADT with Bidirectional Typing

**What:** Extend AST with type definitions. Use bidirectional type checking for GADT refinements.

**When:** Type definitions introduce new type constructors. Pattern matching uses constructors.

**AST extensions:**

```fsharp
// Ast.fs
type TypeDef =
    | SimpleADT of string * DataConstructor list  // type option = None | Some of 'a
    | GADT of string * GADTConstructor list       // type expr : * -> * = ...

and DataConstructor = string * Type option       // None | Some of 'a

and GADTConstructor = string * Type * Type       // Lit : int -> expr int
```

**Type checker strategy:**

For simple ADTs, use Hindley-Milner (Infer.fs):
- Constructors are functions: `Some : 'a -> 'a option`
- Pattern matching generates unification constraints

For GADTs, use bidirectional checking (Bidir.fs):
- Constructors have refined return types
- Pattern matching branches have type refinements
- Requires explicit type annotations on GADT expressions

**Type representation:**

```fsharp
// Type.fs
type Type =
    | ... // existing types
    | TData of string * Type list         // option<int>
    | TGADTInstance of string * Type      // expr int (indexed by type)
```

**Unification extensions:**

```fsharp
// Unify.fs
let rec unify t1 t2 =
    match t1, t2 with
    | TData(n1, args1), TData(n2, args2) when n1 = n2 ->
        List.iter2 unify args1 args2
    | TGADTInstance(n1, idx1), TGADTInstance(n2, idx2) when n1 = n2 ->
        unify idx1 idx2  // Equational constraint from GADT
    | ...
```

**Pitfalls:**

- GADT type inference is undecidable. Require annotations.
- Pattern matching exhaustiveness is harder with GADTs.
- FunLang has both Infer.fs and Bidir.fs. Use appropriate one per expression.

**Sources:**
- [Simple unification-based type inference for GADTs](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/gadt-pldi.pdf) - Simon Peyton Jones' GADT inference approach
- [Bidirectional Typing](https://arxiv.org/pdf/1908.05839) - Jana Dunfield's comprehensive survey
- [How to Choose Between Hindley-Milner and Bidirectional Typing](https://thunderseethe.dev/posts/how-to-choose-between-hm-and-bidir/) - When to use each approach

### Pattern 3: Records with Structural Typing

**What:** Records as first-class values with structural typing (compatible if field names/types match).

**When:** Record creation, field access, record patterns.

**AST extensions:**

```fsharp
// Ast.fs
type Expr =
    | ... // existing
    | RecordCreate of (string * Expr) list          // { x = 1; y = 2 }
    | FieldAccess of Expr * string                  // record.field
    | RecordUpdate of Expr * (string * Expr) list   // { record with x = 3 }

type Pattern =
    | ... // existing
    | RecordPat of (string * Pattern) list          // { x = px; y = py }
```

**Type representation:**

```fsharp
// Type.fs
type Type =
    | ... // existing
    | TRecord of (string * Type) list  // { x: int; y: string }
```

**Unification:**

```fsharp
// Unify.fs
| TRecord(fields1), TRecord(fields2) ->
    // Must have same field names (order independent)
    let sorted1 = fields1 |> List.sortBy fst
    let sorted2 = fields2 |> List.sortBy fst
    if List.map fst sorted1 <> List.map fst sorted2 then
        failwith "Record field mismatch"
    List.iter2 unify (List.map snd sorted1) (List.map snd sorted2)
```

**Runtime representation:**

```fsharp
// Eval.fs
type Value =
    | ... // existing
    | VRecord of Map<string, Value>  // Field name → value mapping
```

**Field access:**

```fsharp
// Eval.fs
| FieldAccess(record, field) ->
    match eval env record with
    | VRecord(fields) ->
        match Map.tryFind field fields with
        | Some v -> v
        | None -> failwith $"Field {field} not found"
    | _ -> failwith "Not a record"
```

**Pitfalls:**

- Row polymorphism (extensible records) is complex. Defer to post-MVP.
- Structural typing can have large types. Consider type aliases.
- Record update syntax requires copying unchanged fields.

**Sources:**
- [Standard ML Programming/Types](https://en.wikibooks.org/wiki/Standard_ML_Programming/Types) - ML record semantics
- [Tagged union Wikipedia](https://en.wikipedia.org/wiki/Tagged_union) - Runtime representation patterns

### Pattern 4: Module System (Namespace Resolution)

**What:** Two-phase compilation: (1) collect module signatures, (2) resolve names and type check.

**When:** Multiple files with module dependencies.

**AST extensions:**

```fsharp
// Ast.fs
type Decl =
    | ... // existing
    | ModuleDef of string * Decl list         // module Math = ...
    | ModuleOpen of string                     // open Math

type QualifiedName = string list              // ["Math"; "add"]
```

**Module system components:**

```fsharp
// New: Modules.fs
type ModuleSignature = {
    name: string
    types: Map<string, Type>       // Exported types
    values: Map<string, Type>      // Exported values
}

type ModuleEnv = Map<string, ModuleSignature>

// Phase 1: Extract signatures
let rec collectSignature (decls: Decl list) : ModuleSignature = ...

// Phase 2: Resolve qualified names
let resolveName (env: ModuleEnv) (qname: QualifiedName) : Type = ...
```

**Environment extensions:**

```fsharp
// Env.fs (extended)
type Env = {
    locals: Map<string, Type>           // Local bindings
    modules: Map<string, ModuleSignature>  // Available modules
    opens: string list                   // Currently open modules
}

// Lookup with fallback through open modules
let lookup (env: Env) (name: string) : Type =
    match Map.tryFind name env.locals with
    | Some t -> t
    | None ->
        env.opens
        |> List.tryPick (fun modName ->
            env.modules
            |> Map.tryFind modName
            |> Option.bind (fun m -> Map.tryFind name m.values))
        |> Option.defaultWith (fun () -> failwith $"Unbound: {name}")
```

**Compilation order:**

```fsharp
// New: Modules.fs
let sortModules (modules: (string * Decl list) list) : (string * Decl list) list =
    // Build dependency graph
    let deps = modules |> List.map (fun (name, decls) ->
        let uses = decls |> collectModuleReferences
        (name, uses))
    // Topological sort
    topoSort deps
```

**Pitfalls:**

- Circular module dependencies are an error. Detect with cycle detection.
- Recursive modules (F# `and`) are advanced. Defer to post-MVP.
- Separate compilation requires `.mli` style interface files. Start with single-file.

**Sources:**
- [OCaml Module System](https://ocaml.org/manual/5.3/moduleexamples.html) - Compilation units and modules
- [F# Modules](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules) - F# module semantics
- [Separate module compilation in OCaml](https://discuss.ocaml.org/t/separate-module-compilation-in-ocaml/3689) - Dependency management

### Pattern 5: Exceptions (Runtime Unwinding)

**What:** Stack-based exception handling. Evaluator maintains handler stack, unwinds on `raise`.

**When:** `try...with` blocks establish handlers. `raise` triggers unwinding.

**AST extensions:**

```fsharp
// Ast.fs
type Expr =
    | ... // existing
    | TryWith of Expr * (Pattern * Expr) list   // try expr with | p1 -> e1 | p2 -> e2
    | Raise of Expr                              // raise E

type Decl =
    | ... // existing
    | ExceptionDef of string * Type option      // exception E of int
```

**Type checker:**

```fsharp
// Type.fs
type Type =
    | ... // existing
    | TExn  // Built-in exception type (extensible)

// Infer.fs
| Raise(e) ->
    let te = infer env e
    // Constraint: e must be exception type
    unify te TExn
    // Raise has type 'a (can appear anywhere)
    freshTyVar()

| TryWith(body, handlers) ->
    let tbody = infer env body
    // Each handler: pattern must be TExn, body must match tbody
    handlers |> List.iter (fun (pat, expr) ->
        let env' = inferPattern env pat TExn
        let texpr = infer env' expr
        unify tbody texpr)
    tbody
```

**Runtime support:**

```fsharp
// New: Exceptions.fs
type ExceptionValue =
    | ExnConstructor of string * Value option  // E(42)

exception LangException of ExceptionValue  // F# exception for unwinding

type Handler = {
    patterns: (Pattern * Expr) list
    env: Env
}

type EvalEnv = {
    bindings: Map<string, Value>
    handlers: Handler list  // Stack of handlers
}
```

**Evaluator changes:**

```fsharp
// Eval.fs
let rec eval (env: EvalEnv) (expr: Expr) : Value =
    match expr with
    | TryWith(body, handlers) ->
        let handler = { patterns = handlers; env = env }
        let env' = { env with handlers = handler :: env.handlers }
        try
            eval env' body
        with LangException(exn) ->
            // Search for matching handler
            tryHandle env.handlers exn

    | Raise(e) ->
        match eval env e with
        | VException(exn) ->
            raise (LangException(exn))  // Unwind
        | _ -> failwith "Raise expects exception"

    | ... // Other cases may raise exceptions

let tryHandle (handlers: Handler list) (exn: ExceptionValue) : Value =
    match handlers with
    | [] -> raise (LangException(exn))  // Uncaught
    | h :: rest ->
        match tryPatterns h.patterns exn h.env with
        | Some value -> value
        | None -> tryHandle rest exn  // Try next handler
```

**Pitfalls:**

- Exception types are extensible. Must allow runtime registration.
- Stack traces require source location tracking. Can be added later.
- Exceptions break Hindley-Milner principal types (raise has type 'a).

**Sources:**
- [Exception Handling Flow-of-control](https://runestone.academy/ns/books/published/thinkcspy/Exceptions/01_intro_exceptions.html) - Control flow fundamentals
- [Stack Unwinding](https://www.zyma.me/post/stack-unwind-intro/) - Unwinding algorithm explanation
- [Exceptions in Cranelift and Wasmtime](https://cfallin.org/blog/2025/11/06/exceptions/) - Zero-cost exception handling

## Component Interaction Summary

### What Depends on What

```
Indentation (Lexer)
    ↓ (Token stream affects all)
Parser (uses tokens)
    ↓ (AST is input to all)
ADT/GADT + Records (AST nodes)
    ↓ (New types affect type checker)
Type Checker (handles new types)
    ↓ (Module resolution uses types)
Modules (namespace resolution)
    ↓ (Environment passed to evaluator)
Evaluator
    ↓ (Runtime values can be exceptions)
Exceptions (runtime support)
```

### Feature Independence

| Feature | Independent Of | Depends On | Impacts |
|---------|---------------|------------|---------|
| **Indentation** | All features | None | Lexer, Parser (minimal) |
| **ADT** | Records, Modules, Exceptions | AST, Type system | Type.fs, Infer.fs, Eval.fs |
| **GADT** | Records, Modules, Exceptions | ADT, Bidir.fs | Bidir.fs (primarily) |
| **Records** | ADT, Modules, Exceptions | AST, Type system | Type.fs, Unify.fs, Eval.fs |
| **Modules** | ADT, Records, Exceptions | Type system (must exist) | All files (namespace resolution) |
| **Exceptions** | ADT, Records, Modules | AST, Eval.fs | Eval.fs, new Exceptions.fs |

### Parallel Development Opportunities

1. **After Indentation:** ADT, GADT, Records can be developed in parallel (different AST nodes, different type checker rules).

2. **After Type System Features:** Modules and Exceptions can be developed in parallel (one affects compile-time, other affects runtime).

3. **Testing Isolation:** Each feature can be tested independently with dedicated test suites.

## Suggested Build Order

### Phase-by-Phase Approach

**Phase 1: Indentation Syntax** (Foundation)
- **Why first:** All code will use indentation. Must be stable before other features.
- **Components:** Lexer.fsl (add indentation stack), Parser.fsy (INDENT/DEDENT tokens)
- **Risk:** Low (well-understood problem, Python-style algorithm proven)
- **Deliverable:** Parser accepts indentation-based syntax, existing tests pass

**Phase 2: ADT Support** (Type System Core)
- **Why second:** Simplest type system extension. Foundation for GADT.
- **Components:** Ast.fs (TypeDef), Type.fs (TData), Infer.fs (constructor typing), Eval.fs (pattern match)
- **Risk:** Low (standard Hindley-Milner extension)
- **Dependencies:** Indentation (for syntax)
- **Deliverable:** Can define and use simple ADTs (option, list, tree)

**Phase 3: GADT Support** (Type System Advanced)
- **Why third:** Builds on ADT infrastructure. Needs bidirectional checking.
- **Components:** Bidir.fs (refinement typing), Unify.fs (equational constraints)
- **Risk:** Medium (type inference is tricky, requires annotations)
- **Dependencies:** ADT (shares infrastructure)
- **Deliverable:** Can define and use GADTs with type indices

**Phase 4: Records** (Type System Parallel Track)
- **Why fourth:** Independent of ADT/GADT. Can be parallel with Phase 3.
- **Components:** Ast.fs (RecordExpr), Type.fs (TRecord), Unify.fs (field unification), Eval.fs (map representation)
- **Risk:** Low (structural typing is straightforward)
- **Dependencies:** Indentation (for syntax), Type system (basic)
- **Deliverable:** Can create, access, pattern match on records

**Phase 5: Module System** (Namespace Resolution)
- **Why fifth:** Requires complete type system. Affects all components.
- **Components:** New Modules.fs, Env.fs (extensions), all files (qualified names)
- **Risk:** Medium (topological sort, dependency cycles)
- **Dependencies:** ADT/GADT/Records (to organize into modules)
- **Deliverable:** Can define modules, open modules, use qualified names

**Phase 6: Exceptions** (Runtime Support)
- **Why last:** Most isolated. Primarily runtime concern.
- **Components:** New Exceptions.fs, Eval.fs (handler stack), Type.fs (TExn)
- **Risk:** Low (F# provides exception mechanism to build on)
- **Dependencies:** ADT (exception constructors), Modules (exception definitions in modules)
- **Deliverable:** Can define, raise, catch exceptions

### Dependency Rationale

1. **Indentation first:** Lexer is foundation. Unstable lexer breaks everything.

2. **ADT before GADT:** GADT is superset of ADT. Share parsing and AST representation.

3. **Records parallel to GADT:** Records don't interact with ADT/GADT. Can develop simultaneously.

4. **Modules after type system:** Module signatures contain types. Must type check module contents.

5. **Exceptions last:** Uses ADT for exception values. Uses modules for exception definitions. Mostly runtime concern.

### Build Order for Parallel Work

If multiple developers:

```
Week 1-2: Indentation (sequential, blocks other work)
    ↓
Week 3-4: ADT (sequential, foundation for GADT)
    ↓
Week 5-6: GADT (Developer A) || Records (Developer B) (parallel)
    ↓
Week 7-8: Modules (sequential, touches all files)
    ↓
Week 9-10: Exceptions (independent)
```

### Testing Strategy per Phase

- **Indentation:** Lexer unit tests (INDENT/DEDENT injection), parser integration tests
- **ADT:** Type inference tests, pattern matching tests, evaluator tests
- **GADT:** Bidirectional typing tests, refinement tests, negative tests (should reject)
- **Records:** Structural typing tests, field access tests, pattern tests
- **Modules:** Multi-file tests, dependency order tests, namespace tests
- **Exceptions:** Control flow tests, unwinding tests, handler matching tests

## Anti-Patterns to Avoid

### Anti-Pattern 1: Mixing Indentation and Braces

**What:** Allowing both `{ }` and indentation-based blocks.

**Why bad:** Ambiguous syntax. Parser complexity explodes.

**Instead:** Commit to indentation-only. Remove brace tokens from lexer.

**Detection:** If `LBRACE` token still exists in Lexer.fsl, you have this anti-pattern.

### Anti-Pattern 2: Type Inference for All GADTs

**What:** Attempting to infer types for GADT pattern matches without annotations.

**Why bad:** GADT type inference is undecidable. Will hit unsolvable cases.

**Instead:** Require type annotations on GADT expressions. Use bidirectional checking.

**Detection:** If Bidir.fs is not used for GADT matches, you have this anti-pattern.

### Anti-Pattern 3: Nominal Records with Global Registry

**What:** Creating a global mutable map of record type names.

**Why bad:** Breaks modularity. Makes parallelism hard.

**Instead:** Use structural typing (no global state) or pass type environment explicitly.

**Detection:** If you see `mutable recordTypes: Map<string, ...>` at module level, you have this anti-pattern.

### Anti-Pattern 4: Recursive Module Support in MVP

**What:** Allowing modules to reference each other circularly.

**Why bad:** Requires complex two-pass compilation, lazy evaluation.

**Instead:** Require acyclic module dependencies. Use topological sort.

**Detection:** If module dependency checker allows cycles, you have this anti-pattern.

### Anti-Pattern 5: Exceptions in Type Signatures

**What:** Adding exception specifications to function types (Java-style `throws`).

**Why bad:** Breaks Hindley-Milner. Complicates type inference massively.

**Instead:** Exceptions are implicit (F# style). Type is independent of exceptions.

**Detection:** If `Type` has a case like `TFun(arg, result, exceptions)`, you have this anti-pattern.

### Anti-Pattern 6: Mutable AST

**What:** Modifying AST nodes in place during type checking or evaluation.

**Why bad:** Makes debugging hard. Breaks referential transparency.

**Instead:** AST is immutable. Type checker produces typed AST (separate structure).

**Detection:** If `Ast.fs` types have `mutable` fields, you have this anti-pattern.

### Anti-Pattern 7: String-Based Module Resolution

**What:** Resolving module names with string concatenation and lookups.

**Why bad:** Error-prone. Hard to track qualified names.

**Instead:** Use `QualifiedName = string list` type. Pattern match on structure.

**Detection:** If module resolution uses `String.Split` or `String.concat`, you have this anti-pattern.

## Scalability Considerations

| Concern | Current (MVP) | At 1K LOC | At 10K LOC | At 100K LOC |
|---------|---------------|-----------|------------|-------------|
| **Parsing** | Single-pass | Single-pass | Single-pass | Single-pass (parser is O(n)) |
| **Type Checking** | Whole program | Whole program | Per-module | Per-module + caching |
| **Module Compilation** | Single-file | Single-file | Multi-file | Incremental compilation |
| **Name Resolution** | Linear search | Linear search | Hash maps | Hash maps + namespaces |
| **Error Messages** | Basic | Basic | Spans with files | Spans with suggestions |

### When to Optimize

- **Now (MVP):** None. Correctness over performance.
- **At 1K LOC:** Add source locations to errors.
- **At 10K LOC:** Module-level caching for type inference.
- **At 100K LOC:** Incremental compilation, parallel type checking.

### Performance Notes

1. **Indentation:** Lexer overhead is minimal (stack operations are O(1)).
2. **ADT/GADT:** Type checking cost is dominated by unification (no worse than base Hindley-Milner).
3. **Records:** Structural typing can produce large types. Type aliases help.
4. **Modules:** Two-pass compilation doubles type checking time (still acceptable for MVP).
5. **Exceptions:** Zero-cost in happy path. Unwinding is expensive but rare.

## Files Modified by Feature

### Indentation
- **Lexer.fsl:** Add indentation stack, emit INDENT/DEDENT
- **Parser.fsy:** Replace `LBRACE`/`RBRACE` with `INDENT`/`DEDENT` in grammar

### ADT
- **Ast.fs:** Add `TypeDef`, `DataConstructor` nodes
- **Type.fs:** Add `TData` type constructor
- **Infer.fs:** Add constructor typing rules
- **Eval.fs:** Add pattern matching for constructors

### GADT
- **Ast.fs:** Add `GADTConstructor` (extends ADT)
- **Type.fs:** Add `TGADTInstance` with type indices
- **Bidir.fs:** Add refinement rules for GADT patterns
- **Unify.fs:** Add equational constraint handling

### Records
- **Ast.fs:** Add `RecordExpr`, `FieldAccess`, `RecordUpdate`
- **Type.fs:** Add `TRecord` type
- **Unify.fs:** Add field-based unification
- **Eval.fs:** Add `VRecord` value, field access evaluation

### Modules
- **Ast.fs:** Add `ModuleDef`, `ModuleOpen`, `QualifiedName`
- **Modules.fs:** (NEW) Module signature extraction, name resolution
- **Env.fs:** Extend with module-aware lookup
- **All files:** Support qualified names in expressions

### Exceptions
- **Ast.fs:** Add `TryWith`, `Raise`, `ExceptionDef`
- **Type.fs:** Add `TExn` type
- **Exceptions.fs:** (NEW) Exception values, handler matching
- **Eval.fs:** Add handler stack, unwinding logic

## Summary for Roadmap Planning

### Critical Path

1. **Indentation** (1-2 weeks) - Blocks all other work
2. **ADT** (1-2 weeks) - Foundation for GADT
3. **GADT + Records** (2-3 weeks) - Parallel development possible
4. **Modules** (2-3 weeks) - Coordinates all components
5. **Exceptions** (1-2 weeks) - Independent, can be done anytime after ADT

**Total estimate:** 7-12 weeks sequential, 6-9 weeks with parallel work

### Research Flags

- **Phase 1 (Indentation):** LOW - Well-documented problem
- **Phase 2 (ADT):** LOW - Standard Hindley-Milner extension
- **Phase 3 (GADT):** MEDIUM - May need deeper research on refinement typing
- **Phase 4 (Records):** LOW - Structural typing is straightforward
- **Phase 5 (Modules):** MEDIUM - Two-phase compilation needs careful design
- **Phase 6 (Exceptions):** LOW - Can leverage F# exception mechanism

### Key Risks

1. **GADT type inference:** May be more complex than anticipated. Mitigation: Require explicit annotations.
2. **Module circular dependencies:** Hard to debug. Mitigation: Good error messages, cycle detection.
3. **Exception stack traces:** Debugging without them is hard. Mitigation: Add source spans to AST nodes.

## Sources

**Indentation & Parsing:**
- [Python Lexical Analysis](https://docs.python.org/3/reference/lexical_analysis.html) - Official indentation algorithm
- [Principled Parsing for Indentation-Sensitive Languages](https://michaeldadams.org/papers/layout_parsing/LayoutParsing.pdf) - Formal offside rule
- [antlr-denter](https://github.com/yshavit/antlr-denter) - Token injection pattern
- [FsLexYacc Documentation](https://github.com/fsprojects/FsLexYacc) - F# lexer/parser tools
- [Parsing Indentation Sensitive Languages](https://rahul.gopinath.org/post/2022/06/04/parsing-indentation/)

**Type Systems (ADT/GADT):**
- [Simple unification-based type inference for GADTs](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/gadt-pldi.pdf) - Simon Peyton Jones
- [Bidirectional Typing](https://arxiv.org/pdf/1908.05839) - Jana Dunfield comprehensive survey
- [How to Choose Between Hindley-Milner and Bidirectional Typing](https://thunderseethe.dev/posts/how-to-choose-between-hm-and-bidir/)
- [Omnidirectional type inference for ML](https://inria.hal.science/hal-05438544v1/document) - 2025 research on ML extensions
- [Typed AST with GADT in Haskell](https://gist.github.com/nebuta/6096345)

**Records & Runtime:**
- [Standard ML Programming/Types](https://en.wikibooks.org/wiki/Standard_ML_Programming/Types) - ML record semantics
- [Tagged Union Wikipedia](https://en.wikipedia.org/wiki/Tagged_union) - Runtime representation
- [C# Discriminated Unions](https://blog.ndepend.com/csharp-discriminated-union/) - Modern implementation approaches

**Modules:**
- [OCaml Module System](https://ocaml.org/manual/5.3/moduleexamples.html) - Compilation units
- [F# Modules](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules) - F# module semantics
- [Separate module compilation in OCaml](https://discuss.ocaml.org/t/separate-module-compilation-in-ocaml/3689)
- [OCaml Namespaces Proposal](https://github.com/lpw25/namespaces)

**Exceptions:**
- [Exception Handling Flow-of-control](https://runestone.academy/ns/books/published/thinkcspy/Exceptions/01_intro_exceptions.html)
- [Stack Unwinding Introduction](https://www.zyma.me/post/stack-unwind-intro/)
- [Exceptions in Cranelift and Wasmtime](https://cfallin.org/blog/2025/11/06/exceptions/) - Zero-cost exception handling
- [LLVM Exception Handling](https://llvm.org/docs/ExceptionHandling.html)

**Architecture & Pipelines:**
- [PyPy Architecture](https://doc.pypy.org/en/latest/architecture.html) - Interpreter pipeline example
- [Project 2: Scheme Lexer and Parser](https://eecs390.github.io/project-scheme-parser/) - Winter 2025 course materials
- [Crafting Interpreters: Representing Code](https://craftinginterpreters.com/representing-code.html)
- [Visitor as a sum type](https://blog.ploeh.dk/2018/06/25/visitor-as-a-sum-type/) - F# pattern matching vs OOP visitors
