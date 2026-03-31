# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-31)

**Core value:** 현대적인 타입 시스템(ADT, GADT, Records)과 F# 스타일 문법을 갖춘 실용 함수형 언어
**Current focus:** v10.0 Type Classes — COMPLETE

## Current Position

Milestone: v10.0 Type Classes — COMPLETE
Phase: 74 of 74 (Built-in Instances and Tests) — Complete
Plan: 2 of 2 complete
Status: Milestone v10.0 complete
Last activity: 2026-03-31 — Completed 74-02-PLAN.md (5 built-in Show/Eq flt tests; 676/676 passing)

Progress: [████████████████████] v1.0-v9.1 done (69 phases, 150 plans)
         [████████████████████] v10.0: 100% complete (phases 70+71+72+73+74 all done)

## Performance Metrics

**Velocity:**
- Total plans completed: 150
- v9.1: 1 plan (phase 69) in 1 day
- v9.0: 7 plans across 2 phases in 1 day
- v8.1: 4 plans (phases 65-66) in 1 day
- v8.0: 5 plans across 2 phases in 1 day
- v7.1: 7 plans across 3 phases in 1 day

## Accumulated Context

### Decisions

(Full log in PROJECT.md Key Decisions table)

Key context carried into v10.0:
- Dictionary passing strategy chosen: constraints elaborated to explicit RecordValue dict args; evaluator stays type-class-unaware
- `ClassEnv`/`InstanceEnv` threaded as module-level mutable refs in `Bidir.fs` (same pattern as `mutableVars`) to avoid call-site explosion
- `Scheme` shape change to `Scheme(vars, constraints, ty)` is most invasive single change — do it first so F# exhaustive matching flags all incomplete sites immediately
- Constraint resolution deferred past unification (never resolve `TVar 1042` before it unifies) to avoid false "no instance" errors

From Phase 70 Plan 01:
- `InstanceInfo` defined WITHOUT `MethodBodies` for Phase 70 (avoids circular dep with Expr); Phase 71 adds bodies
- Tasks 1+2 committed together (T1 alone won't compile due to F# exhaustive matching)
- `mkScheme`/`schemeType` helpers added as zero-cost backward-compat for Phase 71+ gradual migration

From Phase 70 Plan 02:
- `typeCheckModuleWithPrelude` now accepts `preludeClassEnv: ClassEnv` and `preludeInstEnv: InstanceEnv` (same threading pattern as CtorEnv/RecEnv)
- `PreludeResult` has `ClassEnv`/`InstEnv` fields; `loadPrelude` accumulates them per-file
- `loadAndTypeCheckFileImpl` (file import handler) passes `Map.empty` for both — file imports don't declare typeclasses yet
- Test helpers in GadtTests.fs and ModuleTests.fs updated alongside (auto-fixed blocking deviation)

### Pending Todos

None.

From Phase 71 Plan 01:
- FATARROW (`=>`) token placed after LARROW (`<-`) in Lexer.fsl — before the `op_char op_char+` catch-all, no conflict with GE (`>=`)
- ConstraintList grammar uses bare `IDENT TYPE_VAR` (not parenthesized); FATARROW lookahead disambiguates from AtomicType in LALR(1) — no conflicts
- Tasks 1+2 committed together — new Decl variants make TypeCheck/Eval pattern matches non-exhaustive if committed separately
- formatToken in Format.fs must always cover all Parser.token variants (new tokens need entries there too)

From Phase 71 Plan 02:
- TypeClassMethod syntax uses leading PIPE: `| methodName : type` (not `methodName : type`) — IDENT after TypeExpr shifts as type application (163 silent LALR conflicts), PIPE is unambiguous
- TYPECLASS/INSTANCE bodies get InModule context in IndentFilter (not InExprBlock) — prevents spurious IN injection for instance method LET and SEMICOLON between typeclass methods
- flt integration tests: one test case per file (5 files created in tests/flt/file/typeclass/)

From Phase 72 Plan 01:
- TEConstrained in elaborateWithVars/substTypeExprWithMap recurses into inner type only — constraints are handled at Scheme construction, not Type elaboration level
- typeCheckDecls now threads ClassEnv*InstanceEnv as 7-tuple fold; all 12 existing arms pass through unchanged
- currentClassEnv/currentInstEnv mutable refs in TypeCheck.fs initialized from preludeClassEnv/preludeInstEnv; updated incrementally by TypeClassDecl/InstanceDecl arms
- InstanceDecl method body type-checking uses outer module-level env (not method-local env); each body checked against class scheme instantiated with concrete instance type via Map.ofList[classTypeVar,instType]
- InstanceDecl does NOT propagate inner classEnv/instEnv from module/namespace recursion back to outer scope — typeclass decls in nested modules don't leak

From Phase 72 Plan 02:
- instantiate/generalize defined in Bidir.fs shadowing Infer.fs versions — avoids circular dep (Infer cannot reference Bidir); any module open Bidir after Infer uses constraint-aware versions
- currentClassEnv/currentInstEnv moved from TypeCheck.fs to Bidir.fs; TypeCheck.fs sets them via Bidir refs
- applySubstToConstraints called at EVERY generalize call site before generalize — resolves TVar refs from unification before constraint partitioning
- Partition logic: constraints mentioning generalized TVars are deferred into Scheme; concrete constraints resolve against InstanceEnv (NoInstance raised on failure)
- Verified: `show_twice : Show 'a => 'a -> string`; `show_twice 42` type-checks; `show_twice (fun x -> x)` produces NoInstance error

### Blockers/Concerns

- [Phase 71] `where` keyword audit RESOLVED — `where` not used anywhere in Lexer.fsl, no conflict
- [Phase 72] currentClassEnv/currentInstEnv mutable ref pattern: RESOLVED in Plan 02 — refs now live in Bidir.fs
- [Phase 72] GADT branch constraint isolation (LT-2): branch-local type refinements must be applied to constraints before they escape the branch (Phase 72 Plan 03 scope) — DEFERRED to Phase 73+ if needed

From Phase 72 Plan 03:
- flt tests use --check mode (not runtime) because Phase 73 dictionary elaboration not yet implemented; runtime calls to typeclass methods would be "unbound variable: show"
- FsLit Stderr: section does partial matching -- only first error line needed; path-containing --> line varies by temp file path and is excluded
- ++ is list append in LangThree; use + for string concatenation when types are known

From Phase 73 Plan 01:
- elaborateTypeclasses: List.collect flatmap over Decl list; TypeClassDecl -> [], InstanceDecl -> [LetDecl per method], recurses into ModuleDecl/NamespaceDecl
- Program.fs: elaboratedDecls inserted before both Eval.evalModuleDecls call sites (Test subcommand + file execution)
- Last-binding lookup in file execution uses elaboratedDecls (not moduleDecls) — necessary so TypeClassDecl removal doesn't cause missed output
- Prelude.fs call sites NOT updated: prelude files contain no InstanceDecl
- Method names bound directly without mangling (last-wins shadowing for multiple instances; MVP acceptable)

From Phase 73 Plan 02:
- Runtime flt test pattern: typeclass + instance + let result = <expr>, no --check flag, Stdout matches formatValue output
- StringValue prints with surrounding quotes (e.g., show 42 -> "42"); ListValue of StringValues prints as ["1"; "2"; "3"]
- show is a first-class value passable to List.map (higher-order use confirmed at runtime)
- 671 flt tests total after Phase 73 (was 668 after Phase 73-01)

From Phase 74 Plan 01:
- Prelude/Typeclass.fun: Show and Eq classes + instances for int/bool/string/char; loads alphabetically after StringBuilder.fun
- Prelude.fs: loadPrelude and loadAndEvalFileImpl both call Elaborate.elaborateTypeclasses before Eval.evalModuleDecls
- TypeCheck: TypeClassDecl redeclaration is idempotent — if class already in classEnv (from prelude), skip silently; duplicate instances still raise E0702
- Polymorphic instances (list 'a, Option 'a) NOT supported: instance resolution uses exact type equality; TList(TVar 0) != TList TInt; needs unification-based lookup (deferred)
- show 'x' returns "'x'" (with single quotes) — to_string delegates to formatValue for CharValue
- 671 tests still pass after updating 3 typeclass inference tests to use prelude Show

From Phase 74 Plan 02:
- 5 new flt integration tests lock in built-in Show/Eq for primitives; 676/676 tests pass
- Runtime built-in test pattern: no typeclass/instance headers in flt file; just use show/eq directly
- Polymorphic Show instances (Show list, Show option) confirmed out-of-scope for v10.0; need unification-based instance resolution in future milestone

## Session Continuity

Last session: 2026-03-31
Stopped at: Completed 74-02-PLAN.md (5 built-in Show/Eq flt tests; milestone v10.0 complete)
Resume file: None
Next action: Begin next milestone planning

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-31 (phase 74 plan 02 — built-in instances tests; 676/676 tests passing; v10.0 COMPLETE)*
