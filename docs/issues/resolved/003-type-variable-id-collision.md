# issue-003: Infinite loop in type inference due to type variable ID collision

- **Status**: resolved
- **Priority**: high
- **Context**: src/FunLang/TypeInfer.fs, src/FunLang/TypeDefEnvBuilder.fs
- **Created**: 2026-01-10 23:44
- **Resolved**: 2026-01-10 23:51
- **Session Created**: j0123456-0123-9012-3456-789012345678
- **Session Resolved**: j0123456-0123-9012-3456-789012345678

## Description

Type inference entered an infinite loop when processing user-defined types with type parameters. The issue occurred because:

1. `TypeDefEnvBuilder` created type schemes with type variable IDs (1, 2, 3, ...)
2. `TypeHelpers.freshTypeVar()` also generated IDs starting from 1
3. When `apply` function encountered a type variable, it could substitute it with a type containing the same ID, causing infinite recursion

Example triggering the issue:
```fsharp
type Option 'a = None | Some of 'a
// Constructor scheme for None: forall 'a. Option<'a>
// If 'a has ID 1, and freshTypeVar() also generates ID 1,
// apply would infinitely substitute
```

## Resolution

Use negative IDs (-1, -2, -3, ...) in `TypeDefEnvBuilder` to avoid collision with `freshTypeVar()` which only generates positive IDs.

```fsharp
// TypeDefEnvBuilder.fs
let mutable private nextId = 0
let private freshNegativeId () =
    nextId <- nextId - 1
    nextId

// Creates type variable with negative ID: TVar -1, TVar -2, etc.
// This never collides with freshTypeVar() which creates TVar 1, TVar 2, etc.
```

## Key Insight

When multiple systems generate type variable IDs independently, they must use non-overlapping ranges to prevent collision. Using negative vs positive IDs is a simple and effective solution.
