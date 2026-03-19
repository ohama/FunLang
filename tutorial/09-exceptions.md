# Chapter 9: Exceptions

## Exception Declaration

Declare an exception type with `exception`:

```
$ cat exc_basic.l3
exception NotFound
let result = try
    raise NotFound
with
| NotFound -> 42
| _ -> 0

$ langthree exc_basic.l3
42
```

## Exceptions with Data

Constructors can carry values with `of`:

```
$ cat exc_data.l3
exception InvalidArg of string
let result = try
    raise (InvalidArg "bad input")
with
| InvalidArg msg -> "error: " + msg
| _ -> "unknown"

$ langthree exc_data.l3
"error: bad input"
```

Note: `raise` takes an atom, so constructor application needs parentheses:
`raise (InvalidArg "bad input")`, not `raise InvalidArg "bad input"`.

## Multiple Handlers

Match different exception types in the same `try-with`:

```
$ cat exc_multi.l3
exception NotFound
exception Timeout of int
let result = try
    raise (Timeout 30)
with
| NotFound -> "not found"
| Timeout secs -> "timeout after " + to_string secs + "s"
| _ -> "unknown"

$ langthree exc_multi.l3
"timeout after 30s"
```

## When Guards

Add conditions to exception handlers with `when`:

```
$ cat exc_guard.l3
exception Error of int
let result = try
    raise (Error 42)
with
| Error x when x > 100 -> "big error"
| Error x -> "error: " + to_string x
| _ -> "unknown"

$ langthree exc_guard.l3
"error: 42"
```

The guard is evaluated after the pattern matches. If it fails,
matching continues with the next handler.

## Nested Try-With

Unhandled exceptions propagate to outer handlers:

```
$ cat exc_nested.l3
exception Inner
exception Outer
let result = try
    try
        raise Inner
    with
    | Outer -> "wrong"
    | _ -> "inner caught"
with
| Inner -> "outer caught"
| _ -> "fallback"

$ langthree exc_nested.l3
"inner caught"
```

If the inner handler does not match, the exception propagates outward.
Change the inner `raise` to `raise Outer` and the inner handler's
catch-all would match instead.

## Exception Re-raising

When no handler matches, the exception automatically re-raises:

```
$ cat exc_reraise.l3
exception First
exception Second
let result = try
    try
        raise First
    with
    | Second -> "wrong"
    | _ -> "inner fallback"
with
| First -> "outer caught first"
| _ -> "outer fallback"

$ langthree exc_reraise.l3
"inner fallback"
```

## Non-Exhaustive Handler Warning (W0003)

Since exceptions are an open type (new exceptions can be declared
anywhere), the compiler warns when a handler lacks a catch-all:

```
$ cat exc_warn.l3
exception NotFound
let result = try
    raise NotFound
with
| NotFound -> 42

$ langthree exc_warn.l3
Warning: warning[W0003]: Non-exhaustive exception handler: not all exceptions are handled; consider adding a catch-all handler
 --> :0:0-1:0
   = hint: Add a catch-all handler or handle all possible exceptions
42
```

Add `| _ -> ...` to silence the warning:

```
$ cat exc_nowarn.l3
exception NotFound
let result = try
    raise NotFound
with
| NotFound -> 42
| _ -> 0

$ langthree exc_nowarn.l3
42
```

## Practical Example: Safe Division

Combining exceptions with functions:

```
$ cat safe_div.l3
exception DivByZero
let safe_div a b =
    if b = 0 then raise DivByZero
    else a / b
let result = try
    safe_div 10 0
with
| DivByZero -> -1
| _ -> -2

$ langthree safe_div.l3
-1
```

## Syntax Notes

- **`raise` takes an atom:** Use parentheses for constructors with data: `raise (Error msg)`
- **`try` indentation:** Pipes in `with` handlers align like `match` pipes
- **Open type:** Exception types cannot be exhaustively matched (hence W0003)
- **Catch-all:** Add `| _ -> ...` as a final handler to cover all exceptions
