---
title: "Expression-level let rec ... and ... (mutual recursion)"
priority: low
category: language-feature
created: 2026-03-20
source: v1.6 offside rule discussion
---

## Limitation

Expression-level `let rec ... and ...` is not supported. Currently only module-level mutual recursion works:

```fsharp
// Works (module-level)
let rec even n =
    if n = 0 then true else odd (n - 1)
and odd n =
    if n = 0 then false else even (n - 1)

// Does NOT work (expression-level)
let result =
    let rec even n = if n = 0 then true else odd (n - 1)
    and odd n = if n = 0 then false else even (n - 1)
    even 4
```

## Root Cause

1. **AST**: `LetRec` holds a single binding `(name, params, body)`, not a list
2. **Parser**: No `LET REC ... AND_KW ... IN Expr` rule for expressions
3. **Type checker / Evaluator**: Only handle single `LetRec` binding

## Required Changes (~100 lines)

1. New AST variant: `LetRecMutual of bindings: (string * string list * Expr) list * body: Expr`
2. Parser rule: `LET REC RecBindings IN Expr` with `RecBindings: RecBinding AND_KW RecBindings | RecBinding`
3. Type checker: Unify all bindings simultaneously (like module-level)
4. Evaluator: Create mutually-recursive closures with shared env (like module-level)

## Workaround

Use module-level `let rec ... and ...` instead:

```fsharp
module Parity =
    let rec even n = if n = 0 then true else odd (n - 1)
    and odd n = if n = 0 then false else even (n - 1)

Parity.even 4  // true
```
