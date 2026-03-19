# Chapter 2: Functions

## Anonymous Functions

Lambda syntax uses `fun`:

```
funlang> (fun x -> x + 1) 10
11
```

With type annotation:

```
funlang> (fun (x: int) -> x + 1) 10
11
```

## Let Bindings (REPL / Expression Mode)

In the REPL, bind values with `let ... in`:

```
funlang> let x = 5 in x + 1
6
```

Bindings can be chained:

```
funlang> let x = 5 in let y = x + 1 in y * 2
12
```

## Let Bindings (File Mode)

In file mode, `let` bindings are top-level declarations -- no `in` needed.
The last binding's value is printed:

```
$ cat add.l3
let a = 10
let b = 20
let result = a + b

$ langthree add.l3
30
```

## Multi-Parameter Functions

Multi-parameter functions desugar to nested lambdas (currying).
This works at module level (file mode):

```
$ cat multi.l3
let add x y = x + y
let result = add 3 4

$ langthree multi.l3
7
```

Equivalent to:

```
$ cat multi2.l3
let add = fun x -> fun y -> x + y
let result = add 3 4

$ langthree multi2.l3
7
```

## Recursive Functions

Use `let rec` for recursion. This only works at expression level (with `in`),
**not** at module level -- a known limitation.

```
funlang> let rec fact n = if n <= 1 then 1 else n * fact (n - 1) in fact 5
120
```

**Limitation:** `let rec` supports only a single parameter. For multi-parameter
recursive functions, take a single tuple or use nested lambdas inside the body:

```
funlang> let rec len xs = match xs with | [] -> 0 | _ :: rest -> 1 + len rest in len [1, 2, 3]
3
```

In file mode, embed `let rec ... in` inside a top-level `let`:

```
$ cat factorial.l3
let result =
    let rec fact n = if n <= 1 then 1 else n * fact (n - 1)
    in fact 10

$ langthree factorial.l3
3628800
```

## Higher-Order Functions

Functions are first-class values. Pass them as arguments:

```
$ cat hof.l3
let apply f x = f x
let result = apply (fun x -> x + 1) 10

$ langthree hof.l3
11
```

Return functions from functions:

```
$ cat hof2.l3
let make_adder n = fun x -> x + n
let add10 = make_adder 10
let result = add10 5

$ langthree hof2.l3
15
```

## Closures

Functions capture variables from their enclosing scope:

```
$ cat closure.l3
let x = 10
let add_x y = x + y
let result = add_x 5

$ langthree closure.l3
15
```

## Currying and Partial Application

Multi-parameter functions are automatically curried:

```
$ cat curry.l3
let add x y = x + y
let add5 = add 5
let result = add5 3

$ langthree curry.l3
8
```

## Pipe and Composition Operators

The pipe operator `|>` passes a value as the last argument:

```
funlang> 5 |> (fun x -> x + 1)
6
```

Composition operators `>>` (left-to-right) and `<<` (right-to-left):

```
funlang> let f = (fun x -> x + 1) >> (fun x -> x * 2) in f 3
8
```

Here `f 3` computes `(3 + 1) * 2 = 8`.
