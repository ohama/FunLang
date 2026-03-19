# Chapter 12: Prelude and Standard Library

LangThree loads a standard library called the Prelude at startup. Prelude files
provide type declarations and constructors that are available in all user code
without explicit imports.

## How the Prelude Works

The Prelude consists of `.fun` files in the `Prelude/` directory adjacent to the
LangThree binary. At startup, these files are sorted alphabetically, then each is
parsed as a module, type-checked, and evaluated. The types and constructors they
define become available in all subsequent code.

Currently, the Prelude ships with one file:

**Prelude/Option.fun:**
```
type Option 'a = None | Some of 'a
```

This defines the `Option` type with constructors `None` and `Some`, available
everywhere without any `open` directive.

## Using the Option Type

### Creating Option Values

The `Some` and `None` constructors work in both expression mode and file mode:

```
$ langthree --expr 'Some 42'
Some 42

$ langthree --expr 'Some "hello"'
Some "hello"

$ langthree --expr 'None'
None
```

Check inferred types:

```
$ cat check_option.l3
let x = Some 42

$ langthree --emit-type check_option.l3
x : Option<int>
```

### Pattern Matching on Option

Pattern matching on Prelude types works in file mode:

```
$ cat option_match.l3
let x = Some 42
let result =
    match x with
    | Some v -> v
    | None -> 0

$ langthree option_match.l3
42
```

Extracting with a default:

```
$ cat option_default.l3
let getOrDefault default opt =
    match opt with
    | Some x -> x
    | None -> default
let result = getOrDefault 0 None

$ langthree option_default.l3
0
```

### Common Option Patterns

**Mapping over an Option** -- apply a function to the value inside `Some`:

```
$ cat option_map.l3
let optionMap f opt =
    match opt with
    | Some x -> Some (f x)
    | None -> None
let double x = x * 2
let result =
    match optionMap double (Some 5) with
    | Some v -> v
    | None -> 0

$ langthree option_map.l3
10
```

**Binding (flatMap)** -- chain operations that may fail:

```
$ cat option_bind.l3
let optionBind f opt =
    match opt with
    | Some x -> f x
    | None -> None
let safeDivide x =
    if x = 0 then None else Some (100 / x)
let result =
    match optionBind safeDivide (Some 5) with
    | Some v -> v
    | None -> 0

$ langthree option_bind.l3
20
```

**Using Option with pipe:**

```
$ cat option_pipe.l3
let optionMap f opt =
    match opt with
    | Some x -> Some (f x)
    | None -> None
let double x = x * 2
let result =
    match (Some 5 |> optionMap double) with
    | Some v -> v
    | None -> 0

$ langthree option_pipe.l3
10
```

## Extending the Prelude

You can add your own types to the Prelude by creating new `.fun` files in the
`Prelude/` directory. Files are sorted alphabetically, so naming affects load order.

For example, creating `Prelude/Result.fun`:

```
type Result 'a 'b = Ok of 'a | Error of 'b
```

After adding this file, `Ok` and `Error` constructors become available in all code:

```
$ cat result_demo.l3
let safeDivide x y =
    if y = 0 then Error "division by zero"
    else Ok (x / y)
let result =
    match safeDivide 10 3 with
    | Ok v -> v
    | Error _ -> 0

$ langthree result_demo.l3
3
```

Prelude types work in both `--expr` mode and file mode. Constructors from Prelude
files are available without `open`.

## Built-in Type Signatures

Separate from the Prelude, LangThree has a set of **type-only** built-in names.
These have type signatures for type checking but **no runtime implementation**.
They exist so that user code can reference standard function types:

```
$ langthree --emit-type --expr 'map'
('a -> 'b) -> 'a list -> 'b list

$ langthree --emit-type --expr 'filter'
('a -> bool) -> 'a list -> 'a list

$ langthree --emit-type --expr 'id'
'a -> 'a
```

However, calling these at runtime produces an error:

```
$ langthree --expr 'id 42'
Error: Undefined variable: id
```

The full list of type-only built-ins:

| Name | Type Signature |
|------|---------------|
| `map` | `('a -> 'b) -> 'a list -> 'b list` |
| `filter` | `('a -> bool) -> 'a list -> 'a list` |
| `fold` | `('a -> 'b -> 'a) -> 'a -> 'b list -> 'a` |
| `length` | `'a list -> int` |
| `reverse` | `'a list -> 'a list` |
| `append` | `'a list -> 'a list -> 'a list` |
| `hd` | `'a list -> 'a` |
| `tl` | `'a list -> 'a list` |
| `id` | `'a -> 'a` |
| `const` | `'a -> 'b -> 'a` |
| `compose` | `('a -> 'b) -> ('c -> 'a) -> 'c -> 'b` |

To use these operations, define them inline with `let rec ... in`:

```
$ cat my_map.l3
let result =
    let rec myMap f = fun xs -> match xs with | [] -> [] | x :: rest -> f x :: myMap f rest
    in myMap (fun x -> x * 2) [1, 2, 3]

$ langthree my_map.l3
[2, 4, 6]
```

Since `let rec` supports only a single parameter, use a nested `fun` for the
second parameter (see [Chapter 2](02-functions.md) for details).

## Built-in Runtime Functions

A separate set of built-ins **do** have runtime implementations. These come from
the built-in environment, not from Prelude files:

| Function | Type | Description |
|----------|------|-------------|
| `string_length` | `string -> int` | Length of a string |
| `string_concat` | `string -> string -> string` | Concatenate two strings |
| `string_sub` | `string -> int -> int -> string` | Substring (start, length) |
| `string_contains` | `string -> string -> bool` | Substring test |
| `to_string` | `'a -> string` | Convert int/bool/string to string |
| `string_to_int` | `string -> int` | Parse string as integer |
| `print` | `string -> unit` | Print without newline |
| `println` | `string -> unit` | Print with newline |
| `printf` | `string -> ...` | Formatted output |

These work in both expression mode and file mode:

```
$ langthree --expr 'string_length "hello"'
5

$ langthree --expr 'to_string 42'
"42"
```

See [Chapter 11: Strings and Output](11-strings-and-output.md) for full details.

## Prelude vs Built-in Summary

| Category | Source | Available at runtime? | Example |
|----------|--------|----------------------|---------|
| Prelude types | `Prelude/*.fun` files | Yes (types + constructors) | `Option`, `None`, `Some` |
| Type-only built-ins | Type environment | No (type check only) | `map`, `id`, `compose` |
| Runtime built-ins | Built-in environment | Yes (callable) | `print`, `string_length` |

## Notes

- **Prelude files** are `.fun` files loaded alphabetically from the `Prelude/` directory
- **Prelude constructors** (`None`, `Some`) are available without `open`
- **Type-only built-ins** (`map`, `id`, etc.) type-check but fail at runtime
- **Runtime built-ins** (`print`, `string_length`, etc.) work everywhere
- **Pattern matching** on Prelude types works in file mode
