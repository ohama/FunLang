# Chapter 5: Algebraic Data Types

## Simple Enumerations

Define a type with named constructors:

```
$ cat color.l3
type Color = Red | Green | Blue
let result =
    match Green with
    | Red -> "red"
    | Green -> "green"
    | Blue -> "blue"

$ langthree color.l3
"green"
```

## Leading Pipe Syntax

For multi-line type definitions, a leading pipe is allowed:

```
$ cat direction.l3
type Direction =
    | North
    | South
    | East
    | West
let result =
    match North with
    | North -> "up"
    | South -> "down"
    | East -> "right"
    | West -> "left"

$ langthree direction.l3
"up"
```

## Constructors with Data

Constructors can carry values with `of`:

```
$ cat shape.l3
type Shape = Circle of int | Rect of int * int
let area s =
    match s with
    | Circle r -> r * r * 3
    | Rect (w, h) -> w * h
let result = area (Rect (3, 4))

$ langthree shape.l3
12
```

A constructor with a tuple argument is destructured in the pattern.

## Parametric Types

Type parameters go AFTER the type name:

```
$ cat option.l3
type Option 'a = None | Some of 'a
let x = Some 42
let result =
    match x with
    | Some v -> v
    | None -> 0

$ langthree option.l3
42
```

Check inferred types with `--emit-type`:

```
$ langthree --emit-type option.l3
result : int
x : Option<int>
```

Multiple type parameters:

```
$ cat either.l3
type Either 'a 'b = Left of 'a | Right of 'b
let result =
    match Left 42 with
    | Left n -> n
    | Right s -> string_length s

$ langthree either.l3
42
```

## Recursive Types

Types can refer to themselves:

```
$ cat intlist.l3
type IntList = Nil | Cons of int * IntList
let xs = Cons (1, Cons (2, Cons (3, Nil)))
let result =
    let rec sum xs = match xs with | Nil -> 0 | Cons (x, rest) -> x + sum rest in
    sum xs

$ langthree intlist.l3
6
```

Binary tree with a depth function:

```
$ cat tree.l3
type Tree = Leaf of int | Branch of Tree * Tree
let t = Branch (Leaf 1, Branch (Leaf 2, Leaf 3))
let result =
    let rec depth t = match t with | Leaf _ -> 1 | Branch (l, r) -> 1 + (let dl = depth l in let dr = depth r in if dl > dr then dl else dr) in
    depth t

$ langthree tree.l3
3
```

Note: `let rec` only works at expression level (with `in`), not at module level.
For recursive functions in file mode, embed `let rec ... in` inside a top-level `let`.

## Mutually Recursive Types

Use `and` to define types that reference each other:

```
$ cat mutual.l3
type Tree = Leaf of int | Node of Forest
and Forest = Empty | Trees of Tree * Forest
let result = Node (Trees (Leaf 1, Trees (Leaf 2, Empty)))

$ langthree mutual.l3
Node (Trees ((Leaf 1, Trees ((Leaf 2, Empty)))))
```

## Exhaustiveness Checking

The compiler warns when match patterns are incomplete:

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

The program still runs, but the warning tells you which cases are missing.
Add a wildcard `_` or cover all constructors to silence it.

## Practical Example: Simple Calculator

An expression type for a calculator:

```
$ cat calc.l3
type Expr = Num of int | Plus of Expr * Expr | Mul of Expr * Expr
let e = Plus (Num 2, Mul (Num 3, Num 4))
let result =
    let rec eval e = match e with | Num n -> n | Plus (a, b) -> eval a + eval b | Mul (a, b) -> eval a * eval b in
    eval e

$ langthree calc.l3
14
```
