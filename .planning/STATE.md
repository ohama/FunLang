# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-29)

**Core value:** 실용적인 함수형 프로그래밍 언어 -- 인터프리터와 네이티브 컴파일러 모두에서 동일하게 동작
**Current focus:** v7.0 Native Collections & Built-in Library

## Current Position

Milestone: v7.0 Native Collections & Built-in Library
Phase: 58 - Language Constructs ✓ COMPLETE
Plan: 3/3 complete
Status: Phase 58 verified, Phase 59 next
Last activity: 2026-03-29 -- Phase 58 complete (5/5 must-haves, 614 flt tests)

Progress: [████████████████░░░░] 83% -- Phases 54-58 done, Phase 59 next

## Performance Metrics

**Velocity:**
- Total plans completed: 112
- v1.0-v2.2: 92 plans across 37 phases
- v3.0: 6 plans across 4 phases in 1 day
- v4.0: 5 plans across 3 phases in 1 day
- v5.0: 5 plans across 5 phases in 1 day
- v6.0: 5 plans across 4 phases in 2 days
- v7.0: 1 plan across 1/6 phases (Phase 54 complete)

## Accumulated Context

### Decisions

(Full log in PROJECT.md Key Decisions table)

Key cross-milestone context:
- flt runner strips trailing newline from extracted input -- last input line must be a complete parseable top-level declaration
- while loops require `let _ = ...` wrapper at module level -- not a top-level declaration
- String concatenation in LangThree is `^^` (not `^`)
- [|...|] array literals not supported (use Array.ofList)
- FieldAccess now dispatches on value types (Phase 54): TString/TArray in Bidir.fs, StringValue/ArrayValue in Eval.fs
- .Contains returns BuiltinValue so App(FieldAccess(...), arg) dispatch works via existing applyFunc
- Value-type dispatch in FieldAccess must come BEFORE RecordValue match in Eval.fs | _ -> branch
- .Trim() must return BuiltinValue(TupleValue [] -> ...) NOT StringValue directly (App parse requires function in position)
- FsLit only supports // --- Output: not // --- Stdout: for stdout sections; Stdout: silently ignored
- eprintfn mirrors applyPrintfnArgs but writes to stderr; type scheme Scheme([0], TArrow(TString, TVar 0))
- IndexGet/IndexSet AST nodes exist for arr.[i] syntax -- Phase 58 extends for string slicing
- Char module in Prelude/Char.fun: IsDigit, ToUpper, IsLetter, IsUpper, IsLower, ToLower (qualified-only)
- String.concat in Prelude/String.fun wraps string_concat_list (NOT string_concat) to avoid type collision
- to_string on CharValue produces quoted output 'A' not A (formatValue behavior)
- HashtableValue wraps Dictionary<Value,Value> -- Phase 57-02 added .TryGetValue, .Count, .Keys via FieldAccess; THashtable(keyTy,valTy) arm in Bidir.fs (NOT TData)
- MutableListValue wraps System.Collections.Generic.List<Value> (Phase 57-01): Constructor("MutableList") intercepted; Add/Count via FieldAccess; IndexGet/IndexSet bounds-checked; raw builtins mutablelist_* in Eval.fs/TypeCheck.fs; Prelude/MutableList.fun uses raw builtins (same pattern as HashSet/Queue)
- StringSliceExpr AST node: str * start * stop option * span -- Phase 58 s.[start..stop] and s.[start..] syntax
- ListCompExpr AST node: var * collection * body * span -- Phase 58 [for x in coll -> body] syntax; range desugared to Range(...) in parser
- callValueRef forward reference pattern used for builtins that invoke user closures
- StringBuilderValue wraps System.Text.StringBuilder (Phase 55-03): Constructor("StringBuilder") intercepted in Eval.fs/Bidir.fs
- sb.Append/ToString dispatched via FieldAccess StringBuilderValue arm in Eval.fs; TData("StringBuilder",[]) arm in Bidir.fs
- HashSetValue wraps System.Collections.Generic.HashSet<Value> (Phase 56-01): Constructor("HashSet") intercepted; Add/Contains/Count dispatch
- QueueValue wraps System.Collections.Generic.Queue<Value> (Phase 56-01): Constructor("Queue") intercepted; Enqueue/Dequeue/Count dispatch; empty Dequeue raises LangThreeException
- AppExpr DOT IDENT grammar rule added to Parser.fsy for method chaining; inline sb.Append("a").Append("b") still has LALR conflict, use intermediate bindings
- Prelude/StringBuilder.fun: uses stringbuilder_create/append/tostring builtins (not method dispatch -- TVar FieldAccess not supported)
- Prelude/HashSet.fun and Prelude/Queue.fun: same pattern -- use raw builtins hashset_*/queue_* not dot-dispatch (TVar FieldAccess not supported)
- Unit pattern () in module let binding params (let f x () = ...) does not parse in .fun files -- use named param instead
- flt tests: expected output must include () for final let _ = println expr

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-03-29
Stopped at: Phase 58 Plan 03 complete -- 6 flt integration tests, 614/614 suite green
Resume file: None
Next action: None -- Phase 58 complete, v7.0 language constructs milestone done

---
*State initialized: 2026-02-25*
*Last updated: 2026-03-29 (Phase 54 Plan 01 complete)*
