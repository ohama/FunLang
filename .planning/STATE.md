# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-29)

**Core value:** 실용적인 함수형 프로그래밍 언어 -- 인터프리터와 네이티브 컴파일러 모두에서 동일하게 동작
**Current focus:** v7.1 Remove Dot Notation

## Current Position

Milestone: v7.1 Remove Dot Notation — DEFINING
Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-03-29 — Milestone v7.1 started

Progress: [████████████████████] v1.0-v7.0 done (59p/126pl)

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
- Builtins in Eval.fs also need type schemes in TypeCheck.fs initialTypeEnv or .fun files using them will get type errors
- FsLit only supports // --- Output: not // --- Stdout: for stdout sections; Stdout: silently ignored
- flt tests: expected output must include () for final let _ = println expr
- Multi-arg lambdas (fun i x -> ...) fail to parse -- use curried form (fun i -> fun x -> ...)
- mod is not a LangThree keyword -- use % for integer modulo
- Unit pattern () in module let binding params (let f x () = ...) does not parse in .fun files -- use named param instead
- StringBuilder.append Prelude function conflicts with List.append due to `open List` scope pollution — discovered during v7.1 analysis
- to_string on CharValue produces quoted output 'A' not A (formatValue behavior)

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-03-29
Stopped at: Milestone v7.1 initialization
Resume file: None
Next action: Define requirements and roadmap

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-29 (v7.1 milestone start)*
