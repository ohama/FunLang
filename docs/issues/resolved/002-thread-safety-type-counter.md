# Issue #002: Infinite Loop in Parallel Test Execution

**Date**: 2026-01-10
**Status**: Resolved
**Component**: Types.fs (TypeHelpers.counter)

## Problem

When running all tests with Expecto's default parallel execution, tests would hang indefinitely:

```bash
$ dotnet run --project tests/FunLang.Tests
[22:27:20 INF] EXPECTO? Running tests...
# ... hangs forever
```

However, individual test groups passed when run separately:
```bash
$ dotnet run --project tests/FunLang.Tests -- --filter "Type System"
# 48 tests passed

$ dotnet run --project tests/FunLang.Tests -- --sequenced
# 254 tests passed (sequential execution)
```

## Root Cause

The `TypeHelpers` module used a shared mutable counter for generating fresh type variables:

```fsharp
module TypeHelpers =
    let mutable private counter = 0

    let freshTypeVar () : Type =
        counter <- counter + 1
        TVar counter

    let resetCounter () =
        counter <- 0
```

When tests ran in parallel:
1. Multiple threads called `resetCounter()` simultaneously
2. One test's `resetCounter()` affected other tests' type variable generation
3. This caused non-deterministic behavior and potential infinite loops in unification

## Solution

Changed to thread-local storage so each test thread has its own counter:

```fsharp
module TypeHelpers =
    let private counter = new System.Threading.ThreadLocal<int>(fun () -> 0)

    let freshTypeVar () : Type =
        counter.Value <- counter.Value + 1
        TVar counter.Value

    let resetCounter () =
        counter.Value <- 0

    let getCounter () = counter.Value
```

## Files Changed

- `src/FunLang/Types.fs`: Lines 96-107 (TypeHelpers module)

## Verification

All 254 tests now pass with parallel execution:
```bash
$ dotnet run --project tests/FunLang.Tests
EXPECTO! 254 tests run in 00:00:00.2024968 – 254 passed, 1 ignored, 0 failed
```

## Notes

- `ThreadLocal<T>` ensures each thread gets its own counter initialized to 0
- No performance impact since type inference typically runs on a single thread per expression
- Property tests (1 ignored) are disabled for separate reasons (FsCheck generator issues)
