# Let-In Expression Design

## Overview

FunLang supports optional `in` keyword for `let` bindings. The `in` keyword can be omitted when followed by a newline:

| Form | Syntax | Context |
|------|--------|---------|
| With `in` | `let x = expr in body` | Same line or `in` on same line |
| Without `in` | `let x = expr` (newline) `body` | Multi-line (top-level or block) |

**Note:** The `in` keyword must appear on the same line as the `let` expression if used. Placing `in` on a separate line is not supported.

## Syntax Examples

### One-line Let-In (requires `in`)

```funlang
let x = 10 in x + 1
// Result: 11

let square = fun n -> n * n in square 5
// Result: 25

let a = 1 in let b = 2 in a + b
// Result: 3
```

### Multi-line Let (no `in` required)

```funlang
// Top-level bindings (no indentation needed)
let x = 1
let y = 2
x + y
// Result: 3

// Indentation-based block
let result =
    let x = 10
    let y = 20
    x + y
// result = 30

// Function with multiple bindings
let compute n =
    let doubled = n * 2
    let squared = doubled * doubled
    squared + 1

// Nested blocks
let outer =
    let inner1 = 1
    let inner2 =
        let deep = 2
        deep + 1
    inner1 + inner2
// outer = 4
```

## Design Rationale

### Why Two Forms?

1. **One-line form**: Concise for simple expressions
   ```funlang
   let x = 1 in x + 1  // Clean, single-line
   ```

2. **Multi-line form**: Readable for complex code
   ```funlang
   let compute input =
       let validated = validate input
       let processed = process validated
       format processed
   ```

### How the Parser Distinguishes

The parser uses **lookahead** to determine which form is being used:

| After `let x = expr` | Interpretation |
|---------------------|----------------|
| `in` token | One-line let-in expression |
| `NEWLINE` or `DEDENT` | Block-local binding |

This works because `in` is never a valid start of a new statement in a block.

## Grammar Rules

### One-line Let-In (expr rule)

```
expr:
    | LET IDENT EQ expr IN nl_opt expr  { ELet($2, $4, $7) }
    | LET REC IDENT EQ expr IN nl_opt expr { ELetRec($3, $5, $8) }
```

Note: `IN` must appear on the same line as the preceding expression. A newline is only allowed **after** `IN`.

### Multi-line Let (top_level_item / block_item rule)

```
top_level_item:
    | LET IDENT EQ expr                 { BILet($2, $4) }
    | LET REC IDENT EQ expr             { BILetRec($3, $5) }
    | expr                              { BIExpr($1) }

top_level_body:
    | top_level_item                    { [$1] }
    | top_level_item NEWLINE top_level_body { $1 :: $3 }
```

Top-level and block contexts both use the same `block_item` structure, allowing optional `in` everywhere.

## AST Transformation

Both forms produce the same AST (`ELet` or `ELetRec`). The parser transforms block items into nested let expressions:

**Source:**
```funlang
let result =
    let x = 10
    let y = 20
    x + y
```

**Parsed Block Items:**
```
[BILet("x", 10); BILet("y", 20); BIExpr(x + y)]
```

**Transformed AST:**
```
ELet("result",
  ELet("x", 10,
    ELet("y", 20,
      EBinaryOp(Add, x, y))))
```

## Scope Rules

### One-line Form

The binding is scoped to the `in` body only:

```funlang
let x = 1 in x + 1
// x is only visible in "x + 1"
```

### Multi-line Form

The binding is scoped to all subsequent items in the same block:

```funlang
let outer =
    let x = 1       // x visible from here...
    let y = x + 1   // ...to here (y can use x)
    x + y           // ...and here
// x and y are NOT visible outside the block
```

## Comparison with Other Languages

| Language | One-line | Multi-line |
|----------|----------|------------|
| **FunLang** | `let x = 1 in x + 1` | Indentation-based |
| **F#** | `let x = 1 in x + 1` | Indentation-based (same) |
| **OCaml** | `let x = 1 in x + 1` | `in` always required |
| **Haskell** | `let x = 1 in x + 1` | Indentation via `do`/`where` |
| **Scala** | `val x = 1; x + 1` | Block-based `{ }` |

FunLang follows F#'s approach, where indentation makes `in` optional in multi-line contexts.

## Edge Cases

### Mixed Style in Same Block

```funlang
let result =
    let x = 10                     // block-local (no in)
    let y = let z = 5 in z * 2     // nested one-line let-in
    x + y
// result = 20
```

### Side Effects in Blocks

Expressions without bindings are executed for side effects:

```funlang
let main =
    print "Starting"    // side effect
    let x = compute()
    print "Done"        // side effect
    x
```

This is transformed to:
```
ELet("_", print "Starting",
  ELet("x", compute(),
    ELet("_", print "Done",
      x)))
```

## Implementation Notes

### Parser State

The parser maintains an **indent stack** to track block boundaries:
- `INDENT` token: Enter new block scope
- `DEDENT` token: Exit current block scope
- `NEWLINE` token: Separate block items at same level

### No Ambiguity

There is no ambiguity because:
1. `in` is a reserved keyword
2. `in` cannot start an expression
3. Follow set of `block_item` is `{NEWLINE, DEDENT}`, which excludes `IN`

### Error Recovery

If `in` is missing in a one-line context where it's required:
```funlang
let x = 1 x + 1  // Error: expected 'in'
```

The parser will report: `Parse error: expected 'in' after let binding`

## Best Practices

1. **Use one-line form** for simple, short bindings:
   ```funlang
   let double x = x * 2 in double 5
   ```

2. **Use multi-line form** for:
   - Multiple sequential bindings
   - Complex expressions
   - Better readability

3. **Consistent indentation**: Use spaces (not tabs) for reliable parsing

4. **Avoid mixing styles** within the same logical unit for clarity

## Related

- [Indentation-Based Syntax](./indentation-syntax.md)
- [Block Expressions](./block-expressions.md)
- [Pattern Matching](./pattern-matching.md)
