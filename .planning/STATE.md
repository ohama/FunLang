# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-29)

**Core value:** 실용적인 함수형 프로그래밍 언어 -- 인터프리터와 네이티브 컴파일러 모두에서 동일하게 동작
**Current focus:** v7.1 Remove Dot Notation

## Current Position

Milestone: v7.1 Remove Dot Notation — IN PROGRESS
Phase: 60 of 62 (Builtins & Prelude Modules) — COMPLETE
Plan: 02 of 02 complete
Status: Phase complete
Last activity: 2026-03-29 — Completed 60-02-PLAN.md (Prelude module wrappers + flt tests)

Progress: [████████████████████] v1.0-v7.0 done (59p/126pl) | v7.1: [██░░░░░░░░] 20%

## Performance Metrics

**Velocity:**
- Total plans completed: 126
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

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-03-29
Stopped at: Completed 60-02-PLAN.md (Phase 60 complete)
Resume file: None
Next action: Execute Phase 61

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-29 (60-02 complete: Prelude modules + flt tests)*
