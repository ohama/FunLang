# Phase 29: Char Type & Comparisons - Research

**Researched:** 2026-03-24
**Domain:** F# interpreter internals — lexer, parser, AST, type system, evaluator
**Confidence:** HIGH

## Summary

Phase 29 adds the `char` type to LangThree. Currently, `char` does not exist: no `TChar` in `Type.fs`, no `CharValue` in `Ast.fs`, no char literal lexing, and comparisons only work on `int`. The workaround in user code is 26-equality-chain comparisons (e.g., `c = "A" || c = "B" || ...`) using single-character strings. This phase eliminates that pattern.

Three independent requirements map to distinct implementation areas:

- **TYPE-04** (char literal): Add `'A'` syntax — lexer rule, `CHAR` token, `Ast.Char` expr variant, `CharValue` runtime value, `TChar` type, type inference for literals, char pattern matching
- **TYPE-05** (char conversion builtins): `char_to_int : char -> int` and `int_to_char : int -> char` — two entries in `Eval.initialBuiltinEnv` and `TypeCheck.initialTypeEnv`
- **TYPE-06** (ordered comparisons for string and char): `<`, `>`, `<=`, `>=` extended to work on `string` and `char` in `Bidir.fs` (type inference) and `Eval.fs` (runtime evaluation)

The approach is additive — every existing test continues to work because `int` comparison is unchanged. String comparisons are already semantically correct in F# (`System.String.Compare` lexicographic), only the type-checker restriction needs to be lifted.

**Primary recommendation:** Implement in order TYPE-04 → TYPE-05 → TYPE-06, since TYPE-05 depends on `CharValue` and `TChar` from TYPE-04, and TYPE-06 is fully independent.

---

## Standard Stack

No new libraries needed. All implementation uses existing infrastructure:

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| F# / .NET 10 | existing | Implementation language | Already the entire stack |
| FsLexYacc | existing | Lexer (`.fsl`) and parser (`.fsy`) | Already used throughout |
| `System.Char` | .NET BCL | Char↔int conversion (`int c`, `char n`) | Built into .NET |

### No New Dependencies
All changes are within existing source files. No new packages.

**Build:** `dotnet build src/LangThree/LangThree.fsproj` (unchanged)

---

## Architecture Patterns

### LangThree Extension Pattern
Every new type follows the same multi-file pattern. For example, `string` was added across:
1. `Lexer.fsl` — lex the literal
2. `Parser.fsy` — token declaration + grammar rule
3. `Ast.fs` — `String of string * Span` expr variant + `StringConst` pattern constant + `StringValue` runtime value
4. `Type.fs` — `TString` type variant
5. `Elaborate.fs` — `TEString` → `TString`
6. `Bidir.fs` — `String(_, _) -> (empty, TString)` synthesis case
7. `Eval.fs` — `String(s, _) -> StringValue s` eval case + `StringValue` in `formatValue` + `StringValue` comparison in `valuesEqual`
8. `TypeCheck.fs` — no change needed (no built-in functions tied to string literal parsing)
9. `Infer.fs` — `ConstPat(StringConst _, _) -> (Map.empty, TString)` pattern case
10. `Format.fs` — `StringConst s` in `formatPattern`

`char` follows the same pattern.

### Recommended File Modification Order

```
src/LangThree/
├── Ast.fs          # 1. Add TChar? No — Type.fs. Add CharValue, Char expr, CharConst
├── Type.fs         # 2. Add TChar
├── Lexer.fsl       # 3. Add char literal lexing
├── Parser.fsy      # 4. Add CHAR token + grammar rule
├── Elaborate.fs    # 5. Add TEChar -> TChar (TypeExpr needs TEChar too)
├── Bidir.fs        # 6. TYPE-04 synthesis + TYPE-06 comparisons
├── Eval.fs         # 7. TYPE-04 eval + TYPE-05 builtins + TYPE-06 comparisons
├── TypeCheck.fs    # 8. TYPE-05 type schemes
├── Infer.fs        # 9. CharConst pattern inference
└── Format.fs       # 10. CharConst display (for test output / REPL)
```

### Pattern 1: Adding a New Literal Type (TYPE-04)

**Lexer rule** — `Lexer.fsl` uses a `read_char` state machine (like `read_string`):

```fsharp
// In tokenize rule, before single-char operators:
| '\''           { read_char lexbuf }
```

```fsharp
// New rule (after read_string):
and read_char = parse
    | '\\' 'n'   { CHAR '\n' }
    | '\\' 't'   { CHAR '\t' }
    | '\\' '\\'  { CHAR '\\' }
    | '\\' '\''  { CHAR '\'' }
    | '\'' { failwith "Empty char literal" }
    | [^ '\'']   { let c = (lexeme lexbuf).[0]
                   // Consume closing quote
                   match tokenize lexbuf with  // Must consume closing quote
                   | _ -> CHAR c }
```

**IMPORTANT**: The standard approach for char literals is simpler — consume the char and expect `'`:

```fsharp
and read_char = parse
    | '\\' 'n' '\'' { CHAR '\n' }
    | '\\' 't' '\'' { CHAR '\t' }
    | '\\' '\\' '\'' { CHAR '\\' }
    | '\\' '\'' '\'' { CHAR '\'' }
    | _ '\''         { CHAR ((lexeme lexbuf).[0]) }
    | eof            { failwith "Unterminated char literal" }
```

The `_` pattern matches any single character followed by `'`. Both the char and the closing `'` are consumed in one rule. This is the correct FsLex approach.

**AST additions** — `Ast.fs`:

```fsharp
// In Expr DU:
| Char of char * span: Span    // Character literal: 'A'

// In Constant DU (for char pattern matching):
| CharConst of char

// In Value DU:
| CharValue of char  // Phase 29: Character value
```

**NOTE on `Value` CustomEquality**: The `Value` type already has `[<CustomEquality; CustomComparison>]` with explicit `valueEqual` and `valueCompare` static members. Adding `CharValue` requires updating both `valueEqual` and `valueCompare`:

```fsharp
// In valueEqual:
| CharValue a, CharValue b -> a = b

// In valueCompare:
| CharValue a, CharValue b -> compare a b
```

Also update `GetHashCode`:
```fsharp
| CharValue c -> hash c
```

**Type.fs addition**:

```fsharp
type Type =
    | TInt
    | TBool
    | TString
    | TChar    // Phase 29: Character type
    | ...
```

Update `formatType` and `formatTypeNormalized`:
```fsharp
| TChar -> "char"
```

Update `freeVars` (add `TChar` to base cases):
```fsharp
| TInt | TBool | TString | TChar | TExn -> Set.empty
```

Update `apply`:
```fsharp
| TChar -> TChar
```

**TypeExpr addition in `Ast.fs`**:

```fsharp
and TypeExpr =
    | TEInt
    | TEBool
    | TEString
    | TEChar    // Phase 29
    | ...
```

**Lexer keyword** — `Lexer.fsl`:
```fsharp
| "char"        { TYPE_CHAR }
```

**Parser token and rule** — `Parser.fsy`:
```
%token <char> CHAR
%token TYPE_CHAR
```

```
// In AtomicTypeExpr rule:
| TYPE_CHAR     { TEChar }

// In AtomicExpr rule:
| CHAR          { Char ($1, symSpan parseState 1) }

// In pattern (ConstPat):
| CHAR          { ConstPat (CharConst $1, symSpan parseState 1) }
```

**Elaborate.fs**:
```fsharp
| TEChar -> (TChar, vars)
```

Also update `substTypeExprWithMap`:
```fsharp
| Ast.TEChar -> TChar
```

And `collectTypeExprVars`:
```fsharp
| Ast.TEChar -> Set.empty
```

**Bidir.fs synthesis**:
```fsharp
| Char (_, _) -> (empty, TChar)
```

**Eval.fs**:
```fsharp
// In eval:
| Char (c, _) -> CharValue c

// In formatValue:
| CharValue c -> sprintf "'%c'" c

// In valuesEqual:
| CharValue a, CharValue b -> a = b

// In matchPattern:
| ConstPat (CharConst c1, _), CharValue c2 -> if c1 = c2 then Some [] else None
```

**Infer.fs** — `inferPattern`:
```fsharp
| ConstPat (CharConst _, _) -> (Map.empty, TChar)
```

**Format.fs** — `formatPattern`:
```fsharp
| Ast.CharConst c -> sprintf "ConstPat (CharConst '%c')" c
```

**Exhaustive.fs** — char patterns are treated like string/int patterns: `ConstPat` maps to `WildcardPat` in the simplification step. No changes needed (already: `| Ast.ConstPat _ -> WildcardPat`).

### Pattern 2: Char Conversion Builtins (TYPE-05)

Follows the exact pattern from `string_length`, `string_to_int`, etc.

**`Eval.fs`** — add to `initialBuiltinEnv`:
```fsharp
// char_to_int : char -> int
"char_to_int", BuiltinValue (fun v ->
    match v with
    | CharValue c -> IntValue (int c)
    | _ -> failwith "char_to_int: expected char argument")

// int_to_char : int -> char
"int_to_char", BuiltinValue (fun v ->
    match v with
    | IntValue n ->
        if n < 0 || n > 127 then
            failwithf "int_to_char: value %d out of ASCII range (0-127)" n
        else
            CharValue (char n)
    | _ -> failwith "int_to_char: expected int argument")
```

**`TypeCheck.fs`** — add to `initialTypeEnv`:
```fsharp
// char_to_int : char -> int
"char_to_int", Scheme([], TArrow(TChar, TInt))

// int_to_char : int -> char
"int_to_char", Scheme([], TArrow(TInt, TChar))
```

### Pattern 3: Ordered Comparisons for String and Char (TYPE-06)

Currently in `Bidir.fs` (lines 228–231):
```fsharp
// Comparison operators only work on int
| LessThan (e1, e2, _) | GreaterThan (e1, e2, _)
| LessEqual (e1, e2, _) | GreaterEqual (e1, e2, _) ->
    let s = inferBinaryOp ctorEnv recEnv ctx env e1 e2 TInt TInt
    (s, TBool)
```

The `inferBinaryOp` helper forces both operands to `TInt`. To support `string` and `char`, use the same approach as `Add` (which already handles `TInt | TString`):

```fsharp
// Comparison operators work on int, string, or char (ordered types)
| LessThan (e1, e2, span) | GreaterThan (e1, e2, span)
| LessEqual (e1, e2, span) | GreaterEqual (e1, e2, span) ->
    let s1, t1 = synth ctorEnv recEnv ctx env e1
    let s2, t2 = synth ctorEnv recEnv ctx (applyEnv s1 env) e2
    let appliedT1 = apply s2 t1
    let s3 = unifyWithContext ctx [] span appliedT1 t2
    let resultTy = apply s3 appliedT1
    match resultTy with
    | TInt | TString | TChar -> (compose s3 (compose s2 s1), TBool)
    | TVar _ ->
        // Ambiguous - default to int (backward compatible)
        let s4 = unifyWithContext ctx [] span resultTy TInt
        (compose s4 (compose s3 (compose s2 s1)), TBool)
    | _ ->
        raise (TypeException {
            Kind = UnifyMismatch (TInt, resultTy)
            Span = span
            Term = Some expr
            ContextStack = ctx
            Trace = []
        })
```

**`Eval.fs`** — extend all four comparison eval cases:

```fsharp
// Before (int only):
| LessThan (left, right, _) ->
    match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
    | IntValue l, IntValue r -> BoolValue (l < r)
    | _ -> failwith "Type error: < requires integer operands"

// After (int, string, char):
| LessThan (left, right, _) ->
    match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
    | IntValue l, IntValue r -> BoolValue (l < r)
    | StringValue l, StringValue r -> BoolValue (System.String.CompareOrdinal(l, r) < 0)
    | CharValue l, CharValue r -> BoolValue (l < r)
    | _ -> failwith "Type error: < requires operands of same ordered type (int, string, or char)"
```

Apply the same extension to `GreaterThan`, `LessEqual`, `GreaterEqual`.

**String comparison note**: Use `System.String.CompareOrdinal` for byte-by-byte (ordinal) comparison. This matches the constraint requirement: `"abc" < "def"` returns true (correct ordinal order). Alternative `System.String.Compare` uses culture-sensitive comparison which can give unexpected results. `CompareOrdinal` is the F# idiomatic choice for language implementation.

### Anti-Patterns to Avoid

- **Char literal collision with type variables**: `type_var` in `Lexer.fsl` is `'\'' letter (letter | digit | '_')*` — multi-character. Single char literal `'A'` is a single char then closing `'`. The `read_char` sub-lexer approach handles this correctly because the tokenize rule matches `'\''` as the opening quote, then dispatches to `read_char` to consume the char body and closing `'`. No collision.

- **Forgetting to update `Value.GetHashCode` and `IComparable`**: The `Value` type has `[<CustomEquality; CustomComparison>]` with explicit implementations. Adding `CharValue` without updating all three (`valueEqual`, `valueCompare`, `GetHashCode`) will produce compiler warnings and incorrect behavior.

- **Forgetting `apply s TChar` in substitution**: `Type.apply` must handle `TChar -> TChar`, otherwise substitution crashes. Since `TChar` has no type variables inside it, it's a simple base case identical to `TInt`, `TBool`, `TString`.

- **`inferBinaryOp` only accepts a single expected type**: The existing `inferBinaryOp` helper in `Bidir.fs` unifies both operands with one fixed type. For comparison operators that accept multiple types, don't use `inferBinaryOp` — use the `synth`-then-unify pattern shown in Pattern 3 above.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Char↔int conversion | Manual lookup table | `int c` / `char n` (.NET BCL) | .NET `char` is Unicode (UTF-16); `int c` gives the Unicode code point; `char n` converts back |
| String ordering | Character-by-character loop | `System.String.CompareOrdinal` | Handles multi-byte edge cases, ordinal semantics matches expectation |
| Char literal parsing | Manual state machine in tokenize rule | `read_char` sub-lexer rule (FsLex convention) | FsLex supports sub-rules via `and`; same pattern as `read_string` already in the codebase |
| Exhaustiveness of char patterns | Enumerate all possible chars | Map `CharConst` to `WildcardPat` in Exhaustive.fs (already done for all ConstPat) | Char has 128+ values — exhaustiveness is undecidable for finite patterns; treat same as int/string |

**Key insight**: The codebase already has all needed infrastructure. This phase is entirely additive — no existing logic changes except widening two type-inference restrictions.

---

## Common Pitfalls

### Pitfall 1: Type variable `'a` lexer collision with char literal `'a'`
**What goes wrong:** `Lexer.fsl` has rule `type_var = '\'' letter (letter | digit | '_')*` which matches `'a`, `'b`, etc. A char literal `'a'` starts with `'a` — same prefix.
**Why it happens:** FsLex uses longest match + first match. `'a'` as a char literal has 3 characters total, while `'a` as a type var has 2 characters. But `type_var` matches greedily: for input `'a'`, it would match `'a` (the type variable), leaving `'` as a stray token.
**How to avoid:** The `read_char` dispatch approach handles this correctly because the `'\''` single-quote rule in `tokenize` is matched as the opening delimiter ONLY if followed by entering the `read_char` sub-rule. However, the `type_var` rule also starts with `'\''`. **The key is rule ordering in `Lexer.fsl`**: the `type_var` rule must come AFTER the `'\''` dispatch-to-read_char rule, and longest-match will prefer `type_var` for multi-character `'a` sequences.

**CRITICAL DECISION**: To avoid ambiguity, handle char literals in the tokenize rule BEFORE the `type_var` rule, using a look-ahead-aware approach. The simplest correct approach is:

```fsharp
// In tokenize, BEFORE type_var:
| '\'' [^ '\''] '\'' { CHAR ((lexeme lexbuf).[1]) }
| '\'' '\\' 'n' '\'' { CHAR '\n' }
| '\'' '\\' 't' '\'' { CHAR '\t' }
| '\'' '\\' '\\' '\'' { CHAR '\\' }
| '\'' '\\' '\'' '\'' { CHAR '\'' }
// type_var rule comes AFTER:
| type_var           { TYPE_VAR (lexeme lexbuf) }
```

With longest-match: `'A'` (3 chars) beats `'a` (2 chars for type_var). This is the cleanest approach — no sub-lexer needed.

**Warning signs:** If type-variable annotations stop working after this change, the char literal rules are matching before `type_var`. Verify by testing `let f (x: 'a) = x`.

### Pitfall 2: `int c` gives Unicode code point, not ASCII
**What goes wrong:** `.NET char` is UTF-16. `int 'A'` gives 65 (correct for ASCII). But `int '€'` gives 8364. The constraint says `char_to_int 'A'` returns 65 — this is ASCII semantics.
**Why it happens:** .NET uses Unicode internally.
**How to avoid:** For the scope of TYPE-05, only support ASCII chars (0-127). `int_to_char` should guard: `if n < 0 || n > 127 then failwith ...`. This matches the use case (lexer character classification is ASCII-based). Unicode support can be added later.
**Warning signs:** `char_to_int '€'` returns 8364, not an error. Acceptable for now — document that `int_to_char` only supports 0-127 range.

### Pitfall 3: Comparison operators — `inferBinaryOp` hardcodes `TInt`
**What goes wrong:** Using `inferBinaryOp ctorEnv recEnv ctx env e1 e2 TInt TInt` for comparisons forces both operands to `int`. Changing the argument from `TInt` to something else doesn't work because `inferBinaryOp` accepts one fixed type for both operands.
**Why it happens:** `inferBinaryOp` is a helper for homogeneous binary operators. Comparison operators need to be homogeneous (both operands same type) but allow multiple types.
**How to avoid:** Replace `inferBinaryOp` call with the synth-then-unify-then-check-type pattern (shown in Pattern 3). This mirrors the `Add` operator implementation in `Bidir.fs` lines 186–209.
**Warning signs:** `"abc" < "def"` gives a type error about `TInt` vs `TString` instead of type-checking successfully.

### Pitfall 4: Forgetting `TChar` in `Type.apply`
**What goes wrong:** If `TChar` is added to the `Type` DU without adding it to `apply`, F# produces a match-not-exhaustive warning (treated as error in strict builds). At runtime, any substitution touching `TChar` would hit the default branch and return incorrect results.
**Why it happens:** `apply` in `Type.fs` has a match on all `Type` cases. New cases must be added.
**How to avoid:** After adding `TChar` to `Type.fs`, immediately add `| TChar -> TChar` to `apply`. Also update `freeVars` (`| TInt | TBool | TString | TChar | TExn -> Set.empty`) and `formatType` / `formatTypeNormalized` (`| TChar -> "char"`).
**Warning signs:** F# compiler warning CS0050 or FS0025 (incomplete match) in Type.fs.

### Pitfall 5: `ValuesEqual` in Ast.fs — `CharValue` in match
**What goes wrong:** `Ast.Value.valueEqual` and `Ast.Value.valueCompare` have explicit match arms for each value type. Adding `CharValue` without updating these methods causes `false` for all char comparisons and `0` for all char ordering comparisons.
**Why it happens:** The `[<CustomEquality; CustomComparison>]` attribute means F# does NOT auto-generate equality/comparison — all logic is explicit.
**How to avoid:** After adding `CharValue` to the `Value` DU, immediately add the three cases: in `valueEqual`, `valueCompare`, and `GetHashCode`.

---

## Code Examples

### Char literal lexer rules (Lexer.fsl, in tokenize rule before type_var)
```fsharp
// Source: Direct analysis of Lexer.fsl + FsLex longest-match semantics
| '\'' '\\' 'n' '\''  { CHAR '\n' }
| '\'' '\\' 't' '\''  { CHAR '\t' }
| '\'' '\\' '\\' '\'' { CHAR '\\' }
| '\'' '\\' '\'' '\'' { CHAR '\'' }
| '\'' [^ '\'' '\\'] '\'' { CHAR ((lexeme lexbuf).[1]) }
```

### char conversion builtins (Eval.fs, in initialBuiltinEnv)
```fsharp
// Source: Direct analysis of existing builtin pattern (string_length, etc.)
"char_to_int", BuiltinValue (fun v ->
    match v with
    | CharValue c -> IntValue (int c)
    | _ -> failwith "char_to_int: expected char argument")

"int_to_char", BuiltinValue (fun v ->
    match v with
    | IntValue n ->
        if n < 0 || n > 127 then
            failwithf "int_to_char: value %d out of ASCII range (0-127)" n
        else
            CharValue (char n)
    | _ -> failwith "int_to_char: expected int argument")
```

### Comparison type inference widening (Bidir.fs, replacing lines 228-231)
```fsharp
// Source: Direct analysis of Add operator pattern in Bidir.fs lines 186-209
| LessThan (e1, e2, span) | GreaterThan (e1, e2, span)
| LessEqual (e1, e2, span) | GreaterEqual (e1, e2, span) ->
    let s1, t1 = synth ctorEnv recEnv ctx env e1
    let s2, t2 = synth ctorEnv recEnv ctx (applyEnv s1 env) e2
    let appliedT1 = apply s2 t1
    let s3 = unifyWithContext ctx [] span appliedT1 t2
    let resultTy = apply s3 appliedT1
    match resultTy with
    | TInt | TString | TChar -> (compose s3 (compose s2 s1), TBool)
    | TVar _ ->
        let s4 = unifyWithContext ctx [] span resultTy TInt
        (compose s4 (compose s3 (compose s2 s1)), TBool)
    | _ ->
        raise (TypeException {
            Kind = UnifyMismatch (TInt, resultTy)
            Span = span
            Term = Some expr
            ContextStack = ctx
            Trace = []
        })
```

### Comparison eval extension (Eval.fs, all four cases)
```fsharp
// Source: Direct analysis of existing LessThan/GreaterThan/LessEqual/GreaterEqual in Eval.fs
| LessThan (left, right, _) ->
    match eval recEnv moduleEnv env false left, eval recEnv moduleEnv env false right with
    | IntValue l, IntValue r -> BoolValue (l < r)
    | StringValue l, StringValue r -> BoolValue (System.String.CompareOrdinal(l, r) < 0)
    | CharValue l, CharValue r -> BoolValue (l < r)
    | _ -> failwith "Type error: < requires int, string, or char operands"
```

### TChar type infrastructure (Type.fs)
```fsharp
// Source: Direct analysis of TInt/TBool/TString pattern in Type.fs
// Add to Type DU:
| TChar

// Add to formatType:
| TChar -> "char"

// Add to freeVars:
| TInt | TBool | TString | TChar | TExn -> Set.empty

// Add to apply:
| TChar -> TChar
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| int-only comparisons | int + string + char comparisons | Phase 29 | String/char ordering works |
| no char type | `char` primitive type | Phase 29 | `char_to_int`, char literals, char patterns |
| 26-equality-chain workaround | `'A' < 'Z'` single expression | Phase 29 | Eliminates constraint 4.3 and 4.4 |

**Deprecated/outdated after Phase 29:**
- `string_sub s i 1 = "{"` pattern for char comparison — can now use `char` literals and `<`/`>`
- 26-equality-chain for `is_upper` etc. — can now use `'A' <= c && c <= 'Z'`

---

## Open Questions

1. **Unicode vs ASCII range for `int_to_char`**
   - What we know: .NET char is UTF-16; `int c` returns Unicode code point
   - What's unclear: Should `int_to_char` support full Unicode (0–65535) or just ASCII (0–127)?
   - Recommendation: ASCII-only (0–127) for Phase 29. Full Unicode can be a later phase. The constraint document's use case is lexer character classification which is ASCII.

2. **Char pattern matching exhaustiveness**
   - What we know: `Exhaustive.fs` maps `ConstPat _ -> WildcardPat` — char patterns are treated as non-exhaustive by themselves
   - What's unclear: Should `match c with | 'A' -> ... | _ -> ...` warn about non-exhaustiveness?
   - Recommendation: No change needed. Same behavior as int/string patterns — wildcard/variable makes it exhaustive.

3. **`type_var` vs `char literal` longest-match in FsLex**
   - What we know: FsLex uses longest match + first match. `'A'` (3 chars) is longer than `'a` (type var, 2 chars)
   - What's unclear: Does FsLex correctly prefer the 3-char char literal rule over the 2-char type_var rule?
   - Recommendation: Place char literal rules BEFORE `type_var` in `Lexer.fsl` to ensure first-match preference if lengths conflict. Test with both `'a'` (char) and `'a` (type var in annotation).

---

## Sources

### Primary (HIGH confidence)
- Direct read: `src/LangThree/Ast.fs` — full file (Value DU, Expr DU, Pattern/Constant DU, TypeExpr DU)
- Direct read: `src/LangThree/Type.fs` — full file (Type DU, apply, freeVars, formatType)
- Direct read: `src/LangThree/Lexer.fsl` — full file (tokenize rule, type_var, read_string sub-rule)
- Direct read: `src/LangThree/Parser.fsy` — lines 1–100 (token declarations, precedence)
- Direct read: `src/LangThree/Elaborate.fs` — full file (elaborateWithVars, substTypeExprWithMap)
- Direct read: `src/LangThree/Bidir.fs` — lines 1–100, 185–236 (synth, comparison rules)
- Direct read: `src/LangThree/Eval.fs` — lines 145–260, 400–540 (initialBuiltinEnv, eval cases, comparison eval, valuesEqual)
- Direct read: `src/LangThree/TypeCheck.fs` — lines 1–57 (initialTypeEnv)
- Direct read: `src/LangThree/Infer.fs` — lines 1–100 (inferPattern, ConstPat cases)
- Direct read: `src/LangThree/Exhaustive.fs` — ConstPat handling
- Direct read: `src/LangThree/Format.fs` — lines 200–230 (formatPattern)
- Direct read: `langthree-constraints.md` — constraints 4.3 (no char type) and 4.4 (no string comparison operators)
- Direct read: `.planning/phases/26-quick-fixes/26-RESEARCH.md` — builtin addition pattern

### Secondary (MEDIUM confidence)
- `.planning/phases/26-quick-fixes/26-01-PLAN.md` — confirms builtin registration two-step pattern (Eval.fs + TypeCheck.fs)

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new libraries, entirely within existing infrastructure
- Architecture: HIGH — all patterns traced from existing implementations (string type, string builtins)
- TYPE-04 (char literal): HIGH — exact pattern known from string literal implementation; lexer disambiguation confirmed
- TYPE-05 (char builtins): HIGH — identical pattern to `string_to_int`, `string_length`; two files, two entries
- TYPE-06 (ordered comparisons): HIGH — exact code locations identified (`Bidir.fs` lines 228–231, `Eval.fs` lines 490–508); widening pattern mirrors `Add` operator
- Pitfalls: HIGH — all derived from source analysis, not speculation

**Research date:** 2026-03-24
**Valid until:** 2026-04-24 (stable codebase, no active refactoring)
