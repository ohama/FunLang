# Chapter 14: Algorithms and Data Structures

This chapter demonstrates how to implement classic algorithms and data structures
in LangThree. If you have experience with functional programming in OCaml, Haskell,
or similar languages, you will find the patterns familiar -- but LangThree has its
own idioms worth understanding.

## Recursive Patterns in LangThree

All recursion in LangThree uses `let rec` at the **expression level** with `in`.
There is no `let rec` at module level. Recursive functions are defined inside an
expression and chained together:

```
let result =
    let rec f x = ...
    in f 10
```

When a recursive function needs **two or more parameters**, the second parameter
is passed via a closure:

```
let result =
    let rec f x = fun y -> ... f something_else another_thing ...
    in f 1 2
```

Multiple recursive helpers are chained with `let rec ... in let rec ... in`:

```
let answer =
    let rec helper1 x = ...
    in
    let rec helper2 y = ... helper1 ... helper2 ...
    in helper2 start
```

This pattern appears throughout the chapter. Each algorithm is self-contained:
define helpers with `let rec ... in`, then call the entry point at the end.

## Number Theory

### Factorial

The classic recursive factorial. Note that `let rec` must appear inside an
expression -- here it lives inside the `let result = ...` binding:

```
$ cat factorial.l3
let result =
    let rec fact n = if n <= 1 then 1 else n * fact (n - 1)
    in fact 10

$ langthree factorial.l3
3628800
```

`fact 10` computes 10 * 9 * 8 * ... * 1 = 3628800.

### Fibonacci

Naive recursive Fibonacci. The double recursion makes this exponential, but it
demonstrates the pattern clearly:

```
$ cat fibonacci.l3
let result =
    let rec fib n = if n <= 1 then n else fib (n - 1) + fib (n - 2)
    in fib 10

$ langthree fibonacci.l3
55
```

The sequence is 0, 1, 1, 2, 3, 5, 8, 13, 21, 34, **55**.

### GCD and LCM

Euclid's algorithm for greatest common divisor, then least common multiple
derived from it. Since `gcd` needs two parameters and is recursive, the second
parameter comes via a closure (`fun b -> ...`):

```
$ cat gcd_lcm.l3
let result =
    let rec gcd a = fun b -> if b = 0 then a else gcd b (a - (a / b) * b)
    in
    let lcm = fun a -> fun b -> a / gcd a b * b
    in (gcd 48 36, lcm 12 18)

$ langthree gcd_lcm.l3
(12, 36)
```

`gcd 48 36` reduces: gcd 48 36 -> gcd 36 12 -> gcd 12 0 -> 12.
`lcm 12 18` computes 12 / gcd(12,18) * 18 = 12 / 6 * 18 = 36.

Note `a - (a / b) * b` is the modulo operation, since LangThree uses integer
division for `/`.

### Exponentiation

Compute `base^exp` by repeated multiplication:

```
$ cat power.l3
let result =
    let rec power base = fun exp -> if exp = 0 then 1 else base * power base (exp - 1)
    in power 2 10

$ langthree power.l3
1024
```

`power 2 10` computes 2^10 = 1024. The two-parameter recursive pattern is the
same as `gcd`: the first parameter `base` is the `let rec` parameter, the second
`exp` arrives via `fun exp -> ...`.

## List Utilities

### Reverse

Reverse a list using a tail-recursive accumulator. The accumulator `acc` is the
`let rec` parameter; the list `xs` arrives via closure:

```
$ cat reverse.l3
let result =
    let rec rev acc = fun xs -> match xs with | [] -> acc | h :: t -> rev (h :: acc) t
    in rev [] [1, 2, 3, 4, 5]

$ langthree reverse.l3
[5, 4, 3, 2, 1]
```

This is tail-recursive: each step conses the head onto the accumulator and
recurses on the tail. No stack growth.

### Flatten

Flatten a list of lists into a single list. This requires an `app` (append)
helper:

```
$ cat flatten.l3
let result =
    let rec app xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: app t ys
    in
    let rec flatten xss = match xss with | [] -> [] | xs :: rest -> app xs (flatten rest)
    in flatten [[1, 2], [3, 4, 5], [], [6]]

$ langthree flatten.l3
[1, 2, 3, 4, 5, 6]
```

`flatten` processes each sub-list, appending it to the flattened rest.

### Zip

Combine two lists element-wise into a list of pairs. The nested match handles
the case where either list runs out:

```
$ cat zip.l3
let result =
    let rec zip xs = fun ys -> match xs with | [] -> [] | x :: xt -> match ys with | [] -> [] | y :: yt -> (x, y) :: zip xt yt
    in zip [1, 2, 3] [10, 20, 30]

$ langthree zip.l3
[(1, 10), (2, 20), (3, 30)]
```

When the lists have different lengths, `zip` stops at the shorter one.

### Maximum of a List

Find the maximum element. The base case matches a singleton list `x :: []`:

```
$ cat max_list.l3
let result =
    let rec maxList xs = match xs with | x :: [] -> x | x :: rest -> let m = maxList rest in if x > m then x else m
    in maxList [3, 7, 2, 9, 1, 8, 4]

$ langthree max_list.l3
9
```

### Fold (Left)

A left fold applies a binary function across a list, threading an accumulator.
This is the workhorse of functional programming -- many other operations can be
expressed as folds:

```
$ cat fold.l3
let result =
    let rec fold f = fun acc -> fun xs -> match xs with | [] -> acc | h :: t -> fold f (f acc h) t
    in fold (fun acc -> fun x -> acc + x * x) 0 [1, 2, 3, 4, 5]

$ langthree fold.l3
55
```

The fold computes 0 + 1*1 + 2*2 + 3*3 + 4*4 + 5*5 = 1 + 4 + 9 + 16 + 25 = 55.

`fold` takes three parameters via nested closures: the function `f` is the
`let rec` parameter, `acc` arrives via the first `fun`, and `xs` via the second.

### Map via Fold

Once you have `fold`, you can build `map` on top of it:

```
$ cat map_via_fold.l3
let result =
    let rec fold f = fun acc -> fun xs -> match xs with | [] -> acc | h :: t -> fold f (f acc h) t
    in
    let rec rev acc = fun xs -> match xs with | [] -> acc | h :: t -> rev (h :: acc) t
    in
    let map f = fun xs -> rev [] (fold (fun acc -> fun x -> f x :: acc) [] xs)
    in map (fun x -> x * x) [1, 2, 3, 4, 5]

$ langthree map_via_fold.l3
[1, 4, 9, 16, 25]
```

The fold builds the result in reverse, so we reverse it at the end.

### Length

Count the elements of a list:

```
funlang> let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t in length [10, 20, 30, 40]
4
```

## Sorting Algorithms

Sorting is a great lens for comparing algorithmic approaches. All three sorts
below take an unsorted list and return a new sorted list -- no mutation involved.

### Insertion Sort

Build a sorted list by inserting each element into its correct position.
Two recursive helpers: `insert` places one element, `sort` processes the list:

```
$ cat insertion_sort.l3
let sorted =
    let rec insert x = fun xs -> match xs with | [] -> x :: [] | h :: t -> if x <= h then x :: h :: t else h :: insert x t
    in
    let rec sort xs = match xs with | [] -> [] | h :: t -> insert h (sort t)
    in sort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree insertion_sort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

`insert` walks the sorted list until it finds the right spot. `sort` recursively
sorts the tail, then inserts the head. O(n^2) worst case, but simple and stable.

### Quicksort

Partition around a pivot, recursively sort each half, and append. This needs
three helpers -- `filter`, `app` (append), and `qsort` itself:

```
$ cat quicksort.l3
let sorted =
    let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t
    in
    let rec app xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: app t ys
    in
    let rec qsort xs = match xs with | [] -> [] | pivot :: rest -> let lo = filter (fun x -> x < pivot) rest in let hi = filter (fun x -> x >= pivot) rest in app (qsort lo) (pivot :: qsort hi)
    in qsort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree quicksort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

The pivot is simply the head of the list. `filter` selects elements less than
the pivot (`lo`) and elements greater than or equal (`hi`). The sorted result
is `qsort lo ++ [pivot] ++ qsort hi`.

### Merge Sort

Split the list in half, sort each half, then merge. This requires several
helpers -- `length`, `take`, `drop`, `merge`, and `msort`:

```
$ cat merge_sort.l3
let sorted =
    let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t
    in
    let rec take n = fun xs -> if n = 0 then [] else match xs with | [] -> [] | h :: t -> h :: take (n - 1) t
    in
    let rec drop n = fun xs -> if n = 0 then xs else match xs with | [] -> [] | _ :: t -> drop (n - 1) t
    in
    let rec merge xs = fun ys -> match xs with | [] -> ys | x :: xt -> match ys with | [] -> xs | y :: yt -> if x <= y then x :: merge xt (y :: yt) else y :: merge (x :: xt) yt
    in
    let rec msort xs = let len = length xs in if len <= 1 then xs else let mid = len / 2 in merge (msort (take mid xs)) (msort (drop mid xs))
    in msort [5, 3, 8, 1, 9, 2, 7, 4, 6]

$ langthree merge_sort.l3
[1, 2, 3, 4, 5, 6, 7, 8, 9]
```

This is a top-down merge sort. `take` and `drop` split the list at the midpoint.
`merge` interleaves two sorted lists by comparing heads. O(n log n) guaranteed.

Notice how the `let rec ... in` chain builds up a library of helpers. Each later
function can call all previous ones. This is the idiomatic LangThree pattern for
non-trivial programs.

## Tree Data Structures

### Binary Search Tree and Tree Sort

Define a binary tree type, then implement insert, build, and in-order traversal.
The result is a tree-based sort:

```
$ cat tree_sort.l3
type Tree = Leaf | Node of Tree * int * Tree

let sorted =
    let rec treeInsert x = fun t -> match t with | Leaf -> Node (Leaf, x, Leaf) | Node (l, v, r) -> if x <= v then Node (treeInsert x l, v, r) else Node (l, v, treeInsert x r)
    in
    let rec buildTree xs = match xs with | [] -> Leaf | h :: t -> treeInsert h (buildTree t)
    in
    let rec app xs = fun ys -> match xs with | [] -> ys | h :: t -> h :: app t ys
    in
    let rec inorder t = match t with | Leaf -> [] | Node (l, v, r) -> app (inorder l) (v :: inorder r)
    in inorder (buildTree [5, 3, 8, 1, 9, 2, 7])

$ langthree tree_sort.l3
[1, 2, 3, 5, 7, 8, 9]
```

The `Tree` type is an algebraic data type with two constructors: `Leaf` (empty)
and `Node` carrying a left subtree, a value, and a right subtree.

`treeInsert` places a value into the BST by comparing with the node value and
recursing left or right. `buildTree` folds a list into a tree by inserting each
element. `inorder` traversal produces a sorted list.

### Peano Naturals and Church-Style Arithmetic

Natural numbers as an algebraic type, with addition and multiplication defined
by structural recursion:

```
$ cat peano.l3
type Nat = Zero | Succ of Nat

let result =
    let rec toInt n = match n with | Zero -> 0 | Succ p -> 1 + toInt p
    in
    let rec add a = fun b -> match a with | Zero -> b | Succ p -> Succ (add p b)
    in
    let rec mul a = fun b -> match a with | Zero -> Zero | Succ p -> add b (mul p b)
    in
    let three = Succ (Succ (Succ Zero))
    in
    let four = Succ (Succ (Succ (Succ Zero)))
    in toInt (mul three four)

$ langthree peano.l3
12
```

`add` peels off `Succ` from `a` and wraps the result: add (Succ (Succ Zero)) b
= Succ (Succ b). `mul` uses repeated addition: mul (Succ (Succ Zero)) b =
add b (add b Zero).

Three times four is twelve -- verified by converting back to `int` with `toInt`.

This example shows how LangThree's algebraic types and pattern matching make it
natural to define and compute with custom numeric representations.

## Practical Examples

### Balanced Parentheses Checker

Check whether a sequence of parentheses is balanced. Since LangThree does not
have a char type, we encode `(` as 1 and `)` as 0:

```
$ cat balanced.l3
let result =
    let rec check depth = fun cs -> match cs with | [] -> depth = 0 | c :: rest -> if c = 1 then check (depth + 1) rest else if c = 0 then if depth > 0 then check (depth - 1) rest else false else check depth rest
    in (check 0 [1, 1, 0, 0], check 0 [1, 0, 0, 1])

$ langthree balanced.l3
(true, false)
```

The first sequence `(())` is balanced, so `check` returns `true`. The second
sequence `())( ` has a closing paren before a matching open, so `check` returns
`false` immediately when `depth` would go negative.

The algorithm is a single pass: increment depth on `(`, decrement on `)`, fail
if depth goes below zero, and succeed only if depth is zero at the end.

### Option Chaining with Safe Division

Use the Prelude `Option` type to chain computations that might fail:

```
$ cat safe_div.l3
let safediv a = fun b -> if b = 0 then None else Some (a / b)

let bind opt = fun f ->
    match opt with
    | None -> None
    | Some v -> f v

let result =
    let step1 = safediv 100 5 in
    let step2 = bind step1 (fun x -> safediv x 4) in
    let step3 = bind step2 (fun x -> safediv x 0) in
    (step1, step2, step3)

$ langthree safe_div.l3
(Some 20, Some 5, None)
```

`safediv 100 5` gives `Some 20`. Dividing that by 4 gives `Some 5`. Dividing by
zero gives `None`, and the `None` propagates through subsequent `bind` calls.

### Expression Evaluator

Build a small arithmetic expression tree and evaluate it:

```
$ cat expr_eval.l3
type Expr =
    | Lit of int
    | Add of Expr * Expr
    | Mul of Expr * Expr

let result =
    let rec eval e = match e with | Lit n -> n | Add (a, b) -> eval a + eval b | Mul (a, b) -> eval a * eval b
    in eval (Add (Mul (Lit 3, Lit 4), Lit 5))

$ langthree expr_eval.l3
17
```

`Mul (Lit 3, Lit 4)` evaluates to 12, then `Add (_, Lit 5)` gives 17. The
recursive evaluator mirrors the tree structure exactly -- one branch per
constructor.

### Counting with Filter and Length

Combine helpers to count elements satisfying a predicate:

```
$ cat count_evens.l3
let result =
    let rec length xs = match xs with | [] -> 0 | _ :: t -> 1 + length t
    in
    let rec filter pred = fun xs -> match xs with | [] -> [] | h :: t -> if pred h then h :: filter pred t else filter pred t
    in
    let countWhere pred = fun xs -> length (filter pred xs)
    in countWhere (fun x -> x - (x / 2) * 2 = 0) [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

$ langthree count_evens.l3
5
```

There are 5 even numbers in 1..10. The predicate `x - (x / 2) * 2 = 0` checks
evenness using integer arithmetic (since there is no `mod` operator).

## Summary

| Pattern | Example |
|---|---|
| Single-param recursion | `let rec f x = ... f ... in f start` |
| Multi-param recursion | `let rec f x = fun y -> ... f ... in f a b` |
| Chained helpers | `let rec h1 ... in let rec h2 ... in h2 start` |
| Accumulator pattern | `let rec go acc = fun xs -> ... go (acc') t in go init list` |
| ADT + recursion | `type T = ... let rec f t = match t with ...` |
| Option chaining | `bind (safediv a b) (fun x -> safediv x c)` |

Key takeaways:

- **`let rec` is expression-level only.** All recursion lives inside a
  `let name = let rec ... in ...` binding.
- **Multi-parameter recursive functions** use `fun` for the second parameter
  onward: `let rec f x = fun y -> ...`.
- **Chain helpers** with `let rec ... in let rec ... in`. Each helper can call
  all previously defined helpers.
- **Algebraic data types** combine naturally with recursive functions for trees,
  expression languages, and custom numeric types.
- **The Prelude `Option` type** provides `None` and `Some` for safe computation
  without exceptions.
