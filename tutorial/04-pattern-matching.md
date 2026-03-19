# Chapter 4: Pattern Matching

Pattern matching is the primary control flow mechanism in LangThree.
It combines destructuring, conditional dispatch, and variable binding in a single construct.
The compiler checks exhaustiveness and compiles patterns to efficient decision trees.

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

funlang> match 3 with | 1 -> "one" | 2 -> "two" | 3 -> "three" | _ -> "other"
"three"
```

Multiple constants for dispatch tables:

```
$ cat daytype.l3
let dayType d =
    match d with
    | 1 -> "Monday"
    | 2 -> "Tuesday"
    | 3 -> "Wednesday"
    | 4 -> "Thursday"
    | 5 -> "Friday"
    | 6 -> "Saturday"
    | 7 -> "Sunday"
    | _ -> "invalid"
let result = dayType 3

$ langthree daytype.l3
"Wednesday"
```

**Note:** String constant patterns are not supported. Use `if ... then ... else` for string comparison.

### Variable and Wildcard Patterns

A variable pattern binds the matched value to a name. `_` is a wildcard that discards the value:

```
funlang> match 42 with | x -> x + 1
43

funlang> match 42 with | _ -> 0
0
```

Variable patterns always match -- they act as catch-all cases:

```
$ cat sign.l3
let sign x =
    match x with
    | 0 -> 0
    | n when n > 0 -> 1
    | _ -> 0 - 1
let result = (sign 5, sign 0, sign (0 - 3))

$ langthree sign.l3
(1, 0, -1)
```

**Shadowing:** A variable in a pattern shadows any outer binding with the same name:

```
funlang> let x = 10 in match 5 with | x -> x
5
```

The inner `x` binds to 5, not 10.

### Tuple Patterns

Decompose tuples in-place:

```
funlang> match (1, 2) with | (a, b) -> a + b
3
```

Nested tuple patterns:

```
funlang> match ((1, 2), (3, 4)) with | ((a, b), (c, d)) -> a + b + c + d
10
```

Combine tuples with constants and wildcards:

```
$ cat classify_pair.l3
let classify pair =
    match pair with
    | (true, 0) -> "zero-true"
    | (true, x) -> "positive-true: " + to_string x
    | (false, _) -> "false"
let result = classify (true, 42)

$ langthree classify_pair.l3
"positive-true: 42"
```

### List Patterns

Match on empty list, cons, or specific lengths:

```
funlang> match [1, 2, 3] with | [] -> "empty" | x :: _ -> to_string x
"1"

funlang> match [1, 2, 3] with | a :: b :: _ -> a + b | _ -> 0
3
```

Precise length matching:

```
$ cat list_describe.l3
let describe xs =
    match xs with
    | [] -> "empty"
    | x :: [] -> "singleton: " + to_string x
    | x :: y :: [] -> "pair: " + to_string x + "," + to_string y
    | x :: y :: z :: _ -> "three+: " + to_string x + "," + to_string y + "," + to_string z

let r1 = describe []
let r2 = describe [42]
let r3 = describe [1, 2]
let r4 = describe [10, 20, 30, 40]
let result = r1 + " | " + r2 + " | " + r3 + " | " + r4

$ langthree list_describe.l3
"empty | singleton: 42 | pair: 1,2 | three+: 10,20,30"
```

### Constructor Patterns

Match algebraic data type constructors:

```
$ cat shape.l3
type Shape = Circle of int | Rect of int * int

let area s =
    match s with
    | Circle r -> r * r * 3
    | Rect (w, h) -> w * h
let result = area (Circle 5)

$ langthree shape.l3
75
```

Constructors without data (nullary):

```
$ cat card.l3
type Card = Ace | King | Queen | Jack | Num of int

let value c =
    match c with
    | Ace -> 11
    | King -> 10
    | Queen -> 10
    | Jack -> 10
    | Num n -> n

let result = value Ace + value King + value (Num 5)

$ langthree card.l3
26
```

Wildcard inside constructors:

```
$ cat is_leaf.l3
type Tree = Leaf | Node of Tree * int * Tree

let isLeaf t =
    match t with
    | Leaf -> true
    | Node (_, _, _) -> false

let result = (isLeaf Leaf, isLeaf (Node (Leaf, 1, Leaf)))

$ langthree is_leaf.l3
(true, false)
```

### Nested Patterns

Patterns compose arbitrarily -- constructors inside constructors, lists inside tuples, etc:

**Option inside Option:**

```
$ cat deep_option.l3
let deepGet opt =
    match opt with
    | Some (Some (Some x)) -> to_string x
    | Some (Some None) -> "inner none"
    | Some None -> "mid none"
    | None -> "outer none"

let r1 = deepGet (Some (Some (Some 42)))
let r2 = deepGet (Some (Some None))
let r3 = deepGet (Some None)
let r4 = deepGet None
let result = r1 + " | " + r2 + " | " + r3 + " | " + r4

$ langthree deep_option.l3
"42 | inner none | mid none | outer none"
```

**List of tuples:**

```
funlang> let rec sumFirst xs = match xs with | [] -> 0 | (a, _) :: rest -> a + sumFirst rest in sumFirst [(1, "a"), (2, "b"), (3, "c")]
6
```

**Constructor with tuple inside list:**

```
$ cat nested_complex.l3
type Opt 'a = None | Some of 'a
let result =
    match Some [1, 2, 3] with
    | Some (x :: _) -> x
    | Some [] -> 0
    | None -> 0

$ langthree nested_complex.l3
1
```

### Record Patterns

Destructure record fields in a match:

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

Partial record patterns -- match only some fields:

```
$ cat record_partial.l3
type Person = { name: string; age: int; active: bool }

let greet p =
    match p with
    | { name = n; age = a } -> n + " is " + to_string a

let result = greet { name = "Alice"; age = 30; active = true }

$ langthree record_partial.l3
"Alice is 30"
```

## When Guards

Add boolean conditions to patterns with `when`. The guard is evaluated
after the pattern matches. If the guard fails, matching falls through to
the next clause.

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

### Guards for Range Classification

Multiple guards create range-based dispatch:

```
$ cat grade.l3
let grade score =
    match score with
    | s when s >= 90 -> "A"
    | s when s >= 80 -> "B"
    | s when s >= 70 -> "C"
    | s when s >= 60 -> "D"
    | _ -> "F"
let result = grade 85

$ langthree grade.l3
"B"
```

### Guards with Constructors

Combine structural matching with value conditions:

```
$ cat shape_guard.l3
type Shape = Circle of int | Rect of int * int

let isLarge s =
    match s with
    | Circle r when r > 10 -> true
    | Rect (w, h) when w * h > 100 -> true
    | _ -> false

let r1 = isLarge (Circle 15)
let r2 = isLarge (Circle 5)
let r3 = isLarge (Rect (20, 10))
let result = (r1, r2, r3)

$ langthree shape_guard.l3
(true, false, true)
```

### Guard Fallthrough

When a guard fails, matching continues with the next clause -- it does
NOT skip to the default. This enables layered conditions:

```
$ cat fallthrough.l3
let classify x =
    match x with
    | n when n > 100 -> "large"
    | n when n > 10 -> "medium"
    | n when n > 0 -> "small"
    | 0 -> "zero"
    | _ -> "negative"
let result = classify 50

$ langthree fallthrough.l3
"medium"
```

## Match on Computed Values

Match on the result of an expression, not just a variable:

```
$ cat match_expr.l3
let abs x = if x < 0 then 0 - x else x
let classify x =
    match abs x with
    | 0 -> "zero"
    | n when n < 10 -> "small"
    | n when n < 100 -> "medium"
    | _ -> "large"
let result = classify (0 - 42)

$ langthree match_expr.l3
"medium"
```

## Exhaustiveness Checking

The compiler warns about missing cases (W0001):

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

### Redundancy Warnings

The compiler also warns about unreachable patterns (W0002):

```
$ cat redundant.l3
let result =
    match 1 with
    | _ -> "catch all"
    | 1 -> "one"

$ langthree redundant.l3
Warning: warning[W0002]: Redundant pattern match clause. This pattern is never reached
 --> :0:0-1:0
   = hint: Remove this clause or reorder the patterns
"catch all"
```

The wildcard `_` catches everything, so the `| 1` clause is unreachable.

### Exhaustiveness with Tuples of ADTs

Pattern exhaustiveness works across nested structures:

```
$ cat color_mix.l3
type Color = Red | Green | Blue

let mix a b =
    match (a, b) with
    | (Red, Blue) -> "purple"
    | (Blue, Red) -> "purple"
    | (Red, Green) -> "yellow"
    | (Green, Red) -> "yellow"
    | (Blue, Green) -> "cyan"
    | (Green, Blue) -> "cyan"
    | _ -> "same"

let result = mix Red Blue

$ langthree color_mix.l3
"purple"
```

## Let-Pattern Destructuring

Destructure without a full `match`:

```
funlang> let (x, y) = (1, 2) in x + y
3

funlang> let (a, b, c) = (1, 2, 3) in a + b + c
6
```

## Decision Tree Compilation

LangThree compiles pattern matches to binary decision trees using the
Jules Jacobs algorithm. This means:

- **No redundant tests:** Each constructor is tested at most once per execution path
- **Efficient dispatch:** O(depth) per match, not O(clauses)
- **Clause sharing:** Common sub-patterns share decision nodes

You don't need to think about this for correctness, but it means complex
matches are efficient even with many clauses.

## Practical Examples

### Recursive List Processing

The standard pattern for list recursion: match empty vs cons, recurse on tail.

**Sum a list:**

```
funlang> let rec sum xs = match xs with | [] -> 0 | x :: rest -> x + sum rest in sum [1, 2, 3, 4, 5]
15
```

**Count elements:**

```
funlang> let rec length xs = match xs with | [] -> 0 | _ :: rest -> 1 + length rest in length [10, 20, 30]
3
```

**Filter with predicate (closure capture):**

```
funlang> let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t in filter (fun x -> x > 3) [1, 2, 3, 4, 5, 6]
[4, 5, 6]
```

**Take while predicate holds:**

```
$ cat take_while.l3
let result =
    let rec takeWhile pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: takeWhile pred t else []
    in takeWhile (fun x -> x < 5) [1, 2, 3, 4, 5, 6, 7]

$ langthree take_while.l3
[1, 2, 3, 4]
```

### ADT Expression Evaluator

Pattern matching shines with recursive ADT traversal:

```
$ cat expr_eval.l3
type Expr = Num of int | Add of Expr * Expr | Mul of Expr * Expr

let result =
    let rec eval e = match e with | Num n -> n | Add (a, b) -> eval a + eval b | Mul (a, b) -> eval a * eval b
    in eval (Add (Mul (Num 3, Num 4), Num 5))

$ langthree expr_eval.l3
17
```

`eval (Add (Mul (Num 3, Num 4), Num 5))` computes `(3 * 4) + 5 = 17`.

### Lookup in Association List

Pattern matching on list of tuples for key-value lookup:

```
$ cat lookup.l3
let result =
    let rec lookup key = fun xs -> match xs with | [] -> None | (k, v) :: rest -> if k = key then Some v else lookup key rest
    in
    let env = [(1, "one"), (2, "two"), (3, "three")]
    in
    let r1 = lookup 2 env
    in
    let r2 = lookup 9 env
    in (r1, r2)

$ langthree lookup.l3
(Some "two", None)
```

### Tree Traversal with Pattern Matching

All tree operations are natural pattern matches:

```
$ cat tree_ops.l3
type Tree = Leaf | Node of Tree * int * Tree

let result =
    let rec depth t = match t with | Leaf -> 0 | Node (l, _, r) -> let dl = depth l in let dr = depth r in if dl > dr then dl + 1 else dr + 1
    in
    let rec size t = match t with | Leaf -> 0 | Node (l, _, r) -> 1 + size l + size r
    in
    let rec sumTree t = match t with | Leaf -> 0 | Node (l, v, r) -> sumTree l + v + sumTree r
    in
    let t = Node (Node (Leaf, 1, Leaf), 2, Node (Leaf, 3, Node (Leaf, 4, Leaf)))
    in (depth t, size t, sumTree t)

$ langthree tree_ops.l3
(3, 4, 10)
```

### Insertion Sort via Pattern Matching

Two recursive functions chained together:

```
$ cat isort.l3
let sorted =
    let rec insert x = fun xs -> match xs with | [] -> x :: [] | h :: t -> if x <= h then x :: h :: t else h :: insert x t
    in
    let rec sort xs = match xs with | [] -> [] | h :: t -> insert h (sort t)
    in sort [5, 3, 8, 1, 9, 2, 7, 4, 6]

let result = sorted

$ langthree isort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

## Summary

| Pattern | Syntax | Example |
|---------|--------|---------|
| Constant | `0`, `true` | `\| 0 -> "zero"` |
| Variable | `x` | `\| x -> x + 1` |
| Wildcard | `_` | `\| _ -> "default"` |
| Tuple | `(a, b)` | `\| (x, y) -> x + y` |
| List empty | `[]` | `\| [] -> "empty"` |
| List cons | `h :: t` | `\| x :: rest -> x` |
| Constructor | `Some x` | `\| Some v -> v` |
| Record | `{ x = a }` | `\| { x = a } -> a` |
| Nested | `Some (x :: _)` | `\| Some (h :: _) -> h` |
| With guard | `x when cond` | `\| n when n > 0 -> "pos"` |
