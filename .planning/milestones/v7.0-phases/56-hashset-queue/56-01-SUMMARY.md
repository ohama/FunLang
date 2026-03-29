---
phase: 56-hashset-queue
plan: 01
subsystem: interpreter
tags: [fsharp, hashset, queue, mutable, method-dispatch, collections]

# Dependency graph
requires:
  - phase: 55-plan-03
    provides: StringBuilderValue pattern for Value DU cases with CustomEquality/CustomComparison, constructor interception, FieldAccess dispatch in Eval.fs and Bidir.fs
provides:
  - HashSetValue DU case in Ast.fs with CustomEquality/CustomComparison support (identity semantics)
  - QueueValue DU case in Ast.fs with CustomEquality/CustomComparison support (identity semantics)
  - Constructor interception for "HashSet" and "Queue" in Eval.fs and Bidir.fs
  - FieldAccess dispatch for Add/Contains/Count on HashSetValue in Eval.fs
  - FieldAccess dispatch for Enqueue/Dequeue/Count on QueueValue in Eval.fs
  - Type synthesis TData("HashSet",[]) and TData("Queue",[]) in Bidir.fs
  - FieldAccess type rules for all HashSet/Queue methods in Bidir.fs
  - formatValue cases for both new types
affects: [56-02 (flt tests), 57-hashtable, any future phase using collections]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "HashSetValue/QueueValue use System.Object.ReferenceEquals for equality (identity semantics like HashtableValue/StringBuilderValue)"
    - "Constructor interception in Eval.fs/Bidir.fs: match name, argOpt with | 'HashSet'/'Queue', _ -> ... before | _ -> DataValue fallthrough"
    - "FieldAccess dispatch: HashSetValue/QueueValue arms inserted after StringBuilderValue arm, before RecordValue in Eval.fs"
    - "Bidir.fs TData('HashSet',[])/TData('Queue',[]) arms before general TData arm in FieldAccess match"
    - "HashSet.Add returns BoolValue directly (hs.Add returns bool in .NET); no BuiltinValue wrapping for Count property"
    - "Queue.Dequeue is a zero-arg method: takes TupleValue [] arg, raises LangThreeException on empty queue"

key-files:
  created: []
  modified:
    - src/LangThree/Ast.fs
    - src/LangThree/Eval.fs
    - src/LangThree/Bidir.fs

decisions:
  - id: HS-01-equality
    choice: "ReferenceEquals for HashSetValue and QueueValue (identity semantics)"
    rationale: "Mutable collections: two values are equal only if they are the same object, consistent with HashtableValue and StringBuilderValue patterns"
  - id: HS-02-hashset-add
    choice: "hs.Add(arg) returns BoolValue(hs.Add(arg)) directly from .NET HashSet.Add return value"
    rationale: ".NET HashSet<T>.Add returns bool (true if new, false if duplicate); directly maps to LangThree BoolValue"
  - id: HS-03-queue-dequeue
    choice: "Queue.Dequeue takes TupleValue [] (zero-arg method pattern); raises LangThreeException on empty"
    rationale: "Consistent with ToString() pattern for zero-arg methods; LangThreeException is catchable by try-with"
  - id: HS-04-count-property
    choice: "Count returns IntValue directly (not BuiltinValue) since it is a property, not a method"
    rationale: "Properties have no argument; FieldAccess returns the value directly without wrapping in BuiltinValue"

metrics:
  duration: ~15 minutes
  completed: 2026-03-29
---

# Phase 56 Plan 01: HashSet and Queue Core Types Summary

**One-liner:** HashSetValue and QueueValue DU cases with Constructor interception, FieldAccess method/property dispatch (Add/Contains/Count and Enqueue/Dequeue/Count), and full type-checker support.

## What Was Built

Added `HashSet` and `Queue` as first-class mutable collection types to LangThree. `HashSet()` creates an empty unique-element set, `.Add(v)` returns bool (true if new element, false if duplicate), `.Contains(v)` tests membership, and `.Count` returns the element count. `Queue()` creates an empty FIFO queue, `.Enqueue(v)` adds to the back (returns unit), `.Dequeue()` removes and returns the front element (raises a catchable exception on empty queue), and `.Count` returns element count. The type checker synthesizes `TData("HashSet",[])` and `TData("Queue",[])` for constructors and provides typed field access rules for all methods.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add HashSetValue and QueueValue DU cases to Ast.fs | 9527871 | src/LangThree/Ast.fs, src/LangThree/Eval.fs |
| 2 | Constructor interception and FieldAccess dispatch in Eval.fs and Bidir.fs | 90f5081 | src/LangThree/Eval.fs, src/LangThree/Bidir.fs |

## Verification Results

- Build: `dotnet build` succeeded with 0 errors, 0 warnings
- HashSet smoke test: `HashSet()` evaluates to HashSetValue, `.Add(1)` returns true (first insert), `.Add(1)` returns false (duplicate), `.Contains(1)` returns true, `.Count` returns 1
- Queue smoke test: `Queue()` evaluates to QueueValue, `.Enqueue(42)` returns (), `.Dequeue()` returns 42, `.Count` returns 0
- Empty dequeue: raises LangThreeException with message "Queue.Dequeue: queue is empty", catchable via try-with

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Equality | ReferenceEquals (identity) | Mutable collections; consistent with HashtableValue/StringBuilderValue |
| HashSet.Add return | BoolValue from .NET HashSet.Add bool | .NET HashSet.Add returns true for new, false for duplicate |
| Queue.Dequeue arg | TupleValue [] (zero-arg method) | Consistent with other zero-arg methods (e.g., ToString) |
| Count access | IntValue directly (no BuiltinValue) | Property, not method; no argument needed |
| Empty dequeue error | LangThreeException(StringValue msg) | Catchable by user code via try-with |

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

- HashSet and Queue are fully functional with correct semantics
- Phase 56 Plan 02 (flt integration tests) can proceed immediately
- Both types follow the established Value-type method dispatch pattern
