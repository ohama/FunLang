# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-29)

**Core value:** 실용적인 함수형 프로그래밍 언어 -- 인터프리터와 네이티브 컴파일러 모두에서 동일하게 동작
**Current focus:** v8.0 Phase 63 — Angle Bracket Generics

## Current Position

Milestone: v8.0 Declaration Type Annotations
Phase: 63 of 64 (Angle Bracket Generics)
Plan: 1 of 2 complete
Status: In progress
Last activity: 2026-03-30 — Completed 63-01-PLAN.md (Parser angle bracket grammar)

Progress: [██░░░░░░░░░░░░░░░░░░] 10% (1 plan complete, ~9 total plans estimated in v8.0)

## Performance Metrics

**Velocity:**
- Total plans completed: 133
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

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-03-30
Stopped at: Completed 63-01-PLAN.md — angle bracket parser grammar
Resume file: None
Next action: Execute 63-02 (type annotation syntax)

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-30 (v8.0 roadmap created)*
