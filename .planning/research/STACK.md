# Technology Stack: Typed AST Export

**Project:** FunLang — ML-style functional language interpreter
**Researched:** 2026-04-02
**Milestone:** v11.0 — Typed AST Export (binding type env + node annotations)
**Confidence:** HIGH — strategy derived from direct codebase inspection

---

## Existing Stack (No NuGet Changes Needed)

| Technology | Version | Role |
|------------|---------|------|
| F# | .NET 10 | Implementation language |
| FsLexYacc | 11.3.0 | Lexer/parser generation |
| Argu | 6.2.5 | CLI argument parsing |
| Tomlyn | 2.3.0 | funproj.toml parsing |

No new NuGet packages. Typed AST export is a pure internal architecture change
implemented within the existing F# source files.

---

## The Core Decision: What Form Should the Export Take?

Three approaches exist for exporting type information from a type checker. They
are ordered from simplest to most invasive, and the right choice depends on what
FunLangCompiler actually needs.

### Approach A: Binding Type Environment (Phase 1 only)

**What:** After type checking, return `Map<string, Scheme>` — the final type
environment mapping top-level binding names to their inferred type schemes.

**How it maps to FunLang now:** `typeCheckModuleWithPrelude` already returns
`TypeEnv` (which is `Map<string, Scheme>`) as its seventh return value. The
compiler currently ignores this and runs heuristics instead.

**What FunLangCompiler gets:** The type of each top-level `let` binding in the
file. `val myFunc : int -> string -> bool`. Sufficient to replace heuristics
that guess binding types.

**Cost:** Near-zero. The data already exists. The only work is:
1. Add a `--emit-typed-env` CLI flag (or extend `--emit-type` output format)
2. Serialize the `TypeEnv` to a consumable format (JSON or stdout)

**Limitation:** Does not provide types for subexpressions. The compiler cannot
ask "what is the type of this particular `App` node at line 14?"

---

### Approach B: Span-keyed Type Map (Phase 2, recommended)

**What:** During type checking, accumulate a `Map<Span, Type>` that records the
inferred type for every expression node that was checked. Return this alongside
the existing outputs.

**How it maps to FunLang now:** Every `Expr` variant carries a `Span`. The
`synth` function in `Bidir.fs` already computes the type for every expression it
visits. The only missing piece is writing those types into an accumulator.

**What FunLangCompiler gets:** The type of any expression node, identified by
source location. `(Span("foo.fun", 14, 3, 14, 12), TArrow(TInt, TString))`.
The compiler can look up "what is the type at line 14, columns 3-12?" and get
an exact answer instead of heuristics.

**Cost:** Moderate. Requires:
1. A mutable accumulator in `Bidir.fs` (`let mutable nodeTypes : Map<Span, Type> = Map.empty`)
2. One write per `synth`/`check` call: `nodeTypes <- Map.add (spanOf expr) resolvedTy nodeTypes`
3. Expose the accumulator through `typeCheckModuleWithPrelude`
4. A serialization format for external consumption

**Why span-keyed, not node-ID-keyed:** Every AST node already carries a `Span`.
Adding a synthetic node ID would require modifying every Ast.fs DU variant (100+
cases). Span is a stable, deterministic key that the compiler can compute from
source positions it already knows.

**Critical detail — apply substitution before recording:** The `synth` function
returns a `(Subst, Type)` pair. The `Type` may still contain unresolved `TVar`
references at the point of recording. The substitution must be applied before
storing: `Map.add span (Type.apply subst ty) nodeTypes`. Failure to do this
produces stale `TVar n` entries in the map.

---

### Approach C: Annotated AST (full typed tree, Phase 3 if needed)

**What:** Define a parallel `TypedExpr` DU where every constructor carries an
additional `Type` field. Transform `Expr -> TypedExpr` in a post-pass.

**How ML-family compilers do this:**
- GHC uses `HsExpr GhcTc` where the type-checker stage is a phantom type parameter
  that changes the annotation type from `()` to `Type`. The actual annotation is
  stored in a `XRec` wrapper that carries the type alongside the source span.
- OCaml's `typedtree.ml` defines `expression` with a `exp_type : Types.type_expr`
  field on every node. It is a completely separate type from `parsetree.ml`.
- F# compiler (FSharp.Compiler.Service) uses `TypedTreeOps` — a separate typed
  AST with every node carrying a `TType`.
- The standard pattern in all three is: untyped AST (from parser) → separate
  typed AST (from type checker), not mutation of the original.

**Cost for FunLang:** High. Would require duplicating the entire `Expr` DU (~40
constructors) into a `TypedExpr` DU with `Type` annotations, then writing a
complete traversal that converts `Expr * TypeEnv -> TypedExpr`. This is several
hundred lines of mechanical code and would need to stay in sync with every
future `Expr` addition.

**Verdict: Do not build Phase 3 until the consumer (FunLangCompiler) proves it
needs per-node types in a typed tree format rather than a span-keyed lookup.**
The span-keyed map (Approach B) gives identical information with far less
structural coupling.

---

## Recommended Implementation Strategy

### Phase 1: Extend the existing `--emit-type` output and API

`typeCheckModuleWithPrelude` already returns `TypeEnv`. The FunLangCompiler
currently ignores it. Phase 1 is making that data accessible in a structured
format.

**Serialization: JSON via `System.Text.Json`.**

Why JSON:
- .NET 10 ships `System.Text.Json` in the BCL — zero new NuGet dependencies.
- FunLangCompiler is a separate process; in-memory sharing requires a shared
  library or IPC. JSON over stdout is the simplest cross-process interface.
- Existing `--emit-ast` and `--emit-type` already use stdout text output.
  JSON follows that pattern.

Why not binary (MessagePack, Protobuf):
- Would require NuGet packages in both FunLang and FunLangCompiler.
- Adds no benefit for the volume of data (a type env is typically <200 entries).

Why not a shared .NET library:
- Would create a compilation dependency coupling two repos. JSON over process
  boundary preserves independent versioning.

**New CLI flag: `--emit-typed-env`**

Outputs the post-inference `TypeEnv` as JSON to stdout:
```json
{
  "bindings": [
    { "name": "myFunc", "type": "int -> string -> bool", "scheme": "forall 'a. 'a -> string" }
  ]
}
```

The `type` field uses `Type.formatSchemeNormalized` (already exists).
An optional structured `scheme` field for machine-readable type representation
can be added by serializing the `Type` DU as tagged JSON.

**Integration point:** `Program.fs` already has the `--emit-type` branch at
line 329. A new `--emit-typed-env` branch follows the same pattern.

---

### Phase 2: Span-keyed node type map

**New type in `TypeCheck.fs` or a new `TypedAst.fs`:**

```fsharp
/// Map from source span to inferred type, built during type checking.
/// Keyed on Span for cross-process consumption (stable, no synthetic IDs needed).
type NodeTypeMap = Map<Ast.Span, Type>
```

**Accumulator in `Bidir.fs`:**

```fsharp
/// Accumulated per-node type annotations. Reset at typeCheckModuleWithPrelude entry.
/// Populated during synth/check traversal. Apply substitution before writing.
let mutable nodeTypeMap : NodeTypeMap = Map.empty
```

This follows the exact pattern of `mutableVars` and `pendingConstraints` already
in `Bidir.fs` — mutable module-level state reset at the entry point of each
top-level type check.

**Write site — one line added to `synth` at the return point:**

```fsharp
// In Bidir.synth, before returning (subst, ty):
let resolvedTy = Type.apply subst ty
nodeTypeMap <- Map.add (Ast.spanOf expr) resolvedTy nodeTypeMap
(subst, ty)  // Return unchanged — accumulator is side-effect
```

**Expose through `typeCheckModuleWithPrelude`:**

The return type extends from 7-tuple to 8-tuple:
```fsharp
Result<Diagnostic list * ConstructorEnv * RecordEnv * ClassEnv * InstanceEnv
       * Map<string, ModuleExports> * TypeEnv * NodeTypeMap, Diagnostic list>
```

All existing callers use tuple destructuring with `_` for fields they ignore.
The additional field requires only adding `, _nodeTypeMap` to existing match
arms. F# exhaustive matching ensures no site is missed.

**New CLI flag: `--emit-typed-ast`**

Outputs the node type map as JSON keyed by span:
```json
{
  "nodes": [
    {
      "file": "foo.fun", "startLine": 14, "startCol": 3,
      "endLine": 14, "endCol": 12,
      "type": "int -> string"
    }
  ]
}
```

---

## Patterns From ML-Family Compilers

### How OCaml represents typed AST

OCaml's compiler separates `Parsetree` (untyped) from `Typedtree` (typed).
The typed tree is produced by `Typecore.type_expression` and carries `exp_type`
on every node. The key design choice: **typed tree is a separate type**, not an
annotation added to the parsed tree.

**Lesson for FunLang:** The OCaml approach (Approach C above) works well for a
full compiler because the typed tree drives code generation. For FunLang, which
has a separate evaluator that already works on the untyped `Expr`, building a
full parallel typed tree is over-engineering for the stated goal.

### How GHC represents typed AST

GHC uses Trees That Grow (Najd & Peyton Jones, 2016): a single parametric
`HsExpr p` where `p` is a phase index. Type annotations are in `XRec p (HsExpr p)`.
During type checking, the phase is `GhcTc` and the extension fields carry types.

**Lesson for FunLang:** Trees That Grow is elegant but requires F# computation
expressions or type-level tricks to implement correctly. Not worth it for
FunLang's scale. The span-keyed map achieves the same query capability with no
type-level machinery.

### How F# compiler service exposes typed information

FSharp.Compiler.Service exposes `FSharpCheckFileResults.GetSymbolUseAtLocation`
— a span-keyed query API over the typed results. Internally it stores a
`SemanticModel`-like dictionary of span → symbol. The consumer does not need
a typed AST; it queries by position.

**This is exactly Approach B.** The F# compiler itself chose span-keyed maps
for its public API because it decouples the compiler's internal representation
from the consumer's view.

---

## Attaching Type Info Without Massive Refactoring

The standard patterns for avoiding a full typed-tree rewrite are:

| Pattern | How | When to Use |
|---------|-----|-------------|
| **Side-effect accumulator** | Mutable `Map<Span, Type>` populated during traversal | Recommended for FunLang Phase 2 |
| **Parallel dictionary** | `Dictionary<NodeId, Type>` where NodeId is an added field | Requires AST surgery; avoid |
| **Post-pass annotation** | Run a second traversal over the checked AST to add types | Requires re-running substitution logic; error-prone |
| **Full typed AST** | Separate DU mirroring Expr with Type on each node | Maximum correctness; high maintenance cost |

FunLang's `Bidir.synth` is a recursive descent with substitution accumulation.
The side-effect accumulator fits naturally: it is already using mutable module
state (`mutableVars`, `pendingConstraints`, `accumulatedErrors`). Adding
`nodeTypeMap` follows the established pattern without changing any function
signatures in the hot path.

---

## What NOT to Build

**Do not build a TypedExpr DU** (Approach C) for this milestone. The 40+
constructor `Expr` type would need to be mirrored completely. Every future `Expr`
addition would require a matching `TypedExpr` addition. The FunLangCompiler
consumer does not need a typed tree — it needs to query "what is the type at
position X?" which the span-keyed map answers directly.

**Do not add a node ID to Expr variants.** Span is already a stable, unique
(enough) identifier. Adding `id: int` to every Expr constructor is 400+ lines
of mechanical change to Ast.fs, Parser.fsy, Format.fs, and all match arms.

**Do not use MessagePack or Protobuf.** The data volume does not justify the
dependency. System.Text.Json in the BCL is sufficient.

**Do not attempt to serialize FunLang `Type` as a schema-validated external
format in Phase 1.** Use `formatSchemeNormalized` (string) for Phase 1. Phase 2
can add a structured JSON representation of `Type` if the compiler needs
programmatic access to type structure rather than display strings.

---

## File-Level Change Summary

| File | Change | Scope |
|------|--------|-------|
| `Type.fs` | No change | — |
| `Bidir.fs` | Add `nodeTypeMap` mutable accumulator; write resolved type per `synth` call | ~10 lines |
| `TypeCheck.fs` | Reset `nodeTypeMap` at entry; extend return tuple to include it | ~5 lines |
| `Cli.fs` | Add `Emit_Typed_Env` and `Emit_Typed_Ast` DU cases | ~6 lines |
| `Program.fs` | Add `--emit-typed-env` and `--emit-typed-ast` branches using `System.Text.Json` | ~40 lines |
| `Ast.fs` | No change | — |
| `Infer.fs` | No change | — |
| `Unify.fs` | No change | — |
| `Eval.fs` | No change | — |

**Total estimated change: ~60 lines across 3 files. No new NuGet packages.**

The binding type env (Phase 1) requires only the `Program.fs` + `Cli.fs`
changes — the `TypeEnv` is already in the return value. Phase 2 (node map)
adds the `Bidir.fs` + `TypeCheck.fs` changes.

---

## Sources

- Codebase inspection: `Ast.fs`, `Type.fs`, `Bidir.fs`, `TypeCheck.fs`,
  `Program.fs`, `Cli.fs`, `FunLang.fsproj` (all read directly, 2026-04-02)
- OCaml compiler `typing/typedtree.ml`: parallel typed AST design
- GHC Trees That Grow (Najd & Peyton Jones, JFP 2019): parametric phase-indexed AST
- FSharp.Compiler.Service: span-keyed `GetSymbolUseAtLocation` query API
- .NET 10 BCL: `System.Text.Json` — no NuGet required
