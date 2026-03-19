# Chapter 10: Pipes and Composition

## The Pipe Operator

The pipe operator `|>` passes a value as the argument to a function:

```
$ langthree --expr '5 |> (fun x -> x + 1)'
6
```

Pipe chains read left-to-right, describing a data transformation pipeline:

```
$ cat pipe_chain.l3
let double x = x * 2
let inc x = x + 1
let result = 5 |> double |> inc

$ langthree pipe_chain.l3
11
```

Here `5 |> double |> inc` computes `inc (double 5)` = `inc 10` = `11`.

## Pipe with Built-in Functions

Pipe works with built-in functions, not just user-defined ones:

```
$ langthree --expr '"hello" |> string_length'
5
```

Curried built-ins work naturally with pipe:

```
$ langthree --expr '"world" |> string_concat "hello "'
"hello world"
```

## Pipe with Lambdas

Pass a lambda as the function:

```
$ langthree --expr '10 |> (fun x -> x * x)'
100
```

Parentheses around the lambda are required.

## Forward Composition

The `>>` operator composes two functions left-to-right.
`f >> g` creates a function that applies `f` first, then `g`:

```
$ cat compose_fwd.l3
let double x = x * 2
let inc x = x + 1
let f = double >> inc
let result = f 5

$ langthree compose_fwd.l3
11
```

`f 5` computes `inc (double 5)` = `inc 10` = `11`.

## Backward Composition

The `<<` operator composes right-to-left.
`g << f` means "apply `f` first, then `g`" -- same as `f >> g`:

```
$ cat compose_bwd.l3
let double x = x * 2
let inc x = x + 1
let g = inc << double
let result = g 5

$ langthree compose_bwd.l3
11
```

Both `double >> inc` and `inc << double` produce the same function.

## Chained Composition

Composition chains for multi-step transformations:

```
$ cat compose_chain.l3
let add1 x = x + 1
let mul2 x = x * 2
let sub3 x = x - 3
let f = add1 >> mul2 >> sub3
let result = f 5

$ langthree compose_chain.l3
9
```

`f 5` = `sub3 (mul2 (add1 5))` = `sub3 (mul2 6)` = `sub3 12` = `9`.

## Pipe vs Composition

**Pipe** transforms a specific value through a pipeline:

```
$ cat pipe_example.l3
let double x = x * 2
let inc x = x + 1
let result = 5 |> double |> inc

$ langthree pipe_example.l3
11
```

**Composition** builds a new function for later use:

```
$ cat comp_example.l3
let double x = x * 2
let inc x = x + 1
let transform = double >> inc
let a = transform 5
let result = transform 10

$ langthree comp_example.l3
21
```

Use pipe when you have a value and want to transform it now.
Use composition when you want to define a reusable transformation.

## Practical Example: Data Pipeline

Combining pipes with string operations:

```
$ cat pipeline.l3
let result = 42 |> to_string |> string_concat "answer: "

$ langthree pipeline.l3
"answer: 42"
```

Composition for reusable formatters:

```
$ cat formatter.l3
let format_num = to_string >> string_concat "value="
let result = format_num 99

$ langthree formatter.l3
"value=99"
```

## Precedence

Pipe has the lowest precedence of all operators, so expressions like
`x + 1 |> f` pipe the result of `x + 1` into `f`. Composition
operators bind tighter than pipe but looser than arithmetic:

| Operator | Precedence |
|----------|-----------|
| `\|>` | lowest |
| `>>`, `<<` | low |
| `\|\|` | ... |
| `&&` | ... |
| `+`, `-` | ... |
| `*`, `/` | highest |
