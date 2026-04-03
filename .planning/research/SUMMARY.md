# Project Research Summary

**Project:** FunLang — v12.0 Infix Operator Reform + v11.0 Typed AST Export
**Domain:** Language infrastructure — user-defined operator fixity declarations, special AST node removal, and per-expression type annotation export
**Researched:** 2026-04-02
**Confidence:** HIGH

## Executive Summary

This research covers two related but independent milestones for FunLang. The **operator reform** (issues #6 and #7) removes the hardcoded `PipeRight`, `ComposeRight`, and `ComposeLeft` AST nodes and replaces them with ordinary prelude-defined operators backed by a runtime fixity table and a post-parse Pratt rewrite pass. The **typed AST export** (v11.0) adds per-expression type annotations to the pipeline output so FunLangCompiler can replace its own type-guessing elaboration pass with the authoritative FunLang type checker. Both milestones are strictly additive in their first phases, with breaking cleanup deferred to later phases.

The recommended approach for operator reform follows the GHC Haskell model: the LALR(1) parser continues to handle expression structure, but operator chains involving user-declared precedence are collected into `InfixChain` nodes and resolved by a separate Pratt precedence-climbing pass applied immediately after parsing. No replacement of the fsyacc grammar is required or desirable — the existing ~1,000-line grammar handles indent-sensitive parsing, pattern syntax, type classes, and modules, none of which should be touched. For typed AST export, an annotation map approach (span-keyed `Dictionary<Span, Type>`, populated as a side effect during `Bidir.synth`) is preferred over a full parallel `TypedExpr` DU rewrite because it is strictly additive and works with the existing `Ast.Expr` types that FunLangCompiler already understands.

The critical risk for operator reform is silent semantic breakage: the LALR precedence table encodes `|>`'s position relative to comparisons and boolean operators, and any unintended shift produces wrong parse trees that still type-check — just to wrong values. Run the full 714-test suite after every grammar edit. For typed AST export, the main risk is recording unresolved type variables; all `TypeAnnotationMap.record` calls must use `apply s ty` (substitution applied), not the raw synthesis result.

## Key Findings

### Recommended Stack

No new NuGet packages are required for either milestone. The full implementation lives within the existing F# source files. The only new files needed are `FixityEnv.fs` (operator reform), `TypeAnnotationMap.fs` (typed AST export), and `ExportApi.fs` (typed AST export).

**Core technologies:**
- **F# / .NET 10** — implementation language; no change
- **FsLexYacc 11.3.0** — LALR(1) parser; kept as-is; the Pratt pass runs on LALR output, not replacing it
- **System.Collections.Generic.Dictionary** — mutable accumulator for both the fixity table and the type annotation map; O(1) lookup vs. F# immutable `Map`'s O(log n)
- **Argu 6.2.5 / Tomlyn 2.3.0** — no change; optional `--emit-typed-ast` flag adds one Argu case

**Why no parser replacement:** A hand-written Pratt parser would be 2,000–3,000 lines of new code with high regression risk across the indent-sensitive grammar. The GHC two-pass approach achieves the same result with ~300–400 lines of new code and zero grammar rewrite risk.

### Expected Features

**Must have (operator reform table stakes):**
- `#[left N]` / `#[right N]` attribute syntax parsed before `let (op)` definitions (TS-1)
- Runtime fixity table (`FixityEnv`) populated from attribute declarations, with first-character defaults for unattributed operators (TS-2, TS-5)
- Pratt post-processor that resolves `InfixChain` nodes into correctly-nested `App(App(...))` trees before type checking (TS-3)
- `|>`, `>>`, `<<` moved to `Prelude/Core.fun` with `#[left 1]`, `#[left 2]`, `#[right 2]` attributes; `PipeRight`/`ComposeRight`/`ComposeLeft` AST nodes removed (TS-4)
- Error messages for malformed attribute syntax (`#[left]`, `#[nonassoc N]`, negative levels) (TS-6)

**Must have (typed AST export table stakes):**
- `TypeAnnotationMap.fs` — `Dictionary<Span, Type>` populated as `Bidir.synth` side effect (~40 additive `record` calls)
- `ExportApi.fs` — `typeCheckFile` returning `TypedModule` (post-elaboration decls, per-expression types, top-level type schemes, constructor/record/class/instance environments)
- FunLangCompiler project reference to `FunLang.fsproj` (in-process Model A; no serialization)

**Should have (include if low-effort):**
- Attribute on `let rec` operator definitions (D-2 — same parsing infrastructure, minor extra work)
- `--emit-typed-ast` CLI flag (optional Phase 4 of export milestone, useful for debugging)

**Defer to v2+:**
- `#[nonassoc N]` — useful but no current need; comparison operators already non-associative via grammar (D-1)
- Module-scoped fixity — global last-declaration-wins is sufficient for MVP (IR-10)
- Standalone `infixl`/`infixr` fixity declarations separate from the definition (D-3)
- Full `TypedExpr`/`TypedDecl` parallel DU — correct long-term direction but a separate milestone

### Architecture Approach

Operator reform adds `FixityEnv.fs` between `Ast.fs` and the generated lexer in `.fsproj` build order. The LALR parser emits `InfixChain` nodes for user-defined operator chains; `Program.fs` calls `FixityEnv.prattRewrite` immediately after parsing and before type checking. `PreludeResult` gains a `FixityEnv` field so prelude-defined fixity is available to user files. `TypeAnnotationMap.fs` (positioned after `Bidir.fs`, before `TypeCheck.fs`) is populated by Bidir and snapshotted by `ExportApi.fs` (positioned after `Prelude.fs`).

**Major new/modified components:**
1. **FixityEnv.fs** (NEW) — `FixityInfo`, `FixityEnv`, `defaultFixityEnv`, `collectInfixDecls`, `prattRewrite`; inserted between `Ast.fs` and `Lexer.fs` in `.fsproj`
2. **InfixChain / InfixDecl** (NEW in Ast.fs) — transient node for LALR operator chains; `InfixDecl` for `#[left N]` / `#[right N]` declarations
3. **TypeAnnotationMap.fs** (NEW) — `Dictionary<Span, Type>` with `record`, `tryFind`, `clear`, `snapshot`; between `Bidir.fs` and `TypeCheck.fs`
4. **ExportApi.fs** (NEW) — `TypedModule` record + `typeCheckFile` entry point; after `Prelude.fs`; no dependency on `Eval.fs` or `Program.fs`
5. **Bidir.fs** (MODIFIED, additive) — ~40 `TypeAnnotationMap.record span (apply s ty)` calls; no signature changes
6. **Prelude/Core.fun** (MODIFIED) — gains `(|>)`, `(>>)`, `(<<)` definitions with fixity attributes

### Critical Pitfalls

1. **Silent precedence regression (IR-1)** — shifting `|>` relative to comparisons in the `%left`/`%right` table produces wrong values (not parse errors) in 23+ pipe-using flt tests. Run `scripts/fslit tests/flt/` after every grammar edit; add an explicit regression test asserting `1 = 2 |> not` evaluates to `false`.

2. **LALR conflicts from attribute syntax (IR-2)** — `#[` as a new token or `infixl`/`infixr` keywords risk shift-reduce or reduce-reduce conflicts. After any grammar change: `dotnet build ... 2>&1 | grep -i conflict`. Treat any new conflict as a blocker.

3. **Flat-chain ambiguity if LALR and Pratt overlap (IR-3)** — if the LALR grammar partially folds operator chains and Pratt also re-associates, Pratt receives opaque pre-folded `App` nodes it cannot rebalance. Choose one mechanism per operator class: LALR for arithmetic, Pratt for user-declared infix. Do not mix.

4. **FunLangCompiler breaks silently on AST node removal (IR-4)** — removing `PipeRight`/`ComposeRight`/`ComposeLeft` breaks any consumer that pattern-matches those names. Grep all consumers (`grep -rn "PipeRight\|ComposeRight\|ComposeLeft" /Users/ohama/vibe-coding/`) before Phase 4; coordinate with FunLangCompiler.

5. **IndentFilter not updated for new tokens (IR-6)** — any token class change requires updating `isContinuationStart` in `IndentFilter.fs` in the same commit, or multi-line pipe chains silently become parse errors.

6. **Recording unresolved type variables (typed AST anti-pattern)** — in `Bidir.synth`, always record `apply s ty`, never the raw `ty`. Unresolved `TVar` entries are useless to FunLangCompiler.

## Implications for Roadmap

The operator reform phases must be sequential (infrastructure before behavior change, behavior change before prelude migration). The typed AST export phases are independent and can proceed in parallel with operator reform Phase 2 or 3. Both milestones share the same codebase and must maintain a passing 714-test suite at every phase boundary.

### Phase 1: Fixity Infrastructure (no behavior change)

**Rationale:** Add all new types and modules without changing how anything parses or evaluates. This gives confidence the infrastructure compiles cleanly before touching parser behavior. All 714 tests must pass after this phase.
**Delivers:** `FixityEnv.fs`, `InfixDecl` and `InfixChain` AST nodes, `#[left N]` / `#[right N]` attribute parsing, pass-through arms in TypeCheck/Eval/Format, error messages for malformed attributes.
**Addresses:** TS-1, TS-2, TS-6
**Avoids:** IR-2 (check for LALR conflicts after every grammar change), IR-3 (Pratt/LALR boundary must be documented before any grammar changes are written)
**Research flag:** Standard patterns — GHC fixity resolution is the authoritative reference; no additional research needed.

### Phase 2: INFIXOP* Operators Through Pratt

**Rationale:** Change LALR rules for `Expr INFIXOP* Expr` to produce `InfixChain`, implement the full Pratt rewrite, and wire it into `Program.fs` and `Prelude.fs`. For operators with no `InfixDecl`, the default INFIXOP-level-to-numeric-precedence mapping preserves existing behavior exactly.
**Delivers:** Pratt rewrite working end-to-end. User can write `#[left 6] let (<|>) ...` and that operator behaves at precedence 6 regardless of leading character.
**Uses:** `FixityEnv.fs` (Phase 1), `InfixChain` (Phase 1), `PreludeResult.FixityEnv`
**Implements:** TS-3, TS-5
**Avoids:** IR-1 (run full flt suite after grammar changes), IR-7 (use `Dictionary` for fixity table — already mandated by Phase 1 architecture)
**Research flag:** The subtle edge case is the LALR Term/Factor hierarchy interaction (IR-3, ARCHITECTURE-operator-reform Anti-Pattern 5). Strongly consider limiting `InfixChain` to the top-level expression grammar (pipe/compose level) in Phase 2, deferring arithmetic operator routing to a later phase to reduce risk.

### Phase 3: Prelude Migration and Special Node Removal

**Rationale:** With the Pratt rewrite in place and `#[left 1] let (|>) x f = f x` working in Prelude, remove the nine special-case locations for `PipeRight`/`ComposeRight`/`ComposeLeft`. The `--emit-ast` flt tests for compose need updated expected output.
**Delivers:** Issues #6 and #7 closed. ~150 lines removed, ~20 lines added. `|>`, `>>`, `<<` are ordinary prelude functions. 29 `|>` tests, 8 `>>` tests, 4 `<<` tests all pass via the generic `App(App(...))` path.
**Addresses:** TS-4 fully
**Avoids:** IR-4 (grep all consumers before removing AST nodes; coordinate with FunLangCompiler), IR-5 (`-e` mode and REPL must explicitly initialize fixity table — add `fn -e "5 |> (fun x -> x + 1)"` flt test), IR-6 (update `isContinuationStart` atomically with lexer changes), IR-8 (preserve specific Bidir error messages via `match op with "|>" -> ...` in the generic handler), IR-12 (update `Format.fs` in the same commit as `Ast.fs`)
**Research flag:** Needs coordination with FunLangCompiler repo before removing AST nodes. Verify no hard-coded `"PipeRight"` strings in FunLangCompiler's JSON/AST consumer.

### Phase 4: Typed AST Export (TypeAnnotationMap + ExportApi)

**Rationale:** Independent of operator reform; can proceed in parallel with Phase 2 or 3. Strictly additive. FunLangCompiler gains authoritative types and can remove its own elaboration heuristics (~250 lines of 6 tracking sets and 8 heuristic functions).
**Delivers:** `TypeAnnotationMap.fs`, `ExportApi.fs`, `TypedModule` record, `typeCheckFile` entry point. FunLangCompiler project reference replaces its private parser/lexer/elaboration copy.
**Uses:** Existing `Bidir.fs` `synth` cases, `TypeCheck.typeCheckModuleWithPrelude`, `Elaborate.elaborateTypeclasses`
**Avoids:** Recording raw `TVar` (always `apply s ty`), recording in `check` instead of `synth`, putting `ExportApi` in `Program.fs`, using immutable `Map` instead of `Dictionary`
**Research flag:** Standard patterns — annotation map matches existing `Bidir.mutableVars`/`pendingConstraints` idiom. The optional `--emit-typed-ast` CLI sub-phase requires designing a `Type` DU JSON schema; check F# `System.Text.Json` discriminated union support before that sub-phase begins.

### Phase Ordering Rationale

- Phases 1-2-3 (operator reform) are strictly sequential: fixity infrastructure must compile before behavior changes; behavior changes must work before prelude migration; prelude functions must be defined before special AST nodes are removed.
- Phase 4 (typed AST export) is independent of operator reform; the only coordination point is ensuring `ExportApi.fs` is updated alongside Phase 3 if it serializes `PipeRight`/`ComposeRight`/`ComposeLeft` nodes.
- The phased approach preserves a passing 714-test suite at every phase boundary — this is non-negotiable given the breadth of the grammar and the silent-failure modes of precedence regressions.
- Phase 3 (node removal) must not begin until prelude definitions of `|>`, `>>`, `<<` are working end-to-end with correct precedence and TCO — verify with a deep pipe chain tail-call stress test before proceeding.

### Research Flags

Phases needing deeper implementation attention during planning:
- **Phase 2 (Pratt + LALR interaction):** The Term/Factor hierarchy boundary (IR-3) is the subtlest part of the reform. Consider limiting `InfixChain` scope to top-level operators only in Phase 2 to reduce risk surface.
- **Phase 4 (typed export, `--emit-typed-ast` sub-phase):** F# discriminated union JSON serialization needs evaluation before schema is designed. Check `FSharp.SystemTextJson` vs. manual serialization.

Phases with standard patterns (no additional research needed):
- **Phase 1:** Pure type/module addition; well-understood F# patterns.
- **Phase 3:** Mirrors existing `TypeEnv` accumulation in `loadPrelude`; node removal is mechanical with enumerated file/line locations.
- **Phase 4 (core annotation map):** Matches existing mutable-side-table pattern in `Bidir.fs`.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Direct codebase inspection; all file locations enumerated; no new packages |
| Features | HIGH | All 12 consumer files for PipeRight/ComposeRight/ComposeLeft found via grep; INFIXOP0-4 system fully traced; 6 heuristic sets and 8 functions in FunLangCompiler inventoried |
| Architecture (operator reform) | HIGH | GHC fixity resolution is authoritative precedent; FunLang architecture fully traced across all affected files |
| Architecture (typed AST export) | HIGH | `Bidir.synth` return type and substitution threading verified; Span uniqueness confirmed for non-synthetic nodes |
| Pitfalls | HIGH | Derived from direct source reading + known LALR/Pratt failure modes; all IndentFilter line numbers verified |

**Overall confidence:** HIGH

### Gaps to Address

- **TCO preservation for `|>` after prelude migration (MEDIUM):** The `App` trampoline should handle `(|>) x f = f x` in tail position correctly, but needs an explicit test comparing tail-recursive behavior before and after migration. Add a deep pipe chain stress test (e.g., 10,000 iterations) that would stack-overflow without TCO before Phase 3 lands.

- **`<<` right-associativity window (MEDIUM):** Until Phase 2 is complete and `#[right 2]` is active, `<<` may be temporarily left-associative if routed through the character-class rule. Add a Phase 1 regression test asserting `(f << g << h) 1 = f (g (h 1))` fails visibly if associativity is wrong.

- **FunLangCompiler node name audit (requires external repo access):** Before Phase 3, grep the FunLangCompiler repo for `"PipeRight"`, `"ComposeRight"`, `"ComposeLeft"` as strings to determine if a compatibility shim is needed in `ExportApi.fs` during the transition window.

- **`--emit-typed-ast` JSON schema:** The `Type` discriminated union needs a serialization format. This is a sub-gap within Phase 4; the core annotation map work does not require it.

- **MatchCompile.fs pipeline position (typed export):** Verify whether `MatchCompile.fs` runs before or after `elaborateTypeclasses`. If it creates synthetic match tree nodes after type checking, those nodes have no annotation map entries. Establish exact pipeline order before Phase 5 of typed export begins.

## Sources

### Primary (HIGH confidence — direct codebase reads, 2026-04-02)
- `src/FunLang/Ast.fs` — DU cases (lines 104-112), `Decl` type (lines 349-371), `spanOf`
- `src/FunLang/Lexer.fsl` — `classifyOperator` (lines 11-22), hardcoded pipe/compose tokens (lines 127-130)
- `src/FunLang/Parser.fsy` — precedence table (lines 98-111), INFIXOP0-4 rules (lines 293-355), pipe/compose grammar rules (lines 281-283)
- `src/FunLang/IndentFilter.fs` — `isContinuationStart` (lines 104-110)
- `src/FunLang/Bidir.fs` — `synth` cases, pipe/compose handlers (lines 737-773)
- `src/FunLang/Eval.fs` — eval cases for pipe/compose (lines 1584-1621)
- `src/FunLang/TypeCheck.fs` — four match arms (lines 420, 556, 647, 748-750), `typeCheckModuleWithPrelude`
- `src/FunLang/Prelude.fs` — `PreludeResult` type (lines 13-22), `loadPrelude` (lines 266-316)
- `src/FunLang/Program.fs` — pipeline entry points
- `src/FunLang/Format.fs` — AST printer cases (lines 98-100, 209-211)
- `src/FunLang/Infer.fs` — stub arms (lines 407-408)
- `tests/flt/` — 29 files using `|>`, 8 using `>>`, 4 using `<<`, 714 total

### Secondary (HIGH confidence — authoritative external references)
- [Haskell 98 Report: fixity declarations](https://www.haskell.org/onlinereport/decls.html) — two-pass model; direct precedent for the LALR + Pratt approach
- [Kowainik: Fix(ity) me](https://kowainik.github.io/posts/fixity) — GHC post-parse Pratt pass mechanics
- [Simple but Powerful Pratt Parsing (matklad)](https://matklad.github.io/2020/04/13/simple-but-powerful-pratt-parsing.html) — binding-power algorithm used in `prattRewrite`
- [Rust compiler dev guide](https://rustc-dev-guide.rust-lang.org/the-parser.html) — `#[attr]` as single token design
- [OCaml custom operators](https://blog.shaynefletcher.org/2016/09/custom-operators-in-ocaml.html) — character-class precedence (what FunLang already does)
- [Adamant: Operator Precedence](https://blog.adamant-lang.org/2019/operator-precedence/) — intransitive precedence; surveyed and rejected as over-engineering

### Tertiary (MEDIUM confidence — surveyed, not primary guidance)
- FSharp.Compiler.Service `GetSymbolUseAtLocation` — validates span-keyed annotation map as industry practice
- GHC Trees That Grow (Najd & Peyton Jones, JFP 2019) — validates why full `TypedExpr` DU is over-engineering at FunLang's scale
- [Adamant precedence blog post](https://blog.adamant-lang.org/2019/operator-precedence/) — partial-order precedence; surveyed, rejected

---
*Research completed: 2026-04-02*
*Ready for roadmap: yes*
