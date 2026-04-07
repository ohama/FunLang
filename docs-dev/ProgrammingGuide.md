# FunLang Programming Guide

Idiomatic FunLang style for clean, readable code.

## 1. Indentation Over `in`

FunLang uses offside rule. Avoid `in` — let indentation do the work.

```
// Bad: noisy
let result = let x = 1 in let y = 2 in x + y

// Good: clean
let result =
    let x = 1
    let y = 2
    x + y
```

Reserve `in` only for true single-line expressions:

```
let xs = [1; 2; 3]
let size = let n = List.length xs in n * 2
```

Top level is declarations only — bind results with `let`:

```
let x = 1
let y = 2
let result = x + y
```

## 2. Multiline Match

Always use multiline match with aligned pipes.

```
// Bad: cramped
let f x = match x with | 0 -> "zero" | _ -> "other"

// Good: one arm per line
let f x =
    match x with
    | 0 -> "zero"
    | 1 -> "one"
    | _ -> "other"
let result = f 1
```

Match arms with complex bodies — indent the body:

```
type Expr =
    | Lit of int
    | Add of Expr * Expr
    | Neg of Expr

let rec eval expr =
    match expr with
    | Lit n -> n
    | Add (a, b) ->
        let va = eval a
        let vb = eval b
        va + vb
    | Neg e ->
        0 - eval e
let result = eval (Add (Lit 3, Neg (Lit 2)))
```

## 3. Pipe Everything

Use `|>` to eliminate nesting and improve readability.

```
let double x = x * 2
let isEven x = x % 2 = 0
let xs = [3; 1; 2; 5; 4]

// Bad: nested calls
let bad = List.filter isEven (List.map double (List.sort xs))

// Good: pipeline
let result =
    xs
    |> List.sort
    |> List.map double
    |> List.filter isEven
```

Use `>>` for point-free composition:

```
let double x = x * 2
let isEven x = x % 2 = 0
let process = List.sort >> List.map double >> List.filter isEven
let result = process [3; 1; 2]
```

## 4. Prelude Reference

Use Prelude modules instead of hand-rolling common operations.

### Core

| Function | Description |
|----------|-------------|
| `id x` | Identity |
| `const x _` | Always returns x |
| `flip f x y` | Flip arguments |
| `ignore x` | Discard value, return () |
| `not b` | Boolean negation |
| `min a b` / `max a b` | Min/max |
| `abs n` | Absolute value |
| `fst p` / `snd p` | Tuple projection |

### Operators

| Operator | Description |
|----------|-------------|
| `x \|> f` | Forward pipe |
| `f <\| x` | Backward pipe |
| `f >> g` | Forward composition |
| `f << g` | Backward composition |
| `xs ++ ys` | List append |
| `s1 ^^ s2` | String concat |

### List

```
List.map f xs           List.filter pred xs
List.fold f init xs     List.length xs
List.reverse xs         List.append xs ys
List.head xs            List.tail xs
List.sort xs            List.sortBy f xs
List.find pred xs       List.tryFind pred xs
List.any pred xs        List.all pred xs
List.zip xs ys          List.flatten xss
List.take n xs          List.drop n xs
List.nth xs n           List.contains x xs
List.choose f xs        List.partition pred xs
List.groupBy f xs       List.distinctBy f xs
List.mapi f xs          List.iter f xs
List.sum xs             List.sumBy f xs
List.init n f           List.replicate n x
List.scan f init xs     List.collect f xs
List.pairwise xs        List.unzip pairs
List.findIndex pred xs  List.forall pred xs
List.minBy f xs         List.maxBy f xs
```

### Option

```
type Option 'a = None | Some of 'a

optionMap f opt          optionBind f opt
optionDefault def opt    optionDefaultValue def opt
optionFilter pred opt    optionIter f opt
optionIsSome opt         optionIsNone opt
```

### Result

```
type Result 'a 'b = Ok of 'a | Error of 'b

resultMap f r            resultBind f r
resultMapError f r       resultDefault def r
resultDefaultValue def r resultIter f r
resultToOption r         isOk r        isError r
```

### String

```
String.length s         String.concat sep xs
String.contains s sub   String.indexOf s sub
String.replace s old new
String.split s sep      String.join sep xs
String.substring s start len
String.toUpper s        String.toLower s
String.trim s           String.startsWith s prefix
String.endsWith s suffix
```

### Char

```
Char.isDigit c    Char.isLetter c
Char.isUpper c    Char.isLower c
Char.toUpper c    Char.toLower c
Char.toInt c      Char.ofInt n
```

## 5. Side Effects and `ignore`

Top level requires `let` for everything. Use `let _ =` for side effects:

```
let _ = println "step 1"
let _ = println "step 2"
let _ = println "done"
```

Inside an indented block, newline sequencing works — no `let _ =` needed:

```
let _ =
    println "step 1"
    println "step 2"
    println "done"
```

Use `ignore` to discard non-unit return values:

```
// List.map returns a list, but we only want the side effect
let _ = List.map (fun x -> println (to_string x)) xs |> ignore

// ignore is cleaner than binding to _
let result = List.fold (fun acc x -> acc + x) 0 xs
```

## 6. Drop Unnecessary Parentheses

FunLang is curried. Parentheses are only needed for grouping.

```
let f x = x + 1

// Bad: function call doesn't need parens
let r1 = f(3)

// Good
let r2 = f 3
```

Parentheses needed only for:

```
// Negative literals in patterns
let describe n =
    match n with
    | -1 -> "minus one"
    | _ -> "other"

// Grouping subexpressions
let f x = x + 1
let g x = x * 2
let r1 = f (g 3)
let r2 = f (3 + 1)

// Tuple construction
let pair = (1, 2)

// Constructor with complex argument
let opt = Some (3 + 1)
```

## 7. Option/Result Over Exceptions

Prefer `Option` and `Result` for expected failure cases. Reserve exceptions for truly exceptional situations.

```
// Good: Option for lookup
let ht = Hashtable.create ()
let _ = Hashtable.set ht "name" "Alice"

let tryFind key =
    let (found, value) = Hashtable.tryGetValue ht key
    if found then Some value
    else None

let name = tryFind "name" |> optionDefault "anonymous"
let missing = tryFind "email" |> optionDefault "anonymous"
let result = (name, missing)
```

```
// Good: Result for validation
let parseAge input =
    let n = string_to_int input
    if n < 0 then Error "age must be positive"
    else if n > 150 then Error "unrealistic age"
    else Ok n

let process input =
    parseAge input
    |> resultBind (fun age ->
        if age >= 18 then Ok "adult"
        else Ok "minor")
let result = process "25"
```

Exceptions are still appropriate for:

- Programmer errors (bug, should never happen)
- System failures (I/O errors)
- Boundary validation at entry points

## 8. Type Annotations

Add type annotations at module boundaries for clarity. Omit in local code where types are obvious.

```
// Module-level: annotate for documentation
let parseInt (s : string) : int = string_to_int s

// Local: let inference work
let helper x =
    let doubled = x * 2
    let msg = to_string doubled
    msg
let result = parseInt "42"
```

## 9. ADT and Pattern Matching

Define ADTs for domain modeling. Use exhaustive matching.

```
type Shape =
    | Circle of int
    | Rect of int * int
    | Triangle of int * int * int

let area shape =
    match shape with
    | Circle r -> 3 * r * r
    | Rect (w, h) -> w * h
    | Triangle (a, b, c) ->
        let s = (a + b + c) / 2
        s * (s - a) * (s - b) * (s - c)
let result = area (Rect (3, 4))
```

Use or-patterns for shared handling:

```
let isWeekend day =
    match day with
    | "Sat" | "Sun" -> true
    | _ -> false
let result = isWeekend "Sat"
```

## 10. Module Organization

Group related functions in modules. Use `open` sparingly.

```
module Stack =
    let empty = []
    let push x stack = x :: stack
    let pop stack =
        match stack with
        | [] -> (None, [])
        | x :: rest -> (Some x, rest)
    let peek stack =
        match stack with
        | [] -> None
        | x :: _ -> Some x

open Stack
let s = empty |> push 1 |> push 2 |> push 3
let result = peek s
```

## 11. List Idioms

Prefer list operations over manual recursion.

```
// Bad: manual recursion
let rec sumPositive xs =
    match xs with
    | [] -> 0
    | h :: t ->
        if h > 0 then h + sumPositive t
        else sumPositive t

// Good: compose operations
let sumPositive2 xs =
    xs |> List.filter (fun x -> x > 0) |> List.sum
let result = sumPositive2 [1; -2; 3; -4; 5]
```

List comprehensions for transformation:

```
let squares = [for i in 1..10 -> i * i]
let xs = [1; 2; 3]
let pairs = [for x in xs -> (x, x * 2)]
```

Ranges:

```
let digits = [0..9]
let evens = [0..2..20]
```

## 12. Mutable State

When mutation is needed, use `let mutable` and `<-`.

```
let xs = [1; -2; 3; -4; 5]
let mutable count = 0
let _ =
    for x in xs do
        if x > 0 then
            count <- count + 1
let result = count
```

Prefer immutable approaches when practical:

```
// Better: no mutation
let xs = [1; -2; 3; -4; 5]
let count = xs |> List.filter (fun x -> x > 0) |> List.length
```

## 13. `List.map` / `List.iter` Over Manual Recursion

Accumulator-style recursion with `acc ++ [x]` is a common anti-pattern. Use `List.map` instead.

```
// Bad: manual accumulator recursion
let rec processAll items acc =
    match items with
    | [] -> acc
    | x :: rest -> processAll rest (acc ++ [transform x])

// Good: one-liner
let results = List.map transform items
```

Use `List.iter` for side effects instead of `List.map` + ignore:

```
// Bad: map then discard
let _ = List.map (fun x -> println (to_string x)) xs

// Good: iter for side effects
let _ = List.iter (fun x -> println (to_string x)) xs
```

Use `List.mapi` when you need the index:

```
// Bad: manual index tracking
let rec go items i acc =
    match items with
    | [] -> acc
    | x :: rest -> go rest (i + 1) (acc ++ [format i x])

// Good
let results = List.mapi (fun i x -> format i x) items
```

## 14. Result Chaining with `resultBind`

Avoid nested match cascades for Result types. Use `resultBind` to chain.

```
// Bad: nested match pyramid
let result =
    match step1 input with
    | Error e -> Error e
    | Ok v1 ->
        match step2 v1 with
        | Error e -> Error e
        | Ok v2 ->
            match step3 v2 with
            | Error e -> Error e
            | Ok v3 -> Ok (finish v3)

// Good: flat chain
let result =
    step1 input
    |> resultBind step2
    |> resultBind step3
    |> resultMap finish
```

## 15. Block Sequencing for Side Effects

Inside a `let _ =` block, newline sequencing chains expressions. All expressions
in a block must have the same type (usually `unit`):

```
// Good: all println return unit → block sequencing works
let _ =
    println "step 1"
    println "step 2"
    println "done"
```

When expressions return different types, use separate `let _ =`:

```
// Different return types → separate bindings
let _ = HashSet.add visited s
let _ = MutableList.add items s
let _ = Queue.enqueue wl s
```

For mutable imperative code, `while`/`for` inside a block are idiomatic:

```
let xs = [1; 2; 3; 4; 5]
let mutable total = 0
let _ =
    for x in xs do
        total <- total + x
let result = total
```

## 16. Hashtable Lookup Helpers

Wrap verbose `tryGetValue` tuple destructuring in a helper:

```
// Verbose: repeated everywhere
let v =
    match Hashtable.tryGetValue ht key with
    | (true, x) -> x
    | _ -> defaultVal

// Better: define once, reuse
let getOrDefault ht key def =
    let (found, v) = Hashtable.tryGetValue ht key
    if found then v else def
```

## Quick Reference: Style Checklist

- [ ] No `in` unless truly single-line
- [ ] Match arms on separate lines
- [ ] Pipelines over nested calls
- [ ] `List.map`/`List.iter`/`List.mapi` over manual recursion
- [ ] `resultBind` chain over nested match pyramid
- [ ] Block sequencing over repeated `let _ =`
- [ ] No unnecessary parentheses around arguments
- [ ] `Option`/`Result` over exceptions for expected cases
- [ ] Type annotations at module boundaries only
- [ ] Immutable by default, `mutable` when needed
