# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-29)

**Core value:** 실용적인 함수형 프로그래밍 언어 -- 인터프리터와 네이티브 컴파일러 모두에서 동일하게 동작
**Current focus:** v8.0 MILESTONE COMPLETE

## Current Position

Milestone: v8.0 Declaration Type Annotations — COMPLETE
Phase: 64 of 64 (Declaration Type Annotations) — COMPLETE
Plan: 3 of 3 complete
Status: All phases complete, all requirements verified
Last activity: 2026-03-30 — Phase 64 verified, milestone v8.0 complete

Progress: [████████████████████] 100% (5 plans complete across 2 phases)

## Performance Metrics

**Velocity:**
- Total plans completed: 138
- v8.0: 5 plans across 2 phases in 1 day
- v7.1: 7 plans across 3 phases in 1 day
- v1.0-v2.2: 92 plans across 37 phases
- v3.0: 6 plans across 4 phases in 1 day
- v4.0: 5 plans across 3 phases in 1 day
- v5.0: 5 plans across 5 phases in 1 day
- v6.0: 5 plans across 4 phases in 2 days
- v7.0: 14 plans across 6 phases in 1 day

## Accumulated Context

### Decisions

(Full log in PROJECT.md Key Decisions table)

Key cross-milestone context (v8.0 additions):
- Angle bracket generics reuse LT/GT tokens — LALR(1) disambiguates by parser state (no new tokens)
- AngleBracketTypeParams (comma-separated: 'a, 'b) is distinct from TypeParams (space-separated: 'a 'b) — use AngleBracketTypeParams inside < > in declarations
- TypeArgList (comma-separated TypeExpr) is for type expression positions like Map<string, int>
- Angle bracket type annotations work in lambda params: fun (x : Box<int>) -> ...; mixed postfix composes: Box<int> list
- Module-level let binding type annotations (let x : T = ...) do NOT parse — not yet implemented (out of scope for v8.0 phase 63)

Key cross-milestone context:
- flt runner strips trailing newline from extracted input -- last input line must be a complete parseable top-level declaration
- while loops require `let _ = ...` wrapper at module level -- not a top-level declaration
- String concatenation in LangThree is `^^` (not `^`)
- [|...|] array literals not supported (use Array.ofList)
- Blank lines inside .fun module bodies cause parse errors (NEWLINE(0) = DEDENT out of module) -- never use blank lines in Prelude .fun files
- Option.fun renamed to A_Option.fun so Some/None are available when List.fun type-checks (A_ prefix sorts first in Prelude load order)
- Builtins in Eval.fs also need type schemes in TypeCheck.ts initialTypeEnv or .fun files using them will get type errors
- FsLit only supports // --- Output: not // --- Stdout: for stdout sections; Stdout: silently ignored
- flt tests: expected output must include () for final let _ = println expr
- Multi-arg lambdas (fun i x -> ...) fail to parse -- use curried form (fun i -> fun x -> ...)
- mod is not a LangThree keyword -- use % for integer modulo
- Unit pattern () in module let binding params (let f x () = ...) does not parse in .fun files -- use named param instead
- StringBuilder.append renamed to StringBuilder.add to avoid List.append scope conflict — implemented in 60-02
- to_string on CharValue produces quoted output 'A' not A (formatValue behavior)
- Module export builder in TypeCheck.fs filtered out shadow bindings — fixed: now includes binding when type differs from outer env (v <> outerV)
- Module value env in Eval.fs filtered out shadow closures — fixed: uses obj.ReferenceEquals to detect new closures vs inherited
- Module functions CAN shadow globally open'd names (e.g. String.length coexists with List.length) after the TypeCheck+Eval fix
- ForInExpr var is now Pattern (not string) — for-in supports tuple destructuring `for (k, v) in ht do ...`
- Hashtable for-in iteration yields TupleValue [k; v] (not RecordValue KeyValuePair)
- Hashtable module API: Hashtable.tryGetValue/count/keys — no dot-notation anywhere in flt tests
- v7.1 complete: FieldAccess in Eval.fs and Bidir.fs now only handles record access + module qualified access — all value-type dot dispatch removed
- v8.0: LambdaAnnot and AnnotParam already exist in AST/Parser for fun (x : T) -> lambdas; reuse for let declarations
- v8.0 Phase 64-01: MixedParamList subsumes ParamList — remove old ParamList productions to resolve reduce/reduce conflicts when both exist for same IDENT lookahead
- v8.0 Phase 64-01: LetRecDecl first-param extraction must match both Lambda and LambdaAnnot branches when using MixedParamList
- v8.0 Phase 64-02: Return type annotation wraps body in Annot(body, typeExpr, span) — erased at runtime, used for type checking only
- v8.0 Phase 64-02: Module-level `let x : T = ...` now parses (was out of scope for phase 63, implemented in 64-02)

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-03-30
Stopped at: Completed 64-02-PLAN.md — return type annotation parsing for all let-forms
Resume file: None
Next action: Execute Phase 64 Plan 03 (type checking of annotated let declarations)

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-30 (phase 64 plan 02 complete — return type annotation parsing)*
