# Phase 88: Tagged Literals and Arithmetic - Research

**Researched:** 2026-04-07
**Domain:** LLVM IR code generation, tagged value representation (OCaml-style 2n+1 encoding)
**Confidence:** HIGH

## Summary

This phase transforms the FunLang compiler to encode all integer, boolean, char, and unit literals as tagged values (`2n+1`, LSB=1) so that at runtime a simple `val & 1` check distinguishes immediates from heap pointers (LSB=0). Arithmetic operations must be adjusted: addition/subtraction need a +/-1 correction, multiplication/division/remainder need untag-compute-retag sequences, comparisons work unchanged, and unary negation becomes `2 - a`.

The compiler currently emits `ArithConstantOp` with raw values and uses `ArithAddIOp`/`ArithSubIOp`/`ArithMulIOp`/`ArithDivSIOp`/`ArithRemSIOp` directly. The changes are surgical: modify ~3 literal emission sites, ~6 arithmetic operation sites, and add 2 new IR ops (`ArithShRSIOp`, `ArithShLIOp`) for shift operations needed by mul/div/rem tagging.

**Primary recommendation:** Implement tagging at the elaboration level (Elaboration.fs), NOT as a separate pass. Tag literal values at the `ArithConstantOp` emission sites and wrap arithmetic ops with correction/untag/retag sequences inline. Add `ArithShRSIOp` and `ArithShLIOp` to MlirIR.fs and Printer.fs.

## Standard Stack

### Core (existing, no new dependencies)

| Component | Location | Purpose | Role in Phase 88 |
|-----------|----------|---------|-------------------|
| MlirIR.fs | Compiler/ | IR type definitions (MlirOp DU) | Add ArithShRSIOp, ArithShLIOp cases |
| Elaboration.fs | Compiler/ | AST -> MLIR translation | Modify literal emission + arithmetic ops |
| Printer.fs | Compiler/ | MLIR -> LLVM IR text output | Add print cases for new shift ops |
| ElabHelpers.fs | Compiler/ | coerceToI64, coerceToPtrArg | Minimal changes (I1->I64 zext must retag) |

### New MLIR Ops Required

| Op | MLIR Syntax | LLVM Equivalent | Purpose |
|----|-------------|-----------------|---------|
| `ArithShRSIOp` | `arith.shrsi %a, %b : i64` | `ashr i64 %a, %b` | Untag: arithmetic right shift by 1 |
| `ArithShLIOp` | `arith.shli %a, %b : i64` | `shl i64 %a, %b` | Retag: left shift by 1 |
| `ArithOrIOp` | `arith.ori %a, %b : i64` | `or i64 %a, %b` | Set LSB=1 after shift |

## Architecture Patterns

### Pattern 1: Literal Tagging (TAG-01, TAG-02)

**What:** All user-visible integer/bool/char/unit constants are encoded as `2n+1`.
**Where in code:** 3 primary sites in Elaboration.fs

```fsharp
// BEFORE (line 13):
| Number (n, _) ->
    let v = { Name = freshName env; Type = I64 }
    (v, [ArithConstantOp(v, int64 n)])

// AFTER:
| Number (n, _) ->
    let v = { Name = freshName env; Type = I64 }
    (v, [ArithConstantOp(v, int64 n * 2L + 1L)])   // tagged: 2n+1

// Char (line 16): same pattern
| Char (c, _) ->
    let v = { Name = freshName env; Type = I64 }
    (v, [ArithConstantOp(v, int64 (int c) * 2L + 1L)])

// Bool (line 508): I1 type, special handling
| Bool (b, _) ->
    let v = { Name = freshName env; Type = I1 }
    let n = if b then 1L else 0L   // I1 stays 0/1 for branch conditions
    (v, [ArithConstantOp(v, n)])
    // NOTE: Bool as I1 is only used for cf.cond_br. When coerced to I64
    // (via ArithExtuIOp), it becomes 0 or 1 — needs retagging to 1 or 3.
```

### Pattern 2: Add/Sub Correction (ARITH-01)

**What:** `a + b` where both are tagged: `(2a+1) + (2b+1) = 2(a+b) + 2`, need to subtract 1 to get `2(a+b)+1`.
**Where:** Lines 17-26 in Elaboration.fs

```fsharp
// BEFORE:
| Add (lhs, rhs, _) ->
    let (lv, lops) = elaborateExpr env lhs
    let (rv, rops) = elaborateExpr env rhs
    let result = { Name = freshName env; Type = I64 }
    (result, lops @ rops @ [ArithAddIOp(result, lv, rv)])

// AFTER:
| Add (lhs, rhs, _) ->
    let (lv, lops) = elaborateExpr env lhs
    let (rv, rops) = elaborateExpr env rhs
    let raw = { Name = freshName env; Type = I64 }
    let one = { Name = freshName env; Type = I64 }
    let result = { Name = freshName env; Type = I64 }
    (result, lops @ rops @ [
        ArithAddIOp(raw, lv, rv)
        ArithConstantOp(one, 1L)        // raw 1, NOT tagged
        ArithSubIOp(result, raw, one)    // (2a+1)+(2b+1)-1 = 2(a+b)+1
    ])

// Subtract: (2a+1)-(2b+1) = 2(a-b), need to add 1
| Subtract (lhs, rhs, _) ->
    // ... raw = sub lv, rv; result = add raw, 1
```

### Pattern 3: Mul/Div/Rem Untag-Compute-Retag (ARITH-02)

**What:** Must extract raw values, compute, and re-encode.
**Where:** Lines 27-41 in Elaboration.fs

```fsharp
// Multiply: untag both, mul, retag
| Multiply (lhs, rhs, _) ->
    let (lv, lops) = elaborateExpr env lhs
    let (rv, rops) = elaborateExpr env rhs
    let one   = { Name = freshName env; Type = I64 }
    let la    = { Name = freshName env; Type = I64 }  // untagged a
    let rb    = { Name = freshName env; Type = I64 }  // untagged b
    let raw   = { Name = freshName env; Type = I64 }  // a * b
    let shifted = { Name = freshName env; Type = I64 } // (a*b) << 1
    let result = { Name = freshName env; Type = I64 }  // (a*b)<<1 | 1
    (result, lops @ rops @ [
        ArithConstantOp(one, 1L)
        ArithShRSIOp(la, lv, one)    // untag a: (2a+1) >> 1 = a
        ArithShRSIOp(rb, rv, one)    // untag b: (2b+1) >> 1 = b
        ArithMulIOp(raw, la, rb)     // a * b
        ArithShLIOp(shifted, raw, one) // (a*b) << 1
        ArithOrIOp(result, shifted, one) // (a*b)<<1 | 1 = tagged(a*b)
    ])

// Divide and Remainder: same untag-compute-retag pattern
// using ArithDivSIOp / ArithRemSIOp in place of ArithMulIOp
```

### Pattern 4: Unary Negation (ARITH-04)

**What:** `-a` becomes `2 - a` instead of `0 - a`.
**Why:** `-(2a+1) = -2a-1`, but we want `2(-a)+1 = -2a+1`. So: `2 - (2a+1) = -2a+1`.
**Where:** Line 42-46 in Elaboration.fs

```fsharp
// BEFORE:
| Negate (inner, _) ->
    let (iv, iops) = elaborateExpr env inner
    let zero = { Name = freshName env; Type = I64 }
    let result = { Name = freshName env; Type = I64 }
    (result, iops @ [ArithConstantOp(zero, 0L); ArithSubIOp(result, zero, iv)])

// AFTER:
| Negate (inner, _) ->
    let (iv, iops) = elaborateExpr env inner
    let two = { Name = freshName env; Type = I64 }
    let result = { Name = freshName env; Type = I64 }
    (result, iops @ [ArithConstantOp(two, 2L); ArithSubIOp(result, two, iv)])
```

### Pattern 5: Comparisons Need No Change (ARITH-03)

**What:** `2a+1 < 2b+1` iff `a < b`. Signed comparison is preserved.
**Where:** Lines 578-605 (LessThan, GreaterThan, LessEqual, GreaterEqual)
**Action:** Verify no changes needed. The comparison operates on tagged values directly.

**Also applies to:** Equal/NotEqual for integer operands (line 543, 577).

### Anti-Patterns to Avoid

- **Tagging internal constants:** Constants used for GC_malloc sizes (8L, 16L, 272L), GEP indices, and I32 values for strcmp comparison MUST NOT be tagged. Only user-visible `Number`, `Char`, and unit-value constants get tagged.
- **Tagging ADT tag values:** `ArithConstantOp(tagConst, int64 info.Tag)` at lines 2568, 2885, 2909, 3311 are ADT discriminant tags stored in heap blocks. These are NOT user arithmetic values and MUST NOT be tagged.
- **Tagging I1 boolean constants:** `ArithConstantOp(cond, 1L)` where `cond.Type = I1` (lines 2556, 2575, 3300, 3317) are branch conditions, not user values. MUST NOT be tagged.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Shift operations | Inline LLVM IR text | `arith.shrsi` / `arith.shli` / `arith.ori` MLIR ops | MLIR dialect has standard shift ops; adding DU cases keeps the IR properly typed |
| Tag/untag helper | Copy-paste shift sequences everywhere | Helper functions `emitUntag`/`emitRetag` in ElabHelpers.fs | Reduces duplication across mul/div/rem, centralizes the pattern |

## Common Pitfalls

### Pitfall 1: @main Return Value (CRITICAL)

**What goes wrong:** After tagging, unit value becomes `1` (tagged 0 = 2*0+1). Programs ending with `print_int x` return unit, so the process exit code becomes `1` instead of `0`, failing ALL tests.
**Why it happens:** `@main` returns `resultVal` directly as the process exit code. The OS interprets any non-zero exit code as failure.
**How to avoid:** In ElabProgram.fs, after computing `resultVal`, untag it before returning from `@main`: `result >> 1`. OR: always return 0 from main regardless (since the last expression's value is rarely meaningful as an exit code).
**Warning signs:** Every test that checks exit code will fail.

### Pitfall 2: Truthiness Checks (I64 != 0 Comparisons)

**What goes wrong:** Multiple places check if an I64 value is "truthy" by comparing to 0: `ArithCmpIOp(boolVal, "ne", condVal, zeroVal)` where `zeroVal = ArithConstantOp(_, 0L)`. After tagging, tagged `false` = 1, not 0. So `ne 1, 0` = true, meaning `false` is truthy.
**Why it happens:** Lines 616, 692, 704, 741, 753, 3490, 3507 all use this pattern for If/And/Or conditions when `condVal.Type = I64`.
**How to avoid:** Change truthiness checks to compare against tagged false (1L) instead of raw 0L. Or better: compare against `1L` (tagged 0/false) with "ne" predicate: `condVal != 1` means truthy.
**Warning signs:** `if false then ...` takes the true branch.

### Pitfall 3: I1-to-I64 Coercion (ArithExtuIOp)

**What goes wrong:** `ArithExtuIOp` zero-extends I1 to I64: false->0, true->1. But tagged representation needs false->1, true->3.
**Why it happens:** `coerceToI64` in ElabHelpers.fs (line 306-311) uses `ArithExtuIOp` which produces raw 0/1, not tagged values.
**How to avoid:** After `ArithExtuIOp`, apply `result * 2 + 1` or `(result << 1) | 1`. Alternatively, replace the zext with: `shl result, 1; or result, 1` to produce 1 or 3.
**Warning signs:** Boolean values passed through closures or returned from functions lose their tagged encoding.

### Pitfall 4: Constants That Must NOT Be Tagged

**What goes wrong:** Tagging ALL `ArithConstantOp` calls breaks internal operations.
**Why it happens:** The codebase uses `ArithConstantOp` for both user values AND internal constants.
**How to avoid:** Use a clear classification. Only tag constants in these specific AST cases:
- `Number(n, _)` -> user int literal
- `Char(c, _)` -> user char literal  
- Unit value `0L` in assignment results, void-call results, for-loop units, failwith results
- Range default step `1L` (line 2393) -- this IS a user value passed to `lang_range`

Do NOT tag:
- GC_malloc sizes: 8L, 16L, 272L (lines 71, 2369, 2882, 2906, 3103)
- ADT tag constants: `int64 info.Tag` (lines 2568, 2885, 2909, 3311)
- I1 boolean constants: where `result.Type = I1`
- I32 zero constants: for strcmp result comparison (lines 537, 571, 1022, 1044, 2539, 3284)
- Array slot offset `1L` (lines 1122, 1142) -- internal GEP arithmetic
- For-loop increment `1L` (line 3621) -- this IS a user-level +1, must be tagged
- Truthiness check zero constants (lines 616, 692, etc.) -- must use tagged zero (1L)

### Pitfall 5: For-Loop Counter Increment

**What goes wrong:** The for-loop increments the counter with `ArithAddIOp(nextVal, iArg, oneConst)` where `oneConst = 1L`. After tagging, `iArg` is tagged and `oneConst` must also be tagged, BUT the add correction must also apply.
**Why it happens:** The for-loop at line 3621 uses raw arithmetic.
**How to avoid:** Either (a) tag the `1L` constant and apply the add correction (`sub result, 1`), or (b) since adding tagged 1 = adding `(2*1+1)=3` then subtracting 1 = net +2, which is correct since going from `tagged(n)` to `tagged(n+1)` means `(2n+1) + 2 = 2(n+1)+1`. So: use raw constant `2L` and add directly with no correction. This is an optimization opportunity.
**Warning signs:** For-loop counters increment by wrong amount, off-by-one in loop bounds.

### Pitfall 6: C Runtime Function Arguments

**What goes wrong:** C runtime functions (`lang_range`, `lang_array_create`, `lang_array_bounds_check`, `lang_to_string_int`, etc.) receive tagged values but expect raw values.
**Why it happens:** Phase 88 tags compiler-side values, but Phase 89 modifies the C runtime.
**How to avoid:** Phase 88 scope says "compiler-side only." This means either: (a) untag at every C call boundary, or (b) do Phase 88 and 89 together. The phase description says Phase 89 handles C runtime. So Phase 88 must untag values before passing to C functions, and retag values received from C functions.
**Warning signs:** print_int prints doubled values, array indexing goes out of bounds, range creates wrong lists.

## Code Examples

### Helper Functions for ElabHelpers.fs

```fsharp
/// Emit untag sequence: (tagged_val >> 1) -> raw value
let emitUntag (env: ElabEnv) (v: MlirValue) : MlirValue * MlirOp list =
    let one = { Name = freshName env; Type = I64 }
    let result = { Name = freshName env; Type = I64 }
    (result, [ArithConstantOp(one, 1L); ArithShRSIOp(result, v, one)])

/// Emit retag sequence: (raw_val << 1) | 1 -> tagged value
let emitRetag (env: ElabEnv) (v: MlirValue) : MlirValue * MlirOp list =
    let one = { Name = freshName env; Type = I64 }
    let shifted = { Name = freshName env; Type = I64 }
    let result = { Name = freshName env; Type = I64 }
    (result, [ArithConstantOp(one, 1L); ArithShLIOp(shifted, v, one); ArithOrIOp(result, shifted, one)])

/// Tag a compile-time constant: 2n+1
let tagConst (n: int64) : int64 = n * 2L + 1L
```

### New MlirOp Cases for MlirIR.fs

```fsharp
| ArithShRSIOp    of result: MlirValue * lhs: MlirValue * rhs: MlirValue  // arith.shrsi
| ArithShLIOp     of result: MlirValue * lhs: MlirValue * rhs: MlirValue  // arith.shli
| ArithOrIOp      of result: MlirValue * lhs: MlirValue * rhs: MlirValue  // arith.ori
```

### New Printer Cases for Printer.fs

```fsharp
| ArithShRSIOp(result, lhs, rhs) ->
    sprintf "%s%s = arith.shrsi %s, %s : %s"
        indent result.Name lhs.Name rhs.Name (printType result.Type)
| ArithShLIOp(result, lhs, rhs) ->
    sprintf "%s%s = arith.shli %s, %s : %s"
        indent result.Name lhs.Name rhs.Name (printType result.Type)
| ArithOrIOp(result, lhs, rhs) ->
    sprintf "%s%s = arith.ori %s, %s : %s"
        indent result.Name lhs.Name rhs.Name (printType result.Type)
```

## Critical Design Decisions

### Decision 1: Phase 88 Scope vs C Runtime Boundary

The phase description says "compiler-side changes only" with C runtime in Phase 89. However, if we tag literals without modifying C functions, ALL runtime calls will receive wrong values. There are two options:

**Option A (Recommended): Tag + untag at C boundary in Phase 88.**
- Tag all user literals in the compiler.
- Add untag before every `LlvmCallOp`/`LlvmCallVoidOp` that passes integer args to C functions.
- Add retag after every `LlvmCallOp` that returns integer values from C functions.
- Tests keep passing. Phase 89 then moves the untag/retag INTO the C functions and removes the boundary conversions.

**Option B: Do Phase 88 + 89 together as atomic change.**
- Simpler overall, but larger scope.

### Decision 2: @main Return Value

The `@main` function returns the last expression's value as the process exit code. Options:

**Option A (Recommended): Untag the return value in @main.**
- In ElabProgram.fs, after getting `resultVal`, add `shrsi resultVal, 1` to untag before return.
- Preserves existing test behavior (exit code 0 for unit/false).

**Option B: Always return 0 from @main.**
- Ignores the expression value entirely.
- Breaks tests that check specific exit codes (if any).

### Decision 3: For-Loop Counter Optimization

Instead of `tagged_add(i, tagged(1))` = `add i, 3; sub result, 1` = net +2:
- Use `ArithConstantOp(twoConst, 2L); ArithAddIOp(nextVal, iArg, twoConst)` directly.
- Saves 1 op per iteration. The `2L` is a raw (untagged) constant used purely for counter arithmetic.
- The loop comparison `iArg sle stopVal` still works because both are tagged.

## Categorized ArithConstantOp Sites

### Must Tag (user-visible values)

| Line(s) | Pattern | Current Value | Tagged Value |
|----------|---------|---------------|--------------|
| 13 | `Number(n, _)` | `int64 n` | `int64 n * 2L + 1L` |
| 16 | `Char(c, _)` | `int64 (int c)` | `int64 (int c) * 2L + 1L` |
| 88 | Assign unit result | `0L` | `1L` (tagged 0) |
| 1098 | failwith unit result | `0L` | `1L` |
| 1126 | array_set unit result | `0L` | `1L` |
| 2008, 2020, 2042, 2067 | print/println unit | `0L` | `1L` |
| 2329 | Unit literal `()` | `0L` | `1L` |
| 2393 | Range default step | `1L` | `3L` (tagged 1) |
| 3063 | various unit results | `0L` | `1L` |
| 3587, 3652 | while/for loop unit | `0L` | `1L` |
| 3621 | for-loop increment | `1L` | See for-loop optimization |

### Must NOT Tag (internal/structural constants)

| Line(s) | Pattern | Reason |
|----------|---------|--------|
| 71, 2369, 2882, 2906, 3103 | GC_malloc sizes (8L, 16L, 272L) | Byte sizes for allocator |
| 2568, 2885, 2909, 3311 | ADT tag values (`int64 info.Tag`) | Heap block discriminants |
| 537, 571, 1022, 1044, 2539, 3284 | strcmp zero (I32) | C function return comparison |
| 2556, 2575, 3300, 3317 | I1 true constant | Branch condition, not user value |
| 1122, 1142 | Array slot offset `1L` | GEP pointer arithmetic |
| 265 | Closure env size calculation | Byte size for allocator |

### Needs Rewriting (truthiness checks)

| Line(s) | Pattern | Change |
|----------|---------|--------|
| 616, 692, 704, 741, 753, 3490, 3507 | `ne condVal, 0L` (I64 truthiness) | Change to `ne condVal, 1L` (tagged false) |

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Raw I64 for int/bool/char | Tagged 2n+1 for all immediates | Phase 88 | Enables runtime int/ptr discrimination via LSB |
| Separate I64/Ptr MlirType paths | Still keep I64/Ptr for now | Phase 88 | MlirType changes deferred to later phase |

## Open Questions

1. **C Runtime Boundary Strategy**
   - What we know: Phase 88 = compiler, Phase 89 = C runtime. But tests won't pass without boundary handling.
   - What's unclear: Does the phase expect tests to break temporarily, or must they pass?
   - Recommendation: Add untag/retag at C call boundaries in Phase 88 to keep tests green. Phase 89 removes these when C functions learn to handle tagged values.

2. **Prelude func.func Wrappers**
   - What we know: Prelude functions compiled to `func.func` receive all params as i64. Currently values passed might be I64 or Ptr (coerced to I64).
   - What's unclear: Do Prelude functions do their own arithmetic that needs tagging awareness?
   - Recommendation: Since Prelude is compiled separately by the compiler, and Phase 88 changes the compiler, Prelude .fun files will automatically get tagged compilation. This should be transparent.

3. **match Constant Patterns**
   - What we know: `ConstPat(IntConst n)` in match arms emits comparison constants.
   - What's unclear: Need to verify these get tagged too.
   - Recommendation: Search for `IntConst` and `CharConst` pattern emission in match compiler to ensure tagging.

## Sources

### Primary (HIGH confidence)
- `/Users/ohama/vibe-coding/FunLangCompiler/survey/uniform-tagged-representation.md` - Detailed design notes
- `/Users/ohama/vibe-coding/FunLangCompiler/src/FunLangCompiler.Compiler/Elaboration.fs` - All ArithConstantOp and arithmetic op sites
- `/Users/ohama/vibe-coding/FunLangCompiler/src/FunLangCompiler.Compiler/MlirIR.fs` - IR type definitions
- `/Users/ohama/vibe-coding/FunLangCompiler/src/FunLangCompiler.Compiler/ElabHelpers.fs` - Coerce helpers
- `/Users/ohama/vibe-coding/FunLangCompiler/src/FunLangCompiler.Compiler/ElabProgram.fs` - @main function construction
- `/Users/ohama/vibe-coding/FunLangCompiler/src/FunLangCompiler.Compiler/Printer.fs` - LLVM IR emission

### Secondary (MEDIUM confidence)
- [MLIR arith dialect docs](https://mlir.llvm.org/docs/Dialects/ArithOps/) - `arith.shrsi`, `arith.shli`, `arith.ori` op specifications

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Read all source files directly, exact line numbers verified
- Architecture: HIGH - Patterns derived from actual code structure and survey doc
- Pitfalls: HIGH - Each pitfall traced to specific code locations with concrete examples
- C boundary question: MEDIUM - Phase scope ambiguity needs clarification

**Research date:** 2026-04-07
**Valid until:** 2026-05-07 (stable compiler codebase, no external dependency changes)
