# Chapter 8: Modules and Namespaces

## Module Declaration

Define a module with `module M =` followed by an indented body:

```
$ cat config.l3
module Config =
    let width = 800
    let height = 600
    let title = "My App"
let result = Config.title + " (" + to_string Config.width + "x" + to_string Config.height + ")"

$ langthree config.l3
"My App (800x600)"
```

Indentation defines the module body scope -- no `end` keyword needed.

## Qualified Access

Access module members with dot notation:

```
$ cat qualified.l3
module Math =
    let double x = x * 2
    let triple x = x * 3
let result = Math.double 5 + Math.triple 3

$ langthree qualified.l3
19
```

## Open Directive

Bring all module members into scope with `open`:

```
$ cat open_mod.l3
module M =
    let x = 10
    let y = 20
open M
let result = x + y

$ langthree open_mod.l3
30
```

After `open M`, you can use `x` and `y` directly without the `M.` prefix.

## Nested Modules

Modules can nest. Each level uses further indentation:

```
$ cat nested.l3
module Outer =
    module Inner =
        let value = 42
let result = Outer.Inner.value

$ langthree nested.l3
42
```

Chained qualified access works to any depth.

## Multiple Modules

A file can contain multiple module declarations. Modules are resolved
top-to-bottom -- later modules can reference earlier ones:

```
$ cat multi_mod.l3
module A =
    let x = 10
module B =
    let y = 20
let result = A.x + B.y

$ langthree multi_mod.l3
30
```

## Modules with Type Declarations

Modules can contain ADT definitions. Use `open` to bring constructors
into scope, or qualified access for nullary constructors:

```
$ cat mod_type.l3
module Colors =
    type Color = Red | Green | Blue
open Colors
let result =
    match Green with
    | Red -> "red"
    | Green -> "green"
    | Blue -> "blue"

$ langthree mod_type.l3
"green"
```

Qualified constructor access works for both nullary and data-carrying constructors:

```
$ cat mod_ctor.l3
module M =
    type Opt = MNone | MSome of int
let result = M.MSome 42

$ langthree mod_ctor.l3
MSome 42
```

## Module Functions with Pattern Matching

Combine module functions with ADTs:

```
$ cat mod_fn.l3
module M =
    type Opt = MNone | MSome of int
    let unwrap x =
        match x with
        | MSome v -> v
        | MNone -> 0
let result = M.unwrap (M.MSome 42)

$ langthree mod_fn.l3
42
```

## Namespace Declaration

A `namespace` declaration wraps the entire file's contents:

```
$ cat ns.l3
namespace App
let x = 42
let result = x + 1

$ langthree ns.l3
43
```

Unlike `module`, a namespace does not create a nested scope -- declarations
are at the top level.

## Practical Example: Layered Configuration

Multiple modules organizing related values:

```
$ cat layers.l3
module DB =
    let host = "localhost"
    let port = 5432
module App =
    let name = "MyService"
    let version = 1
let result = App.name + " v" + to_string App.version + " -> " + DB.host + ":" + to_string DB.port

$ langthree layers.l3
"MyService v1 -> localhost:5432"
```

## Notes

- **Indentation-based:** Module body is delimited by indentation, not `end` or `}`
- **Top-to-bottom ordering:** Modules must be defined before they are referenced (no circular dependencies)
- **`module M =`** uses `=` for nested modules; top-level `namespace` has no `=`
- **Qualified access** works for values, functions, and constructors
