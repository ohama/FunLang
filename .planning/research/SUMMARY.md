# Project Research Summary

**Project:** FunLang — v11.0 Typed AST Export
**Domain:** Compiler infrastructure — per-expression type annotation export from an ML-style HM inference engine to an external MLIR codegen consumer
**Researched:** 2026-04-02
**Confidence:** HIGH

## Executive Summary

FunLangCompiler currently maintains roughly 250 lines of heuristic code (6 tracking sets, 8 re-inference functions) to guess the types that FunLang's Hindley-Milner type checker already computed. The v11.0 milestone eliminates this entirely by exporting per-expression type information from FunLang's `Bidir.synth` pass into a `TypeAnnotationMap` (a `Dictionary<Span, Type>`), exposing it through a new `ExportApi.typeCheckFile` entry point that bundles the post-elaboration `Decl list`, per-expression types, top-level binding schemes, and environment metadata into a single `TypedModule` record consumed in-process by FunLangCompiler.

The recommended implementation is strictly additive. `Bidir.fs` gains ~40 `TypeAnnotationMap.record` call sites (one per `synth` case), `TypeCheck.fs` gains a `clear()` call at the entry point, and two new files (`TypeAnnotationMap.fs`, `ExportApi.fs`) are added to the project. No new NuGet packages are needed. No existing function signatures change. The full build is estimated at ~60–100 lines of new code across 4 files, with the highest-risk work being correct substitution application at every collection site and correct handling of type-class-elaborated nodes.

The primary risk is correctness, not complexity: recording raw `TVar` indices before applying the accumulated substitution, or failing to resolve instance method types through `TypeEnv` name lookup after `elaborateTypeclasses` rewrites `InstanceDecl` to `LetDecl`. Both are well-understood and have clear, low-cost mitigations. The sequential-only constraint on `Bidir`'s mutable state is an accepted pre-existing limitation of the codebase that this feature extends, not introduces.

---

## Key Findings

### Recommended Stack

No new NuGet packages are required. The entire feature is implemented within the existing F# + .NET 10 stack. `System.Text.Json` (BCL) is sufficient for an optional `--emit-typed-ast` CLI flag if cross-process JSON output is later desired, but the primary integration model is a direct .NET project reference from FunLangCompiler to FunLang, passing `TypedModule` in memory.

**Core technologies:**
- **F# / .NET 10** — implementation language; no version change needed
- **System.Collections.Generic.Dictionary** — mutable `Span -> Type` accumulator in `TypeAnnotationMap.fs`; chosen over F#'s immutable `Map` to avoid O(n) allocation per `synth` call
- **System.Text.Json (BCL)** — optional JSON serialization for `--emit-typed-ast` debug flag; zero NuGet cost

### Expected Features

The feature set is fully determined by what is required to replace all 6 tracking sets and 8 heuristic functions in `FunLangCompiler.Elaboration.fs`. Every item on the must-have list resolves automatically from a single underlying capability: per-expression type annotation.

**Must have (table stakes):**
- **TS-1: Per-expression type annotation** — every `Expr` node in the post-elaboration tree can be queried for its resolved `Type`; this alone replaces all heuristics
- **TS-2: Mutable variable flag** — `LetMut` vs. `Let` distinction preserved in the exported `Decl list`; no additional work (already present in `Ast.Expr` constructors)
- **TS-3 through TS-6: Derived dispatch types** — Hashtable key type, ForIn collection type, Lambda parameter type, to_string argument type all fall out of TS-1 at zero additional cost; the compiler reads `.ty` on the relevant expression node

**Should have (differentiators, deferrable):**
- **D-1: Typed pattern bindings** — types on `VarPat`-bound variables in match arms; replaces residual `BoolVars`/`StringVars` propagation through destructuring
- **D-3: Declaration-level type schemes** — `LetDecl` nodes carry inferred `Scheme`; replaces `FuncSignature.ReturnIsBool` heuristic
- **D-4: Version tag in export** — `version: string` field at export root for FunLangCompiler to detect stale exports at startup

**Defer to post-MVP:**
- JSON/binary serialization of the full `TypedModule` for cross-process use
- `TypedExpr` parallel DU (Approach C from STACK.md) — the span-keyed map gives identical information with no structural coupling; only build if FunLangCompiler proves it needs tree-structured typed output
- Monomorphization, explicit dictionary passing, or Core IR-style lowering in the export

### Architecture Approach

The architecture follows FunLang's established pattern of module-level mutable state for cross-cutting concerns during type checking (`mutableVars`, `pendingConstraints`). A new `TypeAnnotationMap.fs` module holds a `Dictionary<Span, Type>` populated as a side effect of `Bidir.synth`. A new `ExportApi.fs` module wraps the existing pipeline into a single `typeCheckFile` call that returns a `TypedModule` record. FunLangCompiler adds a project reference and removes its own Parser/Lexer/Elaboration.fs entirely.

**Major components:**
1. **TypeAnnotationMap.fs (NEW)** — mutable `Dictionary<Span, Type>`; `record`, `tryFind`, `clear`, `snapshot` API; placed between `Bidir.fs` and `TypeCheck.fs` in project file order
2. **Bidir.fs (MODIFIED, additive)** — one `TypeAnnotationMap.record span (Type.apply s ty)` call added to the return point of each `synth` case (~40 sites); no signature changes
3. **TypeCheck.fs (MODIFIED, minimal)** — `TypeAnnotationMap.clear()` at entry to `typeCheckModuleWithPrelude`; ensures per-file isolation
4. **ExportApi.fs (NEW)** — `TypedModule` record type + `typeCheckFile` function; calls parse, clear, typecheck, snapshot, elaborate, bundle; placed after `Prelude.fs`; no dependency on `Eval.fs` or `Program.fs`
5. **FunLangCompiler (PHASE 5)** — adds project reference, replaces `ElabEnv` set lookups with `TypedModule.ExprTypes[span]` queries, deletes heuristics

### Critical Pitfalls

1. **Annotating pre-elaboration structure for instance methods (TA-1)** — `elaborateTypeclasses` creates new `LetDecl` nodes after type checking, so their spans are not in the annotation map. For elaborated nodes, look up the method's `Scheme` from `TypeEnv` by name. Never key instance method types by span alone.

2. **Recording raw `TVar` before applying the accumulated substitution (TA-2)** — `Bidir.synth` returns `(Subst * Type)` where `Type` may still contain unresolved `TVar n`. Always store `Type.apply s ty`, never the raw `ty`. Failure produces a map full of useless `TVar 1042` entries that the compiler cannot act on.

3. **Changing `synth`'s return type to carry a `TypedExpr` (TA-7)** — this touches 67+ call sites and risks introducing inference bugs. The mutable accumulator achieves the same result without touching existing signatures. Do not change `synth`'s return type.

4. **Using F# immutable `Map` for the accumulator instead of `Dictionary` (TA-9)** — each `Map.add` allocates a new AVL tree node; over tens of thousands of synth calls this is significant. Use `Dictionary<Span, Type>` and gate collection behind a flag so normal interpreter runs incur zero overhead.

5. **Missing builtin and prelude types for `Var` reference resolution (TA-3)** — the per-span map only covers nodes visited by `Bidir.synth` for user-module code. `println`, `map`, `to_string` etc. appear as `Var` nodes with no span map entry. The export must include `TypeCheck.initialTypeEnv` and `PreludeResult.TypeEnv` as separate lookup tables in `TypedModule`.

---

## Implications for Roadmap

The natural phase structure follows the existing architecture and minimizes regression risk at each step. All phases are sequentially dependent except the CLI flag (Phase 4), which is optional.

### Phase 1: TypeAnnotationMap Module

**Rationale:** Foundation with no call sites yet. Build and test the module in isolation before touching `Bidir.fs`. Locks in the data structure choice (Dictionary vs. Map) before any collection code is written.
**Delivers:** `TypeAnnotationMap.fs` with `record`, `tryFind`, `clear`, `snapshot`; project file updated; F# unit tests passing.
**Addresses:** TA-9 (data structure choice locked in before recording sites are written), TA-5 (collection strategy decided up front)
**Avoids:** Retrofitting from immutable Map to Dictionary after 40 recording sites already exist

### Phase 2: Wire Bidir.fs to Record Types

**Rationale:** This is the core work and the highest-risk phase. The annotation map is populated here. Must be complete before ExportApi can return meaningful data. Run the full test suite after each batch of recording sites to catch regressions early.
**Delivers:** Every real-source expression node has a resolved `Type` in the annotation map after type checking. `TypeCheck.fs` clears the map at each `typeCheckModuleWithPrelude` entry.
**Addresses:** TS-1 (all heuristic replacements depend on this), TA-2 (substitution discipline at every site), TA-11 (GADT branch substitution applied before storing)
**Avoids:** Any change to existing function signatures; all changes are additive

### Phase 3: ExportApi Module

**Rationale:** Once the annotation map is populated correctly, bundling outputs into `TypedModule` is mechanical. This phase also resolves the elaboration/synthetic-node issue by applying the `TypeEnv` name fallback for elaborated `LetDecl` nodes.
**Delivers:** `ExportApi.typeCheckFile` returning `TypedModule` with `Decls`, `TopLevelTypes`, `ExprTypes`, `CtorEnv`, `RecEnv`, `ClassEnv`, `InstEnv`, `Warnings`. Integration test: call on a known source file, verify `ExprTypes` is non-empty with correct types for specific spans.
**Addresses:** TA-1 (elaboration span gap resolved via TypeEnv name lookup), TA-3 (builtin/prelude type tables included in TypedModule), TA-4 (per-call-site span keying for type-class dispatch)
**Avoids:** Anti-Pattern 5 — ExportApi must not live in `Program.fs`; keeps CLI/Argu dependencies separate from the library entry point

### Phase 4: CLI Debug Flag (Optional)

**Rationale:** Not required for FunLangCompiler integration (Model A uses in-process reference), but valuable for inspecting export correctness during Phase 5 work.
**Delivers:** `--emit-typed-ast` flag writing `TypedModule` as JSON to stdout; `--emit-typed-env` flag for top-level `TypeEnv` only (essentially free since `TypeEnv` is already in the return tuple).
**Addresses:** D-4 (version tag can be added here)
**Avoids:** Anti-Pattern 3 — do not add JSON serialization layer to the in-process path

### Phase 5: FunLangCompiler Integration

**Rationale:** Can only begin after Phase 3 delivers a validated `ExportApi`. This phase is in the FunLangCompiler repo. The work is largely mechanical: add project reference, replace `ElabEnv` set queries with `ExprTypes[span]` lookups, delete the 6 sets and 8 heuristic functions.
**Delivers:** FunLangCompiler with no duplicate parser/lexer, no type-guessing heuristics, all type dispatch driven by `TypedModule.ExprTypes`. Estimated ~250 lines deleted from `Elaboration.fs`.
**Addresses:** All TS-1 through TS-6 heuristic replacements; TA-12 (consumer documentation on `Scheme` vs. monotype distinction)
**Avoids:** Keeping heuristics as fallback — delete them entirely; partial adoption with heuristic backup creates ambiguity about which path is authoritative

### Phase Ordering Rationale

- Phases 1-2-3 are strictly sequential: the map must exist before Bidir writes to it; Bidir must write before ExportApi returns useful data.
- Phase 4 (CLI flag) is independent of Phase 5 and can be deferred or skipped if FunLangCompiler integration proceeds smoothly via in-process access.
- Phase 5 is in a separate repo and must not begin until Phase 3 passes integration tests on real FunLang source files covering all expression variants.
- The most important ordering constraint: do not attempt FunLangCompiler integration until the annotation map has been validated across all `synth` variants.

### Research Flags

Phases needing careful implementation attention (the domain is fully understood; no external research needed):
- **Phase 2:** Highest implementation risk. GADT branches, type-class method resolution, and substitution correctness at every `synth` case need per-case review. Work in small batches and run tests between them.
- **Phase 5:** FunLangCompiler heuristic inventory is HIGH confidence, but `isPtrParamBody` (250-line function) may surface edge cases not covered by existing tests. Plan for targeted test additions before deleting it.

Phases with standard patterns (no deeper research needed):
- **Phase 1:** Standard F# module with Dictionary. Mechanical.
- **Phase 3:** Standard wrapper/bundling following the existing pipeline. Low risk.
- **Phase 4:** Standard CLI flag addition following the existing `--emit-ast`/`--emit-type` pattern in `Program.fs`.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All conclusions from direct codebase inspection; no external dependencies to validate |
| Features | HIGH | Heuristic inventory read from Elaboration.fs directly; all 6 sets + 8 functions confirmed; replacement mapping is one-to-one |
| Architecture | HIGH | Full source read of Bidir.fs, TypeCheck.fs, Elaborate.fs, Program.fs; proposed pattern matches existing mutable-state conventions |
| Pitfalls | HIGH | All pitfalls derived from actual source structure; no speculation |

**Overall confidence:** HIGH

### Gaps to Address

- **Lambda parameter ground-type coverage (MEDIUM):** `isPtrParamBody` handles edge cases where lambda params are captured from outer scopes or involved in mutual recursion. After typed export, verify inference produces ground types (not `TVar`) for lambda parameters across all test cases before declaring `isPtrParamBody` fully replaceable. If residual `TVar` occurs for some params, a targeted fallback may be needed for that specific case.

- **MatchCompile.fs pipeline position (needs clarification):** Clarify whether `MatchCompile.fs` rewrites match expressions before or after `elaborateTypeclasses`. If it runs after type checking, the post-compilation match tree has synthetic nodes not in the annotation map. Establish exact pipeline order before Phase 5 begins.

- **Per-call-site type-class dispatch correctness (needs a targeted test):** The annotation map should record per-call-site instantiated types for `Var` references to polymorphic methods. Verify with a test case that `show 42` and `show "hello"` in the same file produce distinct `Type` entries for the two `Var("show", span)` nodes before FunLangCompiler integration.

---

## Sources

### Primary (HIGH confidence)
- FunLang source (direct read, 2026-04-02): `Ast.fs`, `Type.fs`, `Bidir.fs`, `TypeCheck.fs`, `Elaborate.fs`, `Prelude.fs`, `Program.fs`, `Cli.fs`, `FunLang.fsproj`
- FunLangCompiler source (direct read, 2026-04-02): `Elaboration.fs` — all 6 tracking sets and 8 heuristic functions inventoried

### Secondary (MEDIUM confidence)
- FSharp.Compiler.Service: span-keyed `GetSymbolUseAtLocation` API — validates the span-keyed map approach as industry practice
- OCaml `typing/typedtree.ml`: parallel typed AST design — validates the alternative (full TypedExpr DU) as correct but higher-cost for this codebase
- GHC Trees That Grow (Najd & Peyton Jones, JFP 2019): parametric phase-indexed AST — validates why a full typed AST DU is over-engineering at FunLang's scale
- "Typing Haskell in Haskell" (Jones 1999): per-expression annotation approach in HM systems

---
*Research completed: 2026-04-02*
*Ready for roadmap: yes*
