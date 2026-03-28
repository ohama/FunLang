# Project Research Summary

**Project:** LangThree v6.0 — Practical Programming Features
**Domain:** ML-style functional language interpreter extension
**Researched:** 2026-03-28
**Confidence:** HIGH

## Executive Summary

LangThree v6.0 adds three practical programming features to an already-mature interpreter codebase: newline implicit sequencing, `for x in collection do` loops, and expanded Option/Result Prelude utilities. All three features extend well-understood existing infrastructure with no new dependencies, no stack changes, and no major architectural redesigns. The recommended approach is to implement in a specific order driven by dependency: newline sequencing first (every subsequent test benefits from it), for-in loops second (new AST node + parser + type checker + evaluator), and Prelude utilities last (pure `.fun` additions with zero interpreter changes).

The key risk is concentrated in Phase 1: the IndentFilter already manages a complex state machine (BracketDepth, InFunctionApp, InLetDecl offside, InExprBlock, InMatch, InTry), and adding SEMICOLON injection to that machine without breaking existing multi-line function application is the most dangerous change in the milestone. The critical guard is: do not emit SEMICOLON when the previous token can be a function and the next token is an atom — this distinguishes "f x / y" (application continuation) from "f x / let y = ..." (new statement). Phase 2 carries moderate risk in the type checker (extracting element types from collection types), and Phase 3 is the lowest-risk phase of the milestone.

The entire implementation touches a small number of files: IndentFilter.fs for sequencing; Ast.fs, Parser.fsy, Bidir.fs, and Eval.fs for for-in (plus passthrough in Elaborate/Format/Infer/Exhaustive); and Prelude/Option.fun and Prelude/Result.fun for utilities. F#'s exhaustive DU matching means any missed ForInExpr case is a compile-time error, not a runtime surprise.

## Key Findings

### Recommended Stack

The stack is unchanged from v5.0. F# on .NET 10, FsLexYacc 12.x for lexer/parser, IndentFilter for token-stream layout processing, and `.fun` Prelude files for the standard library. No new NuGet packages, no tooling changes, no version upgrades needed.

**Core technologies:**
- **IndentFilter.fs**: Layout rule engine — the sole target for newline sequencing work; emits INDENT/DEDENT and now also SEMICOLON in InExprBlock contexts
- **FsLexYacc (fslex/fsyacc)**: Existing LALR(1) parser — receives 2 new grammar rules for ForInExpr; no conflicts expected because FOR/LET are distinct shift states
- **SeqExpr nonterminal**: Already handles `e1; e2` — implicit sequencing injects SEMICOLON upstream with zero parser changes needed
- **Prelude/*.fun**: Standard library in LangThree itself — the only change surface for Option/Result utilities; loaded alphabetically by Prelude.fs

### Expected Features

**Must have (table stakes):**
- Newline-as-semicolon in function bodies, loop bodies, and lambda bodies — F# convention; expected by any ML-style developer
- `for x in list do body` and `for x in array do body` — collection iteration without `List.iter` boilerplate
- Loop variable immutability in for-in — consistent with existing `for i = s to e` semantics
- `optionIter`, `optionFilter`, `resultIter`, `resultToOption` — the four highest-priority missing Prelude functions
- Module-level declarations NOT affected by newline sequencing — critical correctness: SEMICOLON must not appear between top-level `let` bindings

**Should have (competitive):**
- Short-name Prelude aliases (`map`, `bind`, `defaultValue`) for ergonomic use after `open Option`
- `optionGet`, `optionOrElse`, `resultMapBoth`, `resultFold` — useful but not blocking
- Mixing explicit `;` and newline sequencing (both styles work simultaneously)
- `optionToList`, `optionOfBool`, `resultFromOption` — completeness

**Defer (v2+):**
- Tuple pattern destructuring in loop variable (`for (a, b) in pairs do`) — Medium complexity, not urgent
- `for x in seq do` — lazy sequence iteration (Seq type not in scope)
- Computation expressions for Option/Result (`option { ... }`) — requires computation expression infrastructure
- String iteration (`for c in "hello" do`) — not supported; document workaround instead

### Architecture Approach

All three features slot into the existing pipeline without disrupting it. The pipeline is unchanged: Source → Lexer.fsl → IndentFilter.fs → Parser.fsy → Elaborate.fs → Bidir.fs → Eval.fs. Newline sequencing is entirely a token-stream transformation in IndentFilter; the parser and evaluator are unaware of it. ForInExpr follows the exact same extension pattern as the existing ForExpr: new AST variant, two parser rules, one Bidir synth case, one Eval case. Option/Result utilities are implemented purely in `.fun` files, taking advantage of the fact that the existing HM type inference already handles polymorphic ADT functions correctly.

**Major components and their change scope:**

1. **IndentFilter.fs** — SEMICOLON injection at same-level NEWLINE inside InExprBlock; guarded by prevToken/nextToken checks and mutual exclusion with the offside IN rule
2. **Ast.fs + Parser.fsy + Bidir.fs + Eval.fs** — ForInExpr variant with two grammar rules (inline and indented body), element-type extraction from TList/TArray, and loop-body evaluation for both ListValue and ArrayValue
3. **Prelude/Option.fun + Prelude/Result.fun** — additive-only utility functions; load order already correct (O before R); no interpreter changes required

### Critical Pitfalls

1. **Newline sequencing breaks multi-line function application (V6-1)** — `f x` followed by `y` at the same indent is currently `(f x y)`. Implicit SEMICOLON would silently change it to `f x; y`. Prevent by checking `canBeFunction prevToken && isAtom nextToken` — if true, do NOT emit SEMICOLON. Run all existing flt function-application tests immediately after any IndentFilter change.

2. **Double-emission: offside IN and implicit SEMICOLON on same newline (V6-2)** — mutual exclusion required: if the offside rule fires (emits IN in InLetDecl context), SEMICOLON must not also be emitted. These contexts are mutually exclusive — newline sequencing fires only in InExprBlock, offside IN fires in InLetDecl.

3. **SEMICOLON emitted before structural terminators (V6-3)** — `ELSE`, `WITH`, `PIPE`, `IN`, `THEN`, `DEDENT`, `EOF` must be on a "do not emit SEMICOLON before this" blocklist. The IndentFilter already computes `nextToken`; add a terminator-token check before any SEMICOLON emission.

4. **Lines starting with infix operators are continuations, not statements (V6-7)** — `|> List.map f` on the next line is a pipe continuation, not a new statement. If the next token is a binary operator (`|>`, `>>`, `+`, `&&`, etc.), do not emit SEMICOLON.

5. **ForInExpr loop variable scoping in type checker (V6-5)** — extract element type from the collection's synthesized type (`TList elemTy` or `TArray elemTy`), bind loop variable as `Scheme([], elemTy)`, and do NOT add it to `mutableVars`. Forgetting the mutableVars exclusion allows `x <- newValue` inside the body, which is semantically wrong.

## Implications for Roadmap

Three phases are the natural implementation order, driven by technical dependency and risk profile.

### Phase 1: Newline Implicit Sequencing

**Rationale:** Every subsequent test — for for-in loops and Prelude utilities — will want to write multi-line bodies without explicit semicolons. Implementing sequencing first means all later tests are written in clean idiomatic style. It is also the highest-risk change and benefits from being isolated so the full regression suite can be run without interference from other changes.

**Delivers:** Newline-as-semicolon in all expression block contexts (function bodies, loop bodies, lambda bodies, let-RHS blocks). No parser or AST changes — purely an IndentFilter token-stream transformation.

**Files changed:** `src/LangThree/IndentFilter.fs` only (~15-25 lines of logic)

**Features addressed:** Multi-line function bodies, multi-line while/for-to bodies, pipe chains, all imperative code patterns without explicit `;`

**Pitfalls to avoid:** V6-1 (function app breakage via prevToken/nextToken guard), V6-2 (double-emission mutual exclusion with offside), V6-3 (terminator blocklist), V6-7 (operator-continuation rule)

**Research flag: NEEDS phase-specific research.** IndentFilter is the most complex existing component. The precise ordering and interaction of guards (prevToken check, nextToken check, offside mutual exclusion, terminator blocklist) must be specified exactly before coding. One wrong ordering causes silent semantic changes with no compile error and no parse error.

### Phase 2: For-In Collection Loops

**Rationale:** After newline sequencing is stable, the for-in feature can be implemented and tested with clean multi-line bodies. This is the most invasive change (AST + Parser + Bidir + Eval + passthrough files), but the pattern is well-established — it exactly mirrors how ForExpr was added in a prior phase.

**Delivers:** `for x in list do body` and `for x in array do body`. Returns unit. Loop variable immutable. Empty collection is a no-op (zero iterations).

**Files changed:** `Ast.fs`, `Parser.fsy`, `Bidir.fs`, `Eval.fs`, plus passthrough in `Elaborate.fs`, `Format.fs`, `Infer.fs`, `Exhaustive.fs` (each ~2-4 lines)

**Features addressed:** Collection iteration over lists and arrays, replacing `List.iter` boilerplate, ergonomic loop syntax consistent with F#

**Pitfalls to avoid:** V6-4 (LALR conflict — verify at build time with `dotnet build`), V6-5 (element type extraction from TList/TArray; exclude from mutableVars), V6-6 (handle both ListValue and ArrayValue; test empty collection), V6-10 (closure capture correct via Map.add immutable bind), V6-12 (clear error message for string iteration)

**Research flag:** Standard patterns — ForExpr is the direct template. Verify LALR conflict at build time. The type checker's TList vs TArray handling requires a design decision (MVP: constrain to TList only; arrays work at eval time regardless of whether the type checker accepts TArray).

### Phase 3: Option/Result Prelude Utilities

**Rationale:** Pure `.fun` additions with zero interpreter changes. The lowest-risk phase. Placing it last means tests can use the clean multi-line style from Phase 1 and can be validated in programs that also use for-in loops.

**Delivers:** `optionIter`, `optionFilter`, `optionGet`, `optionOrElse`, `optionToList`, `optionOfBool` (Option module); `resultIter`, `resultToOption`, `resultFromOption`, `resultMapBoth`, `resultFold` (Result module). Plus short-name aliases (`map`, `bind`, `defaultValue`) for use after `open Option` / `open Result`.

**Files changed:** `Prelude/Option.fun`, `Prelude/Result.fun` only (~14-20 lines total)

**Features addressed:** Side effects on optional values, conditional filtering, Result-to-Option bridging, complete parity with F# Option/Result modules for common operations

**Pitfalls to avoid:** V6-8 (naming: follow `optionX` / `resultX` prefix convention, not bare `map`/`bind` at top level), V6-9 (verify ADT constructors enter ConstructorEnv when Prelude loads), V6-11 (define in .fun files for inferred polymorphism, not as Eval.fs builtins), V6-14 (snake_case naming consistent with existing builtins), V6-15 (test monad laws — specifically `Error e` unchanged through `result_bind`)

**Research flag:** Standard patterns — Array.fun and Hashtable.fun are the naming/structure templates. No additional research phase needed.

### Phase Ordering Rationale

- **Sequencing before loops:** For-in tests need multi-line bodies; without sequencing those bodies require explicit `;` everywhere, making tests awkward to write and non-representative
- **Sequencing before Prelude:** `optionIter` called on separate lines in a do-block only works with sequencing; tests read better with implicit sequencing in place
- **Loops before Prelude:** Prelude additions are independent and could be done in any order, but keeping Phase 2 diff focused on interpreter files is cleaner when Phase 3 is deferred
- **F# exhaustive DU matching:** Every missed ForInExpr case in propagation is a compile-time error — Phase 2 propagation to all files is caught at build time, not at test time

### Research Flags

**Phases needing deeper research during planning:**
- **Phase 1 (Newline Sequencing):** The IndentFilter state machine has the highest complexity in the codebase. The exact condition for SEMICOLON emission — specifically the ordering and interaction of prevToken, nextToken, context check, offside mutual exclusion, and BracketDepth — requires a precise written specification before coding. One incorrect guard ordering produces silent semantic breakage that only appears at runtime with wrong values, not with parse errors.

**Phases with standard patterns (skip research-phase):**
- **Phase 2 (For-In Loop):** ForExpr is the direct template. Grammar, type-checker, and evaluator patterns are established. Verify LALR conflict at first build.
- **Phase 3 (Prelude Utilities):** Array.fun and Hashtable.fun are the naming and structure templates. Purely additive, no interpreter involvement.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | No changes to stack; all existing tools verified at v5.0 |
| Features | HIGH | All three features have direct F# standard library precedents; scope is well-bounded |
| Architecture | HIGH | ForExpr and IndentFilter patterns are concrete existing code; integration points identified from source inspection |
| Pitfalls | HIGH | Based on direct inspection of IndentFilter.fs, Bidir.fs, Eval.fs source; not theoretical; prior phase analysis (45-RESEARCH.md) already resolved earlier pitfalls |

**Overall confidence:** HIGH

### Gaps to Address

- **TList vs TArray in Bidir.fs for ForInExpr:** MVP recommendation is constrain type checker to `TList elemTy` only (ranges also evaluate to ListValue). ArrayValue works at eval time regardless. If the type checker should also accept `for x in array do`, a second unification attempt against `TArray elemTy` is needed. This design decision should be made explicit during Phase 2 — do not leave it implicit.

- **REPL behavior under newline sequencing (V6-13):** If the REPL uses the same IndentFilter pipeline as file parsing, a mode flag may be needed to suppress implicit SEMICOLON in interactive mode. The recommended approach (REPL requires explicit `;;` or `;`; file mode uses implicit sequencing) mirrors OCaml REPL convention. Verify against `Repl.fs` implementation before releasing Phase 1.

- **Short-name alias coexistence:** Both long-name (`optionMap`) and short-name (`map`) aliases can coexist in Option.fun. The safe choice is additive short-name aliases only, keeping full backward compatibility for all existing tests. This must be confirmed as the policy before Phase 3 implementation.

## Sources

### Primary (HIGH confidence)
- Direct inspection of `IndentFilter.fs` — InExprBlock, BracketDepth, canBeFunction, isAtom, processNewlineWithContext
- Direct inspection of `Parser.fsy` — SeqExpr, ForExpr grammar rules, IN token usage
- Direct inspection of `Bidir.fs` — ForExpr type checker, mutableVars exclusion pattern
- Direct inspection of `Eval.fs` — ForExpr evaluator, ListValue/ArrayValue handling
- Direct inspection of `Ast.fs` — Expr DU, ForExpr/ForInExpr candidate patterns
- Project history via `PROJECT.md` — Key Decisions table (closure capture, mutableVars, SeqExpr)
- Phase 45 research (`45-RESEARCH.md`) — SeqExpr and offside interaction analysis

### Secondary (MEDIUM confidence)
- [F# for...in Expression — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-for-in-expression)
- [F# Option Module — FSharp.Core docs](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-optionmodule.html)
- [F# Result Module — FSharp.Core docs](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
- [F# syntax: indentation and verbosity — F# for fun and profit](https://fsharpforfunandprofit.com/posts/fsharp-syntax/)
- [OCaml Mutability and Imperative Control Flow](https://ocaml.org/docs/mutability-imperative-control-flow)

---
*Research completed: 2026-03-28*
*Ready for roadmap: yes*
