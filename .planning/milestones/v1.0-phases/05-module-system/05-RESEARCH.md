# Phase 5: Module System - Research

**Researched:** 2026-03-09
**Domain:** Module system for F#-style ML language (AST extension, name resolution, circular dependency detection)
**Confidence:** HIGH

## Summary

This phase adds an F#-style module system to LangThree. The work is purely internal -- no new external libraries are needed. The implementation extends the existing AST, parser, type checker, and evaluator with module-related constructs: `module` declarations (top-level and nested), `namespace` declarations, `open` for imports, qualified name access (`Module.function`), and implicit module from filename.

The current codebase already has a `Module` AST node (`Module of decls: Decl list * Span`) and a `parseModule` entry point, but these represent a flat list of declarations with no module/namespace nesting. The key challenge is extending this flat structure to support nested scoped environments while maintaining the existing pipeline: `parseModule -> typeCheckModule -> eval`.

The primary concern is getting the name resolution right -- especially the interplay between qualified names, `open` directives, and nested module scopes. Circular dependency detection (MOD-06 success criterion) is straightforward with DFS-based cycle detection on an import graph. Since recursive modules are explicitly out of scope (MOD-V2-02), any cycle is an error.

**Primary recommendation:** Extend Decl with ModuleDecl and OpenDecl variants, introduce a hierarchical ModuleEnv for name resolution, and implement a two-pass approach: first collect all module declarations and their exports, then resolve names within bodies.

## Standard Stack

This phase uses no new external libraries. All work extends the existing F#/.NET/fslex/fsyacc stack.

### Core
| Component | Current | Purpose | Impact |
|-----------|---------|---------|--------|
| Ast.fs | Decl, Module types | Extended with ModuleDecl, NamespaceDecl, OpenDecl | Major -- new AST nodes |
| Parser.fsy | parseModule entry | Extended with module/namespace/open grammar | Major -- new grammar rules |
| Lexer.fsl | tokenize rule | Add MODULE, NAMESPACE, OPEN keywords | Minor -- 3 new keywords |
| TypeCheck.fs | typeCheckModule | Extended with module-scoped type checking | Major -- hierarchical env |
| Eval.fs | eval, evalModule | Extended with module-scoped evaluation | Major -- hierarchical env |
| Diagnostic.fs | TypeErrorKind | Extended with module error kinds | Minor -- new error codes |

### Supporting
| Component | Purpose | When to Use |
|-----------|---------|-------------|
| IndentFilter.fs | Indentation processing | Already handles INDENT/DEDENT for nested blocks |
| Elaborate.fs | Type elaboration | ConstructorEnv/RecordEnv already scoped per module |
| Type.fs | ConstructorEnv, RecordEnv | Need module-qualified lookup |

### Not Needed
| Instead of | Why Not |
|------------|---------|
| External dependency manager | Single-file modules only; no package system |
| Module linker | Interpreter, not compiler; no separate compilation |
| Import resolution library | Simple enough to implement with Map lookups |

## Architecture Patterns

### Current AST Structure (Before)
```
Module
  Decl list
    LetDecl | TypeDecl | RecordTypeDecl
```

### Target AST Structure (After)
```
Module
  Decl list
    LetDecl | TypeDecl | RecordTypeDecl
    | ModuleDecl (name, Decl list, Span)       -- nested module
    | OpenDecl (qualifiedName, Span)            -- open directive
    | NamespaceDecl (qualifiedName, Decl list, Span) -- namespace
```

### Recommended New/Modified Files
```
src/LangThree/
  Ast.fs           -- Add ModuleDecl, OpenDecl, NamespaceDecl to Decl; QualifiedName expr
  Lexer.fsl        -- Add MODULE, NAMESPACE, OPEN tokens
  Parser.fsy       -- Add module/namespace/open grammar rules
  TypeCheck.fs      -- Module-scoped type checking with hierarchical env
  Eval.fs           -- Module-scoped evaluation with hierarchical env
  Diagnostic.fs     -- Module error codes (E05xx range)
  IndentFilter.fs   -- No changes expected (INDENT/DEDENT already handles nesting)
```

### Pattern 1: Hierarchical Module Environment
**What:** A `ModuleEnv` type that maps module names to their exported bindings (values, types, constructors, records). Each module scope has a parent reference for lexical scoping.
**When to use:** Name resolution during type checking and evaluation.
**Example:**
```fsharp
// Source: Architecture design for LangThree module system
type ModuleEnv = {
    Name: string
    Values: TypeEnv                    // let bindings
    Types: ConstructorEnv              // ADT constructors
    Records: RecordEnv                 // record types
    SubModules: Map<string, ModuleEnv> // nested modules
    Opened: string list                // opened module paths
}
```

### Pattern 2: Qualified Name as Expr/Pattern Extension
**What:** Extend the Expr AST to support `Module.identifier` dot-access for values (not just record fields).
**When to use:** Parsing and evaluating qualified module member access.
**Example:**
```fsharp
// Source: LangThree AST extension design
// Option A: Reuse existing FieldAccess for qualified names
//   Parser already has: Atom DOT IDENT -> FieldAccess
//   At evaluation time, distinguish record field vs module member
//   by checking if the left side is a module name

// Option B: Add a dedicated QualifiedName variant
//   | QualifiedName of modulePath: string list * name: string * Span
//   Pro: clearer semantics; Con: parser ambiguity with FieldAccess

// RECOMMENDATION: Option A (reuse FieldAccess)
// The existing `Atom DOT IDENT` grammar rule already parses `Module.function`.
// At type-check and eval time, when the left side resolves to a module
// (not a record value), treat it as qualified access.
// This avoids LALR(1) conflicts and reuses existing parser infrastructure.
```

### Pattern 3: Two-Pass Module Processing
**What:** First pass collects all module declarations and builds a module dependency graph. Second pass processes modules in dependency order, resolving names.
**When to use:** When processing a file with multiple module declarations and `open` directives.
**Example:**
```fsharp
// Source: Standard compiler design pattern
// Pass 1: Collect module structure (names, nesting, open directives)
// Pass 2: Type-check declarations in dependency order
//
// For v1 (single-file, no cross-file imports), the order within
// a file IS the dependency order (top-to-bottom), so Pass 1 is
// mainly about building the ModuleEnv skeleton.
```

### Pattern 4: Circular Dependency Detection via DFS
**What:** Build directed graph from `open` directives, detect cycles using DFS with gray/black coloring.
**When to use:** Before processing module bodies, validate no circular dependencies exist.
**Example:**
```fsharp
// Source: Standard graph algorithm
type Color = White | Gray | Black

let detectCycle (graph: Map<string, string list>) : string list option =
    let colors = System.Collections.Generic.Dictionary<string, Color>()
    let rec dfs path node =
        match colors.TryGetValue(node) with
        | true, Gray -> Some (List.rev (node :: path))  // Cycle found
        | true, Black -> None  // Already fully explored
        | _ ->
            colors.[node] <- Gray
            let neighbors = Map.tryFind node graph |> Option.defaultValue []
            let cycle = neighbors |> List.tryPick (dfs (node :: path))
            colors.[node] <- Black
            cycle
    graph |> Map.toList |> List.tryPick (fun (node, _) ->
        if not (colors.ContainsKey(node)) then dfs [] node else None)
```

### Anti-Patterns to Avoid
- **Global mutable module registry:** Use immutable maps threaded through functions, consistent with existing ConstructorEnv/RecordEnv pattern.
- **Resolving qualified names at parse time:** The parser should produce AST nodes; resolution happens during type checking. Parsing `A.B.c` should produce a chained FieldAccess or QualifiedName, not resolve the module.
- **Flattening all module members into global scope:** Module boundaries must be preserved for qualified access. Only `open` directives bring names into unqualified scope.
- **Adding LetRecDecl for module-level let rec:** Module-level let rec is a v2 feature (MOD-V2-02 recursive modules). For v1, module-level function recursion works via the existing `let rec` within expressions.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cycle detection | Custom traversal | Standard DFS with 3-color marking | Well-studied algorithm, easy to get wrong with ad-hoc approaches |
| Name shadowing | Custom shadow logic | Map.add (later bindings shadow earlier) | F#'s Map already handles this correctly |
| Scope chain lookup | Linked list of envs | Fold over opened modules + parent lookup | Consistent with existing env-threading pattern |

**Key insight:** The existing codebase already has the environment-threading pattern (ConstructorEnv, RecordEnv, TypeEnv all passed as parameters). The module system extends this pattern by wrapping these environments in a ModuleEnv and providing lookup-with-qualification. Don't invent a new mechanism.

## Common Pitfalls

### Pitfall 1: LALR(1) Conflict Between FieldAccess and QualifiedName
**What goes wrong:** Adding a separate grammar rule for `Module.member` creates a shift-reduce conflict with the existing `Atom DOT IDENT` rule for record field access.
**Why it happens:** Both `x.y` (field access) and `Module.name` (qualified access) have identical syntax structure.
**How to avoid:** Reuse the existing `FieldAccess` AST node and distinguish at the type-checking/evaluation phase. If the left side of DOT is an uppercase IDENT that matches a known module name, treat as qualified access; otherwise treat as field access.
**Warning signs:** fsyacc reports shift-reduce conflicts after grammar changes.

### Pitfall 2: Open Directive Ordering
**What goes wrong:** Processing `open M` before module `M`'s declarations have been type-checked leads to empty or incomplete environments.
**Why it happens:** In a single file, modules are defined top-to-bottom but `open` directives may appear before the opened module is fully processed.
**How to avoid:** For single-file v1: enforce that `open` can only reference modules defined earlier in the file (F# also has this constraint for single-file compilation). Error if forward reference detected.
**Warning signs:** Empty type environments when processing bodies of modules that open later-defined modules.

### Pitfall 3: Namespace vs Module Confusion
**What goes wrong:** Namespaces and modules have different semantics in F#: namespaces can span multiple files and only contain types/modules (not values), while modules can contain values.
**Why it happens:** Treating them identically leads to incorrect scoping rules.
**How to avoid:** For v1, simplify: treat `namespace` as a scoping prefix only (no cross-file spanning since we're an interpreter). A namespace declaration sets the qualified prefix for all subsequent declarations. A module declaration creates an actual scope with its own environment.
**Warning signs:** Confusion about whether `namespace Foo` allows `let x = 1` at top level.

### Pitfall 4: Module-level Let Binding Scope
**What goes wrong:** Module-level `let` bindings need sequential scope (each binding can see previous bindings), but the current implementation evaluates the last binding only.
**Why it happens:** The existing `typeCheckModule` folds over declarations but only returns the last result. Module system needs each module to export ALL its bindings.
**How to avoid:** Change typeCheckModule to build and return the full accumulated TypeEnv and value Env for each module, not just the last binding's type. This is the fundamental shift from "script file" to "module file" semantics.
**Warning signs:** Module members not visible when accessed via qualified names.

### Pitfall 5: Implicit Module Name from Filename
**What goes wrong:** Filename-based module names may conflict with explicitly declared modules.
**Why it happens:** If a file `math.fun` has no `module` declaration, it becomes `Math` implicitly. If another file explicitly declares `module Math`, there's a conflict.
**How to avoid:** Since we're an interpreter (single file at a time), implicit module from filename only applies when no explicit `module` declaration exists at the top. The implicit module wraps all declarations.
**Warning signs:** Duplicate module name errors when running files without explicit module declarations.

### Pitfall 6: IndentFilter Already Handles Nesting
**What goes wrong:** Developers try to add special indent handling for `module` blocks.
**Why it happens:** Module bodies are indented, which seems like it needs special handling.
**How to avoid:** The existing IndentFilter already emits INDENT/DEDENT for any indentation change. Module body rules just need to expect `INDENT Decls DEDENT` in the grammar, exactly like existing indented blocks (type declarations, function bodies).
**Warning signs:** Trying to modify IndentFilter for module-specific behavior.

## Code Examples

### New AST Nodes
```fsharp
// Source: Extension of existing Ast.fs Decl type
type Decl =
    | LetDecl of name: string * body: Expr * Span
    | TypeDecl of TypeDecl
    | RecordTypeDecl of RecordDecl
    // Phase 5 (Modules): New declaration types
    | ModuleDecl of name: string * decls: Decl list * Span
    | OpenDecl of path: string list * Span
    | NamespaceDecl of path: string list * decls: Decl list * Span
```

### New Lexer Tokens
```fsharp
// Source: Extension of existing Lexer.fsl
// Add before the general IDENT rule:
| "module"     { MODULE }
| "namespace"  { NAMESPACE }
| "open"       { OPEN }
```

### New Parser Token Declarations
```fsharp
// Source: Extension of existing Parser.fsy
%token MODULE NAMESPACE OPEN
```

### Module Grammar Rules
```fsharp
// Source: Extension of existing Parser.fsy parseModule/Decls rules

// Top-level module declaration (covers entire file)
parseModule:
    | MODULE QualifiedIdent Decls EOF
        { Module($3, ruleSpan parseState 1 4) }  // module name applied externally
    | NAMESPACE QualifiedIdent Decls EOF
        { Module($3, ruleSpan parseState 1 4) }  // namespace scoping
    | Decls EOF
        { Module($1, ruleSpan parseState 1 2) }
    | EOF
        { EmptyModule(ruleSpan parseState 1 1) }

// Add to Decls alternatives:
Decls:
    // ... existing rules ...
    // Nested module: module Name = <indent>decls<dedent>
    | MODULE IDENT EQUALS INDENT Decls DEDENT
        { [ModuleDecl($2, $5, ruleSpan parseState 1 6)] }
    | MODULE IDENT EQUALS INDENT Decls DEDENT Decls
        { ModuleDecl($2, $5, ruleSpan parseState 1 6) :: $7 }
    // Open directive
    | OPEN QualifiedIdent
        { [OpenDecl($2, ruleSpan parseState 1 2)] }
    | OPEN QualifiedIdent Decls
        { OpenDecl($2, ruleSpan parseState 1 2) :: $3 }

// Qualified identifier: A.B.C
QualifiedIdent:
    | IDENT                         { [$1] }
    | IDENT DOT QualifiedIdent      { $1 :: $3 }
```

### Module-Scoped Type Checking
```fsharp
// Source: Extension of existing TypeCheck.fs typeCheckModule

/// Module environment containing all exported bindings
type ModuleExports = {
    TypeEnv: TypeEnv
    CtorEnv: ConstructorEnv
    RecEnv: RecordEnv
    SubModules: Map<string, ModuleExports>
}

/// Resolve a qualified name through module environments
let rec resolveQualified (modules: Map<string, ModuleExports>) (path: string list) : ModuleExports option =
    match path with
    | [] -> None
    | [name] -> Map.tryFind name modules
    | head :: tail ->
        match Map.tryFind head modules with
        | Some modExports -> resolveQualified modExports.SubModules tail
        | None -> None

/// Merge a module's exports into current scope (for `open` directive)
let openModule (modExports: ModuleExports) (typeEnv: TypeEnv) (ctorEnv: ConstructorEnv) (recEnv: RecordEnv) =
    let typeEnv' = Map.fold (fun acc k v -> Map.add k v acc) typeEnv modExports.TypeEnv
    let ctorEnv' = Map.fold (fun acc k v -> Map.add k v acc) ctorEnv modExports.CtorEnv
    let recEnv' = Map.fold (fun acc k v -> Map.add k v acc) recEnv modExports.RecEnv
    (typeEnv', ctorEnv', recEnv')
```

### Qualified Name Resolution in Eval
```fsharp
// Source: Extension of existing Eval.fs

/// Module value environment for runtime
type ModuleValueEnv = {
    Values: Env
    RecEnv: RecordEnv
    SubModules: Map<string, ModuleValueEnv>
}

/// Resolve FieldAccess as either record field or module member
// In eval for FieldAccess:
| FieldAccess (expr, fieldName, span) ->
    match eval recEnv moduleEnv env expr with
    | RecordValue (_, fields) ->
        // Existing record field access
        match Map.tryFind fieldName fields with
        | Some valueRef -> !valueRef
        | None -> failwithf "Field not found: %s" fieldName
    | _ ->
        // Check if expr is a module name (Var "ModuleName")
        match expr with
        | Var (moduleName, _) ->
            match Map.tryFind moduleName moduleEnv with
            | Some modEnv ->
                match Map.tryFind fieldName modEnv.Values with
                | Some value -> value
                | None -> failwithf "Module %s has no member %s" moduleName fieldName
            | None -> failwithf "Not a record or module: %s" moduleName
        | _ -> failwithf "Field access on non-record value"
```

### Circular Dependency Detection
```fsharp
// Source: Standard DFS cycle detection

/// Build dependency graph from open directives in module declarations
let buildDependencyGraph (decls: Decl list) : Map<string, string list> =
    let rec collect currentModule decls =
        decls |> List.collect (fun d ->
            match d with
            | OpenDecl (path, _) ->
                [(currentModule, String.concat "." path)]
            | ModuleDecl (name, innerDecls, _) ->
                collect name innerDecls
            | _ -> [])
    collect "<root>" decls
    |> List.groupBy fst
    |> List.map (fun (k, vs) -> (k, vs |> List.map snd))
    |> Map.ofList

/// Detect circular dependencies and return cycle path if found
let detectCircularDeps (graph: Map<string, string list>) : string list option =
    let visited = System.Collections.Generic.HashSet<string>()
    let inStack = System.Collections.Generic.HashSet<string>()
    let rec dfs path node =
        if inStack.Contains(node) then
            // Extract cycle from path
            let cycleStart = path |> List.rev |> List.findIndex ((=) node)
            let cycle = (path |> List.rev |> List.skip cycleStart) @ [node]
            Some cycle
        elif visited.Contains(node) then None
        else
            visited.Add(node) |> ignore
            inStack.Add(node) |> ignore
            let deps = Map.tryFind node graph |> Option.defaultValue []
            let result = deps |> List.tryPick (dfs (node :: path))
            inStack.Remove(node) |> ignore
            result
    graph |> Map.toList |> List.tryPick (fun (node, _) -> dfs [] node)
```

### New Diagnostic Error Codes
```fsharp
// Source: Extension of Diagnostic.fs TypeErrorKind
// Module error codes use E05xx range
| CircularModuleDependency of cycle: string list   // E0501
| UnresolvedModule of name: string                 // E0502
| DuplicateModuleName of name: string              // E0503
| ForwardModuleReference of name: string           // E0504
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Flat declaration list | Hierarchical module structure | Phase 5 | Module type becomes a tree, not a flat list |
| Global ConstructorEnv/RecordEnv | Module-scoped environments | Phase 5 | Each module has its own exports |
| Direct variable lookup | Qualified + open-aware lookup | Phase 5 | Name resolution walks module chain |

**Key design choices for v1 (simple):**
- Single-file only (no cross-file imports)
- No module aliases (MOD-V2-01)
- No recursive modules (MOD-V2-02)
- No module signatures (MOD-V2-03)
- No access modifiers (public/private)
- Forward references within a file are errors (top-to-bottom order)

## Open Questions

1. **Top-level module declaration semantics**
   - What we know: F# uses `module Name` at the top of a file to declare the file's module. Subsequent declarations are NOT indented.
   - What's unclear: Should our top-level `module Name` require indentation for its body, or follow F#'s convention of no indentation at top level? The IndentFilter currently expects column 0 for module-level declarations.
   - Recommendation: Follow F# convention -- top-level `module Name` does NOT require indentation. Only nested (local) modules use `module Name = <indent>body<dedent>`. This means the parser treats `module Name` at the start of a file as a special case that doesn't consume INDENT/DEDENT.

2. **Qualified name chain for deeply nested modules**
   - What we know: `A.B.c` should resolve module A, then submodule B, then member c. The existing `Atom DOT IDENT` rule is left-recursive and produces chained `FieldAccess` nodes.
   - What's unclear: `FieldAccess(FieldAccess(Var "A", "B"), "c")` naturally encodes `A.B.c`. But at eval/typecheck time, we need to recognize the chain as module access, not record field access.
   - Recommendation: At eval time, check if the leftmost Var resolves to a module. If so, walk the chain through module environments. If not, fall through to record field access. This requires a "module value" representation or a module name set.

3. **Namespace declaration body scope**
   - What we know: In F#, namespaces can only contain types and modules, not bare let bindings. Modules can contain let bindings.
   - What's unclear: Should we enforce this restriction or simplify by treating namespace like a module prefix?
   - Recommendation: For v1, treat `namespace Foo.Bar` as purely a naming prefix. All declarations in the file are scoped under `Foo.Bar`. No restriction on what can appear in a namespace (simplicity over F# compatibility).

4. **Module-level let rec**
   - What we know: Currently `let rec` only works within expressions (`LetRec` AST node has `inExpr`). Module-level function declarations desugar to lambdas.
   - What's unclear: How do module-level recursive functions work? In F#, `let rec f x = ... f ...` at module level is common.
   - Recommendation: Add `LetRecDecl` to Decl type for module-level recursive function declarations. Alternatively, module-level eval can detect self-references and add the function to its own closure (the existing `App` eval already does this for named function calls via `augmentedClosureEnv`). Test if current behavior already supports this.

## Sources

### Primary (HIGH confidence)
- Codebase analysis: Ast.fs, Parser.fsy, Lexer.fsl, TypeCheck.fs, Eval.fs, Bidir.fs, Diagnostic.fs, IndentFilter.fs, Elaborate.fs, Type.fs, Infer.fs, Program.fs
- [F# Modules - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules) -- Official F# module syntax and semantics

### Secondary (MEDIUM confidence)
- [F# Namespaces - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/namespaces) -- Namespace semantics
- [Topological Sort and Cyclic Dependencies](https://dev.to/dawkaka/topological-sort-and-why-youre-getting-cyclic-dependency-errors-20g6) -- DFS cycle detection patterns
- [F# for Fun and Profit: Organizing Functions](https://fsharpforfunandprofit.com/posts/organizing-functions/) -- Module organization best practices

### Tertiary (LOW confidence)
- None -- all key claims verified against codebase and official docs

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new libraries; purely extends existing codebase patterns
- Architecture: HIGH - Follows established ConstructorEnv/RecordEnv threading pattern; F# module semantics well-documented
- Pitfalls: HIGH - LALR(1) conflict risk confirmed by parser analysis; open-ordering confirmed by F# semantics docs
- Code examples: MEDIUM - Patterns are sound but exact grammar rules may need LALR(1) conflict resolution during implementation

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable domain, no external dependency changes)
