# Chapter 7: GADTs (Generalized Algebraic Data Types)

## What Are GADTs?

Regular ADTs give every constructor the same return type.
GADTs let each constructor specify its own return type, enabling the type
system to refine types within pattern match branches.

## GADT Constructor Syntax

Each constructor declares its argument and return type with `:` and `->`:

```
$ cat expr.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let result = IntLit 42

$ langthree expr.l3
IntLit 42
```

Compare with a regular ADT where you would write `IntLit of int`.
In GADT syntax, `IntLit : int -> int Expr` means:
- Takes an `int` argument
- Produces a value of type `int Expr` (not just `'a Expr`)

Type parameters go after the type name: `type Expr 'a`.
Return types use `int Expr` (not `Expr int` or `Expr<int>`).

## Type Refinement in Match

The key power of GADTs: when you match on a constructor, the compiler
knows the exact type parameter. This requires a type annotation on the
match expression:

```
$ cat eval.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let eval e =
    (match e with
    | IntLit n -> n
    | BoolLit b -> if b then 1 else 0
    : int)
let result = eval (IntLit 42)

$ langthree eval.l3
42
```

The annotation `: int` goes at the END of the match, inside the parentheses.
This puts the match expression in "check mode" so the compiler can refine
types per branch:
- In the `IntLit n` branch, `n` is known to be `int`
- In the `BoolLit b` branch, `b` is known to be `bool`

## The Annotation Is Required

Without the type annotation, GADT matching fails with E0401:

```
$ cat no_anno.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let eval e =
    match e with
    | IntLit n -> n
    | BoolLit b -> if b then 1 else 0
let result = eval (IntLit 42)

$ langthree no_anno.l3
error[E0401]: GADT match requires type annotation on scrutinee of type 'm
 --> :0:0-1:0
   = hint: Add a type annotation to the match scrutinee: match (expr : Type) with ...
```

The fix: wrap the match in parentheses and add `: ResultType` at the end.

## Recursive GADT Evaluation

For recursive constructors like `Add`, use `let rec ... in`:

```
$ cat calc.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr | Add : int Expr * int Expr -> int Expr
let result =
    let rec eval e = (match e with | IntLit n -> n | BoolLit b -> if b then 1 else 0 | Add (a, b) -> eval a + eval b : int) in
    eval (Add (IntLit 10, Add (IntLit 20, IntLit 12)))

$ langthree calc.l3
42
```

Since `let rec` only works at expression level, the recursive evaluator
is defined inside a `let ... in` block.

## GADT Exhaustiveness

The compiler correctly filters impossible constructors based on type
information. Given a value of type `int Expr`, only `IntLit` and `Add`
are possible -- `BoolLit` cannot produce an `int Expr`:

```
$ cat filter.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr
let eval_int e =
    (match e with
    | IntLit n -> n
    : int)
let result = eval_int (IntLit 7)

$ langthree filter.l3
7
```

No warning about missing `BoolLit` -- the compiler knows it is impossible
for an `int Expr` value.

## Multiple Type Parameters in Action

GADTs can encode richer type relationships:

```
$ cat typed.l3
type Val 'a = VInt : int -> int Val | VBool : bool -> bool Val | VStr : string -> string Val
let show_int v =
    (match v with
    | VInt n -> to_string n
    : string)
let result = show_int (VInt 99)

$ langthree typed.l3
"99"
```

## Practical Example: Type-Safe Evaluator

Combining everything -- a small expression language with integer arithmetic:

```
$ cat typed_eval.l3
type Expr 'a = IntLit : int -> int Expr | BoolLit : bool -> bool Expr | Add : int Expr * int Expr -> int Expr | Neg : int Expr -> int Expr
let result =
    let rec eval e = (match e with | IntLit n -> n | BoolLit b -> if b then 1 else 0 | Add (a, b) -> eval a + eval b | Neg x -> 0 - eval x : int) in
    eval (Add (IntLit 10, Neg (IntLit 3)))

$ langthree typed_eval.l3
7
```

The expression `Add(IntLit 10, Neg(IntLit 3))` evaluates to `10 + (-3) = 7`.
Each constructor constrains its return type, so the compiler knows all branches
produce an `int`.

## Summary of GADT Syntax

| Feature | Syntax |
|---------|--------|
| Type declaration | `type Expr 'a = ...` |
| Constructor | `IntLit : int -> int Expr` |
| Return type | `int Expr` (param before name) |
| Match annotation | `(match e with \| ... : int)` |
| If any ctor is GADT | All ctors treated as GADT |
