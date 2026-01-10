# FunLang Indentation Algorithm

## Overview

FunLang uses Python/Haskell-style significant indentation for block syntax. This document describes the complete design and implementation of the indentation system.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Approach | Post-lexer token processing | Keeps lexer simple, separates concerns |
| Block style | Optional (hybrid) | Supports both `let x = 1 in x + 1` and indented blocks |
| Parentheses | Ignore indentation inside | Python-style free formatting in parens |
| Tabs | Allowed (counted as 1 char) | Practical, but spaces recommended |
| Minimum indent | None | Only consistency matters |

## Target Syntax

```funlang
// Traditional style (always works)
let x = 1 in x + 1

// Indented block style
let result =
    let x = 10
    let y = 20
    x + y

// Mixed - traditional inside blocks
let result =
    let inner = let a = 1 in a + 1
    inner * 2

// Multi-line function
let factorial n =
    if n = 0 then
        1
    else
        n * factorial (n - 1)
```

## Architecture

```
Source Code
    ↓
[Lexer (FsLex)] → Raw tokens with NEWLINE
    ↓
[Indentation Processor] → Tokens with INDENT/DEDENT inserted
    ↓
[Parser (FsYacc)] → AST with blocks transformed to nested lets
    ↓
[Interpreter] → Value
```

---

## Phase 1: Indentation Processing

### Token Stream Transformation

The indentation processor takes `(token * Position) list` and produces `token list` with INDENT, DEDENT, and NEWLINE tokens inserted.

### State Machine

```fsharp
type IndentState = {
    IndentStack: int list      // Stack of column levels (initially empty)
    ParenDepth: int            // Nesting depth of () and []
    AtLineStart: bool          // Next non-whitespace token starts a line
    CurrentLine: int           // For error messages
}
```

### Algorithm

```
For each token with position (tok, pos):

1. Handle NEWLINE token:
   - If ParenDepth > 0: skip (ignore newlines inside parens)
   - Else: set AtLineStart = true, don't emit NEWLINE yet

2. Handle other tokens:
   a. Update ParenDepth for ( ) [ ]

   b. If inside parens (ParenDepth > 0 before this token):
      - Just emit the token, no indentation processing

   c. If at line start (AtLineStart = true):
      Let col = pos.Column

      Case: IndentStack is empty
        - First token of file, set IndentStack = [col]
        - Emit token

      Case: col > stack.top
        - Increased indentation
        - Push col onto stack
        - Emit INDENT, then token

      Case: col = stack.top
        - Same level, statement separator
        - Emit NEWLINE (if output is non-empty), then token

      Case: col < stack.top
        - Decreased indentation
        - Pop stack until col matches some level
        - If col doesn't match any level → IndentationError
        - Emit DEDENT for each popped level, then token

   d. If not at line start:
      - Just emit the token

3. At EOF:
   - Emit DEDENT for each level above base
   - Emit EOF
```

### Example Trace

Input:
```
let x =
    10
    20
x
```

```
Token          Pos     Stack    Action              Output
─────────────────────────────────────────────────────────────
LET           (1,1)    []       First token         [LET], stack=[1]
IDENT "x"     (1,5)    [1]      Not line start      [IDENT "x"]
EQ            (1,7)    [1]      Not line start      [EQ]
NEWLINE       (1,8)    [1]      Set AtLineStart     (nothing)
INT 10        (2,5)    [1]      col=5 > top=1       [INDENT, INT 10], stack=[5,1]
NEWLINE       (2,7)    [5,1]    Set AtLineStart     (nothing)
INT 20        (3,5)    [5,1]    col=5 = top=5       [NEWLINE, INT 20]
NEWLINE       (3,7)    [5,1]    Set AtLineStart     (nothing)
IDENT "x"     (4,1)    [5,1]    col=1 < top=5       [DEDENT, IDENT "x"], stack=[1]
EOF           (5,1)    [1]      Close remaining     [EOF]
─────────────────────────────────────────────────────────────
Final output: LET IDENT EQ INDENT INT NEWLINE INT DEDENT IDENT EOF
```

### Parentheses Handling

Inside parentheses or brackets, indentation is ignored:

```funlang
let list = [
    1,
        2,    // indentation doesn't matter here
    3
]
```

The ParenDepth counter tracks nesting:
- `(` or `[` → increment
- `)` or `]` → decrement
- While > 0, skip all indentation processing

---

## Phase 2: Block Grammar

### The Grammar Conflict Problem

Naive approach has a conflict:
```
expr: LET IDENT EQ expr IN expr   // traditional let
block_item: LET IDENT EQ expr     // block-local binding (no IN)
```

Both start with `LET IDENT EQ expr`. How does the parser know which rule to use?

### Resolution: Follow Set Analysis

After parsing `LET IDENT EQ expr`, the parser sees:
- **IN** → Continue with `expr IN expr` rule (shift)
- **NEWLINE or DEDENT** → Reduce as `block_item` (no IN expected)

Key insight: **IN ∉ Follow(block_item)**

```
Follow(block_item) = { NEWLINE, DEDENT }  // from block_body rules
```

Therefore:
- Lookahead **IN** → only `expr` rule applies, shift
- Lookahead **NEWLINE/DEDENT** → only `block_item` rule applies, reduce

**No shift-reduce conflict!**

### Block Item Discrimination Union

```fsharp
// Defined in parser header
type BlockItem =
    | BILet of string * Expr       // let x = e (block-local)
    | BILetRec of string * Expr    // let rec f = e (block-local)
    | BIExpr of Expr               // standalone expression
```

### Grammar Rules

```yacc
block:
    | INDENT block_body DEDENT      { blockToExpr $2 }
;

block_item:
    | LET IDENT EQ expr             { BILet($2, $4) }
    | LET REC IDENT EQ expr         { BILetRec($3, $5) }
    | expr                          { BIExpr $1 }
;

block_body:
    | block_item                    { [$1] }
    | block_item NEWLINE block_body { $1 :: $3 }
;
```

---

## Phase 3: Block Transformation

### Transformation Algorithm

Convert `BlockItem list` to a single `Expr` using nested lets:

```fsharp
let rec blockToExpr (items: BlockItem list) : Expr =
    match items with
    | [] ->
        ELiteral LUnit  // empty block returns unit

    | [BIExpr e] ->
        e  // single expression, just return it

    | [BILet(name, value)] ->
        // binding at end with no body - use unit
        ELet(name, value, ELiteral LUnit)

    | [BILetRec(name, value)] ->
        ELetRec(name, value, ELiteral LUnit)

    | BIExpr e :: rest ->
        // expression for side effects, then continue
        // Use wildcard binding to sequence
        ELet("_", e, blockToExpr rest)

    | BILet(name, value) :: rest ->
        ELet(name, value, blockToExpr rest)

    | BILetRec(name, value) :: rest ->
        ELetRec(name, value, blockToExpr rest)
```

### Transformation Examples

**Example 1: Simple bindings**
```funlang
let x = 10
let y = 20
x + y
```
Parses to: `[BILet("x", 10); BILet("y", 20); BIExpr(x+y)]`
Transforms to: `ELet("x", 10, ELet("y", 20, x+y))`

**Example 2: Side effects**
```funlang
print "hello"
print "world"
42
```
Parses to: `[BIExpr(print "hello"); BIExpr(print "world"); BIExpr(42)]`
Transforms to: `ELet("_", print "hello", ELet("_", print "world", 42))`

**Example 3: Mixed traditional and block**
```funlang
let x = let a = 1 in a + 1
x * 2
```
Parses to: `[BILet("x", ELet("a", 1, a+1)); BIExpr(x*2)]`
Transforms to: `ELet("x", ELet("a", 1, a+1), x*2)`

---

## Invariants and Properties

### Invariant 1: INDENT/DEDENT Balance
```
count(INDENT) = count(DEDENT)
```
The indentation processor always generates matching pairs.

### Invariant 2: Stack Consistency
```
∀ token at column c after DEDENT: c ∈ IndentStack
```
Dedenting always returns to a previously established indentation level.

### Invariant 3: Block Transformation Preserves Scoping
```
let x = 10
let y = x + 1   // x is in scope
y + x           // both x and y are in scope
```
Transformed to: `ELet("x", 10, ELet("y", x+1, y+x))` - scoping is correct.

### Property: Determinism
Same input always produces same output. No mutable state between calls.

### Property: Backward Compatibility
All valid single-line expressions remain valid:
```
let x = 1 in x + 1  // works as before
```

---

## Error Cases

### IndentationError: Inconsistent Dedent
```funlang
let x =
    10
  y      // column 3 doesn't match any level in [1, 5]
```
Error: "Indentation error: expected column 1 or 5, got 3"

### Empty Block
```funlang
let x =
    // nothing here
y
```
Produces `ELiteral LUnit` for the empty block.

---

## Testing Strategy

### Property-Based Tests (FsCheck)
```fsharp
testProperty "INDENT count equals DEDENT count" <| fun tokens ->
    let result = processIndentation tokens
    countIndents result = countDedents result

testProperty "deterministic processing" <| fun tokens ->
    processIndentation tokens = processIndentation tokens

testProperty "parentheses disable indentation" <| fun tokens ->
    // tokens inside parens should not trigger INDENT/DEDENT
```

### Unit Tests
```fsharp
test "INDENT on increased indentation"
test "DEDENT on decreased indentation"
test "multiple DEDENTs for multi-level decrease"
test "NEWLINE for same-level statements"
test "no INDENT/DEDENT inside parentheses"
test "error on inconsistent dedent"
```

### Integration Tests
```fsharp
test "parse and evaluate indented let block"
test "parse and evaluate nested blocks"
test "backward compatibility with traditional let...in"
```

---

## Implementation Checklist

- [x] Define INDENT, DEDENT, NEWLINE tokens in Parser.fsy
- [x] Lexer emits NEWLINE token
- [x] Indentation.fs processIndentation function
- [x] Position tracking in ParserWrapper.fs
- [x] BlockItem type in Parser.fsy header
- [x] blockToExpr transformation function
- [x] block, block_item, block_body grammar rules
- [x] Integration with expr rule
- [x] Tests for block parsing (155 tests passing)
- [x] Tests for end-to-end evaluation

## Verified Examples

```bash
# Simple block with bindings
$ funlang -e 'let x =
    let y = 10
    let z = 20
    y + z
in x'
# Output: 30

# Recursive function with indentation
$ funlang -e 'let rec factorial = fun n ->
    if n = 0 then
        1
    else
        n * factorial (n - 1)
in factorial 5'
# Output: 120

# Nested blocks in if-then-else
$ funlang -e 'let x =
    if true then
        let a = 1
        let b = 2
        a + b
    else
        0
in x'
# Output: 3

# Mixed traditional and block syntax
$ funlang -e 'let x =
    let inner = let a = 1 in a + 1
    inner * 2
in x'
# Output: 4

# Backward compatibility
$ funlang -e 'let x = 1 in let y = 2 in x + y'
# Output: 3
```
