# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-30)

**Core value:** 실용적인 함수형 프로그래밍 언어 -- 인터프리터와 네이티브 컴파일러 모두에서 동일하게 동작
**Current focus:** v8.1 Mutual Recursion Completion

## Current Position

Milestone: v8.1 Mutual Recursion Completion
Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-03-31 — Milestone v8.1 started

Progress: [████████████████████] v1.0-v8.0 done (64 phases, 138 plans)

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
- v8.0: LT/GT tokens reused for angle bracket generics — LALR(1) disambiguates by parser state
- v8.0: MixedParamList subsumes ParamList — remove old ParamList productions to resolve reduce/reduce conflicts
- v8.0: Return type annotation wraps body in Annot(body, typeExpr, span) — erased at runtime

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-03-31
Stopped at: Defining v8.1 requirements
Resume file: None
Next action: Define requirements → create roadmap

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-31 (v8.1 milestone started)*
