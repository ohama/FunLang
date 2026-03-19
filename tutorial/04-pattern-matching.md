# Chapter 4: Pattern Matching

## Basic Match Syntax

Match expressions use `|` pipes aligned with the `match` keyword:

```
funlang> match 2 with | 0 -> "zero" | 1 -> "one" | _ -> "other"
"other"
```

In file mode, multi-line match uses indentation:

```
$ cat classify.l3
let classify x =
    match x with
    | 0 -> "zero"
    | 1 -> "one"
    | _ -> "other"
let result = classify 1

$ langthree classify.l3
"one"
```

Pipes must align with the `match` keyword column -- not indented from it.

## Pattern Types

### Constant Patterns

Integer and boolean literals:

```
funlang> match true with | true -> "yes" | false -> "no"
"yes"
```

**Note:** String constant patterns are not supported.

### Variable and Wildcard Patterns

A variable binds the matched value; `_` discards it:

```
funlang> match 42 with | x -> x + 1
43

funlang> match 42 with | _ -> 0
0
```

### Tuple Patterns

Decompose tuples in-place:

```
funlang> match (1, 2) with | (a, b) -> a + b
3
```

### List Patterns

Match on empty, cons, or literal lists:

```
funlang> match [1, 2, 3] with | [] -> "empty" | x :: _ -> to_string x
"1"

funlang> match [1, 2, 3] with | a :: b :: _ -> a + b | _ -> 0
3
```

### Constructor Patterns

Match algebraic data type constructors (must be in scope):

```
$ cat option_match.l3
type Option 'a = None | Some of 'a
let result =
    match Some 42 with
    | Some x -> x
    | None -> 0

$ langthree option_match.l3
42
```

Custom ADTs:

```
$ cat shape.l3
type Shape = Circle of int | Rect of int
let area s =
    match s with
    | Circle r -> r * r * 3
    | Rect side -> side * side
let result = area (Circle 5)

$ langthree shape.l3
75
```

### Nested Patterns

Patterns compose arbitrarily:

```
$ cat nested.l3
type Option 'a = None | Some of 'a
let result =
    match Some (Some 42) with
    | Some (Some x) -> x
    | _ -> 0

$ langthree nested.l3
42
```

### Record Patterns

Destructure record fields:

```
$ cat record_match.l3
type Point = { x: int; y: int }
let p = { x = 1; y = 2 }
let result =
    match p with
    | { x = a; y = b } -> a + b

$ langthree record_match.l3
3
```

## When Guards

Add conditions to patterns with `when`:

```
$ cat guard.l3
let classify n =
    match n with
    | x when x > 0 -> "positive"
    | 0 -> "zero"
    | _ -> "negative"
let result = classify 5

$ langthree guard.l3
"positive"
```

Guards are evaluated after the pattern matches. If the guard fails,
matching continues with the next clause.

## Exhaustiveness Checking

The compiler warns about missing cases:

```
$ cat exhaustive.l3
type Color = Red | Green | Blue
let result =
    match Red with
    | Red -> 1
    | Green -> 2

$ langthree exhaustive.l3
Warning: warning[W0001]: Incomplete pattern match. Missing cases: Blue
 --> :0:0-1:0
   = hint: Add the missing cases or a wildcard pattern '_' to cover all values
1
```

Add a wildcard or cover all cases to silence the warning.

## Let-Pattern Destructuring

Destructure without a full `match`:

```
funlang> let (x, y) = (1, 2) in x + y
3

funlang> let (a, b, c) = (1, 2, 3) in a + b + c
6
```

## Practical Example: List Processing

Combine patterns with recursion:

```
funlang> let rec sum xs = match xs with | [] -> 0 | x :: rest -> x + sum rest in sum [1, 2, 3, 4, 5]
15
```

Count list elements:

```
funlang> let rec length xs = match xs with | [] -> 0 | _ :: rest -> 1 + length rest in length [10, 20, 30]
3
```
