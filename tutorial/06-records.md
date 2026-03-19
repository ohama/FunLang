# Chapter 6: Records

## Record Type Declaration

Define a record with named fields:

```
$ cat point.l3
type Point = { px: int; py: int }
let p = { px = 3; py = 4 }
let result = p.px + p.py

$ langthree point.l3
7
```

**Important:** Field names must be globally unique across all record types.
Two record types cannot share a field name.

## Field Access

Use dot notation to access fields:

```
$ cat access.l3
type Person = { name: string; age: int }
let alice = { name = "Alice"; age = 30 }
let result = alice.name + " is " + to_string alice.age

$ langthree access.l3
"Alice is 30"
```

## Chained Field Access

Dot notation chains for nested records:

```
$ cat nested.l3
type Inner = { val: int }
type Outer = { inner: Inner }
let o = { inner = { val = 42 } }
let result = o.inner.val

$ langthree nested.l3
42
```

## Copy-and-Update

Create a modified copy with `{ record with field = value }`:

```
$ cat update.l3
type Point = { px: int; py: int }
let p = { px = 1; py = 2 }
let moved = { p with px = 10 }
let result = moved

$ langthree update.l3
{ px = 10; py = 2 }
```

Update multiple fields at once:

```
$ cat multi_update.l3
type Vec3 = { vx: int; vy: int; vz: int }
let v = { vx = 1; vy = 2; vz = 3 }
let result = { v with vx = 10; vy = 20 }

$ langthree multi_update.l3
{ vx = 10; vy = 20; vz = 3 }
```

The original record is unchanged -- copy-and-update produces a new value.

## Record Pattern Matching

Destructure records in `match` expressions:

```
$ cat record_match.l3
type Point = { px: int; py: int }
let p = { px = 3; py = 4 }
let result =
    match p with
    | { px = a; py = b } -> a + b

$ langthree record_match.l3
7
```

## Mutable Fields

Declare a field as `mutable` to allow in-place updates:

```
$ cat counter.l3
type Counter = { mutable count: int }
let c = { count = 0 }
let _ = c.count <- c.count + 1
let _ = c.count <- c.count + 1
let _ = c.count <- c.count + 1
let result = c.count

$ langthree counter.l3
3
```

The `<-` operator updates the field in place and returns unit `()`.
Use `let _ =` to sequence mutations at module level.

## Parametric Records

Records can have type parameters (placed after the type name):

```
$ cat pair.l3
type Pair 'a = { fst: 'a; snd: 'a }
let p = { fst = 1; snd = 2 }
let result = p.fst + p.snd

$ langthree pair.l3
3
```

## Structural Equality

Records support structural equality comparison:

```
$ cat equality.l3
type Point = { px: int; py: int }
let p1 = { px = 1; py = 2 }
let p2 = { px = 1; py = 2 }
let p3 = { px = 1; py = 3 }
let r1 = if p1 = p2 then "equal" else "not equal"
let result = if p1 = p3 then "equal" else "not equal"

$ langthree equality.l3
"not equal"
```

## Practical Example: Mutable State

A bank account with deposit and balance check:

```
$ cat account.l3
type Account = { mutable balance: int }
let acct = { balance = 100 }
let _ = acct.balance <- acct.balance + 50
let _ = acct.balance <- acct.balance - 30
let result = acct.balance

$ langthree account.l3
120
```

## Limitations

- **No field punning:** You cannot write `{ px; py }` as shorthand for `{ px = px; py = py }`.
- **Globally unique fields:** Two record types cannot share the same field name.
  The compiler resolves record types from their field names, so uniqueness is required.
