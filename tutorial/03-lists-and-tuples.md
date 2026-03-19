# Chapter 3: Lists and Tuples

## List Basics

Lists are ordered, homogeneous collections:

```
$ langthree --expr '[1, 2, 3]'
[1, 2, 3]

$ langthree --expr '[]'
[]
```

The cons operator `::` prepends an element:

```
$ langthree --expr '1 :: [2, 3]'
[1, 2, 3]

$ langthree --expr '1 :: 2 :: 3 :: []'
[1, 2, 3]
```

## Tuples

Tuples are fixed-size, heterogeneous collections:

```
$ langthree --expr '(1, "hello")'
(1, "hello")

$ langthree --expr '(1, "hello", true)'
(1, "hello", true)
```

Decompose tuples with pattern binding:

```
$ langthree --expr 'let (x, y) = (1, 2) in x + y'
3

$ langthree --expr 'let (a, b, c) = (1, 2, 3) in a + b + c'
6
```

## Unit

The unit type `()` represents "no value":

```
$ langthree --expr '()'
()
```

## Combining Lists and Tuples

Lists of tuples:

```
$ langthree --expr '[(1, "a"), (2, "b")]'
[(1, "a"), (2, "b")]
```

## Recursive List Functions

Since `let rec` only supports a single parameter, recursive list functions
take the list as their one argument. Use closures to capture extra state.

**Length:**

```
$ langthree --expr 'let rec length xs = match xs with | [] -> 0 | _ :: rest -> 1 + length rest in length [1, 2, 3, 4]'
4
```

**Sum:**

```
$ langthree --expr 'let rec sum xs = match xs with | [] -> 0 | x :: rest -> x + sum rest in sum [1, 2, 3, 4, 5]'
15
```

**Map** (applying a function to each element):

```
$ langthree --expr 'let rec go xs = match xs with | [] -> [] | x :: rest -> x * 10 :: go rest in go [1, 2, 3]'
[10, 20, 30]
```

**Filter** (capture the predicate in a closure):

```
$ langthree --expr 'let f = fun x -> x > 2 in let rec go xs = match xs with | [] -> [] | x :: rest -> if f x then x :: go rest else go rest in go [1, 2, 3, 4, 5]'
[3, 4, 5]
```

## Pattern Matching on Lists

Preview of Chapter 4 -- `match` with list patterns:

```
$ langthree --expr 'match [1, 2, 3] with | [] -> "empty" | x :: _ -> to_string x'
"1"
```

Nested destructuring:

```
$ langthree --expr 'match [1, 2, 3] with | a :: b :: _ -> a + b | _ -> 0'
3
```
